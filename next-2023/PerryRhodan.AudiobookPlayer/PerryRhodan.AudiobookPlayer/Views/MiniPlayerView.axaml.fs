namespace PerryRhodan.AudiobookPlayer.Views

open System
open Avalonia.Animation
open Avalonia.Controls
open Avalonia.Data.Converters
open Avalonia.Markup.Xaml

[<AutoOpen>]
module AvaloniaExtensions =
    type TextBlock with
        member this.NegativeBoundWidth = this.Bounds.Width * -1.0
        
// value converter to get make a value negative
type NegativeConverter() =
    interface IValueConverter with
        member this.Convert(value, targetType, parameter, culture) =
            match value with
            | :? float as d -> d * -1.0 |> box
            | _ -> 0.0
        member this.ConvertBack(value, targetType, parameter, culture) =
            match value with
            | :? float as d -> d * -1.0 |> box
            | _ -> 0.0        

type MiniPlayerView () as this = 
    inherit UserControl ()

    do this.InitializeComponent()

    member this.TitleNegativeWidth = this.FindControl<TextBlock>("TextBlock").NegativeBoundWidth
    
    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
    
    
        
   