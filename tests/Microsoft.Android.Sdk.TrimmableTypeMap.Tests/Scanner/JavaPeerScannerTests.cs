using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Java.Interop.Tools.JavaCallableWrappers;
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
	[InlineData ("MyApp.PlainActivitySubclass", "xx6403e39dfcc696a727/PlainActivitySubclass")]
	[InlineData ("MyApp.UnregisteredClickListener", "xx6403e39dfcc696a727/UnregisteredClickListener")]
	[InlineData ("MyApp.UnregisteredExporter", "xx6403e39dfcc696a727/UnregisteredExporter")]
	public void Scan_UnregisteredType_UsesHashedPackageName (string managedName, string expectedJavaName)
	{
		Assert.Equal (expectedJavaName, FindFixtureByManagedName (managedName).JavaName);
	}

	[Fact]
	public void Scan_UnregisteredType_LowercaseCrc64Policy_UsesLegacyCrc64Hash ()
	{
		const string managedName = "MyApp.PlainActivitySubclass";
		var withXxHash64 = FindFixtureByManagedName (managedName).JavaName;
		var withCrc64 = FindFixtureByManagedName (managedName, "LowercaseCrc64").JavaName;

		var data = Encoding.UTF8.GetBytes ("MyApp:TestFixtures");
		var expectedHash = string.Concat (Crc64Helper.Compute (data).Select (b => b.ToString ("x2")));
		Assert.Equal ($"crc64{expectedHash}/PlainActivitySubclass", withCrc64);
		Assert.StartsWith ("xx64", withXxHash64);
		Assert.NotEqual (withXxHash64, withCrc64);
	}
}
