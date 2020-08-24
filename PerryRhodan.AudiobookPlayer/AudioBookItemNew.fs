module AudioBookItemNew

    open Domain
    open Fabulous
    open Fabulous.XamarinForms
    open Common
    open Xamarin.Forms
    open Services
    open Global
    open System
    open System.IO


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
        AudioFileInfos:AudioBookAudioFilesInfo option
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

        | ShowMetaData

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
                        ("Remove Item from Database", DeleteItemFromDb)  
                        ("Show Entry MetaData", ShowMetaData)

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
                match! DataBase.removeAudiobook model.AudioBook with
                | Error e ->
                    do! Common.Helpers.displayAlert(Translations.current.ErrorRemoveAudioBook,e,"OK")
                    return None
                | Ok _ ->
                    return Some AudioBookDeleted
            }
            |> Cmd.ofAsyncMsgOption

        let showMetaDataCmd (model:Model) =
            fun _ ->
                async {
                    let msg = sprintf "%A" model.AudioBook
                    do! Common.Helpers.displayAlert ("MetaData",msg,"OK")
                }
                |> Async.Start
            |> Cmd.ofSub


    
    let init audiobook = 
        { 
            AudioBook = audiobook
            DownloadState = if audiobook.State.Downloaded then Downloaded else NotDownloaded
            ListenState = 
                match audiobook.State.Completed, audiobook.State.CurrentPosition with
                | true, _           -> Listend
                | false, Some pos   -> InProgress pos
                | false, None       -> Unlistend
            AudioFileInfos = Services.DataBase.getAudioBookFileInfoTimeout 100 audiobook.Id
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

        | ShowMetaData ->
            model, Commands.showMetaDataCmd model
        
    

    and onAudioBookDownloadedMsg result model =
        let newState = {model.AudioBook.State with Downloaded = true; DownloadedFolder = Some result.TargetFolder}
        let imageFullName = result.Images |> Option.map (fun i -> i.Image)
        let thumbnail = result.Images |> Option.map (fun i -> i.Thumbnail)
        let newAudioBook = {model.AudioBook with State = newState; Picture = imageFullName; Thumbnail = thumbnail}                            
        let newModel = {model with AudioBook = newAudioBook; DownloadState = Downloaded }
        newModel, Commands.updateAudiobookInStateFile newModel


    and onAudioBookDeletedMsg model =
        let newState = {model.AudioBook.State with Downloaded = false; DownloadedFolder = None}
        let newAudioBook = {model.AudioBook with State = newState }
        let newModel = { model with AudioBook = newAudioBook; DownloadState = NotDownloaded }
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
        
    let audioBookDuration model =
        model.AudioFileInfos
        |> Option.map (fun fileInfo ->
            fileInfo.AudioFiles |> List.sumBy (fun i -> i.Duration)
        )
        |> Option.defaultValue 0

    
    let getProgressAndTotalDuration (model:Model) =
        if model.DownloadState <> Downloaded then
            None
        else
            match model.AudioFileInfos, model.AudioBook.State.CurrentPosition with
            | Some fileInfo, Some position ->

                let audioBookDuration = fileInfo.AudioFiles |> List.sumBy (fun i -> i.Duration) |> float  

                let trackIndex =
                    fileInfo.AudioFiles
                    |> List.tryFindIndex (fun i -> i.FileName = position.Filename)
                    |> Option.defaultValue 0

                let durationUntilCurrentTrack =
                    fileInfo.AudioFiles
                    |> List.take trackIndex
                    |> List.sumBy (fun i -> i.Duration)

                let currentTimeInMs = position.Position.TotalMilliseconds + (durationUntilCurrentTrack |> float)

                Some {| CurrentProgress = currentTimeInMs; TotalDuration = audioBookDuration; Rest = audioBookDuration - currentTimeInMs |}
            | _ ->
                None
    
    let percentageFinished (model: Model) =
        model
        |> getProgressAndTotalDuration 
        |> Option.map (fun x -> (x.CurrentProgress / x.TotalDuration))
        

    module Draw =

        open global.SkiaSharp
        open global.SkiaSharp.Views.Forms

        let calcAlpha factor =
            let alphaDiffBase = (factor * 1000.0) |> int
            let alphaDiff = Math.Cos(factor * 31.4) * 100.0 |> int
            let alpha = 127 + alphaDiff |> uint8
            alpha

        let loadingPie factor =
            let f32 = float32
            Fabulous.XamarinForms.SkiaSharp.View.SKCanvasView(
                invalidate = true,
                margin = Thickness(5.),                 
                paintSurface = 
                    fun args ->
                        let info = args.Info
                        let surface = args.Surface
                        let canvas = surface.Canvas
                        canvas.Clear()

                        let center = new SKPoint((info.Width |> f32) / 2.0f, (info.Height |> f32) / 2.0f);
                        let explodeOffset = 0;
                        let margin = 20;
                        let radius = (Math.Min(info.Width / 2, info.Height  / 2) - (2 * explodeOffset + 2 * margin)) |> f32
                        let rect = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

                        let drawPie transparent (color:Color) startAngle sweepAngle =
                            use path = new SKPath()
                            use fillPaint = new SKPaint()
                            use outlinePaint = new SKPaint()
                            
                    
                            path.MoveTo(center);
                            path.ArcTo(rect, startAngle, sweepAngle, false);
                            path.Close();

                            fillPaint.Style <- SKPaintStyle.Fill
                            fillPaint.Color <- color.ToSKColor().WithAlpha(calcAlpha factor)
                            fillPaint.BlendMode <- SKBlendMode.Plus
                            fillPaint.IsAntialias <- false

                            outlinePaint.Style <- SKPaintStyle.Stroke;
                            outlinePaint.StrokeWidth <- 6.0f;
                            outlinePaint.Color <- color.ToSKColor() //.WithAlpha(alpha)
                            outlinePaint.IsAntialias <- false

                            
                            
                            // Calculate "explode" transform
                            let angle = startAngle + 0.5f * sweepAngle |> float;
                            let x = (explodeOffset |> float) * Math.Cos(Math.PI * angle / 180.0) |> float32;
                            let y = (explodeOffset |> float) * Math.Sin(Math.PI * angle / 180.0) |> float32;

                            canvas.Save() |> ignore
                            canvas.Translate(x, y);

                            // Fill and stroke the path
                            if not transparent then
                                canvas.DrawPath(path, fillPaint);
                            canvas.DrawPath(path, outlinePaint);
                            canvas.Restore();

                        use textPaint = new SKPaint()
                        textPaint.TextSize <- 75.0f
                        textPaint.Color <- Color.White.ToSKColor()
                        textPaint.BlendMode <- SKBlendMode.Plus
                        textPaint.IsAntialias <- false
                        textPaint.TextAlign <- SKTextAlign.Center
                        
                        let text = sprintf "%i %%" ((factor * 100.0) |> int) 
                        let y = ((info.Height  / 2) |> f32) + (textPaint.TextSize / 2.0f) - 15.0f
                        canvas.DrawText(text, (info.Width / 2) |> f32,y , textPaint)

                        canvas.ResetMatrix()
                        

                        let startAngle = -90.0f
                        let sweepAngle = 
                            let x = 360.0 * factor |> float32;
                            if x >= 360.0f then 359.99f else x

                        drawPie false (Color.FromHex("96FF33")) startAngle sweepAngle
                    )


        let progressPie factor =
            let f32 = float32
            Fabulous.XamarinForms.SkiaSharp.View.SKCanvasView(
                automationId="progressPie",
                invalidate = true,
                margin=Thickness(4.),
                width = 20.,
                height = 20.,
                paintSurface = 
                    fun args ->
                        let info = args.Info
                        let surface = args.Surface
                        let canvas = surface.Canvas
                        canvas.Clear()



                        let center = new SKPoint((info.Width |> f32) / 2.0f, (info.Height |> f32) / 2.0f);
                        let margin = 0;
                        let radius = (Math.Min(info.Width / 2, info.Height  / 2) - (2 * margin)) |> f32
                        let rect = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

                        let drawPie (color:Color) startAngle sweepAngle =
                            use path = new SKPath()
                            use fillPaint = new SKPaint()
                            use outlinePaint = new SKPaint()
                            
                            path.MoveTo(center);
                            path.ArcTo(rect, startAngle, sweepAngle, false);
                            path.Close();

                            fillPaint.Style <- SKPaintStyle.StrokeAndFill
                            fillPaint.Color <- color.ToSKColor()
                            fillPaint.IsAntialias <- false
                            
                            outlinePaint.Style <- SKPaintStyle.Stroke;
                            outlinePaint.StrokeWidth <- 2.0f;
                            outlinePaint.Color <- Color.Black.ToSKColor()
                            outlinePaint.IsAntialias <- false

                            canvas.DrawPath(path, fillPaint);
                            canvas.DrawPath(path, outlinePaint);
                    

                        let startAngle = -90.0f;
                        let sweepAngle = 
                            let x = 360.0 * factor |> float32;
                            if x >= 360.0f then 359.99f else x

                        drawPie Color.Yellow startAngle sweepAngle
                    )
        
       
    let getHoursAndMinutes ms =
        let ts = TimeSpan.FromMilliseconds(ms |> float)
        let minutes = Math.Floor(ts.TotalHours) |> int
        minutes, ts.Minutes   

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
                    View.Image(source=Image.fromPath "AudioBookPlaceholder_Dark.png"
                        , aspect = Aspect.AspectFit
                        , height=100.
                        , width=100.
                        , margin=Thickness 10.).Column(0).Row(0)
                | Some thumb ->
                    View.Image(source=Image.fromPath thumb
                        , aspect = Aspect.AspectFit
                        , height=100.
                        , width=100.
                        , margin=Thickness 10.
                        ).Column(0).Row(0)
                
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
                            Controls.inDownloadQueueLabel.Column(1).Row(1)
                        | Downloading (c,a) ->
                            let factor = (c |> float) / (a |> float)
                            View.Grid(
                                children = [
                                    Draw.loadingPie factor
                                ]
                            ).ColumnSpan(3).RowSpan(3)
                        | Downloaded ->
                            Controls.playerSymbolLabel.Column(1).Row(1)
                            
                        match model.ListenState with
                        | Unlistend ->
                            ()
                        | InProgress pos ->
                            match percentageFinished model with
                            | None -> ()
                            | Some progress ->
                                View.Grid(
                                    children = [
                                        (Draw.progressPie progress)
                                    ]
                                ).Column(0).Row(2)
                                    
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
                ).Column(0).Row(0)



                View.Grid(
                    rowdefs = [Star; Auto; Auto; Star],
                    verticalOptions = LayoutOptions.Center, 
                    horizontalOptions = LayoutOptions.Fill, 
                    children= [
                        View.Label(text=model.AudioBook.FullName, 
                            fontSize = FontSize.fromValue 15., 
                            verticalOptions = LayoutOptions.Fill, 
                            horizontalOptions = LayoutOptions.Fill, 
                            verticalTextAlignment = TextAlignment.Center,
                            horizontalTextAlignment = TextAlignment.Center,
                            textColor = Consts.primaryTextColor,
                            lineBreakMode = LineBreakMode.WordWrap
                        ).Row(1)

                        match model.ListenState with
                        | InProgress _ ->
                            match getProgressAndTotalDuration model with
                            | Some totalTimes ->
                                

                                let (m,s) = totalTimes.Rest |> getHoursAndMinutes
                                let progressStr = sprintf "insgesamt noch %i h %i min übrig" m s

                                View.Label(text=progressStr, 
                                    fontSize = FontSize.fromValue 11., 
                                    verticalOptions = LayoutOptions.Fill, 
                                    horizontalOptions = LayoutOptions.Fill, 
                                    verticalTextAlignment = TextAlignment.Center,
                                    horizontalTextAlignment = TextAlignment.Center,
                                    textColor = Consts.secondaryTextColor,
                                    lineBreakMode = LineBreakMode.WordWrap
                                ).Row(2)
                            | None ->
                                ()
                        | _ ->
                            ()
                    ]
                ).Column(1).Row(0)

                View.Grid(
                    verticalOptions = LayoutOptions.Fill, 
                    horizontalOptions = LayoutOptions.Fill,
                    gestureRecognizers = [
                        View.TapGestureRecognizer(command = fun () -> dispatch OpenAudioBookActionMenu)
                    ],
                    children = [
                        View.Label(text="\uf142",fontFamily = Controls.faFontFamilyName true,
                            fontSize=FontSize.fromValue 35., 
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



    module Helpers =
        
        let getNew toDispatch (audiobookItems:AudioBookItem []) (audiobooks:Domain.AudioBook []) =
            let currentItems = audiobookItems |> Array.map (fun i -> i.Model.AudioBook)
            let newAudioBooks = Domain.filterNewAudioBooks currentItems audiobooks
            newAudioBooks
            |> Array.map (init)
            |> Array.map (fun i ->
                {
                    Model = i
                    Dispatch = toDispatch i
                }
            )

        let synchronize toDispatch (audiobookItems:AudioBookItem []) (audiobooks:Domain.AudioBook []) =
            let currentItems = audiobookItems |> Array.map (fun i -> i.Model.AudioBook)
            let newAudioBooks = Domain.filterNewAudioBooks currentItems audiobooks
            let namesToFix = Domain.findDifferentAudioBookNames currentItems audiobooks
            let newAudioBookItems =
                getNew toDispatch audiobookItems audiobooks 
            Array.concat [audiobookItems; newAudioBookItems]




    

