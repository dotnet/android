using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class TrimmableTypeMapGeneratorTests : FixtureTestBase
{
	readonly List<string> logMessages = new ();

	sealed class TestTrimmableTypeMapLogger (List<string> logMessages, List<string>? warnings = null) : ITrimmableTypeMapLogger
	{
		public void LogNoJavaPeerTypesFound () =>
			logMessages.Add ("No Java peer types found, skipping typemap generation.");
		public void LogJavaPeerScanInfo (int assemblyCount, int peerCount) =>
			logMessages.Add ($"Scanned {assemblyCount} assemblies, found {peerCount} Java peer types.");
		public void LogGeneratingJcwFilesInfo (int jcwPeerCount, int totalPeerCount) =>
			logMessages.Add ($"Generating JCW files for {jcwPeerCount} types (filtered from {totalPeerCount} total).");
		public void LogDeferredRegistrationTypesInfo (int typeCount) =>
			logMessages.Add ($"Found {typeCount} Application/Instrumentation types for deferred registration.");
		public void LogGeneratedTypeMapAssemblyInfo (string assemblyName, int typeCount) =>
			logMessages.Add ($"  {assemblyName}: {typeCount} types");
		public void LogGeneratedRootTypeMapInfo (int assemblyReferenceCount) =>
			logMessages.Add ($"  Root: {assemblyReferenceCount} per-assembly refs");
		public void LogGeneratedTypeMapAssembliesInfo (int assemblyCount) =>
			logMessages.Add ($"Generated {assemblyCount} typemap assemblies.");
		public void LogGeneratedJcwFilesInfo (int sourceCount) =>
			logMessages.Add ($"Generated {sourceCount} JCW Java source files.");
		public void LogRootingManifestReferencedTypeInfo (string javaTypeName, string managedTypeName) =>
			logMessages.Add ($"Rooting manifest-referenced type '{javaTypeName}' ({managedTypeName}) as unconditional.");
		public void LogManifestReferencedTypeNotFoundWarning (string javaTypeName) =>
			warnings?.Add ($"Manifest-referenced type '{javaTypeName}' was not found in any scanned assembly. It may be a framework type.");
	}

	[Fact]
	public void Execute_EmptyAssemblyList_ReturnsEmptyResults ()
	{
		var result = CreateGenerator ().Execute ([], new Version (11, 0), new HashSet<string> ());
		Assert.Empty (result.GeneratedAssemblies);
		Assert.Empty (result.GeneratedJavaSources);
		Assert.Empty (result.AllPeers);
		Assert.Contains (logMessages, m => m.Contains ("No Java peer types found"));
	}

	[Fact]
	public void Execute_AssemblyWithNoPeers_ReturnsEmpty ()
	{
		// Use the test assembly itself — it has no [Register] types
		var testAssemblyPath = typeof (TrimmableTypeMapGeneratorTests).Assembly.Location;
		using var peReader = new PEReader (File.OpenRead (testAssemblyPath));
		var result = CreateGenerator ().Execute (
			new List<(string, PEReader)> { ("TestAssembly", peReader) },
			new Version (11, 0),
			new HashSet<string> ());
		Assert.Empty (result.GeneratedAssemblies);
		Assert.Empty (result.GeneratedJavaSources);
		Assert.Contains (logMessages, m => m.Contains ("No Java peer types found"));
	}

	[Fact]
	public void Execute_WithTestFixtures_ProducesOutputs ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());
		Assert.NotEmpty (result.GeneratedAssemblies);
		Assert.NotEmpty (result.GeneratedJavaSources);
		Assert.Contains (result.GeneratedAssemblies, a => a.Name == "_Microsoft.Android.TypeMaps");
		Assert.Contains (result.GeneratedAssemblies, a => a.Name == "_TestFixtures.TypeMap");
	}

	[Fact]
	public void Execute_CollectsDeferredRegistrationTypes_ForAllApplicationAndInstrumentationSubtypes ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());

		// Abstract Instrumentation/Application subtypes are included too: their native
		// methods (e.g. n_OnCreate, n_OnStart) are declared on the abstract base class
		// and must be registered via ApplicationRegistration.registerApplications ().
		Assert.Contains ("my.app.MyApplication", result.ApplicationRegistrationTypes);
		Assert.Contains ("my.app.MyInstrumentation", result.ApplicationRegistrationTypes);
		Assert.Contains ("my.app.BaseApplication", result.ApplicationRegistrationTypes);
		Assert.Contains ("my.app.BaseInstrumentation", result.ApplicationRegistrationTypes);
		Assert.Contains ("my.app.IntermediateInstrumentation", result.ApplicationRegistrationTypes);
	}

	[Fact]
	public void CollectApplicationRegistrationTypes_ExcludesLegacyFrameworkDescendants ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "android/app/Application", CompatJniName = "android.app.Application",
				ManagedTypeName = "Android.App.Application", ManagedTypeNamespace = "Android.App", ManagedTypeShortName = "Application",
				AssemblyName = "Mono.Android", DoNotGenerateAcw = true, CannotRegisterInStaticConstructor = true,
			},
			new JavaPeerInfo {
				JavaName = "android/app/Instrumentation", CompatJniName = "android.app.Instrumentation",
				ManagedTypeName = "Android.App.Instrumentation", ManagedTypeNamespace = "Android.App", ManagedTypeShortName = "Instrumentation",
				AssemblyName = "Mono.Android", DoNotGenerateAcw = true, CannotRegisterInStaticConstructor = true,
			},
			new JavaPeerInfo {
				JavaName = "android/test/InstrumentationTestRunner", CompatJniName = "android.test.InstrumentationTestRunner",
				ManagedTypeName = "Android.Test.InstrumentationTestRunner", ManagedTypeNamespace = "Android.Test", ManagedTypeShortName = "InstrumentationTestRunner",
				AssemblyName = "Mono.Android", BaseJavaName = "android/app/Instrumentation", DoNotGenerateAcw = true, CannotRegisterInStaticConstructor = true,
			},
			new JavaPeerInfo {
				JavaName = "android/test/mock/MockApplication", CompatJniName = "android.test.mock.MockApplication",
				ManagedTypeName = "Android.Test.Mock.MockApplication", ManagedTypeNamespace = "Android.Test.Mock", ManagedTypeShortName = "MockApplication",
				AssemblyName = "Mono.Android", BaseJavaName = "android/app/Application", DoNotGenerateAcw = true, CannotRegisterInStaticConstructor = true,
			},
			new JavaPeerInfo {
				JavaName = "my/app/BaseInstrumentation", CompatJniName = "my.app.BaseInstrumentation",
				ManagedTypeName = "My.App.BaseInstrumentation", ManagedTypeNamespace = "My.App", ManagedTypeShortName = "BaseInstrumentation",
				AssemblyName = "MyApp", IsAbstract = true, CannotRegisterInStaticConstructor = true,
			},
			new JavaPeerInfo {
				JavaName = "my/app/MyInstrumentation", CompatJniName = "my.app.MyInstrumentation",
				ManagedTypeName = "My.App.MyInstrumentation", ManagedTypeNamespace = "My.App", ManagedTypeShortName = "MyInstrumentation",
				AssemblyName = "MyApp", BaseJavaName = "my/app/BaseInstrumentation", CannotRegisterInStaticConstructor = true,
			},
		};

		var types = TrimmableTypeMapGenerator.CollectApplicationRegistrationTypes (peers);

		Assert.Contains ("android.app.Application", types);
		Assert.Contains ("android.app.Instrumentation", types);
		Assert.Contains ("my.app.BaseInstrumentation", types);
		Assert.Contains ("my.app.MyInstrumentation", types);
		Assert.DoesNotContain ("android.test.InstrumentationTestRunner", types);
		Assert.DoesNotContain ("android.test.mock.MockApplication", types);
	}

	[Fact]
	public void Execute_NullAssemblyList_Throws ()
	{
		IReadOnlyList<(string Name, PEReader Reader)>? n = null;
#pragma warning disable CS8604
		Assert.Throws<ArgumentNullException> (() => CreateGenerator ().Execute (n, new Version (11, 0), new HashSet<string> ()));
#pragma warning restore CS8604
	}

	[Fact]
	public void Execute_GeneratedAssembliesAreValidPE ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());
		foreach (var assembly in result.GeneratedAssemblies) {
			assembly.Content.Position = 0;
			using var vr = new PEReader (assembly.Content, PEStreamOptions.LeaveOpen);
			var md = vr.GetMetadataReader ();
			Assert.Equal (assembly.Name, md.GetString (md.GetAssemblyDefinition ().Name));
		}
	}

	[Fact]
	public void Execute_JavaSourcesHaveCorrectStructure ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());
		foreach (var source in result.GeneratedJavaSources)
			Assert.Contains ("class ", source.Content);
	}

	[Fact]
	public void Execute_ManifestPlaceholdersAreResolvedBeforeRooting ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var manifestTemplate = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="${applicationId}">
			  <application>
			    <activity android:name=".SimpleActivity" />
			  </application>
			</manifest>
			""");

		var result = CreateGenerator ().Execute (
			new List<(string, PEReader)> { ("TestFixtures", peReader) },
			new Version (11, 0),
			new HashSet<string> (),
			useSharedTypemapUniverse: false,
			new ManifestConfig (
				PackageName: "my.app",
				AndroidApiLevel: "35",
				SupportedOSPlatformVersion: "21",
				RuntimeProviderJavaName: "mono.MonoRuntimeProvider",
				ManifestPlaceholders: "applicationId=my.app"),
			manifestTemplate);

		var peer = result.AllPeers.First (p => p.ManagedTypeName == "MyApp.SimpleActivity");
		Assert.True (peer.IsUnconditional, "Relative manifest names should root correctly after placeholder substitution.");
	}

	[Fact]
	public void Execute_ManifestReferencedTypeNames_AreNormalizedInGeneratedManifest ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var manifestTemplate = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="my.app">
			  <application>
			    <activity android:name=".SimpleActivity" />
			  </application>
			</manifest>
			""");

		var result = CreateGenerator ().Execute (
			new List<(string, PEReader)> { ("TestFixtures", peReader) },
			new Version (11, 0),
			new HashSet<string> (),
			manifestConfig: new ManifestConfig (
				PackageName: "my.app",
				AndroidApiLevel: "35",
				SupportedOSPlatformVersion: "21",
				RuntimeProviderJavaName: "mono.MonoRuntimeProvider"),
			manifestTemplate: manifestTemplate);

		var androidName = (string?) result.Manifest?.Document.Root?
			.Element ("application")?
			.Element ("activity")?
			.Attribute (System.Xml.Linq.XName.Get ("name", "http://schemas.android.com/apk/res/android"));

		Assert.Equal ("my.app.SimpleActivity", androidName);
	}


	TrimmableTypeMapGenerator CreateGenerator () => new (new TestTrimmableTypeMapLogger (logMessages));

	TrimmableTypeMapGenerator CreateGenerator (List<string> warnings) =>
		new (new TestTrimmableTypeMapLogger (logMessages, warnings));

	[Theory]
	[InlineData ("com/example/MyActivity", "com.example.MyActivity", "com.example", "activity", "com.example.MyActivity")]
	[InlineData ("com/example/MyActivity", "com.example.MyActivity", "com.example", "activity", ".MyActivity")]
	[InlineData ("com/example/MyService", "com.example.MyService", "com.example", "service", "MyService")]
	[InlineData ("crc64123456789abc/MyActivity", "my/app/MyActivity", "my.app", "activity", ".MyActivity")]
	[InlineData ("com/example/Outer$Inner", "com.example.Outer$Inner", "com.example", "activity", "com.example.Outer$Inner")]
	public void RootManifestReferencedTypes_RootsManifestReferencedTypes (
		string javaName,
		string compatJniName,
		string packageName,
		string elementName,
		string manifestName)
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = javaName, CompatJniName = compatJniName,
				ManagedTypeName = "MyApp.MyTarget", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "MyTarget",
				AssemblyName = "MyApp", IsUnconditional = false,
			},
			new JavaPeerInfo {
				JavaName = "com/example/OtherType", CompatJniName = "com.example.OtherType",
				ManagedTypeName = "MyApp.OtherType", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "OtherType",
				AssemblyName = "MyApp", IsUnconditional = false,
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ($$"""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="{{packageName}}">
			  <application>
			    <{{elementName}} android:name="{{manifestName}}" />
			  </application>
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.True (peers [0].IsUnconditional, "The manifest-referenced type should be rooted as unconditional.");
		Assert.False (peers [1].IsUnconditional, "Non-matching peers should remain conditional.");
		Assert.Contains (logMessages, m => m.Contains ("Rooting manifest-referenced type"));
	}

	[Fact]
	public void RootManifestReferencedTypes_RootsApplicationAndInstrumentationTypes ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "com/example/MyApplication", CompatJniName = "com.example.MyApplication",
				ManagedTypeName = "MyApp.MyApplication", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "MyApplication",
				AssemblyName = "MyApp", IsUnconditional = false,
			},
			new JavaPeerInfo {
				JavaName = "com/example/MyInstrumentation", CompatJniName = "com.example.MyInstrumentation",
				ManagedTypeName = "MyApp.MyInstrumentation", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "MyInstrumentation",
				AssemblyName = "MyApp", IsUnconditional = false,
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			  <application android:name=".MyApplication" />
			  <instrumentation android:name="MyInstrumentation" />
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.True (peers [0].IsUnconditional, "Application type should be rooted from <application android:name>.");
		Assert.True (peers [1].IsUnconditional, "Instrumentation type should be rooted from <instrumentation android:name>.");
		Assert.True (peers [0].CannotRegisterInStaticConstructor, "Application type should defer Runtime.registerNatives().");
		Assert.True (peers [1].CannotRegisterInStaticConstructor, "Instrumentation type should defer Runtime.registerNatives().");
	}

	[Fact]
	public void PropagateDeferredRegistrationToBaseClasses_PropagatesToBaseClassesOfManifestReferencedTypes ()
	{
		var basePeer = new JavaPeerInfo {
			JavaName = "crc64aaa/TestInstrumentation_1", CompatJniName = "crc64aaa/TestInstrumentation_1",
			ManagedTypeName = "Tests.TestInstrumentation`1", ManagedTypeNamespace = "Tests", ManagedTypeShortName = "TestInstrumentation`1",
			AssemblyName = "Tests", IsUnconditional = false,
			BaseJavaName = "android/app/Instrumentation",
		};
		var midPeer = new JavaPeerInfo {
			JavaName = "crc64bbb/NUnitTestInstrumentation", CompatJniName = "crc64bbb/NUnitTestInstrumentation",
			ManagedTypeName = "Tests.NUnitTestInstrumentation", ManagedTypeNamespace = "Tests", ManagedTypeShortName = "NUnitTestInstrumentation",
			AssemblyName = "Tests", IsUnconditional = false,
			BaseJavaName = "crc64aaa/TestInstrumentation_1",
		};
		var leafPeer = new JavaPeerInfo {
			JavaName = "crc64ccc/NUnitInstrumentation", CompatJniName = "crc64ccc/NUnitInstrumentation",
			ManagedTypeName = "Tests.NUnitInstrumentation", ManagedTypeNamespace = "Tests", ManagedTypeShortName = "NUnitInstrumentation",
			AssemblyName = "Tests", IsUnconditional = false,
			BaseJavaName = "crc64bbb/NUnitTestInstrumentation",
		};
		var peers = new List<JavaPeerInfo> { basePeer, midPeer, leafPeer };

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			  <instrumentation android:name="crc64ccc.NUnitInstrumentation" />
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		// RootManifestReferencedTypes sets the flag only on the directly matched leaf
		Assert.True (leafPeer.CannotRegisterInStaticConstructor, "Leaf instrumentation should have deferred registration after manifest rooting.");
		Assert.False (midPeer.CannotRegisterInStaticConstructor, "Mid peer should NOT have deferred registration before propagation.");
		Assert.False (basePeer.CannotRegisterInStaticConstructor, "Base peer should NOT have deferred registration before propagation.");

		// PropagateDeferredRegistrationToBaseClasses walks the BaseJavaName chain
		TrimmableTypeMapGenerator.PropagateDeferredRegistrationToBaseClasses (peers);

		Assert.True (leafPeer.CannotRegisterInStaticConstructor, "Leaf instrumentation should still have deferred registration.");
		Assert.True (midPeer.CannotRegisterInStaticConstructor, "Mid peer should have deferred registration after propagation.");
		Assert.True (basePeer.CannotRegisterInStaticConstructor, "Base peer should have deferred registration after propagation.");
	}

	[Fact]
	public void Execute_PropagatesDeferredRegistrationToBaseClasses ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var manifestTemplate = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="my.app">
			  <instrumentation android:name=".DerivedInstrumentation" />
			</manifest>
			""");

		var result = CreateGenerator ().Execute (
			new List<(string, PEReader)> { ("TestFixtures", peReader) },
			new Version (11, 0),
			new HashSet<string> (),
			manifestConfig: new ManifestConfig (
				PackageName: "my.app",
				AndroidApiLevel: "35",
				SupportedOSPlatformVersion: "21",
				RuntimeProviderJavaName: "mono.MonoRuntimeProvider"),
			manifestTemplate: manifestTemplate);

		var derivedPeer = result.AllPeers.FirstOrDefault (
			p => p.ManagedTypeShortName == "DerivedInstrumentation");
		var basePeer = derivedPeer?.BaseJavaName is not null
			? result.AllPeers.FirstOrDefault (p => p.JavaName == derivedPeer.BaseJavaName)
			: null;

		if (derivedPeer is not null && basePeer is not null) {
			Assert.True (derivedPeer.CannotRegisterInStaticConstructor,
				"Instrumentation type should defer registerNatives.");
			Assert.True (basePeer.CannotRegisterInStaticConstructor,
				"Base class of instrumentation type should also defer registerNatives.");
		}
		// If test fixtures don't have a matching hierarchy, the test is skipped implicitly.
	}

	[Fact]
	public void RootManifestReferencedTypes_RewritesManifestApplicationToActualJavaName ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "crc64123456789abc/App", CompatJniName = "android/apptests/App",
				ManagedTypeName = "Android.AppTests.App", ManagedTypeNamespace = "Android.AppTests", ManagedTypeShortName = "App",
				AssemblyName = "Mono.Android.NET-Tests", IsUnconditional = false,
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="Mono.Android.NET_Tests">
			  <application android:name="android.apptests.App" />
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		var actualName = (string?) doc.Root?
			.Element ("application")?
			.Attribute (System.Xml.Linq.XName.Get ("name", "http://schemas.android.com/apk/res/android"));

		Assert.Equal ("crc64123456789abc.App", actualName);
		Assert.True (peers [0].IsUnconditional);
		Assert.True (peers [0].CannotRegisterInStaticConstructor);
	}

	[Fact]
	public void RootManifestReferencedTypes_WarnsForUnresolvedTypes ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "com/example/MyActivity", CompatJniName = "com.example.MyActivity",
				ManagedTypeName = "MyApp.MyActivity", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "MyActivity",
				AssemblyName = "MyApp",
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			  <application>
			    <service android:name="com.example.NonExistentService" />
			  </application>
			</manifest>
			""");

		var warnings = new List<string> ();
		var generator = CreateGenerator (warnings);
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.Contains (warnings, w => w.Contains ("com.example.NonExistentService"));
	}

	[Fact]
	public void RootManifestReferencedTypes_SkipsAlreadyUnconditional ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "com/example/MyActivity", CompatJniName = "com.example.MyActivity",
				ManagedTypeName = "MyApp.MyActivity", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "MyActivity",
				AssemblyName = "MyApp", IsUnconditional = true,
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			  <application>
			    <activity android:name="com.example.MyActivity" />
			  </application>
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.True (peers [0].IsUnconditional);
		Assert.DoesNotContain (logMessages, m => m.Contains ("Rooting manifest-referenced type"));
	}

	[Fact]
	public void RootManifestReferencedTypes_EmptyManifest_NoChanges ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "com/example/MyActivity", CompatJniName = "com.example.MyActivity",
				ManagedTypeName = "MyApp.MyActivity", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "MyActivity",
				AssemblyName = "MyApp",
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.False (peers [0].IsUnconditional);
	}

	[Fact]
	public void MergeCrossAssemblyAliases_RegisterTakesPrecedenceOverJniTypeSignature ()
	{
		// Java.Interop has JavaObject with [JniTypeSignature("java/lang/Object")]
		var javaInteropPeer = new JavaPeerInfo {
			JavaName = "java/lang/Object", CompatJniName = "java/lang/Object",
			ManagedTypeName = "Java.Interop.JavaObject", ManagedTypeNamespace = "Java.Interop", ManagedTypeShortName = "JavaObject",
			AssemblyName = "Java.Interop", IsFromJniTypeSignature = true, DoNotGenerateAcw = true,
		};

		// Mono.Android has Java.Lang.Object with [Register("java/lang/Object")]
		var monoAndroidPeer = new JavaPeerInfo {
			JavaName = "java/lang/Object", CompatJniName = "java/lang/Object",
			ManagedTypeName = "Java.Lang.Object", ManagedTypeNamespace = "Java.Lang", ManagedTypeShortName = "Object",
			AssemblyName = "Mono.Android", IsFromJniTypeSignature = false, DoNotGenerateAcw = true,
		};

		// Another unique peer in Java.Interop that shouldn't be moved
		var otherPeer = new JavaPeerInfo {
			JavaName = "java/interop/SomeHelper", CompatJniName = "java/interop/SomeHelper",
			ManagedTypeName = "Java.Interop.SomeHelper", ManagedTypeNamespace = "Java.Interop", ManagedTypeShortName = "SomeHelper",
			AssemblyName = "Java.Interop", IsFromJniTypeSignature = true,
		};

		var allPeers = new List<JavaPeerInfo> { javaInteropPeer, monoAndroidPeer, otherPeer };
		var result = TrimmableTypeMapGenerator.MergeCrossAssemblyAliases (allPeers);

		// Both java/lang/Object peers should be in the Mono.Android group ([Register] wins)
		var monoAndroidGroup = result.Single (g => g.AssemblyName == "Mono.Android");
		Assert.Equal (2, monoAndroidGroup.Peers.Count);
		Assert.Contains (monoAndroidGroup.Peers, p => p.ManagedTypeName == "Java.Lang.Object");
		Assert.Contains (monoAndroidGroup.Peers, p => p.ManagedTypeName == "Java.Interop.JavaObject");

		// Java.Interop should only have the unique peer
		var javaInteropGroup = result.Single (g => g.AssemblyName == "Java.Interop");
		Assert.Single (javaInteropGroup.Peers);
		Assert.Equal ("Java.Interop.SomeHelper", javaInteropGroup.Peers [0].ManagedTypeName);
	}

	[Fact]
	public void MergeCrossAssemblyAliases_NoDuplicates_NothingMoved ()
	{
		var peer1 = new JavaPeerInfo {
			JavaName = "com/example/Foo", CompatJniName = "com/example/Foo",
			ManagedTypeName = "MyApp.Foo", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "Foo",
			AssemblyName = "MyApp",
		};
		var peer2 = new JavaPeerInfo {
			JavaName = "com/example/Bar", CompatJniName = "com/example/Bar",
			ManagedTypeName = "MyLib.Bar", ManagedTypeNamespace = "MyLib", ManagedTypeShortName = "Bar",
			AssemblyName = "MyLib",
		};

		var result = TrimmableTypeMapGenerator.MergeCrossAssemblyAliases (new List<JavaPeerInfo> { peer1, peer2 });

		Assert.Equal (2, result.Count);
		Assert.Single (result.Single (g => g.AssemblyName == "MyApp").Peers);
		Assert.Single (result.Single (g => g.AssemblyName == "MyLib").Peers);
	}

	[Fact]
	public void MergeCrossAssemblyAliases_SameAssemblyAliases_NotMoved ()
	{
		// Two peers in the same assembly with the same JNI name — within-assembly alias
		// should NOT be moved; ModelBuilder handles it.
		var peer1 = new JavaPeerInfo {
			JavaName = "java/lang/Object", CompatJniName = "java/lang/Object",
			ManagedTypeName = "Java.Lang.Object", ManagedTypeNamespace = "Java.Lang", ManagedTypeShortName = "Object",
			AssemblyName = "Mono.Android",
		};
		var peer2 = new JavaPeerInfo {
			JavaName = "java/lang/Object", CompatJniName = "java/lang/Object",
			ManagedTypeName = "Java.Lang.IDisposable", ManagedTypeNamespace = "Java.Lang", ManagedTypeShortName = "IDisposable",
			AssemblyName = "Mono.Android",
		};

		var result = TrimmableTypeMapGenerator.MergeCrossAssemblyAliases (new List<JavaPeerInfo> { peer1, peer2 });

		Assert.Single (result);
		Assert.Equal (2, result [0].Peers.Count);
	}

	[Fact]
	public void MergeCrossAssemblyAliases_SameManagedName_DifferentAssemblies_MergedCorrectly ()
	{
		// Reproduces the java/lang/Throwable crash: two assemblies define Java.Lang.Throwable
		// with the same JNI name, plus Java.Interop.JavaException also maps to the same JNI name.
		// All three should be merged into the [Register]-owning assembly's group.
		var javaInteropThrowable = new JavaPeerInfo {
			JavaName = "java/lang/Throwable", CompatJniName = "java/lang/Throwable",
			ManagedTypeName = "Java.Lang.Throwable", ManagedTypeNamespace = "Java.Lang", ManagedTypeShortName = "Throwable",
			AssemblyName = "Java.Interop", IsFromJniTypeSignature = true, DoNotGenerateAcw = true,
		};

		var monoAndroidThrowable = new JavaPeerInfo {
			JavaName = "java/lang/Throwable", CompatJniName = "java/lang/Throwable",
			ManagedTypeName = "Java.Lang.Throwable", ManagedTypeNamespace = "Java.Lang", ManagedTypeShortName = "Throwable",
			AssemblyName = "Mono.Android", IsFromJniTypeSignature = false, DoNotGenerateAcw = true,
		};

		var javaException = new JavaPeerInfo {
			JavaName = "java/lang/Throwable", CompatJniName = "java/lang/Throwable",
			ManagedTypeName = "Java.Interop.JavaException", ManagedTypeNamespace = "Java.Interop", ManagedTypeShortName = "JavaException",
			AssemblyName = "Java.Interop", IsFromJniTypeSignature = true, DoNotGenerateAcw = true,
		};

		var allPeers = new List<JavaPeerInfo> { javaInteropThrowable, monoAndroidThrowable, javaException };
		var result = TrimmableTypeMapGenerator.MergeCrossAssemblyAliases (allPeers);

		// All java/lang/Throwable peers should be in the Mono.Android group ([Register] wins)
		var monoAndroidGroup = result.Single (g => g.AssemblyName == "Mono.Android");
		Assert.Equal (3, monoAndroidGroup.Peers.Count);
		Assert.Contains (monoAndroidGroup.Peers, p => p.ManagedTypeName == "Java.Lang.Throwable" && p.AssemblyName == "Mono.Android");
		Assert.Contains (monoAndroidGroup.Peers, p => p.ManagedTypeName == "Java.Lang.Throwable" && p.AssemblyName == "Java.Interop");
		Assert.Contains (monoAndroidGroup.Peers, p => p.ManagedTypeName == "Java.Interop.JavaException");

		// Java.Interop group should be empty (all peers moved to Mono.Android)
		Assert.DoesNotContain (result, g => g.AssemblyName == "Java.Interop");
	}

	[Fact]
	public void MergeCrossAssemblyAliases_SameManagedName_ProducesCorrectAliasGroup ()
	{
		// End-to-end: after merging, ModelBuilder must produce a 3-way alias group
		// for java/lang/Throwable with indexed entries and a single base entry,
		// ensuring the runtime dictionary only sees java/lang/Throwable once.
		var javaInteropThrowable = new JavaPeerInfo {
			JavaName = "java/lang/Throwable", CompatJniName = "java/lang/Throwable",
			ManagedTypeName = "Java.Lang.Throwable", ManagedTypeNamespace = "Java.Lang", ManagedTypeShortName = "Throwable",
			AssemblyName = "Java.Interop", IsFromJniTypeSignature = true, DoNotGenerateAcw = true,
		};

		var monoAndroidThrowable = new JavaPeerInfo {
			JavaName = "java/lang/Throwable", CompatJniName = "java/lang/Throwable",
			ManagedTypeName = "Java.Lang.Throwable", ManagedTypeNamespace = "Java.Lang", ManagedTypeShortName = "Throwable",
			AssemblyName = "Mono.Android", IsFromJniTypeSignature = false, DoNotGenerateAcw = true,
		};

		var javaException = new JavaPeerInfo {
			JavaName = "java/lang/Throwable", CompatJniName = "java/lang/Throwable",
			ManagedTypeName = "Java.Interop.JavaException", ManagedTypeNamespace = "Java.Interop", ManagedTypeShortName = "JavaException",
			AssemblyName = "Java.Interop", IsFromJniTypeSignature = true, DoNotGenerateAcw = true,
		};

		var allPeers = new List<JavaPeerInfo> { javaInteropThrowable, monoAndroidThrowable, javaException };
		var merged = TrimmableTypeMapGenerator.MergeCrossAssemblyAliases (allPeers);

		// All peers should be in the Mono.Android group
		Assert.Single (merged);
		var group = merged [0];
		Assert.Equal ("Mono.Android", group.AssemblyName);
		Assert.Equal (3, group.Peers.Count);

		// Build the model — should produce a 3-way alias group
		string typeMapAssemblyName = $"_{group.AssemblyName}.TypeMap";
		var model = ModelBuilder.Build (group.Peers, typeMapAssemblyName + ".dll", typeMapAssemblyName);

		// 3 indexed entries + 1 base entry = 4
		Assert.Equal (4, model.Entries.Count);
		Assert.Equal ("java/lang/Throwable[0]", model.Entries [0].JniName);
		Assert.Equal ("java/lang/Throwable[1]", model.Entries [1].JniName);
		Assert.Equal ("java/lang/Throwable[2]", model.Entries [2].JniName);
		Assert.Equal ("java/lang/Throwable", model.Entries [3].JniName);

		// Exactly 1 alias holder
		Assert.Single (model.AliasHolders);
		Assert.Equal (3, model.AliasHolders [0].AliasKeys.Count);

		// The base "java/lang/Throwable" entry points to the alias holder, not a type directly
		var baseEntry = model.Entries [3];
		Assert.Contains ("_Aliases", baseEntry.ProxyTypeReference);

		// 3 associations (one per peer → alias holder)
		Assert.Equal (3, model.Associations.Count);

		// The bare "java/lang/Throwable" key appears exactly once — no duplicates
		Assert.Single (model.Entries, e => e.JniName == "java/lang/Throwable");
	}

	[Fact]
	public void RootManifestReferencedTypes_ResolvesRelativeNames ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "com/example/MyActivity", CompatJniName = "com.example.MyActivity",
				ManagedTypeName = "MyApp.MyActivity", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "MyActivity",
				AssemblyName = "MyApp", IsUnconditional = false,
			},
			new JavaPeerInfo {
				JavaName = "com/example/MyService", CompatJniName = "com.example.MyService",
				ManagedTypeName = "MyApp.MyService", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "MyService",
				AssemblyName = "MyApp", IsUnconditional = false,
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			  <application>
			    <activity android:name=".MyActivity" />
			    <service android:name="MyService" />
			  </application>
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.True (peers [0].IsUnconditional, "Dot-relative name '.MyActivity' should resolve to com.example.MyActivity.");
		Assert.True (peers [1].IsUnconditional, "Simple name 'MyService' should resolve to com.example.MyService.");
	}

	[Fact]
	public void RootManifestReferencedTypes_MatchesCompatNames ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "crc64123456789abc/MyActivity", CompatJniName = "my/app/MyActivity",
				ManagedTypeName = "My.App.MyActivity", ManagedTypeNamespace = "My.App", ManagedTypeShortName = "MyActivity",
				AssemblyName = "MyApp", IsUnconditional = false,
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="my.app">
			  <application>
			    <activity android:name=".MyActivity" />
			  </application>
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.True (peers [0].IsUnconditional, "Relative manifest name should match CompatJniName when JavaName uses a CRC64 package.");
	}

	[Fact]
	public void RootManifestReferencedTypes_MatchesNestedTypes ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "com/example/Outer$Inner", CompatJniName = "com.example.Outer$Inner",
				ManagedTypeName = "MyApp.Outer.Inner", ManagedTypeNamespace = "MyApp", ManagedTypeShortName = "Inner",
				AssemblyName = "MyApp", IsUnconditional = false,
			},
		};

		var doc = System.Xml.Linq.XDocument.Parse ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			  <application>
			    <activity android:name="com.example.Outer$Inner" />
			  </application>
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.True (peers [0].IsUnconditional, "Nested type 'Outer$Inner' should be matched using '$' separator.");
	}


	static PEReader CreateTestFixturePEReader ()
	{
		var dir = Path.GetDirectoryName (typeof (FixtureTestBase).Assembly.Location)
			?? throw new InvalidOperationException ("Cannot determine test assembly directory");
		return new PEReader (File.OpenRead (Path.Combine (dir, "TestFixtures.dll")));
	}
}
