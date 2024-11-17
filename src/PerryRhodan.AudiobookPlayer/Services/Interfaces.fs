module PerryRhodan.AudiobookPlayer.Services.Interfaces

    open System
    open System.Net
    open System.Net.Http
    open System.Threading.Tasks
    open Domain

    type IAudioPlayerServiceController =
        abstract member StartService : unit -> unit
        abstract member StopService : unit -> unit


    [<RequireQualifiedAccess>]
    type AudioPlayerState =
        | Playing
        | Stopped



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


    type IAndroidDownloadFolder =
        abstract member GetAndroidDownloadFolder:unit -> string

    type INotificationService =
        abstract ShowMessage : string->string -> unit

    type IDownloadService =
        abstract member StartDownload: Shop -> unit
        
    type IPictureDownloadService =
        abstract member StartDownload: Shop -> unit

    type IAndroidHttpMessageHandlerService =
        abstract member GetHttpMesageHandler: unit -> HttpMessageHandler
        abstract member GetCookieContainer: unit -> CookieContainer
        abstract member SetAutoRedirect: bool -> unit

    type ICloseApplication =
        abstract member CloseApplication: unit -> unit

    type IScreenService =
        abstract member GetScreenSize: unit -> {| Width:int; Height:int; ScaledDensity: float |}

    type INavigationService =
        abstract BackbuttonPressedAction:(unit->unit) option with get
        abstract member RegisterBackbuttonPressed: (unit -> unit) -> unit
        abstract member ResetBackbuttonPressed: unit -> unit
        abstract member MemorizeBackbuttonCallback: memoId:string -> unit
        abstract member RestoreBackbuttonCallback: memoId:string -> unit


    type ISecureStorageHelper =
        abstract member ClearSecureStoragePreferences: unit -> unit


    type IOldShopDatabase = 
        abstract member Base: IDatabaseProcessor
        
    and INewShopDatabase =
        abstract member Base: IDatabaseProcessor
    
    and IDatabaseProcessor =
        inherit IOldShopDatabase
        inherit INewShopDatabase
        
        abstract member LoadAudioBooksStateFile:                unit -> Task<AudioBook[]>
        abstract member LoadDownloadedAudioBooksStateFile:      unit -> Task<AudioBook[]>
        abstract member InsertNewAudioBooksInStateFile:         audiobooks:AudioBook[] -> Task<Result<unit, string>>
        abstract member UpdateAudioBookInStateFile:             audiobook:AudioBook -> Task<Result<unit, string>>
        abstract member RemoveAudiobookFromDatabase:            audiobook:AudioBook -> Task<Result<unit, string>>
        abstract member DeleteAudiobookDatabase:                unit -> unit
        abstract member GetAudioBookFileInfo:                   id:int -> Task<AudioBookAudioFilesInfo option>
        abstract member GetAudioBookFileInfoTimeout:            timeout:int -> id:int -> AudioBookAudioFilesInfo option
        abstract member InsertAudioBookFileInfos:               fileInfos:AudioBookAudioFilesInfo[] -> Async<Result<unit, string>>
        abstract member UpdateAudioBookFileInfo:                fileInfo:AudioBookAudioFilesInfo -> Task<Result<unit, string>>
        abstract member DeleteAudioBookFileInfo:                id:int -> Task<Result<unit, string>>
        abstract member RemoveAudiobook:                        audiobook:AudioBook -> Task<Result<unit, string>>

    