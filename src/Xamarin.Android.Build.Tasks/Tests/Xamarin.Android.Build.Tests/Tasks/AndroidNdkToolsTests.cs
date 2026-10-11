using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks {

	[TestFixture]
	public class AndroidNdkToolsTests : BaseTest {

		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out, errors = new List<BuildErrorEventArgs> (), warnings = new List<BuildWarningEventArgs> ());
		}

		[Test]
		public void OfficialNdkToolsAndRuntimeLibraries ()
		{
			var log = new TaskLoggingHelper (engine, TestName);
			var ndk = AndroidNdkTools.Create (AndroidNdkPath, log) ?? throw new InvalidOperationException ("The configured NDK must be available.");
			string expectedToolchain = Path.GetFullPath (Path.Combine (AndroidNdkPath, "toolchains", "llvm", "prebuilt", AndroidNdkTools.HostTag));
			Assert.AreEqual (expectedToolchain, ndk.ToolchainDirectory);
			foreach (string tool in new [] { "clang", "clang++", "ld.lld", "llvm-objcopy", "llvm-strip" }) {
				string expected = Path.Combine (expectedToolchain, "bin", tool + (IsWindows ? ".exe" : ""));
				Assert.AreEqual (expected, ndk.GetToolPath (tool), "Tools must come from the official NDK.");
				FileAssert.Exists (expected);
			}
			string runtime = ndk.GetClangDeviceLibraryPath () ?? throw new InvalidOperationException ("Clang runtime libraries must be available.");
			foreach (string library in new [] { "libclang_rt.builtins-aarch64-android.a", "libclang_rt.asan-aarch64-android.so", "libclang_rt.ubsan_standalone-aarch64-android.so" }) {
				FileAssert.Exists (Path.Combine (runtime, library));
			}
			Assert.IsEmpty (errors);
		}

		[TestCase (false, "XA5104")]
		[TestCase (true, "XA5101")]
		public void MissingNdkOrHostToolchain (bool hasVersion, string errorCode)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			if (hasVersion) {
				File.WriteAllText (Path.Combine (directory, "source.properties"), "Pkg.Revision = 27.2.12479018");
			}
			Assert.IsNull (AndroidNdkTools.Create (directory, new TaskLoggingHelper (engine, TestName)));
			Assert.AreEqual (errorCode, errors.Single ().Code);
		}

		[Test]
		public void MissingToolDoesNotFallBack ()
		{
			var ndk = CreateNdk ();
			Assert.IsNull (ndk.GetToolPath ("llvm-strip"));
			Assert.AreEqual ("XA5105", errors.Single ().Code);
		}

		[TestCase ("lib")]
		[TestCase ("lib64")]
		public void ClangRuntimeUsesNewestVersion (string libDirectory)
		{
			var ndk = CreateNdk ();
			string root = Path.Combine (ndk.ToolchainDirectory, libDirectory, "clang");
			foreach (string version in new [] { "9.0.9", "19", "19.0", "20" }) {
				Directory.CreateDirectory (Path.Combine (root, version));
				if (version != "20") {
					Directory.CreateDirectory (Path.Combine (root, version, "lib", "linux"));
				}
			}
			Assert.AreEqual (Path.Combine (root, "19.0", "lib", "linux"), ndk.GetClangDeviceLibraryPath ());
			Assert.IsEmpty (errors);
		}

		[Test]
		public void MissingClangRuntimeIsDiagnosed ()
		{
			var ndk = CreateNdk ();
			Assert.IsNull (ndk.GetClangDeviceLibraryPath ());
			Assert.AreEqual ("XA5101", errors.Single ().Code);
		}

		[TestCase (false, "AndroidIncludeWrapSh")]
		[TestCase (true, "Please add `wrap.sh`")]
		public void CheckedBuildRequiresWrapper (bool includeWrapSh, string diagnostic)
		{
			var task = new CollectNativeFilesForArchive {
				BuildEngine = engine,
				ApkOutputPath = Path.Combine (Root, "temp", TestName, "test.apk"),
				IntermediateOutputPath = Path.Combine (Root, "temp", TestName, "obj"),
				SupportedAbis = ["arm64-v8a"],
				CheckedBuild = "asan",
				IncludeWrapSh = includeWrapSh,
			};
			Assert.IsFalse (task.Execute ());
			StringAssert.Contains (diagnostic, errors.Single ().Message);
			Assert.IsEmpty (task.FilesToAddToArchive);
		}

		AndroidNdkTools CreateNdk ()
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (Path.Combine (directory, "toolchains", "llvm", "prebuilt", AndroidNdkTools.HostTag, "bin"));
			File.WriteAllText (Path.Combine (directory, "source.properties"), "Pkg.Revision = 27.2.12479018");
			return AndroidNdkTools.Create (directory, new TaskLoggingHelper (engine, TestName)) ??
				throw new InvalidOperationException ("The test NDK must have a host toolchain.");
		}
	}
}
