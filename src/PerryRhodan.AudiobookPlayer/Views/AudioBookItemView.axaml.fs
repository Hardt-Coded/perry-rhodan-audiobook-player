namespace PerryRhodan.AudiobookPlayer.Views

open System
open System.Collections.Concurrent
open System.IO
open System.Security.Cryptography
open System.Text
open System.Threading.Tasks
open AsyncImageLoader
open AsyncImageLoader.Loaders
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Media.Imaging

         
             
module Helper =
    let generateMD5Hash (input: string) =
        use md5 = MD5.Create()
        let inputBytes = Encoding.UTF8.GetBytes(input)
        let hashBytes = md5.ComputeHash(inputBytes)
        BitConverter.ToString(hashBytes).Replace("-", "").ToLower()
    
type GlobalCachedImageLoader =
    inherit BaseWebImageLoader
    
    

        
    static let globalMemCache = new ConcurrentDictionary<string, Task<Bitmap>>()        
        
    new(httpClient, disposeHttpClient) = { inherit BaseWebImageLoader(httpClient, disposeHttpClient) }
    new() = { inherit BaseWebImageLoader() }
    
    member this.MyLoad(url) = base.LoadAsync(url)
    
    override this.ProvideImageAsync(url) =
        let loadAsync = this.MyLoad
        task {
            // generate md5 hash from url
            let tempPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            let filename = $"{tempPath.Trim('\\')}\\{Helper.generateMD5Hash(url)}.jpg"
            if File.Exists(filename) then
                return new Bitmap(filename)
            else
                let! bitmap = loadAsync(url)
                bitmap.Save(filename, 100)
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
        image.Loader |> Option.ofObj |> Option.iter (_.Dispose())
        image.Loader <- globalCachedImageLoader
        
