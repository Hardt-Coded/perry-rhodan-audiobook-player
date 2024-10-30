namespace PerryRhodan.AudiobookPlayer.Android

open System.Threading.Tasks
open Android.App
open Android.Content
open Android.Graphics
open Android.Media
open Android.OS
open Android.Media.Session
open System
open Dependencies
open Domain
open Elmish.SideEffect
open MediaManager
open MediaManager.Library
open MediaManager.Player
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer.PlayerElmish
open PerryRhodan.AudiobookPlayer.ViewModels
open ReactiveElmish.Avalonia
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open _Microsoft.Android.Resource.Designer
open PerryRhodan.AudiobookPlayer.Services


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


    // title, album, trackNumber, numTracks, duration, albumArt, displayIcon, art
    type MediaSessionData = {
        Title: string
        Album: string
        TrackNumber: int64
        NumTracks: int64
        Duration: int64
    }

    let getMediaSessionData (mediaSession:MediaSession) =
        mediaSession.Controller.Metadata
        |> Option.ofObj
        |> Option.map (fun metadata ->
            let title = metadata.GetString(MediaMetadata.MetadataKeyDisplayTitle)
            let album = metadata.GetString(MediaMetadata.MetadataKeyAlbum)
            let trackNumber = metadata.GetLong(MediaMetadata.MetadataKeyTrackNumber)
            let numTracks = metadata.GetLong(MediaMetadata.MetadataKeyNumTracks)
            let duration = metadata.GetLong(MediaMetadata.MetadataKeyDuration)
            {
                Title = title
                Album = album
                TrackNumber = trackNumber
                NumTracks = numTracks
                Duration = duration
            }
        )



    let mapAudioPlayerInfoToMediaSessionData audioPlayerInfo =
        audioPlayerInfo.AudioBook
        |> Option.map (fun i ->
            {
                Title = i.AudioBook.FullName
                Album = i.AudioBook.FullName
                TrackNumber = audioPlayerInfo.CurrentFileIndex + 1 |> int64
                NumTracks = audioPlayerInfo.Mp3FileList.Length |> int64
                Duration = audioPlayerInfo.Duration.TotalMilliseconds |> int64
            }
        )





type AudioPlayerService2() as self =

    let store =
        Program.mkAvaloniaProgrammWithSideEffect
            PlayerElmish.init
            PlayerElmish.update
            (PlayerElmish.SideEffects.createSideEffectsProcessor self)
        |> Program.mkStore

    do
        CrossMediaManager.Current.PositionChanged.Subscribe(fun e ->
            let state =
                match CrossMediaManager.Current.State with
                | MediaPlayerState.Playing -> AudioPlayerState.Playing
                | _ -> AudioPlayerState.Stopped
                
            store.Dispatch <| StateControlMsg (UpdatePlayingState (e.Position, CrossMediaManager.Current.Duration, state))
        ) |> ignore
        
        CrossMediaManager.Current.StateChanged.Subscribe(fun e ->
            let state =
                match e.State with
                | MediaPlayerState.Playing -> AudioPlayerState.Playing
                | _ -> AudioPlayerState.Stopped
                
            store.Dispatch <| StateControlMsg (UpdatePlayingState (CrossMediaManager.Current.Position, CrossMediaManager.Current.Duration, state))
        ) |> ignore
        
        
        
        CrossMediaManager.Current.MediaItemFinished.Subscribe(fun e ->
            store.Dispatch <| PlayerControlMsg MoveToNextTrack
        ) |> ignore
    
    interface IMediaPlayer with

        member this.Play(file: string) =
            task {
                try
                    match store.Model.AudioBook with
                    | Some audioBook ->
                        let mediaItem = MediaItem()
                        mediaItem.MediaUri <- $"file://{file}"
                        mediaItem.MediaType <- MediaType.Audio
                        mediaItem.Album <- audioBook.AudioBook.FullName
                        mediaItem.Title <- audioBook.AudioBook.FullName
                        
                        mediaItem.DisplayTitle <- audioBook.AudioBook.FullName
                        let bitmap = audioBook.AudioBook.Picture |> Option.map (fun p -> BitmapFactory.DecodeFile p) |> Option.defaultValue null
                        let thumb = audioBook.AudioBook.Thumbnail |> Option.map (fun p -> BitmapFactory.DecodeFile p) |> Option.defaultValue null
                        mediaItem.Image <- bitmap
                        mediaItem.AlbumImage <- bitmap
                        
                        mediaItem.DisplaySubtitle <- $"Track {store.Model.CurrentFileIndex + 1} of {store.Model.Mp3FileList.Length}"
                        mediaItem.NumTracks <- store.Model.Mp3FileList.Length
                        mediaItem.TrackNumber <- store.Model.CurrentFileIndex + 1
                        CrossMediaManager.Current.Notification.Enabled <- true
                        CrossMediaManager.Current.Notification.ShowNavigationControls   <- true
                        CrossMediaManager.Current.Notification.ShowPlayPauseControls    <- true
                        let! _ = CrossMediaManager.Current.Play (mediaItem, store.Model.Position)
                        return ()
                    | _ ->
                        return ()
                with
                | ex ->
                     Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                     raise ex
            }


        member this.Pause() =
                task {
                    try
                        let! _ = CrossMediaManager.Current.Pause()
                        return ()
                    with
                    | ex ->
                         Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                         raise ex
                 }

        member this.PlayPause() =
            task {
                try
                    if CrossMediaManager.Current.State = MediaPlayerState.Playing then
                        let! _ = CrossMediaManager.Current.Pause()
                        return ()
                    else
                        let! _ = CrossMediaManager.Current.Play()
                        return ()
                with
                | ex ->
                     Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                     raise ex
                 }

        member this.Stop resumeOnAudioFocus =
            task {
                try
                    let! _ = CrossMediaManager.Current.Stop()
                    return ()
                with
                | ex ->
                     Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                     raise ex
            }

        member this.SeekTo(position: TimeSpan) =
            task {
                try
                    let! _ = CrossMediaManager.Current.SeekTo position
                    return ()
                with
                | ex ->
                     Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                     raise ex
            }

        member this.SetPlaybackSpeed(speed: float) =
            task {
                try
                    CrossMediaManager.Current.Speed <- speed |> float32
                    return ()
                with
                | ex ->
                     Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                     raise ex
            }
            
        member this.UpdateNotifcation() =
            ()

    interface IAudioPlayerPause with
        member this.Pause() =
            store.Dispatch <| PlayerControlMsg (Stop false)

    interface IAudioPlayer  with

        member this.Init audiobook fileList =
            store.Dispatch <| StateControlMsg (InitAudioService (audiobook, fileList))

        member this.DisableAudioPlayer() =
            store.Dispatch <| StateControlMsg (DisableAudioService)
            
        
        member this.Play () =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg Play

        member this.PlayExtern file pos =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg (PlayExtern (file, pos))

        member this.Pause() =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg (Stop false)

        member this.PlayPause () =
            if not store.Model.IsBusy then 
                if store.Model.State = AudioPlayerState.Playing then
                    store.Dispatch <| PlayerControlMsg (Stop false)
                else
                    store.Dispatch <| PlayerControlMsg Play


        member this.Stop resumeOnAudioFocus =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg (Stop resumeOnAudioFocus)

        member this.JumpBackwards () =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg JumpBackwards

        member this.JumpForward () =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg JumpForward

        member this.Next () =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg MoveToNextTrack

        member this.Previous () =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg MoveToPreviousTrack

        member this.SeekTo position =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg (GotoPosition position)

        member this.SetPlaybackSpeed speed =
            if not store.Model.IsBusy then store.Dispatch <| PlayerControlMsg (SetPlaybackSpeed speed)

        member this.StartSleepTimer sleepTime =
            if not store.Model.IsBusy then 
                match sleepTime with
                | None ->
                    store.Dispatch <| SleepTimerMsg SleepTimerStop
                | Some sleepTime ->
                    store.Dispatch <| SleepTimerMsg (SleepTimerStart sleepTime)

        member this.AudioPlayerInformation with get() = store.Model
        member this.AudioPlayerInfoChanged = store.Observable



