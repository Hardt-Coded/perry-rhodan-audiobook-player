namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open CherylUI.Controls
open Dependencies
open Microsoft.Maui.ApplicationModel
open PerryRhodan.AudiobookPlayer.ViewModel
open ReactiveElmish
open Services.DependencyServices

type ActionMenuViewModel(audioBook: AudioBookItemViewModel) =
    inherit ReactiveElmishViewModel()
    
    member this.AudioBook = audioBook
    
    member this.StartDownload() =
        this.AudioBook.StartDownload()
        InteractiveContainer.CloseDialog()
        
    member this.RemoveDownload() =
        this.AudioBook.RemoveDownload()
        InteractiveContainer.CloseDialog()
    
    member this.OpenDetail() =
        this.AudioBook.OpenDetail()
        InteractiveContainer.CloseDialog()
    
    member this.MarkAsListend() =
        this.AudioBook.MarkAsListend()
        InteractiveContainer.CloseDialog()
    
    member this.MarkAsUnlistend() =
        this.AudioBook.MarkAsUnlistend()
        InteractiveContainer.CloseDialog()
    
    member this.RemoveAudiobookFromDevice() =
        this.AudioBook.RemoveAudiobookFromDevice()
        InteractiveContainer.CloseDialog()
    
    member this.OpenPlayer() =
        this.AudioBook.OpenPlayer()
        InteractiveContainer.CloseDialog()
    
    member this.ShowMetaData() =
        this.AudioBook.ShowMetaData()
        InteractiveContainer.CloseDialog()
        
    member this.ShowProductPage() =
        // open web browser url
        this.AudioBook.AudioBook.ProductSiteUrl
        |> Option.iter (fun url ->
            let uri = Uri(Services.Consts.baseUrl + url)
            Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred) |> ignore
        )
        InteractiveContainer.CloseDialog()
        
        
        
    member this.ToggleAmbientColor() =
        this.AudioBook.ToggleAmbientColor()
        InteractiveContainer.CloseDialog()
        
    member this.CloseDialog() =
        InteractiveContainer.CloseDialog()
        
    
    /// return the screen size for the login form dialog
    member this.DialogWidth = 
        let screenService = DependencyService.Get<IScreenService>()
        let screenSize = screenService.GetScreenSize()
        let width = ((screenSize.Width |> float) / screenSize.ScaledDensity) |> int
        width
        
        
    
        
    static member DesignVM = new ActionMenuViewModel(AudioBookItemViewModel.DesignVM)




