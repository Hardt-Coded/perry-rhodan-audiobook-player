module AndroidCommon


    module ServiceHelpers =

        open Android.Content
        open Android.OS

        type Context with
            member this.StartForeGroundService<'a when 'a :> Android.App.Service> (?args:Bundle) =
                let intent = new Intent(this,typeof<'a>)
                match args with
                | None -> 
                    ()
                | Some args ->
                    intent.PutExtras(args) |> ignore
                
                if Build.VERSION.SdkInt >= BuildVersionCodes.O then
                    this.StartForegroundService(intent) |> ignore
                else
                    this.StartService(intent)  |> ignore
                
                
            

