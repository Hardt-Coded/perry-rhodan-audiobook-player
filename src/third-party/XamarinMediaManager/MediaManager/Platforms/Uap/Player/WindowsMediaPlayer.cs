﻿using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Platforms.Uap.Media;
using MediaManager.Platforms.Uap.Video;
using MediaManager.Player;
using MediaManager.Video;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Playback;
using MediaPlayerState = MediaManager.Player.MediaPlayerState;

namespace MediaManager.Platforms.Uap.Player
{
    public class WindowsMediaPlayer : MediaPlayerBase, IMediaPlayer<MediaPlayer, VideoView>
    {
        public WindowsMediaPlayer()
        {
        }

        protected MediaManagerImplementation MediaManager = CrossMediaManager.Windows;

        public VideoView PlayerView => VideoView as VideoView;

        private IVideoView _videoView;
        public override IVideoView VideoView
        {
            get => _videoView;
            set
            {
                SetProperty(ref _videoView, value);
                if (PlayerView != null)
                {
                    PlayerView.PlayerView.SetMediaPlayer(Player);
                    UpdateVideoView();
                }
            }
        }


        private MediaPlayer _player;
        public MediaPlayer Player
        {
            get
            {
                if (_player == null)
                    Initialize();
                return _player;
            }
            set => SetProperty(ref _player, value);
        }

        private MediaPlaybackList _mediaPlaybackList;
        public MediaPlaybackList MediaPlaybackList
        {
            get
            {
                if (_mediaPlaybackList == null)
                    _mediaPlaybackList = new MediaPlaybackList();
                return _mediaPlaybackList;
            }
            private set => _mediaPlaybackList = value;
        }

        public override void UpdateVideoAspect(VideoAspectMode videoAspectMode)
        {
            if (PlayerView == null)
                return;

            var playerView = PlayerView.PlayerView;

            switch (videoAspectMode)
            {
                case VideoAspectMode.None:
                    playerView.Stretch = Microsoft.UI.Xaml.Media.Stretch.None;
                    break;
                case VideoAspectMode.AspectFit:
                    playerView.Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform;
                    break;
                case VideoAspectMode.AspectFill:
                    playerView.Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill;
                    break;
                default:
                    playerView.Stretch = Microsoft.UI.Xaml.Media.Stretch.None;
                    break;
            }
        }

        public override void UpdateShowPlaybackControls(bool showPlaybackControls)
        {
            if (PlayerView == null)
                return;

            PlayerView.PlayerView.AreTransportControlsEnabled = showPlaybackControls;
        }

        public override void UpdateVideoPlaceholder(object value)
        {
            if (PlayerView?.PlayerView == null)
                return;

            if (value is ImageSource imageSource)
                PlayerView.PlayerView.PosterSource = imageSource;
        }

        public override void UpdateIsFullWindow(bool isFullWindow)
        {
            if (PlayerView == null)
                return;

            PlayerView.PlayerView.IsFullWindow = isFullWindow;
        }

        public virtual void Initialize()
        {
            Player = new MediaPlayer();
            Player.AudioCategory = MediaPlayerAudioCategory.Media;

            Player.MediaOpened += Player_MediaOpened;
            Player.MediaEnded += Player_MediaEnded;
            Player.MediaFailed += Player_MediaFailed;

            //Player.SourceChanged += MediaPlayer_SourceChanged;

            Player.PlaybackSession.BufferingProgressChanged += PlaybackSession_BufferingProgressChanged;
            Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            Player.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
            Player.PlaybackSession.NaturalVideoSizeChanged += PlaybackSession_NaturalVideoSizeChanged;
            Player.PlaybackSession.SeekCompleted += PlaybackSession_SeekCompleted;
        }

        protected virtual void Player_MediaOpened(MediaPlayer sender, object args)
        {

        }

        protected virtual void PlaybackSession_SeekCompleted(MediaPlaybackSession sender, object args)
        {
            //TODO: Maybe use this?
        }

        protected virtual void PlaybackSession_NaturalVideoSizeChanged(MediaPlaybackSession sender, object args)
        {
            VideoHeight = (int)sender.NaturalVideoHeight;
            VideoWidth = (int)sender.NaturalVideoWidth;
        }

        protected virtual void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
        {
            //TODO: Maybe use this?
        }

        protected virtual void PlaybackSession_BufferingProgressChanged(MediaPlaybackSession sender, object args)
        {
            MediaManager.Buffered = TimeSpan.FromMilliseconds(sender.BufferingProgress);
        }

        protected virtual void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            MediaManager.State = sender.PlaybackState.ToMediaPlayerState();
        }

        protected virtual void Player_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            MediaManager.OnMediaItemFailed(this, new MediaItemFailedEventArgs(MediaManager.Queue.Current, new Exception(args.ErrorMessage), args.ErrorMessage));
        }

        protected virtual void Player_MediaEnded(MediaPlayer sender, object args)
        {
            MediaManager.OnMediaItemFinished(this, new MediaItemEventArgs(MediaManager.Queue.Current));
        }

        public override Task Pause()
        {
            Player.Pause();
            return Task.CompletedTask;
        }

        public override async Task Play(IMediaItem mediaItem)
        {
            InvokeBeforePlaying(this, new MediaPlayerEventArgs(mediaItem, this));

            MediaPlaybackList.Items.Clear();

            foreach (var mediaQueueItem in MediaManager.Queue)
            {
                var mediaPlaybackItem = (await mediaQueueItem.ToMediaSource()).ToMediaPlaybackItem();
                MediaPlaybackList.Items.Add(mediaPlaybackItem);
                if (mediaQueueItem == mediaItem)
                {
                    MediaPlaybackList.StartingItem = mediaPlaybackItem;
                }
            }
            await Play(MediaPlaybackList);

            InvokeAfterPlaying(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        public override async Task Play(IMediaItem mediaItem, TimeSpan startAt, TimeSpan? stopAt = null)
        {
            InvokeBeforePlaying(this, new MediaPlayerEventArgs(mediaItem, this));

            MediaPlaybackList.Items.Clear();

            foreach (var mediaQueueItem in MediaManager.Queue)
            {
                var mediaPlaybackItem = (await mediaQueueItem.ToMediaSource()).ToMediaPlaybackItem(startAt, stopAt);
                MediaPlaybackList.Items.Add(mediaPlaybackItem);
                if (mediaQueueItem == mediaItem)
                {
                    MediaPlaybackList.StartingItem = mediaPlaybackItem;
                }
            }
            await Play(MediaPlaybackList);

            InvokeAfterPlaying(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        public virtual async Task Play(IMediaPlaybackSource source)
        {
            Player.Source = source;
            await Play();
        }

        public override Task Play()
        {
            Player.Play();
            return Task.CompletedTask;
        }

        public override Task SeekTo(TimeSpan position)
        {
            Player.PlaybackSession.Position = position;
            return Task.CompletedTask;
        }

        public override async Task Stop()
        {
            Player.Pause();
            await SeekTo(TimeSpan.Zero);
            MediaManager.State = MediaPlayerState.Stopped;
        }

        protected override void Dispose(bool disposing)
        {
            Player.MediaOpened -= Player_MediaOpened;
            Player.MediaEnded -= Player_MediaEnded;
            Player.MediaFailed -= Player_MediaFailed;
            Player.PlaybackSession.BufferingProgressChanged -= PlaybackSession_BufferingProgressChanged;
            Player.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
            Player.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;
            Player.PlaybackSession.NaturalVideoSizeChanged -= PlaybackSession_NaturalVideoSizeChanged;
            Player.PlaybackSession.SeekCompleted -= PlaybackSession_SeekCompleted;
            Player.Dispose();
            Player = null;
        }
    }
}
