module AudioPlayerPage

open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Common
open Domain
open System
open System.Threading.Tasks
open System.IO
open System.Threading
open System.Threading
open Services
open TimeSpanHelpers
open Services.DependencyServices
open Global

open AudioPlayer

    

    

    type Model = 
      { AudioBook:AudioBook 
        CurrentAudioFile: string option
        CurrentAudioFileIndex: int
        CurrentPosition: TimeSpan option  
        CurrentPositionMs: int option 
        CurrentDuration: TimeSpan option  
        CurrentDurationMs: int option
        CurrentState : AudioPlayerState
        AudioFileList: (string * int) list 
        IsLoading: bool         
        TrackPositionProcess: float
        ProgressbarValue: float         
        TimeUntilSleeps: TimeSpan option 
        AudioPlayerBusy:bool 
        HasPlayedBeforeDragSlider:bool
        SliderIsDraged:bool 
        PlaybackSpeed: float }

    type Msg = 
        | Play 
        | PlayWithoutRewind
        | PlayStarted
        | Stop
        | PlayStopped
        | NextAudioFile
        | PreviousAudioFile
        | JumpForward
        | JumpBackwards
        | RestoreStateFormAudioService of AudioPlayerInfo
        | FileListLoaded of (string * int) list
        | UpdatePostion of filename:string * position:int * duration:int
        | ProgressBarChanged of float
        | SaveCurrentPosition //of AudioBook
        | OpenSleepTimerActionMenu
        | OpenPlaybackSpeedActionMenu
        | StartSleepTimer of TimeSpan option
        | SetPlaybackSpeed of float
        | UpdateSleepTimer of TimeSpan option        
        | SetPlayerStateFromExtern of AudioPlayer.AudioPlayerState
        | UpdateTrackNumber of int
        
        | SliderDragStarted
        | SliderDragFinished

        | ChangeBusyState of bool
        | DoNothing

    

    let audioPlayer = DependencyService.Get<AudioPlayer.IAudioPlayer>()

    let pageRef = ViewRef<CustomContentPage>()
        
    
    module Commands =

        let loadFilesAsyncMsg (model:AudioBook) =
            async {
                match model.State.DownloadedFolder with
                | None -> return None
                | Some folder ->

                let! audioFileInfo = Services.DataBase.getAudioBookFileInfo model.Id
                match audioFileInfo with
                | None ->
                    try
                        let! files = 
                            asyncFunc( 
                                fun () ->  Directory.EnumerateFiles(folder, "*.mp3")
                            )
                
                        let! res =
                            asyncFunc (fun () ->
                                files 
                                |> Seq.toList 
                                |> List.map (
                                    fun i ->
                                        use tfile = TagLib.File.Create(i)
                                        (i,tfile.Properties.Duration |> fromTimeSpan)
                                )
                            )
                        return Some (FileListLoaded (res |> List.sortBy (fun (f,_) -> f)))
                    with
                    | ex ->
                        do! Common.Helpers.displayAlert ("Fehler!", "Konnte Hörbuch Dateien nicht finden.", "OK")
                        return None 
                | Some audioFileInfo ->
                    return Some <| FileListLoaded (audioFileInfo.AudioFiles |> List.map (fun i -> (i.FileName,i.Duration) ))
            }

        let loadFilesAsyncCmd =
             loadFilesAsyncMsg >> Cmd.ofAsyncMsgOption 


        let getTrackNumberFromFileName file model = 
            model.AudioFileList
            |> List.tryFindIndex (fun (f,_) -> f = file)
            |> Option.map (fun i -> i + 1)
            |> Option.defaultValue 1

        let playAudio rewindInSec model =
            (fun (dispatch:Dispatch<Msg>) -> 
                async {
                    if model.CurrentState = Playing then
                        return DoNothing
                    else
                        if model.AudioFileList.Length = 0 then
                            return DoNothing
                        else
                            let (file,_) = model.AudioFileList.[model.CurrentAudioFileIndex]
                            let currentPosition = model.CurrentPosition |> fromTimeSpanOpt
                            let rewindInMs = rewindInSec * 1000
                        
                            audioPlayer.StartAudio file (currentPosition - rewindInMs)

                            return DoNothing
                }
            ) |> Cmd.ofAsyncMsgWithInternalDispatch


        let setAudioPositionRelative value model =
            match model.CurrentPosition with
            | None -> ()            
            |  Some pos ->
                let msPos = pos |> fromTimeSpan
                let newPos = if msPos + value < 0 then 0 else msPos + value
                audioPlayer.GotToPosition (newPos) |> ignore
 
 
        let setAudioPositionAbsolute value =
            audioPlayer.GotToPosition (value) |> ignore


        let saveNewAudioBookStateCmd model =
            async {
                let! res =  model.AudioBook |> DataBase.updateAudioBookInStateFile
                match res with
                | Error e ->
                    do! Common.Helpers.displayAlert(Translations.current.Error_Saving_AudioBookState,e,"Ok")
                    return (Some Stop)
                | Ok _ ->
                    if (model.CurrentState = Stopped) then
                        return None
                    else
                        return None
            } |> Cmd.ofAsyncMsgOption


        let setCurrentPositionToAudiobookState model =
            match model.CurrentPosition, model.CurrentAudioFile with
            | Some cp, Some file ->
                let newPos = { Position = cp; Filename = file } 
                let newState = { model.AudioBook.State with CurrentPosition = Some newPos }
                let newAudioBook = { model.AudioBook with State = newState }
                { model with AudioBook = newAudioBook }
            | _,_ ->
                model

        let updateLastListendTimeInAudioBookState model =        
            let newAbState = 
                {model.AudioBook.State with LastTimeListend = Some DateTime.UtcNow }
            let newAudioBook = {model.AudioBook with State=newAbState}
            {model with AudioBook = newAudioBook }
        
    
        let sleepTimerUpdateCmd model =
            async {
                match model.TimeUntilSleeps with
                | None -> 
                    return None
                | Some t ->
                    do! Async.Sleep 1000
                    return Some DecreaseSleepTimer
            } |> Cmd.ofAsyncMsgOption

            
        

        let unsetBusyCmd = Cmd.ofMsg (ChangeBusyState false)


        let setBusyCmd = Cmd.ofMsg (ChangeBusyState true)


    let initModel audioBook = 
        { AudioBook = audioBook; 
          CurrentAudioFile = None
          CurrentAudioFileIndex = 0
          CurrentPosition= None    
          CurrentPositionMs = None
          CurrentDuration= None    
          CurrentDurationMs = None
          CurrentState = Stopped
          AudioFileList = []
          IsLoading=false
          TrackPositionProcess=0.
          ProgressbarValue = 0. 
          TimeUntilSleeps = None
          AudioPlayerBusy = false 
          HasPlayedBeforeDragSlider = false
          SliderIsDraged = false 
          PlaybackSpeed = 1.0 }


    let startAudioPlayerService abMdl fileList =
            (fun dispatch ->
                async {
                    
                    audioPlayer.StopAudio ()
                    audioPlayer.RunService abMdl fileList


                    let info = (fun info -> 
                        async {
                            dispatch (UpdatePostion (info.Filename, info.Position,info.Duration))                            
                            dispatch (SetPlayerStateFromExtern info.State)                            
                            dispatch (UpdateTrackNumber info.CurrentTrackNumber)
                            dispatch (UpdateSleepTimer info.TimeUntilSleep)
                        }
                    )   
                     

                    InformationDispatcher.audioPlayerStateInformationDispatcher.Post(InformationDispatcher.AddListener ("abPage",info))
                        
                    return Play
                }
            )|> Cmd.ofAsyncMsgWithInternalDispatch

    let init audioBook = 
        let model = audioBook |> initModel

        let addAudioServiceInfoHandler =
            (fun dispatch ->
                async {
                    let info =
                        (fun info -> 
                            async {
                                dispatch (UpdatePostion (info.Filename,info.Position,info.Duration))
                                dispatch (SetPlayerStateFromExtern info.State)                                
                                dispatch (UpdateTrackNumber info.CurrentTrackNumber)
                                dispatch (UpdateSleepTimer info.TimeUntilSleep)
                            }
                        )
                        
                    InformationDispatcher.audioPlayerStateInformationDispatcher.Post(InformationDispatcher.AddListener ("abPage",info))

                    return DoNothing
                }
            ) |> Cmd.ofAsyncMsgWithInternalDispatch


        let decideOnAudioPlayerStateCommand model =
            async {
                let! info = audioPlayer.GetCurrentState()
                match info with
                | None ->
                    
                    return! model.AudioBook |> Commands.loadFilesAsyncMsg
                | Some info ->                    
                    match info.ServiceState with
                    | Started ->
                        // if the same audio is active, restore else start a new one
                        if info.AudioBook.FullName <> model.AudioBook.FullName then
                            return! model.AudioBook |> Commands.loadFilesAsyncMsg
                        else
                            return Some (RestoreStateFormAudioService info)
                    | _ ->
                        return! model.AudioBook |> Commands.loadFilesAsyncMsg
            } |> Cmd.ofAsyncMsgOption

        let cmds = 
            Cmd.batch [
                model |> decideOnAudioPlayerStateCommand
                addAudioServiceInfoHandler
                Commands.setBusyCmd
            ]
                         
        model, cmds



    let rec update msg model =
        match msg with
        | Play -> 
            model |> onPlayMsg
        | PlayWithoutRewind ->
            model |> onPlayWithoutRewindMsg
        | PlayStarted -> 
            model |> onPlayStartedMsg          
        | Stop ->
            model |> onStopMsg
        | NextAudioFile -> 
            model |> onNextAudioFileMsg
        | PreviousAudioFile -> 
            model |> onPreviousAudioFileMsg
        | JumpForward -> 
            model |> onJumpForwardMsg            
        | JumpBackwards -> 
            model |> onJumpBackwardsMsg
        | RestoreStateFormAudioService info ->
            model |> onRestoreStateFormAudioService info
        | FileListLoaded fileList -> 
            model |> onFileListLoadedMsg fileList
        | UpdatePostion (filename, position, duration) -> 
            model |> onUpdatePositionMsg (position, duration)
        | ProgressBarChanged e -> 
            model |> onProgressBarChangedMsg e
        | SaveCurrentPosition  ->
            model |> onSaveCurrentPosition     
        | OpenSleepTimerActionMenu ->
            model |> onOpenSleepTimerActionMenu  
        | OpenPlaybackSpeedActionMenu ->
            model |> onOpenPlaybackSpeedActionMenu
        | StartSleepTimer sleepTime ->
            model |> onSetSleepTime sleepTime
        | UpdateSleepTimer sleepTime ->
            model |> onUpdateSleepTimer sleepTime
        | SetPlayerStateFromExtern state ->
            model |> onSetPlayerStateFromExtern state
        | UpdateTrackNumber num ->
            model |> onUpdateTrackNumber num
        | SetPlaybackSpeed speed ->
            model |> onSetPlaybackSpeed speed
            
        | ChangeBusyState state -> 
            model |> onChangeBusyState state

        | PlayStopped | DoNothing -> 
            model, Cmd.none

        | SliderDragStarted ->
            let isPlaying = model.CurrentState = AudioPlayerState.Playing
            let newModel = { model with HasPlayedBeforeDragSlider = isPlaying; SliderIsDraged = true  }
            newModel, Cmd.ofMsg Stop
        | SliderDragFinished ->
            let cmd = 
                if model.HasPlayedBeforeDragSlider then
                    Cmd.ofMsg PlayWithoutRewind
                else
                    Cmd.none
            let newModel = { model with HasPlayedBeforeDragSlider = false; SliderIsDraged = false }
            match model.CurrentPositionMs with
            | None -> ()
            | Some newPos -> 
                Commands.setAudioPositionAbsolute newPos
            newModel, cmd

    and onRestoreStateFormAudioService info model =
        let newModel = 
            { model with
                CurrentAudioFile = Some info.Filename
                CurrentAudioFileIndex = info.CurrentTrackNumber - 1
                CurrentDuration = Some (info.Duration |> toTimeSpan)
                CurrentPosition = Some (info.Position  |> toTimeSpan)
                CurrentDurationMs = Some info.Duration
                CurrentPositionMs = Some info.Position
                AudioFileList = info.Mp3FileList
                AudioBook = info.AudioBook }

        newModel, Commands.unsetBusyCmd
    
    and onUpdateTrackNumber num model =
        { model with CurrentAudioFileIndex = num - 1}, Cmd.none
    
    
    and onSetPlayerStateFromExtern state model =
        { model with CurrentState = state }, Cmd.none
        


    and onOpenSleepTimerActionMenu model =
        
        let openSleepTimerActionMenu () =            
            async {
                let buttons = [|
                    
                    yield (Translations.current.Off,   (fun () -> StartSleepTimer None)())    
                    yield ("30 sek",(fun () -> StartSleepTimer (Some (TimeSpan.FromSeconds(30.) ))) ())
                    yield ("5 min", (fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(5.) ))) ())
                    yield ("15 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(15.) ))) ())
                    yield ("30 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(30.) ))) ())
                    yield ("45 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(45.) ))) ())
                    yield ("60 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(60.) ))) ())
                    yield ("75 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(75.) ))) ())
                    yield ("90 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(90.) ))) ())
                    yield ("105 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(105.) ))) ())
                    yield ("120 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(120.) ))) ())
                        
                |]
                return! Helpers.displayActionSheet (Some Translations.current.Select_Sleep_Timer) (Some Translations.current.Cancel) buttons
            } |> Cmd.ofAsyncMsgOption

        model, (openSleepTimerActionMenu ())



    and onOpenPlaybackSpeedActionMenu model =
        
        let openPlaybackSpeedActionMenu () =            
            async {
                let buttons = [|
                    
                    yield ("0,70x",(fun () -> SetPlaybackSpeed 0.70) ())
                    yield ("0,75x", (fun () -> SetPlaybackSpeed 0.75) ())
                    yield ("0,80x",(fun () -> SetPlaybackSpeed 0.80) ())
                    yield ("0,85x",(fun () -> SetPlaybackSpeed 0.85) ())
                    yield ("0,90x",(fun () -> SetPlaybackSpeed 0.90) ())
                    yield ("0,95x",(fun () -> SetPlaybackSpeed 0.95) ())
                    yield ("1,00x",(fun () -> SetPlaybackSpeed 1.00) ())
                    yield ("1,05x",(fun () -> SetPlaybackSpeed 1.05) ())
                    yield ("1,10x",(fun () -> SetPlaybackSpeed 1.10) ())
                    yield ("1,15x",(fun () -> SetPlaybackSpeed 1.15) ())
                    yield ("1,20x",(fun () -> SetPlaybackSpeed 1.20) ())
                    yield ("1,25x",(fun () -> SetPlaybackSpeed 1.25) ())
                    yield ("1,30x",(fun () -> SetPlaybackSpeed 1.30) ())
                   
                        
                |]
                return! Helpers.displayActionSheet (Some Translations.current.SelectPlaybackSpeed) (Some Translations.current.Cancel) buttons
            } |> Cmd.ofAsyncMsgOption

        model, (openPlaybackSpeedActionMenu ())
    
    and onSetPlaybackSpeed speed model =
        speed |> audioPlayer.SetPlaybackSpeed 
        { model with PlaybackSpeed = speed }, Cmd.none

    
    // repair model, if current track is greater than the actually count of files
    // it can sometime happen. this has to leave inside until the bug is found
    and fixModelWhenCurrentTrackGreaterThanNumberOfTracks model =
        if model.CurrentAudioFileIndex > (model.AudioFileList.Length - 1) then
            {model with CurrentAudioFileIndex = model.AudioFileList.Length - 1}
        else
            model

    and onPlayBaseMsg rewindInSec model =
        let playAudioCmd = model |> Commands.playAudio rewindInSec
        
        let newModel = 
            {model with CurrentState = Playing}
            |> Commands.setCurrentPositionToAudiobookState
            |> Commands.updateLastListendTimeInAudioBookState
        
        newModel, Cmd.batch [ playAudioCmd; newModel |> Commands.saveNewAudioBookStateCmd ]

    and onPlayMsg model = 
        let model = model |> fixModelWhenCurrentTrackGreaterThanNumberOfTracks
        // determinate Rewind
        let rewindInSec =
            async {
                match model.CurrentPositionMs,model.AudioBook.State.LastTimeListend with
                | _, Some lastTimeListend ->
                    
                    let minutesSinceLastListend =
                        (DateTime.UtcNow.Subtract(lastTimeListend)).TotalMinutes
                    let secondsSinceLastListend =
                        (DateTime.UtcNow.Subtract(lastTimeListend)).TotalSeconds

                    if (secondsSinceLastListend <= 30.0) then
                        return 0
                    else
                        let! longTimeMinutes = 
                            Services.SystemSettings.getLongPeriodBeginsAfterInMinutes ()
                            |> Async.map float
                        if (minutesSinceLastListend >= longTimeMinutes) then
                            return! Services.SystemSettings.getRewindWhenStartAfterLongPeriodInSec ()
                        else
                            return! Services.SystemSettings.getRewindWhenStartAfterShortPeriodInSec ()
                | _, _ ->
                    return 0
                
            } |> Async.RunSynchronously
            
        model |> onPlayBaseMsg rewindInSec


    and onPlayWithoutRewindMsg model =
        let model = model |> fixModelWhenCurrentTrackGreaterThanNumberOfTracks
        model |> onPlayBaseMsg 0


    and onPlayStartedMsg model =
        { model with AudioPlayerBusy = false }, Cmd.none


    and onStopMsg model =
        audioPlayer.StopAudio()
        //let currentAudioBook = AudioBookItemProcessor.getAudioBookItem model.AudioBook.FullName
        //let newModel =
        //    match currentAudioBook with
        //    | None -> model
        //    | Some ca -> {model with AudioBook =ca.AudioBook}
            
            
        model, Cmd.none 

    
    and onNextAudioFileMsg model =
        audioPlayer.MoveForward()
        model, Cmd.none
        


    and onPreviousAudioFileMsg model =
        audioPlayer.MoveBackward()
        model, Cmd.none

    and onJumpForwardMsg model =
        audioPlayer.JumpForward ()
        model, Cmd.none


    and onJumpBackwardsMsg model =
        audioPlayer.JumpBackward ()
        model, Cmd.none


    and onFileListLoadedMsg fileList model =
        
        let cmd = Cmd.batch [startAudioPlayerService model.AudioBook fileList; Commands.unsetBusyCmd]
        
        let beginNew () =
            let (fn,duration) = fileList.[0]
            let currentDuration = duration |> toTimeSpan
            let currentDurationMs = duration
            {model with 
                AudioFileList = fileList
                CurrentDuration = Some currentDuration
                CurrentDurationMs = Some currentDurationMs
                CurrentAudioFile = Some fn
                CurrentAudioFileIndex = 0
                CurrentPosition = None
            }, cmd

        match model.AudioBook.State.CurrentPosition with
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
                let currentPositionMs = cp.Position |> fromTimeSpan
                let currentDuration = duration |> toTimeSpan
                let currentDurationMs = duration
                {model with 
                    AudioFileList = fileList
                    CurrentPosition = Some currentPosition
                    CurrentPositionMs = Some currentPositionMs
                    CurrentDuration = Some currentDuration
                    CurrentDurationMs = Some currentDurationMs
                    CurrentAudioFile = Some fn
                    CurrentAudioFileIndex = idx
                }, cmd


    and onUpdatePositionMsg (position, duration) model =
        if model.SliderIsDraged then
            model, Cmd.none
        else
            let trackProcess = 
                if (duration = 0) then 0.
                else
                    (position |> float) / (duration |> float)

            {model with 
                CurrentPosition = Some (position |> toTimeSpan)
                CurrentPositionMs = Some position
                CurrentDuration = Some (duration |> toTimeSpan)
                CurrentDurationMs = Some duration
                TrackPositionProcess = trackProcess
            }, Cmd.none


    and onProgressBarChangedMsg e model =
        if model.SliderIsDraged then
            match model.CurrentDurationMs with
            | Some currentMs ->
                let newPos = ((currentMs |> float) * e) |> int
                let newModel =
                    {model with 
                        CurrentPosition = Some (newPos |> toTimeSpan)
                        CurrentPositionMs = Some newPos                        
                    }
                {newModel with ProgressbarValue = e}, Cmd.none
            | None ->
                model,Cmd.none
        else
            model,Cmd.none
                

    
    and onSaveCurrentPosition model =
        let newModel =model |> Commands.setCurrentPositionToAudiobookState
        newModel, Cmd.none


    and onSetSleepTime sleepTime model =            
        audioPlayer.SetSleepTimer sleepTime
        model, Cmd.none


    and onUpdateSleepTimer sleepTime model =
        let newModel = {model with TimeUntilSleeps = sleepTime}        
        newModel, Cmd.none


    and onChangeBusyState state model =
        {model with IsLoading = state}, Cmd.none



    let getPositionAudioBookTotal (model:Model) =
        if model.AudioFileList = [] || model.CurrentAudioFileIndex < 0 then
            {| Total = 0; CurrentPos = 0; Rest = 0 |}
        else
            let totalDuration =
                model.AudioFileList
                |> List.sumBy (snd)

            let durationUntilCurrentTrack =
                model.AudioFileList
                |> List.take model.CurrentAudioFileIndex
                |> List.sumBy (snd)

            let currentPos = (model.CurrentPositionMs |> Option.defaultValue 0)

            let currentTimeInMs = currentPos + durationUntilCurrentTrack

            {| Total = totalDuration; CurrentPos = currentTimeInMs; Rest = totalDuration - currentTimeInMs |}


    let getMinutesAndSeconds ms =
        let ts = TimeSpan.FromMilliseconds(ms |> float)
        let minutes = Math.Floor(ts.TotalMinutes) |> int
        minutes, ts.Seconds

    let getHoursAndMinutes ms =
        let ts = TimeSpan.FromMilliseconds(ms |> float)
        let minutes = Math.Floor(ts.TotalHours) |> int
        minutes, ts.Minutes

    let view (model: Model) dispatch =
        
        let title = model.AudioBook.FullName        
        
        let currentTrackString = 
            if not model.IsLoading then
                let numCurrentTrack = model.CurrentAudioFileIndex + 1
                let numAllTracks = model.AudioFileList.Length
                sprintf "Track: %i %s %i" numCurrentTrack Translations.current.Of numAllTracks
            else
                " ... "

        let currentTimeString = 
            if not model.IsLoading then
                let currentPos = (model.CurrentPosition |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")
                let currentDuration = (model.CurrentDuration |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")
                sprintf "%s %s %s" currentPos Translations.current.Of currentDuration
            else
                " ... "

        View.ContentPage(
          title=Translations.current.AudioPlayerPage,
          backgroundColor = Consts.backgroundColor,
          content = 
            View.Grid(padding = Thickness 20.,
                horizontalOptions = LayoutOptions.Fill,
                verticalOptions = LayoutOptions.Fill,                
                rowdefs = [ Star; Auto; Auto; Auto ],
                
                children = [

                    yield dependsOn model.AudioBook.Picture (fun model picture ->
                        View.Image(
                            source= 
                                match picture with
                                | None -> Image.fromPath "AudioBookPlaceholder_Dark.png"
                                | Some p -> Image.fromPath p
                                ,
                            horizontalOptions=LayoutOptions.Fill,
                            verticalOptions=LayoutOptions.Fill,
                            aspect=Aspect.AspectFit
                            
                            ).Row(0)
                    )
                    
                    let totalTimes = getPositionAudioBookTotal model
                    let (m,s) = totalTimes.Rest |> getHoursAndMinutes
                    let restStr = sprintf "insgesamt noch %i h %i min übrig" m s

                    yield dependsOn 
                        (title,currentTrackString,currentTimeString,restStr) 
                        (fun model (title,currentTrackString,currentTimeString,restStr) ->
                            View.Grid(
                                horizontalOptions = LayoutOptions.Fill,
                                verticalOptions = LayoutOptions.Fill,                
                                rowdefs = [ Auto; Auto; Auto; Auto ],
                                children = [
                                    
                                    (Controls.primaryTextColorLabel 17. (title)).Row(0)
                                    (Controls.primaryTextColorLabel 15. (currentTrackString)).Row(1).HorizontalOptions(LayoutOptions.Center)
                                    (Controls.primaryTextColorLabel 13. (currentTimeString)).Row(2).HorizontalOptions(LayoutOptions.Center)
                                    (Controls.primaryTextColorLabel 13. (restStr)).Row(3).HorizontalOptions(LayoutOptions.Center)
                                ]
                            ).Row(1)
                    )
                    
                    
                    
                    let runIfNotBusy (cmd:(unit->unit)) =
                        if not model.AudioPlayerBusy 
                        then cmd
                        else (fun () -> ())
                        
                           

                    yield View.Grid(
                        coldefs=[Star;Star;Star;Star;Star],
                        rowdefs=[Auto;Auto ],
                        children=[
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch PreviousAudioFile) |> runIfNotBusy) 25. true "\uf048").Column(0).Row(0)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch JumpBackwards) |> runIfNotBusy) 25. true "\uf04a").Column(1).Row(0)

                            match model.CurrentState with
                            | Stopped ->
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch Play) 50. false "\uf144").Column(2).Row(0)
                            | Playing ->
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch Stop) 50. false "\uf28b").Column(2).Row(0)

                                
                            
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch JumpForward) |> runIfNotBusy) 25. true "\uf04e").Column(3).Row(0)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch NextAudioFile) |> runIfNotBusy) 25. true "\uf051").Column(4).Row(0)
                            
                            yield (View.Slider(
                                    value=model.TrackPositionProcess,
                                    minimumMaximum = (0.,1.),
                                    horizontalOptions = LayoutOptions.Fill,                                    
                                    valueChanged= (fun e -> dispatch (ProgressBarChanged e.NewValue)),
                                    created = (fun slider ->
                                        slider.DragStarted.Add(fun _ -> dispatch SliderDragStarted)
                                        slider.DragCompleted.Add(fun _ -> dispatch SliderDragFinished)
                                    )
                                    
                                  )).ColumnSpan(5).Row(1)

                        ]).Row(2)
                    
                    yield View.StackLayout(orientation=StackOrientation.Horizontal,
                            children=[
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch OpenSleepTimerActionMenu) 30. true "\uf017")

                                match model.TimeUntilSleeps with
                                | None -> ()
                                | Some tus ->
                                    let formatedTus =
                                        sprintf "%02i:%02i" (tus.TotalMinutes |> int) tus.Seconds
                                    yield (Controls.primaryTextColorLabel 25. formatedTus)
                            ]
                        ).Row(3)

                    yield View.StackLayout(
                        orientation=StackOrientation.Horizontal,
                        horizontalOptions=LayoutOptions.End,
                        
                        children=[
                            let formatedSpeed =
                                sprintf "%sx" (model.PlaybackSpeed.ToString("0.00"))
                            yield (Controls.primaryTextColorLabel 20. formatedSpeed)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch OpenPlaybackSpeedActionMenu) 30. true "\uf3fd")
                            
                            
                        ]
                            ).Row(3)
                    
                    if model.IsLoading then 
                        yield Common.createBusyLayer().RowSpan(4)
                ]
            )
          
          )

    let viewSmall openPlayerPageCommand (model: Model) dispatch =
        View.Grid(
            coldefs=[Auto; Star; Auto],
            rowdefs=[Auto; Absolute 2.0],
            backgroundColor=Consts.cardColor,
            gestureRecognizers = [
                View.TapGestureRecognizer(command=openPlayerPageCommand)
            ],
            children = [
                let currentPos = (model.CurrentPosition |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")


                yield View.Image(
                    source=
                        match model.AudioBook.Picture with
                        | None -> Image.fromPath "AudioBookPlaceholder_Dark.png"
                        | Some p -> Image.fromPath p
                        ,
                    horizontalOptions=LayoutOptions.Fill,
                    verticalOptions=LayoutOptions.Fill,
                    aspect=Aspect.AspectFit,
                    width = 40.,
                    height = 40.,
                    margin=Thickness(2.,2.,2.,2.),
                    backgroundColor=Consts.cardColor
                ).Column(0)

                yield View.Grid(
                    coldefs=[Star],
                    rowdefs=[Auto; Absolute 14.0],
                    rowSpacing=0.0,
                    horizontalOptions=LayoutOptions.Fill,
                    children = [
                        (Controls.tickerBand 20.0 model.AudioBook.FullName)
                            .Column(0)
                            .Row(0)

                        if not model.IsLoading then
                            let totalTimes = getPositionAudioBookTotal model
                            let (m,s) = totalTimes.Rest |> getHoursAndMinutes
                            let restStr = sprintf "insgesamt noch %i h %i min übrig" m s
                            (Controls.primaryTextColorLabel 12. restStr)
                                .Column(0)
                                .Row(1)
                            
                    ]
                ).Column(1)
                
                match model.CurrentState with
                | Stopped ->
                    yield (Controls.primaryColorSymbolLabelWithTapCommandRightAlign (fun () -> dispatch Play) 35. false "\uf144")
                        .Column(2)
                        .Row(0)
                        .With(margin=Thickness(5.,0.,5.,0.))
                | Playing ->
                    yield (Controls.primaryColorSymbolLabelWithTapCommandRightAlign (fun () -> dispatch Stop) 35. false "\uf28b")
                        .Column(2)
                        .Row(0)
                        .With(margin=Thickness(5.,0.,5.,0.))

                yield View.BoxView(color=Consts.backgroundColor,horizontalOptions=LayoutOptions.Fill)
                    .Row(1)
                    .ColumnSpan(3)

            ]
        )
    
