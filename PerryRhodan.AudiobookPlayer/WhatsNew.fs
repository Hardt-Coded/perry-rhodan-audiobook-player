module WhatsNew

open Fabulous




    module Text =

        let messages = [
            ("2020.06.06-dfgdfg",
                """
Hallo.   
                
Dies ist ein Testtext!
                
                """
            )
        ]

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
            

let getLatestMessage () =
    Text.messages |> List.head

let displayLatestMessage () =
    async {
        let latestMessage = Text.messages |> List.head
        let! confimed = fst latestMessage |> Helpers.isMessageConfirmed
        if (not confimed) then
            do! Common.Helpers.displayAlert (fst latestMessage, snd latestMessage, "OK")
            do! fst latestMessage |> Helpers.confirmMessage
    }



