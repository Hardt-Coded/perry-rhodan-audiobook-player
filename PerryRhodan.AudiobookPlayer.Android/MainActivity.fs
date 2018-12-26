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


type AndroidDownloadFolder() =
    interface DependencyServices.IAndroidDownloadFolder with
        member this.GetAndroidDownloadFolder () =
            let path = Android.OS.Environment.GetExternalStoragePublicDirectory (Android.OS.Environment.DirectoryDownloads)
            path.AbsolutePath



type AudioPlayer() =
    
    let mutable lastPositionBeforeStop = None
    
    let mutable onCompletion = None

    let mutable onAfterPrepare = None

    let mutable onInfo = None
    
    let mediaPlayer = 
        let m = new MediaPlayer()
        m.SetWakeMode(Application.Context, WakeLockFlags.Partial);
        
        m.Completion.Add(
            fun _ -> 
                match onCompletion with
                | None -> ()
                | Some cmd -> cmd()
        )

        m.Prepared.Add(
            fun _ ->                 
                match onAfterPrepare with
                | None -> ()
                | Some cmd -> cmd()
                m.Start()
                lastPositionBeforeStop <- None
        )

                
        m

    interface DependencyServices.IAudioPlayer with
        
        member this.LastPositionBeforeStop with get () = lastPositionBeforeStop

        member this.OnCompletion 
            with get () = onCompletion
            and set p = onCompletion <- p

        member this.OnInfo 
            with get () = onInfo
            and set p = onInfo <- p

        member this.PlayFile file position =
            async {
                mediaPlayer.Reset()
                do! mediaPlayer.SetDataSourceAsync(file) |> Async.AwaitTask
                onAfterPrepare <- Some (fun () -> mediaPlayer.SeekTo(position))
                mediaPlayer.PrepareAsync()
                return ()
            }

        
        member this.Stop () =
            if (mediaPlayer.IsPlaying) then
                mediaPlayer.Pause()
                lastPositionBeforeStop <- Some mediaPlayer.CurrentPosition
                
            else
                lastPositionBeforeStop <- Some mediaPlayer.CurrentPosition

            mediaPlayer.Stop()
            ()

        member this.GotToPosition ms =
            mediaPlayer.SeekTo(ms)

        member this.GetInfo () =
            async {
                do! Common.asyncFunc(
                        fun () ->
                            match onInfo,mediaPlayer.IsPlaying with
                            | Some cmd, true -> cmd(mediaPlayer.CurrentPosition,mediaPlayer.Duration)
                            | _ -> ()
                )
            }



[<Activity (Label = "PerryRhodan.AudiobookPlayer.Android", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = (ConfigChanges.ScreenSize ||| ConfigChanges.Orientation),ScreenOrientation = ScreenOrientation.Portrait)>]
type MainActivity() =
    inherit FormsAppCompatActivity()
    override this.OnCreate (bundle: Bundle) =
        FormsAppCompatActivity.TabLayoutResource <- Resources.Layout.Tabbar
        FormsAppCompatActivity.ToolbarResource <- Resources.Layout.Toolbar
        base.OnCreate (bundle)

        Xamarin.Essentials.Platform.Init(this, bundle)

        Xamarin.Forms.Forms.Init (this, bundle)
        Xamarin.Forms.DependencyService.Register<AndroidDownloadFolder>()
        Xamarin.Forms.DependencyService.Register<AudioPlayer>()
        
        Plugin.CurrentActivity.CrossCurrentActivity.Current.Init(this, bundle);

        let appcore  = new PerryRhodan.AudiobookPlayer.App()
        this.LoadApplication (appcore)
    
    override this.OnRequestPermissionsResult(requestCode: int, permissions: string[], [<GeneratedEnum>] grantResults: Android.Content.PM.Permission[]) =
        Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults)
        Plugin.Permissions.PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults)

        base.OnRequestPermissionsResult(requestCode, permissions, grantResults)



