namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open Avalonia.Data.Converters
open CherylUI.Controls
open Common
open Dependencies
open Domain
open PerryRhodan.AudiobookPlayer.Controls
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open ReactiveElmish
open Services


module private DemoData =
    open Domain

    let designAudioBook = {
            Id = 1
            ShopId = None
            FullName = "Perry Rhodan 3000 - Mythos Erde"
            EpisodeNo = Some 3000
            EpisodenTitel = "Mythos Erde"
            Group = "Perry Rhodan"
            Picture = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            Thumbnail = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            DownloadUrl = None
            ProductSiteUrl = None
            State = {
                Completed = true
                CurrentPosition = None
                Downloaded = true
                DownloadedFolder = None
                LastTimeListend = None
            }
            AmbientColor = Some "#FF0000"
            PublishingDate = None
        }

    let designAudioBook2 = {
            Id = 2
            ShopId = None
            FullName = "Perry Rhodan 3001 - Mythos Erde2"
            EpisodeNo = Some 3000
            EpisodenTitel = "Mythos Erde2"
            Group = "Perry Rhodan"
            Picture = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            Thumbnail = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            DownloadUrl = None
            ProductSiteUrl = None
            State = {
                Completed = true
                CurrentPosition = None
                Downloaded = true
                DownloadedFolder = None
                LastTimeListend = None
            }
            AmbientColor = Some "#00FF00"
            PublishingDate = None
        }

    let designAudioBook3 = {
            Id = 3
            ShopId = None
            FullName = "Perry Rhodan 3003 - Mythos Erde2"
            EpisodeNo = Some 3000
            EpisodenTitel = "Mythos Erde3"
            Group = "Perry Rhodan - Dies ist ein ganz langer Titel, der nicht in das Fenster passt"
            Picture = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            Thumbnail = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            DownloadUrl = None
            ProductSiteUrl = None
            State = {
                Completed = true
                CurrentPosition = None
                Downloaded = true
                DownloadedFolder = None
                LastTimeListend = None
            }
            AmbientColor = Some "#0000FF"
            PublishingDate = None
        }


module HomePage =

    type State = {
        AudioBooks: AudioBookItemViewModel[]
        Filter: FilterOptions
        SortOrder: SortOrder
        SearchText: string
        BusyMessage: string
        Shop: Shop
        IsBusy: bool
    }

    and SortOrder =
        | TitleAsc
        | TitleDesc
        | LastListendDesc
        | IdAsc
        | IdDesc


    and FilterOptions =
        | All
        | Downloaded
        | Unfinished
        | Finished
        | NotListend
        | CurrentlyListening
        | Group of groupName:string
        | Free of searchWord:string


    type Msg =
        | SwitchShop of Shop
        | AudioBookItemsChanged of AudioBookItemViewModel array
        | FilterChanged of FilterOptions
        | SortOrderChanged of SortOrder
        | SearchTextChanged of string
        | AppendBusyMessage of string
        | ClearBusyMessage

        | SetBusy of bool


    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | Init


    let init audiobookItems =
        {
            AudioBooks = audiobookItems
            Filter = FilterOptions.Downloaded
            SortOrder = SortOrder.LastListendDesc
            SearchText = ""
            BusyMessage = ""
            IsBusy = false
            Shop = NewShop
        }, SideEffect.Init


    let rec update msg state =
        match msg with
        | SwitchShop shop ->
            let filteredAudiobooks =
                match shop with
                | NewShop -> AudioBookStore.globalAudiobookStore.Model.NewShopAudiobooks
                | OldShop -> AudioBookStore.globalAudiobookStore.Model.OldShopAudiobooks
                |> filterAudioBooks state.Filter
                |> sortAudioBooks state.SortOrder
            { state with Shop = shop; AudioBooks = filteredAudiobooks }, SideEffect.None
        
        | AudioBookItemsChanged audiobookItems ->
            let filteredAudiobooks =
                audiobookItems
                |> filterAudioBooks state.Filter
                |> sortAudioBooks state.SortOrder

            { state with AudioBooks = filteredAudiobooks }, SideEffect.None

        | FilterChanged filter ->
            let filteredAudiobooks =
                match state.Shop with
                | NewShop -> AudioBookStore.globalAudiobookStore.Model.NewShopAudiobooks
                | OldShop -> AudioBookStore.globalAudiobookStore.Model.OldShopAudiobooks
                |> filterAudioBooks filter
                |> sortAudioBooks state.SortOrder
            {
                state with
                    Filter = filter
                    AudioBooks = filteredAudiobooks
            }, SideEffect.None

        | SortOrderChanged sortOrder ->
            {
                state with
                    SortOrder = sortOrder
                    AudioBooks = state.AudioBooks |> sortAudioBooks sortOrder
            }, SideEffect.None

        | SearchTextChanged searchText ->
            let filteredAudiobooks =
                match state.Shop with
                | NewShop -> AudioBookStore.globalAudiobookStore.Model.NewShopAudiobooks
                | OldShop -> AudioBookStore.globalAudiobookStore.Model.OldShopAudiobooks
                |> filterAudioBooks (FilterOptions.Free searchText)
                |> sortAudioBooks state.SortOrder

            { state with SearchText = searchText; AudioBooks = filteredAudiobooks }, SideEffect.None

        | AppendBusyMessage message ->
            { state with BusyMessage = $"{state.BusyMessage}\r\n{message}" }, SideEffect.None
        | ClearBusyMessage ->
            { state with BusyMessage = "" }, SideEffect.None

        | SetBusy isBusy ->
            { state with IsBusy = isBusy }, SideEffect.None


    and sortAudioBooks (sortOrder:SortOrder) (audiobooks:AudioBookItemViewModel[]) =
        match sortOrder with
        | SortOrder.TitleAsc ->         audiobooks |> Array.sortBy (_.Title)
        | SortOrder.TitleDesc ->        audiobooks |> Array.sortByDescending (_.Title)
        | SortOrder.LastListendDesc ->  audiobooks |> Array.sortByDescending (_.AudioBook.State.LastTimeListend)
        | SortOrder.IdAsc ->            audiobooks |> Array.sortBy (_.AudioBook.Id)
        | SortOrder.IdDesc ->           audiobooks |> Array.sortByDescending (_.AudioBook.Id)

    and filterAudioBooks (filter:FilterOptions) (audiobooks:AudioBookItemViewModel[]) =
        match filter with
        | FilterOptions.All ->                  audiobooks
        | FilterOptions.Downloaded ->           audiobooks |> Array.filter (_.AudioBook.State.Downloaded)
        | FilterOptions.Unfinished ->           audiobooks |> Array.filter (fun a -> not a.AudioBook.State.Completed)
        | FilterOptions.Finished ->             audiobooks |> Array.filter (_.AudioBook.State.Completed)
        | FilterOptions.NotListend ->           audiobooks |> Array.filter (fun a -> a.AudioBook.State.LastTimeListend.IsNone && not a.AudioBook.State.Completed)
        | FilterOptions.CurrentlyListening ->   audiobooks |> Array.filter (fun a -> a.AudioBook.State.LastTimeListend.IsSome && not a.AudioBook.State.Completed)
        | FilterOptions.Group groupName ->      audiobooks |> Array.filter (fun a -> a.AudioBook.Group = groupName)
        | FilterOptions.Free searchWord ->      audiobooks |> Array.filter (fun a -> a.Title.ToUpperInvariant().Contains (searchWord.ToUpperInvariant()))


    module SideEffects =
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.Init ->
                    // do something
                    ()
                | _ -> ()
            }




open HomePage
open Elmish.SideEffect
open ReactiveElmish.Avalonia



type HomeViewModel(?audiobookItems) as self =
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
                local.Dispatch (s.IsBusy |> SetBusy)
                match local.Model.Shop with
                | NewShop -> local.Dispatch (s.NewShopAudiobooks |> AudioBookItemsChanged)
                | OldShop -> local.Dispatch (s.OldShopAudiobooks |> AudioBookItemsChanged)
            )
        ()

    new () = new HomeViewModel([||])


    member this.AudioBooks =
        //this.BindList (local, _.AudioBooks)
        this.BindOnChanged(local,_.AudioBooks, _.AudioBooks)

    member this.IsNewShop 
        with get() = this.BindOnChanged(local, _.Shop, _.Shop.IsNewShop)
        and set(value) = local.Dispatch <| SwitchShop (if value then NewShop else OldShop)
        
    member this.IsOldShop =
        this.BindOnChanged(local, _.Shop, _.Shop.IsOldShop)

    member this.IsBusy                      = this.BindOnChanged(local, _.IsBusy, _.IsBusy)
    member this.BusyMessage               = this.BindOnChanged(local, _.BusyMessage, _.BusyMessage)

    member this.SearchText
        with get() = ""
        and set(value) =
            searchStringDebouncer 1000 (fun s ->
                // protect against null
                let s = if s = null then "" else s
                local.Dispatch <| SearchTextChanged s
            ) value


    member this.OnInitialized() =
        let currentAudioBooks =
            match local.Model.Shop with
            | NewShop -> AudioBookStore.globalAudiobookStore.Model.NewShopAudiobooks
            | OldShop -> AudioBookStore.globalAudiobookStore.Model.OldShopAudiobooks
                
        local.Dispatch (currentAudioBooks |> AudioBookItemsChanged)


    member this.LoadOnlineAudiobooks() =
        task {
            local.Dispatch (true |> SetBusy)

            let showMessage title message =
                task {
                    do! Notifications.showMessage title message
                    local.Dispatch <| ClearBusyMessage
                }
                
            let showErrorMessage message =
                task {
                    do! Notifications.showErrorMessage message
                    local.Dispatch <| ClearBusyMessage
                }
                
            let loadCookie =
                match local.Model.Shop with
                | NewShop -> Services.SecureLoginStorage.loadNewShopCookie
                | OldShop -> Services.SecureLoginStorage.loadOldShopCookie
                
            let appendBusyMessage msg = local.Dispatch <| AppendBusyMessage msg
            let openLogin =
                local.Dispatch <| ClearBusyMessage
                this.OpenLogin


            let onSuccess audiobooks =
                local.Dispatch (audiobooks |> AudioBookItemsChanged)
                match local.Model.Shop with
                | OldShop ->
                    AudioBookStore.globalAudiobookStore.Dispatch <| AudioBookStore.AudioBookElmish.AudiobooksLoaded (audiobooks, [||])
                | NewShop ->
                    AudioBookStore.globalAudiobookStore.Dispatch <| AudioBookStore.AudioBookElmish.AudiobooksLoaded ([||], audiobooks)
                    
                DependencyService.Get<IPictureDownloadService>().StartDownload local.Model.Shop

            match local.Model.Shop with
            | OldShop ->
                do! ShopService.synchronizeWithCloudOldShop
                     showMessage
                     showErrorMessage
                     loadCookie
                     appendBusyMessage
                     openLogin
                     onSuccess
                     
            | NewShop ->
                do! ShopService.synchronizeWithCloudNewShop
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
        let vm = new LoginViewModel(local.Model.Shop)
        control.DataContext <- vm
        InteractiveContainer.ShowDialog (control, true)


    member this.SortOrder
        with get() = this.BindOnChanged(local, _.SortOrder, _.SortOrder)
        and set(value) = local.Dispatch <| SortOrderChanged value

    member this.SortOrders = [|
        (SortOrder.LastListendDesc  , "Zuletzt gehört")
        (SortOrder.IdAsc            , "Älteste zuerst")
        (SortOrder.IdDesc           , "Neuste zuerst")
        (SortOrder.TitleAsc         , "Titel aufsteigend")
        (SortOrder.TitleDesc        , "Titel absteigend")
    |]


    member this.Filter
        with get() = this.BindOnChanged(local, _.Filter, _.Filter)
        and set(value) = local.Dispatch <| FilterChanged value


    member this.Filters =
        this.BindOnChanged(local, _.AudioBooks, fun _ ->
            [|
                FilterOptions.Downloaded             , "Auf dem Gerät"  , true
                FilterOptions.CurrentlyListening     , "Laufende"       , true
                FilterOptions.All                    , "Alle"           , true
                FilterOptions.Unfinished             , "Unbeendet"      , true
                FilterOptions.NotListend             , "Ungehört"       , true
                FilterOptions.Finished               , "Beendet"        , true

                
                let orderedGroups =
                    match local.Model.Shop with
                    | NewShop -> AudioBookStore.globalAudiobookStore.Model.NewShopAudiobookGroups
                    | OldShop -> AudioBookStore.globalAudiobookStore.Model.OldShopAudiobookGroups
                    |> Array.sortBy id
                    |> Array.sortBy (fun g ->
                        match g with
                        | "Perry Rhodan" -> 0
                        | "Perry Rhodan Neo" -> 2
                        | "Perry Rhodan Silber Edition" -> 4
                        | "Perry Rhodan Classics" -> 6
                        | x when x.StartsWith("Perry Rhodan") -> 10
                        | _ -> 1000
                    )
                    
                for group in orderedGroups do
                    FilterOptions.Group group, group, false                
                
            |]
            |> Array.map (fun (f,s, isGeneral) ->
                FilterItem(local, f, s, (this.Filter = f), isGeneral)
            )
        )





    static member DesignVM =
        new HomeViewModel(
            [|
                AudioBookItemViewModel(NewShop, DemoData.designAudioBook)
                AudioBookItemViewModel(NewShop, DemoData.designAudioBook2)
                AudioBookItemViewModel(NewShop, DemoData.designAudioBook3)
            |])

and FilterItem(local, filterOption, text, isSelected, isGeneral) =
    member this.Command () = local.Dispatch <| FilterChanged filterOption
    member val FilterOption = filterOption
    member val Text = text
    member val IsSelected = isSelected with get, set
    member val IsGeneral = isGeneral with get, set


