namespace PerryRhodan.AudiobookPlayer.ViewModels

open Domain
open Services.WebAccess.Downloader

type IAudioBookItemViewModel =
    abstract member SetUploadDownloadState:(int*int)->unit
    abstract member SetDownloadCompleted:DownloadResult->unit
    abstract member SetAudioFileList:AudioBookAudioFilesInfo->unit
    abstract member AudioBook:AudioBook

    
type ILoginViewModel = interface end


type IActionMenuService =
    abstract member ShowAudiobookActionMenu:IAudioBookItemViewModel->unit
    
type IMainViewModel =
    abstract member GotoPlayerPage:viewModel:IAudioBookItemViewModel->startPlaying:bool->unit
    abstract member GotoHomePage:unit->unit

type IMainViewAccessService =
    abstract member GetMainViewModel:unit->IMainViewModel
    
