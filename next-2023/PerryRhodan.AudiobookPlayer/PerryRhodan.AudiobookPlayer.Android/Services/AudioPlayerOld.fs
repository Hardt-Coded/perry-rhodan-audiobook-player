namespace PerryRhodan.AudiobookPlayer.Android


(*
// implement an audio player service for Android and use the IAudioPlayer interface
// the player should have a notification to control the player from the notification bar
// also the player should automatically stop, if the audio device changes or a call came in or another app starts playing audio


open System
open System.Reactive.Subjects
open System.Reactive
open System.Reactive.Linq
open Android.App
open Android.Content
open Android.Media
open Android.Media.Session
open Android.OS
open Domain
open FSharpx.Control
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open _Microsoft.Android.Resource.Designer


type AudioFocusChangeListener(
    onAudioFocusChange: AudioFocus -> unit
    ) =
    inherit Java.Lang.Object()
    
    interface AudioManager.IOnAudioFocusChangeListener with
        member _.OnAudioFocusChange(focusChange) =
            onAudioFocusChange focusChange


type MyBroadcastReceiver(callback: Intent -> unit) =
    inherit BroadcastReceiver()
    
    override this.OnReceive(context, intent) =
        callback intent


type MediaSessionCallback() =
        inherit MediaSession.Callback()

        override this.OnPlay() =
            AudioPlayer.startAudioPlayer ()

        override this.OnPause() =
            AudioPlayer.stopAudioPlayer (false)

        override this.OnStop() =
            AudioPlayer.stopAudioPlayer (false)

        override this.OnSkipToNext() =
            AudioPlayer.moveToNextTrack ()

        override this.OnSkipToPrevious() =
            AudioPlayer.moveToPreviousTrack ()

        override this.OnSeekTo(pos: int64) =
            AudioPlayer.setPosition (TimeSpan.FromMilliseconds(float pos))




[<Service(Exported = true,
          Name = "perry.rhodan.audioplayer.mediaplayer",
          ForegroundServiceType = PM.ForegroundService.TypeMediaPlayback)>]
type AudioPlayerService2() as self =
    inherit Service()

    let mutable audioBook: AudioBook option = None
    let mutable currentTrack: int = 0
    
    let player = new MediaPlayer()

    let mutable mediaSession: MediaSession option = None

    let initializeMediaSession () =
        let session = new MediaSession(Application.Context, "PerryRhodanAudioplayer")
        session.SetFlags(MediaSession.FlagHandlesMediaButtons ||| MediaSession.FlagHandlesTransportControls)
        session.SetCallback(new MediaSessionCallback())
        session.Active <- true
        mediaSession <- Some session
    
    
    let audioPlayerInformationSubject =
        new BehaviorSubject<AudioPlayerInformation>(
            {
                State = AudioPlayerState.Stopped
                Duration = TimeSpan.Zero
                CurrentPosition = TimeSpan.Zero
            }
        )
    let audioPlayerFinishedTrackSubject = new Subject<unit>()

        
    let updateAudioPlayerInformation () =
        let state =
            if player.IsPlaying then
                AudioPlayerState.Playing
            else
                AudioPlayerState.Stopped

        let duration = TimeSpan.FromMilliseconds(float player.Duration)
        let currentPosition = TimeSpan.FromMilliseconds(float player.CurrentPosition)

        audioPlayerInformationSubject.OnNext(
            {
                State = state
                Duration = duration
                CurrentPosition = currentPosition
            }
        )

    
    let createMediaSession () =
        match mediaSession, audioBook with
        | Some mediaSession, Some audioBook ->
            let info = audioPlayerInformationSubject.Value
            let context = Android.App.Application.Context
            (*let icon = icon "einsa_small_icon"
            let title = info.AudioBook.FullName*)
            
            let stateBuilder = new PlaybackState.Builder();

            let stateAction =
                match info.State with
                | AudioPlayerState.Playing ->
                    PlaybackState.ActionPlayPause ||| PlaybackState.ActionPause ||| PlaybackState.ActionStop ||| PlaybackState.ActionSkipToNext ||| PlaybackState.ActionSkipToPrevious
                | AudioPlayerState.Stopped ->
                    PlaybackState.ActionPlayPause ||| PlaybackState.ActionPlay ||| PlaybackState.ActionSkipToNext ||| PlaybackState.ActionSkipToPrevious


            stateBuilder.SetActions(stateAction) |> ignore            
            stateBuilder.SetState((if info.State = AudioPlayerState.Stopped then PlaybackStateCode.Stopped else PlaybackStateCode.Playing),info.CurrentPosition.TotalMilliseconds |> int64 , 1.0f) |> ignore
            
            mediaSession.SetPlaybackState(stateBuilder.Build());
            
            let albumPicFile = 
                audioBook.Thumbnail
                |> Option.defaultValue "@drawable/AudioBookPlaceholder_Dark.png"

            let albumPic = Android.Graphics.BitmapFactory.DecodeFile(albumPicFile)

            let mediaMetaData =
                (new MediaMetadata.Builder())
                    .PutBitmap(MediaMetadata.MetadataKeyAlbumArt,albumPic)                                                                                                                               
                    .PutBitmap(MediaMetadata.MetadataKeyDisplayIcon,albumPic)                                                                                                                                                                                       
                    .PutString(MediaMetadata.MetadataKeyDisplayTitle,audioBook.FullName)
                    .PutString(MediaMetadata.MetadataKeyTitle,audioBook.FullName)
                    .PutBitmap(MediaMetadata.MetadataKeyArt, albumPic)
                    .PutLong(MediaMetadata.MetadataKeyDuration, info.Duration.TotalMilliseconds |> int64)
                    
                    .PutString(MediaMetadata.MetadataKeyAlbum,audioBook.FullName)
                    .PutLong(MediaMetadata.MetadataKeyTrackNumber, currentTrack |> int64)  
                    //.PutLong(MediaMetadata.MetadataKeyNumTracks,info.Mp3FileList.Length |> int64) TODO:  
                    .PutLong(MediaMetadata.MetadataKeyTrackNumber,currentTrack |> int64)
                    .Build();

            mediaSession.SetMetadata(mediaMetaData)
            

            let style = new Notification.MediaStyle();
            style.SetMediaSession(mediaSession.SessionToken) |> ignore
        | _ -> ()
    
    let createNotification () =
        
        createMediaSession ()
        
        let notificationManager =
            Application.Context.GetSystemService(Context.NotificationService)
            :?> NotificationManager

        let channelId = "perry_rhodan_audio_channel"

        if Build.VERSION.SdkInt >= BuildVersionCodes.O then
            let channel =
                new NotificationChannel(
                    channelId,
                    "Perry Rhodan Audioplayer",
                    NotificationImportance.Default
                )

            notificationManager.CreateNotificationChannel(channel)

        let playPauseIntent = new Intent(Application.Context, typeof<AudioPlayerService>)
        playPauseIntent.SetAction("ACTION_PLAY_PAUSE") |> ignore

        let playPausePendingIntent =
            PendingIntent.GetService(
                Application.Context,
                0,
                playPauseIntent,
                PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable
            )

        let stopIntent = new Intent(Application.Context, typeof<AudioPlayerService>)
        stopIntent.SetAction("ACTION_STOP") |> ignore

        let stopPendingIntent =
            PendingIntent.GetService(
                Application.Context,
                1,
                stopIntent,
                PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable
            )

        // fast forward
        let fastForwardIntent = new Intent(Application.Context, typeof<AudioPlayerService>)
        fastForwardIntent.SetAction("ACTION_FAST_FORWARD") |> ignore

        let fastForwardPendingIntent =
            PendingIntent.GetService(
                Application.Context,
                2,
                fastForwardIntent,
                PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable
            )
        // rewind
        let rewindIntent = new Intent(Application.Context, typeof<AudioPlayerService>)
        rewindIntent.SetAction("ACTION_REWIND") |> ignore

        let rewindPendingIntent =
            PendingIntent.GetService(
                Application.Context,
                3,
                rewindIntent,
                PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable
            )
        // next
        let nextIntent = new Intent(Application.Context, typeof<AudioPlayerService>)
        nextIntent.SetAction("ACTION_NEXT") |> ignore

        let nextPendingIntent =
            PendingIntent.GetService(
                Application.Context,
                4,
                nextIntent,
                PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable
            )
        // previous
        let previousIntent = new Intent(Application.Context, typeof<AudioPlayerService>)
        previousIntent.SetAction("ACTION_PREVIOUS") |> ignore

        let previousPendingIntent =
            PendingIntent.GetService(
                Application.Context,
                5,
                previousIntent,
                PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable
            )


        let notificationBuilder = new Notification.Builder(Application.Context, channelId)

        notificationBuilder
            .SetContentTitle(audioBook |> Option.map (_.FullName) |> Option.defaultValue "Unknown")
            .SetContentText($"Track {currentTrack}")
            .SetSmallIcon(Resource.Drawable.einsa_small_icon)
            .SetOngoing(true)
            .AddAction(Android.Resource.Drawable.IcMediaPause, "Pause", playPausePendingIntent)
            .AddAction(Android.Resource.Drawable.IcMenuCloseClearCancel, "Stop", stopPendingIntent)
            .AddAction(
                Android.Resource.Drawable.IcMediaFf,
                "Fast Forward",
                fastForwardPendingIntent
            )
            .AddAction(Android.Resource.Drawable.IcMediaRew, "Rewind", rewindPendingIntent)
            .AddAction(Android.Resource.Drawable.IcMediaNext, "Next", nextPendingIntent)
            .AddAction(Android.Resource.Drawable.IcMediaPrevious, "Previous", previousPendingIntent)
            .SetVisibility(NotificationVisibility.Public)
            .Build()

    
    let initThisService () =
        let serviceIntent = new Intent(Application.Context, typeof<AudioPlayerService>)
        Application.Context.StartService(serviceIntent) |> ignore
    
    let startForegroundService () =
        let notification = createNotification ()
        self.StartForeground(1, notification)

    let stopForegroundService () =
        self.StopForeground(true)
        //self.StopSelf()

    let stopPlayer () =
        if player.IsPlaying then
            player.Stop()
            player.Reset()
            updateAudioPlayerInformation ()
            

    let handleAudioFocusChange (focusChange: AudioFocus) =
        match focusChange with
        | AudioFocus.Loss -> stopPlayer ()
        | AudioFocus.LossTransient -> player.Pause()
        | AudioFocus.LossTransientCanDuck -> player.SetVolume(0.1f, 0.1f)
        | AudioFocus.Gain -> player.SetVolume(1.0f, 1.0f)
        | _ -> ()

    let audioFocusChangeListener =
        new AudioFocusChangeListener(handleAudioFocusChange)


    do
        let audioManager =
            Application.Context.GetSystemService(Context.AudioService) :?> AudioManager

        audioManager.RequestAudioFocus(audioFocusChangeListener, Stream.Music, AudioFocus.Gain) |> ignore
        let intentFilter = new IntentFilter()
        intentFilter.AddAction(AudioManager.ActionAudioBecomingNoisy)

        let broadcastReceiver = new MyBroadcastReceiver(fun _ -> stopPlayer ())
        // register broadcast receiver
        Application.Context.RegisterReceiver(broadcastReceiver, intentFilter) |> ignore
        
        // register observable on every change of the player
        player.Completion.Subscribe(fun _ -> audioPlayerFinishedTrackSubject.OnNext ()) |> ignore
        // add timer, which updates the audio player information every second with audioPlayerInformationSubject
        Observable
            .Interval(TimeSpan.FromSeconds(1.0))
            .Subscribe(fun _ -> updateAudioPlayerInformation ())
        |> ignore
        
        
        
        
        
        // Register media button receiver
        let mediaButtonIntent = new Intent(Intent.ActionMediaButton)
        let mediaButtonPendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, mediaButtonIntent, PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable)
        audioManager.RegisterMediaButtonEventReceiver(mediaButtonPendingIntent)
        
        // launch this service as a foreground service
        
    override this.OnCreate() =
        base.OnCreate()
        initializeMediaSession()
        startForegroundService ()
    
    override this.OnDestroy() =
        mediaSession |> Option.iter (fun session -> session.Release())
        player.Release()
        audioPlayerInformationSubject.OnCompleted()
        audioPlayerFinishedTrackSubject.OnCompleted()
        base.OnDestroy()
    
    override this.OnBind(_: Intent) =
        null

    override this.OnStartCommand(intent: Intent, _: StartCommandFlags, _: int) =
        match intent.Action with
        | "ACTION_PLAY_PAUSE" ->
            if player.IsPlaying then
                player.Pause()
            else
                player.Start()

            updateAudioPlayerInformation ()
            
        | "ACTION_STOP" -> stopPlayer ()
        | "ACTION_FOREGROUND_START" ->
            startForegroundService ()
        | "ACTION_FOREGROUND_STOP" ->
            stopForegroundService ()
            this.StopSelf()
        | _ -> ()

        StartCommandResult.Sticky

    interface IAudioPlayer with
        
        member this.StartService () =
            initThisService ()
            
        member this.StopService () =
            Application.Context.StopService(new Intent(Application.Context, typeof<AudioPlayerService>)) |> ignore
            
        
        member this.Init audiobook track =
            audioBook <- Some audiobook
            currentTrack <- track
            
            ()

        member _.Play(file: string) =
            player.Reset()
            player.SetDataSource(file)
            player.Prepare()
            player.Start()
            updateAudioPlayerInformation ()

        member _.Pause() =
            player.Pause()
            updateAudioPlayerInformation ()

        member _.Stop() =
            stopPlayer ()

        member _.SeekTo(position: TimeSpan) =
            player.SeekTo(position.TotalMilliseconds |> int)
            updateAudioPlayerInformation ()
            
        member this.PlayPause() =
            if player.IsPlaying then
                player.Pause()
            else
                player.Start()

            updateAudioPlayerInformation ()
            
            
        member _.SetPlaybackSpeed(speed: float) =
            player.PlaybackParams.SetSpeed(speed |> float32) |> ignore
            ()

        member _.Duration = player.Duration |> TimeSpan.FromMilliseconds

        member _.CurrentPosition = player.CurrentPosition |> TimeSpan.FromMilliseconds

        member _.AudioPlayerInformation = audioPlayerInformationSubject
        
        member _.AudioPlayerFinishedTrack = audioPlayerFinishedTrackSubject
        *)
        
