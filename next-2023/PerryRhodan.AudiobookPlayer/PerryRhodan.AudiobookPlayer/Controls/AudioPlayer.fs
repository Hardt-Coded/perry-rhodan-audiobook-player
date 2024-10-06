namespace PerryRhodan.AudiobookPlayer.Services.AudioPlayer



open Domain
open System
open FSharp.Control
open MediaManager
open PerryRhodan.AudiobookPlayer.ViewModel

module PlayerElmish =
    [<RequireQualifiedAccess>]
    type AudioPlayerServiceState =
        | Stopped        
        | Started


    [<RequireQualifiedAccess>]
    type AudioPlayerState =
        | Playing
        | Stopped


    type Mp3FileList = (string * TimeSpan) list
        
    type AudioPlayerInfo =
        { 
            Filename: string
            Position: TimeSpan
            Duration: TimeSpan
            CurrentTrackNumber: int // starts with zero
            State: AudioPlayerState 
            AudioBook: AudioBookItemViewModel option
            Mp3FileList: Mp3FileList
            PlaybackDelayed: bool 
            ResumeOnAudioFocus: bool 
            ServiceState: AudioPlayerServiceState
            TimeUntilSleep: TimeSpan option 
            PlaybackSpeed:float
            IsBusy: bool
        }
        
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
              ServiceState = AudioPlayerServiceState.Stopped
              TimeUntilSleep = None 
              PlaybackSpeed = 1.0
              IsBusy = false
        }

    type State = AudioPlayerInfo 


    type Msg =
        | StartAudioService of AudioBookItemViewModel * Mp3FileList
        | StopAudioService
        | StartAudioPlayer
        | StartAudioPlayerExtern of filename:string * position: TimeSpan
        | StopAudioPlayer of resumeOnAudioFocus:bool
        | TogglePlayPause
        | MoveToNextTrack
        | MoveToPreviousTrack
        | JumpForward 
        | JumpBackwards 
        | GotoPosition of pos:TimeSpan
        | UpdatePositionExternal of pos:TimeSpan * track:int
        | UpdateInfoDataFromOutside of info:AudioPlayerInfo
        | SetCurrentAudioServiceStateToStarted
        | StartSleepTimer of TimeSpan option
        | DecreaseSleepTimer
        | SetPlaybackSpeed of float
        | SetPlayerStateExternal of AudioPlayerState
        
        | UpdateAudioBookPosition of pos:TimeSpan
        | SetBusy of bool

        | QuitAudioPlayer
        
        
    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | StartAudioService
        | StopAudioService
        | StartAudioPlayer
        | StartAudioPlayerExtern
        | StopAudioPlayer
        | TogglePlayPause
        | MoveToNextTrack
        | MoveToPreviousTrack
        | GotoPositionWithNewTrack
        | GotoPosition of position:TimeSpan
        | StartSleepTimer of TimeSpan option
        | SetPlaybackSpeed of float
        | StopPlayingAndFinishAudioBook
        
        | SendAudioBookInfoEvent

        | QuitAudioPlayer
    // side effects are the same as the message itself, so we can use the same type

    let init () =
        AudioPlayerInfo.Empty, SideEffect.None
        
       
         
       
    module Helpers =
            
            
            let getIndexForFile file (currentMp3ListWithDuration:Mp3FileList) =
                currentMp3ListWithDuration |> List.findIndex (fun (name,_) -> name = file)


            let getFileFromIndex idx (currentMp3ListWithDuration:Mp3FileList) =
                let idx =
                    if idx < 0 then 0
                    elif idx > (currentMp3ListWithDuration.Length - 1) then (currentMp3ListWithDuration.Length - 1)
                    else idx

                currentMp3ListWithDuration[idx]

                    
            let recalcFileAndPos filename pos mp3List =
                let index = mp3List |> getIndexForFile filename

                let rec getFileAndPos filename (pos:TimeSpan) =
                    if pos >= TimeSpan.Zero then
                        let _,currentDuration =  mp3List |> getFileFromIndex index 
                        if pos > currentDuration then
                            // try next track
                            let newFileName,durationNextTrack =  mp3List |> getFileFromIndex (index + 1)
                            if filename = newFileName then
                                // we are at the end of the audio book
                                filename,durationNextTrack
                            else
                                let newPos = pos - durationNextTrack
                                getFileAndPos newFileName newPos
                        else
                            // this is the one
                            filename,pos
                    else
                        
                        let newFileName,durationPrevTrack = mp3List |> getFileFromIndex (index - 1)
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
        match state.ServiceState, msg with
        | AudioPlayerServiceState.Stopped, StartAudioService (ab,mp3List) ->
            // get current filename, track and position from the audiobook
            let audioBookState = ab.AudioBook.State
            
            match audioBookState.CurrentPosition with
            | Some posInfo when posInfo.Filename <> "" ->
                let index = mp3List |> Helpers.getIndexForFile posInfo.Filename
                let filename,duration = mp3List |> Helpers.getFileFromIndex index
                { state with
                    Filename = filename
                    ResumeOnAudioFocus = false
                    PlaybackDelayed = false
                    Position = posInfo.Position
                    Duration = duration
                    CurrentTrackNumber = index
                    Mp3FileList = mp3List
                    AudioBook =  Some ab
                    ServiceState = AudioPlayerServiceState.Started
                }, SideEffect.StartAudioService
                
            | _ ->
                let filename,duration = mp3List |> Helpers.getFileFromIndex 0
                { state with
                    Filename = filename
                    ResumeOnAudioFocus = false
                    PlaybackDelayed = false
                    Position = TimeSpan.Zero
                    Duration = duration
                    CurrentTrackNumber = 0
                    Mp3FileList = mp3List
                    AudioBook =  Some ab
                    ServiceState = AudioPlayerServiceState.Started
                }, SideEffect.StartAudioService
            
        | AudioPlayerServiceState.Started, StopAudioService ->
            {
                state with
                    ServiceState = AudioPlayerServiceState.Stopped
            }, SideEffect.StopAudioService
            
        | AudioPlayerServiceState.Started, StartAudioPlayer ->
            if state.State = AudioPlayerState.Stopped then
                if state.Filename = "" then
                    let filename,duration = state.Mp3FileList |> Helpers.getFileFromIndex 0
                    { state with
                        Filename = filename
                        ResumeOnAudioFocus = false
                        PlaybackDelayed = false
                        Position = TimeSpan.Zero
                        Duration = duration
                        CurrentTrackNumber = 0
                        State = AudioPlayerState.Playing
                    }, SideEffect.StartAudioPlayer
                else
                    { state with
                        State = AudioPlayerState.Playing
                        ResumeOnAudioFocus = false
                        PlaybackDelayed = false
                    }, SideEffect.StartAudioPlayer
            else
                state, SideEffect.None
        
        | AudioPlayerServiceState.Started, StartAudioPlayerExtern (filename, pos) ->
            // recalc pos and maybe file when pos below zero (for jumpback on press play
            let filename,pos = Helpers.recalcFileAndPos filename pos state.Mp3FileList
            let index = state.Mp3FileList |> Helpers.getIndexForFile filename
            let _,duration =  state.Mp3FileList |> Helpers.getFileFromIndex index
            { state with
                Filename = filename
                ResumeOnAudioFocus = false
                PlaybackDelayed = false
                Position = pos
                Duration =  duration
                CurrentTrackNumber = index
                State = AudioPlayerState.Playing }, SideEffect.StartAudioPlayerExtern
            
        | AudioPlayerServiceState.Started, StopAudioPlayer resumeOnAudioFocus ->
            { state with State = AudioPlayerState.Stopped; ResumeOnAudioFocus = resumeOnAudioFocus }, SideEffect.StopAudioPlayer
            
        | AudioPlayerServiceState.Started, TogglePlayPause ->
            {
              state with
                State = 
                    match state.State with
                    | AudioPlayerState.Playing -> AudioPlayerState.Stopped
                    | AudioPlayerState.Stopped -> AudioPlayerState.Playing
            }, SideEffect.TogglePlayPause
            
        | AudioPlayerServiceState.Started, MoveToNextTrack  ->
            if state.IsBusy then
                state, SideEffect.None
            else
                let newIndex = state.CurrentTrackNumber + 1
                if state.Mp3FileList.Length < newIndex + 1 then
                    { state with State = AudioPlayerState.Stopped; Position = state.Duration }, SideEffect.StopPlayingAndFinishAudioBook
                else
                    let newFileName, duration = state.Mp3FileList |> Helpers.getFileFromIndex (state.CurrentTrackNumber + 1) 
                    { state with Filename = newFileName; CurrentTrackNumber = state.CurrentTrackNumber + 1; Duration = duration }, SideEffect.MoveToNextTrack |> Helpers.sideEffectOnlyWhenPlaying state 
                    
        | AudioPlayerServiceState.Started, MoveToPreviousTrack ->
            if state.IsBusy then
                state, SideEffect.None
            else
                if state.Position > TimeSpan.FromMilliseconds 2000 then
                    { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
                else
                    let newIndex = state.CurrentTrackNumber - 1
                    if newIndex < 0 then
                        { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
                    else
                        let newFileName, duration = state.Mp3FileList |> Helpers.getFileFromIndex newIndex
                        { state with Filename = newFileName; CurrentTrackNumber = newIndex; Duration = duration }, SideEffect.MoveToPreviousTrack |> Helpers.sideEffectOnlyWhenPlaying state
                        
        | AudioPlayerServiceState.Started, JumpForward ->
            // calculate, that a track can be ended, and a next one could start
            let newPosition = state.Position + TimeSpan.FromMilliseconds 5000
            if newPosition > state.Duration then
                let rest = newPosition - state.Duration
                let newIndex = state.CurrentTrackNumber + 1
                if state.Mp3FileList.Length < newIndex + 1 then
                    { state with Position = state.Duration }, SideEffect.StopPlayingAndFinishAudioBook
                else
                    let newFileName, duration = state.Mp3FileList |> Helpers.getFileFromIndex newIndex
                    { state with
                        Filename = newFileName
                        CurrentTrackNumber = newIndex
                        Duration = duration
                        Position = rest
                    }, SideEffect.GotoPositionWithNewTrack
            else
                { state with Position = newPosition }, SideEffect.GotoPosition newPosition
           
        | AudioPlayerServiceState.Started, JumpBackwards ->
            let newPosition = state.Position - TimeSpan.FromMilliseconds 5000
            if newPosition < TimeSpan.Zero then
                let newIndex = state.CurrentTrackNumber - 1
                if newIndex < 0 then
                    { state with Position = TimeSpan.Zero }, SideEffect.GotoPosition TimeSpan.Zero
                else
                    let newFileName, duration = state.Mp3FileList |> Helpers.getFileFromIndex newIndex
                    let newPosition = duration + newPosition // newPos is here negative
                    { state with
                        Filename = newFileName
                        CurrentTrackNumber = newIndex
                        Duration = duration
                        Position = newPosition
                    }, SideEffect.GotoPositionWithNewTrack
             else
                { state with Position = newPosition }, SideEffect.GotoPosition newPosition
                
        | AudioPlayerServiceState.Started, GotoPosition pos ->
            { state with Position = pos }, SideEffect.GotoPosition pos
            
        | AudioPlayerServiceState.Started, UpdatePositionExternal (pos, meantTrack) ->
            let filename,_ = Helpers.getFileFromIndex meantTrack state.Mp3FileList
            { state with
                Filename = filename
                Position = pos
                CurrentTrackNumber = meantTrack
            }, SideEffect.GotoPositionWithNewTrack
            
        | AudioPlayerServiceState.Started, SetCurrentAudioServiceStateToStarted ->
            { state with ServiceState = AudioPlayerServiceState.Started }, SideEffect.None
            
        | AudioPlayerServiceState.Started, StartSleepTimer sleepTime ->
            { state with TimeUntilSleep = sleepTime }, SideEffect.StartSleepTimer sleepTime
            
        | AudioPlayerServiceState.Started, DecreaseSleepTimer ->
            let sleepTime = state.TimeUntilSleep |> Option.map (_.Subtract(TimeSpan.FromSeconds(1.)))
            if sleepTime |> Option.exists (fun t -> t <= TimeSpan.Zero) then
                { state with TimeUntilSleep = None }, SideEffect.StopAudioPlayer
            else
                { state with TimeUntilSleep = sleepTime }, SideEffect.None
                
        | AudioPlayerServiceState.Started, SetPlaybackSpeed value ->
            if value < 0.1 || value > 6.0 then
                state, SideEffect.None
            else
                { state with PlaybackSpeed = value }, SideEffect.SetPlaybackSpeed value
                
        | AudioPlayerServiceState.Started, QuitAudioPlayer ->
            AudioPlayerInfo.Empty, SideEffect.QuitAudioPlayer
            
        | _, UpdateAudioBookPosition pos ->
            { state with Position = pos }, SideEffect.None            
            
        | _, UpdateInfoDataFromOutside info ->
            info, SideEffect.StartAudioService
            
        | _, SetBusy b ->
            { state with IsBusy = b }, SideEffect.None
            
        | _, SetPlayerStateExternal pstate ->
            { state with State = pstate }, SideEffect.None
            
        | _ -> state, SideEffect.None
        
        
        
    module SideEffects =

                
        let createSideEffectsProcessor ()  =
            let mutable sleepTime: TimeSpan option = None
            
                        
            let sleepTimer: System.Timers.Timer = new System.Timers.Timer(1000.)
            sleepTimer.Stop()
            sleepTimer.Elapsed.Add(fun _ ->
                sleepTime
                |> Option.iter (fun t ->
                    if t <= TimeSpan.Zero then
                        sleepTimer.Stop()
                        sleepTime <- None
                        CrossMediaManager.Current.Stop() |> ignore
                    else
                        sleepTime <- Some (t.Subtract(TimeSpan.FromSeconds(1.)))
                )
            )
            
            let disposables = System.Collections.Generic.List<IDisposable>()
            
            

            
            fun (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) ->
                task {
                    match sideEffect with
                    | SideEffect.None ->
                        return ()
                        
                    | SideEffect.StartAudioService ->
                        CrossMediaManager.Current.Notification.Enabled <- true
                        CrossMediaManager.Current.Notification.ShowNavigationControls <- true
                        CrossMediaManager.Current.Notification.ShowPlayPauseControls <- true
                        
                        CrossMediaManager.Current.MediaItemFinished.Subscribe(fun _ ->
                            dispatch MoveToNextTrack
                        ) |> ignore
                        
                        let posDisposable =
                            CrossMediaManager.Current.PositionChanged.Subscribe(fun pos ->
                                // update audiobook view model
                                // avoid that on stop the position is set to zero
                                if pos.Position > TimeSpan.Zero then
                                    state.AudioBook
                                    |> Option.iter (fun i ->
                                        i.UpdateAudioBookPosition pos.Position 
                                        )
                                    dispatch (UpdateAudioBookPosition pos.Position)
                            )
                            
                        let stateDisposable =
                            CrossMediaManager.Current.StateChanged.Subscribe(fun state ->
                                match state.State with
                                | MediaManager.Player.MediaPlayerState.Playing ->
                                    dispatch <| SetPlayerStateExternal AudioPlayerState.Playing
                                | MediaManager.Player.MediaPlayerState.Stopped ->
                                    dispatch <| SetPlayerStateExternal AudioPlayerState.Stopped
                                | _ -> ()
                            )
                            
                        disposables.Add posDisposable
                        disposables.Add stateDisposable
                        
                        return ()
                        
                    | SideEffect.StopAudioService ->
                        disposables |> Seq.iter (_.Dispose())
                        disposables.Clear()
                        return ()
                        
                    | SideEffect.StartAudioPlayer ->
                        dispatch <| SetBusy true
                        state.AudioBook |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)
                        let! _ = CrossMediaManager.Current.Play(state.Filename)
                        do! CrossMediaManager.Current.SeekTo state.Position
                        dispatch <| SetBusy false
                        return ()
                        
                    | SideEffect.StartAudioPlayerExtern ->
                        dispatch <| SetBusy true
                        state.AudioBook |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)
                        let! _ = CrossMediaManager.Current.Play state.Filename
                        do! CrossMediaManager.Current.SeekTo state.Position
                        dispatch <| SetBusy false
                        return ()
                        
                    | SideEffect.StopAudioPlayer ->
                        state.AudioBook |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)
                        do! CrossMediaManager.Current.Stop()
                        return ()
                        
                    | SideEffect.TogglePlayPause ->
                        state.AudioBook |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)
                        do! CrossMediaManager.Current.PlayPause()
                        return ()
                        
                    | SideEffect.MoveToNextTrack ->
                        dispatch <| SetBusy true
                        state.AudioBook |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)
                        let! _ = CrossMediaManager.Current.Stop()
                        CrossMediaManager.Current.Queue.Clear()
                        let! _ = CrossMediaManager.Current.Play state.Filename
                        dispatch <| SetBusy false
                        return ()
                        
                    | SideEffect.MoveToPreviousTrack ->
                        dispatch <| SetBusy true
                        state.AudioBook |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)
                        let! _ = CrossMediaManager.Current.Stop()
                        CrossMediaManager.Current.Queue.Clear()
                        let! _ = CrossMediaManager.Current.Play state.Filename
                        dispatch <| SetBusy false
                        return ()
                        
                    | SideEffect.GotoPositionWithNewTrack ->
                        state.AudioBook |> Option.iter (fun i -> i.UpdateCurrentListenFilename state.Filename)
                        state.AudioBook |> Option.iter (fun i -> i.UpdateAudioBookPosition state.Position)
                        let! _ = CrossMediaManager.Current.Play state.Filename
                        do! CrossMediaManager.Current.SeekTo state.Position
                        return ()
                        
                    | SideEffect.GotoPosition position ->
                        state.AudioBook |> Option.iter (fun i -> i.UpdateAudioBookPosition position)
                        do! CrossMediaManager.Current.SeekTo position
                        return ()
                        
                    | SideEffect.StartSleepTimer timeSpanOption ->
                        sleepTime <- timeSpanOption
                        return ()
                        
                    | SideEffect.SetPlaybackSpeed f ->
                        CrossMediaManager.Current.Speed <- (f |> float32)
                        return ()
                        
                    | SideEffect.StopPlayingAndFinishAudioBook ->
                        do! CrossMediaManager.Current.Stop()
                        state.AudioBook
                        |> Option.iter (_.MarkAsListend()
                        )
                        return ()
                        
                    | SideEffect.QuitAudioPlayer -> 
                        do! CrossMediaManager.Current.Stop()
                        return ()
                        
                    | SideEffect.SendAudioBookInfoEvent ->
                        return ()
                        
                    
                    
                }
open ReactiveElmish.Avalonia
open Elmish.SideEffect
open PlayerElmish
module AudioPlayer =
    
    let globalAudioPlayerStore =
        Program.mkAvaloniaProgrammWithSideEffect init update (SideEffects.createSideEffectsProcessor ())
        |> Program.mkStore        
       
    
             
    let startAudioService ab mp3List =
        globalAudioPlayerStore.Dispatch (StartAudioService (ab,mp3List))
        
    let stopAudioService () =
        globalAudioPlayerStore.Dispatch StopAudioService
        
    let startAudioPlayer () =
        globalAudioPlayerStore.Dispatch StartAudioPlayer
        
    let startAudioPlayerExtern filename pos =
        globalAudioPlayerStore.Dispatch (StartAudioPlayerExtern (filename, pos))
        
    let stopAudioPlayer resumeOnAudioFocus =
        globalAudioPlayerStore.Dispatch (StopAudioPlayer resumeOnAudioFocus)
        
    let togglePlayPause () =
        globalAudioPlayerStore.Dispatch TogglePlayPause
        
    let moveToNextTrack () =
        globalAudioPlayerStore.Dispatch MoveToNextTrack
        
    let moveToPreviousTrack () =
        globalAudioPlayerStore.Dispatch MoveToPreviousTrack
        
    let jumpForward () =
        globalAudioPlayerStore.Dispatch JumpForward
        
    let jumpBackwards () =
        globalAudioPlayerStore.Dispatch JumpBackwards
        
    let setPosition pos =
        globalAudioPlayerStore.Dispatch (GotoPosition pos)
        
    let updatePositionExternal pos meantTrack =
        globalAudioPlayerStore.Dispatch (UpdatePositionExternal (pos, meantTrack))
        
    let setCurrentAudioServiceStateToStarted () =
        globalAudioPlayerStore.Dispatch SetCurrentAudioServiceStateToStarted
        
    let startSleepTimer timeSpanOption =
        globalAudioPlayerStore.Dispatch (StartSleepTimer timeSpanOption)
        
    let decreaseSleepTimer () =
        globalAudioPlayerStore.Dispatch DecreaseSleepTimer
        
    let setPlaybackSpeed value =
        globalAudioPlayerStore.Dispatch (SetPlaybackSpeed value)
        
    let quitAudioPlayer () =
        globalAudioPlayerStore.Dispatch QuitAudioPlayer
        
        
        
                
                                    

                
                            

                        
        
        
 