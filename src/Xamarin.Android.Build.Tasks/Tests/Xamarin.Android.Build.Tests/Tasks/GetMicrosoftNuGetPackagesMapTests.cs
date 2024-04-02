#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	public class GetMicrosoftNuGetPackagesMapTests
	{
		[Test]
		public async Task NoCachedFile ()
		{
			var engine = new MockBuildEngine (TestContext.Out, []);
			var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
			var today_file = Path.Combine (temp_cache_dir, $"microsoft-packages-{DateTime.Today:yyyyMMdd}.json");

			try {
				var task = new GetMicrosoftNuGetPackagesMap {
					BuildEngine = engine,
					MavenCacheDirectory = temp_cache_dir,
				};

				await task.RunTaskAsync ();

				Assert.AreEqual (0, engine.Errors.Count);
				Assert.AreEqual (today_file, task.ResolvedPackageMap);
				Assert.IsTrue (File.Exists (today_file));

			} finally {
				MavenDownloadTests.DeleteTempDirectory (temp_cache_dir);
			}
		}

		[Test]
		public async Task CachedTodayFile ()
		{
			// If a file already exists for today, it should be used and nothing new should be downloaded
			var engine = new MockBuildEngine (TestContext.Out, []);
			var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
			var today_file = Path.Combine (temp_cache_dir, $"microsoft-packages-{DateTime.Today:yyyyMMdd}.json");

			try {
				Directory.CreateDirectory (temp_cache_dir);
				File.WriteAllText (today_file, "dummy file");

				var task = new GetMicrosoftNuGetPackagesMap {
					BuildEngine = engine,
					MavenCacheDirectory = temp_cache_dir,
				};

				await task.RunTaskAsync ();

				Assert.AreEqual (0, engine.Errors.Count);
				Assert.AreEqual (today_file, task.ResolvedPackageMap);
				Assert.IsTrue (File.Exists (today_file));

				// Ensure file didn't change
				var text = File.ReadAllText (today_file);
				Assert.AreEqual ("dummy file", text);

			} finally {
				MavenDownloadTests.DeleteTempDirectory (temp_cache_dir);
			}
		}

		[Test]
		public async Task CachedYesterdayFile ()
		{
			// If a file only exists for yesterday, a new one should be downloaded and the old one should be deleted
			var engine = new MockBuildEngine (TestContext.Out, []);
			var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
			var yesterday_file = Path.Combine (temp_cache_dir, $"microsoft-packages-{DateTime.Today.AddDays (-1):yyyyMMdd}.json");
			var today_file = Path.Combine (temp_cache_dir, $"microsoft-packages-{DateTime.Today:yyyyMMdd}.json");

			try {
				Directory.CreateDirectory (temp_cache_dir);
				File.WriteAllText (yesterday_file, "dummy file");

				var task = new GetMicrosoftNuGetPackagesMap {
					BuildEngine = engine,
					MavenCacheDirectory = temp_cache_dir,
				};

				await task.RunTaskAsync ();

				Assert.AreEqual (0, engine.Errors.Count);
				Assert.AreEqual (today_file, task.ResolvedPackageMap);
				Assert.IsFalse (File.Exists (yesterday_file));

			} finally {
				MavenDownloadTests.DeleteTempDirectory (temp_cache_dir);
			}
		}

		[Test]
		public async Task MalformedFileName ()
		{
			// Make sure a malformed file name doesn't cause an exception, a new file should be downloaded and returned
			var engine = new MockBuildEngine (TestContext.Out, []);
			var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
			var malformed_file = Path.Combine (temp_cache_dir, $"microsoft-packages-dummy.json");
			var today_file = Path.Combine (temp_cache_dir, $"microsoft-packages-{DateTime.Today:yyyyMMdd}.json");

			try {
				Directory.CreateDirectory (temp_cache_dir);
				File.WriteAllText (malformed_file, "dummy file");

				var task = new GetMicrosoftNuGetPackagesMap {
					BuildEngine = engine,
					MavenCacheDirectory = temp_cache_dir,
				};

				await task.RunTaskAsync ();

				Assert.AreEqual (0, engine.Errors.Count);
				Assert.AreEqual (today_file, task.ResolvedPackageMap);
				Assert.IsTrue (File.Exists (today_file));

			} finally {
				MavenDownloadTests.DeleteTempDirectory (temp_cache_dir);
			}
		}

		// This test can only be run manually, since it requires changing the URL to a non-existent one.
		// But I wanted to ensure I had tested this scenario.
		//[Test]
		public async Task CachedYesterdayFile_NewFileFails ()
		{
			// If a file only exists for yesterday, but we fail to download a new file today, return
			// the old file and don't delete it
			var engine = new MockBuildEngine (TestContext.Out, []);
			var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
			var yesterday_file = Path.Combine (temp_cache_dir, $"microsoft-packages-{DateTime.Today.AddDays (-1):yyyyMMdd}.json");

			try {
				Directory.CreateDirectory (temp_cache_dir);
				File.WriteAllText (yesterday_file, "dummy file");

				var task = new GetMicrosoftNuGetPackagesMap {
					BuildEngine = engine,
					MavenCacheDirectory = temp_cache_dir,
				};

				await task.RunTaskAsync ();

				Assert.AreEqual (0, engine.Errors.Count);
				Assert.AreEqual (yesterday_file, task.ResolvedPackageMap);
				Assert.IsTrue (File.Exists (yesterday_file));

			} finally {
				MavenDownloadTests.DeleteTempDirectory (temp_cache_dir);
			}
		}
	}
}
