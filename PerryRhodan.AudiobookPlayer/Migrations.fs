module Migrations

open System
open System.IO
open FSharp.Control
open Microsoft.AppCenter.Crashes
open Acr.UserDialogs
open System.Net.Http
open Xamarin.Forms

    
    module Helpers =
    
        open Services
            
        let isMigrationConfirmed id =
            async {
                let! value = (SecureStorageHelper.getSecuredValue $"migration_{id}")
                return value |> Option.map (fun i -> i = "true") |> Option.defaultValue false
            }
    
        let confirmMigration id =
            async {
                do! SecureStorageHelper.setSecuredValue "true" $"migration_{id}"
            }



    module NoMediaMigration =

        let private createNoMediaFile audioBookFolder =
            async {
                let noMediaFile = Path.Combine(audioBookFolder,".nomedia")
                if File.Exists(noMediaFile) |> not then
                    do! File.WriteAllTextAsync(noMediaFile,"") |> Async.AwaitTask
            }

        let runNoMediaMigration () =
            async {
                use dlg = UserDialogs.Instance.Progress("Migration No Media", maskType = MaskType.Gradient)
                    
                dlg.PercentComplete <- 0

                do! createNoMediaFile Services.Consts.audioBookDownloadFolderBase
                dlg.PercentComplete <- 100

                do! Helpers.confirmMigration "NoMediaMigration_"
            }


    module AudiobooksMissingAfterUpdateAndroid10 =

        open LiteDB
        open LiteDB.FSharp
        open Domain

        let mapper = FSharpBsonMapper()

        let cleanupAudiobookDb (audiobookDbFile:string) =
            async {
                use db = new LiteDatabase(audiobookDbFile, mapper)
                let audioBooks = 
                    db.GetCollection<AudioBook>("audiobooks")
                        .FindAll()                             
                        |> Seq.toArray
                        |> Array.sortBy (fun i -> i.FullName)
                        |> Array.Parallel.map (
                            fun i ->
                                if obj.ReferenceEquals(i.State.LastTimeListend,null) then
                                    let newMdl = {i.State with LastTimeListend = None }
                                    { i with State = newMdl }
                                else
                                    i
                        )

                // remove all pathes and pictures
                let migratedBooks =
                    audioBooks
                    |> Array.Parallel.map (fun x ->
                        {
                            x with
                                Picture = None
                                Thumbnail = None
                                State = {
                                    x.State with
                                        Downloaded = false
                                        DownloadedFolder = None
                                }
                        }
                    
                    )

                let result = db.GetCollection<AudioBook>("audiobooks").Update(migratedBooks)

                db.Dispose |> ignore
                
                File.Copy(audiobookDbFile, Services.Consts.audioBooksStateDataFile, true)
                ()
            }
            

        let cleanupAudioBookFilesDb (audiobookFilesDbFile:string) =
            async {
                use db = new LiteDatabase(audiobookFilesDbFile, mapper)

                let infos = 
                    db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos")
                        .FindAll()                             
                        |> Seq.toArray


                let removeOldStoragePath (file:string) =
                    let idx = file.IndexOf("PerryRhodan.AudioBookPlayer/data")
                    file.[idx..].Replace("PerryRhodan.AudioBookPlayer/data","")

                let migratedInfos =
                    infos
                    |> Array.Parallel.map (fun x ->
                        { x with 
                            AudioFiles = 
                                x.AudioFiles
                                |> List.map (fun e ->
                                    { 
                                        e with
                                            FileName = Path.Combine(Services.Consts.baseUrl, removeOldStoragePath e.FileName)
                                    }
                                )
                        }
                    )
                ()

                let result = db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos").Update(migratedInfos)
                
                db.Dispose |> ignore

                File.Copy(audiobookFilesDbFile, Services.Consts.audioBookAudioFileDb, true)
            }

        let importDatabases files =
            async {
                try 
                    let dbFiles =
                        files
                        |> Seq.filter (fun (x:string) -> x.Contains("audiobooks.db") || x.Contains("audiobookfiles.db"))
                        |> Seq.toList

                    if dbFiles |> List.length < 2 then
                        do! Common.Helpers.displayAlert("Fehler", "Sie müssen unbedingt beide Datenbank-Dateien anwählen. Das können Sie durch lange drücken auf eine Datei!", "OK")
                    else
                        let audiobookDbFile = 
                            dbFiles
                            |> Seq.filter (fun (x:string) -> x.Contains("audiobooks.db"))
                            |> Seq.head

                        let audiobookFilesDbFile =
                            dbFiles
                            |> Seq.filter (fun (x:string) -> x.Contains("audiobookfiles.db"))
                            |> Seq.head


                        use dlg = UserDialogs.Instance.Progress("Migration No Media", maskType = MaskType.Gradient)
                    
                        dlg.PercentComplete <- 0
                        
                        do! Async.Sleep 300

                        do! cleanupAudiobookDb audiobookDbFile

                        dlg.PercentComplete <- 50

                        do! Async.Sleep 300

                        do! cleanupAudioBookFilesDb audiobookFilesDbFile

                        dlg.PercentComplete <- 100

                        do! Async.Sleep 300

                        do! Common.Helpers.displayAlert ("Neustart","Die App muss beendet werden, damit der Import wirksam wird.", "OK")

                        DependencyService.Get<Services.DependencyServices.ICloseApplication>().CloseApplication()

                    with
                    | ex ->
                        do! Common.Helpers.displayAlert ("Fehler beim Import",ex.Message, "OK")

                    
            }
            
                



let private currentMigrations = [
    "NoMediaMigration_"
]


let runMigration migration =
    async {
        match migration with
        | "NoMediaMigration_" ->
            do! NoMediaMigration.runNoMediaMigration ()

        | _ ->
            Crashes.TrackError (exn ($"Error Migration '{migration}' not found!"), Map.empty)
    }
    
        

let runMigrations () =
    async {
        do!
            asyncSeq {
                for migration in currentMigrations do
                    let! result = Helpers.isMigrationConfirmed migration
                    yield (migration,result)
            }
            |> AsyncSeq.filter (fun (_,result) -> not result)
            |> AsyncSeq.map fst
            |> AsyncSeq.iterAsync runMigration

    }
            

