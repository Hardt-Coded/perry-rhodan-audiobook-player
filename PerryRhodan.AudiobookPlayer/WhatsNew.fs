module WhatsNew

open FsHttp
open Microsoft.AppCenter.Crashes
open System.Net


    module Helpers =

        open Services
        
        let isMessageConfirmed id =
            async {
                let! value = (SecureStorageHelper.getSecuredValue id)
                return value |> Option.map (fun i -> i = "true") |> Option.defaultValue false
            }

        let confirmMessage id =
            async {
                do! SecureStorageHelper.setSecuredValue "true" id
            }


          

let getLatestMessageAsync () =
    async {
        try
            
            if Global.messageEndpoint = "" then
                return None
            else
                let! resp =
                    http {
                        GET Global.messageEndpoint
                    }
                    |> Request.sendAsync

                if resp.statusCode <> HttpStatusCode.OK then
                    Crashes.TrackError (exn ($"Error loading Messages from '{Global.messageEndpoint}' StatusCode: {resp.statusCode}"), Map.empty)
                    return None
                else
                    let! messsageJsonArray =
                        resp
                        |> Response.toJsonArrayAsync

                    return
                        messsageJsonArray 
                        |> Array.tryHead
                        |> Option.map (fun json ->
                            (json.GetProperty("date").GetString(), json.GetProperty("message").GetString())
                        )
        with
        | ex ->
            Crashes.TrackError(ex, Map.empty)
            return None
    }
    

let displayLatestMessage () =
    async {
        let! latestMessage = getLatestMessageAsync ()
        match latestMessage with
        | None ->
            return ()
        | Some latestMessage ->
            let! confimed = fst latestMessage |> Helpers.isMessageConfirmed
            if (not confimed) then
                do! Common.Helpers.displayAlert (fst latestMessage, snd latestMessage, "OK")
                do! fst latestMessage |> Helpers.confirmMessage
    }



