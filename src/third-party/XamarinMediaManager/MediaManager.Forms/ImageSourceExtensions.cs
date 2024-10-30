﻿namespace MediaManager.Forms
{
    public static partial class ImageSourceExtensions
    {
        public static ImageSource ToImageSource(this object image)
        {
            if (image is ImageSource imageSource)
                return imageSource;
#if ANDROID
            if (image is Android.Graphics.Bitmap bitmap)
                return bitmap.ToImageSource();
#elif IOS || MACCATALYST
            if (image is UIKit.UIImage uIImage)
                return uIImage.ToImageSource();
            if (image is CoreGraphics.CGImage cgImage)
                return cgImage.ToImageSource();
#elif UWP
            //TODO: This one should not be async. It might deadlock
            //if (image is Windows.UI.Xaml.Media.Imaging.BitmapImage bitmapImage)
            //    return bitmapImage.ToImageSource();
#endif
            return null;
        }
    }
}
