﻿namespace PerryRhodan.AudiobookPlayer.Views

open Avalonia.Controls
open Avalonia.Markup.Xaml

type FeedbackView () as this = 
    inherit UserControl ()

    do this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)