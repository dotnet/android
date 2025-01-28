using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// NativeAOT's compiler (ILC) expects to find tooling in $PATH
/// </summary>
public class SetNdkPathForIlc : AndroidTask
{
	public override string TaskPrefix => "SILC";

	[Required]
	public string NdkBinDirectory { get; set; } = "";

	public override bool RunTask ()
	{
		var ndkbin = Path.GetFullPath (NdkBinDirectory);
		var path = $"{ndkbin}{Path.PathSeparator}{Environment.GetEnvironmentVariable ("PATH")}";
		Log.LogDebugMessage ($"Setting $PATH to: {path}");
		Environment.SetEnvironmentVariable ("PATH", path);
		return !Log.HasLoggedErrors;
	}
}
