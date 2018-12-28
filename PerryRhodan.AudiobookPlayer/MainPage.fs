module MainPage

open Fabulous
open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open Domain
open Plugin.Permissions.Abstractions
open Common
open Services

    type Model = 
      { Audiobooks: AudioBookItem.Model[]
        IsLoading:bool }

    type Msg = 
        | AskForAppPermission
        | PermissionDenied
        | LoadLocalAudiobooks
        | LocalAudioBooksLoaded of AudioBook []
        | AudioBooksItemMsg of AudioBookItem.Model * AudioBookItem.Msg
        | UpdateAudioBook of AudioBookItem.Model

        | ChangeBusyState of bool
        | DoNothing

    type ExternalMsg =
        | GotoPermissionDeniedPage
        | OpenAudioBookPlayer of AudioBook
        | UpdateAudioBookGlobal  of AudioBookItem.Model * string

    
    let initModel = { Audiobooks = [||]; IsLoading = false }

    
    let init () = initModel, Cmd.ofMsg AskForAppPermission


    let rec update msg model =
        match msg with
        | AskForAppPermission ->
            model |> onAskForAppPermissionMsg
        | AudioBooksItemMsg (abModel, msg) ->
            model |> onProcessAudioBookItemMsg abModel msg
        | PermissionDenied ->
            model |> onPermissionDeniedMsg
        | LoadLocalAudiobooks -> 
            model |> onLoadAudioBooksMsg
        | LocalAudioBooksLoaded ab ->
            model |> onLocalAudioBooksLoadedMsg ab
        | UpdateAudioBook ab ->
            model |> onUpdateAudioBookMsg ab
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state
        | DoNothing ->
            model |> onDoNothingMsg

    
    and onUpdateAudioBookMsg ab model =
        let newAudioBooks =
            model.Audiobooks
            |> Array.map (
                fun (i:AudioBookItem.Model) ->
                    if (i.AudioBook.FullName = ab.AudioBook.FullName) then
                        ab
                    else
                        i
            )
            |> Array.filter (fun i -> i.AudioBook.State.Downloaded)

        { model with Audiobooks = newAudioBooks }, Cmd.none, None
    
    
    and onAskForAppPermissionMsg model =
        let ask = 
            async { 
                let! res = Common.Helpers.askPermissionAsync Permission.Storage
                if res then return LoadLocalAudiobooks 
                else return PermissionDenied
            } |> Cmd.ofAsyncMsg
        
        model, ask, None
    
    
    and onProcessAudioBookItemMsg abModel msg model =
        let newModel, cmd, externalMsg = AudioBookItem.update msg abModel
        let (externalCmds,mainPageMsg) =
            match externalMsg with
            | None -> Cmd.none, None
            | Some excmd -> 
                match excmd with
                | AudioBookItem.ExternalMsg.UpdateAudioBook ab ->
                    Cmd.ofMsg (UpdateAudioBook ab), Some (UpdateAudioBookGlobal (ab, "MainPage"))
                | AudioBookItem.ExternalMsg.AddToDownloadQueue mdl ->
                    Cmd.ofMsg DoNothing, None
                | AudioBookItem.ExternalMsg.RemoveFromDownloadQueue mdl ->
                    Cmd.ofMsg DoNothing, None
                | AudioBookItem.ExternalMsg.OpenLoginPage ->
                    Cmd.ofMsg DoNothing, None
                | AudioBookItem.ExternalMsg.PageChangeBusyState state ->
                    Cmd.ofMsg (ChangeBusyState state), None
                | AudioBookItem.ExternalMsg.OpenAudioBookPlayer ab ->
                    Cmd.none, Some (OpenAudioBookPlayer ab)
        
        let newDab = 
            model.Audiobooks 
            |> Array.map (fun i -> if i = abModel then newModel else i)

        {model with Audiobooks = newDab}, Cmd.batch [(Cmd.map2 newModel AudioBooksItemMsg cmd); externalCmds ], mainPageMsg

    
    
    and onPermissionDeniedMsg model =
        model, Cmd.none, Some GotoPermissionDeniedPage
    

    and onLoadAudioBooksMsg model =
        
        let loadLocalAudioBooks () =
            async {
                let! audioBooks = FileAccess.loadAudioBooksStateFile ()
                match audioBooks with
                | Error e -> 
                    do! Common.Helpers.displayAlert("Error on loading Local Audiobooks",e,"OK")
                    return Some (ChangeBusyState false)
                | Ok ab -> 
                    match ab with
                    | [||] -> return Some (ChangeBusyState false)
                    | _ ->
                        let ab = 
                            ab
                            |> Array.filter (fun i -> i.State.Downloaded)
                            |> Array.sortByDescending ( fun i -> i.FullName)
                        
                        return Some (LocalAudioBooksLoaded ab)
            } |> Common.Cmd.ofAsyncMsgOption
        
        model, Cmd.batch [ Cmd.ofMsg (ChangeBusyState true); loadLocalAudioBooks ()], None

    
    and onLocalAudioBooksLoadedMsg ab model =
        let mapedAb = ab |> Array.map (fun i -> AudioBookItem.initModel i)
        { model with Audiobooks = mapedAb }, Cmd.ofMsg (ChangeBusyState false), None
    
    and onChangeBusyStateMsg state model =
        {model with IsLoading = state}, Cmd.none, None

    
    and onDoNothingMsg model =
        model, Cmd.ofMsg (ChangeBusyState false), None


    let view (model: Model) dispatch =
            View.Grid(
                rowdefs= [box "auto"; box "*"],
                verticalOptions = LayoutOptions.Fill,
                children = [
                    yield View.Label(text="Audiobooks On Device", fontAttributes = FontAttributes.Bold,
                                                    fontSize = 25.0,
                                                    horizontalOptions = LayoutOptions.Fill,
                                                    horizontalTextAlignment = TextAlignment.Center,
                                                    textColor = Consts.primaryTextColor,
                                                    backgroundColor = Consts.cardColor,
                                                    margin=0.0).GridRow(0)

                    yield View.StackLayout(padding = 10.0, verticalOptions = LayoutOptions.Start,
                        children = [ 
                              
                            yield dependsOn (model.Audiobooks) (fun _ (abItems) ->
                                match abItems with
                                | [||]  ->
                                    View.Label(text="There are currently no audiobooks on your device. Use the button on the upper right corner to browse your online audio books.", fontSize=25.0, textColor=Consts.secondaryTextColor)
                                | _ ->
                                    View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                            verticalOptions = LayoutOptions.Fill,
                                            content = 
                                                View.StackLayout(orientation=StackOrientation.Vertical,
                                                    children= [
                                                        for item in abItems do
                                                            let audioBookItemDispatch =
                                                                let d msg = AudioBooksItemMsg (item,msg)
                                                                d >> dispatch
                                                            yield AudioBookItem.view item audioBookItemDispatch 
                                                    ]
                                                )
                                              )
                                
                                )
                        ]).GridRow(1)


                    if model.IsLoading then 
                        yield Common.createBusyLayer().GridRowSpan(2)
                ]
                )
    
