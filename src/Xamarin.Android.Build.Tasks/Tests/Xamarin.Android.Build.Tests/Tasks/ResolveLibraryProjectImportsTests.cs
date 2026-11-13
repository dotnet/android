using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class ResolveLibraryProjectImportsTests : BaseTest
	{
		string path;

		[SetUp]
		public void Setup ()
		{
			path = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (path))
				Directory.Delete (path, recursive: true);
			Directory.CreateDirectory (path);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (path, recursive: true);
		}

		static byte [] CreateZip (params (string Name, string Content) [] entries)
		{
			using var stream = new MemoryStream ();
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				foreach (var (name, content) in entries) {
					using var writer = new StreamWriter (zip.CreateEntry (name).Open ());
					writer.Write (content);
				}
			}
			return stream.ToArray ();
		}

		string CreateAssembly (string name, params (string Name, byte [] Data) [] resources)
		{
			var assemblyPath = Path.Combine (path, $"{name}.dll");
			using var assembly = AssemblyDefinition.CreateAssembly (new AssemblyNameDefinition (name, new Version ()), name, ModuleKind.Dll);
			foreach (var (resourceName, data) in resources) {
				assembly.MainModule.Resources.Add (new EmbeddedResource (resourceName, ManifestResourceAttributes.Public, data));
			}
			assembly.Write (assemblyPath);
			return assemblyPath;
		}

		ResolveLibraryProjectImports CreateTask (List<BuildWarningEventArgs> warnings, params ITaskItem [] assemblies)
		{
			var lp = Path.Combine (path, "lp");
			return new ResolveLibraryProjectImports {
				BuildEngine = new MockBuildEngine (TestContext.Out, warnings: warnings),
				Assemblies = assemblies,
				ImportsDirectory = "jl",
				NativeImportsDirectory = "nl",
				OutputImportDirectory = lp,
				AssemblyIdentityMapFile = Path.Combine (lp, "map.cache"),
				CacheFile = Path.Combine (path, "libraryprojectimports.cache"),
			};
		}

		[Test]
		public void LegacyEmbeddedContentIsExtractedWithDeprecationWarning ([Values] bool fromNuGet)
		{
			var assemblyPath = CreateAssembly ("Legacy",
				("__AndroidLibraryProjects__.zip", CreateZip (
					("library_project_imports/res/values/strings.xml", "<resources />"),
					("library_project_imports/assets/foo.txt", "foo"))),
				("__AndroidNativeLibraries__.zip", CreateZip (
					("native_library_imports/arm64-v8a/libfoo.so", "foo"))));
			var assembly = new TaskItem (assemblyPath);
			if (fromNuGet) {
				assembly.SetMetadata ("NuGetPackageId", "Legacy.Package");
				assembly.SetMetadata ("NuGetPackageVersion", "1.0.0");
			}

			// The second run takes the path where the assembly hash matches the stamp
			for (int i = 0; i < 2; i++) {
				var warnings = new List<BuildWarningEventArgs> ();
				var task = CreateTask (warnings, assembly);
				Assert.IsTrue (task.Execute (), $"Task should have succeeded. ({i})");

				var warning = warnings.SingleOrDefault (w => w.Code == "XA0150");
				Assert.IsNotNull (warning, $"XA0150 should have been logged. ({i})");
				StringAssert.Contains (assemblyPath, warning.Message);
				if (fromNuGet) {
					StringAssert.Contains ("'Legacy.Package' version '1.0.0'", warning.Message);
				}

				var resourceDirectory = task.ResolvedResourceDirectories?.SingleOrDefault ();
				Assert.IsNotNull (resourceDirectory, $"A resource directory should have been resolved. ({i})");
				FileAssert.Exists (Path.Combine (resourceDirectory.ItemSpec, "values", "strings.xml"));
				var assetDirectory = task.ResolvedAssetDirectories?.SingleOrDefault ();
				Assert.IsNotNull (assetDirectory, $"An asset directory should have been resolved. ({i})");
				FileAssert.Exists (Path.Combine (assetDirectory.ItemSpec, "foo.txt"));
				var extractedDirectory = task.ExtractedDirectories?.SingleOrDefault ();
				Assert.IsNotNull (extractedDirectory, $"An extracted directory should have been returned. ({i})");
				FileAssert.Exists (Path.Combine (extractedDirectory.ItemSpec, "nl", "arm64-v8a", "libfoo.so"));
			}
		}

		[Test]
		public void AssemblyWithoutLegacyContentDoesNotWarn ([Values] bool previouslyLegacy)
		{
			if (previouslyLegacy) {
				// A previous version of the assembly embedded a .jar and resources
				var legacyPath = CreateAssembly ("Modern",
					("classes.jar", CreateZip (("META-INF/MANIFEST.MF", "Manifest-Version: 1.0"))),
					("__AndroidLibraryProjects__.zip", CreateZip (("library_project_imports/res/values/strings.xml", "<resources />"))));
				var legacyWarnings = new List<BuildWarningEventArgs> ();
				Assert.IsTrue (CreateTask (legacyWarnings, new TaskItem (legacyPath)).Execute (), "Legacy task should have succeeded.");
				Assert.IsTrue (legacyWarnings.Any (w => w.Code == "XA0150"), "XA0150 should have been logged for the legacy assembly.");
			}

			var assemblyPath = CreateAssembly ("Modern", ("SomeResource.txt", new byte [] { 1, 2, 3 }));
			// The second run checks that nothing left over from the legacy assembly is treated as up to date
			for (int i = 0; i < 2; i++) {
				var warnings = new List<BuildWarningEventArgs> ();
				var task = CreateTask (warnings, new TaskItem (assemblyPath));
				Assert.IsTrue (task.Execute (), $"Task should have succeeded. ({i})");
				Assert.IsEmpty (warnings, $"No warnings should have been logged. ({i})");
				Assert.IsEmpty (task.ResolvedResourceDirectories, $"No resource directories should have been resolved. ({i})");
				Assert.IsEmpty (task.Jars, $"No jars should have been resolved. ({i})");
			}
			Assert.IsEmpty (Directory.GetFileSystemEntries (Path.Combine (path, "lp")).Where (e => Path.GetFileName (e) != "map.cache"),
				"Nothing should remain extracted for the assembly.");
		}

		[Test]
		public void AssemblyIdentityMapResolvesAarDirectories ()
		{
			var lp = Path.Combine (path, "lp");
			Directory.CreateDirectory (lp);
			var mapFile = Path.Combine (lp, "map.cache");
			File.WriteAllLines (mapFile, new [] { "Foo.dll", "Bar.dll" });
			File.WriteAllLines (Path.Combine (lp, AssemblyIdentityMap.AarLibraryMapFileName), new [] {
				"0ABCDEF1=/packages/foo/foo.aar",
				"00000001=/packages/bar/bar.aar",
			});

			var map = new AssemblyIdentityMap ();
			map.Load (mapFile);
			Assert.AreEqual ("Foo.dll", map.GetAssemblyNameForImportDirectory ("0"));
			Assert.AreEqual ("/packages/foo/foo.aar", map.GetAssemblyNameForImportDirectory ("0ABCDEF1"));
			Assert.AreEqual ("/packages/bar/bar.aar", map.GetAssemblyNameForImportDirectory ("00000001"), ".aar directories should take precedence over numeric indexes.");
			Assert.AreEqual ("", map.GetAssemblyNameForImportDirectory ("2"), "An index equal to the number of entries should not be resolved.");
			Assert.AreEqual ("", map.GetAssemblyNameForImportDirectory ("FFFFFFFF"));
		}
	}
}
