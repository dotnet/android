using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class DalvikArchiveTargetsTests : BaseTest
{
	[Test]
	public void CollectDalvikFiles (
		[Values ("_BuildApkEmbed", "_BuildApkFastDev")] string targetName,
		[Values ("apk", "aab", "AAB", "", "unknown")] string format,
		[Values (false, true)] bool empty)
	{
		for (int build = 0; build < 2; build++) {
			using var collection = new ProjectCollection ();
			var instance = CreateProject (collection, targetName, format, empty);
			Assert.IsTrue (instance.Build (targetName, []), "Packaging item construction should succeed.");
			Assert.IsEmpty (instance.GetItems ("_AndroidDalvikFiles"), "Do not retain a previous collection's scratch items.");
			var files = instance.GetItems ("FilesToAddToArchive").ToArray ();
			if (empty) {
				Assert.IsEmpty (files);
				continue;
			}

			string prefix = format.Equals ("aab", StringComparison.InvariantCultureIgnoreCase) ? "dex/" : "";
			string source = targetName == "_BuildApkFastDev" && prefix == "" ? "fast's files" : "embed's files";
			string [] names = ["classes.dex", " renamed/classes'2.dex ", "classes3.dex", "classes4.dex", "classes.dex"];
			CollectionAssert.AreEqual (new [] { 1, 2, 3, 4, 1 }.Select (i => $"{source}/classes{(i == 1 ? "" : i.ToString ())}.dex"),
				files.Select (file => file.EvaluatedInclude), "Preserve input paths, order, and duplicates.");
			CollectionAssert.AreEqual (names.Select (name => prefix + name), files.Select (file => file.GetMetadataValue ("ArchivePath")));
			foreach (var file in files) {
				CollectionAssert.AreEquivalent (new [] { "ArchivePath" }, file.Metadata.Select (metadata => metadata.Name),
					"Only ArchivePath should survive, not ApkName, JavaArchiveEntry, or unrelated input metadata.");
			}
			Assert.IsTrue (instance.GetItems ("_DexFile").All (file => file.GetMetadataValue ("ArchivePath") == "stale.dex"),
				"Constructing archive items must not mutate the source metadata.");
		}
	}

	[Test]
	public void PackagingTargetsDoNotReuseDalvikItemsOrBundlePrefix ()
	{
		using var collection = new ProjectCollection ();
		var instance = CreateProject (collection, "_BuildApkEmbed", "aab", empty: false);
		Assert.IsTrue (instance.Build ("_BuildApkEmbed", []));
		Assert.AreEqual (5, instance.GetItems ("FilesToAddToArchive").Count);

		foreach (var item in instance.GetItems ("FilesToAddToArchive").ToArray ()) {
			instance.RemoveItem (item);
		}
		instance.SetProperty ("AndroidPackageFormat", "apk");
		instance.SetProperty ("EmbedAssembliesIntoApk", "false");
		Assert.IsTrue (instance.Build ("_BuildApkFastDev", []));
		var files = instance.GetItems ("FilesToAddToArchive").ToArray ();
		Assert.AreEqual (5, files.Length, "Do not append a previous collection's scratch items.");
		Assert.IsTrue (files.All (file => file.EvaluatedInclude.StartsWith ("fast's files/", StringComparison.Ordinal)));
		Assert.IsTrue (files.All (file => !file.GetMetadataValue ("ArchivePath").StartsWith ("dex/", StringComparison.Ordinal)));
	}

	ProjectInstance CreateProject (ProjectCollection collection, string targetName, string format, bool empty)
	{
		var source = XDocument.Load (Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
		var targets = source.Descendants ().Where (element =>
			element.Name.LocalName == "Target" &&
			new [] { "_BuildApkEmbed", "_BuildApkFastDev" }.Contains ((string?) element.Attribute ("Name")))
			.Select (element => new XElement (element)).ToArray ();
		foreach (var target in targets) {
			Assert.AreEqual ("@(FilesToAddToArchive)",
				(string?) target.Elements ().Single (element => element.Name.LocalName == "BuildArchive").Attribute ("FilesToAddToArchive"),
				"The archive writer must consume the constructed items.");
			target.Attribute ("DependsOnTargets")?.Remove ();
			target.Attribute ("Inputs")?.Remove ();
			target.Attribute ("Outputs")?.Remove ();
			// Isolate item construction from unrelated native, managed, and Java packaging work.
			target.Elements ().Where (element => element.Name.LocalName != "ItemGroup" && element.Name.LocalName != "PropertyGroup").Remove ();
			foreach (var element in target.DescendantsAndSelf ()) {
				element.Name = element.Name.LocalName;
			}
		}
		var inputs = new XElement ("ItemGroup");
		if (!empty) {
			foreach (string itemName in new [] { "_DexFile", "_DexFileForFastDev" }) {
				string directory = itemName == "_DexFile" ? "embed's files" : "fast's files";
				foreach (int i in new [] { 1, 2, 3, 4, 1 }) {
					inputs.Add (new XElement (itemName,
						new XAttribute ("Include", $"{directory}/classes{(i == 1 ? "" : i.ToString ())}.dex"),
						new XElement ("ApkName", i == 2 ? " renamed/classes'2.dex " : i == 3 ? " \t " : i == 4 ? "\u2003" : ""),
						new XElement ("ArchivePath", "stale.dex"),
						new XElement ("JavaArchiveEntry", "must-not-be-read-from-a-jar"),
						new XElement ("Unrelated", "discard")));
				}
			}
		}
		var project = new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("AndroidPackageFormat", format),
				new XElement ("EmbedAssembliesIntoApk", targetName == "_BuildApkEmbed" ? "true" : "false")),
			inputs, targets);
		using var reader = project.CreateReader ();
		var root = ProjectRootElement.Create (reader, collection);
		root.FullPath = Path.Combine (Root, "DalvikArchive.proj");
		return new ProjectInstance (root, globalProperties: null, toolsVersion: null, collection);
	}
}
