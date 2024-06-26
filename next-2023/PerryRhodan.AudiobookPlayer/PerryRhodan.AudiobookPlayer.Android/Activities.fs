namespace PerryRhodan.AudiobookPlayer.Android

open Android.App
open Android.Content.PM
open Avalonia
open Avalonia.ReactiveUI
open Avalonia.Android
open PerryRhodan.AudiobookPlayer

[<Activity(
    Label = "PerryRhodan.AudiobookPlayer.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = (ConfigChanges.Orientation ||| ConfigChanges.ScreenSize ||| ConfigChanges.UiMode))>]
type MainActivity() =
    inherit AvaloniaMainActivity<App>()
    
    

    override _.CustomizeAppBuilder(builder) =
        // register services
        
        
        
        base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI()
            
           
