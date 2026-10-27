#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xamarin.Android.Tools;

#if MSBUILD
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
#endif

namespace Xamarin.Android.Tasks;

public partial class MonoAndroidHelper
{
	public static string GetExecutablePath (string? dir, string exe)
	{
		if (dir is not { Length: > 0 })
			return exe;
		foreach (var e in Executables (exe))
			if (File.Exists (Path.Combine (dir, e)))
				return e;
		return exe;
	}

	public static IEnumerable<string> Executables (string executable)
	{
		var pathExt = Environment.GetEnvironmentVariable ("PATHEXT");
		var pathExts = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

		if (pathExts is not null) {
			foreach (var ext in pathExts)
				yield return Path.ChangeExtension (executable, ext);
		}
		yield return executable;
	}

	public static JdkInfo? GetJdkInfo (Action<TraceLevel, string> logger, string? javaSdkPath, Version minSupportedVersion, Version maxSupportedVersion)
	{
		JdkInfo? info = null;
		try {
			info = new JdkInfo (javaSdkPath ?? "", logger:logger);
		} catch {
			info = JdkInfo.GetKnownSystemJdkInfos (logger)
				.Where (jdk => jdk.Version >= minSupportedVersion && jdk.Version <= maxSupportedVersion)
				.FirstOrDefault ();
		}
		return info;
	}

#if MSBUILD
	public static AndroidVersions GetSupportedVersions (IBuildEngine4 engine, string[]? referenceAssemblyPaths)
	{
		var paths = (referenceAssemblyPaths ?? []).Select (Path.GetFullPath).ToArray ();
		// Share immutable API metadata by input, never a process-wide "current SDK".
		var key = (typeof (MonoAndroidHelper).Assembly.Location, nameof (AndroidVersions), string.Join ("\0", paths));
		var cached = engine.GetRegisteredTaskObjectAssemblyLocal<AndroidVersions> (key, RegisteredTaskObjectLifetime.Build);
		if (cached != null)
			return cached;

		var versions = new AndroidVersions (paths);
		engine.RegisterTaskObjectAssemblyLocal (key, versions, RegisteredTaskObjectLifetime.Build);
		return versions;
	}
#endif  // MSBUILD
}
