namespace PerryRhodan.AudiobookPlayer.Desktop
open System
open System.Net.Http
open Avalonia
open Avalonia.ReactiveUI
open Dependencies
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer
open Services


type ScreenService() =
    interface DependencyServices.IScreenService with
        member this.GetScreenSize() = 
            {| Width = 500; Height = 600; ScaledDensity = 1.0 |}




type HttpMessageHandlerService() =
    let myHttpMessageHandler = new HttpClientHandler()
    interface DependencyServices.IAndroidHttpMessageHandlerService with
        member this.GetHttpMesageHandler () =
            myHttpMessageHandler

        member this.GetCookieContainer () =
            myHttpMessageHandler.CookieContainer

        member this.SetAutoRedirect redirect =
            myHttpMessageHandler.AllowAutoRedirect <- redirect


        



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
            .AddSingleton<DependencyServices.IAndroidHttpMessageHandlerService, HttpMessageHandlerService>()
            .AddSingleton<DependencyServices.INavigationService, DependencyServices.NavigationService>()
            |> ignore
            
        
        
        buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
