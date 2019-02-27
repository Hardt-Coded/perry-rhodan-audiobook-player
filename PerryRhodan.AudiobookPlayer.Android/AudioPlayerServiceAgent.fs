namespace PerryRhodan.AudiobookPlayer.Android

module rec AudioPlayerServiceAgent =


    let bla = 1
    //open System
    //open Android.App
    //open Android.OS
    //open AudioPlayerState
    //open Android.Content

    //open Newtonsoft.Json
    //open FSharp.Control

    //open ServiceActions
    //open Android.Media

    //open Microsoft.AppCenter
    //open Microsoft.AppCenter.Crashes
    //open Microsoft.AppCenter.Analytics


    //let AudioServiceComponentName = "perry.rhodan.audiobook.audioservice"


    //module Helpers =

        
    //    open Android.Media.Session
        

    //    let isMyServiceRunning (typ:Type) =
    //        // function is deprecated, BUT it return my own services. That's fine!  
    //        let nameOfService = Java.Lang.Class.FromType(typ).CanonicalName
    //        let services =
    //            (Application.Context.GetSystemService(Context.ActivityService) :?> ActivityManager)
    //                .GetRunningServices(Int32.MaxValue)
    //        services
    //        |> Seq.exists( fun i -> i.Service.ClassName = nameOfService)

        
    //    let serialize x =
    //        JsonConvert.SerializeObject(x)

    //    let deserialize<'a> json =
    //        JsonConvert.DeserializeObject<'a>(json)


    //    let sendCommandToService (servicetype:System.Type) (additional:(string * string) list ) command=
    //        let intent = new Intent(Android.App.Application.Context,servicetype)
    //        intent.SetAction(command) |> ignore
    //        additional |> List.iter (fun (k,v) -> intent.PutExtra(k,v) |> ignore)
    //        //additional intent
    //        let pendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, intent, PendingIntentFlags.UpdateCurrent)
    //        pendingIntent.Send()


    //    let icon name = 
    //        typeof<Resources.Drawable>.GetField(name).GetValue(null) :?> int



    //module ServiceActions =
        
    //    let ACTION_START_SERVICE = "PerryRhodan.action.START_SERVICE"
    //    let ACTION_STOP_SERVICE = "PerryRhodan.action.STOP_SERVICE"
    //    let ACTION_STOP_PLAYER = "PerryRhodan.action.STOP_PLAYER"
    //    let ACTION_START_PLAYER = "PerryRhodan.action.START_PLAYER"
    //    //let ACTION_TOGGLE_PLAYPAUSE = "PerryRhodan.action.TOGGLE_PLAYPAUSE"
    //    let ACTION_MOVEFORWARD_PLAYER = "PerryRhodan.action.MOVE_FORWARD"
    //    let ACTION_MOVEBACKWARD_PLAYER = "PerryRhodan.action.MOVE_BACKWARD"
    //    let ACTION_SETPOSITION_PLAYER = "PerryRhodan.action.SET_POSITION"
    //    let ACTION_JUMP_FORWARD_PLAYER = "PerryRhodan.action.JUMP_FORWARD"
    //    let ACTION_JUMP_BACKWARD_PLAYER = "PerryRhodan.action.JUMP_BACKWARD"


    //module Dependencies =

    //    open Android.Media
    //    open Android.Media.Session

    //    let notificationManager = 
    //        Android.App.Application.Context.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager

    //    let audioManager = 
    //        Android.App.Application.Context.GetSystemService(Android.Content.Context.AudioService) :?> AudioManager

    //    let createMediaSession () =
    //        let session = new MediaSession(Android.App.Application.Context, "PerryRhodanAudioBookPlayer")
    //        session       

    //    let audioFocusRequest audioFocusOnChange = 
    //        (new AudioFocusRequestClass.Builder(AudioFocus.Gain))
    //            .SetAudioAttributes(playbackAttributes)
    //            .SetAcceptsDelayedFocusGain(true)
    //            .SetWillPauseWhenDucked(true)
    //            .SetOnAudioFocusChangeListener(audioFocusOnChange)                    
    //            .Build()


    //    let playbackAttributes =
    //        (new AudioAttributes.Builder())
    //            .SetUsage(AudioUsageKind.Media)
    //            .SetContentType(AudioContentType.Music)
    //            .Build()

    //    let initMediaPlayer (stateMailBox:MailboxProcessor<AudioPlayerCommand>) = 
    //        let mediaPlayer = new MediaPlayer()
    //        mediaPlayer.SetAudioAttributes(playbackAttributes)
    //        mediaPlayer.SetWakeMode(Application.Context, WakeLockFlags.Partial)
        
    //        mediaPlayer.Completion.Add(
    //            fun _ -> 
    //                stateMailBox.Post(MoveToNextTrack)
    //                ()
    //        )

            

    //        mediaPlayer


    //module Notification =

    //    open Helpers 
    //    open ServiceActions
    //    open Android.App
    //    open Android.Media.Session
    //    open Xamarin.Forms.Platform.Android 
    //    open Android.App
    //    open Android.OS
    //    open Android.Media
    //    open Android.Content
    //    open Android.Runtime
    //    open Android.Views
    //    open Xamarin.Forms.Platform.Android
        
    //    type AndroidDrawable = Android.Resource.Drawable


    //    let private RHODAN_CHANNEL_ID = "pr_player_aud_notification_20190221"

    //    let  private ACTION_MAIN_ACTIVITY = "PerryRhodan.action.MAIN_ACTIVITY"
    //    let SERVICE_RUNNING_NOTIFICATION_ID = 10401
        
    //    //let SERVICE_STARTED_KEY = "has_service_been_started";
    //    //let BROADCAST_MESSAGE_KEY = "broadcast_message";
    //    let  private NOTIFICATION_BROADCAST_ACTION = "PerryRhodan.Notification.Action"
    //    //let DELAY_BETWEEN_LOG_MESSAGES = 1000L

    //    let  private context = Android.App.Application.Context


    //    let private buildIntentToShowMainActivity () =
    //        let notificationIntent = new Intent(context, typeof<FormsAppCompatActivity>)
    //        notificationIntent.SetAction(ACTION_MAIN_ACTIVITY) |> ignore
    //        notificationIntent.SetFlags(ActivityFlags.SingleTop ||| ActivityFlags.ClearTask)  |> ignore
    //        //notificationIntent.PutExtra(SERVICE_STARTED_KEY, true)  |> ignore
    //        PendingIntent.GetActivity(context, 0, notificationIntent, PendingIntentFlags.UpdateCurrent)


    //    let private buildStartStopToggleAction state =
    //        match state.State with
    //        | Stopped->
    //            buildStartAudioAction ()
    //        | Playing ->
    //            buildStopAudioAction ()
            
                


    //    let private buildStartAudioAction () =
    //        let icon = AndroidDrawable.IcMediaPlay
    //        let intent = new Intent(context, context.GetType());
    //        intent.SetAction(ACTION_START_PLAYER) |> ignore
    //        let startAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", startAudioPendingIntent)
    //        builder.Build();

    //    let private buildStopAudioAction () =
    //        let icon = AndroidDrawable.IcMediaPause
    //        let intent = new Intent(context, context.GetType())
    //        intent.SetAction(ACTION_STOP_PLAYER) |> ignore
    //        let stopAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
    //        builder.Build();

    //    let private buildForwardAudioAction () =
    //        let icon = AndroidDrawable.IcMediaFf
    //        let intent = new Intent(context, context.GetType())
    //        intent.SetAction(ACTION_JUMP_FORWARD_PLAYER) |> ignore            
    //        let stopAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
    //        builder.Build();

    //    let private buildBackwardAudioAction () =
    //        let icon = AndroidDrawable.IcMediaRew
    //        let intent = new Intent(context, context.GetType())

    //        // ACTION_SETPOSITION_PLAYER
    //        intent.SetAction(ACTION_JUMP_BACKWARD_PLAYER) |> ignore                        
    //        let stopAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
    //        builder.Build();

    //    let private buildStopServiceAction () =
    //        let icon = icon "settings_icon"
    //        let intent = new Intent(context, context.GetType())
    //        intent.SetAction(ACTION_STOP_SERVICE) |> ignore
    //        let startAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", startAudioPendingIntent)
            
    //        builder.Build();


    //    let createNotificationChannel () =
    //        if Build.VERSION.SdkInt < BuildVersionCodes.O then
    //            ()
    //        else
    //            let name = "PerryRhodanNotifyChannel"
    //            let description = "AudioPlayer Desc"
    //            let channel = new NotificationChannel(RHODAN_CHANNEL_ID,name,NotificationImportance.Default, Description = description)
    //            channel.SetSound(null,null)
    //            let notificationManager = context.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
    //            notificationManager.CreateNotificationChannel(channel)
    //            ()


    //    let buildNotification (mediaSession:MediaSession) info =            
    //        let context = Android.App.Application.Context
    //        let icon = icon "pr_small_icon"
    //        let title = info.AudioBook.FullName
                
            
    //        mediaSession.Active <- true
    //        mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons||| MediaSessionFlags.HandlesTransportControls)
           
    //        let style = new Notification.MediaStyle();
    //        style.SetMediaSession(mediaSession.SessionToken) |> ignore

    //        let currenInfoString = 
    //            sprintf "%i - %s / %s"
    //                info.CurrentTrackNumber
    //                ((info.Position |> Common.TimeSpanHelpers.toTimeSpan).ToString("hh\:mm\:ss"))
    //                ((info.Duration |> Common.TimeSpanHelpers.toTimeSpan).ToString("hh\:mm\:ss"))
            
    //        let notify = 
    //            let builder =
    //                if Build.VERSION.SdkInt < BuildVersionCodes.O then
    //                    (new Notification.Builder(context))
    //                else
    //                    (new Notification.Builder(context,RHODAN_CHANNEL_ID))
                        
    //            builder
    //                .SetStyle(style)                    
    //                .SetContentTitle(title)
    //                .SetContentText(currenInfoString)
    //                .SetSmallIcon(icon)                    
    //                .SetContentIntent(buildIntentToShowMainActivity())
    //                .SetOngoing(true)                    
    //                .AddAction(buildBackwardAudioAction())
    //                .AddAction(buildStartStopToggleAction info)
    //                .AddAction(buildForwardAudioAction())

    //        try
    //            let albumPicFile = 
    //                info.AudioBook.Thumbnail
    //                |> Option.defaultValue "@drawable/AudioBookPlaceholder_Dark.png"

    //            let albumPic = Android.Graphics.BitmapFactory.DecodeFile(albumPicFile)
    //            notify.SetLargeIcon(albumPic) |> ignore
    //        with
    //        | _ -> ()

    //        style.SetShowActionsInCompactView(0, 1, 2) |> ignore
    //        notify.Build()

    //    let updateNotification (mediaSession:MediaSession) info =
    //        let notify = info |> buildNotification mediaSession
    //        NotificationManager.FromContext(Android.App.Application.Context).Notify(SERVICE_RUNNING_NOTIFICATION_ID,notify)



    //type NoisyHeadPhoneReceiver(stateMailbox:MailboxProcessor<AudioPlayerCommand>) =
    //    inherit BroadcastReceiver()

    //    override this.OnReceive (context, intent) =
    //        if (intent.Action = AudioManager.ActionAudioBecomingNoisy) then
    //            try
    //                stateMailbox.Post(StopAudioPlayer false)
    //                ()
    //            with
    //            | ex ->
    //                Crashes.TrackError(ex)



    //[<Service>]
    //type AudioPlayerService() as self =
    //    inherit Service()

    //    static let mutable instance:AudioPlayerService option = None
        

    //    let stateMailBox:MailboxProcessor<AudioPlayerCommand> =
    //        AudioPlayer.StateMailBox

    //    let functionMailBox:MailboxProcessor<AudioPlayerEvents> =
    //        AudioPlayer.ServiceFunctionMailBox


    //    interface AudioManager.IOnAudioFocusChangeListener with
    //        member ___.OnAudioFocusChange(focusChange:AudioFocus) =
                

    //            let state =
    //                stateMailBox.PostAndReply((fun rc -> GetCurrentState rc), 1000)

    //            match focusChange with
    //            | AudioFocus.Gain ->
    //                if (state.PlaybackDelayed || state.ResumeOnAudioFocus) then                                    
    //                    stateMailBox.Post(StartAudioPlayer)                                    
    //            | AudioFocus.Loss ->
    //                stateMailBox.Post(StopAudioPlayer true)
    //            | AudioFocus.LossTransient | AudioFocus.LossTransientCanDuck->
    //                stateMailBox.Post(StopAudioPlayer false)
    //            | _ ->
    //                () 


    //    static member Current = instance

    //    override this.OnCreate () =
    //        instance <- Some self
    //        ()

    //    override this.OnStartCommand(intent,_,_) =
    //        if intent.Action = null || intent.Action = "" then
    //            StartCommandResult.Sticky
    //        else
    //            let infoJson = intent.GetStringExtra("info")

    //            if (infoJson = null || infoJson = "") then
    //                // cam from the notifier
    //                match intent.Action with
    //                | x when x = ACTION_START_SERVICE ->
    //                    // not with the notification 
    //                    ()
    //                | x when x = ACTION_STOP_SERVICE ->
    //                    // not with the notification 
    //                    ()
    //                | x when x = ACTION_START_PLAYER ->
    //                    stateMailBox.Post(StartAudioPlayer)
    //                | x when x = ACTION_STOP_PLAYER ->
    //                    stateMailBox.Post(StopAudioPlayer false)
    //                | x when x = ACTION_MOVEFORWARD_PLAYER ->
    //                    stateMailBox.Post(MoveToNextTrack)
    //                | x when x = ACTION_MOVEBACKWARD_PLAYER ->
    //                    stateMailBox.Post(MoveToPreviousTrack)
    //                | x when x = ACTION_SETPOSITION_PLAYER ->
    //                    // not with the notification 
    //                    ()
    //                | x when x = ACTION_JUMP_FORWARD_PLAYER ->
    //                    // use the state mail box for jumping
    //                    stateMailBox.Post(JumpForward)
    //                | x when x = ACTION_JUMP_BACKWARD_PLAYER ->
    //                    stateMailBox.Post(JumpBackwards)

    //            else
    //                let info = infoJson |> Helpers.deserialize<AudioPlayerInfo> 

    //                match intent.Action with
    //                | x when x = ACTION_START_SERVICE ->
    //                    functionMailBox.Post(AudioServiceStarted info)
    //                | x when x = ACTION_STOP_SERVICE ->
    //                    functionMailBox.Post(AudioServiceStopped info)
    //                | x when x = ACTION_START_PLAYER ->
    //                    functionMailBox.Post(AudioPlayerStarted info)
    //                | x when x = ACTION_STOP_PLAYER ->
    //                    functionMailBox.Post(AudioPlayerStopped info)
    //                | x when x = ACTION_MOVEFORWARD_PLAYER ->
    //                    functionMailBox.Post(MovedToNextTrack info)
    //                | x when x = ACTION_MOVEBACKWARD_PLAYER ->
    //                    functionMailBox.Post(MovedToPreviousTrack info)
    //                | x when x = ACTION_SETPOSITION_PLAYER ->
    //                    functionMailBox.Post(PositionSet info)
    //                | x when x = ACTION_JUMP_FORWARD_PLAYER ->
    //                    // use the state mail box for jumping
    //                    stateMailBox.Post(JumpForward)
    //                | x when x = ACTION_JUMP_BACKWARD_PLAYER ->
    //                    stateMailBox.Post(JumpBackwards)

    //            StartCommandResult.Sticky

    //    override this.OnBind _ =
    //        null

    //    override this.OnDestroy() =
    //        stateMailBox.Post(StopAudioService)
    //        ()

    //    member this.ComponentName = this.Class.Name;


    //let initAudioService () =
    //    async {
    //        if Helpers.isMyServiceRunning typeof<AudioPlayerService> then
    //            return true
    //        else
    //            let startServiceIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)            
    //            Android.App.Application.Context.StartService(startServiceIntent) |> ignore

    //            // waiting until foreground service is registered            
    //            // build an async sequence
    //            let waitUntilServiceStarted =
    //                asyncSeq {
    //                    for _ in [1..200] do
    //                        yield Helpers.isMyServiceRunning typeof<AudioPlayerService>
    //                        do! Async.Sleep 50
    //                }

    //            let! isStarted = waitUntilServiceStarted |> AsyncSeq.exists (fun i -> i)
    //            return isStarted
    //    }




    //let audioPlayerServiceIntentMailbox () =          
    //    MailboxProcessor<AudioPlayerEvents>.Start(
            
    //        fun inbox ->

    //            let sendCmd action info =
    //                let json =  info |> Helpers.serialize
    //                action 
    //                |> Helpers.sendCommandToService typeof<AudioPlayerService> [("info",json)]

    //            let rec loop () =
    //                async {
                        
    //                    let! event = inbox.Receive()
    //                    match event with
    //                    | AudioServiceStarted info ->
    //                        info |> sendCmd ACTION_START_SERVICE
    //                    | AudioServiceStopped info ->
    //                        info |> sendCmd ACTION_STOP_SERVICE
    //                    | AudioPlayerStarted info ->
    //                        info |> sendCmd ACTION_START_PLAYER
    //                    | AudioPlayerStopped info ->
    //                        info |> sendCmd ACTION_STOP_PLAYER
    //                    | MovedToNextTrack info ->
    //                        info |> sendCmd ACTION_MOVEFORWARD_PLAYER
    //                    | MovedToPreviousTrack info ->
    //                        info |> sendCmd ACTION_MOVEBACKWARD_PLAYER
    //                    | PositionSet info ->
    //                        info |> sendCmd ACTION_SETPOSITION_PLAYER


    //                    return! loop()
    //                }

    //            loop ()
    //)



    //let audioPlayerServiceFunctionMailbox      
    //    (informationDispatcher: MailboxProcessor<InformationDispatcher.InfoDispatcherMsg>)
    //    (stateMailbox:MailboxProcessor<AudioPlayerCommand>) =        
    //        MailboxProcessor<AudioPlayerEvents>.Start(
            
    //            fun inbox ->

    //                let mediaSession = 
    //                    Dependencies.createMediaSession ()
    //                let mutable onAfterPrepare = None
    //                let mutable mediaPlayer:MediaPlayer = 
    //                    Dependencies.initMediaPlayer stateMailbox
    //                let mutable noisyHeadPhoneReceiver = None
    //                let mutable updateTimer:System.Threading.Timer option = None

    //                let rec loop () =
    //                    async {

    //                        let! event = inbox.Receive()

                            
    //                        match event with
    //                        | AudioServiceStarted info ->
    //                            info |> onAudioServiceStarted
    //                        | AudioServiceStopped info ->
    //                            info |> onAudioServiceStopped
    //                        | AudioPlayerStarted info ->
    //                            do! info |> onAudioPlayerStarted
    //                        | AudioPlayerStopped info ->
    //                            info |> onAudioPlayerStopped
    //                        | MovedToNextTrack info ->
    //                            do! info |> onMovedToNextTrack
    //                        | MovedToPreviousTrack info ->
    //                            do! info |> onMovedToPreviousTrack
    //                        | PositionSet info ->
    //                            info |> onPositionSet
                                


    //                        return! loop ()
    //                    }

    //                and onAudioServiceStarted info =
    //                    match AudioPlayerService.Current with
    //                    | None ->
    //                        Microsoft.AppCenter.Crashes.Crashes.TrackError(exn("android audio player service missing. Not instanciated? wtf?"))
    //                    | Some service ->
    //                        match info.ServiceState with
    //                        | Starting ->
    //                            Notification.createNotificationChannel ()
    //                            let notification = info |> Notification.buildNotification mediaSession
    //                            service.StartForeground(Notification.SERVICE_RUNNING_NOTIFICATION_ID, notification)
    //                            // adding after Prepare event to mediaplayer 
    //                            mediaPlayer.Prepared.Add(
    //                                fun _ ->                 
    //                                    match onAfterPrepare with
    //                                    | None -> ()
    //                                    | Some cmd -> cmd()
    //                                    mediaPlayer.Start()
    //                            )  
    //                            stateMailbox.Post(SetCurrentAudioServiceStateToStarted)
    //                            informationDispatcher.Post(InformationDispatcher.AddListener ("notifyUpdater", fun info -> updateInfo info))
    //                        | _ ->
    //                            info |> Notification.updateNotification mediaSession
                            
    //                    ()

    //                and onAudioServiceStopped info =
    //                    match info.ServiceState with
    //                    | AudioPlayerServiceState.Started ->
    //                        match AudioPlayerService.Current with
    //                        | None ->
    //                            Microsoft.AppCenter.Crashes.Crashes.TrackError(exn("android audio player service missing. Not instanciated? wtf?"))
    //                        | Some audioPlayerService ->
    //                            // shut down service
    //                            match info.State with
    //                            | Playing ->
    //                                stateMailbox.Post(StopAudioPlayer false)
    //                            | _ -> 
    //                                ()

    //                            audioPlayerService.StopForeground(true)
    //                            audioPlayerService.StopSelf()
    //                            // init new Mediaplayer
    //                            mediaPlayer <- Dependencies.initMediaPlayer stateMailbox
                                
    //                            let notificationManager = Android.App.Application.Context.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
    //                            notificationManager.Cancel(Notification.SERVICE_RUNNING_NOTIFICATION_ID)
    //                            mediaSession.Dispose()
                                
    //                    | _ ->
    //                        ()

    //                and onAudioPlayerStarted info =
    //                    async {
    //                        match AudioPlayerService.Current with
    //                        | None ->
    //                            Microsoft.AppCenter.Crashes.Crashes.TrackError(exn("android audio player service missing. Not instanciated? wtf?"))
    //                        | Some audioPlayerService ->                            
    //                            let audioFocusRes = 
    //                                if (Build.VERSION.SdkInt > BuildVersionCodes.O) then
    //                                    Dependencies.audioManager.RequestAudioFocus(Dependencies.audioFocusRequest audioPlayerService)
    //                                else
    //                                    Dependencies.audioManager.RequestAudioFocus(audioPlayerService,Stream.Music,AudioFocus.Gain)
    //                            ()
    //                            match audioFocusRes with
    //                            | AudioFocusRequest.Failed ->
    //                                stateMailbox.Post(StopAudioPlayer false)
    //                            | AudioFocusRequest.Delayed ->
    //                                stateMailbox.Post(StopAudioPlayer true)
    //                            | AudioFocusRequest.Granted ->
    //                                match info.State with
    //                                | Playing ->
    //                                    // don't get confused. the state is set before. So if we move from stop -> playing,
    //                                    // the current state is "Playing"
    //                                    do! (playFile info)
    //                                | Stopped ->
    //                                    ()
    //                            | _ ->
    //                                Microsoft.AppCenter.Crashes.Crashes.TrackError(exn("whaaat? invalid audioFocusRequest Enum."))|> ignore
    //                    }
                        
                    
    //                and playFile info =
    //                    async {
    //                        let file = info.Filename
    //                        let position = info.Position
    //                        registerNoisyHeadPhoneReciever ()                
    //                        // stop info sending until the playing is full ready                
    //                        mediaPlayer.Reset()
    //                        do! mediaPlayer.SetDataSourceAsync(file) |> Async.AwaitTask
    //                        onAfterPrepare <- Some (fun () -> 
    //                            mediaPlayer.SeekTo(position)
    //                            updateTimer <- 
    //                                Some (new System.Threading.Timer(
    //                                    (
    //                                        fun _ -> 
    //                                            stateMailbox.Post(UpdatePositionExternal mediaPlayer.CurrentPosition)
    //                                        ),null,0,1000)
    //                                )
    //                        )
    //                        mediaPlayer.PrepareAsync()
    //                        return ()
    //                    }

    //                and updateInfo (info:AudioPlayerInfo) =
    //                    async{
    //                        info |> Notification.updateNotification mediaSession 
    //                    }
                        

    //                and registerNoisyHeadPhoneReciever () =
    //                    match noisyHeadPhoneReceiver with
    //                    | None -> 
    //                        noisyHeadPhoneReceiver <- Some ( new NoisyHeadPhoneReceiver(stateMailbox) )
    //                        let noiseHpIntentFilter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy)
    //                        Application.Context.RegisterReceiver(noisyHeadPhoneReceiver.Value, noiseHpIntentFilter) |> ignore
    //                    | Some _ ->
    //                        ()


    //                and unregisterNoisyHeadPhoneReciever () =
    //                    match noisyHeadPhoneReceiver with
    //                    | None -> ()
    //                    | Some r ->
    //                        Application.Context.UnregisterReceiver(r)
    //                        noisyHeadPhoneReceiver <- None
    //                        ()

    //                and onAudioPlayerStopped info =
    //                    match info.State with
    //                    | Stopped ->
    //                        mediaPlayer.Stop()
    //                    | Playing  ->
    //                        ()
    //                    unregisterNoisyHeadPhoneReciever ()


    //                and onMovedToNextTrack info =
    //                    async {
    //                        info |> onAudioPlayerStopped
    //                        do! info |> onAudioPlayerStarted
    //                    }
                        

    //                and onMovedToPreviousTrack info =
    //                    async {
    //                        info |> onAudioPlayerStopped
    //                        do! info |> onAudioPlayerStarted
    //                    }
                        

    //                and onPositionSet info =
    //                    match info.State with
    //                    | Playing ->
    //                        mediaPlayer.SeekTo(info.Position)
    //                    | Stopped ->
    //                        ()
    //                    ()


    //                loop ()
    //        )

    
    //let globalIntentMailbox =
    //    AudioPlayerServiceAgent.audioPlayerServiceIntentMailbox ()

    //let globalInfoDispatcher =
    //    AudioPlayerState.InformationDispatcher.audioPlayerStateInformationDispatcher

    //let globalStateMailBox = 
    //    AudioPlayerState.audioPlayerStateMailbox 
    //        globalIntentMailbox 
    //        globalInfoDispatcher 
    //        AudioPlayerServiceAgent.initAudioService

    //let globalFunctionMailbox =
    //    AudioPlayerServiceAgent.audioPlayerServiceFunctionMailbox 
    //        globalInfoDispatcher
    //        globalStateMailBox


    //type AudioPlayer() =
        
    //    let stateMailBox = 
    //        globalStateMailBox

    //    interface AudioPlayerState.IAudioPlayer with

    //        member this.RunService audiobook mp3list =
    //            stateMailBox.Post (StartAudioService (audiobook,mp3list))

    //        member this.StopService () =
    //            stateMailBox.Post(StopAudioService)
    //        member this.StartAudio file pos =
    //            stateMailBox.Post(StartAudioPlayerExtern (file,pos))
    //        member this.StopAudio () =
    //            stateMailBox.Post(StopAudioPlayer false)
    //        member this.TogglePlayPause () =
    //            stateMailBox.Post(TogglePlayPause)
    //        member this.MoveForward () =
    //            stateMailBox.Post(MoveToNextTrack)
    //        member this.MoveBackward () =
    //            stateMailBox.Post(MoveToPreviousTrack)
    //        member this.GotToPosition pos =
    //            stateMailBox.Post(SetPosition pos)
    //        member this.JumpForward () =
    //            stateMailBox.Post(JumpForward)
    //        member this.JumpBackward () =
    //            stateMailBox.Post(JumpBackwards)

    //        member this.GetCurrentState () =
    //            async {
    //                let! res = stateMailBox.PostAndTryAsyncReply((fun rc -> GetCurrentState rc),10000)
    //                return res;
    //            }


    //    // send only Android Intents
    //    static member ServiceIntentMailBox:MailboxProcessor<AudioPlayerEvents> = 
    //        globalIntentMailbox

    //    // dispatches audio player information to consumers
    //    static member InformationDispatcher:MailboxProcessor<InformationDispatcher.InfoDispatcherMsg> =
    //        globalInfoDispatcher

    //    // handles command and state and sends events to the intent mailbox and the inforamtion dispatcher
    //    static member StateMailBox:MailboxProcessor<AudioPlayerCommand>  = 
    //        globalStateMailBox

    //    // this mailbox will be called from the inside of the Android-Service "AudioPlayerService" on an incomming intent
    //    static member ServiceFunctionMailBox:MailboxProcessor<AudioPlayerEvents> = 
    //        globalFunctionMailbox
    

    
                    


