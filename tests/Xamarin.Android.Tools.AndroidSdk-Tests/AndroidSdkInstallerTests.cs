using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.Installer.AndroidSDK;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.AndroidSDK.GoogleV2;
using Xamarin.Installer.AndroidSDK.Manager;
using Xamarin.Installer.Common;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
[NonParallelizable]
public class AndroidSdkInstallerTests
{
	[Test]
	[System.Diagnostics.CodeAnalysis.SuppressMessage ("Security", "CA5350", Justification = "Tests SHA-1 checksums supported by Android SDK manifests.")]
	public void InstallValidatesAllArchivesBeforeExtraction (
		[Values (true, false)] bool throwIfInvalidComponentsFound,
		[Values (-1, 0, 1)] int invalidArchiveIndex,
		[Values ("sha1", "sha256")] string checksumType,
		[Values (false, true)] bool ignoreChecksumErrors)
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-installer-" + Guid.NewGuid ().ToString ("N"));
		var sdkPath = Path.Combine (root, "android-sdk");
		var manifestPath = Path.Combine (root, "manifest.xml");
		var log = new InstallerLogAdapter ();
		var previousLogAdapter = Logger.LogAdapter;
		Directory.CreateDirectory (root);
		try {
			Logger.LogAdapter = log;
			var manifest = new XElement ("android",
				new XAttribute (XNamespace.Xmlns + "sdk", "http://schemas.android.com/sdk/android/repo/repository2/03"),
				new XAttribute (XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"));
			for (int i = 0; i < 2; i++) {
				var name = "component-" + i;
				var archivePath = Path.Combine (root, name + ".zip");
				using (var zip = ZipFile.Open (archivePath, ZipArchiveMode.Create))
				using (var writer = new StreamWriter (zip.CreateEntry ("payload/installed.txt").Open ()))
					writer.Write (name);

				using var hash = checksumType == "sha1" ? (HashAlgorithm) SHA1.Create () : SHA256.Create ();
				using var stream = File.OpenRead (archivePath);
				var checksum = Convert.ToHexString (hash.ComputeHash (stream));
				if (i == invalidArchiveIndex)
					checksum = new string ('0', checksum.Length);

				manifest.Add (new XElement ("platform-tools",
					new XAttribute ("original-type", "generic"),
					new XAttribute ("revision", "1.0.0"),
					new XAttribute ("path", name),
					new XAttribute ("filesystem-path", name),
					new XAttribute ("description", name),
					new XAttribute ("license", "test-license"),
					new XAttribute ("manifest-url", new Uri (manifestPath).AbsoluteUri),
					new XElement ("urls", new XElement ("url",
						new XAttribute ("size", stream.Length),
						new XAttribute ("checksum", checksum),
						new XAttribute ("checksum-type", checksumType),
						new XAttribute ("host-os", AndroidUtilities.GetPlatformOS ()),
						new XAttribute ("host-bits", Environment.Is64BitOperatingSystem ? 64 : 32),
						new Uri (archivePath).AbsoluteUri))));
			}
			manifest.Save (manifestPath);
			var installer = new AndroidSDKInstaller (new Helper (manifestFilePath: manifestPath), manifestURL: new Uri (manifestPath));
			Assert.IsFalse (installer.IgnoreArchiveChecksumErrors);
			installer.IgnoreArchiveChecksumErrors = ignoreChecksumErrors;
			installer.Repository.Parse ();
			var components = installer.Repository.Components;
			Assert.AreEqual (2, components.Count);
			for (int i = 0; i < components.Count; i++) {
				var archive = components [i].Archives.Single ();
				archive.DownloadedFilePath = archive.Url.LocalPath;
				Assert.IsTrue (archive.IsInstallable (verifyChecksum: false));
				Assert.AreEqual (i != invalidArchiveIndex, archive.IsInstallable ());
			}
			int installationProgressCount = 0;
			installer.InstallationProgress += (sender, args) => installationProgressCount++;
			Assert.IsTrue (installer.Discover (new List<string> { sdkPath }, fromScratch: false));
			var instance = installer.FindInstance (sdkPath);
			Assert.IsNotNull (instance);

			bool rejectArchives = invalidArchiveIndex >= 0 && !ignoreChecksumErrors;
			if (rejectArchives && throwIfInvalidComponentsFound) {
				Assert.Throws<InvalidOperationException> (() =>
					installer.Install (instance, components, throwIfInvalidComponentsFound));
			} else {
				Assert.DoesNotThrow (() => installer.Install (instance, components, throwIfInvalidComponentsFound));
			}

			if (rejectArchives) {
				Assert.AreEqual (0, installationProgressCount, "Installation must not start for any component.");
				Assert.IsFalse (Directory.Exists (sdkPath), "No component must be extracted.");
				Assert.Greater (log.Errors, 0, "A rejected installation must report an error.");
			} else {
				Assert.Greater (installationProgressCount, 0);
				for (int i = 0; i < components.Count; i++) {
					var componentPath = Path.Combine (sdkPath, "component-" + i);
					Assert.AreEqual ("component-" + i, File.ReadAllText (Path.Combine (componentPath, "installed.txt")));
					Assert.IsTrue (File.Exists (Path.Combine (componentPath, "package.xml")));
				}
				Assert.AreEqual (0, log.Errors);
				Assert.AreEqual (invalidArchiveIndex >= 0 ? 1 : 0, log.Actions);
			}
		} finally {
			Logger.LogAdapter = previousLogAdapter;
			Directory.Delete (root, recursive: true);
		}
	}

	[TestCase (true)]
	[TestCase (false)]
	public void ChecksumOverrideDoesNotIgnoreInvalidHost (bool throwIfInvalidComponentsFound)
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-installer-" + Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (root);
		try {
			var archivePath = Path.Combine (root, "archive.zip");
			using (ZipFile.Open (archivePath, ZipArchiveMode.Create)) {}
			var archive = new Archive (AndroidUtilities.GetPlatformOS ()) {
				DownloadedFilePath = archivePath,
				ChecksumType = "sha256",
				Checksum = Convert.ToHexString (SHA256.HashData (File.ReadAllBytes (archivePath))),
			};
			var installer = new AndroidSDKInstaller (new Helper (), AndroidManifestType.Local) {
				IgnoreArchiveChecksumErrors = true,
			};
			var component = new RemotePackage (installer.Repository, new Uri (archivePath), null, installer, [archive]) {
				Path = "test-component",
				FileSystemPath = "test-component",
			};
			component.Verify ();
			Assert.IsTrue (archive.IsInstallable ());
			archive.HostArch = "unsupported-test-host";
			Assert.IsFalse (archive.IsInstallable (verifyChecksum: false));
			var sdkPath = Path.Combine (root, "android-sdk");
			Assert.IsTrue (installer.Discover ([sdkPath]));
			var instance = installer.FindInstance (sdkPath);
			Assert.IsNotNull (instance);
			if (throwIfInvalidComponentsFound)
				Assert.Throws<InvalidOperationException> (() => installer.Install (instance, [component]));
			else
				Assert.DoesNotThrow (() => installer.Install (instance, [component], throwIfInvalidComponentsFound: false));
			Assert.IsFalse (Directory.Exists (sdkPath));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	sealed class InstallerLogAdapter : ILogAdapter
	{
		public int Errors { get; private set; }
		public int Actions { get; private set; }

		public void Error (string format, params object[] parms) => Errors++;
		public void Warning (string format, params object[] parms) {}
		public void Exception (string format, Exception ex, params object[] parms) => Errors++;
		public void Action (string format, params object[] parms) => Actions++;
		public void Debug (string format, params object[] parms) {}
		public void Debug (string format, Exception ex, params object[] parms) {}
		public void Info (string format, params object[] parms) {}
		public void SetOperationStatus (OperationStatus status) {}
	}

	[Test]
	public void DiscoverSdkFromSourceProperties ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-installer-" + Guid.NewGuid ().ToString ("N"));
		var platformPath = Path.Combine (root, "platforms", "android-35");
		Directory.CreateDirectory (platformPath);
		try {
			var propertiesPath = Path.Combine (platformPath, "source.properties");
			File.WriteAllText (propertiesPath, """
				Pkg.Revision = 35.0.1
				Pkg.Desc = Android SDK \
				    Platform \u0033\u0035
				Platform.Version = 15
				AndroidVersion.ApiLevel = 35
				Layoutlib.Api = 15
				""");
			var installer = new AndroidSDKInstaller (new Helper (), AndroidManifestType.Local);
			Assert.IsTrue (installer.Discover (new List<string> { root }));
			var instance = installer.FindInstance (root);
			Assert.IsNotNull (instance);
			var component = instance.Components.Single (c => c.ManifestURL == new Uri (propertiesPath));
			Assert.AreEqual ("Android SDK Platform 35", component.DisplayName);
			Assert.AreEqual (new AndroidRevision ("35.0.1"), component.InstalledRevision);
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[TestCase ("JAVA_VERSION=\"21.0.3\"")]
	[TestCase ("JAVA_VERSION='21.0.3'")]
	[TestCase ("JAVA_VERSION=21.0.3")]
	[TestCase ("JAVA_VERSION=\\\"21.0.\\u0033\\\"")]
	public void DiscoverJdkRevisionFromReleaseProperties (string contents)
	{
		var root = Path.Combine (Path.GetTempPath (), "android-jdk-installer-" + Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (root);
		try {
			File.WriteAllText (Path.Combine (root, "release"), contents);
			var installer = new JavaDependencyInstaller (new Helper ()) { JdkPath = root };
			Assert.IsTrue (installer.GetJdkRevision (out var revision));
			Assert.AreEqual (new AndroidRevision ("21.0.3"), revision);
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void DiscoverMissingSdkForFirstTimeInstallation ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-installer-" + Guid.NewGuid ().ToString ("N"));
		var sdkPath = Path.Combine (root, "android-sdk");
		Directory.CreateDirectory (root);
		try {
			var installer = new AndroidSDKInstaller (new Helper (), AndroidManifestType.Local);
			Assert.IsTrue (installer.Discover (new List<string> { sdkPath }));
			Assert.AreEqual (sdkPath, installer.FindInstance (sdkPath)?.Path);
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}
}
