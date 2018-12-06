module DownloadQueue

    open Domain
    open Fabulous.Core
    open Common
    open Fabulous.DynamicViews
    open Xamarin.Forms

    type QueueState =
        | Idle
        | Downloading
        

    type Model = { DownloadQueue: AudioBookItem.Model list 
                   CurrentSessionCookieContainer:Map<string,string> option
                   State: QueueState }

    type Msg =
        | AddItemToQueue of AudioBookItem.Model
        | RemoveItemToQueue of AudioBookItem.Model
        | DownloadCompleted of audioBook:AudioBookItem.Model * mp3path:string * imageFile:string option * thumbnail:string option
        | DownloadFailed of AudioBookItem.Model
        | OpenLoginPage
        | StartProcessing
        | ChangeQueueState of QueueState
        | UpdateDownloadProgress of AudioBookItem.Model * (int * int) option
        | ChangeGlobalBusyState of bool

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


    let updateAudiobookInStateFile audioBook =
        async {
            let! res = audioBook |> Services.updateAudioBookInStateFile            
            match res with
            | Error e ->
                Common.Helpers.displayAlert("Error",e,"OK") |> ignore
                return ChangeGlobalBusyState false
            | Ok _ ->
                return ChangeGlobalBusyState false
        } |> Cmd.ofAsyncMsg
    
    let downloadAudiobook model (audiobookItemModel:AudioBookItem.Model) =
        (fun (dispatch:Dispatch<Msg>) -> 
            async {
                
                match model.CurrentSessionCookieContainer with                
                | None ->
                    return OpenLoginPage
                | Some cc ->
                    let updateProgress (a,b) = dispatch (Msg.UpdateDownloadProgress (audiobookItemModel,(Some (a,b))))
                    
                    let! res = audiobookItemModel.AudioBook |> Services.downloadAudiobook cc updateProgress
                    match res with
                    | Error e ->
                        match e with
                        | SessionExpired s -> 
                            return OpenLoginPage
                        | Other o ->
                            Common.Helpers.displayAlert("Error",o,"OK") |> ignore
                            return DownloadFailed audiobookItemModel
                        | Exception e ->
                            let ex = e.GetBaseException()
                            let msg = ex.Message + "|" + ex.StackTrace
                            Common.Helpers.displayAlert("Error",msg,"OK") |> ignore
                            return DownloadFailed audiobookItemModel
                    | Ok (mp3Path,images) ->
                        let fullImage = images |> Option.map (fun (f,_) -> f)
                        let thumb = images |> Option.map (fun (_,t) -> t)
                        return DownloadCompleted (audiobookItemModel, mp3Path, fullImage, thumb)            
            }) |> Common.Cmd.ofAsyncWithInternalDispatch


    let processQueue model =
            if model.State = Downloading then 
                Cmd.ofMsg (ChangeQueueState Idle)
            else
                let (itemToProcess,_) = model.DownloadQueue |> Helpers.getTailHead
                match itemToProcess with
                | None -> Cmd.ofMsg (ChangeQueueState Idle)
                | Some itemToProcess ->
                    // Todo do it richt
                    itemToProcess |> downloadAudiobook model

    
    let initModel cc = {DownloadQueue = []; State = Idle; CurrentSessionCookieContainer = cc}

    let init cc =
        initModel cc, Cmd.none, None

    let update msg model =
        match msg with
        | AddItemToQueue abModel ->
            let newAbModel = {abModel with QueuedToDownload = true}
            let newModel = {model with DownloadQueue = model.DownloadQueue @ [abModel]}
            let cmd = 
                match model.State with
                | Idle -> Cmd.ofMsg StartProcessing                    
                | Downloading -> Cmd.none
            newModel, cmd, Some (UpdateAudioBook newAbModel)

        | RemoveItemToQueue abModel ->
            let newAbModel = {abModel with QueuedToDownload = false}
            let newQueue = model.DownloadQueue |> List.filter (fun i -> i.AudioBook.FullName <> abModel.AudioBook.FullName)
            let newModel = {model with DownloadQueue = newQueue}
            let cmd = 
                match model.State with
                | Idle -> Cmd.ofMsg StartProcessing                    
                | Downloading -> Cmd.none
            newModel, cmd, Some (UpdateAudioBook newAbModel)

        | DownloadCompleted (abModel,mp3Path,imageFullName,thumbnail) ->
            
            let (processedItem,newQueue) = model.DownloadQueue |> Helpers.getTailHead
            match processedItem with
            | None ->
                model, Cmd.none, None
            | Some processedItem ->
                let newAudioBookState = {abModel.AudioBook.State with Downloaded = true; DownloadedFolder = Some mp3Path}
                let newAudioBook = {abModel.AudioBook with State = newAudioBookState; Picture = imageFullName; Thumbnail = thumbnail}             
                let newProcessedItem = {processedItem with AudioBook = newAudioBook; IsLoading = false; QueuedToDownload = false }

                let newModel = {model with DownloadQueue = newQueue; State = Idle}

                let updateStateCmd = newAudioBook |> updateAudiobookInStateFile
                newModel, Cmd.batch [Cmd.ofMsg StartProcessing; Helpers.setGlobalBusyCmd; updateStateCmd], Some (UpdateAudioBook newProcessedItem)


        | DownloadFailed abModel ->
            let (processedItem,tailQueue) = model.DownloadQueue |> Helpers.getTailHead
            match processedItem with
            | None ->
                model, Cmd.none, None
            | Some processedItem ->
                // and to the end
                let newItem = {processedItem with IsLoading = false; QueuedToDownload = true}
                let newQueue = tailQueue @ [newItem]
                let newModel = {model with DownloadQueue = newQueue; State = Idle}
                newModel, Cmd.ofMsg StartProcessing, Some (UpdateAudioBook newItem)

        | OpenLoginPage ->
            let newModel = {model with State = Idle}
            newModel, Cmd.none, Some ExOpenLoginPage
        | StartProcessing ->
            // Todo start Processing
            let (itemToProcess,queueTail) = model.DownloadQueue |> Helpers.getTailHead
            match itemToProcess with
            | None ->
                model, Cmd.none, None
            | Some itemToProcess ->
                let newItemToProcess = {itemToProcess with IsLoading = true; QueuedToDownload = true}
                let newQueue = newItemToProcess::queueTail
                let newModel = {model with DownloadQueue = newQueue}
                let processCmd = newModel |> processQueue
                newModel, Cmd.batch [Cmd.ofMsg (ChangeQueueState Downloading); processCmd ], Some (UpdateAudioBook newItemToProcess)


        | ChangeQueueState state ->
            let newModel = {model with State = state}
            newModel, Cmd.none, None

        | Msg.UpdateDownloadProgress (abModel,progress) ->
            let newAbModel = {abModel with CurrentDownloadProgress = progress}
            let newQueue = 
                model.DownloadQueue
                |> List.map (fun i -> if i.AudioBook.FullName = abModel.AudioBook.FullName then newAbModel else i)
            let newModel = {model with DownloadQueue = newQueue}
            newModel, Cmd.none, Some (ExternalMsg.UpdateDownloadProgress (newAbModel,progress))

        | ChangeGlobalBusyState state -> 
            model, Cmd.none, Some (PageChangeBusyState state)

    
    let view model dispatch =
        View.StackLayout(
            orientation = StackOrientation.Horizontal,
            children = [
                yield View.Label(
                    text=match model.State with
                         | Idle -> "State: Idle"
                         | Downloading -> "State: running"
                )
                for item in model.DownloadQueue do
                    yield View.Label(
                        text="X", fontSize=35.0
                    )
            ]
        )

