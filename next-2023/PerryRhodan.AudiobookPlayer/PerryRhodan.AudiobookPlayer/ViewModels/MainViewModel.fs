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
        member this.GoPlayerPage audiobook = this.GoPlayer (audiobook :?> AudioBookItemViewModel)
    
    member this.ContentView = 
        this.BindOnChanged (app, _.View, fun m -> 
            // reset Backbutton when change View
            // but not when the app is initializing
            // and the di container is not ready
            if DependencyService.IsComplete then
                DependencyService.Get<INavigationService>().ResetBackbuttonPressed()
            
            match m.View with
            | View.HomeView ->
                root.GetView<HomeViewModel>()
            
            | View.PlayerView audiobook  ->
                try
                    let view = PlayerView()
                    let viewModel = new PlayerViewModel(audiobook)
                    view.DataContext <- viewModel
                    view
                with
                | ex ->
                    Services.Notifications.showErrorMessage ex.Message |> ignore
                    reraise()
                
            | View.BrowserView ->
                let view = BrowserView()
                let viewModel = new BrowserViewModel([])
                view.DataContext <- viewModel
                view
        )
        
    member this.IsLoading = this.Bind (app, _.IsLoading)
    
    member this.GoHome() = app.Dispatch GoHome   
    member this.GoPlayer audiobook = app.Dispatch <| SetView (View.PlayerView audiobook)   
    member this.OpenLogin() = app.Dispatch Login   
    member this.OpenBrowserView() = app.Dispatch <| SetView View.BrowserView   
       
        
    static member DesignVM = new MainViewModel(Design.stub)
    
