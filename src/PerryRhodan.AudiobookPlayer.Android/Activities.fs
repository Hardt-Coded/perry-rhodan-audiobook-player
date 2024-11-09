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
open Microsoft.AppCenter.Analytics
open Microsoft.AppCenter
open Microsoft.AppCenter.Crashes.Android
open Microsoft.ApplicationInsights.Extensibility
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




    override _.CustomizeAppBuilder(builder) =

        AppDomain.CurrentDomain.UnhandledException.Subscribe(fun args ->
            let ex = args.ExceptionObject :?> Exception
            Microsoft.AppCenter.Crashes.Crashes.TrackError(ex)
            Global.telemetryClient.TrackException ex
        ) |> ignore
        
        
        let bitmapConverter = {
            new IBitmapConverter with
                member this.GetBitmap path =
                    let bitmap = BitmapFactory.DecodeFile(path)
                    bitmap :> obj
        }
        
        let packageInfo = self.PackageManager.GetPackageInfo(self.PackageName, PackageInfoFlags.MetaData)
        
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
        
        // register services
        DependencyService.ServiceCollection
            .AddSingleton<IScreenService, ScreenService>()
            .AddSingleton<IAndroidHttpMessageHandlerService, AndroidHttpClientHandlerService>()
            .AddSingleton<ICloseApplication, CloseApplication>()
            .AddSingleton<IAndroidDownloadFolder, AndroidDownloadFolder>()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<IDownloadService, DownloadServiceImplementation.DependencyService.DownloadService>()
            .AddSingleton<IPictureDownloadService, PictureDownloadServiceImplementation.DependencyService.PictureAndroidDownloadService>()
            .AddSingleton<INotificationService, NotificationService.NotificationService>()
            .AddTransient<ILoginViewModel, LoginViewModel>()
            .AddSingleton<IActionMenuService, ActionMenuService>()
            .AddSingleton<IBitmapConverter>(bitmapConverter)
            .AddSingleton<IPackageInformation>(packageInformation)
            |> ignore
        
        // convert function to C# Func
        
        // let androidOptions = AndroidPlatformOptions()
        // // Todo: do not forget Fallback
        // let renderModes = [ AndroidRenderingMode.Vulkan ] |> System.Collections.Generic.List
        // androidOptions.RenderingMode <- renderModes.AsReadOnly()
        //
        // let vulkanOptions = VulkanOptions()
        // vulkanOptions.VulkanInstanceCreationOptions <- VulkanInstanceCreationOptions()

        base.CustomizeAppBuilder(builder)
            // .With(androidOptions)
            // .With(vulkanOptions)
            .UseAndroid()
            .WithInterFont()

            .UseReactiveUI()


    override this.OnResume() =
        base.OnResume()


    override this.OnCreate(savedInstanceState) =
        
        AppDomain.CurrentDomain.UnhandledException.Subscribe(fun args ->
            let ex = args.ExceptionObject :?> Exception
            Microsoft.AppCenter.Crashes.Crashes.TrackError(ex)
            Global.telemetryClient.TrackException ex
        ) |> ignore
        
        base.OnCreate savedInstanceState
        
        
        
        AppCenter.Start(Global.appcenterAndroidId, typeof<Analytics>, typeof<Crashes>)
        Microsoft.AppCenter.Analytics.Analytics.TrackEvent("App started")
        Global.telemetryClient.TrackEvent ("ApplicationStarted")
        
        (*// set complete to build service provider here, to avoid that the dependencies,
        // which are registered in app.xaml.fs are also included in the service provider
        DependencyService.SetComplete()*)
        CrossMediaManager.Current.Init(this)
        let audioService = AudioPlayerService()
        DependencyService.ServiceCollection
            .AddSingleton<IAudioPlayer>(audioService)
            .AddSingleton<IAudioPlayerPause>(audioService)
        |> ignore
        
        DependencyService.SetComplete()
        
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











