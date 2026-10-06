#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

static class JniPreloadPolicy
{
	public static bool ShouldIgnore (TaskLoggingHelper log, ICollection<string> ignored, ITaskItem item)
	{
		if (ignored.Count == 0) {
			return false;
		}
		string? name = GetFileName (log, item);
		return name != null && ignored.Contains (name);
	}

	public static ICollection<string> MakeIgnoreCollection (TaskLoggingHelper log, ICollection<ITaskItem>? alwaysPreload, ICollection<ITaskItem>? ignorePreload)
	{
		var ignored = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		if (ignorePreload == null || ignorePreload.Count == 0) {
			return ignored;
		}
		var always = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (ITaskItem item in alwaysPreload ?? []) {
			string? name = GetFileName (log, item);
			if (name != null) {
				always.Add (name);
			}
		}
		foreach (ITaskItem item in ignorePreload) {
			string? name = GetFileName (log, item);
			if (name == null) {
				continue;
			}
			if (always.Contains (name)) {
				log.LogDebugMessage ($"Native library '{item.ItemSpec}' cannot be ignored when preloading JNI native libraries.");
			} else {
				ignored.Add (name);
			}
		}
		return ignored;
	}

	static string? GetFileName (TaskLoggingHelper log, ITaskItem item)
	{
		string? name = item.GetMetadata ("ArchiveFileName");
		if (name.IsNullOrEmpty ()) {
			name = MonoAndroidHelper.GetNormalizedNativeLibraryName (item);
		}
		if (name.IsNullOrEmpty ()) {
			log.LogDebugMessage ($"Failed to convert item path '{item.ItemSpec}' to canonical native shared library name.");
			return null;
		}
		return name;
	}
}
