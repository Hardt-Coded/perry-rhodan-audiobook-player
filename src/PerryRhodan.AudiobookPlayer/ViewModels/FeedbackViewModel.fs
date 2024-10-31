namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open CherylUI.Controls
open Dependencies
open Elmish.SideEffect
open ReactiveElmish.Avalonia
open ReactiveElmish
open Services
open Services.DependencyServices
open Services.Helpers
open System.Threading.Tasks

module FeedbackPage =

    type State = {
        EMail: string
        Message: string
        IsBusy: bool
    }

    type Msg =
        | RunSideEffects of SideEffect
        | ChangeEMail of string
        | ChangeMessage of string
        | SetBusy of bool
        | KeyboardStateChanged

    and [<RequireQualifiedAccess>]
        SideEffect =
        | None
        | SendFeedback
        | CloseDialog


    let init () =
        { EMail = ""; Message = ""; IsBusy = false }, SideEffect.None


    let update msg (state:State) =
        match msg with
        | ChangeEMail email -> { state with EMail = email }, SideEffect.None
        | ChangeMessage message -> { state with Message = message }, SideEffect.None
        | RunSideEffects sideEffect -> state, sideEffect
        | SetBusy isBusy -> { state with IsBusy = isBusy }, SideEffect.None
        | KeyboardStateChanged -> state, SideEffect.None



    module SideEffects =
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()

                | SideEffect.SendFeedback ->
                    dispatch <| SetBusy true
                    let! res = Services.SupportFeedback.sendSupportFeedBack state.EMail "Feedback" state.Message
                    dispatch <| SetBusy false

                    match res with
                    | Ok _ ->
                        // empty form
                        dispatch <| ChangeEMail ""
                        dispatch <| ChangeMessage ""
                        do! Task.Delay 500
                        dispatch <| RunSideEffects SideEffect.CloseDialog
                        return ()

                    | Error e ->
                        do! Notifications.showErrorMessage e
                        return ()

                | SideEffect.CloseDialog ->
                    InteractiveContainer.CloseDialog()
                    // Backbutton back to default
                    DependencyService.Get<INavigationService>().ResetBackbuttonPressed()
            }

open FeedbackPage

type FeedbackViewModel(?designView) =
    inherit ReactiveElmishViewModel()

    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore

    let designView = defaultArg designView false
    do
        if OperatingSystem.IsAndroid() && not designView then
            base.Subscribe(InputPaneService.InputPane.StateChanged, fun _ -> local.Dispatch KeyboardStateChanged)
            let notificationService = DependencyService.Get<INavigationService>()
            notificationService.RegisterBackbuttonPressed (fun () -> local.Dispatch <| RunSideEffects SideEffect.CloseDialog)
            ()

    new() =
        new FeedbackViewModel(false)

    member this.EMail
        with get() = this.Bind(local, _.EMail)
        and set v = local.Dispatch (ChangeEMail v)

    member this.Message
        with get() = this.Bind(local, _.Message)
        and set v = local.Dispatch (ChangeMessage v)

    member this.IsBusy = this.Bind(local, _.IsBusy)

    member this.SendFeedback() = local.Dispatch <| RunSideEffects SideEffect.SendFeedback
    member this.Cancel() = local.Dispatch <| RunSideEffects SideEffect.CloseDialog

    /// return the screen size for the login form dialog
    member this.DialogWidth = this.Bind(local, fun _ ->
        let screenService = DependencyService.Get<IScreenService>()
        let screenSize = screenService.GetScreenSize()
        let width = ((screenSize.Width |> float) / screenSize.ScaledDensity) |> int
        width
        )
    member this.InputPaneHeight = this.Bind(local, fun _ ->
        match InputPaneService.InputPane with
        | null -> 0.0
        | ip -> ip.OccludedRect.Height
        )
    static member DesignVM = new FeedbackViewModel(true)

