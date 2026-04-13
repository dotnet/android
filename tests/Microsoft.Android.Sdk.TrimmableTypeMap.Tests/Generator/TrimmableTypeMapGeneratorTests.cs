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
	public void Execute_CollectsDeferredRegistrationTypes_ForConcreteApplicationAndInstrumentation ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());

		Assert.Contains ("my.app.MyApplication", result.ApplicationRegistrationTypes);
		Assert.Contains ("my.app.MyInstrumentation", result.ApplicationRegistrationTypes);
		Assert.DoesNotContain ("my.app.BaseApplication", result.ApplicationRegistrationTypes);
		Assert.DoesNotContain ("my.app.BaseInstrumentation", result.ApplicationRegistrationTypes);
		Assert.DoesNotContain ("my.app.IntermediateInstrumentation", result.ApplicationRegistrationTypes);
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

	static PEReader CreateTestFixturePEReader ()
	{
		var dir = Path.GetDirectoryName (typeof (FixtureTestBase).Assembly.Location)
			?? throw new InvalidOperationException ("Cannot determine test assembly directory");
		return new PEReader (File.OpenRead (Path.Combine (dir, "TestFixtures.dll")));
	}
}
