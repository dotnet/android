using Xamarin.Tools.Zip;

namespace Xamarin.Android.Application;

class DetectorIsAabArchive : DetectorSupportedAndroidArchive
{
	protected override InputReader? CreateReader (ZipArchive archive, string inputFilePath)
	{
		return new InputReaderAab (archive, inputFilePath);
	}

	protected override bool HasAllRequiredEntries (ZipArchive archive, string inputFilePath)
	{
		return archive.ContainsEntry ("base/manifest/AndroidManifest.xml", caseSensitive: true);
	}
}
