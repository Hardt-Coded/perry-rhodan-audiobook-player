module Global

    let appcenterAndroidId = "e806b20e-0e4c-4209-81c1-9ff48478f932"
    let supportMessageApi = "https://prod-86.westeurope.logic.azure.com:443/workflows/0a2e452eb2aa4546bb6ce267e7cb3c28/triggers/manual/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=0DtXE6NS5QzYoAqH1_vuQfhIvg48VqGp16st4tS55fg"
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

    

    

