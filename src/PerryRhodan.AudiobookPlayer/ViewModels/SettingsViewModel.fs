namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open CherylUI.Controls
open Dependencies
open Global
open Microsoft.Maui.ApplicationModel
open PerryRhodan.AudiobookPlayer.Services
open PerryRhodan.AudiobookPlayer.ViewModels.LoginPage
open PerryRhodan.AudiobookPlayer.Views
open ReactiveElmish.Avalonia
open ReactiveElmish
open Elmish.SideEffect
open Services
open Services.DependencyServices
open Services.Helpers

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
        InteractiveContainer.ShowDialog feedBackView
    
    member this.GoBackHome() =
        DependencyService.Get<IMainViewModel>().GotoHomePage()


    member this.DeveloperModeSwitchCounter = this.Bind(local, fun e -> e.DeveloperModeSwitchCounter)
    member this.DeveloperMode = this.Bind(local, fun e -> e.DeveloperMode)

    static member DesignVM = new SettingsViewModel()



