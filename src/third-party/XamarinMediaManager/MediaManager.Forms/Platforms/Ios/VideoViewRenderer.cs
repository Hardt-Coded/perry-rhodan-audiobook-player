﻿using MediaManager.Forms.Platforms.iOS;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls.Compatibility.Platform.iOS;
using Microsoft.Maui.Controls.Platform;

[assembly: ExportRenderer(typeof(MediaManager.Forms.VideoView), typeof(VideoViewRenderer))]
namespace MediaManager.Forms.Platforms.iOS
{
    [Foundation.Preserve(AllMembers = true)]
    public class VideoViewRenderer : ViewRenderer<VideoView, MediaManager.Platforms.Ios.Video.VideoView>
    {
        private MediaManager.Platforms.Ios.Video.VideoView _videoView;

        protected override void OnElementChanged(ElementChangedEventArgs<VideoView> args)
        {
            if (args.OldElement != null)
            {
                args.OldElement.Dispose();
            }
            if (args.NewElement != null)
            {
                if (Control == null)
                {
                    //TODO: maybe pass in the UIView to the videoview here
                    _videoView = new MediaManager.Platforms.Ios.Video.VideoView();
                    SetNativeControl(_videoView);
                }
            }

            base.OnElementChanged(args);
        }

        protected override void SetBackgroundColor(Color color)
        {
            base.SetBackgroundColor(color);
            if (Control?.PlayerViewController?.View != null)
                Control.PlayerViewController.View.BackgroundColor = color.ToUIColor();
        }

        protected override void Dispose(bool disposing)
        {
            _videoView = null;
            base.Dispose(disposing);
        }
    }
}
