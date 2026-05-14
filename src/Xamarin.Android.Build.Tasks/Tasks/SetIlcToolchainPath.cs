#nullable enable
using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Adds the toolchain bin directory to $PATH so that NativeAOT's compiler (ILC)
/// can find tools like llvm-objcopy and llvm-ar.
/// </summary>
public class SetIlcToolchainPath : AndroidTask
{
	public override string TaskPrefix => "SILC";

	[Required]
	public string ToolchainBinDirectory { get; set; } = "";

	public override bool RunTask ()
	{
		var binDir = Path.GetFullPath (ToolchainBinDirectory);
		var path = $"{binDir}{Path.PathSeparator}{Environment.GetEnvironmentVariable ("PATH")}";
		Log.LogDebugMessage ($"Setting $PATH to: {path}");
		Environment.SetEnvironmentVariable ("PATH", path);
		return !Log.HasLoggedErrors;
	}
}
