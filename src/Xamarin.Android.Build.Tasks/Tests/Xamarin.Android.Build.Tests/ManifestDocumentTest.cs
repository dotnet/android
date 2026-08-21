using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Java.Interop.Tools.Cecil;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

/// <summary>
/// In-process tests for how <see cref="ManifestDocument"/> merges the computed
/// minSdkVersion/targetSdkVersion into a user-authored &lt;uses-sdk/&gt; element.
/// </summary>
[TestFixture]
[Parallelizable (ParallelScope.Self)]
public class ManifestDocumentTest : BaseTest
{
	const string DefaultMinSdkVersion = "21";
	const string DefaultTargetSdkVersion = "37";

	static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";

	static string CreateManifest (string usesSdk) => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.xamarin.usessdk"">
	{usesSdk}
	<application android:label=""UsesSdk"">
	</application>
</manifest>";

	static XElement MergeAndGetUsesSdk (string usesSdk, out XDocument document)
	{
		var templateFile = Path.GetTempFileName ();
		try {
			File.WriteAllText (templateFile, CreateManifest (usesSdk));

			var manifest = new ManifestDocument (templateFile) {
				PackageName = "com.xamarin.usessdk",
				TargetSdkVersion = DefaultTargetSdkVersion,
				MinSdkVersion = DefaultMinSdkVersion,
				VersionResolver = new MockVersionResolver {
					GetApiLevelFromIdFunc = id => int.TryParse (id, out var apiLevel) ? (int?) apiLevel : null,
					GetIdFromApiLevelFunc = apiLevel => apiLevel,
				},
			};

			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, nameof (ManifestDocumentTest));
			manifest.Merge (log, new TypeDefinitionCache (), new List<TypeDefinition> (), applicationClass: null, embed: false, bundledWearApplicationName: null, mergedManifestDocuments: null);

			var writer = new StringWriter ();
			manifest.Save ((code, message) => { }, writer);

			document = XDocument.Parse (writer.ToString ());
			var element = document.Root.Element ("uses-sdk");
			Assert.IsNotNull (element, "uses-sdk element should exist");
			return element;
		} finally {
			File.Delete (templateFile);
		}
	}

	static void AssertUsesSdk (string usesSdk, string expectedMinSdkVersion, string expectedTargetSdkVersion)
	{
		var element = MergeAndGetUsesSdk (usesSdk, out var document);

		Assert.AreEqual (expectedMinSdkVersion, element.Attribute (AndroidNs + "minSdkVersion")?.Value, "unexpected android:minSdkVersion");
		Assert.AreEqual (expectedTargetSdkVersion, element.Attribute (AndroidNs + "targetSdkVersion")?.Value, "unexpected android:targetSdkVersion");

		foreach (var comment in document.DescendantNodes ().OfType<XComment> ()) {
			StringAssert.DoesNotContain ("UsesMinSdkAttributes", comment.Value, "the lint suppression comment should no longer be emitted");
		}
	}

	[Test]
	public void NoUsesSdkElement ()
	{
		AssertUsesSdk ("", DefaultMinSdkVersion, DefaultTargetSdkVersion);
	}

	[Test]
	public void MinSdkVersionOnly ()
	{
		// Regression test: targetSdkVersion was never written, so `aapt2` defaulted it to minSdkVersion.
		AssertUsesSdk (@"<uses-sdk android:minSdkVersion=""24"" />", "24", DefaultTargetSdkVersion);
	}

	[Test]
	public void TargetSdkVersionOnly ()
	{
		AssertUsesSdk (@"<uses-sdk android:targetSdkVersion=""30"" />", DefaultMinSdkVersion, "30");
	}

	[Test]
	public void MinAndTargetSdkVersion ()
	{
		AssertUsesSdk (@"<uses-sdk android:minSdkVersion=""24"" android:targetSdkVersion=""30"" />", "24", "30");
	}
}

class MockVersionResolver : IVersionResolver
{
	public Func<string, int?> GetApiLevelFromIdFunc { get; set; } = _ => 99;
	public Func<string, string> GetIdFromApiLevelFunc { get; set; } = _ => "API-99";

	public int? GetApiLevelFromId (string id) => GetApiLevelFromIdFunc (id);

	public string GetIdFromApiLevel (string apiLevel) => GetIdFromApiLevelFunc (apiLevel);
}
