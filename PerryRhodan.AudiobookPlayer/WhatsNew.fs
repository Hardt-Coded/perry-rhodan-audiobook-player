module WhatsNew

open Fabulous




    module Text =

        let messages = [
            ("2020.06.06-dsdfe",
                """
Hallo Liebe Mitfans =)

Was gibt neues in dieser Version:

- Downloads laufen jetzt im Hintergrund:

Downloads laufen jetzt in einem Hintergrundprozess, damit soll verhindert werden, dass wenn das Telefon die App automatisch beendet, der Download abbricht.

- Anpassung der Titelerkennung, sowie Reperatur der Titel:

Wichtig! Damit die Titel "repariert" werden, müssen unter "Browse" einmal die Titel aktualisiert werden.

Da ich als Entwickler noch keine direkte Schnittstelle zu Einsamedien haben, muss ich die Titel und Gruppen automatisch anhand der Einträge auf der Shop-Download-Seite erkennen.
Wie ihr sicher verstehen, kann es so oftmal zu Fehlern in der Erkennung kommen. Ich habe einige Fälle jetzt angepasst. Darunter Warhammer 40k, die Perry Rhodan Storys und Mission SOL 2. (Danke Thomas)
Ich bin dabei auf eurte Mithilfe angewiesen, denn ich habe nicht alle Hörbücher, die es gibt. Wenn ihr was seht, schreibt mir eine Nachricht unter Support-Feedback oder an info@hardt-solutions.de. Danke


- Gesamtfortschitt im Hörbuch:

Wieviel euch noch von dem Hörbuch verbleibt, wird jetzt auch angezeigt. Einmal als kleine "Torte" links im Hörbuch-Bild oder eben als Text unter dem Titel.
Außerdem noch auf der Player-Seite.

- Design-Anapssungen:

Auf der Playerseite ist jetzt das Cover mehr "präsent". Dazu noch ein paar andere Kleinigkeiten.

- Bugfixes:

Das merkwürdige Verhalten des Einschlaftimers ist behoben.

Über Anregungen und Feedback würde ich mich freuen. Wie gesagtr unter "Support-Feedback" in den Optionen oder direkt an info@hardt-solutions.de.
Über eine Bewertung würde ich mich freuen.
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



