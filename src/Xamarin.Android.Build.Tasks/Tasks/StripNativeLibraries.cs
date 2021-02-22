using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Processes .so files coming from @(ResolvedFileToPublish).
	/// * Checks if ABI is valid
	/// * Strips debug information from the native libraries
	/// </summary>
	public class StripNativeLibraries : AndroidTask
	{
		public override string TaskPrefix => "SNL";

		[Required]
		public ITaskItem [] Libraries { get; set; }

		[Required]
		public string LocalPath { get; set; }

		[Required]
		public string AndroidBinUtilsDirectory { get; set; }

		[Output]
		public ITaskItem [] OutputLibraries { get; set; }

		public override bool RunTask ()
		{
			if (Libraries == null || Libraries.Length == 0)
				return true;

			var output = new List<ITaskItem> (Libraries.Length);
			var ext = OS.IsWindows ? ".exe" : "";
			foreach (var library in Libraries) {
				var abi = AndroidRidAbiHelper.GetNativeLibraryAbi (library);
				if (string.IsNullOrEmpty (abi)) {
					var packageId = library.GetMetadata ("NuGetPackageId");
					if (!string.IsNullOrEmpty (packageId)) {
						Log.LogCodedWarning ("XA4301", library.ItemSpec, 0, Properties.Resources.XA4301_ABI_NuGet, library.ItemSpec, packageId);
					} else {
						Log.LogCodedWarning ("XA4301", library.ItemSpec, 0, Properties.Resources.XA4301_ABI, library.ItemSpec);
					}
					continue;
				}

				var tripple = GetNdkTrippleFromAbi (abi);
				var exe = Path.Combine (AndroidBinUtilsDirectory, $"{tripple}-strip{ext}");
				Log.LogDebugMessage ($"strip: {exe} lib: {library} abi: {abi}");

				var localDir = Path.Combine (LocalPath, library.GetMetadata ("RuntimeIdentifier"));
				Directory.CreateDirectory (localDir);

				var filename = library.GetMetadata ("Filename");
				var extension = library.GetMetadata ("Extension");
				var localLib = Path.Combine (localDir, $"{filename}{extension}");
				using var proc = Process.Start (Path.Combine (AndroidBinUtilsDirectory, $"{tripple}-strip{ext}"), $"--strip-debug \"{library.ItemSpec}\" -o \"{localLib}\"");
				proc.WaitForExit ();

				var code = proc.ExitCode;
				if (code != 0)
					Log.LogCodedError ("XA0000", $"Unable to strip native library: {library.ItemSpec}, strip tool exited with code: {code}");

				Log.LogDebugMessage ($"localLib: {localLib} code: {code}");
				library.ItemSpec = localLib;
				output.Add (library);
			}

			OutputLibraries = output.ToArray ();

			return !Log.HasLoggedErrors;
		}

		string GetNdkTrippleFromAbi (string abi)
		{
			return abi switch {
				"arm64-v8a" => "aarch64-linux-android",
				"armeabi-v7a" => "arm-linux-androideabi",
				"x86" => "i686-linux-android",
				"x86_64" => "x86_64-linux-android",
				_ => throw new InvalidOperationException ($"Unknown ABI: {abi}"),
			};
		}
	}
}
