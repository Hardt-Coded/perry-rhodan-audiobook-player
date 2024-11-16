module Services

open System
open System.Diagnostics
open System.IO
open System.Threading
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Platform
open Avalonia.Threading
open CherylUI.Controls
open DialogHostAvalonia
open Domain
open System.Net
open FSharp.Control
open HtmlAgilityPack
open Microsoft.Maui.ApplicationModel
open Newtonsoft.Json
open System.Net.Http
open ICSharpCode.SharpZipLib.Zip
open Common
open FsHttp
open PerryRhodan.AudiobookPlayer.Notification.ViewModels
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer
open SkiaSharp
open Dependencies
open PerryRhodan.AudiobookPlayer.Common


type StateConnectionString = | StateConnectionString of string
type FileInfoConnectionString = | FileInfoConnectionString of string




module DependencyServices =


    type NavigationService() =
        let mutable backbuttonPressedAction = None
        let concurrentDict = System.Collections.Concurrent.ConcurrentDictionary<string,(unit->unit)>()
        interface INavigationService with
            member this.BackbuttonPressedAction
                with get() = backbuttonPressedAction

            member this.RegisterBackbuttonPressed(action) =
                backbuttonPressedAction <- Some action

            member this.ResetBackbuttonPressed() =
                backbuttonPressedAction <- None

            member this.MemorizeBackbuttonCallback(memoId) =
                let this = this :> INavigationService
                match this.BackbuttonPressedAction with
                | None ->
                    // nothing to memorize
                    this.ResetBackbuttonPressed()
                | Some action ->
                    let func = Func<string,(unit->unit),(unit->unit)>(fun _ _ -> action)
                    concurrentDict.AddOrUpdate(memoId, action, func) |> ignore
                    this.ResetBackbuttonPressed()


            member this.RestoreBackbuttonCallback(memoId) =
                let this = this :> INavigationService
                let gotAction, storedAction = concurrentDict.TryGetValue(memoId)
                if gotAction then
                    this.RegisterBackbuttonPressed storedAction
                else
                    this.ResetBackbuttonPressed()

module Consts =


    let isToInternalStorageMigrated () =
        File.Exists (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".migrated"))

    let createCurrentFolders =
        let mutable folders = None
        fun () ->
            match folders with
            | None ->

                let currentLocalBaseFolder =
                    let storageFolder =
                        Microsoft.Maui.Storage.FileSystem.AppDataDirectory

                    let baseFolder =
                        let bf = Path.Combine(storageFolder,"PerryRhodan.AudioBookPlayer")
                        bf

                    try
                        if not (Directory.Exists(baseFolder)) then
                            Directory.CreateDirectory(baseFolder) |> ignore
                        baseFolder
                    with
                    | ex ->
                        baseFolder

                let currentLocalDataFolder =
                    Path.Combine(currentLocalBaseFolder,"data")


                let stateFileFolder = Path.Combine(currentLocalDataFolder,"states")
                let audioBooksStateDataFile = Path.Combine(stateFileFolder,"audiobooks.db")
                let audioBookAudioFileDb = Path.Combine(stateFileFolder,"audiobookfiles.db")
                let audioBookDownloadFolderBase = Path.Combine(currentLocalDataFolder,"audiobooks")
                if not (Directory.Exists(stateFileFolder)) then
                    Directory.CreateDirectory(stateFileFolder) |> ignore
                if not (Directory.Exists(audioBookDownloadFolderBase)) then
                    Directory.CreateDirectory(audioBookDownloadFolderBase) |> ignore
                let result = {|
                        currentLocalBaseFolder = currentLocalBaseFolder
                        currentLocalDataFolder = currentLocalDataFolder
                        stateFileFolder = stateFileFolder
                        audioBooksStateDataFilePath = audioBooksStateDataFile
                        audioBooksStateDataConnectionString = StateConnectionString $"FileName={audioBooksStateDataFile};Upgrade=true;Collation=en-US/None"
                        audioBookAudioFileInfoDbFilePath = audioBookAudioFileDb
                        audioBookAudioFileInfoDbConnectionString = FileInfoConnectionString $"Filename={audioBookAudioFileDb};Upgrade=true;Collation=en-US/None"
                        audioBookDownloadFolderBase=audioBookDownloadFolderBase
                    |}
                folders <- Some result
                result
            | Some folders -> folders

    let baseUrl = "https://www.einsamedien.de/"



module Notifications =

    open System.Reflection
    open System.Threading.Tasks

    let private notificationService = lazy DependencyService.Get<INotificationService>()

    let private hasDialogHostLoadedInstances () =
        let fieldInfo:FieldInfo | null = typeof<DialogHost>.GetField("LoadedInstances", BindingFlags.Static ||| BindingFlags.NonPublic)
        match fieldInfo with
        | Null -> false
        | fieldInfo ->
            let value = fieldInfo.GetValue(null) :?> seq<DialogHost>
            if value = null then false else value |> Seq.length > 0

    let waitUntilDialogHostHasAnInstance (timeoutMs:int) =
        task {
            let token = CancellationTokenSource(timeoutMs).Token
            while hasDialogHostLoadedInstances() |> not && token.IsCancellationRequested |> not do
                do! Task.Delay 100
        }


    let showNotification title message =
        let ns = notificationService.Force()
        ns.ShowMessage title message


    let showToasterMessage message =
        Dispatcher.UIThread.Invoke<unit> (fun _ ->
            let control = Border(
                BorderBrush=Avalonia.Media.Brushes.DarkGray,
                BorderThickness=Thickness(1.0),
                CornerRadius=CornerRadius(5.0),
                Child=TextBlock(Margin=Thickness(5),Text=message)
            )
            InteractiveContainer.ShowToast (control, 3)
        )



    let showErrorMessage (msg:string) =
        Dispatcher.UIThread.InvokeAsync<unit> (fun _ ->
            task {
                do! waitUntilDialogHostHasAnInstance 2000
                let! _ = DialogHost.Show(MessageBoxViewModel("Achtung!", msg))
                ()
            }
        )

    let showMessage title (msg:string) =
        Dispatcher.UIThread.InvokeAsync<unit> (fun _ ->
            task {
                // Get static private enumerable "LoadedInstances" from class DialogHost, use Reflection
                do! waitUntilDialogHostHasAnInstance 2000
                let! _ = DialogHost.Show(MessageBoxViewModel(title, msg))
                ()
            }
        )


    let showQuestionDialog title message okButtonLabel cancelButtonLabel =
        Dispatcher.UIThread.InvokeAsync<bool> (fun _ ->
            task {
                do! waitUntilDialogHostHasAnInstance 2000
                let! res = DialogHost.Show(QuestionBoxViewModel(title, message, okButtonLabel, cancelButtonLabel))
                return res = "OK"
            }
        )



module SecureStorageHelper =

    open Microsoft.Maui.Storage


    let getSecuredValue key =
        task {
            try
                if OperatingSystem.IsAndroid() then
                    let! value =  SecureStorage.GetAsync(key)
                    return value |> Option.ofObj
                elif OperatingSystem.IsWindows() then
                    try
                        let! value =  File.ReadAllTextAsync($"store-{key}.txt")
                        return value |> Option.ofObj
                    with
                    | _ ->
                        return None
                else
                    return None
            with
            | ex ->
                Global.telemetryClient.TrackTrace "error reading value from secure storage"
                Global.telemetryClient.TrackException ex
                // clear storage, when error occurs
                let service = DependencyService.Get<ISecureStorageHelper>()
                if service <> Unchecked.defaultof<ISecureStorageHelper> then
                    Global.telemetryClient.TrackTrace "clearing secure storage preferences"
                    service.ClearSecureStoragePreferences()

                return None
        }

    let setSecuredValue value key =
        task {
            try
                if OperatingSystem.IsAndroid() then
                    do! SecureStorage.SetAsync(key,value)
                elif OperatingSystem.IsWindows() then
                    let _ = File.WriteAllTextAsync($"store-{key}.txt", value)
                    ()
            with
            | ex ->
                Global.telemetryClient.TrackTrace "error storing value in secure storage"
                Global.telemetryClient.TrackException ex
                Global.telemetryClient.TrackTrace "try to clean the storage preferences"
                // clear storage, when error occurs
                let service = DependencyService.Get<ISecureStorageHelper>()
                if service <> Unchecked.defaultof<ISecureStorageHelper> then
                    Global.telemetryClient.TrackTrace "clearing secure storage preferences"
                    service.ClearSecureStoragePreferences()
                    // and try to save again
                    try
                        Global.telemetryClient.TrackTrace "store again!"
                        if OperatingSystem.IsAndroid() then
                            do! SecureStorage.SetAsync(key,value)
                        return ()
                    with
                    | ex ->
                        Global.telemetryClient.TrackTrace "second atttemped failed!"
                        Global.telemetryClient.TrackException ex
                        return ()
        }




module SecureLoginStorage =
    open SecureStorageHelper

    let private secOldShopCookieKey = "perryRhodanAudioBookCookie"
    let private secOldShopStoreUsernameKey = "perryRhodanAudioBookUsername"
    let private secOldShopStorePasswordKey = "perryRhodanAudioBookPassword"
    let private secOldShopStoreRememberLoginKey = "perryRhodanAudioBookRememberLogin"

    let saveOldShopLoginCredentials username password rememberLogin =
        task {
            try
                do! secOldShopStoreUsernameKey |> setSecuredValue username
                do! secOldShopStorePasswordKey |> setSecuredValue password
                do! secOldShopStoreRememberLoginKey |> setSecuredValue (if rememberLogin then "Jupp" else "")
                return Ok true
            with
            | e -> return (Error e.Message)
        }

    let loadOldShopLoginCredentials () =
        task {
            try
                let! username =  secOldShopStoreUsernameKey |> getSecuredValue
                let! password =  secOldShopStorePasswordKey|> getSecuredValue
                let! rememberLoginStr = secOldShopStoreRememberLoginKey |> getSecuredValue
                return Ok (username,password,(rememberLoginStr = Some "Jupp"))
            with
            | e -> return (Error e.Message)
        }


    let saveOldShopCookie (cookies:Map<string,string>) =
        task {
            try
                let cookieJson = JsonConvert.SerializeObject(cookies)
                do! secOldShopCookieKey |> setSecuredValue cookieJson
                return Ok true
            with
            | e -> return (Error e.Message)
        }


    let loadOldShopCookie () =
        task {
            try
                let! cookie =
                    secOldShopCookieKey
                    |> getSecuredValue
                return cookie |> Option.map JsonConvert.DeserializeObject<Map<string,string>> |> Ok
            with
            | e -> return (Error e.Message)
        }
        
        
    let private secNewShopCookieKey             = "perryRhodanAudioBookNewShopCookie"
    let private secNewShopStoreUsernameKey      = "perryRhodanAudioBookNewShopUsername"
    let private secNewShopStorePasswordKey      = "perryRhodanAudioBookNewShopPassword"
    let private secNewShopStoreRememberLoginKey = "perryRhodanAudioBookNewShopRememberLogin"

    let saveNewShopLoginCredentials username password rememberLogin =
        task {
            try
                do! secNewShopStoreUsernameKey |> setSecuredValue username
                do! secNewShopStorePasswordKey |> setSecuredValue password
                do! secNewShopStoreRememberLoginKey |> setSecuredValue (if rememberLogin then "Jupp" else "")
                return Ok true
            with
            | e -> return (Error e.Message)
        }

    let loadNewShopLoginCredentials () =
        task {
            try
                let! username =  secNewShopStoreUsernameKey |> getSecuredValue
                let! password =  secNewShopStorePasswordKey|> getSecuredValue
                let! rememberLoginStr = secNewShopStoreRememberLoginKey |> getSecuredValue
                return Ok (username,password,(rememberLoginStr = Some "Jupp"))
            with
            | e -> return (Error e.Message)
        }


    let saveNewShopCookie (cookies:Map<string,string>) =
        task {
            try
                let cookieJson = JsonConvert.SerializeObject(cookies)
                do! secNewShopCookieKey |> setSecuredValue cookieJson
                return Ok true
            with
            | e -> return (Error e.Message)
        }


    let loadNewShopCookie () =
        task {
            try
                let! cookie =
                    secNewShopCookieKey
                    |> getSecuredValue
                return cookie |> Option.map JsonConvert.DeserializeObject<Map<string,string>> |> Ok
            with
            | e -> return (Error e.Message)
        }


module Files =

    open FsToolkit.ErrorHandling

    let getMp3FileList folder =
        async {
            let! files =
                async {
                    try
                        return Directory.EnumerateFiles(folder, "*.mp3")
                    with
                    | _ -> return Seq.empty
                }

            let! res =
                async {
                    return
                        files
                        |> Seq.toList
                        |> List.map (
                            fun i ->
                                use tfile = TagLib.File.Create(i)
                                { FileName = i; Duration = tfile.Properties.Duration }
                        )
                }

            return res
        }


module SystemSettings =
    open SecureStorageHelper
    open Common.StringHelpers
    open FsToolkit.ErrorHandling

    let private defaultRewindWhenStartAfterShortPeriodInSec = 5
    let private defaultRewindWhenStartAfterLongPeriodInSec = 30
    let private defaultLongPeriodBeginsAfterInMinutes = 60
    let private defaultAudioJumpDistance = 30000

    let private keyRewindWhenStartAfterShortPeriodInSec = "PerryRhodanAudioBookRewindWhenStartAfterShortPeriodInSec"
    let private keykeyRewindWhenStartAfterLongPeriodInSec ="PerryRhodanAudioBookRewindWhenStartAfterLongPeriodInSec"
    let private keyLongPeriodBeginsAfterInMinutes ="PerryRhodanAudioBookLongPeriodBeginsAfterInMinutes"
    let private keyAudioJumpDistance = "PerryRhodanAudioBookAudioJumpDistance"
    let private keyDeveloperMode= "PerryRhodanAudioBookDeveloperModee"
    let private keyIsFirstStart= "IsFirstStart"
    let private keyPlaybackSpeed= "PlaybackSpeed"

    let getRewindWhenStartAfterShortPeriodInSec () =
        keyRewindWhenStartAfterShortPeriodInSec
        |> getSecuredValue
        |> Async.AwaitTask
        |> Async.map (fun result ->
            result |> optToInt defaultRewindWhenStartAfterShortPeriodInSec
        )



    let getRewindWhenStartAfterLongPeriodInSec () =
        keykeyRewindWhenStartAfterLongPeriodInSec
        |> getSecuredValue
        |> Async.AwaitTask
        |> Async.map (fun result ->
            result |> optToInt defaultRewindWhenStartAfterLongPeriodInSec
        )


    let getLongPeriodBeginsAfterInMinutes () =
        keyLongPeriodBeginsAfterInMinutes
        |> getSecuredValue
        |> Async.AwaitTask
        |> Async.map (fun result ->
            result |> optToInt defaultLongPeriodBeginsAfterInMinutes
        )


    let getJumpDistance () =
        keyAudioJumpDistance
        |> getSecuredValue
        |> Async.AwaitTask
        |> Async.map (fun result ->
            let distance = result |> optToInt defaultAudioJumpDistance
            if distance < 1000 then 1000 else distance
        )

    let getDeveloperMode () =
        keyDeveloperMode
        |> getSecuredValue
        |> Async.AwaitTask
        |> Async.map (fun result ->
            result |> Option.map(fun v -> v = "true") |> Option.defaultValue false
        )

    let getIsFirstStart () =
        keyIsFirstStart
        |> getSecuredValue
        |> Async.AwaitTask
        |> Async.map (fun result ->
            result |> Option.map(fun v -> v = "true") |> Option.defaultValue true
        )

    let getPlaybackSpeed () =
        keyPlaybackSpeed
        |> getSecuredValue
        |> Async.AwaitTask
        |> Async.map (fun result ->
            result |> Option.map(fun v -> Decimal.Parse(v)) |> Option.defaultValue 1.0m
        )

    let setRewindWhenStartAfterShortPeriodInSec (value:int) =
        keyRewindWhenStartAfterShortPeriodInSec |> setSecuredValue (value.ToString())


    let setRewindWhenStartAfterLongPeriodInSec (value:int) =
        keykeyRewindWhenStartAfterLongPeriodInSec |> setSecuredValue (value.ToString())


    let setLongPeriodBeginsAfterInMinutes (value:int) =
        keyLongPeriodBeginsAfterInMinutes |> setSecuredValue (value.ToString())


    let setJumpDistance (value:int) =
        keyAudioJumpDistance |> setSecuredValue (value.ToString())

    let setDeveloperMode(value:bool) =
        keyDeveloperMode |> setSecuredValue (if value then "true" else "false")

    let setIsFirstStart(value:bool) =
        keyIsFirstStart |> setSecuredValue (if value then "true" else "false")


    let setPlaybackSpeed(value:decimal) =
        keyPlaybackSpeed |> setSecuredValue (value.ToString())








module Helpers =

    type InputPaneService() =
        // static member for IInputPane
        static member val InputPane:IInputPane = null with get, set



