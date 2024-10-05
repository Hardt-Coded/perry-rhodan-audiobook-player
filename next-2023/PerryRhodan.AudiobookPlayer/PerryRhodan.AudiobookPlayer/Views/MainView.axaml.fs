namespace PerryRhodan.AudiobookPlayer.Views

open Avalonia.Controls
open Avalonia.Markup.Xaml
open Services.Helpers

type MainView () as this = 
    inherit UserControl ()

    
    do
        this.Initialized.Add(fun e ->
            let topLevel = TopLevel.GetTopLevel(this)
            InputPaneService.InputPane <- topLevel.InputPane
            ()
            )    
        this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
        

