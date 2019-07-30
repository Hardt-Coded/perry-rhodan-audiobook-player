namespace PerryRhodan.AudiobookPlayer.Android

open CustomRender


[<assembly: Xamarin.Forms.ExportRenderer (typeof<Xamarin.Forms.Shell>, typeof<CustomShellRenderer>)>] do()

[<assembly: Xamarin.Forms.ExportRenderer (typeof<Xamarin.Forms.WebView>, typeof<WorkaroundNotScrollableViewRenderer>)>] do()


do ()



