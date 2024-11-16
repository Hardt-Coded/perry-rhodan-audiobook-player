namespace PerryRhodan.AudiobookPlayer.Views

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open AsyncImageLoader
open AsyncImageLoader.Loaders
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Media.Imaging

         
             

type GlobalCachedImageLoader =
    inherit BaseWebImageLoader
        
    static let globalMemCache = new ConcurrentDictionary<string, Task<Bitmap>>()        
        
    new(httpClient, disposeHttpClient) = { inherit BaseWebImageLoader(httpClient, disposeHttpClient) }
    new() = { inherit BaseWebImageLoader() }
    
    member this.MyLoad(url) = base.LoadAsync(url)
    
    override this.ProvideImageAsync(url) =
        let loadAsync = this.MyLoad
        task {
            let! bitmap = globalMemCache.GetOrAdd(url, loadAsync)
            if bitmap = null then globalMemCache.TryRemove(url) |> ignore
            return bitmap
        }



type AudioBookItemView () as this = 
    inherit UserControl ()

    static let globalCachedImageLoader = new GlobalCachedImageLoader()
    
    do this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
        // get Grid
        let image = this.FindControl<AdvancedImage>("Thumbnail")
        // set ImageLoader
        image.Loader <- globalCachedImageLoader
        
