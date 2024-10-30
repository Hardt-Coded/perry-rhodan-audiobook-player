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
            (PlayerElmish.SideEffects.createSideEffectsProcessor ())
        |> Program.mkStore


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



