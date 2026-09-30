using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

public class ResolveAndroidNdk : AndroidTask
{
	public override string TaskPrefix => "RANDK";

	public string? AndroidNdkDirectory { get; set; }
	public string AndroidRuntime { get; set; } = "";
	public bool StripNativeLibraries { get; set; }
	public string? CheckedBuild { get; set; }

	[Output]
	public string? ToolchainDirectory { get; set; }

	[Output]
	public string? LinkerToolPath { get; set; }

	[Output]
	public string? ObjcopyToolPath { get; set; }

	[Output]
	public string? StripToolPath { get; set; }

	[Output]
	public string? ClangRuntimeDirectory { get; set; }

	public override bool RunTask ()
	{
		var ndk = AndroidNdkTools.Create (AndroidNdkDirectory, Log);
		if (ndk == null) {
			return false;
		}
		ToolchainDirectory = ndk.ToolchainDirectory;
		bool nativeAot = string.Equals (AndroidRuntime, "NativeAOT", StringComparison.OrdinalIgnoreCase);
		if (nativeAot) {
			LinkerToolPath = ndk.GetToolPath ("ld.lld");
			ObjcopyToolPath = ndk.GetToolPath ("llvm-objcopy");
			string libraries = Path.Combine (ToolchainDirectory, "sysroot", "usr", "lib");
			if (!Directory.Exists (libraries)) {
				Log.LogCodedError ("XA5101", Properties.Resources.XA5101, libraries);
			}
		}
		if (StripNativeLibraries) {
			StripToolPath = ndk.GetToolPath ("llvm-strip");
		}
		if (nativeAot || !string.IsNullOrEmpty (CheckedBuild)) {
			ClangRuntimeDirectory = ndk.GetClangDeviceLibraryPath ();
		}
		return !Log.HasLoggedErrors;
	}
}
