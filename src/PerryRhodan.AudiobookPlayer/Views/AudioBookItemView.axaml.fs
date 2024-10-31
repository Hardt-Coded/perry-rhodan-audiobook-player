namespace PerryRhodan.AudiobookPlayer.Views

open Avalonia.Controls
open Avalonia.Markup.Xaml

type AudioBookItemView () as this = 
    inherit UserControl ()

    do this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
        // get Grid
        let grid = this.FindControl<Grid>("TextGrid")
        grid.Tapped.Subscribe (fun _ -> ()) |> ignore
