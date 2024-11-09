namespace PerryRhodan.AudiobookPlayer.ViewModels

open Avalonia.Media
open Dependencies
open PerryRhodan.AudiobookPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
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
                    root.GetView<HomeViewModel>()

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
    
    member this.HomeButtonColor =
        this.BindOnChanged (app, _.View, fun e ->  SolidColorBrush(match e.View with | View.HomePage ->  Colors.WhiteSmoke | _ -> Colors.DarkGray))

    member this.SettingsButtonColor =
        this.BindOnChanged (app, _.View, fun e -> SolidColorBrush(match e.View with | View.SettingsPage -> Colors.WhiteSmoke | _ -> Colors.DarkGray))
        
    member this.PlayerButtonColor =
        this.BindOnChanged (app, _.View, fun e -> SolidColorBrush(match e.View with | View.PlayerPage _  -> Colors.WhiteSmoke | _ -> Colors.DarkGray))
        
    member this.PlayerAvailable =
        this.BindOnChanged (app, _.PlayerViewModel, _.PlayerViewModel.IsSome)


    static member DesignVM = new MainViewModel(Design.stub)

