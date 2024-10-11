module PerryRhodan.AudiobookPlayer.Services.Interfaces

    open System
    open System.Threading.Tasks
    open Domain
    
    [<RequireQualifiedAccess>]
    type AudioPlayerState =
        | Playing
        | Stopped
        
      
    type AudioPlayerInformation  = {
        State: AudioPlayerState
        Duration: TimeSpan
        CurrentPosition: TimeSpan
    } with static member Empty = {
            State = AudioPlayerState.Stopped
            Duration = TimeSpan.Zero
            CurrentPosition = TimeSpan.Zero
        }
        
    
    type IAudioPlayer =
        abstract member StartService : unit -> Task<unit>
        abstract member StopService : unit -> Task<unit>
        abstract member SetMetaData: audiobook:AudioBook -> numberOfTracks:int -> curentTrack:int -> unit
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
        
        
    
        
        
        
        