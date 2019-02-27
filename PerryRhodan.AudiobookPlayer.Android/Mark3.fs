namespace PerryRhodan.AudiobookPlayer.Android

module rec Mark3 =

    open System
    open Android.App
    open Android.OS
    open AudioPlayerState
    open Android.Content

    open Newtonsoft.Json
    open FSharp.Control

    open ServiceActions
    open Android.Media

    open Microsoft.AppCenter
    open Microsoft.AppCenter.Crashes
    open Microsoft.AppCenter.Analytics


    let AudioServiceComponentName = "perry.rhodan.audiobook.audioservice"


    module Helpers =

        
        open Android.Media.Session
        

        let isMyServiceRunning (typ:Type) =
            // function is deprecated, BUT it return my own services. That's fine!  
            let nameOfService = Java.Lang.Class.FromType(typ).CanonicalName
            let services =
                (Application.Context.GetSystemService(Context.ActivityService) :?> ActivityManager)
                    .GetRunningServices(Int32.MaxValue)
            services
            |> Seq.exists( fun i -> i.Service.ClassName = nameOfService)

        
        let serialize x =
            JsonConvert.SerializeObject(x)

        let deserialize<'a> json =
            JsonConvert.DeserializeObject<'a>(json)


        let inline sendCommandToService (servicetype:System.Type) (additionalString:(string * string) list) (additionalInt:(string * int) list) command  =
            let intent = new Intent(Android.App.Application.Context,servicetype)
            intent.SetAction(command) |> ignore
            additionalString
            |> List.iter (
                fun (k,v) -> intent.PutExtra(k,v) |> ignore
            )
            additionalInt
            |> List.iter (
                fun (k,v) -> intent.PutExtra(k,v) |> ignore
            )
            //additional intent
            let pendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, intent, PendingIntentFlags.UpdateCurrent)
            pendingIntent.Send()


        let icon name = 
            typeof<Resources.Drawable>.GetField(name).GetValue(null) :?> int



    module ServiceActions =
        
        let START_SERVICE = "PerryRhodan.action.START_SERVICE"
        let STOP_SERVICE = "PerryRhodan.action.STOP_SERVICE"
        let STOP_PLAYER = "PerryRhodan.action.STOP_PLAYER"
        let START_PLAYER = "PerryRhodan.action.START_PLAYER"
        let TOGGLE_PLAYPAUSE = "PerryRhodan.action.TOGGLE_PLAYPAUSE"
        let MOVEFORWARD_PLAYER = "PerryRhodan.action.MOVE_FORWARD"
        let MOVEBACKWARD_PLAYER = "PerryRhodan.action.MOVE_BACKWARD"
        let SETPOSITION_PLAYER = "PerryRhodan.action.SET_POSITION"
        let JUMP_FORWARD_PLAYER = "PerryRhodan.action.JUMP_FORWARD"
        let JUMP_BACKWARD_PLAYER = "PerryRhodan.action.JUMP_BACKWARD"
        let GET_CURRENT_STATE_PLAYER = "PerryRhodan.action.GET_CURRENT_STATE"


    module Dependencies =

        open Android.Media
        open Android.Media.Session

        let notificationManager = 
            Android.App.Application.Context.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager

        let audioManager = 
            Android.App.Application.Context.GetSystemService(Android.Content.Context.AudioService) :?> AudioManager

        let createMediaSession () =
            let session = new MediaSession(Android.App.Application.Context, "PerryRhodanAudioBookPlayer")
            session       

        let audioFocusRequest audioFocusOnChange = 
            (new AudioFocusRequestClass.Builder(AudioFocus.Gain))
                .SetAudioAttributes(playbackAttributes)
                .SetAcceptsDelayedFocusGain(true)
                .SetWillPauseWhenDucked(true)
                .SetOnAudioFocusChangeListener(audioFocusOnChange)                    
                .Build()


        let playbackAttributes =
            (new AudioAttributes.Builder())
                .SetUsage(AudioUsageKind.Media)
                .SetContentType(AudioContentType.Music)
                .Build()

        let initMediaPlayer 
            (moveToNextTrack: unit -> unit)
            = 
            let mediaPlayer = new MediaPlayer()
            mediaPlayer.SetAudioAttributes(playbackAttributes)
            mediaPlayer.SetWakeMode(Application.Context, WakeLockFlags.Partial)
        
            mediaPlayer.Completion.Add(
                fun _ -> 
                    moveToNextTrack()
                    ()
            )

            

            mediaPlayer


    module Notification =

        open Helpers 
        open ServiceActions
        open Android.App
        open Android.Media.Session
        open Xamarin.Forms.Platform.Android 
        open Android.App
        open Android.OS
        open Android.Media
        open Android.Content
        open Android.Runtime
        open Android.Views
        open Xamarin.Forms.Platform.Android
        
        type AndroidDrawable = Android.Resource.Drawable


        let private RHODAN_CHANNEL_ID = "pr_player_aud_notification_20190221"

        let  private ACTION_MAIN_ACTIVITY = "PerryRhodan.action.MAIN_ACTIVITY"
        let SERVICE_RUNNING_NOTIFICATION_ID = 10401
        
        //let SERVICE_STARTED_KEY = "has_service_been_started";
        //let BROADCAST_MESSAGE_KEY = "broadcast_message";
        let  private NOTIFICATION_BROADCAST_ACTION = "PerryRhodan.Notification.Action"
        //let DELAY_BETWEEN_LOG_MESSAGES = 1000L

        let  private context = Android.App.Application.Context


        let private buildIntentToShowMainActivity () =
            let notificationIntent = new Intent(context, typeof<FormsAppCompatActivity>)
            notificationIntent.SetAction(ACTION_MAIN_ACTIVITY) |> ignore
            notificationIntent.SetFlags(ActivityFlags.SingleTop ||| ActivityFlags.ClearTask)  |> ignore
            //notificationIntent.PutExtra(SERVICE_STARTED_KEY, true)  |> ignore
            PendingIntent.GetActivity(context, 0, notificationIntent, PendingIntentFlags.UpdateCurrent)


        let private buildStartStopToggleAction state =
            match state.State with
            | Stopped->
                buildStartAudioAction ()
            | Playing ->
                buildStopAudioAction ()
            
                


        let private buildStartAudioAction () =
            let icon = AndroidDrawable.IcMediaPlay
            let intent = new Intent(context, typeof<Services.AudioPlayerService>);
            intent.SetAction(START_PLAYER) |> ignore
            let startAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
            let builder = 
                new Notification.Action.Builder(icon, "", startAudioPendingIntent)
            builder.Build();

        let private buildStopAudioAction () =
            let icon = AndroidDrawable.IcMediaPause
            let intent = new Intent(context, typeof<Services.AudioPlayerService>)
            intent.SetAction(STOP_PLAYER) |> ignore
            let stopAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
            let builder = 
                new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
            builder.Build();

        let private buildForwardAudioAction () =
            let icon = AndroidDrawable.IcMediaFf
            let intent = new Intent(context, typeof<Services.AudioPlayerService>)
            intent.SetAction(JUMP_FORWARD_PLAYER) |> ignore            
            let stopAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
            let builder = 
                new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
            builder.Build();

        let private buildBackwardAudioAction () =
            let icon = AndroidDrawable.IcMediaRew
            let intent = new Intent(context, typeof<Services.AudioPlayerService>)

            // ACTION_SETPOSITION_PLAYER
            intent.SetAction(JUMP_BACKWARD_PLAYER) |> ignore                        
            let stopAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
            let builder = 
                new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
            builder.Build();

        let private buildStopServiceAction () =
            let icon = icon "settings_icon"
            let intent = new Intent(context, typeof<Services.AudioPlayerService>)
            intent.SetAction(STOP_SERVICE) |> ignore
            let startAudioPendingIntent = PendingIntent.GetService(context, 0, intent, PendingIntentFlags.UpdateCurrent)
            
            let builder = 
                new Notification.Action.Builder(icon, "", startAudioPendingIntent)
            
            builder.Build();


        let createNotificationChannel () =
            if Build.VERSION.SdkInt < BuildVersionCodes.O then
                ()
            else
                let name = "PerryRhodanNotifyChannel"
                let description = "AudioPlayer Desc"
                let channel = new NotificationChannel(RHODAN_CHANNEL_ID,name,NotificationImportance.Default, Description = description)
                channel.SetSound(null,null)
                let notificationManager = context.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
                notificationManager.CreateNotificationChannel(channel)
                ()


        let buildNotification (mediaSession:MediaSession) info =            
            let context = Android.App.Application.Context
            let icon = icon "pr_small_icon"
            let title = info.AudioBook.FullName
                
            
            mediaSession.Active <- true
            mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons||| MediaSessionFlags.HandlesTransportControls)
           
            let style = new Notification.MediaStyle();
            style.SetMediaSession(mediaSession.SessionToken) |> ignore

            let currenInfoString = 
                sprintf "%i - %s / %s"
                    info.CurrentTrackNumber
                    ((info.Position |> Common.TimeSpanHelpers.toTimeSpan).ToString("mm\:ss"))
                    ((info.Duration |> Common.TimeSpanHelpers.toTimeSpan).ToString("mm\:ss"))
            
            let notify = 
                let builder =
                    if Build.VERSION.SdkInt < BuildVersionCodes.O then
                        (new Notification.Builder(context))
                    else
                        (new Notification.Builder(context,RHODAN_CHANNEL_ID))
                        
                builder
                    .SetStyle(style)                    
                    .SetContentTitle(title)
                    .SetContentText(currenInfoString)
                    .SetSmallIcon(icon)                    
                    .SetContentIntent(buildIntentToShowMainActivity())
                    .SetOngoing(true)                    
                    .AddAction(buildBackwardAudioAction())
                    .AddAction(buildStartStopToggleAction info)
                    .AddAction(buildForwardAudioAction())

            try
                let albumPicFile = 
                    info.AudioBook.Thumbnail
                    |> Option.defaultValue "@drawable/AudioBookPlaceholder_Dark.png"

                let albumPic = Android.Graphics.BitmapFactory.DecodeFile(albumPicFile)
                notify.SetLargeIcon(albumPic) |> ignore
            with
            | _ -> ()

            style.SetShowActionsInCompactView(0, 1, 2) |> ignore
            notify.Build()

        let updateNotification (mediaSession:MediaSession) info =
            let notify = info |> buildNotification mediaSession
            NotificationManager.FromContext(Android.App.Application.Context).Notify(SERVICE_RUNNING_NOTIFICATION_ID,notify)


    module Receivers =

        type NoisyHeadPhoneReceiver() =
            inherit BroadcastReceiver()

            override this.OnReceive (context, intent) =
                if (intent.Action = AudioManager.ActionAudioBecomingNoisy) then
                    try
                        // use android intents to send a Message !   
                        // ToDo: Impleent
                        ServiceActions.STOP_PLAYER
                        |> Helpers.sendCommandToService typeof<Services.AudioPlayerService> [] []
                        ()
                    with
                    | ex ->
                        Crashes.TrackError(ex)


   


    module AudioPlayerFuntionImplementations =

        
        open Android.Media
        open Android.Media.Session
        
        let private onStartAudioService 
            (service:Services.AudioPlayerService)
            (mediaPlayer:MediaPlayer)
            (mediaSession:MediaSession)
            (onAfterPrepare:(unit->unit) option ref )
            (informationDispatcher:MailboxProcessor<InformationDispatcher.InfoDispatcherMsg>)
            (updateInfo: AudioPlayerInfo -> Async<unit>)
            info =
                match info.ServiceState with
                | AudioPlayerServiceState.Stopped ->
                    Notification.createNotificationChannel ()
                    let notification = info |> Notification.buildNotification mediaSession
                    service.StartForeground(Notification.SERVICE_RUNNING_NOTIFICATION_ID, notification)
                    // adding after Prepare event to mediaplayer 
                    mediaPlayer.Prepared.Add(
                        fun _ ->                 
                            match onAfterPrepare.Value with
                            | None -> ()
                            | Some cmd -> cmd()
                            mediaPlayer.Start()
                    )                 
                    informationDispatcher.Post(InformationDispatcher.AddListener ("notifyUpdater", fun info -> updateInfo info))
                | _ ->
                    info |> Notification.updateNotification mediaSession
                
                info

        let private onStopAudioService 
            (service:Services.AudioPlayerService)
            (mediaPlayer:MediaPlayer byref)
            (mediaSession:MediaSession byref)
            (stopPlayer:AudioPlayerInfo->AudioPlayerInfo)            
            info =
                match info.ServiceState with
                | AudioPlayerServiceState.Started ->
                    // shut down service
                    let newState =
                        match info.State with
                        | Playing ->
                            (stopPlayer {info with ResumeOnAudioFocus = false })                            
                        | _ -> 
                            info

                    service.StopForeground(true)
                    service.StopSelf()                    
                    
                    let notificationManager = Android.App.Application.Context.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
                    notificationManager.Cancel(Notification.SERVICE_RUNNING_NOTIFICATION_ID)
                    mediaSession.Dispose()
                    newState
                | _ ->
                    info


        let private onStartAudioPlayer 
            (stopPlayer:AudioPlayerInfo->AudioPlayerInfo)
            (playFile: AudioPlayerInfo -> Async<AudioPlayerInfo>)
            (service:Services.AudioPlayerService)
            (registerNoisyHeadPhoneReciever:unit -> unit)
            info =
                async {
                    let audioFocusRes = 
                        if (Build.VERSION.SdkInt > BuildVersionCodes.O) then
                            Dependencies.audioManager.RequestAudioFocus(Dependencies.audioFocusRequest service)
                        else
                            Dependencies.audioManager.RequestAudioFocus(service,Stream.Music,AudioFocus.Gain)
                    
                    match audioFocusRes with
                    | AudioFocusRequest.Failed ->
                        return stopPlayer {info with ResumeOnAudioFocus = false }
                    | AudioFocusRequest.Delayed ->
                        return stopPlayer {info with ResumeOnAudioFocus = true }
                    | AudioFocusRequest.Granted ->
                        match info.State with
                        | Stopped ->
                            registerNoisyHeadPhoneReciever ()
                            return! (playFile info)
                        | Playing ->
                            return info
                    | _ ->
                        Microsoft.AppCenter.Crashes.Crashes.TrackError(exn("whaaat? invalid audioFocusRequest Enum."))|> ignore
                        return info
                }
                

        let private playFile 
            (mediaPlayer:MediaPlayer)
            (onAfterPrepare:(unit->unit) option ref )  // muatble dependency or ref type some thing
            (updateTimer:System.Threading.Timer option ref )
            (updateTimerFunc:unit->unit)
            info =
                async {
                    let file = info.Filename
                    let position = info.Position
                    mediaPlayer.Reset()
                    do! mediaPlayer.SetDataSourceAsync(file) |> Async.AwaitTask 
                    onAfterPrepare := (Some (fun () -> 
                        mediaPlayer.SeekTo(position)

                        // set update timer only when it is not alread there
                        match updateTimer.Value with
                        | Some _ ->
                            ()
                        | None ->
                            updateTimer := 
                                Some (new System.Threading.Timer(
                                        (
                                            fun _ -> 
                                                updateTimerFunc()
                                        ),null,0,1000)
                                )
                    ))
                    mediaPlayer.PrepareAsync()
                    return info
                }

        let private updateInfo 
            (mediaSession:MediaSession)
            (info:AudioPlayerInfo) =
                async{
                    info |> Notification.updateNotification mediaSession 
                }
            

        let private registerNoisyHeadPhoneReciever 
            (noisyHeadPhoneReceiver:Receivers.NoisyHeadPhoneReceiver option byref) =
                match noisyHeadPhoneReceiver with
                | None -> 
                    noisyHeadPhoneReceiver <- Some ( new Receivers.NoisyHeadPhoneReceiver() )
                    let noiseHpIntentFilter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy)
                    Application.Context.RegisterReceiver(noisyHeadPhoneReceiver.Value, noiseHpIntentFilter) |> ignore
                | Some _ ->
                    ()


        let private unregisterNoisyHeadPhoneReciever 
            (noisyHeadPhoneReceiver:Receivers.NoisyHeadPhoneReceiver option byref) =
                match noisyHeadPhoneReceiver with
                | None -> ()
                | Some r ->
                    Application.Context.UnregisterReceiver(r)
                    noisyHeadPhoneReceiver <- None
                    ()

        let private onStopAudioPlayer 
            (mediaPlayer:MediaPlayer) 
            (unregisterNoisyHeadPhoneReciever:unit -> unit)
            (updateTimer:System.Threading.Timer option ref )
            info =
                match info.State with
                | Stopped ->
                    info
                | Playing  ->
                    mediaPlayer.Pause()
                    let newState =
                        { info with Position = mediaPlayer.CurrentPosition;Duration = mediaPlayer.Duration }                    
                    mediaPlayer.Stop()
                    updateTimer.Value |> Option.map(fun i -> i.Dispose()) |> ignore
                    updateTimer := None
                    unregisterNoisyHeadPhoneReciever ()
                    newState


        // stop media playing without state change and "overhead"
        let private internalMediaPlayerStop
            (mediaPlayer:MediaPlayer) =
                mediaPlayer.Stop()



        let private onChangeTrack
            (stopAudioPlayer:unit -> unit)
            (startAudioPlayer:AudioPlayerInfo -> Async<AudioPlayerInfo>)
            info =
                async {
                    match info.State with
                    | Playing ->
                        stopAudioPlayer ()
                        let! info = startAudioPlayer info
                        return info
                    | Stopped ->
                        return info
                }
            

            

        let private onSetPosition 
            (mediaPlayer:MediaPlayer)
            info =
                match info.State with
                | Playing ->
                    mediaPlayer.SeekTo(info.Position)
                | Stopped ->
                    ()
                info


        type AudioServiceImplementation(service:Services.AudioPlayerService) as self =
            let informationDispatcher =
                AudioPlayerState.InformationDispatcher.audioPlayerStateInformationDispatcher
            
            let stateMailbox = 
                audioPlayerStateMailbox
                    self
                    informationDispatcher
            
            let mutable mediaSession = Dependencies.createMediaSession ()
                
            let mutable onAfterPrepare:(unit->unit) option ref  = ref None

            let moveToNextTrackForMediaPlayer () =
                stateMailbox.Post(MoveToNextTrack)

            let mutable mediaPlayer:MediaPlayer = 
                Dependencies.initMediaPlayer moveToNextTrackForMediaPlayer
            let mutable noisyHeadPhoneReceiver:Receivers.NoisyHeadPhoneReceiver option = None
            let mutable updateTimer:System.Threading.Timer option ref  = ref None

            

            let updateInfo =
                updateInfo mediaSession

            let registerNoisyHeadPhoneReciever ()=
                registerNoisyHeadPhoneReciever &noisyHeadPhoneReceiver

            let unregisterNoisyHeadPhoneReciever ()=
                unregisterNoisyHeadPhoneReciever &noisyHeadPhoneReceiver

            let stopPlayer = 
                onStopAudioPlayer
                    mediaPlayer
                    unregisterNoisyHeadPhoneReciever
                    updateTimer

            let internalMediaPlayerStop () =
                internalMediaPlayerStop
                    mediaPlayer
            
            // send new Postion from mediaPlayer to state
            let updateTimerFunc () =
                            
                    // Send update position only when playing,                     
                    // this avoids "position" hobbing with invalid or zero values, when the player is stopped
                    if mediaPlayer.IsPlaying then
                        stateMailbox.Post(UpdatePositionExternal mediaPlayer.CurrentPosition)

            let playFile =
                playFile 
                    mediaPlayer
                    onAfterPrepare
                    updateTimer                    
                    updateTimerFunc
                    
                    


            interface IAudioServiceImplementation with

                member this.StartAudioService info =
                    mediaSession <- Dependencies.createMediaSession ()
                    onStartAudioService
                        service
                        mediaPlayer
                        mediaSession
                        onAfterPrepare
                        informationDispatcher
                        updateInfo
                        info

                member this.StopAudioService info =
                    onStopAudioService
                        service
                        &mediaPlayer
                        &mediaSession
                        stopPlayer
                        info

                member this.StartAudioPlayer info =
                    onStartAudioPlayer
                        stopPlayer
                        playFile
                        service
                        registerNoisyHeadPhoneReciever
                        info
                member this.StopAudioPlayer info =
                    onStopAudioPlayer
                        mediaPlayer
                        unregisterNoisyHeadPhoneReciever
                        updateTimer
                        info
                member this.MoveToNextTrack info =                    
                    onChangeTrack
                        internalMediaPlayerStop
                        playFile
                        info
                member this.MoveToPreviousTrack info =
                    onChangeTrack
                        internalMediaPlayerStop
                        playFile
                        info
                member this.SetPosition info =
                    onSetPosition
                        mediaPlayer
                        info

                member this.OnUpdatePositionNumber info =
                    info

                member this.StateMailbox =
                    stateMailbox



            

    
    
    module Services =

        [<Service>]
        type AudioPlayerService() as self =
            inherit Service()

            static let mutable instance = None

            let stateMailBox =
                (AudioPlayerFuntionImplementations.AudioServiceImplementation(self) :> IAudioServiceImplementation).StateMailbox
                       
            interface AudioManager.IOnAudioFocusChangeListener with
                member ___.OnAudioFocusChange(focusChange:AudioFocus) =
                    
                    let state = stateMailBox.PostAndReply((fun rc -> GetCurrentState rc),2000)

                    match focusChange with
                    | AudioFocus.Gain ->
                        if (state.PlaybackDelayed || state.ResumeOnAudioFocus) then                                    
                            stateMailBox.Post(StartAudioPlayer)                                    
                    | AudioFocus.Loss ->
                        stateMailBox.Post(StopAudioPlayer true)
                    | AudioFocus.LossTransient | AudioFocus.LossTransientCanDuck->
                        stateMailBox.Post(StopAudioPlayer false)
                    | _ ->
                        () 
            

            override this.OnCreate () =
                instance <- Some self
                ()

            override this.OnStartCommand(intent,_,_) =
                if intent.Action = null || intent.Action = "" then
                    StartCommandResult.Sticky
                else
                       
                    match intent.Action with
                    | x when x = START_SERVICE ->
                        let audiobook = intent.GetStringExtra("audiobook") |> Helpers.deserialize<Domain.AudioBook>
                        let mp3list = intent.GetStringExtra("mp3list") |> Helpers.deserialize<Mp3FileList>
                        stateMailBox.Post (StartAudioService (audiobook,mp3list))
                    | x when x = STOP_SERVICE ->
                        stateMailBox.Post(StopAudioService)
                    | x when x = START_PLAYER ->
                        let file = intent.GetStringExtra("file")
                        let pos = intent.GetIntExtra("pos",0)
                        if file = null || file = "" then
                            stateMailBox.Post(StartAudioPlayer)
                        else
                            stateMailBox.Post(StartAudioPlayerExtern (file,pos))
                    | x when x = STOP_PLAYER ->
                        stateMailBox.Post(StopAudioPlayer false)
                    | x when x = MOVEFORWARD_PLAYER ->
                        stateMailBox.Post(MoveToNextTrack)
                    | x when x = MOVEBACKWARD_PLAYER ->
                        stateMailBox.Post(MoveToPreviousTrack)
                    | x when x = SETPOSITION_PLAYER ->
                        let pos = intent.GetIntExtra("pos",0)
                        stateMailBox.Post(SetPosition pos)
                    | x when x = JUMP_FORWARD_PLAYER ->
                        stateMailBox.Post(JumpForward)
                    | x when x = JUMP_BACKWARD_PLAYER ->
                        stateMailBox.Post(JumpBackwards)
                    | x when x = GET_CURRENT_STATE_PLAYER ->
                        // get current state and send it to the current state broad caster
                        let state = stateMailBox.TryPostAndReply((fun rc -> GetCurrentState rc),2000)
                        match state with
                        | None -> ()
                        | Some state ->
                            let stateJson = state |> Helpers.serialize
                            let intent = new Intent(DecpencyService.ACTION_GET_CURRENT_STATE)
                            intent.PutExtra("currentstate",stateJson) |> ignore
                            this.SendBroadcast(intent)


                    

                    StartCommandResult.Sticky

            override this.OnBind _ =
                null

            override this.OnDestroy() =                
                ()

            member this.ComponentName = this.Class.Name;

            member this.Current = instance



        let initAudioService () =
            async {
                if Helpers.isMyServiceRunning typeof<AudioPlayerService> then
                    return true
                else
                    let startServiceIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)            
                    Android.App.Application.Context.StartService(startServiceIntent) |> ignore

                    // waiting until foreground service is registered            
                    // build an async sequence
                    let waitUntilServiceStarted =
                        asyncSeq {
                            for _ in [1..200] do
                                yield Helpers.isMyServiceRunning typeof<AudioPlayerService>
                                do! Async.Sleep 50
                        }

                    let! isStarted = waitUntilServiceStarted |> AsyncSeq.exists (fun i -> i)
                    return isStarted
            }
    
    
    
    
    
    module DecpencyService =
    
        open Services
        
        let ACTION_GET_CURRENT_STATE:string = "perry.rhodan.audio.get.current.state"

        [<IntentFilter([|"perry.rhodan.audio.get.current.state"|])>]
        type CurrentStateBroadcastReceiver() = 
            inherit BroadcastReceiver()

            let mutable handler = None

            override this.OnReceive(_,intent) =
                match intent.Action with
                | x when x = ACTION_GET_CURRENT_STATE ->
                    match handler with
                    | Some handler ->
                        let info = intent.GetStringExtra("currentstate")
                        handler(info)
                    | None ->
                        ()
                    
                | _ ->
                    ()

            member this.Handler 
                with get() = handler
                and set p = handler <- p
        

        type AudioPlayer() =
            let mutable started = false

            let currentStateReciever =                
                new CurrentStateBroadcastReceiver()

            do 
                Android.App.Application.Context.RegisterReceiver(currentStateReciever,new IntentFilter(ACTION_GET_CURRENT_STATE)) |> ignore


            interface AudioPlayerState.IAudioPlayer with

                member this.RunService audiobook mp3list =
                    let jsonAudioBook = audiobook |> Helpers.serialize
                    let jsonMp3List = mp3list |> Helpers.serialize
                    let input = [
                        ("audiobook",jsonAudioBook)
                        ("mp3list",jsonMp3List)
                    ]
                    ServiceActions.START_SERVICE
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> input []
                    started <- true
                member this.StopService () =
                    
                    ServiceActions.STOP_SERVICE
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> [] []
                    started <- false
                member this.StartAudio file pos =
                    let inputStr = [ ("file",file) ]
                    let inputInt = [ ("pos",pos) ]
                    ServiceActions.START_PLAYER
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> inputStr inputInt
                    
                member this.StopAudio () =                    
                    ServiceActions.STOP_PLAYER
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> [] []
                member this.TogglePlayPause () =
                    ServiceActions.TOGGLE_PLAYPAUSE
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> [] []
                member this.MoveForward () =
                    ServiceActions.MOVEFORWARD_PLAYER
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> [] []
                member this.MoveBackward () =
                    ServiceActions.MOVEBACKWARD_PLAYER
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> [] []
                member this.GotToPosition pos =
                    let input = [ ("pos",pos) ]
                    ServiceActions.SETPOSITION_PLAYER
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> [] input
                member this.JumpForward () =
                    ServiceActions.JUMP_FORWARD_PLAYER
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> [] []
                member this.JumpBackward () =
                    ServiceActions.JUMP_BACKWARD_PLAYER
                    |> Helpers.sendCommandToService typeof<AudioPlayerService> [] []
                member this.GetCurrentState() =
                    if not started then
                        // no state, when service not started jet
                        None |> async.Return
                    else
                        let tcs = new System.Threading.Tasks.TaskCompletionSource<AudioPlayerInfo option>()
                        let jsonCallBack json =
                            let state = 
                                if json = null || json = "" then
                                    None
                                else
                                    Some (json |> Helpers.deserialize<AudioPlayerInfo>)
                                
                            tcs.TrySetResult(state) |> ignore
                        
                        currentStateReciever.Handler <- Some jsonCallBack
                        ServiceActions.GET_CURRENT_STATE_PLAYER
                        |> Helpers.sendCommandToService typeof<AudioPlayerService> [] []

                        // timeout after 3 seconds
                        System.Threading.Tasks.Task.Delay(3000).ContinueWith( fun _ -> tcs.TrySetResult(None)) |> ignore

                        tcs.Task |> Async.AwaitTask


                

