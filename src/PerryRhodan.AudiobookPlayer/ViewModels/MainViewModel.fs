﻿namespace PerryRhodan.AudiobookPlayer.ViewModels

open Avalonia.Media
open Dependencies
open PerryRhodan.AudiobookPlayer
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.Views
open ReactiveElmish
open PerryRhodan.AudiobookPlayer.ViewModels.App
open ReactiveElmish.Avalonia
open Services.DependencyServices

type MainViewModel(root:CompositionRoot) =
    inherit ReactiveElmishViewModel()

    interface IMainViewModel with
        member this.GotoPlayerPage audiobook startPlayer = this.GotoPlayerPage (audiobook :?> AudioBookItemViewModel) startPlayer
        member this.OpenMiniplayer audiobook startPlayer = this.OpenMiniplayer (audiobook :?> AudioBookItemViewModel) startPlayer
        member this.CloseMiniplayer() = app.Dispatch CloseMiniplayer
        member this.GotoHomePage() = this.GotoHomePage()
        member this.CurrentPlayerAudiobookViewModel = this.Bind(app, fun e -> e.PlayerViewModel |> Option.map (fun vm -> vm.AudioBook))

    member this.SetMiniPlayerViewModel (playerViewModel:PlayerViewModel) = app.Dispatch <| SetPlayerViewModel playerViewModel

    member this.MiniplayerControl =
        this.BindOnChanged (app, _.MiniPlayerControl, fun e ->
            e.MiniPlayerControl |> Option.defaultValue (Unchecked.defaultof<MiniPlayerView>)
        )

    member this.MiniplayerIsVisible =
        this.BindOnChanged (app, (fun c -> (c.View, c.MiniPlayerControl)),
                fun e ->
                    match e.View with
                    | View.PlayerPage _ -> false
                    | _ -> e.MiniPlayerControl.IsSome
        )

    member this.ContentView =
        this.BindOnChanged (app, _.View, fun m ->
            // reset Backbutton when change View
            // but not when the app is initializing
            // and the di container is not ready
            if DependencyService.IsComplete then
                DependencyService.Get<INavigationService>().ResetBackbuttonPressed()

            try
                match m.View with
                | View.HomePage ->
                    let view = root.GetView<HomeViewModel>()
                    view

                | View.BrowserPage ->
                    root.GetView<BrowserViewModel>()

                | View.SettingsPage ->
                    root.GetView<SettingsViewModel>()

                | View.PlayerPage playerView ->
                    playerView
            with
            | ex ->
                let e = ex
                reraise()

        )

    member this.IsLoading = this.Bind (app, _.IsLoading)

    member this.GotoHomePage() =
        app.Dispatch <| SetView View.HomePage

    member this.OpenCurrentPlayerPage () =
        if not app.Model.IsLoading then app.Dispatch <| OpenCurrentPlayerPage
    
    member this.GotoPlayerPage audiobook startPlaying =
        if not app.Model.IsLoading then app.Dispatch <| OpenPlayerPage (audiobook, startPlaying)

    member this.OpenMiniplayer audiobook startPlaying =
        if not app.Model.IsLoading then app.Dispatch <| OpenMiniplayer (audiobook, startPlaying)

    member this.OpenLoginForm() =
        if not app.Model.IsLoading then app.Dispatch OpenLoginView

    member this.GotoOptionPage() =
        if not app.Model.IsLoading then app.Dispatch <| SetView View.SettingsPage

    member this.GotoBrowserPage() =
        if not app.Model.IsLoading then app.Dispatch <| SetView View.BrowserPage

    member this.HomeButtonColor =
        this.Bind (app, fun e ->  SolidColorBrush(if e.View = View.HomePage then  Colors.WhiteSmoke else Colors.DarkGray))

    member this.BrowserButtonColor =
        this.Bind (app, fun e -> SolidColorBrush(if e.View = View.BrowserPage then Colors.WhiteSmoke else Colors.DarkGray))

    member this.SettingsButtonColor =
        this.Bind (app, fun e -> SolidColorBrush(if e.View = View.SettingsPage then Colors.WhiteSmoke else Colors.DarkGray))
        
    member this.PlayerAvailable =
        this.Bind (app, fun e -> e.PlayerViewModel.IsSome)


    static member DesignVM = new MainViewModel(Design.stub)

