module Migrations

    open System
    open System.IO
    open FSharp.Control
    open Microsoft.AppCenter.Crashes
    open Acr.UserDialogs
    open Xamarin.Forms


   
    let [<Literal>] noMediaMigration = "NoMediaMigration_"
    let [<Literal>] internalStorageMigration = "InternalStorageMigration_"


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

                let folders = Services.Consts.createCurrentFolders ()

                do! createNoMediaFile folders.audioBookDownloadFolderBase
                dlg.PercentComplete <- 100

                do! Helpers.confirmMigration noMediaMigration
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
                use db = new LiteDatabase(audiobookFilesDbFile, mapper)

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
            
       
    // migrating to internal storage as long you have access to it (Android 9)
    module MigrateToInternalStorage =
        open System.Threading.Tasks
        open LiteDB
        open LiteDB.FSharp
        open Domain

        let mapper = FSharpBsonMapper()

        let removeOldStoragePath (file:string) =
            let idx = file.IndexOf("PerryRhodan.AudioBookPlayer/data")
            file.[idx..].Replace("PerryRhodan.AudioBookPlayer/data","")

        let private cleanupAudiobookDb (audiobookDbFile:string) newDataFolder =
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
                                        CurrentPosition = 
                                            x.State.CurrentPosition 
                                            |> Option.map (fun pos -> 
                                                let cleanedFile = removeOldStoragePath pos.Filename
                                                let newFile = $"{newDataFolder}{cleanedFile}"
                                                
                                                #if DEBUG
                                                System.Diagnostics.Debug.WriteLine($"newDataFolder: {newDataFolder}")
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
                ()
            }
            

        let private cleanupAudioBookFilesInfoDb (audiobookFilesDbFile:string) (newDataFolder:string)=
            async {
                use db = new LiteDatabase(audiobookFilesDbFile, mapper)

                let infos = 
                    db.GetCollection<AudioBookAudioFilesInfo>("audiobookfileinfos")
                        .FindAll()                             
                        |> Seq.toArray


                

                let migratedInfos =
                    infos
                    |> Array.Parallel.map (fun x ->
                        { x with 
                            AudioFiles = 
                                x.AudioFiles
                                |> List.map (fun e ->
                                    let newFileName = $"{newDataFolder}{removeOldStoragePath e.FileName}"
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
            }


        let rec directoryCopy srcPath dstPath =
            if not <| System.IO.Directory.Exists(srcPath) then
                let msg = System.String.Format("Source directory does not exist or could not be found: {0}", srcPath)
                raise (System.IO.DirectoryNotFoundException(msg))
        
            if not <| System.IO.Directory.Exists(dstPath) then
                System.IO.Directory.CreateDirectory(dstPath) |> ignore
        
            let srcDir = new System.IO.DirectoryInfo(srcPath)
        
            for file in srcDir.GetFiles() do
                let temppath = System.IO.Path.Combine(dstPath, file.Name)
                file.CopyTo(temppath, true) |> ignore
        
            for subdir in srcDir.GetDirectories() do
                let dstSubDir = System.IO.Path.Combine(dstPath, subdir.Name)
                directoryCopy subdir.FullName dstSubDir


        let moveFilesToInternalStorage () =
            async {
                let internalStorageBasePath =
                    Path.Combine(Xamarin.Essentials.FileSystem.AppDataDirectory, "PerryRhodan.AudioBookPlayer")

                
                if Services.Consts.isToInternalStorageMigrated() then
                    return ()
                else
                    let folders = Services.Consts.createCurrentFolders ()

                    if not (Directory.Exists(folders.currentLocalBaseFolder)) then
                        return ()
                    else

                        if folders.currentLocalBaseFolder.ToUpperInvariant() = internalStorageBasePath.ToUpperInvariant() then
                            return ()
                        else
                
                            use dlg = UserDialogs.Instance.Progress("Migration Move To Internal Storage", maskType = MaskType.Gradient)
                
                            dlg.PercentComplete <- 10

                            if (Directory.Exists(internalStorageBasePath)) then
                                Directory.Delete(internalStorageBasePath, true) |> ignore


                            // folder infos
                            let currentLocalDataFolder = Path.Combine(internalStorageBasePath,"data")
                            let stateFileFolder = Path.Combine(currentLocalDataFolder,"states")
                            let newAudiobookDbFile = Path.Combine(stateFileFolder,"audiobooks.db")
                            let newAudiobookFilesDbFile = Path.Combine(stateFileFolder,"audiobookfiles.db")

                            // move all files!
                            try
                                do! Task.Run(fun () -> directoryCopy folders.currentLocalBaseFolder internalStorageBasePath) |> Async.AwaitTask
                                do! Task.Run(fun () -> Directory.Delete (folders.currentLocalBaseFolder, true)) |> Async.AwaitTask
                            with
                            | ex ->
                                if (Directory.Exists(internalStorageBasePath)) then
                                    Directory.Delete (internalStorageBasePath, true)

                                // save the db files
                                if (Directory.Exists(folders.stateFileFolder)) then
                                    directoryCopy folders.stateFileFolder stateFileFolder
                    
                                // delete all
                                if (Directory.Exists(folders.currentLocalBaseFolder)) then
                                    Directory.Delete (folders.currentLocalBaseFolder, true)
                    
                                do! Common.Helpers.displayAlert ("Fehler","Fehler beim Verschieben von Dateien. App muss neugestartet werden!", "OK")

                                DependencyService.Get<Services.DependencyServices.ICloseApplication>().CloseApplication()
                                return ()
                    

                

                            dlg.PercentComplete <- 60

                            do! cleanupAudiobookDb newAudiobookDbFile currentLocalDataFolder

                            dlg.PercentComplete <- 70

                            do! cleanupAudioBookFilesInfoDb newAudiobookFilesDbFile currentLocalDataFolder

                            dlg.PercentComplete <- 80

                            // set migrated to internal flag
                            File.WriteAllText(Path.Combine(Xamarin.Essentials.FileSystem.AppDataDirectory,".migrated"), "")

                            dlg.PercentComplete <- 90
                
                            do! Helpers.confirmMigration internalStorageMigration

                            dlg.PercentComplete <- 100

                            do! Common.Helpers.displayAlert (
                                "Neustart",
                                "Ihre Hörbucher wurden in den Aufgrund Einschränkungen zukünftiger Android Versionen verschoben. Die App muss dazu beendet werden. Starten Sie diese neu.", "OK")
                
                            DependencyService.Get<Services.DependencyServices.ICloseApplication>().CloseApplication()

                            return ()
            }
            

            
        


    let private currentMigrations = [
        noMediaMigration
        internalStorageMigration
    ]


    let runMigration migration =
        async {
            match migration with
            | "NoMediaMigration_" ->
                do! NoMediaMigration.runNoMediaMigration ()

            | "InternalStorageMigration_" ->
                do! MigrateToInternalStorage.moveFilesToInternalStorage ()

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
            

