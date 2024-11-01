using MediaManager.Library;

namespace MediaManager.Playback
{
    public class PositionChangedEventArgs : EventArgs
    {
        public PositionChangedEventArgs(TimeSpan position, IMediaItem mediaItem)
        {
            Position = position;
            MediaItem = mediaItem;
        }

        public TimeSpan Position { get; }
        public IMediaItem MediaItem { get; }
    }
}
