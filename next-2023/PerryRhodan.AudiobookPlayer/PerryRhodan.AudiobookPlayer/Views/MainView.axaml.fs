namespace PerryRhodan.AudiobookPlayer.Views

open Avalonia.Controls
open Avalonia.Markup.Xaml
open PerryRhodan.AudiobookPlayer

type MainView () as this = 
    inherit UserControl ()

    do this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
        

