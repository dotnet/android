using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class R8RuntimeRemappingBuildTests : BaseTest
	{
		[TestCase (AndroidRuntime.CoreCLR, false, false)]
		[TestCase (AndroidRuntime.CoreCLR, true, true)]
		[TestCase (AndroidRuntime.NativeAOT, true, false)]
		public void OrdinaryBuildPackagesBinaryRemapping (AndroidRuntime runtime, bool release, bool aab)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = release,
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a", "x86_64" });
			proj.SetProperty ("AndroidPackageFormats", aab ? "aab" : "apk");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj));
			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var blobs = Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories)
				.ToDictionary (path => path, File.GetLastWriteTimeUtc);
			var abis = release ? new [] { "arm64-v8a", "x86_64" } : new [] { "arm64-v8a" };
			Assert.AreEqual (abis.Length, blobs.Count, "Every built ABI needs a remapping data library even without R8 or XML remaps.");
			var archivePath = aab
				? Path.Combine (intermediate, "android", "bin", $"{proj.PackageName}.aab")
				: Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
			using (var archive = ZipFile.OpenRead (archivePath)) {
				foreach (var abi in abis)
					Assert.IsNotNull (archive.GetEntry ($"{(aab ? "base/" : "")}lib/{abi}/libbinary_blobs.so"));
			}
			Assert.IsTrue (builder.Build (proj), "A no-op build should preserve the data libraries.");
			foreach (var blob in blobs)
				Assert.AreEqual (blob.Value, File.GetLastWriteTimeUtc (blob.Key));
			var missing = blobs.Keys.Single (path => path.Contains ("arm64-v8a", StringComparison.Ordinal));
			File.Delete (missing);
			Assert.IsTrue (builder.Build (proj), "A deleted data library must be recreated.");
			FileAssert.Exists (missing);
			Assert.IsTrue (builder.Clean (proj));
			foreach (var blob in blobs.Keys)
				FileAssert.DoesNotExist (blob);
		}

		[TestCase (true)]
		[TestCase (false)]
		public void UnchangedProguardRulesDoNotRerunR8 (bool obfuscation)
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new AndroidItem.AndroidJavaSource ("Peer.java") {
						Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
						Metadata = { { "Bind", "True" } },
						TextContent = () => """
							package example;
							public class Peer {
								public int first () { return 1; }
								public int second () { return 2; }
							}
							""",
					},
				},
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a" });
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("AndroidR8ObfuscationMode", obfuscation ? "runtime-remapping" : "disabled");
			proj.SetProperty ("AndroidPackageFormats", "apk");
			proj.SetProperty ("TrimMode", "full");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", """
				using var peer = new Example.Peer ();
				System.Console.WriteLine (peer.First ());
				""");

			using var builder = CreateApkBuilder ();
			void AssertTaskCount (string task, int expected)
			{
				var build = BinaryLog.ReadBuild (Path.Combine (Root, builder.ProjectDirectory,
					$"{Path.GetFileNameWithoutExtension (builder.BuildLogFile)}.binlog"));
				Assert.AreEqual (expected, build.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Task> ()
					.Count (t => t.Name == task), $"Unexpected {task} invocation count.");
			}

			Assert.IsTrue (builder.Build (proj));
			AssertTaskCount ("R8", 1);
			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var rules = Directory.GetFiles (intermediate, "proguard_project_references.cfg", SearchOption.AllDirectories).Single ();
			var originalRules = File.ReadAllText (rules);
			var originalTime = File.GetLastWriteTimeUtc (rules);
			string? memberRules = null;
			string? originalMemberRules = null;
			DateTime originalMemberTime = default;
			if (obfuscation) {
				StringAssert.Contains ("first(...)", originalRules);
				FileAssert.Exists (rules + ".stamp");
			} else {
				StringAssert.Contains ("-keep class example.Peer", originalRules);
				StringAssert.DoesNotContain ("first(...)", originalRules);
				memberRules = Directory.GetFiles (intermediate, "proguard_typemap_members.cfg", SearchOption.AllDirectories).Single ();
				originalMemberRules = File.ReadAllText (memberRules);
				originalMemberTime = File.GetLastWriteTimeUtc (memberRules);
				StringAssert.Contains ("-keepclassmembers class example.Peer { *; }", originalMemberRules);
			}

			proj.MainActivity = proj.MainActivity.Replace ("peer.First ()", "peer.First () + 1");
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj), "A managed-only change should follow the active ProGuard pipeline's incrementality.");
			AssertTaskCount ("Csc", 1);
			AssertTaskCount ("GenerateProguardConfiguration", obfuscation ? 1 : 0);
			AssertTaskCount ("GenerateTypeMapProguardConfiguration", obfuscation ? 0 : 1);
			AssertTaskCount ("GenerateTypeMapMemberProguardConfiguration", obfuscation ? 0 : 1);
			AssertTaskCount ("R8", obfuscation ? 0 : 1);
			Assert.AreEqual (originalRules, File.ReadAllText (rules));
			if (obfuscation) {
				Assert.AreEqual (originalTime, File.GetLastWriteTimeUtc (rules));
			} else {
				Assert.Greater (File.GetLastWriteTimeUtc (rules), originalTime);
			}
			if (memberRules != null) {
				Assert.AreEqual (originalMemberRules, File.ReadAllText (memberRules));
				Assert.Greater (File.GetLastWriteTimeUtc (memberRules), originalMemberTime);
				originalMemberTime = File.GetLastWriteTimeUtc (memberRules);
			}

			Assert.IsTrue (builder.Build (proj));
			AssertTaskCount ("GenerateProguardConfiguration", 0);
			AssertTaskCount ("GenerateTypeMapProguardConfiguration", 0);
			AssertTaskCount ("GenerateTypeMapMemberProguardConfiguration", 0);
			AssertTaskCount ("R8", 0);

			File.Delete (rules);
			Assert.IsTrue (builder.Build (proj), "A missing rule file must be restored even when the stamp exists.");
			AssertTaskCount ("GenerateProguardConfiguration", obfuscation ? 1 : 0);
			AssertTaskCount ("GenerateTypeMapProguardConfiguration", obfuscation ? 0 : 1);
			AssertTaskCount ("GenerateTypeMapMemberProguardConfiguration", 0);
			AssertTaskCount ("R8", 1);
			Assert.AreEqual (originalRules, File.ReadAllText (rules));

			proj.MainActivity = proj.MainActivity.Replace ("peer.First () + 1", "peer.Second ()");
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj), "Newly retained bindings must update the keep rules.");
			if (obfuscation) {
				AssertTaskCount ("GenerateProguardConfiguration", 1);
				StringAssert.Contains ("second(...)", File.ReadAllText (rules));
				Assert.AreNotEqual (originalRules, File.ReadAllText (rules));
				AssertTaskCount ("R8", 1);
			} else {
				AssertTaskCount ("GenerateProguardConfiguration", 0);
				AssertTaskCount ("GenerateTypeMapProguardConfiguration", 1);
				AssertTaskCount ("GenerateTypeMapMemberProguardConfiguration", 1);
				AssertTaskCount ("R8", 1);
				Assert.AreEqual (originalRules, File.ReadAllText (rules));
				Assert.IsNotNull (memberRules);
				if (memberRules == null) {
					throw new AssertionException ("Scoped member rules were not generated.");
				}
				Assert.AreEqual (originalMemberRules, File.ReadAllText (memberRules));
				Assert.Greater (File.GetLastWriteTimeUtc (memberRules), originalMemberTime);
			}

			Assert.IsTrue (builder.Clean (proj));
			Assert.IsFalse (File.Exists (rules), "Clean should remove the rules.");
			Assert.IsFalse (File.Exists (rules + ".stamp"), "Clean should remove the legacy generation stamp.");
			if (memberRules != null) {
				Assert.IsFalse (File.Exists (memberRules), "Clean should remove the scoped member rules.");
			}
		}

		[TestCase (AndroidRuntime.CoreCLR, false, false)]
		[TestCase (AndroidRuntime.CoreCLR, true, false)]
		[TestCase (AndroidRuntime.NativeAOT, false, false)]
		[TestCase (AndroidRuntime.NativeAOT, true, false)]
		[TestCase (AndroidRuntime.CoreCLR, false, true)]
		[TestCase (AndroidRuntime.NativeAOT, false, true)]
		public void MultiRidUsesOneR8Mapping (AndroidRuntime runtime, bool explicitPrimaryRid, bool aab)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a", "x86_64" });
			if (explicitPrimaryRid) {
				proj.SetProperty ("RuntimeIdentifier", "android-arm64");
			}
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("AndroidR8ObfuscationMode", "runtime-remapping");
			proj.SetProperty ("AndroidCreateProguardMappingFile", "false");
			proj.SetProperty ("AndroidPackageFormats", aab ? "aab" : "apk");

			using var builder = CreateApkBuilder ();
			(int R8, int NativeLinks) ReadInvocationCounts ()
			{
				var build = BinaryLog.ReadBuild (Path.Combine (Root, builder.ProjectDirectory,
					$"{Path.GetFileNameWithoutExtension (builder.BuildLogFile)}.binlog"));
				var tasks = build.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Task> ().ToList ();
				return (tasks.Count (t => t.Name == "R8"), tasks.Count (t => t.Name == "LinkNativeAotSharedLibrary"));
			}

			Assert.IsTrue (builder.Build (proj), "Both RIDs should build from the same final R8 mapping.");
			var first = ReadInvocationCounts ();
			Assert.AreEqual (1, first.R8);
			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var maps = Directory.GetFiles (intermediate, "r8-jni-final-mapping.txt", SearchOption.AllDirectories);
			Assert.AreEqual (1, maps.Length, "R8 output must be shared, not regenerated for each RID.");
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.AreEqual (2, first.NativeLinks, "Each RID should link once, after R8.");
				Assert.AreEqual (2, Directory.GetFiles (intermediate, "r8-jni-remap.xml", SearchOption.AllDirectories).Length);
			}
			var binaryBlobs = Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories)
				.ToDictionary (path => path, File.GetLastWriteTimeUtc);
			Assert.AreEqual (2, binaryBlobs.Count, "Each ABI needs its own independent binary-blob library.");
			var archivePath = aab
				? Path.Combine (intermediate, "android", "bin", $"{proj.PackageName}.aab")
				: Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
			using (var archive = ZipFile.OpenRead (archivePath)) {
				foreach (var abi in new [] { "arm64-v8a", "x86_64" }) {
					string prefix = aab ? "base/lib/" : "lib/";
					Assert.IsNotNull (archive.GetEntry ($"{prefix}{abi}/libbinary_blobs.so"), $"Missing {abi} remapping data.");
					if (runtime == AndroidRuntime.NativeAOT)
						Assert.IsNotNull (archive.GetEntry ($"{prefix}{abi}/lib{proj.ProjectName}.so"), $"Missing final {abi} native library.");
				}
			}
			var objects = Directory.GetFiles (intermediate, $"{proj.ProjectName}.o", SearchOption.AllDirectories)
				.ToDictionary (path => path, File.GetLastWriteTimeUtc);
			Assert.IsTrue (builder.Build (proj), "A multi-RID no-op build should succeed.");
			Assert.AreEqual ((0, 0), ReadInvocationCounts (), "No-op builds must not run R8 or native linking.");
			foreach (var entry in binaryBlobs)
				Assert.AreEqual (entry.Value, File.GetLastWriteTimeUtc (entry.Key), "No-op builds must retain binary-blob timestamps.");
			foreach (var entry in objects) {
				Assert.AreEqual (entry.Value, File.GetLastWriteTimeUtc (entry.Key), "No-op builds must not recompile ILC.");
			}
			var missingBlob = binaryBlobs.Keys.Single (path => path.Contains ("arm64-v8a", StringComparison.Ordinal));
			File.Delete (missingBlob);
			Assert.IsTrue (builder.Build (proj), "A missing ABI blob should be regenerated.");
			Assert.AreEqual ((0, 0), ReadInvocationCounts (), "Restoring data must not rerun R8 or relink NativeAOT.");
			FileAssert.Exists (missingBlob);
			foreach (var entry in objects)
				Assert.AreEqual (entry.Value, File.GetLastWriteTimeUtc (entry.Key), "Restoring data must not recompile ILC.");
			Assert.IsTrue (builder.Clean (proj));
			foreach (var blob in binaryBlobs.Keys)
				FileAssert.DoesNotExist (blob, "Clean must remove generated binary-blob libraries.");
		}
	}
}
