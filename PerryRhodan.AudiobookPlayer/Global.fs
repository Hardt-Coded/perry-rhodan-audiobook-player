﻿module Global

    let appcenterAndroidId = ""
    let supportMessageApi = ""
    let messageEndpoint = "https://einsamedienappmessages.z1.web.core.windows.net/messages.json"




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

    

    

