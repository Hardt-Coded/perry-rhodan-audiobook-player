﻿using Android.Graphics;
using MediaManager.Library;
using MediaManager.Media;

namespace MediaManager.Platforms.Android.Media
{
    public class UriImageProvider : MediaExtractorProviderBase, IMediaItemImageProvider
    {
        public async Task<object> ProvideImage(IMediaItem mediaItem)
        {
            object image = null;
            try
            {
                if (!string.IsNullOrEmpty(mediaItem.ImageUri))
                {
                    if (mediaItem.ImageUri.StartsWith("/"))
                    {
                        mediaItem.Image = image = await GetBitmapFromFile(mediaItem.ImageUri).ConfigureAwait(false);
                    } else
                    {
                        mediaItem.Image = image = await GetBitmapFromUrl(mediaItem.ImageUri).ConfigureAwait(false);
                    }


                }
                if (image == null && !string.IsNullOrEmpty(mediaItem.AlbumImageUri))
                {
                    if (mediaItem.AlbumImageUri.StartsWith("/"))
                    {
                        mediaItem.AlbumImage = image = await GetBitmapFromFile(mediaItem.AlbumImageUri).ConfigureAwait(false);
                    } else
                    {
                        mediaItem.AlbumImage = image = await GetBitmapFromUrl(mediaItem.AlbumImageUri).ConfigureAwait(false);
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return image;
        }

        protected virtual async Task<Bitmap> GetBitmapFromUrl(string uri)
        {
            var url = new Java.Net.URL(uri);
            return await Task.Run(() => BitmapFactory.DecodeStreamAsync(url.OpenStream())).ConfigureAwait(false);
        }

        private async Task<Bitmap> GetBitmapFromFile(string file)
        {
            return await Task.Run(() => BitmapFactory.DecodeFile(file)).ConfigureAwait(false);
        }
    }
}
