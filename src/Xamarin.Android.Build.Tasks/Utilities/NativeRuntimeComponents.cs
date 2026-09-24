#nullable enable
using System.Collections.Generic;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Static archives of the native runtime components, used by NativeAOT builds to find
/// the JNI initialization functions of BCL components used by the application.
/// </summary>
class NativeRuntimeComponents
{
	internal sealed class Archive
	{
		public readonly string Name;
		public readonly string? JniOnLoadName;

		public Archive (string name, string? jniOnLoadName = null)
		{
			Name = name;
			JniOnLoadName = jniOnLoadName;
		}
	}

	public readonly List<Archive> KnownArchives;

	public NativeRuntimeComponents ()
	{
		KnownArchives = new () {
			// CoreCLR runtime + BCL
			new Archive ("libcoreclr_static.a"),
			new Archive ("libbrotlienc.a"),
			new Archive ("libbrotlidec.a"),
			new Archive ("libbrotlicommon.a"),

			new Archive ("libSystem.Globalization.Native.a"),
			new Archive ("libSystem.IO.Compression.Native.a"),
			new Archive ("libSystem.Native.a"),
			new Archive ("libSystem.Security.Cryptography.Native.Android.a", jniOnLoadName: "AndroidCryptoNative_InitLibraryOnLoad"),

			// .NET for Android
			new Archive ("libnet-android.release-static-release.a"),
			new Archive ("libruntime-base-common-release.a"),
			new Archive ("libruntime-base-release.a"),
			new Archive ("libxa-java-interop-release.a"),
			new Archive ("libxa-shared-bits-release.a"),
			new Archive ("libxamarin-startup-release.a"),

			// LLVM clang built-ins archives
			new Archive ("libclang_rt.builtins-aarch64-android.a"),
			new Archive ("libclang_rt.builtins-arm-android.a"),
			new Archive ("libclang_rt.builtins-i686-android.a"),
			new Archive ("libclang_rt.builtins-x86_64-android.a"),
		};
	}
}
