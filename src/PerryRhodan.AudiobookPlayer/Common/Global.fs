module Global

    let appcenterAndroidId = ""
    let supportMessageApi = ""
    let messageEndpoint = ""
    let appInsightsKey = ""



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





