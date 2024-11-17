module Global

    open Avalonia.Controls
    open Microsoft.ApplicationInsights
    open Microsoft.ApplicationInsights.Extensibility

    let appcenterAndroidId      = ""
    let supportMessageApi       = ""
    let messageEndpoint         = ""
    let appInsightsConnection   = ""
    



    let config = TelemetryConfiguration.CreateDefault()
    if Design.IsDesignMode then
        config.DisableTelemetry <- true
    if Design.IsDesignMode |> not then
        config.ConnectionString <- appInsightsConnection
    let telemetryClient = new TelemetryClient(config)
    

    

    





