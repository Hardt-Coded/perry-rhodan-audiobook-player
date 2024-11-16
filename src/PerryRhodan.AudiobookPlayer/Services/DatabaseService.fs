namespace PerryRhodan.AudiobookPlayer.Services

open System
open System.IO
open LiteDB
open LiteDB.FSharp
open FsToolkit.ErrorHandling
open Domain
open Microsoft.ApplicationInsights.DataContracts
open Services
open Services.Consts

module DataBaseCommon =

    let mapper = FSharpBsonMapper()

    type StorageMsg =
        | UpdateAudioBook of AudioBook * AsyncReplyChannel<Result<unit,string>>
        | InsertAudioBooks of AudioBook [] * AsyncReplyChannel<Result<unit,string>>
        | GetAudioBooks of AsyncReplyChannel<AudioBook[]>
        | RemoveAudiobookFromDatabase of AudioBook * AsyncReplyChannel<Result<unit,string>>
        | DeleteDatabase

        | GetAudioBookFileInfo of int * AsyncReplyChannel<AudioBookAudioFilesInfo option>
        | InsertAudioBookFileInfos of AudioBookAudioFilesInfo [] * AsyncReplyChannel<Result<unit,string>>
        | UpdateAudioBookFileInfo of AudioBookAudioFilesInfo * AsyncReplyChannel<Result<unit,string>>
        | DeleteAudioBookFileInfo of int * AsyncReplyChannel<Result<unit,string>>


    type Collection =
        | OldShopDatabaseCollection
        | NewShopDatabaseCollection

        member this.MainCollectionName =
            match this with
            | OldShopDatabaseCollection -> "audiobooks"
            | NewShopDatabaseCollection -> "audiobooksnewshop"
            
        member this.FileCollectionName =
            match this with
            | OldShopDatabaseCollection -> "audiobookfileinfos"
            | NewShopDatabaseCollection -> "audiobookfileinfosnewshop"


    let initAppFolders () =
        let folders = createCurrentFolders ()
        if not (Directory.Exists(folders.currentLocalDataFolder)) then
            Directory.CreateDirectory(folders.currentLocalDataFolder) |> ignore
        if not (Directory.Exists(folders.stateFileFolder)) then
            Directory.CreateDirectory(folders.stateFileFolder) |> ignore


    module private MainDatabase =

        let insertNewAudioBooksDb (StateConnectionString audioBooksStateDataConnectionString) (collection:Collection) (audioBooks:AudioBook[]) =
            try
                use db = new LiteDatabase(audioBooksStateDataConnectionString, mapper)
                let audioBooksCol =
                    db.GetCollection<AudioBook>(collection.MainCollectionName)

                audioBooksCol.Upsert audioBooks |> ignore
                Ok ()
            with
            | e ->
                Global.telemetryClient.TrackException e
                Error e.Message


        let updateAudioBookDb (StateConnectionString audioBooksStateDataConnectionString) (collection:Collection) (audioBook:AudioBook) =
            try
                use db = new LiteDatabase(audioBooksStateDataConnectionString, mapper)
                let audioBooks = db.GetCollection<AudioBook>(collection.MainCollectionName)

                audioBooks.Upsert audioBook |> ignore
                Ok ()
                
            with
            | e ->
                Global.telemetryClient.TrackException e
                Error e.Message



        let deleteAudioBookDb (StateConnectionString audioBooksStateDataConnectionString) (collection:Collection) (audioBook:AudioBook) =
            try
                use db = new LiteDatabase(audioBooksStateDataConnectionString, mapper)
                let audioBooks = db.GetCollection<AudioBook>(collection.MainCollectionName)

                let res =
                    audioBooks.DeleteMany(fun x -> x.Id = audioBook.Id)

                if res > -1
                then (Ok ())
                else (Error Translations.current.ErrorDbWriteAccess)
            with
            | e ->
                Global.telemetryClient.TrackException e
                Error e.Message


        let deleteDatabase (StateConnectionString audioBooksStateDataConnectionString) (FileInfoConnectionString audioBookAudioFileDbConnectionString) (collection:Collection) =
            use db = new LiteDatabase(audioBooksStateDataConnectionString, mapper)
            let _result = db.DropCollection(collection.MainCollectionName)
            use db2 = new LiteDatabase(audioBookAudioFileDbConnectionString, mapper)
            let _result = db.DropCollection("audiobookfileinfos")
            ()


        let loadAudioBooksFromDb (StateConnectionString audioBooksStateDataConnectionString) (collection:Collection)=
            initAppFolders ()

            use db = new LiteDatabase(audioBooksStateDataConnectionString, mapper)
            let audioBooks =
                db.GetCollection<AudioBook>(collection.MainCollectionName)
                    .FindAll()
                    |> Seq.toArray
                    |> Array.sortBy (_.FullName)
                    |> Array.Parallel.map (
                        fun i ->
                            if obj.ReferenceEquals(i.State.LastTimeListend,null) then
                                let newMdl = {i.State with LastTimeListend = None }
                                { i with State = newMdl }
                            else
                                i
                    )

            audioBooks


    module private AudioBookFilesDataDatabase =

        // type for backwards compatibility
        type AudioBookAudioFileDb = {
            FileName:string
            Duration:int
        }

        type AudioBookAudioFilesInfoDb = {
            Id: int
            AudioFiles: AudioBookAudioFileDb list
        }

        let private fromMsSpan (ts:int) =
            TimeSpan.FromMilliseconds ts

        let private fromTimeSpan (ts:TimeSpan) =
            ts.TotalMilliseconds |> int

        let private toDomain (info:AudioBookAudioFilesInfoDb) =
            {
                AudioBookAudioFilesInfo.Id = info.Id
                AudioFiles =
                    info.AudioFiles
                    |> List.map (fun f ->
                        {
                            AudioBookAudioFile.FileName = f.FileName
                            Duration = f.Duration |> fromMsSpan
                        }
                    )
            }

        let private fromDomain (info:AudioBookAudioFilesInfo) =
            {
                Id = info.Id
                AudioFiles =
                    info.AudioFiles
                    |> List.map (fun f ->
                        {
                            FileName = f.FileName
                            Duration = f.Duration |> fromTimeSpan
                        }
                    )
            }

        let loadAudioBookAudioFileInfosFromDb (FileInfoConnectionString audioBookAudioFileDbConnectionString) (collection:Collection)=
            use db = new LiteDatabase(audioBookAudioFileDbConnectionString, mapper)
            let infos =
                db.GetCollection<AudioBookAudioFilesInfoDb>(collection.FileCollectionName)
                    .FindAll()
                    |> Seq.toArray


            infos |> Array.Parallel.map toDomain


        let addAudioFilesInfoToAudioBook (FileInfoConnectionString audioBookAudioFileDbConnectionString) (collection:Collection) (audioFileInfos:AudioBookAudioFilesInfo []) =
            try
                use db = new LiteDatabase(audioBookAudioFileDbConnectionString, mapper)
                let audioBooksCol =
                    db.GetCollection<AudioBookAudioFilesInfoDb>(collection.FileCollectionName)

                let toInsert =
                    audioFileInfos
                    |> Array.Parallel.map fromDomain

                audioBooksCol.Upsert toInsert |> ignore
                Ok ()
            with
            | e -> Error e.Message


        let updateAudioBookFileInfo (FileInfoConnectionString audioBookAudioFileDbConnectionString) (collection:Collection) (audioFileInfo:AudioBookAudioFilesInfo) =
            use db = new LiteDatabase(audioBookAudioFileDbConnectionString, mapper)
            let audioBooks = db.GetCollection<AudioBookAudioFilesInfoDb>(collection.FileCollectionName)

            let audioFileInfo = audioFileInfo |> fromDomain
            audioBooks.Upsert audioFileInfo |> ignore
            Ok ()


        let deleteAudioBookInfoFromDb (FileInfoConnectionString audioBookAudioFileDbConnectionString) (collection:Collection) (audioFileInfo:AudioBookAudioFilesInfo) =
            use db = new LiteDatabase(audioBookAudioFileDbConnectionString, mapper)
            let audioBooks = db.GetCollection<AudioBookAudioFilesInfoDb>(collection.FileCollectionName)

            let res =
                audioBooks.DeleteMany(fun x -> x.Id = audioFileInfo.Id)

            if  res > -1
            then (Ok ())
            else (Error Translations.current.ErrorDbWriteAccess)


    type private StorageState = {
        AudioBooks: AudioBook []
        AudioBookAudioFilesInfos: AudioBookAudioFilesInfo []
    }

    // lazy evaluation, to avoid try loading data without permission
    let createStorageProcessor (collection:Collection) =
        let storageProcessor = MailboxProcessor<StorageMsg>.Start(
            fun inbox ->
                let mutable reloadedAfterFailedInsert = false
                async {
                    try
                        // init stuff
                        initAppFolders ()
                        let folders = createCurrentFolders ()

                        let loadStateFromDb () =
                            try
                                let audioBooks =
                                    MainDatabase.loadAudioBooksFromDb folders.audioBooksStateDataConnectionString collection

                                let audioFileInfos =
                                    AudioBookFilesDataDatabase.loadAudioBookAudioFileInfosFromDb folders.audioBookAudioFileInfoDbConnectionString

                                {
                                    AudioBooks = audioBooks
                                    AudioBookAudioFilesInfos = audioFileInfos collection
                                }
                            with
                            | ex ->
                                Notifications.showErrorMessage ex.Message |> ignore
                                reraise ()

                        let initState = loadStateFromDb ()

                        let rec loop (state:StorageState) =
                            async {
                                let! msg = inbox.Receive()
                                match msg with
                                | UpdateAudioBook (audiobook,replyChannel) ->
                                    let dbRes = MainDatabase.updateAudioBookDb folders.audioBooksStateDataConnectionString collection audiobook
                                    match dbRes with
                                    | Ok _ ->
                                        let newState =
                                            state.AudioBooks
                                            |> Array.Parallel.map (fun i ->
                                                if i.Id = audiobook.Id then
                                                    audiobook
                                                else
                                                    i
                                            )

                                        replyChannel.Reply(Ok ())
                                        return! (loop { state with AudioBooks = newState })
                                    | Error e ->
                                        replyChannel.Reply(Error e)
                                        return! (loop state)

                                | InsertAudioBooks (audiobooks,replyChannel) ->
                                    let dbRes = MainDatabase.insertNewAudioBooksDb folders.audioBooksStateDataConnectionString collection audiobooks
                                    match dbRes with
                                    | Ok _ ->
                                        let newState =
                                            state.AudioBooks |> Array.append audiobooks

                                        replyChannel.Reply(Ok ())
                                        return! (loop { state with AudioBooks = newState })
                                    | Error e ->
                                        if reloadedAfterFailedInsert then
                                            replyChannel.Reply(Error e)
                                            return! (loop state)
                                        else
                                            // reload database and try again to insert
                                            let state = loadStateFromDb ()
                                            inbox.Post <| InsertAudioBooks (audiobooks,replyChannel)
                                            return! (loop state)


                                | GetAudioBooks replyChannel ->
                                    replyChannel.Reply(state.AudioBooks |> Array.sortBy (_.FullName))
                                    return! (loop state)

                                | RemoveAudiobookFromDatabase (audiobook,replyChannel) ->
                                    let dbRes = MainDatabase.deleteAudioBookDb folders.audioBooksStateDataConnectionString collection audiobook
                                    match dbRes with
                                    | Ok _ ->
                                        let newState =
                                            state.AudioBooks |> Array.filter (fun i -> i.Id <> audiobook.Id)


                                        replyChannel.Reply(Ok ())
                                        return! (loop { state with AudioBooks = newState })
                                    | Error e ->
                                        replyChannel.Reply(Error e)
                                        return! (loop state)

                                | DeleteDatabase ->
                                    MainDatabase.deleteDatabase folders.audioBooksStateDataConnectionString folders.audioBookAudioFileInfoDbConnectionString collection
                                    return! (loop <| loadStateFromDb ())


                                | GetAudioBookFileInfo (id,replyChannel) ->
                                    let item =
                                        state.AudioBookAudioFilesInfos
                                        |> Array.tryFind (fun i -> i.Id = id)

                                    replyChannel.Reply(item)
                                    return! loop state

                                | InsertAudioBookFileInfos (infos,replyChannel) ->

                                    // remove already saved
                                    let infos =
                                        infos
                                        |> Array.filter (fun i -> state.AudioBookAudioFilesInfos |> Array.exists (fun x -> x.Id = i.Id) |> not)
                                        |> Array.filter (fun i -> i.AudioFiles.Length > 0)

                                    let newState =
                                        {
                                            state with
                                                AudioBookAudioFilesInfos =
                                                    state.AudioBookAudioFilesInfos
                                                    |> Array.append infos
                                        }

                                    let res = AudioBookFilesDataDatabase.addAudioFilesInfoToAudioBook folders.audioBookAudioFileInfoDbConnectionString collection infos
                                    replyChannel.Reply(res)
                                    match res with
                                    | Error _ ->
                                        return! loop state
                                    | Ok _ ->
                                        return! loop newState



                                | UpdateAudioBookFileInfo (info,replyChannel) ->
                                    let newState =
                                        {
                                            state with
                                                AudioBookAudioFilesInfos =
                                                    state.AudioBookAudioFilesInfos
                                                    |> Array.map (fun i ->
                                                        if info.Id = i.Id then
                                                            info
                                                        else
                                                            i
                                                    )
                                        }

                                    let res = AudioBookFilesDataDatabase.updateAudioBookFileInfo folders.audioBookAudioFileInfoDbConnectionString collection info
                                    replyChannel.Reply(res)
                                    match res with
                                    | Error _ ->
                                        return! loop state
                                    | Ok _ ->
                                        return! loop newState

                                | DeleteAudioBookFileInfo (id,replyChannel) ->
                                    let newState =
                                        {
                                            state with
                                                AudioBookAudioFilesInfos =
                                                    state.AudioBookAudioFilesInfos
                                                    |> Array.filter (fun i -> i.Id <> id)
                                        }

                                    let info =
                                        state.AudioBookAudioFilesInfos
                                        |> Array.tryFind (fun i -> i.Id = id)

                                    match info with
                                    | None ->
                                        replyChannel.Reply(Ok())
                                        return! loop state
                                    | Some info ->
                                        let res = AudioBookFilesDataDatabase.deleteAudioBookInfoFromDb folders.audioBookAudioFileInfoDbConnectionString collection info
                                        replyChannel.Reply(res)
                                        match res with
                                        | Error _ ->
                                            return! loop state
                                        | Ok _ ->
                                            return! loop newState
                            }

                        return! (loop initState)
                    with
                    | ex ->
                        Global.telemetryClient.TrackTrace("Error in storage processor", SeverityLevel.Error)
                        Global.telemetryClient.TrackException(ex)
                        failwith "machine down!"
                }
        )

        let loadAudioBooksStateFile () =
            storageProcessor.PostAndAsyncReply(GetAudioBooks) |> Async.StartAsTask


        let loadDownloadedAudioBooksStateFile () =
            storageProcessor.PostAndAsyncReply(GetAudioBooks)
            |> Async.map (fun res ->
                res |> Array.filter (_.State.Downloaded)
            )
            |> Async.StartAsTask


        let insertNewAudioBooksInStateFile (audioBooks:AudioBook[]) =
            let msg replyChannel =
                InsertAudioBooks (audioBooks,replyChannel)
            storageProcessor.PostAndAsyncReply(msg) |> Async.StartAsTask


        let updateAudioBookInStateFile (audioBook:AudioBook) =
            let msg replyChannel =
                UpdateAudioBook (audioBook,replyChannel)
            storageProcessor.PostAndAsyncReply(msg) |> Async.StartAsTask


        let removeAudiobookFromDatabase audiobook =
            let msg replyChannel =
                RemoveAudiobookFromDatabase (audiobook,replyChannel)
            storageProcessor.PostAndAsyncReply(msg) |> Async.StartAsTask


        let deleteAudiobookDatabase () =
            storageProcessor.Post(DeleteDatabase)


        let getAudioBookFileInfo id =
            let msg replyChannel =
                GetAudioBookFileInfo (id,replyChannel)
            storageProcessor.PostAndAsyncReply(msg) |> Async.StartAsTask

        let getAudioBookFileInfoTimeout timeout id =
            let msg replyChannel =
                GetAudioBookFileInfo (id,replyChannel)
            storageProcessor.TryPostAndReply(msg,timeout)
            |> Option.bind (fun i -> i)

        let insertAudioBookFileInfos infos =
            let msg replyChannel =
                InsertAudioBookFileInfos (infos,replyChannel)
            storageProcessor.PostAndAsyncReply(msg)

        let updateAudioBookFileInfo info =
            let msg replyChannel =
                UpdateAudioBookFileInfo (info,replyChannel)
            storageProcessor.PostAndAsyncReply(msg)

        let deleteAudioBookFileInfo id =
            let msg replyChannel =
                DeleteAudioBookFileInfo (id,replyChannel)
            storageProcessor.PostAndAsyncReply(msg)


        let removeAudiobook (audiobook:AudioBook) =
            task {
                try
                    match audiobook.State.DownloadedFolder with
                    | None ->
                        // when there is no folder, then all is okay
                        return Ok ()
                    | Some folder ->
                        Directory.Delete(folder,true)
                        let! res = deleteAudioBookFileInfo audiobook.Id
                        return res
                with
                | e ->
                    return Error e.Message
            }


        {|
            LoadAudioBooksStateFile             = loadAudioBooksStateFile
            LoadDownloadedAudioBooksStateFile   = loadDownloadedAudioBooksStateFile
            InsertNewAudioBooksInStateFile      = insertNewAudioBooksInStateFile
            UpdateAudioBookInStateFile          = updateAudioBookInStateFile
            RemoveAudiobookFromDatabase         = removeAudiobookFromDatabase
            DeleteAudiobookDatabase             = deleteAudiobookDatabase
            GetAudioBookFileInfo                = getAudioBookFileInfo
            GetAudioBookFileInfoTimeout         = getAudioBookFileInfoTimeout
            InsertAudioBookFileInfos            = insertAudioBookFileInfos
            UpdateAudioBookFileInfo             = updateAudioBookFileInfo
            DeleteAudioBookFileInfo             = deleteAudioBookFileInfo
            RemoveAudiobook                     = removeAudiobook
        |}




    let parseDownloadFolderForAlreadyDownloadedAudioBooks () =
        let folders = createCurrentFolders ()

        if (not (Directory.Exists(folders.audioBookDownloadFolderBase))) then
            [||]
        else
            let directories = Directory.EnumerateDirectories(folders.audioBookDownloadFolderBase)
            directories
            |> Seq.map (
                fun lookupPath ->
                    let audioBookName = DirectoryInfo(lookupPath).Name
                    if (not (Directory.Exists(lookupPath))) then
                        None
                    else
                        let picFiles = Directory.EnumerateFiles(lookupPath,"*.jpg")
                        let thumb = picFiles |> Seq.tryFind (fun f -> f = Path.Combine(lookupPath,audioBookName + ".thumb.jpg"))
                        let pic = picFiles |> Seq.tryFind (fun f -> f = Path.Combine(lookupPath,audioBookName + ".jpg"))
                        let audioPath = Path.Combine(lookupPath,"audio")
                        let hasAudioBook =
                            if Directory.Exists(audioPath) then
                                let audioFiles = Directory.EnumerateFiles(audioPath,"*.mp3")
                                let downloadingFlagFile = Path.Combine(audioPath,"downloading")
                                let hasCorruptDownloadingFlagFile = File.Exists(downloadingFlagFile)
                                (audioFiles |> Seq.length) > 0 && not hasCorruptDownloadingFlagFile
                            else false
                        let audioBookPath = if hasAudioBook then Some audioPath else None
                        Some {|
                            Name = audioBookName
                            Pic = pic
                            Thumb = thumb
                            HasAudioBook = hasAudioBook
                            AudioBookPath = audioBookPath
                        |}
            )
            |> Seq.choose id
            |> Seq.toArray



    let getAudiobooksFromDownloadFolder audiobooks =
        let audioBooksOnDevice = parseDownloadFolderForAlreadyDownloadedAudioBooks ()
        let result =
            audioBooksOnDevice
            |> Array.choose (
                fun onDeviceItem ->
                    let audiobook =
                        audiobooks
                        |> Array.tryFind (fun item -> onDeviceItem.Name = item.FullName || onDeviceItem.Name = $"{item.Id}")
                    match audiobook with
                    | None -> None
                    | Some audiobook ->
                        Some {
                            audiobook with
                                State.Downloaded = onDeviceItem.HasAudioBook
                                State.DownloadedFolder = onDeviceItem.AudioBookPath
                                Picture = onDeviceItem.Pic
                                Thumbnail = onDeviceItem.Thumb
                        }

            )
        result



module OldShopDatabase =

    let storageProcessor = DataBaseCommon.createStorageProcessor DataBaseCommon.Collection.OldShopDatabaseCollection


module NewShopDatabase =

    let storageProcessor = DataBaseCommon.createStorageProcessor DataBaseCommon.Collection.NewShopDatabaseCollection
