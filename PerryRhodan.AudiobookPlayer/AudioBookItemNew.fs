module AudioBookItemNew

    open Domain
    open Fabulous
    open Fabulous.XamarinForms
    open Common
    open Xamarin.Forms
    open Services
    open Global
    open System


    type DownloadState =
        | NotDownloaded
        | Queued
        | Downloading of (int * int)
        | Downloaded


    type ListenState =
        | Unlistend
        | InProgress of Domain.AudioBookPosition
        | Listend




    type Model = { 
        AudioBook: AudioBook
        DownloadState: DownloadState
        ListenState: ListenState
    }


    type Msg =
        | OpenAudioBookActionMenu
        | AddToDownloadQueue
        | RemoveFromDownloadQueue
        
        | DeleteAudiobook
        | AudioBookDeleted
        | MarkAudioBookAsListend
        | UnmarkAudioBookAsListend
        | UpdateDownloadProgress of (int * int)
        | OpenAudioBookPlayer
        | OpenAudioBookDetail
        | DeleteItemFromDb
        | DeletedFromDb
        | DownloadCompleted of Services.WebAccess.Downloader.DownloadResult


    type AudioBookItem = {
        Dispatch: Msg -> unit
        Model:Model
    }


    module Commands =


        let openMenuCmd model =
            async {
                let buttons = [|
                    (Translations.current.AudioBookDescription,OpenAudioBookDetail)

                    match model.DownloadState with
                    | Downloaded    ->
                        (Translations.current.RemoveFromDevice,DeleteAudiobook)
                    | Queued        ->
                        (Translations.current.RemoveFromDownloaQueue,RemoveFromDownloadQueue)
                    | NotDownloaded ->
                        (Translations.current.DownloadAudioBook,AddToDownloadQueue)
                    | Downloading   ->
                        ()

                    
                    match model.ListenState with
                    | Unlistend ->
                        (Translations.current.MarkAsListend,MarkAudioBookAsListend)
                    | InProgress _ 
                    | Listend   ->
                        (Translations.current.UnmarkAsListend,UnmarkAudioBookAsListend)
                    

                    let isDevMode = 
                        Services.SystemSettings.getDeveloperMode() |> Async.RunSynchronously
                    if isDevMode then
                        ("Remove Item from Database",DeleteItemFromDb)  

                |]
                return! Helpers.displayActionSheet (Some Translations.current.PleaseSelect) (Some Translations.current.Cancel) buttons
            }
            |> Cmd.ofAsyncMsgOption

    
        let updateAudiobookInStateFile (model:Model) =
            fun _ ->
                async {
                    let! res = model.AudioBook |> DataBase.updateAudioBookInStateFile            
                    match res with
                    | Error e ->
                        do! Common.Helpers.displayAlert(Translations.current.Error,e,"OK")
                        ()
                    | Ok _ ->
                        ()
                }
                |> Async.Start
            |> Cmd.ofSub


        let deleteItemFromDb (model:Model) =
            async {
                let! diaRes = Common.Helpers.displayAlertWithConfirm("Remove item from DB","Are you sure?",Translations.current.Yes,Translations.current.No)
                if diaRes then
                    let! res = DataBase.removeAudiobookFromDatabase model.AudioBook
                    match res with
                    | Error e ->
                        do! Common.Helpers.displayAlert("Delete Audiobook Entry",e,"OK")
                        return None
                    | Ok _ ->
                       return (Some DeletedFromDb)
                else
                    return None
            } |> Cmd.ofAsyncMsgOption


        let deleteAudiobook (model:Model) =
            async {
                match DataBase.removeAudiobook model.AudioBook with
                | Error e ->
                    do! Common.Helpers.displayAlert(Translations.current.ErrorRemoveAudioBook,e,"OK")
                    return None
                | Ok _ ->
                    return Some AudioBookDeleted
            }
            |> Cmd.ofAsyncMsgOption



        
            

    
    let init audiobook = 
        { 
            AudioBook = audiobook
            DownloadState = if audiobook.State.Downloaded then Downloaded else NotDownloaded
            ListenState = 
                match audiobook.State.Completed, audiobook.State.CurrentPosition with
                | true, _           -> Listend
                | false, Some pos   -> InProgress pos
                | false, None       -> Unlistend
        }

    

    let rec update msg (model:Model) =
        match msg with
        | OpenAudioBookActionMenu ->
            model, Commands.openMenuCmd model

        | Msg.AddToDownloadQueue ->
            { model with DownloadState = Queued }, Cmd.none

        | Msg.RemoveFromDownloadQueue ->
            { model with DownloadState = NotDownloaded }, Cmd.none

        | DownloadCompleted result ->
            model |> onAudioBookDownloadedMsg result

        | DeleteAudiobook ->
            model, Commands.deleteAudiobook model

        | AudioBookDeleted ->
            model |> onAudioBookDeletedMsg

        | MarkAudioBookAsListend ->
            model |> onMarkAudioBookListendMsg

        | UnmarkAudioBookAsListend ->
            model |> onMarkAudioBookUnlistendMsg

        | UpdateDownloadProgress progress ->            
            { model with DownloadState = Downloading progress }, Cmd.none

        | Msg.OpenAudioBookPlayer  ->
            model, Cmd.none

        | Msg.OpenAudioBookDetail ->
            model, Cmd.none

        | DeleteItemFromDb ->
            model, Commands.deleteItemFromDb model

        | DeletedFromDb ->
            model, Cmd.none
        
    

    and onAudioBookDownloadedMsg result model =
        let newState = {model.AudioBook.State with Downloaded = true; DownloadedFolder = Some result.TargetFolder}
        let imageFullName = result.Images |> Option.map (fun i -> i.Image)
        let thumbnail = result.Images |> Option.map (fun i -> i.Thumbnail)
        let newAudioBook = {model.AudioBook with State = newState; Picture = imageFullName; Thumbnail = thumbnail}                            
        let newModel = {model with AudioBook = newAudioBook; DownloadState = Downloaded }
        newModel, Commands.updateAudiobookInStateFile newModel


    and onAudioBookDeletedMsg model =
        let newState = {model.AudioBook.State with Downloaded = false; DownloadedFolder = None}
        let newAudioBook = {model.AudioBook with State = newState; }
        let newModel = {model with AudioBook = newAudioBook }
        newModel, Commands.updateAudiobookInStateFile newModel

    
    and onMarkAudioBookListendMsg model =
        match model.ListenState with
        | Listend ->
            model, Cmd.none
        | InProgress _
        | Unlistend ->
            let newState = {model.AudioBook.State with Completed = true}
            let newAudioBook = {model.AudioBook with State = newState; }                
            let newModel = {model with AudioBook = newAudioBook; ListenState = Listend }
            newModel, newModel |> Commands.updateAudiobookInStateFile


        
    
    and onMarkAudioBookUnlistendMsg model =
        match model.ListenState with
        | Unlistend ->
            model, Cmd.none

        | InProgress _
        | Listend ->
            let newState = {
                model.AudioBook.State 
                    with 
                        Completed = false
                        CurrentPosition = None
            }
            let newAudioBook = {model.AudioBook with State = newState;  }
            let newModel = {model with AudioBook = newAudioBook; ListenState = Unlistend }
            newModel, newModel |> Commands.updateAudiobookInStateFile
        
   

    let view (model: Model) dispatch =
        View.Grid(
            backgroundColor = Consts.cardColor,
            margin=Thickness 5.,
            height = 120.,
            coldefs = [Auto; Star; Auto],
            rowdefs = [Auto],
            children = [
                match model.AudioBook.Thumbnail with
                | None ->
                    yield View.Image(source=ImagePath "AudioBookPlaceholder_Dark.png"
                        , aspect = Aspect.AspectFit
                        , height=100.
                        , width=100.
                        , margin=Thickness 10.).Column(0).Row(0)
                | Some thumb ->
                    yield View.Image(source=ImagePath thumb
                        , aspect = Aspect.AspectFit
                        , height=100.
                        , width=100.
                        , margin=Thickness 10.
                        ).Column(0).Row(0)
                
                // audioBook state
                yield (
                    View.Grid(
                        backgroundColor = Color.Transparent,
                        margin=Thickness 10.,
                        coldefs = [Star; Star; Star],
                        rowdefs = [Star; Star; Star],
                        children = [
                            match model.DownloadState with
                            | NotDownloaded ->
                                Controls.arrowDownLabel.Column(1).Row(1)
                            | Queued ->
                                Controls.inDownloadQueueLabel.Column(2).Row(2)
                            | Downloading (c,a) ->
                                ((c,a) |> Controls.showDownloadProgress).ColumnSpan(3).Row(2).Column(0)
                            | Downloaded ->
                                Controls.playerSymbolLabel.Column(1).Row(1)


                            match model.ListenState with
                            | Unlistend ->
                                ()
                            | InProgress pos ->
                                ()
                            | Listend ->
                                Controls.listendCheckLabel.Column(2).Row(2)
                        ]
                        , gestureRecognizers = 
                            [
                                View.TapGestureRecognizer(
                                    command = (fun () -> 
                                        if model.DownloadState=Downloaded then 
                                            dispatch OpenAudioBookPlayer
                                    )
                                )
                        ]
                    )
                
                ).Column(0).Row(0)

                yield View.Label(text=model.AudioBook.FullName, 
                    fontSize = FontSize 15., 
                    verticalOptions = LayoutOptions.Fill, 
                    horizontalOptions = LayoutOptions.Fill, 
                    verticalTextAlignment = TextAlignment.Center,
                    horizontalTextAlignment = TextAlignment.Center,
                    textColor = Consts.secondaryTextColor,
                    lineBreakMode = LineBreakMode.WordWrap
                    ).Column(1).Row(0)
                yield View.Grid(
                    verticalOptions = LayoutOptions.Fill, 
                    horizontalOptions = LayoutOptions.Fill,
                    gestureRecognizers = [
                        View.TapGestureRecognizer(command = fun () -> dispatch OpenAudioBookActionMenu)
                    ],
                    children = [
                        View.Label(text="\uf142",fontFamily = Controls.faFontFamilyName true,
                            fontSize=FontSize 35., 
                            margin = Thickness(10., 0. ,10. ,0.),                    
                            verticalOptions = LayoutOptions.Fill, 
                            horizontalOptions = LayoutOptions.Fill, 
                            verticalTextAlignment = TextAlignment.Center,
                            horizontalTextAlignment = TextAlignment.Center,
                            textColor = Consts.secondaryTextColor
                            )
                    ]
                ).Column(2).Row(0)

        ]
        )
    


    

