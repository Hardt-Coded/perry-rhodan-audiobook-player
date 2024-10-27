﻿module Global

    let appcenterAndroidId = "e806b20e-0e4c-4209-81c1-9ff48478f932"
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





