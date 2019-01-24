module Translations

    open Xamarin
    open Xamarin.Forms
    open System.Globalization


    type Translation = 
        { Error:string 
          Close:string
          Cancel:string
          Error_Save_Position:string
          Off:string
          Select_Sleep_Timer:string
          Error_Saving_AudioBookState:string
          Of:string
          AudioBookDescription:string
          RemoveFromDevice:string
          RemoveFromDownloaQueue:string
          DownloadAudioBook:string
          MarkAsListend:string
          UnmarkAsListend:string
          PleaseSelect:string
          LoginToEinsAMedienAccount:string
          Username:string
          Password:string
          RememberLogin:string
          Login:string
          LoginFailed:string
          ErrorLoadingLocalAudiobook:string
          AudiobookOnDevice:string
          NoAudiobooksOnDevice:string
          BrowseAudioBooks:string
          Browse:string
          LoadYourAudioBooks:string
          LoadYourAudioBooksHint:string
          ErrorRemoveAudioBook:string
          PermissionError:string
          ErrorDbWriteAccess:string
          ErrorDownloadAudiobook:string
          NetworkError:string
          NetworkTimeoutError:string
          InternalError:string
          UnexpectedServerBehaviorError:string
          SessionExpired:string
          NoDownloadUrlFoundError:string
          AudioBookAlreadyDownloadedError:string
          ProductPageEmptyError:string
          Back:string
          NewAudioBooksSinceLastRefresh:string

          MainPage:string
          LoginPage:string
          BrowserPage:string
          AudioPlayerPage:string
          PermissionDeniedPage:string
          AudioBookDetailPage:string
          SettingsPage:string


        }


        static member English =
            {   Error = "Error" 
                Close = "Close"
                Cancel="Cancel"
                Error_Save_Position = "Error saving Position."
                Off = "off"
                Select_Sleep_Timer="Select Sleep Timer ..."
                Error_Saving_AudioBookState = "Error save audiobook state!"
                Of = "of"
                AudioBookDescription="Audiobook Description"
                RemoveFromDevice="Remove from Device"
                RemoveFromDownloaQueue="Remove from Download Queue"
                DownloadAudioBook="Download Audiobook"
                MarkAsListend="Mark as Listend"
                UnmarkAsListend="Unmark as Listend"
                PleaseSelect="Please Select ..."
                LoginToEinsAMedienAccount="Please login to your Eins A Medien Account."
                Username="Enter Username"
                Password="Enter Password"
                RememberLogin="remember login data"
                Login = "Login"
                LoginFailed="Login failed!"
                ErrorLoadingLocalAudiobook="Error on loading Local Audiobooks."
                AudiobookOnDevice="Audiobooks On Device"
                NoAudiobooksOnDevice="There are currently no audiobooks on your device. Use the magnify symbol in the upper right corner to browse your online audio books."
                BrowseAudioBooks="Browse your Audiobooks"
                Browse="Browse"
                LoadYourAudioBooks="Refresh Audiobooks"
                LoadYourAudioBooksHint="Press 'Refresh Audiobooks' to get your current available audiobooks from your Eins A Medien Account"
                ErrorRemoveAudioBook="Error Remove Audiobook."
                PermissionError="Sorry without Permission the App is not useable! Because this App stores the audio books and the state file on your public storage. So you can access this data without any hacking."
                ErrorDbWriteAccess="error storing audiobook data into database."
                ErrorDownloadAudiobook="Error on downloading Audiobook."
                NetworkError="Network Error. Please check your internet connection and try again."
                NetworkTimeoutError="Network Timeout Error. Please check your internet connection and try again."
                InternalError="Internal App Error. Please try again or contact support."
                UnexpectedServerBehaviorError="Unexpected Server Behavior. Please try again or contact support."
                SessionExpired="Online Session expired!"
                NoDownloadUrlFoundError="no download url for this audiobook available."
                AudioBookAlreadyDownloadedError="Audiobook already downloaded"
                ProductPageEmptyError="ProductPage response is empty."
                Back="Back"
                NewAudioBooksSinceLastRefresh="New Audiobooks since last refresh:"

                MainPage="Home"
                LoginPage="Login"
                BrowserPage="Browse"
                AudioPlayerPage="Player"
                PermissionDeniedPage="Permission Error"
                AudioBookDetailPage="Detail"
                SettingsPage="Settings"
            }

    
        static member German =
            {   Error = "Fehler" 
                Close = "Schließen"
                Cancel="Abbrechen"
                Error_Save_Position = "Fehler beim Speichern der aktuellen Hörbuchposition."
                Off = "aus"
                Select_Sleep_Timer="Einschlafzeit wählen ..."
                Error_Saving_AudioBookState = "Fehler beim Speichern des aktuellen Hörbuch-Status!"
                Of="von"
                AudioBookDescription="Hörbuchbeschreibung"
                RemoveFromDevice="Vom Gerät löschen"
                RemoveFromDownloaQueue="Aus der Warteschlange entfernen"
                DownloadAudioBook="Hörbuch runterladen"
                MarkAsListend="Als gehört markieren"
                UnmarkAsListend="Als ungehört markieren"
                PleaseSelect="Bitte auswählen ..."
                LoginToEinsAMedienAccount="Bitte loggen Sie sich in Ihren Eins A Medien Account ein."
                Username="Benutzername eingeben..."
                Password="Passwort eingeben..."
                RememberLogin="Zugangsdaten merken"
                Login = "Einloggen"
                LoginFailed="Einloggen ist fehlgeschlagen."
                ErrorLoadingLocalAudiobook="Fehler beim Laden der auf dem Gerät gespeicherten Hörbücher."
                AudiobookOnDevice="Hörbücher auf dem Gerät"
                NoAudiobooksOnDevice="Es befinden sich derzeit keine Hörbücher auf Ihrem Gerät. Benutzen Sie bitte das oben rechts befindliche Lupensymbol, um Ihre Hörbücher auf Eins A Medien zu durchsuchen."
                BrowseAudioBooks="Ihre Hörbücher durchsuchen"
                Browse="Durchsuche"
                LoadYourAudioBooks="Hörbücher aktualisieren"
                LoadYourAudioBooksHint="Drücken Sie 'Hörbücher aktualisieren', um Ihre aktuellen Eins A Medien Hörbücher anzuzeigen."
                ErrorRemoveAudioBook="Fehler beim Löschen des Hörbuchs."
                PermissionError="Sorry, aber ohne die Berechtigungen kann die App nicht funktionieren. Die App speichert die Hörbücher und das Status-File direkt auf deinem öffentlichen Speicher. Damit du direkt Zugriff auf die Dateien hast, ohne irgendeinen Hack zu machen."
                ErrorDbWriteAccess="Fehler beim Speichern in die Datenbank."
                ErrorDownloadAudiobook="Fehler beim Laden des Hörbuchs."
                NetworkError="Netzwerkfehler. Bitte prüfen Sie Ihre Internetverbindung und versuchen Sie es nochmal."
                NetworkTimeoutError="Netzwerkfehler (Timeout). Bitte prüfen Sie Ihre Internetverbindung und versuchen Sie es nochmal."
                InternalError="Interner Programmfehler. Bitte nochmal probieren oder den Support anschreiben."
                UnexpectedServerBehaviorError="Unerwartetes Verhalten des Hörbuchservers. Bitte versuchen Sie es nochmal oder schreiben Sie den Support an."
                SessionExpired="Ihre Onlinesession ist abgelaufen!"
                NoDownloadUrlFoundError="Für dieses Hörbuch wurde keine Download gefunden."
                AudioBookAlreadyDownloadedError="Hörbuch ist bereits runtergeladen."
                ProductPageEmptyError="Die Produktseite gibt keine Daten zurück."
                Back="Zurück"
                NewAudioBooksSinceLastRefresh="Neue Hörbücher seit der letzten Aktualisierung:"

                MainPage="Startseite"
                LoginPage="Login"
                BrowserPage="Durchsuchen"
                AudioPlayerPage="Player"
                PermissionDeniedPage="Berechtigungsfehler"
                AudioBookDetailPage="Hörbuchinfo"
                SettingsPage="Einstellungen"
            }



    let current =
        match CultureInfo.CurrentUICulture.TwoLetterISOLanguageName with
        | "de" | "DE" | "De" -> Translation.German
        | _ -> Translation.English
        
        

