namespace PerryRhodan.AudiobookPlayer

open System.Diagnostics
open Xamarin.Forms
open Plugin.Permissions.Abstractions
open Common
open Domain
open Microsoft.AppCenter
open Microsoft.AppCenter.Crashes
open Microsoft.AppCenter.Analytics
open Fabulous
open Fabulous.XamarinForms
open Services.DownloadService
open System.IO
open AudioBookItem
open System.Text.RegularExpressions

module App = 
    open Xamarin.Essentials
    open System.Net
    //open Fabulous.DynamicViews
    open Global
    
    let mainPageRoute = "mainpage"
    let browserPageRoute = "browserpage"
    let settingsPageRoute = "settingspage"
    let playerPageRoute = "playerpage"
    let downloadQueue = "downloadqueue"

    
    

    type Model = { 
        IsNav:bool

        AudioBookItems : AudioBookItemNew.AudioBookItem []
        CookieContainer: Map<string,string> option

        MainPageModel:MainPage.Model option
        LoginPageModel:LoginPage.Model option
        BrowserPageModels: (BrowserPage.Model) list
        AudioPlayerPageModel:AudioPlayerPage.Model option
        AudioBookDetailPageModel:AudioBookDetailPage.Model option
        SettingsPageModel:SettingsPage.Model option
        SupportFeedbackModel:SupportFeedback.Model option

        DownloadQueueModel: DownloadQueue.Model 
        
        AppLanguage:Language
        CurrentPage: Pages
        NavIsVisible:bool 
        PageStack: Pages list
        BacktapsOnMainSite:int 
        HasAudioItemTrigger:bool 
        StoragePermissionDenied:bool
    }

    type Msg = 
        | Init
        | AudioBookItemsRead of AudioBookItemNew.AudioBookItem []
        | AudioBookItemMsg of (AudioBookItemNew.Model * AudioBookItemNew.Msg)
        | AudioBookItemsUpdated

        | AskForAppPermission
        | MainPageMsg of MainPage.Msg 
        | LoginPageMsg of LoginPage.Msg 

        
        | BrowserPageMsg of (string list * BrowserPage.Msg)
        | BrowserPageModelClosed of string


        | AudioPlayerPageMsg of AudioPlayerPage.Msg
        | AudioBookDetailPageMsg of AudioBookDetailPage.Msg
        | SettingsPageMsg of SettingsPage.Msg
        | SupportFeedbackPageMsg of SupportFeedback.Msg
        | DownloadQueueMsg of DownloadQueue.Msg

        | GotoMainPage
        | GotoBrowserPage
        | ProcessFurtherActionsOnBrowserPageAfterLogin of Map<string,string> * LoginRequestCameFrom
        | GotoAudioPlayerPage of AudioBook
        | CloseAudioPlayerPage
        | GotoLoginPage of LoginRequestCameFrom
        | LoginClosed
        | GotoPermissionDeniedPage
        | GotoSettingsPage
        | GotoFeedbackSupportPage
        | FeedbackSupportPageClosed

        | GotoDownloadPage of DownloadServiceState

        | OpenAudioBookDetailPage of AudioBook
        | CloseAudioBookDetailPage
        | NavigationPopped of Pages
        | UpdateAudioBook of AudioBookItem.Model * string

        | QuitApplication
        

    let routeToShellNavigationState (route:string) =
        ShellNavigationState.op_Implicit route

    

    let createShellContent title route icon content =
        View.ShellContent(
            title=title,
            route=route,
            icon=ImagePath icon,
            content=content,
            shellBackgroundColor=Consts.backgroundColor,
            shellForegroundColor=Consts.primaryTextColor
        )
        

    let createShellSection title route icon content =
        View.ShellSection(
            title=title,
            icon=ImagePath icon,
            items = [
                createShellContent title route icon content
            ]
        )
        

        
    let shellRef = ViewRef<Shell>()

    let browserPageRef = ViewRef<CustomContentPage>()


    let gotoPage routeName =
        async {
            match shellRef.TryValue with
            | Some sr ->
                let pageThere = sr.Items |> Seq.exists (fun i -> i.Route = routeName)
                if Device.RuntimePlatform = Device.iOS || not pageThere then
                    do! Async.Sleep 1000 
                do! sr.GoToAsync(sprintf "//%s" routeName |> routeToShellNavigationState,true) |> Async.AwaitTask
            | None ->
                ()
        } |> Async.StartImmediate


    let closeCurrentModal () =
        shellRef.TryValue
        |> Option.map (fun sr ->
            async {
                let! _ = sr.Navigation.PopModalAsync(true) |> Async.AwaitTask
                return ()
            } |> Async.StartImmediate
        ) |> ignore



    module AudioBookItemHelper =
        
        open AudioBookItemNew

        let createAudioBookItem dispatch audioBookItemModel =
            { 
                AudioBookItemNew.AudioBookItem.Model = audioBookItemModel
                Dispatch = fun msg -> dispatch <| AudioBookItemMsg (audioBookItemModel,msg)
            }


    module Commands =

        let removeDownloadCmd (abModel:AudioBookItemNew.Model) =
            fun _ -> 
                Services.DownloadService.removeDownload <| Services.DownloadService.DownloadInfo.New None abModel.AudioBook
            |> Cmd.ofSub


        let downloadAudiobookCmd (audiobookItem:AudioBookItemNew.Model) (model:Model) =
            fun dispatch -> 
                match model.CookieContainer with                
                | None ->
                    // deactivate loading spinner if login needed, 
                    // because the startProcessing checks if loading already is actived, 
                    // and do not start the download at all
                    //dispatch <| DeactivateLoading audiobookItemModel;
                    //dispatch <| ChangeQueueState Idle
                    dispatch <| GotoLoginPage DownloadAudioBook
                    
                | Some cc ->

                    Services.DownloadService.startService ()

                    //Services.DownloadService.registerShutDownEvent (fun () -> async { dispatch <| ChangeQueueState Idle } )

                    Services.DownloadService.addInfoListener "downloadQueueListener" (fun info ->
                        async {

                            let abModel =
                                model.AudioBookItems
                                |> Array.tryFind (fun i -> i.Model.AudioBook.Id = info.AudioBook.Id)

                            match abModel,info.State with
                            | Some abModel, Services.DownloadService.Running (all,current) ->
                                dispatch <| AudioBookItemMsg (abModel.Model, AudioBookItemNew.UpdateDownloadProgress (current,all))
                            | Some abModel, Services.DownloadService.Open ->
                                ()
                            | Some abModel, Services.DownloadService.Finished result ->
                                dispatch <| AudioBookItemMsg (abModel.Model, AudioBookItemNew.DownloadCompleted result)
                            | _, _ ->
                                ()
                        }
                    )

                    Services.DownloadService.registerErrorListener (fun (info,error) ->
                        async {
                            let abModel =
                                model.AudioBookItems
                                |> Array.tryFind (fun i -> i.Model.AudioBook.Id = info.AudioBook.Id)

                            match abModel, error with
                            | Some abModel, ComError.SessionExpired msg ->
                                Services.DownloadService.shutDownService ()
                                //dispatch <| DeactivateLoading abModel;
                                //dispatch <| ChangeQueueState Idle
                                dispatch <| GotoLoginPage DownloadAudioBook

                            | Some abModel, ComError.Other msg ->
                                do! Common.Helpers.displayAlert(Translations.current.Error, msg,"OK")

                            | Some abModel, ComError.Network msg ->
                                // the download service restarts network error automatically
                                do! Common.Helpers.displayAlert(Translations.current.Error, msg,"OK")
                                ()
                            | Some abModel, ComError.Exception e ->
                                let ex = e.GetBaseException()
                                let msg = ex.Message + "|" + ex.StackTrace
                                do! Common.Helpers.displayAlert(Translations.current.Error, msg,"OK")
                                    
                            | _, _ ->
                                ()
                        }
                    )

                    Services.DownloadService.addDownload <| Services.DownloadService.DownloadInfo.New (Some cc) audiobookItem.AudioBook

                    Services.DownloadService.startDownloads ()
                        
            |> Cmd.ofSub


    let init () = 
        let initModel = { 
            IsNav = false

            AudioBookItems = [||]
            CookieContainer = None

            MainPageModel = Some MainPage.emptyModel
            LoginPageModel = None
            BrowserPageModels = []
            AudioPlayerPageModel = None 
            AudioBookDetailPageModel = None 
            SettingsPageModel = None 
            SupportFeedbackModel = None

            DownloadQueueModel = DownloadQueue.initModel None

            AppLanguage = English
            CurrentPage = MainPage
            NavIsVisible = false 
            PageStack = [ MainPage]
            BacktapsOnMainSite = 0 
            HasAudioItemTrigger = false
            StoragePermissionDenied = false
        }

        let cmd =
            match Device.RuntimePlatform with
            | Device.Android | Device.iOS ->
                Cmd.ofMsg AskForAppPermission
            | _ ->
                Cmd.ofMsg Init

        

        initModel, cmd


    let rec update msg model =
        match msg with
        | Init ->
            model |> onInitMsg
        | AudioBookItemsRead items ->

            // check if download Service is running
            let checkDownloadServiceCmd =
                async {
                    let! state = Services.DownloadService.getCurrentState ()
                    return state 
                        |> Option.map (fun state ->
                            GotoDownloadPage state
                        )
                            
                }
                |> Cmd.ofAsyncMsgOption
                    

            // check if audio player is current available, if so, init AudioPlayer as well
            let checkAudioPlayerRunningCmds =
                async {
                    let audioPlayer = DependencyService.Get<AudioPlayer.IAudioPlayer>()
                    let! info = audioPlayer.GetCurrentState();
                    return info
                        |> Option.map (fun state -> 
                            GotoAudioPlayerPage state.AudioBook
                        )
                } |> Cmd.ofAsyncMsgOption
            
            let mainPageModel, mainPageMsg = MainPage.init items
            let browserPageModel, browserPageMsg = BrowserPage.init [] items
            let settingsPageModel, settingsPageMsg, _ = SettingsPage.init shellRef true

            let newModel = { 
                model with
                    AudioBookItems = items
                    MainPageModel = Some mainPageModel
                    BrowserPageModels = [ browserPageModel ]
                    SettingsPageModel = Some settingsPageModel 
            }
            
            let cmds =
                Cmd.batch [ 
                    (Cmd.map MainPageMsg mainPageMsg)
                    (Cmd.map BrowserPageMsg browserPageMsg)
                    (Cmd.map SettingsPageMsg settingsPageMsg)
                    checkAudioPlayerRunningCmds
                    checkDownloadServiceCmd
                ]

            newModel, cmds

        | AudioBookItemsUpdated ->
            let mainPageModel, mainPageMsg = MainPage.init model.AudioBookItems

            let browserStates =
                model.BrowserPageModels
                |> List.map (fun i -> BrowserPage.init i.SelectedGroups model.AudioBookItems)

            let newModel = { 
                model with
                    MainPageModel = Some mainPageModel
                    BrowserPageModels = browserStates |> List.map fst
            }

            let cmds =
                Cmd.batch [ 
                    (Cmd.map MainPageMsg mainPageMsg)
                    yield! 
                        browserStates
                        |> List.map (fun (m,c)-> Cmd.map (fun msg -> BrowserPageMsg (m.SelectedGroups,msg)) c)
                    
                ]

            newModel, cmds


        | AudioBookItemMsg (abModel, AudioBookItemNew.AddToDownloadQueue) ->
            let (model,cmd) =
                model |> onProcessAudioBookItemMsg (abModel, AudioBookItemNew.AddToDownloadQueue)
            let cmds = 
                Cmd.batch [
                    cmd
                    Commands.downloadAudiobookCmd abModel model
                ]
            model, cmds

        | AudioBookItemMsg (abModel, AudioBookItemNew.RemoveFromDownloadQueue) ->
            let (model,cmd) =
                model |> onProcessAudioBookItemMsg (abModel, AudioBookItemNew.RemoveFromDownloadQueue)
            let cmds = 
                Cmd.batch [
                    cmd
                    Commands.removeDownloadCmd abModel
                ]
            model, cmds

        | AudioBookItemMsg msg ->
            model |> onProcessAudioBookItemMsg msg

        | AskForAppPermission ->
            model |> onAskForAppPermissionMsg

        

        | MainPageMsg msg ->
            model |> onProcessMainPageMsg msg
        | LoginPageMsg msg ->
            model |> onProcessLoginPageMsg msg

        | BrowserPageMsg (currentGroups, BrowserPage.AddSelectGroup newGroup) ->
            let newGroups = currentGroups @ [ newGroup ]
            let (newPageModel,cmd) = BrowserPage.init newGroups model.AudioBookItems
            let newModel = { 
                model with
                    BrowserPageModels = model.BrowserPageModels @ [ newPageModel ]
            }
            
            let cmd =
                Cmd.map (fun msg -> BrowserPageMsg (newGroups,msg)) cmd
            
            newModel, cmd
            

        | BrowserPageMsg msg ->
            model |> onProcessBrowserPageMsg msg

        | BrowserPageModelClosed groups ->
            let concatStr (str:string list) = System.String.Join ("", str)

            let newModels =
                model.BrowserPageModels
                |> List.filter (fun i -> (i.SelectedGroups |> concatStr) <> groups)

            { model with BrowserPageModels = newModels }, Cmd.none


        | AudioPlayerPageMsg msg ->
            model |> onProcessAudioPlayerMsg msg
        | AudioBookDetailPageMsg msg ->
            model |> onProcessAudioBookDetailPageMsg msg
        | SettingsPageMsg msg ->
            model |> onProcessSettingsPageMsg msg
        | SupportFeedbackPageMsg msg ->
            model |> onSupportFeedbackPageMsg msg
        | DownloadQueueMsg msg ->
            model |> onProcessDownloadQueueMsg msg
        | GotoMainPage ->
            model |> onGotoMainPageMsg
        | GotoLoginPage cameFrom ->
            model |> onGotoLoginPageMsg cameFrom
        | LoginClosed ->
            model |> onLoginClosed
        | GotoBrowserPage ->
            model |> onGotoBrowserPageMsg
        | GotoAudioPlayerPage audioBook ->
            model |> onGotoAudioPageMsg audioBook
        | CloseAudioPlayerPage ->
            model |> onCloseAudioPlayerPageMsg
        | GotoPermissionDeniedPage ->
            model |> onGotoPermissionDeniedMsg
        | GotoSettingsPage ->
            model |> onGotoSettingsPageMsg
        | GotoFeedbackSupportPage ->
            model |> onGotoFeedbackSupportPageMsg
        | GotoDownloadPage state ->
            model |> onGotoDownloadPage state
        | FeedbackSupportPageClosed ->
            model |> onFeedbackSupportPageClosedPage
        | OpenAudioBookDetailPage ab ->
            model |> onOpenAudioBookDetailPage ab
        | CloseAudioBookDetailPage ->
            model |> onCloseAudioBookDetailPage
        | NavigationPopped page ->
            //model |> onNavigationPoppedMsg page
            // weg!
            model, Cmd.none
        | ProcessFurtherActionsOnBrowserPageAfterLogin (cc,cameFrom) ->
            model |> onSetBrowserPageCookieContainerAfterSucceedLoginMsg cc cameFrom
        | UpdateAudioBook (ab, cameFrom) ->
            model |> onUpdateAudioBookMsg ab cameFrom
        | QuitApplication ->
            model |> onQuitApplication


    and onInitMsg model =
        let loadAudioBookItemsCmd =
            fun dispatch ->
                async {
                    let! ab = Services.DataBase.loadAudioBooksStateFile ()
                    let itemsWithDispatcher =
                        ab 
                        |> Array.map (fun i -> AudioBookItemNew.init i) 
                        |> Array.map (fun i -> AudioBookItemHelper.createAudioBookItem dispatch i)
                        

                    dispatch <| AudioBookItemsRead itemsWithDispatcher
                }
                |> Async.Start

            |> Cmd.ofSub

        model, loadAudioBookItemsCmd


    and onAskForAppPermissionMsg model =
        let ask = 
            async { 
                let! res = Common.Helpers.askPermissionAsync Permission.Storage
                if res then return Init 
                else return GotoPermissionDeniedPage
            } |> Cmd.ofAsyncMsg
        
        model, ask

    
    and onQuitApplication model =

        let quitApp () =
            try
                Process.GetCurrentProcess().CloseMainWindow() |> ignore
            with
            | _ as ex ->
                Crashes.TrackError ex

        let quitAppWithMessage () =
            async {
                let! yes = Common.Helpers.displayAlertWithConfirm(Translations.current.QuitQuestionTitle,Translations.current.QuitQuestionMessage,Translations.current.Yes,Translations.current.No)
                if yes then
                    quitApp()
                return None
            } |> Cmd.ofAsyncMsgOption

        let isPlayerRunning = 
            model.AudioPlayerPageModel 
            |> Option.map (fun i -> i.CurrentState = AudioPlayer.Playing)
            |> Option.defaultValue false

        if isPlayerRunning then
            model, quitAppWithMessage()            
        else
            quitApp()
            model, Cmd.none
        
    

    and onUpdateAudioBookMsg ab cameFrom model =
        model, Cmd.none
        ////let browserPageMsg = BrowserPageMsg (BrowserPage.Msg.UpdateAudioBookItemList ab) |> Cmd.ofMsg
        //let bpCmd = BrowserPageMsg BrowserPage.Msg.UpdateAudioBook |> Cmd.ofMsg
        ////let mpCmd = MainPageMsg MainPage.Msg.UpdateAudioBook |> Cmd.ofMsg
        //let cmd = Cmd.batch [ bpCmd; (* mpCmd; *) ] //browserPageMsg]

        //let audioPlayerModel =
        //    match model.AudioPlayerPageModel with
        //    | None -> None
        //    | Some amdl ->
        //        if amdl.AudioBook.FullName = ab.AudioBook.FullName then
        //            if ab.AudioBook.State.Downloaded then
                
        //                Some ({amdl with AudioBook = ab.AudioBook})
        //            else
        //                None
        //        else
        //            model.AudioPlayerPageModel

        //{model with AudioPlayerPageModel = audioPlayerModel}, cmd        
            
    
    and onProcessAudioBookItemMsg msg model =
        let (msgItem, msg) = msg
        let item = 
            model.AudioBookItems
            |> Array.tryFind (fun i -> i.Model.AudioBook.Id = msgItem.AudioBook.Id)

        match item with
        | None ->
            model, Cmd.none
        | Some itemModel ->
            let itemModel,cmd = AudioBookItemNew.update msg itemModel.Model
            let cmd = Cmd.map (fun i -> AudioBookItemMsg (itemModel,i)) cmd
            let audioBooks =
                model.AudioBookItems
                |> Array.map (fun i ->
                    if i.Model.AudioBook.Id = itemModel.AudioBook.Id then 
                        { i with Model = itemModel }
                    else i
                )

            let cmds =
                Cmd.batch [
                    cmd
                    Cmd.ofMsg AudioBookItemsUpdated
                ]

            { model with AudioBookItems = audioBooks }, cmds
    
    
    
    and onProcessMainPageMsg msg model =
        match model.MainPageModel with
        | None ->
            model,Cmd.none
        | Some mdl ->
            //let mainPageExternalMsgToCommand externalMsg =
            //    match externalMsg with
            //    | None -> Cmd.none
            //    | Some excmd -> 
            //        match excmd with                   
            //        | MainPage.ExternalMsg.OpenAudioBookPlayer ab ->
            //            Cmd.ofMsg (GotoAudioPlayerPage ab)
            //        | MainPage.ExternalMsg.UpdateAudioBookGlobal (ab,cameFrom) ->
            //            Cmd.ofMsg (UpdateAudioBook (ab, cameFrom))
            //        | MainPage.ExternalMsg.OpenAudioBookDetail ab ->
            //            Cmd.ofMsg (OpenAudioBookDetailPage ab)

            let m,cmd = MainPage.update msg mdl     
            //let externalCmds =
            //    externalMsg |> mainPageExternalMsgToCommand        

            {model with MainPageModel = Some m; HasAudioItemTrigger = true}, Cmd.batch [(Cmd.map MainPageMsg cmd) ]


    and onProcessLoginPageMsg msg model =
        let loginPageExternalMsgToCommand externalMsg =
            match externalMsg with
            | None -> Cmd.none
            | Some excmd -> 
                match excmd with
                | LoginPage.ExternalMsg.GotoForwardToBrowsing (cookies,cameFrom) ->                    
                    Cmd.batch ([ Cmd.ofMsg (ProcessFurtherActionsOnBrowserPageAfterLogin (cookies,cameFrom)) ])

        match model.LoginPageModel with
        | Some loginPageModel ->
            let m,cmd, externalMsg = LoginPage.update msg loginPageModel

            let externalCmds =
                externalMsg |> loginPageExternalMsgToCommand

            let updateLoginPageCmd =
                fun dispatch ->
                    ModalHelpers.updateLoginModal dispatch LoginPageMsg LoginClosed shellRef m
                |> Cmd.ofSub
            
            {model with LoginPageModel = Some m}, Cmd.batch [updateLoginPageCmd;(Cmd.map LoginPageMsg cmd); externalCmds ]
        | None -> model, Cmd.none   


    //and browserExternalMsgToCommand externalMsg =
    //    match externalMsg with
    //    | None -> Cmd.none
    //    | Some excmd -> 
    //        match excmd with
    //        | BrowserPage.ExternalMsg.OpenLoginPage cameFrom ->
    //            Cmd.ofMsg (GotoLoginPage cameFrom)
    //        | BrowserPage.ExternalMsg.OpenAudioBookPlayer ab ->
    //            Cmd.ofMsg (GotoAudioPlayerPage ab)
    //        | BrowserPage.ExternalMsg.UpdateAudioBookGlobal (ab,cameFrom) ->
    //            Cmd.ofMsg (UpdateAudioBook (ab,cameFrom))
    //        | BrowserPage.ExternalMsg.OpenAudioBookDetail ab ->
    //            Cmd.ofMsg (OpenAudioBookDetailPage ab)
    //        | BrowserPage.ExternalMsg.DownloadQueueMsg dqMsg ->
    //            Cmd.ofMsg (DownloadQueueMsg dqMsg)
    //        | BrowserPage.ExternalMsg.StartDownloadQueue ->
    //            Cmd.ofMsg (DownloadQueueMsg DownloadQueue.Msg.StartProcessing)
        

    and onProcessBrowserPageMsg (groups, msg) model =

        let bModel = 
            model.BrowserPageModels
            |> List.tryFind (fun i -> i.SelectedGroups = groups)

        match bModel with
        | None ->
            model,Cmd.none
        | Some mdl ->
            let m,cmd = BrowserPage.update msg mdl
            let models =
                model.BrowserPageModels
                |> List.map (fun i -> if i.SelectedGroups = groups then m else i)

            let cmd =
                Cmd.map (fun m -> BrowserPageMsg (groups,m)) cmd
            
            {model with BrowserPageModels = models}, cmd


    and onProcessAudioPlayerMsg msg model =

        let audioPlayerExternalMsgToCommand externalMsg =
            match externalMsg with
            | None -> Cmd.none
            | Some excmd -> 
                Cmd.none    

        match model.AudioPlayerPageModel with
        | Some audioPlayerPageModel ->
            let m,cmd,externalMsg = AudioPlayerPage.update msg audioPlayerPageModel

            let externalCmds = 
                externalMsg |> audioPlayerExternalMsgToCommand

            {model with AudioPlayerPageModel = Some m}, Cmd.batch [(Cmd.map AudioPlayerPageMsg cmd); externalCmds]

        | None -> model, Cmd.none

    and onProcessAudioBookDetailPageMsg msg model =

        let audioBookDetailPageExternalMsgToCommand externalMsg =
            match externalMsg with
            | None -> Cmd.none
            | Some excmd -> 
                Cmd.none   

        match model.AudioBookDetailPageModel with
        | Some audioBookDetailPageModel ->
            let m,cmd,externalMsg = AudioBookDetailPage.update msg audioBookDetailPageModel

            let externalCmds = 
                externalMsg |> audioBookDetailPageExternalMsgToCommand

            let updateAudioBookDetailPageCmd =
                fun dispatch ->
                    ModalHelpers.updateDetailModal dispatch AudioBookDetailPageMsg CloseAudioBookDetailPage shellRef m
                |> Cmd.ofSub

            {model with AudioBookDetailPageModel = Some m}, Cmd.batch [updateAudioBookDetailPageCmd; (Cmd.map AudioBookDetailPageMsg cmd); externalCmds ]

        | None -> model, Cmd.none



    and onProcessSettingsPageMsg msg model =
        match model.SettingsPageModel with
        | None ->
            model,Cmd.none
        | Some mdl ->
            let settingsPageExternalMsgToCommand externalMsg =
                match externalMsg with
                | None -> Cmd.none
                | Some excmd -> 
                    Cmd.none  
        
            let m,cmd,externalMsg = SettingsPage.update msg mdl
            let externalCmds = 
                externalMsg |> settingsPageExternalMsgToCommand

            let feedbackPageCmd =
                if msg = SettingsPage.Msg.OpenFeedbackPage then Cmd.ofMsg GotoFeedbackSupportPage else Cmd.none

            {model with SettingsPageModel = Some m}, Cmd.batch [(Cmd.map SettingsPageMsg cmd); externalCmds; feedbackPageCmd]


    and onSupportFeedbackPageMsg msg model =
         match model.SupportFeedbackModel with
         | None ->
             model,Cmd.none
         | Some mdl ->
             
             let m,cmd = SupportFeedback.update msg mdl
             let updateFeedbackPageCmd =
                 fun dispatch ->
                     ModalHelpers.updateFeedbackModal dispatch SupportFeedbackPageMsg FeedbackSupportPageClosed shellRef m
                 |> Cmd.ofSub
             
             if msg = SupportFeedback.Msg.SendSuccessful then 
                 Common.ModalBaseHelpers.closeCurrentModal shellRef


             {model with SupportFeedbackModel = Some m}, Cmd.batch [updateFeedbackPageCmd;(Cmd.map SupportFeedbackPageMsg cmd)]


    and onProcessDownloadQueueMsg msg model =
        let newModel, cmd, externalMsg = DownloadQueue.update msg model.DownloadQueueModel
        let mainCmds =
            match externalMsg with
            | None -> Cmd.none
            | Some excmd -> 
                match excmd with
                | DownloadQueue.ExternalMsg.ExOpenLoginPage cameFrom ->
                    Cmd.ofMsg (GotoLoginPage cameFrom)
                | DownloadQueue.ExternalMsg.UpdateAudioBook abModel ->
                    Cmd.ofMsg (UpdateAudioBook (abModel,""))
                | DownloadQueue.ExternalMsg.UpdateDownloadProgress (abModel,progress) ->
                    Cmd.ofMsg (UpdateAudioBook (abModel,""))
                | DownloadQueue.ExternalMsg.PageChangeBusyState state ->
                    Cmd.none

        { model with DownloadQueueModel = newModel}, Cmd.batch [(Cmd.map DownloadQueueMsg cmd); mainCmds ]


    and onGotoMainPageMsg model =
        match shellRef.TryValue with
        | Some sr ->
            sr.GoToAsync("mainpage" |> routeToShellNavigationState) |> Async.AwaitTask |> Async.RunSynchronously
        | None ->
            ()
        model, Cmd.none


    and onGotoLoginPageMsg cameFrom model =
        let m,cmd = LoginPage.init cameFrom

        let openLoginPageCmd =
            fun dispatch ->
                ModalHelpers.pushLoginModal dispatch LoginPageMsg LoginClosed shellRef m
            |> Cmd.ofSub

        {model with LoginPageModel = Some m},Cmd.batch [ openLoginPageCmd; (Cmd.map LoginPageMsg cmd)  ]
        

    and onLoginClosed model =
        {model with LoginPageModel = None},Cmd.none


    and onGotoBrowserPageMsg model =
        gotoPage browserPageRoute
        model,Cmd.none


    and onGotoAudioPageMsg audioBook model =
        let brandNewPage () = 
            let m,cmd = AudioPlayerPage.init audioBook
            {model with CurrentPage = AudioPlayerPage; AudioPlayerPageModel = Some m}, Cmd.batch [ (Cmd.map AudioPlayerPageMsg cmd) ]
        
        gotoPage playerPageRoute

        match model.AudioPlayerPageModel with
        | None ->
            brandNewPage()
        | Some abModel ->
            if (abModel.AudioBook <> audioBook) then
                if abModel.CurrentState = AudioPlayer.Playing then
                    // stop audio player
                    AudioPlayerPage.audioPlayer.StopAudio()
                brandNewPage()
            else
                model, Cmd.none


    and onCloseAudioPlayerPageMsg model =
        {model with AudioPlayerPageModel = None}, Cmd.none


    and onGotoPermissionDeniedMsg model =
        async {
            do! Common.Helpers.displayAlert(Translations.current.Error,"Ohne Freigabe auf den Speicher funktioniert die App nicht. Sie wird jetzt beendet.", "OK") 
            try
                Process.GetCurrentProcess().CloseMainWindow() |> ignore
            with
            | _ as ex ->
                Crashes.TrackError ex
            return ()
        } |> Async.StartImmediate
        
        model, Cmd.none


    and onGotoSettingsPageMsg model =
        gotoPage settingsPageRoute
        {model with CurrentPage = SettingsPage}, Cmd.none


    and onGotoFeedbackSupportPageMsg model =
        let m,cmd = SupportFeedback.init ()
        
        let openFeedbackPageCmd =
            fun dispatch ->
                ModalHelpers.pushFeedbackModal dispatch Msg.SupportFeedbackPageMsg FeedbackSupportPageClosed shellRef m
            |> Cmd.ofSub
        
        {model with SupportFeedbackModel = Some m},Cmd.batch [ openFeedbackPageCmd; (Cmd.map SupportFeedbackPageMsg cmd)  ]
        

    and onGotoDownloadPage state model =
        let (dpModel,cmd) = DownloadQueue.initFromDownloadService state
        let cmd = Cmd.map DownloadQueueMsg cmd

        { model with DownloadQueueModel = dpModel }, cmd


    and onFeedbackSupportPageClosedPage model =
        {model with SupportFeedbackModel = None }, Cmd.none



    and onOpenAudioBookDetailPage audiobook model =
        let m,cmd = AudioBookDetailPage.init audiobook 
        
        let openAudioBookDetailPageCmd =
            fun dispatch ->
                ModalHelpers.pushDetailModal dispatch AudioBookDetailPageMsg CloseAudioBookDetailPage shellRef m
            |> Cmd.ofSub

        { model with AudioBookDetailPageModel = Some m }, Cmd.batch [ (Cmd.map AudioBookDetailPageMsg cmd); openAudioBookDetailPageCmd ]

    and onCloseAudioBookDetailPage model =
        {model with AudioBookDetailPageModel = None }, Cmd.none


    and onSetBrowserPageCookieContainerAfterSucceedLoginMsg cc cameFrom model =

        let model,cmd = 
            match cameFrom with
            | RefreshAudiobooks ->
                let models = 
                    model.BrowserPageModels
                    |> List.map (fun browserModel ->
                        { browserModel with CurrentSessionCookieContainer = Some cc}
                    )
                let model = { model with BrowserPageModels = models }
                model, Cmd.ofMsg (BrowserPageMsg (models.[0].SelectedGroups, BrowserPage.Msg.LoadOnlineAudiobooks))

            | DownloadAudioBook ->
                let downloadQueueModel = { model.DownloadQueueModel with CurrentSessionCookieContainer = Some cc}
                { model with DownloadQueueModel = downloadQueueModel}, Cmd.ofMsg (DownloadQueueMsg DownloadQueue.Msg.StartProcessing)

        // and close Login Modal
        closeCurrentModal ()
        // set here login page model to None to avoid reopening of the loginPage
        { model with LoginPageModel = None; CookieContainer = Some cc }, cmd
        
            



    let subscription model =
        Cmd.ofSub (fun dispatch ->

            // avoid double registration of event handlers

            if (not AudioBookItemProcessor.abItemUpdatedEvent.HasListeners) then
                AudioBookItemProcessor.Events.onAbItemUpdated.Add(fun i ->
                    //dispatch (MainPageMsg (MainPage.Msg.UpdateAudioBook))
                    //dispatch (BrowserPageMsg (BrowserPage.Msg.UpdateAudioBook))
                    dispatch AudioBookItemsUpdated
            )
            // when updating an audio book, that update the underlying audio book in the Item
            if (not Services.DataBase.storageProcessorAudioBookUpdatedEvent.HasListeners) then
                Services.DataBase.Events.storageProcessorOnAudiobookUpdated.Add(fun item ->
                    AudioBookItemProcessor.updateUnderlyingAudioBookInItem item
                )

            if (not Services.DataBase.storageProcessorAudioBookDeletedEvent.HasListeners) then
                Services.DataBase.Events.storageProcessorOnAudiobookDeleted.Add(fun item->
                    AudioBookItemProcessor.deleteAudioBookInItem item
                )

            if (not Services.DataBase.storageProcessorAudioBookAdded.HasListeners) then
                Services.DataBase.Events.storageProcessorOnAudiobookAdded.Add(fun items ->
                    AudioBookItemProcessor.insertAudiobooks items
                )

            AudioPlayer.InformationDispatcher.audioPlayerStateInformationDispatcher.Post(AudioPlayer.InformationDispatcher.RegisterShutDownEvent (fun _ -> async { return dispatch CloseAudioPlayerPage }))
        )

        


    let view (model: Model) dispatch =
        // it's the same as  (MainPageMsg >> dispatch)
        // I had to do this, to get m head around this
        let mainPageDispatch mainMsg =
            let msg = mainMsg |> MainPageMsg
            dispatch msg

        let audioPlayerOverlay apmodel =
            dependsOn apmodel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun (m:AudioPlayerPage.Model) ->
                        let cmd = m.AudioBook |> GotoAudioPlayerPage 
                        (AudioPlayerPage.viewSmall 
                            (fun () -> dispatch cmd) 
                            m 
                            (AudioPlayerPageMsg >> dispatch))                        
                )            
            )


        let mainPage = 
            model.MainPageModel
            |> Option.map (fun mainPageModel ->
                dependsOn (mainPageModel, model.AudioPlayerPageModel) (fun _ (mdl, abMdl)->
                    (Controls.contentPageWithBottomOverlay
                        AudioPlayerPage.pageRef
                        (audioPlayerOverlay abMdl)
                        (MainPage.view mdl (mainPageDispatch))
                        mainPageModel.IsLoading
                        Translations.current.MainPage)
                )
            )
            


        // try show login page, if necessary
        //model.LoginPageModel
        //|> Option.map (
        //    fun m -> 
        //        m |> ModalHelpers.showLoginModal dispatch LoginPageMsg LoginClosed shellRef 
        //) |> ignore


        //// show audio book detail page modal
        //model.AudioBookDetailPageModel
        //|> Option.map (
        //    fun m -> 
        //        m |> ModalHelpers.showDetailModal dispatch AudioBookDetailPageMsg CloseAudioBookDetailPage shellRef 
        //) |> ignore
        

        let (browserPage,rest) =
            let createPage ref (browserModel:BrowserPage.Model) =
                let bDispatch msg =
                    dispatch <| BrowserPageMsg (browserModel.SelectedGroups, msg)

                let newView =
                    (BrowserPage.view browserModel bDispatch)

                (Controls.contentPageWithBottomOverlay 
                    ref
                    (audioPlayerOverlay model.AudioPlayerPageModel)
                    newView
                    browserModel.IsLoading
                    (String.concatStr browserModel.SelectedGroups)
                    )

            match model.BrowserPageModels with
            | [] ->
                None, []
            | [x] ->
                Some <| createPage browserPageRef x, []
            | head::tail ->
                let h = Some <| createPage browserPageRef head
                let t = tail |> List.map (fun m -> createPage BrowserPage.pageRef m, m.SelectedGroups)
                h, t
            
        
        
        rest
        |> List.iteri (
            fun idx (bPage,selectedGroup) ->
                
                //let modalStackPage =
                //    browserPageRef.Value.Navigation.ModalStack
                //    |> Seq.tryItem idx

                //match modalStackPage with
                //| None ->
                //    let pageTitle = selectedGroup |> String.concatStr
                //    ModalHelpers.BrowserPageModal.pushPage dispatch BrowserPageModelClosed pageTitle false browserPageRef bPage
                //| Some page ->
                let pageTitle = selectedGroup |> String.concatStr
                ModalHelpers.BrowserPageModal.updatePage dispatch BrowserPageModelClosed pageTitle false browserPageRef bPage
        )

        
            

        
        
        let audioPlayerPage =
            dependsOn model.AudioPlayerPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m ->
                        (AudioPlayerPage.view m (AudioPlayerPageMsg >> dispatch))
                )
            )

        
        let settingsPage =
            model.SettingsPageModel
            |> Option.map (fun settingsModel ->
                (SettingsPage.view settingsModel (SettingsPageMsg >> dispatch))
            )
        

        View.Shell(
            ref = shellRef,     
            flyoutBehavior=FlyoutBehavior.Disabled,
            title= "Eins A Medien",
            shellForegroundColor=Color.White,
            navigating=(fun e ->
                ()
            ),
            // makenav bar invisible
            created=(fun e -> Shell.SetNavBarIsVisible(e,false)),
            items=[
                

                yield View.ShellItem(                    
                    shellUnselectedColor = Consts.secondaryTextColor,
                    shellTabBarBackgroundColor=Consts.cardColor,
                    items=[
                        match mainPage with
                        | None -> ()
                        | Some mainPage -> 
                            yield createShellContent Translations.current.TabBarStartLabel mainPageRoute "home_icon.png" mainPage

                        match browserPage with
                        | None -> ()
                        | Some browserPage -> 
                            yield createShellContent Translations.current.TabBarBrowserLabel browserPageRoute "browse_icon.png" browserPage

                        match settingsPage with
                        | None -> ()
                        | Some settingsPage -> 
                            yield createShellContent Translations.current.TabBarOptionsLabel settingsPageRoute "settings_icon.png" settingsPage

                        match audioPlayerPage with
                        | Some ap ->
                            yield createShellContent Translations.current.TabBarPlayerLabel playerPageRoute "player_icon.png" ap
                        | None ->
                            ()

                        
                        let dqView = DownloadQueue.view model.DownloadQueueModel (DownloadQueueMsg >> dispatch)

                        match model.DownloadQueueModel.State with
                        | DownloadQueue.QueueState.Downloading -> 
                            let icon = "download_icon_running.png"
                            yield createShellContent "Downloads" downloadQueue icon dqView
                        | DownloadQueue.QueueState.Paused -> 
                            let icon = "download_icon_error.png"
                            yield createShellContent "Downloads" downloadQueue icon dqView
                        | DownloadQueue.QueueState.Idle -> 
                            ()
                        
                        
                        

                        match mainPage,browserPage,settingsPage,audioPlayerPage with
                        | None, None, None, None ->
                            yield View.ShellContent(route="emptypage",content=View.ContentPage(content=View.Label(text="...")))
                        | _ ->
                            ()
                    ]
                    
                )

                yield View.ShellContent(route="permissiondeniedpage",content=View.ContentPage(content=View.Label(text="...")))

                match model.DownloadQueueModel.State with
                | DownloadQueue.QueueState.Idle -> 
                    let dqView = DownloadQueue.view model.DownloadQueueModel (DownloadQueueMsg >> dispatch)
                    yield createShellContent "Downloads" downloadQueue "" dqView
                | _ -> ()


                
            ]
        )
                


    let program = Program.mkProgram init update view

type MainApp () as app = 
    inherit Application ()
   
    do 
        AppCenter.Start(sprintf "ios=(...);android=%s" Global.appcenterAndroidId, typeof<Analytics>, typeof<Crashes>)
        // Display Error when somthing is with the storage processor
        if not Services.DataBase.storageProcessorErrorEvent.HasListeners then
            Services.DataBase.Events.storageProcessorOnError.Add(fun e ->
                async {
                    let message = 
                        sprintf "Es ist ein Fehler mit der Datenbank aufgetreten. (%s). Das Programm wird jetzt beendet. Versuchen Sie es noch einmal oder melden Sie sich bin Support." e.Message

                    do! Common.Helpers.displayAlert(Translations.current.Error,message, "OK") 
                    try
                        Process.GetCurrentProcess().CloseMainWindow() |> ignore
                    with
                    | _ as ex ->
                        Crashes.TrackError ex
                    return ()
                } |> Async.StartImmediate
            )

        if not AudioBookItemProcessor.abItemErrorEvent.HasListeners then
            AudioBookItemProcessor.Events.abItemOnError.Add(fun e ->
                async {
                    let message = sprintf "(%s)" e.Message
                    do! Common.Helpers.displayAlert(Translations.current.Error,message, "OK") 
                    return ()
                } |> Async.StartImmediate
            )

        

    
    let runner =
        App.program
//#if DEBUG
//        |> Program.withConsoleTrace
//#endif   
        |> Program.withSubscription App.subscription
        |> Program.withErrorHandler(
            fun (s,exn)-> 
                let baseException = exn.GetBaseException()
                Common.Helpers.displayAlert(Translations.current.Error,
                    (sprintf "%s / %s" s baseException.Message),
                    "OK") |> Async.StartImmediate
        )
        |> Common.AppCenter.withAppCenterTrace
        |> XamarinFormsProgram.run app

    

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/tools.html for further  instructions.
    //
    //do runner.EnableLiveUpdate()
#endif    
            
    override this.OnSleep() =         
        base.OnSleep()           
        ()

    override __.OnResume() = 
        base.OnResume()
        ()

    override this.OnStart() = 
        base.OnStart()    
        //BrowserPage.PushModalHelper.clearPushPages ()
        ()

    
    


    

        

    



