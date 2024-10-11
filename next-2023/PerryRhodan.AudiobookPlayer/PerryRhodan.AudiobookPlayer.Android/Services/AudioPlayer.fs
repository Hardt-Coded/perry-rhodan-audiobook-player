namespace PerryRhodan.AudiobookPlayer.Android

open System.Threading.Tasks
open Android.App
open Android.Content
open Android.Media
open Android.OS
open Android.Media.Session
open System
open System.Reactive.Subjects
open Dependencies
open Domain
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open _Microsoft.Android.Resource.Designer






[<Service(Exported = true,
          Name = "perry.rhodan.audioplayer.mediaplayer",
          ForegroundServiceType = PM.ForegroundService.TypeMediaPlayback)>]
type AudioPlayerService() as self =
    inherit Service()
    let id = Guid.NewGuid().ToString()
    let notificationId = 47112024
    let player = new MediaPlayer()
    let binder = new AudioPlayerBinder(self)
    let mediaSessionCallback = new MediaSessionCallback()
    let playerInfoSubject = new BehaviorSubject<AudioPlayerInformation>(AudioPlayerInformation.Empty)
    let finishedTrackSubject = new Subject<unit>()
    let mutable mediaSession: MediaSession option = None
    let mutable notificationManager: NotificationManager option = None
    let mutable audioBook: AudioBook option = None
    let mutable currentTrack: int = 0
    let mutable numOfTracks: int = 0
    let mutable currentPlaybackSpeed = 1.0f
    
    let createMediaSession () =
        match mediaSession, audioBook with
        | Some mediaSession, Some audioBook ->
            let info = playerInfoSubject.Value
            let context = Android.App.Application.Context
            (*let icon = icon "einsa_small_icon"
            let title = info.AudioBook.FullName*)
            
            let stateBuilder = new PlaybackState.Builder();

            let stateAction =
                match info.State with
                | AudioPlayerState.Playing ->
                    PlaybackState.ActionPlayPause |||
                    PlaybackState.ActionPause |||
                    PlaybackState.ActionStop |||
                    PlaybackState.ActionSkipToNext |||
                    PlaybackState.ActionSkipToPrevious |||
                    PlaybackState.ActionRewind |||
                    PlaybackState.ActionFastForward
                | AudioPlayerState.Stopped ->
                    PlaybackState.ActionPlayPause |||
                    PlaybackState.ActionPlay |||
                    PlaybackState.ActionSkipToNext |||
                    PlaybackState.ActionSkipToPrevious


            stateBuilder.SetActions(stateAction) |> ignore            
            stateBuilder.SetState((if info.State = AudioPlayerState.Stopped then PlaybackStateCode.Stopped else PlaybackStateCode.Playing),info.CurrentPosition.TotalMilliseconds |> int64 , currentPlaybackSpeed) |> ignore
            
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
                    .PutLong(MediaMetadata.MetadataKeyNumTracks,numOfTracks |> int64)  
                    .PutLong(MediaMetadata.MetadataKeyTrackNumber,currentTrack |> int64)
                    .Build();

            mediaSession.SetMetadata(mediaMetaData)
            
        | _ -> ()
    
    
    let updateAudioPlayerInformation () =
        let state =
            if player.IsPlaying then
                AudioPlayerState.Playing
            else
                AudioPlayerState.Stopped

        let duration = TimeSpan.FromMilliseconds(float player.Duration)
        let currentPosition = TimeSpan.FromMilliseconds(float player.CurrentPosition)

        playerInfoSubject.OnNext(
            {
                State = state
                Duration = duration
                CurrentPosition = currentPosition
            }
        )
        
    
    let createAction (icon: int) (title: string) (intentAction: string) =
        let intent = new Intent(self, typeof<AudioPlayerService>)
        intent.SetAction(intentAction) |> ignore
        let pendingIntent = PendingIntent.GetService(self, 0, intent, PendingIntentFlags.UpdateCurrent ||| PendingIntentFlags.Immutable)
        (new Notification.Action.Builder(icon, title, pendingIntent)).Build()
    
    
    let buildNotification () =
        createMediaSession ()
        
        let channelId = "perry_rhodan_audio_channel"

        if Build.VERSION.SdkInt >= BuildVersionCodes.O then
            let channel =
                new NotificationChannel(
                    channelId,
                    "Perry Rhodan Audioplayer",
                    NotificationImportance.Low
                )

            notificationManager |> Option.iter (_.CreateNotificationChannel(channel))
        
        let selfInterface = self :> IAudioPlayer
        let style =
            (new Notification.MediaStyle())
            
        match audioBook, mediaSession with
        | Some _, Some mediaSession ->
            style.SetMediaSession(mediaSession.SessionToken) |> ignore
            (new Notification.Builder(self, channelId))
                .SetContentTitle(audioBook |> Option.map (_.FullName) |> Option.defaultValue "Unknown")
                .SetContentText($"Track {currentTrack + 1} of {numOfTracks}")
                .SetSmallIcon(Resource.Drawable.einsa_icon)
                .SetVisibility(NotificationVisibility.Public)
                .SetProgress(selfInterface.Duration.TotalSeconds |> int, selfInterface.CurrentPosition.TotalSeconds |> int, false)
                .SetStyle(style)
                .Build()
            
        // do not show any notification            
        | _ ->
            (new Notification.Builder(self, channelId))
                .Build()
    
    
    let updateNotification () =
        notificationManager
        |> Option.iter (fun manager -> manager.Notify(notificationId, buildNotification ()))
        

    override this.OnBind(intent) =
        let myId = id
        binder :> IBinder

    override this.OnCreate() =
        base.OnCreate()
        let myId = id
        // overwrite dependency injection with that instance
        DependencyService.ServiceCollection.AddSingleton<IAudioPlayer>(this) |> ignore
        DependencyService.SetComplete()
        
        notificationManager <- this.GetSystemService(Context.NotificationService) :?> NotificationManager |> Some
        mediaSession <- new MediaSession(this, "PerryRhodanAudioplayer") |> Some
        createMediaSession ()
        mediaSession |> Option.iter (_.SetCallback(mediaSessionCallback))
        this.StartForeground(notificationId, buildNotification ())
        
        // timer to update the audio player information
        System.Reactive.Linq.Observable
            .Interval(TimeSpan.FromSeconds(1.0))
            .Subscribe(fun _ -> updateAudioPlayerInformation ())
        |> ignore
        
        player.Completion.Subscribe (fun _ -> finishedTrackSubject.OnNext ()) |> ignore
        
        
        AudioPlayerService.ServiceIsRunningInForeground <- true                            


    override this.OnDestroy() =
        base.OnDestroy()
        mediaSession |> Option.iter (fun session -> session.Release())
        player.Release()
        playerInfoSubject.OnCompleted()
        finishedTrackSubject.OnCompleted()
        
        AudioPlayerService.ServiceIsRunningInForeground <- false

    
    override this.OnStartCommand(intent: Intent, _: StartCommandFlags, _: int) =
        let myId = id
        match intent.Action with
        | "ACTION_PAUSE" ->
            AudioPlayer.stopAudioPlayer (false)
            StartCommandResult.Sticky
            
        | "ACTION_PLAY" ->
            AudioPlayer.startAudioPlayer ()
            StartCommandResult.Sticky
            
        | "ACTION_STOP" ->
            AudioPlayer.stopAudioPlayer (false)
            StartCommandResult.Sticky
            
        | "ACTION_REWIND" ->
            AudioPlayer.setPosition (TimeSpan.FromMilliseconds(float player.CurrentPosition - 10000.0))
            StartCommandResult.Sticky
            
        | "ACTION_FF" ->
            AudioPlayer.setPosition (TimeSpan.FromMilliseconds(float player.CurrentPosition + 10000.0))
            StartCommandResult.Sticky
            
        | "ACTION_NEXT" ->
            AudioPlayer.moveToNextTrack ()
            StartCommandResult.Sticky
            
        | "ACTION_PREVIOUS" ->
            AudioPlayer.moveToPreviousTrack ()
            StartCommandResult.Sticky
            
        | "ACTION_CLOSE" ->
            AudioPlayer.stopAudioPlayer (true)
            StartCommandResult.Sticky
            
        | _ ->
            StartCommandResult.Sticky
                
            
        
        
        
    static member val ServiceIsRunningInForeground = false with get, set 
    
    interface IAudioPlayer with
        member this.StartService() =
            task {
                let intent = new Intent(Application.Context, typeof<AudioPlayerService>)
                Application.Context.StartService(intent) |> ignore
                while not AudioPlayerService.ServiceIsRunningInForeground do
                    do! Task.Delay(200)
                return ()    
            }
            
            

        member this.StopService() =
            task {
                if AudioPlayerService.ServiceIsRunningInForeground then
                    let intent = new Intent(Application.Context, typeof<AudioPlayerService>)
                    Application.Context.StopService(intent) |> ignore
                    while AudioPlayerService.ServiceIsRunningInForeground do
                        do! Task.Delay(200)
            }
            

        member this.SetMetaData audiobook numberTracks track =
            audioBook <- Some audiobook
            currentTrack <- track
            numOfTracks <- numberTracks
            updateNotification ()
            ()

        member this.Play(file: string) =
            let myId = id
            player.Reset()
            player.SetDataSource(file)
            player.Prepare()
            player.Start()
            updateAudioPlayerInformation ()
            updateNotification ()

        member this.Pause() =
            let myId = id
            player.Pause()
            updateAudioPlayerInformation ()
            updateNotification ()

        member this.PlayPause() =
            let myId = id
            if player.IsPlaying then
                player.Pause()
            else
                player.Start()

            updateAudioPlayerInformation ()
            updateNotification ()

        member this.Stop() =
            let myId = id
            player.Stop ()
            updateAudioPlayerInformation ()
            updateNotification ()

        member this.SeekTo(position: TimeSpan) =
            player.SeekTo(position.TotalMilliseconds |> int)
            updateAudioPlayerInformation ()

        member this.SetPlaybackSpeed(speed: float) =
            player.PlaybackParams.SetSpeed(speed |> float32) |> ignore
            currentPlaybackSpeed <- speed |> float32

        member this.Duration = player.Duration |> TimeSpan.FromMilliseconds

        member this.CurrentPosition = player.CurrentPosition |> TimeSpan.FromMilliseconds

        member this.AudioPlayerInformation =
            playerInfoSubject

        member this.AudioPlayerFinishedTrack =
            finishedTrackSubject

and MediaSessionCallback() =
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

and AudioPlayerBinder(service:AudioPlayerService) =
    inherit Binder()
    member this.GetService() =
        service



[<BroadcastReceiver(Enabled = true)>]
type NotificationActionReceiver() =
    inherit BroadcastReceiver()
    override this.OnReceive(context, intent) =
        match intent.Action with
        | "ACTION_PAUSE" ->
            AudioPlayer.stopAudioPlayer (false)
            ()
        | "ACTION_PLAY" ->
            AudioPlayer.startAudioPlayer ()
            ()
        | _ ->
            ()
            
            
            
(*
type AudioPlayer() =
    interface IAudioPlayer with
        member this.AudioPlayerFinishedTrack = 
        member this.AudioPlayerInformation = 
        member this.CurrentPosition = 
        member this.Duration = 
        member this.Init audiobook numberOfTracks curentTrack = 
        member this.Pause() =
        member this.Play(var0) = 
        member this.PlayPause() = 
        member this.SeekTo(var0) = 
        member this.SetPlaybackSpeed(var0) = 
        member this.StartService() = 
        member this.Stop() = 
        member this.StopService() = 
        *)
            