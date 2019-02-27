module AudioPlayerAgents

    open Android.App
    open Android.OS
    open Android.Media
    open Android.Media.Session
    open Android.Content
    
    open Android.Runtime
    open Android.Views
    

    open Xamarin.Forms.Platform.Android   

    open AudioPlayerState


    [<Service>]
    type AudioPlayerService() as self =
        inherit Service()

        override this.OnCreate () =
            ()

        override this.OnStartCommand(intent,_,_) =

            StartCommandResult.Sticky

        override this.OnBind _ =
            null

        override this.OnDestroy() =
            ()



            

