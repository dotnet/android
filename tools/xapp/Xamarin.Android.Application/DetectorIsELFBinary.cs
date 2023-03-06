using System;

namespace Xamarin.Android.Application;

class DetectorIsELFBinary : InputTypeDetector
{
	public override (bool accepted, InputReader? reader) Detect (string inputFilePath, InputTypeDetector? parent)
	{
		throw new NotImplementedException ();

		return (false, null);
	}
}
