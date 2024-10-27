namespace PerryRhodan.AudiobookPlayer.Android.Services

open Services

type ScreenService() =
    interface DependencyServices.IScreenService with
        member this.GetScreenSize() = 
            let metrics = Android.App.Application.Context.Resources.DisplayMetrics
            {| Width = metrics.WidthPixels; Height =metrics.HeightPixels; ScaledDensity = metrics.ScaledDensity |> float |}


