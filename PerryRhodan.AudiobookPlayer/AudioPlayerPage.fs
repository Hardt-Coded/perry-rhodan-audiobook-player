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


    

    type PlayerState =
        | Stopped        
        | Playing

    type Model = 
      { AudioBook:AudioBook 
        CurrentAudioFile: string option
        CurrentAudioFileIndex: int
        CurrentPosition: TimeSpan option  
        CurrentPositionMs: int option 
        CurrentDuration: TimeSpan option  
        CurrentDurationMs: int option
        CurrentState : PlayerState
        AudioFileList: (string * int) list 
        IsLoading: bool 
        CurrentPlayingStateUpdateTimer:Timer option 
        TrackPositionProcess: float
        ProgressbarValue: float         
        TimeUntilSleeps: TimeSpan option 
        AudioPlayerBusy:bool }

    type Msg = 
        | Play 
        | PlayStarted of Timer
        | Stop
        | PlayStopped
        | NextAudioFile
        | PreviousAudioFile
        | JumpForward
        | JumpBackwards
        | FileListLoaded of (string * int) list
        | UpdatePostion of position:int * duration:int
        | ProgressBarChanged of float
        | SaveCurrentPosition //of AudioBook
        | OpenSleepTimerActionMenu
        | StartSleepTimer of TimeSpan option
        | DecreaseSleepTimer
        
        | ChangeBusyState of bool
        | DoNothing

    type ExternalMsg =
        | GotoMainPage
        | GotoBrowserPage


    let audioPlayer = DependencyService.Get<DependencyServices.IAudioPlayer>()

    let toTimeSpan (ms:int) =
        TimeSpan.FromMilliseconds(ms |> float)

    let fromTimeSpan (ts:TimeSpan) =
        ts.TotalMilliseconds |> int
    
    let fromTimeSpanOpt (ts:TimeSpan option) =
        match ts with
        | None -> 0
        | Some ts -> ts |> fromTimeSpan

    


    let loadFilesCommand model =
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
        } |> Cmd.ofAsyncMsgOption


    let playAudio model =
        (fun (dispatch:Dispatch<Msg>) -> 
            async {
                //let audioPlayer = DependencyService.Get<Services.IAudioPlayer>()
                if model.AudioFileList.Length = 0 then
                    return DoNothing
                else
                    let (file,_) = model.AudioFileList.[model.CurrentAudioFileIndex]
                    let currentPosition =model.CurrentPosition |> fromTimeSpanOpt

                    audioPlayer.OnCompletion <- Some (fun ()-> dispatch NextAudioFile)
                    audioPlayer.OnNoisyHeadPhone <- Some (fun () -> dispatch Stop)
                    audioPlayer.OnInfo <- Some (fun (p,d) -> 
                        dispatch (UpdatePostion (p,d))
                        let tsPos =  (p |> toTimeSpan)
                        if tsPos.Seconds % 5 = 0 then
                            dispatch (SaveCurrentPosition)
                        )

                    do! audioPlayer.PlayFile file currentPosition

                    let timer = 
                        new Timer(
                            fun _ -> audioPlayer.GetInfo() |> Async.Start
                            ,null,0,1000)

                    
                    return (PlayStarted timer)
            }
        ) |> Cmd.ofAsyncMsgWithInternalDispatch

    let stopAudio model =
        audioPlayer.Stop () |> ignore
        // Unregister all delegates from the audio service
        audioPlayer.OnCompletion <- None
        audioPlayer.OnNoisyHeadPhone <- None
        audioPlayer.OnInfo <- None
        match model.CurrentPlayingStateUpdateTimer with
        | None -> None
        | Some timer ->
            timer.Dispose()
            audioPlayer.LastPositionBeforeStop

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
          CurrentPlayingStateUpdateTimer=None
          TrackPositionProcess=0.
          ProgressbarValue = 0. 
          TimeUntilSleeps = None
          AudioPlayerBusy = false }

    let init audioBook = 
        let model = audioBook |> initModel

        model, Cmd.batch [(audioBook |> loadFilesCommand); setBusyCmd]


    let rec update msg model =
        match msg with
        | Play -> 
            model |> onPlayMsg
        | PlayStarted timer -> 
            model |> onPlayStartedMsg timer          
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
            
        | ChangeBusyState state -> 
            model |> onChangeBusyState state
        | PlayStopped | DoNothing -> 
            model, Cmd.none, None

    
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


    and onPlayStartedMsg timer model =
        { model with CurrentPlayingStateUpdateTimer = Some timer; AudioPlayerBusy = false }, Cmd.none, None


    and onStopMsg model =
        let lastPosition = 
            model 
            |> stopAudio
            |> Option.map (toTimeSpan)
        let newModel = 
            { model with CurrentState = Stopped; CurrentPlayingStateUpdateTimer = None; CurrentPosition = lastPosition}
            |> setCurrentPositionToAudiobookState
            |> updateLastListendTimeInAudioBookState
        newModel, newModel |> saveNewAudioBookStateCmd , None

    
    and onNextAudioFileMsg model =
        let max = model.AudioFileList.Length - 1
        let n = model.CurrentAudioFileIndex + 1
        let newIndex = 
            if n > max then max else n
        let (fn,duration) = model.AudioFileList.[newIndex]
        let currentDuration = duration |> toTimeSpan
        let currentDurationMs = duration
               
        let newModel = 
            { model with 
                CurrentAudioFileIndex = newIndex
                CurrentDuration = Some currentDuration
                CurrentDurationMs = Some currentDurationMs
                CurrentAudioFile = Some fn
                CurrentPosition = None }
        
        if model.CurrentState = Playing then            
            model |> stopAudio |> ignore

            // do not play "next" file if on end of the audio book
            if (n>max) then
                let audioBook = {model.AudioBook with State = {model.AudioBook.State with Completed = true}}
                let newModel =
                    { model with AudioBook = audioBook; CurrentState = Stopped }
                    |> setCurrentPositionToAudiobookState
                    |> updateLastListendTimeInAudioBookState

                newModel, newModel |> saveNewAudioBookStateCmd, None
            else
                let newModel = 
                    { newModel with AudioPlayerBusy = true }
                    |> setCurrentPositionToAudiobookState
                    |> updateLastListendTimeInAudioBookState

                let playAudioCmd = newModel |> playAudio
                newModel, Cmd.batch [ playAudioCmd; newModel |> saveNewAudioBookStateCmd ], None
        else
            newModel, Cmd.none, None


    and onPreviousAudioFileMsg model =
        let newIndex =                 
            let n = model.CurrentAudioFileIndex - 1
            if n < 0 then 0 else n
        let (fn,duration) = model.AudioFileList.[newIndex]
        let currentDuration = duration |> toTimeSpan
        let currentDurationMs = duration
        let newModel = 
            { model with 
                CurrentAudioFileIndex = newIndex
                CurrentDuration = Some currentDuration
                CurrentDurationMs = Some currentDurationMs
                CurrentAudioFile = Some fn
                CurrentPosition = None }

        if newModel.CurrentState = Playing then
            let newModel = { newModel with AudioPlayerBusy = true }
            newModel |> stopAudio |> ignore
            let playAudioCmd = newModel |> playAudio
            newModel, playAudioCmd, None
        else
            newModel, Cmd.none, None


    and onJumpForwardMsg model =
        model |> setAudioPositionRelative 30000
        model, Cmd.none, None


    and onJumpBackwardsMsg model =
        model |> setAudioPositionRelative -30000
        model, Cmd.none, None


    and onFileListLoadedMsg fileList model =
        match model.AudioBook.State.CurrentPosition with
        | None ->
            let (fn,duration) = fileList.[0]
            let currentDuration = duration |> toTimeSpan
            let currentDurationMs = duration
            {model with 
                AudioFileList = fileList
                CurrentDuration = Some currentDuration
                CurrentDurationMs = Some currentDurationMs
                CurrentAudioFile = Some fn
                CurrentAudioFileIndex = 0
            }, unsetBusyCmd, None
        | Some cp ->
            let lastListenFile = 
                fileList
                |> List.indexed
                |> List.tryFind (fun (_,(fn,_)) -> fn = cp.Filename)
            match lastListenFile with
            | None -> 
                {model with AudioFileList = fileList}, unsetBusyCmd, None
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
                }, unsetBusyCmd, None


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
        newModel, newModel |> saveNewAudioBookStateCmd, None


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
    
