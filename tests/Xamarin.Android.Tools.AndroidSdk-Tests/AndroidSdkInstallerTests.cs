using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

	[Test]
	public void UnzipPreservesContentsTimestampsAndPermissions ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		var timestamp = new DateTime (2024, 5, 6, 7, 8, 10, DateTimeKind.Local);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var dir = zip.CreateEntry ("pkg/bin/");
				dir.LastWriteTime = timestamp;
				var tool = zip.CreateEntry ("pkg/bin/tool");
				tool.LastWriteTime = timestamp;
				tool.ExternalAttributes = Convert.ToInt32 ("100755", 8) << 16;
				using (var writer = new StreamWriter (tool.Open ()))
					writer.Write ("#!/bin/sh\necho hi\n");
				var data = zip.CreateEntry ("pkg/data.txt", CompressionLevel.NoCompression);
				data.LastWriteTime = timestamp;
				data.ExternalAttributes = Convert.ToInt32 ("100600", 8) << 16;
				using (var writer = new StreamWriter (data.Open ()))
					writer.Write ("data");
			}

			new Helper ().Unzip (outPath, zipPath, null, null);

			var toolPath = Path.Combine (outPath, "pkg", "bin", "tool");
			var dataPath = Path.Combine (outPath, "pkg", "data.txt");
			Assert.AreEqual ("#!/bin/sh\necho hi\n", File.ReadAllText (toolPath));
			Assert.AreEqual ("data", File.ReadAllText (dataPath));
			Assert.AreEqual (timestamp, File.GetLastWriteTime (toolPath));
			Assert.AreEqual (timestamp, File.GetLastWriteTime (dataPath));
			Assert.IsTrue (Directory.Exists (Path.Combine (outPath, "pkg", "bin")));
			if (!OperatingSystem.IsWindows ()) {
				Assert.AreEqual (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
						UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
						UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
					File.GetUnixFileMode (toolPath));
				Assert.AreEqual (UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode (dataPath));
			}
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipPreservesExtendedTimestamp ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		var timestamp = new DateTimeOffset (2024, 5, 6, 7, 8, 11, TimeSpan.Zero);
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create))
			using (var writer = new StreamWriter (zip.CreateEntry ("pkg/data.txt").Open ()))
				writer.Write ("timestamp");

			var archive = AddExtendedTimestampToCentralDirectory (File.ReadAllBytes (zipPath), timestamp.ToUnixTimeSeconds ());
			File.WriteAllBytes (zipPath, archive);

			new Helper ().Unzip (outPath, zipPath, null, null);

			var filePath = Path.Combine (outPath, "pkg", "data.txt");
			Assert.AreEqual ("timestamp", File.ReadAllText (filePath));
			Assert.AreEqual (timestamp.UtcDateTime, File.GetLastWriteTimeUtc (filePath));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipRecognizesDosDirectoryAndFileAttributes ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				zip.CreateEntry ("pkg/dos-dir");
				using (var writer = new StreamWriter (zip.CreateEntry ("pkg/readme.txt").Open ()))
					writer.Write ("dos attributes");
			}

			byte [] archive = File.ReadAllBytes (zipPath);
			archive = SetDosAttributes (archive, "pkg/dos-dir", 0x10);
			archive = SetDosAttributes (archive, "pkg/readme.txt", 0x21);
			File.WriteAllBytes (zipPath, archive);

			new Helper ().Unzip (outPath, zipPath, null, null);

			Assert.IsTrue (Directory.Exists (Path.Combine (outPath, "pkg", "dos-dir")));
			var filePath = Path.Combine (outPath, "pkg", "readme.txt");
			Assert.AreEqual ("dos attributes", File.ReadAllText (filePath));
			if (OperatingSystem.IsWindows ())
				Assert.IsTrue ((File.GetAttributes (filePath) & FileAttributes.ReadOnly) != 0);
		} finally {
			var readOnlyFile = Path.Combine (outPath, "pkg", "readme.txt");
			if (File.Exists (readOnlyFile))
				File.SetAttributes (readOnlyFile, FileAttributes.Normal);
			Directory.Delete (root, recursive: true);
		}
	}

	[TestCase ("104755")]
	[TestCase ("102755")]
	[TestCase ("106755")]
	public void UnzipStripsSetIdPermissions (string archiveMode)
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("Unix permission bits are not applied on Windows.");
			return;
		}

		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var tool = zip.CreateEntry ("tool");
				tool.ExternalAttributes = Convert.ToInt32 (archiveMode, 8) << 16;
				using (var writer = new StreamWriter (tool.Open ()))
					writer.Write ("test");
			}

			new Helper ().Unzip (outPath, zipPath, null, null);

			Assert.AreEqual (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
					UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
					UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
				File.GetUnixFileMode (Path.Combine (outPath, "tool")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipRejectsParentTraversal ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				using (var writer = new StreamWriter (zip.CreateEntry ("../evil.txt").Open ()))
					writer.Write ("evil");
			}

			var ex = Assert.Throws<Exception> (() => new Helper ().Unzip (outPath, zipPath, null, null));
			StringAssert.Contains ("parent traversal", ex?.Message);
			Assert.IsFalse (File.Exists (Path.Combine (root, "evil.txt")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipRejectsCaseVariantSibling ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create))
			using (var writer = new StreamWriter (zip.CreateEntry ("../OUT/evil.txt").Open ()))
				writer.Write ("evil");
			Assert.Throws<Exception> (() => new Helper ().Unzip (outPath, zipPath, null, null));
			Assert.IsFalse (File.Exists (Path.Combine (root, "OUT", "evil.txt")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void TemporaryDirectoryIsPrivateOnUnix ()
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("Unix permission bits are not applied on Windows.");
			return;
		}
		var type = typeof (Helper).Assembly.GetType ("Xamarin.Installer.AndroidSDK.Common.TemporaryDirectory");
		Assert.IsNotNull (type);
		var create = type.GetMethod ("Create");
		Assert.IsNotNull (create);
		var path = create.Invoke (null, null) as string;
		Assert.IsNotNull (path);
		try {
			Assert.AreEqual (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode (path));
		} finally {
			Directory.Delete (path);
		}
	}

	[TestCase (false)]
	[TestCase (true)]
	public void UnzipStripsGroupAndOtherWritePermissions (bool unspecifiedMode)
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("Unix permission bits are not applied on Windows.");
			return;
		}

		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var entry = zip.CreateEntry ("tool");
				entry.ExternalAttributes = unspecifiedMode ? -1 : Convert.ToInt32 ("100777", 8) << 16;
				using var writer = new StreamWriter (entry.Open ());
				writer.Write ("tool");
			}
			new Helper ().Unzip (outPath, zipPath, null, null);
			var expected = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
			if (!unspecifiedMode)
				expected |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
			Assert.AreEqual (expected,
				File.GetUnixFileMode (Path.Combine (outPath, "tool")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipAndMovePreserveUnixSymbolicLinks ()
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");

		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		var installed = Path.Combine (root, "installed");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				WriteEntry (zip, "pkg/bin/clang", "compiler");
				WriteEntry (zip, "pkg/bin/clang++", "clang");
				WriteEntry (zip, "pkg/bin/cc", "clang++");
				WriteEntry (zip, "pkg/links/compiler", "../bin/cc");
				WriteEntry (zip, "pkg/bin-link", "bin");
			}
			byte [] archive = File.ReadAllBytes (zipPath);
			foreach (string name in new [] { "pkg/bin/clang++", "pkg/bin/cc", "pkg/links/compiler", "pkg/bin-link" })
				archive = SetUnixAttributes (archive, name, Convert.ToUInt32 ("120777", 8) << 16);
			File.WriteAllBytes (zipPath, archive);

			var helper = new Helper ();
			var componentPath = helper.Unzip (outPath, zipPath, null, null);
			CommonUtilities.Helpers = helper;
			CommonUtilities.MoveDirectory (componentPath, installed);
			Assert.AreEqual ("clang", new FileInfo (Path.Combine (installed, "bin", "clang++")).LinkTarget);
			Assert.AreEqual ("clang++", new FileInfo (Path.Combine (installed, "bin", "cc")).LinkTarget);
			Assert.AreEqual ("../bin/cc", new FileInfo (Path.Combine (installed, "links", "compiler")).LinkTarget);
			Assert.AreEqual ("bin", new DirectoryInfo (Path.Combine (installed, "bin-link")).LinkTarget);
			Assert.AreEqual ("compiler", File.ReadAllText (Path.Combine (installed, "links", "compiler")));
			Assert.AreEqual ("compiler", File.ReadAllText (Path.Combine (installed, "bin-link", "clang")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void MoveRejectsExistingDestinationDirectorySymlink ()
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-move-" + Guid.NewGuid ().ToString ("N"));
		var source = Path.Combine (root, "source");
		var destination = Path.Combine (root, "destination");
		var outside = Path.Combine (root, "outside");
		Directory.CreateDirectory (Path.Combine (source, "pkg"));
		Directory.CreateDirectory (destination);
		Directory.CreateDirectory (outside);
		try {
			File.WriteAllText (Path.Combine (source, "pkg", "data"), "data");
			Directory.CreateSymbolicLink (Path.Combine (destination, "pkg"), outside);
			CommonUtilities.Helpers = new Helper ();
			Assert.Throws<IOException> (() => CommonUtilities.MoveDirectory (source, destination, move: false, overwrite: true));
			Assert.IsFalse (File.Exists (Path.Combine (outside, "data")));
			Assert.IsTrue (CommonUtilities.DeleteDirectoryRecursively (destination));
			Assert.IsTrue (Directory.Exists (outside));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[TestCase ("../../outside")]
	[TestCase ("../pkg/data")]
	[TestCase ("directory/../../outside")]
	[TestCase ("missing")]
	[TestCase ("link")]
	public void UnzipRejectsUnsafeUnixLinkTargets (string target)
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				WriteEntry (zip, "pkg/data", "data");
				WriteEntry (zip, "pkg/link", target);
			}
			File.WriteAllBytes (zipPath, SetUnixAttributes (File.ReadAllBytes (zipPath), "pkg/link", Convert.ToUInt32 ("120777", 8) << 16));
			Assert.Throws<InvalidDataException> (() => new Helper ().Unzip (outPath, zipPath, null, null));
			Assert.IsFalse (File.Exists (Path.Combine (root, "outside")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipStoresAbsoluteUnixLinkTargetAsFile ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				WriteEntry (zip, "pkg/data", "data");
				WriteEntry (zip, "pkg/bin/as", "/buildbot/out/bin/as");
			}
			File.WriteAllBytes (zipPath, SetUnixAttributes (File.ReadAllBytes (zipPath), "pkg/bin/as", Convert.ToUInt32 ("120777", 8) << 16));
			new Helper ().Unzip (outPath, zipPath, null, null);
			var path = Path.Combine (outPath, "pkg", "bin", "as");
			Assert.IsNull (new FileInfo (path).LinkTarget);
			Assert.AreEqual ("/buildbot/out/bin/as", File.ReadAllText (path));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipRejectsExistingDirectorySymlink ()
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		var outside = Path.Combine (root, "outside");
		Directory.CreateDirectory (Path.Combine (outPath, "pkg"));
		Directory.CreateDirectory (outside);
		try {
			Directory.CreateSymbolicLink (Path.Combine (outPath, "pkg", "link"), outside);
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create))
				WriteEntry (zip, "pkg/link/evil", "evil");
			Assert.Throws<InvalidDataException> (() => new Helper ().Unzip (outPath, zipPath, null, null));
			Assert.IsFalse (File.Exists (Path.Combine (outside, "evil")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipRejectsExistingTopLevelSymlinkForEmptyArchive ()
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		var outside = Path.Combine (root, "outside");
		Directory.CreateDirectory (outPath);
		Directory.CreateDirectory (outside);
		try {
			Directory.CreateSymbolicLink (Path.Combine (outPath, "link"), outside);
			using (ZipFile.Open (zipPath, ZipArchiveMode.Create)) {}
			Assert.Throws<InvalidDataException> (() => new Helper ().Unzip (outPath, zipPath, null, null));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	static void WriteEntry (ZipArchive zip, string name, string content)
	{
		using var writer = new StreamWriter (zip.CreateEntry (name).Open ());
		writer.Write (content);
	}

	[Test]
	public void UnzipExtractsArchiveWithDataDescriptor ()
	{
		byte [] archive;
		using (var stream = new NonSeekableMemoryStream ()) {
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				using (var writer = new StreamWriter (zip.CreateEntry ("pkg/data.txt").Open ()))
					writer.Write (ArchiveContents);
			}
			archive = stream.ToArray ();
		}
		Assert.AreNotEqual (0, archive [6] & 0x08, "Expected the entry to use a data descriptor.");

		var outPath = UnzipBytes (archive, out var root);
		try {
			Assert.AreEqual (ArchiveContents, File.ReadAllText (Path.Combine (outPath, "pkg", "data.txt")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipExtractsZip64Archive ()
	{
		byte [] archive = CreateZip64Archive (null);
		var outPath = UnzipBytes (archive, out var root);
		try {
			Assert.AreEqual (ArchiveContents, File.ReadAllText (Path.Combine (outPath, "pkg", "data.txt")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[TestCase ("count")]
	[TestCase ("disk")]
	[TestCase ("size")]
	public void UnzipRejectsConflictingZip64Directory (string sentinel)
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			File.WriteAllBytes (zipPath, CreateZip64Archive (sentinel));
			Assert.Throws<InvalidDataException> (() => new Helper ().Unzip (outPath, zipPath, null, null));
			Assert.IsFalse (File.Exists (Path.Combine (outPath, "pkg", "data.txt")));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void UnzipRejectsEndRecordInsideComment ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		try {
			byte [] source = CreateSimpleArchive ();
			int end = FindSignature (source, 0x06054b50);
			var archive = new byte [source.Length + 25];
			Buffer.BlockCopy (source, 0, archive, 0, source.Length);
			WriteUInt16 (archive, end + 20, 25);
			Buffer.BlockCopy (source, end, archive, source.Length, 22);
			File.WriteAllBytes (zipPath, archive);
			Assert.Throws<InvalidDataException> (() => new Helper ().Unzip (outPath, zipPath, null, null));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	static byte [] CreateSimpleArchive ()
	{
		using var stream = new MemoryStream ();
		using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true))
			WriteEntry (zip, "pkg/data.txt", ArchiveContents);
		return stream.ToArray ();
	}

	static byte [] CreateZip64Archive (string conflict)
	{
		byte [] source = CreateSimpleArchive ();
		int end = FindSignature (source, 0x06054b50);
		uint centralSize = ReadUInt32 (source, end + 12);
		uint centralOffset = ReadUInt32 (source, end + 16);
		using var stream = new MemoryStream ();
		using var writer = new BinaryWriter (stream);
		writer.Write (source, 0, end);
		ulong zip64Offset = centralOffset;
		if (conflict != null) {
			zip64Offset = (ulong) stream.Position;
			writer.Write (source, (int) centralOffset, (int) centralSize);
		}
		ulong recordOffset = (ulong) stream.Position;
		writer.Write (0x06064b50u);
		writer.Write (44ul);
		writer.Write ((ushort) 45);
		writer.Write ((ushort) 45);
		writer.Write (0u);
		writer.Write (0u);
		writer.Write (1ul);
		writer.Write (1ul);
		writer.Write ((ulong) centralSize);
		writer.Write (zip64Offset);
		writer.Write (0x07064b50u);
		writer.Write (0u);
		writer.Write (recordOffset);
		writer.Write (1u);
		byte [] endRecord = source [end..];
		if (conflict == "disk") {
			WriteUInt16 (endRecord, 4, ushort.MaxValue);
			WriteUInt16 (endRecord, 6, ushort.MaxValue);
		} else if (conflict == "size") {
			WriteUInt32 (endRecord, 12, uint.MaxValue);
		} else {
			WriteUInt16 (endRecord, 8, ushort.MaxValue);
			WriteUInt16 (endRecord, 10, ushort.MaxValue);
		}
		writer.Write (endRecord);
		return stream.ToArray ();
	}

	[Test]
	public void CalculateMoveProgressSplitSupportsZip64Archives ()
	{
		var root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "large.zip");
		Directory.CreateDirectory (root);
		try {
			using (var zip = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				for (int i = 0; i < 65536; i++)
					zip.CreateEntry ($"f{i}");
			}

			Assert.AreEqual (50, DirectorySizeMonitoringTimer.CalculateMoveProgressSplit (zipPath));
			using var stream = File.OpenRead (zipPath);
			using var archive = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true);
			var metadataType = typeof (Helper).Assembly.GetType ("Xamarin.Installer.AndroidSDK.Manager.ZipCentralDirectory");
			Assert.IsNotNull (metadataType);
			var read = metadataType.GetMethod ("Read");
			Assert.IsNotNull (read);
			var entries = read.Invoke (null, [stream, archive.Entries.Count]) as Array;
			Assert.IsNotNull (entries);
			Assert.AreEqual (65536, entries.Length);
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	const string ArchiveContents = "Android SDK package contents. Android SDK package contents. Android SDK package contents.";

	static string UnzipBytes (byte [] archive, out string root)
	{
		root = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		var zipPath = Path.Combine (root, "package.zip");
		var outPath = Path.Combine (root, "out");
		Directory.CreateDirectory (root);
		File.WriteAllBytes (zipPath, archive);
		new Helper ().Unzip (outPath, zipPath, null, null);
		return outPath;
	}

	static byte [] AddExtendedTimestampToCentralDirectory (byte [] archive, long timestamp)
	{
		const uint EndOfCentralDirectorySignature = 0x06054b50;
		const uint CentralDirectoryHeaderSignature = 0x02014b50;
		const int TimestampExtraFieldSize = 9;
		int endOfCentralDirectory = FindSignature (archive, EndOfCentralDirectorySignature);
		int centralDirectoryOffset = checked ((int) ReadUInt32 (archive, endOfCentralDirectory + 16));
		Assert.AreEqual (CentralDirectoryHeaderSignature, ReadUInt32 (archive, centralDirectoryOffset));

		int nameLength = ReadUInt16 (archive, centralDirectoryOffset + 28);
		int extraLength = ReadUInt16 (archive, centralDirectoryOffset + 30);
		int extraDataOffset = centralDirectoryOffset + 46 + nameLength;
		int insertOffset = extraDataOffset + extraLength;
		var updatedArchive = new byte [archive.Length + TimestampExtraFieldSize];
		Buffer.BlockCopy (archive, 0, updatedArchive, 0, insertOffset);
		updatedArchive [insertOffset] = 0x55;
		updatedArchive [insertOffset + 1] = 0x54;
		updatedArchive [insertOffset + 2] = 5;
		updatedArchive [insertOffset + 4] = 1;
		WriteUInt32 (updatedArchive, insertOffset + 5, unchecked ((uint) (int) timestamp));
		Buffer.BlockCopy (archive, insertOffset, updatedArchive, insertOffset + TimestampExtraFieldSize, archive.Length - insertOffset);
		WriteUInt16 (updatedArchive, centralDirectoryOffset + 30, (ushort) (extraLength + TimestampExtraFieldSize));
		WriteUInt32 (updatedArchive, endOfCentralDirectory + TimestampExtraFieldSize + 12,
			ReadUInt32 (archive, endOfCentralDirectory + 12) + TimestampExtraFieldSize);
		return updatedArchive;
	}

	static byte [] SetDosAttributes (byte [] archive, string entryName, uint attributes)
		=> SetAttributes (archive, entryName, attributes, 0);

	static byte [] SetUnixAttributes (byte [] archive, string entryName, uint attributes)
		=> SetAttributes (archive, entryName, attributes, 3);

	static byte [] SetAttributes (byte [] archive, string entryName, uint attributes, int hostSystem)
	{
		const uint EndOfCentralDirectorySignature = 0x06054b50;
		const uint CentralDirectoryHeaderSignature = 0x02014b50;
		int endOfCentralDirectory = FindSignature (archive, EndOfCentralDirectorySignature);
		int centralDirectoryOffset = checked ((int) ReadUInt32 (archive, endOfCentralDirectory + 16));
		while (ReadUInt32 (archive, centralDirectoryOffset) == CentralDirectoryHeaderSignature) {
			int nameLength = ReadUInt16 (archive, centralDirectoryOffset + 28);
			int extraLength = ReadUInt16 (archive, centralDirectoryOffset + 30);
			int commentLength = ReadUInt16 (archive, centralDirectoryOffset + 32);
			string name = Encoding.ASCII.GetString (archive, centralDirectoryOffset + 46, nameLength);
			if (name == entryName) {
				var updatedArchive = (byte []) archive.Clone ();
				WriteUInt16 (updatedArchive, centralDirectoryOffset + 4, (ushort) ((ReadUInt16 (archive, centralDirectoryOffset + 4) & 0x00ff) | (hostSystem << 8)));
				WriteUInt32 (updatedArchive, centralDirectoryOffset + 38, attributes);
				return updatedArchive;
			}
			centralDirectoryOffset += 46 + nameLength + extraLength + commentLength;
		}

		throw new InvalidDataException ($"ZIP entry '{entryName}' not found.");
	}

	static int FindSignature (byte [] data, uint signature)
	{
		for (int i = data.Length - 4; i >= 0; i--) {
			if (ReadUInt32 (data, i) == signature)
				return i;
		}
		throw new InvalidDataException ("ZIP signature not found.");
	}

	static ushort ReadUInt16 (byte [] data, int position) =>
		(ushort) (data [position] | (data [position + 1] << 8));

	static uint ReadUInt32 (byte [] data, int position) =>
		(uint) (data [position] | (data [position + 1] << 8) | (data [position + 2] << 16) | (data [position + 3] << 24));

	static void WriteUInt16 (byte [] data, int position, ushort value)
	{
		data [position] = (byte) value;
		data [position + 1] = (byte) (value >> 8);
	}

	static void WriteUInt32 (byte [] data, int position, uint value)
	{
		data [position] = (byte) value;
		data [position + 1] = (byte) (value >> 8);
		data [position + 2] = (byte) (value >> 16);
		data [position + 3] = (byte) (value >> 24);
	}

	sealed class NonSeekableMemoryStream : MemoryStream
	{
		public override bool CanSeek => false;
		public override long Seek (long offset, SeekOrigin loc) => throw new NotSupportedException ();
	}
}
