namespace PerryRhodan.AudiobookPlayer.Services.AudioPlayer



open System.Threading
open System.Threading.Tasks
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
open PerryRhodan.AudiobookPlayer.ViewModel.AudioBookItem
open PerryRhodan.AudiobookPlayer.ViewModel.AudioBookStore.AudioBookElmish
open PerryRhodan.AudiobookPlayer.ViewModels
open SkiaSharp
open System.Reactive.Linq


module PlayerElmish =

    [<RequireQualifiedAccess>]
    type AudioPlayerServiceState =
        | Stopped
        | Started


    type Mp3FileList = (string * TimeSpan) list

    // // I hate it, but the position is jumping around
    // type RecendlyMoved =
    //     | No
    //     | ForwardPos
    //     | BackwardsPos
    //     | ForwardTrack
    //     | BackwardsTrack
    //     | ForwardBoth
    //     | BackwardsBoth

    type AudioPlayerInfo = {
        IsInitialized: bool
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
        PlaybackSpeed: decimal
        IsBusy: bool
        // RecendlyMoved: RecendlyMoved

    } with

        member this.NumOfTracks = this.Mp3FileList.Length

        static member Empty = {
            IsInitialized = false
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
            PlaybackSpeed = 1.0m
            IsBusy = false
            // RecendlyMoved = No
        }


    and SleepTimerState = {
        SleepTimerStartValue: TimeSpan
        SleepTimerCurrentTime: TimeSpan
    }



    type State = AudioPlayerInfo


    type Msg =
        | StateControlMsg of StateControlMsg
        | PlayerControlMsg of PlayerControlMsg
        | RunSideEffect of SideEffect


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
        | SetPlaybackSpeed of decimal
        | QuitAudioPlayer

    and StateControlMsg =
        | InitAudioService of AudioBookItemViewModel * Mp3FileList
        | InitComplete
        | DisableAudioService
        | UpdateInfoDataFromOutside of info: AudioPlayerInfo
        | SetBusy of bool
        | UpdatePosition of position: TimeSpan
        | UpdateAudioPlayerState of state: AudioPlayerState
        | UpdateCurrentMediaItem of mediaItem: IMediaItem



    and [<RequireQualifiedAccess>]
        SideEffect =
        | None
        | InitMediaPlayer
        | ResetMediaPlayer
        | StartPlaying
        | StopPlaying
        | TogglePlayPause
        | PlayNextTrack
        | PlayPreviousTrack
        | JumpForward
        | JumpBackwards
        | PlayNewTrackAndSeekToPosition
        | SeekToPosition of position: TimeSpan
        | SetPlaybackSpeed of decimal
        | StopPlayingAndFinishAudioBook

        | GotUpdateInfoDataFromOutside
      
        | QuitAudioPlayer
    // side effects are the same as the message itself, so we can use the same type




    let init () =
        AudioPlayerInfo.Empty, SideEffect.None




    module Helpers =


        let getIndexForFile file (currentMp3ListWithDuration: Mp3FileList) =
            currentMp3ListWithDuration |> List.tryFindIndex (fun (name, _) -> name = file) |> Option.defaultValue 0


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
            if mp3List = [] then
                filename, pos
            else
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
        | RunSideEffect sideEffect ->
            state, sideEffect

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
                SideEffect.InitMediaPlayer

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
                SideEffect.InitMediaPlayer

        | StateControlMsg InitComplete ->
            { state with IsInitialized = true }, SideEffect.None


        | StateControlMsg DisableAudioService  ->
            AudioPlayerInfo.Empty, SideEffect.ResetMediaPlayer

        | StateControlMsg(SetBusy b) ->
            { state with IsBusy = b }, SideEffect.None

        | StateControlMsg (UpdateInfoDataFromOutside info) ->
            info, SideEffect.None

        | StateControlMsg (UpdatePosition pos) ->
            { state with Position = pos }, SideEffect.None


        | StateControlMsg (UpdateAudioPlayerState astate) ->
            { state with State = astate }, SideEffect.None

        | StateControlMsg (UpdateCurrentMediaItem mediaItem) ->
            if mediaItem.Id = state.Filename then
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
                            IsBusy = true
                    },
                    SideEffect.StartPlaying
                else
                    {
                        state with
                            State = AudioPlayerState.Playing
                            ResumeOnAudioFocus = false
                            PlaybackDelayed = false
                            IsBusy = true
                    },
                    SideEffect.StartPlaying
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
                    IsBusy = true
            },
            SideEffect.StartPlaying

        | PlayerControlMsg(Stop resumeOnAudioFocus) ->
            {
                state with
                    State = AudioPlayerState.Stopped
                    ResumeOnAudioFocus = resumeOnAudioFocus
            },
            SideEffect.StopPlaying

        | PlayerControlMsg TogglePlayPause ->
            state, SideEffect.TogglePlayPause

        | PlayerControlMsg MoveToNextTrack ->
            // let state = { state with RecendlyMoved = ForwardPos }

            let sideEffect, busy =
                if state.State = AudioPlayerState.Playing then
                    SideEffect.PlayNextTrack, false
                else
                    SideEffect.None, false

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
                        Position = TimeSpan.Zero
                        IsBusy = busy
                }, sideEffect


        | PlayerControlMsg MoveToPreviousTrack ->
            // let state = { state with RecendlyMoved = BackwardsPos }
            let sideEffect, busy =
                if state.State = AudioPlayerState.Playing then
                    SideEffect.PlayPreviousTrack, false
                else
                    SideEffect.None, false

            if state.Position > TimeSpan.FromMilliseconds 2000 then
                { state with Position = TimeSpan.Zero; IsBusy = busy }, sideEffect
            else
                let newIndex = state.CurrentFileIndex - 1

                if newIndex < 0 then
                    { state with Position = TimeSpan.Zero; IsBusy = busy }, sideEffect
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex newIndex

                    {
                        state with
                            Filename = newFileName
                            CurrentFileIndex = newIndex
                            Duration = duration
                            IsBusy = busy
                    }, sideEffect

        | PlayerControlMsg JumpForward ->
            // let state = { state with RecendlyMoved = ForwardPos }
            let sideEffect, busy =
                if state.State = AudioPlayerState.Playing then
                    SideEffect.JumpForward, false
                else
                    SideEffect.None, false

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
                            IsBusy = busy
                    }, sideEffect
            else
                { state with Position = newPosition; IsBusy = busy }, sideEffect

        | PlayerControlMsg JumpBackwards ->
            // let state = { state with RecendlyMoved = BackwardsPos }
            let sideEffect, busy =
                if state.State = AudioPlayerState.Playing then
                    SideEffect.JumpBackwards, false
                else
                    SideEffect.None, false

            let jumpDistance = DependencyService.Get<GlobalSettingsService>().JumpDistance |> float
            let newPosition = state.Position - TimeSpan.FromMilliseconds jumpDistance

            if newPosition < TimeSpan.Zero then
                let newIndex = state.CurrentFileIndex - 1

                if newIndex < 0 then
                    { state with
                        Position = TimeSpan.Zero
                        IsBusy = busy  }, sideEffect
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
                            IsBusy = busy
                    }, sideEffect
            else
                { state with
                    Position = newPosition
                    IsBusy = busy  }, sideEffect

        | PlayerControlMsg (GotoPosition pos) ->
            { state with Position = pos }, SideEffect.SeekToPosition pos

        | PlayerControlMsg (SetPlaybackSpeed value) ->
            if value < 0.1m || value > 6.0m then
                state, SideEffect.None
            else
                { state with PlaybackSpeed = value }, SideEffect.SetPlaybackSpeed value

        | PlayerControlMsg QuitAudioPlayer ->
            AudioPlayerInfo.Empty, SideEffect.QuitAudioPlayer




    module SideEffects =

        let createSideEffectsProcessor () =
            let mutable sleepTimer: Timer option = None
            let mutable currentMediaItems: IMediaItem array = [||]
            let disposables = new System.Collections.Generic.List<IDisposable>()
            let mutable suppressPositionAndTrackUpdates = false
            let mutable previousPosition = TimeSpan.Zero
            let mutable previousMediaItem = Unchecked.defaultof<IMediaItem>
            // used to deteminate, if we need to save the position

            let playQueueBouncer =
                Common.Extensions.debounceAsync<(State * (Msg -> unit)) > 350 (fun (state, dispatch) ct ->
                    async {
                        if not ct.IsCancellationRequested then
                            let! _ =  CrossMediaManager.Current.PlayQueueItem(state.CurrentFileIndex + 1, state.Position) |> Async.AwaitTask
                            #if DEBUG
                            System.Diagnostics.Trace.WriteLine $"PlayQueueItem: {state.CurrentFileIndex + 1} Position: {state.Position}"
                            #endif
                            dispatch <| StateControlMsg (SetBusy false)
                            do! Async.Sleep 2000
                            suppressPositionAndTrackUpdates <- false
                            return ()
                    }
                )

            let globalSettings = DependencyService.Get<GlobalSettingsService>()
            fun (sideEffect: SideEffect)
                (state: State)
                (dispatch: Msg -> unit) ->

                task {
                    match sideEffect with
                    | SideEffect.None ->
                        return ()

                    | SideEffect.InitMediaPlayer ->
                        dispatch <| StateControlMsg (SetBusy true)
                        CrossMediaManager.Current.Init()
                        do! CrossMediaManager.Current.Stop()
                        CrossMediaManager.Current.Notification.Enabled <- true
                        CrossMediaManager.Current.Queue.Clear()
                        CrossMediaManager.Current.ClearQueueOnPlay <- false

                        let! mediaItems =
                            match state.AudioBook with
                            | Some audiobook ->
                                try
                                    state.Mp3FileList
                                    |> List.map (fun (filename, _) ->
                                        task {
                                            let! mediaItem =
                                                CrossMediaManager.Current.Extractor.CreateMediaItem($"file://{filename}")
                                            mediaItem.Id <- filename
                                            return mediaItem
                                        }

                                    )
                                    |> Task.WhenAll
                                with
                                | ex ->
                                    Global.telemetryClient.TrackException ex
                                    Task.FromResult [||]
                            | None -> Task.FromResult [||]

                        // add artificial media item at the beginning, because of a bug, where sometimes the plyer move to media item 0
                        // keep in mind, with that the index will shift
                        let mediaItems: IMediaItem array =
                            [|
                                new MediaItem(Id="empty", TrackNumber=0) :> IMediaItem
                                yield! mediaItems
                            |]

                        mediaItems
                        |> Array.iter (fun item -> CrossMediaManager.Current.Queue.Add item)

                        currentMediaItems <- mediaItems

                        disposables |> Seq.iter (fun i -> i.Dispose())

                        // use debouncer to avoid jumping around, because the prepare function of the
                        // mediaplayer messes up the positions and mediaitems

                        let posTimer =
                            Reactive.Linq.Observable.Interval (TimeSpan.FromSeconds 1.0)
                            |> Observable.subscribe (fun _ ->
                                if (CrossMediaManager.Current.Queue.Current.Id <> "empty") && not suppressPositionAndTrackUpdates then
                                    let currentPos = CrossMediaManager.Current.Position
                                    let currentItem = CrossMediaManager.Current.Queue.Current
                                    // only send position and track updates, if they changed
                                    if currentPos <> previousPosition then
                                        dispatch <| StateControlMsg (UpdatePosition currentPos)
                                        previousPosition <- currentPos

                                    if currentItem <> previousMediaItem then
                                        dispatch <| StateControlMsg (UpdateCurrentMediaItem currentItem)
                                        previousMediaItem <- currentItem
                            )

                        disposables.Add posTimer

                        let stateDebouncer =
                            Common.Extensions.debounce<AudioPlayerState> 750 (fun state ->
                                if not suppressPositionAndTrackUpdates then
                                    dispatch <| StateControlMsg (UpdateAudioPlayerState state)
                            )

                        disposables.Add
                            <| CrossMediaManager.Current.StateChanged.Subscribe(fun e ->
                                match e.State with
                                | MediaPlayerState.Playing -> Some AudioPlayerState.Playing, false
                                | MediaPlayerState.Paused -> Some AudioPlayerState.Stopped, false
                                | MediaPlayerState.Stopped -> Some AudioPlayerState.Stopped, false
                                | MediaPlayerState.Loading -> None, true
                                | MediaPlayerState.Buffering -> None, true
                                | MediaPlayerState.Failed -> None, false
                                | _ -> None, false
                                |> fun (abstate, isBusy) ->
                                    #if DEBUG
                                    System.Diagnostics.Trace.WriteLine $"MediaPlayerStateTrigggered: {e.State} Translated To: {abstate}/{isBusy}"
                                    #endif

                                    dispatch <| StateControlMsg (SetBusy isBusy) |> ignore
                                    abstate |> Option.iter stateDebouncer
                            )


                        dispatch <| PlayerControlMsg (SetPlaybackSpeed globalSettings.PlaybackSpeed)
                        dispatch <| StateControlMsg InitComplete

                        dispatch <| StateControlMsg (SetBusy false)

                        return ()

                    | SideEffect.ResetMediaPlayer ->
                         CrossMediaManager.Current.Notification.Enabled <- false
                         CrossMediaManager.Current.Queue.Clear()
                         disposables |> Seq.iter (fun i -> i.Dispose())
                         disposables.Clear()
                         return ()

                    | SideEffect.StartPlaying ->
                         suppressPositionAndTrackUpdates <- true
                         playQueueBouncer (state,dispatch)
                         return ()

                    | SideEffect.StopPlaying ->
                         do! CrossMediaManager.Current.Pause()
                         return ()

                    | SideEffect.TogglePlayPause ->
                         do! CrossMediaManager.Current.PlayPause()
                         return ()

                    | SideEffect.PlayNextTrack ->
                         suppressPositionAndTrackUpdates <- true
                         do! CrossMediaManager.Current.Pause()
                         playQueueBouncer (state,dispatch)
                         return ()

                    | SideEffect.PlayPreviousTrack ->
                         suppressPositionAndTrackUpdates <- true
                         do! CrossMediaManager.Current.Pause()
                         playQueueBouncer (state,dispatch)
                         return ()

                    | SideEffect.JumpForward ->
                         suppressPositionAndTrackUpdates <- true
                         do! CrossMediaManager.Current.Pause()
                         playQueueBouncer (state,dispatch)
                         //let jumpDistance = globalSettings.JumpDistance |> TimeSpan.FromMilliseconds
                         //if jumpDistance <> CrossMediaManager.Current.StepSizeForward then
                         //    CrossMediaManager.Current.StepSizeForward <- jumpDistance
                         //let! _ = CrossMediaManager.Current.StepForward()
                         return ()

                    | SideEffect.JumpBackwards ->
                         suppressPositionAndTrackUpdates <- true
                         do! CrossMediaManager.Current.Pause()
                         playQueueBouncer (state,dispatch)
                         //let jumpDistance = globalSettings.JumpDistance |> TimeSpan.FromMilliseconds
                         //if jumpDistance <> CrossMediaManager.Current.StepSizeBackward then
                         //    CrossMediaManager.Current.StepSizeBackward <- jumpDistance

                         //let! _ = CrossMediaManager.Current.StepBackward()
                         return ()

                    | SideEffect.PlayNewTrackAndSeekToPosition ->
                         suppressPositionAndTrackUpdates <- true
                         do! CrossMediaManager.Current.Pause()
                         playQueueBouncer (state,dispatch)
                         return ()

                    | SideEffect.SeekToPosition position ->
                         do! CrossMediaManager.Current.SeekTo position
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

                }


open PlayerElmish

type IAudioPlayer =
    inherit IAudioPlayerPause
    //abstract member SetMetaData: audiobook:AudioBook -> numberOfTracks:int -> curentTrack:int -> unit
    abstract member Init:
        audioBook: AudioBookItemViewModel -> fileList: PlayerElmish.Mp3FileList -> Task<unit>
    abstract member DisableAudioPlayer: unit -> Task<unit>

    abstract member Play: unit -> Task<unit>
    abstract member PlayExtern: file: string -> pos: TimeSpan -> force:bool ->  Task<unit>
    abstract member Pause: unit -> Task<unit>
    abstract member PlayPause: unit -> Task<unit>
    abstract member Stop: bool -> Task<unit>
    abstract member SeekTo: TimeSpan -> Task<unit>
    abstract member SetPlaybackSpeed: decimal -> Task<unit>
    abstract member Next: unit -> Task<unit>
    abstract member Previous: unit -> Task<unit>
    abstract member JumpForward: unit -> Task<unit>
    abstract member JumpBackwards: unit -> Task<unit>
    abstract member AudioPlayerInformation: PlayerElmish.AudioPlayerInfo
    // observable
    abstract member AudioPlayerInfoChanged: IObservable<PlayerElmish.AudioPlayerInfo>


open ReactiveElmish.Avalonia
open Elmish.SideEffect
open PlayerElmish
open Elmish.SyncedProgram
open Elmish




type AudioPlayerService() =

    let store =
        Program.mkAvaloniaProgrammWithSideEffect
            PlayerElmish.init
            PlayerElmish.update
            (PlayerElmish.SideEffects.createSideEffectsProcessor ())
        #if DEBUG
        |> Program.withTrace(fun msg state _ ->
            System.Diagnostics.Trace.WriteLine($"Player: \r\n Msg: \r\n {msg} \r\n State: \r\n {({ state with Mp3FileList = [] })}")
        )
        #endif
        |> Program.mkStore

    let disposables = new System.Collections.Generic.List<IDisposable>()
    let mutable lastPosition = TimeSpan.Zero
    let mutable lastFile = ""

    let (|PositionOrFileChanged|_|) (state:AudioPlayerInfo) =
        match state.Position, state.Filename with
        | x, _ when x = TimeSpan.Zero -> None
        | _, "" -> None
        | _, _ ->
            if state.Position <> lastPosition || state.Filename <> lastFile then
                Some ()
            else
                None

    do
        // update current position in audiobook viewmodel on change of file or position
        disposables.Add
            <| store.Observable.Subscribe(fun state ->
                match state with
                | PositionOrFileChanged ->
                    lastPosition <- state.Position
                    lastFile <- state.Filename
                    state.AudioBook |> Option.iter (fun ab ->
                        ab.UpdateAudioBookPosition (state.Position)
                        ab.UpdateCurrentListenFilename (state.Filename)

                    )
                | _ -> ()

                // update player state
                state.AudioBook |> Option.iter (fun ab -> ab.ToggleIsPlaying (state.State = AudioPlayerState.Playing))
            )


    interface IAudioPlayerPause with
        member this.Pause() =
            task { store.Dispatch <| PlayerControlMsg (Stop false) }

        member this.Play() =
            task { store.Dispatch <| PlayerControlMsg Play }

    interface IAudioPlayer  with

        member this.Init audiobook fileList =
            let tcs = TaskCompletionSource<unit>()
            let rec subscription:IDisposable = store.Observable.Subscribe(fun _ ->
                if store.Model.IsInitialized then
                    tcs.SetResult()
                    subscription.Dispose()
            )
            store.Dispatch <| StateControlMsg (InitAudioService (audiobook, fileList))
            tcs.Task

        member this.DisableAudioPlayer() =
            task { store.Dispatch <| StateControlMsg (DisableAudioService) }

        member this.Play () =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg Play
            }


        member this.PlayExtern file pos force =
            task {
                if not store.Model.IsBusy || force then
                    store.Dispatch <| PlayerControlMsg (PlayExtern (file, pos))
            }

        member this.Pause() =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg (Stop false)
            }

        member this.PlayPause () =
            task {
                if not store.Model.IsBusy then
                    if store.Model.State = AudioPlayerState.Playing then
                        store.Dispatch <| PlayerControlMsg (Stop false)
                    else
                        store.Dispatch <| PlayerControlMsg Play
            }



        member this.Stop resumeOnAudioFocus =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg (Stop resumeOnAudioFocus)
            }

        member this.JumpBackwards () =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg JumpBackwards
            }

        member this.JumpForward () =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg JumpForward
            }

        member this.Next () =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg MoveToNextTrack
            }

        member this.Previous () =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg MoveToPreviousTrack
            }

        member this.SeekTo position =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg (GotoPosition position)
            }

        member this.SetPlaybackSpeed speed =
            task {
                if not store.Model.IsBusy then
                    store.Dispatch <| PlayerControlMsg (SetPlaybackSpeed speed)
            }
        

        member this.AudioPlayerInformation with get() = store.Model
        member this.AudioPlayerInfoChanged = store.Observable


    interface IDisposable with
        member this.Dispose() =
            disposables |> Seq.iter (fun i -> i.Dispose())