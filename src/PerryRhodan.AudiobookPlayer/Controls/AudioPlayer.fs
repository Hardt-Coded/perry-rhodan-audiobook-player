namespace PerryRhodan.AudiobookPlayer.Services.AudioPlayer



open System.Threading
open Dependencies
open Domain
open System
open FSharp.Control
open MediaManager
open MediaManager.Library
open MediaManager.Media
open MediaManager.Player
open PerryRhodan.AudiobookPlayer.Services
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.ViewModels
open SkiaSharp


module PlayerElmish =

    [<RequireQualifiedAccess>]
    type AudioPlayerServiceState =
        | Stopped
        | Started


    type Mp3FileList = (string * TimeSpan) list

    type AudioPlayerInfo = {
        Filename: string
        Position: TimeSpan
        Duration: TimeSpan
        CurrentFileIndex: int // starts with zero
        State: AudioPlayerState
        AudioBook: AudioBookItemViewModel option
        Mp3FileList: Mp3FileList
        PlaybackDelayed: bool
        ResumeOnAudioFocus: bool
        TimeUntilSleep: TimeSpan option
        PlaybackSpeed: float
        IsBusy: bool
        SleepTimerState: SleepTimerState option
    } with

        member this.NumOfTracks = this.Mp3FileList.Length

        static member Empty = {
            Filename = ""
            Position = TimeSpan.Zero
            Duration = TimeSpan.Zero
            CurrentFileIndex = 0
            State = AudioPlayerState.Stopped
            AudioBook = None
            Mp3FileList = []
            PlaybackDelayed = false
            ResumeOnAudioFocus = false
            TimeUntilSleep = None
            PlaybackSpeed = 1.0
            IsBusy = false
            SleepTimerState = None
        }


    and SleepTimerState = {
        SleepTimerStartValue: TimeSpan
        SleepTimerCurrentTime: TimeSpan
    }



    type State = AudioPlayerInfo


    type Msg =
        | StateControlMsg of StateControlMsg
        | PlayerControlMsg of PlayerControlMsg
        | SleepTimerMsg of SleepTimerMsg


    and PlayerControlMsg =
        | Play
        | PlayExtern of filename: string * position: TimeSpan
        | Stop of resumeOnAudioFocus: bool
        | TogglePlayPause
        | MoveToNextTrack
        | MoveToPreviousTrack
        | JumpForward
        | JumpBackwards
        | GotoPosition of pos: TimeSpan
        | SetPlaybackSpeed of float
        | QuitAudioPlayer

    and StateControlMsg =
        | InitAudioService of AudioBookItemViewModel * Mp3FileList
        | DisableAudioService
        | UpdateInfoDataFromOutside of info: AudioPlayerInfo
        | SetBusy of bool
        | UpdatePosition of position: TimeSpan
        | UpdateAudioPlayerState of state: AudioPlayerState
        | UpdateCurrentMediaItem of mediaItem: IMediaItem

    and SleepTimerMsg =
        | SleepTimerTick
        | SleepTimerStop
        | SleepTimerStart of TimeSpan




    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | InitAudioPlayer
        | DisableAudioService
        | StartAudioPlayer
        | StartAudioPlayerExtern
        | StopAudioPlayer
        | TogglePlayPause
        | MoveToNextTrack
        | MoveToPreviousTrack
        | GotoPositionWithNewTrack
        | GotoPosition of position: TimeSpan
        | SetPlaybackSpeed of float
        | StopPlayingAndFinishAudioBook

        | GotUpdateInfoDataFromOutside
        | UpdateAudioBookViewModelPosition

        | StartSleepTimer
        | StopSleepTimer
        | SleepTimeReachedEnd

        | QuitAudioPlayer
    // side effects are the same as the message itself, so we can use the same type




    let init () =
        AudioPlayerInfo.Empty, SideEffect.None




    module Helpers =


        let getIndexForFile file (currentMp3ListWithDuration: Mp3FileList) =
            currentMp3ListWithDuration |> List.findIndex (fun (name, _) -> name = file)


        let getFileFromIndex idx (currentMp3ListWithDuration: Mp3FileList) =
            let idx =
                if idx < 0 then
                    0
                elif idx > (currentMp3ListWithDuration.Length - 1) then
                    (currentMp3ListWithDuration.Length - 1)
                else
                    idx

            currentMp3ListWithDuration[idx]


        let recalcFileAndPos filename pos mp3List =
            let index = mp3List |> getIndexForFile filename

            let rec getFileAndPos filename (pos: TimeSpan) =
                if pos >= TimeSpan.Zero then
                    let _, currentDuration = mp3List |> getFileFromIndex index

                    if pos > currentDuration then
                        // try next track
                        let newFileName, durationNextTrack = mp3List |> getFileFromIndex (index + 1)

                        if filename = newFileName then
                            // we are at the end of the audio book
                            filename, durationNextTrack
                        else
                            let newPos = pos - durationNextTrack
                            getFileAndPos newFileName newPos
                    else
                        // this is the one
                        filename, pos
                else

                    let newFileName, durationPrevTrack = mp3List |> getFileFromIndex (index - 1)
                    // we are on the first track
                    if (filename = newFileName) then
                        filename, TimeSpan.Zero
                    else
                        let newPos = pos + durationPrevTrack
                        getFileAndPos newFileName newPos

            getFileAndPos filename pos


        let sideEffectOnlyWhenPlaying state sideEffect =
            if state.State = AudioPlayerState.Playing then
                sideEffect
            else
                SideEffect.None




    let update msg state =
        match msg with
        //################ STATE CONTROL  ##################
        | StateControlMsg (InitAudioService(ab, mp3List)) ->
            // get current filename, track and position from the audiobook
            let audioBookState = ab.AudioBook.State

            match audioBookState.CurrentPosition with
            | Some posInfo when posInfo.Filename <> "" ->
                let index = mp3List |> Helpers.getIndexForFile posInfo.Filename
                let filename, duration = mp3List |> Helpers.getFileFromIndex index

                {
                    state with
                        Filename = filename
                        ResumeOnAudioFocus = false
                        PlaybackDelayed = false
                        Position = posInfo.Position
                        Duration = duration
                        CurrentFileIndex = index
                        Mp3FileList = mp3List
                        AudioBook = Some ab
                },
                SideEffect.InitAudioPlayer

            | _ ->
                let filename, duration = mp3List |> Helpers.getFileFromIndex 0

                {
                    state with
                        Filename = filename
                        ResumeOnAudioFocus = false
                        PlaybackDelayed = false
                        Position = TimeSpan.Zero
                        Duration = duration
                        CurrentFileIndex = 0
                        Mp3FileList = mp3List
                        AudioBook = Some ab
                },
                SideEffect.None

        | StateControlMsg DisableAudioService  ->
            AudioPlayerInfo.Empty, SideEffect.DisableAudioService

        | StateControlMsg(SetBusy b) ->
            { state with IsBusy = b }, SideEffect.None

        | StateControlMsg (UpdateInfoDataFromOutside info) ->
            info, SideEffect.None

        | StateControlMsg (UpdatePosition pos) ->
            { state with Position = if state.IsBusy then state.Position else pos }, SideEffect.UpdateAudioBookViewModelPosition

        | StateControlMsg (UpdateAudioPlayerState astate) ->
            { state with State = if state.IsBusy then state.State else astate }, SideEffect.None

        | StateControlMsg (UpdateCurrentMediaItem mediaItem) ->
            if state.IsBusy then
                state, SideEffect.None
            else
                {
                    state with
                        Filename = mediaItem.Id
                        CurrentFileIndex = mediaItem.TrackNumber - 1
                        Duration = mediaItem.Duration
                }, SideEffect.None


        //################ PLAYER CONTROL  ##################

        | PlayerControlMsg Play ->
            if state.State = AudioPlayerState.Stopped then
                if state.Filename = "" then
                    let filename, duration = state.Mp3FileList |> Helpers.getFileFromIndex 0

                    {
                        state with
                            Filename = filename
                            ResumeOnAudioFocus = false
                            PlaybackDelayed = false
                            Position = TimeSpan.Zero
                            Duration = duration
                            CurrentFileIndex = 0
                            State = AudioPlayerState.Playing
                    },
                    SideEffect.StartAudioPlayer
                else
                    {
                        state with
                            State = AudioPlayerState.Playing
                            ResumeOnAudioFocus = false
                            PlaybackDelayed = false
                    },
                    SideEffect.StartAudioPlayer
            else
                state, SideEffect.None

        | PlayerControlMsg (PlayExtern(filename, pos)) ->
            // recalc pos and maybe file when pos below zero (for jumpback on press play
            let filename, pos = Helpers.recalcFileAndPos filename pos state.Mp3FileList
            let index = state.Mp3FileList |> Helpers.getIndexForFile filename
            let _, duration = state.Mp3FileList |> Helpers.getFileFromIndex index

            {
                state with
                    Filename = filename
                    ResumeOnAudioFocus = false
                    PlaybackDelayed = false
                    Position = pos
                    Duration = duration
                    CurrentFileIndex = index
                    State = AudioPlayerState.Playing
            },
            SideEffect.StartAudioPlayerExtern

        | PlayerControlMsg(Stop resumeOnAudioFocus) ->
            {
                state with
                    State = AudioPlayerState.Stopped
                    ResumeOnAudioFocus = resumeOnAudioFocus
            },
            SideEffect.StopAudioPlayer

        | PlayerControlMsg TogglePlayPause ->
            {
                state with
                    State =
                        match state.State with
                        | AudioPlayerState.Playing -> AudioPlayerState.Stopped
                        | AudioPlayerState.Stopped -> AudioPlayerState.Playing
            },
            SideEffect.TogglePlayPause

        | PlayerControlMsg MoveToNextTrack ->
            if state.IsBusy then
                state, SideEffect.None
            else
                let newIndex = state.CurrentFileIndex + 1

                if state.Mp3FileList.Length < newIndex + 1 then
                    {
                        state with
                            State = AudioPlayerState.Stopped
                            Position = state.Duration
                    },
                    SideEffect.StopPlayingAndFinishAudioBook
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex (newIndex)

                    {
                        state with
                            Filename = newFileName
                            CurrentFileIndex = newIndex
                            Duration = duration
                    },
                    SideEffect.MoveToNextTrack |> Helpers.sideEffectOnlyWhenPlaying state

        | PlayerControlMsg MoveToPreviousTrack ->
            if state.IsBusy then
                state, SideEffect.None
            else if state.Position > TimeSpan.FromMilliseconds 2000 then
                { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
            else
                let newIndex = state.CurrentFileIndex - 1

                if newIndex < 0 then
                    { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex newIndex

                    {
                        state with
                            Filename = newFileName
                            CurrentFileIndex = newIndex
                            Duration = duration
                    },
                    SideEffect.MoveToPreviousTrack |> Helpers.sideEffectOnlyWhenPlaying state

        | PlayerControlMsg JumpForward ->
            // calculate, that a track can be ended, and a next one could start
            let jumpDistance = DependencyService.Get<GlobalSettingsService>().JumpDistance |> float
            let newPosition = state.Position + TimeSpan.FromMilliseconds jumpDistance

            if newPosition > state.Duration then
                let rest = newPosition - state.Duration
                let newIndex = state.CurrentFileIndex + 1

                if state.Mp3FileList.Length < newIndex + 1 then
                    { state with Position = state.Duration },
                    SideEffect.StopPlayingAndFinishAudioBook
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex newIndex

                    {
                        state with
                            Filename = newFileName
                            CurrentFileIndex = newIndex
                            Duration = duration
                            Position = rest
                    },
                    SideEffect.GotoPositionWithNewTrack
            else
                { state with Position = newPosition }, SideEffect.GotoPosition newPosition

        | PlayerControlMsg JumpBackwards ->
            let jumpDistance = DependencyService.Get<GlobalSettingsService>().JumpDistance |> float
            let newPosition = state.Position - TimeSpan.FromMilliseconds jumpDistance

            if newPosition < TimeSpan.Zero then
                let newIndex = state.CurrentFileIndex - 1

                if newIndex < 0 then
                    { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex newIndex

                    let newPosition = duration + newPosition // newPos is here negative

                    {
                        state with
                            Filename = newFileName
                            CurrentFileIndex = newIndex
                            Duration = duration
                            Position = newPosition
                    },
                    SideEffect.GotoPositionWithNewTrack
            else
                { state with Position = newPosition }, SideEffect.GotoPosition newPosition

        | PlayerControlMsg (GotoPosition pos) ->
            { state with Position = pos }, SideEffect.GotoPosition pos

        | PlayerControlMsg (SetPlaybackSpeed value) ->
            if value < 0.1 || value > 6.0 then
                state, SideEffect.None
            else
                { state with PlaybackSpeed = value }, SideEffect.SetPlaybackSpeed value

        | PlayerControlMsg QuitAudioPlayer ->
            AudioPlayerInfo.Empty, SideEffect.QuitAudioPlayer



        | SleepTimerMsg SleepTimerTick ->
            state.SleepTimerState
            |> Option.map (fun sleepTimerState ->
                if sleepTimerState.SleepTimerCurrentTime <= TimeSpan.Zero then
                    state, SideEffect.SleepTimeReachedEnd
                else
                    { state with SleepTimerState = Some { sleepTimerState with SleepTimerCurrentTime = sleepTimerState.SleepTimerCurrentTime - TimeSpan.FromSeconds 1.0 } }, SideEffect.None
            )
            |> Option.defaultValue (state, SideEffect.None)

        | SleepTimerMsg SleepTimerStop ->
            { state with SleepTimerState = None }, SideEffect.StopSleepTimer

        | SleepTimerMsg (SleepTimerStart time) ->
            { state
              with SleepTimerState = {
                SleepTimerStartValue = time
                SleepTimerCurrentTime = time
                } |> Some
            }, SideEffect.StartSleepTimer



    module SideEffects =

        let createMediaItem filename (audioBook: AudioBookItemViewModel) numTracks tracknumber duration =
            let mediaItem = MediaItem()
            mediaItem.MediaUri <- $"file://{filename}"
            mediaItem.MediaType <- MediaType.Audio
            mediaItem.Album <- audioBook.AudioBook.FullName
            mediaItem.Title <- $"Track {tracknumber} of {numTracks}"
            mediaItem.Id <- filename

            mediaItem.DisplayTitle <- audioBook.AudioBook.FullName

            mediaItem.ImageUri <- audioBook.AudioBook.Thumbnail |> Option.defaultValue Unchecked.defaultof<_>
            mediaItem.AlbumImageUri <- audioBook.AudioBook.Thumbnail |> Option.defaultValue Unchecked.defaultof<_>

            mediaItem.DisplaySubtitle <- $"Track {tracknumber} of {numTracks}"
            mediaItem.NumTracks <- numTracks
            mediaItem.TrackNumber <- tracknumber
            mediaItem.Duration <- duration
            mediaItem


        let createSideEffectsProcessor () =
            let mutable sleepTimer: Timer option = None
            let mutable currentMediaItem: MediaItem list = []
            let disposables = new System.Collections.Generic.List<IDisposable>()
            fun (sideEffect: SideEffect)
                (state: State)
                (dispatch: Msg -> unit) ->
                task {
                    match sideEffect with
                    | SideEffect.None ->
                        return ()

                    // normally all ops went into busy state, but this one shouldn
                    | SideEffect.UpdateAudioBookViewModelPosition ->
                        state.AudioBook
                        |> Option.iter (fun i ->
                            i.UpdateAudioBookPosition state.Position
                            i.ToggleIsPlaying(state.State = AudioPlayerState.Playing)
                        )

                        return ()

                    | SideEffect.StartSleepTimer ->
                        sleepTimer |> Option.iter (_.Dispose())
                        sleepTimer <- Some <| new Timer(
                            fun _ ->
                                dispatch <| SleepTimerMsg SleepTimerTick
                            , null, TimeSpan.FromSeconds 1.0, TimeSpan.FromSeconds 1.0)
                        return ()
                    | _ ->

                        dispatch <| StateControlMsg (SetBusy true)
                        do!
                            task {
                                match sideEffect with
                                | SideEffect.None ->
                                    return ()

                                | SideEffect.InitAudioPlayer ->
                                    do! CrossMediaManager.Current.Stop()
                                    CrossMediaManager.Current.Notification.Enabled <- true
                                    CrossMediaManager.Current.Queue.Clear()
                                    let mediaItems =
                                        match state.AudioBook with
                                        | Some audiobook ->
                                            state.Mp3FileList
                                            |> List.indexed
                                            |> List.map (fun (i,(filename, duration)) ->
                                                createMediaItem filename state.AudioBook.Value state.Mp3FileList.Length (i + 1) duration
                                            )
                                        | None -> []
                                    mediaItems |> List.iter (fun item -> CrossMediaManager.Current.Queue.Add item)
                                    currentMediaItem <- mediaItems



                                    disposables |> Seq.iter (fun i -> i.Dispose())
                                    disposables.Add
                                        <| CrossMediaManager.Current.PositionChanged.Subscribe(fun pos ->
                                            dispatch <| StateControlMsg (UpdatePosition pos.Position)
                                        )

                                    disposables.Add
                                        <| CrossMediaManager.Current.PropertyChanged.Subscribe(fun e ->
                                            let a = e
                                            ()
                                        )

                                    disposables.Add
                                        <| CrossMediaManager.Current.MediaItemChanged.Subscribe(fun e ->
                                            dispatch <| StateControlMsg (UpdateCurrentMediaItem e.MediaItem)
                                        )

                                    disposables.Add
                                        <| CrossMediaManager.Current.StateChanged.Subscribe(fun e ->
                                            match e.State with
                                            | MediaPlayerState.Playing -> Some AudioPlayerState.Playing
                                            | MediaPlayerState.Paused -> Some AudioPlayerState.Stopped
                                            | MediaPlayerState.Stopped -> Some AudioPlayerState.Stopped
                                            | _ -> None
                                            |> Option.iter (fun i -> dispatch <| StateControlMsg (UpdateAudioPlayerState i))
                                        )


                                    CrossMediaManager.Current.Speed <- state.PlaybackSpeed |> float32
                                    return ()

                                | SideEffect.DisableAudioService ->
                                    CrossMediaManager.Current.Notification.Enabled <- false
                                    disposables |> Seq.iter (fun i -> i.Dispose())
                                    disposables.Clear()
                                    return ()

                                | SideEffect.StartAudioPlayer ->
                                    state.AudioBook
                                    |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                                    let! _ =  CrossMediaManager.Current.PlayQueueItem(state.CurrentFileIndex)
                                    do! CrossMediaManager.Current.SeekTo state.Position
                                    return ()

                                | SideEffect.StartAudioPlayerExtern ->
                                    state.AudioBook
                                    |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)
                                    let queue = CrossMediaManager.Current.Queue
                                    let currentMediaItems = queue |> Seq.toList
                                    let mediaItem = CrossMediaManager.Current.Queue.Current

                                    let! _ =  CrossMediaManager.Current.PlayQueueItem(state.CurrentFileIndex, state.Position)
                                    //do! CrossMediaManager.Current.SeekTo state.Position
                                    return ()


                                | SideEffect.StopAudioPlayer ->
                                    state.AudioBook
                                    |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)

                                    do! CrossMediaManager.Current.Pause()

                                    return ()

                                | SideEffect.TogglePlayPause ->
                                    state.AudioBook
                                    |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)
                                    do! CrossMediaManager.Current.PlayPause()
                                    return ()

                                | SideEffect.MoveToNextTrack ->
                                    state.AudioBook
                                    |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                                    let! _ =  CrossMediaManager.Current.PlayQueueItem(state.CurrentFileIndex)
                                    //do! CrossMediaManager.Current.SeekTo state.Position
                                    return ()

                                | SideEffect.MoveToPreviousTrack ->
                                    state.AudioBook
                                    |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                                    let! _ =  CrossMediaManager.Current.PlayQueueItem(state.CurrentFileIndex)
                                    //do! CrossMediaManager.Current.SeekTo state.Position
                                    return ()

                                | SideEffect.GotoPositionWithNewTrack ->
                                    state.AudioBook
                                    |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                                    state.AudioBook
                                    |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)
                                    if state.State = AudioPlayerState.Playing then
                                        let! _ =  CrossMediaManager.Current.PlayQueueItem(state.CurrentFileIndex)
                                        do! CrossMediaManager.Current.SeekTo state.Position
                                        ()

                                    return ()

                                | SideEffect.GotoPosition position ->
                                    state.AudioBook |> Option.iter (fun i -> i.UpdateAudioBookPosition position)
                                    if state.State = AudioPlayerState.Playing then
                                        do! CrossMediaManager.Current.SeekTo position
                                        ()
                                    return ()



                                | SideEffect.SetPlaybackSpeed f ->
                                    CrossMediaManager.Current.Speed <- f |> float32
                                    return ()

                                | SideEffect.StopPlayingAndFinishAudioBook ->
                                    do! CrossMediaManager.Current.Pause()
                                    state.AudioBook |> Option.iter (_.MarkAsListend())
                                    return ()

                                | SideEffect.QuitAudioPlayer ->
                                    do! CrossMediaManager.Current.Pause()
                                    return ()

                                | SideEffect.GotUpdateInfoDataFromOutside ->
                                    // open Playerpage
                                    match state.AudioBook with
                                    | Some audiobook ->
                                        DependencyService
                                            .Get<IMainViewModel>()
                                            .GotoPlayerPage
                                            audiobook
                                            false
                                    | None -> ()

                                | SideEffect.UpdateAudioBookViewModelPosition ->
                                    // process earlier
                                    return ()

                                | SideEffect.StartSleepTimer ->
                                    return ()

                                | SideEffect.StopSleepTimer ->
                                    sleepTimer |> Option.iter (_.Dispose())
                                    return ()

                                | SideEffect.SleepTimeReachedEnd ->
                                    do! CrossMediaManager.Current.Pause()
                                    dispatch <| SleepTimerMsg SleepTimerStop
                                    return ()
                            }

                        dispatch <| StateControlMsg (SetBusy false)
                }


type IAudioPlayer =
    inherit IAudioPlayerPause
    //abstract member SetMetaData: audiobook:AudioBook -> numberOfTracks:int -> curentTrack:int -> unit
    abstract member Init:
        audioBook: AudioBookItemViewModel -> fileList: PlayerElmish.Mp3FileList -> unit
    abstract member DisableAudioPlayer: unit -> unit

    abstract member Play: unit -> unit
    abstract member PlayExtern: file: string -> pos: TimeSpan -> unit
    abstract member Pause: unit -> unit
    abstract member PlayPause: unit -> unit
    abstract member Stop: bool -> unit
    abstract member SeekTo: TimeSpan -> unit
    abstract member SetPlaybackSpeed: float -> unit
    abstract member Next: unit -> unit
    abstract member Previous: unit -> unit
    abstract member JumpForward: unit -> unit
    abstract member JumpBackwards: unit -> unit
    abstract member StartSleepTimer: spleepTime: TimeSpan option -> unit
    abstract member AudioPlayerInformation: PlayerElmish.AudioPlayerInfo
    // observable
    abstract member AudioPlayerInfoChanged: IObservable<PlayerElmish.AudioPlayerInfo>
