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
    Label = "PerryRhodan.AudiobookPlayer.Android",
    Theme = "@style/MyTheme.NoActionBar",
    //Theme = "@style/Theme.AppCompat.NoActionBar",
    Icon = "@drawable/eins_a_launcher",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = (ConfigChanges.Orientation ||| ConfigChanges.ScreenSize ||| ConfigChanges.UiMode))>]
type MainActivity() as self =
    inherit AvaloniaMainActivity<App>()

    let mutable serviceConnection: ServiceConnection option = None
    let mutable isBound = false 
    
    
    let bindToService() =
        let serviceConnection = 
            match serviceConnection with
            | Some sc ->
                sc
            | None ->
                let sc = new ServiceConnection()
                serviceConnection <- Some sc
                sc.ServiceConnected.Subscribe(fun args ->
                    match args.Binder with
                    | :? AudioPlayerBinder as binder ->
                        // make dependency injection aware of this instance of the service
                        let service = binder.GetService()
                        DependencyService.ServiceCollection.AddSingleton<IAudioPlayer>(service) |> ignore
                        DependencyService.ServiceCollection.AddSingleton<IAudioPlayerPause>(service) |> ignore
                        DependencyService.ServiceCollection.AddSingleton<IMediaPlayer>(service) |> ignore
                        DependencyService.SetComplete()
                        
                        isBound <- true
                    | _ -> ()
                ) |> ignore
                sc
            
            
        let intent = new Intent(self, typeof<AudioPlayerService>)
        self.BindService(intent, serviceConnection, Bind.AutoCreate) |> ignore

        
    
            

    override _.CustomizeAppBuilder(builder) =
        
        // register services
        DependencyService.ServiceCollection
            .AddSingleton<IScreenService, ScreenService>()
            .AddSingleton<IAndroidHttpMessageHandlerService, AndroidHttpClientHandlerService>()
            .AddSingleton<ICloseApplication, CloseApplication>()
            .AddSingleton<IAndroidDownloadFolder, AndroidDownloadFolder>()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<IDownloadService, DownloadServiceImplementation.DependencyService.DownloadService>()
            .AddSingleton<INotificationService, NotificationService.NotificationService>()
            .AddSingleton<IAudioPlayerServiceController, AudioPlayerServiceController>()
            |> ignore
        
        base.CustomizeAppBuilder(builder)
            .UseAndroid()
            .WithInterFont()
            //.UseSkia()
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
        
        
        Microsoft.Maui.ApplicationModel.Platform.Init(this, savedInstanceState)
        
        // start audioplayer foreground service 
        let intent = new Intent(Application.Context, typeof<AudioPlayerService>)
        Application.Context.StartService(intent) |> ignore
        bindToService()
        AppCompatDelegate.DefaultNightMode <- AppCompatDelegate.ModeNightYes

    override _.OnDestroy() =
        base.OnDestroy()
        match serviceConnection with
        | Some sc when isBound ->
            self.UnbindService sc
            isBound <- false
            serviceConnection <- None
        | _ -> ()
    
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
                    DependencyService.Get<IAudioPlayerServiceController>().StopService()
                    this.FinishAffinity()
                    
            )
            |> Async.StartImmediate
       
       

            
        
        
        
    
        
            
           
