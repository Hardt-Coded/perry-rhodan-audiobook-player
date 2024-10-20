namespace PerryRhodan.AudiobookPlayer.Services.AudioPlayer



open Dependencies
open Domain
open System
open FSharp.Control
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.ViewModels


[<RequireQualifiedAccess>]
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
        CurrentTrackNumber: int // starts with zero
        State: AudioPlayerState
        AudioBook: AudioBookItemViewModel option
        Mp3FileList: Mp3FileList
        PlaybackDelayed: bool
        ResumeOnAudioFocus: bool
        TimeUntilSleep: TimeSpan option
        PlaybackSpeed: float
        IsBusy: bool
        JumpDistance: int
    } with

        member this.NumOfTracks = this.Mp3FileList.Length

        static member Empty = {
            Filename = ""
            Position = TimeSpan.Zero
            Duration = TimeSpan.Zero
            CurrentTrackNumber = 0
            State = AudioPlayerState.Stopped
            AudioBook = None
            Mp3FileList = []
            PlaybackDelayed = false
            ResumeOnAudioFocus = false
            TimeUntilSleep = None
            PlaybackSpeed = 1.0
            IsBusy = false
            JumpDistance = 30000
        }

    type State = AudioPlayerInfo


    type Msg =
        | InitAudioService of AudioBookItemViewModel * Mp3FileList
        | Play
        | PlayExtern of filename: string * position: TimeSpan
        | Stop of resumeOnAudioFocus: bool
        | TogglePlayPause
        | MoveToNextTrack
        | MoveToPreviousTrack
        | JumpForward
        | JumpBackwards
        | GotoPosition of pos: TimeSpan
        | UpdatePlayingState of pos: TimeSpan * duration: TimeSpan * state: AudioPlayerState
        | UpdateInfoDataFromOutside of info: AudioPlayerInfo
        | StartSleepTimer of TimeSpan option
        | DecreaseSleepTimer
        | SetPlaybackSpeed of float

        | SetBusy of bool

        | QuitAudioPlayer
        | SetJumpDistance of int


    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | InitAudioPlayer
        | StartAudioPlayer
        | StartAudioPlayerExtern
        | StopAudioPlayer
        | TogglePlayPause
        | MoveToNextTrack
        | MoveToPreviousTrack
        | GotoPositionWithNewTrack
        | GotoPosition of position: TimeSpan
        | StartSleepTimer of TimeSpan option
        | SetPlaybackSpeed of float
        | StopPlayingAndFinishAudioBook

        | GotUpdateInfoDataFromOutside
        | UpdateAudioBookViewModelPosition

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
        | InitAudioService(ab, mp3List) ->
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
                        CurrentTrackNumber = index
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
                        CurrentTrackNumber = 0
                        Mp3FileList = mp3List
                        AudioBook = Some ab
                },
                SideEffect.None

        | Play ->
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
                            CurrentTrackNumber = 0
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

        | PlayExtern(filename, pos) ->
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
                    CurrentTrackNumber = index
                    State = AudioPlayerState.Playing
            },
            SideEffect.StartAudioPlayerExtern

        | Stop resumeOnAudioFocus ->
            {
                state with
                    State = AudioPlayerState.Stopped
                    ResumeOnAudioFocus = resumeOnAudioFocus
            },
            SideEffect.StopAudioPlayer

        | TogglePlayPause ->
            {
                state with
                    State =
                        match state.State with
                        | AudioPlayerState.Playing -> AudioPlayerState.Stopped
                        | AudioPlayerState.Stopped -> AudioPlayerState.Playing
            },
            SideEffect.TogglePlayPause

        | MoveToNextTrack ->
            if state.IsBusy then
                state, SideEffect.None
            else
                let newIndex = state.CurrentTrackNumber + 1

                if state.Mp3FileList.Length < newIndex + 1 then
                    {
                        state with
                            State = AudioPlayerState.Stopped
                            Position = state.Duration
                    },
                    SideEffect.StopPlayingAndFinishAudioBook
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex (state.CurrentTrackNumber + 1)

                    {
                        state with
                            Filename = newFileName
                            CurrentTrackNumber = state.CurrentTrackNumber + 1
                            Duration = duration
                    },
                    SideEffect.MoveToNextTrack |> Helpers.sideEffectOnlyWhenPlaying state

        | MoveToPreviousTrack ->
            if state.IsBusy then
                state, SideEffect.None
            else if state.Position > TimeSpan.FromMilliseconds 2000 then
                { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
            else
                let newIndex = state.CurrentTrackNumber - 1

                if newIndex < 0 then
                    { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex newIndex

                    {
                        state with
                            Filename = newFileName
                            CurrentTrackNumber = newIndex
                            Duration = duration
                    },
                    SideEffect.MoveToPreviousTrack |> Helpers.sideEffectOnlyWhenPlaying state

        | JumpForward ->
            // calculate, that a track can be ended, and a next one could start
            let newPosition = state.Position + TimeSpan.FromMilliseconds state.JumpDistance

            if newPosition > state.Duration then
                let rest = newPosition - state.Duration
                let newIndex = state.CurrentTrackNumber + 1

                if state.Mp3FileList.Length < newIndex + 1 then
                    { state with Position = state.Duration },
                    SideEffect.StopPlayingAndFinishAudioBook
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex newIndex

                    {
                        state with
                            Filename = newFileName
                            CurrentTrackNumber = newIndex
                            Duration = duration
                            Position = rest
                    },
                    SideEffect.GotoPositionWithNewTrack
            else
                { state with Position = newPosition }, SideEffect.GotoPosition newPosition

        | JumpBackwards ->
            let newPosition = state.Position - TimeSpan.FromMilliseconds state.JumpDistance

            if newPosition < TimeSpan.Zero then
                let newIndex = state.CurrentTrackNumber - 1

                if newIndex < 0 then
                    { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
                else
                    let newFileName, duration =
                        state.Mp3FileList |> Helpers.getFileFromIndex newIndex

                    let newPosition = duration + newPosition // newPos is here negative

                    {
                        state with
                            Filename = newFileName
                            CurrentTrackNumber = newIndex
                            Duration = duration
                            Position = newPosition
                    },
                    SideEffect.GotoPositionWithNewTrack
            else
                { state with Position = newPosition }, SideEffect.GotoPosition newPosition

        | GotoPosition pos ->
            { state with Position = pos }, SideEffect.GotoPosition pos

        | StartSleepTimer sleepTime ->
            {
                state with
                    TimeUntilSleep = sleepTime
            },
            SideEffect.StartSleepTimer sleepTime

        | DecreaseSleepTimer ->
            let sleepTime =
                state.TimeUntilSleep |> Option.map (_.Subtract(TimeSpan.FromSeconds(1.)))

            if sleepTime |> Option.exists (fun t -> t <= TimeSpan.Zero) then
                { state with TimeUntilSleep = None }, SideEffect.StopAudioPlayer
            else
                {
                    state with
                        TimeUntilSleep = sleepTime
                },
                SideEffect.None

        | SetPlaybackSpeed value ->
            if value < 0.1 || value > 6.0 then
                state, SideEffect.None
            else
                { state with PlaybackSpeed = value }, SideEffect.SetPlaybackSpeed value

        | QuitAudioPlayer ->
            AudioPlayerInfo.Empty, SideEffect.QuitAudioPlayer

        | UpdateInfoDataFromOutside info -> info, SideEffect.GotUpdateInfoDataFromOutside

        | SetBusy b -> { state with IsBusy = b }, SideEffect.None

        | UpdatePlayingState(pos, duration, apstate) ->
            {
                state with
                    Position = pos
                    Duration = duration
                    State = apstate
            }, SideEffect.UpdateAudioBookViewModelPosition

        | SetJumpDistance jd ->
            { state with JumpDistance = jd }, SideEffect.None

        


    module SideEffects =


        let createSideEffectsProcessor (audioPlayer: IMediaPlayer) =
            let mutable sleepTime: TimeSpan option = None


            let sleepTimer: System.Timers.Timer = new System.Timers.Timer(1000.)
            sleepTimer.Stop()

            sleepTimer.Elapsed.Add(fun _ ->
                sleepTime
                |> Option.iter (fun t ->
                    if t <= TimeSpan.Zero then
                        sleepTimer.Stop()
                        sleepTime <- None

                        audioPlayer.Stop()
                    else
                        sleepTime <- Some(t.Subtract(TimeSpan.FromSeconds(1.)))
                )
            )

            let disposables = System.Collections.Generic.List<IDisposable>()




            fun (sideEffect: SideEffect) (state: State) (dispatch: Msg -> unit) ->
                task {
                    

                    match sideEffect with
                    | SideEffect.None -> return ()

                    | SideEffect.InitAudioPlayer ->
                        let! jumpDistance = Services.SystemSettings.getJumpDistance()
                        dispatch <| SetJumpDistance jumpDistance
                        return ()
                    
                    | SideEffect.StartAudioPlayer ->
                        dispatch <| SetBusy true

                        state.AudioBook
                        |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                        do! audioPlayer.Play state.Filename
                        audioPlayer.SeekTo state.Position

                        dispatch <| SetBusy false
                        return ()

                    | SideEffect.StartAudioPlayerExtern ->
                        dispatch <| SetBusy true

                        state.AudioBook
                        |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                        do! audioPlayer.Play state.Filename
                        audioPlayer.SeekTo state.Position

                        dispatch <| SetBusy false
                        return ()

                    | SideEffect.StopAudioPlayer ->
                        state.AudioBook
                        |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)

                        audioPlayer.Stop()

                        return ()

                    | SideEffect.TogglePlayPause ->
                        state.AudioBook
                        |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)

                        audioPlayer.PlayPause()

                        return ()

                    | SideEffect.MoveToNextTrack ->
                        dispatch <| SetBusy true

                        state.AudioBook
                        |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                        audioPlayer.Stop()
                        do! audioPlayer.Play state.Filename

                        dispatch <| SetBusy false
                        return ()

                    | SideEffect.MoveToPreviousTrack ->
                        dispatch <| SetBusy true

                        state.AudioBook
                        |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                        audioPlayer.Stop()
                        do! audioPlayer.Play state.Filename

                        dispatch <| SetBusy false
                        return ()

                    | SideEffect.GotoPositionWithNewTrack ->
                        state.AudioBook
                        |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)

                        state.AudioBook
                        |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)

                        do! audioPlayer.Play state.Filename
                        audioPlayer.SeekTo state.Position

                        return ()

                    | SideEffect.GotoPosition position ->
                        state.AudioBook |> Option.iter (fun i -> i.UpdateAudioBookPosition position)
                        audioPlayer.SeekTo position

                        return ()

                    | SideEffect.StartSleepTimer timeSpanOption ->
                        sleepTime <- timeSpanOption
                        return ()

                    | SideEffect.SetPlaybackSpeed f ->
                        do! audioPlayer.SetPlaybackSpeed f

                        return ()

                    | SideEffect.StopPlayingAndFinishAudioBook ->
                        audioPlayer.Stop()
                        state.AudioBook |> Option.iter (_.MarkAsListend())

                        return ()

                    | SideEffect.QuitAudioPlayer ->
                        audioPlayer.Stop()
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
                        
                        state.AudioBook |> Option.iter (fun i ->
                            i.UpdateAudioBookPosition state.Position
                            i.ToggleIsPlaying (state.State = AudioPlayerState.Playing)
                        )
                        return ()




                }


type IAudioPlayer =
    inherit IAudioPlayerPause
    //abstract member SetMetaData: audiobook:AudioBook -> numberOfTracks:int -> curentTrack:int -> unit
    abstract member Init : audioBook:AudioBookItemViewModel -> fileList:PlayerElmish.Mp3FileList -> unit
    abstract member Play : unit -> unit
    abstract member PlayExtern : file:string -> pos:TimeSpan -> unit
    abstract member Pause : unit -> unit
    abstract member PlayPause : unit -> unit
    abstract member Stop : bool -> unit
    abstract member SeekTo : TimeSpan -> unit
    abstract member SetPlaybackSpeed : float -> unit
    abstract member Next : unit -> unit
    abstract member Previous : unit -> unit
    abstract member JumpForward : unit -> unit
    abstract member JumpBackwards : unit -> unit
    abstract member StartSleepTimer : spleepTime:TimeSpan option -> unit
    abstract member Duration : TimeSpan
    abstract member CurrentPosition : TimeSpan
    abstract member AudioPlayerInformation : PlayerElmish.AudioPlayerInfo
    // observable
    abstract member AudioPlayerInfoChanged : IObservable<PlayerElmish.AudioPlayerInfo> 
    
