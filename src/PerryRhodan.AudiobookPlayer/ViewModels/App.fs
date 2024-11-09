module PerryRhodan.AudiobookPlayer.ViewModels.App

open System.Threading.Tasks
open Avalonia.Controls
open CherylUI.Controls
open Dependencies
open Elmish
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer.Services
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.Views
open ReactiveElmish.Avalonia
open Services
open PerryRhodan.AudiobookPlayer.Controls
open System
open Microsoft.Maui.ApplicationModel




type Model = {
    View: View
    IsLoading: bool
    PlayerViewModel: PlayerViewModel option
    MiniPlayerControl: MiniPlayerView option
}


and [<RequireQualifiedAccess>] View =
    | HomePage
    | PlayerPage of PlayerView
    | SettingsPage



type Msg =
    | SetView of View

    | OpenPlayerPage of viewModel: AudioBookItemViewModel * startPlaying: bool
    | OpenCurrentPlayerPage
    | OpenLoginView

    | CloseMiniplayer
    | OpenMiniplayer of audiobook:AudioBookItemViewModel * startPlaying: bool

    | SetPlayerViewModel of PlayerViewModel
    | SetMiniPlayerControl of MiniPlayerView option

    | IsLoading of bool



[<RequireQualifiedAccess>]
type SideEffect =
    | None
    | InitApplication

    | OpenMiniplayer of audiobook:AudioBookItemViewModel * startPlaying:bool
    | OpenPlayerPage of audiobook:AudioBookItemViewModel * startPlaying:bool
    | OpenCurrentPlayerPage

    | OpenLoginView



let init () =
    {
        View = View.HomePage
        IsLoading = false
        PlayerViewModel = None
        MiniPlayerControl = None
    },
    SideEffect.InitApplication


let update msg state =
    match msg with
    | SetView view ->
        { state with View = view }, SideEffect.None

    | OpenPlayerPage(viewModel, startPlaying) ->
        state, SideEffect.OpenPlayerPage (viewModel, startPlaying)

    | OpenCurrentPlayerPage ->
        state, SideEffect.OpenCurrentPlayerPage

    | OpenMiniplayer (viewModel, startPlaying) ->
        state, SideEffect.OpenMiniplayer (viewModel, startPlaying)

    | CloseMiniplayer ->
        { state with PlayerViewModel = None; MiniPlayerControl = None }, SideEffect.None

    | OpenLoginView ->
        state, SideEffect.OpenLoginView

    | IsLoading isLoading ->
        { state with IsLoading = isLoading }, SideEffect.None

    | SetPlayerViewModel playerViewModel ->
        {
            state with
                PlayerViewModel = Some playerViewModel
        },
        SideEffect.None

    | SetMiniPlayerControl miniPlayerViewOption ->
        {
            state with
                MiniPlayerControl = miniPlayerViewOption
        },
        SideEffect.None



let runSideEffect sideEffect state dispatch =
    task {
        if sideEffect = SideEffect.None then
            return ()
        else
            dispatch <| IsLoading true
            do!
                task {
                    match sideEffect with
                    | SideEffect.None ->
                        return ()

                    | SideEffect.OpenLoginView ->
                        let control = LoginView()
                        let vm = new LoginViewModel()
                        control.DataContext <- vm
                        InteractiveContainer.ShowDialog(control, true)

                    | SideEffect.InitApplication ->
                        // if the global audiobook store is busy display here a loading indicator
                        AudioBookStore.globalAudiobookStore.Observable.Subscribe(fun s ->
                            dispatch (Msg.IsLoading s.IsBusy)
                        ) |> ignore


                        let globalSettings = DependencyService.Get<GlobalSettingsService>()
                        do! globalSettings.Init()
                        
                        if globalSettings.IsFirstStart then
                            do! Notifications.showMessage
                                    "Willkommen!"
                                    "Hallo und Willkommen zum Eins A Medien Audioplayer im neuen Gewand. Ich habe den Player neugeschrieben. Ich bitte um Entschuldigung, es handelt sich derzeit um eine Beta-Version. Bei Problemen, meldet euch einfach wie bekannt unter 'info@hardt-solutions.de' Oder über die Feedbackseite. \r\n Viel Spass!"

                        // On first start ask for notification permission
                        if OperatingSystem.IsAndroid() && globalSettings.IsFirstStart then
                            let! a = Permissions.RequestAsync<Permissions.PostNotifications>()
                            match a with
                            | PermissionStatus.Granted ->
                                Services.Notifications.showToasterMessage "Permission granted"
                            | _ ->
                                ()


                        


                        globalSettings.IsFirstStart <- false



                    | SideEffect.OpenMiniplayer (audioBookItemViewModel, startPlaying) ->
                        let viewModel = PlayerViewModelStore.create audioBookItemViewModel startPlaying
                        dispatch <| SetPlayerViewModel viewModel
                        let miniPlayerView = MiniPlayerView()
                        miniPlayerView.DataContext <- viewModel
                        dispatch <| SetMiniPlayerControl (Some miniPlayerView)
                        return ()

                    | SideEffect.OpenPlayerPage (audioBookItemViewModel, startPlaying) ->
                        let viewModel = PlayerViewModelStore.create audioBookItemViewModel startPlaying
                        dispatch <| SetPlayerViewModel viewModel
                        let playerView = PlayerView()
                        let position = viewModel.CurrentPositionMs
                        playerView.DataContext <- viewModel
                        // after assignment of the viewmodel to the player page, the position is set to zero.
                        // therefore we have to set the position again
                        viewModel.CurrentPositionMs <- position
                        dispatch <| SetView (View.PlayerPage playerView)
                        return ()

                    | SideEffect.OpenCurrentPlayerPage  ->
                        match state.PlayerViewModel with
                        | None -> return ()
                        | Some viewModel ->
                            let playerView = PlayerView()
                            let position = viewModel.CurrentPositionMs
                            playerView.DataContext <- viewModel
                            // after assignment of the viewmodel to the player page, the position is set to zero.
                            // therefore we have to set the position again
                            viewModel.CurrentPositionMs <- position
                            dispatch <| SetView (View.PlayerPage playerView)
                            return ()
                }

            dispatch <| IsLoading false


    }



open Elmish.SideEffect

DependencyService.ServiceCollection.AddSingleton<GlobalSettingsService>(GlobalSettingsService()) |> ignore
DependencyService.SetComplete()

let app =
    Program.mkAvaloniaProgrammWithSideEffect init update runSideEffect
    |> Program.withErrorHandler (fun (_, ex) ->
        Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
        Global.telemetryClient.TrackException ex
    )
    //|> Program.withConsoleTrace
    |> Program.mkStore
