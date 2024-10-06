module PerryRhodan.AudiobookPlayer.Android


open Android.Content
open AndroidX.Media3.ExoPlayer
open Com.Google.Android.Exoplayer2


// an exo player wrapper
type AudioPlayer(context:Context) =
    
    
    let exoplayer = (new IExoPlayer.Builder(context)).Build()
    
    // init the player
    let initPlayer(fileList) =
        exoplayer.AddMediaItem(MediaItem.FromUri(fileList))
        { new IPlayer.IListener with
            member this.OnPlayerStateChanged(playWhenReady, playbackState) = failwith "todo"
            member this.OnAudioAttributesChanged(audioAttributes) = failwith "todo"
            member this.OnAudioSessionIdChanged(audioSessionId) = failwith "todo"
            member this.OnAvailableCommandsChanged(availableCommands) = failwith "todo"
            member this.OnCues(cueGroup) = failwith "todo"
            member this.OnDeviceInfoChanged(deviceInfo) = failwith "todo"
            member this.OnDeviceVolumeChanged(volume, muted) = failwith "todo"
            member this.OnEvents(player, events) = failwith "todo"
            member this.OnIsLoadingChanged(isLoading) = failwith "todo"
            member this.OnIsPlayingChanged(isPlaying) = failwith "todo"
            member this.OnLoadingChanged(isLoading) = failwith "todo"
            member this.OnMaxSeekToPreviousPositionChanged(maxSeekToPreviousPositionMs) = failwith "todo"
            member this.OnMediaItemTransition(mediaItem, reason) = failwith "todo"
            member this.OnMediaMetadataChanged(mediaMetadata) = failwith "todo"
            member this.OnMetadata(metadata) = failwith "todo"
            member this.OnPlayWhenReadyChanged(playWhenReady, reason) = failwith "todo"
            member this.OnPlaybackParametersChanged(playbackParameters) = failwith "todo"
            member this.OnPlaybackStateChanged(playbackState) = failwith "todo"
            member this.OnPlaybackSuppressionReasonChanged(playbackSuppressionReason) = failwith "todo"
            member this.OnPlayerError(error) = failwith "todo"
            member this.OnPlayerErrorChanged(error) = failwith "todo"
            member this.OnPlaylistMetadataChanged(mediaMetadata) = failwith "todo"
            member this.OnPositionDiscontinuity(reason) = failwith "todo"
            member this.OnRenderedFirstFrame() = failwith "todo"
            member this.OnRepeatModeChanged(repeatMode) = failwith "todo"
            member this.OnSeekBackIncrementChanged(seekBackIncrementMs) = failwith "todo"
            member this.OnSeekForwardIncrementChanged(seekForwardIncrementMs) = failwith "todo"
            member this.OnSeekProcessed() = failwith "todo"
            member this.OnShuffleModeEnabledChanged(shuffleModeEnabled) = failwith "todo"
            member this.OnSkipSilenceEnabledChanged(skipSilenceEnabled) = failwith "todo"
            member this.OnSurfaceSizeChanged(width, height) = failwith "todo"
            member this.OnTimelineChanged(timeline, reason) = failwith "todo"
            member this.OnTrackSelectionParametersChanged(parameters) = failwith "todo"
            member this.OnTracksChanged(tracks) = failwith "todo"
            member this.OnVideoSizeChanged(videoSize) = failwith "todo"
            member this.OnVolumeChanged(volume) = failwith "todo"
        }
        exoplayer.AddListener()
        
    
    
