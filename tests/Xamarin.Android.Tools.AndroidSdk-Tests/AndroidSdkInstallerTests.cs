using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Xamarin.Installer.AndroidSDK;
using Xamarin.Installer.AndroidSDK.Manager;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
[NonParallelizable]
public class AndroidSdkInstallerTests
{
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
