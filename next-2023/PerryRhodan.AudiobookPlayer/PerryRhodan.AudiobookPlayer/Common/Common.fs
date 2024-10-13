module Common

    open System
    open System.Net.Http.Headers
    open Avalonia.Controls
    open Avalonia.Media
    

    type ComError =
    | SessionExpired of string
    | Other of string
    | Exception of exn
    | Network of string

    let (|InvariantEqual|_|) (str:string) arg = 
        if String.Compare(str, arg, StringComparison.InvariantCultureIgnoreCase) = 0
        then Some() else None


    let (|OrdinalEqual|_|) (str:string) arg = 
        if String.Compare(str, arg, StringComparison.OrdinalIgnoreCase) = 0
        then Some() else None


    let (|InvariantContains|_|) (str:string) (arg:string) = 
        if arg.IndexOf(str, StringComparison.InvariantCultureIgnoreCase) > -1
        then Some() else None


    let (|OrdinalContains|_|) (str:string) (arg:string) = 
        if arg.IndexOf(str, StringComparison.OrdinalIgnoreCase) > -1
        then Some() else None    



 


    module Extensions =
        open System.Collections.Generic
        open System.Threading
        
        let debounce<'T> =
            let memoizations = Dictionary<obj, CancellationTokenSource>(HashIdentity.Structural)
            fun (timeout: int) (fn: 'T -> unit) value ->
                let key = fn.GetType()
                 // Cancel previous debouncer
                match memoizations.TryGetValue(key) with
                | true, cts -> cts.Cancel()
                | _ -> ()
                 // Create a new cancellation token and memoize it
                let cts = new CancellationTokenSource()
                memoizations[key] <- cts
                 // Start a new debouncer
                (async {
                    try
                        // Wait timeout to see if another event will cancel this one
                        do! Async.Sleep timeout
                         // If still not cancelled, then proceed to invoke the callback and discard the unused token
                        memoizations.Remove(key) |> ignore
                        fn value
                    with
                    | _ -> ()
                })
                |> (fun task -> Async.StartImmediate(task, cts.Token)) 


        
        
        
    
    //module Helpers =

        
   


    module Consts =        

        let backgroundColor = Color.Parse("#303030")

        let cardColor = Color.Parse("#424242")

        let appBarColor = Color.Parse("#212121")

        let statusBarColor = Color.Parse("#000000")


        let primaryTextColor = Color.Parse("#FFFFFFFF")

        let secondaryTextColor = Color.Parse("#B3FFFFFF")

        let disabledTextColor = Color.Parse("#61FFFFFF")

    
    module HttpHelpers =
        
        let getFileSizeFromHttpHeadersOrDefaultValue defaultValue (headers:HttpContentHeaders) : int =
            // Get FileSize from Download
            headers.ContentLength 
            |> Option.ofNullable 
            |> Option.defaultValue defaultValue 
            |> int32
            
            


    module StringHelpers =

        let optToInt defaultValue (optStr:string option) =
            optStr
            |> Option.map (fun v ->
                let isInt,value = Int32.TryParse(v)
                match isInt with
                | true ->
                    value
                | false ->
                    defaultValue
            )
            |> Option.defaultValue defaultValue


    module RegExHelper =

        open System.Text.RegularExpressions

        let regexMatch pattern input =
            let res = Regex.Match(input,pattern)
            if res.Success then
                Some res.Value
            else
                None

        let regexMatchOpt pattern input =
            input
            |> Option.bind (regexMatch pattern)

        let regexMatchGroup pattern group input =
            let res = Regex.Match(input,pattern)
            if res.Success && res.Groups.Count >= group then
                Some res.Groups[group].Value
            else
                None

        let regexMatchGroupOpt pattern group input =
            input
            |> Option.bind (regexMatchGroup group pattern)
            




    module ZipHelpers =

        open ICSharpCode.SharpZipLib.Zip

        let (|Mp3File|PicFile|Other|) (z:ZipEntry) = 
            if z.Name.Contains(".mp3") then Mp3File
            elif z.Name.Contains(".jpg") then PicFile
            else Other



    module TimeSpanHelpers =

        let toTimeSpan (ms:int) =
            TimeSpan.FromMilliseconds(ms |> float)

        let fromTimeSpan (ts:TimeSpan) =
            ts.TotalMilliseconds |> int
        
        let fromTimeSpanOpt (ts:TimeSpan option) =
            ts
            |> Option.defaultValue TimeSpan.Zero
            |> fromTimeSpan


    module MailboxExtensions =

        let PostWithDelay msg (ms:int) (mailbox:MailboxProcessor<_>) =
            let post () = 
                async {
                    do! Async.Sleep ms
                    mailbox.Post(msg)
                }
            post() |> Async.StartImmediate


    module PatternMatchHelpers =

        let (|StringContains|_|) (str:string) (input:string) =
            if input.Contains(str) then Some () else None



   


  

    module EventHelper =
        
        

        type CountedEvent<'a>() = 
            let evt = new Event<'a>()
            let mutable counter = 0
            let published = { 
                new IEvent<'a> with
                    member x.AddHandler(h) = 
                        evt.Publish.AddHandler(h)
                        counter <- counter + 1; 
                    member x.RemoveHandler(h) = 
                        evt.Publish.RemoveHandler(h)
                        counter <- counter - 1; 
                    member x.Subscribe(s) = 
                        let h = Handler<_>(fun _ -> s.OnNext)
                        x.AddHandler(h)
                        { new System.IDisposable with 
                            member y.Dispose() = x.RemoveHandler(h) } }
            member x.Trigger(v) = evt.Trigger(v)
            member x.Publish = published
            member x.HasListeners = counter > 0



    module String =
        
        let concatStr (str:string list) = System.String.Join ("", str)
        
        

       