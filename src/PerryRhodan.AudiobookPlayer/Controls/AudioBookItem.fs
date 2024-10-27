namespace rec PerryRhodan.AudiobookPlayer.ViewModel

open Avalonia.Controls
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Threading
open CherylUI.Controls
open Common
open Dependencies
open Domain
open PerryRhodan.AudiobookPlayer.ViewModels
open ReactiveElmish
open ReactiveElmish.Avalonia
open Elmish.SideEffect
open Services
open System
open AudioBookItem
open Services.DependencyServices
open SkiaSharp



module AudioBookStore =

    /// handles audiobook viewmodels all over the application
    module AudioBookElmish =

        type State = {
            Audiobooks:AudioBookItemViewModel array
            CurrentAudioBook:AudioBookItemViewModel option
            IsLoading:bool
        }

        type Msg =
            | AudiobooksLoaded of AudioBookItemViewModel array
            | DeleteAudiobookFromDatabase of AudioBook
            | RemoveAudiobookFromDevice of AudioBook
            | AudioBookChanged // only trigger for the reactive subscriptions on this store
            | IsBusy of bool

        [<RequireQualifiedAccess>]
        type SideEffect =
            | None
            | LoadAudiobooks
            | DeleteAudiobookFromDatabase of AudioBook
            | RemoveAudiobookFromDevice of AudioBook


        let init () =
            { Audiobooks = [||]; CurrentAudioBook = None; IsLoading = false }, SideEffect.LoadAudiobooks


        let update (msg:Msg) (state:State) =
            match msg with
            | AudioBookChanged ->
                state, SideEffect.None
                
            | AudiobooksLoaded audiobooks ->
                { state with Audiobooks = audiobooks }, SideEffect.None

            | DeleteAudiobookFromDatabase audiobook ->
                { state with
                    Audiobooks =
                        state.Audiobooks
                        |> Array.filter (fun x -> x.AudioBook <> audiobook)
                }, SideEffect.DeleteAudiobookFromDatabase audiobook

            | RemoveAudiobookFromDevice audiobook ->
                // let the view model do the work
                state, SideEffect.RemoveAudiobookFromDevice audiobook

            | IsBusy b ->
                { state with IsLoading = b }, SideEffect.None


        module SideEffects =

            let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
                task {
                    if sideEffect = SideEffect.None then
                        return ()
                    else
                        dispatch <| IsBusy true
                        do!
                            task {
                                match sideEffect with
                                | SideEffect.None ->
                                    return ()

                                | SideEffect.LoadAudiobooks ->
                                    let! audiobooks = DataBase.loadAudioBooksStateFile()
                                    dispatch <| AudiobooksLoaded (audiobooks |> Array.map (fun i -> new AudioBookItemViewModel(i)))
                                    return ()


                                | SideEffect.DeleteAudiobookFromDatabase audiobook ->
                                    Notifications.showToasterMessage $"TODO: Audiobook deleted from Database {audiobook.FullName}"
                                    return ()

                                | SideEffect.RemoveAudiobookFromDevice audiobook ->
                                    let! res = DataBase.removeAudiobook audiobook
                                    match res with
                                    | Ok _ ->
                                        // remove from the viewmodel (should already done by the action menu)
                                        Notifications.showToasterMessage $"Hörbuch {audiobook.FullName} vom Gerät entfernt"
                                        
                                    | Error e ->
                                        do! Notifications.showErrorMessage $"Error removing audiobook {audiobook.FullName} from database: {e}"
                                    
                                    return()
                            }

                        dispatch <| IsBusy false
                }


    let globalAudiobookStore =
        Program.mkAvaloniaProgrammWithSideEffect AudioBookElmish.init AudioBookElmish.update AudioBookElmish.SideEffects.runSideEffects
        |> Program.mkStore


module AudioBookItem =

    // State
    type State = {
        ViewModel: AudioBookItemViewModel
        Audiobook: AudioBook
        DownloadState: DownloadState
        ListenState: ListenState
        AudioFileInfos:AudioBookAudioFilesInfo option
        AmbientColor: string option
        IsPlaying: bool
        IsBusy: bool
    }

    and DownloadState =
        | NotDownloaded
        | Queued
        | Downloading of current:int * total:int
        | Downloaded


    and ListenState =
        | Unlistend
        | InProgress of AudioBookPosition
        | Listend


    type Msg =
        | RunOnlySideEffect of SideEffect

        | StartDownload
        | RemoveDownloadFromQueue
        | RemoveAudiobookFromDevice
        | MarkAudioBookAsListend
        | UnmarkAudioBookAsListend

        | OpenAudioBookPlayer of startPlaying:bool

        | ToggleAmbientColor

        | UpdateDownloadProgress of current:int * total:int
        | DownloadCompleted of WebAccess.Downloader.DownloadResult
        | UpdateAudioBookPosition of position:TimeSpan
        | UpdateCurrentListenFilename of filename:string
        | UpdateCurrentAudioFileList of audioFiles:AudioBookAudioFilesInfo
        | SetAmbientColor of color:string
        | SendPauseCommandToAudioPlayer
        
        | ToggleIsPlaying of pstate:bool
        
        | IsBusy of bool



    and [<RequireQualifiedAccess>]
        SideEffect =
        | None
        | Init
        | OpenAudioBookActionMenu
        | CloseAudioBookActionMenu
        | OpenAudioBookPlayer of startPlaying:bool

        | DeleteItemFromDb
        | ShowMetaData

        | StartDownload
        | DownloadCompleted
        | RemoveDownloadFromQueue
        | RemoveAudioBookFromDevice
        | MarkAsUnlistend
        | MarkAsListend
        | SendPauseCommandToAudioPlayer



        | UpdateDatabase





    let init viewmodel audiobook =
        {
            ViewModel = viewmodel
            Audiobook = audiobook
            DownloadState = if audiobook.State.Downloaded then Downloaded else NotDownloaded
            AmbientColor = None
            ListenState =
                match audiobook.State.Completed, audiobook.State.CurrentPosition with
                | true, _           -> Listend
                | false, Some pos   -> InProgress pos
                | false, None       -> Unlistend
            AudioFileInfos = DataBase.getAudioBookFileInfoTimeout 100 audiobook.Id // Todo: unbedingt ändern!
            IsPlaying = false
            IsBusy = false 
        }, SideEffect.Init



    let update msg state =
        match msg with
        | IsBusy b ->
            { state with IsBusy =  b }, SideEffect.None
            
        | RunOnlySideEffect sideEffect ->
            state, sideEffect

        | StartDownload ->
            { state with DownloadState = Queued }, SideEffect.StartDownload

        | RemoveDownloadFromQueue ->
            match state.DownloadState with
            | Downloading _ ->
                state, SideEffect.None
            | _ ->
                { state with DownloadState = NotDownloaded }, SideEffect.RemoveDownloadFromQueue

        | DownloadCompleted result ->
            let imageFullName = result.Images |> Option.map (_.Image)
            let thumbnail = result.Images |> Option.map (_.Thumbnail)
            let newModel = {
                state with
                    Audiobook.State.Downloaded = true
                    Audiobook.State.DownloadedFolder = Some result.TargetFolder
                    Audiobook.Picture = imageFullName
                    Audiobook.Thumbnail = thumbnail
                    DownloadState = Downloaded
            }
            newModel, SideEffect.DownloadCompleted

        | RemoveAudiobookFromDevice ->
            let newAudioBook = {
                state.Audiobook with
                    State.Downloaded = false
                    State.DownloadedFolder = None
            }
            let newState = { state with Audiobook = newAudioBook; DownloadState = NotDownloaded }
            newState, SideEffect.RemoveAudioBookFromDevice

        | MarkAudioBookAsListend ->
            match state.ListenState with
            | Listend ->
                state, SideEffect.None
            | InProgress _
            | Unlistend ->
                let newAudioBook = {
                    state.Audiobook with
                        State.Completed = true
                        State.CurrentPosition = None
                }
                let newModel = {state with Audiobook = newAudioBook; ListenState = Listend }
                newModel, SideEffect.MarkAsListend

        | UnmarkAudioBookAsListend ->
            match state.ListenState with
            | Unlistend ->
                state, SideEffect.None

            | InProgress _
            | Listend ->
                let newState = {
                    state.Audiobook.State
                        with
                            Completed = false
                            CurrentPosition = None
                }
                let newAudioBook = {state.Audiobook with State = newState;  }
                let newModel = {state with Audiobook = newAudioBook; ListenState = Unlistend }
                newModel, SideEffect.MarkAsUnlistend

        | UpdateDownloadProgress (current, total) ->
            { state with DownloadState = Downloading (current, total) }, SideEffect.None

        | OpenAudioBookPlayer startPlaying  ->
            state, SideEffect.OpenAudioBookPlayer startPlaying

        | UpdateAudioBookPosition pos ->
            match state.Audiobook.State.CurrentPosition with
            | Some p ->
                let position = { p with Position = pos }
                { state with
                    Audiobook.State.CurrentPosition = Some position
                    Audiobook.State.Completed = false // if you hear this audio book again, the complete flag is removed
                    ListenState = InProgress position
                    Audiobook.State.LastTimeListend = Some DateTime.Now
                }, SideEffect.UpdateDatabase
            | None ->
                // position can only set, if we have a current filename
                state, SideEffect.None

        | UpdateCurrentListenFilename filename ->
            match state.Audiobook.State.CurrentPosition with
            | Some p ->
                let position = { p with Filename =  filename }
                { state with
                    Audiobook.State.CurrentPosition = Some position
                    Audiobook.State.Completed = false // if you hear this audio book again, the complete flag is removed
                    ListenState = InProgress position
                    Audiobook.State.LastTimeListend = Some DateTime.Now
                }, SideEffect.UpdateDatabase
            | None ->
                // if we have no position, set it to 0
                { state with
                    Audiobook.State.CurrentPosition = Some { Filename =  filename; Position = TimeSpan.Zero }
                    ListenState = InProgress { Filename = filename; Position = TimeSpan.Zero }
                    Audiobook.State.LastTimeListend = Some DateTime.Now
                }, SideEffect.UpdateDatabase

        | SetAmbientColor color ->
            { state with AmbientColor = Some color }, SideEffect.None

        | ToggleAmbientColor ->
            // generate random 6 digit hex  string
            let random = System.Random()
            let color = $"#{random.Next(0x1000000):X6}"
            { state with AmbientColor = Some color }, SideEffect.None

        | UpdateCurrentAudioFileList audioFiles ->
            { state with AudioFileInfos = Some audioFiles }, SideEffect.None

        | ToggleIsPlaying pstate ->
            { state with IsPlaying = pstate }, SideEffect.None

        | SendPauseCommandToAudioPlayer ->
            { state with IsPlaying = false }, SideEffect.SendPauseCommandToAudioPlayer





    module SideEffects =

        [<AutoOpen>]
        module private Helpers =

            let openLoginForm () =
                Dispatcher.UIThread.Invoke<unit> (fun _ ->
                    let control = PerryRhodan.AudiobookPlayer.Views.LoginView()
                    let vm = DependencyService.Get<ILoginViewModel>()
                    control.DataContext <- vm
                    InteractiveContainer.ShowDialog (control, true)
                    let navigationService = DependencyService.Get<INavigationService>()
                    navigationService
                        .RegisterBackbuttonPressed (fun _ ->
                            InteractiveContainer.CloseDialog()
                            navigationService.ResetBackbuttonPressed()
                        )
                )



            let removeDownloadFromQueue (ab:AudioBook) =
                DownloadService.removeDownload <| DownloadService.DownloadInfo.Create ab


            let downloadAudiobook (audiobook:IAudioBookItemViewModel) dispatch =
                task {
                    match! SecureLoginStorage.loadCookie() with
                    | Error _ ->
                        InteractiveContainer.CloseDialog()
                        openLoginForm ()
                        dispatch <| RemoveDownloadFromQueue


                    | Ok _ ->

                        DownloadService.startService ()
                        let listenerName = $"AudioBook{audiobook.AudioBook.Id}Listener"
                        DownloadService.addInfoListener listenerName (fun info ->
                            async {

                                match info.State with
                                | DownloadService.Running (total,current) ->
                                    audiobook.SetUploadDownloadState (current,total)
                                | DownloadService.Open ->
                                    ()
                                | DownloadService.Finished (result, audiofileinfo) ->
                                    audiobook.SetDownloadCompleted result
                                    audiofileinfo |> Option.iter audiobook.SetAudioFileList

                                    DownloadService.removeInfoListener listenerName
                                | _ ->
                                    ()
                            }
                        )

                        DownloadService.registerErrorListener (fun (_,error) ->
                            async {
                                match error with
                                | ComError.SessionExpired _ ->
                                    DownloadService.shutDownService ()
                                    openLoginForm ()
                                    dispatch <| RemoveDownloadFromQueue
                                    DownloadService.removeInfoListener listenerName

                                | ComError.Other msg ->
                                    do! Notifications.showErrorMessage msg |> Async.AwaitTask
                                    DownloadService.removeInfoListener listenerName

                                | ComError.Network msg ->
                                    // the download service restarts network error automatically
                                    do! Notifications.showErrorMessage msg |> Async.AwaitTask
                                    ()
                                | ComError.Exception e ->
                                    let ex = e.GetBaseException()
                                    let msg = ex.Message + "|" + ex.StackTrace
                                    do! Notifications.showErrorMessage msg |> Async.AwaitTask
                                    DownloadService.removeInfoListener listenerName


                            }
                        )

                        DownloadService.addDownload <| DownloadService.DownloadInfo.Create audiobook.AudioBook

                        DownloadService.startDownloads ()

                        InteractiveContainer.CloseDialog()

                }


            let updateDatabase audiobook =
                task {
                    let! result =  DataBase.updateAudioBookInStateFile audiobook
                    match result with
                    | Ok _ ->
                        return ()
                    | Error e ->
                        do! Notifications.showErrorMessage e
                }


            let getAmbientColorFromPicture (picture:string) =
                let bitmap = SkiaSharp.SKBitmap.Decode picture
                if bitmap = null then
                    "#ff483d8b"
                else
                    let bitmap = bitmap.Resize(SkiaSharp.SKImageInfo(5,5, SKColorType.Rgb888x), SKSamplingOptions(SKFilterMode.Nearest))

                    let getHue (x,_,_) = x

                    let hues =
                        [
                            bitmap.GetPixel(0,1).ToHsv() |> getHue
                            bitmap.GetPixel(0,2).ToHsv() |> getHue
                            bitmap.GetPixel(0,3).ToHsv() |> getHue
                            bitmap.GetPixel(0,4).ToHsv() |> getHue
                            bitmap.GetPixel(1,4).ToHsv() |> getHue
                            bitmap.GetPixel(2,4).ToHsv() |> getHue
                            bitmap.GetPixel(3,4).ToHsv() |> getHue
                            bitmap.GetPixel(4,4).ToHsv() |> getHue
                            bitmap.GetPixel(4,3).ToHsv() |> getHue
                            bitmap.GetPixel(4,2).ToHsv() |> getHue
                            bitmap.GetPixel(4,1).ToHsv() |> getHue
                            bitmap.GetPixel(4,0).ToHsv() |> getHue
                            bitmap.GetPixel(3,0).ToHsv() |> getHue
                            bitmap.GetPixel(2,0).ToHsv() |> getHue
                            bitmap.GetPixel(3,3).ToHsv() |> getHue
                        ]

                    let avgeHue = hues |> List.average
                    let aboveAvg = hues |> List.filter (fun x -> x > avgeHue)
                    let belowAvg = hues |> List.filter (fun x -> x < avgeHue)
                    let avgHue =
                        if aboveAvg.Length > belowAvg.Length then
                            aboveAvg |> List.average
                        else
                            belowAvg |> List.average



                        //|> List.average

                    let avgHueColor = SkiaSharp.SKColor.FromHsv(avgHue, 100.0f, 50.0f)
                    avgHueColor.ToString()


        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            let navigationService = DependencyService.Get<INavigationService>()
            task {
                try
                    if sideEffect = SideEffect.None then
                        return ()
                    else
                        dispatch <| IsBusy true
                        do!
                            task {
                                match sideEffect with
                                | SideEffect.None ->
                                    return ()

                                | SideEffect.Init ->
                                    match state.Audiobook.Picture with
                                    | Some picture ->
                                        let color = getAmbientColorFromPicture picture
                                        dispatch <| SetAmbientColor color
                                    | None ->
                                        ()
                                    return ()

                                | SideEffect.OpenAudioBookActionMenu ->
                                    let menuService = DependencyService.Get<IActionMenuService>()
                                    menuService.ShowAudiobookActionMenu state.ViewModel

                                    navigationService.MemorizeBackbuttonCallback "PreviousToActionMenu"
                                    navigationService
                                        .RegisterBackbuttonPressed (fun _ ->
                                            InteractiveContainer.CloseDialog()
                                            navigationService.RestoreBackbuttonCallback "PreviousToActionMenu"
                                        )
                                    return ()

                                | SideEffect.CloseAudioBookActionMenu ->
                                    InteractiveContainer.CloseDialog()
                                    DependencyService.Get<INavigationService>().RestoreBackbuttonCallback "PreviousToActionMenu"
                                    return()

                                | SideEffect.OpenAudioBookPlayer startPlaying ->
                                    let mainViewModel = DependencyService.Get<IMainViewModel>()
                                    match mainViewModel.CurrentPlayerAudiobookViewModel with
                                    | Some current when current.AudioBook.Id = state.ViewModel.AudioBook.Id ->
                                        return () // do nothing
                                    | _ ->
                                        mainViewModel.OpenMiniplayer state.ViewModel startPlaying
                                        return()

                                | SideEffect.StartDownload ->
                                    do! downloadAudiobook state.ViewModel dispatch
                                    return()

                                | SideEffect.RemoveDownloadFromQueue ->
                                    removeDownloadFromQueue state.Audiobook
                                    Notifications.showToasterMessage "Download von Warteschlange entfernt"
                                    return()

                                | SideEffect.RemoveAudioBookFromDevice ->
                                    AudioBookStore.globalAudiobookStore.Dispatch <| AudioBookStore.AudioBookElmish.RemoveAudiobookFromDevice state.Audiobook
                                    do! updateDatabase state.Audiobook
                                    DependencyService.Get<IMainViewModel>().CloseMiniplayer()
                                    return()

                                | SideEffect.MarkAsUnlistend ->
                                    do! updateDatabase state.Audiobook

                                    return()

                                | SideEffect.MarkAsListend ->
                                    do! updateDatabase state.Audiobook

                                    return()

                                | SideEffect.DeleteItemFromDb ->
                                    // update the audio file list, to trigger update of other views
                                    AudioBookStore.globalAudiobookStore.Dispatch <| AudioBookStore.AudioBookElmish.DeleteAudiobookFromDatabase state.Audiobook
                                    return()

                                | SideEffect.ShowMetaData ->
                                    do! Notifications.showMessage "Metadata" (state.Audiobook.ToString())
                                    return()

                                | SideEffect.UpdateDatabase ->
                                    do! updateDatabase state.Audiobook
                                    return ()

                                | SideEffect.DownloadCompleted ->
                                    do! updateDatabase state.Audiobook
                                    // refresh ambient color
                                    match state.Audiobook.Picture with
                                    | Some picture ->
                                        let color = getAmbientColorFromPicture picture
                                        dispatch <| SetAmbientColor color
                                    | None ->
                                        ()
                                    // update the audio file list, to trigger update of other views
                                    AudioBookStore.globalAudiobookStore.Dispatch <| AudioBookStore.AudioBookElmish.AudioBookChanged

                                | SideEffect.SendPauseCommandToAudioPlayer ->
                                    DependencyService.Get<IAudioPlayerPause>().Pause()
                            }
                
                        dispatch <| IsBusy true
                
                with
                | ex ->
                    Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                    dispatch <| IsBusy true
                    raise ex




            }






module private Draw =

    open global.SkiaSharp
    open global.Avalonia.Skia

    let calcAlpha factor =
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
        let center = SKPoint((width |> f32) / 2.0f, (height |> f32) / 2.0f);
        let explodeOffset = 0;
        let margin = 20;
        let radius = (Math.Min(width / 2, height  / 2) - (2 * explodeOffset + 2 * margin)) |> f32
        let rect = SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

        let drawPie transparent (color:SKColor) startAngle sweepAngle =
            use path = new SKPath()
            use fillPaint = new SKPaint()
            use outlinePaint = new SKPaint()


            path.MoveTo(center);
            path.ArcTo(rect, startAngle, sweepAngle, false);
            path.Close();

            let alphaColor = color.WithAlpha(calcAlpha factor)

            fillPaint.Style <- SKPaintStyle.Fill
            fillPaint.Color <- alphaColor
            fillPaint.BlendMode <- SKBlendMode.Plus
            fillPaint.IsAntialias <- false

            outlinePaint.Style <- SKPaintStyle.Stroke;
            outlinePaint.StrokeWidth <- 1.0f;
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
        textPaint.Color <- SKColor.Parse("FFFFFF")
        textPaint.BlendMode <- SKBlendMode.Plus
        textPaint.IsAntialias <- false
        
        use font = new SKFont()
        font.Size <- 75.0f
        
        let text = $"{((factor * 100.0) |> int)} %%"
        let y = ((height  / 2) |> f32) + (font.Size / 2.0f) - 15.0f
        
        
        canvas.DrawText(text, (width / 2) |> f32, y ,SKTextAlign.Center, font, textPaint)

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



module private Helpers =
    let getProgressAndTotalDuration (state:State) =
        if state.DownloadState <> Downloaded then
            None
        else
            match state.AudioFileInfos, state.Audiobook.State.CurrentPosition with
            | Some fileInfo, Some position ->

                let audioBookDuration =
                    fileInfo.AudioFiles
                    |> List.map (_.Duration)
                    |> List.fold (fun state t -> state + t) TimeSpan.Zero

                let trackIndex =
                    fileInfo.AudioFiles
                    |> List.tryFindIndex (fun i -> i.FileName = position.Filename)
                    |> Option.defaultValue 0

                let durationUntilCurrentTrack =
                    fileInfo.AudioFiles
                    |> List.take trackIndex
                    |> List.map (_.Duration)
                    |> List.fold (fun state t -> state + t) TimeSpan.Zero

                let currentTimeInMs = position.Position + durationUntilCurrentTrack

                Some {| CurrentProgress = currentTimeInMs; TotalDuration = audioBookDuration; Rest = audioBookDuration - currentTimeInMs |}
            | _ ->
                None


module private DemoData =
    let designAudioBook = {
            Id = 1
            FullName = "Perry Rhodan 3000 - Mythos Erde"
            EpisodeNo = Some 3000
            EpisodenTitel = "Mythos Erde"
            Group = "Perry Rhodan"
            Picture = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            Thumbnail = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
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


type AudioBookItemViewModel(audiobook: AudioBook) as self =
    inherit ReactiveElmishViewModel()

    let init () =
        init self audiobook

    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore

    
    
    interface IAudioBookItemViewModel with
        member this.SetUploadDownloadState newState = local.Dispatch (UpdateDownloadProgress newState)
        member this.SetDownloadCompleted result     = local.Dispatch (DownloadCompleted result)
        member this.AudioBook                       = this.AudioBook
        member this.SetAudioFileList audioFiles     = local.Dispatch (UpdateCurrentAudioFileList audioFiles)

    member this.AudioBook       = this.Bind(local, _.Audiobook)
    member this.DownloadState   = this.Bind(local, _.DownloadState)
    member this.IsDownloaded    = this.Bind(local, fun s -> s.DownloadState = Downloaded)
    member this.IsQueued        = this.Bind(local, fun s -> s.DownloadState = Queued)
    member this.IsDownloading   = this.Bind(local, fun s -> match s.DownloadState with | Downloading _ -> true | _ -> false)
    member this.IsNotDownloaded = this.Bind(local, fun s -> s.DownloadState = NotDownloaded)
    member this.IsComplete      = this.Bind(local, fun s -> s.ListenState = Listend)
    member this.IsPlaying       = this.Bind(local, _.IsPlaying)
    member this.IsPlayButtonVisible = this.Bind(local, fun s ->  s.DownloadState = Downloaded && not s.IsPlaying)
    member this.ListenState     = this.Bind(local, _.ListenState)
    member this.AmbientColor    = this.BindOnChanged(local, _.AmbientColor, (fun i ->
        let ac = i.AmbientColor |> Option.defaultValue "#ff483d8b"
        Color.Parse ac
        ))
    
    member this.IconColor      = this.BindOnChanged(local, _.AmbientColor, (fun i ->
        i.AmbientColor
        |> Option.map ColorHelpers.invertHexColor
        |> Option.defaultValue "#ffffff"
        |> SolidColorBrush.Parse
        ))

    member this.OpenDialog()                = local.Dispatch <| RunOnlySideEffect SideEffect.OpenAudioBookActionMenu
    member this.CloseDialog()               = local.Dispatch <| RunOnlySideEffect SideEffect.CloseAudioBookActionMenu
    member this.StartDownload()             = local.Dispatch StartDownload
    member this.RemoveDownload()            = local.Dispatch RemoveDownloadFromQueue
    member this.RemoveAudiobookFromDevice() = local.Dispatch RemoveAudiobookFromDevice
    member this.MarkAsListend()             = local.Dispatch MarkAudioBookAsListend
    member this.MarkAsUnlistend()           = local.Dispatch UnmarkAudioBookAsListend
    member this.OpenPlayerAndPlay()         = local.Dispatch <| OpenAudioBookPlayer true
    member this.OpenPlayer()                = if this.IsDownloaded then local.Dispatch <| OpenAudioBookPlayer false
    member this.DeleteItemFromDb()          = local.Dispatch <| RunOnlySideEffect SideEffect.DeleteItemFromDb
    member this.ShowMetaData()              = local.Dispatch <| RunOnlySideEffect SideEffect.ShowMetaData
    member this.UpdateAudioBookPosition pos = local.Dispatch (UpdateAudioBookPosition pos)
    member this.UpdateCurrentListenFilename filename = local.Dispatch (UpdateCurrentListenFilename filename)
    member this.ToggleAmbientColor()        = local.Dispatch (ToggleAmbientColor)
    member this.ToggleIsPlaying pstate      = local.Dispatch (ToggleIsPlaying pstate)
    member this.PauseAudiobook()            = local.Dispatch SendPauseCommandToAudioPlayer


    member this.StartDownloadVisible    = this.Bind(local, fun s -> s.DownloadState = NotDownloaded)
    member this.RemoveDownloadVisible   = this.Bind(local, fun s -> s.DownloadState = Queued)
    member this.RemoveAudiobookFromDeviceVisible = this.Bind(local, fun s -> s.DownloadState = Downloaded)
    member this.MarkAsListendVisible    = this.Bind(local, fun s -> s.ListenState <> ListenState.Listend)
    member this.MarkAsUnlistendVisible  = this.Bind(local, fun s -> s.ListenState = ListenState.Listend)
    member this.OpenPlayerVisible       = this.Bind(local, fun s -> s.DownloadState = Downloaded)


    member this.Title = this.Bind(local, _.Audiobook.FullName)
    member this.EpisodeTitle = this.Bind(local, _.Audiobook.EpisodenTitel)

    member this.Thumbnail =
        this.BindOnChanged(local,
            _.Audiobook.Thumbnail,
              fun s ->
                  s.Audiobook.Thumbnail
                  |> Option.defaultValue "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
        )

    member this.LoadingPie = this.BindOnChanged(local, _.DownloadState, fun s ->
        match s.DownloadState with
        | Downloading (current, total) -> Draw.loadingPie (float current / float total)
        | _ -> Unchecked.defaultof<Bitmap>
    )

    member this.ListendenProgress = this.BindOnChanged(local, _.ListenState, fun s ->
        match s.ListenState with
        | InProgress pos ->
            s
            |> Helpers.getProgressAndTotalDuration
            |> Option.map (fun x -> x.CurrentProgress / x.TotalDuration * 100.)
            |> Option.defaultValue 0.
        | _ -> 0.
    )



    static member DesignVM = new AudioBookItemViewModel(DemoData.designAudioBook)
