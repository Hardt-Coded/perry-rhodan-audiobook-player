module Migrations

open System
open System.IO
open FSharp.Control
open Microsoft.AppCenter.Crashes
open Acr.UserDialogs
open System.Net.Http

    
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
            

