module PerryRhodan.AudiobookPlayer.Services.Interfaces

    open System
    open Domain
    
    [<RequireQualifiedAccess>]
    type AudioPlayerState =
        | Playing
        | Stopped
        
      
    type AudioPlayerInformation  = {
        State: AudioPlayerState
        Duration: TimeSpan
        CurrentPosition: TimeSpan
    }
        
    
    type IAudioPlayer =
        abstract member StartService : unit -> unit
        abstract member StopService : unit -> unit
        abstract member Init: audiobook:AudioBook -> cuurentTrack:int -> unit
        abstract member Play : string -> unit
        abstract member Pause : unit -> unit
        abstract member PlayPause : unit -> unit
        abstract member Stop : unit -> unit
        abstract member SeekTo : TimeSpan -> unit
        abstract member SetPlaybackSpeed : float -> unit
        abstract member Duration : TimeSpan
        abstract member CurrentPosition : TimeSpan
        // observable audio player information
        abstract member AudioPlayerInformation : IObservable<AudioPlayerInformation>
        // event audio player audioplayer finished track
        abstract member AudioPlayerFinishedTrack : IObservable<unit>
        
        
    
        
        
        
        