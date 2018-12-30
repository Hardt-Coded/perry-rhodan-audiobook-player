
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


    type IAndroidDownloadFolder = 
        abstract member GetAndroidDownloadFolder:unit -> string 


    type IAudioPlayer = 

        //abstract member CurrentPosition: int with get
        //abstract member CurrentDuration: int with get
        abstract member LastPositionBeforeStop: int option with get
        abstract member CurrentFile: string option with get

        abstract member OnCompletion: (unit -> unit) option with get,set
        abstract member OnNoisyHeadPhone: (unit -> unit) option with get,set
        abstract member OnInfo: (int * int -> unit) option with get,set

        abstract member PlayFile:string -> int -> Async<unit>
        abstract member Stop:unit -> unit
        abstract member GotToPosition: int -> unit
        // triggers to get async position and duration via onInfo Handler
        abstract member GetInfo: unit -> Async<unit>

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
                    
                    audioBooksCol.InsertBulk(audioBooks)
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
            else return (Error "error storing audiobook data into database.")
            
        }


    let removeAudiobook audiobook = 
        try
            match audiobook.State.DownloadedFolder with
            | None -> Error ("Audiobook is not downloaded!")
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

    let login username password =
        async {
            let! resp = 
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
        
            let location = resp.Headers |> Seq.filter (fun m -> m.Key = "Location") |> Seq.tryHead
            match location with
            | None -> return None
            | Some v ->
                if v.Value.Contains("98") then return None
                else if v.Value.Contains("61") then return (Some resp.Cookies)
                else return None
        }


    let getDownloadPage (cc:Map<string,string>) =
        async {       
            let seqCC = cc |> Map.toSeq
            let! html = Http.AsyncRequestString(baseUrl + "index.php?id=61",cookies = seqCC, httpMethod="GET")
    
            if (html.Contains("<input name=\"username\"")) then return Error (SessionExpired "Session expired!")
            else return Ok html
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
            let! resp = 
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
            if (not (resp.Headers.ContainsKey("Location"))) then
                return Error (Other "DownloadUrl not found!")
            else
                let downloadUrl = resp.Headers.Item "Location"
                if (downloadUrl.Contains("index.php?id=98")) then
                    return Error (SessionExpired "Session expired!")
                else
                    return Ok downloadUrl
        }


    let downloadAudiobook cookies updateProgress audiobook =
        async {
            if (audiobook.State.Downloaded) then 
                return Error (Other "Audiobook alread downloaded!")
            else
                let audioBookFolder = Path.Combine(audioBookDownloadFolderBase,audiobook.FullName)        
                if not (Directory.Exists(audioBookFolder)) then
                    Directory.CreateDirectory(audioBookFolder) |> ignore
                
                match audiobook.DownloadUrl with
                | None -> return Error (Other "no download url for this audiobook available")
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
                                
                                

                                


                                //use fileStream = new FileStream(targetFileName,FileMode.Create)
                                use zipStream = new ZipInputStream(resp.ResponseStream)

                                

                                let zipSeq =
                                    seq {
                                        let mutable entryAvailable = true
                                        while entryAvailable do
                                            match zipStream.GetNextEntry() with
                                            | null ->
                                                entryAvailable <- false
                                            | entry -> 
                                                yield (entry, zipStream.Length)
                                            
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


                                let processMp3File initProgress entrySize (entry:ZipEntry) =
                                    let name = Path.GetFileName(entry.Name)
                                    let extractFullPath = Path.Combine(unzipTargetFolder,name)
                                    if (File.Exists(extractFullPath)) then
                                        File.Delete(extractFullPath)

                                    use streamWriter = File.Create(extractFullPath)
                                    let progress = copyStream zipStream streamWriter initProgress entrySize
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
                                        fun (entry, entrySize) ->
                                            match entry with
                                            | ZipHelpers.Mp3File ->
                                                globalProgress <- (entry |> processMp3File globalProgress (entrySize|>int))
                                            | ZipHelpers.PicFile ->
                                                globalProgress <- (processPicFile globalProgress (entrySize|>int))
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

                                updateProgress (fileSize, fileSize)
                            

                                return Ok (unzipTargetFolder,imageFileNames)
                            with
                            | _ as e -> 
                                let text = e.Message
                                let ex = e
                                return Error (Exception e)
        }


    let loadDescription audiobook =
        async {
            match audiobook.ProductSiteUrl with
            | None -> return Ok (None,None)
            | Some ps ->
                let productPageUri = Uri(baseUrl + ps)
                try
                    let! productPage = Http.AsyncRequestString(productPageUri.AbsoluteUri)
                    if productPage = "" then
                        return Error (Other "ProductPage response is empty.")
                    else
                        let desc = productPage |> Domain.parseProductPageForDescription
                        let img = 
                            productPage 
                            |> Domain.parseProductPageForImage
                            |> Option.map (fun i ->
                                let uri = Uri(baseUrl + i)
                                uri.AbsoluteUri
                            )
                        
                        
                        return Ok (desc,img)
                with
                | _ as e ->
                    return Error (Exception e)
        }

module SecureLoginStorage =

    let private secStoreUsernameKey = "perryRhodanAudioBookUsername"
    let private secStorePasswordKey = "perryRhodanAudioBookPassword"
    let private secStoreRememberLoginKey = "perryRhodanAudioBookRememberLogin"

    let saveLoginCredentials username password rememberLogin =
        async {
            try
                do! SecureStorage.SetAsync(secStoreUsernameKey,username) |> Async.AwaitTask
                do! SecureStorage.SetAsync(secStorePasswordKey,password) |> Async.AwaitTask
                do! SecureStorage.SetAsync(secStoreRememberLoginKey,(if rememberLogin then "Jupp" else "")) |> Async.AwaitTask
                return Ok true
            with
            | _ as e -> return (Error (e.Message))
        }
    
    let loadLoginCredentials () =
        async {
            try
                let! username =  SecureStorage.GetAsync(secStoreUsernameKey) |> Async.AwaitTask
                let! password =  SecureStorage.GetAsync(secStorePasswordKey) |> Async.AwaitTask
                let! rememberLoginStr =  SecureStorage.GetAsync(secStoreRememberLoginKey) |> Async.AwaitTask
                return Ok (username,password,(rememberLoginStr = "Jupp"))
            with
            | _ as e -> return (Error (e.Message))
        }



