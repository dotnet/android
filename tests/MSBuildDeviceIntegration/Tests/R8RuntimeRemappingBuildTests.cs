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
		[TestCase (true, "trimmable")]
		[TestCase (false, "trimmable")]
		[TestCase (false, "llvm-ir")]
		public void UnchangedProguardRulesDoNotRerunR8 (bool obfuscation, string typeMap)
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
			proj.SetProperty ("AndroidTypeMapImplementation", typeMap);
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("AndroidEnableR8Obfuscation", obfuscation.ToString ());
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
			StringAssert.Contains ("first(...)", originalRules);
			FileAssert.Exists (rules + ".stamp");

			proj.MainActivity = proj.MainActivity.Replace ("peer.First ()", "peer.First () + 1");
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj), "A managed-only change should rebuild without running R8.");
			AssertTaskCount ("Csc", 1);
			AssertTaskCount ("GenerateProguardConfiguration", 1);
			AssertTaskCount ("R8", 0);
			Assert.AreEqual (originalRules, File.ReadAllText (rules));
			Assert.AreEqual (originalTime, File.GetLastWriteTimeUtc (rules));

			Assert.IsTrue (builder.Build (proj));
			AssertTaskCount ("GenerateProguardConfiguration", 0);
			AssertTaskCount ("R8", 0);

			File.Delete (rules);
			Assert.IsTrue (builder.Build (proj), "A missing rule file must be restored even when the stamp exists.");
			AssertTaskCount ("GenerateProguardConfiguration", 1);
			AssertTaskCount ("R8", obfuscation ? 1 : 0);
			Assert.AreEqual (originalRules, File.ReadAllText (rules));

			proj.MainActivity = proj.MainActivity.Replace ("peer.First () + 1", "peer.Second ()");
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj), "Newly retained bindings must update the keep rules.");
			AssertTaskCount ("GenerateProguardConfiguration", 1);
			StringAssert.Contains ("second(...)", File.ReadAllText (rules));
			if (typeMap == "trimmable") {
				Assert.AreNotEqual (originalRules, File.ReadAllText (rules));
			} else {
				Assert.AreEqual (originalRules, File.ReadAllText (rules), "LLVM typemaps already retain both bound methods.");
			}
			if (obfuscation) {
				AssertTaskCount ("R8", 1);
			}

			Assert.IsTrue (builder.Clean (proj));
			Assert.IsFalse (File.Exists (rules), "Clean should remove the rules.");
			Assert.IsFalse (File.Exists (rules + ".stamp"), "Clean should remove the generation stamp.");
		}

		[TestCase (AndroidRuntime.CoreCLR, false)]
		[TestCase (AndroidRuntime.NativeAOT, false)]
		[TestCase (AndroidRuntime.NativeAOT, true)]
		public void MultiRidUsesOneR8Mapping (AndroidRuntime runtime, bool explicitPrimaryRid)
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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("AndroidEnableR8Obfuscation", "true");
			proj.SetProperty ("AndroidCreateProguardMappingFile", "false");
			proj.SetProperty ("AndroidPackageFormats", "apk");

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
				using var apk = ZipFile.OpenRead (Path.Combine (Root, builder.ProjectDirectory,
					proj.OutputPath, $"{proj.PackageName}-Signed.apk"));
				foreach (var abi in new [] { "arm64-v8a", "x86_64" }) {
					Assert.IsNotNull (apk.GetEntry ($"lib/{abi}/lib{proj.ProjectName}.so"), $"Missing final {abi} native library.");
				}
			}
			var objects = Directory.GetFiles (intermediate, $"{proj.ProjectName}.o", SearchOption.AllDirectories)
				.ToDictionary (path => path, File.GetLastWriteTimeUtc);
			Assert.IsTrue (builder.Build (proj), "A multi-RID no-op build should succeed.");
			Assert.AreEqual ((0, 0), ReadInvocationCounts (), "No-op builds must not run R8 or native linking.");
			foreach (var entry in objects) {
				Assert.AreEqual (entry.Value, File.GetLastWriteTimeUtc (entry.Key), "No-op builds must not recompile ILC.");
			}
		}
	}
}
