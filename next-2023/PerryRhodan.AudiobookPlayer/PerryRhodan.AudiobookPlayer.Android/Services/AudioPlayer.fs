namespace PerryRhodan.AudiobookPlayer.Android

open System.Threading.Tasks
open Android.App
open Android.Content
open Android.Media
open Android.OS
open Android.Media.Session
open System
open Dependencies
open Domain
open Elmish.SideEffect
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer.PlayerElmish
open PerryRhodan.AudiobookPlayer.ViewModels
open ReactiveElmish.Avalonia
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open _Microsoft.Android.Resource.Designer


module Helper =
    let sendAction<'a> (action: string) =
        let intent = new Intent(Application.Context, typeof<'a>)
        intent.SetAction(action) |> ignore
        Application.Context.StartService(intent) |> ignore


    let prepareMediaplayerAsync (player:MediaPlayer) =
        let mutable disp: IDisposable = null
        let tcs = new TaskCompletionSource<unit>()
        disp <- player.Prepared.Subscribe(fun _ ->
            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("mediaplayer prepared")
            tcs.SetResult()
            disp.Dispose()
        )
        player.PrepareAsync()
        tcs.Task

    type AudioFocusChangeListener(onAudioFocusChange: AudioFocus -> unit) =
        inherit Java.Lang.Object()
        interface AudioManager.IOnAudioFocusChangeListener with
            member this.OnAudioFocusChange(focusChange: AudioFocus) =
                onAudioFocusChange focusChange



[<Service(Exported = true,
          Name = "perry.rhodan.audioplayer.mediaplayer",
          ForegroundServiceType = PM.ForegroundService.TypeMediaPlayback)>]
type AudioPlayerService() as self =
    inherit Service()

    let store =
        Program.mkAvaloniaProgrammWithSideEffect
            PlayerElmish.init
            PlayerElmish.update
            (PlayerElmish.SideEffects.createSideEffectsProcessor self)
        |> Program.mkStore



    let notificationId = 47112024
    let player = new MediaPlayer()
    let binder = new AudioPlayerBinder(self)
    let mediaSessionCallback = new MediaSessionCallback()
    let mutable audioManager :AudioManager option = None
    //let playerInfoSubject = new BehaviorSubject<AudioPlayerInformation>(AudioPlayerInformation.Empty)
    //let finishedTrackSubject = new Subject<unit>()
    let mutable mediaSession: MediaSession option = None
    let mutable notificationManager: NotificationManager option = None
    let mutable currentPlaybackSpeed = 1.0f
    let disposables = System.Collections.Generic.List<IDisposable>()

    let createMediaSession (state:PlayerElmish.AudioPlayerInfo) =
        match mediaSession, state.AudioBook |> Option.map (_.AudioBook) with
        | Some mediaSession, Some audioBook ->

            let stateBuilder = new PlaybackState.Builder();



            let stateAction =
                match state.State with
                | AudioPlayerState.Playing ->
                    PlaybackState.ActionPlayPause |||
                    PlaybackState.ActionPause |||
                    PlaybackState.ActionSkipToNext |||
                    PlaybackState.ActionSkipToPrevious |||
                    PlaybackState.ActionRewind |||
                    PlaybackState.ActionFastForward |||
                    PlaybackState.ActionSeekTo
                | AudioPlayerState.Stopped ->
                    PlaybackState.ActionPlayPause |||
                    PlaybackState.ActionPlay |||
                    PlaybackState.ActionSkipToNext |||
                    PlaybackState.ActionSkipToPrevious |||
                    PlaybackState.ActionSeekTo


            stateBuilder.SetActions(stateAction) |> ignore
            stateBuilder.SetState(
                (if state.State = AudioPlayerState.Stopped then PlaybackStateCode.Stopped else PlaybackStateCode.Playing),
                state.Position.TotalMilliseconds |> int64,
                currentPlaybackSpeed) |> ignore

            mediaSession.SetPlaybackState(stateBuilder.Build());

            let albumPicFile =
                audioBook.Picture
                |> Option.defaultValue "@drawable/AudioBookPlaceholder_Dark.png"

            let albumPic = Android.Graphics.BitmapFactory.DecodeFile(albumPicFile)

            let thumbPicFile =
                audioBook.Thumbnail
                |> Option.defaultValue "@drawable/AudioBookPlaceholder_Dark.png"

            let thumbPic = Android.Graphics.BitmapFactory.DecodeFile(thumbPicFile)

            let mediaMetaData =
                (new MediaMetadata.Builder())
                    .PutBitmap(MediaMetadata.MetadataKeyAlbumArt,albumPic)
                    .PutBitmap(MediaMetadata.MetadataKeyDisplayIcon,thumbPic)
                    .PutString(MediaMetadata.MetadataKeyDisplayTitle,audioBook.FullName)
                    .PutString(MediaMetadata.MetadataKeyTitle,audioBook.FullName)
                    .PutBitmap(MediaMetadata.MetadataKeyArt, albumPic)
                    .PutLong(MediaMetadata.MetadataKeyDuration, state.Duration.TotalMilliseconds |> int64)
                    .PutString(MediaMetadata.MetadataKeyAlbum,audioBook.FullName)
                    .PutLong(MediaMetadata.MetadataKeyTrackNumber, state.CurrentTrackNumber + 1 |> int64)
                    .PutLong(MediaMetadata.MetadataKeyNumTracks, state.Mp3FileList.Length |> int64)
                    .Build();

            mediaSession.SetMetadata(mediaMetaData)

        | _ -> ()


    let sendCurrentPlayerState () =
        if player.IsPlaying then
            let duration = TimeSpan.FromMilliseconds(float player.Duration)
            let currentPosition = TimeSpan.FromMilliseconds(float player.CurrentPosition)
            store.Dispatch <| StateControlMsg (UpdatePlayingState (currentPosition, duration, AudioPlayerState.Playing))


    let updateAudioPlayerInformation () =
        match player.IsPlaying with
        | false ->
            store.Dispatch <| StateControlMsg (UpdatePlayingState (store.Model.Position, store.Model.Duration, AudioPlayerState.Stopped))
        | true ->
            sendCurrentPlayerState ()


    let createAction (icon: int) (title: string) (intentAction: string) =
        let intent = new Intent(self, typeof<AudioPlayerService>)
        intent.SetAction(intentAction) |> ignore
        let pendingIntent = PendingIntent.GetService(self, 0, intent, PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable)
        (new Notification.Action.Builder(icon, title, pendingIntent)).Build()


    let buildNotification (state:PlayerElmish.AudioPlayerInfo) =
        createMediaSession state

        let channelId = "perry_rhodan_audio_channel"

        if Build.VERSION.SdkInt >= BuildVersionCodes.O then
            let channel =
                new NotificationChannel(
                    channelId,
                    "Eins A Medien Audioplayer",
                    NotificationImportance.Low
                )

            notificationManager |> Option.iter (_.CreateNotificationChannel(channel))

        let style =
            (new Notification.MediaStyle())

        let duration =
            if player.IsPlaying then
                player.Duration
            else
                state.Duration.TotalMilliseconds |> int

        let currentPosition =
            if player.IsPlaying then
                player.CurrentPosition
            else
                state.Position.TotalMilliseconds |> int

        match state.AudioBook |> Option.map (_.AudioBook), mediaSession with
        | Some audioBook, Some mediaSession ->
            style.SetMediaSession(mediaSession.SessionToken) |> ignore
            (new Notification.Builder(self, channelId))
                .SetStyle(style)
                .SetContentTitle(audioBook.FullName)
                .SetContentText($"Track {state.CurrentTrackNumber + 1} of {state.NumOfTracks}")
                .SetSmallIcon(Resource.Drawable.einsa_icon)
                .SetVisibility(NotificationVisibility.Public)
                .SetProgress(duration, currentPosition, false)
                .Build()

        // do not show any notification
        | _ ->
            (new Notification.Builder(self, channelId))
                .SetSmallIcon(Resource.Drawable.einsa_icon)
                .SetPriority(NotificationPriority.Low |> int)
                .Build()


    let updateNotification (state:PlayerElmish.AudioPlayerInfo) =
        notificationManager
        |> Option.iter (_.Notify(notificationId, buildNotification state))


    let audioFocusRequest =
            (new AudioFocusRequestClass
                .Builder(AudioFocus.Gain))
                    .SetAudioAttributes(
                        (new AudioAttributes.Builder())
                            .SetUsage(AudioUsageKind.Media)
                            .SetContentType(AudioContentType.Music)
                            .Build()
                        )
                    .SetAcceptsDelayedFocusGain(true)
                    .SetOnAudioFocusChangeListener(
                            new Helper.AudioFocusChangeListener(
                                onAudioFocusChange = fun focusChange ->
                                    let audioPlayer = self :> IAudioPlayer
                                    match focusChange with
                                    | AudioFocus.LossTransientCanDuck ->
                                        player.SetVolume(0.2f, 0.2f)
                                    | AudioFocus.Gain ->
                                        player.SetVolume(1.0f, 1.0f)
                                        if audioPlayer.AudioPlayerInformation.State = AudioPlayerState.Stopped then
                                            audioPlayer.Play()
                                    | AudioFocus.GainTransient ->
                                        player.SetVolume(1.0f, 1.0f)
                                        if audioPlayer.AudioPlayerInformation.State = AudioPlayerState.Stopped then
                                            audioPlayer.Play()
                                    | AudioFocus.GainTransientExclusive ->
                                        player.SetVolume(1.0f, 1.0f)
                                        if audioPlayer.AudioPlayerInformation.State = AudioPlayerState.Stopped then
                                            audioPlayer.Play()
                                    | AudioFocus.GainTransientMayDuck ->
                                        player.SetVolume(1.0f, 1.0f)
                                        if audioPlayer.AudioPlayerInformation.State = AudioPlayerState.Stopped then
                                            audioPlayer.Play()
                                    | AudioFocus.Loss ->
                                        audioPlayer.Stop true
                                    | AudioFocus.LossTransient ->
                                        audioPlayer.Stop true
                                    | AudioFocus.None ->
                                        ()
                                    | _ ->
                                        ()
                            )
                    )
                    .Build()


    override this.OnBind(intent) =
        binder :> IBinder

    override this.OnCreate() =
        base.OnCreate()
        // overwrite dependency injection with that instance
        DependencyService.ServiceCollection.AddSingleton<IAudioPlayer>(this) |> ignore
        DependencyService.SetComplete()

        notificationManager <- this.GetSystemService(Context.NotificationService) :?> NotificationManager |> Some
        mediaSession <- new MediaSession(this, "Eins A Medien Audioplayer") |> Some

        createMediaSession store.Model
        mediaSession |> Option.iter (_.SetCallback(mediaSessionCallback))
        this.StartForeground(notificationId, buildNotification store.Model)

        // timer to update the audio player information
        System.Reactive.Linq.Observable
            .Interval(TimeSpan.FromSeconds(1.0))
            .Subscribe(fun _ -> updateAudioPlayerInformation ())
        |> ignore


        disposables.Add <| store.Observable.Subscribe (fun state ->
            if state.IsBusy |> not then
                updateNotification state
        )
        disposables.Add <| player.Completion.Subscribe (fun _ ->
            store.Dispatch <| PlayerControlMsg MoveToNextTrack
        )

        AudioPlayerService.ServiceIsRunningInForeground <- true


    override this.OnDestroy() =
        base.OnDestroy()
        mediaSession |> Option.iter (_.Release())
        player.Release()
        disposables |> Seq.iter (_.Dispose())

        AudioPlayerService.ServiceIsRunningInForeground <- false


    override this.OnStartCommand(intent: Intent, _: StartCommandFlags, _: int) =
        //Acquire wake lock
        let pm = PowerManager.FromContext(this)
        let wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "AudioPlayerService")
        wakeLock.Acquire()


        let audioPlayer = this :> IAudioPlayer
        match intent.Action with
        | "ACTION_PAUSE" ->
            audioPlayer.Pause()
            StartCommandResult.Sticky

        | "ACTION_PLAY" ->
            audioPlayer.Play ()
            StartCommandResult.Sticky

        | "ACTION_STOP" ->
            audioPlayer.Stop false
            StartCommandResult.Sticky

        | "ACTION_REWIND" ->
            audioPlayer.JumpBackwards ()
            StartCommandResult.Sticky

        | "ACTION_FF" ->
            audioPlayer.JumpForward ()
            StartCommandResult.Sticky

        | "ACTION_NEXT" ->
            audioPlayer.Next()
            StartCommandResult.Sticky

        | "ACTION_PREVIOUS" ->
            audioPlayer.Previous()
            StartCommandResult.Sticky

        | "ACTION_SEEK" ->
            let pos = intent.GetLongExtra("position", 0L)
            audioPlayer.SeekTo(TimeSpan.FromMilliseconds(float pos))
            StartCommandResult.Sticky

        | "ACTION_CLOSE" ->
            // Don't know yet
            StartCommandResult.Sticky

        | _ ->
            StartCommandResult.Sticky


    member val CurrentAudioPlayerState = store.Model with get


    static member val ServiceIsRunningInForeground = false with get, set


    interface IMediaPlayer with

        member this.Play(file: string) =
            task {


                try

                    player.Reset()
                    player.SetDataSource(file)
                    let playbackParams =
                        (new PlaybackParams())
                            .SetSpeed(currentPlaybackSpeed)
                            .SetPitch(1.0f)
                            .SetAudioFallbackMode(0)

                    player.PlaybackParams <- playbackParams
                    do! Helper.prepareMediaplayerAsync player
                    
                    // ask audiomanager to give us audio focus
                    audioManager |> Option.iter (fun a -> a.AbandonAudioFocusRequest(audioFocusRequest) |> ignore)
                    audioManager
                    |> Option.iter (fun a ->
                        let audioFocus = a.RequestAudioFocus(audioFocusRequest)
                        if audioFocus = AudioFocusRequest.Granted then
                            player.Start()
                            updateAudioPlayerInformation ()
                    )
                    
                with
                | ex ->
                     Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                     raise ex
            }


        member this.Pause() =
            try
                sendCurrentPlayerState () // send state right before pausing
                player.Pause()
                updateAudioPlayerInformation ()
            with
            | ex ->
                 Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                 reraise()

        member this.PlayPause() =
            try
                if player.IsPlaying then
                    sendCurrentPlayerState () // send state right before pausing
                    player.Pause()
                else
                    player.Start()
                updateAudioPlayerInformation ()
            with
            | ex ->
                 Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                 reraise()

        member this.Stop resumeOnAudioFocus =
            try
                sendCurrentPlayerState () // send state right before pausing
                if not resumeOnAudioFocus then
                    audioManager |> Option.iter (fun a -> a.AbandonAudioFocusRequest(audioFocusRequest) |> ignore)

                player.Stop ()
                updateAudioPlayerInformation ()
            with
            | ex ->
                 Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                 reraise()

        member this.SeekTo(position: TimeSpan) =
            try
                sendCurrentPlayerState () // send state right before pausing
                player.SeekTo(position.TotalMilliseconds |> int)
                updateAudioPlayerInformation ()
            with
            | ex ->
                 Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                 reraise()

        member this.SetPlaybackSpeed(speed: float) =
            task {
                try
                    let wasPlaying = player.IsPlaying
                    currentPlaybackSpeed <- speed |> float32
                    let currentPos =
                        if player.IsPlaying then
                            player.CurrentPosition
                        else
                            store.Model.Position.TotalMilliseconds |> int

                    player.Stop()
                    player.Reset()
                    player.SetDataSource store.Model.Filename
                    let playbackParams =
                        (new PlaybackParams())
                            .SetSpeed(currentPlaybackSpeed)
                            .SetPitch(1.0f)
                            .SetAudioFallbackMode(0)
                    player.PlaybackParams <- playbackParams
                    do! Helper.prepareMediaplayerAsync player
                    player.SeekTo currentPos
                    if wasPlaying then
                        player.Start()
                    updateAudioPlayerInformation ()
                with
                | ex ->
                     Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                     raise ex
            }

    interface IAudioPlayerPause with
        member this.Pause() =
            store.Dispatch <| PlayerControlMsg (Stop false)

    interface IAudioPlayer  with

        member this.Init audiobook fileList =
            store.Dispatch <| StateControlMsg (InitAudioService (audiobook, fileList))
            // add audio manager
            audioManager <- Some (self.GetSystemService(Context.AudioService) :?> AudioManager)

        member this.Play () =
            store.Dispatch <| PlayerControlMsg Play

        member this.PlayExtern file pos =
            store.Dispatch <| PlayerControlMsg (PlayExtern (file, pos))

        member this.Pause() =
            store.Dispatch <| PlayerControlMsg (Stop false)

        member this.PlayPause () =
            if player.IsPlaying then
                store.Dispatch <| PlayerControlMsg (Stop false)
            else
                store.Dispatch <| PlayerControlMsg Play


        member this.Stop resumeOnAudioFocus =
            store.Dispatch <| PlayerControlMsg (Stop resumeOnAudioFocus)

        member this.JumpBackwards () =
            store.Dispatch <| PlayerControlMsg JumpBackwards

        member this.JumpForward () =
            store.Dispatch <| PlayerControlMsg JumpForward

        member this.Next () =
            store.Dispatch <| PlayerControlMsg MoveToNextTrack

        member this.Previous () =
            store.Dispatch <| PlayerControlMsg MoveToPreviousTrack

        member this.SeekTo position =
            store.Dispatch <| PlayerControlMsg (GotoPosition position)

        member this.SetPlaybackSpeed speed =
            store.Dispatch <| PlayerControlMsg (SetPlaybackSpeed speed)

        member this.StartSleepTimer sleepTime =
            match sleepTime with
            | None ->
                store.Dispatch <| SleepTimerMsg SleepTimerStop
            | Some sleepTime ->
                store.Dispatch <| SleepTimerMsg (SleepTimerStart sleepTime)

        member this.AudioPlayerInformation with get() = store.Model
        member this.AudioPlayerInfoChanged = store.Observable



and MediaSessionCallback() =
    inherit MediaSession.Callback()

    override this.OnPlay() =
        Helper.sendAction<AudioPlayerService> "ACTION_PLAY"

    override this.OnPause() =
        Helper.sendAction<AudioPlayerService> "ACTION_PAUSE"

    override this.OnStop() =
        Helper.sendAction<AudioPlayerService> "ACTION_STOP"

    override this.OnSkipToNext() =
        Helper.sendAction<AudioPlayerService> "ACTION_NEXT"

    override this.OnSkipToPrevious() =
        Helper.sendAction<AudioPlayerService> "ACTION_PREVIOUS"

    override this.OnSeekTo(pos: int64) =
        let intent = new Intent(Application.Context, typeof<AudioPlayerService>)
        intent.SetAction("ACTION_SEEK") |> ignore
        intent.PutExtra("position", pos) |> ignore
        Application.Context.StartService(intent) |> ignore


and AudioPlayerBinder(service:AudioPlayerService) =
    inherit Binder()
    member _.GetService() =
        service



[<BroadcastReceiver(Enabled = true)>]
type NotificationActionReceiver() =
    inherit BroadcastReceiver()
    override this.OnReceive(context, intent) =
        match intent.Action with
        | "ACTION_PAUSE"
        | "ACTION_STOP"->
            Helper.sendAction<AudioPlayerService> "ACTION_PAUSE"
        | "ACTION_PLAY" ->
            Helper.sendAction<AudioPlayerService> "ACTION_PLAY"
        | _ ->
            ()



type AudioPlayerServiceController() =
    interface IAudioPlayerServiceController with
        member this.StartService() =
            let intent = new Intent(Application.Context, typeof<AudioPlayerService>)
            Application.Context.StartService(intent) |> ignore



        member this.StopService() =
            if AudioPlayerService.ServiceIsRunningInForeground then
                let intent = new Intent(Application.Context, typeof<AudioPlayerService>)
                Application.Context.StopService(intent) |> ignore
