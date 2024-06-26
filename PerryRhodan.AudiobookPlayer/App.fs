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


module App =
    open Xamarin.Essentials
    open System.Net

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

        //DownloadQueueModel: DownloadQueue.Model

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
        | InitMigrationDone
        | InitSuccessful of AudioBookItemNew.AudioBookItem []
        | AudioBookItemsChanged of AudioBookItemNew.AudioBookItem []
        | AudioBookItemMsg of (AudioBookItemNew.Model * AudioBookItemNew.Msg)
        | AudioBookItemsUpdated
        | UpdateAudioBookItemFromAudioBook of AudioBook

        | AskForAppPermission
        | MainPageMsg of MainPage.Msg
        | LoginPageMsg of LoginPage.Msg

        | BrowserPageMsg of (string list * BrowserPage.Msg)
        | BrowserPageModelClosed of string

        | AudioPlayerPageMsg of AudioPlayerPage.Msg
        | AudioBookDetailPageMsg of AudioBookDetailPage.Msg
        | SettingsPageMsg of SettingsPage.Msg
        | SupportFeedbackPageMsg of SupportFeedback.Msg

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


        | QuitApplication


    let routeToShellNavigationState (route:string) =
        ShellNavigationState.op_Implicit route



    let createShellContent title route icon content =
        View.ShellContent(
            title=title,
            route=route,
            icon=Image.fromPath icon,
            content=content,
            shellBackgroundColor=Consts.backgroundColor,
            shellForegroundColor=Consts.primaryTextColor
        )


    let createShellSection title route icon content =
        View.ShellSection(
            title=title,
            icon=Image.fromPath icon,
            items = [
                createShellContent title route icon content
            ]
        )



    let shellRef = ModalHelpers.ModalManager.shellRef


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




    module AudioBookItemHelper =

        open AudioBookItemNew

        let createAudioBookItem dispatch audioBookItemModel =
            {
                AudioBookItemNew.AudioBookItem.Model = audioBookItemModel
                Dispatch = fun msg -> dispatch <| AudioBookItemMsg (audioBookItemModel,msg)
            }


    module Commands =


        let runMigrations =
            fun dispatch ->
                async {
                    do! Migrations.runMigrations ()
                    dispatch InitMigrationDone
                }
                |> Async.Start

            |> Cmd.ofSub

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


        let synchonizeCurrentAudiobooksWithDeviceCmd model =
            fun dispatch ->
                async {

                    let currentAudioBooks =
                        model.AudioBookItems |> Array.map (fun i -> i.Model.AudioBook)

                    let audioBooksAlreadyOnTheDevice =
                        Services.DataBase.getAudiobooksFromDownloadFolder currentAudioBooks
                        |> Array.map (fun i -> AudioBookItemNew.init i)
                        |> Array.map (fun i -> AudioBookItemHelper.createAudioBookItem dispatch i)

                    // mark all other audio book as not downloaded
                    let newAudiobookItem =
                        model.AudioBookItems
                        |> Array.map (fun i ->
                            audioBooksAlreadyOnTheDevice
                            |> Array.tryFind (fun d -> d.Model.AudioBook.Id = i.Model.AudioBook.Id)
                            |> Option.defaultValue {
                                i with Model = {
                                    i.Model with
                                        DownloadState = AudioBookItemNew.NotDownloaded
                                        AudioBook = {
                                            i.Model.AudioBook with
                                                State = {
                                                    i.Model.AudioBook.State with Downloaded = false
                                                }
                                        }
                                }
                            }
                        )

                    let! savedAudioBookResult =
                        newAudiobookItem
                        |> Array.map (fun i -> i.Model.AudioBook)
                        |> Array.map Services.DataBase.updateAudioBookInStateFile
                        |> Async.Sequential


                    let error =
                        savedAudioBookResult
                        |> Array.choose (function | Error e -> Some e | Ok _ -> None )

                    if error.Length > 0 then
                        do! Common.Helpers.displayAlert ("Fehler", "Beim Update der Hörbücher ist ein Speicherfehler aufgetreten.", "OK")
                    else
                        dispatch <| AudioBookItemsChanged newAudiobookItem

                }
                |> Async.Start
            |> Cmd.ofSub


        let loadAudioBookItemsCmd =
            fun dispatch ->
                async {
                    let! ab = Services.DataBase.loadAudioBooksStateFile ()
                    let itemsWithDispatcher =
                        ab
                        |> Array.map (fun i -> AudioBookItemNew.init i)
                        |> Array.map (fun i -> AudioBookItemHelper.createAudioBookItem dispatch i)

                    dispatch <| InitSuccessful itemsWithDispatcher
                }
                |> Async.Start

            |> Cmd.ofSub


        let createAudioBookFileListIfNecessayCmd model : Cmd<Msg> =
            fun dispatch  ->
                async {
                    let! audioBooksWithFileInfo =
                        model.AudioBookItems
                        |> Array.filter (fun i -> i.Model.DownloadState = AudioBookItemNew.Downloaded)
                        |> Array.map (fun i ->
                            async {
                                let! files = Services.DataBase.getAudioBookFileInfo i.Model.AudioBook.Id
                                return (i,files)
                            }
                        )
                        |> Async.Sequential

                    let! audioFileInfos =
                        audioBooksWithFileInfo
                        |> Array.filter (fun (i,files) -> files.IsNone)
                        |> Array.map (fun (i,_) ->
                            async {
                                match i.Model.AudioBook.State.DownloadedFolder with
                                | None ->
                                    return None
                                | Some folder ->
                                    let! files =
                                        Services.Files.getMp3FileList folder

                                    let fileInfo = {
                                        Id = i.Model.AudioBook.Id
                                        AudioFiles = files |> List.sortBy (fun f -> f.FileName)
                                    }
                                    return Some fileInfo
                            }
                        )
                        |> Async.Sequential

                    let! res = Services.DataBase.insertAudioBookFileInfos (audioFileInfos |> Array.choose id)

                    match res with
                    | Error e ->
                        do! Common.Helpers.displayAlert ("Fehler!","Beim erstellen den AduiFile-Liste ist ein Fehler aufgetreten.","OK!")
                    | Ok _ ->
                        ()
                }
                |> Async.Start

            |> Cmd.ofSub




        type SynchronizeWithCloudErrors =
            | NoSessionAvailable
            | WebError of ComError
            | StorageError of string

        let synchronizeWithCloudCmd model =
            fun dispatch ->
                async {

                    let audioBookItemDispatch item msg =
                        dispatch <| AudioBookItemMsg (item, msg)

                    let notifyAfterSync (synchedAb:AudioBookItemNew.AudioBookItem []) =
                        async {
                            match synchedAb with
                            | [||] ->
                                do! Common.Helpers.displayAlert(Translations.current.NoNewAudioBooksSinceLastRefresh," ¯\_(ツ)_/¯","OK")
                            | _ ->
                                let message = synchedAb |> Array.map (fun i -> i.Model.AudioBook.FullName) |> String.concat "\r\n"
                                do! Common.Helpers.displayAlert(Translations.current.NewAudioBooksSinceLastRefresh,message,"OK")
                        }

                    let checkLoginSession () =
                        match model.CookieContainer with
                        | Some cc ->
                            Ok cc
                        | None ->
                            Error NoSessionAvailable
                        |> AsyncResult.ofResult


                    let loadAudioBooksFromCloud (sessionResult:AsyncResult<Map<string,string>,SynchronizeWithCloudErrors>) =
                        asyncResult {
                            let! cookies = sessionResult
                            let! audioBooks =
                                Services.WebAccess.getAudiobooksOnline cookies
                                |> AsyncResult.mapError (fun e -> WebError e)
                            return audioBooks
                        }

                    let loadAudioBooksFromDevice
                        (modelAudioBooks:AudioBookItemNew.AudioBookItem[])
                        (loadResult:AsyncResult<Domain.AudioBook[],SynchronizeWithCloudErrors>) =
                        asyncResult {
                            let! cloudAudioBooks = loadResult

                            let audioBooksAlreadyOnTheDevice =
                                Services.DataBase.getAudiobooksFromDownloadFolder cloudAudioBooks
                                // remove items that are already in the model itself
                                |> Array.filter (fun i -> modelAudioBooks |> Array.exists (fun a -> a.Model.AudioBook.Id = i.Id) |> not)
                                |> Array.map (fun i -> AudioBookItemNew.init i)
                                |> Array.map (fun i -> AudioBookItemHelper.createAudioBookItem dispatch i)



                            return (audioBooksAlreadyOnTheDevice,cloudAudioBooks)
                        }

                    let processLoadedAudioBookFromDevice
                        (input:AsyncResult<(AudioBookItemNew.AudioBookItem [] * Domain.AudioBook[]),SynchronizeWithCloudErrors>) =
                        asyncResult {
                            let! (audioBooksItemsAlreadyOnTheDevice, cloudAudioBooks) = input

                            let audioBooksAlreadyOnTheDevice =
                                audioBooksItemsAlreadyOnTheDevice
                                |> Array.map (fun i -> i.Model.AudioBook)

                            do! audioBooksAlreadyOnTheDevice
                                |> Services.DataBase.insertNewAudioBooksInStateFile
                                |> AsyncResult.mapError (fun e -> StorageError e)

                            return (audioBooksItemsAlreadyOnTheDevice, cloudAudioBooks)
                        }




                    let determinateNewAddedAudioBooks
                        (modelAudioBooks:AudioBookItemNew.AudioBookItem[])
                        (input:AsyncResult<(AudioBookItemNew.AudioBookItem [] * Domain.AudioBook[]),SynchronizeWithCloudErrors>) =
                        asyncResult {
                            let! (audioBooksAlreadyOnTheDevice, cloudAudioBooks) = input

                            let modelAndDeviceAudiobooks = Array.concat [audioBooksAlreadyOnTheDevice; modelAudioBooks]

                            let newAudioBookItems =
                                AudioBookItemNew.Helpers.getNew audioBookItemDispatch modelAndDeviceAudiobooks cloudAudioBooks

                            return newAudioBookItems, modelAndDeviceAudiobooks, cloudAudioBooks
                        }





                    let processNewAddedAudioBooks
                        (input:AsyncResult<(AudioBookItemNew.AudioBookItem [] * AudioBookItemNew.AudioBookItem [] * Domain.AudioBook[]),SynchronizeWithCloudErrors>) =
                        asyncResult {
                            let! (newAudioBookItems,currentAudioBooks,cloudAudioBooks) = input

                            let onlyAudioBooks =
                                newAudioBookItems
                                |> Array.map (fun i -> i.Model.AudioBook)

                            do! onlyAudioBooks
                                |> Services.DataBase.insertNewAudioBooksInStateFile
                                |> AsyncResult.mapError (fun e -> StorageError e)


                            return newAudioBookItems, currentAudioBooks, cloudAudioBooks

                        }

                    let repairAudiobookMetadataIfNeeded
                        (input:AsyncResult<(AudioBookItemNew.AudioBookItem [] * AudioBookItemNew.AudioBookItem [] * Domain.AudioBook []),SynchronizeWithCloudErrors>) =
                        asyncResult {
                            let! newAudioBookItems, currentAudioBooks, cloudAudioBooks = input

                            let hasDiffMetaData a b =
                                a.FullName <> b.FullName ||
                                a.EpisodeNo <> b.EpisodeNo ||
                                a.EpisodenTitel <> b.EpisodenTitel ||
                                a.Group <> b.Group

                            let folders = Services.Consts.createCurrentFolders ()


                            let repairedAudioBooksItem =
                                currentAudioBooks
                                |> Array.choose (fun i ->
                                    cloudAudioBooks
                                    |> Array.tryFind (fun c -> c.Id = i.Model.AudioBook.Id)
                                    |> Option.bind (fun c ->
                                        if hasDiffMetaData c (i.Model.AudioBook) then
                                            let newAudioBookFolder = System.IO.Path.Combine(folders.audioBookDownloadFolderBase, c.FullName)

                                            let opt predicate input =
                                                if predicate then
                                                    Some input
                                                else
                                                    None

                                            let newAb = {
                                                i.Model.AudioBook with
                                                    FullName = c.FullName
                                                    EpisodeNo = c.EpisodeNo
                                                    EpisodenTitel = c.EpisodenTitel
                                                    Group = c.Group
                                                    Thumbnail = System.IO.Path.Combine(newAudioBookFolder, c.FullName + ".thumb.jpg") |> opt i.Model.AudioBook.State.Downloaded
                                                    Picture =   System.IO.Path.Combine(newAudioBookFolder, c.FullName + ".jpg") |> opt i.Model.AudioBook.State.Downloaded
                                                    State = {
                                                        i.Model.AudioBook.State with
                                                            DownloadedFolder = System.IO.Path.Combine(newAudioBookFolder,"audio") |> opt i.Model.AudioBook.State.Downloaded
                                                    }
                                            }
                                            // we need to generate a new Item, because the dispatch itself contains also the audiobook data
                                            let newItem =
                                                newAb
                                                |> AudioBookItemNew.init
                                                |> AudioBookItemHelper.createAudioBookItem dispatch
                                            Some newItem
                                        else
                                            None
                                    )
                                )

                            let nameDiffOldNewDownloaded =
                                currentAudioBooks
                                |> Array.choose (fun i ->
                                    cloudAudioBooks
                                    |> Array.tryFind (fun c -> c.Id = i.Model.AudioBook.Id && i.Model.DownloadState = AudioBookItemNew.Downloaded)
                                    |> Option.bind (fun c ->
                                        if c.FullName <> i.Model.AudioBook.FullName then
                                            Some {| OldName = i.Model.AudioBook.FullName; NewName = c.FullName |}
                                        else
                                            None
                                    )
                                )



                            match repairedAudioBooksItem with
                            | [||] ->
                                return newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded
                            | _ ->
                                do!
                                    repairedAudioBooksItem
                                    |> Array.map (fun i -> i.Model.AudioBook)
                                    |> Array.map (fun i -> Services.DataBase.updateAudioBookInStateFile i)
                                    |> Async.Sequential
                                    |> Async.map (fun i ->
                                        i
                                        |> Array.tryFind (function | Error _ -> true | Ok _ -> false)
                                        |> Option.defaultValue (Ok ())
                                    )
                                    |> AsyncResult.mapError (StorageError)

                                // replacing fixed entries
                                let currentAudioBooks =
                                    currentAudioBooks
                                    |> Array.map (fun c ->
                                        match repairedAudioBooksItem |> Array.tryFind (fun r -> c.Model.AudioBook.Id = r.Model.AudioBook.Id) with
                                        | None ->
                                            c
                                        | Some r ->
                                            r
                                    )

                                return newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded

                        }

                    let fixDownloadFolders
                        (input:AsyncResult<AudioBookItemNew.AudioBookItem [] * AudioBookItemNew.AudioBookItem [] * {| OldName:string; NewName:string |} [],SynchronizeWithCloudErrors>) =
                        asyncResult {
                            let! newAudioBookItems, currentAudioBooks, nameDiffOldNewDownloaded = input
                            let folders = Services.Consts.createCurrentFolders ()
                            try
                                nameDiffOldNewDownloaded
                                |> Array.map (fun x ->
                                    let oldFolder = System.IO.Path.Combine(folders.audioBookDownloadFolderBase,x.OldName)
                                    let newFolder = System.IO.Path.Combine(folders.audioBookDownloadFolderBase,x.NewName)
                                    {|
                                        OldFolder =     oldFolder
                                        NewFolder =     newFolder

                                        OldPicName =    System.IO.Path.Combine(oldFolder, x.OldName + ".jpg")
                                        NewPicName =    System.IO.Path.Combine(oldFolder, x.NewName + ".jpg")
                                        OldThumbName =  System.IO.Path.Combine(oldFolder, x.OldName + ".thumb.jpg")
                                        NewThumbName =  System.IO.Path.Combine(oldFolder, x.NewName + ".thumb.jpg")
                                    |}
                                )
                                |> Array.iter (fun x ->
                                    System.IO.File.Move(x.OldPicName,x.NewPicName)
                                    System.IO.File.Move(x.OldThumbName,x.NewThumbName)
                                    System.IO.Directory.Move(x.OldFolder,x.NewFolder)
                                )
                            with
                            | ex ->
                                return! AsyncResult.ofResult <| Error (StorageError ex.Message)

                            return newAudioBookItems, currentAudioBooks
                        }


                    let processResult
                        (input:AsyncResult<AudioBookItemNew.AudioBookItem [] * AudioBookItemNew.AudioBookItem [] ,SynchronizeWithCloudErrors>) =
                        async {
                            match! input with
                            | Ok (newAudioBookItems,currentAudioBooks) ->
                                do! newAudioBookItems |> notifyAfterSync
                                dispatch <| AudioBookItemsChanged (Array.concat [newAudioBookItems;currentAudioBooks])
                            | Error err ->
                                match err with
                                | NoSessionAvailable ->
                                    dispatch <| GotoLoginPage RefreshAudiobooks

                                | WebError comError ->
                                    match comError with
                                    | SessionExpired e ->
                                        dispatch <| GotoLoginPage RefreshAudiobooks

                                    | Other e ->
                                        do! Common.Helpers.displayAlert (Translations.current.Error, e, "OK")

                                    | Exception e ->
                                        let ex = e.GetBaseException()
                                        let msg = ex.Message + "|" + ex.StackTrace
                                        do! Common.Helpers.displayAlert (Translations.current.Error, msg, "OK")

                                    | Network msg ->
                                        do! Common.Helpers.displayAlert (Translations.current.Error, msg, "OK")

                                | StorageError msg ->
                                    do! Common.Helpers.displayAlert (Translations.current.Error, msg, "OK")
                        }

                    do!
                        checkLoginSession ()
                        |> loadAudioBooksFromCloud
                        |> loadAudioBooksFromDevice model.AudioBookItems
                        |> processLoadedAudioBookFromDevice
                        |> determinateNewAddedAudioBooks model.AudioBookItems
                        |> processNewAddedAudioBooks
                        |> repairAudiobookMetadataIfNeeded
                        |> fixDownloadFolders
                        |> processResult


                    dispatch <| BrowserPageMsg ([], BrowserPage.Msg.ChangeBusyState false)
                    dispatch AudioBookItemsUpdated
                }
                |> Async.Start
            |> Cmd.ofSub



        let displayLatestInfoMessageCmd : Cmd<Msg> =
            fun _ ->
                WhatsNew.displayLatestMessage () |> Async.Start
            |> Cmd.ofSub



        let deleteDatabaseCmd =
            fun dispatch ->
                Services.DataBase.deleteAudiobookDatabase ()
                dispatch Init
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

            //DownloadQueueModel = DownloadQueue.initModel None

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
            model, Commands.runMigrations
        | InitMigrationDone ->
            model, Commands.loadAudioBookItemsCmd
        | InitSuccessful items ->

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
                    Commands.synchonizeCurrentAudiobooksWithDeviceCmd newModel
                    Commands.displayLatestInfoMessageCmd
                    Commands.createAudioBookFileListIfNecessayCmd newModel
                ]

            newModel, cmds

        | AudioBookItemsChanged items ->
            let newModel = {
                model with
                    AudioBookItems = items
            }
            let cmds =
                Cmd.batch [
                    Cmd.ofMsg AudioBookItemsUpdated
                    // disable spinner only on main browser site
                    Cmd.ofMsg <| BrowserPageMsg ([], BrowserPage.Msg.ChangeBusyState false)
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

        | UpdateAudioBookItemFromAudioBook audioBook ->
            let cmd =
                fun dispatch ->
                    let newItems =
                        model.AudioBookItems
                        |> Array.map (fun i ->
                            if i.Model.AudioBook.Id = audioBook.Id then
                                let mdl = AudioBookItemNew.init audioBook
                                AudioBookItemHelper.createAudioBookItem dispatch mdl
                            else
                                i

                        )
                    dispatch <| AudioBookItemsChanged newItems

                |> Cmd.ofSub
            model, cmd



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

        | AudioBookItemMsg (abModel, AudioBookItemNew.OpenAudioBookDetail) ->
            let cmd = Cmd.ofMsg <| OpenAudioBookDetailPage abModel.AudioBook
            model, cmd

        | AudioBookItemMsg (abModel, AudioBookItemNew.OpenAudioBookPlayer) ->
            let cmd = Cmd.ofMsg <| GotoAudioPlayerPage abModel.AudioBook
            model, cmd

        | AudioBookItemMsg (abModel, AudioBookItemNew.DownloadCompleted result) ->
            let (model,cmd) =
                model |> onProcessAudioBookItemMsg (abModel, AudioBookItemNew.DownloadCompleted result)
            let (newAbMdl,_) = AudioBookItemNew.update (AudioBookItemNew.DownloadCompleted result) abModel
            let cmds =
                Cmd.batch [
                    cmd
                    // update all audiobook Items after download
                    Cmd.ofMsg <| UpdateAudioBookItemFromAudioBook newAbMdl.AudioBook
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


        | BrowserPageMsg (_, BrowserPage.LoadOnlineAudiobooks) ->
            let cmds =
                Cmd.batch [
                    // show spinner only on main browser site
                    Cmd.ofMsg <| BrowserPageMsg ([], BrowserPage.Msg.ChangeBusyState true)
                    Commands.synchronizeWithCloudCmd model

                ]

            model, cmds


        | BrowserPageMsg msg ->
            model |> onProcessBrowserPageMsg msg

        | BrowserPageModelClosed groups ->
            let concatStr (str:string list) = System.String.Join ("", str)

            let newModels =
                model.BrowserPageModels
                |> List.filter (fun i -> (i.SelectedGroups |> concatStr) <> groups)

            { model with BrowserPageModels = newModels }, Cmd.none


        | AudioPlayerPageMsg (AudioPlayerPage.UpdatePostion (filename, position, duration)) ->
            match model.AudioPlayerPageModel with
            | None ->
                model, Cmd.none
            | Some apModel ->

                let newAudioBook = {
                    apModel.AudioBook with
                        State = {
                            apModel.AudioBook.State with
                                CurrentPosition = Some {
                                    Filename = filename
                                    Position = position |> float |> System.TimeSpan.FromMilliseconds
                                }
                        }
                }

                let cmds = [
                    Cmd.ofMsg <| UpdateAudioBookItemFromAudioBook newAudioBook
                ]

                model |> onProcessAudioPlayerMsg cmds (AudioPlayerPage.UpdatePostion (filename, position, duration))

        | AudioPlayerPageMsg msg ->
            model |> onProcessAudioPlayerMsg [] msg



        | AudioBookDetailPageMsg msg ->
            model |> onProcessAudioBookDetailPageMsg msg
        | SettingsPageMsg (SettingsPage.DeleteDatabase) ->
            let cmds = Cmd.batch [
                Commands.deleteDatabaseCmd
            ]
            { model with AudioBookItems = [||] }, cmds

        | SettingsPageMsg msg ->
            model |> onProcessSettingsPageMsg msg
        | SupportFeedbackPageMsg msg ->
            model |> onSupportFeedbackPageMsg msg
        //| DownloadQueueMsg msg ->
        //    model |> onProcessDownloadQueueMsg msg
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

        | QuitApplication ->
            model |> onQuitApplication





    and onAskForAppPermissionMsg model =
        let ask =
            async {
                // currenty no permission to ask
                return Init
            } |> Cmd.ofAsyncMsg

        model, ask


    and onQuitApplication model =

        let quitApp () =
            try
                Process.GetCurrentProcess().CloseMainWindow() |> ignore
            with
            | ex ->
                Crashes.TrackError (ex, Map.empty)

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
                [
                    cmd
                    Cmd.ofMsg AudioBookItemsUpdated
                ] |> Cmd.batch

            { model with AudioBookItems = audioBooks }, cmds



    and onProcessMainPageMsg msg model =
        match model.MainPageModel with
        | None ->
            model,Cmd.none
        | Some mdl ->
            let m,cmd = MainPage.update msg mdl
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
                    let modalInput:ModalHelpers.ModalManager.PushModelInput = {
                        Appearence=ModalHelpers.ModalManager.Shell
                        UniqueId="loginPage"
                        CloseEvent= (fun () -> dispatch <| LoginClosed)
                        Page = LoginPage.view m (LoginPageMsg >> dispatch)
                    }
                    ModalHelpers.ModalManager.pushOrUpdateModal modalInput
                |> Cmd.ofSub

            {model with LoginPageModel = Some m}, Cmd.batch [updateLoginPageCmd;(Cmd.map LoginPageMsg cmd); externalCmds ]
        | None -> model, Cmd.none




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


    and onProcessAudioPlayerMsg additionalCmds msg model =

        match model.AudioPlayerPageModel with
        | Some audioPlayerPageModel ->
            let m,cmd = AudioPlayerPage.update msg audioPlayerPageModel

            let cmds =
                [
                    Cmd.map AudioPlayerPageMsg cmd
                    Cmd.ofMsg AudioBookItemsUpdated
                ] @ additionalCmds
                |> Cmd.batch

            {model with AudioPlayerPageModel = Some m}, cmds

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
                    let modalInput:ModalHelpers.ModalManager.PushModelInput = {
                        Appearence=ModalHelpers.ModalManager.Shell
                        UniqueId="audiobookDetail"
                        CloseEvent= (fun () -> dispatch <| CloseAudioBookDetailPage)
                        Page = AudioBookDetailPage.view m (AudioBookDetailPageMsg >> dispatch)
                    }
                    ModalHelpers.ModalManager.pushOrUpdateModal modalInput

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
                     let modalInput:ModalHelpers.ModalManager.PushModelInput = {
                         Appearence=ModalHelpers.ModalManager.Shell
                         UniqueId="supportFeedback"
                         CloseEvent= (fun () -> dispatch <| FeedbackSupportPageClosed)
                         Page = SupportFeedback.view m (SupportFeedbackPageMsg >> dispatch)
                     }
                     ModalHelpers.ModalManager.pushOrUpdateModal modalInput
                 |> Cmd.ofSub

             if msg = SupportFeedback.Msg.SendSuccessful then
                 Common.ModalBaseHelpers.closeCurrentModal shellRef


             {model with SupportFeedbackModel = Some m}, Cmd.batch [updateFeedbackPageCmd;(Cmd.map SupportFeedbackPageMsg cmd)]


    //and onProcessDownloadQueueMsg msg model =
    //    let newModel, cmd, externalMsg = DownloadQueue.update msg model.DownloadQueueModel
    //    let mainCmds =
    //        match externalMsg with
    //        | None -> Cmd.none
    //        | Some excmd ->
    //            match excmd with
    //            | DownloadQueue.ExternalMsg.ExOpenLoginPage cameFrom ->
    //                Cmd.ofMsg (GotoLoginPage cameFrom)
    //            | DownloadQueue.ExternalMsg.UpdateAudioBook abModel ->
    //                Cmd.ofMsg (UpdateAudioBook (abModel,""))
    //            | DownloadQueue.ExternalMsg.UpdateDownloadProgress (abModel,progress) ->
    //                Cmd.ofMsg (UpdateAudioBook (abModel,""))
    //            | DownloadQueue.ExternalMsg.PageChangeBusyState state ->
    //                Cmd.none

    //    { model with DownloadQueueModel = newModel}, Cmd.batch [(Cmd.map DownloadQueueMsg cmd); mainCmds ]


    and onGotoMainPageMsg model =
        match shellRef.TryValue with
        | Some sr ->
            sr.GoToAsync("///mainpage" |> routeToShellNavigationState) |> Async.AwaitTask |> Async.RunSynchronously
        | None ->
            ()
        model, Cmd.none


    and onGotoLoginPageMsg cameFrom model =
        let m,cmd = LoginPage.init cameFrom

        let openLoginPageCmd =
            fun dispatch ->
                let modalInput:ModalHelpers.ModalManager.PushModelInput = {
                    Appearence=ModalHelpers.ModalManager.Shell
                    UniqueId="loginPage"
                    CloseEvent= (fun () -> dispatch <| LoginClosed)
                    Page = LoginPage.view m (LoginPageMsg >> dispatch)
                }
                ModalHelpers.ModalManager.pushOrUpdateModal modalInput
            |> Cmd.ofSub

        {model with LoginPageModel = Some m},Cmd.batch [ openLoginPageCmd; (Cmd.map LoginPageMsg cmd)  ]


    and onLoginClosed model =
        // if there is no cookie container, than remove all queued items
        // because when the login page appears it's maybe from download
        // so if the user dismiss the login, than remove the queued state
        let model =
            if model.CookieContainer.IsNone then
                {
                    model with
                        AudioBookItems =
                            model.AudioBookItems
                            |> Array.map (fun i ->
                                if i.Model.DownloadState = AudioBookItemNew.Queued then
                                    { i with Model = { i.Model with DownloadState = AudioBookItemNew.NotDownloaded } }
                                else
                                    i
                            )
                }
            else
                model


        { model with LoginPageModel = None }, Cmd.ofMsg AudioBookItemsUpdated


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
                Crashes.TrackError (ex, Map.empty)
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
                let modalInput:ModalHelpers.ModalManager.PushModelInput = {
                    Appearence=ModalHelpers.ModalManager.Shell
                    UniqueId="supportFeedback"
                    CloseEvent= (fun () -> dispatch <| FeedbackSupportPageClosed)
                    Page = SupportFeedback.view m (SupportFeedbackPageMsg >> dispatch)
                }
                ModalHelpers.ModalManager.pushOrUpdateModal modalInput
            |> Cmd.ofSub

        {model with SupportFeedbackModel = Some m},Cmd.batch [ openFeedbackPageCmd; (Cmd.map SupportFeedbackPageMsg cmd)  ]


    and onGotoDownloadPage state model =
        let queue =
            state.Downloads
            |> List.filter (fun i -> match i.State with | Services.DownloadService.Open | Services.DownloadService.Running _ -> true | _ -> false )
            |> List.choose (fun i ->
                let abModel =
                    model.AudioBookItems
                    |> Array.tryFind (fun a -> a.Model.AudioBook.Id = i.AudioBook.Id)

                match i.State with
                | Services.DownloadService.Open ->
                    abModel |> Option.map (fun abModel -> { abModel with Model = { abModel.Model with DownloadState = AudioBookItemNew.Queued } })
                | Services.DownloadService.Running (a,c) ->
                    abModel |> Option.map (fun abModel -> { abModel with Model = { abModel.Model with DownloadState = AudioBookItemNew.Downloading (a,c)} })
                | _ ->
                    None
            )

        let newItems =
            model.AudioBookItems
            |> Array.map (fun ab ->
                queue
                |> List.tryFind (fun i -> i.Model.AudioBook.Id = ab.Model.AudioBook.Id)
                |> Option.defaultValue ab
            )

        { model with AudioBookItems = newItems }, Cmd.none




    and onFeedbackSupportPageClosedPage model =
        {model with SupportFeedbackModel = None }, Cmd.none



    and onOpenAudioBookDetailPage audiobook model =
        let m,cmd = AudioBookDetailPage.init audiobook

        let openAudioBookDetailPageCmd =
            fun dispatch ->
                let modalInput:ModalHelpers.ModalManager.PushModelInput = {
                    Appearence=ModalHelpers.ModalManager.Shell
                    UniqueId="audiobookDetail"
                    CloseEvent= (fun () -> dispatch <| CloseAudioBookDetailPage)
                    Page = AudioBookDetailPage.view m (AudioBookDetailPageMsg >> dispatch)
                }
                ModalHelpers.ModalManager.pushOrUpdateModal modalInput
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
                // look who is queued and start the download again
                let queued =
                    model.AudioBookItems
                    |> Array.filter (fun i -> i.Model.DownloadState = AudioBookItemNew.Queued)

                let restartDownloadCmds =
                    queued
                    |> Array.map (fun i -> AudioBookItemMsg (i.Model, AudioBookItemNew.Msg.AddToDownloadQueue))
                    |> Array.map (Cmd.ofMsg)
                    |> Array.toList

                // Todo: try download again
                model, Cmd.batch restartDownloadCmds

        // and close Login Modal
        ModalHelpers.ModalManager.removeLastModal ()
        // set here login page model to None to avoid reopening of the loginPage
        { model with LoginPageModel = None; CookieContainer = Some cc }, cmd





    let subscription model =
        Cmd.ofSub (fun dispatch ->

            //// avoid double registration of event handlers

            //if (not AudioBookItemProcessor.abItemUpdatedEvent.HasListeners) then
            //    AudioBookItemProcessor.Events.onAbItemUpdated.Add(fun i ->
            //        //dispatch (MainPageMsg (MainPage.Msg.UpdateAudioBook))
            //        //dispatch (BrowserPageMsg (BrowserPage.Msg.UpdateAudioBook))
            //        dispatch AudioBookItemsUpdated
            //)
            //// when updating an audio book, that update the underlying audio book in the Item
            //if (not Services.DataBase.storageProcessorAudioBookUpdatedEvent.HasListeners) then
            //    Services.DataBase.Events.storageProcessorOnAudiobookUpdated.Add(fun item ->
            //        AudioBookItemProcessor.updateUnderlyingAudioBookInItem item
            //    )

            //if (not Services.DataBase.storageProcessorAudioBookDeletedEvent.HasListeners) then
            //    Services.DataBase.Events.storageProcessorOnAudiobookDeleted.Add(fun item->
            //        AudioBookItemProcessor.deleteAudioBookInItem item
            //    )

            //if (not Services.DataBase.storageProcessorAudioBookAdded.HasListeners) then
            //    Services.DataBase.Events.storageProcessorOnAudiobookAdded.Add(fun items ->
            //        AudioBookItemProcessor.insertAudiobooks items
            //    )

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
                        Translations.current.MainPage
                        "mainPage")
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

                Controls.contentPageWithBottomOverlay
                    ref
                    (audioPlayerOverlay model.AudioPlayerPageModel)
                    newView
                    browserModel.IsLoading
                    "BrowserPage"
                    (String.concatStr browserModel.SelectedGroups)


            match model.BrowserPageModels with
            | [] ->
                None, []
            | [x] ->
                Some <| createPage ModalHelpers.ModalManager.browserRef x, []
            | head::tail ->
                let h = Some <| createPage ModalHelpers.ModalManager.browserRef head
                let t = tail |> List.map (fun m -> createPage BrowserPage.pageRef m, m.SelectedGroups)
                h, t



        rest
        |> List.iteri (
            fun idx (bPage,selectedGroup) ->
                let pageTitle = selectedGroup |> String.concatStr
                let modalInput:ModalHelpers.ModalManager.PushModelInput = {
                    Appearence=ModalHelpers.ModalManager.BrowserPage
                    UniqueId=pageTitle
                    CloseEvent= (fun () -> dispatch <| BrowserPageModelClosed pageTitle)
                    Page = bPage
                }
                ModalHelpers.ModalManager.pushOrUpdateModal modalInput
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
            ref = ModalHelpers.ModalManager.shellRef,
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


                        match DownloadQueueNew.init model.AudioBookItems with
                        | [||] ->
                            ()
                        | items ->
                            yield createShellContent "Downloads" downloadQueue "download_icon.png" <| DownloadQueueNew.view items


                        match mainPage,browserPage,settingsPage,audioPlayerPage with
                        | None, None, None, None ->
                            yield View.ShellContent(route="emptypage",content=View.ContentPage(content=View.Label(text="...")))
                        | _ ->
                            ()
                    ]

                )

                yield View.ShellContent(route="permissiondeniedpage",content=View.ContentPage(content=View.Label(text="...")))


            ]
        )



    let program = Program.mkProgram init update view

type MainApp () as app =
    inherit Application ()

    do
        AppCenter.Start(sprintf "ios=(...);android=%s" Global.appcenterAndroidId, typeof<Analytics>, typeof<Crashes>)
        // Display Error when somthing is with the storage processor
        //if not Services.DataBase.storageProcessorErrorEvent.HasListeners then
        //    Services.DataBase.Events.storageProcessorOnError.Add(fun e ->
        //        async {
        //            let message =
        //                sprintf "Es ist ein Fehler mit der Datenbank aufgetreten. (%s). Das Programm wird jetzt beendet. Versuchen Sie es noch einmal oder melden Sie sich bin Support." e.Message

        //            do! Common.Helpers.displayAlert(Translations.current.Error,message, "OK")
        //            try
        //                Process.GetCurrentProcess().CloseMainWindow() |> ignore
        //            with
        //            | _ as ex ->
        //                Crashes.TrackError ex
        //            return ()
        //        } |> Async.StartImmediate
        //    )



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
        ModalHelpers.ModalManager.cleanUpModalPageStack ()
        ()













