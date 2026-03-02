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

namespace Xamarin.Android.Tools.Tests;
[TestFixture]
public class SdkManagerTests
{
	SdkManager manager;

	[SetUp]
	public void SetUp ()
	{
		manager = new SdkManager (logger: (level, message) => {
			TestContext.WriteLine ($"[{level}] {message}");
		});
	}

	[TearDown]
	public void TearDown ()
	{
		manager?.Dispose ();
		manager = null;
	}

	[Test]
	public void ParseManifest_CmdlineTools_ReturnsComponents ()
	{
		var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xamarin-android>
<cmdline-tools revision=""19.0"" path=""cmdline-tools;19.0"" filesystem-path=""cmdline-tools/latest"" description=""Android SDK Command-line Tools (latest)"" obsolete=""False"">
	<urls>
		<url host-os=""linux"" size=""164760899"" checksum-type=""sha1"" checksum=""5fdcc763663eefb86a5b8879697aa6088b041e70"">https://dl.google.com/android/repository/commandlinetools-linux-13114758_latest.zip</url>
		<url host-os=""macosx"" size=""143250852"" checksum-type=""sha1"" checksum=""c3e06a1959762e89167d1cbaa988605f6f7c1d24"">https://dl.google.com/android/repository/commandlinetools-mac-13114758_latest.zip</url>
		<url host-os=""windows"" size=""143040480"" checksum-type=""sha1"" checksum=""54a582f3bf73e04253602f2d1c80bd5868aac115"">https://dl.google.com/android/repository/commandlinetools-win-13114758_latest.zip</url>
	</urls>
</cmdline-tools>
<platform-tools revision=""36.0.0"" path=""platform-tools"" description=""Android SDK Platform-Tools"" obsolete=""False"">
	<urls>
		<url host-os=""linux"" size=""7904253"" checksum-type=""sha1"" checksum=""ddb0cd76d952d9a1f4c8a32e4ec0e73d7a8bebb8"">https://dl.google.com/android/repository/platform-tools_r36.0.0-linux.zip</url>
		<url host-os=""macosx"" size=""14186137"" checksum-type=""sha1"" checksum=""69ffc978ad66667c6b2eb7979a09f5af20f83aaa"">https://dl.google.com/android/repository/platform-tools_r36.0.0-darwin.zip</url>
		<url host-os=""windows"" size=""7138784"" checksum-type=""sha1"" checksum=""18bb505f9fbfbdf1e44fca4d794e74e01b63d30e"">https://dl.google.com/android/repository/platform-tools_r36.0.0-win.zip</url>
	</urls>
</platform-tools>
</xamarin-android>";

		var components = manager.ParseManifest (xml);

		Assert.IsNotNull (components);
		Assert.IsTrue (components.Count >= 2, $"Expected at least 2 components, got {components.Count}");

		var cmdline = components.FirstOrDefault (c => c.ElementName == "cmdline-tools");
		Assert.IsNotNull (cmdline, "Should find cmdline-tools component");
		Assert.AreEqual ("19.0", cmdline!.Revision);
		Assert.IsNotEmpty (cmdline.DownloadUrl!);
		Assert.IsNotEmpty (cmdline.Checksum!);
		Assert.AreEqual (ChecksumType.Sha1, cmdline.ChecksumType);
		Assert.Greater (cmdline.Size, 0);

		var platformTools = components.FirstOrDefault (c => c.ElementName == "platform-tools");
		Assert.IsNotNull (platformTools, "Should find platform-tools component");
		Assert.AreEqual ("36.0.0", platformTools!.Revision);
	}

	[Test]
	public void ParseManifest_ObsoleteComponents_AreIncluded ()
	{
		var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xamarin-android>
<cmdline-tools revision=""9.0"" path=""cmdline-tools;9.0"" description=""Old tools"" obsolete=""True"">
	<urls>
		<url host-os=""linux"" size=""100"" checksum-type=""sha1"" checksum=""abc123"">https://example.com/old.zip</url>
		<url host-os=""macosx"" size=""100"" checksum-type=""sha1"" checksum=""abc123"">https://example.com/old-mac.zip</url>
		<url host-os=""windows"" size=""100"" checksum-type=""sha1"" checksum=""abc123"">https://example.com/old-win.zip</url>
	</urls>
</cmdline-tools>
</xamarin-android>";

		var components = manager.ParseManifest (xml);
		var obsolete = components.FirstOrDefault (c => c.Revision == "9.0");
		Assert.IsNotNull (obsolete);
		Assert.IsTrue (obsolete!.IsObsolete);
	}

	[Test]
	public void ParseManifest_EmptyXml_ReturnsEmpty ()
	{
		var xml = @"<?xml version=""1.0"" encoding=""utf-8""?><xamarin-android></xamarin-android>";
		var components = manager.ParseManifest (xml);
		Assert.IsNotNull (components);
		Assert.AreEqual (0, components.Count);
	}

	[Test]
	public void ParseManifest_MultipleVersions_AllReturned ()
	{
		var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xamarin-android>
<cmdline-tools revision=""19.0"" path=""cmdline-tools;19.0"" description=""Latest"" obsolete=""False"">
	<urls>
		<url host-os=""linux"" size=""164760899"" checksum-type=""sha1"" checksum=""aaa"">https://example.com/19.zip</url>
		<url host-os=""macosx"" size=""143250852"" checksum-type=""sha1"" checksum=""bbb"">https://example.com/19-mac.zip</url>
		<url host-os=""windows"" size=""143040480"" checksum-type=""sha1"" checksum=""ccc"">https://example.com/19-win.zip</url>
	</urls>
</cmdline-tools>
<cmdline-tools revision=""17.0"" path=""cmdline-tools;17.0"" description=""Older"" obsolete=""False"">
	<urls>
		<url host-os=""linux"" size=""163426701"" checksum-type=""sha1"" checksum=""ddd"">https://example.com/17.zip</url>
		<url host-os=""macosx"" size=""142285185"" checksum-type=""sha1"" checksum=""eee"">https://example.com/17-mac.zip</url>
		<url host-os=""windows"" size=""141848100"" checksum-type=""sha1"" checksum=""fff"">https://example.com/17-win.zip</url>
	</urls>
</cmdline-tools>
</xamarin-android>";

		var components = manager.ParseManifest (xml);
		var cmdlineTools = components.Where (c => c.ElementName == "cmdline-tools").ToList ();
		Assert.AreEqual (2, cmdlineTools.Count, "Should find both cmdline-tools versions");
	}

	[Test]
	public void ParseManifest_JdkElements_Parsed ()
	{
		var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xamarin-android>
<jdk revision=""21.0.9"" description=""Open JDK (Microsoft distribution)"" obsolete=""False"">
	<urls>
		<url host-os=""windows"" host-arch=""x64"" size=""200938696"" checksum-type=""sha1"" checksum=""2fb084"">https://aka.ms/download-jdk/microsoft-jdk-21.0.9-windows-x64.zip</url>
		<url host-os=""macosx"" host-arch=""x64"" size=""202224829"" checksum-type=""sha1"" checksum=""92cb38"">https://aka.ms/download-jdk/microsoft-jdk-21.0.9-macOS-x64.tar.gz</url>
		<url host-os=""macosx"" host-arch=""aarch64"" size=""199876000"" checksum-type=""sha1"" checksum=""0bd46d"">https://aka.ms/download-jdk/microsoft-jdk-21.0.9-macOS-aarch64.tar.gz</url>
		<url host-os=""linux"" host-arch=""x64"" size=""206305105"" checksum-type=""sha1"" checksum=""7db701"">https://aka.ms/download-jdk/microsoft-jdk-21.0.9-linux-x64.tar.gz</url>
	</urls>
</jdk>
</xamarin-android>";

		var components = manager.ParseManifest (xml);
		var jdk = components.FirstOrDefault (c => c.ElementName == "jdk");
		Assert.IsNotNull (jdk, "Should find jdk component");
		Assert.AreEqual ("21.0.9", jdk!.Revision);
		Assert.IsNotEmpty (jdk.DownloadUrl!);
	}

	[Test]
	public void ParseSdkManagerList_ParsesInstalledAndAvailable ()
	{
		var output = @"Installed packages:
  Path                 | Version | Description                    | Location
  -------              | ------- | -------                        | -------
  build-tools;35.0.0   | 35.0.0  | Android SDK Build-Tools 35     | build-tools/35.0.0
  emulator             | 35.3.10 | Android Emulator               | emulator
  platform-tools       | 36.0.0  | Android SDK Platform-Tools     | platform-tools

Available Packages:
  Path                               | Version | Description
  -------                            | ------- | -------
  build-tools;36.0.0                 | 36.0.0  | Android SDK Build-Tools 36
  platforms;android-35               | 5       | Android SDK Platform 35
  system-images;android-35;google_apis;arm64-v8a | 14 | Google APIs ARM 64 v8a System Image

Available Updates:
  Path         | Installed | Available
  platform-tools | 35.0.2  | 36.0.0
";

		var (installed, available) = SdkManager.ParseSdkManagerList (output);

		Assert.AreEqual (3, installed.Count, "Should have 3 installed packages");
		Assert.AreEqual (3, available.Count, "Should have 3 available packages");

		var platformTools = installed.FirstOrDefault (p => p.Path == "platform-tools");
		Assert.IsNotNull (platformTools);
		Assert.AreEqual ("36.0.0", platformTools!.Version);
		Assert.IsTrue (platformTools.IsInstalled);

		var buildTools36 = available.FirstOrDefault (p => p.Path == "build-tools;36.0.0");
		Assert.IsNotNull (buildTools36);
		Assert.AreEqual ("36.0.0", buildTools36!.Version);
		Assert.IsFalse (buildTools36.IsInstalled);
	}

	[Test]
	public void ParseSdkManagerList_EmptyOutput_ReturnsEmpty ()
	{
		var (installed, available) = SdkManager.ParseSdkManagerList ("");
		Assert.AreEqual (0, installed.Count);
		Assert.AreEqual (0, available.Count);
	}

	[Test]
	public void ParseSdkManagerList_OnlyInstalledSection ()
	{
		var output = @"Installed packages:
  Path                 | Version | Description
  -------              | ------- | -------
  platform-tools       | 36.0.0  | Android SDK Platform-Tools
";

		var (installed, available) = SdkManager.ParseSdkManagerList (output);
		Assert.AreEqual (1, installed.Count);
		Assert.AreEqual (0, available.Count);
		Assert.AreEqual ("platform-tools", installed[0].Path);
	}

	[Test]
	public void FindSdkManagerPath_NullSdkPath_ReturnsNull ()
	{
		manager.AndroidSdkPath = null;
		Assert.IsNull (manager.FindSdkManagerPath ());
	}

	[Test]
	public void FindSdkManagerPath_CmdlineToolsLatest_Found ()
	{
		var sdkDir = Path.Combine (Path.GetTempPath (), $"sdk-test-{Guid.NewGuid ()}");
		try {
			var binDir = Path.Combine (sdkDir, "cmdline-tools", "latest", "bin");
			Directory.CreateDirectory (binDir);

			var sdkManagerName = OS.IsWindows ? "sdkmanager.bat" : "sdkmanager";
			File.WriteAllText (Path.Combine (binDir, sdkManagerName), "#!/bin/sh\necho test");

			manager.AndroidSdkPath = sdkDir;
			var result = manager.FindSdkManagerPath ();

			Assert.IsNotNull (result, "Should find sdkmanager in cmdline-tools/latest/bin");
			Assert.That (result, Does.Contain ("sdkmanager"));
		}
		finally {
			if (Directory.Exists (sdkDir))
				Directory.Delete (sdkDir, recursive: true);
		}
	}

	[Test]
	public void FindSdkManagerPath_VersionedDir_Found ()
	{
		var sdkDir = Path.Combine (Path.GetTempPath (), $"sdk-test-{Guid.NewGuid ()}");
		try {
			var binDir = Path.Combine (sdkDir, "cmdline-tools", "12.0", "bin");
			Directory.CreateDirectory (binDir);

			var sdkManagerName = OS.IsWindows ? "sdkmanager.bat" : "sdkmanager";
			File.WriteAllText (Path.Combine (binDir, sdkManagerName), "#!/bin/sh\necho test");

			manager.AndroidSdkPath = sdkDir;
			var result = manager.FindSdkManagerPath ();

			Assert.IsNotNull (result, "Should find sdkmanager in versioned dir");
		}
		finally {
			if (Directory.Exists (sdkDir))
				Directory.Delete (sdkDir, recursive: true);
		}
	}

	[Test]
	public void FindSdkManagerPath_LegacyToolsDir_Found ()
	{
		var sdkDir = Path.Combine (Path.GetTempPath (), $"sdk-test-{Guid.NewGuid ()}");
		try {
			var binDir = Path.Combine (sdkDir, "tools", "bin");
			Directory.CreateDirectory (binDir);

			var sdkManagerName = OS.IsWindows ? "sdkmanager.bat" : "sdkmanager";
			File.WriteAllText (Path.Combine (binDir, sdkManagerName), "#!/bin/sh\necho test");

			manager.AndroidSdkPath = sdkDir;
			var result = manager.FindSdkManagerPath ();

			Assert.IsNotNull (result, "Should find sdkmanager in legacy tools/bin");
		}
		finally {
			if (Directory.Exists (sdkDir))
				Directory.Delete (sdkDir, recursive: true);
		}
	}

	[Test]
	public void FindSdkManagerPath_NoSdkManager_ReturnsNull ()
	{
		var sdkDir = Path.Combine (Path.GetTempPath (), $"sdk-test-{Guid.NewGuid ()}");
		try {
			Directory.CreateDirectory (sdkDir);
			manager.AndroidSdkPath = sdkDir;
			Assert.IsNull (manager.FindSdkManagerPath ());
		}
		finally {
			if (Directory.Exists (sdkDir))
				Directory.Delete (sdkDir, recursive: true);
		}
	}

	[Test]
	public void DefaultManifestFeedUrl_IsSet ()
	{
		Assert.AreEqual ("https://aka.ms/AndroidManifestFeed/d18-0", SdkManager.DefaultManifestFeedUrl);
		Assert.AreEqual (SdkManager.DefaultManifestFeedUrl, manager.ManifestFeedUrl);
	}

	[Test]
	public void ManifestFeedUrl_IsConfigurable ()
	{
		manager.ManifestFeedUrl = "https://example.com/manifest.xml";
		Assert.AreEqual ("https://example.com/manifest.xml", manager.ManifestFeedUrl);
	}

	[Test]
	public void Constructor_DefaultLogger_DoesNotThrow ()
	{
		var defaultManager = new SdkManager ();
		Assert.IsNotNull (defaultManager);
	}

	// --- AreLicensesAccepted ---

	[Test]
	public void AreLicensesAccepted_NullSdkPath_ReturnsFalse ()
	{
		manager.AndroidSdkPath = null;
		Assert.IsFalse (manager.AreLicensesAccepted ());
	}

	[Test]
	public void AreLicensesAccepted_NoLicensesDir_ReturnsFalse ()
	{
		var sdkDir = Path.Combine (Path.GetTempPath (), $"sdk-test-{Guid.NewGuid ()}");
		try {
			Directory.CreateDirectory (sdkDir);
			manager.AndroidSdkPath = sdkDir;
			Assert.IsFalse (manager.AreLicensesAccepted ());
		}
		finally {
			if (Directory.Exists (sdkDir))
				Directory.Delete (sdkDir, recursive: true);
		}
	}

	[Test]
	public void AreLicensesAccepted_WithLicenseFiles_ReturnsTrue ()
	{
		var sdkDir = Path.Combine (Path.GetTempPath (), $"sdk-test-{Guid.NewGuid ()}");
		try {
			var licensesDir = Path.Combine (sdkDir, "licenses");
			Directory.CreateDirectory (licensesDir);
			File.WriteAllText (Path.Combine (licensesDir, "android-sdk-license"), "abc123");

			manager.AndroidSdkPath = sdkDir;
			Assert.IsTrue (manager.AreLicensesAccepted ());
		}
		finally {
			if (Directory.Exists (sdkDir))
				Directory.Delete (sdkDir, recursive: true);
		}
	}

	[Test]
	public async Task GetManifestComponentsAsync_ReturnsComponents ()
	{
		IReadOnlyList<SdkManifestComponent> components;
		try {
			components = await manager.GetManifestComponentsAsync ();
		}
		catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException) {
			Assert.Ignore ($"Network unavailable: {ex.Message}");
			return;
		}

		Assert.IsNotNull (components);
		if (components.Count == 0) {
			Assert.Ignore ("No components returned.");
			return;
		}

		// Should find cmdline-tools
		var cmdline = components.FirstOrDefault (c => c.ElementName == "cmdline-tools");
		Assert.IsNotNull (cmdline, "Manifest should contain cmdline-tools");
		Assert.IsNotEmpty (cmdline!.DownloadUrl!);
		Assert.IsNotEmpty (cmdline.Checksum!);

		// Should find platform-tools
		var platformTools = components.FirstOrDefault (c => c.ElementName == "platform-tools");
		Assert.IsNotNull (platformTools, "Manifest should contain platform-tools");
	}

	[Test]
	public async Task BootstrapAsync_NullPath_Throws ()
	{
		Assert.ThrowsAsync<ArgumentNullException> (
			async () => await manager.BootstrapAsync (null!));
	}

	[Test]
	public void InstallAsync_NoSdkManager_Throws ()
	{
		manager.AndroidSdkPath = Path.Combine (Path.GetTempPath (), "nonexistent");
		Assert.ThrowsAsync<InvalidOperationException> (
			async () => await manager.InstallAsync (new[] { "platform-tools" }));
	}

	[Test]
	public void InstallAsync_EmptyPackages_Throws ()
	{
		Assert.ThrowsAsync<ArgumentException> (
			async () => await manager.InstallAsync (new string[0]));
	}

	[Test]
	public void InstallAsync_NullPackages_Throws ()
	{
		Assert.ThrowsAsync<ArgumentException> (
			async () => await manager.InstallAsync (null!));
	}

	[Test]
	public void UninstallAsync_NoSdkManager_Throws ()
	{
		manager.AndroidSdkPath = Path.Combine (Path.GetTempPath (), "nonexistent");
		Assert.ThrowsAsync<InvalidOperationException> (
			async () => await manager.UninstallAsync (new[] { "platform-tools" }));
	}

	[Test]
	public void UninstallAsync_EmptyPackages_Throws ()
	{
		Assert.ThrowsAsync<ArgumentException> (
			async () => await manager.UninstallAsync (new string[0]));
	}

	[Test]
	public void ListAsync_NoSdkManager_Throws ()
	{
		manager.AndroidSdkPath = Path.Combine (Path.GetTempPath (), "nonexistent");
		Assert.ThrowsAsync<InvalidOperationException> (
			async () => await manager.ListAsync ());
	}

	[Test]
	public void UpdateAsync_NoSdkManager_Throws ()
	{
		manager.AndroidSdkPath = Path.Combine (Path.GetTempPath (), "nonexistent");
		Assert.ThrowsAsync<InvalidOperationException> (
			async () => await manager.UpdateAsync ());
	}

	[Test]
	public void AcceptLicensesAsync_NoSdkManager_Throws ()
	{
		manager.AndroidSdkPath = Path.Combine (Path.GetTempPath (), "nonexistent");
		Assert.ThrowsAsync<InvalidOperationException> (
			async () => await manager.AcceptLicensesAsync ());
	}

	// --- License Parsing ---

	[Test]
	public void ParseLicenseOutput_SingleLicense_Parsed ()
	{
		var output = @"
License android-sdk-license:
---------------------------------------
Terms and Conditions

This is the license text.

---------------------------------------
Accept? (y/N): ";

		var licenses = SdkManager.ParseLicenseOutput (output);

		Assert.AreEqual (1, licenses.Count, "Should parse one license");
		Assert.AreEqual ("android-sdk-license", licenses[0].Id);
		Assert.That (licenses[0].Text, Does.Contain ("Terms and Conditions"));
		Assert.That (licenses[0].Text, Does.Contain ("This is the license text"));
	}

	[Test]
	public void ParseLicenseOutput_MultipleLicenses_Parsed ()
	{
		var output = @"
License android-sdk-license:
---------------------------------------
SDK License Text
---------------------------------------
Accept? (y/N): n
License android-sdk-preview-license:
---------------------------------------
Preview License Text
---------------------------------------
Accept? (y/N): ";

		var licenses = SdkManager.ParseLicenseOutput (output);

		Assert.AreEqual (2, licenses.Count, "Should parse two licenses");
		Assert.AreEqual ("android-sdk-license", licenses[0].Id);
		Assert.AreEqual ("android-sdk-preview-license", licenses[1].Id);
	}

	[Test]
	public void ParseLicenseOutput_NoLicenses_ReturnsEmpty ()
	{
		var output = "All SDK package licenses accepted.";

		var licenses = SdkManager.ParseLicenseOutput (output);

		Assert.AreEqual (0, licenses.Count, "Should return empty list");
	}
}
