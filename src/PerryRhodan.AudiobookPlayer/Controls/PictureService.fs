namespace PerryRhodan.AudiobookPlayer.Services

open PerryRhodan.AudiobookPlayer.ViewModel
open FsToolkit.ErrorHandling



type PictureDownloadService (notificationCallback: (string -> unit), finishedCallback: unit->unit) =

    let addOnlineUrlForPictureForAllWhichHasNone
        (audioBooks:AudioBookItemViewModel array) =
        task {
            // split audiobook in 5 packages
            let audioBooksWithoutPics =
                audioBooks
                |> Array.filter (_.AudioBook.Picture.IsNone)


            for idx, audiobook in audioBooksWithoutPics |> Array.indexed do
                try
                    notificationCallback $"Bild {(idx+1)} von {audioBooksWithoutPics.Length} runtergeladen!"
                    let! url = Services.WebAccess.getPictureOnlineUrl audiobook.AudioBook
                    match url with
                    | None ->
                        ()
                    | Some url ->
                        do!
                            AudioBookItem.SideEffects.Helpers.getBitmapFromUrl url
                            |> Task.bind (fun bitmap ->
                                task {
                                    match bitmap with
                                    | None -> ()
                                    | Some bitmap ->

                                        audiobook.SetPicture (Some url) (Some url)
                                        let! color =
                                            AudioBookItem.SideEffects.Helpers.getAmbientColorFromPicUrl url

                                        color |> Option.iter audiobook.SetAmbientColor
                                        ()
                                }
                            )
                with
                | ex ->
                    // log
                    Global.telemetryClient.TrackException ex
                    ()
            return ()
        }


    member this.StartDownloadPictures items =
            task {
                try
                    notificationCallback "Starte Download der Bilder..."
                    do! addOnlineUrlForPictureForAllWhichHasNone items
                    notificationCallback "Bilder heruntergeladen!"
                    finishedCallback()
                with
                | ex ->
                    // log
                    Global.telemetryClient.TrackException ex
            }



