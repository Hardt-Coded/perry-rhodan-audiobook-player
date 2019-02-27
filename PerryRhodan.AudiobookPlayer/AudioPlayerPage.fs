module AudioPlayerPage

open Fabulous
open Fabulous.Core
open Fabulous.DynamicViews
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
open AudioPlayerState

    

    

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
        AudioPlayerBusy:bool }

    type Msg = 
        | Play 
        | PlayStarted
        | Stop
        | PlayStopped
        | NextAudioFile
        | PreviousAudioFile
        | JumpForward
        | JumpBackwards
        | RestoreStateFormAudioService of AudioPlayerInfo
        | FileListLoaded of (string * int) list
        | UpdatePostion of position:int * duration:int
        | ProgressBarChanged of float
        | SaveCurrentPosition //of AudioBook
        | OpenSleepTimerActionMenu
        | StartSleepTimer of TimeSpan option
        | DecreaseSleepTimer
        | SetPlayerStateFromExtern of AudioPlayerState.AudioPlayerState
        | UpdateTrackNumber of int
        
        | ChangeBusyState of bool
        | DoNothing

    type ExternalMsg =
        | GotoMainPage
        | GotoBrowserPage


    let audioPlayer = DependencyService.Get<AudioPlayerState.IAudioPlayer>()


        

    

    let loadFilesAsyncMsg (model:AudioBook) =
        async {
            match model.State.DownloadedFolder with
            | None -> return None
            | Some folder ->
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
        }

    let loadFilesAsyncCmd =
         loadFilesAsyncMsg >> Cmd.ofAsyncMsgOption 


    let getTrackNumberFromFileName file model = 
        model.AudioFileList
        |> List.tryFindIndex (fun (f,_) -> f = file)
        |> Option.map (fun i -> i + 1)
        |> Option.defaultValue 1

    let playAudio model =
        (fun (dispatch:Dispatch<Msg>) -> 
            async {
                //let audioPlayer = DependencyService.Get<Services.IAudioPlayer>()
                if model.CurrentState = Playing then
                    return DoNothing
                else
                    if model.AudioFileList.Length = 0 then
                        return DoNothing
                    else
                        let (file,_) = model.AudioFileList.[model.CurrentAudioFileIndex]
                        let currentPosition =model.CurrentPosition |> fromTimeSpanOpt
                        
                        audioPlayer.StartAudio file currentPosition

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
            let! res =  model.AudioBook |> FileAccess.updateAudioBookInStateFile
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
          AudioPlayerBusy = false }


    let startAudioPlayerService abMdl fileList =
            (fun dispatch ->
                async {
                    //do! audioPlayer.StopService()
                    audioPlayer.StopAudio ()
                    audioPlayer.RunService abMdl fileList


                    let info = (fun info -> 
                        async {
                            dispatch (UpdatePostion (info.Position,info.Duration))                            
                            dispatch (SetPlayerStateFromExtern info.State)                            
                            dispatch (UpdateTrackNumber info.CurrentTrackNumber)
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
                                dispatch (UpdatePostion (info.Position,info.Duration))
                                dispatch (SetPlayerStateFromExtern info.State)                                
                                dispatch (UpdateTrackNumber info.CurrentTrackNumber)
                            
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
                    
                    return! model.AudioBook |> loadFilesAsyncMsg
                | Some info ->
                    match info.ServiceState with
                    | Started ->
                        return Some (RestoreStateFormAudioService info)
                    | _ ->
                        return! model.AudioBook |> loadFilesAsyncMsg
            } |> Cmd.ofAsyncMsgOption

        let cmds = 
            Cmd.batch [
                model |> decideOnAudioPlayerStateCommand
                addAudioServiceInfoHandler
                setBusyCmd
            ]
                         
        model, cmds


    let rec update msg model =
        match msg with
        | Play -> 
            model |> onPlayMsg
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
        | UpdatePostion (position, duration) -> 
            model |> onUpdatePositionMsg (position, duration)
        | ProgressBarChanged e -> 
            model |> onProgressBarChangedMsg e
        | SaveCurrentPosition  ->
            model |> onSaveCurrentPosition     
        | OpenSleepTimerActionMenu ->
            model |> onOpenSleepTimerActionMenu        
        | StartSleepTimer sleepTime ->
            model |> onStartSleepTimer sleepTime            
        | DecreaseSleepTimer ->
            model |> onUpdateSleepTimerMsg 
        | SetPlayerStateFromExtern state ->
            model |> onSetPlayerStateFromExtern state
        | UpdateTrackNumber num ->
            model |> onUpdateTrackNumber num
            
        | ChangeBusyState state -> 
            model |> onChangeBusyState state

        | PlayStopped | DoNothing -> 
            model, Cmd.none, None


    and onRestoreStateFormAudioService info model =
        let newModel = 
            { model with
                CurrentAudioFile = Some info.Filename
                CurrentAudioFileIndex = info.CurrentTrackNumber - 1
                CurrentDuration = Some (info.Duration |> toTimeSpan)
                CurrentPosition = Some (info.Position  |> toTimeSpan)
                CurrentDurationMs = Some info.Duration
                CurrentPositionMs = Some info.Position
                AudioBook = info.AudioBook }

        newModel, model.AudioBook |> loadFilesAsyncCmd, None
    
    and onUpdateTrackNumber num model =
        { model with CurrentAudioFileIndex = num - 1}, Cmd.none, None
    
    
    and onSetPlayerStateFromExtern state model =
        { model with CurrentState = state }, Cmd.none, None
        


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
                        
                |]
                return! Helpers.displayActionSheet (Some Translations.current.Select_Sleep_Timer) (Some Translations.current.Cancel) buttons
            } |> Cmd.ofAsyncMsgOption

        model, (openSleepTimerActionMenu ()), None
    
    
    and onPlayMsg model = 
        let playAudioCmd = model |> playAudio
        
        let newModel = 
            {model with CurrentState = Playing}
            |> setCurrentPositionToAudiobookState
            |> updateLastListendTimeInAudioBookState
        
        newModel, Cmd.batch [ playAudioCmd; newModel |> saveNewAudioBookStateCmd ], None


    and onPlayStartedMsg model =
        { model with AudioPlayerBusy = false }, Cmd.none, None


    and onStopMsg model =
        audioPlayer.StopAudio()
        model, Cmd.none , None

    
    and onNextAudioFileMsg model =
        audioPlayer.MoveForward()
        model, Cmd.none, None
        


    and onPreviousAudioFileMsg model =
        audioPlayer.MoveBackward()
        model, Cmd.none, None

    and onJumpForwardMsg model =
        audioPlayer.JumpForward ()
        model, Cmd.none, None


    and onJumpBackwardsMsg model =
        audioPlayer.JumpBackward ()
        model, Cmd.none, None


    and onFileListLoadedMsg fileList model =
        
        let cmd = Cmd.batch [startAudioPlayerService model.AudioBook fileList; unsetBusyCmd]
        
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
            }, cmd, None

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
                }, cmd, None


    and onUpdatePositionMsg (position, duration) model =
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
        }, Cmd.none, None


    and onProgressBarChangedMsg e model =
        let min = model.TrackPositionProcess - 0.3
        let max = model.TrackPositionProcess + 0.3
        if (e < min || e > max ) then
            if model.CurrentDurationMs.IsSome then
                let newPos = ((model.CurrentDurationMs.Value |> float) * e) |> int
                setAudioPositionAbsolute  newPos
                let newModel =
                    {model with 
                        CurrentPosition = Some (newPos |> toTimeSpan)
                        CurrentPositionMs = Some newPos                        
                    }
                {newModel with ProgressbarValue = e}, Cmd.none, None
            else
                model,Cmd.none, None
        else
            model,Cmd.none, None
                
           
        


    and onUpdateSleepTimerMsg model =
        match model.TimeUntilSleeps with
        | None ->
            model, Cmd.none, None
        | Some t ->
            let sleepTime = t.Subtract(TimeSpan.FromSeconds(1.))
            if sleepTime <= TimeSpan.Zero then
                {model with TimeUntilSleeps = None},Cmd.ofMsg Stop, None
            else
                let newModel = {model with TimeUntilSleeps = Some sleepTime}
                newModel, newModel |> sleepTimerUpdateCmd, None

    
    and onSaveCurrentPosition model =
        let newModel =model |> setCurrentPositionToAudiobookState
        newModel, Cmd.none, None


    and onStartSleepTimer sleepTime model =
        let newModel = {model with TimeUntilSleeps = sleepTime}
        match model.TimeUntilSleeps with
        | None ->
            
            newModel, newModel |> sleepTimerUpdateCmd, None
        | Some _ ->
            newModel, Cmd.none, None


    and onChangeBusyState state model =
        {model with IsLoading = state}, Cmd.none, None



    let view (model: Model) dispatch =
        
        let title = model.AudioBook.FullName        
        
        let currentTrackString = 
            let numCurrentTrack = model.CurrentAudioFileIndex + 1
            let numAllTracks = model.AudioFileList.Length
            sprintf "Track: %i %s %i" numCurrentTrack Translations.current.Of numAllTracks

        let currentTimeString = 
            let currentPos = (model.CurrentPosition |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")
            let currentDuration = (model.CurrentDuration |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")
            sprintf "%s %s %s" currentPos Translations.current.Of currentDuration

        View.ContentPage(
          title=Translations.current.AudioPlayerPage,useSafeArea=true,
          backgroundColor = Consts.backgroundColor,
          content = 
            View.Grid(padding = 20.,
                horizontalOptions = LayoutOptions.Fill,
                verticalOptions = LayoutOptions.Fill,                
                rowdefs = [ box "*"; box "*"; box "*"; box "auto" ],
                
                children = [

                    yield dependsOn 
                        (title,currentTrackString,currentTimeString) 
                        (fun model (title,currentTrackString,currentTimeString) ->
                            View.Grid(
                                horizontalOptions = LayoutOptions.Fill,
                                verticalOptions = LayoutOptions.Fill,                
                                rowdefs = [ box "auto"; box "auto"; box "auto" ],
                                children = [
                                    yield (Controls.primaryTextColorLabel 25. (title)).GridRow(0)
                                    yield (Controls.primaryTextColorLabel 30. (currentTrackString)).GridRow(1).HorizontalOptions(LayoutOptions.Center)
                                    yield (Controls.primaryTextColorLabel 30. (currentTimeString)).GridRow(2).HorizontalOptions(LayoutOptions.Center)
                                ]
                            ).GridRow(0)
                    )
                    
                    yield View.Image(
                        source=
                            match model.AudioBook.Picture with
                            | None -> "AudioBookPlaceholder_Dark.png"
                            | Some p -> p
                            ,
                        horizontalOptions=LayoutOptions.Fill,
                        verticalOptions=LayoutOptions.Fill,
                        aspect=Aspect.AspectFit
                        
                        ).GridRow(1)
                    
                    let runIfNotBusy (cmd:(unit->unit)) =
                        if not model.AudioPlayerBusy 
                        then cmd
                        else (fun () -> ())
                        
                           

                    yield View.Grid(
                        coldefs=[box "*";box "*";box "*";box "*";box "*"],
                        rowdefs=[box "*";box "*" ],
                        children=[
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch PreviousAudioFile) |> runIfNotBusy) 30. true "\uf048").GridColumn(0).GridRow(0)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch JumpBackwards) |> runIfNotBusy) 30. true "\uf04a").GridColumn(1).GridRow(0)

                            match model.CurrentState with
                            | Stopped ->
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch Play) 60. false "\uf144").GridColumn(2).GridRow(0)
                            | Playing ->
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch Stop) 60. false "\uf28b").GridColumn(2).GridRow(0)
                                
                            
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch JumpForward) |> runIfNotBusy) 30. true "\uf04e").GridColumn(3).GridRow(0)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch NextAudioFile) |> runIfNotBusy) 30. true "\uf051").GridColumn(4).GridRow(0)
                            
                            yield (View.Slider(
                                    value=model.TrackPositionProcess,
                                    minimumMaximum = (0.,1.),
                                    //minimum = 0., maximum = 1., 
                                    horizontalOptions = LayoutOptions.Fill,
                                    valueChanged= (fun e -> dispatch (ProgressBarChanged e.NewValue))
                                  )).GridColumnSpan(5).GridRow(1)

                        ]).GridRow(2)
                    
                    yield View.StackLayout(orientation=StackOrientation.Horizontal,
                            children=[
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch OpenSleepTimerActionMenu) 45. true "\uf017")

                                match model.TimeUntilSleeps with
                                | None -> ()
                                | Some tus ->
                                    let formatedTus =
                                        sprintf "%02i:%02i" (tus.TotalMinutes |> int) tus.Seconds
                                    yield (Controls.primaryTextColorLabel 30. formatedTus)
                            ]
                        ).GridRow(3)
                    
                    if model.IsLoading then 
                        yield Common.createBusyLayer().GridRowSpan(4)
                ]
            )
          
          )

    let viewSmall openPlayerPageCommand (model: Model) dispatch =
        View.Grid(
            coldefs=[box "auto"; box "*"; box "auto"; box "auto"; box "auto"],
            backgroundColor=Consts.cardColor,
            gestureRecognizers = [
                View.TapGestureRecognizer(command=openPlayerPageCommand)
            ],
            children = [
                let currentPos = (model.CurrentPosition |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")

                yield View.Image(
                    source=
                        match model.AudioBook.Picture with
                        | None -> "AudioBookPlaceholder_Dark.png"
                        | Some p -> p
                        ,
                    horizontalOptions=LayoutOptions.Fill,
                    verticalOptions=LayoutOptions.Fill,
                    aspect=Aspect.AspectFit,
                    widthRequest = 30.,
                    heightRequest = 30.,
                    margin=Thickness(5.,3.,5.,3.)
                    
                    ).GridColumn(0)

                yield (Controls.primaryTextColorLabel 12. (model.AudioBook.FullName ))
                    .GridColumn(1)
                    .With(horizontalOptions=LayoutOptions.Start, margin=Thickness(5.,0.,0.,0.))
                    

                yield (Controls.primaryTextColorLabel 12. (sprintf "%i/%i" (model.CurrentAudioFileIndex + 1) model.AudioFileList.Length ))
                    .GridColumn(2)
                yield (Controls.primaryTextColorLabel 12. (sprintf "%s" currentPos))
                    .GridColumn(3)
                match model.CurrentState with
                | Stopped ->
                    yield (Controls.primaryColorSymbolLabelWithTapCommandRightAlign (fun () -> dispatch Play) 30. false "\uf144")
                        .GridColumn(4)
                        .GridRow(0)
                        .With(margin=Thickness(5.,3.,5.,3.))
                | Playing ->
                    yield (Controls.primaryColorSymbolLabelWithTapCommandRightAlign (fun () -> dispatch Stop) 30. false "\uf28b")
                        .GridColumn(4)
                        .GridRow(0)
                        .With(margin=Thickness(5.,3.,5.,3.))

            ]
        )
    
