#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

		static ITaskItem Asset (string path, string assetPack, string deliveryType)
		{
			var item = new TaskItem (path);
			item.SetMetadata ("AssetPack", assetPack);
			item.SetMetadata ("DeliveryType", deliveryType);
			return item;
		}
	}
}
