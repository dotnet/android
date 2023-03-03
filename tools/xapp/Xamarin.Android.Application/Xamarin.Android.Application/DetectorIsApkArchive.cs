using System;

namespace Xamarin.Android.Application;

class DetectorIsApkArchive : InputTypeDetector
{
	public override (bool accepted, InputReader? reader) Detect (string inputFilePath, InputTypeDetector? parent)
	{
		if (parent == null) {
			throw new ArgumentNullException (nameof (parent));
		}

		var zipDetector = parent as DetectorIsZipArchive ?? throw new ArgumentException ("must be an instance of DetectorIsZipArchive class", nameof (parent));

		throw new NotImplementedException ();

		return (false, null);
	}
}
