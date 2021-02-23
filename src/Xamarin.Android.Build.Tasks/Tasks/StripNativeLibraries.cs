using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Strips debug information from the native libraries
	/// </summary>
	public class StripNativeLibraries : AndroidTask
	{
		public override string TaskPrefix => "SNL";

		public ITaskItem [] SourceFiles { get; set; }

		public ITaskItem [] DestinationFiles { get; set; }

		[Required]
		public string AndroidBinUtilsDirectory { get; set; }

		public override bool RunTask ()
		{
			if (SourceFiles.Length != DestinationFiles.Length)
				throw new ArgumentException ("source and destination count mismatch");
			if (SourceFiles == null || SourceFiles.Length == 0)
				return true;

			var ext = OS.IsWindows ? ".exe" : "";
			for (int i = 0; i < SourceFiles.Length; i++) {
				var source = SourceFiles [i];
				var destination = DestinationFiles [i];

				var abi = AndroidRidAbiHelper.GetNativeLibraryAbi (source);
				if (string.IsNullOrEmpty (abi)) {
					var packageId = source.GetMetadata ("NuGetPackageId");
					if (!string.IsNullOrEmpty (packageId)) {
						Log.LogCodedWarning ("XA4301", source.ItemSpec, 0, Properties.Resources.XA4301_ABI_NuGet, source.ItemSpec, packageId);
					} else {
						Log.LogCodedWarning ("XA4301", source.ItemSpec, 0, Properties.Resources.XA4301_ABI, source.ItemSpec);
					}
					continue;
				}

				var triple = GetNdkTripleFromAbi (abi);
				var exe = Path.Combine (AndroidBinUtilsDirectory, $"{triple}-strip{ext}");
				Directory.CreateDirectory (Path.GetDirectoryName (destination.ItemSpec));

				using var proc = Process.Start (Path.Combine (AndroidBinUtilsDirectory, exe), $"--strip-debug \"{source.ItemSpec}\" -o \"{destination.ItemSpec}\"");
				proc.WaitForExit ();

				var code = proc.ExitCode;
				if (code != 0)
					Log.LogCodedError ("XA3008", source.ItemSpec, code);
			}

			return !Log.HasLoggedErrors;
		}

		string GetNdkTripleFromAbi (string abi)
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
