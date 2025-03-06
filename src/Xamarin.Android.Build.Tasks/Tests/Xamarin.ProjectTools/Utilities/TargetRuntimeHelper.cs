using System;
using System.Collections.Generic;

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

	public static bool UseMonoRuntime => useMonoRuntime;
	public static bool UseCoreCLR => !useMonoRuntime;
	public static string[] CoreClrSupportedAbis => coreClrSupportedAbis;

	public static bool CoreClrSupportsAbi (string abiName) => coreClrAbis.Contains (abiName);
}
