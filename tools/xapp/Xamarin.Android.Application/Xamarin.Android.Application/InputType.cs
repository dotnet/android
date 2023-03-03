using System.Collections.Generic;

namespace Xamarin.Android.Application;

public class InputType
{
	static readonly List<InputTypeDetector> detectors = new List<InputTypeDetector> {
		new DetectorIsFile {
			Nested = {
				new DetectorIsZipArchive {
					Nested = {
						new DetectorIsApkArchive (),
						new DetectorIsAabArchive (),
					},
				},

				new DetectorIsELFBinary {
					Nested = {
						new DetectorIsXamarinAppSharedLibrary (),
					},
				},

				new DetectorIsAssemblyStore (),
			},
		},
	};

	public static InputReader? Detect (string inputFilePath)
	{
		return null;
	}
}
