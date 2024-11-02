﻿module Global

    open Microsoft.ApplicationInsights
    open Microsoft.ApplicationInsights.Extensibility

    let appcenterAndroidId      = ""
    let supportMessageApi       = ""
    let messageEndpoint         = ""
    let appInsightsConnection   = ""


    let config = TelemetryConfiguration.CreateDefault()
    config.ConnectionString <- appInsightsConnection
    let telemetryClient = new TelemetryClient(config)
    

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





