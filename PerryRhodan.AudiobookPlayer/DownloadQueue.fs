module DownloadQueue

    open Domain
    open Fabulous
    open Fabulous.XamarinForms
    open Common
    open Xamarin.Forms
    open Services
    open Global

    type QueueState =
        | Idle
        | Downloading
        | Paused
        

    type Model = { DownloadQueue: AudioBookItem.Model list 
                   CurrentSessionCookieContainer:Map<string,string> option
                   State: QueueState }

    type Msg =
        | AddItemToQueue of AudioBookItem.Model
        | RemoveItemFromQueue of AudioBookItem.Model
        | DownloadCompleted of audioBook:AudioBookItem.Model * WebAccess.Downloader.DownloadResult
        | DownloadFailed of AudioBookItem.Model
        | DeactivateLoading of AudioBookItem.Model
        | OpenLoginPage
        | StartProcessing
        | PauseProcessing
        | ChangeQueueState of QueueState
        | UpdateDownloadProgress of AudioBookItem.Model * (int * int) option
        | ChangeGlobalBusyState of bool
        | OpenActionMenu of AudioBookItem.Model
        | ShowErrorMessage of string

    type ExternalMsg =
        | ExOpenLoginPage of LoginRequestCameFrom
        | UpdateAudioBook of AudioBookItem.Model
        | UpdateDownloadProgress of AudioBookItem.Model * (int * int) option
        | PageChangeBusyState of bool
        

    module Helpers =
        let getTailHead l =
            match l with
            | [] -> (None,[])
            | [x] ->(Some x,[])
            | x::xs -> (Some x,xs)
        

        let setGlobalBusyCmd = Cmd.ofMsg (ChangeGlobalBusyState true)

    
    module Commands =

        //let initDownloadServiceCmd 
        //    shutDownloadEvent 
        //    listener =
        //    fun _ ->
        //        Services.DownloadService.registerShutDownEvent shutDownloadEvent
        //        Services.DownloadService.registerServiceListener listener
        //    |> Cmd.ofSub


        //let addDownloadCmd cookieContainer (abModel:AudioBookItem.Model) =
        //    fun _ -> 
        //        Services.DownloadService.addDownload <| Services.DownloadService.DownloadInfo.New cookieContainer abModel.AudioBook
        //    |> Cmd.ofSub


        let removeDownloadCmd (abModel:AudioBookItem.Model) =
            fun _ -> 
                Services.DownloadService.removeDownload <| Services.DownloadService.DownloadInfo.New None abModel.AudioBook
            |> Cmd.ofSub


        //let startDownloadsCmd ()  =
        //    fun _ -> 
        //        Services.DownloadService.startDownloads ()
        //    |> Cmd.ofSub

        let downloadAudiobookCmd model (audiobookItemModel:AudioBookItem.Model) =
            fun dispatch -> 
                match model.CurrentSessionCookieContainer with                
                | None ->
                    // deactivate loading spinner if login needed, 
                    // because the startProcessing checks if loading already is actived, 
                    // and do not start the download at all
                    dispatch <| DeactivateLoading audiobookItemModel;
                    dispatch <| ChangeQueueState Idle
                    dispatch <| OpenLoginPage
                    
                | Some cc ->

                    Services.DownloadService.startService ()

                    Services.DownloadService.registerShutDownEvent (fun () -> async { dispatch <| ChangeQueueState Idle } )

                    Services.DownloadService.addInfoListener "downloadQueueListener" (fun info ->
                        async {

                            let abModel =
                                model.DownloadQueue
                                |> List.tryFind (fun i -> i.AudioBook.Id = info.AudioBook.Id)

                            match abModel,info.State with
                            | Some abModel, Services.DownloadService.Running (all,current) ->
                                dispatch <| Msg.UpdateDownloadProgress (abModel,(Some (current,all)))
                            | Some abModel, Services.DownloadService.Open ->
                                ()
                            | Some abModel, Services.DownloadService.Finished result ->
                                dispatch <| DownloadCompleted (abModel, result)
                            | _, _ ->
                                ()
                        }
                    )

                    Services.DownloadService.registerErrorListener (fun (info,error) ->
                        async {
                            let abModel =
                                model.DownloadQueue
                                |> List.tryFind (fun i -> i.AudioBook.Id = info.AudioBook.Id)
                            match abModel, error with
                            | Some abModel, ComError.SessionExpired msg ->
                                Services.DownloadService.shutDownService ()
                                dispatch <| DeactivateLoading abModel;
                                dispatch <| ChangeQueueState Idle
                                dispatch <| OpenLoginPage

                            | Some abModel, ComError.Other msg ->
                                dispatch <| ShowErrorMessage msg

                            | Some abModel, ComError.Network msg ->
                                // the download service restarts network error automatically
                                dispatch <| ShowErrorMessage msg
                                ()
                            | Some abModel, ComError.Exception e ->
                                let ex = e.GetBaseException()
                                let msg = ex.Message + "|" + ex.StackTrace
                                dispatch <| ShowErrorMessage msg
                                    
                            | _, _ ->
                                ()
                        }
                    )

                    Services.DownloadService.addDownload <| Services.DownloadService.DownloadInfo.New (Some cc) audiobookItemModel.AudioBook

                    Services.DownloadService.startDownloads ()
                        
            |> Cmd.ofSub


        let openDownloadQueueActionMenu (audiobook:AudioBookItem.Model) =            
            async {
                if (audiobook.IsDownloading) then
                    return None
                else
                    let buttons = [|
                        
                        yield ("Remove from Download Queue",(fun a -> (RemoveItemFromQueue audiobook)) audiobook)
                        
                    |]
                    return! Helpers.displayActionSheet (Some audiobook.AudioBook.FullName) (Some Translations.current.Cancel) buttons
                
            } |> Cmd.ofAsyncMsgOption


        let updateAudiobookInStateFile audioBook =
            async {
                let! res = audioBook |> DataBase.updateAudioBookInStateFile            
                match res with
                | Error e ->                    
                    return ShowErrorMessage e
                | Ok _ ->
                    return ChangeGlobalBusyState false
            } |> Cmd.ofAsyncMsg



    let initModel cc = {DownloadQueue = []; State = Idle; CurrentSessionCookieContainer = cc}

    let init cc =
        initModel cc, Cmd.none, None


    let initFromDownloadService (info:Services.DownloadService.DownloadServiceState) =
        let queue =
            info.Downloads
            |> List.filter (fun i -> match i.State with | Services.DownloadService.Open | Services.DownloadService.Running _ -> true | _ -> false )
            |> List.choose (fun i ->
                let abModel = AudioBookItemProcessor.getAudioBookItem i.AudioBook.FullName
                match i.State with
                | Services.DownloadService.Open ->
                    abModel |> Option.map (fun abModel -> { abModel with IsDownloading = false; QueuedToDownload = true })
                | Services.DownloadService.Running _ ->
                    abModel |> Option.map (fun abModel ->  { abModel with IsDownloading = true; QueuedToDownload = false })
                | _ ->
                    None
            )

        let cmd model =
            fun dispatch ->
                Services.DownloadService.startService ()
            
                Services.DownloadService.registerShutDownEvent (fun () -> async { dispatch <| ChangeQueueState Idle } )
            
                Services.DownloadService.addInfoListener "downloadQueueListener" (fun info ->
                    async {
            
                        let abModel =
                            model.DownloadQueue
                            |> List.tryFind (fun i -> i.AudioBook.Id = info.AudioBook.Id)
            
                        match abModel,info.State with
                        | Some abModel, Services.DownloadService.Running (all,current) ->
                            dispatch <| Msg.UpdateDownloadProgress (abModel,(Some (current,all)))
                        | Some abModel, Services.DownloadService.Open ->
                            ()
                        | Some abModel, Services.DownloadService.Finished result ->
                            dispatch <| DownloadCompleted (abModel, result)
                        | _, _ ->
                            ()
                    }
                )
            
                Services.DownloadService.registerErrorListener (fun (info,error) ->
                    async {
                        let abModel =
                            model.DownloadQueue
                            |> List.tryFind (fun i -> i.AudioBook.Id = info.AudioBook.Id)
                        match abModel, error with
                        | Some abModel, ComError.SessionExpired msg ->
                            Services.DownloadService.shutDownService ()
                            dispatch <| DeactivateLoading abModel;
                            dispatch <| ChangeQueueState Idle
                            dispatch <| OpenLoginPage
            
                        | Some abModel, ComError.Other msg ->
                            dispatch <| ShowErrorMessage msg
            
                        | Some abModel, ComError.Network msg ->
                            // the download service restarts network error automatically
                            dispatch <| ShowErrorMessage msg
                            ()
                        | Some abModel, ComError.Exception e ->
                            let ex = e.GetBaseException()
                            let msg = ex.Message + "|" + ex.StackTrace
                            dispatch <| ShowErrorMessage msg
                                                
                        | _, _ ->
                            ()
                    }
                )
            |> Cmd.ofSub

        let cc = 
            match info.Downloads with
            | [] -> None
            | head::_ -> 
                head.CookieContainer


        let newModel =
            { initModel cc with 
                DownloadQueue = queue
                State = Downloading 
            }
            
        newModel, cmd newModel


    let rec update msg model =
        match msg with
        | AddItemToQueue abModel ->
            model |> onAddItemToQueueMsg abModel
        | RemoveItemFromQueue abModel ->
            model |> onRemoveItemFromQueueMsg abModel
        | DownloadCompleted (abModel,downloadResult) ->
            model |> onDownloadCompleteMsg abModel downloadResult
        | DownloadFailed abModel ->
            model |> onDownloadFailedMsg abModel
        | DeactivateLoading abModel ->
            model |> onDeactivateLoadingMsg abModel
        | OpenLoginPage ->
            model |> onOpenLoginPageMsg
        | StartProcessing ->
            model |> onStartProcessingMsg
        | PauseProcessing ->
            model |> onPauseProcessingMsg
        | ChangeQueueState state ->
            model |> onChangeQueueStateMsg state
        | Msg.UpdateDownloadProgress (abModel,progress) ->
            model |> onUpdateDownloadProgressMsg abModel progress
        | ChangeGlobalBusyState state -> 
            model |> onChangeGlobalBusyStateMsg state
        | OpenActionMenu abModel ->
            model |> onOpenActionMenuMsg abModel
        | ShowErrorMessage e ->
            model |> onShowErrorMessageMsg e

    
    and onOpenActionMenuMsg abModel model =
        model, (abModel |> Commands.openDownloadQueueActionMenu), None
    
    
    and onAddItemToQueueMsg abModel model =
        let newAbModel = {abModel with QueuedToDownload = true}

        let newModel = 
            let isAlreadyInQueue = model.DownloadQueue |> List.exists (fun i -> i.AudioBook.Id = abModel.AudioBook.Id)
            if isAlreadyInQueue then
                model
            else
                {model with DownloadQueue = model.DownloadQueue @ [abModel]}

        let cmd,exCmd = 
            // use old download queue, to check, if the download queue is in Download Mode, but empty
            match (model.State,model.DownloadQueue) with
            | Idle, _ ->
                Cmd.ofMsg StartProcessing, Some (UpdateAudioBook newAbModel)
            | Downloading, [] -> 
                Commands.downloadAudiobookCmd newModel newAbModel, Some (UpdateAudioBook newAbModel)
            | _ -> 
                Cmd.none, None

        AudioBookItemProcessor.updateAudiobookItem newAbModel

        newModel, cmd, exCmd
    
    and onRemoveItemFromQueueMsg abModel model =        
        let newAbModel = {abModel with QueuedToDownload = false; IsDownloading = false}
        let newQueue = model.DownloadQueue |> List.filter (fun i -> i.AudioBook.FullName <> abModel.AudioBook.FullName)
        let newModel = {model with DownloadQueue = newQueue}
        let cmd = 
            Commands.removeDownloadCmd abModel

        AudioBookItemProcessor.updateAudiobookItem newAbModel
            
        newModel, cmd, None //Some (UpdateAudioBook newAbModel)
        
    
    and onDownloadCompleteMsg abModel downloadResult model =
        let (processedItem,newQueue) = model.DownloadQueue |> Helpers.getTailHead
        match processedItem with
        | None ->
            {model with State = Idle}, Cmd.none, None
        | Some processedItem ->
            let newAudioBookState = {abModel.AudioBook.State with Downloaded = true; DownloadedFolder = Some downloadResult.TargetFolder}
            let newAudioBook = 
                match downloadResult.Images with
                | None ->
                    abModel.AudioBook
                | Some images ->
                    { abModel.AudioBook with State = newAudioBookState; Picture = Some images.Image; Thumbnail = Some images.Thumbnail}             

            let newProcessedItem = {processedItem with AudioBook = newAudioBook; IsDownloading = false; QueuedToDownload = false }

            let newModel = {model with DownloadQueue = newQueue; State = Idle}

            let updateStateCmd = newAudioBook |> Commands.updateAudiobookInStateFile
            AudioBookItemProcessor.updateAudiobookItem newProcessedItem
            newModel, Cmd.batch [Cmd.ofMsg StartProcessing; Helpers.setGlobalBusyCmd; updateStateCmd], Some (UpdateAudioBook newProcessedItem)

    
    and onDownloadFailedMsg abModel model =
        let (processedItem,tailQueue) = model.DownloadQueue |> Helpers.getTailHead
        match processedItem with
        | None ->
            model, Cmd.none, None
        | Some processedItem ->
            // and to the end
            let newItem = {processedItem with IsDownloading = false; QueuedToDownload = true}
            let newQueue = tailQueue @ [newItem]
            let newModel = {model with DownloadQueue = newQueue; State = Idle}
            AudioBookItemProcessor.updateAudiobookItem newItem
            newModel, Cmd.ofMsg StartProcessing, None //Some (UpdateAudioBook newItem)
    

    and onDeactivateLoadingMsg abModel model =
        let newModel = {abModel with IsDownloading=false}
        let newQueue= 
            model.DownloadQueue 
            |> List.map (fun i -> if i.AudioBook.Id = newModel.AudioBook.Id then newModel else i)

        {model with DownloadQueue = newQueue}, Cmd.none, None



    and onOpenLoginPageMsg model =
        let newModel = {model with State = Idle}
        newModel, Cmd.none, Some (ExOpenLoginPage DownloadAudioBook)
    

    and onStartProcessingMsg model =
        // Start all downlaod in queue after login succeeded
        match model.DownloadQueue with
        | [] ->
            model, Cmd.none, None
        | _ ->

            let cmd =
                Cmd.batch [
                    for item in model.DownloadQueue do
                        Commands.downloadAudiobookCmd model item
                        let newItem = {item with IsDownloading = false; QueuedToDownload = true}
                        AudioBookItemProcessor.updateAudiobookItem newItem
                ]

            let newdq = 
                model.DownloadQueue
                |> List.map (fun item -> {item with IsDownloading = false; QueuedToDownload = true} )

            newdq
            |> List.iter (AudioBookItemProcessor.updateAudiobookItem)
               

            { model with State = Downloading; DownloadQueue = newdq }, cmd, None

    
    and onPauseProcessingMsg model =

        let retryCommand = 
            async {
                // wait 30 Seconds
                do! Async.Sleep 30000
                return StartProcessing
            } |> Cmd.ofAsyncMsg

        let (currentProcessingItem,queueTail) = model.DownloadQueue |> Helpers.getTailHead
        match currentProcessingItem with
        | None ->
            model,Cmd.none,None
        | Some currentProcessingItem ->
            let newCurrentProcessingItem = {currentProcessingItem with IsDownloading = false}
            let newQueue = newCurrentProcessingItem::queueTail
            let newModel = {model with State = Paused; DownloadQueue = newQueue}
            newModel, retryCommand, None
    
    
    
    and onChangeQueueStateMsg state model =
        let newModel = {model with State = state}
        newModel, Cmd.none, None

    
    and onUpdateDownloadProgressMsg abModel progress model =
        let newAbModel = { abModel with CurrentDownloadProgress = progress; IsDownloading = true }
        let newQueue = 
            model.DownloadQueue
            |> List.map (fun i -> if i.AudioBook.Id = abModel.AudioBook.Id then newAbModel else i)
        let newModel = { model with DownloadQueue = newQueue }
        AudioBookItemProcessor.updateAudiobookItem newAbModel
        newModel, Cmd.none, Some (ExternalMsg.UpdateDownloadProgress (newAbModel,progress))

    
    and onChangeGlobalBusyStateMsg state model =
        model, Cmd.none, Some (PageChangeBusyState state)

    
    and onShowErrorMessageMsg e model =
        Common.Helpers.displayAlert(Translations.current.Error,e,"OK") |> Async.StartImmediate
        model, Cmd.ofMsg (ChangeGlobalBusyState false), None
    
    
    
    
    let view model dispatch =
        View.ContentPage(
            title="Downloads",useSafeArea=true,
            backgroundColor = Consts.backgroundColor,
            content = View.Grid(
                
                children = [
                    View.StackLayout(
                        orientation = StackOrientation.Vertical,
                        children = [
                            yield View.Label(text="aktuelle Downloads", fontAttributes = FontAttributes.Bold,
                                fontSize=FontSize 25.,
                                verticalOptions=LayoutOptions.Fill,
                                horizontalOptions=LayoutOptions.Fill,
                                horizontalTextAlignment=TextAlignment.Center,
                                verticalTextAlignment=TextAlignment.Center,
                                textColor = Consts.primaryTextColor,
                                backgroundColor = Consts.cardColor,
                                margin=Thickness 0.)
                            
                            
                            if model.DownloadQueue.Length > 0 then
                                if model.State = Paused then
                                    yield Controls.secondaryTextColorLabel 16. "Netzwerkfehler! Eventuell bestehlt keine Internetverbindung mehr. Es wird in 30 sek nochmal versucht."

                                yield View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                    verticalOptions = LayoutOptions.Fill,
                                    content = 
                                        View.StackLayout(orientation=StackOrientation.Vertical,
                                            children= [
                                                let downloadItemTitles = 
                                                    model.DownloadQueue 
                                                    |> List.map (fun x -> x.AudioBook.FullName)
                                                    |> List.toArray
                                                let abItems = AudioBookItemProcessor.getAudioBookItems downloadItemTitles
                                                for item in abItems do
                                                    
                                                    yield item.AudioBook 
                                                        |> Controls.renderAudiobookEntry 
                                                            (fun ()-> dispatch (OpenActionMenu item)) 
                                                            (fun ()-> dispatch (OpenActionMenu item)) 
                                                            item.IsDownloading 
                                                            item.QueuedToDownload 
                                                            item.CurrentDownloadProgress
                                                   
                                            ]
                                        )
                                    )
                            else
                                yield Controls.secondaryTextColorLabel 24. "aktuell laufen keine Downloads oder sind welche geplant."
                            
                        ]
                    )
                ]
            )
        )
        

