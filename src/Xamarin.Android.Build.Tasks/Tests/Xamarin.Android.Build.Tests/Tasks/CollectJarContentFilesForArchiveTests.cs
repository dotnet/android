#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class CollectJarContentFilesForArchiveTests
{
	string? tempDirectory;

	string TempDirectory => tempDirectory ?? throw new InvalidOperationException ("Setup has not run.");

	[SetUp]
	public void Setup ()
	{
		tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
		Directory.CreateDirectory (tempDirectory);
	}

	[TearDown]
	public void TearDown ()
	{
		if (!tempDirectory.IsNullOrEmpty () && Directory.Exists (tempDirectory))
			Directory.Delete (tempDirectory, recursive: true);
	}

	[Test]
	public void BuildTimeJavaResourcesAreNotPackaged ()
	{
		var metadataJar = CreateArchive (
			"metadata.jar",
			("META-INF/kotlin-project-structure-metadata.json", "{}"),
			("commonMain/default/manifest", "manifest"),
			("commonMain/default/linkdata/module", "module"),
			("commonMain/default/linkdata/package_example/0_example.knm", "metadata"),
			("classes.jar", "nested jar"),
			("META-INF/kotlinx_metadata.version", "1.0"));
		var runtimeJar = CreateArchive (
			"runtime.jar",
			("META-INF/services/example.Service", "example.ServiceImpl"),
			("kotlin/kotlin.kotlin_builtins", "builtins"),
			("META-INF/androidx.collection_collection.version", "1.0"),
			("META-INF/androidx/collection/collection/LICENSE.txt", "license"),
			("META-INF/maven/example/library/pom.xml", "pom"),
			("nested.jar", "nested jar"),
			("metadata/example.knm", "metadata"),
			("R.txt", "resources"),
			("proguard.txt", "rules"),
			("META-INF/proguard/library.pro", "rules"),
			("META-INF/com.android.tools/proguard/library.pro", "rules"),
			("META-INF/com.android.tools/r8/library.pro", "rules"),
			("META-INF/com.android.tools/r8-from-1.6.0/library.pro", "rules"),
			("META-INF/com/android/build/gradle/aar-metadata.properties", "metadata"));

		var task = new CollectJarContentFilesForArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			JavaLibraries = [new TaskItem (metadataJar), new TaskItem (runtimeJar)],
		};

		Assert.IsTrue (task.RunTask (), "Task should succeed.");

		var archivePaths = task.FilesToAddToArchive.Select (item => item.GetMetadata ("ArchivePath")).ToArray ();
		string [] expected = [
			"META-INF/kotlinx_metadata.version",
			"META-INF/androidx.collection_collection.version",
			"META-INF/services/example.Service",
			"kotlin/kotlin.kotlin_builtins",
			"META-INF/androidx/collection/collection/LICENSE.txt",
			"META-INF/maven/example/library/pom.xml",
		];
		CollectionAssert.AreEquivalent (expected, archivePaths);
	}

	[Test]
	public void IncludePatternCanRestoreBuildTimeJavaResource ()
	{
		var jar = CreateArchive ("library.jar", ("classes.jar", "nested jar"));
		var task = new CollectJarContentFilesForArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			IncludeFiles = ["classes.jar"],
			JavaLibraries = [new TaskItem (jar)],
		};

		Assert.IsTrue (task.RunTask (), "Task should succeed.");
		Assert.AreEqual ("classes.jar", task.FilesToAddToArchive.Single ().GetMetadata ("ArchivePath"));
	}

	string CreateArchive (string fileName, params (string Path, string Contents) [] entries)
	{
		var path = Path.Combine (TempDirectory, fileName);
		using (var stream = File.Create (path))
		using (var archive = ZipArchive.Create (stream)) {
			foreach (var entry in entries) {
				archive.AddEntry (entry.Path, entry.Contents, encoding: Encoding.UTF8);
			}
		}
		return path;
	}
}
