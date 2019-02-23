
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



module DependencyServices =

    open Global

    type IAndroidDownloadFolder = 
        abstract member GetAndroidDownloadFolder:unit -> string 

    

    type IAudioPlayer = 

        abstract member OnInfo: (AudioPlayerInfo -> unit) option with get,set
        abstract member OnUpdateState: (AudioPlayerState -> unit) option with get,set
        abstract member CurrentInfo: AudioPlayerInfo option with get
        abstract member CurrentAudiobook: AudioBook option with get

        abstract member IsStarted: bool with get
        //// triggers to get async position and duration via onInfo Handler
        //abstract member GetInfo: unit -> Async<unit>
        

        abstract member RunService: AudioBook -> (string * int) list -> Async<unit>
        abstract member StopService: unit -> Async<unit>
        //abstract member GetRunningService: unit -> IAudioPlayer option

        abstract member StartAudio: AudioPlayerInfo -> unit
        abstract member StopAudio: unit -> unit
        abstract member TogglePlayPause: unit -> unit
        abstract member MoveForward: unit -> unit
        abstract member MoveBackward: unit -> unit
        abstract member GotToPosition: int -> unit
        abstract member UpdateMetaData: AudioBook -> unit



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




module FileAccess =

    open Consts
    open LiteDB
    open LiteDB.FSharp
    

    let mapper = FSharpBsonMapper()    


    let initAppFolders () =
        if not (Directory.Exists(currentLocalDataFolder)) then
            Directory.CreateDirectory(currentLocalDataFolder) |> ignore
        if not (Directory.Exists(stateFileFolder)) then
            Directory.CreateDirectory(stateFileFolder) |> ignore
    
    
    let loadAudioBooksStateFile () =
        async {
            try
                initAppFolders ()
                let! res = asyncFunc (fun () ->
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
                )

                return res |> Ok
            with
            | _ as e -> return Error e.Message

        }

    let loadDownloadedAudioBooksStateFile () =
        async {
            try
                initAppFolders ()
                let! res = asyncFunc (fun () ->
                    use db = new LiteDatabase(audioBooksStateDataFile, mapper)
                    let audioBooksCol =
                        db.GetCollection<AudioBook>("audiobooks")
                    audioBooksCol.EnsureIndex(fun i -> i.State.Downloaded) |> ignore

                    let audioBooks = 
                        audioBooksCol
                            .Find(fun x -> x.State.Downloaded)
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
                )

                return res |> Ok
            with
            | _ as e -> return Error e.Message

        }

    let insertNewAudioBooksInStateFile (audioBooks:AudioBook[]) =
        async {
            try
            
                let! res = asyncFunc (fun () ->
                    use db = new LiteDatabase(audioBooksStateDataFile, mapper)
                    let audioBooksCol = 
                        db.GetCollection<AudioBook>("audiobooks")
                    
                    audioBooksCol.InsertBulk(audioBooks) |> ignore
                    
                )

                return res |> Ok
            with
            | _ as e -> return Error e.Message

        }


    let updateAudioBookInStateFile (audioBook:AudioBook) =
        async {

            let! res = asyncFunc (fun () ->
                use db = new LiteDatabase(audioBooksStateDataFile, mapper)
                let audioBooks = db.GetCollection<AudioBook>("audiobooks")
                audioBooks.Update(audioBook)
            )

            if res 
            then return (Ok ())
            else return (Error Translations.current.ErrorDbWriteAccess)
            
        }


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
    open FileAccess
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
    
                match! getDownloadPage cc with
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
                        match! (abDownloadUrl |> getDownloadUrl cookies) with
                        | Error e -> 
                            return Error e

                        | Ok url -> 
                            try
                                let! resp = Http.AsyncRequestStream(url,httpMethod=HttpMethod.Get)

                                let targetFileName = Path.Combine(audioBookFolder,audiobook.FullName.Replace(" ","_") + ".zip")
                                if (resp.StatusCode <> 200) then 
                                    return Error (Other (sprintf "download statuscode %i" resp.StatusCode))
                                else
                                
                                    let unzipTargetFolder = Path.Combine(audioBookFolder,"audio")
                                    if not (Directory.Exists(unzipTargetFolder)) then
                                        Directory.CreateDirectory(unzipTargetFolder) |> ignore
                                
                                    let fileSize = 
                                        (resp.Headers
                                        |> HttpHelpers.getFileSizeFromHttpHeadersOrDefaultValue 0) / (1024 * 1024)
                                
                                
                                    use zipStream = new ZipInputStream(resp.ResponseStream)

                                    let mutable zipStreamFullLength = 0

                                    let zipSeq =
                                        seq {
                                            let mutable entryAvailable = true
                                            while entryAvailable do
                                                match zipStream.GetNextEntry() with
                                                | null ->
                                                    entryAvailable <- false
                                                | entry -> 
                                                    zipStreamFullLength <- zipStreamFullLength + (zipStream.Length |> int)
                                                    yield (entry, zipStreamFullLength)
                                            
                                        }

                                    let buffer:byte[] = Array.zeroCreate (500*1024)

                                    let copyStream (src:Stream) (dst:Stream) initProgress entrySize =
                                    
                                        let mutable copying = true
                                        let mutable progress = initProgress
                                        while copying do
                                            let bytesRead = src.Read(buffer,0,buffer.Length)
                                            progress <- progress + bytesRead
                                            updateProgress (progress / (1024 * 1024), entrySize / (1024 * 1024))
                                            if bytesRead > 0 then
                                                dst.Write(buffer, 0, bytesRead)
                                            else
                                                dst.Flush()
                                                copying <- false
                                        progress


                                    let processMp3File initProgress zipStreamLength (entry:ZipEntry) =
                                        let name = Path.GetFileName(entry.Name)
                                        let extractFullPath = Path.Combine(unzipTargetFolder,name)
                                        if (File.Exists(extractFullPath)) then
                                            File.Delete(extractFullPath)

                                        use streamWriter = File.Create(extractFullPath)
                                        let progress = copyStream zipStream streamWriter initProgress zipStreamLength
                                        streamWriter.Close()
                                        progress                    


                                    let processPicFile initProgress entrySize =
                                        let mutable progress = initProgress
                                        let imageFullName = Path.Combine(audioBookFolder,audiobook.FullName + ".jpg")
                                        if not (File.Exists(imageFullName)) then
                                            use streamWriter = File.Create(imageFullName)
                                            progress <- copyStream zipStream streamWriter initProgress entrySize
                                            streamWriter.Close()                                        

                                        let thumbFullName = Path.Combine(audioBookFolder,audiobook.FullName + ".thumb.jpg")
                                                
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

                                
                                    let mutable globalProgress = 0

                                    do! asyncFunc(fun () ->
                                        zipSeq
                                        |> Seq.iter (
                                            fun (entry, streamLength) ->
                                                match entry with
                                                | ZipHelpers.Mp3File ->
                                                    globalProgress <- (entry |> processMp3File globalProgress streamLength)
                                                | ZipHelpers.PicFile ->
                                                    globalProgress <- (processPicFile globalProgress streamLength)
                                                | _ -> ()
                                        )
                                    )
                                
                                    zipStream.Close()
                                    resp.ResponseStream.Close()       
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

module SecureLoginStorage =

    let private secStoreUsernameKey = "perryRhodanAudioBookUsername"
    let private secStorePasswordKey = "perryRhodanAudioBookPassword"
    let private secStoreRememberLoginKey = "perryRhodanAudioBookRememberLogin"

    let getSecuredValue key =
        async {
            let! value =  SecureStorage.GetAsync(key) |> Async.AwaitTask
            return if value  = null then None else Some value
        }

    let setSecuredValue value key =
        async {
            do! SecureStorage.SetAsync(key,value) |> Async.AwaitTask            
        }

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
        



