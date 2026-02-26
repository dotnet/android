// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class JdkInstallerTests
	{
		JdkInstaller installer;

		[SetUp]
		public void SetUp ()
		{
			installer = new JdkInstaller (logger: (level, message) => {
				TestContext.WriteLine ($"[{level}] {message}");
			});
		}

		[TearDown]
		public void TearDown ()
		{
			installer?.Dispose ();
			installer = null!;
		}

		[Test]
		public void IsValid_NullPath_ReturnsFalse ()
		{
			Assert.IsFalse (installer.IsValid (null!));
		}

		[Test]
		public void IsValid_EmptyPath_ReturnsFalse ()
		{
			Assert.IsFalse (installer.IsValid (""));
		}

		[Test]
		public void IsValid_NonExistentPath_ReturnsFalse ()
		{
			Assert.IsFalse (installer.IsValid (Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ())));
		}

		[Test]
		public void IsValid_EmptyDirectory_ReturnsFalse ()
		{
			var dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
			Directory.CreateDirectory (dir);
			try {
				Assert.IsFalse (installer.IsValid (dir));
			}
			finally {
				Directory.Delete (dir, recursive: true);
			}
		}

		[Test]
		public void IsValid_FauxJdk_ReturnsTrue ()
		{
			var dir = Path.Combine (Path.GetTempPath (), $"jdk-test-{Guid.NewGuid ()}");
			try {
				JdkInfoTests.CreateFauxJdk (dir, releaseVersion: "17.0.1", releaseBuildNumber: "1", javaVersion: "17.0.1-1");
				Assert.IsTrue (installer.IsValid (dir));
			}
			finally {
				if (Directory.Exists (dir))
					Directory.Delete (dir, recursive: true);
			}
		}

		[Test]
		public void IsValid_SystemJdk_ReturnsTrue ()
		{
			// Find first known JDK on the system
			var jdk = JdkInfo.GetKnownSystemJdkInfos ().FirstOrDefault ();
			if (jdk is null) {
				Assert.Ignore ("No system JDK found to validate.");
				return;
			}
			Assert.IsTrue (installer.IsValid (jdk.HomePath));
		}

		[Test]
		public async Task DiscoverAsync_ReturnsVersions ()
		{
			IReadOnlyList<JdkVersionInfo> versions;
			try {
				using (var cts = new CancellationTokenSource (TimeSpan.FromSeconds (15))) {
					versions = await installer.DiscoverAsync (cts.Token);
				}
			}
			catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException) {
				Assert.Ignore ($"Network unavailable: {ex.Message}");
				return;
			}

			// We should get at least one version (if network is available)
			Assert.IsNotNull (versions);
			if (versions.Count == 0) {
				Assert.Ignore ("No versions returned (network may be restricted).");
				return;
			}

			// Verify structure of returned info
			foreach (var v in versions) {
				Assert.Greater (v.MajorVersion, 0, "MajorVersion should be positive");
				Assert.IsNotEmpty (v.DisplayName, "DisplayName should not be empty");
				Assert.IsNotEmpty (v.DownloadUrl, "DownloadUrl should not be empty");
				Assert.That (v.DownloadUrl, Does.Contain ("aka.ms/download-jdk"), "DownloadUrl should use Microsoft OpenJDK");
			}
		}

		[Test]
		public async Task DiscoverAsync_ContainsExpectedMajorVersions ()
		{
			IReadOnlyList<JdkVersionInfo> versions;
			try {
				versions = await installer.DiscoverAsync ();
			}
			catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException) {
				Assert.Ignore ($"Network unavailable: {ex.Message}");
				return;
			}

			if (versions.Count == 0) {
				Assert.Ignore ("No versions returned.");
				return;
			}

			var majorVersions = versions.Select (v => v.MajorVersion).Distinct ().ToList ();
			Assert.That (majorVersions, Does.Contain (21), "Should contain JDK 21");
		}

		[Test]
		public async Task DiscoverAsync_CancellationToken_Cancels ()
		{
			using var cts = new CancellationTokenSource ();
			cts.Cancel ();

			Assert.ThrowsAsync<OperationCanceledException> (
				async () => await installer.DiscoverAsync (cts.Token));
		}

		[Test]
		public void InstallAsync_InvalidVersion_Throws ()
		{
			Assert.ThrowsAsync<ArgumentException> (
				async () => await installer.InstallAsync (8, Path.GetTempPath ()));
		}

		[Test]
		public void InstallAsync_NullPath_Throws ()
		{
			Assert.ThrowsAsync<ArgumentNullException> (
				async () => await installer.InstallAsync (21, null!));
		}

		[Test]
		public async Task InstallAsync_ReportsProgress ()
		{
			// This test actually downloads and installs a JDK, so it may be slow.
			// Skip if running in CI or if network is unavailable.
			if (Environment.GetEnvironmentVariable ("CI") is not null ||
			    Environment.GetEnvironmentVariable ("TF_BUILD") is not null) {
				Assert.Ignore ("Skipping download test in CI environment.");
				return;
			}

			var targetPath = Path.Combine (Path.GetTempPath (), $"jdk-install-test-{Guid.NewGuid ()}");
			var reportedPhases = new List<JdkInstallPhase> ();
			var progress = new Progress<JdkInstallProgress> (p => {
				reportedPhases.Add (p.Phase);
			});

			try {
				using var cts = new CancellationTokenSource (TimeSpan.FromMinutes (10));
				await installer.InstallAsync (21, targetPath, progress, cts.Token);

				// Verify installation
				Assert.IsTrue (installer.IsValid (targetPath), "Installed JDK should be valid");
				Assert.IsTrue (reportedPhases.Contains (JdkInstallPhase.Downloading), "Should report Downloading phase");
				Assert.IsTrue (reportedPhases.Contains (JdkInstallPhase.Extracting), "Should report Extracting phase");
				Assert.IsTrue (reportedPhases.Contains (JdkInstallPhase.Complete), "Should report Complete phase");

				// Verify we can create a JdkInfo from it
				var jdkInfo = new JdkInfo (targetPath);
				Assert.IsNotNull (jdkInfo.Version);
				Assert.AreEqual (21, jdkInfo.Version!.Major);
			}
			catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException) {
				Assert.Ignore ($"Network unavailable: {ex.Message}");
			}
			finally {
				if (Directory.Exists (targetPath))
					Directory.Delete (targetPath, recursive: true);
			}
		}

		[Test]
		public void Constructor_DefaultLogger_DoesNotThrow ()
		{
			using var defaultInstaller = new JdkInstaller ();
			Assert.IsNotNull (defaultInstaller);
		}

		[Test]
		public void RecommendedMajorVersion_Is21 ()
		{
			Assert.AreEqual (21, JdkInstaller.RecommendedMajorVersion);
		}

		[Test]
		public void SupportedVersions_ContainsExpected ()
		{
			Assert.That (JdkInstaller.SupportedVersions, Does.Contain (21));
		}

		[Test]
		public void IsTargetPathWritable_TempDir_ReturnsTrue ()
		{
			var dir = Path.Combine (Path.GetTempPath (), $"jdk-write-test-{Guid.NewGuid ()}");
			try {
				Assert.IsTrue (FileUtil.IsTargetPathWritable (dir, (level, msg) => TestContext.WriteLine ($"[{level}] {msg}")));
			}
			finally {
				if (Directory.Exists (dir))
					Directory.Delete (dir, recursive: true);
			}
		}

		[Test]
		public void IsTargetPathWritable_NullOrEmpty_ReturnsFalse ()
		{
			var logger = new Action<TraceLevel, string> ((level, msg) => TestContext.WriteLine ($"[{level}] {msg}"));
			Assert.IsFalse (FileUtil.IsTargetPathWritable (null!, logger));
			Assert.IsFalse (FileUtil.IsTargetPathWritable ("", logger));
		}

		[Test]
		public void Remove_NonExistentPath_ReturnsFalse ()
		{
			Assert.IsFalse (installer.Remove (Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ())));
		}

		[Test]
		public void Remove_ExistingDirectory_RemovesIt ()
		{
			var dir = Path.Combine (Path.GetTempPath (), $"jdk-remove-test-{Guid.NewGuid ()}");
			Directory.CreateDirectory (dir);
			File.WriteAllText (Path.Combine (dir, "test.txt"), "test");

			Assert.IsTrue (installer.Remove (dir));
			Assert.IsFalse (Directory.Exists (dir));
		}
	}
}
