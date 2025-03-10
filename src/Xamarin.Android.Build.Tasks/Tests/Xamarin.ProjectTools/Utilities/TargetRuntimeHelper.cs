using System;
using System.Collections.Generic;

using Xamarin.Android.Tasks;

namespace Xamarin.ProjectTools;

public class TargetRuntimeHelper
{
	static readonly bool useMonoRuntime;
	static readonly string[] coreClrSupportedAbis = new []{ "arm64-v8a", "x86_64" };
	static readonly HashSet<string> coreClrAbis = new (coreClrSupportedAbis, StringComparer.OrdinalIgnoreCase);

	static TargetRuntimeHelper ()
	{
		string? envvar = Environment.GetEnvironmentVariable ("USE_MONO_RUNTIME");
		if (envvar == null || envvar.Length == 0 || String.Compare ("true", envvar, StringComparison.OrdinalIgnoreCase) == 0) {
			useMonoRuntime = true;
		} else {
			useMonoRuntime = false;
		}
	}

	/// <summary>
	/// This must be changed when we're ready to release. It is used to make tests which require a certain amount of warnings to
	/// work when the XA1040 warning is issued (see Xamarin.Android.Common.targets, the `_CheckNonIdealAppConfigurations` target)
	/// </summary>
	public static bool CoreClrIsExperimental => true;
	public static bool UseMonoRuntime => useMonoRuntime;
	public static bool UseCoreCLR => !useMonoRuntime;
	public static string[] CoreClrSupportedAbis => coreClrSupportedAbis;

	public static bool CoreClrSupportsAbi (string abiName) => coreClrAbis.Contains (abiName);

	/// <summary>
	/// <param ref="runtimeIdentifiers" /> contains a list of semicolon-separated RIDs (a single RID without
	/// semicolons is also fine) which will be checked against the list of RIDs supported by CoreCLR. If even
	/// a single RID isn't supported, `false` is returned.
	/// </summary>
	public static bool CoreClrSupportsAllRIDs (string runtimeIdentifiers)
	{
		foreach (string rid in runtimeIdentifiers.Split (';', StringSplitOptions.RemoveEmptyEntries)) {
			if (!CoreClrSupportsAbi (MonoAndroidHelper.RidToAbi (rid))) {
				return false;
			}
		}

		return true;
	}
}
