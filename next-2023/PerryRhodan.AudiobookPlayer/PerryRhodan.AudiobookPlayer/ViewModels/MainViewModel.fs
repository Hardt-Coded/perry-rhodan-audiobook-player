namespace PerryRhodan.AudiobookPlayer.ViewModels

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
        member this.GotoHomePage() = this.GotoHomePage()
    
    member this.SetMiniPlayerViewModel (playerViewModel:PlayerViewModel) = app.Dispatch <| SetMiniPlayerViewModel playerViewModel
    member this.MiniplayerControl =
        this.BindOnChanged (app, _.MiniPlayerViewModel, fun e ->
            match e.MiniPlayerViewModel with
            | None ->
                Unchecked.defaultof<MiniPlayerView>
            | Some vm ->
                let view = MiniPlayerView()
                view.DataContext <- vm
                view
        )
        
    member this.MiniplayerIsVisible =
        this.Bind (app,
                fun e ->
                    match e.View with
                    | View.PlayerPage _ -> false
                    | _ -> e.MiniPlayerViewModel.IsSome
        )
        
    member this.ContentView = 
        this.BindOnChanged (app, _.View, fun m -> 
            // reset Backbutton when change View
            // but not when the app is initializing
            // and the di container is not ready
            if DependencyService.IsComplete then
                DependencyService.Get<INavigationService>().ResetBackbuttonPressed()
            
            match m.View with
            | View.HomePage ->
                root.GetView<HomeViewModel>()
            
            | View.PlayerPage (audiobook, startPlaying)  ->
                try
                    let view = PlayerView()
                    let viewModel = PlayerViewModelStore.create audiobook startPlaying
                    view.DataContext <- viewModel
                    this.SetMiniPlayerViewModel viewModel
                    view
                with
                | ex ->
                    Services.Notifications.showErrorMessage ex.Message |> ignore
                    reraise()
                
            | View.BrowserPage ->
                let view = BrowserView()
                let viewModel = new BrowserViewModel([])
                view.DataContext <- viewModel
                view
        )
        
    member this.IsLoading = this.Bind (app, _.IsLoading)
    
    member this.GotoHomePage() = app.Dispatch GotoHomePage   
    member this.GotoPlayerPage audiobook startPlaying = app.Dispatch <| SetView (View.PlayerPage (audiobook, startPlaying))   
    member this.OpenLoginForm() = app.Dispatch OpenLoginView   
    member this.GotoBrowserPage() = app.Dispatch <| SetView View.BrowserPage   
       
        
    static member DesignVM = new MainViewModel(Design.stub)
    
