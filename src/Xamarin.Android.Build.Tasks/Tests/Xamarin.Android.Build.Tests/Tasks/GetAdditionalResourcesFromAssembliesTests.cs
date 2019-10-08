using System;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class GetAdditionalResourcesFromAssembliesTests : BaseTest
	{

		[Test]
		public void IsValidDownloadTest ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				PackageReferences = {
					KnownPackages.SupportCompat_24_2_1,
				},
				TargetFrameworkVersion = "v7.0",
			};
			using (var b = CreateApkBuilder (Path.Combine("temp", "IsValidDownloadTest"))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}
	}
}
