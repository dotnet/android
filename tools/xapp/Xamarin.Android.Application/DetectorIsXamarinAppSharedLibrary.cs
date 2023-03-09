using System;

using Xamarin.Android.Application.Utilities;

namespace Xamarin.Android.Application;

class DetectorIsXamarinAppSharedLibrary : InputTypeDetector
{
	public override (bool accepted, InputReader? reader) Detect (string inputFilePath, InputTypeDetector? parent, ILogger log)
	{
		if (parent == null) {
			throw new ArgumentNullException (nameof (parent));
		}

		var elfDetector = parent as DetectorIsELFBinary ?? throw new ArgumentException ("must be an instance of DetectorIsELFBinary class", nameof (parent));

		throw new NotImplementedException ();

		return (false, null);
	}
}
