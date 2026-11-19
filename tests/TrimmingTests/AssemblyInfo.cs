using NUnit.Framework;

// Case builds are heavy (full trimming, NativeAOT), so cap how many run at once.
[assembly: LevelOfParallelism (2)]
