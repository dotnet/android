using System.IO;

namespace Xamarin.Android.Application;

class DetectorIsFile : InputTypeDetector
{
    public override (bool accepted, InputReader? reader) Detect (string inputFilePath, InputTypeDetector? parent)
    {
	    if (Directory.Exists (inputFilePath)) {
		    FailureReason = $"Input path '{inputFilePath}' refers to directory";
		    return (false, null);
	    }

	    if (!File.Exists (inputFilePath)) {
		    FailureReason = $"Input path '{inputFilePath}' does not exist";
		    return (false, null);
	    }

	    return (true, null);
    }
}
