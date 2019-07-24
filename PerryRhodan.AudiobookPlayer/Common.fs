module Common

    open System
    open Xamarin.Forms
    open System.IO
    open System.Threading.Tasks
    open Fabulous
    open Fabulous.XamarinForms
    

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



    let createBusyLayer () =
        View.Grid(
            backgroundColor = Color.FromHex "#A0000000",
            children = [
                View.ActivityIndicator(
                    isRunning = true,
                    color = Color.White,
                    scale = 0.1
                )
            ]
        )

    let asyncFunc f = 
        async {  
            let original = System.Threading.SynchronizationContext.Current
            do! Async.SwitchToNewThread() 
            let result = f() 
            do! Async.SwitchToContext(original)
            return result 
        }

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
                memoizations.[key] <- cts
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


    module Helpers =
        open Plugin.Permissions
        open Plugin.Permissions.Abstractions
        open Microsoft.AppCenter.Analytics
        open Microsoft.AppCenter.Crashes
        open Fabulous
        
        let displayAlert(title, message, cancel) =
            let tsc = TaskCompletionSource()
            
            Device.BeginInvokeOnMainThread(
                (fun () -> 
                    async {
                        try
                            do! Application.Current.MainPage.DisplayAlert(title, message, cancel) |> Async.AwaitTask
                            tsc.SetResult()
                        with
                        | _ as ex ->
                            Crashes.TrackError(ex)
                            tsc.SetResult()
                    } |> Async.StartImmediate
                )
            )

            tsc.Task |> Async.AwaitTask
            


        let displayAlertWithConfirm(title, message, accept, cancel) =
            let tsc = TaskCompletionSource<bool>()
            
            Device.BeginInvokeOnMainThread(
                (fun () -> 
                    async {
                        try
                            let! res = Application.Current.MainPage.DisplayAlert(title, message, accept, cancel) |> Async.AwaitTask
                            tsc.SetResult(true)
                        with
                        | _ as ex ->
                            Crashes.TrackError(ex)
                            tsc.SetResult(false)
                    } |> Async.StartImmediate
                )
            )

            tsc.Task |> Async.AwaitTask
            


        let askPermissionAsync permission = async {
            try
                let! status = CrossPermissions.Current.CheckPermissionStatusAsync(permission) |> Async.AwaitTask
                if status = PermissionStatus.Granted then
                    return true
                else
                    let! status = CrossPermissions.Current.RequestPermissionsAsync([| permission |]) |> Async.AwaitTask
                    return status.[permission] = PermissionStatus.Granted
                           || status.[permission] = PermissionStatus.Unknown
            with exn ->
                return false
        }

        // diplay action sheet an returns a command fitting
        let displayActionSheet title cancel buttons =
            async {
                let title =
                    match title with
                    | None -> null
                    | Some label -> label

                let cancel =
                    match cancel with
                    | None -> null
                    | Some label -> label
                let buttonTexts = buttons |> Array.map (fun (txt,_)-> txt)
                let! tapedButton = Application.Current.MainPage.DisplayActionSheet(title,cancel,null,buttonTexts) |> Async.AwaitTask
                let tapedButtonCmd = buttons |> Array.tryFind (fun (txt,_) -> txt = tapedButton)
                match tapedButtonCmd with
                | None -> return None
                | Some (txt,_) when (txt = "cancel") -> return None
                | Some (_, msg) -> return Some msg
            }
            

    module Cmd =
        open Fabulous

        let ofAsyncMsgWithInternalDispatch (p: (Dispatch<'msg> -> Async<'msg>)) : Cmd<'msg> =
            [ fun dispatch -> async { let! msg = (p dispatch)  in dispatch msg } |> Async.StartImmediate ] 

        let ofAsyncMsgOptionWithInternalDispatch (p: (Dispatch<'msg> -> Async<'msg option>)) : Cmd<'msg> =
            [ fun dispatch -> async { let! msg = (p dispatch)  in match msg with None -> () | Some msg -> dispatch msg } |> Async.StartImmediate ]
        
        
        let ofMultipleAsyncMsgWithInternalDispatch (p: (Dispatch<'msg> -> Async<#seq<'msg>>)) : Cmd<'msg> =
            [ fun dispatch -> async { let! msgs = (p dispatch) in msgs |> Seq.iter (fun msg -> dispatch msg) } |> Async.StartImmediate ] 
        
        let ofAsyncMsgOption (p: Async<'msg option>) : Cmd<'msg> =
            [ fun dispatch -> async { let! msg = p in match msg with None -> () | Some msg -> dispatch msg } |> Async.StartImmediate ]

        let ofMultipleAsyncMsgs (p: Async<#seq<'msg>>) : Cmd<'msg> =
            [ fun dispatch -> async { let! msgs = p in msgs |> Seq.iter (fun msg -> dispatch msg) } |> Async.StartImmediate ]

        let ofMultipleAsyncMsgOptions (p: Async<#seq<'msg option>>) : Cmd<'msg> =
            [ fun dispatch -> async { let! msgs = p in msgs |> Seq.iter (fun msg -> match msg with None -> () | Some msg -> dispatch msg) } |> Async.StartImmediate ]

        
        /// Message with specifiy item as fst of tuple
        let map2 (item:'item) (f: 'item * 'a -> 'msg) (cmd: Fabulous.Cmd<'a>) : Fabulous.Cmd<'msg> =
            cmd 
            |> List.map (
                fun g ->
                    (fun dispatch -> 
                        let msg mainMsg = f (item,mainMsg)    
                        msg >> dispatch 
                        )  >> g                       
            )


    module Consts =        

        let backgroundColor = Color.FromHex("#303030")

        let cardColor = Color.FromHex("#424242")

        let appBarColor = Color.FromHex("#212121")

        let statusBarColor = Color.FromHex("#000000")


        let primaryTextColor = Color.FromHex("#FFFFFFFF")

        let secondaryTextColor = Color.FromHex("#B3FFFFFF")

        let disabledTextColor = Color.FromHex("#61FFFFFF")

    
    module HttpHelpers =
        
        let getFileSizeFromHttpHeadersOrDefaultValue defaultValue (headers:Map<string,string>) =
            // Get FileSize from Download
            let (hasLength,contentLengthHeader) = 
                headers.TryGetValue("Content-Length")
            let (fileSizeFound,contentLength) = 
                if (hasLength) then
                    Int32.TryParse(contentLengthHeader)
                else
                    (false,0)
            if (fileSizeFound) then contentLength else defaultValue


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
                Some res.Groups.[group].Value
            else
                None

        let regexMatchGroupOpt pattern group input =
            input
            |> Option.bind (regexMatchGroup group pattern)
            


    module AppCenter =

        open Microsoft.AppCenter
        open Microsoft.AppCenter.Analytics
        open Microsoft.AppCenter.Crashes

        type AppCenterUpdateTracer<'msg, 'model> =
            'msg -> 'model -> (string * (string * string) list) option

        /// Trace all the updates to AppCenter
        let withAppCenterTrace (program: Program<_, _, _>) =
            let traceError (message, exn) =
                Crashes.TrackError(exn, dict [ ("Message", message) ])

            { program with                 
                onError = traceError }


    module ZipHelpers =

        open ICSharpCode.SharpZipLib.Zip

        let (|Mp3File|PicFile|Other|) (z:ZipEntry) = 
            if (z.Name.Contains(".mp3")) then Mp3File
            elif (z.Name.Contains(".jpg")) then PicFile
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

        let PostWithDelay msg ms (mailbox:MailboxProcessor<_>) =
            let post () = 
                async {
                    do! Async.Sleep ms
                    mailbox.Post(msg)
                }
            post() |> Async.StartImmediate

       