namespace PerryRhodan.AudiobookPlayer.Android

module rec AudioPlayerService =

    open Android.App
    open Android.OS
    open Android.Media
    open Android.Media.Session
    open Android.Content
    
    open Android.Runtime
    open Android.Views
    open Android.Widget

    open Xamarin.Forms.Platform.Android
    open Android.Support.V4.Media.Session
    open Android.Support.V4.Media
    open Android.Support.V4.App

    open Microsoft.AppCenter.Crashes
    
    open Services

    open Android.Util
    open Newtonsoft.Json
    open Global
    
    
    type NoisyHeadPhoneReceiver(audioPlayer:DependencyServices.IAudioPlayer) =
        inherit BroadcastReceiver()

        override this.OnReceive (context, intent) =
            if (intent.Action = AudioManager.ActionAudioBecomingNoisy) then
                try
                    if audioPlayer.OnNoisyHeadPhone.IsSome then
                        audioPlayer.OnNoisyHeadPhone.Value()
                with
                | ex ->
                    Crashes.TrackError(ex)
                

    let SERVICE_RUNNING_NOTIFICATION_ID = 10401
    let RHODAN_CHANNEL_ID = "pr_player_aud_notification"
    let SERVICE_STARTED_KEY = "has_service_been_started";
    let BROADCAST_MESSAGE_KEY = "broadcast_message";
    let NOTIFICATION_BROADCAST_ACTION = "PerryRhodan.Notification.Action";
    
    let ACTION_START_SERVICE = "PerryRhodan.action.START_SERVICE";
    let ACTION_STOP_SERVICE = "PerryRhodan.action.STOP_SERVICE";
    let ACTION_STOP_PLAYER = "PerryRhodan.action.STOP_PLAYER";
    let ACTION_START_PLAYER = "PerryRhodan.action.START_PLAYER";
    let ACTION_MAIN_ACTIVITY = "PerryRhodan.action.MAIN_ACTIVITY";



    [<Service>]
    type AudioPlayer() =
        inherit Service()
       
        static let mutable instance = None

        let mutable currentAudioBook:(Domain.AudioBook * (string * int) list) option = None
        let mutable currentStartFile:string = ""
        let mutable currentStartPos = 0
        let mutable currentPlayingState = Stopped

        let mutable lastPositionBeforeStop = None
    
        let mutable onCompletion = None

        let mutable onAfterPrepare = None

        let mutable onInfo = None

        let mutable onNoisyHeadPhone = None

        let mutable currentFile = None

        let mutable noisyHeadPhoneReceiver = None

    
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
        
        let registerNoisyHeadPhoneReciever this =
            match noisyHeadPhoneReceiver with
            | None -> 
                noisyHeadPhoneReceiver <- Some ( new NoisyHeadPhoneReceiver(this) )
                let noiseHpIntentFilter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy)
                Application.Context.RegisterReceiver(noisyHeadPhoneReceiver.Value, noiseHpIntentFilter) |> ignore
            | Some _ ->
                ()

        let unregisterNoisyHeadPhoneReciever () =
            match noisyHeadPhoneReceiver with
            | None -> ()
            | Some r ->
                Application.Context.UnregisterReceiver(r)
                noisyHeadPhoneReceiver <- None
                ()

        let mutable isStarted = false

        let mutable handler:Handler = null


        let createNotificationChannel () =
            if Build.VERSION.SdkInt < BuildVersionCodes.O then
                ()
            else
                let name = "PerryRhodanNotifyChannel"
                let description = "AudioPlayer Desc"
                let channel = new NotificationChannel(RHODAN_CHANNEL_ID,name,NotificationImportance.Default, Description = description)
                let notificationManager = Android.App.Application.Context.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
                notificationManager.CreateNotificationChannel(channel)
        
        

        interface DependencyServices.IAudioPlayer with
        
            member this.LastPositionBeforeStop with get () = lastPositionBeforeStop

            member this.CurrentFile with get () = currentFile

            member this.OnCompletion 
                with get () = onCompletion
                and set p = onCompletion <- p


            member this.OnNoisyHeadPhone 
                with get () = onNoisyHeadPhone
                and set p = onNoisyHeadPhone <- p


            member this.OnInfo 
                with get () = onInfo
                and set p = onInfo <- p


            member this.PlayFile file position =
                async {
                    this |> registerNoisyHeadPhoneReciever
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
                    currentStartPos <- mediaPlayer.CurrentPosition
                else
                    lastPositionBeforeStop <- Some mediaPlayer.CurrentPosition
                    currentStartPos <- mediaPlayer.CurrentPosition

                mediaPlayer.Stop()
                unregisterNoisyHeadPhoneReciever ()
                ()

            member this.GotToPosition ms =
                mediaPlayer.SeekTo(ms)

            member this.GetInfo () =
                async {
                    do! Common.asyncFunc(
                            fun () ->
                                match onInfo,mediaPlayer.IsPlaying with
                                | Some cmd, true -> cmd(mediaPlayer.CurrentPosition,mediaPlayer.Duration,currentPlayingState)
                                | Some cmd, false -> cmd(currentStartPos,mediaPlayer.Duration,currentPlayingState)
                                | _ -> ()
                    )
                }

            member this.RunService ab file currentPosition =
                async {
                    let startServiceIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayer>)
                    let json = JsonConvert.SerializeObject(ab)
                    startServiceIntent.PutExtra("abdata",json) |> ignore
                    startServiceIntent.PutExtra("file",file) |> ignore
                    startServiceIntent.PutExtra("pos",currentPosition) |> ignore
                    startServiceIntent.SetAction(ACTION_START_SERVICE) |> ignore
                    Android.App.Application.Context.StartService(startServiceIntent) |> ignore

                    // waiting until foreground service is registered
                    let mutable counter = 0
                    while instance = None do
                        do! Async.Sleep 50
                        counter <- counter + 1
                        if counter > 200 then
                            failwith ("unable to start foreground service!")

                    

                    return ()
                }

            member this.GetRunningService () =
                instance |> Option.map (fun i -> i :> Services.DependencyServices.IAudioPlayer)


            member this.StopService() =
                currentAudioBook <- None
                let stopServiceIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayer>)
                stopServiceIntent.SetAction(ACTION_STOP_SERVICE) |> ignore
                Android.App.Application.Context.StopService(stopServiceIntent) |> ignore

            member this.StartAudio () =
                if isStarted then
                    let startAudioIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayer>)
                    startAudioIntent.SetAction(ACTION_START_PLAYER) |> ignore
                    let startAudioPendingIntent = PendingIntent.GetService(this, 0, startAudioIntent, PendingIntentFlags())
                    startAudioPendingIntent.Send()
                    ()

            member this.StopAudio () =
                if isStarted then
                    let stopAudioIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayer>)
                    stopAudioIntent.SetAction(ACTION_STOP_PLAYER) |> ignore
                    let stopAudioPendingIntent = PendingIntent.GetService(this, 0, stopAudioIntent, PendingIntentFlags())
                    stopAudioPendingIntent.Send()
                    ()

        static member TAG = typeof<AudioPlayer>.FullName

        static member Instance 
            with get () = instance
            and set p = instance <- p

        member this.RegisterForegroundService() =
            createNotificationChannel ()
            let icon = typeof<Resources.Drawable>.GetField("app_icon").GetValue(null) :?> int
            let title = 
                currentAudioBook 
                |> Option.map (fun (ab,_) -> ab.FullName)
                |> Option.defaultValue "No Title!"

            let notify = 
                (new NotificationCompat.Builder(this,RHODAN_CHANNEL_ID))
                    .SetContentTitle(title)
                    .SetContentText("mal sehen!")
                    .SetSmallIcon(icon)
                    .SetContentIntent(this.BuildIntentToShowMainActivity())
                    .SetOngoing(true)
                    .AddAction(this.BuildStartAudioAction())
                    .AddAction(this.BuildStopAudioAction())
                    .AddAction(this.BuildStopServiceAction())
                    .Build();

            this.StartForeground(SERVICE_RUNNING_NOTIFICATION_ID, notify);

            ()

        member this.BuildIntentToShowMainActivity () =
            let notificationIntent = new Intent(this, typeof<FormsAppCompatActivity>)
            notificationIntent.SetAction(ACTION_MAIN_ACTIVITY) |> ignore
            notificationIntent.SetFlags(ActivityFlags.SingleTop ||| ActivityFlags.ClearTask)  |> ignore
            notificationIntent.PutExtra(SERVICE_STARTED_KEY, true)  |> ignore
            PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent)


        member this.BuildStartAudioAction () =
            let icon = typeof<Resources.Drawable>.GetField("home_icon").GetValue(null) :?> int
            let startAudioIntent = new Intent(this, this.GetType());
            startAudioIntent.SetAction(ACTION_START_PLAYER) |> ignore
            let startAudioPendingIntent = PendingIntent.GetService(this, 0, startAudioIntent, PendingIntentFlags())
            
            let builder = 
                new NotificationCompat.Action.Builder(icon, "Play", startAudioPendingIntent)
            
            builder.Build();

        member this.BuildStopAudioAction () =
            let icon = typeof<Resources.Drawable>.GetField("browse_icon").GetValue(null) :?> int
            let startAudioIntent = new Intent(this, this.GetType());
            startAudioIntent.SetAction(ACTION_STOP_PLAYER) |> ignore
            let startAudioPendingIntent = PendingIntent.GetService(this, 0, startAudioIntent, PendingIntentFlags())
            
            let builder = 
                new NotificationCompat.Action.Builder(icon, "Pause", startAudioPendingIntent)
            
            builder.Build();

        member this.BuildStopServiceAction () =
            let icon = typeof<Resources.Drawable>.GetField("settings_icon").GetValue(null) :?> int
            let startAudioIntent = new Intent(this, this.GetType());
            startAudioIntent.SetAction(ACTION_STOP_SERVICE) |> ignore
            let startAudioPendingIntent = PendingIntent.GetService(this, 0, startAudioIntent, PendingIntentFlags())
            
            let builder = 
                new NotificationCompat.Action.Builder(icon, "Stop Service", startAudioPendingIntent)
            
            builder.Build();


        override this.OnCreate () =
            base.OnCreate()
            Log.Debug(AudioPlayer.TAG,"init audio service") |> ignore
            AudioPlayer.Instance <- Some this
            handler <- new Handler()


        override this.OnStartCommand(intent,flags,startId) =

            match intent.Action with
            | x when x = ACTION_START_SERVICE ->
                let json = intent.GetStringExtra("abdata")
                let file = intent.GetStringExtra("file")
                let pos = intent.GetIntExtra("pos", 0)
                if json = "" then 
                    ()
                else
                    let ab = JsonConvert.DeserializeObject<Domain.AudioBook>(json)
                    match ab.State.DownloadedFolder with
                    | None ->
                        ()
                    | Some folder ->                
                        let mp3List = folder |> Services.Files.getMp3FileList |> Async.RunSynchronously
                        currentAudioBook <- Some (ab,mp3List)
                        currentStartFile <- file
                        currentStartPos <- pos
                        if not isStarted then
                            this.RegisterForegroundService()
                            isStarted <- true
            | x when x = ACTION_STOP_SERVICE ->
                this.StopForeground(true)
                this.StopSelf()
                isStarted <- false
            | x when x = ACTION_START_PLAYER ->
                Log.Debug(AudioPlayer.TAG,"player start") |> ignore
               
                match currentAudioBook with
                | Some (_,_) ->
                    let abp = this :> Services.DependencyServices.IAudioPlayer
                    abp.PlayFile currentStartFile currentStartPos |> Async.RunSynchronously
                    currentPlayingState <- Playing
                | _ ->
                    Log.Debug(AudioPlayer.TAG,"Nope!") |> ignore
                    ()


            | x when x = ACTION_STOP_PLAYER ->
                Log.Debug(AudioPlayer.TAG,"player stop") |> ignore
                let abp = this :> Services.DependencyServices.IAudioPlayer
                abp.Stop ()
                currentPlayingState <- Playing

            StartCommandResult.Sticky

        override this.OnBind intent =
            null

        override this.OnDestroy() =
            Log.Debug(AudioPlayer.TAG,"onDestroy called") |> ignore
            //handler.RemoveCallbacks(runnable)
            let notificationManager = this.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
            notificationManager.Cancel(SERVICE_RUNNING_NOTIFICATION_ID)
            isStarted <- false


        


        





        





        




    let CHANNEL_ID = "pr_player_notification"
    let NOTIFICATION_ID  = 1000

    let createNotificationChannel (mainActivity:FormsAppCompatActivity) =
        if Build.VERSION.SdkInt < BuildVersionCodes.O then
            ()
        else
            let name = "PerryRhodanNotifyChannel"
            let description = "AudioPlayer Desc"
            let channel = new NotificationChannel(CHANNEL_ID,name,NotificationImportance.Default, Description = description)
            let notificationManager = mainActivity.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
            notificationManager.CreateNotificationChannel(channel)


    [<Activity(Label="my second")>]
    type SecondActivity () =
        inherit Activity()

        override this.OnCreate bundle =
            base.OnCreate(bundle)

            let notService = Xamarin.Forms.DependencyService.Get<NotificationService>(Xamarin.Forms.DependencyFetchTarget.GlobalInstance) :> DependencyServices.INotificationService
            match notService.OnSecondActivity with
            | None ->
                ()
            | Some x ->
                x "came from the second"

            ()

    

    type NotificationService ()  =
        
        let mutable onSecondActivity = None

        let mutable mainActivity:Context = Android.App.Application.Context

        


        interface DependencyServices.INotificationService with

            member this.ShowNotification str =
                let valuesForActivity = new Bundle()
                valuesForActivity.PutString("message",str)
                let resultIntent = new Intent(mainActivity, typeof<SecondActivity>)
                
                // Pass some values to SecondActivity:
                resultIntent.PutExtras(valuesForActivity)|> ignore
                
                // Construct a back stack for cross-task navigation:
                let stackBuilder = TaskStackBuilder.Create(mainActivity);                
                stackBuilder.AddParentStack(Java.Lang.Class.FromType(typeof<SecondActivity>)) |> ignore
                stackBuilder.AddNextIntent(resultIntent) |> ignore
                
                // Create the PendingIntent with the back stack:
                let resultPendingIntent = stackBuilder.GetPendingIntent(0, (int) PendingIntentFlags.UpdateCurrent);

                
                let icon = typeof<Resources.Drawable>.GetField("app_icon").GetValue(null) :?> int

                let builder = 
                    (new NotificationCompat.Builder(mainActivity, CHANNEL_ID))
                        .SetAutoCancel(true) // Dismiss the notification from the notification area when the user clicks on it
                        .SetContentIntent(resultPendingIntent) // Start up this activity when the user clicks the intent.
                        .SetContentTitle("Message Send!") // Set the title   
                        .SetSmallIcon(icon)
                //        .SetLargeIcon(Android.Graphics.Bitmap.FromArray)
                        .SetContentText(sprintf "Message %s" str); // the message to display.

                // Finally, publish the notification:
                let notificationManager = NotificationManagerCompat.From(mainActivity)
                notificationManager.Notify(NOTIFICATION_ID, builder.Build())
                ()

            member this.OnSecondActivity               
                with get () = onSecondActivity
                and set p = onSecondActivity <- p


        


            

        

      
        
