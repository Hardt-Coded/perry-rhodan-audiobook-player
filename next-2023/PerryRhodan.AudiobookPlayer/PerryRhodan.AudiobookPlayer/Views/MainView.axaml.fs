namespace PerryRhodan.AudiobookPlayer.Views

open System.Diagnostics
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

    override this.OnPropertyChanged(change) =
        base.OnPropertyChanged(change)
        if change.Property.Name = "Bounds" then
            Trace.WriteLine($"Bounds changed to {this.Bounds.Width}x{this.Bounds.Height}")
            ()
        
    
