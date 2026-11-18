#nullable enable

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class ResolveLibraryProjectImportsTests : BaseTest
{
	[Test]
	public void DuplicateJarPreservesFirstMetadata ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string aar = Path.Combine (directory, "library.aar");
		using (var archive = ZipFile.Open (aar, ZipArchiveMode.Create)) {
			archive.CreateEntry ("classes.jar").Open ().Dispose ();
		}
		var first = new TaskItem (aar);
		first.SetMetadata ("NuGetPackageId", "First");
		var second = new TaskItem (aar);
		second.SetMetadata ("NuGetPackageId", "Second");
		var task = new ResolveLibraryProjectImports {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
			ImportsDirectory = "jl",
			NativeImportsDirectory = "nl",
			OutputDirectory = directory,
			OutputImportDirectory = "lp",
			AssemblyIdentityMapFile = "lp.map",
			Assemblies = [],
			AarLibraries = [first, second],
		};

		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (1, task.Jars?.Length);
		var jar = task.Jars? [0] ?? throw new AssertionException ("Expected a resolved jar.");
		string expectedJar = Path.Combine (directory, "lp", "0", "jl", "classes.jar");
		Assert.AreEqual (expectedJar, jar.ItemSpec);
		FileAssert.Exists (expectedJar);
		Assert.AreEqual ("First", jar.GetMetadata ("NuGetPackageId"));
	}
}
