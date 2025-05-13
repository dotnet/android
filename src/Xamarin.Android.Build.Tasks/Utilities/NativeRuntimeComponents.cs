using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

class NativeRuntimeComponents
{
	public sealed class KnownSets
	{
		public const string BCL = "bcl";
		public const string CoreClrRuntime = "coreclr";
		public const string CplusPlusRuntime = "c++";
		public const string XamarinAndroidRuntime = "xaruntime";
	}

	internal class Archive
	{
		public readonly string Name;
		public readonly string? JniOnLoadName;
		public bool Include => shouldInclude (this);
		public readonly bool WholeArchive;
		public bool DontExportSymbols { get; set; }
		public HashSet<string>? SymbolsToPreserve { get; set; }
		public string SetName { get; }

		public readonly bool NeedsClrHack;

		Func<Archive, bool> shouldInclude;

		public Archive (string name, string setName, Func<Archive, bool>? include = null, bool wholeArchive = false, string? jniOnLoadName = null, bool needsClrHack = false)
		{
			Name = name;
			SetName = setName;
			shouldInclude = include == null ? ((Archive arch) => true) : include;
			WholeArchive = wholeArchive;
			JniOnLoadName = jniOnLoadName;
			NeedsClrHack = needsClrHack;
		}
	}

	sealed class ClangBuiltinsArchive : Archive
	{
		public ClangBuiltinsArchive (string clangAbi)
			: base ($"libclang_rt.builtins-{clangAbi}-android.a", KnownSets.CplusPlusRuntime)
		{}
	}

	class AndroidArchive : Archive
	{
		public AndroidArchive (string name, bool wholeArchive = false)
			: base (name, KnownSets.XamarinAndroidRuntime, wholeArchive: wholeArchive)
		{}
	}

	sealed class BclArchive : Archive
	{
		public BclArchive (string name, bool wholeArchive = false, string? jniOnLoadName = null)
			: base (name, KnownSets.BCL, wholeArchive: wholeArchive, jniOnLoadName: jniOnLoadName, needsClrHack: true)
		{
			DontExportSymbols = true;
		}
	}

	sealed class ClrArchive : Archive
	{
		public ClrArchive (string name, bool wholeArchive = false)
			: base (name, KnownSets.CoreClrRuntime, wholeArchive: wholeArchive, needsClrHack: true)
		{
			DontExportSymbols = true;
		}
	}

	sealed class CplusPlusArchive : Archive
	{
		public CplusPlusArchive (string name)
			: base (name, KnownSets.CplusPlusRuntime)
		{
			DontExportSymbols = true;
		}
	}

	readonly ITaskItem[]? monoComponents;

	public readonly List<Archive> KnownArchives;
	public readonly List<string> NativeLibraries;
	public readonly List<string> LinkStartFiles;
	public readonly List<string> LinkEndFiles;

	public NativeRuntimeComponents (ITaskItem[]? monoComponents)
	{
		this.monoComponents = monoComponents;
		KnownArchives = new () {
			// CoreCLR runtime + BCL
			new ClrArchive ("libcoreclr_static.a"),
			// new ClrArchive ("libcoreclrminipal.a"),
			// new ClrArchive ("libgc_pal.a"),

			// new ClrArchive ("libcoreclrpal.a", wholeArchive: true),
			// new ClrArchive ("libeventprovider.a"),
			// new ClrArchive ("libnativeresourcestring.a"),
			// new ClrArchive ("libminipal.a")
			new ClrArchive ("libbrotlienc.a"),,
			new ClrArchive ("libbrotlidec.a"),
			new ClrArchive ("libbrotlicommon.a"),

			new BclArchive ("libSystem.Globalization.Native.a"),
			new BclArchive ("libSystem.IO.Compression.Native.a"),
			new BclArchive ("libSystem.IO.Ports.Native.a"),
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
			new AndroidArchive ("libnet-android.release-static-release.a", wholeArchive: true),
			new AndroidArchive ("libpinvoke-override-dynamic-release.a", wholeArchive: true),
			new AndroidArchive ("libruntime-base-common-release.a"),
			new AndroidArchive ("libruntime-base-release.a"),
			new AndroidArchive ("libxa-java-interop-release.a"),
			new AndroidArchive ("libxa-lz4-release.a"),
			new AndroidArchive ("libxa-shared-bits-release.a"),
			new AndroidArchive ("libxamarin-startup-release.a"),

			// C++ standard library
			new CplusPlusArchive ("libc++_static.a"),
			new CplusPlusArchive ("libc++abi.a"),

			// LLVM clang built-ins archives
			new ClangBuiltinsArchive ("aarch64"),
			new ClangBuiltinsArchive ("arm"),
			new ClangBuiltinsArchive ("i686"),
			new ClangBuiltinsArchive ("x86_64"),

			new CplusPlusArchive ("libunwind.a"), // techically it's from clang
		};

		// Just the base names of libraries to link into the unified runtime.  Must have all the dependencies of all the static archives we
		// link into the final library.
		NativeLibraries = new () {
			"c",
			"dl",
			"log",
			"m",
			"z",
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
}
