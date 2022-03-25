using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Strips debug information from the native libraries
	/// </summary>
	public class StripNativeLibraries : AndroidToolTask
	{
		public override string TaskPrefix => "SNL";

		public ITaskItem [] SourceFiles { get; set; }

		public ITaskItem [] DestinationFiles { get; set; }

		string triple;
		ITaskItem source;
		ITaskItem destination;

		public override bool RunTask ()
		{
			if (SourceFiles.Length != DestinationFiles.Length)
				throw new ArgumentException ("source and destination count mismatch");
			if (SourceFiles == null || SourceFiles.Length == 0)
				return true;

			for (int i = 0; i < SourceFiles.Length; i++) {
				source = SourceFiles [i];
				destination = DestinationFiles [i];

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

				triple = GetNdkTripleFromAbi (abi);
				Directory.CreateDirectory (Path.GetDirectoryName (destination.ItemSpec));

				// This runs the tool
				base.RunTask ();

				// Stop early on failure
				if (Log.HasLoggedErrors)
					return false;
			}

			return !Log.HasLoggedErrors;
		}

		protected override string ToolName => GetToolName ($"{triple}-strip");

		protected override string GenerateFullPathToTool () => Path.Combine (ToolPath, ToolName);

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitchIfNotNull ("--strip-debug ", source.ItemSpec);
			cmd.AppendSwitchIfNotNull ("-o ", destination.ItemSpec);
			return cmd.ToString ();
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

		// We need this because `GenerateFullPathToTool()` is ignored when `ToolPath` is set (https://docs.microsoft.com/en-us/dotnet/api/microsoft.build.utilities.tooltask.generatefullpathtotool?view=msbuild-17-netcore)
		// Some tools we use with llvm-based toolchain have .cmd wrappers instead of .exe, so we need to determine the actual name from filesystem.
		string GetToolName (string toolName)
		{
			if (OS.IsWindows) {
				return Path.GetFileName (MonoAndroidHelper.GetExecutablePath (ToolPath, toolName));
			}

			return toolName;
		}
	}
}
