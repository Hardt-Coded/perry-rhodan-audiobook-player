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
    | BrowserPage
    | SettingsPage



type Msg =
    | SetView of View

    | OpenPlayerPage of viewModel: AudioBookItemViewModel * startPlaying: bool
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
                            dispatch (Msg.IsLoading s.IsLoading)
                        ) |> ignore


                        let globalSettings = DependencyService.Get<GlobalSettingsService>()
                        do! globalSettings.Init()

                        // On first start ask for notification permission
                        if OperatingSystem.IsAndroid() && globalSettings.IsFirstStart then
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
                        playerView.DataContext <- viewModel
                        dispatch <| SetView (View.PlayerPage playerView)
                        return ()

                }

            dispatch <| IsLoading false


    }



open Elmish.SideEffect

let app =
    Program.mkAvaloniaProgrammWithSideEffect init update runSideEffect
    |> Program.withErrorHandler (fun (_, ex) ->
        Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
    )
    //|> Program.withConsoleTrace
    |> Program.mkStore
