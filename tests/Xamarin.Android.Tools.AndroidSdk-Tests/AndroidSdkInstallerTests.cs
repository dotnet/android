using System;
using System.Buffers.Binary;
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

	[TestCase (false)]
	[TestCase (true)]
	public void UnzipPreservesContentsTimestampsAndPermissions (bool dosAttributes)
	{
		using var temp = new TempDirectory ();
		var timestamp = new DateTime (2024, 5, 6, 7, 8, 10, DateTimeKind.Local);
		using (var zip = ZipFile.Open (temp.ZipPath, ZipArchiveMode.Create)) {
			zip.CreateEntry ("pkg/bin/").LastWriteTime = timestamp;
			// Set-ID, sticky, and group/other write bits are stripped; an unspecified mode defaults to 0644.
			foreach (var (name, mode) in new [] { ("bin/tool", "100755"), ("data.txt", "100600"), ("bin/setid", "107777"), ("unspecified", null) }) {
				var entry = zip.CreateEntry ("pkg/" + name);
				entry.LastWriteTime = timestamp;
				entry.ExternalAttributes = mode == null ? -1 : Convert.ToInt32 (mode, 8) << 16;
				using var writer = new StreamWriter (entry.Open ());
				writer.Write (name);
			}
		}

		// -1 contains the DOS directory bit, so associate it only with Unix creator metadata.
		byte [] archive = File.ReadAllBytes (temp.ZipPath);
		archive = dosAttributes
			? SetDosAttributes (archive, "pkg/unspecified", (uint) FileAttributes.Normal)
			: SetUnixAttributes (archive, "pkg/unspecified", uint.MaxValue);
		File.WriteAllBytes (temp.ZipPath, archive);
		temp.Unzip ();

		Assert.IsTrue (Directory.Exists (Path.Combine (temp.OutPath, "pkg", "bin")));
		foreach (var (name, mode) in new [] { ("bin/tool", "755"), ("data.txt", "600"), ("bin/setid", "755"), ("unspecified", "644") }) {
			var path = Path.Combine (temp.OutPath, "pkg", name);
			Assert.AreEqual (name, File.ReadAllText (path));
			Assert.AreEqual (timestamp, File.GetLastWriteTime (path));
			if (!OperatingSystem.IsWindows ())
				Assert.AreEqual ((UnixFileMode) Convert.ToInt32 (mode, 8), File.GetUnixFileMode (path), name);
		}
	}

	[Test]
	public void UnzipPreservesExtendedTimestamp ()
	{
		using var temp = new TempDirectory ();
		var timestamp = new DateTimeOffset (2024, 5, 6, 7, 8, 11, TimeSpan.Zero);
		File.WriteAllBytes (temp.ZipPath, AddExtendedTimestampToCentralDirectory (CreateSimpleArchive (), timestamp.ToUnixTimeSeconds ()));

		temp.Unzip ();

		Assert.AreEqual (timestamp.UtcDateTime, File.GetLastWriteTimeUtc (Path.Combine (temp.OutPath, "pkg", "data.txt")));
	}

	[Test]
	public void UnzipRecognizesDosDirectoryAndFileAttributes ()
	{
		using var temp = new TempDirectory ();
		using (var zip = ZipFile.Open (temp.ZipPath, ZipArchiveMode.Create)) {
			zip.CreateEntry ("pkg/dos-dir");
			WriteEntry (zip, "pkg/readme.txt", "dos attributes");
		}
		byte [] archive = File.ReadAllBytes (temp.ZipPath);
		archive = SetDosAttributes (archive, "pkg/dos-dir", 0x10);
		archive = SetDosAttributes (archive, "pkg/readme.txt", 0x21);
		File.WriteAllBytes (temp.ZipPath, archive);

		temp.Unzip ();

		Assert.IsTrue (Directory.Exists (Path.Combine (temp.OutPath, "pkg", "dos-dir")));
		var filePath = Path.Combine (temp.OutPath, "pkg", "readme.txt");
		Assert.AreEqual ("dos attributes", File.ReadAllText (filePath));
		if (OperatingSystem.IsWindows ())
			Assert.IsTrue ((File.GetAttributes (filePath) & FileAttributes.ReadOnly) != 0);
	}

	[Test]
	public void UnzipExtractsArchiveWithDataDescriptor ()
	{
		using var temp = new TempDirectory ();
		using (var stream = new NonSeekableMemoryStream ()) {
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true))
				WriteEntry (zip, "pkg/data.txt", ArchiveContents);
			File.WriteAllBytes (temp.ZipPath, stream.ToArray ());
		}
		Assert.AreNotEqual (0, File.ReadAllBytes (temp.ZipPath) [6] & 0x08, "Expected the entry to use a data descriptor.");

		temp.Unzip ();

		Assert.AreEqual (ArchiveContents, File.ReadAllText (Path.Combine (temp.OutPath, "pkg", "data.txt")));
	}

	// ZipArchive extracts the data while ZipCentralDirectory reads the metadata: both must select the same central directory.
	[TestCase ("zip64", true)]
	[TestCase ("count", false)]
	[TestCase ("disk", false)]
	[TestCase ("size", false)]
	[TestCase ("comment", false)]
	public void UnzipRequiresUnambiguousCentralDirectory (string variant, bool valid)
	{
		using var temp = new TempDirectory ();
		File.WriteAllBytes (temp.ZipPath, variant == "comment" ? CreateArchiveWithEndRecordInComment () : CreateZip64Archive (variant));
		var dataPath = Path.Combine (temp.OutPath, "pkg", "data.txt");

		if (valid) {
			temp.Unzip ();
			Assert.AreEqual (ArchiveContents, File.ReadAllText (dataPath));
		} else {
			Assert.Throws<InvalidDataException> (() => temp.Unzip ());
			Assert.IsFalse (File.Exists (dataPath));
		}
	}

	[Test]
	public void CalculateMoveProgressSplitSupportsZip64Archives ()
	{
		using var temp = new TempDirectory ();
		using (var zip = ZipFile.Open (temp.ZipPath, ZipArchiveMode.Create)) {
			for (int i = 0; i < 65536; i++)
				zip.CreateEntry ($"f{i}");
		}

		Assert.AreEqual (50, DirectorySizeMonitoringTimer.CalculateMoveProgressSplit (temp.ZipPath));
	}

	[TestCase ("../evil.txt")]
	[TestCase ("../OUT/evil.txt")] // escapes on case-sensitive file systems
	public void UnzipRejectsPathsOutsideDestination (string entryName)
	{
		using var temp = new TempDirectory ();
		using (var zip = ZipFile.Open (temp.ZipPath, ZipArchiveMode.Create))
			WriteEntry (zip, entryName, "evil");

		var ex = Assert.Throws<Exception> (() => temp.Unzip ());
		StringAssert.Contains ("parent traversal", ex?.Message);
		Assert.IsFalse (File.Exists (Path.GetFullPath (Path.Combine (temp.OutPath, entryName))));
	}

	[Test]
	public void UnzipRejectsExistingDirectorySymlink ()
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");
		using var temp = new TempDirectory ();
		var outside = Path.Combine (temp.Root, "outside");
		Directory.CreateDirectory (outside);
		Directory.CreateDirectory (Path.Combine (temp.OutPath, "pkg"));
		Directory.CreateSymbolicLink (Path.Combine (temp.OutPath, "pkg", "link"), outside);
		using (var zip = ZipFile.Open (temp.ZipPath, ZipArchiveMode.Create))
			WriteEntry (zip, "pkg/link/evil", "evil");

		Assert.Throws<InvalidDataException> (() => temp.Unzip ());
		Assert.IsFalse (File.Exists (Path.Combine (outside, "evil")));
	}

	[Test]
	public void UnzipAndMovePreserveUnixSymbolicLinks ()
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");
		using var temp = new TempDirectory ();
		using (var zip = ZipFile.Open (temp.ZipPath, ZipArchiveMode.Create)) {
			WriteEntry (zip, "pkg/bin/clang", "compiler");
			WriteEntry (zip, "pkg/bin/clang++", "clang", Symlink);
			WriteEntry (zip, "pkg/bin/cc", "clang++", Symlink);
			WriteEntry (zip, "pkg/links/compiler", "../bin/cc", Symlink);
			WriteEntry (zip, "pkg/bin-link", "bin", Symlink);
			// Absolute targets (e.g. NDK r23b `bin/as`) are stored as regular files, as SharpZipLib did.
			WriteEntry (zip, "pkg/bin/as", "/buildbot/bin/as", Symlink);
		}

		var helper = new Helper ();
		var componentPath = helper.Unzip (temp.OutPath, temp.ZipPath, null, null);
		CommonUtilities.Helpers = helper;
		var installed = Path.Combine (temp.Root, "installed");
		CommonUtilities.MoveDirectory (componentPath, installed);

		Assert.AreEqual ("clang", new FileInfo (Path.Combine (installed, "bin", "clang++")).LinkTarget);
		Assert.AreEqual ("clang++", new FileInfo (Path.Combine (installed, "bin", "cc")).LinkTarget);
		Assert.AreEqual ("../bin/cc", new FileInfo (Path.Combine (installed, "links", "compiler")).LinkTarget);
		Assert.AreEqual ("bin", new DirectoryInfo (Path.Combine (installed, "bin-link")).LinkTarget);
		Assert.AreEqual ("compiler", File.ReadAllText (Path.Combine (installed, "links", "compiler")));
		Assert.AreEqual ("compiler", File.ReadAllText (Path.Combine (installed, "bin-link", "clang")));
		Assert.IsNull (new FileInfo (Path.Combine (installed, "bin", "as")).LinkTarget);
		Assert.AreEqual ("/buildbot/bin/as", File.ReadAllText (Path.Combine (installed, "bin", "as")));
	}

	[TestCase ("../../outside")]
	[TestCase ("../pkg/data")] // climbs out of the install root and re-enters by its (archive-controlled) name
	[TestCase ("directory/../../outside")]
	[TestCase ("missing")]
	[TestCase ("link")]
	public void UnzipRejectsUnsafeUnixLinkTargets (string target)
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");
		using var temp = new TempDirectory ();
		using (var zip = ZipFile.Open (temp.ZipPath, ZipArchiveMode.Create)) {
			WriteEntry (zip, "pkg/data", "data");
			WriteEntry (zip, "pkg/link", target, Symlink);
		}

		Assert.Throws<InvalidDataException> (() => temp.Unzip ());
		Assert.IsFalse (File.Exists (Path.Combine (temp.Root, "outside")));
	}

	[Test]
	public void MoveRejectsExistingDestinationDirectorySymlink ()
	{
		if (OperatingSystem.IsWindows ())
			Assert.Ignore ("Unix symbolic links require Unix.");
		using var temp = new TempDirectory ();
		var source = Path.Combine (temp.Root, "source");
		var destination = Path.Combine (temp.Root, "destination");
		var outside = Path.Combine (temp.Root, "outside");
		Directory.CreateDirectory (Path.Combine (source, "pkg"));
		Directory.CreateDirectory (destination);
		Directory.CreateDirectory (outside);
		File.WriteAllText (Path.Combine (source, "pkg", "data"), "data");
		Directory.CreateSymbolicLink (Path.Combine (destination, "pkg"), outside);
		CommonUtilities.Helpers = new Helper ();

		Assert.Throws<IOException> (() => CommonUtilities.MoveDirectory (source, destination, move: false, overwrite: true));
		Assert.IsFalse (File.Exists (Path.Combine (outside, "data")));
		Assert.IsTrue (CommonUtilities.DeleteDirectoryRecursively (destination));
		Assert.IsTrue (Directory.Exists (outside));
	}

	const string ArchiveContents = "Android SDK package contents. Android SDK package contents. Android SDK package contents.";
	const string Symlink = "120777";

	sealed class TempDirectory : IDisposable
	{
		public string Root { get; } = Path.Combine (Path.GetTempPath (), "android-sdk-unzip-" + Guid.NewGuid ().ToString ("N"));
		public string ZipPath => Path.Combine (Root, "package.zip");
		public string OutPath => Path.Combine (Root, "out");

		public TempDirectory () => Directory.CreateDirectory (Root);

		public string Unzip () => new Helper ().Unzip (OutPath, ZipPath, null, null);

		public void Dispose ()
		{
			if (OperatingSystem.IsWindows ()) {
				foreach (var file in Directory.EnumerateFiles (Root, "*", SearchOption.AllDirectories))
					File.SetAttributes (file, FileAttributes.Normal);
			}
			Directory.Delete (Root, recursive: true);
		}
	}

	// ZipArchive records the Unix creator platform on Unix, so the mode is honored there.
	static void WriteEntry (ZipArchive zip, string name, string content, string unixMode = null)
	{
		var entry = zip.CreateEntry (name);
		if (unixMode != null)
			entry.ExternalAttributes = Convert.ToInt32 (unixMode, 8) << 16;
		using var writer = new StreamWriter (entry.Open ());
		writer.Write (content);
	}

	static byte [] CreateSimpleArchive ()
	{
		using var stream = new MemoryStream ();
		using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true))
			WriteEntry (zip, "pkg/data.txt", ArchiveContents);
		return stream.ToArray ();
	}

	static byte [] CreateArchiveWithEndRecordInComment ()
	{
		byte [] source = CreateSimpleArchive ();
		int end = FindSignature (source, 0x06054b50);
		var archive = new byte [source.Length + 25];
		Buffer.BlockCopy (source, 0, archive, 0, source.Length);
		WriteUInt16 (archive, end + 20, 25);
		Buffer.BlockCopy (source, end, archive, source.Length, 22);
		return archive;
	}

	// "zip64" is a valid ZIP64 archive; the other variants point the ZIP64 record at a second
	// central directory and mark a different end-record field as a ZIP64 sentinel.
	static byte [] CreateZip64Archive (string variant)
	{
		byte [] source = CreateSimpleArchive ();
		int end = FindSignature (source, 0x06054b50);
		uint centralSize = ReadUInt32 (source, end + 12);
		uint centralOffset = ReadUInt32 (source, end + 16);
		using var stream = new MemoryStream ();
		using var writer = new BinaryWriter (stream);
		writer.Write (source, 0, end);
		ulong zip64Offset = centralOffset;
		if (variant != "zip64") {
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
		if (variant == "disk") {
			WriteUInt16 (endRecord, 4, ushort.MaxValue);
			WriteUInt16 (endRecord, 6, ushort.MaxValue);
		} else if (variant == "size") {
			WriteUInt32 (endRecord, 12, uint.MaxValue);
		} else {
			WriteUInt16 (endRecord, 8, ushort.MaxValue);
			WriteUInt16 (endRecord, 10, ushort.MaxValue);
		}
		writer.Write (endRecord);
		return stream.ToArray ();
	}

	static byte [] AddExtendedTimestampToCentralDirectory (byte [] archive, long timestamp)
	{
		const int TimestampExtraFieldSize = 9;
		int endOfCentralDirectory = FindSignature (archive, 0x06054b50);
		int centralDirectoryOffset = checked ((int) ReadUInt32 (archive, endOfCentralDirectory + 16));
		int nameLength = ReadUInt16 (archive, centralDirectoryOffset + 28);
		int extraLength = ReadUInt16 (archive, centralDirectoryOffset + 30);
		int insertOffset = centralDirectoryOffset + 46 + nameLength + extraLength;
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
		int endOfCentralDirectory = FindSignature (archive, 0x06054b50);
		int centralDirectoryOffset = checked ((int) ReadUInt32 (archive, endOfCentralDirectory + 16));
		while (ReadUInt32 (archive, centralDirectoryOffset) == 0x02014b50) {
			int nameLength = ReadUInt16 (archive, centralDirectoryOffset + 28);
			int extraLength = ReadUInt16 (archive, centralDirectoryOffset + 30);
			int commentLength = ReadUInt16 (archive, centralDirectoryOffset + 32);
			if (Encoding.ASCII.GetString (archive, centralDirectoryOffset + 46, nameLength) == entryName) {
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

	static ushort ReadUInt16 (byte [] data, int position) => BinaryPrimitives.ReadUInt16LittleEndian (data.AsSpan (position));

	static uint ReadUInt32 (byte [] data, int position) => BinaryPrimitives.ReadUInt32LittleEndian (data.AsSpan (position));

	static void WriteUInt16 (byte [] data, int position, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian (data.AsSpan (position), value);

	static void WriteUInt32 (byte [] data, int position, uint value) => BinaryPrimitives.WriteUInt32LittleEndian (data.AsSpan (position), value);

	sealed class NonSeekableMemoryStream : MemoryStream
	{
		public override bool CanSeek => false;
		public override long Seek (long offset, SeekOrigin loc) => throw new NotSupportedException ();
	}
}
