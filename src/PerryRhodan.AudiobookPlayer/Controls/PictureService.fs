namespace PerryRhodan.AudiobookPlayer.Services

open System.IO
open PerryRhodan.AudiobookPlayer.ViewModel
open System.Threading.Tasks
open PerryRhodan.AudiobookPlayer.Common
open Services
open SkiaSharp



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
                            |> Task.map (fun bitmap ->
                                match bitmap with
                                | None -> ()
                                | Some bitmap ->
                                    let folders = Consts.createCurrentFolders()
                                    let audioBookPath = Path.Combine(folders.audioBookDownloadFolderBase,$"{audiobook.AudioBook.Id}")
                                    if not <| Directory.Exists audioBookPath then Directory.CreateDirectory audioBookPath |> ignore
                                    let picturePath = Path.Combine(audioBookPath, $"{audiobook.AudioBook.Id}.jpg")
                                    let thumbFullName = Path.Combine(audioBookPath,$"{audiobook.AudioBook.Id}.thumb.jpg")
                                    SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Jpeg, 100).AsStream().CopyTo(File.Create(picturePath))
                                    use thumb = bitmap.Resize(SKImageInfo(200, 200), SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                                    SKImage.FromBitmap(thumb).Encode(SKEncodedImageFormat.Jpeg, 100).AsStream().CopyTo(File.Create(thumbFullName))
                                    // save picture path
                                    audiobook.SetPicture picturePath thumbFullName
                                    let color = AudioBookItem.SideEffects.Helpers.getAmbientColorFromSkBitmap bitmap
                                    // save ambient color
                                    audiobook.SetAmbientColor color
                                    ()
                            )
                with
                | ex ->
                    // log
                    Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
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
                    Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                    Global.telemetryClient.TrackException ex
            }
    
    

