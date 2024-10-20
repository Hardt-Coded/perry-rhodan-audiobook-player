namespace PerryRhodan.AudiobookPlayer.ViewModels

open System.Collections.ObjectModel
open System.Threading.Tasks
open CherylUI.Controls
open Common
open Dependencies
open Domain
open PerryRhodan.AudiobookPlayer.ViewModel
open Services
open Services.DependencyServices


module BrowserPage =
    
    type State = {
        AudioBookItems: AudioBookItemViewModel []
        PreviousGroups: string list
        AvailableGroups: string list
        SelectedGroups:string list
        SelectedGroupItems: AudioBookListType
        IsLoading:bool
        SearchText:string
    }


    type Msg = 
        | LoadOnlineAudiobooks
        | AudioBookItemsChanged of AudioBookItemViewModel []
        | SelectPreviousGroup of string
        | AddSelectGroup of string
        | RemoveLastSelectGroup of string
        | ShowErrorMessage of string
        | ChangeBusyState of bool
        | OpenLoginPage
        | SetSearchText of string
        | SetSearchResult of AudioBookItemViewModel []


    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | LoadCurrentAudioBooks
        | LoadOnlineAudioBooks
        | OpenLoginPage
        | ShowErrorMessage of string
        | OnSelectedGroupChanged
        | StartSearch
    


    let filter selectedGroups (audioBookItems:AudioBookItemViewModel []) =
        
        //let selectedGroups = [ "Perry Rhodan"; "Perry Rhodan 3000 - 3000" ]
        
        let audioBooks = 
            audioBookItems 
            |> Array.map (_.AudioBook)

        let groupedAudioBooks = audioBooks |> Filters.nameFilter

        // filter audio books
        let filteredAb =
            match selectedGroups with
            | [] -> 
                (GroupList groupedAudioBooks)
            | _ ->                
                ((GroupList groupedAudioBooks) |> Filters.groupsFilter selectedGroups)

        let audioBooks =
            match filteredAb with
            | GroupList _ -> [||]
            | AudioBookList (_, ab) ->
                audioBookItems
                |> Array.filter (fun i -> ab |> Array.exists (fun a -> a.Id = i.AudioBook.Id))
           
        {|
            FilteredAudioBooks = filteredAb
            CurrentVisibleAudioBooks = audioBooks
            PreviousAvailableGroups = groupedAudioBooks |> Array.map fst |> Array.toList
            AvailableGroups =
                match filteredAb with
                | GroupList grp -> grp |> Array.map fst |> Array.toList
                | _ -> []
        |}     
        

    let init selectedGroups initAudioBooks = 
        let filterResult = filter [] initAudioBooks
        
        let newModel = {
            AudioBookItems = filterResult.CurrentVisibleAudioBooks
            IsLoading = false
            SelectedGroups = selectedGroups
            SelectedGroupItems = filterResult.FilteredAudioBooks
            PreviousGroups = filterResult.PreviousAvailableGroups 
            AvailableGroups = filterResult.AvailableGroups
            SearchText = ""
        }
    
        newModel, if initAudioBooks = [||] then SideEffect.LoadCurrentAudioBooks else SideEffect.None


    let update msg (state:State) =
        match msg with
        | LoadOnlineAudiobooks ->
            state, SideEffect.LoadOnlineAudioBooks
            
        | AudioBookItemsChanged audioBooks ->
            let filterResult = filter state.SelectedGroups audioBooks
            { state with
                AudioBookItems = filterResult.CurrentVisibleAudioBooks
                IsLoading = false
                SelectedGroupItems = filterResult.FilteredAudioBooks
                PreviousGroups = filterResult.PreviousAvailableGroups 
                AvailableGroups = filterResult.AvailableGroups }, SideEffect.None
            
        | SelectPreviousGroup group ->
            { state with SelectedGroups = [group] }, SideEffect.LoadCurrentAudioBooks
        | AddSelectGroup group ->
            { state with SelectedGroups = state.SelectedGroups @ [group] }, SideEffect.OnSelectedGroupChanged
            
        | RemoveLastSelectGroup groupName ->
            { state with SelectedGroups = state.SelectedGroups |> List.filter (fun i -> i <> groupName) }, SideEffect.OnSelectedGroupChanged
            
        | ShowErrorMessage e ->
            state, SideEffect.ShowErrorMessage e
        | ChangeBusyState bstate -> 
            { state with IsLoading = bstate}, SideEffect.None
        | OpenLoginPage ->
            state, SideEffect.OpenLoginPage
        | SetSearchText s ->
            { state with SearchText = s }, SideEffect.StartSearch
        | SetSearchResult audioBookItemViewModels ->
            { state with AudioBookItems = audioBookItemViewModels }, SideEffect.None
        
        
    module SideEffects =
        open Common
        
        type SynchronizeWithCloudErrors =
            | NoSessionAvailable
            | WebError of ComError
            | StorageError of string
        
        let private synchronizeWithCloudCmd state dispatch =
            task {
                dispatch <| ChangeBusyState true
                
                
                let notifyAfterSync (synchedAb:AudioBookItemViewModel []) =
                    task {
                        match synchedAb with
                        | [||] ->
                            do! Notifications.showMessage Translations.current.NoNewAudioBooksSinceLastRefresh " ¯\_(ツ)_/¯"
                        | _ ->
                            let message = synchedAb |> Array.map (_.AudioBook.FullName) |> String.concat "\r\n"
                            do! Notifications.showMessage Translations.current.NewAudioBooksSinceLastRefresh message 
                    }
                
                
                let checkLoginSession () =
                    task {
                        let! cookies = SecureLoginStorage.loadCookie ()
                        match cookies with
                        | Ok (Some cc) ->
                            return Some cc
                        | Error e ->
                            do! Notifications.showErrorMessage e
                            return None
                        | Ok None ->
                            return None
                    }
                    
                
                let loadAudioBooksFromCloud (cookies:Task<Map<string,string> option>) =
                    task {
                        let! cookies = cookies
                        match cookies with
                        | None ->
                            return Error NoSessionAvailable
                        | Some cookies ->
                            let! audioBooks = WebAccess.getAudiobooksOnline cookies
                            return audioBooks |> Result.mapError WebError
                    }

                
                let loadAudioBooksFromDevice 
                    (modelAudioBooks:AudioBookItemViewModel[])
                    (loadResult:Task<Result<AudioBook[],SynchronizeWithCloudErrors>>) =
                    task {
                        let! cloudAudioBooks = loadResult
                        
                        match cloudAudioBooks with
                        | Error e -> return Error e
                        | Ok cloudAudioBooks ->
                            let audioBooksAlreadyOnTheDevice =
                                DataBase.getAudiobooksFromDownloadFolder cloudAudioBooks
                                // remove items that are already in the model itself
                                |> Array.filter (fun i -> modelAudioBooks |> Array.exists (fun a -> a.AudioBook.Id = i.Id) |> not)
                                
                            
                            return Ok (audioBooksAlreadyOnTheDevice,cloudAudioBooks)
                    }

                
                let processLoadedAudioBookFromDevice 
                    (input:Task<Result<AudioBook [] * AudioBook[],SynchronizeWithCloudErrors>>) =
                    task {
                        let! input = input
                        match input with
                        | Error e -> return Error e
                        | Ok (audioBooksItemsAlreadyOnTheDevice, cloudAudioBooks) ->
                            let _ = audioBooksItemsAlreadyOnTheDevice |> DataBase.insertNewAudioBooksInStateFile

                            return (audioBooksItemsAlreadyOnTheDevice, cloudAudioBooks) |> Ok
                    }

                
                let determinateNewAddedAudioBooks 
                    (modelAudioBooks:AudioBookItemViewModel[])
                    (input:Task<Result<AudioBook [] * AudioBook[],SynchronizeWithCloudErrors>>) =
                    task {
                        let! input = input
                        match input with
                        | Error e -> return Error e
                        | Ok (audioBooksAlreadyOnTheDevice, cloudAudioBooks) ->
                            let audioBooksAlreadyOnTheDevice = audioBooksAlreadyOnTheDevice |> Array.map (fun i -> new AudioBookItemViewModel(i))
                            let modelAndDeviceAudiobooks = Array.concat [audioBooksAlreadyOnTheDevice; modelAudioBooks]
                            let newAudioBookItems =
                                let currentAudioBooks = modelAndDeviceAudiobooks |> Array.map (_.AudioBook)
                                filterNewAudioBooks currentAudioBooks cloudAudioBooks
                                |> Array.map (fun i -> new AudioBookItemViewModel(i))
                            return Ok (newAudioBookItems, audioBooksAlreadyOnTheDevice, cloudAudioBooks)
                    }


                let processNewAddedAudioBooks 
                    (input:Task<Result<AudioBookItemViewModel [] * AudioBookItemViewModel [] * AudioBook[],SynchronizeWithCloudErrors>>) =
                    task {
                        let! input = input
                        match input with
                        | Error e -> return Error e
                        | Ok (newAudioBookItems,currentAudioBooks,cloudAudioBooks) ->
                            let onlyAudioBooks = 
                                newAudioBookItems 
                                |> Array.map (_.AudioBook)

                            let! ab =
                                onlyAudioBooks 
                                |> DataBase.insertNewAudioBooksInStateFile 
                                
                            match ab with
                            | Error e ->
                                return StorageError e |> Error
                            | Ok _ ->
                                return (newAudioBookItems, currentAudioBooks, cloudAudioBooks) |> Ok
                        
                    }

                
                let repairAudiobookMetadataIfNeeded
                    (input:Task<Result<AudioBookItemViewModel [] * AudioBookItemViewModel [] * AudioBook [],SynchronizeWithCloudErrors>>) =
                    task {
                        let! input = input
                        match input with
                        | Error e ->
                            return Error e
                        | Ok (newAudioBookItems,currentAudioBooks,cloudAudioBooks) ->

                            let hasDiffMetaData a b =
                                a.FullName <> b.FullName ||
                                a.EpisodeNo <> b.EpisodeNo ||
                                a.EpisodenTitel <> b.EpisodenTitel ||
                                a.Group <> b.Group

                            let folders = Services.Consts.createCurrentFolders ()


                            let repairedAudioBooksItem =
                                currentAudioBooks
                                |> Array.choose (fun i ->
                                    cloudAudioBooks 
                                    |> Array.tryFind (fun c -> c.Id = i.AudioBook.Id)
                                    |> Option.bind (fun c -> 
                                        if hasDiffMetaData c i.AudioBook then
                                            let newAudioBookFolder = System.IO.Path.Combine(folders.audioBookDownloadFolderBase, c.FullName)
                                            
                                            let opt predicate input =
                                                if predicate then
                                                    Some input
                                                else
                                                    None

                                            let newAb = { 
                                                i.AudioBook with 
                                                    FullName = c.FullName
                                                    EpisodeNo = c.EpisodeNo
                                                    EpisodenTitel = c.EpisodenTitel
                                                    Group = c.Group
                                                    Thumbnail = System.IO.Path.Combine(newAudioBookFolder, c.FullName + ".thumb.jpg") |> opt i.AudioBook.State.Downloaded 
                                                    Picture =   System.IO.Path.Combine(newAudioBookFolder, c.FullName + ".jpg") |> opt i.AudioBook.State.Downloaded 
                                                    State = {
                                                        i.AudioBook.State with
                                                            DownloadedFolder = System.IO.Path.Combine(newAudioBookFolder,"audio") |> opt i.AudioBook.State.Downloaded 
                                                    }
                                            }
                                            // we need to generate a new Item, because the dispatch itself contains also the audiobook data
                                            let newItem = 
                                                new AudioBookItemViewModel(newAb)
                                                
                                            Some newItem
                                        else
                                            None
                                    )
                                )

                            let nameDiffOldNewDownloaded =
                                currentAudioBooks
                                |> Array.choose (fun i ->
                                    cloudAudioBooks 
                                    |> Array.tryFind (fun c -> c.Id = i.AudioBook.Id && i.DownloadState = AudioBookItem.Downloaded)
                                    |> Option.bind (fun c -> 
                                        if c.FullName <> i.AudioBook.FullName then
                                            Some {| OldName = i.AudioBook.FullName; NewName = c.FullName |}
                                        else
                                            None
                                    )
                                )

                            

                            match repairedAudioBooksItem with
                            | [||] ->
                                return (newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded) |> Ok
                            | _ ->
                                
                                for i in repairedAudioBooksItem do
                                    let! result = DataBase.updateAudioBookInStateFile i.AudioBook
                                    ()

                                // replacing fixed entries
                                let currentAudioBooks =
                                    currentAudioBooks
                                    |> Array.map (fun c ->
                                        match repairedAudioBooksItem |> Array.tryFind (fun r -> c.AudioBook.Id = r.AudioBook.Id) with
                                        | None ->
                                            c
                                        | Some r ->
                                            r
                                    )

                                return (newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded) |> Ok
                        
                    }

                
                let fixDownloadFolders 
                    (input:Task<Result<AudioBookItemViewModel [] * AudioBookItemViewModel [] * {| OldName:string; NewName:string |} [],SynchronizeWithCloudErrors>>) =
                    task {
                        let! input = input
                        match input with
                        | Error e -> return Error e
                        | Ok (newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded) ->
                            let folders = Services.Consts.createCurrentFolders ()
                            try
                                nameDiffOldNewDownloaded
                                |> Array.map (fun x -> 
                                    let oldFolder = System.IO.Path.Combine(folders.audioBookDownloadFolderBase,x.OldName)
                                    let newFolder = System.IO.Path.Combine(folders.audioBookDownloadFolderBase,x.NewName)
                                    {| 
                                        OldFolder =     oldFolder
                                        NewFolder =     newFolder

                                        OldPicName =    System.IO.Path.Combine(oldFolder, x.OldName + ".jpg")
                                        NewPicName =    System.IO.Path.Combine(oldFolder, x.NewName + ".jpg")
                                        OldThumbName =  System.IO.Path.Combine(oldFolder, x.OldName + ".thumb.jpg")
                                        NewThumbName =  System.IO.Path.Combine(oldFolder, x.NewName + ".thumb.jpg")
                                    |} 
                                )
                                |> Array.iter (fun x ->
                                    System.IO.File.Move(x.OldPicName,x.NewPicName)
                                    System.IO.File.Move(x.OldThumbName,x.NewThumbName)
                                    System.IO.Directory.Move(x.OldFolder,x.NewFolder)
                                )
                                
                                return (newAudioBookItems, currentAudioBooks) |> Ok
                            with
                            | ex ->
                                return (StorageError ex.Message) |> Error

                            
                    }


                let processResult 
                    (input:Task<Result<AudioBookItemViewModel [] * AudioBookItemViewModel [] ,SynchronizeWithCloudErrors>>) =
                    task {
                        let! input = input
                        match input with
                        | Ok (newAudioBookItems,currentAudioBooks) ->
                            do! newAudioBookItems |> notifyAfterSync
                            let audioBooks =(Array.concat [newAudioBookItems;currentAudioBooks])
                            dispatch <| AudioBookItemsChanged audioBooks
                            // also sync with global store
                            AudioBookStore.globalAudiobookStore.Dispatch (AudioBookStore.AudioBookElmish.AudiobooksLoaded audioBooks)
                        | Error err ->
                            match err with
                            | NoSessionAvailable ->
                                dispatch <| OpenLoginPage

                            | WebError comError ->
                                match comError with
                                | SessionExpired e -> 
                                    dispatch <| OpenLoginPage
                                    
                                | Other e -> 
                                    do! Notifications.showErrorMessage e
                                    
                                | Exception e ->
                                    let ex = e.GetBaseException()
                                    let msg = ex.Message + "|" + ex.StackTrace
                                    do! Notifications.showErrorMessage msg

                                | Network msg ->
                                    do! Notifications.showErrorMessage msg

                            | StorageError msg ->
                                do! Notifications.showErrorMessage msg
                    }

                do!
                    checkLoginSession ()
                    |> loadAudioBooksFromCloud
                    |> loadAudioBooksFromDevice state.AudioBookItems
                    |> processLoadedAudioBookFromDevice
                    |> determinateNewAddedAudioBooks state.AudioBookItems
                    |> processNewAddedAudioBooks
                    |> repairAudiobookMetadataIfNeeded
                    |> fixDownloadFolders
                    |> processResult


                dispatch <| ChangeBusyState false
                
                    }
                
                
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            let loadCurrentAudiobooks () =
                let audioBooks =
                        AudioBookStore.globalAudiobookStore.Model.Audiobooks
                dispatch <| AudioBookItemsChanged audioBooks
            
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()
                | SideEffect.LoadCurrentAudioBooks ->
                    loadCurrentAudiobooks ()
                    return ()
                    
                | SideEffect.LoadOnlineAudioBooks ->
                    do! synchronizeWithCloudCmd state dispatch
                    
                    return ()
                    
                | SideEffect.OpenLoginPage ->
                    let control = PerryRhodan.AudiobookPlayer.Views.LoginView()
                    let vm = new LoginViewModel()
                    control.DataContext <- vm
                    InteractiveContainer.ShowDialog (control, true)
                    return ()
                    
                | SideEffect.ShowErrorMessage e ->
                    do! Notifications.showErrorMessage e
                    return ()

                | SideEffect.OnSelectedGroupChanged ->
                    loadCurrentAudiobooks ()
                    let navService = DependencyService.Get<INavigationService>()
                    match state.SelectedGroups with
                    | [] ->
                        navService.ResetBackbuttonPressed()
                    | _ ->
                        navService.RegisterBackbuttonPressed (fun () -> dispatch (RemoveLastSelectGroup (List.last state.SelectedGroups)))
                    
                    return ()

                | SideEffect.StartSearch ->
                    if state.SearchText = "" then
                        loadCurrentAudiobooks ()
                    else
                        // search for audio books with the given search text
                        let filteredAudioBooks =
                            AudioBookStore.globalAudiobookStore.Model.Audiobooks
                            |> Array.filter (_.AudioBook.FullName.ToLower().Contains(state.SearchText.ToLower()))
                            
                        dispatch <| Msg.SetSearchResult filteredAudioBooks
                        
                     
                    
            }

open BrowserPage
open ReactiveElmish.Avalonia
open ReactiveElmish
open Elmish.SideEffect

module private DemoData =
    let designAudioBook = {
            Id = 1
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
        }

    let designAudioBook2 = {
            Id = 2
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
        }
    
    let designAudioBook3 = {
            Id = 3
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
        }


type BrowserViewModel(selectedGroups, audiobookItems) =
    inherit ReactiveElmishViewModel()
    
    
    let init() = init selectedGroups audiobookItems 
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
        
        
    let searchStringDebouncer = Extensions.debounce<string>
    
    new(selectedGroups) = 
        new BrowserViewModel(selectedGroups, [||])
      
    member this.AudioBooks =
        this.Bind(local, fun s -> ObservableCollection(s.AudioBookItems))
    
    member this.PreviousGroups:string list = this.Bind(local, _.PreviousGroups)
    member this.AvailableGroups:string list = this.Bind(local, _.AvailableGroups)
    member this.SelectedGroups:string list = this.Bind(local, _.SelectedGroups)
    member this.GroupItems = this.Bind(local, fun s -> ObservableCollection(match s.SelectedGroupItems with | GroupList grp -> grp | _ -> [||]))
    member this.IsLoading:bool = this.Bind(local, _.IsLoading)
    member this.HasAudioBooks = this.Bind(local, fun s -> s.AudioBookItems.Length > 0)
    
    member this.SearchText
        with get() = "nix"
        and set(value) =
            searchStringDebouncer 1000 (fun s ->
                local.Dispatch <| SetSearchText s
            ) value
            
            
        
    member this.LoadOnlineAudiobooks() = local.Dispatch LoadOnlineAudiobooks
    member this.SelectPreviousGroup(group:string) = local.Dispatch (SelectPreviousGroup group)
    member this.SelectAdditionalGroup(group:string) = local.Dispatch (AddSelectGroup group)
    static member DesignVM =
        new BrowserViewModel(
            [],
            [|
                AudioBookItemViewModel(DemoData.designAudioBook)
                AudioBookItemViewModel(DemoData.designAudioBook2)
                AudioBookItemViewModel(DemoData.designAudioBook3)
            |])            
