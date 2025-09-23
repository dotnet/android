using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ApplicationUtility;

/// <summary>
/// Given path to a file, or a stream, this class tries to
/// detect whether or not the thing is an application aspect
/// we know or we can handle.
/// </summary>
public class Detector
{
	// Aspects must be listed in the order of detection, from the biggest (the most generic) to the
	// smallest (the least generic) aspects.
	public readonly static List<Type> KnownTopLevelAspects = new () {
		typeof (ApplicationPackage),
		typeof (AssemblyStore),
		typeof (ApplicationAssembly),
		typeof (NativeAotSharedLibrary),
		typeof (SharedLibrary),
	};

	public static IAspect? FindAspect (string path)
	{
		Log.Debug ($"Looking for aspect matching '{path}'");
		if (!File.Exists (path)) {
			return null;
		}

		using Stream fs = File.OpenRead (path);
		return TryFindAspect (fs, path);
	}

	public static IAspect? FindAspect (Stream stream, string? description = null)
	{
		Log.Debug ($"Looking for aspect supporting a stream ('{description}')");
		return TryFindAspect (stream, description);
	}

	static IAspect? TryFindAspect (Stream stream, string? description)
	{
		var flags = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static;

		foreach (Type aspect in KnownTopLevelAspects) {
			LogBanner ($"Probing aspect: {aspect}");

			object? result = aspect.InvokeMember (
				"ProbeAspect", flags, null, null, new object?[] { stream, description }
			);

			var state = result as IAspectState;
			if (state == null || !state.Success) {
				continue;
			}

			LogBanner ($"Loading aspect: {aspect}");
			result = aspect.InvokeMember (
				"LoadAspect", flags, null, null, new object?[] { stream, state, description }
			);
			if (result != null) {
				return (IAspect)result;
			}
		}

		return null;

		void LogBanner (string what)
		{
			Log.Debug ();
			Log.Debug ("##########");
			Log.Debug (what);
			Log.Debug ();
		}
	}

}
