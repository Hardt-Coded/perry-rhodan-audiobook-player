module PerryRhodan.AudiobookPlayer.ViewModels.App

open System.Threading.Tasks
open Avalonia.Controls
open CherylUI.Controls
open Elmish
open Microsoft.Maui.ApplicationModel
open PerryRhodan.AudiobookPlayer.ViewModel
open ReactiveElmish.Avalonia




type Model = {
    View: View
    IsLoading: bool
    MiniPlayerViewModel: PlayerViewModel option
}


and [<RequireQualifiedAccess>] View =
    | HomePage
    | PlayerPage of viewModel: AudioBookItemViewModel * startPlaying: bool
    | BrowserPage



type Msg =
    | SetView of View
    | GotoHomePage
    | OpenLoginView
    | IsLoading of bool
    | SetMiniPlayerViewModel of PlayerViewModel


[<RequireQualifiedAccess>]
type SideEffect =
    | None
    | InitApplication
    | OpenLoginView



let init () =
    {
        View = View.HomePage
        IsLoading = false
        MiniPlayerViewModel = None
    },
    SideEffect.InitApplication


let update msg state =
    match msg with
    | SetView view ->
        { state with View = view }, SideEffect.None
    | GotoHomePage ->
        { state with View = View.HomePage }, SideEffect.None
    | OpenLoginView ->
        state, SideEffect.OpenLoginView
    | IsLoading isLoading ->
        { state with IsLoading = isLoading }, SideEffect.None
    | SetMiniPlayerViewModel playerViewModel ->
        {
            state with
                MiniPlayerViewModel = Some playerViewModel
        },
        SideEffect.None


let runSideEffect sideEffect state dispatch =
    task {
        match sideEffect with
        | SideEffect.None -> return ()

        | SideEffect.OpenLoginView ->
            let control = PerryRhodan.AudiobookPlayer.Views.LoginView()
            let vm = new LoginViewModel()
            control.DataContext <- vm

            InteractiveContainer.ShowDialog(control, true)

        | SideEffect.InitApplication ->
            // if the global audiobook store is busy display here a loading indicator
            AudioBookStore.globalAudiobookStore.Observable.Subscribe(fun s ->
                dispatch (Msg.IsLoading s.IsLoading)
            )
            |> ignore

            do!
                Task.Delay 5000
#if ANROID
                let! a = Permissions.CheckStatusAsync<Permissions.PostNotifications>()

                match a with
                | PermissionStatus.Granted ->
                    Services.Notifications.showToasterMessage "Permission granted"
                | _ ->
                    // Todo: check if user already saw this message
                    let! result =
                        Services.Notifications.showQuestionDialog
                            "Benachrichtigungen"
                            "Benachrichtungen sind deaktiviert, damit wird der Downloadfortschritt nicht außerhalb der App angezeigt. In den Telefon-Einstellung zur App, können diese aktiviert werden."
                            "Einstellungen"
                            "Abbrechen"

                    if result then
                        AppInfo.ShowSettingsUI()
#endif

    }



open Elmish.SideEffect

let app =
    Program.mkAvaloniaProgrammWithSideEffect init update runSideEffect
    |> Program.withErrorHandler (fun (_, ex) ->
        Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
    )
    //|> Program.withConsoleTrace
    |> Program.mkStore
