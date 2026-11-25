#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class GetAssetPacksTests : BaseTest
	{
		[Test]
		[SetCulture ("en-US")]
		public void GroupsAssetsByAssetPack_en_US ()
		{
			GroupsAssetsByAssetPack ();
		}

		[Test]
		[SetCulture ("fa-IR")]
		public void GroupsAssetsByAssetPack_fa_IR ()
		{
			GroupsAssetsByAssetPack ();
		}

		void GroupsAssetsByAssetPack ()
		{
			var dir = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (dir);
			var asset1 = Path.Combine (dir, "a.txt");
			var asset2 = Path.Combine (dir, "b.txt");
			File.WriteAllText (asset1, "a");
			File.WriteAllText (asset2, "b");

			var errors = new List<BuildErrorEventArgs> ();
			var task = new GetAssetPacks {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors),
				IntermediateDir = new TaskItem (Path.Combine (dir, "assetpacks")),
				Assets = [
					Asset (asset1, "My_Pack1", "OnDemand"),
					Asset (asset2, "My_Pack1", ""),
					Asset (Path.Combine (dir, "base.txt"), "base", ""),
					Asset (Path.Combine (dir, "none.txt"), "", ""),
				],
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			Assert.AreEqual (0, errors.Count);
			var packs = task.AssetPacks ?? [];
			Assert.AreEqual (1, packs.Length);
			var pack = packs [0];
			Assert.AreEqual ("My_Pack1", pack.ItemSpec);
			Assert.AreEqual ("OnDemand", pack.GetMetadata ("DeliveryType"));
			var cacheFile = pack.GetMetadata ("AssetPackCacheFile");
			Assert.AreEqual (Path.Combine (dir, "assetpacks", "My_Pack1", "assetpack.cache"), cacheFile);
			var lines = File.ReadAllLines (cacheFile);
			Assert.AreEqual (3, lines.Length);
			Assert.AreEqual ($"{asset1}:{File.GetLastWriteTimeUtc (asset1).ToString (CultureInfo.InvariantCulture)}", lines [0]);
			Assert.AreEqual ($"{asset2}:{File.GetLastWriteTimeUtc (asset2).ToString (CultureInfo.InvariantCulture)}", lines [1]);
			Assert.AreEqual ("OnDemand", lines [2]);

			File.SetLastWriteTimeUtc (cacheFile, new DateTime (2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
			var originalWriteTime = File.GetLastWriteTimeUtc (cacheFile);
			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (originalWriteTime, File.GetLastWriteTimeUtc (cacheFile));
		}

		[TestCase ("my-pack")]
		[TestCase ("my.pack")]
		[TestCase ("my_pack\u0131")]
		public void InvalidAssetPackName (string assetPack)
		{
			var dir = Path.Combine (Root, "temp", TestName);
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GetAssetPacks {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors),
				IntermediateDir = new TaskItem (Path.Combine (dir, "assetpacks")),
				Assets = [Asset (Path.Combine (dir, "a.txt"), assetPack, "")],
			};

			Assert.IsFalse (task.Execute (), "Task should fail.");
			Assert.AreEqual ("XA0140", errors.Single ().Code);
			Assert.AreEqual (0, task.AssetPacks?.Length);
		}

		[Test]
		public void AssetPackOutputsAreRegisteredBeforeIncrementalClean ()
		{
			var dir = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (dir);
			var asset = Path.Combine (dir, "asset.txt");
			File.WriteAllText (asset, "asset");
			var intermediate = Path.Combine (dir, "obj");
			var targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks",
				"MSBuild", "Xamarin", "Android", "Xamarin.Android.Assets.targets");
			var project = Path.Combine (dir, "assetpacks.proj");
			new XDocument (
				new XElement ("Project",
					new XElement ("PropertyGroup",
						new XElement ("IntermediateOutputPath", intermediate + Path.DirectorySeparatorChar),
						new XElement ("OutDir", Path.Combine (dir, "out") + Path.DirectorySeparatorChar),
						new XElement ("CleanFile", "assetpacks.FileListAbsolute.txt"),
						new XElement ("AndroidApplication", "true"),
						new XElement ("AndroidPackageFormat", "aab"),
						new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (GetAssetPacks).Assembly.Location)),
					new XElement ("Import", new XAttribute ("Project", "$(MSBuildToolsPath)/Microsoft.Common.CurrentVersion.targets")),
					new XElement ("Import", new XAttribute ("Project", targets)),
					new XElement ("PropertyGroup",
						new XElement ("BeforeBuildAndroidAssetPacks", "_CalculateAssetPacks"),
						new XElement ("IncrementalCleanDependsOn", "_CalculateAssetPacks")),
					new XElement ("ItemGroup",
						new XElement ("_AssetsWithAssetPackMetaData", new XAttribute ("Include", asset),
							new XElement ("AssetPack", "pack"))),
					new XElement ("Target", new XAttribute ("Name", "_CalculateAssetsWithAssetPackMetaData"))))
				.Save (project);

			string cache = Path.Combine (intermediate, "assetpacks", "pack", "assetpack.cache");
			string manifest = Path.Combine (intermediate, "assetpacks", "pack", "AndroidManifest.xml");
			string archive = Path.Combine (intermediate, "assetpacks", "pack.zip");
			var calculated = RunTarget ("_CalculateAssetPacks");
			CollectionAssert.AreEquivalent (new [] { cache, manifest, archive }, calculated);

			File.SetLastWriteTimeUtc (cache, DateTime.UtcNow.AddMinutes (-1));
			File.WriteAllText (archive, "archive");
			var skipped = RunTarget ("_BuildAssetPacks");
			CollectionAssert.Contains (skipped, archive, "An up-to-date pack must remain tracked.");
			Assert.IsFalse (skipped.Any (file => file.StartsWith ("$", StringComparison.Ordinal)),
				"FileWrites must not contain a literal dollar-sign prefix.");

			File.WriteAllText (manifest, "manifest");
			RunTarget ("IncrementalClean");
			RunTarget ("IncrementalClean");
			FileAssert.Exists (archive, "An unchanged pack must survive incremental clean.");
			RunTarget ("IncrementalClean", "-p:AndroidPackageFormat=apk");
			FileAssert.DoesNotExist (archive);
			FileAssert.DoesNotExist (manifest);
			FileAssert.DoesNotExist (cache);

			string [] RunTarget (string target, params string [] properties)
			{
				string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
				var arguments = new [] { "msbuild", project, $"-t:{target}", "-getItem:FileWrites", "-v:quiet", "-nr:false" }
					.Concat (properties).ToArray ();
				var result = NativeAotBootstrapTestTools.Run (dotnet, arguments);
				Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
				using var document = JsonDocument.Parse (result.Output);
				return document.RootElement.GetProperty ("Items").GetProperty ("FileWrites").EnumerateArray ()
					.Select (item => item.GetProperty ("Identity").GetString ()
						?? throw new InvalidOperationException ("FileWrites item has no identity"))
					.ToArray ();
			}
		}

		static ITaskItem Asset (string path, string assetPack, string deliveryType)
		{
			var item = new TaskItem (path);
			item.SetMetadata ("AssetPack", assetPack);
			item.SetMetadata ("DeliveryType", deliveryType);
			return item;
		}
	}
}
