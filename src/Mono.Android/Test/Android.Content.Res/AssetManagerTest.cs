using System.IO;
using Android.App;
using Android.Graphics.Drawables;
using Android.Widget;
using NUnit.Framework;

namespace Android.Content.ResTests
{
	[TestFixture]
	public class AssetManagerTests
	{
		static object [] AssetContentsSource = new object [] {
			new object[] {
				/* asset */    "asset1.txt",
				/* expected */ "Asset1",
			},
			new object[] {
				/* asset */    "subfolder/asset2.txt",
				/* expected */ "Asset2",
			},
			new object[] {
				/* asset */    "linked_text.txt",
				/* expected */ "this is a test from a linked file"
			},
			new object[] {
				/* asset */    "linked_text2.txt",
				/* expected */ "this is a test from a linked file in a library"
			},
			new object[] {
				/* asset */    "LibAssetSubFolder/LibrarySubFolderText.txt",
				/* expected */ "this is a test from a library sub folder"
			}
		};

		string[] AssetPngs = {
			"hamburger.png",
			"subfolder/accept_request.png",
			"problem_solving.png",
			"LibAssetSubFolder/folder.png"
		};

		[Test]
		[TestCaseSource (nameof (AssetContentsSource))]
		public void AssetContents (string asset, string expected)
		{
			using (var s = Application.Context.Assets.Open (asset))
			using (var r = new StreamReader (s)) {
				var actual = r.ReadToEnd ();
				Assert.AreEqual (expected, actual, $"Asset `{asset}` did not have expected contents!");
			}
		}

		[Test]
		[TestCaseSource (nameof (AssetPngs))]
		public void ConstructDrawableFromPngAsset (string assetPath)
		{
			using (var s = Application.Context.Assets.Open (assetPath)) {
				var d = Drawable.CreateFromStream (s, assetPath);
				Assert.IsTrue (d.IntrinsicHeight > 0,
					$"Height ({d.IntrinsicHeight}) of drawable created from asset `{assetPath}` was not greater than 0!");
			}
		}
	}
}