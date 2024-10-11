namespace PerryRhodan.AudiobookPlayer.Android

open Android.App
open Android.Content.PM
open AndroidX.AppCompat.App
open Avalonia
open Avalonia.ReactiveUI
open Avalonia.Android
open Dependencies
open Microsoft.AppCenter.Analytics
open Microsoft.AppCenter
open Microsoft.AppCenter.Crashes.Android
open PerryRhodan.AudiobookPlayer
open Microsoft.Extensions.DependencyInjection
//open PerryRhodan.AudiobookPlayer.Android.AudioPlayerServiceImplementation.DecpencyService
open PerryRhodan.AudiobookPlayer.Services.Interfaces
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




[<Activity(
    Label = "PerryRhodan.AudiobookPlayer.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/eins_a_launcher",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = (ConfigChanges.Orientation ||| ConfigChanges.ScreenSize ||| ConfigChanges.UiMode))>]
type MainActivity() =
    inherit AvaloniaMainActivity<App>()
    

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
            .AddSingleton<IAudioPlayer, AudioPlayerService>()
            //.AddSingleton<IAudioPlayer,AudioPlayer>()
            |> ignore
        
        base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI()
            
            
    override this.OnCreate(savedInstanceState) =
        base.OnCreate savedInstanceState
        
        AppCenter.Start(Global.appcenterAndroidId, typeof<Analytics>, typeof<Crashes>)
        Microsoft.AppCenter.Analytics.Analytics.TrackEvent("App started")
                
        (*// set complete to build service provider here, to avoid that the dependencies,
        // which are registered in app.xaml.fs are also included in the service provider
        DependencyService.SetComplete()*)
        
        
        Microsoft.Maui.ApplicationModel.Platform.Init(this, savedInstanceState)
        DependencyService.Get<IAudioPlayer>().StartService() |> Async.AwaitTask |> ignore
        AppCompatDelegate.DefaultNightMode <- AppCompatDelegate.ModeNightYes

    override this.OnStop() =
        base.OnStop()
        DependencyService.Get<IAudioPlayer>().StopService() |> Async.AwaitTask |> ignore
    
    
    override this.OnBackPressed() =
        match DependencyService.Get<INavigationService>().BackbuttonPressedAction with
        | Some action ->
            action()
        | None ->
            Notifications.showQuestionDialog "App beenden?" "Möchten Sie die App wirklich beenden?" "Ja" "Nein"
            |> Async.AwaitTask
            |> Async.map (fun result ->
                if result then
                    this.FinishAffinity()
            )
            |> Async.StartImmediate
                   
            
            
            
        
        
        
    
        
            
           
