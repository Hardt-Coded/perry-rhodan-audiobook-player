namespace PerryRhodan.AudiobookPlayer

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml
open PerryRhodan.AudiobookPlayer.ViewModels
open PerryRhodan.AudiobookPlayer.Views

type App() =
    inherit Application()
    
    let appRoot = AppCompositionRoot()

    override this.Initialize() =
            AvaloniaXamlLoader.Load(this)
       
    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.MainWindow <- 
                MainWindow(Content = appRoot.GetView<ViewModels.MainViewModel>())

        | :? ISingleViewApplicationLifetime as singleViewLifetime ->
            try
                singleViewLifetime.MainView <- appRoot.GetView<ViewModels.MainViewModel>()
            with x ->
                printfn $"Exception: {x.Message} \n {x.StackTrace}"
        | _ -> 
            // leave this here for design view re-renders
            let a = 1
            ()

        base.OnFrameworkInitializationCompleted()
