using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TrimmableTypeMapManifestAliasTests
{
	const string GeneratedActivityName = "crc64deadbeefcafebabe.UnnamedActivity";

	[TestCase ("${applicationId}.UnnamedAlias", "my.app.UnnamedAlias")]
	[TestCase (".legacy-alias", ".legacy-alias")]
	[TestCase ("my.app.legacy-alias", "my.app.legacy-alias")]
	[TestCase ("${applicationId}.legacy-alias", "my.app.legacy-alias")]
	[TestCase ("${applicationId}.\u00a2Alias", "my.app.\u00a2Alias")]
	[TestCase ("${applicationId}.for", "my.app.for")]
	public void ActivityAliasKeepsNameWhileRootingAndRewritingTarget (string aliasName, string expectedAliasName)
	{
		var peer = new JavaPeerInfo {
			JavaName = "crc64deadbeefcafebabe/UnnamedActivity",
			CompatJniName = "myapp/UnnamedActivity",
			ManagedTypeName = "MyApp.UnnamedActivity",
			ManagedTypeNamespace = "MyApp",
			ManagedTypeShortName = "UnnamedActivity",
			AssemblyName = "MyApp",
		};
		var peers = new List<JavaPeerInfo> { peer };
		var manifest = XDocument.Parse ($$"""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="${applicationId}">
			  <application>
			    <activity-alias android:name="{{aliasName}}" android:targetActivity="${targetPackage}.UnnamedActivity" />
			  </application>
			</manifest>
			""");

		var manifestForRooting = new XDocument (manifest);
		ManifestGenerator.ResolvePackageName (manifestForRooting.Root, "my.app");
		ManifestGenerator.ApplyPlaceholders (manifestForRooting, "targetPackage=myapp", "my.app");
		var typeMapGenerator = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ());
		typeMapGenerator.RootManifestReferencedTypes (peers, manifestForRooting);
		Assert.IsTrue (peer.IsUnconditional, "The activity-alias target must root its Java peer.");
		Assert.IsTrue (typeMapGenerator.ValidateJavaNames (peers, manifest: manifestForRooting),
			"An alias name is an arbitrary component identifier, not a Java type.");

		var manifestGenerator = new ManifestGenerator {
			PackageName = "my.app",
			MinSdkVersion = "24",
			TargetSdkVersion = "35",
			RuntimeProviderJavaName = "mono.MonoRuntimeProvider",
			ManifestPlaceholders = "targetPackage=myapp",
		};
		var (document, _) = manifestGenerator.Generate (manifest, peers, new AssemblyManifestInfo ());
		var alias = document.Descendants ("activity-alias").Single ();
		XNamespace android = "http://schemas.android.com/apk/res/android";
		Assert.AreEqual (expectedAliasName, (string) alias.Attribute (android + "name"));
		Assert.AreEqual (GeneratedActivityName, (string) alias.Attribute (android + "targetActivity"));
	}

	[Test]
	public void PackageNameTokenIsReplacedOnlyInGeneratedAttributes ()
	{
		var provider = new JavaPeerInfo {
			JavaName = "crc64deadbeefcafebabe/TokenProvider",
			CompatJniName = "myapp/TokenProvider",
			ManagedTypeName = "MyApp.TokenProvider",
			ManagedTypeNamespace = "MyApp",
			ManagedTypeShortName = "TokenProvider",
			AssemblyName = "MyApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.ContentProvider,
				Properties = new Dictionary<string, object?> {
					["Authorities"] = "my.app.provider",
					["ReadPermission"] = "@PACKAGE_NAME@.permission.READ",
				},
			},
		};
		var manifest = XDocument.Parse ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="my.app">
			  <application
			    android:description="@PACKAGE_NAME@.template.description"
			    android:permission="@PACKAGE_NAME@.permission.READ" />
			</manifest>
			""");
		var assemblyInfo = new AssemblyManifestInfo {
			ApplicationProperties = new Dictionary<string, object?> {
				["Permission"] = "@PACKAGE_NAME@.permission.READ",
			},
		};
		var manifestGenerator = new ManifestGenerator {
			PackageName = "my.app",
			MinSdkVersion = "24",
			TargetSdkVersion = "35",
			RuntimeProviderJavaName = "mono.MonoRuntimeProvider",
		};

		var (document, _) = manifestGenerator.Generate (manifest, [provider], assemblyInfo);
		XNamespace android = "http://schemas.android.com/apk/res/android";
		var application = document.Root?.Element ("application");
		Assert.IsNotNull (application);
		Assert.AreEqual ("@PACKAGE_NAME@.template.description", (string) application.Attribute (android + "description"));
		Assert.AreEqual ("my.app.permission.READ", (string) application.Attribute (android + "permission"));
		var generatedProvider = application.Element ("provider");
		Assert.IsNotNull (generatedProvider);
		Assert.AreEqual ("my.app.permission.READ", (string) generatedProvider.Attribute (android + "readPermission"));
	}

	[Test]
	public void InstrumentationEmitsIntentFilters ()
	{
		var peer = new JavaPeerInfo {
			JavaName = "myapp/TestInstrumentation",
			CompatJniName = "myapp/TestInstrumentation",
			ManagedTypeName = "MyApp.TestInstrumentation",
			ManagedTypeNamespace = "MyApp",
			ManagedTypeShortName = "TestInstrumentation",
			AssemblyName = "MyApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Instrumentation,
				MetaData = [new MetaDataInfo { Name = "instrumentation.metadata", Value = "present" }],
				IntentFilters = [new IntentFilterInfo {
					Actions = ["my.app.INSTRUMENT"],
					Categories = ["my.app.TEST"],
					Properties = new Dictionary<string, object?> { ["Priority"] = 7 },
				}],
				ManifestProperties = [new PropertyInfo { Name = "instrumentation.property", Value = "present" }],
			},
		};
		var manifestGenerator = new ManifestGenerator {
			PackageName = "my.app",
			MinSdkVersion = "24",
			TargetSdkVersion = "35",
			RuntimeProviderJavaName = "mono.MonoRuntimeProvider",
		};

		var (document, _) = manifestGenerator.Generate (null, [peer], new AssemblyManifestInfo ());
		var instrumentation = document.Root?.Element ("instrumentation");
		Assert.IsNotNull (instrumentation);
		CollectionAssert.AreEqual (new [] { "meta-data", "intent-filter", "property" },
			instrumentation.Elements ().Select (element => element.Name.LocalName));
		var filter = instrumentation.Element ("intent-filter");
		Assert.IsNotNull (filter);
		XNamespace android = "http://schemas.android.com/apk/res/android";
		Assert.AreEqual ("7", (string) filter.Attribute (android + "priority"));
		Assert.AreEqual ("my.app.INSTRUMENT", (string) filter.Element ("action")?.Attribute (android + "name"));
		Assert.AreEqual ("my.app.TEST", (string) filter.Element ("category")?.Attribute (android + "name"));
	}

	[Test]
	public void MultipleAssemblyApplicationAttributesAreRejected ()
	{
		var manifestGenerator = new ManifestGenerator {
			PackageName = "my.app",
			MinSdkVersion = "24",
			TargetSdkVersion = "35",
			RuntimeProviderJavaName = "mono.MonoRuntimeProvider",
		};
		var manifest = XDocument.Parse ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="my.app">
			  <application />
			</manifest>
			""");
		var assemblyInfo = new AssemblyManifestInfo {
			ApplicationAttributeCount = 2,
		};

		var exception = Assert.Throws<InvalidOperationException> (
			() => manifestGenerator.Generate (manifest, [], assemblyInfo));
		Assert.AreEqual ("There can be only one [assembly:Application] attribute defined.", exception.Message);
	}
}
