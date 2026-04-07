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

	TrimmableTypeMapGenerator CreateGenerator () => new (msg => logMessages.Add (msg));

	TrimmableTypeMapGenerator CreateGenerator (List<string> warnings) =>
		new (msg => logMessages.Add (msg), msg => warnings.Add (msg));

	[Fact]
	public void RootManifestReferencedTypes_RootsMatchingPeers ()
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
			    <activity android:name="com.example.MyActivity" />
			  </application>
			</manifest>
			""");

		var generator = CreateGenerator ();
		generator.RootManifestReferencedTypes (peers, doc);

		Assert.True (peers [0].IsUnconditional, "MyActivity should be rooted as unconditional.");
		Assert.False (peers [1].IsUnconditional, "MyService should remain conditional.");
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
