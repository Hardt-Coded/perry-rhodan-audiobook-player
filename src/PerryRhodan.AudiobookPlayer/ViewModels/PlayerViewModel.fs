namespace PerryRhodan.AudiobookPlayer.ViewModels


open System
open System.IO
open System.Reflection
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Dependencies
open Domain
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer.Services
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer.PlayerElmish
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open ReactiveElmish

module PlayerPage =

    open Services


    type State = {
        AudioBook: AudioBookItemViewModel
        AudioFileList: (string * TimeSpan) list
        CurrentAudioFile: string
        CurrentAudioFileIndex: int
        CurrentPosition: TimeSpan
        CurrentDuration: TimeSpan
        CurrentState: AudioPlayerState
        PlaybackSpeed: decimal


        AudioPlayerBusy: bool
        HasPlayedBeforeDragSlider: bool
        SliderIsDragging: bool
        StartPlayingOnOpen: bool
        IsLoading: bool
        SelectedSleepTime: TimeSpan option
        TimeUntilSleeps: TimeSpan option
    }


    let disposables = new System.Collections.Generic.List<IDisposable>()

    type Msg =
        | PlayerControlMsg of PlayerControlMsg
        | ButtonActionMsg of ButtonActionMsg

        | RunOnlySideEffect of SideEffect

        | RestoreStateFormAudioService of PlayerElmish.AudioPlayerInfo
        | FileListLoaded of (string * TimeSpan) list

        | SetTrackPosition of TimeSpan
        | SetDragging of bool
        | UpdateScreenInformation of info: PlayerElmish.State

        | SetBusy of bool

    and PlayerControlMsg =
        | Play of force:bool
        | PlayWithoutRewind
        | Stop

    and ButtonActionMsg =
        | SetSleepTimer of TimeSpan option
        | SetPlaybackSpeed of decimal



    and [<RequireQualifiedAccess>]
        SideEffect =
        | None
        | InitAudioPlayer

        | SliderDragStarted

        | StartAudioBookService of fileList: (string * TimeSpan) list

        | Play of force:bool
        | PlayWithoutRewind
        | Stop
        | Next
        | Previous
        | JumpForward
        | JumpBackwards
        | StartSleepTimer of TimeSpan option
        | SetPlaybackSpeed of decimal
        | SetAudioPositionAbsolute of pos: TimeSpan
        | ContinuePlayingAfterSlide of pos: TimeSpan

        | UpdatePositionOnDatabase

        | ClosePlayerPage





    let initModel startPlaying audioBook = {
        AudioBook = audioBook
        CurrentAudioFile = ""
        CurrentAudioFileIndex = 0
        CurrentPosition = TimeSpan.Zero
        CurrentDuration = TimeSpan.Zero
        CurrentState = AudioPlayerState.Stopped
        AudioFileList = []
        IsLoading = false
        AudioPlayerBusy = false
        HasPlayedBeforeDragSlider = false
        SliderIsDragging = false
        StartPlayingOnOpen = startPlaying
        PlaybackSpeed = 1.0m
        SelectedSleepTime = None
        TimeUntilSleeps = Some <| TimeSpan.FromMinutes 130.0
    }




    let init audioBook startPlaying =
        let model = audioBook |> initModel startPlaying
        model, SideEffect.InitAudioPlayer



    let rec update msg state =
        match msg with
        | RunOnlySideEffect sideEffect ->
            state, sideEffect

        | PlayerControlMsg (Play force) ->
            let state = state |> fixModelWhenCurrentTrackGreaterThanNumberOfTracks

            {
                state with
                    //CurrentState = AudioPlayerState.Playing
                    CurrentAudioFileIndex =
                        if state.CurrentAudioFileIndex < 0 then
                            0
                        else
                            state.CurrentAudioFileIndex
            },
            SideEffect.Play force

        | PlayerControlMsg PlayWithoutRewind ->
            let state = state |> fixModelWhenCurrentTrackGreaterThanNumberOfTracks
            state, SideEffect.PlayWithoutRewind

        | PlayerControlMsg Stop -> state, SideEffect.Stop


        | ButtonActionMsg (SetSleepTimer sleepTime) ->
            { state with SelectedSleepTime = sleepTime }, SideEffect.StartSleepTimer sleepTime

        | ButtonActionMsg (SetPlaybackSpeed speed) ->
            { state with PlaybackSpeed = speed }, SideEffect.SetPlaybackSpeed speed



        | RestoreStateFormAudioService info ->
            let newModel =
                info.AudioBook
                |> Option.map (fun ab -> {
                    state with
                        CurrentAudioFile = info.Filename
                        CurrentAudioFileIndex = info.CurrentFileIndex
                        CurrentDuration = info.Duration
                        CurrentPosition = info.Position
                        AudioFileList = info.Mp3FileList
                        AudioBook = ab
                })
                |> Option.defaultValue state

            newModel, SideEffect.None

        | FileListLoaded fileList ->
            let sideEffect = SideEffect.StartAudioBookService fileList

            let beginNew () =
                let file, duration = fileList[0]
                let currentDuration = duration

                {
                    state with
                        AudioFileList = fileList
                        CurrentDuration = currentDuration
                        CurrentAudioFile = file
                        CurrentAudioFileIndex = 0
                        CurrentPosition = TimeSpan.Zero
                },
                sideEffect

            match state.AudioBook.AudioBook.State.CurrentPosition with
            | None ->
                beginNew ()

            | Some cp ->
                let lastListenFile =
                    fileList |> List.indexed |> List.tryFind (fun (_, (fn, _)) -> fn = cp.Filename)

                match lastListenFile with
                | None -> beginNew ()
                | Some(idx, (fn, duration)) ->
                    let currentPosition = cp.Position
                    let currentDuration = duration

                    {
                        state with
                            AudioFileList = fileList
                            CurrentPosition = currentPosition
                            CurrentDuration = currentDuration
                            CurrentAudioFile = fn
                            CurrentAudioFileIndex = idx
                    },
                    sideEffect


        | SetBusy bstate ->
            { state with IsLoading = bstate }, SideEffect.None

        | SetDragging b ->
            // slider released
            let sideEffect =
                if b then
                    SideEffect.SliderDragStarted
                elif state.SliderIsDragging && b = false && state.HasPlayedBeforeDragSlider then
                    SideEffect.ContinuePlayingAfterSlide state.CurrentPosition
                elif state.SliderIsDragging && b = false then
                    SideEffect.SetAudioPositionAbsolute state.CurrentPosition
                else
                    SideEffect.None

            {
                state with
                    SliderIsDragging = b
                    HasPlayedBeforeDragSlider = b && state.CurrentState = AudioPlayerState.Playing
            },
            sideEffect

        | SetTrackPosition pos ->
            { state with CurrentPosition = pos }, SideEffect.None

        | UpdateScreenInformation info ->
            // always update the time until sleeps and audioplayer state
            let state = {
                state with
                    TimeUntilSleeps =
                        info.SleepTimerState
                        |> Option.map (_.SleepTimerCurrentTime)
                    SelectedSleepTime =
                        // reset selected sleep time if the sleep timer has stopped
                        info.SleepTimerState |> Option.bind (fun _ -> state.SelectedSleepTime)
                    CurrentState = info.State

            }

            if state.SliderIsDragging then
                state, SideEffect.None
            else
                {
                    state with
                        CurrentPosition = info.Position
                        CurrentDuration = info.Duration
                        CurrentState = info.State
                        CurrentAudioFile = info.Filename
                        CurrentAudioFileIndex = info.CurrentFileIndex
                        IsLoading = info.IsBusy
                },
                SideEffect.None
            //else
            //    state, SideEffect.None




    // repair model, if current track is greater than the actually count of files
    // it can sometime happen. this has to leave inside until the bug is found
    and fixModelWhenCurrentTrackGreaterThanNumberOfTracks model =
        if model.CurrentAudioFileIndex > (model.AudioFileList.Length - 1) then
            {
                model with
                    CurrentAudioFileIndex = model.AudioFileList.Length - 1
            }
        else
            model





    module SideEffects =


        let private rewindInSec state =
            let globalSettings = DependencyService.Get<GlobalSettingsService>()
            match state.AudioBook.AudioBook.State.LastTimeListend with
            | Some lastTimeListend ->

                let minutesSinceLastListend =
                    DateTime.UtcNow
                        .Subtract(lastTimeListend)
                        .TotalMinutes

                let secondsSinceLastListend =
                    DateTime.UtcNow
                        .Subtract(lastTimeListend)
                        .TotalSeconds

                if (secondsSinceLastListend <= 30.0) then
                    0
                else
                    let longTimeMinutes = globalSettings.LongPeriodBeginsAfterInMinutes

                    if (minutesSinceLastListend >= longTimeMinutes) then
                        globalSettings.RewindWhenStartAfterLongPeriodInSec
                    else
                        globalSettings.RewindWhenStartAfterShortPeriodInSec
            | _ -> 0


        let private loadFiles (shop:Shop) (model: AudioBook) =
            task {
                match model.State.DownloadedFolder with
                | None -> return None
                | Some folder ->
                    try
                        let! audioFileInfo =
                            match shop with
                            | OldShop ->
                                OldShopDatabase.storageProcessor.GetAudioBookFileInfo model.Id
                            | NewShop ->
                                NewShopDatabase.storageProcessor.GetAudioBookFileInfo model.Id

                        match audioFileInfo with
                        | None ->
                            try
                                let files =
                                    Directory.EnumerateFiles(folder, "*.mp3")
                                    |> Seq.toArray
                                    |> Array.Parallel.map (fun i ->
                                        use tfile = TagLib.File.Create(i)
                                        (i, tfile.Properties.Duration)
                                    )
                                    |> Array.sortBy (fun (f, _) -> f)
                                    |> Array.toList

                                return files |> Some
                            with ex ->
                                do!
                                    Notifications.showErrorMessage
                                        "Konnte Hörbuch Dateien nicht finden."

                                Global.telemetryClient.TrackException(ex)
                                return None

                        | Some audioFileInfo ->
                            return
                                audioFileInfo.AudioFiles
                                |> List.map (fun i -> (i.FileName, i.Duration))
                                |> Some
                    with
                    | ex ->
                        Global.telemetryClient.TrackException ex
                        return raise ex

            }

        let runSideEffects (sideEffect: SideEffect) (state: State) (dispatch: Msg -> unit) =
            task {
                if sideEffect = SideEffect.None then
                    return ()
                 else

                    // somehow there is an null ref exception on the audioplayer, so make sure, if this happen, to reregister the service
                    let audioPlayer =
                        let ab = DependencyService.Get<IAudioPlayer>()
                        if ab = Unchecked.defaultof<_> then
                            // register the audioplayer
                            let audioService = new AudioPlayerService()
                            DependencyService.ServiceCollection
                                .AddSingleton<IAudioPlayer>(audioService)
                                .AddSingleton<IAudioPlayerPause>(audioService)
                            |> ignore
                            DependencyService.SetComplete()
                            audioService :> IAudioPlayer
                        else
                            ab
                            
                    let globalSettings = DependencyService.Get<GlobalSettingsService>()
                    dispatch <| SetBusy true
                    do!
                        task {
                            match sideEffect with
                            | SideEffect.None -> return ()

                            | SideEffect.InitAudioPlayer ->
                                // check if the global audio player is active and already an audiobook is loaded
                                match audioPlayer.AudioPlayerInformation.AudioBook with
                                // when the user tapped on the already active audiobook
                                | Some audioBook when (audioBook.AudioBook.Id = state.AudioBook.AudioBook.Id) ->
                                    dispatch <| RestoreStateFormAudioService audioPlayer.AudioPlayerInformation

                                    if
                                        state.StartPlayingOnOpen
                                        && audioPlayer.AudioPlayerInformation.State = AudioPlayerState.Stopped
                                    then
                                        dispatch <| PlayerControlMsg (Play true)

                                // the user tapped on a different audiobook
                                | Some infoAudioBook ->
                                    let! files = state.AudioBook.AudioBook |> loadFiles state.AudioBook.Shop

                                    match files with
                                    | None ->
                                        dispatch <| RunOnlySideEffect SideEffect.ClosePlayerPage
                                        ()
                                    | Some files ->
                                        dispatch <| FileListLoaded files // initialized the audioplayer with the new files

                                | _ ->
                                    // there is no current state in the store, so load the files and connect to the service
                                    let! files = state.AudioBook.AudioBook |> loadFiles state.AudioBook.Shop

                                    match files with
                                    | None ->
                                        dispatch <| RunOnlySideEffect SideEffect.ClosePlayerPage
                                        ()
                                    | Some files ->
                                        dispatch <| FileListLoaded files // initialized the audioplayer with the new files



                                dispatch <| ButtonActionMsg (SetPlaybackSpeed globalSettings.PlaybackSpeed)

                                return ()

                            | SideEffect.Play force ->
                                match state.AudioFileList with
                                | [] -> do! Notifications.showErrorMessage "Keine Dateien gefunden."
                                | _ ->
                                    let file, _ = state.AudioFileList[state.CurrentAudioFileIndex]
                                    let currentPosition = state.CurrentPosition
                                    let rewindInSec = rewindInSec state |> int64 |> TimeSpan.FromSeconds

                                    let newPosition =
                                        let p = currentPosition - rewindInSec

                                        if p < TimeSpan.Zero then
                                            TimeSpan.Zero
                                        else
                                            p

                                    do! audioPlayer.PlayExtern file newPosition force

                            | SideEffect.PlayWithoutRewind ->
                                match state.AudioFileList with
                                | [] -> do! Notifications.showErrorMessage "Keine Dateien gefunden."
                                | _ ->
                                    let file, _ = state.AudioFileList[state.CurrentAudioFileIndex]
                                    do! audioPlayer.PlayExtern file state.CurrentPosition false

                                    return ()

                            | SideEffect.Stop ->
                                do! audioPlayer.Stop false
                                return ()

                            | SideEffect.Next ->
                                do! audioPlayer.Next()
                                return ()

                            | SideEffect.Previous ->
                                do! audioPlayer.Previous()
                                return ()

                            | SideEffect.JumpForward ->
                                do! audioPlayer.JumpForward()
                                return ()

                            | SideEffect.JumpBackwards ->
                                do! audioPlayer.JumpBackwards()
                                return ()

                            | SideEffect.StartAudioBookService fileList ->
                                do! audioPlayer.Stop false
                                do! audioPlayer.Init state.AudioBook fileList

                                disposables |> Seq.iter (_.Dispose())
                                disposables.Add
                                    <| audioPlayer.AudioPlayerInfoChanged.Subscribe(fun info ->
                                        //if info.State = AudioPlayerState.Playing then
                                            dispatch <| UpdateScreenInformation info
                                    )

                                // update the screen information
                                dispatch <| UpdateScreenInformation audioPlayer.AudioPlayerInformation

                                if state.StartPlayingOnOpen then
                                    dispatch <| PlayerControlMsg (Play true)

                                return ()

                            | SideEffect.UpdatePositionOnDatabase ->
                                // Todo?
                                return ()

                            | SideEffect.StartSleepTimer sleepTime ->
                                do! audioPlayer.StartSleepTimer sleepTime
                                return ()

                            | SideEffect.SetPlaybackSpeed speed ->
                                do! audioPlayer.SetPlaybackSpeed speed
                                globalSettings.PlaybackSpeed <- speed

                            | SideEffect.SetAudioPositionAbsolute pos ->
                                do! audioPlayer.SeekTo pos
                                return ()

                            | SideEffect.ContinuePlayingAfterSlide pos ->
                                do! audioPlayer.SeekTo pos
                                dispatch <| PlayerControlMsg PlayWithoutRewind
                                return ()

                            | SideEffect.SliderDragStarted ->
                                do! audioPlayer.Stop false
                                return ()

                            | SideEffect.ClosePlayerPage ->
                                do! audioPlayer.Stop false

                                DependencyService
                                    .Get<IMainViewModel>()
                                    .GotoHomePage()

                                return ()



                        }

                    dispatch <| SetBusy false

            }








    let getPositionAudioBookTotal (model: State) =
        if model.AudioFileList = [] || model.CurrentAudioFileIndex < 0 then
            {|
                Total = TimeSpan.Zero
                CurrentPos = TimeSpan.Zero
                Rest = TimeSpan.Zero
            |}
        else
            let totalDuration =
                model.AudioFileList
                |> List.map snd
                |> List.fold (fun sum t -> sum + t) TimeSpan.Zero

            let durationUntilCurrentTrack =
                model.AudioFileList
                |> List.take model.CurrentAudioFileIndex
                |> List.map snd
                |> List.fold (fun sum t -> sum + t) TimeSpan.Zero

            let currentTime = model.CurrentPosition + durationUntilCurrentTrack

            {|
                Total = totalDuration
                CurrentPos = currentTime
                Rest = totalDuration - currentTime
            |}


    let getMinutesAndSeconds (ts: TimeSpan) =
        let minutes = Math.Floor(ts.TotalMinutes) |> int
        minutes, ts.Seconds

    let getHoursAndMinutes (ts: TimeSpan) =
        let minutes = Math.Floor(ts.TotalHours) |> int
        minutes, ts.Minutes


open PlayerPage
open ReactiveElmish.Avalonia
open Elmish.SideEffect
open Elmish


type PlayerViewModel(audiobook: AudioBookItemViewModel, startPlaying) =
    inherit ReactiveElmishViewModel()

    let init () =
        init audiobook startPlaying

    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        #if DEBUG
        |> Program.withTrace(fun msg state _ ->
            System.Diagnostics.Trace.WriteLine($"PlayerViewModel: \r\n Msg: \r\n {msg} \r\n State: \r\n {({ state with AudioFileList = [] })}")
        )
        #endif
        |> Program.mkStore


    member this.IsLoading = this.Bind(local, _.IsLoading)

    member this.AudioBook = this.Bind(local, _.AudioBook)

    member this.Picture =
        this.BindOnChanged(
            local,
            _.AudioBook.AudioBook.Picture,
            fun s ->
                s.AudioBook.AudioBook.Picture
                |> Option.defaultValue
                    "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
        )

    member this.CurrentTrackNumberString =
        this.BindOnChanged(local, _.CurrentAudioFileIndex, (fun s -> $"Track: {s.CurrentAudioFileIndex + 1}"))

    member this.CurrentPositionMs
        with get () =
            this.Bind(local, fun i -> i.CurrentPosition.TotalMilliseconds)
        and set v = local.Dispatch(SetTrackPosition(TimeSpan.FromMilliseconds v))


    member this.SliderValue
        with get () =
            this.Bind(local, _.CurrentPosition.TotalSeconds)
        and set v =
            local.Dispatch(SetTrackPosition(TimeSpan.FromMilliseconds v * 1000.0))
            #if DEBUG
            System.Diagnostics.Trace.WriteLine($"PlayerViewModel: SliderValue: {v}")  |> ignore
            #endif


    member this.SliderMax =
        this.BindOnChanged(local, _.CurrentDuration.TotalSeconds, _.CurrentDuration.TotalSeconds)

    member this.CurrentDurationMs =
        this.BindOnChanged(local, _.CurrentDuration.TotalMilliseconds, _.CurrentDuration.TotalMilliseconds)

    member this.TimeUntilSleeps =
        this.BindOnChanged(local, _.TimeUntilSleeps, fun x ->
            x.TimeUntilSleeps
            |> Option.map (fun time ->
                let minutes, seconds = getMinutesAndSeconds time
                $"{time.Minutes:``##0``}:{time.Seconds:``00``}"
            )
            |> Option.defaultValue ""
        )

    member this.SleepClockVisible =
        this.BindOnChanged(local, _.SelectedSleepTime, fun x ->
            x.SelectedSleepTime |> Option.isSome
        )

    member this.SliderIsDragging
        with get () = this.BindOnChanged(local, _.SliderIsDragging, _.SliderIsDragging)
        and set v = local.Dispatch(SetDragging v)

    member this.TotalPositionString =
        this.BindOnChanged(
            local,
            (fun s -> s.CurrentPosition,s.CurrentAudioFileIndex),
            fun s ->
                let pos = getPositionAudioBookTotal s
                $"Gesamt: {pos.CurrentPos:``hh\:mm\:ss``} / {pos.Total:``hh\:mm\:ss``}"
        )

    member this.PositionString =
        this.BindOnChanged(
            local,
            _.CurrentPosition,
            fun s -> $"{s.CurrentPosition:``hh\:mm\:ss``}/{s.CurrentDuration:``hh\:mm\:ss``}"
        )

    member this.SleepTimerValues =
        [|
            (None, "Aus")
            (Some <| TimeSpan.FromMinutes 5.0, "5 Minuten")
            (Some <| TimeSpan.FromMinutes 10.0, "10 Minuten")
            (Some <| TimeSpan.FromMinutes 15.0, "15 Minuten")
            (Some <| TimeSpan.FromMinutes 20.0, "20 Minuten")
            (Some <| TimeSpan.FromMinutes 30.0, "30 Minuten")
            (Some <| TimeSpan.FromMinutes 45.0, "45 Minuten")
            (Some <| TimeSpan.FromMinutes 60.0, "60 Minuten")
            (Some <| TimeSpan.FromMinutes 90.0, "90 Minuten")
            (Some <| TimeSpan.FromMinutes 120.0, "120 Minuten")
            (Some <| TimeSpan.FromMinutes 150.0, "150 Minuten")
            (Some <| TimeSpan.FromMinutes 180.0, "180 Minuten")
            (Some <| TimeSpan.FromMinutes 210.0, "210 Minuten")

        |]

    member this.SleepTimer
        with get() = this.BindOnChanged(local, _.SelectedSleepTime, _.SelectedSleepTime)
        and set v = local.Dispatch  (ButtonActionMsg <| SetSleepTimer v)

    member this.SleepTimerTextColor =
        let ambientColor = this.AudioBook.AmbientColor |> Common.ColorHelpers.convertColorToHexString
        let timerColor = Common.ColorHelpers.invertHexColor ambientColor
        Avalonia.Media.Color.Parse timerColor


    member this.PlaybackSpeeds =
        [|
            for i in 0.1m .. 0.1m .. 6.0m do
                i, $"{i:``0.0x``}"
        |]

    member this.PlaybackSpeed
        with get () = this.BindOnChanged (local, _.PlaybackSpeed, _.PlaybackSpeed)
        and set v = local.Dispatch (ButtonActionMsg <| SetPlaybackSpeed v)


    member this.JumpDistance =
        this.Bind(local, (fun i ->
            let globalSettings = DependencyService.Get<GlobalSettingsService>()
            if globalSettings <> Unchecked.defaultof<_> then
                globalSettings.JumpDistance / 1000
            else
                30
        ))


    member this.Play() =
        local.Dispatch <| PlayerControlMsg (Play false)

    member this.IsPlaying =
        this.BindOnChanged(local, _.CurrentState, (fun i -> i.CurrentState = AudioPlayerState.Playing))

    member this.PlayWithoutRewind() =
        local.Dispatch <| PlayerControlMsg PlayWithoutRewind

    member this.Stop() =
        local.Dispatch <| PlayerControlMsg Stop

    member this.IsStopped =
        this.BindOnChanged(local, _.CurrentState, (fun i -> i.CurrentState = AudioPlayerState.Stopped))

    member this.Next() =
        local.Dispatch <| RunOnlySideEffect SideEffect.Next

    member this.Previous() =
        local.Dispatch <| RunOnlySideEffect SideEffect.Previous

    member this.JumpForward() =
        local.Dispatch <| RunOnlySideEffect SideEffect.JumpForward

    member this.JumpBackwards() =
        local.Dispatch <| RunOnlySideEffect SideEffect.JumpBackwards

    member this.OnDraggingChanged b =
        local.Dispatch(SetDragging b)

    member this.OpenMainPlayerPage() =
        let mainViewModel = DependencyService.Get<IMainViewModel>()
        mainViewModel.GotoPlayerPage audiobook false


    member this.GoBackHome() =
        DependencyService.Get<IMainViewModel>().GotoHomePage()

    static member DesignVM =
        let vm = new PlayerViewModel(AudioBookItemViewModel.DesignVM, false)
        vm.SleepTimer <- Some <| TimeSpan.FromMinutes 10.0
        vm



module PlayerViewModelStore =
    let mutable private viewmodel: PlayerViewModel option = None

    /// creates only a new viewmodel if the audiobook is different
    let create (audiobook: AudioBookItemViewModel) startPlaying =
        match viewmodel with
        | Some viewmodel when viewmodel.AudioBook.AudioBook.Id = audiobook.AudioBook.Id ->
            if startPlaying then
                viewmodel.Play()

            viewmodel
        | _ ->
            viewmodel |> Option.iter (fun i -> (i :> IDisposable).Dispose())
            let vm = new PlayerViewModel(audiobook, startPlaying)
            viewmodel <- Some vm
            vm


    let getCurrentPlayerViewModel () =
        viewmodel

