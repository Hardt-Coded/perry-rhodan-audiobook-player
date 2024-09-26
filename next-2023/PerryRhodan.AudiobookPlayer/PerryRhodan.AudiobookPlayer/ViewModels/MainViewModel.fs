namespace PerryRhodan.AudiobookPlayer.ViewModels

open PerryRhodan.AudiobookPlayer
open ReactiveElmish
open PerryRhodan.AudiobookPlayer.ViewModels.App
open ReactiveElmish.Avalonia

type MainViewModel(root:CompositionRoot) =
    inherit ReactiveElmishViewModel()

    member this.ContentView = 
        this.BindOnChanged (app, _.View, fun m -> 
            match m.View with
            | View.HomeView -> root.GetView<HomeViewModel>()
            | View.PlayerView -> root.GetView<PlayerViewModel>()
            
        )
        
    member this.GoHome() = app.Dispatch GoHome   
    member this.GoPlayer() = app.Dispatch <| SetView View.PlayerView   
    member this.OpenLogin() = app.Dispatch Login   
        
    static member DesignVM = new MainViewModel(Design.stub)
    
