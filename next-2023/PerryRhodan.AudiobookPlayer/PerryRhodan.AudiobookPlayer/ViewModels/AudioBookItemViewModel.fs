namespace PerryRhodan.AudiobookPlayer.ViewModels

open Avalonia
open Avalonia.Controls

open Avalonia.Media.Imaging
open Avalonia.Platform
open CherylUI.Controls
open Domain
open Elmish
open Material.Dialog


module AudioBookItem =
    // State
    type State = { 
        AudioBook: AudioBook
        DownloadState: DownloadState
        ListenState: ListenState
        AudioFileInfos:AudioBookAudioFilesInfo option
    }
    
    and DownloadState =
        | NotDownloaded
        | Queued
        | Downloading of (int * int)
        | Downloaded


    and ListenState =
        | Unlistend
        | InProgress of Domain.AudioBookPosition
        | Listend
        
        
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
        
        
    [<RequireQualifiedAccess>]
    type SideEffect =
        | OpenAudioBookActionMenu
        | None
        


    
    let init audiobook = 
        { 
            AudioBook = audiobook
            DownloadState = if audiobook.State.Downloaded then Downloaded else NotDownloaded
            ListenState = 
                match audiobook.State.Completed, audiobook.State.CurrentPosition with
                | true, _           -> Listend
                | false, Some pos   -> InProgress pos
                | false, None       -> Unlistend
            AudioFileInfos = Services.DataBase.getAudioBookFileInfoTimeout 100 audiobook.Id // Todo: unbedingt ändern!
        }
        
        
        
    let update msg state =
        match msg with
        | OpenAudioBookActionMenu ->
            state, SideEffect.OpenAudioBookActionMenu
        | _ -> state, SideEffect.None
        
        
        
    module SideEffects =
        
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()

                | SideEffect.OpenAudioBookActionMenu ->
                    let control = PerryRhodan.AudiobookPlayer.Views.ActionMenuView()
                    let vm = new ActionMenuViewModel(state.AudioBook)
                    control.DataContext <- vm
                    InteractiveContainer.ShowDialog (control, true)
                    
                    return ()
            }
        
        

open AudioBookItem
open ReactiveElmish
open ReactiveElmish.Avalonia
open Elmish.SideEffect
open System

module DemoData =
    let designAudioBook = {
            Id = 1
            FullName = "AudioBook Name" 
            EpisodeNo = Some 1000
            EpisodenTitel = "Episode Title"
            Group = "Group 1"
            Picture = None
            Thumbnail = None
            DownloadUrl = None
            ProductSiteUrl = None
            State = {
                Completed = true
                CurrentPosition = None
                Downloaded = true
                DownloadedFolder = None
                LastTimeListend = None
            }
        }            


 module Draw =

        open global.SkiaSharp
        open global.Avalonia.Skia

        let calcAlpha factor =
            let alphaDiffBase = (factor * 1000.0) |> int
            let alphaDiff = Math.Cos(factor * 31.4) * 100.0 |> int
            let alpha = 127 + alphaDiff |> uint8
            alpha

        let loadingPie factor =
            let f32 = float32

            let width = 250
            let height = 250
            use bitmap =  new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)
            use canvas =  new SKCanvas(bitmap)
            canvas.Clear()
            let center = new SKPoint((width |> f32) / 2.0f, (height |> f32) / 2.0f);
            let explodeOffset = 0;
            let margin = 20;
            let radius = (Math.Min(width / 2, height  / 2) - (2 * explodeOffset + 2 * margin)) |> f32
            let rect = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

            let drawPie transparent (color:SKColor) startAngle sweepAngle =
                use path = new SKPath()
                use fillPaint = new SKPaint()
                use outlinePaint = new SKPaint()


                path.MoveTo(center);
                path.ArcTo(rect, startAngle, sweepAngle, false);
                path.Close();

                fillPaint.Style <- SKPaintStyle.Fill
                fillPaint.Color <- color.WithAlpha(calcAlpha factor)
                fillPaint.BlendMode <- SKBlendMode.Plus
                fillPaint.IsAntialias <- false

                outlinePaint.Style <- SKPaintStyle.Stroke;
                outlinePaint.StrokeWidth <- 6.0f;
                outlinePaint.Color <- color //.WithAlpha(alpha)
                outlinePaint.IsAntialias <- false



                // Calculate "explode" transform
                let angle = startAngle + 0.5f * sweepAngle |> float;
                let x = (explodeOffset |> float) * Math.Cos(Math.PI * angle / 180.0) |> float32;
                let y = (explodeOffset |> float) * Math.Sin(Math.PI * angle / 180.0) |> float32;

                canvas.Save() |> ignore
                canvas.Translate(x, y);

                // Fill and stroke the path
                if not transparent then
                    canvas.DrawPath(path, fillPaint)
                canvas.DrawPath(path, outlinePaint)
                canvas.Restore()

            use textPaint = new SKPaint()
            textPaint.TextSize <- 75.0f
            textPaint.Color <- SKColor.Parse("FFFFFF")
            textPaint.BlendMode <- SKBlendMode.Plus
            textPaint.IsAntialias <- false
            textPaint.TextAlign <- SKTextAlign.Center

            let text = $"{((factor * 100.0) |> int)} %%" 
            let y = ((height  / 2) |> f32) + (textPaint.TextSize / 2.0f) - 15.0f
            canvas.DrawText(text, (width / 2) |> f32,y , textPaint)

            canvas.ResetMatrix()


            let startAngle = -90.0f
            let sweepAngle =
                let x = 360.0 * factor |> float32;
                if x >= 360.0f then 359.99f else x

            drawPie false (SKColor.Parse("96FF33")) startAngle sweepAngle
            canvas.Save() |> ignore
            let data = bitmap.Encode(SKEncodedImageFormat.Png, 100)
            use stream = data.AsStream()
            let image = new Bitmap(stream)
            image
            
        
                




        let progressPie factor =
            let f32 = float32

            let width = 250
            let height = 250
            use bitmap =  new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)
            use canvas =  new SKCanvas(bitmap)
            canvas.Clear()

            let center = new SKPoint((width |> f32) / 2.0f, (height |> f32) / 2.0f);
            let margin = 0;
            let radius = (Math.Min(width / 2, height  / 2) - (2 * margin)) |> f32
            let rect = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

            let drawPie (color:SKColor) startAngle sweepAngle =
                use path = new SKPath()
                use fillPaint = new SKPaint()
                use outlinePaint = new SKPaint()

                path.MoveTo(center);
                path.ArcTo(rect, startAngle, sweepAngle, false);
                path.Close();

                fillPaint.Style <- SKPaintStyle.StrokeAndFill
                fillPaint.Color <- color
                fillPaint.IsAntialias <- false

                outlinePaint.Style <- SKPaintStyle.Stroke;
                outlinePaint.StrokeWidth <- 2.0f;
                outlinePaint.Color <- SKColors.Black
                outlinePaint.IsAntialias <- false

                canvas.DrawPath(path, fillPaint);
                canvas.DrawPath(path, outlinePaint);


            let startAngle = -90.0f;
            let sweepAngle =
                let x = 360.0 * factor |> float32;
                if x >= 360.0f then 359.99f else x

            drawPie SKColors.Yellow startAngle sweepAngle
            canvas.Save() |> ignore
            let data = bitmap.Encode(SKEncodedImageFormat.Png, 100)
            use stream = data.AsStream()
            let image = new Bitmap(stream)
            image


module Helpers =
    let getProgressAndTotalDuration (state:State) =
        if state.DownloadState <> Downloaded then
            None
        else
            match state.AudioFileInfos, state.AudioBook.State.CurrentPosition with
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

    
type AudioBookItemViewModel(audiobook: Domain.AudioBook option) =
    inherit ReactiveElmishViewModel()
    
    let init () =
        init audiobook.Value, SideEffect.None
    
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
    
    member this.AudioBook = this.Bind(local, _.AudioBook)
    member this.DownloadState = this.Bind(local, _.DownloadState)
    member this.ListenState = this.Bind(local, _.ListenState)
    member this.AudioFileInfos = this.Bind(local, _.AudioFileInfos)
    member this.OpenDialog() = local.Dispatch (OpenAudioBookActionMenu)
    
    member this.Title = this.Bind(local, _.AudioBook.FullName)
    member this.EpisodeTitle = this.Bind(local, _.AudioBook.EpisodenTitel)        
        
    member this.Thumbnail = this.Bind(local,
                                      fun s ->
                                          s.AudioBook.Thumbnail
                                          |> Option.defaultValue "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
                                          )
    member this.LoadingPie = this.Bind(local, fun s ->
        match s.DownloadState with
        | Downloading (current, total) -> Draw.loadingPie (float current / float total)
        | _ -> Unchecked.defaultof<Bitmap>
    )
    
    member this.ProgressPie = this.Bind(local, fun s ->
        let percentageFinished (s: State) =
            s
            |> Helpers.getProgressAndTotalDuration
            |> Option.map (fun x -> (x.CurrentProgress / x.TotalDuration))
        
        match percentageFinished s with
        | Some progress -> Draw.progressPie progress 
        | _ -> Unchecked.defaultof<Bitmap>)
    
    static member DesignVM = new AudioBookItemViewModel(Some DemoData.designAudioBook)
        
        
