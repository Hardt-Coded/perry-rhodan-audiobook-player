module Global

    let appcenterAndroidId = ""


    type Language =
        | English
        | German


    type Pages = 
        | MainPage
        | LoginPage
        | BrowserPage
        | AudioPlayerPage
        | PermissionDeniedPage
        | AudioBookDetailPage
        | SettingsPage


    type LoginRequestCameFrom =
        | RefreshAudiobooks
        | DownloadAudioBook

    type AudioPlayerState =
        | Playing
        | Stopped

    type AudioPlayerInfo =
        { Filename: string
          Position: int
          Duration: int
          CurrentTrackNumber: int
          State: AudioPlayerState }
        
        static member Empty =
            { Filename = ""
              Position = 0
              Duration = 0
              CurrentTrackNumber = 0
              State = Stopped }

