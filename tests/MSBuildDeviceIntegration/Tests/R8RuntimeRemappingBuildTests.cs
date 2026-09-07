using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class R8RuntimeRemappingBuildTests : BaseTest
	{
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
