namespace PerryRhodan.AudiobookPlayer

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml
open Dependencies
open Microsoft.Extensions.DependencyInjection
open PerryRhodan.AudiobookPlayer.Controls
open PerryRhodan.AudiobookPlayer.Services
open PerryRhodan.AudiobookPlayer.ViewModels
open PerryRhodan.AudiobookPlayer.Views



type App() =
    inherit Application()


    override this.Initialize() =
        AvaloniaXamlLoader.Load(this)
        ()

    override this.OnFrameworkInitializationCompleted() =

        let mainViewModel = DependencyService.Get<MainViewModel>()
        let mainView = MainView()
        
        mainView.DataContext <- mainViewModel
        

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
