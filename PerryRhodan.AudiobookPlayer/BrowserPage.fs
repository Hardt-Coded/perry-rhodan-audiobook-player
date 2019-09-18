module rec BrowserPage

open Fabulous
open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open System
open System.Net
open System.IO
open Domain
open Common
open Services
open Global


    type ListState = {
        GroupName:string
        ListType: AudioBookListType option
        DisplayedAudioBooks: string []
        SelectedGroups:string []
        // this value is to ensure, that the view function is call, when we trigger the audio item events externally
        DummyUpdateValue:Guid
    }

    type Model = {
        CurrentSessionCookieContainer:Map<string,string> option
        AudioBooks : NameGroupedAudioBooks option
        SelectedGroups:string []
        SelectedGroupItems: AudioBookListType option
        DisplayedAudioBooks: string[]
        LastSelectedGroup: string option
        IsLoading:bool 
        CurrentDownloadProgress: (int * int) option
        //DownloadQueueModel: DownloadQueue.Model 
        ListStates:ListState list
        DummyUpdateValue:Guid
    }

    let updateItemListState item list =
        if (list |> List.exists (fun i -> i.GroupName = item.GroupName)) then
            // update
            list
            |> List.map (fun i ->
                if i.GroupName = item.GroupName then
                    item
                else
                    i
            )
        else
            // add
            list @ [item]
        


    type Msg = 
        | LoadLocalAudiobooks
        | RefreshLocalAudiobooks
        | LoadOnlineAudiobooks 
        | InitAudiobooks of NameGroupedAudioBooks
        //| ShowAudiobooks of AudioBookListType
        | AddSelectGroup of string
        | RemoveLastSelectGroup of string
        | ShowErrorMessage of string
        | ChangeBusyState of bool
        | GoToLoginPage of LoginRequestCameFrom

        | AudioBooksItemMsg of AudioBookItem.Model * AudioBookItem.Msg
        //| DownloadQueueMsg of DownloadQueue.Msg
        | UpdateAudioBookItemList of AudioBookItem.Model
        //| StartDownloadQueue

        | UpdateAudioBook


        //| UpdateDownloadPrograss of (int * int) option
        | DoNothing
        

    type ExternalMsg =
        | OpenLoginPage of LoginRequestCameFrom
        | OpenAudioBookPlayer of AudioBook 
        | OpenAudioBookDetail of AudioBook
        | UpdateAudioBookGlobal  of AudioBookItem.Model *  string
        | DownloadQueueMsg of DownloadQueue.Msg
        | StartDownloadQueue


    let pageRef = ViewRef<ContentPage>()

    let initModel = {
        AudioBooks = None
        CurrentSessionCookieContainer = None
        IsLoading = false
        SelectedGroups = [||]
        DisplayedAudioBooks = [||]
        SelectedGroupItems = None
        LastSelectedGroup = None
        CurrentDownloadProgress = None 
        //DownloadQueueModel = DownloadQueue.initModel None 
        ListStates = []     
        DummyUpdateValue = Guid.Empty
    }


    let loadLocalAudioBooks () =
        async {
            let! audioBooks = DataBase.loadAudioBooksStateFile ()
            match audioBooks with
            | [||] -> return DoNothing
            | _ ->     
                let result = audioBooks |> Domain.Filters.nameFilter
                return (InitAudiobooks result)
        } |> Cmd.ofAsyncMsg
    

    let unbusyCmd = Cmd.ofMsg (ChangeBusyState false)


    let busyCmd = Cmd.ofMsg (ChangeBusyState true)


    module PushModalHelper =

        let private prevPageMap = new System.Collections.Generic.Dictionary<string, ViewElement>()

        let private pushPageInternal dispatch (sr:ContentPage) closeEventMsg (page:ViewElement) =
        
            let p = page.Create() :?> ContentPage
            // a littlebit hacky, but trigger change of the model
            // which knows about every site, only when the shell is currently on the browser page
            // the disappearing event will also triggert, when you hcange the side with the tab button in the bottom
            p.Disappearing.Add(fun e -> 
                let shell = Shell.Current
                let item = shell.CurrentItem
                if item.CurrentItem.Title = Translations.current.TabBarBrowserLabel then
                    dispatch closeEventMsg
                    if prevPageMap.ContainsKey(p.Title) then
                        prevPageMap.Remove(p.Title) |> ignore
            )

            sr.Navigation.PushAsync(p) |> Async.AwaitTask |> Async.StartImmediate
    
        let private tryFindPage (sr:ContentPage) title =
            sr.Navigation.NavigationStack |> Seq.filter (fun e -> e<>null) |> Seq.tryFind (fun i -> i.Title = title)

        
   


        let pushPage dispatch closeMessage pageTitle suppressUpdate (pageRef:ViewRef<ContentPage>) page =
            pageRef.TryValue
            |> Option.map (fun sr ->
                let hasPageInStack =
                    tryFindPage sr pageTitle
                match hasPageInStack with
                | None ->
                    // creates a new page and push it to the modal stack
                    page |> pushPageInternal dispatch sr (closeMessage pageTitle)
                    prevPageMap.Add(pageTitle,page)
                    ()
                | _ ->
                    ()
            )
            |> ignore

        let updatePage dispatch closeMessage pageTitle suppressUpdate (pageRef:ViewRef<ContentPage>) (page:ViewElement) =
            pageRef.TryValue
            |> Option.map (fun sr ->
                let hasPageInStack =
                    tryFindPage sr pageTitle //
                match hasPageInStack with
                | None ->
                    // in case no page on stack found, remove it from the dictionary
                    if (prevPageMap.ContainsKey pageTitle) then
                        prevPageMap.Remove pageTitle |> ignore
                    else 
                        ()
                | Some pushedPage -> 
                    if suppressUpdate then
                        ()
                    else
                        // this uses the new view Element and through model updated Page 
                        // and updates the current viewed from the shel modal stack :) nice!
                        let (hasPrev,prevPage) = prevPageMap.TryGetValue(pageTitle)
                        match hasPrev with
                        | false ->
                            page.Update(pushedPage)
                            if (prevPageMap.ContainsKey(pageTitle)) then
                                prevPageMap.[pageTitle] <- page
                            else
                                prevPageMap.Add(pageTitle,page)
                        | true ->
                            //prevPage.UpdateIncremental(page,pushedPage)
                            page.UpdateIncremental(prevPage,pushedPage)
                            if (prevPageMap.ContainsKey(pageTitle)) then
                                prevPageMap.[pageTitle] <- page
                            else
                                prevPageMap.Add(pageTitle,page)

                        ()
            )
            |> ignore


    module PushCmdHelper =

    // push ne page
        let pushBaseCmd pushAction model newStateItem=
            fun dispatch ->
                let subRef = ViewRef<ContentPage>()
                let newModel = { 
                    model with 
                        ListStates= []
                        LastSelectedGroup = Some newStateItem.GroupName
                        SelectedGroupItems = newStateItem.ListType
                        DisplayedAudioBooks = newStateItem.DisplayedAudioBooks
                }
            
                let cp = Controls.contentPageWithBottomOverlay subRef None (browseView newModel dispatch) false newStateItem.GroupName
                pushAction dispatch RemoveLastSelectGroup newStateItem.GroupName false pageRef cp
                ()
            |> Cmd.ofSub


    let pushNewPageCmd = PushCmdHelper.pushBaseCmd PushModalHelper.pushPage


    let updatePushPage = PushCmdHelper.pushBaseCmd PushModalHelper.updatePage

    // run all open pages and do a pseudo update for refreshing with audio items
    let updateOpenPagesSubCmd model =
        model.ListStates
        |> List.map (fun pageState ->
            let newPageState = { pageState with DummyUpdateValue = Guid.NewGuid()}
            updatePushPage model newPageState
        )
        |> Cmd.batch


    let init () = initModel, Cmd.batch [(loadLocalAudioBooks ()); busyCmd], None


    let rec update msg (model:Model) =
        match msg with
        | LoadLocalAudiobooks ->
            model |> onLoadLocalAudiobooksMsg
        | RefreshLocalAudiobooks ->
            model |> onRefreshLocalAudioBooksMsg
        | LoadOnlineAudiobooks ->
            model |> onLoadOnlineAudiobooksMsg
        | InitAudiobooks ab ->
            model |> onInitAudiobooksMsg ab
        | UpdateAudioBook ->
            // updates the dummy value to ensure that he view function is called to update the audio items
            let newListeState =
                model.ListStates
                |> List.map (fun i -> 
                    if i.DisplayedAudioBooks.Length>0 then
                        { i with DummyUpdateValue = Guid.NewGuid() }
                    else
                        i
                )
            let newModel = {model with ListStates = newListeState}
            newModel, updateOpenPagesSubCmd newModel, None
        //| ShowAudiobooks ab ->
        //    model |> onShowAudiobooksMsg ab
        | AddSelectGroup group ->
            model |> onAddSelectGroupMsg group
        | RemoveLastSelectGroup groupName ->
            model |> onRemoveLastSelectGroupMsg groupName           
        | ShowErrorMessage e ->
            model |> onShowErrorMessageMsg e
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state
        | GoToLoginPage cameFrom ->
            model |> onGotoLoginPageMsg cameFrom
        | AudioBooksItemMsg (abModel, msg) ->
            model |> onProcessAudioBookItemMsg abModel msg
        //| DownloadQueueMsg msg ->
        //    model |> onProcessDownloadQueueMsg msg            
        | UpdateAudioBookItemList abModel ->
            model |> onUpdateAudioBookItemListMsg abModel            
        //| StartDownloadQueue ->
        //    model |> onStartDownloadQueueMsg
        | DoNothing ->
            model |> onDoNothingMsg            

    and onLoadLocalAudiobooksMsg model =
        model, Cmd.batch [(loadLocalAudioBooks ())], None


    and onRefreshLocalAudioBooksMsg model =
        let refreshedAb = 
            DataBase.loadAudioBooksStateFile () 
            |> Async.RunSynchronously
            |> Domain.Filters.nameFilter

        let newStateLists = 
            model.ListStates
            |> List.map (fun state ->
                let newGroupAb =
                    match state.SelectedGroups with
                    | [||] -> 
                        (GroupList refreshedAb)
                    | _ ->                
                        ((GroupList refreshedAb) |> Filters.groupsFilter state.SelectedGroups)
                
                match newGroupAb with
                | GroupList _ ->
                    let newStateItem =
                        {
                            GroupName=state.GroupName
                            ListType = Some newGroupAb
                            DisplayedAudioBooks = [||]
                            SelectedGroups = state.SelectedGroups
                            DummyUpdateValue=Guid.NewGuid()
                        }
                        
                    newStateItem

                | AudioBookList (_,items) ->
                    //let dab = 
                    //    items 
                    //    |> Array.map (fun i -> 
                    //        let (model,_,_) = AudioBookItem.init (i)
                    //        model
                    //    )
                    //    // synchronize with download queue items!
                    //    |> Array.map (
                    //        fun i ->
                    //            let queueItem = 
                    //                model.DownloadQueueModel.DownloadQueue 
                    //                |> List.tryFind (fun q -> q.AudioBook.FullName = i.AudioBook.FullName)
                    //            match queueItem with
                    //            | None -> i
                    //            | Some qi -> qi
                    //    )
                    let newStateItem =
                        {
                            GroupName=state.GroupName
                            ListType = Some newGroupAb
                            DisplayedAudioBooks = items |> Array.map (fun i -> i.FullName)
                            SelectedGroups = state.SelectedGroups
                            DummyUpdateValue=Guid.NewGuid()
                        }
                    newStateItem
                    )    
                
            
        {model with ListStates = newStateLists}, Cmd.none, None
            
    

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
                        let localFileSynced = DataBase.syncPossibleDownloadFolder ab
                        let! saveRes = localFileSynced |> DataBase.insertNewAudioBooksInStateFile

                        // and add audio boot items
                        localFileSynced 
                        |> Array.Parallel.map (fun i -> 
                            let model,_ ,_ = AudioBookItem.init i
                            model
                        ) 
                        |> AudioBookItemProcessor.insertAudiobookItems

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
                            let! saveRes = synchedAb |> DataBase.insertNewAudioBooksInStateFile 
                            match saveRes with
                            | Error e ->
                                return (ShowErrorMessage e)
                            | Ok _ ->                            
                                let! audioBooks = DataBase.loadAudioBooksStateFile ()
                                match audioBooks with
                                | [||] -> return DoNothing
                                | _ ->     
                                    let result = audioBooks |> Domain.Filters.nameFilter                                
                                    return (InitAudiobooks result)
            } |> Cmd.ofAsyncMsg        
        
        
        match model.CurrentSessionCookieContainer with
        | None ->
            model, Cmd.none, Some (OpenLoginPage RefreshAudiobooks)
        | Some cc ->
            let loadAudioBooksCmd = model |> loadOnlineAudioBooks
            model, Cmd.batch [loadAudioBooksCmd; busyCmd], None


    //and filterAudiobooksByGroups model =        
    //    match model.AudioBooks with
    //    | None -> Cmd.ofMsg DoNothing
    //    | Some a -> 
    //        match model.SelectedGroups with
    //        | [||] -> 
    //            Cmd.ofMsg (ShowAudiobooks (GroupList a))
    //        | _ ->                
    //            let filtered =(GroupList a) |> Filters.groupsFilter model.SelectedGroups
    //            Cmd.ofMsg (ShowAudiobooks filtered)


    and onInitAudiobooksMsg ab model =
        let newModel = { model with AudioBooks = Some ab }
        { newModel with 
            SelectedGroupItems = Some (GroupList ab)
            DisplayedAudioBooks = [||]
        }, Cmd.batch [ unbusyCmd ], None
    
 

    and onAddSelectGroupMsg group model =
        let newGroups = [|group|] |> Array.append model.SelectedGroups
        let newModel = {
            model with 
                SelectedGroups = newGroups
                //LastSelectedGroup = Some group
        }
        // filter audio books
        let filteredAb =
            match newModel.AudioBooks with
            | None -> 
                None
            | Some a -> 
                match newModel.SelectedGroups with
                | [||] -> 
                    Some (GroupList a)
                | _ ->                
                    Some ((GroupList a) |> Filters.groupsFilter newModel.SelectedGroups)


        

        match filteredAb with
        | None ->
            {newModel with SelectedGroupItems = None}, Cmd.none, None

        | Some ab ->
            match ab with
            | GroupList _ ->
                let newStateItem =
                    {
                        GroupName=group
                        ListType = Some ab
                        DisplayedAudioBooks = [||]
                        SelectedGroups = newModel.SelectedGroups
                        DummyUpdateValue=Guid.NewGuid()

                    }
                    
                let newStateList = newModel.ListStates |> updateItemListState  newStateItem
                {newModel with ListStates = newStateList}, pushNewPageCmd newModel newStateItem, None

            | AudioBookList (_,items) ->
                let newStateItem =
                    {
                        GroupName=group
                        ListType = Some ab
                        DisplayedAudioBooks = items |> Array.map (fun i -> i.FullName)
                        SelectedGroups = newModel.SelectedGroups
                        DummyUpdateValue=Guid.NewGuid()
                    }
                let newStateList = newModel.ListStates |> updateItemListState  newStateItem


                {newModel with ListStates = newStateList}, pushNewPageCmd newModel newStateItem, None
                    

        


    and onRemoveLastSelectGroupMsg groupname model =
        // remove entry from state list
        let newStateList =
            model.ListStates |> List.filter (fun i->i.GroupName <> groupname)
            

        let last = newStateList |> List.tryLast
        // restore last state
        { model 
            with 
                //LastSelectedGroup = last |> Option.map (fun i -> i.GroupName)
                SelectedGroups = last |> Option.map (fun i -> i.SelectedGroups) |> Option.defaultValue [||]
                ListStates = newStateList 

        },Cmd.none,None
        


    and onShowErrorMessageMsg e model =
        Common.Helpers.displayAlert("Error",e,"OK") |> Async.StartImmediate
        model, Cmd.ofMsg (ChangeBusyState false), None
    

    and onChangeBusyStateMsg state model =
        let newModel = {model with IsLoading = state}
        newModel, updateOpenPagesSubCmd newModel, None
    

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
                    Cmd.none, Some (DownloadQueueMsg (DownloadQueue.Msg.AddItemToQueue mdl))
                | AudioBookItem.ExternalMsg.RemoveFromDownloadQueue mdl ->
                    Cmd.none, Some (DownloadQueueMsg (DownloadQueue.Msg.RemoveItemFromQueue mdl))
                | AudioBookItem.ExternalMsg.OpenLoginPage cameFrom ->
                    Cmd.ofMsg DoNothing, Some (OpenLoginPage cameFrom)
                | AudioBookItem.ExternalMsg.PageChangeBusyState state ->
                    Cmd.ofMsg (ChangeBusyState state), None
                | AudioBookItem.ExternalMsg.OpenAudioBookPlayer ab ->
                    Cmd.none, Some (OpenAudioBookPlayer ab)
                | AudioBookItem.ExternalMsg.OpenAudioBookDetail ab ->
                    Cmd.none, Some (OpenAudioBookDetail ab)
        
        AudioBookItemProcessor.updateAudiobookItem newModel

        model, Cmd.batch [(Cmd.map2 newModel AudioBooksItemMsg cmd); externalCmds ], mainPageMsg
    
    
    and onProcessDownloadQueueMsg msg model =
        //let newModel, cmd, externalMsg = DownloadQueue.update msg model.DownloadQueueModel
        //let (externalCmds,mainPageMsg) =
        //    match externalMsg with
        //    | None -> Cmd.none, None
        //    | Some excmd -> 
        //        match excmd with
        //        | DownloadQueue.ExternalMsg.ExOpenLoginPage cameFrom ->
        //            Cmd.ofMsg DoNothing, Some (OpenLoginPage cameFrom)
        //        | DownloadQueue.ExternalMsg.UpdateAudioBook abModel ->
        //            Cmd.ofMsg (UpdateAudioBookItemList abModel), Some (UpdateAudioBookGlobal (abModel, "Browser"))
        //        | DownloadQueue.ExternalMsg.UpdateDownloadProgress (abModel,progress) ->
        //            Cmd.batch [ Cmd.ofMsg (AudioBooksItemMsg (abModel,(AudioBookItem.Msg.UpdateDownloadProgress progress))); Cmd.ofMsg (UpdateAudioBookItemList abModel) ], None
        //        | DownloadQueue.ExternalMsg.PageChangeBusyState state ->
        //            Cmd.ofMsg (ChangeBusyState state), None

        //{model with DownloadQueueModel = newModel}, Cmd.batch [(Cmd.map DownloadQueueMsg cmd); externalCmds; updateOpenPagesSubCmd model ], mainPageMsg
        model, Cmd.none, None
    
    
    and onUpdateAudioBookItemListMsg abModel model =
        AudioBookItemProcessor.updateAudiobookItem abModel
        {model with DummyUpdateValue = Guid.NewGuid() }, updateOpenPagesSubCmd model, None
    
    
    and onStartDownloadQueueMsg model =
        model, Cmd.ofMsg (DownloadQueueMsg DownloadQueue.Msg.StartProcessing), None

    
    and onDoNothingMsg model =
        model, unbusyCmd, None

    
    

    
    let rec browseView (model: Model) dispatch =
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
                                    verticalOptions=LayoutOptions.Fill,
                                    horizontalOptions=LayoutOptions.Fill,
                                    horizontalTextAlignment=TextAlignment.Center,
                                    verticalTextAlignment=TextAlignment.Center,
                                    textColor = Consts.primaryTextColor,
                                    backgroundColor = Consts.cardColor,
                                    margin=0.).GridRow(0)
                    
                yield View.StackLayout(padding = 10., verticalOptions = LayoutOptions.Start,
                    children = [ 
                        
                        // show refresh button only on category selection
                        if (model.LastSelectedGroup.IsNone) then
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
                                                                , horizontalOptions = LayoutOptions.Fill
                                                                , horizontalTextAlignment= if (model.LastSelectedGroup.IsNone) then TextAlignment.Start else TextAlignment.Center
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
                                                let abItems = AudioBookItemProcessor.getAudioBookItems model.DisplayedAudioBooks |> Async.RunSynchronously
                                                for item in abItems do
                                                    let audioBookItemDispatch =
                                                        let d msg = AudioBooksItemMsg (item,msg)
                                                        d >> dispatch
                                                    yield AudioBookItem.view item audioBookItemDispatch 
                                            ]
                                        )
                                        )
                                
                                            
                        
                    ])
                    .GridRow(1)


                //if (model.SelectedGroups.Length > 0) then
                //    yield View.Button(text = Translations.current.Back,command = (fun ()-> dispatch RemoveLastSelectGroup)).GridRow(2)
                
                //let downloadQueueDispatch =
                //    DownloadQueueMsg >> dispatch

                //yield (DownloadQueue.view model.DownloadQueueModel downloadQueueDispatch).GridRow(3)


                

                if model.IsLoading then 
                    yield Common.createBusyLayer().GridRowSpan(2)

                    
            ]
        )


    let view (model:Model) dispatch =
        
        //// new nav page
        //dependsOn model.ListStates (fun _ listStates ->

            

        //    listStates            
        //    |> List.iteri (fun idx i ->
        //        let subRef = ViewRef<ContentPage>()
        //        // for a nav page remove all selected groups, every nav page stand alone
        //        let newModel = { 
        //            model with 
        //                ListStates= []
        //                LastSelectedGroup = Some i.GroupName
        //                SelectedGroupItems = i.ListType
        //                DisplayedAudioBooks = i.DisplayedAudioBooks
        //        }
        //        let cp = Controls.contentPageWithBottomOverlay subRef None (browseView newModel dispatch) false i.GroupName

        //        let suppressEntry = listStates.Length = (idx + 1) |> not
        //        PushModalHelper.pushOrUpdatePage dispatch RemoveLastSelectGroup i.GroupName suppressEntry pageRef cp
        //        ()
        //    )
        //)
        

        dependsOn model (fun _ m ->
            browseView m dispatch
        )
                
            
