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
		typeof (PackageAPK),
		typeof (PackageAAB),
		typeof (PackageBase),
		typeof (AssemblyStore),
		typeof (ApplicationAssembly),
		typeof (NativeAotSharedLibrary),
		typeof (XamarinAppSharedLibrary),
		typeof (MonoAotSharedLibrary),
		typeof (DotNetAndroidWrapperSharedLibrary),
		typeof (SharedLibrary),
	};

	readonly static List<Type> KnownSharedLibraryAspects = new () {
		typeof (NativeAotSharedLibrary),
		typeof (XamarinAppSharedLibrary),
		typeof (MonoAotSharedLibrary),
		typeof (DotNetAndroidWrapperSharedLibrary),
		typeof (SharedLibrary),
	};

	public static IAspect? FindAspect (string path)
	{
		Log.Debug ($"Looking for aspect matching '{path}'");
		if (!File.Exists (path)) {
			return null;
		}

		using Stream fs = File.OpenRead (path);
		return TryFindTopLevelAspect (fs, path);
	}

	public static IAspect? FindAspect (Stream stream, string? description = null)
	{
		Log.Debug ($"Looking for aspect supporting a stream ('{description}')");
		return TryFindTopLevelAspect (stream, description);
	}

	public static SharedLibrary? FindSharedLibraryAspect (Stream stream, string? description = null)
	{
		Log.Debug ($"Looking for shared library aspect ('{description}')");
		return (SharedLibrary?)TryFindAspect (KnownSharedLibraryAspects, stream, description);
	}

	static IAspect? TryFindTopLevelAspect (Stream stream, string? description) => TryFindAspect (KnownTopLevelAspects, stream, description);

	static IAspect? TryFindAspect (List<Type> aspectTypes, Stream stream, string? description)
	{
		foreach (Type aspectType in aspectTypes) {
			try {
				IAspect? aspect = TryProbeAndLoadAspect (aspectType, stream, description);
				if (aspect != null) {
					return aspect;
				}
			} catch (Exception ex) {
				Log.Warning ($"Failed to probe and load aspect '{aspectType}'", ex);
			}
		}

		return null;
	}

	static IAspect? TryProbeAndLoadAspect (Type aspect, Stream stream, string? description)
	{
		const BindingFlags flags = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

		LogBanner ($"Probing aspect: {aspect}");
		object? result = aspect.InvokeMember (
			"ProbeAspect", flags, null, null, new object?[] { stream, description }
		);

		var state = result as IAspectState;
		if (state == null || !state.Success) {
			return null;
		}

		LogBanner ($"Loading aspect: {aspect}");
		result = aspect.InvokeMember (
			"LoadAspect", flags, null, null, new object?[] { stream, state, description }
		);

		if (result != null) {
			return (IAspect)result;
		}

		return null;

		void LogBanner (string what)
		{
			Log.Debug ();
			Log.Debug ($"# {what}");
			Log.Debug ();
		}
	}
}
