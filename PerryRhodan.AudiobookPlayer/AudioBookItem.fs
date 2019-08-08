module AudioBookItem

    open Domain
    open Fabulous
    open Fabulous.XamarinForms
    open Common
    open Xamarin.Forms
    open Services
    open Global

    type Model = { AudioBook: AudioBook
                   CurrentDownloadProgress: (int * int) option
                   QueuedToDownload: bool
                   IsDownloading: bool }

    type Msg =
        | OpenAudioBookActionMenu
        | AddToDownloadQueue
        | RemoveFromDownloadQueue
        | AudiobookDownloaded of audioBook:AudioBook * mp3path:string * imageFile:string option * thumbnail:string option
        | DeleteAudiobook
        | MarkAudioBookAsListend
        | UnmarkAudioBookAsListend
        | ChangeBusyState of bool
        | ChangeGlobalBusyState of bool
        | UpdateDownloadProgress of (int * int) option
        | OpenAudioBookPlayer
        | OpenAudioBookDetail
        | DeleteItemFromDb
        | TriggerUpdateAudiobook of AudioBook

        | DoNothing

    type ExternalMsg =
        | OpenLoginPage of LoginRequestCameFrom
        | UpdateAudioBook of Model
        | AddToDownloadQueue of Model
        | RemoveFromDownloadQueue of Model
        | PageChangeBusyState of bool
        | OpenAudioBookPlayer of AudioBook
        | OpenAudioBookDetail of AudioBook


    module Helpers =

        let unsetBusyCmd = Cmd.ofMsg (ChangeBusyState false)


        let setBusyCmd = Cmd.ofMsg (ChangeBusyState true)


        let unsetGlobalBusyCmd = Cmd.ofMsg (ChangeGlobalBusyState false)


        let setGlobalBusyCmd = Cmd.ofMsg (ChangeGlobalBusyState true)

        

    
    let updateAudiobookInStateFile model =
        async {
            let! res = model.AudioBook |> DataBase.updateAudioBookInStateFile            
            match res with
            | Error e ->
                Common.Helpers.displayAlert(Translations.current.Error,e,"OK") |> Async.StartImmediate
                return ChangeGlobalBusyState false
            | Ok _ ->
                return ChangeGlobalBusyState false
        } |> Cmd.ofAsyncMsg
    
    let initModel audiobook = { AudioBook = audiobook; CurrentDownloadProgress = None; QueuedToDownload=false; IsDownloading = false }

    
    let init audiobook = audiobook |> initModel, Cmd.none, None


    let rec update msg model =
        match msg with
        | OpenAudioBookActionMenu ->
            model |> onOpenAudioBookActionMenuMsg
        | Msg.AddToDownloadQueue ->
            model |> onAddToDownloadQueueMsg
        | Msg.RemoveFromDownloadQueue ->
            model |> onRemoveFromDownloadQueueMsg
        | AudiobookDownloaded (ab,mp3Path,imageFullName,thumbnail) ->
            model |> onAudioBookDownloadedMsg ab mp3Path imageFullName thumbnail
        | DeleteAudiobook ->
            model |> onDeleteAudioBookMsg
        | MarkAudioBookAsListend ->
            model |> onMarkAudioBookListendMsg
        | UnmarkAudioBookAsListend ->
            model |> onMarkAudioBookUnlistendMsg
        | UpdateDownloadProgress progress ->            
            model |> onUpdateDownloadProgressMsg progress
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state
        | ChangeGlobalBusyState state -> 
            model |> onChangeGlobalBusyStateMsg state
        | Msg.OpenAudioBookPlayer  ->
            model |> onOpenAudioBookPlayerMsg
        | Msg.OpenAudioBookDetail ->
            model |> onOpenAudioBookDetailMsg
        | DeleteItemFromDb ->
            model |> onDeleteItemFromDb
        | DoNothing ->
            model |> onDoNothingMsg
        | TriggerUpdateAudiobook ab ->
            model |> onTriggerUpdateAudiobook ab

    

    and onTriggerUpdateAudiobook ab model =
        model,Cmd.none, Some (UpdateAudioBook model)


    and onDeleteItemFromDb model =
        let cmd =
            async {
                let! diaRes = Common.Helpers.displayAlertWithConfirm("Remove item from DB","Are you sure?",Translations.current.Yes,Translations.current.No)
                if diaRes then
                    let! res = DataBase.removeAudiobookFromDatabase model.AudioBook
                    match res with
                    | Error e ->
                        do! Common.Helpers.displayAlert("Delete Audiobook Entry",e,"OK")
                        return None
                    | Ok _ ->
                       return (Some (TriggerUpdateAudiobook model.AudioBook))
                else
                    return None
            } |> Cmd.ofAsyncMsgOption

        model,cmd,None
        

        


    and onOpenAudioBookActionMenuMsg model =
        
        let openActionMenuCmd =
            Controls.audioBookEntryActionSheet 
                (fun a -> Msg.AddToDownloadQueue)
                (fun a -> Msg.RemoveFromDownloadQueue)
                (fun a -> DeleteAudiobook)
                (fun a -> MarkAudioBookAsListend)
                (fun a -> UnmarkAudioBookAsListend)
                (fun a -> DoNothing)
                (fun a -> Msg.OpenAudioBookDetail)
                (fun a -> DeleteItemFromDb)
                model.QueuedToDownload 
                model.IsDownloading
                model.AudioBook
            |> Cmd.ofAsyncMsgOption
            
        model, openActionMenuCmd, None

    
    and onAddToDownloadQueueMsg model =
        let newModel = { model with QueuedToDownload = true}
        newModel, Cmd.none, Some (ExternalMsg.AddToDownloadQueue model)

    
    and onRemoveFromDownloadQueueMsg model =
        let newModel = { model with QueuedToDownload = false}
        newModel, Cmd.none, Some (ExternalMsg.RemoveFromDownloadQueue model)


    and onAudioBookDownloadedMsg ab mp3Path imageFullName thumbnail model =
        let newState = {model.AudioBook.State with Downloaded = true; DownloadedFolder = Some mp3Path}
        let newAudioBook = {ab with State = newState; Picture = imageFullName; Thumbnail = thumbnail}                            
        let newModel = {model with AudioBook = newAudioBook }
        let updateStateCmd = newModel |> updateAudiobookInStateFile
        newModel, Cmd.batch [Helpers.unsetBusyCmd;Helpers.setGlobalBusyCmd; updateStateCmd], Some (UpdateAudioBook newModel)
    
    
    and onDeleteAudioBookMsg model =
        let newState = {model.AudioBook.State with Downloaded = false; DownloadedFolder = None}
        let newAudioBook = {model.AudioBook with State = newState; }
        match DataBase.removeAudiobook model.AudioBook with
        | Error e ->
            Common.Helpers.displayAlert(Translations.current.ErrorRemoveAudioBook,e,"OK") |> Async.StartImmediate
            model,Cmd.none,None
        | Ok _ ->
            let newModel = {model with AudioBook = newAudioBook }
            let updateStateCmd = newModel |> updateAudiobookInStateFile
            newModel, Cmd.batch [Helpers.setGlobalBusyCmd;updateStateCmd ], Some (UpdateAudioBook newModel)

    
    and onMarkAudioBookListendMsg model =
        let newState = {model.AudioBook.State with Completed = true}
        let newAudioBook = {model.AudioBook with State = newState; }                
        let newModel = {model with AudioBook = newAudioBook }
        let updateStateCmd = newModel |> updateAudiobookInStateFile
        newModel, Cmd.batch [Helpers.setGlobalBusyCmd; updateStateCmd ], Some (UpdateAudioBook newModel)
    
    and onMarkAudioBookUnlistendMsg model =
        let newState = {model.AudioBook.State with Completed = false}
        let newAudioBook = {model.AudioBook with State = newState; }
        let newModel = {model with AudioBook = newAudioBook }
        let updateStateCmd = newModel |> updateAudiobookInStateFile
        newModel, Cmd.batch [Helpers.setGlobalBusyCmd; updateStateCmd ], Some (UpdateAudioBook newModel)
    
    
    and onUpdateDownloadProgressMsg progress model =
        { model with CurrentDownloadProgress = progress }, Cmd.none, None
    
    
    and onChangeBusyStateMsg state model =
        {model with IsDownloading = state}, Cmd.none, None

    
    and onChangeGlobalBusyStateMsg state model =
        model, Cmd.none, Some (PageChangeBusyState state)
    
    
    and onOpenAudioBookPlayerMsg model =
        model, Cmd.none, Some (ExternalMsg.OpenAudioBookPlayer model.AudioBook)

    
    and onOpenAudioBookDetailMsg model =
        model, Cmd.none, Some (ExternalMsg.OpenAudioBookDetail model.AudioBook)


    and onDoNothingMsg model =
        model, Cmd.ofMsg (ChangeBusyState false), None




    let view (model: Model) dispatch =
        model.AudioBook 
        |> Controls.renderAudiobookEntry 
            (fun ()-> dispatch (OpenAudioBookActionMenu)) 
            (fun ()-> dispatch (Msg.OpenAudioBookPlayer)) 
            model.IsDownloading 
            model.QueuedToDownload 
            model.CurrentDownloadProgress
    


    

