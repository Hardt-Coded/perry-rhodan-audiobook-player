namespace PerryRhodan.AudiobookPlayer

open System
open System.IO
open System.Net
open System.Net.Http
open Common
open Dependencies
open Domain
open HtmlAgilityPack
open Microsoft.ApplicationInsights.DataContracts
open PerryRhodan.AudiobookPlayer.Services.DataBaseCommon
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open FsToolkit.ErrorHandling
open System.Net.Sockets
open FsHttp
open Services.Consts
open ICSharpCode.SharpZipLib.Zip
open SkiaSharp



module DownloaderCommon =

    type UpdateProgress = {
            UpdateProgress:int * int -> unit
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


    type ImagePaths = {
            Image:string
            Thumbnail:string
        }

    type DownloadResult = {
        TargetFolder:string
        Images: ImagePaths option
    }


    let copyStream (src:Stream) (dst:Stream) =
        let buffer:byte[] = Array.zeroCreate (1024*1024)
        let dowloadStreamSeq =
            seq {
                let mutable copying = true
                while copying do
                    let bytesRead = src.Read(buffer,0,buffer.Length)
                    if bytesRead > 0 then
                        dst.Write(buffer, 0, bytesRead)
                        yield bytesRead
                    else
                        dst.Flush()
                        copying <- false
            }

        dowloadStreamSeq



module WebAccessCommon =

    let httpHandlerService = lazy DependencyService.Get<IAndroidHttpMessageHandlerService>()
    let currentHttpClientHandler = lazy httpHandlerService.Force().GetHttpMesageHandler()
    let currentCookieContainer = lazy httpHandlerService.Force().GetCookieContainer()
    let httpClient = lazy (new HttpClient(currentHttpClientHandler.Force()))
    let useAndroidHttpClient redirect = (
        httpHandlerService.Force().SetAutoRedirect redirect
        fun _ -> httpClient.Force()
    )


    let handleException f =
        task {
            try
                let! res = f()
                return (Ok res)
            with
            | exn ->
                let ex = exn.GetBaseException()
                Global.telemetryClient.TrackException ex
                match ex with
                | :? WebException | :? SocketException ->
                    return Error (Network Translations.current.NetworkError)
                | :? TimeoutException ->
                    return Error (Network Translations.current.NetworkTimeoutError)
                | _ ->
                    return Error (Other Translations.current.InternalError)
        }



    let getAudiobooksOnlineBase
        getDownloadPage
        (parseShopDownloadData: string -> Result<AudioBook[],string>) 
        cookies =
        task {
            initAppFolders ()

            let! html = getDownloadPage cookies
            let audioBooks =
                html
                |> Result.bind (fun d -> parseShopDownloadData d |> Result.mapError ComError.Other)
                |> Result.map (Array.distinctBy (fun (ab: AudioBook) -> ab.Id))


            return audioBooks
        }



[<RequireQualifiedAccess>]
module OldShopWebAccessService =

    open WebAccessCommon

    let baseUrl = "https://www.einsamedien.de/"



    let login username password =
        task {

            let! res =
                fun () ->
                    http {
                        POST $"{baseUrl}butler.php"
                        body
                        formUrlEncoded [
                            "action","login"
                            "username",username
                            "password",password
                        ]
                        config_transformHttpClient (useAndroidHttpClient false)
                    }
                    |> Request.sendTAsync
                |> handleException

            return
                res
                |> Result.bind(
                    fun resp ->
                        let location =
                            resp.headers
                            |> Seq.filter (fun m -> m.Key = "Location")
                            |> Seq.tryHead
                        match location with
                        | None ->
                            Global.telemetryClient.TrackTrace("no location header found.")
                            Error (Other Translations.current.UnexpectedServerBehaviorError)
                        | Some v ->

                            let value = v.Value |> Seq.head
                            if value.Contains("98") then Ok None
                            else if value.Contains("61") then
                                let cookies =
                                    currentCookieContainer.Force().GetCookies(Uri(baseUrl))
                                    |> Seq.cast<Cookie>
                                    |> Seq.map (fun cc -> (cc.Name,cc.Value))
                                    |> Map.ofSeq
                                Ok (Some cookies)
                            else Ok None
                )


        }





    let getDownloadPage (cc:Map<string,string>) =
        task {
            let seqCC = cc |> Map.toSeq

            let! res =
                fun () ->
                    task {
                        let! resp =
                            http {
                                GET $"{baseUrl}index.php?id=61"
                                headers [
                                    "Cookie", seqCC |> Seq.map (fun (k,v) -> $"{k}={v}") |> String.concat "; "
                                ]
                                config_transformHttpClient (useAndroidHttpClient true)
                            }
                            |> Request.sendTAsync

                        return! resp |> Response.toTextAsync
                    }
                |> handleException

            return res
                |> Result.bind (
                    fun html ->
                        if html.Contains("<input name=\"username\"") then Error (SessionExpired Translations.current.SessionExpired)
                        else Ok html
                )
        }


    let getAudiobooksOnline = getAudiobooksOnlineBase getDownloadPage parseOldShopDownloadData



    let private getDownloadUrl cookies url =
        task {
            let seqCC = cookies |> Map.toSeq

            let! res =
                fun () ->
                    http {
                        GET $"{baseUrl}{url}"
                        headers [
                            "Cookie", seqCC |> Seq.map (fun (k,v) -> $"{k}={v}") |> String.concat "; "
                        ]
                        config_transformHttpClient (useAndroidHttpClient false)
                    }
                    |> Request.sendTAsync
                |> handleException

            return
                res
                |> Result.bind (
                    fun resp ->
                        let location = resp.headers |> Seq.filter (fun m -> m.Key = "Location") |> Seq.tryHead
                        match location with
                        | None ->
                            Error (Other Translations.current.NoDownloadUrlFoundError)
                        | Some location ->
                            let downloadUrl = location.Value |> Seq.head
                            if downloadUrl.Contains("index.php?id=98") then
                                Error (SessionExpired Translations.current.SessionExpired)
                            else
                                Ok downloadUrl
                )


        }


    let parseFromPictureUrlInHtml html =
        // parse html and get picture url from element wit id "mainimg"
        let doc = new HtmlDocument()
        doc.LoadHtml(html)
        let img = doc.DocumentNode.SelectSingleNode("//*[@id='mainimg']")
        if img = null then None
        else
            let src = img.Attributes.["src"].Value
            Some src


    let getPictureOnlineUrl (audiobook:AudioBook) =
        task {
            match audiobook.ProductSiteUrl with
            | None ->
                return None

            | Some siteUrl ->
                try
                    let! html =
                        http {
                            GET $"{baseUrl}{siteUrl}"
                            config_transformHttpClient (useAndroidHttpClient false)
                        }
                        |> Request.sendTAsync

                        |> Task.map (fun resp ->
                            if resp.statusCode <> HttpStatusCode.OK then
                                #if DEBUG
                                System.Diagnostics.Trace.WriteLine($"getPictureOnlineUrl: Error {resp.statusCode}")
                                #endif
                                None
                            else
                                Some resp
                        )
                        |> Task.map (fun resp -> resp |> Option.map Response.toText)


                    return html
                        |> Option.bind parseFromPictureUrlInHtml
                        |> Option.map (fun url -> $"{baseUrl}{url}")
                with
                | ex ->
                    #if DEBUG
                    System.Diagnostics.Trace.WriteLine($"getPictureOnlineUrl:  Error {ex.Message}")
                    #endif
                    return None



        }


    module Downloader =

        open DownloaderCommon


        let private processMp3File (updateProgress:UpdateProgress) zipStream unzipTargetFolder (entry:ZipEntry) =
            let scale = (entry.CompressedSize |> float) / (entry.Size |> float)
            let name = Path.GetFileName(entry.Name)
            let extractFullPath = Path.Combine(unzipTargetFolder,name)
            if File.Exists(extractFullPath) then
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


        let private processPicFile (updateProgress:UpdateProgress) zipStream audioBookFolder (audiobook:AudioBook) (entry:ZipEntry) =
            let scale = (entry.CompressedSize |> float) / (entry.Size |> float)
            let imageFullName = Path.Combine(audioBookFolder,$"{audiobook.Id}.jpg")

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


            let thumbFullName = Path.Combine(audioBookFolder,$"{audiobook.Id}.thumb.jpg")

            // try create thumb nail picture if necessary
            if not (File.Exists(thumbFullName)) then

                //use thumbInputStream = File.OpenRead()
                use orig = SKBitmap.Decode(imageFullName)
                use thumb = orig.Resize(SKImageInfo(280, 280), SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                if isNull thumb then
                    ()
                else
                    use thumbImage = SKImage.FromBitmap(thumb)
                    use fileStream = new FileStream(thumbFullName,FileMode.Create)
                    thumbImage.Encode(SKEncodedImageFormat.Jpeg,100).SaveTo(fileStream)

                    fileStream.Close()

            progress


        let downloadAudiobook cookies updateProgress (audiobook:AudioBook) =
            task {
                try
                    Global.telemetryClient.TrackEvent ("AudioBookDownload")
                    let folders = createCurrentFolders ()

                    match audiobook with
                    | { State = { Downloaded = true } } ->
                        return Error (Other "Audiobook already downloaded!")

                    | { DownloadUrl = None } ->
                        return Error (Other "No download url found!")

                    | { DownloadUrl = Some abDownloadUrl } ->
                        let audioBookFolder = Path.Combine(folders.audioBookDownloadFolderBase,$"{audiobook.Id}")
                        if not (Directory.Exists(audioBookFolder)) then
                            Directory.CreateDirectory(audioBookFolder) |> ignore

                        let noMediaFile = Path.Combine(audioBookFolder,".nomedia")
                        if File.Exists(noMediaFile) |> not then
                            do! File.WriteAllTextAsync(noMediaFile,"") |> Async.AwaitTask

                        let! downloadUrl = (abDownloadUrl |> getDownloadUrl cookies)
                        match downloadUrl with
                        | Error e ->
                            return Error e

                        | Ok url ->
                            try
                                let! resp =
                                    http {
                                        GET url
                                        config_transformHttpClient (useAndroidHttpClient true)
                                    }
                                    |> Request.sendAsync

                                if (resp.statusCode <> HttpStatusCode.OK) then
                                    return Error (Other $"download statuscode {resp.statusCode}")
                                else

                                    let unzipTargetFolder = Path.Combine(audioBookFolder,"audio")



                                    if not (Directory.Exists(unzipTargetFolder)) then
                                        Directory.CreateDirectory(unzipTargetFolder) |> ignore


                                    let downloadingFlagFile = Path.Combine(unzipTargetFolder,"downloading")
                                    // create flag file, to determinate download was maybe interrupted!
                                    File.WriteAllText(downloadingFlagFile,"downloading")


                                    let fileSize =
                                        (resp.content.Headers
                                        |> HttpHelpers.getFileSizeFromHttpHeadersOrDefaultValue 0)

                                    let! responseStream = resp |> Response.toStreamAsync
                                    use zipStream = new ZipInputStream(responseStream)

                                    let zipSeq =
                                        seq {
                                            let mutable entryAvailable = true
                                            while entryAvailable do
                                                match zipStream.GetNextEntry() with
                                                | null ->
                                                    entryAvailable <- false
                                                | entry ->
                                                    yield entry

                                        }

                                    let! gloablProgress =
                                        async {
                                            return
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
                                        }


                                    zipStream.Close()
                                    responseStream.Close()

                                    updateProgress (fileSize / (1024 * 1024), fileSize / (1024 * 1024))


                                    let imageFullName = Path.Combine(audioBookFolder,$"{audiobook.Id}.jpg")
                                    let thumbFullName = Path.Combine(audioBookFolder,$"{audiobook.Id}.thumb.jpg")

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
                    Global.telemetryClient.TrackException ex
                    match ex with
                    | :? WebException | :? SocketException ->
                        return Error (Network Translations.current.NetworkError)
                    | :? TimeoutException ->
                        return Error (Network Translations.current.NetworkTimeoutError)
                    | _ ->
                        return Error (Other Translations.current.InternalError)
            }



[<RequireQualifiedAccess>]
module NewShopWebAccessService =

    open WebAccessCommon

    let baseUrl = "https://www.einsamedien-verlag.de/"



    let login username password =
        task {
            let! res =
                fun () ->
                    http {
                        POST $"{baseUrl}account/login"
                        body
                        formUrlEncoded [
                            "redirectTo","frontend.account.home.page"
                            "redirectParameters", "[]"
                            "username",username
                            "password",password
                        ]
                        config_transformHttpClient (useAndroidHttpClient false)
                    }
                    |> Request.sendTAsync
                |> handleException
                |> TaskResult.bind(fun resp ->
                    taskResult {
                        let location =
                            resp.headers
                            |> Seq.filter (fun m -> m.Key = "Location")
                            |> Seq.tryHead
                        match location with
                        | None ->
                            Global.telemetryClient.TrackTrace("login failed.")
                            return! Error <| ComError.Other "Login failed."
                        | Some loc ->
                            let cookies =
                                currentCookieContainer.Force().GetCookies(Uri(baseUrl))
                                |> Seq.cast<Cookie>
                                |> Seq.map (fun cc -> (cc.Name,cc.Value))
                                |> Map.ofSeq
                            return (Some cookies)
                    }
                )

            return res
        }



    let getDownloadPage (cc:Map<string,string>) =
        task {
            let seqCC = cc |> Map.toSeq
            let requestUrl = $"{baseUrl}account/downloads"
        return!
            fun () ->
                http {
                    GET requestUrl
                    headers [
                        "Cookie", seqCC |> Seq.map (fun (k,v) -> $"{k}={v}") |> String.concat "; "
                    ]
                    config_transformHttpClient (useAndroidHttpClient true)
                }
                |> Request.sendTAsync
            |> handleException
            |> TaskResult.bind (fun resp ->
                taskResult {
                    let location =
                        resp.headers
                        |> Seq.filter (fun m -> m.Key = "Location")
                        |> Seq.collect (_.Value)
                        |> Seq.tryHead
                    match location, resp.statusCode with
                    // download page successfully loaded
                    | None, HttpStatusCode.OK ->
                        return! resp |> Response.toTextAsync
                    // redirect to login page
                    | Some loc , _ when loc.Contains("login") ->
                        return! Error (ComError.SessionExpired "Session expired.")
                    | _ ->
                        let entry = new TraceTelemetry()
                        entry.SeverityLevel <- SeverityLevel.Error
                        entry.Message <- "Unexpected server behavior."
                        entry.Properties.Add("StatusCode", resp.statusCode.ToString())
                        entry.Properties.Add("Location", $"{location}")
                        entry.Properties.Add("Url", requestUrl)
                        let! content = resp.content.ReadAsStringAsync()
                        entry.Properties.Add("Content", content)
                        Global.telemetryClient.Track(entry)
                        return! Error (ComError.Other "Unexpected server behavior.")
                }
            )
        }


    let getAudiobooksOnline = getAudiobooksOnlineBase getDownloadPage parseNewShopDownloadData



    module Downloader =

        open DownloaderCommon


        let private processMp3File (updateProgress:UpdateProgress) zipStream unzipTargetFolder (entry:ZipEntry) =
            let scale = (entry.CompressedSize |> float) / (entry.Size |> float)
            let name = Path.GetFileName(entry.Name)
            let extractFullPath = Path.Combine(unzipTargetFolder,name)
            if File.Exists(extractFullPath) then
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


        let private processPicFile (updateProgress:UpdateProgress) zipStream audioBookFolder (audiobook:AudioBook) (entry:ZipEntry) =
            let scale = (entry.CompressedSize |> float) / (entry.Size |> float)
            let imageFullName = Path.Combine(audioBookFolder,$"{audiobook.Id}.jpg")

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


            let thumbFullName = Path.Combine(audioBookFolder,$"{audiobook.Id}.thumb.jpg")

            // try create thumb nail picture if necessary
            if not (File.Exists(thumbFullName)) then

                //use thumbInputStream = File.OpenRead()
                use orig = SKBitmap.Decode(imageFullName)
                use thumb = orig.Resize(SKImageInfo(100, 100), SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                if isNull thumb then
                    ()
                else
                    use thumbImage = SKImage.FromBitmap(thumb)
                    use fileStream = new FileStream(thumbFullName,FileMode.Create)
                    thumbImage.Encode(SKEncodedImageFormat.Jpeg,90).SaveTo(fileStream)

                    fileStream.Close()

            progress


        let downloadAudiobook (cookies:Map<string,string>) updateProgress (audiobook:AudioBook) =
            task {
                try
                    Global.telemetryClient.TrackEvent ("AudioBookDownload")
                    let folders = createCurrentFolders ()

                    match audiobook with
                    | { State = { Downloaded = true } } ->
                        return Error (Other "Audiobook already downloaded!")

                    | { DownloadUrl = None } ->
                        return Error (Other "No download url found!")

                    | { DownloadUrl = Some abDownloadUrl } ->
                        let audioBookFolder = Path.Combine(folders.audioBookDownloadFolderBase,$"{audiobook.Id}")
                        if not (Directory.Exists(audioBookFolder)) then
                            Directory.CreateDirectory(audioBookFolder) |> ignore

                        let noMediaFile = Path.Combine(audioBookFolder,".nomedia")
                        if File.Exists(noMediaFile) |> not then
                            do! File.WriteAllTextAsync(noMediaFile,"") |> Async.AwaitTask


                        try
                            let! resp =
                                http {
                                    GET abDownloadUrl
                                    headers [
                                        "Cookie", cookies |> Map.toList |> List.map (fun (k,v) -> $"{k}={v}") |> String.concat "; "
                                    ]
                                    config_transformHttpClient (useAndroidHttpClient true)
                                }
                                |> Request.sendAsync

                            if (resp.statusCode <> HttpStatusCode.OK) then
                                return Error (Other $"download statuscode {resp.statusCode}")
                            else

                                let unzipTargetFolder = Path.Combine(audioBookFolder,"audio")



                                if not (Directory.Exists(unzipTargetFolder)) then
                                    Directory.CreateDirectory(unzipTargetFolder) |> ignore


                                let downloadingFlagFile = Path.Combine(unzipTargetFolder,"downloading")
                                // create flag file, to determinate download was maybe interrupted!
                                File.WriteAllText(downloadingFlagFile,"downloading")


                                let fileSize =
                                    (resp.content.Headers
                                    |> HttpHelpers.getFileSizeFromHttpHeadersOrDefaultValue 0)

                                let! responseStream = resp |> Response.toStreamAsync
                                use zipStream = new ZipInputStream(responseStream)

                                let zipSeq =
                                    seq {
                                        let mutable entryAvailable = true
                                        while entryAvailable do
                                            match zipStream.GetNextEntry() with
                                            | null ->
                                                entryAvailable <- false
                                            | entry ->
                                                yield entry

                                    }

                                let! gloablProgress =
                                    async {
                                        return
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
                                    }


                                zipStream.Close()
                                responseStream.Close()

                                updateProgress (fileSize / (1024 * 1024), fileSize / (1024 * 1024))


                                let imageFullName = Path.Combine(audioBookFolder,$"{audiobook.Id}.jpg")
                                let thumbFullName = Path.Combine(audioBookFolder,$"{audiobook.Id}.thumb.jpg")

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
                    Global.telemetryClient.TrackException ex
                    match ex with
                    | :? WebException | :? SocketException ->
                        return Error (Network Translations.current.NetworkError)
                    | :? TimeoutException ->
                        return Error (Network Translations.current.NetworkTimeoutError)
                    | _ ->
                        return Error (Other Translations.current.InternalError)
            }
