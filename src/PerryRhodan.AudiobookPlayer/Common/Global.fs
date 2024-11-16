module Global

    open Microsoft.ApplicationInsights
    open Microsoft.ApplicationInsights.Extensibility

    let appcenterAndroidId      = "***REMOVED***"
    let supportMessageApi       = "***REMOVED***"
    let messageEndpoint         = "https://einsamedienappmessages.z1.web.core.windows.net/messages.json"
    let appInsightsConnection   = "***REMOVED***"
    



    let config = TelemetryConfiguration.CreateDefault()
    config.ConnectionString <- appInsightsConnection
    let telemetryClient = new TelemetryClient(config)
    

    

    





