open System.Runtime.Versioning
open Avalonia
open Avalonia.Browser
open Avalonia.ReactiveUI

open PerryRhodan.AudiobookPlayer

module Program =
    [<assembly: SupportedOSPlatform("browser")>]
    do ()

    [<CompiledName "BuildAvaloniaApp">] 
    let buildAvaloniaApp () = 
        AppBuilder
            .Configure<App>()

    [<EntryPoint>]
    let main argv =
        task {
            do!
                   buildAvaloniaApp()
                       .WithInterFont()
                       .UseReactiveUI()
                        .StartBrowserAppAsync("out")
        }
        |> ignore
        0