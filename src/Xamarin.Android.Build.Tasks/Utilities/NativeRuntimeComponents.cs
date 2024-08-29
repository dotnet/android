using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

class NativeRuntimeComponents
{
	internal class Archive
	{
		public readonly string Name;
		public bool Include => shouldInclude (this);
		public readonly bool WholeArchive;

		Func<Archive, bool> shouldInclude;

		public Archive (string name, Func<Archive, bool>? include = null, bool wholeArchive = false)
		{
			Name = name;
			shouldInclude = include == null ? ((Archive arch) => true) : include;
			WholeArchive = wholeArchive;
		}
	}

	internal class MonoComponentArchive : Archive
	{
		public readonly string ComponentName;

		public MonoComponentArchive (string name, string componentName, Func<Archive, bool> include)
			: base (name, include)
		{
			ComponentName = componentName;
		}
	}

	class AndroidArchive : Archive
	{
		public AndroidArchive (string name)
			: base (name, wholeArchive: true)
		{}
	}

	class BclArchive : Archive
	{
		public BclArchive (string name, bool wholeArchive = true)
			: base (name, wholeArchive: wholeArchive)
		{}
	}

	readonly ITaskItem[] monoComponents;

	public readonly List<Archive> KnownArchives;
	public readonly List<string> NativeLibraries;

	public NativeRuntimeComponents (ITaskItem[] monoComponents)
	{
		this.monoComponents = monoComponents;
		KnownArchives = new () {
			// Mono components
			new MonoComponentArchive ("libmono-component-debugger-static.a",                 "debugger",            IncludeIfMonoComponentPresent),
			new MonoComponentArchive ("libmono-component-debugger-stub-static.a",            "debugger",            IncludeIfMonoComponentAbsent),
			new MonoComponentArchive ("libmono-component-diagnostics_tracing-static.a",      "diagnostics_tracing", IncludeIfMonoComponentPresent),
			new MonoComponentArchive ("libmono-component-diagnostics_tracing-stub-static.a", "diagnostics_tracing", IncludeIfMonoComponentAbsent),
			new MonoComponentArchive ("libmono-component-hot_reload-static.a",               "hot_reload",          IncludeIfMonoComponentPresent),
			new MonoComponentArchive ("libmono-component-hot_reload-stub-static.a",          "hot_reload",          IncludeIfMonoComponentAbsent),
			new MonoComponentArchive ("libmono-component-marshal-ilgen-static.a",            "marshal-ilgen",       IncludeIfMonoComponentPresent),
			new MonoComponentArchive ("libmono-component-marshal-ilgen-stub-static.a",       "marshal-ilgen",       IncludeIfMonoComponentAbsent),

			// MonoVM runtime + BCL
			new Archive ("libmonosgen-2.0.a"),
			new BclArchive ("libSystem.Globalization.Native.a"),
			new BclArchive ("libSystem.IO.Compression.Native.a"),
			new BclArchive ("libSystem.Native.a"),
			new BclArchive ("libSystem.Security.Cryptography.Native.Android.a"),

			// .NET for Android
			new AndroidArchive ("libpinvoke-override-dynamic-release.a"),
			new AndroidArchive ("libruntime-base-release.a"),
			new AndroidArchive ("libxa-java-interop-release.a"),
			new AndroidArchive ("libxa-lz4-release.a"),
			new AndroidArchive ("libxa-shared-bits-release.a"),
			new AndroidArchive ("libmono-android.release-static-release.a"),
		};

		// Just the base names of libraries to link into the unified runtime.  Must have all the dependencies of all the static archives we
		// link into the final library.
		NativeLibraries = new () {
			"c",
			"dl",
			"m",
			"z",
			"log",
		};
	}

	bool MonoComponentExists (Archive archive)
	{
		if (monoComponents.Length == 0) {
			return false;
		}

		var mcArchive = archive as MonoComponentArchive;
		if (mcArchive == null) {
			throw new ArgumentException (nameof (archive), "Must be an instance of MonoComponentArchive");
		}

		foreach (ITaskItem item in monoComponents) {
			if (String.Compare (item.ItemSpec, mcArchive.ComponentName, StringComparison.OrdinalIgnoreCase) == 0) {
				return true;
			}
		}

		return false;
	}

	bool IncludeIfMonoComponentAbsent (Archive archive)
	{
		return !MonoComponentExists (archive);
	}

	bool IncludeIfMonoComponentPresent (Archive archive)
	{
		return MonoComponentExists (archive);
	}
}
