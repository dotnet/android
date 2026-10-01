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
}
