module SleepTimerServiceImplementation

    open System
    open AndroidX.AppCompat.App

    open Android.App
    open Android.OS
    open Android.Content
    open Domain
    open MediaManager
    open PerryRhodan.AudiobookPlayer.Services.Interfaces
    open PerryRhodan.AudiobookPlayer.ViewModel
    open PerryRhodan.AudiobookPlayer.ViewModels
    open _Microsoft.Android.Resource.Designer

    [<AutoOpen>]
    module private Settings =


        let smallIcon = Resource.Drawable.einsa_small_icon

        let logo =
            Android.Graphics.BitmapFactory.DecodeResource(Application.Context.Resources ,Resource.Drawable.eins_a_medien_logo)

        let timerServiceNotificationId = 234256
        let timerServiceChannelId = "perry.rhodan.sleeptimer.notification"
        let timerServiceChannelName = "SleepTimer Notification"
        let timerServiceChannelDescription = "SleepTimer Notification for the Eins A Medien Audiobook Player"


    module private AndroidService =

        [<Service(Exported=true,Name="perry.rhodan.audioplayer.sleeptimerservice",ForegroundServiceType=PM.ForegroundService.TypeDataSync)>]
        type SleepTimerService() as self =

            inherit Service() with

                let mutable sleepTime = None
                let mutable timer = None
                
                let buildNotification (title:string) (text:string) =
                    let intent = new Intent(self, typeof<AppCompatActivity>)
                    let pendingIntentId = 83475
                    let pendingIntent = PendingIntent.GetActivity(self, pendingIntentId, intent, PendingIntentFlags.Immutable ||| PendingIntentFlags.UpdateCurrent)

                    let builder =
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) then
                            (new Notification.Builder(self, timerServiceChannelId))
                                .SetContentIntent(pendingIntent)
                                .SetContentTitle(title)
                                .SetContentText(text)
                                .SetSmallIcon(smallIcon)
                                .SetLargeIcon(logo)

                        else
                            (new Notification.Builder(self, timerServiceChannelId))
                                .SetContentIntent(pendingIntent)
                                .SetContentTitle(title)
                                .SetContentText(text)
                                .SetSmallIcon(smallIcon)
                                .SetLargeIcon(logo)
                                .SetSound(null)
                                .SetVibrate(null)

                    builder.Build()


                let createNotificationChannel (manager:NotificationManager) =
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O) then
                        let channelNameJava = new Java.Lang.String(timerServiceChannelName)
                        let channel = new NotificationChannel(timerServiceChannelId, channelNameJava, NotificationImportance.Default, Description = timerServiceChannelDescription)
                        channel.SetSound(null,null)
                        channel.SetVibrationPattern(null)
                        manager.CreateNotificationChannel(channel) |> ignore
                    ()


                let createDownloadServiceNotification title text =

                    let manager = (Application.Context.GetSystemService(Application.NotificationService) :?> NotificationManager)

                    let notify (manager:NotificationManager) (title:string) (text:string) =
                        let notification = buildNotification title text
                        manager.Notify(timerServiceNotificationId, notification)
                        ()

                    createNotificationChannel manager
                    notify manager title text


                let updateNotification title text =
                    //createDownloadServiceNotification title text
                    let manager = (Application.Context.GetSystemService(Application.NotificationService) :?> NotificationManager)
                    let notification = buildNotification title text
                    manager.Notify(timerServiceNotificationId, notification)


                let shutDownService () =
                    self.StopForeground(true)
                    self.StopSelf()


                override this.OnBind _ =
                    null

                override this.OnCreate () =
                    try
                        let manager = (Application.Context.GetSystemService(Application.NotificationService) :?> NotificationManager)
                        createNotificationChannel manager
                        this.StartForeground(timerServiceNotificationId, buildNotification "SleepTimer" "SleepTimer gestartet", PM.ForegroundService.TypeDataSync)
                    with
                    | ex ->
                        Global.telemetryClient.TrackException ex
                        reraise()



                override this.OnStartCommand (intent,_,_) =
                    try
                        let timeStr = intent.GetStringExtra("time")
                        sleepTime <-
                            match timeStr with
                            | "" -> None
                            | _ -> Some (TimeSpan.Parse(timeStr))
                            
                        
                        match timer with
                        | None ->
                            timer <- Some (new System.Timers.Timer())
                            timer.Value.Elapsed.Add(fun _ ->
                                sleepTime <- sleepTime |> Option.map (fun t -> t - TimeSpan.FromSeconds(1.0))
                                PlayerPage.sleepTimerEvent.Trigger sleepTime
                                if sleepTime.Value <= TimeSpan.Zero then
                                    CrossMediaManager.Current.Pause().Wait()
                                    shutDownService()
                                    timer |> Option.iter (fun t -> t.Stop())
                                    timer <- None
                                    sleepTime <- None
                                    PlayerPage.sleepTimerEvent.Trigger sleepTime
                                else
                                    sleepTime |> Option.iter (fun sleepTime ->
                                        updateNotification "SleepTimer" $"""läuft noch {(sleepTime.ToString("hh\\:mm\\:ss"))}"""
                                    )
                            )
                            timer.Value.Interval <- 1000.0
                            timer.Value.Start()
                        | Some _ ->
                            ()
                            
                        StartCommandResult.Sticky
                    with
                    | ex ->
                        Global.telemetryClient.TrackException ex
                        shutDownService()
                        StartCommandResult.Sticky

    module DependencyService =

        open AndroidCommon.ServiceHelpers

        type SleepTimerService () =
            interface ISleepTimerService with

                override this.StartSleepTimer time =
                    let bundle = new Bundle()
                    bundle.PutString("time", time |> Option.map (_.ToString("hh\\:mm\\:ss")) |> Option.defaultValue "")
                    Application.Context.StartForeGroundService<AndroidService.SleepTimerService>(bundle)