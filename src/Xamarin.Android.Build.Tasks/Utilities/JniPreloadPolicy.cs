#nullable enable

using System;
using System.Collections.Generic;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

static class JniPreloadPolicy
{
	public static bool ShouldIgnore (TaskLoggingHelper log, ICollection<string> libsToIgnore, ITaskItem libItem)
	{
		if (libsToIgnore.Count == 0) {
			return false;
		}

		string? libFileName = GetFileName (log, libItem);
		if (libFileName == null) {
			return false;
		}

		return libsToIgnore.Contains (libFileName);
	}

	public static ICollection<string> MakeIgnoreCollection (TaskLoggingHelper log, ICollection<ITaskItem>? alwaysPreload, ICollection<ITaskItem>? ignorePreload)
	{
		var libsToIgnore = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		if (ignorePreload == null || ignorePreload.Count == 0) {
			return libsToIgnore;
		}

		var neverIgnore = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		if (alwaysPreload != null) {
			foreach (ITaskItem item in alwaysPreload) {
				string? fileName = GetFileName (log, item);
				if (fileName != null) {
					neverIgnore.Add (fileName);
				}
			}
		}

		foreach (ITaskItem item in ignorePreload) {
			string? fileName = GetFileName (log, item);
			if (fileName == null) {
				continue;
			}

			if (neverIgnore.Contains (fileName)) {
				log.LogDebugMessage ($"Native library '{item.ItemSpec}' cannot be ignored when preloading JNI native libraries.");
				continue;
			}

			libsToIgnore.Add (fileName);
		}

		return libsToIgnore;
	}

	static string? GetFileName (TaskLoggingHelper log, ITaskItem item)
	{
		string? name = item.GetMetadata ("ArchiveFileName");
		if (String.IsNullOrEmpty (name)) {
			name = MonoAndroidHelper.GetNormalizedNativeLibraryName (item);
		}

		if (String.IsNullOrEmpty (name)) {
			log.LogDebugMessage ($"Failed to convert item path '{item.ItemSpec}' to canonical native shared library name.");
			return null;
		}

		return name;
	}
}
