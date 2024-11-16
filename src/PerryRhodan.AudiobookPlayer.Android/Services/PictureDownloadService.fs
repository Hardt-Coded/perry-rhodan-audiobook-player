module PictureDownloadServiceImplementation

    open AndroidX.AppCompat.App

    open Android.App
    open Android.OS
    open Android.Content
    open Domain
    open PerryRhodan.AudiobookPlayer.Services.Interfaces
    open PerryRhodan.AudiobookPlayer.ViewModel
    open _Microsoft.Android.Resource.Designer

    [<AutoOpen>]
    module private Settings =


        let smallIcon = Resource.Drawable.einsa_small_icon

        let logo =
            Android.Graphics.BitmapFactory.DecodeResource(Application.Context.Resources ,Resource.Drawable.eins_a_medien_logo)

        let downloadServiceNotificationId = 234254
        let downloadServiceChannelId = "perry.rhodan.download.notification"
        let downloadServiceChannelName = "Download Notification"
        let downloadServiceChannelDescription = "Download Notification for the Eins A Medien Audiobook Player"


    module private AndroidService =

        [<Service(Exported=true,Name="perry.rhodan.audioplayer.picdownloadservice",ForegroundServiceType=PM.ForegroundService.TypeDataSync)>]
        type PictureDownloadService() as self =

            inherit Service() with

                let buildNotification (title:string) (text:string) =
                    let intent = new Intent(self, typeof<AppCompatActivity>)
                    let pendingIntentId = 83475
                    let pendingIntent = PendingIntent.GetActivity(self, pendingIntentId, intent, PendingIntentFlags.Immutable ||| PendingIntentFlags.UpdateCurrent)

                    let builder =
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) then
                            (new Notification.Builder(self, downloadServiceChannelId))
                                .SetContentIntent(pendingIntent)
                                .SetContentTitle(title)
                                .SetContentText(text)
                                .SetSmallIcon(smallIcon)
                                .SetLargeIcon(logo)

                        else
                            (new Notification.Builder(self, downloadServiceChannelId))
                                .SetContentIntent(pendingIntent)
                                .SetContentTitle(title)
                                .SetContentText(text)
                                .SetSmallIcon(smallIcon)
                                .SetLargeIcon(logo)
                                .SetSound(null)
                                .SetVibrate(null)

                    builder.Build()


                let createNotificationChannel (manager:NotificationManager) =
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O) then
                        let channelNameJava = new Java.Lang.String(downloadServiceChannelName)
                        let channel = new NotificationChannel(downloadServiceChannelId, channelNameJava, NotificationImportance.Default, Description = downloadServiceChannelDescription)
                        channel.SetSound(null,null)
                        channel.SetVibrationPattern(null)
                        manager.CreateNotificationChannel(channel) |> ignore
                    ()


                let createDownloadServiceNotification title text =

                    let manager = (Application.Context.GetSystemService(Application.NotificationService) :?> NotificationManager)

                    let notify (manager:NotificationManager) (title:string) (text:string) =
                        let notification = buildNotification title text
                        manager.Notify(downloadServiceNotificationId, notification)
                        ()

                    createNotificationChannel manager
                    notify manager title text


                let updateNotification title text =
                    //createDownloadServiceNotification title text
                    let manager = (Application.Context.GetSystemService(Application.NotificationService) :?> NotificationManager)
                    let notification = buildNotification title text
                    manager.Notify(downloadServiceNotificationId, notification)


                let shutDownService () =
                    self.StopForeground(true)
                    self.StopSelf()


                override this.OnBind _ =
                    null

                override this.OnCreate () =
                    try
                        let manager = (Application.Context.GetSystemService(Application.NotificationService) :?> NotificationManager)
                        createNotificationChannel manager
                        this.StartForeground(downloadServiceNotificationId, buildNotification "Download" "Starte Download!", PM.ForegroundService.TypeDataSync)
                    with
                    | ex ->
                        Global.telemetryClient.TrackException ex
                        reraise()



                override this.OnStartCommand (intent,_,_) =
                    try
                        let shop =
                            match intent.GetStringExtra("shop") with
                            | null -> NewShop
                            | "OldShop" -> OldShop
                            | "NewShop" -> NewShop
                            | _ -> NewShop
                            
                            
                        let notification = updateNotification "Download"
                        let audioBooks =
                            match shop with
                            | NewShop -> AudioBookStore.globalAudiobookStore.Model.NewShopAudiobooks
                            | OldShop -> AudioBookStore.globalAudiobookStore.Model.OldShopAudiobooks
                            
                        PerryRhodan.AudiobookPlayer.Services.PictureDownloadService(shop, notification, shutDownService)
                            .StartDownloadPictures(audioBooks) |> ignore
                        StartCommandResult.Sticky
                    with
                    | ex ->
                        Global.telemetryClient.TrackException ex
                        shutDownService()
                        StartCommandResult.Sticky

    module DependencyService =

        open AndroidCommon.ServiceHelpers

        type PictureAndroidDownloadService () =
            interface IPictureDownloadService with

                override this.StartDownload shop =
                    let bundle = new Bundle()
                    bundle.PutString("shop", shop.ToString()) |> ignore
                    Application.Context.StartForeGroundService<AndroidService.PictureDownloadService>(bundle)