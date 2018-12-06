module AudioBookItem

    open Domain
    open Fabulous.Core
    open Common
    open Fabulous.DynamicViews
    open Xamarin.Forms

    type Model = { AudioBook: AudioBook
                   CurrentDownloadProgress: (int * int) option
                   QueuedToDownload: bool
                   IsLoading: bool }

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

        | DoNothing

    type ExternalMsg =
        | OpenLoginPage
        | UpdateAudioBook of AudioBook
        | AddToDownloadQueue of Model
        | RemoveFromDownloadQueue of Model
        | PageChangeBusyState of bool
        | OpenAudioBookPlayer of AudioBook


    module Helpers =

        let unsetBusyCmd = Cmd.ofMsg (ChangeBusyState false)


        let setBusyCmd = Cmd.ofMsg (ChangeBusyState true)


        let unsetGlobalBusyCmd = Cmd.ofMsg (ChangeGlobalBusyState false)


        let setGlobalBusyCmd = Cmd.ofMsg (ChangeGlobalBusyState true)

        

    
    let updateAudiobookInStateFile model =
        async {
            let! res = model.AudioBook |> Services.updateAudioBookInStateFile            
            match res with
            | Error e ->
                Common.Helpers.displayAlert("Error",e,"OK") |> ignore
                return ChangeGlobalBusyState false
            | Ok _ ->
                return ChangeGlobalBusyState false
        } |> Cmd.ofAsyncMsg
    
    let initModel audiobook = { AudioBook = audiobook; CurrentDownloadProgress = None; QueuedToDownload=false; IsLoading = false }

    
    let init audiobook = audiobook |> initModel, Cmd.none, None


    let update msg model =
        match msg with
        | OpenAudioBookActionMenu ->
            let openActionMenuCmd =
                Controls.audioBookEntryActionSheet 
                    (fun a -> Msg.AddToDownloadQueue)
                    (fun a -> Msg.RemoveFromDownloadQueue)
                    (fun a -> DeleteAudiobook)
                    (fun a -> MarkAudioBookAsListend)
                    (fun a -> UnmarkAudioBookAsListend)
                    (fun a -> DoNothing)
                    model.QueuedToDownload
                    model.AudioBook
                |> Cmd.ofAsyncMsgOption
                    
            model, openActionMenuCmd, None
        | Msg.AddToDownloadQueue ->
            
            let newModel = { model with QueuedToDownload = true}
            newModel, Cmd.none, Some (ExternalMsg.AddToDownloadQueue model)
        | Msg.RemoveFromDownloadQueue ->
            
            let newModel = { model with QueuedToDownload = false}
            newModel, Cmd.none, Some (ExternalMsg.RemoveFromDownloadQueue model)

        | AudiobookDownloaded (ab,mp3Path,imageFullName,thumbnail) ->
            let newState = {model.AudioBook.State with Downloaded = true; DownloadedFolder = Some mp3Path}
            let newAudioBook = {ab with State = newState; Picture = imageFullName; Thumbnail = thumbnail}                            
            let newModel = {model with AudioBook = newAudioBook }
            let updateStateCmd = newModel |> updateAudiobookInStateFile
            newModel, Cmd.batch [Helpers.unsetBusyCmd;Helpers.setGlobalBusyCmd; updateStateCmd], Some (UpdateAudioBook newAudioBook)

        | DeleteAudiobook ->
            // removeAudiobook
            let newState = {model.AudioBook.State with Downloaded = false; DownloadedFolder = None}
            let newAudioBook = {model.AudioBook with State = newState; }
            match Services.removeAudiobook model.AudioBook with
            | Error e ->
                Common.Helpers.displayAlert("Error Remove Audiobook",e,"OK") |> ignore
                model,Cmd.none,None
            | Ok _ ->
                let newModel = {model with AudioBook = newAudioBook }
                let updateStateCmd = newModel |> updateAudiobookInStateFile
                newModel, Cmd.batch [Helpers.setGlobalBusyCmd;updateStateCmd ], Some (UpdateAudioBook newAudioBook)

        | MarkAudioBookAsListend ->
            
            let newState = {model.AudioBook.State with Completed = true}
            let newAudioBook = {model.AudioBook with State = newState; }                
            let newModel = {model with AudioBook = newAudioBook }
            let updateStateCmd = newModel |> updateAudiobookInStateFile
            newModel, Cmd.batch [Helpers.setGlobalBusyCmd; updateStateCmd ], Some (UpdateAudioBook newAudioBook)

        | UnmarkAudioBookAsListend ->
           
            let newState = {model.AudioBook.State with Completed = false}
            let newAudioBook = {model.AudioBook with State = newState; }
            let newModel = {model with AudioBook = newAudioBook }
            let updateStateCmd = newModel |> updateAudiobookInStateFile
            newModel, Cmd.batch [Helpers.setGlobalBusyCmd; updateStateCmd ], Some (UpdateAudioBook newAudioBook)

        | UpdateDownloadProgress progress ->            
            {model with CurrentDownloadProgress = progress}, Cmd.none, None

        | ChangeBusyState state -> 
            {model with IsLoading = state}, Cmd.none, None

        | ChangeGlobalBusyState state -> 
            model, Cmd.none, Some (PageChangeBusyState state)

        | Msg.OpenAudioBookPlayer  ->
            model, Cmd.none, Some (ExternalMsg.OpenAudioBookPlayer model.AudioBook)

        | DoNothing ->
            model, Cmd.ofMsg (ChangeBusyState false), None


    let view (model: Model) dispatch =
        model.AudioBook 
        |> Controls.renderAudiobookEntry 
            (fun ()-> dispatch (OpenAudioBookActionMenu)) 
            (fun ()-> dispatch (Msg.OpenAudioBookPlayer)) 
            model.IsLoading 
            model.QueuedToDownload 
            model.CurrentDownloadProgress
    


    

