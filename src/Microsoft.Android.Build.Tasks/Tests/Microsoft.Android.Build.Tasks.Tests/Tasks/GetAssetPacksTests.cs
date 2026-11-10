#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		public void GroupsAssetsByAssetPack ()
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
			StringAssert.StartsWith ($"{asset1}:", lines [0]);
			StringAssert.StartsWith ($"{asset2}:", lines [1]);
			Assert.AreEqual ("OnDemand", lines [2]);
		}

		[TestCase ("my-pack")]
		[TestCase ("my.pack")]
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

		[TestCase ("Microsoft.Android.Tasks.GetAssetPacks")]
		[TestCase ("GetAssetPacks")]
		public void RegisteredTaskNamesExecute (string taskName)
		{
			var dir = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (dir);
			var asset = Path.Combine (dir, "a.txt");
			File.WriteAllText (asset, "a");
			string assetsTargets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
				"src", "Xamarin.Android.Build.Tasks", "MSBuild", "Xamarin", "Android", "Xamarin.Android.Assets.targets");
			var registrations = XDocument.Load (assetsTargets).Descendants ()
				.Where (e => e.Name.LocalName == "UsingTask" && (string?) e.Attribute ("TaskName") == "Microsoft.Android.Tasks.GetAssetPacks")
				.Select (e => new XElement ("UsingTask", e.Attributes ()))
				.ToArray ();
			Assert.AreEqual (2, registrations.Length);
			string project = Path.Combine (dir, "assetpacks.proj");
			string output = Path.Combine (dir, "packs.txt");
			new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (GetAssetPacks).Assembly.Location)),
				new XElement ("ItemGroup",
					new XElement ("_Asset", new XAttribute ("Include", asset),
						new XElement ("AssetPack", "pack1"))),
				registrations,
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement (taskName,
						new XAttribute ("Assets", "@(_Asset)"),
						new XAttribute ("IntermediateDir", Path.Combine (dir, "assetpacks")),
						new XElement ("Output", new XAttribute ("ItemName", "_Pack"), new XAttribute ("TaskParameter", "AssetPacks"))),
					new XElement ("WriteLinesToFile",
						new XAttribute ("File", output),
						new XAttribute ("Lines", "@(_Pack)"),
						new XAttribute ("Overwrite", "true")))))
				.Save (project);

			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-nr:false");
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			CollectionAssert.AreEqual (new [] { "pack1" }, File.ReadAllLines (output));
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
