namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open System.Collections.ObjectModel
open System.Threading.Tasks
open CherylUI.Controls
open Common
open Dependencies
open Domain
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.ViewModel.AudioBookItem
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
        BusyMessage:string
    }


    type Msg =
        | RunOnlySideEffect of SideEffect
        | LoadOnlineAudiobooks
        | AudioBookItemsChanged of AudioBookItemViewModel []
        | SelectPreviousGroup of string
        | AddSelectGroup of string
        | RemoveLastSelectGroup of string
        | ShowErrorMessage of string
        | SetBusy of bool
        | OpenLoginPage
        | SetSearchText of string
        | SetSearchResult of AudioBookItemViewModel []
        | AppendBusyMessage of string




    and [<RequireQualifiedAccess>]
        SideEffect =
        | None
        | Init
        | LoadCurrentAudioBooks
        | LoadOnlineAudioBooks
        | OpenLoginPage
        | ShowErrorMessage of string
        | OnSelectedGroupChanged
        | StartSearch

    let disposables = new System.Collections.Generic.List<IDisposable>()

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


    let init initAudioBooks =
        let filterResult = filter [] initAudioBooks

        let newModel = {
            AudioBookItems = filterResult.CurrentVisibleAudioBooks
            IsLoading = false
            SelectedGroups = []
            SelectedGroupItems = filterResult.FilteredAudioBooks
            PreviousGroups = filterResult.PreviousAvailableGroups
            AvailableGroups = filterResult.AvailableGroups
            SearchText = ""
            BusyMessage = ""
        }

        newModel, if initAudioBooks = [||] then SideEffect.Init else SideEffect.None


    let update msg (state:State) =
        match msg with
        | RunOnlySideEffect sideEffect ->
            state, sideEffect

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
        | SetBusy bstate ->
            { state with IsLoading = bstate; BusyMessage = "" }, SideEffect.None
        | AppendBusyMessage msg ->
            { state with BusyMessage = $"{state.BusyMessage}\r\n{msg}" }, SideEffect.None

        | OpenLoginPage ->
            state, SideEffect.OpenLoginPage
        | SetSearchText s ->
            { state with SearchText = s }, SideEffect.StartSearch
        | SetSearchResult audioBookItemViewModels ->
            { state with AudioBookItems = audioBookItemViewModels }, SideEffect.None


    module SideEffects =
        open PerryRhodan.AudiobookPlayer.Common

        type SynchronizeWithCloudErrors =
            | NoSessionAvailable
            | WebError of ComError
            | StorageError of string

        let private synchronizeWithCloudCmd state dispatch =
            task {
                dispatch <| SetBusy true


                let notifyAfterSync (synchedAb:AudioBookItemViewModel []) =
                    task {
                        match synchedAb with
                        | [||] ->
                            do! Notifications.showMessage "Neue Hörbücher" $"{Translations.current.NoNewAudioBooksSinceLastRefresh} ¯\_(ツ)_/¯"
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


                let loadAudioBooksFromCloud (cookies:Map<string,string> option) =
                    task {
                        dispatch <| AppendBusyMessage "Lade verfügbare Hörbücher aus dem Shop..."
                        match cookies with
                        | None ->
                            return Error NoSessionAvailable
                        | Some cookies ->
                            let! audioBooks = WebAccess.getAudiobooksOnline cookies
                            return audioBooks |> Result.mapError WebError
                    }




                let lookForOrphanedAudiobookOnDevice
                    (modelAudioBooks:AudioBookItemViewModel[])
                    (loadFromCloud:Result<AudioBook[],SynchronizeWithCloudErrors>) =
                        match loadFromCloud with
                        | Error e -> Error e
                        | Ok cloudAudioBooks ->
                            dispatch <| AppendBusyMessage "Suche nach bereits runtergeladenen Hörbücher..."
                            let audioBooksAlreadyOnTheDevice =
                                DataBase.getAudiobooksFromDownloadFolder cloudAudioBooks
                                // remove items that are already in the model itself
                                |> Array.filter (fun i -> modelAudioBooks |> Array.exists (fun a -> a.AudioBook.Id = i.Id) |> not)


                            Ok {| OnDevice = audioBooksAlreadyOnTheDevice; InCloud = cloudAudioBooks |}


                let processLoadedAudioBookFromDevice
                    (input:Result<{| OnDevice: AudioBook []; InCloud:AudioBook[] |},SynchronizeWithCloudErrors>) =
                     task {
                         match input with
                         | Error e -> return Error e
                         | Ok e ->
                             let! _ = e.OnDevice |> DataBase.insertNewAudioBooksInStateFile
                             return {| OnDevice = e.OnDevice; InCloud = e.InCloud |} |> Ok
                     }


                let determinateNewAddedAudioBooks
                    (modelAudioBooks:AudioBookItemViewModel[])
                    (input:Result<{| OnDevice: AudioBook []; InCloud:AudioBook[] |},SynchronizeWithCloudErrors>) =
                    match input with
                    | Error e -> Error e
                    | Ok e ->
                         dispatch <| AppendBusyMessage "Ermittle neue Hörbücher..."
                         let audioBooksAlreadyOnTheDevice = e.OnDevice |> Array.map (fun i -> new AudioBookItemViewModel(i))
                         let modelAndDeviceAudiobooks = Array.concat [audioBooksAlreadyOnTheDevice; modelAudioBooks]
                         let newAudioBookItems =
                             let currentAudioBooks = modelAndDeviceAudiobooks |> Array.map (_.AudioBook)
                             filterNewAudioBooks currentAudioBooks e.InCloud
                             |> Array.map (fun i -> new AudioBookItemViewModel(i))
                         Ok {| New = newAudioBookItems; OnDevice = modelAndDeviceAudiobooks; InCloud = e.InCloud |}


                let checkIfCurrentAudiobookAreReallyDownloaded 
                    (input:Result<{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; InCloud:AudioBook[] |},SynchronizeWithCloudErrors>) =
                        match input with
                        | Error e -> Error e
                        | Ok e ->
                            dispatch <| AppendBusyMessage "Prüfe ob alle Hörbücher wirklich heruntergeladen sind..."
                            let audioBooks =
                                e.OnDevice
                                |> Array.filter (fun i -> i.DownloadState = AudioBookItem.Downloaded)
                                

                            // check on the file system if the audio books are really downloaded
                            audioBooks
                            |> Array.filter (fun i ->
                                let folder = i.AudioBook.State.DownloadedFolder
                                match folder with
                                | Some f ->
                                    System.IO.Directory.Exists(f)
                                    |> fun folderExists ->
                                        if folderExists then
                                            let audioFiles = System.IO.Directory.GetFiles(f, "*.mp3")
                                            audioFiles.Length = 0
                                        else
                                            true
                                // ignore these, who has no download folder
                                | None -> false
                            )
                            |> Array.iter (fun i -> i.SetDownloadPath None)
                            
                            Ok {| New = e.New; OnDevice = e.OnDevice; InCloud = e.InCloud |}
                    
                
                let processNewAddedAudioBooks
                    (input:Result<{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; InCloud:AudioBook[] |},SynchronizeWithCloudErrors>) =
                        task {
                             match input with
                             | Error e -> return Error e
                             | Ok i ->
                                 dispatch <| AppendBusyMessage "Speichere neue Hörbücher..."
                                 let onlyAudioBooks =
                                     i.New
                                     |> Array.map (_.AudioBook)

                                 let! ab =
                                     onlyAudioBooks
                                     |> DataBase.insertNewAudioBooksInStateFile

                                 match ab with
                                 | Error e ->
                                     return StorageError e |> Error
                                 | Ok _ ->
                                     return {| New = i.New; OnDevice = i.OnDevice; InCloud = i.InCloud |} |> Ok

                        }


                let repairAudiobookMetadataIfNeeded
                    (input:Result<{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; InCloud:AudioBook[] |},SynchronizeWithCloudErrors>) =
                    task {
                        match input with
                        | Error e ->
                            return Error e
                        | Ok e ->
                            dispatch <| AppendBusyMessage "Suche nach defekten Metadaten..."
                            let hasDiffMetaData a b =
                                a.FullName <> b.FullName ||
                                a.EpisodeNo <> b.EpisodeNo ||
                                a.EpisodenTitel <> b.EpisodenTitel ||
                                a.Group <> b.Group

                            let folders = Services.Consts.createCurrentFolders ()


                            let repairedAudioBooksItem =
                                e.OnDevice
                                |> Array.choose (fun i ->
                                    e.InCloud
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
                                e.OnDevice
                                |> Array.choose (fun i ->
                                    e.InCloud
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
                                return {| New = e.New; OnDevice = e.OnDevice; DifferNames = nameDiffOldNewDownloaded |} |> Ok
                                //return (newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded) |> Ok
                            | _ ->

                                for i in repairedAudioBooksItem do
                                    let! result = DataBase.updateAudioBookInStateFile i.AudioBook
                                    ()

                                // replacing fixed entries
                                let currentAudioBooks =
                                    e.OnDevice
                                    |> Array.map (fun c ->
                                        match repairedAudioBooksItem |> Array.tryFind (fun r -> c.AudioBook.Id = r.AudioBook.Id) with
                                        | None ->
                                            c
                                        | Some r ->
                                            r
                                    )

                                return {| New = e.New; OnDevice = currentAudioBooks; DifferNames = nameDiffOldNewDownloaded |} |> Ok
                                //return (newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded) |> Ok

                     }


                let fixDownloadFolders
                    (input:Result<{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; DifferNames: {| OldName:string; NewName:string |} array |},SynchronizeWithCloudErrors>) =
                        match input with
                        | Error e -> Error e
                        | Ok e ->
                            dispatch <| AppendBusyMessage "Repariere mögliche Probleme mit den Downloadordnern..."
                            let folders = Services.Consts.createCurrentFolders ()
                            try
                                e.DifferNames
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

                                //return (newAudioBookItems, currentAudioBooks) |> Ok
                                {| New = e.New; OnDevice = e.OnDevice |} |> Ok
                            with
                            | ex ->
                                (StorageError ex.Message) |> Error





                let processResult
                    (input:Result<{| New : AudioBookItemViewModel[]; OnDevice : AudioBookItemViewModel[] |} ,SynchronizeWithCloudErrors>) =
                    task {
                        match input with
                        | Ok e ->
                            dispatch <| AppendBusyMessage "Fertig!"
                            let audioBooks =(Array.concat [e.New;e.OnDevice])
                            // also sync with global store
                            AudioBookStore.globalAudiobookStore.Dispatch <| AudioBookStore.AudioBookElmish.AudiobooksLoaded audioBooks
                            dispatch <| AudioBookItemsChanged audioBooks
                            do! Task.Delay 1000
                            // start picture download background service
                            DependencyService.Get<IPictureDownloadService>().StartDownload()
                            do! e.New |> notifyAfterSync

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

                try
                    do!
                        checkLoginSession ()
                        |> Task.bind loadAudioBooksFromCloud
                        |> Task.map (lookForOrphanedAudiobookOnDevice AudioBookStore.globalAudiobookStore.Model.Audiobooks)
                        |> Task.bind processLoadedAudioBookFromDevice
                        |> Task.map (determinateNewAddedAudioBooks AudioBookStore.globalAudiobookStore.Model.Audiobooks)
                        |> Task.map  checkIfCurrentAudiobookAreReallyDownloaded
                        |> Task.bind processNewAddedAudioBooks
                        |> Task.bind repairAudiobookMetadataIfNeeded
                        |> Task.map  fixDownloadFolders
                        |> Task.bind processResult
                with
                | ex ->
                    do! Notifications.showErrorMessage ex.Message



                dispatch <| SetBusy false

            }


        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            let loadCurrentAudiobooks () =
                let audioBooks =
                        AudioBookStore.globalAudiobookStore.Model.Audiobooks
                dispatch <| AudioBookItemsChanged audioBooks



            task {
                if sideEffect = SideEffect.None then
                    return ()
                else
                    dispatch <| SetBusy true
                    do!
                        task {
                            match sideEffect with
                            | SideEffect.None ->
                                return ()

                            | SideEffect.Init ->
                                // listen to global audiobook store
                                disposables.Add
                                    <| AudioBookStore.globalAudiobookStore.Observable.Subscribe(fun m ->
                                        dispatch <| AudioBookItemsChanged m.Audiobooks
                                    )

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
                                let selectedGroups = state.SelectedGroups
                                match selectedGroups with
                                | [] ->
                                    navService.ResetBackbuttonPressed()
                                | _ ->
                                    navService.RegisterBackbuttonPressed (fun () -> dispatch (RemoveLastSelectGroup (List.last selectedGroups)))

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

                    dispatch <| SetBusy false
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
            AmbientColor = Some "#FF0000"
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
            AmbientColor = Some "#00FF00"
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
            AmbientColor = Some "#0000FF"
        }


type BrowserViewModel(?audiobookItems) as self =
    inherit ReactiveElmishViewModel()

    let audiobookItems = [||] |> defaultArg audiobookItems
    let init() = init audiobookItems
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore

    let searchStringDebouncer = Extensions.debounce<string>

    do
        do
            self.AddDisposable
                <| AudioBookStore.globalAudiobookStore.Observable.Subscribe(fun s ->
                    local.Dispatch (s.IsLoading |> SetBusy)
                )
            ()

    new () =
        new BrowserViewModel([||])

    member this.AudioBooks =
        this.Bind(local, fun s -> ObservableCollection(s.AudioBookItems))

    member this.PreviousGroups:string list  = this.Bind(local, _.PreviousGroups)
    member this.AvailableGroups:string list = this.Bind(local, _.AvailableGroups)
    member this.SelectedGroups:string list  = this.Bind(local, _.SelectedGroups)
    member this.GroupItems                  = this.BindList(local, fun s -> ObservableCollection(match s.SelectedGroupItems with | GroupList grp -> grp | _ -> [||]))
    member this.IsBusy                      = this.BindOnChanged(local, _.IsLoading, _.IsLoading)
    member this.BusyMessage                 = this.BindOnChanged(local, _.BusyMessage, _.BusyMessage)

    member this.BackButtonVisible           = this.Bind(local, fun s -> s.SelectedGroups.Length > 0)
    member this.AudioBookItemsVisible       = this.Bind(local, fun s -> s.AudioBookItems.Length > 0)
    member this.CategoryItemsVisible =
        this.Bind(local, fun s ->
            match s.SelectedGroupItems with
            | GroupList _ -> true
            | _ -> false

        )

    member this.IsEmpty = this.Bind(local, fun s -> s.AudioBookItems.Length = 0 && s.AvailableGroups.Length = 0)

    member this.SearchText
        with get() = "nix"
        and set(value) =
            searchStringDebouncer 1000 (fun s ->
                // protect against null
                let s = if s = null then "" else s
                local.Dispatch <| SetSearchText s
            ) value




    member this.OnInitialized() = local.Dispatch <| RunOnlySideEffect SideEffect.LoadCurrentAudioBooks
    member this.LoadOnlineAudiobooks() = local.Dispatch LoadOnlineAudiobooks
    member this.SelectPreviousGroup(group:string) = local.Dispatch (SelectPreviousGroup group)
    member this.SelectAdditionalGroup(group:string) = local.Dispatch (AddSelectGroup group)
    member this.RemoveLastSelectedGroup () =
        match this.SelectedGroups with
        | [] ->
            ()
        | _ ->
            local.Dispatch <| RemoveLastSelectGroup (local.Model.SelectedGroups |> List.last)

    member this.OnLoaded() =
        // always set backbutton, when enter this page
        let navService = DependencyService.Get<INavigationService>()
        if navService <> Unchecked.defaultof<_> then // because of design time
            let selectedGroups = local.Model.SelectedGroups
            match selectedGroups with
            | [] ->
                navService.ResetBackbuttonPressed()
            | _ ->
                navService.RegisterBackbuttonPressed (fun () -> local.Dispatch (RemoveLastSelectGroup (List.last selectedGroups)))

    member this.GoBackHome() =
        DependencyService.Get<IMainViewModel>().GotoHomePage()


    static member DesignVM =
        new BrowserViewModel(
            [|
                AudioBookItemViewModel(DemoData.designAudioBook)
                AudioBookItemViewModel(DemoData.designAudioBook2)
                AudioBookItemViewModel(DemoData.designAudioBook3)
            |])
