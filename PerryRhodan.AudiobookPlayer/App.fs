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


module App = 
    open Xamarin.Essentials
    open System.Net
    //open Fabulous.DynamicViews
    open Global
    


    
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
        BacktapsOnMainSite:int }

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


    let initModel = { IsNav = false
                      MainPageModel = MainPage.initModel
                      LoginPageModel = None
                      BrowserPageModel = BrowserPage.initModel
                      AudioPlayerPageModel = None 
                      AudioBookDetailPageModel = None 
                      SettingsPageModel = SettingsPage.initModel true 
                      AppLanguage = English
                      CurrentPage = MainPage
                      NavIsVisible = false 
                      PageStack = [ MainPage]
                      BacktapsOnMainSite = 0 }




    let init () = 
        let mainPageModel, mainPageMsg = MainPage.init ()

        {initModel with MainPageModel = mainPageModel}, Cmd.batch [ (Cmd.map MainPageMsg mainPageMsg)]


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
            model |> onNavigationPoppedMsg page
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
        {model with MainPageModel = m}, Cmd.batch [(Cmd.map MainPageMsg cmd); externalCmds ]


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
                match excmd with
                | AudioBookDetailPage.ExternalMsg.CloseAudioBookDetailPage ->
                    Cmd.ofMsg CloseAudioBookDetailPage    

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
        gotoPage "loginpage"
        {newPageModel with CurrentPage = LoginPage; LoginPageModel = Some m},Cmd.batch [ (Cmd.map LoginPageMsg cmd) ]
        


    and onGotoBrowserPageMsg model =
        gotoPage "browsepage"
        model,Cmd.none


    and onGotoAudioPageMsg audioBook model =
        let newPageModel = model |> addPageToPageStack AudioPlayerPage
        let brandNewPage () = 
            let m,cmd = AudioPlayerPage.init audioBook
            {newPageModel with CurrentPage = AudioPlayerPage; AudioPlayerPageModel = Some m}, Cmd.batch [ (Cmd.map AudioPlayerPageMsg cmd) ]
        

        gotoPage "playerpage"

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
        gotoPage "settingspage"
        {model with CurrentPage = SettingsPage}, Cmd.none


    and onOpenAudioBookDetailPage audiobook model =
        let newPageModel = model |> addPageToPageStack AudioBookDetailPage
        let m,cmd = AudioBookDetailPage.init audiobook
        gotoPage "detailpage"
        { newPageModel with CurrentPage = AudioBookDetailPage; AudioBookDetailPageModel = Some m }, Cmd.batch [ (Cmd.map AudioBookDetailPageMsg cmd) ]

    and onCloseAudioBookDetailPage model =
        gotoPage "mainpage"
        model, Cmd.none

    and onNavigationPoppedMsg page model =
        if page = MainPage then
            let cmd =                 
                Cmd.ofMsg (MainPageMsg MainPage.Msg.LoadLocalAudiobooks)

            {model with PageStack = [MainPage];BacktapsOnMainSite = model.BacktapsOnMainSite + 1}, cmd
        else
            let newPageStack = model.PageStack |> List.filter ( fun i -> i <> page && i <> LoginPage)

            // Reload AudioBooks when reach MainPage, it always at least one in the list
            let lastEntry = newPageStack |> List.last
            let cmd =
                match lastEntry with
                | MainPage ->
                    (Cmd.ofMsg (MainPageMsg MainPage.Msg.LoadLocalAudiobooks))
                | BrowserPage ->
                    (Cmd.ofMsg (BrowserPageMsg BrowserPage.Msg.LoadLocalAudiobooks))
                | _ ->
                    Cmd.none

            {model with PageStack = newPageStack; BacktapsOnMainSite = 0}, cmd


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

        
        { model with BrowserPageModel = browserPageModel}, cmd


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
                    (audioPlayerOverlay abMdl)
                    (MainPage.view mdl (mainPageDispatch))
                    model.MainPageModel.IsLoading
                    Translations.current.MainPage)
            )

        // you can do an explict match or an Option map
        let loginPage =             
            dependsOn model.LoginPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m -> 
                        (LoginPage.view m (LoginPageMsg >> dispatch))                           
                )
            )

        let browserPage =
            dependsOn (model.BrowserPageModel, model.AudioPlayerPageModel) (fun _ (mdl, abMdl) ->
                (Controls.contentPageWithBottomOverlay 
                    (audioPlayerOverlay abMdl)
                    (BrowserPage.view mdl (BrowserPageMsg >> dispatch))
                    mdl.IsLoading
                    Translations.current.BrowserPage)
            )
            
        
        let audioPlayerPage =
            dependsOn model.AudioPlayerPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m ->
                        (AudioPlayerPage.view m (AudioPlayerPageMsg >> dispatch))
                )
            )

        let audioBookDetailPage =
            dependsOn model.AudioBookDetailPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m ->
                        (AudioBookDetailPage.view m (AudioBookDetailPageMsg >> dispatch))
                )
            )

        let settingsPage =
            dependsOn model.SettingsPageModel (fun _ mdl ->
                (SettingsPage.view mdl (SettingsPageMsg >> dispatch))
            )
        

        // gets the page from the title to manage the page stack
        let determinatePageByTitle title = 
            if title = Translations.current.MainPage then MainPage
            elif title = Translations.current.BrowserPage then BrowserPage
            elif title = Translations.current.AudioPlayerPage then AudioPlayerPage
            elif title = Translations.current.AudioBookDetailPage then AudioBookDetailPage
            elif title = Translations.current.LoginPage then LoginPage
            elif title = Translations.current.SettingsPage then SettingsPage
            else MainPage            

        // Workaround iOS bug: https://github.com/xamarin/Xamarin.Forms/issues/3509
        let dispatchNavPopped =
            let mutable lastRemovedPageIdentifier: int = -1
            let apply dispatch (e: Xamarin.Forms.NavigationEventArgs) =
                let removedPageIdentifier = e.Page.GetHashCode()
                match lastRemovedPageIdentifier = removedPageIdentifier with
                | false ->
                    lastRemovedPageIdentifier <- removedPageIdentifier
                    let pageType = e.Page.Title |> determinatePageByTitle
                    dispatch (NavigationPopped pageType)
                | true ->
                    ()
            apply

        let popNav () =
            async { 
                let! x = shellRef.Value.Navigation.PopAsync(true) |> Async.AwaitTask
                return ()
            } |> Async.StartImmediate

        View.Shell(
            ref = shellRef,     
            flyoutBehavior=FlyoutBehavior.Disabled,
            title= "Eins A Medien",
            shellForegroundColor=Color.White,
            shellBackButtonBehavior=
                View.BackButtonBehavior(
                    command=popNav,isEnabled = true
                ),
            created=(fun e ->
                //let action = Action(fun _ -> popNav ())
                //Shell.SetBackButtonBehavior(e,new BackButtonBehavior(Command=Command(action)))
                ()
            ),
            items=[
                
                yield View.TabBar(
                    shellUnselectedColor = Consts.secondaryTextColor,
                    shellTabBarBackgroundColor=Consts.cardColor,
                    items=[
                        createShellContent "Start" "mainpage" "home_icon.png" mainPage
                        createShellContent "Browse" "browsepage" "browse_icon.png" browserPage
                        createShellContent "Start" "settingspage" "settings_icon.png" settingsPage
                    ]
                    
                )
                yield View.ShellSection(
                    items= [
                        match loginPage with
                        | Some lp ->
                            yield createShellContent "Login" "loginpage" "" lp
                        | None -> ()

                        match audioPlayerPage with
                        | Some ap ->
                            yield createShellContent "Player" "playerpage" "" ap
                        | None -> ()

                        match audioBookDetailPage with
                        | Some abdp ->
                            yield createShellContent "Detail" "detailpage" "" abdp
                        | None -> ()

                        let pmdPage = 
                            View.ContentPage(
                                title=Translations.current.PermissionDeniedPage,useSafeArea=true,
                                content = View.Label(text=Translations.current.PermissionError, horizontalOptions = LayoutOptions.Center, widthRequest=200., horizontalTextAlignment=TextAlignment.Center,fontSize=20.)
                            )
                        yield createShellContent "Permissionenied" "permissiondeniedpage" "" pmdPage    
                    ]
                )
                
                
            ]
                //View.TabBar(
                    
                //    items=[
                        
                //        //View.ShellContent(
                //        //    title="MainPage",
                //        //    route="MainPage",
                //        //    icon="home_icon.png",
                //        //    //content=View.ContentPage(content=View.Label(text="Meh"))
                //        //    content=mainPage
                //        //) 
                //        //View.ShellContent(
                //        //    title="BrowsePage",
                //        //    route="BrowserPage",
                //        //    icon="browse_icon.png",
                //        //    content=(browserPage |> Option.defaultValue mainPage)
                //        //) 
                //    ]
                //)
                
            // ]
        )
        //View.NavigationPage(barBackgroundColor = Consts.appBarColor,
        //    barTextColor=Consts.primaryTextColor,           
        //    popped = (dispatchNavPopped dispatch),
        //    pages = [
        //        for page in model.PageStack do
        //            match page with
        //            | MainPage -> 
        //                yield mainPage  
        //            | LoginPage ->
        //                if loginPage.IsSome then
        //                    yield loginPage.Value
        //            | BrowserPage ->
        //                if browserPage.IsSome then
        //                    yield browserPage.Value
        //            | AudioPlayerPage ->
        //                if audioPlayerPage.IsSome then
        //                    yield audioPlayerPage.Value
        //            | AudioBookDetailPage ->
        //                if audioBookDetailPage.IsSome then
        //                    yield audioBookDetailPage.Value
        //            |SettingsPage ->
        //                if settingsPage.IsSome then
        //                    yield settingsPage.Value
        //            | PermissionDeniedPage ->
        //                yield View.ContentPage(
        //                    title=Translations.current.PermissionDeniedPage,useSafeArea=true,
        //                    content = View.Label(text=Translations.current.PermissionError, horizontalOptions = LayoutOptions.Center, widthRequest=200., horizontalTextAlignment=TextAlignment.Center,fontSize=20.)
        //                )

        //    ]
        //)



    let program = Program.mkProgram init update view

type App () as app = 
    inherit Application ()

    do AppCenter.Start(sprintf "ios=(...);android=%s" Global.appcenterAndroidId, typeof<Analytics>, typeof<Crashes>)
    
    let runner =
        
        App.program
//#if DEBUG
//        |> Program.withConsoleTrace
//#endif   
        |> Program.withErrorHandler(
            fun (s,exn)-> 
                let baseException = exn.GetBaseException()
                Common.Helpers.displayAlert(Translations.current.Error,
                    (sprintf "%s / %s" s baseException.Message),
                    "OK") |> Async.RunSynchronously
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

    

        

    



