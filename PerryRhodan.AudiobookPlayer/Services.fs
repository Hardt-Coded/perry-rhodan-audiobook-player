
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
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open ICSharpCode.SharpZipLib.Zip

open Common
open Plugin.Permissions.Abstractions
open Common.EventHelper




module DependencyServices =

    open Global

    type IAndroidDownloadFolder = 
        abstract member GetAndroidDownloadFolder:unit -> string 

    

    type INotificationService =
        abstract member ShowNotification: string -> unit
        abstract member OnSecondActivity: (string -> unit) option with get,set


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
    let audioBookDownloadFolderBase = Path.Combine(currentLocalDataFolder,"audiobooks")


    let baseUrl = "https://www.einsamedien.de/"




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
        | _ as e -> Error e.Message



    let private updateAudioBookDb (audioBook:AudioBook) =
            use db = new LiteDatabase(audioBooksStateDataFile, mapper)
            let audioBooks = db.GetCollection<AudioBook>("audiobooks")

            if audioBooks.Update(audioBook)
            then (Ok ())
            else (Error Translations.current.ErrorDbWriteAccess)


    let private deleteAudioBookDb (audioBook:AudioBook) =
        use db = new LiteDatabase(audioBooksStateDataFile, mapper)
        let audioBooks = db.GetCollection<AudioBook>("audiobooks")

        let query = Query.Where("FullName", (fun name -> name.AsString = audioBook.FullName))
        let check = audioBooks.Find(query)

        let res = 
            if check |> Seq.length > 0 then               
                audioBooks.Delete(query)
            else
                -1

        if  res > -1
        then (Ok ())
        else (Error Translations.current.ErrorDbWriteAccess)


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

    let storageProcessorErrorEvent = CountedEvent<exn>()
    let storageProcessorAudioBookUpdatedEvent = CountedEvent<AudioBook>()
    let storageProcessorAudioBookAdded = CountedEvent<AudioBook[]>()
    let storageProcessorAudioBookDeletedEvent = CountedEvent<AudioBook>()
    
    module Events =

        let storageProcessorOnError = storageProcessorErrorEvent.Publish
        let storageProcessorOnAudiobookUpdated = storageProcessorAudioBookUpdatedEvent.Publish
        let storageProcessorOnAudiobookAdded = storageProcessorAudioBookAdded.Publish
        let storageProcessorOnAudiobookDeleted= storageProcessorAudioBookDeletedEvent.Publish



    // lazy evaluation, to avoid try loading data without permission
    let private storageProcessor = 
        lazy 
        MailboxProcessor<StorageMsg>.Start(
            fun inbox ->
                async {
                    try
                        // init stuff
                        initAppFolders ()

                        let audioBooks =
                            loadAudioBooksFromDb ()
                    
                        let rec loop state =
                            async {
                                let! msg = inbox.Receive()
                                match msg with
                                | UpdateAudioBook (audiobook,replyChannel) ->
                                    let dbRes = updateAudioBookDb audiobook
                                    match dbRes with
                                    | Ok _ ->
                                        let newState =
                                            state
                                            |> Array.Parallel.map (fun i ->
                                                if i.FullName = audiobook.FullName then
                                                    audiobook
                                                else
                                                    i
                                            )
                                        storageProcessorAudioBookUpdatedEvent.Trigger(audiobook)
                                        replyChannel.Reply(Ok ())
                                        return! (loop newState)
                                    | Error e ->
                                        replyChannel.Reply(Error e)
                                        return! (loop state)

                                | InsertAudioBooks (audiobooks,replyChannel) ->
                                    let dbRes = insertNewAudioBooksDb audiobooks
                                    match dbRes with
                                    | Ok _ ->
                                        let newState =
                                            state |> Array.append audiobooks

                                        storageProcessorAudioBookAdded.Trigger(audiobooks)
                                        replyChannel.Reply(Ok ())
                                        return! (loop newState)
                                    | Error e ->
                                        replyChannel.Reply(Error e)
                                        return! (loop state)

                                | GetAudioBooks replyChannel ->
                                    replyChannel.Reply(state |> Array.sortBy (fun i -> i.FullName))
                                    return! (loop state)

                                | RemoveAudiobookFromDatabase (audiobook,replyChannel) ->
                                    let dbRes = deleteAudioBookDb audiobook
                                    match dbRes with
                                    | Ok _ ->
                                        let newState =
                                            state |> Array.filter (fun i -> i.FullName <> audiobook.FullName)

                                        storageProcessorAudioBookDeletedEvent.Trigger(audiobook)
                                        replyChannel.Reply(Ok ())
                                        return! (loop newState)
                                    | Error e ->
                                        replyChannel.Reply(Error e)
                                        return! (loop state)

                                return! (loop state)
                                        
                            }
                        
                        return! (loop audioBooks)
                    with
                    | _ as ex ->
                        storageProcessorErrorEvent.Trigger(ex)
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


    let removeAudiobook audiobook = 
        try
            match audiobook.State.DownloadedFolder with
            | None -> Error (Translations.current.ErrorDownloadAudiobook)
            | Some folder ->
                Directory.Delete(folder,true)
                Ok ()
        with
        | _ as e -> Error (e.Message)


    
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
                                (audioFiles |> Seq.length) > 0
                            else false
                        let audioBookPath = if hasAudioBook then Some audioPath else None
                        Some (audioBookName, pic, thumb, hasAudioBook, audioBookPath)
            )
            |> Array.filter (fun i -> i.IsSome)
            |> Array.Parallel.map (fun i-> i.Value)
    
    
    let syncPossibleDownloadFolder audiobooks =
        let audioBooksOnDevice = parseDownloadFolderForAlreadyDownloadedAudioBooks ()
        audiobooks
        |> Array.map (
            fun i ->
                let onDeviceItem = audioBooksOnDevice |> Array.tryFind (fun (title,_,_,_,_) -> title = i.FullName)
                match onDeviceItem with
                | None -> i
                | Some (_, picPath, thumbPath, hasAudioBook, audioBookPath) ->
                    let newState = {i.State with Downloaded = hasAudioBook; DownloadedFolder=audioBookPath}
                    {i with State = newState; Picture = picPath; Thumbnail = thumbPath}
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
                Crashes.TrackError(ex)
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
        async {
            initAppFolders ()        
    
            match cookies with
            | None -> return Ok [||]
            | Some cc ->
                let! dPage = getDownloadPage cc
                match dPage with
                | Error e -> return Error e
                | Ok html ->
                    let audioBooks =
                        html
                        |> parseDownloadData
    
                    return Ok audioBooks
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
        
        //let copyStream (src:Stream) (dst:Stream) initProgress scale updateProgress fileSize buffer =
        //    let mutable copying = true
        //    let mutable progress = initProgress
        //    while copying do
        //        let bytesRead = src.Read(buffer,0,buffer.Length)
        //        let toAdd = ((bytesRead |> float) * scale) |> int
        //        progress <- progress + toAdd
        //        updateProgress (progress / (1024 * 1024), fileSize / (1024 * 1024))
        //        if bytesRead > 0 then
        //            dst.Write(buffer, 0, bytesRead)
        //        else
        //            dst.Flush()
        //            copying <- false
        //    progress


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
                use thumb = SixLabors.ImageSharp.Image.Load(imageFullName)
                thumb.Mutate(fun x -> 
                    x.Resize(200,200) |> ignore
                    ()
                    ) |> ignore                                        

                use fileStream = new FileStream(thumbFullName,FileMode.Create)
                thumb.SaveAsJpeg(fileStream)
                fileStream.Close()
        
            progress


        


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
                                                Some (imageFullName,thumbFullName)
                                            else None

                                        return Ok (unzipTargetFolder,imageFileNames)
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
                    Crashes.TrackError(ex)
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
            return if value  = null then None else Some value
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
            | _ as e -> return (Error (e.Message))
        }
    
    let loadLoginCredentials () =
        async {
            try
                let! username =  secStoreUsernameKey |> getSecuredValue
                let! password =  secStorePasswordKey|> getSecuredValue
                let! rememberLoginStr = secStoreRememberLoginKey |> getSecuredValue
                return Ok (username,password,(rememberLoginStr = Some "Jupp"))
            with
            | _ as e -> return (Error (e.Message))
        }

module Files =

    let fromTimeSpan (ts:TimeSpan) =
        ts.TotalMilliseconds |> int

    let getMp3FileList folder =
        async {
            let! files = 
                asyncFunc( 
                    fun () ->  Directory.EnumerateFiles(folder, "*.mp3")
                )
            
            let! res =
                asyncFunc (fun () ->
                    files 
                    |> Seq.toList 
                    |> List.map (
                        fun i ->
                            use tfile = TagLib.File.Create(i)
                            (i,tfile.Properties.Duration |> fromTimeSpan)
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

        



