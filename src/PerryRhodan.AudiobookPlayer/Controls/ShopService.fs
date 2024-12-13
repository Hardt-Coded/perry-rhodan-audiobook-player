namespace PerryRhodan.AudiobookPlayer.Controls

open System.Threading.Tasks
open Common
open Dependencies
open Domain
open FsToolkit.ErrorHandling
open Microsoft.ApplicationInsights.DataContracts
open PerryRhodan.AudiobookPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open Services


module ShopService =

    type SynchronizeWithCloudErrors =
        | NoSessionAvailable
        | WebError of ComError
        | StorageError of string


    let private synchronizeWithCloud
        (shop: Shop)
        getCurrentAudiobooks
        getAudiobooksOnline
        insertNewAudioBooksInStateFile
        updateAudioBookInStateFile
        showMessage
        showErrorMessage
        (loadCookie: unit -> Task<Result<Map<string,string> option, string>>)
        appendMessage
        openLogin
        onSuccess =
            task {


                let notifyAfterSync (synchedAb:AudioBookItemViewModel []) =
                    task {
                        match synchedAb with
                        | [||] ->
                            do! showMessage "Neue Hörbücher" $"{Translations.current.NoNewAudioBooksSinceLastRefresh} ¯\_(ツ)_/¯"
                        | _ ->
                            let message = synchedAb |> Array.map (_.AudioBook.FullName) |> String.concat "\r\n"
                            do! showMessage Translations.current.NewAudioBooksSinceLastRefresh message
                    }


                let checkLoginSession () =
                    taskResult {
                        let! cookies =
                            loadCookie ()
                            |> Task.map (fun i -> i |> Result.mapError (fun _ -> NoSessionAvailable))
                        return cookies
                    }


                let loadAudioBooksFromCloud (cookies:Map<string,string> option) =
                    taskResult {
                        appendMessage "Lade verfügbare Hörbücher aus dem Shop..."
                        match cookies with
                        | None ->
                            return! Error NoSessionAvailable
                        | Some cookies ->
                            let! audioBooks =
                                getAudiobooksOnline cookies
                                |> Task.map (fun i -> i |> Result.mapError WebError)
                            let debugAudioBooks = audioBooks |> Array.Parallel.map (fun i -> $"%A{i}")
                            return audioBooks
                    }




                let lookForOrphanedAudiobookOnDevice
                    (getCurrentAudiobooks:unit -> AudioBookItemViewModel array)
                    (cloudAudioBooks:AudioBook array) =
                        appendMessage "Suche nach bereits runtergeladenen Hörbücher..."
                        let audioBooksAlreadyOnTheDevice : AudioBook [] =
                            DataBaseCommon.getAudiobooksFromDownloadFolder cloudAudioBooks
                            // remove items that are already in the model itself
                            |> Array.filter (fun i -> getCurrentAudiobooks () |> Array.exists (fun a -> a.AudioBook.Id = i.Id) |> not)
                        {| OnDevice = audioBooksAlreadyOnTheDevice; InCloud = cloudAudioBooks |}





                let processLoadedAudioBookFromDevice
                    (input:{| OnDevice: AudioBook []; InCloud:AudioBook[] |}) =
                    taskResult {
                        let! _ =
                            input.OnDevice |> insertNewAudioBooksInStateFile
                            |> Task.map (fun i -> i |> Result.mapError StorageError)
                        return {| OnDevice = input.OnDevice; InCloud = input.InCloud |}
                     }



                let determinateNewAddedAudioBooks
                    (getCurrentAudioBooks:unit -> AudioBookItemViewModel[])
                    (input:{| OnDevice: AudioBook []; InCloud:AudioBook[] |}) =
                        appendMessage "Ermittle neue Hörbücher..."
                        let audioBooksAlreadyOnTheDevice = input.OnDevice |> Array.map (fun i -> new AudioBookItemViewModel(shop, i))
                        let modelAndDeviceAudiobooks =
                         Array.concat [audioBooksAlreadyOnTheDevice; getCurrentAudioBooks() ]
                         |> Array.distinctBy (_.AudioBook.Id)

                        let newAudioBookItems =
                         let currentAudioBooks = modelAndDeviceAudiobooks |> Array.map (_.AudioBook)
                         filterNewAudioBooks currentAudioBooks input.InCloud
                         |> Array.map (fun i -> new AudioBookItemViewModel(shop, i))
                        {| New = newAudioBookItems; OnDevice = modelAndDeviceAudiobooks; InCloud = input.InCloud |}


                let checkIfCurrentAudiobookAreReallyDownloaded
                    (input:{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; InCloud:AudioBook[] |}) =
                        appendMessage "Prüfe ob alle Hörbücher wirklich heruntergeladen sind..."
                        let audioBooks =
                            input.OnDevice
                            |> Array.filter (fun i -> i.DownloadState = AudioBookItem.Downloaded)


                        // check on the file system if the audiobooks are really downloaded
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
                        |> Array.iter (fun i ->
                            i.SetDownloadPath None
                            // also remove orphant pictures
                            i.SetPicture None None
                        )

                        {| New = input.New; OnDevice = input.OnDevice; InCloud = input.InCloud |}

                let removeOrphanPictures
                    (input:{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; InCloud:AudioBook[] |}) =
                        appendMessage "Entferne verwaiste Bilder..."
                        input.OnDevice
                        |> Array.filter
                            (fun i ->
                            i.AudioBook.Picture
                            |> Option.map (fun p -> p.StartsWith("http") |> not)
                            |> Option.defaultValue false)
                        |> Array.iter (fun i ->
                            i.AudioBook.Picture
                            |> Option.iter (fun p ->
                                if (p.StartsWith "http" |> not) &&  (System.IO.File.Exists(p) |> not) then
                                    i.SetPicture None None
                            )
                        )
                        {| New = input.New; OnDevice = input.OnDevice; InCloud = input.InCloud |}


                let processNewAddedAudioBooks
                    (input:{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; InCloud:AudioBook[] |}) =
                        taskResult {
                             appendMessage "Speichere neue Hörbücher..."
                             let onlyAudioBooks =
                                 input.New
                                 |> Array.map (_.AudioBook)

                             let! _ =
                                 onlyAudioBooks
                                 |> insertNewAudioBooksInStateFile
                                 |> Task.map (fun i -> i |> Result.mapError StorageError)

                             return {| New = input.New; OnDevice = input.OnDevice; InCloud = input.InCloud |}

                        }


                let repairAudiobookMetadataIfNeeded
                    (input:{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; InCloud:AudioBook[] |}) =
                    taskResult {
                        appendMessage "Suche nach defekten Metadaten..."
                        let hasDiffMetaData a b =
                            a.FullName <> b.FullName ||
                            a.EpisodeNo <> b.EpisodeNo ||
                            a.EpisodenTitel <> b.EpisodenTitel ||
                            a.Group <> b.Group

                        let folders = Consts.createCurrentFolders ()


                        let repairedAudioBooksItem =
                            input.OnDevice
                            |> Array.choose (fun i ->
                                input.InCloud
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
                                            new AudioBookItemViewModel(shop,newAb)

                                        Some newItem
                                    else
                                        None
                                )
                            )

                        let nameDiffOldNewDownloaded =
                            input.OnDevice
                            |> Array.choose (fun i ->
                                input.InCloud
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
                            return {| New = input.New; OnDevice = input.OnDevice; DifferNames = nameDiffOldNewDownloaded |}
                            //return (newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded) |> Ok
                        | _ ->

                            for i in repairedAudioBooksItem do
                                let! _ =
                                    updateAudioBookInStateFile i.AudioBook
                                    |> Task.map (fun i -> i |> Result.mapError StorageError)
                                ()

                            // replacing fixed entries
                            let currentAudioBooks =
                                input.OnDevice
                                |> Array.map (fun c ->
                                    repairedAudioBooksItem
                                    |> Array.tryFind (fun r -> c.AudioBook.Id = r.AudioBook.Id)
                                    |> Option.defaultValue c
                                )

                            return {| New = input.New; OnDevice = currentAudioBooks; DifferNames = nameDiffOldNewDownloaded |}
                     }


                let fixDownloadFolders
                    (input:{| New: AudioBookItemViewModel[]; OnDevice: AudioBookItemViewModel []; DifferNames: {| OldName:string; NewName:string |} array |}) =
                    result {
                        appendMessage "Repariere mögliche Probleme mit den Downloadordnern..."
                        let folders = Consts.createCurrentFolders ()
                        try
                            input.DifferNames
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
                            return {| New = input.New; OnDevice = input.OnDevice |}
                        with
                        | ex ->
                            return! (StorageError ex.Message) |> Error
                    } |> TaskResult.ofResult




                let processResult
                    (input:Result<{| New : AudioBookItemViewModel[]; OnDevice : AudioBookItemViewModel[] |} ,SynchronizeWithCloudErrors>) =
                    task {
                        match input with
                        | Ok e ->
                            appendMessage "Fertig!"
                            let audioBooks =
                                (Array.concat [e.New;e.OnDevice])
                                |> Array.sortBy (_.AudioBook.FullName)

                            onSuccess audioBooks
                            // also sync with global store
                            //AudioBookStore.globalAudiobookStore.Value.Dispatch <| AudioBookStore.AudioBookElmish.AudiobooksLoaded audioBooks
                            //dispatch <| AudioBookItemsChanged audioBooks
                            // start picture download background service
                            //DependencyService.Get<IPictureDownloadService>().StartDownload()
                            do! e.New |> notifyAfterSync

                        | Error err ->
                            match err with
                            | NoSessionAvailable ->
                                openLogin ()

                            | WebError comError ->
                                match comError with
                                | SessionExpired _ ->
                                    Global.telemetryClient.TrackEvent("SessionExpired")
                                    do! showErrorMessage "Login ist abgelaufen, bitte neu einloggen!"
                                    openLogin ()

                                | Other e ->
                                    Global.telemetryClient.TrackTrace(e, SeverityLevel.Error)
                                    do! showErrorMessage e

                                | Exception e ->
                                    let ex = e.GetBaseException()
                                    let msg = ex.Message + "|" + ex.StackTrace
                                    Global.telemetryClient.TrackTrace(msg, SeverityLevel.Error)
                                    do! showErrorMessage msg

                                | Network msg ->
                                    Global.telemetryClient.TrackTrace(msg, SeverityLevel.Error)
                                    do! showErrorMessage msg

                            | StorageError msg ->
                                Global.telemetryClient.TrackTrace(msg, SeverityLevel.Error)
                                do! showErrorMessage msg
                    }

                try
                    do!
                        checkLoginSession ()
                        |> TaskResult.bind loadAudioBooksFromCloud
                        |> TaskResult.map (lookForOrphanedAudiobookOnDevice getCurrentAudiobooks)
                        |> TaskResult.bind processLoadedAudioBookFromDevice
                        |> TaskResult.map (determinateNewAddedAudioBooks getCurrentAudiobooks)
                        |> TaskResult.map  checkIfCurrentAudiobookAreReallyDownloaded
                        |> TaskResult.map  removeOrphanPictures
                        |> TaskResult.bind processNewAddedAudioBooks
                        |> TaskResult.bind repairAudiobookMetadataIfNeeded
                        |> TaskResult.bind fixDownloadFolders
                        |> Task.bind processResult

                with
                | ex ->
                    do! showErrorMessage ex.Message

            }




    let synchronizeWithCloudOldShop
        showMessage
        showErrorMessage
        (loadCookie: unit -> Task<Result<Map<string,string> option, string>>)
        appendMessage
        openLogin
        onSuccess
        =
        synchronizeWithCloud
            OldShop
            (fun () -> AudioBookStore.globalAudiobookStore.Value.Model.OldShopAudiobooks)
            OldShopWebAccessService.getAudiobooksOnline
            (DependencyService.Get<IOldShopDatabase>().Base.InsertNewAudioBooksInStateFile)
            (DependencyService.Get<IOldShopDatabase>().Base.UpdateAudioBookInStateFile)
            showMessage
            showErrorMessage
            (loadCookie: unit -> Task<Result<Map<string,string> option, string>>)
            appendMessage
            openLogin
            onSuccess


    let synchronizeWithCloudNewShop
        showMessage
        showErrorMessage
        (loadCookie: unit -> Task<Result<Map<string,string> option, string>>)
        appendMessage
        openLogin
        onSuccess
        =
        synchronizeWithCloud
            NewShop
            (fun () -> AudioBookStore.globalAudiobookStore.Value.Model.NewShopAudiobooks)
            NewShopWebAccessService.getAudiobooksOnline
            (DependencyService.Get<INewShopDatabase>().Base.InsertNewAudioBooksInStateFile)
            (DependencyService.Get<INewShopDatabase>().Base.UpdateAudioBookInStateFile)
            showMessage
            showErrorMessage
            (loadCookie: unit -> Task<Result<Map<string,string> option, string>>)
            appendMessage
            openLogin
            onSuccess
