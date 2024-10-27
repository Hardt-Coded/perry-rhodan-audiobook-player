﻿module PerryRhodan.AudiobookPlayer.Services.Interfaces

    open System
    open System.Threading.Tasks
    open Domain
    
    type IAudioPlayerServiceController =
        abstract member StartService : unit -> unit
        abstract member StopService : unit -> unit
    
    
    [<RequireQualifiedAccess>]
    type AudioPlayerState =
        | Playing
        | Stopped
    
        
    
    
        
    type IMediaPlayer =    
        abstract member Play : string -> Task<unit>
        abstract member Pause : unit -> unit
        abstract member PlayPause : unit -> unit
        abstract member Stop : resumeOnAudioFocus:bool -> unit
        abstract member SeekTo : TimeSpan -> unit
        abstract member SetPlaybackSpeed : float -> Task<unit>
        abstract member UpdateNotifcation : unit -> unit
        
    and AudioPlayerInformation  = {
        State: AudioPlayerState
        Duration: TimeSpan
        CurrentPosition: TimeSpan
    }
        with static member Empty = {
                State = AudioPlayerState.Stopped
                Duration = TimeSpan.Zero
                CurrentPosition = TimeSpan.Zero
            }
        
        
    
        
        
        
        