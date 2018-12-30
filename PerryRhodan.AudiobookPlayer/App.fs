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
    
    type Pages = 
        | MainPage
        | LoginPage
        | BrowserPage
        | AudioPlayerPage
        | PermissionDeniedPage
        | AudioBookDetailPage
    
    type Model = 
      { IsNav:bool
        MainPageModel:MainPage.Model
        LoginPageModel:LoginPage.Model option
        BrowserPageModel:BrowserPage.Model option
        AudioPlayerPageModel:AudioPlayerPage.Model option
        AudioBookDetailPageModel:AudioBookDetailPage.Model option
        CurrentPage: Pages
        NavIsVisible:bool 
        PageStack: Pages list}

    type Msg = 
        | MainPageMsg of MainPage.Msg 
        | LoginPageMsg of LoginPage.Msg 
        | BrowserPageMsg of BrowserPage.Msg 
        | AudioPlayerPageMsg of AudioPlayerPage.Msg
        | AudioBookDetailPageMsg of AudioBookDetailPage.Msg

        | GotoMainPage
        | GotoBrowserPage
        | SetBrowserPageCookieContainerAfterSucceededLogin of Map<string,string>
        | GotoAudioPlayerPage of AudioBook
        | GotoLoginPage
        | GotoPermissionDeniedPage
        | OpenAudioBookDetailPage of AudioBook
        | CloseAudioBookDetailPage
        | NavigationPopped of Pages
        | UpdateAudioBook of AudioBookItem.Model * string
        

    let initModel = { IsNav = false
                      MainPageModel = MainPage.initModel
                      LoginPageModel = None
                      BrowserPageModel = None
                      AudioPlayerPageModel = None 
                      AudioBookDetailPageModel = None 
                      CurrentPage = MainPage
                      NavIsVisible = false 
                      PageStack = [ MainPage] }




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
        | GotoMainPage ->
            model |> onGotoMainPageMsg
        | GotoLoginPage ->
            model |> onGotoLoginPageMsg
        | GotoBrowserPage ->
            model |> onGotoBrowserPageMsg
        | GotoAudioPlayerPage audioBook ->
            model |> onGotoAudioPageMsg audioBook
        | GotoPermissionDeniedPage ->
            model |> onGotoPermissionDeniedMsg
        | OpenAudioBookDetailPage ab ->
            model |> onOpenAudioBookDetailPage ab
        | CloseAudioBookDetailPage ->
            model |> onCloseAudioBookDetailPage
        | NavigationPopped page ->
            model |> onNavigationPoppedMsg page
        | SetBrowserPageCookieContainerAfterSucceededLogin cc ->
            model |> onSetBrowserPageCookieContainerAfterSucceedLoginMsg cc
        | UpdateAudioBook (ab, cameFrom) ->
            model |> onUpdateAudioBookMsg ab cameFrom
        
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
                | LoginPage.ExternalMsg.GotoForwardToBrowsing c ->                    
                    Cmd.batch ([ Cmd.ofMsg (SetBrowserPageCookieContainerAfterSucceededLogin c); Cmd.ofMsg GotoBrowserPage ])

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
            | BrowserPage.ExternalMsg.OpenLoginPage ->
                Cmd.ofMsg (GotoLoginPage)
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


    and onGotoLoginPageMsg model =
        let newPageModel = model |> addPageToPageStack LoginPage
        match model.LoginPageModel with
        | None ->
            let m,cmd = LoginPage.init ()
            {newPageModel with CurrentPage = LoginPage; LoginPageModel = Some m},Cmd.batch [ (Cmd.map LoginPageMsg cmd) ]
        | Some lpm  -> 
            let newModel = 
                if not lpm.RememberLogin then
                    {lpm with Username = ""; Password = ""}
                else
                    lpm
            {newPageModel with CurrentPage = LoginPage; LoginPageModel = Some newModel}, Cmd.none


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
                if abModel.CurrentState = AudioPlayerPage.Playing then
                    // stop audio player
                    AudioPlayerPage.audioPlayer.Stop()
                brandNewPage()
            else
                newPageModel, Cmd.none


    and onGotoPermissionDeniedMsg model =
        let newPageModel = model |> addPageToPageStack PermissionDeniedPage
        {newPageModel with CurrentPage = PermissionDeniedPage}, Cmd.none


    and onOpenAudioBookDetailPage audiobook model =
        let newPageModel = model |> addPageToPageStack AudioBookDetailPage
        let m,cmd = AudioBookDetailPage.init audiobook
        { newPageModel with CurrentPage = AudioBookDetailPage; AudioBookDetailPageModel = Some m }, Cmd.batch [ (Cmd.map AudioBookDetailPageMsg cmd) ]

    and onCloseAudioBookDetailPage model =
        let newPageStack = model.PageStack |> List.filter (fun i -> i <> AudioBookDetailPage)
        {model with PageStack = newPageStack }, Cmd.none

    and onNavigationPoppedMsg page model =
        if page = MainPage then
            {model with PageStack = [MainPage]}, (Cmd.ofMsg (MainPageMsg MainPage.Msg.LoadLocalAudiobooks))
        else
            let newPageStack = model.PageStack |> List.filter ( fun i -> i <> page && i <> LoginPage)

            // Reload AudioBooks when reach MainPage, it always at least one in the list
            let lastEntry = newPageStack |> List.last
            let cmd =
                if lastEntry = MainPage then
                    (Cmd.ofMsg (MainPageMsg MainPage.Msg.LoadLocalAudiobooks))
                else
                    Cmd.none

            {model with PageStack = newPageStack}, cmd


    and onSetBrowserPageCookieContainerAfterSucceedLoginMsg cc model =
        match model.BrowserPageModel with 
        | None -> model, Cmd.none
        | Some bm ->
    
        let downloadQueueModel = {bm.DownloadQueueModel with CurrentSessionCookieContainer = Some cc}
        let bModel = {bm with CurrentSessionCookieContainer = Some cc; DownloadQueueModel = downloadQueueModel}
        
        { model with BrowserPageModel = Some bModel}, Cmd.batch [Cmd.ofMsg (BrowserPageMsg BrowserPage.Msg.LoadLocalAudiobooks) ]


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
                    "Home")
                        .ToolbarItems([
                            View.ToolbarItem(
                                icon="browse_icon.png",
                                command=(fun ()-> dispatch GotoBrowserPage
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
                            "Browse your AudioBooks")
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
        

        // gets the page from the title to manage the page stack
        let determinatePageByTitle title =            
            match title with
            | "Home" -> MainPage
            | "Browse your AudioBooks" -> BrowserPage
            | "Player" -> AudioPlayerPage
            | "Detail" -> AudioBookDetailPage
            | "Login" -> LoginPage
            | _ -> MainPage

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
                    | PermissionDeniedPage ->
                        yield View.ContentPage(
                            title="Login Page",useSafeArea=true,
                            content = View.Label(text="Sorry without Permission the App is not useable!", horizontalOptions = LayoutOptions.Center, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Center,fontSize=20.0)
                        )

            ]            
        )



    let program = Program.mkProgram init update view

type App () as app = 
    inherit Application ()

    do AppCenter.Start("ios=(...);android=", typeof<Analytics>, typeof<Crashes>)
    
    let runner =
        
        App.program
//#if DEBUG
//        |> Program.withConsoleTrace
//#endif   
        |> Program.withErrorHandler(
            fun (s,exn)-> 
                let baseException = exn.GetBaseException()
                Common.Helpers.displayAlert("Error",
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

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/models.html for further  instructions.
#if APPSAVE
    let modelId = "model"
    override __.OnSleep() = 

        let json = Newtonsoft.Json.JsonConvert.SerializeObject(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<App.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()
#endif


