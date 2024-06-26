module Migrations

    open System
    open System.IO
    open FSharp.Control
    open Microsoft.AppCenter.Crashes
    open Acr.UserDialogs
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



    module AudiobooksMissingAfterUpdateAndroid10 =

        open LiteDB
        open LiteDB.FSharp
        open Domain

        let mapper = FSharpBsonMapper()


        let private removeOldStoragePath (file:string) =
            let idx = file.IndexOf("PerryRhodan.AudioBookPlayer/data")
            file.[idx..].Replace("PerryRhodan.AudioBookPlayer/data","")


        let private cleanupAudiobookDb (audiobookDbFile:string) =
            let folders = Services.Consts.createCurrentFolders ()
            async {
                use db = new LiteDatabase(audiobookDbFile |> Common.DatabaseHelper.toLiteDbConnectionString, mapper)
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
                                        CurrentPosition =
                                            x.State.CurrentPosition
                                            |> Option.map (fun pos ->
                                                let cleanedFile = removeOldStoragePath pos.Filename
                                                let newFile = $"{folders.currentLocalDataFolder}{cleanedFile}"

                                                #if DEBUG
                                                System.Diagnostics.Debug.WriteLine($"old: {pos.Filename}")
                                                System.Diagnostics.Debug.WriteLine($"cleanedFile: {cleanedFile}")
                                                System.Diagnostics.Debug.WriteLine($"newFile: {newFile}")
                                                #endif
                                                { pos with
                                                    Filename = newFile
                                                }
                                            )
                                }
                        }

                    )

                let result = db.GetCollection<AudioBook>("audiobooks").Update(migratedBooks)

                db.Dispose |> ignore

                File.Copy(audiobookDbFile, folders.audioBooksStateDataFile, true)
                ()
            }


        let private cleanupAudioBookFilesInfoDb (audiobookFilesDbFile:string) =
            async {
                use db = new LiteDatabase(audiobookFilesDbFile |> Common.DatabaseHelper.toLiteDbConnectionString, mapper)

                let infos =
                    db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos")
                        .FindAll()
                        |> Seq.toArray


                let folders = Services.Consts.createCurrentFolders ()

                let migratedInfos =
                    infos
                    |> Array.Parallel.map (fun x ->
                        { x with
                            AudioFiles =
                                x.AudioFiles
                                |> List.map (fun e ->
                                    let newFileName = $"{folders.currentLocalDataFolder}{removeOldStoragePath e.FileName}"
                                    {
                                        e with
                                            FileName = newFileName
                                    }
                                )
                        }
                    )
                ()

                let result = db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos").Update(migratedInfos)

                db.Dispose |> ignore
                let folders = Services.Consts.createCurrentFolders ()
                File.Copy(audiobookFilesDbFile, folders.audioBookAudioFileInfoDb, true)
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


                        use dlg = UserDialogs.Instance.Progress("Importiere Datenbank", maskType = MaskType.Gradient)

                        dlg.PercentComplete <- 0

                        do! Async.Sleep 300

                        do! cleanupAudiobookDb audiobookDbFile

                        dlg.PercentComplete <- 50

                        do! Async.Sleep 300

                        do! cleanupAudioBookFilesInfoDb audiobookFilesDbFile

                        dlg.PercentComplete <- 100

                        do! Async.Sleep 300

                        do! Common.Helpers.displayAlert ("Neustart","Die App muss beendet werden, damit der Import wirksam wird.", "OK")

                        DependencyService.Get<Services.DependencyServices.ICloseApplication>().CloseApplication()

                    with
                    | ex ->
                        do! Common.Helpers.displayAlert ("Fehler beim Import",ex.Message, "OK")


            }


    let private currentMigrations = []


    let runMigration migration =
        async {
            match migration with
            | _ ->
                Crashes.TrackError (exn ($"Error Migration '{migration}' not found!"), Map.empty)
        }



    let runMigrations () =
        async {

            for migration in currentMigrations do
                let! result = Helpers.isMigrationConfirmed migration
                if result then
                    do! runMigration migration
        }


