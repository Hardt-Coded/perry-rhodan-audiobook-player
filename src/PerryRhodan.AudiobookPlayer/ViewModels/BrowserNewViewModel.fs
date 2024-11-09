namespace PerryRhodan.AudiobookPlayer.ViewModels

open CherylUI.Controls
open Common
open Dependencies
open PerryRhodan.AudiobookPlayer.Controls
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open ReactiveElmish
open Services


module BrowserPage =

    type State = {
        AudioBooks: AudioBookItemViewModel[]
        Filter: string
        SortOrder: SortOrder
        SearchText: string
        BusyMessage: string

        IsBusy: bool
    }

    and SortOrder =
        | TitleAsc
        | TitleDesc
        | LastListendAsc
        | LastListendDesc
        | IdAsc
        | IdDesc


    type Msg =
        | AudioBookItemsChanged of AudioBookItemViewModel array
        | FilterChanged of string
        | SortOrderChanged of SortOrder
        | SearchTextChanged of string
        | AppendBusyMessage of string

        | SetBusy of bool

    
    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | Init
        

    let init audiobookItems =
        {
            AudioBooks = audiobookItems
            Filter = ""
            SortOrder = SortOrder.IdAsc
            SearchText = ""
            BusyMessage = ""
            IsBusy = false
        }, SideEffect.Init
        
        
    let rec update msg state =
        match msg with
        | AudioBookItemsChanged audiobookItems ->
            { state with AudioBooks = audiobookItems |> sortAudioBooks state.SortOrder }, SideEffect.None
        
        | FilterChanged filter ->
            {
                state with
                    Filter = filter
                    AudioBooks = state.AudioBooks |> sortAudioBooks state.SortOrder
            }, SideEffect.None
            
        | SortOrderChanged sortOrder ->
            {
                state with
                    SortOrder = sortOrder
                    AudioBooks = state.AudioBooks |> sortAudioBooks sortOrder
            }, SideEffect.None
            
        | SearchTextChanged searchText ->
            { state with SearchText = searchText }, SideEffect.None
            
        | AppendBusyMessage message ->
            { state with BusyMessage = $"{state.BusyMessage}\r\n{message}" }, SideEffect.None
            
        | SetBusy isBusy ->
            { state with IsBusy = isBusy }, SideEffect.None
            
            
    and sortAudioBooks (sortOrder:SortOrder) (audiobooks:AudioBookItemViewModel[]) =
        match sortOrder with
        | SortOrder.TitleAsc ->         audiobooks |> Array.sortBy (_.Title)
        | SortOrder.TitleDesc ->        audiobooks |> Array.sortByDescending (_.Title)
        | SortOrder.LastListendAsc ->   audiobooks |> Array.sortBy (_.AudioBook.State.LastTimeListend)
        | SortOrder.LastListendDesc ->  audiobooks |> Array.sortByDescending (_.AudioBook.State.LastTimeListend)
        | SortOrder.IdAsc ->            audiobooks |> Array.sortBy (_.AudioBook.Id)
        | SortOrder.IdDesc ->           audiobooks |> Array.sortByDescending (_.AudioBook.Id)
        
            
    
    module SideEffects =
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.Init ->
                    // do something
                    ()
                | _ -> ()    
            }
                



open BrowserPage
open Elmish.SideEffect
open ReactiveElmish.Avalonia

type BrowserViewModel(?audiobookItems) as self =
    inherit ReactiveElmishViewModel()

    let audiobookItems = [||] |> defaultArg audiobookItems

    let init () =
        init audiobookItems

    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
        
    let searchStringDebouncer = Extensions.debounce<string>
    
    do
        self.AddDisposable
            <| AudioBookStore.globalAudiobookStore.Observable.Subscribe(fun s ->
                local.Dispatch (s.IsLoading |> SetBusy)
                local.Dispatch (s.Audiobooks |> AudioBookItemsChanged)
            )
        ()
        
    member this.AudioBooks = this.BindList(local, _.AudioBooks)
    
    
    member this.IsBusy                      = this.BindOnChanged(local, _.IsBusy, _.IsBusy)
    member this.BusyMessage                 = this.BindOnChanged(local, _.BusyMessage, _.BusyMessage)
    
    member this.SearchText
        with get() = "nix"
        and set(value) =
            searchStringDebouncer 1000 (fun s ->
                // protect against null
                let s = if s = null then "" else s
                local.Dispatch <| SearchTextChanged s
            ) value
       
            
    member this.OnInitialized() =
        let currentAudioBooks = AudioBookStore.globalAudiobookStore.Model.Audiobooks
        local.Dispatch (currentAudioBooks |> AudioBookItemsChanged)
       
        
    member this.LoadOnlineAudiobooks() =
        task {
            local.Dispatch (true |> SetBusy)
            
            let showMessage = Notifications.showMessage
            let showErrorMessage = Notifications.showErrorMessage
            let loadCookie = Services.SecureLoginStorage.loadCookie
            let appendBusyMessage msg = local.Dispatch <| AppendBusyMessage msg
            let openLogin = this.OpenLogin
            
            
            let onSuccess audiobooks =
                local.Dispatch (audiobooks |> AudioBookItemsChanged)
                AudioBookStore.globalAudiobookStore.Dispatch <| AudioBookStore.AudioBookElmish.AudiobooksLoaded audiobooks
                AudioBookStore.globalAudiobookStore.Dispatch <| AudioBookStore.AudioBookElmish.AudiobooksLoaded audiobooks
                DependencyService.Get<IPictureDownloadService>().StartDownload()
                
            
            do! ShopService.synchronizeWithCloud
                     showMessage
                     showErrorMessage
                     loadCookie
                     appendBusyMessage
                     openLogin
                     onSuccess
                
            local.Dispatch (false |> SetBusy)
            
        }
        
    member this.OpenLogin () =
        let control = PerryRhodan.AudiobookPlayer.Views.LoginView()
        let vm = new LoginViewModel()
        control.DataContext <- vm
        InteractiveContainer.ShowDialog (control, true)
        
        
    member this.SortOrder 
        with get() = this.BindOnChanged(local, _.SortOrder, _.SortOrder)
        and set(value) = local.Dispatch <| SortOrderChanged value
        
    member this.SortOrders = [
        (SortOrder.TitleAsc         , "Title aufsteigend")
        (SortOrder.TitleDesc        , "Title absteigend")
        (SortOrder.LastListendAsc   , "Zuletzt gehört zuerst")
        (SortOrder.LastListendDesc  , "Zuletzt gehört zuletzt")
        (SortOrder.IdAsc            , "Neuste zuerst")
        (SortOrder.IdDesc           , "Älteste zuerst")
    ]
        
        
            
    
    static member DesignVM =
        new BrowserViewModel(
            [|
                AudioBookItemViewModel(DemoData.designAudioBook)
                AudioBookItemViewModel(DemoData.designAudioBook2)
                AudioBookItemViewModel(DemoData.designAudioBook3)
            |])
    
