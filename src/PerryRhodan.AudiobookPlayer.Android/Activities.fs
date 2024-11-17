namespace PerryRhodan.AudiobookPlayer.Android



open System
open Android.App
open Android.Content
open Android.Content.PM
open Android.OS
open AndroidX.AppCompat.App
open Avalonia
open Avalonia.ReactiveUI
open Avalonia.Android
open Avalonia.Vulkan
open Dependencies
open MediaManager
open Microsoft.ApplicationInsights.Extensibility
open Microsoft.Maui.ApplicationModel
open PerryRhodan.AudiobookPlayer
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModels
open Services
open Services.DependencyServices
open Xamarin.Android.Net
open PerryRhodan.AudiobookPlayer.Controls
open Android.Graphics
open FsToolkit.ErrorHandling




type AndroidDownloadFolder() =
    interface IAndroidDownloadFolder with
        member this.GetAndroidDownloadFolder () =
            Android.OS.Environment.ExternalStorageDirectory.Path

module HttpStuff =
    //let androidHttpHandler = Xamarin.Android.Net.AndroidClientHandler()
    let androidHttpHandler = new AndroidMessageHandler()

type AndroidHttpClientHandlerService() =
    interface IAndroidHttpMessageHandlerService with
        member this.GetHttpMesageHandler () =
            HttpStuff.androidHttpHandler

        member this.GetCookieContainer () =
            HttpStuff.androidHttpHandler.CookieContainer

        member this.SetAutoRedirect redirect =
            HttpStuff.androidHttpHandler.AllowAutoRedirect <- redirect


type CloseApplication() =
    interface ICloseApplication with
        member this.CloseApplication () =
            Android.OS.Process.KillProcess(Android.OS.Process.MyPid())



type ServiceConnectedEventArgs(binder: IBinder) =
    inherit System.EventArgs()
    member val Binder = binder with get, set

type ServiceConnection() =
    inherit Java.Lang.Object()

    let serviceConnected = Event<EventHandler<ServiceConnectedEventArgs>, ServiceConnectedEventArgs>()
    member val ServiceConnected = serviceConnected.Publish

    interface IServiceConnection with


        member this.OnServiceConnected(name: ComponentName, service: IBinder) =
            serviceConnected.Trigger(this, ServiceConnectedEventArgs(service))

        member this.OnServiceDisconnected(name: ComponentName) = ()






[<Activity(
    Label = "Eins A Medien Audiobook Player",
    Theme = "@style/MyTheme.NoActionBar",
    //Theme = "@style/Theme.AppCompat.NoActionBar",
    Icon = "@drawable/eins_a_launcher",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = (ConfigChanges.Orientation ||| ConfigChanges.ScreenSize ||| ConfigChanges.UiMode),
    ScreenOrientation = ScreenOrientation.Portrait)>]
type MainActivity() as self =
    inherit AvaloniaMainActivity<App>()


    let copyDemoDatabaseToDbFolderWhenDev (packageInfo:PackageInfo) =
        let pathes = Services.Consts.createCurrentFolders ()
        let dbExists = System.IO.File.Exists(pathes.audioBooksStateDataFilePath)
        if packageInfo.PackageName.EndsWith(".dev") && not dbExists then
            let resourceName = "PerryRhodan.AudiobookPlayer.dev.audiobooks.db"
            use dbFileStream = typeof<App>.Assembly.GetManifestResourceStream(resourceName)
            use fileStream = System.IO.File.Create(pathes.audioBooksStateDataFilePath)
            dbFileStream.CopyTo(fileStream)
            ()
        ()


    do
        AppDomain.CurrentDomain.UnhandledException.Subscribe(fun args ->
            let ex = args.ExceptionObject :?> Exception
            Global.telemetryClient.TrackException ex
        ) |> ignore



    override this.CustomizeAppBuilder(builder) =

        let bitmapConverter = {
            new IBitmapConverter with
                member this.GetBitmap path =
                    let bitmap = BitmapFactory.DecodeFile(path)
                    bitmap :> obj
        }

        let packageInfo = self.PackageManager.GetPackageInfo(self.PackageName, PackageInfoFlags.MetaData)

        let secureStorageHelper =
            { new ISecureStorageHelper with
                member _.ClearSecureStoragePreferences () =
                    let packageName = AppInfo.Current.PackageName;
                    let alias = $"{packageName}.microsoft.maui.essentials.preferences"
                    let preferences = this.ApplicationContext.GetSharedPreferences(alias, FileCreationMode.Private)
                    // Igitt! This is a workaround for a bug in the Xamarin.Essentials SecureStorage
                    if preferences <> null then
                        let pref = preferences.Edit()
                        if pref <> null then
                            let pref = pref.Clear()
                            if pref <> null then
                                pref.Apply()
            }

        copyDemoDatabaseToDbFolderWhenDev packageInfo

        let packageInformation = {
            new IPackageInformation with
                member this.Name() =
                    packageInfo.PackageName
                member this.GetVersion() =
                    packageInfo.VersionName
                member this.GetBuild() =
                    packageInfo.VersionCode.ToString()
        }

        CrossMediaManager.Current.Init(this)

        AppCompositionRoot.registerServices (fun serviceCollection ->
            serviceCollection
                .AddSingleton<IScreenService, ScreenService>()
                .AddSingleton<IAndroidHttpMessageHandlerService, AndroidHttpClientHandlerService>()
                .AddSingleton<ICloseApplication, CloseApplication>()
                .AddSingleton<IAndroidDownloadFolder, AndroidDownloadFolder>()
                .AddSingleton<IDownloadService, DownloadServiceImplementation.DependencyService.DownloadService>()
                .AddSingleton<IPictureDownloadService, PictureDownloadServiceImplementation.DependencyService.PictureAndroidDownloadService>()
                .AddSingleton<INotificationService, NotificationService.NotificationService>()
                .AddSingleton<IBitmapConverter>(bitmapConverter)
                .AddSingleton<IPackageInformation>(packageInformation)
                .AddSingleton<ISecureStorageHelper>(secureStorageHelper)
                |> ignore
        )


        // convert function to C# Func

        let androidOptions = AndroidPlatformOptions()
        let renderModes = [ AndroidRenderingMode.Vulkan; AndroidRenderingMode.Egl ] |> System.Collections.Generic.List
        androidOptions.RenderingMode <- renderModes.AsReadOnly()


        base.CustomizeAppBuilder(builder)
            .UseAndroid()
            .WithInterFont()
            .With(androidOptions)
            .UseReactiveUI()


    override this.OnResume() =
        base.OnResume()


    override this.OnCreate(savedInstanceState) =
        base.OnCreate savedInstanceState

        Global.telemetryClient.TrackEvent ("ApplicationStarted")

        Microsoft.Maui.ApplicationModel.Platform.Init(this, savedInstanceState)

        AppCompatDelegate.DefaultNightMode <- AppCompatDelegate.ModeNightYes




    override _.OnDestroy() =
        base.OnDestroy()
        Global.telemetryClient.Flush()

    override this.OnStop() =
        base.OnStop()



    override this.OnBackPressed() =
        match DependencyService.Get<INavigationService>().BackbuttonPressedAction with
        | Some action ->
            action()
        | None ->
            Notifications.showQuestionDialog "App beenden?" "Möchten Sie die App wirklich beenden?" "Ja" "Nein"
            |> Async.AwaitTask
            |> Async.map (fun result ->
                if result then
                    //DependencyService.Get<IAudioPlayerServiceController>().StopService()
                    CrossMediaManager.Current.Dispose()
                    this.FinishAffinity()

            )
            |> Async.StartImmediate











