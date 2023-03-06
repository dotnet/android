using System;
using System.Collections.Generic;

namespace Xamarin.Android.Application;

class InputType
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
		foreach (InputTypeDetector detector in detectors) {
			InputReader? reader = DetectRecursively (detector, null, inputFilePath);
			if (reader != null) {
				return reader;
			}
		}

		return null;
	}

	static InputReader? DetectRecursively (InputTypeDetector detector, InputTypeDetector? parentDetector, string inputFilePath)
	{
		bool success;
		InputReader? reader;

		try {
			(success, reader) = detector.Detect (inputFilePath, parentDetector);
		} catch (Exception ex) {
			// TODO: real logging, no trace
			Console.Error.WriteLine ("Detector failed with exception:");
			Console.Error.WriteLine (ex.StackTrace);
			Console.Error.WriteLine ();
			return null;
		}

		if (!success) {
			return null;
		}

		if (reader != null) {
			return reader;
		}

		if (detector.Nested.Count == 0) {
			return null;
		}

		foreach (InputTypeDetector nestedDetector in detector.Nested) {
			reader = DetectRecursively (nestedDetector, detector, inputFilePath);
			if (reader != null) {
				return reader;
			}
		}

		return null;
	}
}
