// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests
{
	[TestClass]
	public class JdkInstallerTests
	{
		JdkInstaller installer;

		[TestInitialize]
		public void SetUp ()
		{
			installer = new JdkInstaller (logger: (level, message) => {
				Console.WriteLine ($"[{level}] {message}");
			});
		}

		[TestCleanup]
		public void TearDown ()
		{
			installer?.Dispose ();
			installer = null!;
		}

		[TestMethod]
		public void IsValid_NullPath_ReturnsFalse ()
		{
			Assert.IsFalse (installer.IsValid (null!));
		}

		[TestMethod]
		public void IsValid_EmptyPath_ReturnsFalse ()
		{
			Assert.IsFalse (installer.IsValid (""));
		}

		[TestMethod]
		public void IsValid_NonExistentPath_ReturnsFalse ()
		{
			Assert.IsFalse (installer.IsValid (Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ())));
		}

		[TestMethod]
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

		[TestMethod]
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

		[TestMethod]
		[DataRow ("invalid", false)]
		[DataRow ("invalid", true)]
		[DataRow ("cancel", true)]
		[DataRow ("valid", true)]
		public async Task ExtractArchiveAsync_ValidatesBeforeCommitting (string outcome, bool existingTarget)
		{
			var dir = Path.Combine (Path.GetTempPath (), $"jdk-rollback-test-{Guid.NewGuid ():N}");
			var target = Path.Combine (dir, "target");
			var staging = Path.Combine (dir, "staging");
			var root = Path.Combine (staging, "jdk");
			var jdkRoot = OS.IsMac ? Path.Combine (root, "Contents", "Home") : root;
			var archive = Path.Combine (dir, "jdk" + FileUtil.GetArchiveExtension ());
			Directory.CreateDirectory (jdkRoot);
			try {
				if (outcome != "invalid")
					JdkInfoTests.CreateFauxJdk (jdkRoot, releaseVersion: "21.0.1", releaseBuildNumber: "1", javaVersion: "21.0.1-1");
				File.WriteAllText (Path.Combine (jdkRoot, "new.txt"), "replacement");
				if (OS.IsWindows)
					ZipFile.CreateFromDirectory (staging, archive);
				else {
					var psi = ProcessUtils.CreateProcessStartInfo ("/usr/bin/tar", "-czf", archive, "-C", staging, "jdk");
					Assert.AreEqual (0, await ProcessUtils.StartProcess (psi, null, null, CancellationToken.None));
				}
				if (existingTarget) {
					JdkInfoTests.CreateFauxJdk (target, releaseVersion: "17.0.1", releaseBuildNumber: "1", javaVersion: "17.0.1-1");
					Directory.CreateDirectory (Path.Combine (target, "custom"));
					File.WriteAllText (Path.Combine (target, "custom", "preserve.txt"), "previous installation");
				}
				var previousFiles = existingTarget
					? Directory.GetFiles (target, "*", SearchOption.AllDirectories).ToDictionary (p => Path.GetRelativePath (target, p), File.ReadAllBytes)
					: new Dictionary<string, byte []> ();
				var unrelatedBackup = target + ".old-unrelated";
				Directory.CreateDirectory (unrelatedBackup);
				File.WriteAllText (Path.Combine (unrelatedBackup, "preserve.txt"), "unrelated");
				using var cts = new CancellationTokenSource ();
				var progress = new InlineProgress (p => {
					if (p.Phase != JdkInstallPhase.Validating)
						return;
					Assert.IsTrue (File.Exists (Path.Combine (target, "new.txt")));
					Assert.AreEqual (existingTarget ? 2 : 1, Directory.GetDirectories (dir, "target.old-*").Length);
					if (outcome == "cancel")
						cts.Cancel ();
				});

				if (outcome == "valid") {
					await installer.ExtractArchiveAsync (archive, target, cts.Token, progress);
					Assert.IsTrue (installer.IsValid (target));
					Assert.AreEqual ("replacement", File.ReadAllText (Path.Combine (target, "new.txt")));
					Assert.IsFalse (Directory.Exists (Path.Combine (target, "custom")));
				} else {
					if (outcome == "cancel")
						await Assert.ThrowsExactlyAsync<OperationCanceledException> (async () => await installer.ExtractArchiveAsync (archive, target, cts.Token, progress));
					else
						await Assert.ThrowsExactlyAsync<InvalidOperationException> (async () => await installer.ExtractArchiveAsync (archive, target, cts.Token, progress));

					Assert.AreEqual (existingTarget, Directory.Exists (target));
					if (existingTarget) {
						Assert.IsTrue (installer.IsValid (target));
						CollectionAssert.AreEquivalent (previousFiles.Keys.ToArray (), Directory.GetFiles (target, "*", SearchOption.AllDirectories).Select (p => Path.GetRelativePath (target, p)).ToArray ());
						foreach (var file in previousFiles)
							CollectionAssert.AreEqual (file.Value, File.ReadAllBytes (Path.Combine (target, file.Key)), file.Key);
					}
				}
				Assert.IsEmpty (Directory.GetDirectories (dir, ".jdk-extract-*"));
				CollectionAssert.AreEquivalent (new [] { unrelatedBackup }, Directory.GetDirectories (dir, "target.old-*"));
				Assert.AreEqual ("unrelated", File.ReadAllText (Path.Combine (unrelatedBackup, "preserve.txt")));
			}
			finally {
				Directory.Delete (dir, recursive: true);
			}
		}

		sealed class InlineProgress : IProgress<JdkInstallProgress>
		{
			readonly Action<JdkInstallProgress> report;

			public InlineProgress (Action<JdkInstallProgress> report) => this.report = report;

			public void Report (JdkInstallProgress value) => report (value);
		}

		[TestMethod]
		public void IsValid_SystemJdk_ReturnsTrue ()
		{
			// Find first known JDK on the system
			var jdk = JdkInfo.GetKnownSystemJdkInfos ().FirstOrDefault ();
			if (jdk is null) {
				Assert.Inconclusive ("No system JDK found to validate.");
				return;
			}
			Assert.IsTrue (installer.IsValid (jdk.HomePath));
		}

		[TestMethod]
		public async Task DiscoverAsync_ReturnsVersions ()
		{
			IReadOnlyList<JdkVersionInfo> versions;
			try {
				using (var cts = new CancellationTokenSource (TimeSpan.FromSeconds (15))) {
					versions = await installer.DiscoverAsync (cts.Token);
				}
			}
			catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException) {
				Assert.Inconclusive ($"Network unavailable: {ex.Message}");
				return;
			}

			// We should get at least one version (if network is available)
			Assert.IsNotNull (versions);
			if (versions.Count == 0) {
				Assert.Inconclusive ("No versions returned (network may be restricted).");
				return;
			}

			Assert.Contains (21, versions.Select (v => v.MajorVersion), "Should contain JDK 21");

			// Verify structure of returned info
			foreach (var v in versions) {
				Assert.IsGreaterThan (0, v.MajorVersion, "MajorVersion should be positive");
				Assert.IsNotEmpty (v.DisplayName, "DisplayName should not be empty");
				Assert.IsNotEmpty (v.DownloadUrl, "DownloadUrl should not be empty");
				Assert.Contains ("aka.ms/download-jdk", v.DownloadUrl, "DownloadUrl should use Microsoft OpenJDK");
			}
		}

		[TestMethod]
		public async Task DiscoverAsync_CancellationToken_Cancels ()
		{
			using var cts = new CancellationTokenSource ();
			cts.Cancel ();

			await Assert.ThrowsExactlyAsync<OperationCanceledException> (
				async () => await installer.DiscoverAsync (cts.Token));
		}

		[TestMethod]
		public async Task InstallAsync_InvalidVersion_Throws ()
		{
			await Assert.ThrowsExactlyAsync<ArgumentException> (
				async () => await installer.InstallAsync (8, Path.GetTempPath ()));
		}

		[TestMethod]
		public async Task InstallAsync_NullPath_Throws ()
		{
			await Assert.ThrowsExactlyAsync<ArgumentNullException> (
				async () => await installer.InstallAsync (21, null!));
		}

		[TestMethod]
		public async Task InstallAsync_ReportsProgress ()
		{
			// This test actually downloads and installs a JDK, so it may be slow.
			// Skip if running in CI or if network is unavailable.
			if (Environment.GetEnvironmentVariable ("CI") is not null ||
			    Environment.GetEnvironmentVariable ("TF_BUILD") is not null) {
				Assert.Inconclusive ("Skipping download test in CI environment.");
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
				Assert.Inconclusive ($"Network unavailable: {ex.Message}");
			}
			finally {
				if (Directory.Exists (targetPath))
					Directory.Delete (targetPath, recursive: true);
			}
		}

		[TestMethod]
		public void Constructor_DefaultLogger_DoesNotThrow ()
		{
			using var defaultInstaller = new JdkInstaller ();
			Assert.IsNotNull (defaultInstaller);
		}

		[TestMethod]
		public void RecommendedMajorVersion_Is21 ()
		{
			Assert.AreEqual (21, JdkInstaller.RecommendedMajorVersion);
		}

		[TestMethod]
		public void SupportedVersions_ContainsExpected ()
		{
			Assert.Contains (21, JdkInstaller.SupportedVersions);
		}

		[TestMethod]
		public void IsTargetPathWritable_TempDir_ReturnsTrue ()
		{
			var dir = Path.Combine (Path.GetTempPath (), $"jdk-write-test-{Guid.NewGuid ()}");
			try {
				Assert.IsTrue (FileUtil.IsTargetPathWritable (dir, (level, msg) => Console.WriteLine ($"[{level}] {msg}")));
			}
			finally {
				if (Directory.Exists (dir))
					Directory.Delete (dir, recursive: true);
			}
		}

		[TestMethod]
		public void IsTargetPathWritable_NullOrEmpty_ReturnsFalse ()
		{
			var logger = new Action<TraceLevel, string> ((level, msg) => Console.WriteLine ($"[{level}] {msg}"));
			Assert.IsFalse (FileUtil.IsTargetPathWritable (null!, logger));
			Assert.IsFalse (FileUtil.IsTargetPathWritable ("", logger));
		}

		[TestMethod]
		public void Remove_NonExistentPath_ReturnsFalse ()
		{
			Assert.IsFalse (installer.Remove (Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ())));
		}

		[TestMethod]
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
