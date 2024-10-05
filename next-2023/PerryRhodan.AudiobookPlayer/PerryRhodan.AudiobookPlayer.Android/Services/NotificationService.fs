namespace PerryRhodan.AudiobookPlayer.Android

module NotificationService =
    open AndroidX.AppCompat.App
    open Android.App
    open Android.OS
    open Services.DependencyServices
    open Android.Content
    open _Microsoft.Android.Resource.Designer

    
    
    let icon name = 
            let typ = typeof<Resource.Drawable>
            let field = typ.GetProperty(name)
            let value = field.GetValue(null)
            value :?> int
    
    type NotificationService () =
        let channelId = "perryrhodan.audiobookplayer.bla.notification"
        let channelName = "perryrhodan.audiobookplayer.bla.notification"
        let channelDescription = "perryrhodan.audiobookplayer.bla.notification"
        let pendingIntentId = 0815

        let titleKey = "title"
        let messageKey = "message"

        let smallIcon = Resource.Drawable.einsa_small_icon
        let logo = 
            //Android.Graphics.BitmapFactory.DecodeFile("@drawable/eins_a_medien_logo.png")
            Android.Graphics.BitmapFactory.DecodeResource(Application.Context.Resources ,Resource.Drawable.eins_a_medien_logo)
            
        let mutable messageId = -1
        let mutable (manager:NotificationManager option) = None

        let createNotificationChannel () =
            manager <- Some (Application.Context.GetSystemService(Application.NotificationService) :?> NotificationManager)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O) then
                let channelNameJava = new Java.Lang.String(channelName)
                let channel = new NotificationChannel(channelId,channelNameJava,NotificationImportance.Default, Description = channelDescription)
                channel.SetSound(null,null)
                channel.SetVibrationPattern(null)
                
                manager
                |> Option.map (_.CreateNotificationChannel(channel)
                )
                |> ignore
            ()

        interface INotificationService with
            
            override this.ShowMessage title message =

                let buildNotification (manager:NotificationManager) =
                    messageId <- messageId + 1
                    let intent = new Intent(Application.Context, typeof<AppCompatActivity>)
                    intent.PutExtra(titleKey,title) |> ignore
                    intent.PutExtra(messageKey,message) |> ignore
                    
                    let pendingIntent = PendingIntent.GetActivity(Application.Context, pendingIntentId, intent,PendingIntentFlags.Immutable ||| PendingIntentFlags.UpdateCurrent)
                    let builder = 
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) then
                            (new Notification.Builder(Application.Context, channelId))
                                .SetContentIntent(pendingIntent)
                                .SetContentTitle(title)
                                .SetContentText(message)
                                .SetSmallIcon(smallIcon)
                                .SetLargeIcon(logo)
                            
                        else
                            (new Notification.Builder(Application.Context, channelId))
                                    .SetContentIntent(pendingIntent)
                                    .SetContentTitle(title)
                                    .SetContentText(message)
                                    .SetSmallIcon(smallIcon)
                                    .SetLargeIcon(logo)
                                    .SetSound(null)
                                    .SetVibrate(null)

                    let notification = builder.Build()
                    manager.Notify(messageId,notification)
                    
                        
                    


                match manager with
                | None ->
                    createNotificationChannel ()
                    buildNotification manager.Value
                | Some manager ->
                    buildNotification manager
                ()

            
            
            
        
    

