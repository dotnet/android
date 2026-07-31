using System;
using System.Runtime.InteropServices;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class RuntimeInformationTest {

		// Maps a .NET RID to the ABI name Android reports in `android.os.Build.SUPPORTED_ABIS`
		static string RidToAbi (string rid) => rid switch {
			"android-arm64" => "arm64-v8a",
			"android-arm"   => "armeabi-v7a",
			"android-x64"   => "x86_64",
			"android-x86"   => "x86",
			_ => throw new NotSupportedException ($"Unexpected runtime identifier '{rid}'."),
		};

		// https://github.com/dotnet/android/issues/12273
		[Test]
		public void RuntimeIdentifierMatchesAbi ()
		{
			string rid = RuntimeInformation.RuntimeIdentifier;
			Assert.AreNotEqual ("unknown", rid, "The native runtime did not set the `RUNTIME_IDENTIFIER` property.");

			// `android.os.Process.is64Bit()` tells us the bitness this process actually runs as,
			// and `android.os.Build.SUPPORTED_{32,64}_BIT_ABIS` are ordered most-preferred-first,
			// so the first entry is the ABI Android picked for us.
			var abis = global::Android.OS.Process.Is64Bit ()
				? global::Android.OS.Build.Supported64BitAbis
				: global::Android.OS.Build.Supported32BitAbis;
			// `Assert.IsNotEmpty()` reads `.Count` through reflection, which NativeAOT trims away.
			if (abis == null || abis.Count == 0) {
				Assert.Fail ("`android.os.Build` did not report any supported ABIs.");
				return;
			}
			Assert.AreEqual (abis [0], RidToAbi (rid), $"`RuntimeInformation.RuntimeIdentifier` was '{rid}'.");
		}
	}
}
