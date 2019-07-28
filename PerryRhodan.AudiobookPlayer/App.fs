// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
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
open System
open System.IO
open Common.PatternMatchHelpers


module App = 
    open Xamarin.Essentials
    open System.Net
    //open Fabulous.DynamicViews
    open Global
    
    let mainPageRoute = "mainpage"
    let browserPageRoute = "browserpage"
    let settingsPageRoute = "settingspage"
    let playerPageRoute = "playerpage"

    
    type Model = 
      { IsNav:bool
        MainPageModel:MainPage.Model
        LoginPageModel:LoginPage.Model option
        BrowserPageModel:BrowserPage.Model
        AudioPlayerPageModel:AudioPlayerPage.Model option
        AudioBookDetailPageModel:AudioBookDetailPage.Model option
        SettingsPageModel:SettingsPage.Model
        
        AppLanguage:Language
        CurrentPage: Pages
        NavIsVisible:bool 
        PageStack: Pages list
        BacktapsOnMainSite:int 
        HasAudioItemTrigger:bool }

    type Msg = 
        | MainPageMsg of MainPage.Msg 
        | LoginPageMsg of LoginPage.Msg 
        | BrowserPageMsg of BrowserPage.Msg 
        | AudioPlayerPageMsg of AudioPlayerPage.Msg
        | AudioBookDetailPageMsg of AudioBookDetailPage.Msg
        | SettingsPageMsg of SettingsPage.Msg

        | GotoMainPage
        | GotoBrowserPage
        | ProcessFurtherActionsOnBrowserPageAfterLogin of Map<string,string> * LoginRequestCameFrom
        | GotoAudioPlayerPage of AudioBook
        | GotoLoginPage of LoginRequestCameFrom
        | LoginClosed
        | GotoPermissionDeniedPage
        | GotoSettingsPage

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
            icon=icon,
            content=content,
            shellBackgroundColor=Consts.backgroundColor,
            shellForegroundColor=Consts.primaryTextColor
        )
        

    let createShellSection title route icon content =
        View.ShellSection(
            title=title,
            icon=icon,
            items = [
                createShellContent title route icon content
            ]
        )
        

        
    let shellRef = ViewRef<Shell>()

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



    let init () = 
        let mainPageModel, mainPageMsg = MainPage.init ()
        let browserPageModel, browserPageMsg, _ = BrowserPage.init ()
        let settingsPageModel, settingsPageMsg, _ = SettingsPage.init true
        
        let initModel = { 
            IsNav = false
            MainPageModel = mainPageModel
            LoginPageModel = None
            BrowserPageModel = browserPageModel
            AudioPlayerPageModel = None 
            AudioBookDetailPageModel = None 
            SettingsPageModel = settingsPageModel 
            AppLanguage = English
            CurrentPage = MainPage
            NavIsVisible = false 
            PageStack = [ MainPage]
            BacktapsOnMainSite = 0 
            HasAudioItemTrigger = false
        }
        
        let cmds =
            Cmd.batch [ 
                (Cmd.map MainPageMsg mainPageMsg)
                (Cmd.map BrowserPageMsg browserPageMsg)
                (Cmd.map SettingsPageMsg settingsPageMsg)
            ]

        initModel, cmds


    let rec update msg model =
        match msg with
        | MainPageMsg msg ->
            model |> onProcessMainPageMsg msg
        | LoginPageMsg msg ->
            model |> onProcessLoginPageMsg msg
        | BrowserPageMsg msg ->
            model |> onProcessBrowserPageMsg msg
        | AudioPlayerPageMsg msg ->
            model |> onProcessAudioPlayerMsg msg
        | AudioBookDetailPageMsg msg ->
            model |> onProcessAudioBookDetailPageMsg msg
        | SettingsPageMsg msg ->
            model |> onProcessSettingsPageMsg msg
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
        | GotoPermissionDeniedPage ->
            model |> onGotoPermissionDeniedMsg
        | GotoSettingsPage ->
            model |> onGotoSettingsPageMsg
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

        let mainPageMsg = 
            if cameFrom = "MainPage" then
                MainPageMsg (MainPage.Msg.DoNothing)
            else
                MainPageMsg (MainPage.Msg.UpdateAudioBook ab)

        let browserPageMsg = 
            if cameFrom = "Browser" then
                BrowserPageMsg (BrowserPage.Msg.DoNothing)
            else
                BrowserPageMsg (BrowserPage.Msg.UpdateAudioBookItemList ab)


        let audioPlayerModel =
            match model.AudioPlayerPageModel with
            | None -> None
            | Some amdl ->
                if amdl.AudioBook.FullName = ab.AudioBook.FullName then
                    if ab.AudioBook.State.Downloaded then
                
                        Some ({amdl with AudioBook = ab.AudioBook})
                    else
                        None
                else
                    model.AudioPlayerPageModel

        {model with AudioPlayerPageModel = audioPlayerModel}, Cmd.batch [Cmd.ofMsg mainPageMsg; Cmd.ofMsg browserPageMsg ]        
            
    
    and onProcessMainPageMsg msg model =

        let mainPageExternalMsgToCommand externalMsg =
            match externalMsg with
            | None -> Cmd.none
            | Some excmd -> 
                match excmd with
                | MainPage.ExternalMsg.GotoPermissionDeniedPage ->
                    Cmd.ofMsg GotoPermissionDeniedPage
                | MainPage.ExternalMsg.OpenAudioBookPlayer ab ->
                    Cmd.ofMsg (GotoAudioPlayerPage ab)
                | MainPage.ExternalMsg.UpdateAudioBookGlobal (ab,cameFrom) ->
                    Cmd.ofMsg (UpdateAudioBook (ab, cameFrom))
                | MainPage.ExternalMsg.OpenAudioBookDetail ab ->
                    Cmd.ofMsg (OpenAudioBookDetailPage ab)

        let m,cmd, externalMsg = MainPage.update msg model.MainPageModel        
        let externalCmds =
            externalMsg |> mainPageExternalMsgToCommand        
        {model with MainPageModel = m; HasAudioItemTrigger = true}, Cmd.batch [(Cmd.map MainPageMsg cmd); externalCmds ]


    and onProcessLoginPageMsg msg model =
        let loginPageExternalMsgToCommand externalMsg =
            match externalMsg with
            | None -> Cmd.none
            | Some excmd -> 
                match excmd with
                | LoginPage.ExternalMsg.GotoForwardToBrowsing (cookies,cameFrom) ->                    
                    Cmd.batch ([ Cmd.ofMsg (ProcessFurtherActionsOnBrowserPageAfterLogin (cookies,cameFrom)); Cmd.ofMsg GotoBrowserPage ])

        match model.LoginPageModel with
        | Some loginPageModel ->
            let m,cmd, externalMsg = LoginPage.update msg loginPageModel

            let externalCmds =
                externalMsg |> loginPageExternalMsgToCommand
            
            {model with LoginPageModel = Some m}, Cmd.batch [(Cmd.map LoginPageMsg cmd); externalCmds ]
        | None -> model, Cmd.none   


    and browserExternalMsgToCommand externalMsg =
        match externalMsg with
        | None -> Cmd.none
        | Some excmd -> 
            match excmd with
            | BrowserPage.ExternalMsg.OpenLoginPage cameFrom ->
                Cmd.ofMsg (GotoLoginPage cameFrom)
            | BrowserPage.ExternalMsg.OpenAudioBookPlayer ab ->
                Cmd.ofMsg (GotoAudioPlayerPage ab)
            | BrowserPage.ExternalMsg.UpdateAudioBookGlobal (ab,cameFrom) ->
                Cmd.ofMsg (UpdateAudioBook (ab,cameFrom))
            | BrowserPage.ExternalMsg.OpenAudioBookDetail ab ->
                Cmd.ofMsg (OpenAudioBookDetailPage ab)
        

    and onProcessBrowserPageMsg msg model =
        let m,cmd,externalMsg = BrowserPage.update msg model.BrowserPageModel
        let externalCmds =
            externalMsg |> browserExternalMsgToCommand
        {model with BrowserPageModel = m}, Cmd.batch [(Cmd.map BrowserPageMsg cmd); externalCmds ]


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

            {model with AudioBookDetailPageModel = Some m}, Cmd.batch [(Cmd.map AudioBookDetailPageMsg cmd); externalCmds]

        | None -> model, Cmd.none



    and onProcessSettingsPageMsg msg model =
        let settingsPageExternalMsgToCommand externalMsg =
            match externalMsg with
            | None -> Cmd.none
            | Some excmd -> 
                Cmd.none  
        
        let m,cmd,externalMsg = SettingsPage.update msg model.SettingsPageModel
        let externalCmds = 
            externalMsg |> settingsPageExternalMsgToCommand
        {model with SettingsPageModel = m}, Cmd.batch [(Cmd.map SettingsPageMsg cmd); externalCmds]


    and addPageToPageStack page model =
        let hasItem = model.PageStack |> List.tryFind (fun i -> i = page)
        match hasItem with
        | None ->
            {model with PageStack = model.PageStack @ [page]}
        | Some _ ->
            let pageStackWithoutNewPage = 
                model.PageStack 
                |> List.filter (fun i -> i <> page)
            {model with PageStack = pageStackWithoutNewPage @ [page]}

    
    and onGotoMainPageMsg model =
        match shellRef.TryValue with
        | Some sr ->
            sr.GoToAsync("mainpage" |> routeToShellNavigationState) |> Async.AwaitTask |> Async.RunSynchronously
        | None ->
            ()
        model, Cmd.none


    and onGotoLoginPageMsg cameFrom model =
        let newPageModel = model |> addPageToPageStack LoginPage
        let m,cmd = LoginPage.init cameFrom
        {newPageModel with LoginPageModel = Some m},Cmd.batch [ (Cmd.map LoginPageMsg cmd) ]
        

    and onLoginClosed model =
        {model with LoginPageModel = None},Cmd.none


    and onGotoBrowserPageMsg model =
        gotoPage browserPageRoute
        model,Cmd.none


    and onGotoAudioPageMsg audioBook model =
        let newPageModel = model |> addPageToPageStack AudioPlayerPage
        let brandNewPage () = 
            let m,cmd = AudioPlayerPage.init audioBook
            {newPageModel with CurrentPage = AudioPlayerPage; AudioPlayerPageModel = Some m}, Cmd.batch [ (Cmd.map AudioPlayerPageMsg cmd) ]

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
                newPageModel, Cmd.none


    and onGotoPermissionDeniedMsg model =
        gotoPage "permissiondeniedpage"
        model, Cmd.none


    and onGotoSettingsPageMsg model =
        gotoPage settingsPageRoute
        {model with CurrentPage = SettingsPage}, Cmd.none


    and onOpenAudioBookDetailPage audiobook model =
        let newPageModel = model |> addPageToPageStack AudioBookDetailPage
        let m,cmd = AudioBookDetailPage.init audiobook        
        { newPageModel with AudioBookDetailPageModel = Some m }, Cmd.batch [ (Cmd.map AudioBookDetailPageMsg cmd) ]

    and onCloseAudioBookDetailPage model =
        {model with AudioBookDetailPageModel = None }, Cmd.none


    and onSetBrowserPageCookieContainerAfterSucceedLoginMsg cc cameFrom model =
        let downloadQueueModel = {model.BrowserPageModel.DownloadQueueModel with CurrentSessionCookieContainer = Some cc}
        let browserPageModel = {model.BrowserPageModel with CurrentSessionCookieContainer = Some cc; DownloadQueueModel = downloadQueueModel}
        let cmd = 
            Cmd.batch [
                match cameFrom with
                | RefreshAudiobooks ->
                    yield Cmd.ofMsg (BrowserPageMsg BrowserPage.Msg.LoadOnlineAudiobooks)
                | DownloadAudioBook ->
                    yield Cmd.ofMsg (BrowserPageMsg BrowserPage.Msg.StartDownloadQueue)
            ]

        // and close Login Modal
        closeCurrentModal ()
        // set here login page model to None to avoid reopening of the loginPage
        { model with BrowserPageModel = browserPageModel; LoginPageModel = None}, cmd



    let subscription model =
        Cmd.ofSub (fun dispatch ->
            AudioBookItemProcessor.onAbItemUpdated.Add(fun i ->
                dispatch (MainPageMsg (MainPage.Msg.UpdateAudioBook i))
                dispatch (BrowserPageMsg (BrowserPage.Msg.UpdateAudioBook))
            )
            // when updating an audio book, that update the underlying audio book in the Item
            Services.DataBase.storageProcessorOnAudiobookUpdated.Add(fun item ->
                AudioBookItemProcessor.updateUnderlyingAudioBookInItem item
            )
            Services.DataBase.storageProcessorOnAudiobookAdded.Add(fun items ->
                AudioBookItemProcessor.insertAudiobooks items
            )
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
            dependsOn (model.MainPageModel, model.AudioPlayerPageModel) (fun _ (mdl, abMdl)->
                (Controls.contentPageWithBottomOverlay
                    AudioPlayerPage.pageRef
                    (audioPlayerOverlay abMdl)
                    (MainPage.view mdl (mainPageDispatch))
                    model.MainPageModel.IsLoading
                    Translations.current.MainPage)
            )


        // try show login page, if necessary
        model.LoginPageModel
        |> Option.map (
            fun m -> 
                m |> ModalHelpers.showLoginModal dispatch LoginPageMsg LoginClosed shellRef 
        ) |> ignore


        // show audio book detail page modal
        model.AudioBookDetailPageModel
        |> Option.map (
            fun m -> 
                m |> ModalHelpers.showDetailModal dispatch AudioBookDetailPageMsg CloseAudioBookDetailPage shellRef 
        ) |> ignore
            

        let browserPage =
            let newView =
                (BrowserPage.view model.BrowserPageModel (BrowserPageMsg >> dispatch))
            (Controls.contentPageWithBottomOverlay 
                BrowserPage.pageRef
                (audioPlayerOverlay model.AudioPlayerPageModel)
                newView
                model.BrowserPageModel.IsLoading
                Translations.current.BrowserPage)
            
        
        let audioPlayerPage =
            dependsOn model.AudioPlayerPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m ->
                        (AudioPlayerPage.view m (AudioPlayerPageMsg >> dispatch))
                )
            )

        
        let settingsPage =
            dependsOn model.SettingsPageModel (fun _ mdl ->
                (SettingsPage.view mdl (SettingsPageMsg >> dispatch))
            )
        

        View.Shell(
            ref = shellRef,     
            flyoutBehavior=FlyoutBehavior.Disabled,
            title= "Eins A Medien",
            shellForegroundColor=Color.White,
            navigating=(fun e ->
                
                //match e.Target.Location.ToString() with
                //| StringContains mainPageRoute ->
                //    dispatch (MainPageMsg MainPage.Msg.LoadLocalAudiobooks)
                //| StringContains browserPageRoute ->
                //    dispatch (BrowserPageMsg BrowserPage.Msg.RefreshLocalAudiobooks)
                //| _ ->
                //    ()

                ()
                // LoadLocalAudiobooks
            ),
            // makenav bar invisible
            created=(fun e -> Shell.SetNavBarIsVisible(e,false)),
            items=[
                
                yield View.ShellItem(                    
                    shellUnselectedColor = Consts.secondaryTextColor,
                    shellTabBarBackgroundColor=Consts.cardColor,
                    items=[
                        yield createShellContent Translations.current.TabBarStartLabel mainPageRoute "home_icon.png" mainPage
                        yield createShellContent Translations.current.TabBarBrowserLabel browserPageRoute "browse_icon.png" browserPage
                        yield createShellContent Translations.current.TabBarOptionsLabel settingsPageRoute "settings_icon.png" settingsPage
                        match audioPlayerPage with
                        | Some ap ->
                            yield createShellContent Translations.current.TabBarPlayerLabel playerPageRoute "player_icon.png" ap
                        | None ->
                            ()
                    ]
                    
                )
            ]
        )
                


    let program = Program.mkProgram init update view

type MainApp () as app = 
    inherit Application ()

    do 
        AppCenter.Start(sprintf "ios=(...);android=%s" Global.appcenterAndroidId, typeof<Analytics>, typeof<Crashes>)
        // Display Error when somthing is with the storage processor
        Services.DataBase.storageProcessorOnError.Add(fun e ->
            async {
                let message = sprintf "Es ist ein Fehler mit der Datenbank aufgetreten. (%s). Das Programm wird jetzt beendet. Versuchen Sie es noch einmal oder melden Sie sich bin Support." e.Message
                do! Common.Helpers.displayAlert(Translations.current.Error,message, "OK") 
                try
                    Process.GetCurrentProcess().CloseMainWindow() |> ignore
                with
                | _ as ex ->
                    Crashes.TrackError ex
                return ()
            } |> Async.StartImmediate
        )

        AudioBookItemProcessor.abItemOnError.Add(fun e ->
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

    override __.OnSleep() =         
        base.OnSleep()        
        ()

    override __.OnResume() = 
        base.OnResume()
        ()

    override this.OnStart() = 
        base.OnStart()
        ()

    

        

    



