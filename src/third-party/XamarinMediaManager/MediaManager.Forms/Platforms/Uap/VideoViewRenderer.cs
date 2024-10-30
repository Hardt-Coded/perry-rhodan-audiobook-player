﻿using MediaManager.Forms.Platforms.Uap;
using Windows.Foundation;
using Windows.UI.Xaml.Media;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(MediaManager.Forms.VideoView), typeof(VideoViewRenderer))]
namespace MediaManager.Forms.Platforms.Uap
{
    public class VideoViewRenderer : ViewRenderer<VideoView, MediaManager.Platforms.Uap.Video.VideoView>
    {
        private MediaManager.Platforms.Uap.Video.VideoView _videoView;
        private static IMediaManager MediaManager => CrossMediaManager.Windows;

        protected override void OnElementChanged(ElementChangedEventArgs<VideoView> args)
        {
            base.OnElementChanged(args);

            if (args.OldElement != null)
            {
                args.OldElement.Dispose();
            }
            if (args.NewElement != null)
            {
                if (Control == null)
                {
                    _videoView = new MediaManager.Platforms.Uap.Video.VideoView();
                    SetNativeControl(_videoView);
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_videoView != null)
            {
                if (!double.IsInfinity(availableSize.Height))
                {
                    _videoView.Height = availableSize.Height;
                    _videoView.PlayerView.Height = availableSize.Height;
                }
                else
                {
                    _videoView.Height = MediaManager.MediaPlayer.VideoHeight > 0 ? MediaManager.MediaPlayer.VideoHeight : 300;
                    _videoView.PlayerView.Height = MediaManager.MediaPlayer.VideoHeight > 0 ? MediaManager.MediaPlayer.VideoHeight : 300;
                }

                _videoView.Width = availableSize.Width;
                _videoView.PlayerView.Width = availableSize.Width;
            }
            try
            {
                return base.MeasureOverride(availableSize);
            }
            catch (ArgumentException)
            {
                return DesiredSize;
            }
        }

        protected override void UpdateBackgroundColor()
        {
            base.UpdateBackgroundColor();
            if (Control != null)
                Control.Background = new SolidColorBrush(Element.BackgroundColor.ToWindowsColor());
        }

        protected override void Dispose(bool disposing)
        {
            _videoView = null;
            base.Dispose(disposing);
        }
    }
}
