﻿using Foundation;
using MediaManager.Media;

namespace MediaManager.Platforms.Ios.Media
{
    public class AppleMediaExtractor : MediaExtractorBase, IMediaExtractor
    {
        public AppleMediaExtractor()
        {
        }

        public override IList<IMediaExtractorProvider> CreateProviders()
        {
            var providers = base.CreateProviders();
            providers.Add(new AVAssetProvider());
            providers.Add(new Ios.Media.AVAssetImageProvider());
            return providers;
        }

        protected override Task<string> GetResourcePath(string resourceName)
        {
            string path = null;

            var filename = Path.GetFileNameWithoutExtension(resourceName);
            var extension = Path.GetExtension(resourceName);

            path = NSBundle.MainBundle.PathForResource(filename, extension);

            return Task.FromResult(path);
        }
    }
}
