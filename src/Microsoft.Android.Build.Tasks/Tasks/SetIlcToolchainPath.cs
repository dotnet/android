using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

/// <summary>
/// Adds the toolchain bin directory to $PATH so that NativeAOT's compiler (ILC)
/// can find the official Android NDK tools.
/// </summary>
public class SetIlcToolchainPath : AndroidTask
{
	public override string TaskPrefix => "SILC";

	[Required]
	public string ToolchainBinDirectory { get; set; } = "";

	public override bool RunTask ()
	{
		var binDir = Path.GetFullPath (ToolchainBinDirectory);
		if (!Directory.Exists (binDir)) {
			Log.LogCodedError ("XA5101", Properties.Resources.XA5101, binDir);
			return false;
		}
		var path = $"{binDir}{Path.PathSeparator}{Environment.GetEnvironmentVariable ("PATH")}";
		Log.LogDebugMessage ($"Setting $PATH to: {path}");
		Environment.SetEnvironmentVariable ("PATH", path);
		return !Log.HasLoggedErrors;
	}
}
