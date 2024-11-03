namespace PerryRhodan.AudiobookPlayer.Android.Services

open PerryRhodan.AudiobookPlayer.Services.Interfaces
open Services

type ScreenService() =
    interface IScreenService with
        member this.GetScreenSize() = 
            let metrics = Android.App.Application.Context.Resources.DisplayMetrics
            {| Width = metrics.WidthPixels; Height =metrics.HeightPixels; ScaledDensity = metrics.ScaledDensity |> float |}


