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





type MyShellBottomNavViewAppearanceTracker(context,item) =
    inherit ShellBottomNavViewAppearanceTracker(context,item)

    let makeColorStateList () =
        let states = [|
            [| -Android.Resource.Attribute.StateEnabled |]
            [| Android.Resource.Attribute.StateChecked |]
            [| |]
        |]
    
        let disabledInt =         
            Android.Graphics.Color.Gray.ToArgb()
    
        let checkedInt = 
            Common.Consts.primaryTextColor.ToAndroid().ToArgb()
    
        let defaultColor =
            Common.Consts.secondaryTextColor.ToAndroid().ToArgb()
            

        let colors = [| disabledInt; checkedInt; defaultColor |]
    
        new Res.ColorStateList(states, colors);

    override this.SetAppearance(bottomView, appearance) =
        base.SetAppearance(bottomView,appearance)
        use colorStateList = makeColorStateList()
        bottomView.ItemTextColor <- colorStateList
        bottomView.ItemIconTintList <- colorStateList
        

//type CustomShellItemRenderer(context) =
//    inherit ShellItemRenderer(context)

//    let states =
//        [|
//            [| Android.Resource.Attribute.StateActive |]
//            [| Android.Resource.Attribute.StatePressed |]
//            [| -Android.Resource.Attribute.StateActive |]
//            [| -Android.Resource.Attribute.StatePressed |]
//        |]
    
    
    
//    let colors:int array =
//        [|
//            Android.Resource.Color.White
//            Android.Resource.Color.HoloBlueLight
//            Android.Resource.Color.DarkerGray
//            Android.Resource.Color.White
//        |] 

//    let mutable ol:View = null

//    member this.OL = ol

//    override this.OnCreateView(inflater, container, savedInstanceState) =
//        let outerLayout = base.OnCreateView(inflater, container, savedInstanceState);  
//        ol <- outerLayout
//        let bottomView = outerLayout.FindViewById<BottomNavigationView>(Xamarin.Forms.Platform.Android.Resource.Id.bottomtab_tabbar )
//        //let navigationArea = outerLayout.FindViewById<FrameLayout>(Xamarin.Forms.Platform.Android.Resource.Id.bottomtab_navarea)
//        bottomView.SetBackgroundColor(Android.Graphics.Color.Rgb(0x21,0x21,0x21))
//        let colorList = new Res.ColorStateList(states,colors)
//        bottomView.ItemTextColor <- colorList
//        bottomView.ItemIconTintList <- colorList
//        outerLayout

type CustomShellRenderer (context) =
    inherit ShellRenderer(context) 

    //override this.CreateShellItemRenderer(item) =
    //    let ctx = this :> IShellContext
    //    let renderer = new CustomShellItemRenderer(ctx)  
    //    renderer :> IShellItemRenderer

    override this.CreateBottomNavViewAppearanceTracker(item) =
        let sctx = this :> IShellContext
        new MyShellBottomNavViewAppearanceTracker(sctx,item) :> IShellBottomNavViewAppearanceTracker
    



type AndroidDownloadFolder() =
    interface DependencyServices.IAndroidDownloadFolder with
        member this.GetAndroidDownloadFolder () =
            let path = Android.OS.Environment.ExternalStorageDirectory.Path
            path




[<Activity (Label = "Eins A Medien Audiobook Player", Icon = "@mipmap/eins_a_launcher", Theme = "@style/MainTheme.Launcher", MainLauncher = true, ConfigurationChanges = (ConfigChanges.ScreenSize ||| ConfigChanges.Orientation),ScreenOrientation = ScreenOrientation.Portrait)>]
type MainActivity() =
    inherit FormsAppCompatActivity()
    override this.OnCreate (bundle: Bundle) =
        base.SetTheme(PerryRhodan.AudiobookPlayer.Android.Resources.Style.MainTheme)
        FormsAppCompatActivity.TabLayoutResource <- Resources.Layout.Tabbar
        FormsAppCompatActivity.ToolbarResource <- Resources.Layout.Toolbar
        base.OnCreate (bundle)

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



