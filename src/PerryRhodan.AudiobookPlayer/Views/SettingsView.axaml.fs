﻿namespace PerryRhodan.AudiobookPlayer.Views

open Avalonia.Controls
open Avalonia.Markup.Xaml

type SettingsView () as this = 
    inherit UserControl ()

    do this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)