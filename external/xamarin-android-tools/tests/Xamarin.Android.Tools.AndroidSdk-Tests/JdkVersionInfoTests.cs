// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class JdkVersionInfoTests
	{
		[Test]
		public void Constructor_SetsAllProperties ()
		{
			var info = new JdkVersionInfo (
				majorVersion: 21,
				displayName: "Microsoft OpenJDK 21",
				downloadUrl: "https://example.com/jdk-21.zip",
				checksumUrl: "https://example.com/jdk-21.zip.sha256sum.txt",
				size: 123456789,
				checksum: "abc123");

			Assert.AreEqual (21, info.MajorVersion);
			Assert.AreEqual ("Microsoft OpenJDK 21", info.DisplayName);
			Assert.AreEqual ("https://example.com/jdk-21.zip", info.DownloadUrl);
			Assert.AreEqual ("https://example.com/jdk-21.zip.sha256sum.txt", info.ChecksumUrl);
			Assert.AreEqual (123456789, info.Size);
			Assert.AreEqual ("abc123", info.Checksum);
		}

		[Test]
		public void Constructor_DefaultSizeAndChecksum ()
		{
			var info = new JdkVersionInfo (
				majorVersion: 17,
				displayName: "Microsoft OpenJDK 17",
				downloadUrl: "https://example.com/jdk-17.zip",
				checksumUrl: "https://example.com/jdk-17.zip.sha256sum.txt");

			Assert.AreEqual (0, info.Size);
			Assert.IsNull (info.Checksum);
		}

		[Test]
		public void ToString_ReturnsDisplayName ()
		{
			var info = new JdkVersionInfo (21, "Microsoft OpenJDK 21", "https://example.com/dl", "https://example.com/cs");
			Assert.AreEqual ("Microsoft OpenJDK 21", info.ToString ());
		}

		[Test]
		public void MutableProperties_CanBeSet ()
		{
			var info = new JdkVersionInfo (21, "Test", "https://example.com/dl", "https://example.com/cs");

			info.Size = 999;
			info.Checksum = "deadbeef";
			info.ResolvedUrl = "https://resolved.example.com/jdk-21.0.5.zip";

			Assert.AreEqual (999, info.Size);
			Assert.AreEqual ("deadbeef", info.Checksum);
			Assert.AreEqual ("https://resolved.example.com/jdk-21.0.5.zip", info.ResolvedUrl);
		}

		[Test]
		public void ResolvedUrl_DefaultsToNull ()
		{
			var info = new JdkVersionInfo (21, "Test", "https://example.com/dl", "https://example.com/cs");
			Assert.IsNull (info.ResolvedUrl);
		}
	}
}
