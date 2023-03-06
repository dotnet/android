using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class FilterAssembliesTests : BaseTest
	{
		string tempDirectory;

		[SetUp]
		public void Setup ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (tempDirectory, recursive: true);
		}

		Task<string> DownloadFromNuGet (string url, string filename = "") =>
			Task.Factory.StartNew (() => new DownloadedCache ().GetAsFile (url, filename));

		async Task<string []> GetAssembliesFromNuGet (string url, string filename, string path)
		{
			var assemblies = new List<string> ();
			var nuget = await DownloadFromNuGet (url, filename);
			using (var zip = ZipArchive.Open (nuget, FileMode.Open)) {
				foreach (var entry in zip) {
					if (entry.FullName.StartsWith (path, StringComparison.OrdinalIgnoreCase) &&
						entry.FullName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
						var temp = Path.Combine (tempDirectory, Path.GetFileName (entry.NativeFullName));
						assemblies.Add (temp);
						using (var fileStream = File.Create (temp)) {
							entry.Extract (fileStream);
						}
					}
				}
			}
			return assemblies.ToArray ();
		}

		string [] Run (params string [] assemblies)
		{
			var task = new FilterAssemblies {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				InputAssemblies = assemblies.Select (a => new TaskItem (a)).ToArray (),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			return task.OutputAssemblies.Select (a => Path.GetFileName (a.ItemSpec)).ToArray ();
		}

		[Test]
		public async Task CircleImageView ()
		{
			var assemblies = await GetAssembliesFromNuGet (
				"https://www.nuget.org/api/v2/package/Refractored.Controls.CircleImageView/1.0.1",
				"Refractored.Controls.CircleImageView.1.0.1.nupkg",
				"lib/MonoAndroid10/");
			var actual = Run (assemblies);
			var expected = new [] { "Refractored.Controls.CircleImageView.dll" };
			CollectionAssert.AreEqual (expected, actual);
		}

		[Test]
		public async Task XamarinForms ()
		{
			var assemblies = await GetAssembliesFromNuGet (
				"https://www.nuget.org/api/v2/package/Xamarin.Forms/3.6.0.220655",
				"Xamarin.Forms.3.6.0.220655.nupkg",
				"lib/MonoAndroid90/");
			var actual = Run (assemblies);
			var expected = new [] {
				"FormsViewGroup.dll",
				"Xamarin.Forms.Platform.Android.dll",
				"Xamarin.Forms.Platform.dll",
			};
			CollectionAssert.AreEqual (expected, actual);
		}

		[Test]
		public async Task GuavaListenableFuture ()
		{
			var assemblies = await GetAssembliesFromNuGet (
				"https://www.nuget.org/api/v2/package/Xamarin.Google.Guava.ListenableFuture/1.0.0",
				"Xamarin.Google.Guava.ListenableFuture.1.0.0.nupkg",
				"lib/MonoAndroid50/");
			var actual = Run (assemblies);
			var expected = new [] { "Xamarin.Google.Guava.ListenableFuture.dll" };
			CollectionAssert.AreEqual (expected, actual);
		}
	}
}
