module MainPage

open System.Globalization
open System.Resources
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
        LastTimeListendAudioBook: AudioBookItem.Model option
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
        | OpenAudioBookDetail of AudioBook
        | UpdateAudioBookGlobal  of AudioBookItem.Model * string

    
    let initModel = { Audiobooks = [||]; IsLoading = false; LastTimeListendAudioBook = None }

    
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

    and unbusyMsg =
        (ChangeBusyState false)


    and busyMsg =
        (ChangeBusyState true)
    
    
    and unbusyCmd =
        Cmd.ofMsg unbusyMsg


    and busyCmd =
        Cmd.ofMsg busyMsg


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

        let newLastListend =
            model.LastTimeListendAudioBook
            |> Option.map (fun i ->
                if (i.AudioBook.FullName = ab.AudioBook.FullName) then
                    ab
                else
                    i
            )

        { model with Audiobooks = newAudioBooks; LastTimeListendAudioBook = newLastListend }, Cmd.none, None
    
    
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
                | AudioBookItem.ExternalMsg.OpenLoginPage _ ->
                    Cmd.ofMsg DoNothing, None
                | AudioBookItem.ExternalMsg.PageChangeBusyState state ->
                    Cmd.ofMsg (ChangeBusyState state), None
                | AudioBookItem.ExternalMsg.OpenAudioBookPlayer ab ->
                    Cmd.none, Some (OpenAudioBookPlayer ab)
                | AudioBookItem.ExternalMsg.OpenAudioBookDetail ab ->
                    Cmd.none, Some (OpenAudioBookDetail ab)
        
        let newDab = 
            model.Audiobooks 
            |> Array.map (fun i -> if i = abModel then newModel else i)

        {model with Audiobooks = newDab}, Cmd.batch [(Cmd.map2 newModel AudioBooksItemMsg cmd); externalCmds ], mainPageMsg

    

    and onPermissionDeniedMsg model =
        model, Cmd.none, Some GotoPermissionDeniedPage
    

    and onLoadAudioBooksMsg model =
        
        let loadLocalAudioBooks () =
            async {

                let! audioBooks = FileAccess.loadDownloadedAudioBooksStateFile ()
                match audioBooks with
                | Error e -> 
                    do! Common.Helpers.displayAlert(Translations.current.ErrorLoadingLocalAudiobook,e,"OK")
                    return Some unbusyMsg
                | Ok ab -> 
                    match ab with
                    | [||] -> return Some unbusyMsg
                    | _ ->
                        let ab = 
                            ab
                            |> Array.filter (fun i -> i.State.Downloaded)
                            |> Array.sortBy ( fun i -> i.FullName)
                        
                        return Some (LocalAudioBooksLoaded ab)
            } |> Common.Cmd.ofAsyncMsgOption
        
        model, Cmd.batch [ busyCmd; loadLocalAudioBooks ()], None

    
    and onLocalAudioBooksLoadedMsg ab model =
        // look out for the last listend
        let getLastListendAb () =
            ab 
            |> Array.sortByDescending (fun i -> i.State.LastTimeListend) 
            |> Array.tryHead
            |> Option.bind (fun i -> 
                //let ltl = if obj.ReferenceEquals(i.State.LastTimeListend,null) then None else i.State.LastTimeListend
                match i.State.LastTimeListend with
                | None -> None
                | Some _ -> Some (i |> AudioBookItem.initModel)
            ) 
            
        
        let lastTimeListendAudiobook = 
            match ab with
            | [||] | [|_|] ->
                getLastListendAb ()
            | _ ->
                getLastListendAb ()
            

        let mapedAb = 
            ab 
            // filter last listend audio book out of the rest
            |> Array.filter( fun i -> lastTimeListendAudiobook |> Option.map (fun l -> l.AudioBook.FullName <> i.FullName) |> Option.defaultValue true)
            |> Array.Parallel.map (fun i -> AudioBookItem.initModel i)
        { model with Audiobooks = mapedAb; LastTimeListendAudioBook = lastTimeListendAudiobook }, unbusyCmd, None
    
    and onChangeBusyStateMsg state model =
        {model with IsLoading = state}, Cmd.none, None

    
    and onDoNothingMsg model =
        model, unbusyCmd, None


    let view (model: Model) dispatch =
            View.Grid(
                rowdefs= [box "auto"; box "auto"; box "auto"; box "*"],
                rowSpacing = 0.,
                verticalOptions = LayoutOptions.Fill,
                children = [

                    match model.LastTimeListendAudioBook with
                    | None ->()
                    | Some labItem ->
                        yield View.Label(text=Translations.current.LastListendAudioBookTitle, fontAttributes = FontAttributes.Bold,
                            fontSize = 25.,
                            horizontalOptions = LayoutOptions.Fill,
                            horizontalTextAlignment = TextAlignment.Center,
                            textColor = Consts.primaryTextColor,
                            backgroundColor = Consts.cardColor,
                            margin=0.).GridRow(0)

                        let audioBookItemDispatch =
                            let d msg = AudioBooksItemMsg (labItem,msg)
                            d >> dispatch

                        yield (AudioBookItem.view labItem audioBookItemDispatch).Margin(10.).GridRow(1)

                    yield View.Label(text=Translations.current.AudiobookOnDevice, fontAttributes = FontAttributes.Bold,
                                                    fontSize = 25.,
                                                    horizontalOptions = LayoutOptions.Fill,
                                                    horizontalTextAlignment = TextAlignment.Center,
                                                    textColor = Consts.primaryTextColor,
                                                    backgroundColor = Consts.cardColor,
                                                    margin=0.).GridRow(2)

                    yield View.StackLayout(padding = 10., verticalOptions = LayoutOptions.Start,
                        children = [ 
                              
                            yield dependsOn (model.Audiobooks) (fun _ (abItems) ->
                                match abItems,model.LastTimeListendAudioBook with
                                | [||], None  ->
                                    View.Label(text=Translations.current.NoAudiobooksOnDevice, fontSize=25., textColor=Consts.secondaryTextColor)
                                | _, _ ->
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
                        ]).GridRow(3)


                    if model.IsLoading then 
                        yield Common.createBusyLayer().GridRowSpan(4)
                ]
                )
    
