// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace PerryRhodan.AudiobookPlayer

open System.Diagnostics
open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open Plugin.Permissions.Abstractions
open Common
open Domain
open Microsoft.AppCenter
open Microsoft.AppCenter.Crashes
open Microsoft.AppCenter.Analytics


module App = 
    open Xamarin.Essentials
    open System.Net
    open Fabulous.DynamicViews
    open Global
    


    
    type Model = 
      { IsNav:bool
        MainPageModel:MainPage.Model
        LoginPageModel:LoginPage.Model option
        BrowserPageModel:BrowserPage.Model option
        AudioPlayerPageModel:AudioPlayerPage.Model option
        AudioBookDetailPageModel:AudioBookDetailPage.Model option
        SettingsPageModel:SettingsPage.Model option
        
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
        

    let initModel = { IsNav = false
                      MainPageModel = MainPage.initModel
                      LoginPageModel = None
                      BrowserPageModel = None
                      AudioPlayerPageModel = None 
                      AudioBookDetailPageModel = None 
                      SettingsPageModel = None 
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
        match model.BrowserPageModel with
        | Some browserPageModel ->
            let m,cmd,externalMsg = BrowserPage.update msg browserPageModel

            let externalCmds =
                externalMsg |> browserExternalMsgToCommand

            {model with BrowserPageModel = Some m}, Cmd.batch [(Cmd.map BrowserPageMsg cmd); externalCmds ]

        | None -> model, Cmd.none


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

        match model.SettingsPageModel with
        | Some settingsPageModel ->
            let m,cmd,externalMsg = SettingsPage.update msg settingsPageModel

            let externalCmds = 
                externalMsg |> settingsPageExternalMsgToCommand

            {model with SettingsPageModel = Some m}, Cmd.batch [(Cmd.map SettingsPageMsg cmd); externalCmds]

        | None -> model, Cmd.none



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
        let newModel = model |> addPageToPageStack MainPage
        {newModel with CurrentPage = MainPage}, Cmd.batch [ (Cmd.ofMsg (MainPageMsg MainPage.Msg.LoadLocalAudiobooks)) ]


    and onGotoLoginPageMsg cameFrom model =
        let newPageModel = model |> addPageToPageStack LoginPage
        let m,cmd = LoginPage.init cameFrom
        {newPageModel with CurrentPage = LoginPage; LoginPageModel = Some m},Cmd.batch [ (Cmd.map LoginPageMsg cmd) ]
        


    and onGotoBrowserPageMsg model =
        let newPageModel = model |> addPageToPageStack BrowserPage
        match model.BrowserPageModel with
        | None ->
            let m,cmd, externalMsg = BrowserPage.init ()
            let externalCmds =
                externalMsg |> browserExternalMsgToCommand

            {newPageModel with CurrentPage = BrowserPage; BrowserPageModel = Some m}, Cmd.batch [(Cmd.map BrowserPageMsg cmd); externalCmds ]
        | Some _  -> 
            {newPageModel with CurrentPage = BrowserPage}, Cmd.none


    and onGotoAudioPageMsg audioBook model =
        let newPageModel = model |> addPageToPageStack AudioPlayerPage
        let brandNewPage () = 
            let m,cmd = AudioPlayerPage.init audioBook
            {newPageModel with CurrentPage = AudioPlayerPage; AudioPlayerPageModel = Some m}, Cmd.batch [ (Cmd.map AudioPlayerPageMsg cmd) ]

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
        let newPageModel = model |> addPageToPageStack PermissionDeniedPage

        {newPageModel with CurrentPage = PermissionDeniedPage}, Cmd.none


    and onGotoSettingsPageMsg model =
        let newPageModel = model |> addPageToPageStack SettingsPage
        let model,cmd,externalCmd = SettingsPage.init true
        {newPageModel with SettingsPageModel = Some model; CurrentPage = SettingsPage}, (Cmd.map SettingsPageMsg cmd)


    and onOpenAudioBookDetailPage audiobook model =
        let newPageModel = model |> addPageToPageStack AudioBookDetailPage
        let m,cmd = AudioBookDetailPage.init audiobook
        { newPageModel with CurrentPage = AudioBookDetailPage; AudioBookDetailPageModel = Some m }, Cmd.batch [ (Cmd.map AudioBookDetailPageMsg cmd) ]

    and onCloseAudioBookDetailPage model =
        let newPageStack = model.PageStack |> List.filter (fun i -> i <> AudioBookDetailPage)
        {model with PageStack = newPageStack }, Cmd.none

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
                if lastEntry = MainPage then
                    (Cmd.ofMsg (MainPageMsg MainPage.Msg.LoadLocalAudiobooks))
                else
                    Cmd.none

            {model with PageStack = newPageStack; BacktapsOnMainSite = 0}, cmd


    and onSetBrowserPageCookieContainerAfterSucceedLoginMsg cc cameFrom model =
        match model.BrowserPageModel with 
        | None -> model, Cmd.none
        | Some bm ->
    
        let downloadQueueModel = {bm.DownloadQueueModel with CurrentSessionCookieContainer = Some cc}
        let browserPageModel = {bm with CurrentSessionCookieContainer = Some cc; DownloadQueueModel = downloadQueueModel}
        let cmd = 
            Cmd.batch [
                match cameFrom with
                | RefreshAudiobooks ->
                    yield Cmd.ofMsg (BrowserPageMsg BrowserPage.Msg.LoadOnlineAudiobooks)
                | DownloadAudioBook ->
                    yield Cmd.ofMsg (BrowserPageMsg BrowserPage.Msg.StartDownloadQueue)
            ]

        
        { model with BrowserPageModel = Some browserPageModel}, cmd


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
                        .ToolbarItems([
                            View.ToolbarItem(
                                text=Translations.current.Quit,
                                command = (fun () -> dispatch QuitApplication)
                            )

                            View.ToolbarItem(
                                icon="browse_icon.png",
                                command=(fun ()-> dispatch GotoBrowserPage
                            ))

                            View.ToolbarItem(
                                icon="settings_icon.png",
                                command=(fun ()-> dispatch GotoSettingsPage
                            ))
                                
                        ])
                    .HasNavigationBar(true)
                    .HasBackButton(false)
                
            )

        // you can do an explict match or an Option map
        let loginPage =             
            dependsOn model.LoginPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m -> 
                        (LoginPage.view m (LoginPageMsg >> dispatch))
                            .HasNavigationBar(false)
                            .HasBackButton(true)
                )
            )

        let browserPage =
            dependsOn (model.BrowserPageModel, model.AudioPlayerPageModel) (fun _ (mdl, abMdl) ->
                mdl
                |> Option.map(
                    fun m ->
                        (Controls.contentPageWithBottomOverlay 
                            (audioPlayerOverlay abMdl)
                            (BrowserPage.view m (BrowserPageMsg >> dispatch))
                            (model.BrowserPageModel |> Option.map (fun bm -> bm.IsLoading) |> Option.defaultValue false)
                            Translations.current.BrowserPage)
                            .ToolbarItems([
                                View.ToolbarItem(
                                    icon="home_icon.png",
                                    command=(fun ()-> dispatch (NavigationPopped BrowserPage)))
                                    ])
                            .HasNavigationBar(true)
                            .HasBackButton(true)
                            
                            
                )
            )
            
        
        let audioPlayerPage =
            dependsOn model.AudioPlayerPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m ->
                        (AudioPlayerPage.view m (AudioPlayerPageMsg >> dispatch))
                            .ToolbarItems([
                                View.ToolbarItem(
                                    icon="home_icon.png",
                                    command=(fun ()-> dispatch GotoMainPage))
                                View.ToolbarItem(
                                    icon="browse_icon.png",
                                    command=(fun ()-> dispatch GotoBrowserPage))
                                    ])
                            .HasNavigationBar(true)
                            .HasBackButton(true)
                            
                )
            )

        let audioBookDetailPage =
            dependsOn model.AudioBookDetailPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m ->
                        (AudioBookDetailPage.view m (AudioBookDetailPageMsg >> dispatch))
                            .ToolbarItems([
                                View.ToolbarItem(
                                    icon="home_icon.png",
                                    command=(fun ()-> dispatch GotoMainPage))
                                View.ToolbarItem(
                                    icon="browse_icon.png",
                                    command=(fun ()-> dispatch GotoBrowserPage))
                                    
                                View.ToolbarItem(
                                    text="Close",
                                    command=(fun ()-> dispatch CloseAudioBookDetailPage))
                                    ])
                            .HasNavigationBar(true)
                            .HasBackButton(true)
                            
                )
            )

        let settingsPage =
            dependsOn model.SettingsPageModel (fun _ mdl ->
                mdl
                |> Option.map (
                    fun m ->
                        (SettingsPage.view m (SettingsPageMsg >> dispatch))
                            .ToolbarItems([
                                View.ToolbarItem(
                                    icon="home_icon.png",
                                    command=(fun ()-> dispatch GotoMainPage))
                                View.ToolbarItem(
                                    icon="browse_icon.png",
                                    command=(fun ()-> dispatch GotoBrowserPage))                                    
                                ])
                            .HasNavigationBar(true)
                            .HasBackButton(true)
                            
                )
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

        View.NavigationPage(barBackgroundColor = Consts.appBarColor,
            barTextColor=Consts.primaryTextColor,           
            popped = (dispatchNavPopped dispatch),
            pages = [
                for page in model.PageStack do
                    match page with
                    | MainPage -> 
                        yield mainPage  
                    | LoginPage ->
                        if loginPage.IsSome then
                            yield loginPage.Value
                    | BrowserPage ->
                        if browserPage.IsSome then
                            yield browserPage.Value
                    | AudioPlayerPage ->
                        if audioPlayerPage.IsSome then
                            yield audioPlayerPage.Value
                    | AudioBookDetailPage ->
                        if audioBookDetailPage.IsSome then
                            yield audioBookDetailPage.Value
                    |SettingsPage ->
                        if settingsPage.IsSome then
                            yield settingsPage.Value
                    | PermissionDeniedPage ->
                        yield View.ContentPage(
                            title=Translations.current.PermissionDeniedPage,useSafeArea=true,
                            content = View.Label(text=Translations.current.PermissionError, horizontalOptions = LayoutOptions.Center, widthRequest=200., horizontalTextAlignment=TextAlignment.Center,fontSize=20.)
                        )

            ]
        )



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
        |> Program.runWithDynamicView app

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

    

        

    



