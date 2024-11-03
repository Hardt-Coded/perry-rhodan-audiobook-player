namespace PerryRhodan.AudiobookPlayer.Desktop
open System
open System.Net.Http
open Avalonia
open Avalonia.ReactiveUI
open Dependencies
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open Services


type ScreenService() =
    interface IScreenService with
        member this.GetScreenSize() = 
            {| Width = 500; Height = 600; ScaledDensity = 1.0 |}




type HttpMessageHandlerService() =
    let myHttpMessageHandler = new HttpClientHandler()
    interface IAndroidHttpMessageHandlerService with
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
            .AddSingleton<IScreenService, ScreenService>()
            .AddSingleton<IAndroidHttpMessageHandlerService, HttpMessageHandlerService>()
            .AddSingleton<INavigationService, DependencyServices.NavigationService>()
            |> ignore
            
        
        
        buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
