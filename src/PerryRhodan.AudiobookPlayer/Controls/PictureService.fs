namespace PerryRhodan.AudiobookPlayer.Services

open System.IO
open System.Net.Http
open Domain
open PerryRhodan.AudiobookPlayer
open PerryRhodan.AudiobookPlayer.ViewModel
open FsToolkit.ErrorHandling
open PerryRhodan.AudiobookPlayer.Views
open SkiaSharp


module BitmapHelper =
    
    let getBitmapFromUrl (url: string) =
        task {
            try
                use client = new HttpClient()
                let! imageData = client.GetByteArrayAsync(url)
                use stream = new MemoryStream(imageData)
                let bitmap = SKBitmap.Decode(stream)
                return if bitmap = null || bitmap.IsEmpty || bitmap.IsNull then None else Some bitmap
            with
            | ex ->
                Global.telemetryClient.TrackException (ex, [("url", url)] |> Map.ofList)
                #if DEBUG
                System.Diagnostics.Debug.WriteLine($"Error loading image from url: {url}")
                #endif
                return None
        }

    let getAmbientColorFromSkBitmap (bitmap:SKBitmap) =
        let bitmap = bitmap.Resize(SkiaSharp.SKImageInfo(5,5, SKColorType.Rgb888x), SKSamplingOptions(SKFilterMode.Nearest))
        let getHue (x,_,_) = x
        let hues =
            [
                bitmap.GetPixel(0,1).ToHsv() |> getHue
                bitmap.GetPixel(0,2).ToHsv() |> getHue
                bitmap.GetPixel(0,3).ToHsv() |> getHue
                bitmap.GetPixel(0,4).ToHsv() |> getHue
                bitmap.GetPixel(1,4).ToHsv() |> getHue
                bitmap.GetPixel(2,4).ToHsv() |> getHue
                bitmap.GetPixel(3,4).ToHsv() |> getHue
                bitmap.GetPixel(4,4).ToHsv() |> getHue
                bitmap.GetPixel(4,3).ToHsv() |> getHue
                bitmap.GetPixel(4,2).ToHsv() |> getHue
                bitmap.GetPixel(4,1).ToHsv() |> getHue
                bitmap.GetPixel(4,0).ToHsv() |> getHue
                bitmap.GetPixel(3,0).ToHsv() |> getHue
                bitmap.GetPixel(2,0).ToHsv() |> getHue
                bitmap.GetPixel(3,3).ToHsv() |> getHue
            ]

        let avgeHue = hues |> List.average
        let aboveAvg = hues |> List.filter (fun x -> x > avgeHue)
        let belowAvg = hues |> List.filter (fun x -> x < avgeHue)
        let avgHue =
            if aboveAvg.Length > belowAvg.Length then
                aboveAvg |> List.average
            elif aboveAvg.Length < belowAvg.Length then
                belowAvg |> List.average
            else
                avgeHue

        let avgHueColor = SkiaSharp.SKColor.FromHsv(avgHue, 100.0f, 50.0f)
        avgHueColor.ToString()


    let getAmbientColorFromPicUrl (url:string) =
        task {
            try
                return!
                    getBitmapFromUrl url
                    |> Task.map (
                        fun t -> t |> Option.map (getAmbientColorFromSkBitmap)
                    )
            with
            | ex ->
                Global.telemetryClient.TrackException ex
                return None

        }

    let getAmbientColorFromPicFile (picture:string) =
        try
            SkiaSharp.SKBitmap.Decode picture
            |> Option.ofObj
            |> Option.map getAmbientColorFromSkBitmap
        with
        | ex ->
            Global.telemetryClient.TrackException ex
            None


type PictureDownloadService (shop: Shop,notificationCallback: (string -> unit), finishedCallback: unit->unit) =

    let addOnlineUrlForPictureForAllWhichHasNone
        (audioBooks:AudioBookItemViewModel array) =
        task {
            let audioBooksWithoutPics =
                match shop with
                | OldShop ->
                    audioBooks
                    |> Array.filter (_.AudioBook.Picture.IsNone)
                | NewShop ->
                    audioBooks
                    |> Array.filter (_.AudioBook.AmbientColor.IsNone)


            let processOnlyAmbientColor (audiobook:AudioBookItemViewModel) =
                task {
                    match audiobook.AudioBook.Picture with
                    | None ->
                        // the new shop should always have already a picture
                        return ()
                    | Some pictureUrl ->
                        // Only generate ambient color for new shop
                        do!
                            BitmapHelper.getBitmapFromUrl pictureUrl
                            |> Task.bind (fun bitmap ->
                                task {
                                    match bitmap with
                                    | None -> ()
                                    | Some bitmap ->
                                        let! color =
                                            BitmapHelper.getAmbientColorFromPicUrl pictureUrl
                                        color |> Option.iter audiobook.SetAmbientColor
                                        ()
                                }
                            )
                        return ()    
                }
                
            
            
            for idx, audiobook in audioBooksWithoutPics |> Array.indexed do
                try
                    match shop with
                    | OldShop ->
                        notificationCallback $"Bild {(idx+1)} von {audioBooksWithoutPics.Length} runtergeladen!"
                        let! url = OldShopWebAccessService.getPictureOnlineUrl audiobook.AudioBook
                        match url with
                        | None ->
                            ()
                        | Some url ->
                            do!
                                BitmapHelper.getBitmapFromUrl url
                                |> Task.bind (fun bitmap ->
                                    task {
                                        match bitmap with
                                        | None -> ()
                                        | Some bitmap ->

                                            audiobook.SetPicture (Some url) (Some url)
                                            let! color =
                                                BitmapHelper.getAmbientColorFromPicUrl url

                                            color |> Option.iter audiobook.SetAmbientColor
                                            ()
                                    }
                                )
                    | NewShop ->
                        notificationCallback $"Ambient Color {(idx+1)} von {audioBooksWithoutPics.Length} ermittelt!"
                        do! processOnlyAmbientColor audiobook
                with
                | ex ->
                    // log
                    Global.telemetryClient.TrackException ex
                    ()
                    
            // old shop should also get the ambient color, if needed in a second round
            if shop = OldShop then
                let audioBooksWithoutAmbientColor =
                    audioBooks
                    |> Array.filter (_.AudioBook.AmbientColor.IsNone)
                    
                for idx, audiobook in audioBooksWithoutAmbientColor |> Array.indexed do
                    notificationCallback $"Ambient Color {(idx+1)} von {audioBooksWithoutPics.Length} ermittelt!"
                    do! processOnlyAmbientColor audiobook
                    
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



