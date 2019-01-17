module DownloadQueue

    open Domain
    open Fabulous.Core
    open Common
    open Fabulous.DynamicViews
    open Xamarin.Forms
    open Services

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
        | DownloadCompleted of audioBook:AudioBookItem.Model * mp3path:string * imageFile:string option * thumbnail:string option
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
        | ExOpenLoginPage
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

    
    let initModel cc = {DownloadQueue = []; State = Idle; CurrentSessionCookieContainer = cc}

    let init cc =
        initModel cc, Cmd.none, None

    let rec update msg model =
        match msg with
        | AddItemToQueue abModel ->
            model |> onAddItemToQueueMsg abModel
        | RemoveItemFromQueue abModel ->
            model |> onRemoveItemFromQueueMsg abModel
        | DownloadCompleted (abModel,mp3Path,imageFullName,thumbnail) ->
            model |> onDownloadCompleteMsg abModel mp3Path imageFullName thumbnail
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

        let openDownloadQueueActionMenu audiobook =            
            async {
                let buttons = [|
                        
                    yield ("Remove from Download Queue",(fun a -> (RemoveItemFromQueue audiobook)) audiobook)
                        
                |]
                return! Helpers.displayActionSheet (Some audiobook.AudioBook.FullName) (Some "Cancel") buttons
            } |> Cmd.ofAsyncMsgOption

        model, (abModel |> openDownloadQueueActionMenu), None
    
    
    and onAddItemToQueueMsg abModel model =
        let newAbModel = {abModel with QueuedToDownload = true}

        let newModel = 
            let isAlreadyInQueue = model.DownloadQueue |> List.exists (fun i -> i.AudioBook.Id = abModel.AudioBook.Id)
            if isAlreadyInQueue then
                model
            else
                {model with DownloadQueue = model.DownloadQueue @ [abModel]}

        let cmd,exCmd = 
            match model.State with
            | Idle -> Cmd.ofMsg StartProcessing, Some (UpdateAudioBook newAbModel)                
            | Downloading | Paused -> Cmd.none, None
        newModel, cmd, exCmd
    
    and onRemoveItemFromQueueMsg abModel model =        
        let newAbModel = {abModel with QueuedToDownload = false; IsDownloading = false}
        let newQueue = model.DownloadQueue |> List.filter (fun i -> i.AudioBook.FullName <> abModel.AudioBook.FullName)
        let newModel = {model with DownloadQueue = newQueue}
        let cmd = 
            match model.State with
            | Idle -> Cmd.ofMsg StartProcessing                    
            | Downloading | Paused -> Cmd.none
            
        newModel, cmd, Some (UpdateAudioBook newAbModel)
        
    
    and onDownloadCompleteMsg abModel mp3Path imageFullName thumbnail model =
        
        let updateAudiobookInStateFile audioBook =
            async {
                let! res = audioBook |> FileAccess.updateAudioBookInStateFile            
                match res with
                | Error e ->                    
                    return ShowErrorMessage e
                | Ok _ ->
                    return ChangeGlobalBusyState false
            } |> Cmd.ofAsyncMsg
        
        
        let (processedItem,newQueue) = model.DownloadQueue |> Helpers.getTailHead
        match processedItem with
        | None ->
            model, Cmd.none, None
        | Some processedItem ->
            let newAudioBookState = {abModel.AudioBook.State with Downloaded = true; DownloadedFolder = Some mp3Path}
            let newAudioBook = {abModel.AudioBook with State = newAudioBookState; Picture = imageFullName; Thumbnail = thumbnail}             
            let newProcessedItem = {processedItem with AudioBook = newAudioBook; IsDownloading = false; QueuedToDownload = false }

            let newModel = {model with DownloadQueue = newQueue; State = Idle}

            let updateStateCmd = newAudioBook |> updateAudiobookInStateFile
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
            newModel, Cmd.ofMsg StartProcessing, Some (UpdateAudioBook newItem)
    

    and onDeactivateLoadingMsg abModel model =
        let newModel = {abModel with IsDownloading=false}
        let newQueue= 
            model.DownloadQueue 
            |> List.map (fun i -> if i.AudioBook.Id = newModel.AudioBook.Id then newModel else i)

        {model with DownloadQueue = newQueue}, Cmd.none, None



    and onOpenLoginPageMsg model =
        let newModel = {model with State = Idle}
        newModel, Cmd.none, Some ExOpenLoginPage
    

    and onStartProcessingMsg model =
        
        let downloadAudiobook model (audiobookItemModel:AudioBookItem.Model) =
            (fun (dispatch:Dispatch<Msg>) -> 
                async {
                    
                    match model.CurrentSessionCookieContainer with                
                    | None ->
                        // deactivate loading spinner if login needed, 
                        // because the startProcessing checks if loading already is actived, 
                        // and do not start the download at all
                        return [ DeactivateLoading audiobookItemModel; OpenLoginPage ]
                    | Some cc ->
                        let updateProgress (a,b) = dispatch (Msg.UpdateDownloadProgress (audiobookItemModel,(Some (a,b))))
                        
                        let! res = audiobookItemModel.AudioBook |> WebAccess.downloadAudiobook cc updateProgress
                        match res with
                        | Error e ->
                            match e with
                            | SessionExpired s -> 
                                return [ DeactivateLoading audiobookItemModel; OpenLoginPage ]
                            | Other o ->
                                return [ DownloadFailed audiobookItemModel; ShowErrorMessage o ]
                            | Network o ->
                                
                                return [ 
                                    yield PauseProcessing; 
                                    if model.State <> Paused then
                                        yield ShowErrorMessage o 
                                ]
                            | Exception e ->
                                let ex = e.GetBaseException()
                                let msg = ex.Message + "|" + ex.StackTrace
                                return [ DownloadFailed audiobookItemModel; ShowErrorMessage msg ]
                        | Ok (mp3Path,images) ->
                            let fullImage = images |> Option.map (fun (f,_) -> f)
                            let thumb = images |> Option.map (fun (_,t) -> t)
                            return [ DownloadCompleted (audiobookItemModel, mp3Path, fullImage, thumb) ]
                }) |> Common.Cmd.ofMultipleAsyncMsgWithInternalDispatch
        
        
        let processQueue model =
            if model.State = Downloading then 
                Cmd.ofMsg (ChangeQueueState Idle), model, None
            else
                let (itemToProcess,queueTail) = model.DownloadQueue |> Helpers.getTailHead
                match itemToProcess with
                | None ->
                    Cmd.none, model, None
                | Some itemToProcess ->
                    let newItemToProcess = {itemToProcess with IsDownloading = true; QueuedToDownload = true}
                    let newQueue = newItemToProcess::queueTail
                    let newModel = {model with DownloadQueue = newQueue}

                    
                    if not itemToProcess.IsDownloading then
                        newItemToProcess |> downloadAudiobook model, newModel, Some (UpdateAudioBook newItemToProcess)
                    else
                        Cmd.none, model, None
                    
            
        let processCmd, newModel,exCmd = model |> processQueue

        newModel, Cmd.batch [Cmd.ofMsg (ChangeQueueState Downloading); processCmd ], exCmd

    
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
        let newAbModel = {abModel with CurrentDownloadProgress = progress}
        let newQueue = 
            model.DownloadQueue
            |> List.map (fun i -> if i.AudioBook.FullName = abModel.AudioBook.FullName then newAbModel else i)
        let newModel = {model with DownloadQueue = newQueue}
        newModel, Cmd.none, Some (ExternalMsg.UpdateDownloadProgress (newAbModel,progress))

    
    and onChangeGlobalBusyStateMsg state model =
        model, Cmd.none, Some (PageChangeBusyState state)

    
    and onShowErrorMessageMsg e model =
        Common.Helpers.displayAlert("Error",e,"OK") |> Async.StartImmediate
        model, Cmd.ofMsg (ChangeGlobalBusyState false), None
    
    
    
    
    let view model dispatch =
        View.StackLayout(
            orientation = StackOrientation.Vertical,
            children = [
                if model.DownloadQueue.Length > 0 then
                    yield Controls.secondaryTextColorLabel 16.0 "Queued Downloads:"
                    if model.State = Paused then
                        yield Controls.secondaryTextColorLabel 16.0 "(network error - retrying in 30 seconds):"
                    yield View.StackLayout(
                        orientation = StackOrientation.Horizontal,
                        children = [
                        
                            for (idx,item) in model.DownloadQueue |> List.indexed do
                                let (cmd,isRed) =
                                    if (idx > 0) then
                                        (fun () -> dispatch (OpenActionMenu item)), true
                                    else
                                        (fun () -> ()), false
                                
                                let item = 
                                    if isRed then
                                        (Controls.primaryColorSymbolLabelWithTapCommand cmd 35.0 true "\uf019").TextColor(Color.Red)
                                    else
                                        Controls.primaryColorSymbolLabelWithTapCommand cmd 35.0 true "\uf019"

                                yield item
                        ]
                )
            ]
        )
        

