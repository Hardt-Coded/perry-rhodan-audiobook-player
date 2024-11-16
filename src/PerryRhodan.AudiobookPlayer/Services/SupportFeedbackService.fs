namespace PerryRhodan.AudiobookPlayer.Services

open System.Net
open FsHttp
open Newtonsoft.Json
open PerryRhodan.AudiobookPlayer




module SupportFeedback =

    type Message = {
        Category:string
        Name:string
        Message:string
    }

    let sendSupportFeedBack name category message =
        task {
            let msg = {
                Category = category
                Message = message
                Name = name
            }
            let jsonStr = JsonConvert.SerializeObject(msg)
            // use FsHttp to send the message

            try
                let! res =
                    http {
                        POST Global.supportMessageApi
                        body
                        json jsonStr
                        config_transformHttpClient (WebAccessCommon.useAndroidHttpClient false)
                    }
                    |> Request.sendTAsync


                if res.statusCode <> HttpStatusCode.Accepted then
                    return Error "Fehler beim Senden der Nachricht. Probieren Sie es noch einmal."
                else
                    return Ok ()
            with
            | ex ->
                Global.telemetryClient.TrackException ex
                return Error "Fehler beim Senden der Nachricht. Probieren Sie es noch einmal."
        }

