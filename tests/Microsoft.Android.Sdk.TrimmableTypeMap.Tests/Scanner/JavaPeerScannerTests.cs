using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (JavaPeerScannerTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	List<JavaPeerInfo> ScanFixtures ()
	{
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan (new [] { TestFixtureAssemblyPath });
	}

	JavaPeerInfo FindByJavaName (List<JavaPeerInfo> peers, string javaName)
	{
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	JavaPeerInfo FindByManagedName (List<JavaPeerInfo> peers, string managedName)
	{
		var peer = peers.FirstOrDefault (p => p.ManagedTypeName == managedName);
		Assert.NotNull (peer);
		return peer;
	}

	[Fact]
	public void Scan_FindsAllJavaPeerTypes ()
	{
		var peers = ScanFixtures ();
		Assert.NotEmpty (peers);
		Assert.Contains (peers, p => p.JavaName == "java/lang/Object");
		Assert.Contains (peers, p => p.JavaName == "android/app/Activity");
		Assert.Contains (peers, p => p.JavaName == "my/app/MainActivity");
	}

	[Theory]
	[InlineData ("android/app/Activity", true)]
	[InlineData ("android/widget/Button", true)]
	[InlineData ("my/app/MainActivity", false)]
	public void Scan_DoNotGenerateAcw (string javaName, bool expected)
	{
		var peers = ScanFixtures ();
		Assert.Equal (expected, FindByJavaName (peers, javaName).DoNotGenerateAcw);
	}

	[Theory]
	[InlineData ("my/app/MainActivity", true)]
	[InlineData ("my/app/MyService", true)]
	[InlineData ("my/app/MyReceiver", true)]
	[InlineData ("my/app/MyProvider", true)]
	[InlineData ("my/app/MyApplication", true)]
	[InlineData ("my/app/MyInstrumentation", true)]
	[InlineData ("my/app/MyBackupAgent", true)]
	[InlineData ("my/app/MyManageSpaceActivity", true)]
	[InlineData ("my/app/MyHelper", false)]
	[InlineData ("android/app/Activity", false)]
	public void Scan_IsUnconditional (string javaName, bool expected)
	{
		var peers = ScanFixtures ();
		Assert.Equal (expected, FindByJavaName (peers, javaName).IsUnconditional);
	}

	[Fact]
	public void Scan_TypeMetadata_IsCorrect ()
	{
		var peers = ScanFixtures ();
		Assert.True (FindByJavaName (peers, "my/app/AbstractBase").IsAbstract);
		Assert.True (FindByManagedName (peers, "Android.Views.IOnClickListener").IsInterface);
		Assert.False (FindByManagedName (peers, "Android.Views.IOnClickListener").DoNotGenerateAcw);

		var generic = FindByJavaName (peers, "my/app/GenericHolder");
		Assert.True (generic.IsGenericDefinition);
		Assert.Equal ("MyApp.Generic.GenericHolder`1", generic.ManagedTypeName);
	}

	[Fact]
	public void Scan_InvokerAndInterface_ShareJavaName ()
	{
		var peers = ScanFixtures ();
		var clickListenerPeers = peers.Where (p => p.JavaName == "android/view/View$OnClickListener").ToList ();
		Assert.Equal (2, clickListenerPeers.Count);
		Assert.Contains (clickListenerPeers, p => p.IsInterface);
		Assert.Contains (clickListenerPeers, p => p.DoNotGenerateAcw);
	}

	[Fact]
	public void Scan_AllTypes_HaveAssemblyName ()
	{
		var peers = ScanFixtures ();
		Assert.All (peers, peer =>
			Assert.False (string.IsNullOrEmpty (peer.AssemblyName),
				$"Type {peer.ManagedTypeName} should have assembly name"));
	}
}
