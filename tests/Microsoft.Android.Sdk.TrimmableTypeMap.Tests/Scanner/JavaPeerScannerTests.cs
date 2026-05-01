using System;
using System.Collections.Generic;
using System.Linq;
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
	public void Scan_ContentProvider_CapturesAuthorities ()
	{
		var provider = FindFixtureByJavaName ("my/app/MyProvider");
		var component = provider.ComponentAttribute;
		Assert.NotNull (component);
		Assert.Equal (ComponentKind.ContentProvider, component.Kind);
		Assert.True (component.Properties.TryGetValue ("Authorities", out var authorities));
		Assert.Equal ("my.app.provider", authorities);
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
	[InlineData ("MyApp.PlainActivitySubclass", "crc64eb3df85c64aa1af6/PlainActivitySubclass")]
	[InlineData ("MyApp.UnregisteredClickListener", "crc64eb3df85c64aa1af6/UnregisteredClickListener")]
	[InlineData ("MyApp.UnregisteredExporter", "crc64eb3df85c64aa1af6/UnregisteredExporter")]
	public void Scan_UnregisteredType_UsesCrc64PackageName (string managedName, string expectedJavaName)
	{
		Assert.Equal (expectedJavaName, FindFixtureByManagedName (managedName).JavaName);
	}

	[Fact]
	public void Scan_JniTypeSignature_IsDiscovered ()
	{
		var peer = FindFixtureByJavaName ("net/dot/jni/test/JavaDisposedObject");
		Assert.Equal ("Java.Interop.TestTypes.JavaDisposedObject", peer.ManagedTypeName);
		Assert.False (peer.DoNotGenerateAcw, "GenerateJavaPeer=true should map to DoNotGenerateAcw=false");
	}

	[Fact]
	public void Scan_JniTypeSignature_DoNotGenerateAcw ()
	{
		var nonGenerated = FindFixtureByJavaName ("net/dot/jni/test/MyJavaObject");
		Assert.True (nonGenerated.DoNotGenerateAcw, "NonGeneratedJavaObject has GenerateJavaPeer=false");
	}

	[Fact]
	public void Scan_JniTypeSignature_DuplicateJniName_BothPresent ()
	{
		// Java.Interop.TestTypes.JavaObject has [JniTypeSignature("java/lang/Object", GenerateJavaPeer=false)]
		// and Java.Lang.Object has [Register("java/lang/Object", DoNotGenerateAcw=true)].
		// Both should be present in the scan results — alias support handles the runtime deduplication.
		var peers = ScanFixtures ();
		var javaObjectPeers = peers.Where (p => p.JavaName == "java/lang/Object").ToList ();
		Assert.Equal (2, javaObjectPeers.Count);
	}

	[Fact]
	public void Scan_JniTypeSignature_SubclassExtendsJavaPeer ()
	{
		// JavaDisposedObject extends JavaObject which has [JniTypeSignature(GenerateJavaPeer=false)].
		var peer = FindFixtureByJavaName ("net/dot/jni/test/JavaDisposedObject");
		Assert.NotNull (peer);
	}

	[Fact]
	public void Scan_JniTypeSignature_ArrayRank_IsExcluded ()
	{
		// Types with [JniTypeSignature(ArrayRank > 0)] represent JNI array wrappers
		// (e.g., JavaBooleanArray with IsKeyword=true, or JavaObjectArray<T> without).
		// The scanner must skip all of them — they are handled by the built-in tables
		// in JniRuntime.JniTypeManager, not the typemap.
		var peers = ScanFixtures ();

		// Keyword primitive array (e.g., JavaBooleanArray with "Z")
		Assert.DoesNotContain (peers, p => p.ManagedTypeName == "Java.Interop.TestTypes.KeywordPrimitiveArray");

		// Non-keyword array (e.g., JavaObjectArray<T> with "java/lang/Object", ArrayRank=1)
		Assert.DoesNotContain (peers, p => p.ManagedTypeName == "Java.Interop.TestTypes.NonKeywordArrayType");
	}
}
