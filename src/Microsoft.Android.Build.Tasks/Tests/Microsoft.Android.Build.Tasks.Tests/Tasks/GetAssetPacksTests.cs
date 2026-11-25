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

		[TestCase ("fa-IR")]
		[TestCase ("th-TH")]
		public void CacheFileIsCultureIndependent (string cultureName)
		{
			var dir = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (dir);
			var asset = Path.Combine (dir, "a.txt");
			File.WriteAllText (asset, "a");
			File.SetLastWriteTimeUtc (asset, new DateTime (2026, 11, 25, 10, 11, 12, DateTimeKind.Utc));

			var task = new GetAssetPacks {
				BuildEngine = new MockBuildEngine (TestContext.Out, []),
				IntermediateDir = new TaskItem (Path.Combine (dir, "assetpacks")),
				Assets = [Asset (asset, "My_Pack1", "OnDemand")],
			};
			var originalCulture = CultureInfo.CurrentCulture;
			try {
				CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo ("en-US");
				Assert.IsTrue (task.Execute ());
				var cacheFile = Path.Combine (dir, "assetpacks", "My_Pack1", "assetpack.cache");
				var originalContents = File.ReadAllText (cacheFile);
				File.SetLastWriteTimeUtc (cacheFile, new DateTime (2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
				var originalWriteTime = File.GetLastWriteTimeUtc (cacheFile);

				CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo (cultureName);
				Assert.IsTrue (task.Execute ());
				Assert.AreEqual (originalContents, File.ReadAllText (cacheFile));
				Assert.AreEqual (originalWriteTime, File.GetLastWriteTimeUtc (cacheFile));
			} finally {
				CultureInfo.CurrentCulture = originalCulture;
			}
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
