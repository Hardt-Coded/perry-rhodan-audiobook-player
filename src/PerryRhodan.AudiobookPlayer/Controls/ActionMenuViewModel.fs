namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open CherylUI.Controls
open Dependencies
open Microsoft.Maui.ApplicationModel
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open ReactiveElmish
open Services
open Services.DependencyServices

type ActionMenuViewModel(audioBook: AudioBookItemViewModel) =
    inherit ReactiveElmishViewModel()
    
    member this.AudioBook = audioBook
    
    member this.StartDownload() =
        this.AudioBook.StartDownload()
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
        
    member this.RemoveDownload() =
        this.AudioBook.RemoveDownload()
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
    
    member this.MarkAsListend() =
        this.AudioBook.MarkAsListend()
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
    
    member this.MarkAsUnlistend() =
        this.AudioBook.MarkAsUnlistend()
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
    
    member this.RemoveAudiobookFromDevice() =
        this.AudioBook.RemoveAudiobookFromDevice()
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
    
    member this.OpenPlayer() =
        this.AudioBook.OpenPlayer()
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
    
    member this.ShowMetaData() =
        this.AudioBook.ShowMetaData()
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
        
    member this.ShowProductPage() =
        // open web browser url
        this.AudioBook.AudioBook.ProductSiteUrl
        |> Option.iter (fun url ->
            let uri = Uri(Services.Consts.baseUrl + url)
            Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred) |> ignore
        )
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
        
        
        
    member this.ToggleAmbientColor() =
        this.AudioBook.ToggleAmbientColor()
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
        
    member this.CloseDialog() =
        DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
        InteractiveContainer.CloseDialog()
        
        
    
    /// return the screen size for the login form dialog
    member this.DialogWidth = 
        let screenService = DependencyService.Get<IScreenService>()
        let screenSize = screenService.GetScreenSize()
        let width = ((screenSize.Width |> float) / screenSize.ScaledDensity) |> int
        width
        
        
    
        
    static member DesignVM = new ActionMenuViewModel(AudioBookItemViewModel.DesignVM)




