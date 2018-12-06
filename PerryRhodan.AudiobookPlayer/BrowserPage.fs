module BrowserPage

open Fabulous
open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open System.Net
open System.IO
open Domain
open Common
open System.Text.RegularExpressions
open Fabulous.DynamicViews


    type Model = 
      { CurrentSessionCookieContainer:Map<string,string> option
        AudioBooks : NameGroupedAudioBooks option
        SelectedGroups:string []
        SelectedGroupItems: AudioBookListType option
        DisplayedAudioBooks: AudioBookItem.Model []
        LastSelectedGroup: string option
        IsLoading:bool 
        CurrentDownloadProgress: (int * int) option
        DownloadQueueModel: DownloadQueue.Model }

    type Msg = 
        | LoadLocalAudiobooks
        | LoadOnlineAudiobooks 
        | InitAudiobooks of NameGroupedAudioBooks
        | ShowAudiobooks of AudioBookListType
        | AddSelectGroup of string
        | RemoveLastSelectGroup
        | ShowErrorMessage of string
        | ChangeBusyState of bool
        | GoToLoginPage

        | AudioBooksItemMsg of AudioBookItem.Model * AudioBookItem.Msg
        | DownloadQueueMsg of DownloadQueue.Msg
        | UpdateAudioBookItemList of AudioBookItem.Model
        | StartDownloadQueue


        //| UpdateDownloadPrograss of (int * int) option
        | DoNothing
        

    type ExternalMsg =
        | OpenLoginPage
        | OpenAudioBookPlayer of AudioBook
        

    let initModel = { AudioBooks = None
                      CurrentSessionCookieContainer = None
                      IsLoading = false
                      SelectedGroups = [||]
                      DisplayedAudioBooks = [||]
                      SelectedGroupItems = None
                      LastSelectedGroup = None
                      CurrentDownloadProgress = None 
                      DownloadQueueModel = DownloadQueue.initModel None }


    let loadOnlineAudioBooks model =
        async {
            let! audioBooks = 
                Services.getAudiobooksOnline model.CurrentSessionCookieContainer
                
            match audioBooks with
            | Error e -> 
                match e with
                | SessionExpired e -> return GoToLoginPage
                | Other e -> return (ShowErrorMessage e)
                | Exception e ->
                    let ex = e.GetBaseException()
                    let msg = ex.Message + "|" + ex.StackTrace
                    return (ShowErrorMessage msg)
            | Ok ab -> 
                
                // Synchronize it with local
                match model.AudioBooks with
                | None -> 
                    // if your files is empty, than sync with the folders
                    let localFileSynced = Services.syncPossibleDownloadFolder ab
                    let! saveRes = localFileSynced |> Services.saveAudioBooksStateFile
                    match saveRes with
                    | Error e ->
                        return (ShowErrorMessage e)
                    | Ok _ ->
                        let nameGroupedAudiobooks = localFileSynced |> Domain.Filters.nameFilter
                        return (InitAudiobooks nameGroupedAudiobooks)
                    
                | Some la ->
                    let loadedFlatten = la |> AudioBooks.flatten
                    let synchedAb = Services.synchronizeAudiobooks loadedFlatten ab
                    let! saveRes = synchedAb |> Services.saveAudioBooksStateFile 
                    match saveRes with
                    | Error e ->
                        return (ShowErrorMessage e)
                    | Ok _ ->
                        let result = synchedAb |> Domain.Filters.nameFilter
                        return (InitAudiobooks result)
        } |> Cmd.ofAsyncMsg


    let loadLocalAudioBooks () =
        async {
            let! audioBooks = Services.loadAudioBooksStateFile ()
            match audioBooks with
            | Error e -> return (ShowErrorMessage e)
            | Ok ab -> 
                match ab with
                | None -> return DoNothing
                | Some ab ->     
                    let result = ab |> Domain.Filters.nameFilter
                    return (InitAudiobooks result)
        } |> Cmd.ofAsyncMsg
    
    
    let filterAudiobooksByGroups model =        
        match model.AudioBooks with
        | None -> Cmd.ofMsg DoNothing
        | Some a -> 
            match model.SelectedGroups with
            | [||] -> 
                Cmd.ofMsg (ShowAudiobooks (GroupList a))
            | _ ->                
                let filtered =(GroupList a) |> Filters.groupsFilter model.SelectedGroups
                Cmd.ofMsg (ShowAudiobooks filtered)
    


    let init () = initModel, Cmd.batch [(loadLocalAudioBooks ());Cmd.ofMsg (ChangeBusyState true)], None


    let unsetBusyCmd = Cmd.ofMsg (ChangeBusyState false)


    let setBusyCmd = Cmd.ofMsg (ChangeBusyState true)


    let update msg model =
        match msg with
        | LoadLocalAudiobooks ->
            model, Cmd.batch [(loadLocalAudioBooks ());Cmd.ofMsg (ChangeBusyState true)], None
        | LoadOnlineAudiobooks ->
            match model.CurrentSessionCookieContainer with
            | None ->
                model, Cmd.none, Some OpenLoginPage
            | Some cc ->
                let loadAudioBooksCmd = model |> loadOnlineAudioBooks
                model, Cmd.batch [loadAudioBooksCmd; setBusyCmd], None
        | InitAudiobooks ab ->
            let newModel = {model with AudioBooks = Some ab}            
            let showAudiobooksCmd = newModel |> filterAudiobooksByGroups
            newModel, Cmd.batch [ showAudiobooksCmd; unsetBusyCmd], None
        | ShowAudiobooks ab ->
            let newModel = 
                match ab with
                | GroupList _ ->
                    {model with SelectedGroupItems = Some ab; DisplayedAudioBooks = [||]}
                | AudioBookList (_,items) ->
                    let dab = 
                        items 
                        |> Array.map (fun i -> 
                            let (model,_,_) = AudioBookItem.init (i)
                            model
                        )
                    {model with SelectedGroupItems = Some ab; DisplayedAudioBooks = dab}
            // sync display Audiobooks with dowbnload queue
            let syncDab =
                newModel.DisplayedAudioBooks 
                |> Array.map (
                    fun i ->
                        let queueItem = 
                            model.DownloadQueueModel.DownloadQueue 
                            |> List.tryFind (fun q -> q.AudioBook.FullName = i.AudioBook.FullName)
                        match queueItem with
                        | None -> i
                        | Some qi -> qi
                )
            let newModel = {newModel with DisplayedAudioBooks = syncDab }
            let cmd = if model.DownloadQueueModel.DownloadQueue.Length > 0 then Cmd.ofMsg StartDownloadQueue else Cmd.none                
            newModel, Cmd.batch [ unsetBusyCmd; cmd ], None
                
        | AddSelectGroup group ->
            let newGroups = [|group|] |> Array.append model.SelectedGroups
            let newModel = {model with SelectedGroups = newGroups; LastSelectedGroup = Some group}
            let showAudiobooksCmd = newModel |> filterAudiobooksByGroups            
            newModel, showAudiobooksCmd, None
        | RemoveLastSelectGroup ->
            let (newGroups,lastSelectedGroup) = 
                match model.SelectedGroups with
                | [||] -> [||], None
                | [|_|] -> [||], None
                | x -> x.[.. x.Length-2], Some x.[x.Length - 2]
            let newModel = {model with SelectedGroups = newGroups; LastSelectedGroup = lastSelectedGroup}
            let showAudiobooksCmd = newModel |> filterAudiobooksByGroups            
            newModel, showAudiobooksCmd, None        
        | ShowErrorMessage e ->
            Common.Helpers.displayAlert("Error",e,"OK") |> ignore
            model, Cmd.ofMsg (ChangeBusyState false), None
        | ChangeBusyState state -> 
            {model with IsLoading = state}, Cmd.none, None
        | GoToLoginPage ->
            model,Cmd.none,Some OpenLoginPage

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
                        Cmd.ofMsg (DownloadQueueMsg (DownloadQueue.Msg.AddItemToQueue mdl)), None
                    | AudioBookItem.ExternalMsg.RemoveFromDownloadQueue mdl ->
                        Cmd.ofMsg (DownloadQueueMsg (DownloadQueue.Msg.RemoveItemToQueue mdl)), None
                    | AudioBookItem.ExternalMsg.OpenLoginPage ->
                        Cmd.ofMsg DoNothing, Some OpenLoginPage
                    | AudioBookItem.ExternalMsg.PageChangeBusyState state ->
                        Cmd.ofMsg (ChangeBusyState state), None
                    | AudioBookItem.ExternalMsg.OpenAudioBookPlayer ab ->
                        Cmd.none, Some (OpenAudioBookPlayer ab)
            
            let newDab = 
                model.DisplayedAudioBooks 
                |> Array.map (fun i -> if i = abModel then newModel else i)

            {model with DisplayedAudioBooks = newDab}, Cmd.batch [(Cmd.map2 newModel AudioBooksItemMsg cmd); externalCmds ], mainPageMsg

        | DownloadQueueMsg msg ->
            let newModel, cmd, externalMsg = DownloadQueue.update msg model.DownloadQueueModel
            let (externalCmds,mainPageMsg) =
                match externalMsg with
                | None -> Cmd.none, None
                | Some excmd -> 
                    match excmd with
                    | DownloadQueue.ExternalMsg.ExOpenLoginPage ->
                        Cmd.ofMsg DoNothing, Some OpenLoginPage
                    | DownloadQueue.ExternalMsg.UpdateAudioBook abModel ->
                        Cmd.ofMsg (UpdateAudioBookItemList abModel), None
                    | DownloadQueue.ExternalMsg.UpdateDownloadProgress (abModel,progress) ->
                        Cmd.batch [ Cmd.ofMsg (AudioBooksItemMsg (abModel,(AudioBookItem.Msg.UpdateDownloadProgress progress))); Cmd.ofMsg (UpdateAudioBookItemList abModel) ], None
                    | DownloadQueue.ExternalMsg.PageChangeBusyState state ->
                        Cmd.ofMsg (ChangeBusyState state), None


            {model with DownloadQueueModel = newModel}, Cmd.batch [(Cmd.map DownloadQueueMsg cmd); externalCmds ], mainPageMsg
        | UpdateAudioBookItemList abModel ->
            let newDab = 
                model.DisplayedAudioBooks 
                |> Array.map (fun i -> if i.AudioBook.FullName = abModel.AudioBook.FullName then abModel else i)
            {model with DisplayedAudioBooks = newDab}, Cmd.none, None

        | StartDownloadQueue ->
            model, Cmd.ofMsg (DownloadQueueMsg DownloadQueue.Msg.StartProcessing), None


     
        //| UpdateDownloadPrograss progress ->            
        //    {model with CurrentDownloadProgress = progress}, Cmd.none, None
            
        | DoNothing ->
            model, Cmd.ofMsg (ChangeBusyState false), None



    let view (model: Model) dispatch =
        //View.ContentPage(
        //    title="Browsing Page",useSafeArea=true,
        //    backgroundColor = Consts.backgroundColor,
        //    isBusy = model.IsLoading,
        //    content = 
                View.Grid(
                    rowdefs= [box "auto"; box "*"; box "auto"],
                    verticalOptions = LayoutOptions.Fill,
                    children = [

                        yield View.Label(text="Browser Page", fontAttributes = FontAttributes.Bold,
                                            fontSize = 16.0,
                                            horizontalOptions = LayoutOptions.Fill,
                                            horizontalTextAlignment = TextAlignment.Center,
                                            textColor = Consts.primaryTextColor,
                                            backgroundColor = Consts.cardColor,
                                            margin=0.0).GridRow(0)
                    
                        yield View.StackLayout(padding = 10.0, verticalOptions = LayoutOptions.Start,
                            children = [ 
                            
                                yield View.Button(text="Online Refresh", command = (fun () -> dispatch LoadOnlineAudiobooks))
                            
                                match model.SelectedGroupItems with
                                | None ->
                                    yield View.Label(text="Nothing here!")

                                | Some sg ->
                                    match sg with
                                    | GroupList gp ->
                                        let groups = gp |> Array.map (fun (key,_) -> key)
                                        if model.LastSelectedGroup.IsSome then                                             
                                            yield View.Label(text=model.LastSelectedGroup.Value
                                                ,fontSize=22.0
                                                ,textColor=Color.DarkViolet
                                                ,horizontalOptions=LayoutOptions.Fill
                                                ,horizontalTextAlignment=TextAlignment.Center)
                                        
                                        yield View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                            verticalOptions = LayoutOptions.Fill,
                                            content = 
                                                View.StackLayout(orientation=StackOrientation.Vertical,
                                                    children= [
                                                        match groups with
                                                        | [||] -> yield View.Label(text="Press Online Refresh to load your Audiobooks")
                                                        | _ ->
                                                            for (idx,item) in groups |> Array.indexed do
                                                                yield 
                                                                    View.Label(text=item
                                                                        , margin = 2.0
                                                                        , fontSize = 20.0
                                                                        , textColor = Consts.secondaryTextColor
                                                                        //, backgroundColor = (if idx % 2 = 0 then Color.FromRgb(240,240,255) else Color.Transparent)
                                                                        , verticalOptions = LayoutOptions.Fill
                                                                        , verticalTextAlignment = TextAlignment.Center                                                                        
                                                                        , gestureRecognizers = [View.TapGestureRecognizer(command = (fun () -> dispatch (AddSelectGroup item)))]
                                                                        , created = (fun x -> 
                                                                            //let tapRec = View.TapGestureRecognizer(command = (fun () -> dispatch (AddSelectGroup i)))
                                                                            let tapRec = Xamarin.Forms.TapGestureRecognizer()
                                                                            tapRec.Command <- makeCommand(fun () ->                                                                                 
                                                                                x.BackgroundColor <- Color.FromRgb(200,200,255)
                                                                                ()
                                                                                )
                                                                            x.GestureRecognizers.Add(tapRec)
                                                                            ()
                                                                            )
                                                                        )
                                                                    
                                                        ]
                                                    ))
                                        
                                    | AudioBookList (key, items) ->
                                        yield View.Label(text=key
                                                ,fontSize=22.0
                                                ,textColor=Color.DarkViolet
                                                , horizontalOptions=LayoutOptions.Fill
                                                ,horizontalTextAlignment=TextAlignment.Center)
                                        
                                        yield View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                            verticalOptions = LayoutOptions.Fill,
                                            content = 
                                                View.StackLayout(orientation=StackOrientation.Vertical,
                                                    children= [
                                                        for item in model.DisplayedAudioBooks do
                                                            let audioBookItemDispatch =
                                                                let d msg = AudioBooksItemMsg (item,msg)
                                                                d >> dispatch
                                                            yield AudioBookItem.view item audioBookItemDispatch 
                                                    ]
                                                )
                                                )
                                        let downloadQueueDispatch =
                                            DownloadQueueMsg >> dispatch
                                        yield DownloadQueue.view model.DownloadQueueModel downloadQueueDispatch
                                            
                                if (model.SelectedGroups.Length > 0) then
                                    yield View.Button(text = "Back",command = (fun ()-> dispatch RemoveLastSelectGroup))
                            ])
                            .GridRow(1)

                    

                        if model.IsLoading then 
                            yield Common.createBusyLayer().GridRowSpan(2)

                    
                    ]
                )
                
            //)
