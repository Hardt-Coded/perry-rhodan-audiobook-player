namespace PerryRhodan.AudiobookPlayer.Services

open PerryRhodan.AudiobookPlayer.ViewModel
open System.Threading.Tasks




type PictureDownloadService (notificationCallback: (string -> unit), finishedCallback: unit->unit) =
    
    [<Literal>]
    let parallelDownloads = 2    
    
    let addOnlineUrlForPictureForAllWhichHasNone
        (audioBooks:AudioBookItemViewModel array) =
        task {
            // split audiobook in 5 packages
            let chunks =
                audioBooks
                |> Array.filter (_.AudioBook.Picture.IsNone)
                |> Array.chunkBySize parallelDownloads


            let runChunk (chunk:AudioBookItemViewModel array) =
                task {
                    let! audioBooks =
                        chunk
                        |> Array.map (fun i ->
                            task {
                                try
                                    match i.AudioBook.Picture with
                                    | Some _ ->
                                        return ()
                                    | None ->
                                        let! url = Services.WebAccess.getPictureOnlineUrl i.AudioBook
                                        match url with
                                        | None -> return ()
                                        | Some url ->
                                            i.SetPicture url
                                            return ()
                                with
                                | ex ->
                                    // log
                                    Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                                    Global.telemetryClient.TrackException ex
                                    return ()
                            }
                        )
                        |> Task.WhenAll

                    return audioBooks
                }
                
                

            #if DEBUG
            System.Diagnostics.Trace.WriteLine $"Processing {chunks.Length}..."
            #endif
            for idx, chunk in chunks |> Array.indexed do
                try 
                    let! _ = runChunk chunk
                    notificationCallback $"Bild {(idx+1) * parallelDownloads} von {chunks.Length * parallelDownloads} runtergeladen!"
                    #if DEBUG
                    System.Diagnostics.Trace.WriteLine $"Chunk {idx+1} of {chunks.Length} done!"
                    #endif
                    ()
                with
                | ex ->
                    // log
                    Microsoft.AppCenter.Crashes.Crashes.TrackError(ex, Map.empty)
                    Global.telemetryClient.TrackException ex

            return ()
            
        }
    
    
    member this.StartDownloadPictures items =
            task {
                notificationCallback "Starte Download der Bilder..."
                do! addOnlineUrlForPictureForAllWhichHasNone items
                notificationCallback "Bilder heruntergeladen!"
            }
    
    

