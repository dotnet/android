using System.IO;
using Android.App;
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
	}
}