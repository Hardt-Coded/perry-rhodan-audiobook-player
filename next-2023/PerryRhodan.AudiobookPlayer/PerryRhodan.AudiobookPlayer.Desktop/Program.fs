namespace PerryRhodan.AudiobookPlayer.Desktop
open System
open Avalonia
open Avalonia.ReactiveUI
open Dependencies
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer
open Services


type ScreenService() =
    interface DependencyServices.IScreenService with
        member this.GetScreenSize() = 
            {| Width = 400; Height = 600 |}



module Program =

    [<CompiledName "BuildAvaloniaApp">] 
    let buildAvaloniaApp () = 
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(areas = Array.empty)
            .UseReactiveUI()

    [<EntryPoint; STAThread>]
    let main argv =
        
        DependencyService.ServiceCollection
            .AddSingleton<DependencyServices.IScreenService, ScreenService>()
            |> ignore
        
        buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
