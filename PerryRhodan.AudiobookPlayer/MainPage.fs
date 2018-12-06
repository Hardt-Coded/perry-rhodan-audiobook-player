module MainPage

open Fabulous
open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open Domain
open Plugin.Permissions.Abstractions
open Common

    type Model = 
      { Audiobooks: AudioBookItem.Model[]
        IsLoading:bool }

    type Msg = 
        | AskForAppPermission
        | PermissionDenied
        | LoadLocalAudiobooks
        | LocalAudioBooksLoaded of AudioBook []
        | AudioBooksItemMsg of AudioBookItem.Model * AudioBookItem.Msg

        | ChangeBusyState of bool
        | DoNothing

    type ExternalMsg =
        | GotoPermissionDeniedPage
        | OpenAudioBookPlayer of AudioBook

    let initModel = { Audiobooks = [||]; IsLoading = false }

    let loadLocalAudioBooks () =
        async {
            let! audioBooks = Services.loadAudioBooksStateFile ()
            match audioBooks with
            | Error e -> 
                do! Common.Helpers.displayAlert("Error on loading Local Audiobooks",e,"OK")
                return Some (ChangeBusyState false)
            | Ok ab -> 
                match ab with
                | None -> return Some (ChangeBusyState false)
                | Some ab ->
                    let ab = 
                        ab                         
                        |> Array.filter (fun i -> i.State.Downloaded)
                        |> Array.sortByDescending ( fun i -> i.FullName)
                    
                    return Some (LocalAudioBooksLoaded ab)
        } |> Common.Cmd.ofAsyncMsgOption

    let init () = initModel, Cmd.ofMsg AskForAppPermission

    let update msg model =
        match msg with
        | AskForAppPermission ->
            let ask = 
                async { 
                    let! res = Common.Helpers.askPermissionAsync Permission.Storage
                    if res then return LoadLocalAudiobooks 
                    else return PermissionDenied
                } |> Cmd.ofAsyncMsg
            
            model, ask, None
        
        | AudioBooksItemMsg (abModel, msg) ->
            let newModel, cmd, externalMsg = AudioBookItem.update msg abModel
            let (externalCmds,mainPageMsg) =
                match externalMsg with
                | None -> Cmd.none, None
                | Some excmd -> 
                    match excmd with
                    | AudioBookItem.ExternalMsg.UpdateAudioBook ab ->
                        Cmd.ofMsg DoNothing, None
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

        | PermissionDenied ->
            model, Cmd.none, Some GotoPermissionDeniedPage
        | LoadLocalAudiobooks -> 
            model, Cmd.batch [ loadLocalAudioBooks (); Cmd.ofMsg (ChangeBusyState true)], None
        | LocalAudioBooksLoaded ab ->
            let mapedAb = ab |> Array.map (fun i -> AudioBookItem.initModel i)
            { model with Audiobooks = mapedAb }, Cmd.ofMsg (ChangeBusyState false), None
        | ChangeBusyState state -> 
            {model with IsLoading = state}, Cmd.none, None
        | DoNothing ->
            model, Cmd.ofMsg (ChangeBusyState false), None



    let view (model: Model) dispatch =
        //View.ContentPage(
        //  title="Home",useSafeArea=true,
        //  backgroundColor = Consts.backgroundColor,
        //  isBusy = model.IsLoading,
        //  content = 
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
                                    View.Label(text="Swipe from left border to right to open the menu and browse for your audio books", fontSize=25.0, textColor=Consts.secondaryTextColor)
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
            //)
    
