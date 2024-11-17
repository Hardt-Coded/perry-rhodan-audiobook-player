namespace PerryRhodan.AudiobookPlayer.Views

open System.Diagnostics
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Services.Helpers

type MainView () as this =
    inherit UserControl ()


    do
        this.Initialized.Add(fun e ->
            TopLevel.GetTopLevel(this)
            |> Option.ofObj
            |> Option.bind (fun x -> x.InputPane |> Option.ofObj)
            |> Option.iter (fun inputPane -> InputPaneService.InputPane <- inputPane)
        )

        this.InitializeComponent()


    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)




