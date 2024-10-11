namespace PerryRhodan.AudiobookPlayer.ViewModels


open System
open System.IO
open System.Reflection
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Domain
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer.PlayerElmish
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open ReactiveElmish

module PlayerPage =

    open Services
    

    type State = {
        AudioBook:AudioBookItemViewModel 
        AudioFileList: (string * TimeSpan) list
        CurrentAudioFile: string
        CurrentAudioFileIndex: int
        CurrentPosition: TimeSpan  
        CurrentDuration: TimeSpan  
        CurrentState : AudioPlayerState
        PlaybackSpeed: float
        TimeUntilSleeps: TimeSpan option
         
        AudioPlayerBusy:bool 
        HasPlayedBeforeDragSlider:bool
        SliderIsDragging:bool
        StartPlayingOnOpen:bool
        IsLoading: bool
    }
      

    type Msg = 
        | Play 
        | PlayWithoutRewind
        | Stop
        | Next
        | Previous
        | JumpForward
        | JumpBackwards
        | RestoreStateFormAudioService of AudioPlayerInfo
        | FileListLoaded of (string * TimeSpan) list
        
        | OpenSleepTimerActionMenu
        | OpenPlaybackSpeedActionMenu
        | StartSleepTimer of TimeSpan option
        | SetPlaybackSpeed of float
        | UpdateSleepTimer of TimeSpan option        
        | SetPlayerStateFromExtern of AudioPlayerState
        | UpdateTrackNumber of int
        
        | SetTrackPosition of TimeSpan
        | SetDragging of bool
        | UpdateScreenInformation of info:PlayerElmish.State

        | ChangeBusyState of bool

    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | InitAudioPlayer
        | SliderDragStarted
        
        | StartAudioBookService of fileList:(string * TimeSpan) list
        | StopAudioBookService
        
        | Play
        | PlayWithoutRewind
        | Stop
        | Next
        | Previous
        | JumpForward
        | JumpBackwards
        | StartSleepTimer of TimeSpan option
        | SetPlaybackSpeed of float
        | SetAudioPositionAbsolute of pos:TimeSpan
        | ContinuePlayingAfterSlide of pos:TimeSpan
        
        | UpdatePositionOnDatabase
        
        
        | OpenSleepTimerActionMenu
        | OpenPlaybackSpeedActionMenu
        
        
    
    let initModel startPlaying audioBook  = {
        AudioBook = audioBook; 
        CurrentAudioFile = ""
        CurrentAudioFileIndex = 0
        CurrentPosition= TimeSpan.Zero    
        CurrentDuration= TimeSpan.Zero    
        CurrentState = AudioPlayerState.Stopped
        AudioFileList = []
        IsLoading=false
        TimeUntilSleeps = None
        AudioPlayerBusy = false 
        HasPlayedBeforeDragSlider = false
        SliderIsDragging = false 
        StartPlayingOnOpen = startPlaying 
        PlaybackSpeed = 1.0
    }


    

    let init audioBook startPlaying = 
        let model = audioBook |> initModel startPlaying
        model, SideEffect.InitAudioPlayer



    let rec update msg state =
        match msg with
        | Play -> 
            let model = state |> fixModelWhenCurrentTrackGreaterThanNumberOfTracks
            { model with
                CurrentState = AudioPlayerState.Playing
                CurrentAudioFileIndex = if state.CurrentAudioFileIndex < 0 then 0 else state.CurrentAudioFileIndex }, SideEffect.Play
            
        | PlayWithoutRewind ->
            let model = state |> fixModelWhenCurrentTrackGreaterThanNumberOfTracks
            { model with CurrentState = AudioPlayerState.Playing }, SideEffect.PlayWithoutRewind
            
        | Stop ->
            state, SideEffect.Stop
        
        | Next -> 
            state, SideEffect.Next
        
        | Previous -> 
            state, SideEffect.Previous
        
        | JumpForward -> 
            state, SideEffect.JumpForward
            
        | JumpBackwards -> 
            state, SideEffect.JumpBackwards

        
        | RestoreStateFormAudioService info ->
            let newModel =
                info.AudioBook
                |> Option.map (fun ab ->
                    { state with
                        CurrentAudioFile = info.Filename
                        CurrentAudioFileIndex = info.CurrentTrackNumber - 1
                        CurrentDuration = info.Duration
                        CurrentPosition = info.Position
                        AudioFileList = info.Mp3FileList
                        AudioBook = ab
                    }
                )
                |> Option.defaultValue state
            newModel, SideEffect.None
            
        | FileListLoaded fileList -> 
            let sideEffect = SideEffect.StartAudioBookService fileList
        
            let beginNew () =
                let file,duration = fileList[0]
                let currentDuration = duration
                { state with 
                    AudioFileList = fileList
                    CurrentDuration = currentDuration
                    CurrentAudioFile = file
                    CurrentAudioFileIndex = 0
                    CurrentPosition = TimeSpan.Zero
                }, sideEffect

            match state.AudioBook.AudioBook.State.CurrentPosition with
            | None ->
                beginNew ()
            | Some cp ->
                let lastListenFile = 
                    fileList
                    |> List.indexed
                    |> List.tryFind (fun (_,(fn,_)) -> fn = cp.Filename)
                match lastListenFile with
                | None -> 
                    beginNew ()
                | Some (idx, (fn, duration)) ->
                    let currentPosition = cp.Position
                    let currentDuration = duration
                    {state with 
                        AudioFileList = fileList
                        CurrentPosition = currentPosition
                        CurrentDuration = currentDuration
                        CurrentAudioFile = fn
                        CurrentAudioFileIndex = idx
                    }, sideEffect
                    
        
     
        | OpenSleepTimerActionMenu ->
            state, SideEffect.OpenSleepTimerActionMenu
        
        | OpenPlaybackSpeedActionMenu ->
            state, SideEffect.OpenPlaybackSpeedActionMenu
        
        | StartSleepTimer sleepTime ->
            state, SideEffect.StartSleepTimer sleepTime
            
        | UpdateSleepTimer sleepTime ->
            let newModel = {state with TimeUntilSleeps = sleepTime}        
            newModel, SideEffect.None
        
        | SetPlayerStateFromExtern exstate ->
            { state with CurrentState = exstate }, SideEffect.None
            
        | UpdateTrackNumber num ->
            { state with CurrentAudioFileIndex = num - 1}, SideEffect.None
            
        | SetPlaybackSpeed speed ->
            
            { state with PlaybackSpeed = speed }, SideEffect.SetPlaybackSpeed speed
            
        | ChangeBusyState bstate -> 
            {state with IsLoading = bstate}, SideEffect.None

        | SetDragging b ->
            // slider released
            let sideEffect =
                if b then
                    SideEffect.SliderDragStarted
                elif state.SliderIsDragging && b = false && state.HasPlayedBeforeDragSlider then
                    SideEffect.SetAudioPositionAbsolute state.CurrentPosition
                else
                    SideEffect.None
                
            { state with SliderIsDragging = b; HasPlayedBeforeDragSlider = b && state.CurrentState = AudioPlayerState.Playing }, sideEffect
        
        
        | SetTrackPosition pos ->
            { state with CurrentPosition = pos }, SideEffect.None

        | UpdateScreenInformation info ->
            { state with 
                CurrentPosition = info.Position
                CurrentDuration = info.Duration
                CurrentState = info.State
                CurrentAudioFile = info.Filename
                CurrentAudioFileIndex = info.CurrentTrackNumber
                IsLoading = info.IsBusy 
            }, SideEffect.None
            
            
    // repair model, if current track is greater than the actually count of files
    // it can sometime happen. this has to leave inside until the bug is found
    and fixModelWhenCurrentTrackGreaterThanNumberOfTracks model =
        if model.CurrentAudioFileIndex > (model.AudioFileList.Length - 1) then
            {model with CurrentAudioFileIndex = model.AudioFileList.Length - 1}
        else
            model

        


    
    module SideEffects =
        
        
        let private rewindInSec state =
            async {
                match state.AudioBook.AudioBook.State.LastTimeListend with
                | Some lastTimeListend ->
                    
                    let minutesSinceLastListend =
                        DateTime.UtcNow.Subtract(lastTimeListend).TotalMinutes
                    let secondsSinceLastListend =
                        DateTime.UtcNow.Subtract(lastTimeListend).TotalSeconds

                    if (secondsSinceLastListend <= 30.0) then
                        return 0
                    else
                        let! longTimeMinutes = 
                            SystemSettings.getLongPeriodBeginsAfterInMinutes ()
                            |> Async.map float
                        if (minutesSinceLastListend >= longTimeMinutes) then
                            return! SystemSettings.getRewindWhenStartAfterLongPeriodInSec ()
                        else
                            return! SystemSettings.getRewindWhenStartAfterShortPeriodInSec ()
                | _ ->
                    return 0
            }
        
        
        let private loadFiles (model:AudioBook) =
            task {
                match model.State.DownloadedFolder with
                | None -> return None
                | Some folder ->

                let! audioFileInfo = DataBase.getAudioBookFileInfo model.Id
                match audioFileInfo with
                | None ->
                    try
                        let files =
                            Directory.EnumerateFiles(folder, "*.mp3")
                            |> Seq.toArray
                            |> Array.Parallel.map (
                                fun i ->
                                    use tfile = TagLib.File.Create(i)
                                    (i,tfile.Properties.Duration)
                            )
                            |> Array.sortBy (fun (f,_) -> f)
                            |> Array.toList
                
                        return FileListLoaded files |> Some
                    with
                    | ex ->
                        do! Notifications.showErrorMessage "Konnte Hörbuch Dateien nicht finden."
                        Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                        return None 
                | Some audioFileInfo ->
                    return Some <| FileListLoaded (audioFileInfo.AudioFiles |> List.map (fun i -> (i.FileName,i.Duration) ))
            }
        
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()
                    
                | SideEffect.InitAudioPlayer ->
                    let decideOnAudioPlayerStateCommand state =
                        task {
                            let info = AudioPlayer.globalAudioPlayerStore.Model
                            match info.AudioBook with
                            | None ->
                                return! state.AudioBook.AudioBook |> loadFiles
                            | Some infoAudioBook ->                    
                                match info.ServiceState with
                                | AudioPlayerServiceState.Started ->
                                    // if the same audio is active, restore else start a new one
                                    if infoAudioBook.AudioBook.FullName <> state.AudioBook.AudioBook.FullName then
                                        return! state.AudioBook.AudioBook |> loadFiles
                                    else
                                        return Some (RestoreStateFormAudioService info)
                                | _ ->
                                    return! state.AudioBook.AudioBook |> loadFiles
                        }
                    
                    // stop current Audioplayer and Service
                    AudioPlayer.stopAudioPlayer false
                    AudioPlayer.stopAudioService()
                    
                    //do! addAudioServiceInfoHandler()
                    let! stateMsg = decideOnAudioPlayerStateCommand state
                    match stateMsg with
                    | None -> ()
                    | Some (FileListLoaded files) ->
                        dispatch <| FileListLoaded files
                        AudioPlayer.startAudioService state.AudioBook files
                    | Some stateMsg ->
                        dispatch <| stateMsg
                        
                    if state.StartPlayingOnOpen then
                        dispatch Play
                        
                    return ()
                    
                | SideEffect.Play ->
                    match state.AudioFileList with
                    | [] ->
                        do! Notifications.showErrorMessage "Keine Dateien gefunden."
                    | _ ->
                        let file,_ = state.AudioFileList[state.CurrentAudioFileIndex]
                        let currentPosition = state.CurrentPosition
                        let! rewindInSec = rewindInSec state |> Async.map (TimeSpan.FromSeconds)
                        let newPosition =
                            let p = currentPosition - rewindInSec
                            if p < TimeSpan.Zero then TimeSpan.Zero else p
                            
                        AudioPlayer.startAudioPlayerExtern file newPosition
                        return ()
                    
                | SideEffect.PlayWithoutRewind ->
                    match state.AudioFileList with
                    | [] ->
                        do! Notifications.showErrorMessage "Keine Dateien gefunden."
                    | _ ->
                        let file,_ = state.AudioFileList[state.CurrentAudioFileIndex]
                        AudioPlayer.startAudioPlayerExtern file state.CurrentPosition
                        return ()
                    
                | SideEffect.Stop ->
                    AudioPlayer.stopAudioPlayer false
                    return ()
                    
                | SideEffect.Next ->
                    AudioPlayer.moveToNextTrack()
                    return ()
                    
                | SideEffect.Previous ->
                    AudioPlayer.moveToPreviousTrack()
                    return ()
                    
                | SideEffect.JumpForward ->
                    AudioPlayer.jumpForward()
                    return ()
                    
                | SideEffect.JumpBackwards ->
                    AudioPlayer.jumpBackwards()
                    return ()
                    
                | SideEffect.StartAudioBookService fileList ->
                    AudioPlayer.stopAudioPlayer false
                    AudioPlayer.startAudioService state.AudioBook fileList
                    return ()
                    
                | SideEffect.UpdatePositionOnDatabase ->
                    // Todo?
                    return ()
                    
                | SideEffect.OpenSleepTimerActionMenu ->
                    // Todo
                    Notifications.showToasterMessage "Todo: OpenSleepTimerActionMenu"
                    return ()
                    
                | SideEffect.OpenPlaybackSpeedActionMenu ->
                    // Todo
                    Notifications.showToasterMessage "Todo: OpenPlaybackSpeedActionMenu"
                    return ()
                    
                | SideEffect.StartSleepTimer sleepTime ->
                    AudioPlayer.startSleepTimer sleepTime
                    return ()
                    
                | SideEffect.SetPlaybackSpeed speed ->
                    AudioPlayer.setPlaybackSpeed speed
                    
                | SideEffect.SetAudioPositionAbsolute pos ->
                    AudioPlayer.setPosition pos
                    AudioPlayer.startAudioPlayer ()
                    return ()
                    
                | SideEffect.ContinuePlayingAfterSlide pos ->
                    AudioPlayer.setPosition pos
                    dispatch <| PlayWithoutRewind
                    return ()

                | SideEffect.SliderDragStarted ->
                    AudioPlayer.stopAudioPlayer false
                    return ()
                    
                | SideEffect.StopAudioBookService ->
                    AudioPlayer.stopAudioService()
                    return ()
                    
            }
    
    
    
        


    

    let getPositionAudioBookTotal (model:State) =
        if model.AudioFileList = [] || model.CurrentAudioFileIndex < 0 then
            {| Total = TimeSpan.Zero; CurrentPos = TimeSpan.Zero; Rest = TimeSpan.Zero |}
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

            {| Total = totalDuration; CurrentPos = currentTime; Rest = totalDuration - currentTime |}


    let getMinutesAndSeconds (ts:TimeSpan) =
        let minutes = Math.Floor(ts.TotalMinutes) |> int
        minutes, ts.Seconds

    let getHoursAndMinutes (ts:TimeSpan) =
        let minutes = Math.Floor(ts.TotalHours) |> int
        minutes, ts.Minutes
    

open PlayerPage
open ReactiveElmish.Avalonia
open Elmish.SideEffect

[<AutoOpen>]
module Extension =
    type Slider with
        member this.Dragging with get() =
            let t = this.GetType()
            let p = t.GetProperty("IsDragging", BindingFlags.Instance ||| BindingFlags.NonPublic)
            let v = p.GetValue(this)
            v :?> bool

type PlayerViewModel(audiobook:AudioBookItemViewModel, startPlaying) as self =
    inherit ReactiveElmishViewModel()
    
    let init () = init audiobook startPlaying
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
        
        
    do
        let disposable = AudioPlayer.globalAudioPlayerStore.Observable.Subscribe (fun state -> local.Dispatch (UpdateScreenInformation state))
        self.AddDisposable disposable
    
        
    member this.AudioBook                   = this.Bind(local, _.AudioBook)
    member this.Picture =
        this.Bind(local,
          fun s ->
              s.AudioBook.AudioBook.Picture
              |> Option.defaultValue "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
        )
    
    member this.CurrentTrackNumberString    = this.Bind(local, fun s -> $"Track: {s.CurrentAudioFileIndex + 1}")
    member this.CurrentAudioFile            = this.Bind(local, _.CurrentAudioFile)
    member this.CurrentAudioFileIndex       = this.Bind(local, _.CurrentAudioFileIndex)
    member this.CurrentPosition             = this.Bind(local, _.CurrentPosition)
    member this.CurrentPositionMs
        with get() = this.Bind(local, _.CurrentPosition.TotalMilliseconds)
        and set v = local.Dispatch (SetTrackPosition (TimeSpan.FromMilliseconds v))
        
    member this.CurrentDuration             = this.Bind(local, _.CurrentDuration)
    member this.CurrentDurationMs           = this.Bind(local, _.CurrentDuration.TotalMilliseconds)
    member this.CurrentState                = this.Bind(local, _.CurrentState)
    member this.AudioFileList               = this.Bind(local, _.AudioFileList)
    member this.IsLoading                   = this.Bind(local, _.IsLoading)
    member this.TimeUntilSleeps             = this.Bind(local, _.TimeUntilSleeps)
    member this.AudioPlayerBusy             = this.Bind(local, _.AudioPlayerBusy)
    member this.HasPlayedBeforeDragSlider   = this.Bind(local, _.HasPlayedBeforeDragSlider)
    member this.SliderIsDragging
        with get() = this.Bind(local, _.SliderIsDragging)
        and set v = local.Dispatch (SetDragging v)
        
    member this.PlaybackSpeed               = this.Bind(local, _.PlaybackSpeed)
    member this.TotalPositionString =
        this.Bind(local,
            fun s ->
                let pos = getPositionAudioBookTotal s
                $"Gesamt: {pos.CurrentPos:``hh\:mm\:ss``} / {pos.Total:``hh\:mm\:ss``}"
        )
    member this.PositionString =
        this.Bind(local,
            fun s ->
                $"{s.CurrentPosition:``hh\:mm\:ss``}/{s.CurrentDuration:``hh\:mm\:ss``}"
        )
    
    
    member this.Play()                          = local.Dispatch Play
    member this.IsPlaying                       = this.Bind(local, fun i -> i.CurrentState = AudioPlayerState.Playing)
    member this.PlayWithoutRewind()             = local.Dispatch PlayWithoutRewind
    member this.Stop()                          = local.Dispatch Stop
    member this.IsStopped                       = this.Bind(local, fun i -> i.CurrentState = AudioPlayerState.Stopped)
    member this.Next()                          = local.Dispatch Next
    member this.Previous()                      = local.Dispatch Previous
    member this.JumpForward()                   = local.Dispatch JumpForward
    member this.JumpBackwards()                 = local.Dispatch JumpBackwards
    member this.OpenSleepTimerActionMenu()      = local.Dispatch OpenSleepTimerActionMenu
    member this.OpenPlaybackSpeedActionMenu()   = local.Dispatch OpenPlaybackSpeedActionMenu
    member this.OnDraggingChanged b             = local.Dispatch (SetDragging b)
    member this.SilderValueChanged (e:RangeBaseValueChangedEventArgs) =
        match e.Source with
        | :? Slider as s ->
            local.Dispatch <| SetDragging s.Dragging
        | _ ->
            local.Dispatch <| SetDragging false
            
            
        //if not dragging then
        //local.Dispatch <| SetTrackPosition (TimeSpan.FromMilliseconds e.NewValue)
        
    static member DesignVM = new PlayerViewModel(AudioBookItemViewModel.DesignVM, false)

