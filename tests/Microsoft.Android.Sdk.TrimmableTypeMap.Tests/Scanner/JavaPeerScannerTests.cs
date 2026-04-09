using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests : FixtureTestBase
{
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
		Assert.Equal (expected, FindFixtureByJavaName (javaName).DoNotGenerateAcw);
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
		Assert.Equal (expected, FindFixtureByJavaName (javaName).IsUnconditional);
	}

	[Fact]
	public void Scan_TypeMetadata_IsCorrect ()
	{
		Assert.True (FindFixtureByJavaName ("my/app/AbstractBase").IsAbstract);
		Assert.True (FindFixtureByManagedName ("Android.Views.IOnClickListener").IsInterface);
		Assert.False (FindFixtureByManagedName ("Android.Views.IOnClickListener").DoNotGenerateAcw);

		var generic = FindFixtureByJavaName ("my/app/GenericHolder");
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

	[Fact]
	public void Scan_RegisterAttribute_DotFormat_NormalizedToSlashes ()
	{
		// [Register ("com.example.dotformat.MainActivity")] uses dots (Java class name format)
		// — the scanner must normalize to slashes (JNI format).
		// Reproduces the crash from Tests/Xamarin.ProjectTools/Resources/DotNet/MainActivity.cs
		// where the template expands to [Register ("${JAVA_PACKAGENAME}.MainActivity"), Activity (...)].
		var peer = FindFixtureByJavaName ("com/example/dotformat/MainActivity");
		Assert.Equal ("com/example/dotformat/MainActivity", peer.JavaName);
		Assert.Equal ("com/example/dotformat/MainActivity", peer.CompatJniName);
		Assert.False (peer.DoNotGenerateAcw);
		Assert.True (peer.IsUnconditional, "Should be unconditional due to [Activity]");
	}
	[Theory]
	[InlineData ("MyApp.PlainActivitySubclass")]
	[InlineData ("MyApp.UnregisteredClickListener")]
	[InlineData ("MyApp.UnregisteredExporter")]
	public void Scan_UnregisteredType_UsesCrc64PackageName (string managedName)
	{
		var testAssemblyDir = Path.GetDirectoryName (typeof (FixtureTestBase).Assembly.Location)
			?? throw new InvalidOperationException ("Cannot determine test assembly directory");
		var fixtureAssemblyPath = Path.Combine (testAssemblyDir, "TestFixtures.dll");
		var fixtureAssembly = Assembly.LoadFrom (fixtureAssemblyPath);
		var fixtureType = fixtureAssembly.GetType (managedName);
		if (fixtureType is null) {
			throw new InvalidOperationException ($"Could not load fixture type '{managedName}' from '{fixtureAssemblyPath}'.");
		}

		var assemblyName = fixtureType.Assembly.GetName ().Name
			?? throw new InvalidOperationException ($"Could not determine assembly name for '{managedName}'.");
		var data = Encoding.UTF8.GetBytes ($"{fixtureType.Namespace}:{assemblyName}");
		var hash = System.IO.Hashing.Crc64.Hash (data);
		var expectedJavaName = $"crc64{BitConverter.ToString (hash).Replace ("-", "").ToLowerInvariant ()}/{fixtureType.Name}";

		Assert.Equal (expectedJavaName, FindFixtureByManagedName (managedName).JavaName);
	}
}
