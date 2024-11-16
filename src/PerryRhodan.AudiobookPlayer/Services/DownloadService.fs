namespace PerryRhodan.AudiobookPlayer.Services

open Common
open Dependencies
open Domain
open PerryRhodan.AudiobookPlayer
open PerryRhodan.AudiobookPlayer.DownloaderCommon
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open Services


module DownloadService =

    type DownloadState =
        | Open
        | Running of int * int
        | Finished of DownloadResult * AudioBookAudioFilesInfo option
        | Failed of ComError


    type DownloadInfo =
        {
            State:DownloadState
            AudioBook:AudioBook
        }
        static member Create audiobook =
            { State = Open; AudioBook = audiobook }




    type private Listener = string * (DownloadInfo -> Async<unit>)

    type private ShutDownEventHandler = unit -> Async<unit>

    type private ErrorEventHandler = DownloadInfo * ComError -> Async<unit>

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

    type ServiceListener =  ServiceMessages -> unit


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

    type DownloadCallbackService = {
        StartService: unit -> unit
        ShutDownService: unit -> unit
        StartDownloads: unit -> unit
        AddDownload: DownloadInfo -> unit
        RemoveDownload: DownloadInfo -> unit
        RegisterServiceListener: ServiceListener -> unit
        SignalError: DownloadInfo * ComError -> unit
        SignalServiceCrashed: exn -> unit
        RegisterErrorListener: ErrorEventHandler -> unit
        AddInfoListener: string -> (DownloadInfo -> Async<unit>) -> unit
        RemoveInfoListener: string -> unit
        SendInfo: DownloadInfo -> unit
        RegisterShutDownEvent: ShutDownEventHandler -> unit
        SignalShutDownService: unit -> unit
        GetCurrentState: unit -> Async<DownloadServiceState option>
    }
    
    
    let createDownloadServiceCallback (shop:Shop) =
        let downloadServiceCallback =
            MailboxProcessor<Msg>.Start(
                let downloadService = DependencyService.Get<IDownloadService>()
                fun inbox ->
                    let rec loop (state:HandlerState) =
                         async {
                             try
                                 let! msg = inbox.Receive()

                                 match msg with
                                 | StartService ->
                                     match state.ServiceListener with
                                     | None ->
                                         downloadService.StartDownload shop
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
                                     Global.telemetryClient.TrackException ex
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
                                        // replace listener
                                        return! loop { state with Listeners = state.Listeners |> List.map (fun (k,h) -> if k = key then (key,handler) else (k,h)) }

                                 | RemoveInfoListener key ->
                                     let newState =
                                         { state with Listeners = state.Listeners |> List.filter (fun (k,_) -> k <> key) }
                                     return! loop newState
                                 | SendInfo info ->
                                     // send info only to listener with proper name
                                     let listenerName = $"AudioBook{info.AudioBook.Id}Listener"
                                     do!
                                         state.Listeners
                                         |> List.tryFind (fun (k,_) -> k = listenerName)
                                         |> Option.map (fun (_,handler) -> async { do! handler(info) })
                                         |> Option.defaultValue (async { return () })

                                     return! loop state

                                 | GetState reply ->
                                     match state.ServiceListener with
                                     | None ->
                                         reply.Reply(None)
                                         return! loop state

                                     | Some listener ->
                                         listener <| ServiceMessages.GetState reply
                                         return! loop state

                             with
                             | ex ->
                                Global.telemetryClient.TrackException ex
                                do! Notifications.showErrorMessage ex.Message |> Async.AwaitTask
                                return! loop state

                        }

                    loop { Listeners = []; ShutdownEvent = None; ServiceListener = None; ErrorEventListener = None }

                 )


        let startService () =
            downloadServiceCallback.Post <| StartService


        let shutDownService () =
            downloadServiceCallback.Post <| ShutDownService


        let startDownloads () =
            downloadServiceCallback.Post <| StartDownloads


        let addDownload download =
            downloadServiceCallback.Post <| AddDownload download


        let removeDownload download  =
            downloadServiceCallback.Post <| RemoveDownload download


        let registerServiceListener listener =
            downloadServiceCallback.Post <| RegisterServiceListener listener


        let signalError error =
            downloadServiceCallback.Post <| SignalError error


        let signalServiceCrashed ex =
            downloadServiceCallback.Post <| SignalServiceCrashed ex


        let registerErrorListener errorHandler =
            downloadServiceCallback.Post <| RegisterErrorListener errorHandler


        let addInfoListener name listenerCallback =
            downloadServiceCallback.Post <| AddInfoListener (name, listenerCallback)


        let removeInfoListener name =
            downloadServiceCallback.Post <| RemoveInfoListener name


        let sendInfo info =
            downloadServiceCallback.Post <| SendInfo info


        let registerShutDownEvent shutDownEvent =
             downloadServiceCallback.Post <| RegisterShutDownListener shutDownEvent


        let signalShutDownService () =
            downloadServiceCallback.Post <| SignalServiceShutDown


        let getCurrentState () =
            downloadServiceCallback.PostAndAsyncReply(fun reply -> GetState reply)
            
        {
            StartService = startService
            ShutDownService = shutDownService
            StartDownloads = startDownloads
            AddDownload = addDownload
            RemoveDownload = removeDownload
            RegisterServiceListener = registerServiceListener
            SignalError = signalError
            SignalServiceCrashed = signalServiceCrashed
            RegisterErrorListener = registerErrorListener
            AddInfoListener = addInfoListener
            RemoveInfoListener = removeInfoListener
            SendInfo = sendInfo
            RegisterShutDownEvent = registerShutDownEvent
            SignalShutDownService = signalShutDownService
            GetCurrentState = getCurrentState
        }


    module External =

        let oldShopCallbackService = createDownloadServiceCallback OldShop
        let newShopCallbackService = createDownloadServiceCallback NewShop
        
        
        type Msg =
            | AddDownload of DownloadInfo
            | RemoveDownload of DownloadInfo
            | StartDownload
            | DownloadError of ComError
            | FinishedDownload of DownloadInfo * DownloadResult
            | ShutDownService
            | UpdateNotification of DownloadInfo * int
            | GetState of AsyncReplyChannel<DownloadServiceState>


        let createExternalDownloadService
            (shop:Shop)
            startDownload
            shutDownExternalService
            updateNotification =

            let callback =
                match shop with
                | OldShop -> oldShopCallbackService
                | NewShop -> newShopCallbackService
                
            let database =
                match shop with
                | OldShop -> OldShopDatabase.storageProcessor
                | NewShop -> NewShopDatabase.storageProcessor
            
            MailboxProcessor<Msg>.Start(
                fun inbox ->
                    let rec loop (state:DownloadServiceState) =
                        async {
                            try
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

                                            // start download, do not wait for the result, the loop mus continue
                                            startDownload inbox download |> Async.AwaitTask |> ignore

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


                                        callback.SignalError (download, error)

                                        return! loop newState

                                | FinishedDownload (info,downloadResult) ->
                                    match state.CurrentDownload with
                                    | None ->
                                        return! loop state
                                    | Some download ->


                                        // store file infos
                                        let! audioFileInformation =
                                            async {
                                                match info.AudioBook.State.DownloadedFolder with
                                                | None ->
                                                    return None
                                                | Some folder ->
                                                    let! files = Files.getMp3FileList folder
                                                    let fileInfo = {
                                                        Id = download.AudioBook.Id
                                                        AudioFiles = files |> List.sortBy (_.FileName)
                                                    }
                                                    let! _ = database.InsertAudioBookFileInfos [| fileInfo |]
                                                    return Some fileInfo
                                            }

                                        let download =
                                            { download
                                                with
                                                    State = Finished (downloadResult,audioFileInformation)
                                            }

                                        let newState =
                                            { state with
                                                CurrentDownload = None
                                                Downloads = state.Downloads |> List.map (fun i -> if i.AudioBook.Id = download.AudioBook.Id then download else i)
                                            }

                                        // send info that the download is complete
                                        callback.SendInfo download

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
                                        | Running _
                                        | Finished _ ->
                                            return! loop state
                                        | Open
                                        | Failed _ ->
                                            return! loop { state with Downloads = state.Downloads |> List.filter (fun i -> i.AudioBook.Id <> item.AudioBook.Id ) }

                                | ShutDownService ->
                                    callback.SignalShutDownService ()
                                    shutDownExternalService ()

                                    return! loop { Downloads = []; CurrentDownload = None }

                                | GetState reply ->
                                    reply.Reply(state)
                                    return! loop state

                                | UpdateNotification (info,percent) ->
                                    let openCount =
                                        state.Downloads |> List.filter (fun i -> match i.State with | Open | Failed _ -> true | _ -> false) |> List.length
                                    let allCount = state.Downloads  |> List.length// |> List.filter (fun i -> match i.State with | Finished -> true | _ -> false)

                                    let stateText  =
                                        $"(noch %i{openCount + 1} von {allCount}) {info.AudioBook.FullName}"

                                    let stateTitle =
                                        $"Lade runter... {percent} %%)"

                                    updateNotification stateTitle stateText

                                    return! loop state
                            with
                            | ex ->
                                Global.telemetryClient.TrackException ex
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


        let startDownload (shop:Shop) (inbox:MailboxProcessor<Msg>)  (info:DownloadInfo) =

            let callback =
                match shop with
                | OldShop -> oldShopCallbackService
                | NewShop -> newShopCallbackService
                
                
            let downloadAudiobook =
                match shop with
                | OldShop -> OldShopWebAccessService.Downloader.downloadAudiobook
                | NewShop -> NewShopWebAccessService.Downloader.downloadAudiobook
                
            let database =
                match shop with
                | OldShop -> OldShopDatabase.storageProcessor
                | NewShop -> NewShopDatabase.storageProcessor
                
            
            let updateStateDownloadInfo newState (downloadInfo:DownloadInfo) =
                {downloadInfo with State = newState}

            task {

                //let mutable mutDemoData = info

                let updateProgress (c,a) =
                    let factor = if a = 0 then 0.0 else (c |> float) / (a |> float)
                    let percent = factor * 100.0 |> int
                    inbox.Post <| UpdateNotification (info,percent)
                    let newState = updateStateDownloadInfo  (Running (a,c)) info
                    callback.SendInfo newState

                let loadCookies =
                    match shop with
                    | OldShop ->
                        SecureLoginStorage.loadOldShopCookie
                    | NewShop ->
                        SecureLoginStorage.loadNewShopCookie
                
                match! loadCookies() with
                | Error _
                | Ok None ->
                    inbox.Post (DownloadError <| ComError.SessionExpired "session expired")
                | Ok (Some cc) ->

                    let! res =
                        downloadAudiobook
                            cc
                            updateProgress
                            info.AudioBook

                    match res with
                    | Error error ->
                        inbox.Post <| DownloadError error
                    | Ok result ->

                        let newAb =
                            { info.AudioBook with
                                Thumbnail = result.Images |> Option.map (_.Thumbnail)
                                Picture = result.Images |> Option.map (_.Image)
                                State =
                                    { info.AudioBook.State with
                                        Downloaded = true
                                        DownloadedFolder = Some result.TargetFolder
                                    }
                            }

                        let! saveResult = database.UpdateAudioBookInStateFile newAb
                        let newInfo = {
                            info with AudioBook = newAb
                        }
                        match saveResult with
                        | Error msg ->
                            inbox.Post <| DownloadError (ComError.Other msg)
                        | Ok _ ->
                            inbox.Post <| FinishedDownload (newInfo,result)
            }

