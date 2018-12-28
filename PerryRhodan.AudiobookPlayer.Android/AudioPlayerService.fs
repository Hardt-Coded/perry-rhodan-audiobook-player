namespace PerryRhodan.AudiobookPlayer.Android

module rec AudioPlayerService =

    open Android.App
    open Android.OS
    open Android.Media
    open Android.Media.Session
    open Android.Content
    
    open Android.Runtime
    open Android.Views
    open Android.Widget

    open Xamarin.Forms.Platform.Android
    open Android.Support.V4.Media.Session
    open Android.Support.V4.Media
    open Android.Support.V4.App
    
    open Services
    
    
    type NoisyHeadPhoneReceiver(audioPlayer:DependencyServices.IAudioPlayer) =
        inherit BroadcastReceiver()

        override this.OnReceive (context, intent) =
            if (intent.Action = AudioManager.ActionAudioBecomingNoisy) then
                if audioPlayer.OnNoisyHeadPhone.IsSome then
                    audioPlayer.OnNoisyHeadPhone.Value()
                

            
   
    type AudioPlayer() =
       
        let mutable lastPositionBeforeStop = None
    
        let mutable onCompletion = None

        let mutable onAfterPrepare = None

        let mutable onInfo = None

        let mutable onNoisyHeadPhone = None

        let mutable currentFile = None

        let mutable noisyHeadPhoneReceiver = None

        let audioManager = Application.Context.GetSystemService(Context.AudioService) :?> AudioManager
    
        let mediaPlayer = 
            let m = new MediaPlayer()
            m.SetWakeMode(Application.Context, WakeLockFlags.Partial);
        
            m.Completion.Add(
                fun _ -> 
                    match onCompletion with
                    | None -> ()
                    | Some cmd -> cmd()
            )

            m.Prepared.Add(
                fun _ ->                 
                    match onAfterPrepare with
                    | None -> ()
                    | Some cmd -> cmd()
                    m.Start()
                    lastPositionBeforeStop <- None
            )  

            m

        

       
       


        interface DependencyServices.IAudioPlayer with
        
            member this.LastPositionBeforeStop with get () = lastPositionBeforeStop

            member this.CurrentFile with get () = currentFile

            member this.OnCompletion 
                with get () = onCompletion
                and set p = onCompletion <- p

            member this.OnNoisyHeadPhone 
                with get () = onNoisyHeadPhone
                and set p = onNoisyHeadPhone <- p

            member this.OnInfo 
                with get () = onInfo
                and set p = onInfo <- p

            member this.PlayFile file position =
                async {
                    noisyHeadPhoneReceiver <- Some ( new NoisyHeadPhoneReceiver(this) )
                    let noiseHpIntentFilter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy)
                    Application.Context.RegisterReceiver(noisyHeadPhoneReceiver.Value, noiseHpIntentFilter) |> ignore

                    mediaPlayer.Reset()
                    do! mediaPlayer.SetDataSourceAsync(file) |> Async.AwaitTask
                    onAfterPrepare <- Some (fun () -> mediaPlayer.SeekTo(position))
                    mediaPlayer.PrepareAsync()
                    
                    
                    return ()
                }

        
            member this.Stop () =
                if (mediaPlayer.IsPlaying) then
                    mediaPlayer.Pause()
                    lastPositionBeforeStop <- Some mediaPlayer.CurrentPosition
                
                else
                    lastPositionBeforeStop <- Some mediaPlayer.CurrentPosition

                mediaPlayer.Stop()

                Application.Context.UnregisterReceiver(noisyHeadPhoneReceiver.Value)
                ()

            member this.GotToPosition ms =
                mediaPlayer.SeekTo(ms)

            member this.GetInfo () =
                async {
                    do! Common.asyncFunc(
                            fun () ->
                                match onInfo,mediaPlayer.IsPlaying with
                                | Some cmd, true -> cmd(mediaPlayer.CurrentPosition,mediaPlayer.Duration)
                                | _ -> ()
                    )
                }


    type MediaSessionCallback(service:MediaPlayerServiceBinder) =
        inherit MediaSession.Callback()

        override this.OnPause () =
            match service.Service.CurrentFile, service.Service.LastPositionBeforeStop with
            | Some f, Some p ->
                service.Service.PlayFile f p |> Async.RunSynchronously
                ()
            | _ ->
                ()

            
        override this.OnStop () =
            service.Service.Stop ()




    type MediaPlayerServiceBinder(service:DependencyServices.IAudioPlayer) =
        inherit Binder()

        member this.Service with get():DependencyServices.IAudioPlayer = service

        
        
