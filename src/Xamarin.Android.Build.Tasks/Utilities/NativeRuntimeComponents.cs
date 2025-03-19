using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

class NativeRuntimeComponents
{
	internal class Archive
	{
		public readonly string Name;
		public readonly string JniOnLoadName;
		public bool Include => shouldInclude (this);
		public readonly bool WholeArchive;
		public bool DontExportSymbols { get; set; }
		public HashSet<string>? SymbolsToPreserve { get; set; }

		Func<Archive, bool> shouldInclude;

		public Archive (string name, Func<Archive, bool>? include = null, bool wholeArchive = false, string? jniOnLoadName = null)
		{
			Name = name;
			shouldInclude = include == null ? ((Archive arch) => true) : include;
			WholeArchive = wholeArchive;
			JniOnLoadName = jniOnLoadName;
		}
	}

	internal class MonoComponentArchive : Archive
	{
		public readonly string ComponentName;

		public MonoComponentArchive (string name, string componentName, Func<Archive, bool> include)
			: base (name, include)
		{
			ComponentName = componentName;
			DontExportSymbols = true;
		}
	}

	sealed class ClangBuiltinsArchive : Archive
	{
		public ClangBuiltinsArchive (string clangAbi)
			: base ($"libclang_rt.builtins-{clangAbi}-android.a")
		{}
	}

	class AndroidArchive : Archive
	{
		public AndroidArchive (string name)
			: base (name, wholeArchive: false)
		{}
	}

	sealed class BclArchive : Archive
	{
		public BclArchive (string name, bool wholeArchive = false, string? jniOnLoadName = null)
			: base (name, wholeArchive: wholeArchive, jniOnLoadName: jniOnLoadName)
		{
			DontExportSymbols = true;
		}
	}

	readonly ITaskItem[] monoComponents;

	public readonly List<Archive> KnownArchives;
	public readonly List<string> NativeLibraries;
	public readonly List<string> LinkStartFiles;
	public readonly List<string> LinkEndFiles;

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
			new Archive ("libmonosgen-2.0.a") {
				DontExportSymbols = true,
			},
			new BclArchive ("libSystem.Globalization.Native.a"),
			new BclArchive ("libSystem.IO.Compression.Native.a"),
			new BclArchive ("libSystem.Native.a"),
			new BclArchive ("libSystem.Security.Cryptography.Native.Android.a", jniOnLoadName: "AndroidCryptoNative_InitLibraryOnLoad") {
				SymbolsToPreserve = new (StringComparer.Ordinal) {
					// This isn't referenced directly by any code in libSystem.Security.Cryptography.Native.Android.  It is instead
					// referenced by the Java code shipped with the component (`DotnetProxyTrustManager`), as a native Java method:
					//
					//   static native boolean verifyRemoteCertificate(long sslStreamProxyHandle);
					//
					// Therefore we must reference it explicitly
					"Java_net_dot_android_crypto_DotnetProxyTrustManager_verifyRemoteCertificate"
				},

				// For now, we have to export all the symbols from this archive because we need the above `Java_net*` symbol to be
				// externally visible, and the linker's `--exclude-libs` flag works on the archive (.a) level.
				//
				// TODO: use `llvm-ar` to extract the relevant object file and link it separately?
				DontExportSymbols = false,
			},

			// .NET for Android
			new AndroidArchive ("libpinvoke-override-dynamic-release.a"),
			new AndroidArchive ("libruntime-base-release.a"),
			new AndroidArchive ("libxa-java-interop-release.a"),
			new AndroidArchive ("libxa-lz4-release.a"),
			new AndroidArchive ("libxa-shared-bits-release.a"),
			new AndroidArchive ("libmono-android.release-static-release.a"),

			// LLVM clang built-ins archives
			new ClangBuiltinsArchive ("aarch64"),
			new ClangBuiltinsArchive ("arm"),
			new ClangBuiltinsArchive ("i686"),
			new ClangBuiltinsArchive ("x86_64"),

			// Remove once https://github.com/dotnet/runtime/pull/107615 is merged and released
			new Archive ("libunwind.a") {
				DontExportSymbols = true,
			},
		};

		// Just the base names of libraries to link into the unified runtime.  Must have all the dependencies of all the static archives we
		// link into the final library.
		NativeLibraries = new () {
			"c",
			"dl",
			"m",
			"z",
			"log",

			// Atomic is a static library in clang, need to investigate if it's really needed
//			"atomic",
		};

		// Files that will be linked before any other object/archive/library files
		LinkStartFiles = new () {
			"crtbegin_so.o",
		};

		// Files that will be linked after any other object/archive/library files
		LinkEndFiles = new () {
			"crtend_so.o",
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
