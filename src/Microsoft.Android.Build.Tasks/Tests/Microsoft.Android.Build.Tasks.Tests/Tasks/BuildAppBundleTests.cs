#nullable enable

using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class BuildAppBundleTests
	{
		[Test]
		public void BuildAppBundleCommand ()
		{
			var task = new BuildAppBundle {
				BaseZip = "base.zip",
				Output = "foo.aab",
				JarPath = "bundletool.jar",
			};
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ("-jar bundletool.jar build-bundle --modules base.zip --output foo.aab", cmd);
		}
	}
}
