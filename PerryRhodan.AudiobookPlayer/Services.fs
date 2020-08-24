﻿
module Services

open System
open System.IO
open Domain
open System.Net
open Xamarin.Essentials
open Newtonsoft.Json
open FSharp.Data
open Xamarin.Forms
open System.Net.Http
open System.IO.Compression
open System.Threading.Tasks
open ICSharpCode.SharpZipLib.Zip

open Common
open Plugin.Permissions.Abstractions
open Common.EventHelper
open SkiaSharp




module DependencyServices =

    open Global

    type IAndroidDownloadFolder = 
        abstract member GetAndroidDownloadFolder:unit -> string 

    type INotificationService = 
        abstract ShowMessage : string->string -> unit

    type IDownloadService =
        abstract member StartDownload: unit -> unit


module Consts =
    
    open DependencyServices

    let currentLocalDataFolder =  
        let baseFolder = 
            match Device.RuntimePlatform with
            | Device.Android -> DependencyService.Get<IAndroidDownloadFolder>().GetAndroidDownloadFolder ()
            //| Device.Android -> Path.Combine(DependencyService.Get<IAndroidDownloadFolder>().GetAndroidDownloadFolder (),"..")
            | Device.iOS -> Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            | _ -> Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        Path.Combine(baseFolder,"PerryRhodan.AudioBookPlayer","data")
    let stateFileFolder = Path.Combine(currentLocalDataFolder,"states")
    let audioBooksStateDataFile = Path.Combine(stateFileFolder,"audiobooks.db")
    let audioBookAudioFileDb = Path.Combine(stateFileFolder,"audiobookfiles.db")
    let audioBookDownloadFolderBase = Path.Combine(currentLocalDataFolder,"audiobooks")


    let baseUrl = "https://www.einsamedien.de/"





module Notifications =

    let private notificationService = lazy (DependencyService.Get<DependencyServices.INotificationService>())

    let showNotification title message =
        let ns = notificationService.Force()
        ns.ShowMessage title message


module DataBase =

    open Consts
    open LiteDB
    open LiteDB.FSharp
    open FSharpx.Control.AsyncExtensions
    

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


    let initAppFolders () =
        if not (Directory.Exists(currentLocalDataFolder)) then
            Directory.CreateDirectory(currentLocalDataFolder) |> ignore
        if not (Directory.Exists(stateFileFolder)) then
            Directory.CreateDirectory(stateFileFolder) |> ignore


    let private insertNewAudioBooksDb (audioBooks:AudioBook[]) =
        try
            use db = new LiteDatabase(audioBooksStateDataFile, mapper)
            let audioBooksCol = 
                db.GetCollection<AudioBook>("audiobooks")

            audioBooksCol.InsertBulk(audioBooks) |> ignore                    
            Ok ()
        with
        | e -> Error e.Message


    let private updateAudioBookDb (audioBook:AudioBook) =
        use db = new LiteDatabase(audioBooksStateDataFile, mapper)
        let audioBooks = db.GetCollection<AudioBook>("audiobooks")

        if audioBooks.Update(audioBook)
        then (Ok ())
        else (Error Translations.current.ErrorDbWriteAccess)


    let private deleteAudioBookDb (audioBook:AudioBook) =
        use db = new LiteDatabase(audioBooksStateDataFile, mapper)
        let audioBooks = db.GetCollection<AudioBook>("audiobooks")

        let res = 
            audioBooks.Delete(fun x -> x.Id = audioBook.Id)

        if res > -1
        then (Ok ())
        else (Error Translations.current.ErrorDbWriteAccess)


    let private deleteDatabase () =
        use db = new LiteDatabase(audioBooksStateDataFile, mapper)
        let _result = db.DropCollection("audiobooks")
        use db2 = new LiteDatabase(audioBookAudioFileDb, mapper)
        let _result = db.DropCollection("audiobookfileinfos")
        ()


    let private loadAudioBooksFromDb () =
        initAppFolders ()
                
        use db = new LiteDatabase(audioBooksStateDataFile, mapper)
        let audioBooks = 
            db.GetCollection<AudioBook>("audiobooks")
                .FindAll()                             
                |> Seq.toArray
                |> Array.sortBy (fun i -> i.FullName)
                |> Array.Parallel.map (
                    fun i ->
                        if obj.ReferenceEquals(i.State.LastTimeListend,null) then
                            let newMdl = {i.State with LastTimeListend = None }
                            { i with State = newMdl }
                        else
                            i
                )

        audioBooks


    module private AudioBookFiles =

        let loadAudioBookAudioFileInfosFromDb () =
            use db = new LiteDatabase(audioBookAudioFileDb, mapper)
            let infos = 
                db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos")
                    .FindAll()                             
                    |> Seq.toArray

            infos


        let addAudioFilesInfoToAudioBook (audioFileInfos:AudioBookAudioFilesInfo []) =
            try
                use db = new LiteDatabase(audioBookAudioFileDb, mapper)
                let audioBooksCol = 
                    db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos")

                audioBooksCol.InsertBulk(audioFileInfos) |> ignore                    
                Ok ()
            with
            | e -> Error e.Message


        let updateAudioBookFileInfo (audioFileInfo:AudioBookAudioFilesInfo) =
            use db = new LiteDatabase(audioBookAudioFileDb, mapper)
            let audioBooks = db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos")

            if audioBooks.Update(audioFileInfo)
            then (Ok ())
            else (Error Translations.current.ErrorDbWriteAccess)


        let deleteAudioBookInfoFromDb (audioFileInfo:AudioBookAudioFilesInfo) =
            use db = new LiteDatabase(audioBookAudioFileDb, mapper)
            let audioBooks = db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos")

            let res = 
                audioBooks.Delete(fun x -> x.Id = audioFileInfo.Id)

            if  res > -1
            then (Ok ())
            else (Error Translations.current.ErrorDbWriteAccess)


    type private StorageState = {
        AudioBooks: AudioBook []
        AudioBookAudioFilesInfos: AudioBookAudioFilesInfo []
    }

    // lazy evaluation, to avoid try loading data without permission
    let private storageProcessor = 
        lazy 
        MailboxProcessor<StorageMsg>.Start(
            fun inbox ->
                let mutable reloadedAfterFailedInsert = false
                async {
                    try
                        // init stuff
                        initAppFolders ()

                        let loadStateFromDb () =
                            let audioBooks =
                                loadAudioBooksFromDb ()

                            let audioFileInfos =
                                AudioBookFiles.loadAudioBookAudioFileInfosFromDb ()

                            {
                                AudioBooks = audioBooks
                                AudioBookAudioFilesInfos = audioFileInfos 
                            }

                        let initState = loadStateFromDb ()    
                    
                        let rec loop (state:StorageState) =
                            async {
                                let! msg = inbox.Receive()
                                match msg with
                                | UpdateAudioBook (audiobook,replyChannel) ->
                                    let dbRes = updateAudioBookDb audiobook
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
                                    let dbRes = insertNewAudioBooksDb audiobooks
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
                                    replyChannel.Reply(state.AudioBooks |> Array.sortBy (fun i -> i.FullName))
                                    return! (loop state)

                                | RemoveAudiobookFromDatabase (audiobook,replyChannel) ->
                                    let dbRes = deleteAudioBookDb audiobook
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
                                    deleteDatabase ()
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

                                    let res = AudioBookFiles.addAudioFilesInfoToAudioBook infos
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

                                    let res = AudioBookFiles.updateAudioBookFileInfo info
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
                                        let res = AudioBookFiles.deleteAudioBookInfoFromDb info 
                                        replyChannel.Reply(res)
                                        match res with
                                        | Error _ ->
                                            return! loop state
                                        | Ok _ ->
                                            return! loop newState
                            }
                        
                        return! (loop initState)

                        let weschoulndenduphere = 0
                        ()
                    with
                    | ex ->
                        //storageProcessorErrorEvent.Trigger(ex)
                        failwith "machine down!"
                }
                
                    
        )


    
    
    let loadAudioBooksStateFile () =
        storageProcessor.Force().PostAndAsyncReply(GetAudioBooks)
    

    let loadDownloadedAudioBooksStateFile () =
        storageProcessor.Force().PostAndAsyncReply(GetAudioBooks)        
        |> Async.map (fun res ->
            res |> Array.filter (fun i -> i.State.Downloaded)
        )
        
        

    let insertNewAudioBooksInStateFile (audioBooks:AudioBook[]) =
        let msg replyChannel = 
            InsertAudioBooks (audioBooks,replyChannel)
        storageProcessor.Force().PostAndAsyncReply(msg)


    let updateAudioBookInStateFile (audioBook:AudioBook) =
        let msg replyChannel = 
            UpdateAudioBook (audioBook,replyChannel)
        storageProcessor.Force().PostAndAsyncReply(msg)


    let removeAudiobookFromDatabase audiobook =
        let msg replyChannel = 
            RemoveAudiobookFromDatabase (audiobook,replyChannel)
        storageProcessor.Force().PostAndAsyncReply(msg)


    let deleteAudiobookDatabase () =
        storageProcessor.Force().Post(DeleteDatabase)


    
        


    let getAudioBookFileInfo id =
        let msg replyChannel = 
            GetAudioBookFileInfo (id,replyChannel)
        storageProcessor.Force().PostAndAsyncReply(msg)

    let getAudioBookFileInfoTimeout timeout id =
        let msg replyChannel = 
            GetAudioBookFileInfo (id,replyChannel)
        storageProcessor.Force().TryPostAndReply(msg,timeout)
        |> Option.bind (fun i -> i)
        
    let insertAudioBookFileInfos infos =
        let msg replyChannel = 
            InsertAudioBookFileInfos (infos,replyChannel)
        storageProcessor.Force().PostAndAsyncReply(msg)

    let updateAudioBookFileInfo info =
        let msg replyChannel = 
            UpdateAudioBookFileInfo (info,replyChannel)
        storageProcessor.Force().PostAndAsyncReply(msg)
    
    let deleteAudioBookFileInfo id =
        let msg replyChannel = 
            DeleteAudioBookFileInfo (id,replyChannel)
        storageProcessor.Force().PostAndAsyncReply(msg)



    let removeAudiobook audiobook = 
        async {
            try
                match audiobook.State.DownloadedFolder with
                | None -> 
                    return Error (Translations.current.ErrorRemoveAudioBook)
                | Some folder ->
                    Directory.Delete(folder,true)
                    let! res = deleteAudioBookFileInfo audiobook.Id
                    return res
            with
            | e -> 
                return Error (e.Message)
        }

    
    let parseDownloadFolderForAlreadyDownloadedAudioBooks () =
        if (not (Directory.Exists(audioBookDownloadFolderBase))) then
            [||]
        else
            let directories = Directory.EnumerateDirectories(audioBookDownloadFolderBase)    
            directories
            |> Seq.toArray
            |> Array.Parallel.map (
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
                        Some (audioBookName, pic, thumb, hasAudioBook, audioBookPath)
            )
            |> Array.filter (fun i -> i.IsSome)
            |> Array.Parallel.map (fun i-> i.Value)
    
    
    let getAudiobooksFromDownloadFolder audiobooks =
        let audioBooksOnDevice = parseDownloadFolderForAlreadyDownloadedAudioBooks ()
        audiobooks
        |> Array.choose (
            fun i ->
                let onDeviceItem = audioBooksOnDevice |> Array.tryFind (fun (title,_,_,_,_) -> title = i.FullName)
                match onDeviceItem with
                | None -> None
                | Some (_, picPath, thumbPath, hasAudioBook, audioBookPath) ->
                    let newState = {i.State with Downloaded = hasAudioBook; DownloadedFolder=audioBookPath}
                    Some {i with State = newState; Picture = picPath; Thumbnail = thumbPath}
        )
   


module WebAccess =

    open Consts
    open DataBase
    open System.Net
    open System.Net.Sockets
    open Microsoft.AppCenter.Crashes
    open Microsoft.AppCenter.Analytics
    
    let handleException f =
        async {
            try
                let! res = f()
                return (Ok res)
            with
            | exn ->
                let ex = exn.GetBaseException()
                Crashes.TrackError(ex, Map.empty)
                match ex with
                | :? WebException | :? SocketException ->
                    return Error (Network Translations.current.NetworkError)
                | :? TimeoutException ->
                    return Error (Network Translations.current.NetworkTimeoutError)
                | _ ->
                    return Error (Other Translations.current.InternalError)
        }
        

    let login username password =
        async {
                let! res = 
                    (fun () -> 
                        Http.AsyncRequest(
                            baseUrl + "butler.php",
                            body = FormValues [("action","login"); ("username",username); ("password",password)],
                            httpMethod = HttpMethod.Post,
                            customizeHttpRequest = 
                                (fun req ->                         
                                    req.AllowAutoRedirect <- false
                                    req
                                )
                            )
                    )
                    |> handleException

                return 
                    res
                    |> Result.bind(
                        fun (resp) ->
                            let location = resp.Headers |> Seq.filter (fun m -> m.Key = "Location") |> Seq.tryHead
                            match location with
                            | None -> 
                                Analytics.TrackEvent("no location on login response found")
                                Error (Other Translations.current.UnexpectedServerBehaviorError)
                            | Some v ->
                                if v.Value.Contains("98") then Ok (None)
                                else if v.Value.Contains("61") then Ok (Some resp.Cookies)
                                else Ok (None)                        
                    )
        
                
        }

    
        


    let getDownloadPage (cc:Map<string,string>) =
        async {       
            let seqCC = cc |> Map.toSeq

            let! res = 
                fun () -> Http.AsyncRequestString(baseUrl + "index.php?id=61",cookies = seqCC, httpMethod="GET")
                |> handleException

            return res 
                |> Result.bind (
                    fun html ->
                        if (html.Contains("<input name=\"username\"")) then Error (SessionExpired Translations.current.SessionExpired)
                        else Ok html
                )
        }
    
    
    let getAudiobooksOnline cookies =
        asyncResult {
            initAppFolders ()        
           
            let! html = getDownloadPage cookies
            let audioBooks =
                html
                |> parseDownloadData
    
            return audioBooks
        }


    let private getDownloadUrl cookies url =
        async {
            let seqCC = cookies |> Map.toSeq

            let! res = 
                (fun () ->
                    Http.AsyncRequest(
                        baseUrl + url,                
                        httpMethod = HttpMethod.Get,
                        cookies = seqCC,
                        customizeHttpRequest = 
                            (fun req ->                         
                                req.AllowAutoRedirect <- false
                                req
                            )
                        )
                )
                |> handleException

            return
                res
                |> Result.bind (
                    fun resp ->
                        if (not (resp.Headers.ContainsKey("Location"))) then
                            Error (Other Translations.current.NoDownloadUrlFoundError)
                        else
                            let downloadUrl = resp.Headers.Item "Location"
                            if (downloadUrl.Contains("index.php?id=98")) then
                                Error (SessionExpired Translations.current.SessionExpired)
                            else
                                Ok downloadUrl
                )
                
            
        }

    
    
    module Downloader =
        
        type UpdateProgress = {
            UpdateProgress:(int * int) -> unit
            FileSize:int
            CurrentProgress:int
        }
            with 
                static member create updateProgress filesize currentProgress =
                    {
                        UpdateProgress = updateProgress
                        FileSize = filesize
                        CurrentProgress = currentProgress
                    }


        let private copyStream (src:Stream) (dst:Stream) =
            let buffer:byte[] = Array.zeroCreate (1024*1024)
            let dowloadStreamSeq =
                seq {
                    let mutable copying = true
                    while copying do
                        let bytesRead = src.Read(buffer,0,buffer.Length)    
                        if bytesRead > 0 then
                            dst.Write(buffer, 0, bytesRead)
                            yield (bytesRead)
                        else
                            dst.Flush()
                            copying <- false
                }

            dowloadStreamSeq
            



        let private processMp3File (updateProgress:UpdateProgress) zipStream unzipTargetFolder (entry:ZipEntry) =
            let scale = (entry.CompressedSize |> float) / (entry.Size |> float)
            let name = Path.GetFileName(entry.Name)
            let extractFullPath = Path.Combine(unzipTargetFolder,name)
            if (File.Exists(extractFullPath)) then
                File.Delete(extractFullPath)

            use streamWriter = File.Create(extractFullPath)
            let progress = 
                copyStream zipStream streamWriter
                |> Seq.map (fun bytesRead -> ((bytesRead |> float) * scale) |> int)
                |> Seq.fold (fun state progress ->
                    let newProgress = state + progress
                    // send progress update message to ui
                    let displayProgress = updateProgress.CurrentProgress + newProgress
                    updateProgress.UpdateProgress ( displayProgress / (1024 * 1024), updateProgress.FileSize / (1024 * 1024))
                    newProgress
                ) 0
            streamWriter.Close()
            progress   


        let private processPicFile (updateProgress:UpdateProgress) zipStream audioBookFolder audiobook (entry:ZipEntry) =
            let scale = (entry.CompressedSize |> float) / (entry.Size |> float)            
            let imageFullName = Path.Combine(audioBookFolder,audiobook.FullName + ".jpg")

            // try download picture if necessary
            let progress =
                if not (File.Exists(imageFullName)) then
                    use streamWriter = File.Create(imageFullName)
                    let progress = 
                        copyStream zipStream streamWriter
                        |> Seq.map (fun bytesRead -> ((bytesRead |> float) * scale) |> int)
                        |> Seq.fold (fun state progress ->
                            let newProgress = state + progress
                            // send progress update message to ui
                            let displayProgress = updateProgress.CurrentProgress + newProgress
                            updateProgress.UpdateProgress ( displayProgress / (1024 * 1024), updateProgress.FileSize / (1024 * 1024))
                            newProgress
                        ) 0
                        
                    streamWriter.Close()                                        
                    progress
                else
                    entry.Size |> int
            
            
            let thumbFullName = Path.Combine(audioBookFolder,audiobook.FullName + ".thumb.jpg")
                    
            // try create thumb nail picture if necessary
            if not (File.Exists(thumbFullName)) then

                //use thumbInputStream = File.OpenRead()
                use orig = SKBitmap.Decode(imageFullName)
                use thumb = orig.Resize(SKImageInfo(200, 200),SKFilterQuality.Medium)
                if isNull thumb then
                    ()
                else
                    use thumbImage = SKImage.FromBitmap(thumb)
                    use fileStream = new FileStream(thumbFullName,FileMode.Create)
                    thumbImage.Encode(SKEncodedImageFormat.Jpeg,90).SaveTo(fileStream)
                  
                    fileStream.Close()
        
            progress


        type ImagePaths = {
            Image:string
            Thumbnail:string
        }

        type DownloadResult = {
            TargetFolder:string
            Images: ImagePaths option
        }


        let downloadAudiobook cookies updateProgress audiobook =
            async {
                try
                    Microsoft.AppCenter.Analytics.Analytics.TrackEvent("download audiobook")

                    if (audiobook.State.Downloaded) then 
                        return Error (Other "Audiobook already downloaded!")
                    else
                        let audioBookFolder = Path.Combine(audioBookDownloadFolderBase,audiobook.FullName)        
                        if not (Directory.Exists(audioBookFolder)) then
                            Directory.CreateDirectory(audioBookFolder) |> ignore
                    
                        match audiobook.DownloadUrl with
                        | None -> return Error (Other Translations.current.NoDownloadUrlFoundError)
                        | Some abDownloadUrl ->    
                            let! downloadUrl = (abDownloadUrl |> getDownloadUrl cookies)
                            match downloadUrl with
                            | Error e -> 
                                return Error e

                            | Ok url -> 
                                try
                                    

                                    let! resp = Http.AsyncRequestStream(url,httpMethod=HttpMethod.Get)

                                    if (resp.StatusCode <> 200) then 
                                        return Error (Other (sprintf "download statuscode %i" resp.StatusCode))
                                    else
                                
                                        let unzipTargetFolder = Path.Combine(audioBookFolder,"audio")
                                        


                                        if not (Directory.Exists(unzipTargetFolder)) then
                                            Directory.CreateDirectory(unzipTargetFolder) |> ignore


                                        let downloadingFlagFile = Path.Combine(unzipTargetFolder,"downloading")
                                        // create flag file, to determinate download was maybe interrupted!
                                        File.WriteAllText(downloadingFlagFile,"downloading")

                                
                                        let fileSize = 
                                            (resp.Headers
                                            |> HttpHelpers.getFileSizeFromHttpHeadersOrDefaultValue 0)
                                    
                                        use zipStream = new ZipInputStream(resp.ResponseStream)

                                        let zipSeq =
                                            seq {
                                                let mutable entryAvailable = true
                                                while entryAvailable do
                                                    match zipStream.GetNextEntry() with
                                                    | null ->
                                                        entryAvailable <- false
                                                    | entry -> 
                                                        yield (entry)
                                            
                                            }

                                        let! gloablProgress = 
                                            asyncFunc(fun () ->
                                                zipSeq
                                                |> Seq.fold (fun state (entry:ZipEntry) ->
                                                    let updateProgress = UpdateProgress.create updateProgress fileSize state
                                                    let progress =
                                                        match entry with
                                                        | ZipHelpers.Mp3File ->
                                                            entry |> processMp3File updateProgress zipStream unzipTargetFolder 
                                                        | ZipHelpers.PicFile ->
                                                            entry |> processPicFile updateProgress zipStream audioBookFolder audiobook
                                                        | _ ->
                                                        0
                                                    let newProgress = state + progress
                                                    // send progress update message to ui
                                                    updateProgress.UpdateProgress (newProgress / (1024 * 1024), fileSize / (1024 * 1024))
                                                    newProgress
                                                ) 0
                                                
                                        )
                                
                                        zipStream.Close()
                                        resp.ResponseStream.Close()    
                                        
                                        updateProgress (fileSize / (1024 * 1024), fileSize / (1024 * 1024))


                                        let imageFullName = Path.Combine(audioBookFolder,audiobook.FullName + ".jpg")
                                        let thumbFullName = Path.Combine(audioBookFolder,audiobook.FullName + ".thumb.jpg")

                                        let imageFileNames = 
                                            if File.Exists(imageFullName) && File.Exists(thumbFullName) then
                                                Some <| { Image = imageFullName; Thumbnail = thumbFullName }
                                            else None

                                        // delete downloading flag file
                                        File.Delete(downloadingFlagFile)

                                        return Ok {
                                            TargetFolder = unzipTargetFolder
                                            Images = imageFileNames
                                        }
                                       
                                    with
                                    | :? WebException | :? SocketException ->
                                        return Error (Network Translations.current.NetworkError)
                                    | :? TimeoutException ->
                                        return Error (Network Translations.current.NetworkTimeoutError)
                                    | _ ->
                                        return Error (Other Translations.current.InternalError)
                with
                | exn ->
                    let ex = exn.GetBaseException()
                    Crashes.TrackError(ex, Map.empty)
                    match ex with
                    | :? WebException | :? SocketException ->
                        return Error (Network Translations.current.NetworkError)
                    | :? TimeoutException ->
                        return Error (Network Translations.current.NetworkTimeoutError)
                    | _ ->
                        return Error (Other Translations.current.InternalError)
            }


    let loadDescription audiobook =
        async {
            match audiobook.ProductSiteUrl with
            | None -> return Ok (None,None)
            | Some ps ->
                let productPageUri = Uri(baseUrl + ps)

                let! productPageRes = 
                    (fun () -> Http.AsyncRequestString(productPageUri.AbsoluteUri))
                    |> handleException
                return productPageRes
                    |> Result.bind (
                        fun productPage ->
                            if productPage = "" then
                                Error (Other Translations.current.ProductPageEmptyError)
                            else
                                let desc = productPage |> Domain.parseProductPageForDescription
                                let img = 
                                    productPage 
                                    |> Domain.parseProductPageForImage
                                    |> Option.map (fun i ->
                                        let uri = Uri(baseUrl + i)
                                        uri.AbsoluteUri
                                    )
                                    
                                    
                                Ok (desc,img)
                    )
                
                
        }


module SecureStorageHelper =
    let getSecuredValue key =
        async {
            let! value =  SecureStorage.GetAsync(key) |> Async.AwaitTask
            return if value |> isNull then None else Some value
        }

    let setSecuredValue value key =
        async {
            do! SecureStorage.SetAsync(key,value) |> Async.AwaitTask            
        }


module SecureLoginStorage =
    open SecureStorageHelper

    let private secStoreUsernameKey = "perryRhodanAudioBookUsername"
    let private secStorePasswordKey = "perryRhodanAudioBookPassword"
    let private secStoreRememberLoginKey = "perryRhodanAudioBookRememberLogin"

    let saveLoginCredentials username password rememberLogin =
        async {
            try
                do! secStoreUsernameKey |> setSecuredValue username
                do! secStorePasswordKey |> setSecuredValue password
                do! secStoreRememberLoginKey |> setSecuredValue (if rememberLogin then "Jupp" else "")
                return Ok true
            with
            | e -> return (Error (e.Message))
        }
    
    let loadLoginCredentials () =
        async {
            try
                let! username =  secStoreUsernameKey |> getSecuredValue
                let! password =  secStorePasswordKey|> getSecuredValue
                let! rememberLoginStr = secStoreRememberLoginKey |> getSecuredValue
                return Ok (username,password,(rememberLoginStr = Some "Jupp"))
            with
            | e -> return (Error (e.Message))
        }


module Files =

    let fromTimeSpan (ts:TimeSpan) =
        ts.TotalMilliseconds |> int

    let getMp3FileList folder =
        async {
            let! files = 
                asyncFunc( 
                    fun () ->  
                        try
                            Directory.EnumerateFiles(folder, "*.mp3")
                        with
                        | _ -> Seq.empty
                )
            
            let! res =
                asyncFunc (fun () ->
                    files 
                    |> Seq.toList 
                    |> List.map (
                        fun i ->
                            use tfile = TagLib.File.Create(i)
                            { FileName = i; Duration = tfile.Properties.Duration |> fromTimeSpan }
                    )
                )

            return res
        }


module SystemSettings =
    open SecureStorageHelper
    open FSharpx.Control
    open Common.StringHelpers

    let defaultRewindWhenStartAfterShortPeriodInSec = 5
    let defaultRewindWhenStartAfterLongPeriodInSec = 30
    let defaultLongPeriodBeginsAfterInMinutes = 60
    let defaultAudioJumpDistance = 30000

    let private keyRewindWhenStartAfterShortPeriodInSec = "PerryRhodanAudioBookRewindWhenStartAfterShortPeriodInSec"
    let private keykeyRewindWhenStartAfterLongPeriodInSec ="PerryRhodanAudioBookRewindWhenStartAfterLongPeriodInSec"
    let private keyLongPeriodBeginsAfterInMinutes ="PerryRhodanAudioBookLongPeriodBeginsAfterInMinutes"
    let private keyAudioJumpDistance = "PerryRhodanAudioBookAudioJumpDistance"
    let private keyDeveloperMode= "PerryRhodanAudioBookDeveloperModee"

    let getRewindWhenStartAfterShortPeriodInSec () =
        keyRewindWhenStartAfterShortPeriodInSec 
        |> getSecuredValue
        |> Async.map (fun result ->
            result |> optToInt defaultRewindWhenStartAfterShortPeriodInSec
        )


    let getRewindWhenStartAfterLongPeriodInSec () =
        keykeyRewindWhenStartAfterLongPeriodInSec 
        |> getSecuredValue
        |> Async.map (fun result ->
            result |> optToInt defaultRewindWhenStartAfterLongPeriodInSec
        )


    let getLongPeriodBeginsAfterInMinutes () =
        keyLongPeriodBeginsAfterInMinutes 
        |> getSecuredValue
        |> Async.map (fun result ->
            result |> optToInt defaultLongPeriodBeginsAfterInMinutes
        )

    
    let getJumpDistance () =
        keyAudioJumpDistance 
        |> getSecuredValue
        |> Async.map (fun result ->
            result |> optToInt defaultAudioJumpDistance
        )

    let getDeveloperMode () =
        keyDeveloperMode 
        |> getSecuredValue
        |> Async.map (fun result ->
            result |> Option.map(fun v -> v = "true") |> Option.defaultValue false
        )


    let setRewindWhenStartAfterShortPeriodInSec (value:int) =
        keyRewindWhenStartAfterShortPeriodInSec |> setSecuredValue (value.ToString())


    let setRewindWhenStartAfterLongPeriodInSec (value:int) =
        keykeyRewindWhenStartAfterLongPeriodInSec |> setSecuredValue (value.ToString())


    let setLongPeriodBeginsAfterInMinutes (value:int) =
        keyLongPeriodBeginsAfterInMinutes |> setSecuredValue (value.ToString())


    let setJumpDistance (value:int) =
        keyAudioJumpDistance |> setSecuredValue (value.ToString())

    let setDeveloperMode(value:bool) =
        keyDeveloperMode |> setSecuredValue (if value then "true" else "false")

 
module SupportFeedback =

    open FSharp.Data.HttpRequestHeaders
    
    type Message = {
        Category:string
        Name:string
        Message:string
    }

    let sendSupportFeedBack name category message =
        async {
            
            let msg = {
                Category = category
                Message = message
                Name = name
            }
            let json = JsonConvert.SerializeObject(msg)
            let body = HttpRequestBody.TextRequest json
            try
                let! response = Http.AsyncRequest(url=Global.supportMessageApi,httpMethod="POST", body=body,headers=[Accept HttpContentTypes.Json;ContentType HttpContentTypes.Json])
                if response.StatusCode <> 202 then
                    return Error "Fehler beim Senden der Nachricht. Probieren Sie es noch einmal."
                else
                    return Ok ()
            with
            | ex ->
                Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                return Error "Fehlerbeim Senden der Nachricht. Probieren Sie es noch einmal."

        }


module DownloadService =
    
    type DownloadState =
        | Open
        | Running of int * int
        | Finished of WebAccess.Downloader.DownloadResult
        | Failed of ComError


    type DownloadInfo = 
        {
            State:DownloadState
            AudioBook:AudioBook
            CookieContainer:Map<string,string> option
        }
        static member New cookieContainer audiobook =
            { State = Open; AudioBook = audiobook; CookieContainer = cookieContainer }



    open Common.MailboxExtensions
    open Common
    open FSharp.Control
    open FSharpx.Control.AsyncExtensions

    type private Listener = (string * (DownloadInfo -> Async<unit>))

    type private ShutDownEventHandler = (unit -> Async<unit>)

    type private ErrorEventHandler = (DownloadInfo * ComError -> Async<unit>)

    type DownloadServiceState = {
        Downloads: DownloadInfo list
        CurrentDownload: DownloadInfo option
    }


    type ServiceMessages =
        | AddDownload of DownloadInfo
        | RemoveDownload of DownloadInfo
        | StartDownloads
        | ShutDownService
        | GetState of AsyncReplyChannel<DownloadServiceState option>

    type ServiceListener =  (ServiceMessages -> unit)


    type private Msg =
        | StartService
        | ShutDownService
        | StartDownloads
        | AddDownload of DownloadInfo
        | RemoveDownload of DownloadInfo

        | RegisterServiceListener of ServiceListener
        
        | SignalError of DownloadInfo * ComError
        | SignalServiceCrashed of exn
        | SignalServiceShutDown

        | RegisterShutDownListener of ShutDownEventHandler
        | RegisterErrorListener of ErrorEventHandler
        | AddInfoListener of Listener
        | RemoveInfoListener of string
        | SendInfo of DownloadInfo

        | GetState of AsyncReplyChannel<DownloadServiceState option>


    type private HandlerState = {
        ServiceListener: ServiceListener option
        Listeners: Listener list 
        ErrorEventListener: ErrorEventHandler option
        ShutdownEvent: ShutDownEventHandler option
    }


    let private downloadServiceCallback =
        lazy (
            MailboxProcessor<Msg>.Start(
                let downloadService = DependencyService.Get<DependencyServices.IDownloadService>()
                
                fun inbox ->
                    let rec loop (state:HandlerState) =
                        async {
                            let! msg = inbox.Receive()

                            match msg with
                            | StartService ->
                                match state.ServiceListener with
                                | None ->
                                    downloadService.StartDownload ()
                                    return! loop state
                                | Some _ ->
                                    return! loop state

                            | ShutDownService ->
                                match state.ServiceListener with
                                | None ->
                                    return! loop state
                                | Some listener ->
                                    listener ServiceMessages.ShutDownService
                                    return! loop state

                            | StartDownloads ->
                                match state.ServiceListener with
                                | None ->
                                    // when no listener is present and the message was send, than I wait for the service to start
                                    do! Async.Sleep 1000
                                    inbox.Post StartDownloads
                                    return! loop state

                                | Some listener ->
                                    listener <| ServiceMessages.StartDownloads
                                    return! loop state
                                    

                            | AddDownload info ->
                                match state.ServiceListener with
                                | None ->
                                    // when no listener is present and the message was send, than I wait for the service to start
                                    do! Async.Sleep 1000
                                    inbox.Post <| AddDownload info
                                    return! loop state
                                | Some listener ->
                                    listener <| ServiceMessages.AddDownload info
                                    return! loop state

                            | RemoveDownload info ->
                                match state.ServiceListener with
                                | None ->
                                    // when no listener is present and the message was send, than I wait for the service to start
                                    do! Async.Sleep 1000
                                    inbox.Post <| RemoveDownload info
                                    return! loop state
                                | Some listener ->
                                    listener <| ServiceMessages.RemoveDownload info
                                    return! loop state

                            | RegisterServiceListener listener ->
                                return! loop { state with ServiceListener = Some listener }

                            | SignalServiceCrashed ex ->
                                Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                                return! loop { state with ServiceListener = None }

                            | SignalError (info,error) ->
                                state.ErrorEventListener
                                |> Option.map (fun handler -> handler (info, error) |> Async.RunSynchronously)
                                |> ignore
                                return! loop state

                            | SignalServiceShutDown ->
                                state.ShutdownEvent
                                |> Option.map (fun handler -> handler () |> Async.RunSynchronously)
                                |> ignore
                                return! loop { state with ServiceListener = None }

                            | RegisterShutDownListener handler ->
                                return! loop { state with ShutdownEvent = Some handler }

                            | RegisterErrorListener handler ->
                                return! loop { state with ErrorEventListener = Some handler }

                            | AddInfoListener (key,handler) ->
                                if not (state.Listeners |> List.exists (fun (k,_) -> k = key)) then
                                    return! loop { state with Listeners = state.Listeners @ [(key,handler)] }
                                else
                                    return! loop state
                            | RemoveInfoListener (key) ->
                                let newState =
                                    { state with Listeners = state.Listeners |> List.filter (fun (k,_) -> k <> key) }
                                return! loop newState
                            | SendInfo info ->
                                state.Listeners 
                                |> List.map (fun (_,handler) -> async { do! handler(info) })
                                |> Async.Sequential
                                |> Async.RunSynchronously
                                |> ignore

                                return! loop state
                            | GetState reply ->
                                match state.ServiceListener with
                                | None ->
                                    reply.Reply(None)
                                    return! loop state
                                | Some listener ->
                                    listener <| ServiceMessages.GetState reply
                                    return! loop state


                        }

                    loop { Listeners = []; ShutdownEvent = None; ServiceListener = None; ErrorEventListener = None }
            )
        )



    let startService () =
        downloadServiceCallback.Force().Post <| StartService


    let shutDownService () =
        downloadServiceCallback.Force().Post <| ShutDownService


    let startDownloads () =
        downloadServiceCallback.Force().Post <| StartDownloads


    let addDownload download =
        downloadServiceCallback.Force().Post <| AddDownload download


    let removeDownload download  =
        downloadServiceCallback.Force().Post <| RemoveDownload download


    let registerServiceListener listener =
        downloadServiceCallback.Force().Post <| RegisterServiceListener listener


    let signalError error =
        downloadServiceCallback.Force().Post <| SignalError error 


    let signalServiceCrashed ex =
        downloadServiceCallback.Force().Post <| SignalServiceCrashed ex


    let registerErrorListener errorHandler =
        downloadServiceCallback.Force().Post <| RegisterErrorListener errorHandler


    let addInfoListener name listenerCallback =
        downloadServiceCallback.Force().Post <| AddInfoListener (name, listenerCallback)


    let removeInfoListener name =
        downloadServiceCallback.Force().Post <| RemoveInfoListener name


    let sendInfo info =
        downloadServiceCallback.Force().Post <| SendInfo info


    let registerShutDownEvent shutDownEvent =
         downloadServiceCallback.Force().Post <| RegisterShutDownListener shutDownEvent


    let signalShutDownService () =
        downloadServiceCallback.Force().Post <| SignalServiceShutDown


    let getCurrentState () =
        downloadServiceCallback.Force().PostAndAsyncReply(fun reply -> GetState reply)


    module External =
        

        type Msg =
            | AddDownload of DownloadInfo
            | RemoveDownload of DownloadInfo
            | StartDownload
            | DownloadError of ComError
            | FinishedDownload of DownloadInfo * WebAccess.Downloader.DownloadResult
            | ShutDownService
            | UpdateNotification of DownloadInfo * int
            | GetState of AsyncReplyChannel<DownloadServiceState>


        let createExternalDownloadService 
            startDownload
            shutDownExternalService 
            updateNotification =
            MailboxProcessor<Msg>.Start(
                fun inbox ->
                    let rec loop (state:DownloadServiceState) =
                        async {
                            let! msg = inbox.Receive()
                                
                            match msg with
                            | StartDownload ->
                                match state.CurrentDownload with
                                | Some _ ->
                                    // when currently a download is running, than wait
                                    do! Async.Sleep 3000
                                    inbox.Post StartDownload
                                    return! loop state
                                | None ->
                                    let openDownloads =  
                                        state.Downloads 
                                        |> List.filter (fun i -> i.State = Open)

                                    let download =
                                        openDownloads
                                        |> List.tryHead

                                    match download with
                                    | None ->
                                        // change failed network downloads to state open, if there where any
                                        if state.Downloads |> List.exists (fun i -> match i.State with | Failed (ComError.Network _) -> true | _ -> false) then
                                            let newState =
                                                { state with
                                                    Downloads = 
                                                        state.Downloads 
                                                        |> List.map (fun i -> match i.State with | Failed (ComError.Network _) -> { i with State = Open } | _ -> i)
                                                }
                                            // wait a moment to try again
                                            do! Async.Sleep 30000
                                            inbox.Post StartDownload
                                            return! loop newState
                                        else
                                            // no failed download than shut down the service
                                            inbox.Post ShutDownService
                                            return! loop state

                                    | Some download ->
                                        let download = 
                                            { download 
                                                with 
                                                    State = DownloadState.Running (0,0)
                                            }

                                        let newState = 
                                            { state with 
                                                CurrentDownload = Some download 
                                                Downloads = state.Downloads |> List.map (fun i -> if i.AudioBook.Id = download.AudioBook.Id then download else i) 
                                            }

                                        // start download
                                        startDownload inbox download |> Async.Start

                                        return! loop newState

                            | DownloadError error ->
                                match state.CurrentDownload with
                                | None ->
                                    return! loop state
                                | Some download ->
                                    let download = 
                                        { download 
                                            with 
                                                State = Failed error
                                        }

                                    let newState = 
                                        { state with 
                                            CurrentDownload = None 
                                            Downloads = state.Downloads |> List.map (fun i -> if i.AudioBook.Id = download.AudioBook.Id then download else i) 
                                        }


                                    signalError (download, error)
                                
                                    return! loop newState

                            | FinishedDownload (info,downloadResult) ->
                                match state.CurrentDownload with
                                | None ->
                                    return! loop state
                                | Some download ->
                                    let download = 
                                        { download 
                                            with 
                                                State = Finished downloadResult
                                        }

                                    let newState = 
                                        { state with 
                                            CurrentDownload = None 
                                            Downloads = state.Downloads |> List.map (fun i -> if i.AudioBook.Id = download.AudioBook.Id then download else i) 
                                        }
                                    
                                    // store file infos
                                    match info.AudioBook.State.DownloadedFolder with
                                    | None ->
                                        ()
                                    | Some folder ->
                                        let! files = Files.getMp3FileList folder
                                        let fileInfo = {
                                            Id = download.AudioBook.Id
                                            AudioFiles = files |> List.sortBy (fun f -> f.FileName)
                                        }
                                        let! _ = DataBase.insertAudioBookFileInfos [| fileInfo |]
                                        ()
                                    


                                    // send info that the download is complete
                                    sendInfo download

                                    // start next Download
                                    inbox.Post StartDownload

                                    return! loop newState

                            | AddDownload download ->
                                if (state.Downloads |> List.exists (fun i -> i.AudioBook.Id = download.AudioBook.Id)) then
                                    return! loop state
                                else
                                    return! loop { state with Downloads = state.Downloads @ [ download ] }

                            | RemoveDownload download ->
                                let item =
                                    state.Downloads
                                    |> List.tryFind (fun i -> i.AudioBook.Id = download.AudioBook.Id)

                                match item with
                                | None ->
                                    // do nothing
                                    return! loop state
                                | Some item ->
                                    match item.State with
                                    | Running
                                    | Finished ->
                                        return! loop state
                                    | Open _ 
                                    | Failed _ ->
                                        return! loop { state with Downloads = state.Downloads |> List.filter (fun i -> i.AudioBook.Id <> item.AudioBook.Id ) }

                            | ShutDownService ->
                                signalShutDownService ()
                                shutDownExternalService ()
                                
                                return! loop { Downloads = []; CurrentDownload = None }

                            | GetState reply ->
                                reply.Reply(state)
                                return! loop state

                            | UpdateNotification (info,percent) ->
                                let openCount = 
                                    state.Downloads |> List.filter (fun i -> match i.State with | Open | Failed -> true | _ -> false) |> List.length 
                                let allCount = state.Downloads  |> List.length// |> List.filter (fun i -> match i.State with | Finished -> true | _ -> false)

                                let stateText  =
                                    sprintf "(noch %i von %i) %s" (openCount + 1) allCount info.AudioBook.FullName

                                let stateTitle =
                                    sprintf "Lade runter... (%i %%)" percent

                                updateNotification stateTitle stateText

                                return! loop state

                        }

                    loop { Downloads = []; CurrentDownload = None }
             )


        let downloadServiceListener (downloadServiceMailbox:MailboxProcessor<Msg>) msg =
            match msg with
            | ServiceMessages.AddDownload download ->
                downloadServiceMailbox.Post <| AddDownload download
                    
            | ServiceMessages.RemoveDownload download ->
                downloadServiceMailbox.Post <| RemoveDownload download

            | ServiceMessages.StartDownloads ->
                downloadServiceMailbox.Post <| StartDownload
                ()
            | ServiceMessages.ShutDownService ->
                downloadServiceMailbox.Post <| ShutDownService
                ()
            | ServiceMessages.GetState reply ->
                let state = downloadServiceMailbox.TryPostAndReply(fun c -> GetState c)
                reply.Reply(state)


        let startDownload (inbox:MailboxProcessor<Msg>) (info:DownloadInfo) =

                    

                    
                    let updateStateDownloadInfo newState (downloadInfo:DownloadInfo) =
                            {downloadInfo with State = newState}
                            

                    async {

                        //let mutable mutDemoData = info

                        let updateProgress (c,a) =
                            let factor = if a = 0 then 0.0 else (c |> float) / (a |> float)
                            let percent = factor * 100.0 |> int
                            inbox.Post <| UpdateNotification (info,percent)
                            let newState = updateStateDownloadInfo  (Running (a,c)) info
                            sendInfo newState

                        match info.CookieContainer with
                        | None ->
                            inbox.Post (DownloadError <| ComError.SessionExpired "session expired")
                        | Some cc ->

                            let! res = 
                                WebAccess.Downloader.downloadAudiobook 
                                    cc
                                    updateProgress
                                    info.AudioBook

                            match res with
                            | Error error ->
                                inbox.Post <| DownloadError error
                            | Ok result ->
                                
                                let newAb = 
                                    { info.AudioBook with 
                                        Thumbnail = result.Images |> Option.map (fun i -> i.Thumbnail)
                                        Picture = result.Images |> Option.map (fun i -> i.Image)
                                        State =
                                            { info.AudioBook.State with 
                                                Downloaded = true
                                                DownloadedFolder = Some result.TargetFolder
                                            }
                                    }

                                let! saveResult = DataBase.updateAudioBookInStateFile newAb
                                let newInfo = {
                                    info with AudioBook = newAb
                                }
                                match saveResult with
                                | Error msg ->
                                    inbox.Post <| DownloadError (ComError.Other msg)
                                | Ok _ ->
                                    inbox.Post <| FinishedDownload (newInfo,result)
                    }






