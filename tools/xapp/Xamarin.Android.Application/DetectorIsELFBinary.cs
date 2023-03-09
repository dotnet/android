using System;

using Xamarin.Android.Application.Utilities;

namespace Xamarin.Android.Application;

class DetectorIsELFBinary : InputTypeDetector
{
	public override (bool accepted, InputReader? reader) Detect (string inputFilePath, InputTypeDetector? parent, ILogger log)
	{
		throw new NotImplementedException ();
	}
}
