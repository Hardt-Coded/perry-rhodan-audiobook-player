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
open PerryRhodan.AudiobookPlayer
open Microsoft.Extensions.DependencyInjection
//open PerryRhodan.AudiobookPlayer.Android.AudioPlayerServiceImplementation.DecpencyService
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModels
open Services
open Services.DependencyServices
open Xamarin.Android.Net
open PerryRhodan.AudiobookPlayer.Controls
open Android.Graphics
open Avalonia.Xaml.Interactivity




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


    override _.CustomizeAppBuilder(builder) =


        let bitmapConverter = {
            new IBitmapConverter with
                member this.GetBitmap path =
                    let bitmap = BitmapFactory.DecodeFile(path)
                    bitmap :> obj
        }
        
        let packageInfo = self.PackageManager.GetPackageInfo(self.PackageName, PackageInfoFlags.MetaData)
        let packageInformation = {
            new IPackageInformation with
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
            .AddSingleton<INotificationService, NotificationService.NotificationService>()
            //.AddSingleton<IAudioPlayerServiceController, AudioPlayerServiceController>()
            .AddTransient<ILoginViewModel, LoginViewModel>()
            .AddSingleton<IActionMenuService, ActionMenuService>()
            .AddSingleton<GlobalSettingsService>(GlobalSettingsService())
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
        base.OnCreate savedInstanceState
        AppDomain.CurrentDomain.UnhandledException.Subscribe(fun args ->
            let ex = args.ExceptionObject :?> Exception
            Microsoft.AppCenter.Crashes.Crashes.TrackError(ex)
        ) |> ignore
        AppCenter.Start(Global.appcenterAndroidId, typeof<Analytics>, typeof<Crashes>)
        Microsoft.AppCenter.Analytics.Analytics.TrackEvent("App started")

        (*// set complete to build service provider here, to avoid that the dependencies,
        // which are registered in app.xaml.fs are also included in the service provider
        DependencyService.SetComplete()*)
        CrossMediaManager.Current.Init(this)
        let audioService = AudioPlayerService2()
        DependencyService.ServiceCollection
            .AddSingleton<IAudioPlayer>(audioService)
        |> ignore

        DependencyService.SetComplete()

        Microsoft.Maui.ApplicationModel.Platform.Init(this, savedInstanceState)

        AppCompatDelegate.DefaultNightMode <- AppCompatDelegate.ModeNightYes

        AppDomain.CurrentDomain.UnhandledException.Subscribe(fun args ->
            let ex = args.ExceptionObject :?> Exception
            Microsoft.AppCenter.Crashes.Crashes.TrackError(ex)
        ) |> ignore


    override _.OnDestroy() =
        base.OnDestroy()

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











