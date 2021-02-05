using System.IO;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-1")]
	[Parallelizable (ParallelScope.Children)]
	public partial class SingleProjectTest : BaseTest
	{
		[Test]
		public void AndroidManifestProperties ()
		{
			var packageName = "com.xamarin.singleproject";
			var applicationLabel = "My Sweet App";
			var versionName = "2.1";
			var versionCode = "42";
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidManifest = proj.AndroidManifest
				.Replace ("package=\"${PACKAGENAME}\"", "")
				.Replace ("android:label=\"${PROJECT_NAME}\"", "")
				.Replace ("android:versionName=\"1.0\"", "")
				.Replace ("android:versionCode=\"1\"", "");
			if (!Builder.UseDotNet) {
				proj.SetProperty ("GenerateApplicationManifest", "true");
			}
			proj.SetProperty ("ApplicationId", packageName);
			proj.SetProperty ("ApplicationTitle", applicationLabel);
			proj.SetProperty ("ApplicationVersion", versionName);
			proj.SetProperty ("AndroidVersionCode", versionCode);

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				var manifest = b.Output.GetIntermediaryPath ("android/AndroidManifest.xml");
				FileAssert.Exists (manifest);

				using (var stream = File.OpenRead (manifest)) {
					var doc = XDocument.Load (stream);
					Assert.AreEqual (packageName, doc.Root.Attribute ("package")?.Value);
					Assert.AreEqual (versionName, doc.Root.Attribute (AndroidAppManifest.AndroidXNamespace + "versionName")?.Value);
					Assert.AreEqual (versionCode, doc.Root.Attribute (AndroidAppManifest.AndroidXNamespace + "versionCode")?.Value);
					Assert.AreEqual (applicationLabel, doc.Root.Element("application").Attribute (AndroidAppManifest.AndroidXNamespace + "label")?.Value);
				}

				var apk = b.Output.GetIntermediaryPath ($"android/bin/{packageName}.apk");
				FileAssert.Exists (apk);
			}
		}
	}
}
