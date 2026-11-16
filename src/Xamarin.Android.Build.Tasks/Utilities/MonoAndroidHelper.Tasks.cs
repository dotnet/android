#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

// Helpers shared by tasks in Xamarin.Android.Build.Tasks and Microsoft.Android.Build.Tasks.
partial class MonoAndroidHelper
{
	internal static bool LogInternalExceptions {
		get {
			return string.Equals (
					"icanhaz",
					Environment.GetEnvironmentVariable ("__XA_LOG_ERRORS__"),
					StringComparison.OrdinalIgnoreCase);
		}
	}

	/// <summary>
	/// Returns true if a filename starts with a . character.
	/// </summary>
	public static bool IsInvalidFilename (string path) =>
		Path.GetFileName (path).StartsWith (".", StringComparison.Ordinal);

	public static AndroidVersions GetSupportedVersions (IBuildEngine4 engine, string[]? referenceAssemblyPaths)
		=> GetSupportedVersions (engine, referenceAssemblyPaths?.Select (Path.GetFullPath).ToArray (), TaskEnvironment.Fallback);

	public static AndroidVersions GetSupportedVersions (IBuildEngine4 engine, string[]? referenceAssemblyPaths, TaskEnvironment taskEnvironment)
	{
		// GetAbsolutePath roots paths but does not collapse ".." for the cache key.
		var paths = (referenceAssemblyPaths ?? []).Select (path => new DirectoryInfo (taskEnvironment.GetAbsolutePath (path)).FullName).ToArray ();
		// Share immutable API metadata by input, never a process-wide "current SDK".
		var key = (typeof (MonoAndroidHelper).Assembly.Location, nameof (AndroidVersions), string.Join ("\0", paths));
		var cached = engine.GetRegisteredTaskObjectAssemblyLocal<AndroidVersions> (key, RegisteredTaskObjectLifetime.Build);
		if (cached != null)
			return cached;

		var versions = new AndroidVersions (paths);
		engine.RegisterTaskObjectAssemblyLocal (key, versions, RegisteredTaskObjectLifetime.Build);
		return versions;
	}
}
