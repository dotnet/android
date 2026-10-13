using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.Installer.AndroidSDK;
using Xamarin.Installer.AndroidSDK.Manager;
using Xamarin.Installer.Common;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
[NonParallelizable]
public class AndroidSdkInstallerTests
{
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
