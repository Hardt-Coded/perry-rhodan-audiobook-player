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


    type PlayerState =
        | Stopped        
        | Playing

    type Model = 
      { AudioBook:AudioBook 
        CurrentAudioFile: string option
        CurrentAudioFileIndex: int
        CurrentPosition: TimeSpan option 
        CurrentState : PlayerState
        AudioFileList: string list 
        IsLoading: bool 
        CurrentPlayingStateUpdateTimer:Timer option}

    type Msg = 
        | Play 
        | PlayStarted of Timer
        | Stop
        | PlayStopped
        | NextAudioFile
        | PreviousAudioFile
        | JumpForward
        | JumpBackwards
        | FileListLoaded of string list

        | UpdatePostion of int

        | ChangeBusyState of bool
        | DoNothing

    type ExternalMsg =
        | GotoMainPage
        | GotoBrowserPage


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
                    asyncMethod( 
                        fun () -> folder |> Directory.EnumerateFiles
                    )
                return Some (FileListLoaded (files |> Seq.toList))
        } |> Cmd.ofAsyncMsgOption


    let playAudio model =
        (fun (dispatch:Dispatch<Msg>) -> 
            async {
                let audioPlayer = DependencyService.Get<Services.IAudioPlayer>()
                if model.AudioFileList.Length = 0 then
                    return DoNothing
                else
                    let file = model.AudioFileList.[model.CurrentAudioFileIndex]
                    let currentPosition =model.CurrentPosition |> fromTimeSpanOpt

                    audioPlayer.OnCompletion <- Some (fun ()-> dispatch NextAudioFile)

                    do! audioPlayer.PlayFile file currentPosition
                    let timer =
                        new Timer(
                            fun _ -> dispatch (UpdatePostion audioPlayer.CurrentPosition)
                        , null, 0, 1000)
                    return (PlayStarted timer)
            }
        ) |> Cmd.ofAsyncWithInternalDispatch

    let stopAudio model =
        let audioPlayer = DependencyService.Get<Services.IAudioPlayer>()
        audioPlayer.Stop () |> ignore
        audioPlayer.OnCompletion <- None
        match model.CurrentPlayingStateUpdateTimer with
        | None -> None
        | Some timer ->
            timer.Dispose()
            audioPlayer.LastPositionBeforeStop

    let setAudioPosition value model =
        match model.CurrentPosition with
        | None -> ()            
        |  Some pos ->
            let msPos = pos |> fromTimeSpan
            let newPos = if msPos + value < 0 then 0 else msPos + value
            let audioPlayer = DependencyService.Get<Services.IAudioPlayer>()
            audioPlayer.GotToPosition (newPos) |> ignore

            
        
        

    let unsetBusyCmd = Cmd.ofMsg (ChangeBusyState false)


    let setBusyCmd = Cmd.ofMsg (ChangeBusyState true)

    let initModel audioBook = 
        { AudioBook = audioBook; 
          CurrentAudioFile = None; 
          CurrentAudioFileIndex = 0
          CurrentPosition= None; 
          CurrentState = Stopped; 
          AudioFileList = []; 
          IsLoading=false; 
          CurrentPlayingStateUpdateTimer=None }

    let init audioBook = 
        let model = audioBook |> initModel
        model, Cmd.batch [(audioBook |> loadFilesCommand); setBusyCmd]

    

    let update msg model =
        match msg with
        | Play ->
            let playAudioCmd = model |> playAudio
            {model with CurrentState = Playing}, Cmd.batch [ playAudioCmd ], None
        | PlayStarted timer ->
            {model with CurrentPlayingStateUpdateTimer = Some timer}, Cmd.none, None
        | Stop -> 
            let lastPosition = 
                model 
                |> stopAudio
                |> Option.map (toTimeSpan)

            {model with CurrentState = Stopped; CurrentPlayingStateUpdateTimer = None; CurrentPosition = lastPosition}, Cmd.none, None
        | NextAudioFile -> 
            let newIndex = 
                let max = model.AudioFileList.Length - 1
                let n = model.CurrentAudioFileIndex + 1
                if n > max then max else n

            let newModel = {model with CurrentAudioFileIndex = newIndex; CurrentPosition = None}
            
            if newModel.CurrentState = Playing then
                newModel |> stopAudio |> ignore
                let playAudioCmd = newModel |> playAudio
                newModel, playAudioCmd, None
            else
                newModel, Cmd.none, None
                
            
        | PreviousAudioFile -> 
            let newIndex =                 
                let n = model.CurrentAudioFileIndex - 1
                if n < 0 then 0 else n
            let newModel = {model with CurrentAudioFileIndex = newIndex; CurrentPosition = None}

            if newModel.CurrentState = Playing then
                newModel |> stopAudio |> ignore
                let playAudioCmd = newModel |> playAudio
                newModel, playAudioCmd, None
            else
                newModel, Cmd.none, None

        | JumpForward -> 
            model |> setAudioPosition 30000
            model, Cmd.none, None
        | JumpBackwards -> 
            model |> setAudioPosition -30000
            model, Cmd.none, None
        | FileListLoaded fileList ->
            {model with AudioFileList = fileList}, unsetBusyCmd, None

        | UpdatePostion ms ->
            {model with CurrentPosition = Some (ms |> toTimeSpan)}, Cmd.none, None

        | ChangeBusyState state -> 
            {model with IsLoading = state}, Cmd.none, None
        | PlayStopped | DoNothing -> 
            model, Cmd.none, None

    let view (model: Model) dispatch =
        View.ContentPage(
          title="Player",useSafeArea=true,
          backgroundColor = Consts.backgroundColor,
          content = 
            View.Grid(padding = 20.0,
                horizontalOptions = LayoutOptions.Fill,
                verticalOptions = LayoutOptions.Fill,                
                rowdefs = [ box "*"; box "*"; box "*" ],
                
                children = [

                    // Todo: Data Stuff
                    let currentPos = (model.CurrentPosition |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")
                    yield View.StackLayout(orientation = StackOrientation.Vertical,
                        verticalOptions=LayoutOptions.Fill,
                        horizontalOptions=LayoutOptions.Center,                        
                        children = [
                            yield (Controls.primaryTextColorLabel 25.0 (model.AudioBook.FullName ))
                            yield (Controls.primaryTextColorLabel 40.0 (sprintf "Track: %i von %i" (model.CurrentAudioFileIndex + 1) model.AudioFileList.Length ))
                            yield (Controls.primaryTextColorLabel 40.0 (sprintf "%s" currentPos))
                        ]
                    ).GridRow(0)
                    
                    
                    yield View.Image(
                        source=
                            match model.AudioBook.Picture with
                            | None -> "AudioBookPlaceholder_Dark.png"
                            | Some p -> p
                            ,
                        horizontalOptions=LayoutOptions.CenterAndExpand,
                        verticalOptions=LayoutOptions.CenterAndExpand,
                        aspect=Aspect.AspectFill
                        ).GridRow(1)

                    yield View.Grid(
                        coldefs=[box "*";box "*";box "*";box "*";box "*"],
                        rowdefs=[box "*";box "*" ],
                        children=[
                            yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch PreviousAudioFile) 45.0 true "\uf048").GridColumn(0).GridRow(0)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch JumpBackwards) 45.0 true "\uf04a").GridColumn(1).GridRow(0)

                            match model.CurrentState with
                            | Stopped ->
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch Play) 45.0 false "\uf144").GridColumn(2).GridRow(0)
                            | Playing ->
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch Stop) 45.0 false "\uf28b").GridColumn(2).GridRow(0)
                                
                            
                            yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch JumpForward) 45.0 true "\uf04e").GridColumn(3).GridRow(0)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch NextAudioFile) 45.0 true "\uf051").GridColumn(4).GridRow(0)
                            
                        ]).GridRow(2)

                    if model.IsLoading then 
                        yield Common.createBusyLayer().GridRowSpan(3)
                ]
            )
          
          )

    let viewSmall openPlayerPageCommand (model: Model) dispatch =
        View.Grid(
            coldefs=[box "*"; box "auto"; box "auto"; box "auto"],
            backgroundColor=Consts.cardColor,
            gestureRecognizers = [
                View.TapGestureRecognizer(command=openPlayerPageCommand)
            ],
            children = [
                let currentPos = (model.CurrentPosition |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")

                yield (Controls.primaryTextColorLabel 12.0 (model.AudioBook.FullName ))
                    .GridColumn(0)
                    .With(horizontalOptions=LayoutOptions.Start, margin=Thickness(5.0,0.0,0.0,0.0))
                    

                yield (Controls.primaryTextColorLabel 12.0 (sprintf "%i/%i" (model.CurrentAudioFileIndex + 1) model.AudioFileList.Length ))
                    .GridColumn(1)
                yield (Controls.primaryTextColorLabel 12.0 (sprintf "%s" currentPos))
                    .GridColumn(2)
                match model.CurrentState with
                | Stopped ->
                    yield (Controls.primaryColorSymbolLabelWithTapCommandRightAlign (fun () -> dispatch Play) 30.0 false "\uf144")
                        .GridColumn(3)
                        .GridRow(0)
                        .With(margin=Thickness(5.0,3.0,5.0,3.0))
                | Playing ->
                    yield (Controls.primaryColorSymbolLabelWithTapCommandRightAlign (fun () -> dispatch Stop) 30.0 false "\uf28b")
                        .GridColumn(3)
                        .GridRow(0)
                        .With(margin=Thickness(5.0,3.0,5.0,3.0))

            ]
        )
    
