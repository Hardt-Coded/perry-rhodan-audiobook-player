namespace PerryRhodan.AudiobookPlayer.Android

module rec AudioPlayerService =

    open Android.App
    open Android.OS
    open Android.Media
    open Android.Media.Session
    open Android.Content
    
    open Android.Runtime
    open Android.Views
    

    open Xamarin.Forms.Platform.Android      
    open Android.Support.V4.Media.Session
    open Android.Support.V4.Media
    open Android.Support.V4.App

    open Microsoft.AppCenter.Crashes
    
    
    open Services

    open Android.Util
    open Newtonsoft.Json
    open Global
    open Domain
    open AudioPlayerState
    
    
    let bla = 1

    //type NoisyHeadPhoneReceiver(audioPlayer:AudioPlayerService) =
    //    inherit BroadcastReceiver()

    //    override this.OnReceive (context, intent) =
    //        if (intent.Action = AudioManager.ActionAudioBecomingNoisy) then
    //            try
    //                audioPlayer.Stop()
    //                ()
    //            with
    //            | ex ->
    //                Crashes.TrackError(ex)
                

    //let SERVICE_RUNNING_NOTIFICATION_ID = 10401
    //let RHODAN_CHANNEL_ID = "pr_player_aud_notification_20190221"
    //let SERVICE_STARTED_KEY = "has_service_been_started";
    //let BROADCAST_MESSAGE_KEY = "broadcast_message";
    //let NOTIFICATION_BROADCAST_ACTION = "PerryRhodan.Notification.Action"
    //let DELAY_BETWEEN_LOG_MESSAGES = 1000L
    
    //let ACTION_START_SERVICE = "PerryRhodan.action.START_SERVICE"
    //let ACTION_STOP_SERVICE = "PerryRhodan.action.STOP_SERVICE"
    //let ACTION_STOP_PLAYER = "PerryRhodan.action.STOP_PLAYER"
    //let ACTION_START_PLAYER = "PerryRhodan.action.START_PLAYER"
    //let ACTION_TOGGLE_PLAYPAUSE = "PerryRhodan.action.TOGGLE_PLAYPAUSE"
    //let ACTION_MOVEFORWARD_PLAYER = "PerryRhodan.action.MOVE_FORWARD"
    //let ACTION_MOVEBACKWARD_PLAYER = "PerryRhodan.action.MOVE_BACKWARD"
    //let ACTION_SETPOSITION_PLAYER = "PerryRhodan.action.SET_POSITION"
    //let ACTION_JUMP_FORWARD_PLAYER = "PerryRhodan.action.JUMP_FORWARD"
    //let ACTION_JUMP_BACKWARD_PLAYER = "PerryRhodan.action.JUMP_BACKWARD"
    //let ACTION_UPDATE_METADATA_PLAYER = "PerryRhodan.action.UPDATE_METADATA"



    //let ACTION_MAIN_ACTIVITY = "PerryRhodan.action.MAIN_ACTIVITY";

    //let icon name = 
    //    typeof<Resources.Drawable>.GetField(name).GetValue(null) :?> int

    //type AndroidDrawable = Android.Resource.Drawable


    //[<BroadcastReceiver>]
    //[<IntentFilter([| Intent.ActionMediaButton |])>]
    //type RemoteControlBroadcastReceiver() =
    //    inherit BroadcastReceiver()
            
    //        override this.OnReceive(context,intent) =
    //            if intent.Action <> Intent.ActionMediaButton then
    //                ()
    //            else
    //                let key = intent.GetParcelableExtra(Intent.ExtraKeyEvent) :?> KeyEvent
    //                if (key.Action <> KeyEventActions.Down) then
    //                    ()
    //                else
    //                    let action = 
    //                        match key.KeyCode with
    //                        | Keycode.Headsethook | Keycode.MediaPlayPause ->
    //                            ACTION_TOGGLE_PLAYPAUSE
    //                        | Keycode.MediaPlay ->
    //                            ACTION_START_PLAYER
    //                        | Keycode.MediaPause -> 
    //                            ACTION_STOP_PLAYER
    //                        | Keycode.MediaStop -> 
    //                            ACTION_STOP_PLAYER
    //                        | Keycode.MediaNext -> 
    //                            ACTION_MOVEFORWARD_PLAYER
    //                        | Keycode.MediaPrevious -> 
    //                            ACTION_MOVEBACKWARD_PLAYER
                           
    //                    let remoteIntent = new Intent(action);
    //                    context.StartService(remoteIntent) |> ignore

    //    member this.ComponentName = this.Class.Name;

                



    //[<Service>]
    //type AudioPlayerService() as self=
    //    inherit Service()
       
    //    static let mutable instance:AudioPlayerService option = None

    //    let mutable currentAudioBook:Domain.AudioBook option = None
    //    let mutable currentMp3ListWithDuration: (string * int) list = []
    //    let mutable currentAudioplayInfo:AudioPlayerInfo option = None
        
    //    // other stuff
    //    let mutable noisyHeadPhoneReceiver = None
    //    let mutable mediaActionReceiver = None

    //    //  delegates
    //    let mutable onInfo = None
    //    let mutable onAfterPrepare = None
    //    let mutable onUpdateState = None

    //    let mutable updateTimer = None

    //    // other stuff
    //    let mutable isStarted = false
    //    let mutable handler:Handler = null


    //    let mutable audioManager :AudioManager = null
    //    let mutable mediaSessionCompat:MediaSession = null
    //    let mutable mediaControllerCompat:MediaController = null


    //    let jumpDistance =
    //        30000

    //    let addToCurrentPos x =
    //        currentAudioplayInfo
    //        |> Option.map (fun i -> i.Position + x)
    //        |> Option.defaultValue 0
            
    
    //    let getIndexForFile file =
    //        currentMp3ListWithDuration |> List.findIndex (fun (name,_) -> name = file)


    //    let getFileFromIndex idx =
    //        let idx =
    //            if idx < 0 then 0
    //            elif idx > (currentMp3ListWithDuration.Length - 1) then (currentMp3ListWithDuration.Length - 1)
    //            else idx

    //        currentMp3ListWithDuration.[idx]


    //    let buildNotification () =            
    //        let icon = icon "pr_small_icon"
    //        let title = 
    //            currentAudioBook 
    //            |> Option.map (fun ab -> ab.FullName)
    //            |> Option.defaultValue "No Title!"
            
    //        mediaSessionCompat.Active <- true
    //        mediaSessionCompat.SetFlags(MediaSessionFlags.HandlesMediaButtons||| MediaSessionFlags.HandlesTransportControls)
           
    //        let style = new Notification.MediaStyle();
    //        style.SetMediaSession(mediaSessionCompat.SessionToken) |> ignore

    //        let currentInfo = 
    //            currentAudioplayInfo                
    //            |> Option.defaultValue AudioPlayerInfo.Empty
                

    //        let currenInfoString = 
    //            sprintf "%i - %s / %s"
    //                currentInfo.CurrentTrackNumber
    //                ((currentInfo.Position |> Common.TimeSpanHelpers.toTimeSpan).ToString("hh\:mm\:ss"))
    //                ((currentInfo.Duration |> Common.TimeSpanHelpers.toTimeSpan).ToString("hh\:mm\:ss"))
            
    //        let notify = 
    //            let builder =
    //                if Build.VERSION.SdkInt < BuildVersionCodes.O then
    //                    (new Notification.Builder(self))
    //                else
    //                    (new Notification.Builder(self,RHODAN_CHANNEL_ID))
                        
    //            builder
    //                .SetStyle(style)                    
    //                .SetContentTitle(title)
    //                .SetContentText(currenInfoString)
    //                .SetSmallIcon(icon)                    
    //                .SetContentIntent(self.BuildIntentToShowMainActivity())
    //                .SetOngoing(true)                    
    //                .AddAction(self.BuildBackwardAudioAction())
    //                .AddAction(self.BuildStartStopToggleAction())
    //                .AddAction(self.BuildForwardAudioAction())

    //        try
    //            let albumPicFile = 
    //                currentAudioBook
    //                |> Option.bind (fun i -> i.Thumbnail)
    //                |> Option.defaultValue "@drawable/AudioBookPlaceholder_Dark.png"

    //            let albumPic = Android.Graphics.BitmapFactory.DecodeFile(albumPicFile)
    //            notify.SetLargeIcon(albumPic) |> ignore
    //        with
    //        | _ -> ()

    //        style.SetShowActionsInCompactView(0, 1, 2) |> ignore
    //        notify.Build()


    //    let updateNotification () =
    //        let notify = buildNotification ()
    //        NotificationManager.FromContext(Android.App.Application.Context).Notify(SERVICE_RUNNING_NOTIFICATION_ID,notify)
        

    //    let storeCurrentAudiobookState () =
    //        match currentAudioplayInfo, currentAudioBook with
    //        | Some info, Some ab ->
    //            let abPos = { Filename = info.Filename; Position = info.Position |> Common.TimeSpanHelpers.toTimeSpan }
    //            let newAb = {ab with State = {ab.State with CurrentPosition = Some abPos; LastTimeListend = Some System.DateTime.UtcNow } }
    //            let res = (Services.FileAccess.updateAudioBookInStateFile newAb) |> Async.RunSynchronously
    //            match res with
    //            | Error e ->
    //                Log.Error(AudioPlayerService.TAG,"narf pos nicht gespeichert! Msg:" + e) |> ignore                            
    //            | Ok () ->
    //                ()
    //        | _, _ -> ()
            
        
    //    let mutable updateInfoRunning = false

    //    let updateInfo (mediaplayer:MediaPlayer) =
    //        updateInfoRunning <- true
    //        match instance with
    //        | None -> 
    //            ()
    //        | Some _ ->
    //            Log.Debug(AudioPlayerService.TAG, "send info update ...") |> ignore
    //            match onInfo,currentAudioplayInfo,currentAudioBook with
    //            | Some onInfo, Some info, Some ab ->
    //                if mediaplayer.IsPlaying then
    //                    let currentPos = mediaplayer.CurrentPosition
    //                    let info = {info with Position = currentPos }
    //                    currentAudioplayInfo <- Some info 
    //                    // Save state on audio book
    //                    let newPos = info.Position |> Common.TimeSpanHelpers.toTimeSpan 
    //                    if newPos.Seconds = 5 || newPos.Seconds = 0 then
    //                        storeCurrentAudiobookState ()
                            
    //                    onInfo(info)
    //                else
    //                    onInfo(info)
    //            | _, _, _ ->
    //                ()
    //            // update mini player notification
    //            updateNotification ()
    //        updateInfoRunning <- false

        

    //    let sendUpdate mediaplayer =
    //        let i = new Intent(NOTIFICATION_BROADCAST_ACTION);
    //        Android.Support.V4.Content.LocalBroadcastManager.GetInstance(Android.App.Application.Context).SendBroadcast(i) |> ignore
    //        // arg sometimes not so ice with the Action-Stuff
    //        let action = new System.Action(fun () -> updateInfo mediaplayer)
    //        handler.Post(action) |> ignore
    //        ()


    //    let playNextTrackWithPos pos =
    //        match currentAudioplayInfo with
    //        | None ->
    //            ()
    //        | Some info ->            
    //            c

    //            let newIndex = index + 1

    //            if newIndex > (currentMp3ListWithDuration.Length - 1) then
    //                // stop the hole thing and write the compele state to db
    //                self.Stop()
                    
    //                match currentAudioBook with
    //                | None -> ()
    //                | Some ab ->
    //                    let newAb = {ab with State = {ab.State with LastTimeListend = Some System.DateTime.UtcNow; Completed = true } }
    //                    let res = (Services.FileAccess.updateAudioBookInStateFile newAb) |> Async.RunSynchronously
    //                    match res with
    //                    | Error e ->
    //                        Log.Error(AudioPlayerService.TAG,"narf pos nicht gespeichert! Msg:" + e) |> ignore                            
    //                    | Ok () ->
                                

    //                ()
    //            else
    //                let (newFile,newDuration) = newIndex |> getFileFromIndex 
    //                let newState = {info with Filename = newFile; Duration = newDuration; Position = pos; CurrentTrackNumber = newIndex + 1}
                    
    //                currentAudioplayInfo <- Some newState
    //                sendUpdate self.MediaPlayer                    
    //                if info.State = Playing then
                        
    //                    self.StopBase ()
    //                    self.PlayFile newFile pos |> Async.RunSynchronously
    //                ()
        
    //    let playNextTrack () =
    //        playNextTrackWithPos 0



    //    let playPreviousTrackWithPos pos =
    //        match currentAudioplayInfo with
    //        | None ->
    //            ()
    //        | Some info ->            
    //            let index =
    //                info.Filename
    //                |> getIndexForFile

    //            let newIndex = 
    //                index - 1

    //            // check if index okay in get file function
    //            let (newFile,newDuration) = newIndex |> getFileFromIndex 
    //            let newState = {info with Filename = newFile; Duration = newDuration; Position = pos; CurrentTrackNumber = newIndex + 1}
    //            currentAudioplayInfo <- Some newState
    //            sendUpdate self.MediaPlayer

    //            if info.State = Playing then
    //                self.StopBase ()
    //                self.PlayFile newFile pos |> Async.RunSynchronously
    //            ()


    //    let playPreviousTrack () =
    //        playPreviousTrackWithPos 0


    //    let seekToPos (mediaplayer:MediaPlayer) ms =
    //        match currentAudioplayInfo with
    //        | None ->
                
    //            ()
    //        | Some info ->

    //            let setPosOnCurrentTrack (apinfo:AudioPlayerInfo) pos =
    //                if apinfo.State = Playing then
    //                    mediaplayer.SeekTo(pos)
    //                let newState = {apinfo with Position = pos;}
    //                currentAudioplayInfo <- Some newState
    //                sendUpdate mediaplayer
                
    //            // when your new pos is actually on the next track
    //            if ms > info.Duration then
    //                let diff = ms - info.Duration
    //                playNextTrackWithPos diff
    //            // when you new position is actually on the previous track
    //            elif ms < 0 then
    //                let (file,durationPrevTrack) = getFileFromIndex (info.CurrentTrackNumber - 2)
    //                // are we already on the first track
    //                if file = info.Filename then 
    //                    setPosOnCurrentTrack info 0
    //                else
    //                    let posPrevTrack = durationPrevTrack + ms
    //                    playPreviousTrackWithPos posPrevTrack
    //            // no edge case                        
    //            else
    //                setPosOnCurrentTrack info ms

    //    let mutable mediaPlayer:MediaPlayer = null
    //    let mutable playbackAttributes:AudioAttributes = null
    //    let mutable audioFocusRequest:AudioFocusRequestClass = null
    //    let mutable playbackDelayed = false
    //    let mutable resumeOnAudioFocus = false

    //    let initMediaPlayer () = 
    //        audioManager <- self.GetSystemService(Android.Content.Context.AudioService) :?> AudioManager            
    //        playbackAttributes <- 
    //            (new AudioAttributes.Builder())
    //                .SetUsage(AudioUsageKind.Media)
    //                .SetContentType(AudioContentType.Music)
    //                .Build()
            
    //        let audioFocusOnChange = self :> AudioManager.IOnAudioFocusChangeListener

    //        if (Build.VERSION.SdkInt > BuildVersionCodes.O) then
    //            audioFocusRequest <- 
    //                (new AudioFocusRequestClass.Builder(AudioFocus.Gain))
    //                    .SetAudioAttributes(playbackAttributes)
    //                    .SetAcceptsDelayedFocusGain(true)
    //                    .SetWillPauseWhenDucked(true)
    //                    .SetOnAudioFocusChangeListener(audioFocusOnChange)                    
    //                    .Build()

                
                    
            
    //        mediaPlayer <- new MediaPlayer()
    //        mediaPlayer.SetAudioAttributes(playbackAttributes)
    //        mediaPlayer.SetWakeMode(Application.Context, WakeLockFlags.Partial);
        
    //        mediaPlayer.Completion.Add(
    //            fun _ -> 
    //                playNextTrack ()
    //                ()
    //        )

    //        mediaPlayer.Prepared.Add(
    //            fun _ ->                 
    //                match onAfterPrepare with
    //                | None -> ()
    //                | Some cmd -> cmd()
    //                mediaPlayer.Start()    
    //                updateNotification ()
    //                storeCurrentAudiobookState ()
    //        )  

    //        ()


        
    //    let registerNoisyHeadPhoneReciever () =
    //        match noisyHeadPhoneReceiver with
    //        | None -> 
    //            noisyHeadPhoneReceiver <- Some ( new NoisyHeadPhoneReceiver(self) )
    //            let noiseHpIntentFilter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy)
    //            Application.Context.RegisterReceiver(noisyHeadPhoneReceiver.Value, noiseHpIntentFilter) |> ignore
    //        | Some _ ->
    //            ()


    //    let unregisterNoisyHeadPhoneReciever () =
    //        match noisyHeadPhoneReceiver with
    //        | None -> ()
    //        | Some r ->
    //            Application.Context.UnregisterReceiver(r)
    //            noisyHeadPhoneReceiver <- None
    //            ()

    //    let registerMediaActionReciever () =
    //        ()


    //    let unregisterMediaActionReciever () =
    //        ()


    //    let createNotificationChannel () =
    //        if Build.VERSION.SdkInt < BuildVersionCodes.O then
    //            ()
    //        else
    //            let name = "PerryRhodanNotifyChannel"
    //            let description = "AudioPlayer Desc"
    //            let channel = new NotificationChannel(RHODAN_CHANNEL_ID,name,NotificationImportance.Default, Description = description)
    //            channel.SetSound(null,null)
    //            let notificationManager = Android.App.Application.Context.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
    //            notificationManager.CreateNotificationChannel(channel)
    //            ()



    //    /// ***************************
    //    /// Service Command function 
    //    /// ***************************

    //    let actionStartService (intent:Intent) =
    //        registerMediaActionReciever () 
    //        let json = intent.GetStringExtra("abdata")
    //        let jsonList = intent.GetStringExtra("fileList")

    //        if json = "" || jsonList = "" then 
    //            ()
    //        else
    //            let ab = JsonConvert.DeserializeObject<Domain.AudioBook>(json)
                
    //            match ab.State.DownloadedFolder with
    //            | None ->
    //                ()
    //            | Some folder ->                
    //                let mp3List = JsonConvert.DeserializeObject<(string * int)[]>(jsonList) |> Array.toList
    //                currentAudioBook <- Some ab
    //                currentMp3ListWithDuration <- mp3List  
                        
    //                if not isStarted then
    //                    mediaSessionCompat <- new MediaSession(Android.App.Application.Context, "PerryRhodanAudioBookPlayer")
    //                    mediaControllerCompat <- mediaSessionCompat.Controller        
    //                    self.RegisterForegroundService()
    //                    let action = new System.Action(fun () -> updateInfo mediaPlayer)
    //                    handler.PostDelayed(action, DELAY_BETWEEN_LOG_MESSAGES) |> ignore
    //                    isStarted <- true


    //    let actionStopService () =
    //        if mediaPlayer.IsPlaying then
    //            self.Stop()

    //        Log.Debug(AudioPlayerService.TAG,"on Stop Service called") |> ignore
    //        self.StopForeground(true)
    //        self.StopSelf()       
    //        unregisterMediaActionReciever () 
    //        try
    //            let notificationManager = self.GetSystemService(Android.Content.Context.NotificationService) :?> NotificationManager
    //            notificationManager.Cancel(SERVICE_RUNNING_NOTIFICATION_ID)
    //            mediaSessionCompat.Dispose()
    //        with
    //        | _ as ex -> Microsoft.AppCenter.Crashes.Crashes.TrackError(ex)
    //        isStarted <- false

    //    let playNow () =
    //        match currentAudioplayInfo with
    //        | Some info ->
    //            match info.State with
    //            | Stopped ->
    //                onUpdateState |> Option.map (fun i -> i(Playing)) |> ignore
                
    //                self.PlayFile info.Filename info.Position |> Async.RunSynchronously
    //                currentAudioplayInfo <- Some { info with State = Playing }
    //                // really ?
    //                updateInfo mediaPlayer

    //            | Playing ->
    //                ()
    //        | _ ->
    //            Log.Debug(AudioPlayerService.TAG,"Nope!") |> ignore
    //            ()


    //    let startPlayer infoJson =
    //        if infoJson <> "" then
    //            let info = JsonConvert.DeserializeObject<AudioPlayerInfo>(infoJson)
    //            currentAudioplayInfo <- Some info
    //            // really ?
    //            updateInfo mediaPlayer

    //        resumeOnAudioFocus <- true

    //        let audioFocusOnChange = self :> AudioManager.IOnAudioFocusChangeListener

    //        let audioFocusRes = 
    //            if (Build.VERSION.SdkInt > BuildVersionCodes.O) then
    //                audioManager.RequestAudioFocus(audioFocusRequest)
    //            else
    //                audioManager.RequestAudioFocus(audioFocusOnChange,Stream.Music,AudioFocus.Gain)
    //        match audioFocusRes with
    //        | AudioFocusRequest.Failed ->
    //            playbackDelayed <- false
    //        | AudioFocusRequest.Granted ->
    //            playbackDelayed <- false
    //            playNow ()
    //        | AudioFocusRequest.Delayed ->
    //            playbackDelayed <- true
    //        | _ ->
    //            Log.Wtf(AudioPlayerService.TAG,"whaaat? invalid audioFocusRequest Enum.") |> ignore


    //    let actionStartPlayer (intent:Intent) =
    //        Log.Debug(AudioPlayerService.TAG,"player start") |> ignore
    //        let infoJson = intent.GetStringExtra("info")
    //        // if empty than the play comes from the foreground service and not the the app
    //        infoJson |> startPlayer


    //    let actionStopPlayer () =
    //        Log.Debug(AudioPlayerService.TAG,"player stop") |> ignore
    //        self.Stop ()
    //        onUpdateState |> Option.map (fun i -> i(Stopped)) |> ignore
    //        currentAudioplayInfo <- currentAudioplayInfo |> Option.map (fun info -> { info with State = Stopped })
    //        updateInfo mediaPlayer
        

    //    let actionTogglePlayPause (intent:Intent) =
    //        currentAudioplayInfo
    //        |> Option.map (
    //            fun i ->
    //                match i.State with
    //                | Stopped ->
    //                    actionStartPlayer intent
    //                | Playing ->
    //                    actionStopPlayer()
    //        ) |> ignore


    //    let actionMoveForward () =
    //        Log.Debug(AudioPlayerService.TAG,"move forward") |> ignore
    //        playNextTrack ()
    //        ()


    //    let actionMoveBackward () =
    //        Log.Debug(AudioPlayerService.TAG,"move backward") |> ignore
    //        match currentAudioplayInfo with
    //        | None -> 
    //            Crashes.TrackError(exn("AudioPlayerService: moveBack without current audio play info"))
    //            ()
    //        | Some info -> 
    //            if (info.Position < 2000) then
    //                playPreviousTrack ()
    //            else
    //                0 |> seekToPos mediaPlayer 
                

    //    let actionSetPosition (intent:Intent) =
    //        let newPos = intent.GetIntExtra("pos",0)
    //        Log.Debug(AudioPlayerService.TAG,sprintf "seek to %i ms" newPos) |> ignore
    //        newPos |> seekToPos mediaPlayer

        
    //    let actionJumpForward () =
    //        let newPos = addToCurrentPos (jumpDistance)
    //        Log.Debug(AudioPlayerService.TAG,sprintf "jump forward to %i ms" newPos) |> ignore
    //        newPos |> seekToPos mediaPlayer


    //    let actionJumpBackward () =            
    //        let newPos = addToCurrentPos (jumpDistance * -1)
    //        Log.Debug(AudioPlayerService.TAG,sprintf "jump backward to %i ms" newPos) |> ignore
    //        newPos |> seekToPos mediaPlayer
            


    //    let actionUpdateMetadata (intent:Intent) =
    //        let json =  intent.GetStringExtra("audiobook")                
    //        let ab = JsonConvert.DeserializeObject<Domain.AudioBook>(json)
    //        currentAudioBook <- Some ab
    //        match ab.State.DownloadedFolder with
    //        | None ->
    //            ()
    //        | Some folder ->                
    //            let mp3List = folder |> Services.Files.getMp3FileList |> Async.RunSynchronously                    
    //            currentMp3ListWithDuration <- mp3List



        
    //    interface AudioManager.IOnAudioFocusChangeListener with

    //        member ___.OnAudioFocusChange([<GeneratedEnum>]focusChange:AudioFocus) =
    //            match focusChange with
    //            | AudioFocus.Gain ->
    //                if (playbackDelayed || resumeOnAudioFocus) then
    //                    playbackDelayed <- false
    //                    resumeOnAudioFocus <- false
    //                    playNow ()
    //                    mediaPlayer.SetVolume(1. |> float32,1. |> float32)
    //            | AudioFocus.Loss ->
    //                playbackDelayed <- false
    //                resumeOnAudioFocus <- false
    //                actionStopPlayer ()
    //            | AudioFocus.LossTransient | AudioFocus.LossTransientCanDuck->
    //                playbackDelayed <- false
    //                resumeOnAudioFocus <- mediaPlayer.IsPlaying
    //                actionStopPlayer ()
    //            | _ ->
    //                ()
                


    //    static member TAG = typeof<AudioPlayer>.FullName

    //    static member Current
    //        with get () = instance

    //    static member IsStarted
    //        with get () = 
    //            instance |> Option.map (fun i -> i.IsRunning) |> Option.defaultValue false

        
    //    member this.IsRunning
    //        with get () = 
    //            isStarted

        
    //    member this.OnInfo 
    //        with get () = onInfo
    //        and set p = onInfo <- p

    //    member this.OnUpdateState 
    //        with get () = onUpdateState
    //        and set p = onUpdateState <- p


    //    member this.MediaPlayer 
    //        with get() = mediaPlayer

    //    member this.CurrentInfo 
    //        with get () = currentAudioplayInfo

    //    member this.CurrentAudiobook 
    //        with get () = currentAudioBook

    //    member this.PlayFile (file:string) position =
    //        async {
    //            registerNoisyHeadPhoneReciever ()                
    //            // stop info sending until the playing is full ready                
    //            mediaPlayer.Reset()
    //            do! mediaPlayer.SetDataSourceAsync(file) |> Async.AwaitTask
    //            onAfterPrepare <- Some (fun () -> 
    //                mediaPlayer.SeekTo(position)
    //                updateInfo mediaPlayer
    //                updateTimer <- Some (new System.Threading.Timer((fun _ -> updateInfo mediaPlayer),null,0,1000))
    //            )
    //            mediaPlayer.PrepareAsync()
    //            return ()

    //        }
        
    //    member this.StopBase () =
    //        if (mediaPlayer.IsPlaying) then
    //            mediaPlayer.Pause()                
            
    //        updateTimer |> Option.map (fun t -> t.Dispose()) |> ignore
    //        updateTimer <- None
    //        let newInfo = 
    //            currentAudioplayInfo 
    //            |> Option.map (fun i -> {i with Position = mediaPlayer.CurrentPosition })
    //        currentAudioplayInfo <- newInfo
    //        mediaPlayer.Stop()

    //    member this.Stop () =
    //        this.StopBase()
    //        unregisterNoisyHeadPhoneReciever ()
    //        updateNotification ()
    //        storeCurrentAudiobookState ()
    //        ()


    //    member this.RegisterForegroundService() =
    //        createNotificationChannel ()
            
    //        let notify =
    //            buildNotification ()

    //        this.StartForeground(SERVICE_RUNNING_NOTIFICATION_ID, notify);

    //        ()

    //    member this.BuildIntentToShowMainActivity () =
    //        let notificationIntent = new Intent(this, typeof<FormsAppCompatActivity>)
    //        notificationIntent.SetAction(ACTION_MAIN_ACTIVITY) |> ignore
    //        notificationIntent.SetFlags(ActivityFlags.SingleTop ||| ActivityFlags.ClearTask)  |> ignore
    //        notificationIntent.PutExtra(SERVICE_STARTED_KEY, true)  |> ignore
    //        PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent)


    //    member this.BuildStartStopToggleAction () =
    //        if mediaPlayer.IsPlaying then
    //            this.BuildStopAudioAction ()
    //        else
    //            this.BuildStartAudioAction ()


    //    member this.BuildStartAudioAction () =
    //        let icon = AndroidDrawable.IcMediaPlay
    //        let intent = new Intent(this, this.GetType());
    //        intent.SetAction(ACTION_START_PLAYER) |> ignore
    //        intent.PutExtra("info","") |> ignore   
    //        intent.PutExtra("fileList","") |> ignore   
            
    //        let startAudioPendingIntent = PendingIntent.GetService(this, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", startAudioPendingIntent)
    //        builder.Build();

    //    member this.BuildStopAudioAction () =
    //        let icon = AndroidDrawable.IcMediaPause
    //        let intent = new Intent(this, this.GetType());
    //        intent.SetAction(ACTION_STOP_PLAYER) |> ignore
    //        let stopAudioPendingIntent = PendingIntent.GetService(this, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
    //        builder.Build();

    //    member this.BuildForwardAudioAction () =
    //        let icon = AndroidDrawable.IcMediaFf
    //        let intent = new Intent(this, this.GetType());
    //        intent.SetAction(ACTION_JUMP_FORWARD_PLAYER) |> ignore            
    //        let stopAudioPendingIntent = PendingIntent.GetService(this, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
    //        builder.Build();

    //    member this.BuildBackwardAudioAction () =
    //        let icon = AndroidDrawable.IcMediaRew
    //        let intent = new Intent(this, this.GetType());

    //        // ACTION_SETPOSITION_PLAYER
    //        intent.SetAction(ACTION_JUMP_BACKWARD_PLAYER) |> ignore                        
    //        let stopAudioPendingIntent = PendingIntent.GetService(this, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", stopAudioPendingIntent)
            
    //        builder.Build();

    //    member this.BuildStopServiceAction () =
    //        let icon = icon "settings_icon"
    //        let intent = new Intent(this, this.GetType());
    //        intent.SetAction(ACTION_STOP_SERVICE) |> ignore
    //        let startAudioPendingIntent = PendingIntent.GetService(this, 0, intent, PendingIntentFlags.UpdateCurrent)
            
    //        let builder = 
    //            new Notification.Action.Builder(icon, "", startAudioPendingIntent)
            
    //        builder.Build();


    //    override this.OnCreate () =
    //        base.OnCreate()
    //        Log.Debug(AudioPlayerService.TAG,"init audio service") |> ignore
    //        instance <- Some this
    //        handler <- new Handler()
    //        initMediaPlayer ()
            
            



    //    override this.OnStartCommand(intent,_,_) =
    //        match intent.Action with
    //        | x when x = ACTION_START_SERVICE ->
    //            actionStartService intent
    //        | x when x = ACTION_STOP_SERVICE ->
    //            actionStopService ()
    //        | x when x = ACTION_START_PLAYER ->
    //            actionStartPlayer intent
    //        | x when x = ACTION_STOP_PLAYER ->
    //            actionStopPlayer ()
    //        | x when x = ACTION_TOGGLE_PLAYPAUSE ->
    //            actionTogglePlayPause intent
    //        | x when x = ACTION_MOVEFORWARD_PLAYER ->
    //            actionMoveForward ()
    //        | x when x = ACTION_MOVEBACKWARD_PLAYER ->
    //            actionMoveBackward ()
    //        | x when x = ACTION_SETPOSITION_PLAYER ->
    //            actionSetPosition intent
    //        | x when x = ACTION_JUMP_FORWARD_PLAYER ->
    //            actionJumpForward ()
    //        | x when x = ACTION_JUMP_BACKWARD_PLAYER ->
    //            actionJumpBackward ()
    //        | x when x = ACTION_UPDATE_METADATA_PLAYER ->
    //            actionUpdateMetadata intent


    //        StartCommandResult.Sticky

    //    override this.OnBind _ =
    //        null

    //    override this.OnDestroy() =
    //        ()



    //let sendCommandToService (servicetype:System.Type) additional command=
    //    let intent = new Intent(Android.App.Application.Context,servicetype)
    //    intent.SetAction(command) |> ignore
    //    additional intent
    //    let pendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, intent, PendingIntentFlags.UpdateCurrent)
    //    pendingIntent.Send()



    //type AudioPlayer() =

    //    let mutable isStarted = false

    //    interface DependencyServices.IAudioPlayer with
        
    //        member this.OnInfo 
    //            with get () = 
    //                AudioPlayerService.Current
    //                |> Option.bind (fun i -> i.OnInfo)

    //            and set p = 
    //                match AudioPlayerService.Current with
    //                | None -> ()
    //                | Some aps ->
    //                    aps.OnInfo <- p
                    
                        

    //        member this.OnUpdateState 
    //            with get () = 
    //                AudioPlayerService.Current
    //                |> Option.bind (fun i -> i.OnUpdateState)

    //            and set p = 
    //                match AudioPlayerService.Current with
    //                | None -> ()
    //                | Some aps ->
    //                    aps.OnUpdateState <- p
                


    //        member this.CurrentInfo 
    //            with get () = 
    //                AudioPlayerService.Current 
    //                |> Option.bind (fun i -> i.CurrentInfo)
                

    //        member this.CurrentAudiobook 
    //            with get () = 
    //                AudioPlayerService.Current 
    //                |> Option.bind (fun i -> i.CurrentAudiobook)


    //        member this.IsStarted 
    //            with get () = isStarted
            

    //        member this.RunService ab fileList =
    //            async {
    //                let startServiceIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                let json = JsonConvert.SerializeObject(ab)
    //                let jsonList = JsonConvert.SerializeObject(fileList)
                    
    //                startServiceIntent.PutExtra("abdata",json) |> ignore
    //                startServiceIntent.PutExtra("fileList",jsonList) |> ignore
                                 
    //                startServiceIntent.SetAction(ACTION_START_SERVICE) |> ignore
    //                Android.App.Application.Context.StartService(startServiceIntent) |> ignore

    //                // waiting until foreground service is registered
    //                let mutable counter = 0
    //                while not AudioPlayerService.IsStarted do
    //                    do! Async.Sleep 50
    //                    counter <- counter + 1
    //                    if counter > 200 then
    //                        failwith ("unable to start foreground service!")
                    
    //                isStarted <- true

    //                return ()
    //            }

    //        member this.StopService() =
    //            async {
    //                if isStarted then
    //                    let stopServiceIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                    stopServiceIntent.SetAction(ACTION_STOP_SERVICE) |> ignore
    //                    Android.App.Application.Context.StopService(stopServiceIntent) |> ignore

    //                    //let mutable counter = 0
    //                    //while AudioPlayerService.IsStarted do
    //                    //    do! Async.Sleep 50
    //                    //    counter <- counter + 1
    //                    //    if counter > 200 then
    //                    //        failwith ("unable to stop foreground service!")

    //                    isStarted <- false
    //                    return ()
    //            }
                

    //        member this.StartAudio info =
    //            if isStarted then

    //                ACTION_START_PLAYER 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun intent ->
    //                        let infoJson = JsonConvert.SerializeObject(info)
    //                        intent.PutExtra("info",infoJson) |> ignore    
    //                )

    //                //let startAudioIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                //startAudioIntent.SetAction(ACTION_START_PLAYER) |> ignore
    //                //let infoJson = JsonConvert.SerializeObject(info)
    //                //startAudioIntent.PutExtra("info",infoJson) |> ignore       
    //                //let startAudioPendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, startAudioIntent, PendingIntentFlags.UpdateCurrent)
    //                //startAudioPendingIntent.Send()
    //                ()

    //        member this.StopAudio () =
    //            if isStarted then
    //                ACTION_STOP_PLAYER 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun _ -> ()   
    //                )
    //                //let stopAudioIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                //stopAudioIntent.SetAction(ACTION_STOP_PLAYER) |> ignore
    //                //let stopAudioPendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, stopAudioIntent, PendingIntentFlags.UpdateCurrent)
    //                //stopAudioPendingIntent.Send()
    //                ()

    //        member this.TogglePlayPause () =
    //            if isStarted then
    //                ACTION_TOGGLE_PLAYPAUSE 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun _ -> ()   
    //                )
    //                //let stopAudioIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                //stopAudioIntent.SetAction(ACTION_TOGGLE_PLAYPAUSE) |> ignore
    //                //let stopAudioPendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, stopAudioIntent, PendingIntentFlags.UpdateCurrent)
    //                //stopAudioPendingIntent.Send()
    //                ()

    //        member this.GotToPosition (ms:int) =
    //            if isStarted then
    //                ACTION_SETPOSITION_PLAYER 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun intent ->
    //                        intent.PutExtra("pos",ms) |> ignore   
    //                )

    //                //let setPositionAudioIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                //setPositionAudioIntent.SetAction(ACTION_SETPOSITION_PLAYER) |> ignore
    //                //setPositionAudioIntent.PutExtra("pos",ms) |> ignore
    //                //let setPositionPendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, setPositionAudioIntent, PendingIntentFlags.UpdateCurrent)
    //                //setPositionPendingIntent.Send()
    //                ()


    //        member this.JumpForward () =
    //            if isStarted then
    //                ACTION_JUMP_FORWARD_PLAYER 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun intent -> ()
    //                )


    //        member this.JumpBackward () =
    //            if isStarted then
    //                ACTION_JUMP_BACKWARD_PLAYER 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun intent -> ()
    //                )
    //            // ACTION_JUMP_FORWARD_PLAYER

    //        member this.MoveForward () =
    //            if isStarted then
    //                ACTION_MOVEFORWARD_PLAYER 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun _ -> ()
    //                )

    //                //let stopAudioIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                //stopAudioIntent.SetAction(ACTION_MOVEFORWARD_PLAYER) |> ignore
    //                //let stopAudioPendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, stopAudioIntent, PendingIntentFlags.UpdateCurrent)
    //                //stopAudioPendingIntent.Send()
    //                ()

    //        member this.MoveBackward () =
    //            if isStarted then
    //                ACTION_MOVEBACKWARD_PLAYER 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun _ -> ()
    //                )

    //                //let stopAudioIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                //stopAudioIntent.SetAction(ACTION_MOVEBACKWARD_PLAYER) |> ignore
    //                //let stopAudioPendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, stopAudioIntent, PendingIntentFlags.UpdateCurrent)
    //                //stopAudioPendingIntent.Send()
    //                ()

    //        member this.UpdateMetaData audioBook =
    //            if isStarted then
    //                ACTION_UPDATE_METADATA_PLAYER 
    //                |> sendCommandToService typeof<AudioPlayerService> (
    //                    fun intent ->
    //                        let infoJson = JsonConvert.SerializeObject(audioBook)
    //                        intent.PutExtra("audiobook",infoJson) |> ignore     
    //                )

    //                //let updateMetaIntent = new Intent(Android.App.Application.Context,typeof<AudioPlayerService>)
    //                //let infoJson = JsonConvert.SerializeObject(audioBook)
    //                //updateMetaIntent.PutExtra("audiobook",infoJson) |> ignore  
    //                //updateMetaIntent.SetAction(ACTION_UPDATE_METADATA_PLAYER) |> ignore
    //                //let updateMetaPendingIntent = PendingIntent.GetService(Android.App.Application.Context, 0, updateMetaIntent, PendingIntentFlags.UpdateCurrent)
    //                //updateMetaPendingIntent.Send()
    //                ()


    


        


        





        





        




    


        


            

        

      
        
