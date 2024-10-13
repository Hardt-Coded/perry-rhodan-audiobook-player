namespace PerryRhodan.AudiobookPlayer

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml
open Dependencies
open Microsoft.AppCenter
open Microsoft.AppCenter.Analytics
open Microsoft.AppCenter.Crashes
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer.Controls
open PerryRhodan.AudiobookPlayer.ViewModels
open PerryRhodan.AudiobookPlayer.Views



type App() =
    inherit Application()
    
    
    
    let appRoot = AppCompositionRoot()
    
    let mainViewModel = new MainViewModel(appRoot)
    let mainView = MainView()
    
    
    
    let mainAccessViewService =
        {
            new IMainViewAccessService with 
                member this.GetMainViewModel() = mainView.DataContext :?> IMainViewModel
        }
    
    do
        mainView.DataContext <- mainViewModel
        
        DependencyService.ServiceCollection
            .AddTransient<ILoginViewModel, LoginViewModel>()
            .AddSingleton<IActionMenuService, ActionMenuService>()
            .AddSingleton<IMainViewAccessService>(mainAccessViewService)
            .AddSingleton<IMainViewModel>(mainViewModel)
        |> ignore
        
        
        
    
    override this.Initialize() =
            AvaloniaXamlLoader.Load(this)
       
    override this.OnFrameworkInitializationCompleted() =
        DependencyService.SetComplete()
        
        
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.MainWindow <- 
                MainWindow(Content = mainView)

        | :? ISingleViewApplicationLifetime as singleViewLifetime ->
            try
                singleViewLifetime.MainView <- mainView
                
            with x ->
                printfn $"Exception: {x.Message} \n {x.StackTrace}"
        | _ -> 
            // leave this here for design view re-renders
            ()

        
        
        base.OnFrameworkInitializationCompleted()