module DownloadServiceImplementation

    open Xamarin.Forms.Platform.Android
    open Android.Support.V4.App
    open Android.App
    open Android.OS
    open Android.Content

    [<AutoOpen>]
    module private Settings =

        open PerryRhodan.AudiobookPlayer.Android

        let icon name = 
            typeof<Resources.Drawable>.GetField(name).GetValue(null) :?> int


        let smallIcon = icon "einsa_small_icon"
    
        let logo = 
            Android.Graphics.BitmapFactory.DecodeResource(Android.App.Application.Context.Resources ,icon "eins_a_medien_logo")

        let downloadServiceNotificationId = 234254
        let downloadServiceChannelId = "perry.rhodan.sodaihfoiawqfoehrw"
        let downloadServiceChannelName = "perry.rhodan.sodaihfoiawqfoehrw"
        let downloadServiceChannelDescription = "Download Notification for the Eins A Medien Audiobook Player"


    module private AndroidService =

        [<Service(Name="perry.rhodan.audioplayer.downloadservice.bla0815")>]
        type DownloadService() as self =

            inherit Service() with

                let buildNotification (title:string) (text:string) =
                    let intent = new Intent(Android.App.Application.Context, typeof<FormsAppCompatActivity>)
                    let pendingIntentId = 83475
                    let pendingIntent = PendingIntent.GetActivity(Android.App.Application.Context, pendingIntentId, intent,PendingIntentFlags.UpdateCurrent)
                   
                    let builder = 
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) then
                            (new Notification.Builder(Android.App.Application.Context, downloadServiceChannelId))
                                .SetContentIntent(pendingIntent)
                                .SetContentTitle(title)
                                .SetContentText(text)
                                .SetSmallIcon(smallIcon)
                                .SetLargeIcon(logo)
                                
                        else
                            (new Notification.Builder(Android.App.Application.Context, downloadServiceChannelId))
                                .SetContentIntent(pendingIntent)
                                .SetContentTitle(title)
                                .SetContentText(text)
                                .SetSmallIcon(smallIcon)
                                .SetLargeIcon(logo)
                                .SetSound(null)
                                .SetVibrate(null)




                               
                    builder.Build()

                let createDownloadServiceNotification () =

                    let manager = (Android.App.Application.Context.GetSystemService(Android.App.Application.NotificationService) :?> NotificationManager)

                    let createNotificationChannel (manager:NotificationManager) =
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) then
                            let channelNameJava = new Java.Lang.String(downloadServiceChannelName)
                            let channel = new NotificationChannel(downloadServiceChannelId, channelNameJava, NotificationImportance.Default, Description = downloadServiceChannelDescription)
                            channel.SetSound(null,null)
                            channel.SetVibrationPattern(null)
                            manager.CreateNotificationChannel(channel) |> ignore
                        ()

                    let notify (manager:NotificationManager) (title:string) (text:string) =
                        let notification = buildNotification title text
                        manager.Notify(downloadServiceNotificationId, notification)
                        ()

                    createNotificationChannel manager
                    notify manager
                    
            
                let updateNotification =
                    createDownloadServiceNotification ()
                

                let shutDownService () =
                    self.StopForeground(true)
                    self.StopSelf()


                let downloadServiceMailbox =
                     Services.DownloadService.External.createExternalDownloadService
                        (Services.DownloadService.External.startDownload)
                        shutDownService
                        updateNotification


                override this.OnBind _ =
                    null

                override this.OnCreate () =
                    this.StartForeground(downloadServiceNotificationId, buildNotification "Download" "Starte Download!");
                
                
                
                override this.OnStartCommand (_,_,_) =
                
                    let serviceListener = Services.DownloadService.External.downloadServiceListener downloadServiceMailbox

                    serviceListener |> Services.DownloadService.registerServiceListener 

                    StartCommandResult.Sticky


    module DependencyService =
        
        open AndroidCommon.ServiceHelpers

        type DownloadService () =
            interface Services.DependencyServices.IDownloadService with

                override this.StartDownload () =
                    Android.App.Application.Context.StartForeGroundService<AndroidService.DownloadService>()