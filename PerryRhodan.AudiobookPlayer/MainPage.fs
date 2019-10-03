module MainPage

open System
open System.Resources
open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Domain
open Plugin.Permissions.Abstractions
open Common
open Services

    

    type Model = { 
        Audiobooks: string[]
        LastTimeListendAudioBook: string option
        IsLoading:bool
        // this value is to ensure, that the view function is call, when we trigger the audio item events externally
        DummyUpdateValue:Guid
    }

    type Msg = 
        | LoadLocalAudiobooks
        | LocalAudioBooksLoaded of AudioBook []
        | AudioBooksItemMsg of AudioBookItem.Model * AudioBookItem.Msg
        | UpdateAudioBook
        | RefreshAudioBookList

        | ChangeBusyState of bool
        | DoNothing

    type ExternalMsg =
        | OpenAudioBookPlayer of AudioBook
        | OpenAudioBookDetail of AudioBook
        | UpdateAudioBookGlobal  of AudioBookItem.Model * string

    
    let initModel = { Audiobooks = [||] ; IsLoading = true; LastTimeListendAudioBook = None; DummyUpdateValue = Guid.NewGuid() }

    
    let init () = 
        initModel, Cmd.ofMsg LoadLocalAudiobooks


    let rec update msg model =
        match msg with
        | AudioBooksItemMsg (abModel, msg) ->
            model |> onProcessAudioBookItemMsg abModel msg
        | LoadLocalAudiobooks -> 
            model |> onLoadAudioBooksMsg
        | LocalAudioBooksLoaded ab ->
            model |> onLocalAudioBooksLoadedMsg ab
        | UpdateAudioBook ->
            model |> onUpdateAudioBookMsg
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state
        | RefreshAudioBookList ->
            model |> onRefreshAudioBookListMsg
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


    and onUpdateAudioBookMsg model =
        {model with DummyUpdateValue=Guid.NewGuid()}, Cmd.ofMsg RefreshAudioBookList, None
    
    and onRefreshAudioBookListMsg model =
        let cmd =
            let allDownloadedAndDownloadingItems =
                AudioBookItemProcessor.getDownloadingAndDownloadedAudioBookItems ()
                |> Array.map (fun i -> i.AudioBook)
            Cmd.ofMsg <| LocalAudioBooksLoaded allDownloadedAndDownloadingItems 
        model, cmd, None
    
    and onProcessAudioBookItemMsg abModel msg model =
        let newModel, cmd, externalMsg = AudioBookItem.update msg abModel
        let (externalCmds,mainPageMsg) =
            match externalMsg with
            | None -> Cmd.none, None
            | Some excmd -> 
                match excmd with
                | AudioBookItem.ExternalMsg.UpdateAudioBook ab ->
                    // in case you remove the audio book from the main page. refresh list of audio books
                    let cmd =
                        Cmd.ofMsg UpdateAudioBook
                        //if not ab.AudioBook.State.Downloaded then
                        //    Cmd.ofMsg RefreshAudioBookList 
                        //else
                        //    Cmd.none

                    cmd, Some (UpdateAudioBookGlobal (ab, "MainPage"))

                | AudioBookItem.ExternalMsg.AddToDownloadQueue mdl ->
                    Cmd.ofMsg DoNothing, None
                | AudioBookItem.ExternalMsg.RemoveFromDownloadQueue mdl ->
                    Cmd.ofMsg DoNothing, None
                | AudioBookItem.ExternalMsg.OpenLoginPage _ ->
                    Cmd.ofMsg DoNothing, None
                //| AudioBookItem.ExternalMsg.PageChangeBusyState state ->
                //    Cmd.ofMsg (ChangeBusyState state), None
                | AudioBookItem.ExternalMsg.OpenAudioBookPlayer ab ->
                    Cmd.none, Some (OpenAudioBookPlayer ab)
                | AudioBookItem.ExternalMsg.OpenAudioBookDetail ab ->
                    Cmd.none, Some (OpenAudioBookDetail ab)

        AudioBookItemProcessor.updateAudiobookItem newModel
        

        model, Cmd.batch [(Cmd.map2 newModel AudioBooksItemMsg cmd); externalCmds ], mainPageMsg


    and onLoadAudioBooksMsg model =
        
        let loadLocalAudioBooks () =
            async {

                let! audioBooks = DataBase.loadDownloadedAudioBooksStateFile ()
                match audioBooks with
                | [||] -> return Some unbusyMsg
                | _ ->
                    return
                        audioBooks
                        |> LocalAudioBooksLoaded
                        |> Some
                    
            } |> Common.Cmd.ofAsyncMsgOption
        
        model, Cmd.batch [ loadLocalAudioBooks ()], None

    
    and onLocalAudioBooksLoadedMsg ab model =
        // look out for the last listend
        let getLastListendAb () =
            ab 
            |> Array.sortByDescending (fun i -> i.State.LastTimeListend) 
            |> Array.tryHead
            |> Option.bind (fun i -> 
                match i.State.LastTimeListend with
                | None -> None
                | Some _ -> Some (i.FullName)
            ) 
            
        
        let lastTimeListendAudiobook = 
                getLastListendAb ()
            

        let mapedAb = 
            ab 
            // filter last listend audio book out of the rest
            |> Array.filter(fun i -> 
                lastTimeListendAudiobook 
                |> Option.map (fun l -> l <> i.FullName) 
                |> Option.defaultValue true
            )
            |> Array.Parallel.map (fun i -> i.FullName)
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

                    let abItem = AudioBookItemProcessor.getAudioBookItem labItem
                    match abItem with
                    | None ->
                        ()
                    | Some abItem ->
                        yield View.Label(text=Translations.current.LastListendAudioBookTitle, fontAttributes = FontAttributes.Bold,
                            fontSize = 25.,
                            horizontalOptions = LayoutOptions.Fill,
                            horizontalTextAlignment = TextAlignment.Center,
                            textColor = Consts.primaryTextColor,
                            backgroundColor = Consts.cardColor,
                            margin=0.).GridRow(0)

                        let audioBookItemDispatch =
                            let d msg = AudioBooksItemMsg (abItem,msg)
                            d >> dispatch

                        yield (AudioBookItem.view abItem audioBookItemDispatch).Margin(10.).GridRow(1)

                yield View.Label(text=Translations.current.AudiobookOnDevice, fontAttributes = FontAttributes.Bold,
                                                fontSize = 25.,
                                                horizontalOptions = LayoutOptions.Fill,
                                                horizontalTextAlignment = TextAlignment.Center,
                                                textColor = Consts.primaryTextColor,
                                                backgroundColor = Consts.cardColor,
                                                margin=0.).GridRow(2)

                    
                yield View.StackLayout(padding = 10., verticalOptions = LayoutOptions.Start,
                    children = [ 
                        if not model.IsLoading then
                            //yield dependsOn (model.Audiobooks) (fun _ (abItems) ->
                            match model.Audiobooks,model.LastTimeListendAudioBook with
                            | [||], None  ->
                                yield View.Label(text=Translations.current.NoAudiobooksOnDevice, fontSize=25., textColor=Consts.secondaryTextColor)
                            | [||], Some _  ->
                                yield View.Label(text="...", fontSize=25., textColor=Consts.secondaryTextColor)
                            | _, _ ->
                                yield View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                        verticalOptions = LayoutOptions.Fill,
                                        content = 
                                            View.StackLayout(orientation=StackOrientation.Vertical,
                                                children= [
                                                    let abItems = AudioBookItemProcessor.getAudioBookItems model.Audiobooks
                                                    for item in abItems do
                                                        let audioBookItemDispatch =
                                                            let d msg = AudioBooksItemMsg (item,msg)
                                                            d >> dispatch
                                                        yield AudioBookItem.view item audioBookItemDispatch 
                                                ]
                                            )
                                            )
                                
                            //)
                    ]).GridRow(3)


                if model.IsLoading then 
                    yield Common.createBusyLayer().GridRowSpan(4)
            ]
            )
    
