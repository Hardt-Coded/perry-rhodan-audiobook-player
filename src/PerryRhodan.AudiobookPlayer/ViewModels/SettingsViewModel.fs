namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open System.IO
open CherylUI.Controls
open Dependencies
open Microsoft.Maui.ApplicationModel
open Microsoft.Maui.ApplicationModel.DataTransfer
open Microsoft.Maui.Devices
open Microsoft.Maui.Storage
open PerryRhodan.AudiobookPlayer.Common
open PerryRhodan.AudiobookPlayer.Services
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.Views
open ReactiveElmish.Avalonia
open ReactiveElmish
open Elmish.SideEffect

module SettingsPage =


    type State = {
        DataProtectionStuff: bool
        RewindWhenStartAfterShortPeriodInSec:int
        RewindWhenStartAfterLongPeriodInSec:int
        LongPeriodBeginsAfterInMinutes:int
        JumpDistance:int
        DeveloperModeSwitchCounter:int
        DeveloperMode:bool
        FirstStart: bool

        ExternalAudioBookFileUrl: string
        ExternalAudioBookName: string
        IsBusy: bool
    }


    type Msg =
        | ShowDataProtectionStuff
        | HideDataProtectionStuff

        | SetJumpDistanceValue of int
        | SetRewindWhenStartAfterShortPeriodInSecValue of int
        | SetRewindWhenStartAfterLongPeriodInSecValue of int
        | SetLongPeriodBeginsAfterInMinutesValue of int

        | SetDeveloperModeSwitchCounter of int
        | SetDeveloperMode of bool
        | SetFirstStart of bool
        | DeleteDatabase
        | ImportDatabase

        | AddExternalAudioBookZipFile
        | ChangeExternalAudioBookFileUrl of url:string
        | ChangeExternalAudioBookName of name:string

        | OpenFeedbackPage
        | KeyboardStateChanged
        | CloseDialog

        | IsBusy of bool



    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | CloseDialog
        | Init
        | StoreJumpDistance
        | StoreRewindWhenStartAfterShortPeriodInSec
        | StoreRewindWhenStartAfterLongPeriodInSec
        | StoreLongPeriodBeginsAfterInMinutes
        | StoreDeveloperMode
        | StoreFirstStart



    let init () =
        {
            DataProtectionStuff = false
            RewindWhenStartAfterShortPeriodInSec = 0
            RewindWhenStartAfterLongPeriodInSec = 0
            LongPeriodBeginsAfterInMinutes = 0
            JumpDistance = 30
            DeveloperMode = false
            DeveloperModeSwitchCounter = 0
            ExternalAudioBookFileUrl = ""
            ExternalAudioBookName = ""
            FirstStart = false
            IsBusy = false
        }, SideEffect.Init



    let rec update msg (state:State) =
        match msg with
        | IsBusy b ->
            { state with IsBusy = b }, SideEffect.None
        | ShowDataProtectionStuff ->
            state |> onShowDataProtectionStuffMsg
        | HideDataProtectionStuff ->
            state |> onHideDataProtectionStuffMsg
        | SetJumpDistanceValue jd ->
            state |> onSetJumpDistanceValue jd
        | SetRewindWhenStartAfterShortPeriodInSecValue sec ->
            state |> onSetRewindWhenStartAfterShortPeriodInSecValue sec
        | SetRewindWhenStartAfterLongPeriodInSecValue sec ->
            state |> onSetRewindWhenStartAfterLongPeriodInSecValue sec
        | SetLongPeriodBeginsAfterInMinutesValue min ->
            state |> onSetLongPeriodBeginsAfterInMinutesValue min

        | SetDeveloperMode value ->
            state |> onSetDeveloperMode value
        | SetDeveloperModeSwitchCounter value ->
            state |> onSetDeveloperSwitchCounter value
        | OpenFeedbackPage ->
            state, SideEffect.None
        | DeleteDatabase ->
            state, SideEffect.None
        | CloseDialog ->
            state, SideEffect.CloseDialog
        | ImportDatabase ->
            (*let cmd =
                fun dispatch ->
                    async {
                        let fileTypes =
                            [
                                (DevicePlatform.Android, ["*/*"] |> List.toSeq )
                            ] |> dict
                        let! result = FilePicker.PickMultipleAsync(PickOptions(PickerTitle="Select Database Files", FileTypes = FilePickerFileType(fileTypes))) |> Async.AwaitTask
                        if result |> isNull |> not then
                            do! Async.Sleep 1000
                            do! Migrations.AudiobooksMissingAfterUpdateAndroid10.importDatabases (result |> Seq.map (fun x -> x.FullPath))
                    }
                    |> Async.StartImmediate
                |> Cmd.ofSub*)
            state,  SideEffect.None

        | AddExternalAudioBookZipFile ->
            (*let cmd =
                fun dispatch ->
                    async {
                        if (model.ExternalAudioBookFileUrl = "" || model.ExternalAudioBookName = "") then
                            do! Common.Helpers.displayAlert("Externes Audiobook hinzufügen", "Bitte Namen und Url angeben!", "OK!")
                        else

                            let audioBook =
                                {
                                    AudioBook.Empty with
                                        FullName = model.ExternalAudioBookName
                                        EpisodenTitel =
                                            if model.ExternalAudioBookName.Contains ("-") then
                                                model.ExternalAudioBookName.Split([| '-' |]).[1]
                                            else
                                                ""
                                        DownloadUrl = Some model.ExternalAudioBookFileUrl
                                        Id = 16000000 + (System.Random().Next(500000))
                                        Group = "External Audiobooks"

                                }
                            let! res =  Services.DataBase.insertNewAudioBooksInStateFile [| audioBook |]
                            match res with
                            | Error e ->
                                do! Common.Helpers.displayAlert("Externes Audiobook hinzufügen", e, "OK!")
                            | Ok _ ->
                                do! Common.Helpers.displayAlert("Externes Audiobook hinzufügen", "Erfolgreich!", "OK!")
                    }
                    |> Async.StartImmediate
                |> Cmd.ofSub*)
            state,  SideEffect.None
        | ChangeExternalAudioBookFileUrl url ->
            { state with ExternalAudioBookFileUrl = url },  SideEffect.None
        | ChangeExternalAudioBookName name ->
            { state with ExternalAudioBookName = name },  SideEffect.None
        | KeyboardStateChanged ->
            state, SideEffect.None
        | SetFirstStart b ->
            { state with FirstStart = b }, SideEffect.StoreFirstStart


    and onSetDeveloperSwitchCounter value state  =
        (*let switchCmd =
            if value > 5 then
                Cmd.ofMsg (SetDeveloperMode (not model.DeveloperMode))
            else
                Cmd.none
        let resetCmd =
            if value = 1 then
                async {
                    do! Async.Sleep 3000
                    return (SetDeveloperModeSwitchCounter 0)
                } |> Cmd.ofAsyncMsg
            else
                Cmd.none*)

        {state with DeveloperModeSwitchCounter = value}, SideEffect.None

    and onSetDeveloperMode value state =
        {state with DeveloperMode = value;}, SideEffect.StoreDeveloperMode


    and onSetJumpDistanceValue jd state =
        {state with JumpDistance = jd}, SideEffect.StoreJumpDistance

    and onSetRewindWhenStartAfterShortPeriodInSecValue sec state =
        {state with RewindWhenStartAfterShortPeriodInSec = sec}, SideEffect.StoreRewindWhenStartAfterShortPeriodInSec

    and onSetRewindWhenStartAfterLongPeriodInSecValue sec state =
        {state with RewindWhenStartAfterLongPeriodInSec = sec}, SideEffect.StoreRewindWhenStartAfterLongPeriodInSec


    and onSetLongPeriodBeginsAfterInMinutesValue min state =
        {state with LongPeriodBeginsAfterInMinutes = min}, SideEffect.StoreLongPeriodBeginsAfterInMinutes


    and onShowDataProtectionStuffMsg state =
        { state with DataProtectionStuff = true },  SideEffect.None


    and onHideDataProtectionStuffMsg state =
        { state with DataProtectionStuff = false },  SideEffect.None

module SideEffects =
    open SettingsPage

    let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
        let globalSettings = DependencyService.Get<GlobalSettingsService>()
        task {
            if sideEffect = SideEffect.None then
                return ()
            else
                dispatch <| IsBusy true
                do!
                    task {
                        match sideEffect with
                        | SideEffect.None ->
                            return ()

                        | SideEffect.Init ->


                            dispatch (SetRewindWhenStartAfterShortPeriodInSecValue globalSettings.RewindWhenStartAfterShortPeriodInSec)
                            dispatch (SetRewindWhenStartAfterLongPeriodInSecValue globalSettings.RewindWhenStartAfterLongPeriodInSec)
                            dispatch (SetLongPeriodBeginsAfterInMinutesValue globalSettings.LongPeriodBeginsAfterInMinutes)
                            dispatch (SetJumpDistanceValue (globalSettings.JumpDistance / 1000))
                            //dispatch (SetDeveloperMode globalSettings.)
                            dispatch (SetFirstStart globalSettings.IsFirstStart)


                            return ()

                        | SideEffect.CloseDialog ->
                            InteractiveContainer.CloseDialog()
                            return ()

                        | SideEffect.StoreJumpDistance ->
                            //do! Services.SystemSettings.setJumpDistance <| state.JumpDistance * 1000
                            globalSettings.JumpDistance <- state.JumpDistance * 1000

                            return ()

                        | SideEffect.StoreRewindWhenStartAfterShortPeriodInSec ->
                            //do! Services.SystemSettings.setRewindWhenStartAfterShortPeriodInSec state.RewindWhenStartAfterShortPeriodInSec
                            globalSettings.RewindWhenStartAfterShortPeriodInSec <- state.RewindWhenStartAfterShortPeriodInSec
                            return ()

                        | SideEffect.StoreRewindWhenStartAfterLongPeriodInSec ->
                            //do! Services.SystemSettings.setRewindWhenStartAfterLongPeriodInSec state.RewindWhenStartAfterLongPeriodInSec
                            globalSettings.RewindWhenStartAfterLongPeriodInSec <- state.RewindWhenStartAfterLongPeriodInSec
                            return ()

                        | SideEffect.StoreLongPeriodBeginsAfterInMinutes ->
                            //do! Services.SystemSettings.setLongPeriodBeginsAfterInMinutes state.LongPeriodBeginsAfterInMinutes
                            globalSettings.LongPeriodBeginsAfterInMinutes <- state.LongPeriodBeginsAfterInMinutes
                            return ()

                        | SideEffect.StoreDeveloperMode ->
                            //do! Services.SystemSettings.setDeveloperMode state.DeveloperMode
                            return ()

                        | SideEffect.StoreFirstStart ->
                            //do! Services.SystemSettings.setIsFirstStart state.FirstStart
                            globalSettings.IsFirstStart <- state.FirstStart
                            return ()
                    }

                dispatch <| IsBusy false
        }



open SettingsPage

type SettingsViewModel() =
    inherit ReactiveElmishViewModel()


    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore


    let feedBackView =
        let view = FeedbackView()
        view.DataContext <- new FeedbackViewModel()
        view


    // option members from state
    member this.DataProtectionStuff
        with get() = this.Bind(local, fun e -> e.DataProtectionStuff)
        and set(value) = if this.IsInitialized then local.Dispatch (if value then ShowDataProtectionStuff else HideDataProtectionStuff)

    member this.RewindWhenStartAfterShortPeriodInSec
        with get() = this.Bind(local, fun e -> e.RewindWhenStartAfterShortPeriodInSec)
        and set(value) = if this.IsInitialized then local.Dispatch (SetRewindWhenStartAfterShortPeriodInSecValue value)

    member this.RewindWhenStartAfterLongPeriodInSec
        with get() = this.Bind(local, fun e -> e.RewindWhenStartAfterLongPeriodInSec)
        and set(value) = if this.IsInitialized then local.Dispatch (SetRewindWhenStartAfterLongPeriodInSecValue value)

    member this.LongPeriodBeginsAfterInMinutes
        with get() = this.Bind(local, fun e -> e.LongPeriodBeginsAfterInMinutes)
        and set(value) = if this.IsInitialized then local.Dispatch (SetLongPeriodBeginsAfterInMinutesValue value)

    member this.JumpDistance
        with get() = this.Bind(local, fun e -> e.JumpDistance)
        and set(value) =
            if this.IsInitialized then local.Dispatch (SetJumpDistanceValue value)

    member this.FirstStart
        with get() = this.Bind(local, fun e -> e.FirstStart)
        and set(value) =
            if this.IsInitialized then local.Dispatch (SetFirstStart value)

    member val IsInitialized = false with get, set

    member this.OnInitialized() =
        this.IsInitialized <- true

    member this.SecondsValues =
        [|
            for i in 0 .. 120 do
                i, $"{i} Sekunden"
        |]


    member this.MinutesValues =
        [|
            for i in 0 .. 120 do
                i, $"{i} Minuten"
        |]

    member this.YesNorValues =
        [|
            false, "Nein"
            true, "Ja"
        |]

    member this.ShowPrivacyPolicies() =
        // open web browser url
        let uri = Uri("https://www.hardt-solutions.com/PrivacyPolicies/EinsAMedienAudioBookPlayer.html")
        Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred) |> ignore


    member this.ShowFeedbackPage() =
        InteractiveContainer.ShowDialog(feedBackView, true)

    member this.GoBackHome() =
        DependencyService.Get<IMainViewModel>().GotoHomePage()

    member this.PackageName =
        $"Name: {DependencyService.Get<IPackageInformation>().Name()}"

    member this.Version =
        $"Version: {DependencyService.Get<IPackageInformation>().GetVersion()}"

    member this.Build =
        $"Build: {DependencyService.Get<IPackageInformation>().GetBuild()}"

    member this.SendMail() =
        Launcher.OpenAsync(new Uri("mailto:info@hardt-solutions.de?subject=Eins A Medien Audioplayer"))


    member this.ShareZippedDatabase() =
        try
            let stateFile = Services.Consts.createCurrentFolders().audioBooksStateDataFilePath
            // zip file with ICSharpCode.SharpZipLib and share
            let zipFile = System.IO.Path.Combine(Services.Consts.createCurrentFolders().audioBookDownloadFolderBase, $"{DateTime.Now:```yyyyMMddHHmm``}-einsamedien-backup.zip")
            let zip = new ICSharpCode.SharpZipLib.Zip.FastZip()
            zip.CreateZip(zipFile, stateFile.Replace("audiobooks.db",""), true, "audiobooks.db")
            Share.RequestAsync(ShareFileRequest("Einsamedien Backup", ShareFile(zipFile, "application/zip")))
            |> Task.tmap (fun () ->
                System.IO.File.Delete(zipFile)
            )
            |> ignore
        with
        | ex ->
            Global.telemetryClient.TrackException ex
            Services.Notifications.showErrorMessage ex.Message |> ignore


    member this.ImportZippedDatabase() =
        let fileTypes =
            [
                (DevicePlatform.Android, ["*/*"] |> List.toSeq )
            ] |> dict
        FilePicker.PickMultipleAsync(PickOptions(PickerTitle="Backup auswählen", FileTypes = FilePickerFileType(fileTypes)))
        |> Task.bind (fun files ->
            task {
            if files |> Seq.isEmpty then
                return ()
            else
                let file = files |> Seq.head
                let folder = Services.Consts.createCurrentFolders()
                try
                    if file.FullPath.EndsWith("audiobooks.db") then
                        File.Move(file.FullPath, folder.audioBooksStateDataFilePath, true)
                        AudioBookStore.globalAudiobookStore.Dispatch AudioBookStore.AudioBookElmish.ReloadAudiobooks
                        do! Services.Notifications.showMessage "Erfolgreich" "Ihr Backup wurde erfolgreich importiert. Bitte App neustarten!"
                        System.Environment.Exit(0)
                    else
                        let zip = new ICSharpCode.SharpZipLib.Zip.FastZip()
                        zip.ExtractZip(file.FullPath, folder.audioBookDownloadFolderBase, "")
                        let stateFile = System.IO.Path.Combine(folder.audioBookDownloadFolderBase, "audiobooks.db")
                        if System.IO.File.Exists(stateFile) |> not then
                            do! Services.Notifications.showErrorMessage "Fehler beim Importieren des Backups: Datei nicht gefunden."
                        else
                            File.Move(stateFile, folder.audioBooksStateDataFilePath, true)
                            AudioBookStore.globalAudiobookStore.Dispatch AudioBookStore.AudioBookElmish.ReloadAudiobooks
                            do! Services.Notifications.showMessage "Erfolgreich" "Ihr Backup wurde erfolgreich importiert. Bitte die App neustarten!"
                            System.Environment.Exit(0)
                with
                | ex ->
                    Global.telemetryClient.TrackException ex
                    do! Services.Notifications.showErrorMessage $"Fehler beim Importieren des Backups: {ex.Message}"
            }
        )



    member this.DeveloperModeSwitchCounter = this.Bind(local, fun e -> e.DeveloperModeSwitchCounter)
    member this.DeveloperMode = this.Bind(local, fun e -> e.DeveloperMode)

    static member DesignVM = new SettingsViewModel()



