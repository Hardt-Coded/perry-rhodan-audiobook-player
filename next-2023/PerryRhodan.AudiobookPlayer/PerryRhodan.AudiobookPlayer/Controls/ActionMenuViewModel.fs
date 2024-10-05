namespace PerryRhodan.AudiobookPlayer.ViewModels

open Dependencies
open PerryRhodan.AudiobookPlayer.ViewModel
open ReactiveElmish
open Services.DependencyServices

type ActionMenuViewModel(audioBook: AudioBookItemViewModel) =
    inherit ReactiveElmishViewModel()
    
    member this.AudioBook = audioBook
    
    /// return the screen size for the login form dialog
    member this.DialogWidth = 
        let screenService = DependencyService.Get<IScreenService>()
        let screenSize = screenService.GetScreenSize()
        let width = ((screenSize.Width |> float) / screenSize.ScaledDensity) |> int
        width
        
        
    
        
    static member DesignVM = new ActionMenuViewModel(AudioBookItemViewModel.DesignVM)




