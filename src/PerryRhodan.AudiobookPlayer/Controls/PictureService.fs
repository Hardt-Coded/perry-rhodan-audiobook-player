namespace PerryRhodan.AudiobookPlayer.Services

open Domain
open PerryRhodan.AudiobookPlayer
open PerryRhodan.AudiobookPlayer.ViewModel
open FsToolkit.ErrorHandling



type PictureDownloadService (shop: Shop,notificationCallback: (string -> unit), finishedCallback: unit->unit) =

    let addOnlineUrlForPictureForAllWhichHasNone
        (audioBooks:AudioBookItemViewModel array) =
        task {
            // split audiobook in 5 packages
            let audioBooksWithoutPics =
                match shop with
                | OldShop ->
                    audioBooks
                    |> Array.filter (_.AudioBook.Picture.IsNone)
                | NewShop ->
                    audioBooks
                    |> Array.filter (_.AudioBook.AmbientColor.IsNone)


            for idx, audiobook in audioBooksWithoutPics |> Array.indexed do
                try
                    notificationCallback $"Bild {(idx+1)} von {audioBooksWithoutPics.Length} runtergeladen!"
                    match shop with
                    | OldShop ->
                        let! url = OldShopWebAccessService.getPictureOnlineUrl audiobook.AudioBook
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
                    | NewShop ->
                        match audiobook.AudioBook.Picture with
                        | None ->
                            // the new shop should always have already a picture
                            return ()
                        | Some pictureUrl ->
                            // Only generate ambient color for new shop
                            do!
                                AudioBookItem.SideEffects.Helpers.getBitmapFromUrl pictureUrl
                                |> Task.bind (fun bitmap ->
                                    task {
                                        match bitmap with
                                        | None -> ()
                                        | Some bitmap ->
                                            let! color =
                                                AudioBookItem.SideEffects.Helpers.getAmbientColorFromPicUrl pictureUrl
                                            color |> Option.iter audiobook.SetAmbientColor
                                            ()
                                    }
                                )
                            return ()
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



