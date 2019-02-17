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
open Services
open Global


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
        | GoToLoginPage of LoginRequestCameFrom

        | AudioBooksItemMsg of AudioBookItem.Model * AudioBookItem.Msg
        | DownloadQueueMsg of DownloadQueue.Msg
        | UpdateAudioBookItemList of AudioBookItem.Model
        | StartDownloadQueue


        //| UpdateDownloadPrograss of (int * int) option
        | DoNothing
        

    type ExternalMsg =
        | OpenLoginPage of LoginRequestCameFrom
        | OpenAudioBookPlayer of AudioBook 
        | OpenAudioBookDetail of AudioBook
        | UpdateAudioBookGlobal  of AudioBookItem.Model *  string
        

    let initModel = { AudioBooks = None
                      CurrentSessionCookieContainer = None
                      IsLoading = false
                      SelectedGroups = [||]
                      DisplayedAudioBooks = [||]
                      SelectedGroupItems = None
                      LastSelectedGroup = None
                      CurrentDownloadProgress = None 
                      DownloadQueueModel = DownloadQueue.initModel None }


    let loadLocalAudioBooks () =
        async {
            let! audioBooks = FileAccess.loadAudioBooksStateFile ()
            match audioBooks with
            | Error e -> return (ShowErrorMessage e)
            | Ok ab -> 
                match ab with
                | [||] -> return DoNothing
                | _ ->     
                    let result = ab |> Domain.Filters.nameFilter
                    return (InitAudiobooks result)
        } |> Cmd.ofAsyncMsg
    

    let unbusyCmd = Cmd.ofMsg (ChangeBusyState false)


    let busyCmd = Cmd.ofMsg (ChangeBusyState true)


    let init () = initModel, Cmd.batch [(loadLocalAudioBooks ()); busyCmd], None


    let rec update msg model =
        match msg with
        | LoadLocalAudiobooks ->
            model |> onLoadLocalAudiobooksMsg
        | LoadOnlineAudiobooks ->
            model |> onLoadOnlineAudiobooksMsg
        | InitAudiobooks ab ->
            model |> onInitAudiobooksMsg ab
        | ShowAudiobooks ab ->
            model |> onShowAudiobooksMsg ab
        | AddSelectGroup group ->
            model |> onAddSelectGroupMsg group
        | RemoveLastSelectGroup ->
            model |> onRemoveLastSelectGroupMsg            
        | ShowErrorMessage e ->
            model |> onShowErrorMessageMsg e
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state
        | GoToLoginPage cameFrom ->
            model |> onGotoLoginPageMsg cameFrom
        | AudioBooksItemMsg (abModel, msg) ->
            model |> onProcessAudioBookItemMsg abModel msg
        | DownloadQueueMsg msg ->
            model |> onProcessDownloadQueueMsg msg            
        | UpdateAudioBookItemList abModel ->
            model |> onUpdateAudioBookItemListMsg abModel            
        | StartDownloadQueue ->
            model |> onStartDownloadQueueMsg
        | DoNothing ->
            model |> onDoNothingMsg            

    and onLoadLocalAudiobooksMsg model =
        model, Cmd.batch [(loadLocalAudioBooks ()); busyCmd], None
    

    and onLoadOnlineAudiobooksMsg model =
        
        let notifyAfterSync (synchedAb:AudioBook[]) =
            async {
                match synchedAb with
                | [||] ->
                    do! Common.Helpers.displayAlert(Translations.current.NoNewAudioBooksSinceLastRefresh," ¯\_(ツ)_/¯","OK")
                | _ ->
                    let message = synchedAb |> Array.map (fun i -> i.FullName) |> String.concat "\r\n"
                    do! Common.Helpers.displayAlert(Translations.current.NewAudioBooksSinceLastRefresh,message,"OK")
            }

        let loadOnlineAudioBooks model =
            async {
                let! audioBooks = 
                    WebAccess.getAudiobooksOnline model.CurrentSessionCookieContainer
                    
                match audioBooks with
                | Error e -> 
                    match e with
                    | SessionExpired e -> return (GoToLoginPage RefreshAudiobooks)
                    | Other e -> return (ShowErrorMessage e)
                    | Exception e ->
                        let ex = e.GetBaseException()
                        let msg = ex.Message + "|" + ex.StackTrace
                        return (ShowErrorMessage msg)
                    | Network msg ->
                        return (ShowErrorMessage msg)
                | Ok ab -> 
                    
                    // Synchronize it with local
                    match model.AudioBooks with
                    | None -> 
                        // if your files is empty, than sync with the folders
                        let localFileSynced = FileAccess.syncPossibleDownloadFolder ab
                        let! saveRes = localFileSynced |> FileAccess.insertNewAudioBooksInStateFile
                        match saveRes with
                        | Error e ->
                            return (ShowErrorMessage e)
                        | Ok _ ->
                            let nameGroupedAudiobooks = localFileSynced |> Domain.Filters.nameFilter
                            return (InitAudiobooks nameGroupedAudiobooks)
                        
                    | Some la ->
                        let loadedFlatten = la |> AudioBooks.flatten
                        let synchedAb = Domain.filterNewAudioBooks loadedFlatten ab
                        do! synchedAb |> notifyAfterSync
                        match synchedAb with
                        | [||] ->
                            return DoNothing
                        | _ ->
                            let! saveRes = synchedAb |> FileAccess.insertNewAudioBooksInStateFile 
                            match saveRes with
                            | Error e ->
                                return (ShowErrorMessage e)
                            | Ok _ ->                            
                                let! audioBooks = FileAccess.loadAudioBooksStateFile ()
                                match audioBooks with
                                | Error e -> return (ShowErrorMessage e)
                                | Ok ab -> 
                                    match ab with
                                    | [||] -> return DoNothing
                                    | _ ->     
                                        let result = ab |> Domain.Filters.nameFilter                                
                                        return (InitAudiobooks result)
            } |> Cmd.ofAsyncMsg        
        
        
        match model.CurrentSessionCookieContainer with
        | None ->
            model, Cmd.none, Some (OpenLoginPage RefreshAudiobooks)
        | Some cc ->
            let loadAudioBooksCmd = model |> loadOnlineAudioBooks
            model, Cmd.batch [loadAudioBooksCmd; busyCmd], None


    and filterAudiobooksByGroups model =        
        match model.AudioBooks with
        | None -> Cmd.ofMsg DoNothing
        | Some a -> 
            match model.SelectedGroups with
            | [||] -> 
                Cmd.ofMsg (ShowAudiobooks (GroupList a))
            | _ ->                
                let filtered =(GroupList a) |> Filters.groupsFilter model.SelectedGroups
                Cmd.ofMsg (ShowAudiobooks filtered)


    and onInitAudiobooksMsg ab model =
        let newModel = { model with AudioBooks = Some ab }       
        let showAudiobooksCmd = newModel |> filterAudiobooksByGroups
        newModel, Cmd.batch [ showAudiobooksCmd; unbusyCmd], None
    
    
    and onShowAudiobooksMsg ab model =
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
        newModel, Cmd.batch [ unbusyCmd; cmd ], None


    and onAddSelectGroupMsg group model =
        let newGroups = [|group|] |> Array.append model.SelectedGroups
        let newModel = {model with SelectedGroups = newGroups; LastSelectedGroup = Some group}
        let showAudiobooksCmd = newModel |> filterAudiobooksByGroups            
        newModel, showAudiobooksCmd, None


    and onRemoveLastSelectGroupMsg model =
        let (newGroups,lastSelectedGroup) = 
            match model.SelectedGroups with
            | [||] -> [||], None
            | [|_|] -> [||], None
            | x -> x.[.. x.Length-2], Some x.[x.Length - 2]
        let newModel = {model with SelectedGroups = newGroups; LastSelectedGroup = lastSelectedGroup}
        let showAudiobooksCmd = newModel |> filterAudiobooksByGroups            
        newModel, showAudiobooksCmd, None


    and onShowErrorMessageMsg e model =
        Common.Helpers.displayAlert("Error",e,"OK") |> Async.StartImmediate
        model, Cmd.ofMsg (ChangeBusyState false), None
    

    and onChangeBusyStateMsg state model =
        {model with IsLoading = state}, Cmd.none, None
    

    and onGotoLoginPageMsg cameFrom model =
        model,Cmd.none,Some (OpenLoginPage cameFrom)
    

    and onProcessAudioBookItemMsg abModel msg model =
        let newModel, cmd, externalMsg = AudioBookItem.update msg abModel
        let (externalCmds,mainPageMsg) =
            match externalMsg with
            | None -> Cmd.none, None
            | Some excmd -> 
                match excmd with
                | AudioBookItem.ExternalMsg.UpdateAudioBook ab ->
                    Cmd.ofMsg DoNothing, Some (UpdateAudioBookGlobal (ab, "Browser"))
                | AudioBookItem.ExternalMsg.AddToDownloadQueue mdl ->
                    Cmd.ofMsg (DownloadQueueMsg (DownloadQueue.Msg.AddItemToQueue mdl)), None
                | AudioBookItem.ExternalMsg.RemoveFromDownloadQueue mdl ->
                    Cmd.ofMsg (DownloadQueueMsg (DownloadQueue.Msg.RemoveItemFromQueue mdl)), None
                | AudioBookItem.ExternalMsg.OpenLoginPage cameFrom ->
                    Cmd.ofMsg DoNothing, Some (OpenLoginPage cameFrom)
                | AudioBookItem.ExternalMsg.PageChangeBusyState state ->
                    Cmd.ofMsg (ChangeBusyState state), None
                | AudioBookItem.ExternalMsg.OpenAudioBookPlayer ab ->
                    Cmd.none, Some (OpenAudioBookPlayer ab)
                | AudioBookItem.ExternalMsg.OpenAudioBookDetail ab ->
                    Cmd.none, Some (OpenAudioBookDetail ab)

        let newDab = 
            model.DisplayedAudioBooks 
            |> Array.map (fun i -> if i = abModel then newModel else i)

        {model with DisplayedAudioBooks = newDab}, Cmd.batch [(Cmd.map2 newModel AudioBooksItemMsg cmd); externalCmds ], mainPageMsg
    
    
    and onProcessDownloadQueueMsg msg model =
        let newModel, cmd, externalMsg = DownloadQueue.update msg model.DownloadQueueModel
        let (externalCmds,mainPageMsg) =
            match externalMsg with
            | None -> Cmd.none, None
            | Some excmd -> 
                match excmd with
                | DownloadQueue.ExternalMsg.ExOpenLoginPage cameFrom ->
                    Cmd.ofMsg DoNothing, Some (OpenLoginPage cameFrom)
                | DownloadQueue.ExternalMsg.UpdateAudioBook abModel ->
                    Cmd.ofMsg (UpdateAudioBookItemList abModel), Some (UpdateAudioBookGlobal (abModel, "Browser"))
                | DownloadQueue.ExternalMsg.UpdateDownloadProgress (abModel,progress) ->
                    Cmd.batch [ Cmd.ofMsg (AudioBooksItemMsg (abModel,(AudioBookItem.Msg.UpdateDownloadProgress progress))); Cmd.ofMsg (UpdateAudioBookItemList abModel) ], None
                | DownloadQueue.ExternalMsg.PageChangeBusyState state ->
                    Cmd.ofMsg (ChangeBusyState state), None

        {model with DownloadQueueModel = newModel}, Cmd.batch [(Cmd.map DownloadQueueMsg cmd); externalCmds ], mainPageMsg
    
    
    and onUpdateAudioBookItemListMsg abModel model =
        let newDab = 
            model.DisplayedAudioBooks 
            |> Array.map (fun i -> if i.AudioBook.FullName = abModel.AudioBook.FullName then abModel else i)
        {model with DisplayedAudioBooks = newDab}, Cmd.none, None
    
    
    and onStartDownloadQueueMsg model =
        model, Cmd.ofMsg (DownloadQueueMsg DownloadQueue.Msg.StartProcessing), None

    
    and onDoNothingMsg model =
        model, unbusyCmd, None


    
    let view (model: Model) dispatch =
        View.Grid(
            rowdefs= [box "auto"; box "*"; box "auto"; box "auto"],
            verticalOptions = LayoutOptions.Fill,
            children = [
                let browseTitle = 
                    match model.LastSelectedGroup with
                    | None -> Translations.current.BrowseAudioBooks
                    | Some t -> sprintf "%s: %s" Translations.current.Browse t                   
                        

                yield View.Label(text=browseTitle, fontAttributes = FontAttributes.Bold,
                                    fontSize = 25.,
                                    horizontalOptions = LayoutOptions.Fill,
                                    horizontalTextAlignment = TextAlignment.Center,
                                    textColor = Consts.primaryTextColor,
                                    backgroundColor = Consts.cardColor,
                                    margin=0.).GridRow(0)
                    
                yield View.StackLayout(padding = 10., verticalOptions = LayoutOptions.Start,
                    children = [ 
                        
                        
                        yield View.Button(text=Translations.current.LoadYourAudioBooks, command = (fun () -> dispatch LoadOnlineAudiobooks))
                            
                        match model.SelectedGroupItems with
                        | None ->
                            yield Controls.secondaryTextColorLabel 20. Translations.current.LoadYourAudioBooksHint

                        | Some sg ->
                            match sg with
                            | GroupList gp ->
                                let groups = gp |> Array.map (fun (key,_) -> key)

                                        
                                yield View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                    verticalOptions = LayoutOptions.Fill,
                                    content = 
                                        View.StackLayout(orientation=StackOrientation.Vertical,
                                            verticalOptions = LayoutOptions.Fill,
                                            children= [
                                                match groups with
                                                | [||] -> yield Controls.secondaryTextColorLabel 20. Translations.current.LoadYourAudioBooksHint
                                                | _ ->
                                                    for (idx,item) in groups |> Array.indexed do
                                                        yield 
                                                            View.Label(text=item
                                                                , margin = 2.
                                                                , fontSize = 20.
                                                                , textColor = Consts.secondaryTextColor
                                                                , verticalOptions = LayoutOptions.Fill
                                                                , verticalTextAlignment = TextAlignment.Center                                                                        
                                                                , gestureRecognizers = [View.TapGestureRecognizer(command = (fun () -> dispatch (AddSelectGroup item)))]
                                                                
                                                                )
                                                                    
                                                ]
                                            ))
                                        
                            | AudioBookList (key, items) ->
                                
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
                                
                                            
                        
                    ])
                    .GridRow(1)


                if (model.SelectedGroups.Length > 0) then
                    yield View.Button(text = Translations.current.Back,command = (fun ()-> dispatch RemoveLastSelectGroup)).GridRow(2)
                
                let downloadQueueDispatch =
                    DownloadQueueMsg >> dispatch

                yield (DownloadQueue.view model.DownloadQueueModel downloadQueueDispatch).GridRow(3)


                

                if model.IsLoading then 
                    yield Common.createBusyLayer().GridRowSpan(2)

                    
            ]
        )
                
            
