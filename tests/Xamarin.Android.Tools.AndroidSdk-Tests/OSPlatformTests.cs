// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class OSPlatformTests
{
	[Test]
	public void PlatformClassification_MatchesRuntimePlatform ()
	{
		var isWindows = RuntimeInformation.IsOSPlatform (OSPlatform.Windows);
		var isMac = RuntimeInformation.IsOSPlatform (OSPlatform.OSX) ||
			RuntimeInformation.IsOSPlatform (OSPlatform.Create ("MACCATALYST")) ||
			RuntimeInformation.IsOSPlatform (OSPlatform.Create ("IOS")) ||
			RuntimeInformation.IsOSPlatform (OSPlatform.Create ("TVOS")) ||
			RuntimeInformation.IsOSPlatform (OSPlatform.Create ("WATCHOS"));
		var isLinux = !isWindows && !isMac;
		var expectedNativeLibraryFormat = isWindows ? "{0}.dll" : isMac ? "lib{0}.dylib" : "lib{0}.so";

		Assert.AreEqual (isWindows, OS.IsWindows);
		Assert.AreEqual (isMac, OS.IsMac);
		Assert.AreEqual (isLinux, OS.IsLinux);
		Assert.AreEqual (expectedNativeLibraryFormat, OS.NativeLibraryFormat);
	}
}
