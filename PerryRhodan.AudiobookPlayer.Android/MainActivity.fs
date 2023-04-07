namespace PerryRhodan.AudiobookPlayer.Android

open System

open Android.App
open Android.Content
open Android.Content.PM
open Android.Runtime
open Android.OS
open Xamarin.Forms.Platform.Android
open Services
open Microsoft.AppCenter
open Microsoft.AppCenter.Crashes
open Microsoft.AppCenter.Analytics
open Acr.UserDialogs
open AndroidX.Core.Content
open AndroidX.Core.App





type AndroidDownloadFolder() =
    interface DependencyServices.IAndroidDownloadFolder with
        member this.GetAndroidDownloadFolder () =
            Android.OS.Environment.ExternalStorageDirectory.Path

module HttpStuff =
    let androidHttpHandler = Xamarin.Android.Net.AndroidClientHandler()

type AndroidHttpClientHandlerService() =
    interface DependencyServices.IAndroidHttpMessageHandlerService with
        member this.GetHttpMesageHandler () =
            HttpStuff.androidHttpHandler

        member this.GetCookieContainer () =
            HttpStuff.androidHttpHandler.CookieContainer

        member this.SetAutoRedirect redirect =
            HttpStuff.androidHttpHandler.AllowAutoRedirect <- redirect


type CloseApplication() =
    interface DependencyServices.ICloseApplication with
        member this.CloseApplication () =
            Android.OS.Process.KillProcess(Android.OS.Process.MyPid())


[<Activity (Label = "Eins A Medien Audiobook Player", Icon = "@drawable/eins_a_launcher", Theme = "@style/MainTheme.Launcher", MainLauncher = true,LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = (ConfigChanges.ScreenSize ||| ConfigChanges.Orientation),ScreenOrientation = ScreenOrientation.Portrait)>]
type MainActivity() =
    inherit FormsAppCompatActivity()

    override this.OnDestroy () =
        base.OnDestroy()
        // remove all push pages if main app is destroyed
        //BrowserPage.PushModalHelper.clearPushPages ()
        ()


    override this.OnCreate (bundle: Bundle) =
        base.SetTheme(PerryRhodan.AudiobookPlayer.Android.Resources.Style.MainTheme)
        FormsAppCompatActivity.TabLayoutResource <- Resources.Layout.Tabbar
        FormsAppCompatActivity.ToolbarResource <- Resources.Layout.Toolbar
        base.OnCreate (bundle)


        GlobalType.typeOfMainactivity <- typeof<MainActivity>


        AppCenter.Start(Global.appcenterAndroidId, typeof<Analytics>, typeof<Crashes>)

        Xamarin.Essentials.Platform.Init(this, bundle)

        Xamarin.Forms.Forms.Init (this, bundle)
        Xamarin.Forms.DependencyService.Register<AndroidDownloadFolder>()
        Xamarin.Forms.DependencyService.Register<AudioPlayerServiceImplementation.DecpencyService.AudioPlayer>()
        Xamarin.Forms.DependencyService.Register<NotificationService.NotificationService>()
        Xamarin.Forms.DependencyService.Register<DownloadServiceImplementation.DependencyService.DownloadService>()
        Xamarin.Forms.DependencyService.Register<AndroidHttpClientHandlerService>()
        Xamarin.Forms.DependencyService.Register<CloseApplication>()





        Plugin.CurrentActivity.CrossCurrentActivity.Current.Init(this, bundle);

        UserDialogs.Init this

        // styleId as resource id for store testing
        Xamarin.Forms.Forms.ViewInitialized.Add(fun e ->
            if not (System.String.IsNullOrWhiteSpace(e.View.StyleId)) then
                e.NativeView.ContentDescription <- e.View.StyleId
        )

        let appcore  = new PerryRhodan.AudiobookPlayer.MainApp()
        this.LoadApplication (appcore)


    override this.OnRequestPermissionsResult(requestCode: int, permissions: string[], [<GeneratedEnum>] grantResults: Android.Content.PM.Permission[]) =
        Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults)
        Plugin.Permissions.PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults)

        base.OnRequestPermissionsResult(requestCode, permissions, grantResults)





