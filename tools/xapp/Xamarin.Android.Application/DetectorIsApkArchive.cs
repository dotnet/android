using Xamarin.Tools.Zip;

namespace Xamarin.Android.Application;

class DetectorIsApkArchive : DetectorSupportedAndroidArchive
{
	protected override InputReader? CreateReader (ZipArchive archive, string inputFilePath)
	{
		return new InputReaderApk (archive, inputFilePath);
	}

	protected override bool HasAllRequiredEntries (ZipArchive archive, string inputFilePath)
	{
		return archive.ContainsEntry ("AndroidManifest.xml", caseSensitive: true);
	}
}
