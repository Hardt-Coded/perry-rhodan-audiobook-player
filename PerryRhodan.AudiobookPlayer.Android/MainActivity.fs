// Copyright 2018 Fabulous contributors. See LICENSE.md for license.

namespace PerryRhodan.AudiobookPlayer.Android

open System

open Android.App
open Android.Content
open Android.Content.PM
open Android.Runtime
open Android.Views
open Android.Widget
open Android.Media
open Android.OS
open Xamarin.Forms.Platform.Android
open Services
open Microsoft.AppCenter
open Microsoft.AppCenter.Crashes
open Microsoft.AppCenter.Analytics
open Android.Support.Design.Widget
open Xamarin.Forms.Platform.Android
open Android.Arch.Lifecycle



    



type AndroidDownloadFolder() =
    interface DependencyServices.IAndroidDownloadFolder with
        member this.GetAndroidDownloadFolder () =
            let path = Android.OS.Environment.ExternalStorageDirectory.Path
            path




[<Activity (Label = "Eins A Medien Audiobook Player", Icon = "@mipmap/eins_a_launcher", Theme = "@style/MainTheme.Launcher", MainLauncher = true,LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = (ConfigChanges.ScreenSize ||| ConfigChanges.Orientation),ScreenOrientation = ScreenOrientation.Portrait)>]
type MainActivity() =
    inherit FormsAppCompatActivity()

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
                    
        Plugin.CurrentActivity.CrossCurrentActivity.Current.Init(this, bundle);
        let appcore  = new PerryRhodan.AudiobookPlayer.MainApp()
        this.LoadApplication (appcore)
    
    
    override this.OnRequestPermissionsResult(requestCode: int, permissions: string[], [<GeneratedEnum>] grantResults: Android.Content.PM.Permission[]) =
        Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults)
        Plugin.Permissions.PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults)

        base.OnRequestPermissionsResult(requestCode, permissions, grantResults)

 
 

 module LinkerStuff =
 // Linker build errors
 // force to use
     open Android.Support.V7.Widget

     let ignoreFitWindowStuff = new FitWindowsFrameLayout(Application.Context)
     let ignoreFitOther = new ContentFrameLayout(Application.Context)



