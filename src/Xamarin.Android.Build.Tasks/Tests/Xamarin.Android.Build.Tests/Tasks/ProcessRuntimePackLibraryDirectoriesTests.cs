using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class ProcessRuntimePackLibraryDirectoriesTests : BaseTest
{
	[Test]
	public void OnlyArchiveDsoStubAndSystemLibrariesAreRemoved ()
	{
		string directory = Path.Combine (Root, "temp", nameof (OnlyArchiveDsoStubAndSystemLibrariesAreRemoved));
		Directory.CreateDirectory (directory);

		var items = new List<ITaskItem> ();
		var expectedRemoved = new List<string> ();
		var expectedKept = new List<string> ();

		void AddLibrary (string fileName, bool shouldBeRemoved)
		{
			string path = Path.Combine (directory, fileName);
			File.WriteAllText (path, "");
			items.Add (new TaskItem (path, new Dictionary<string, string> {
				["RuntimeIdentifier"] = "android-arm64",
				["NuGetPackageId"] = "Microsoft.Android.Runtime.CoreCLR.37.android-arm64",
			}));
			(shouldBeRemoved ? expectedRemoved : expectedKept).Add (fileName);
		}

		// Excluded: the archive DSO stub (a placeholder used when an assembly store has no
		// payload) and the small set of system libraries that ship in the runtime pack for
		// linking purposes only, never for packaging into the app.
		AddLibrary ("libarchive-dso-stub.so", shouldBeRemoved: true);
		AddLibrary ("libc.so", shouldBeRemoved: true);
		AddLibrary ("libdl.so", shouldBeRemoved: true);
		AddLibrary ("liblog.so", shouldBeRemoved: true);
		AddLibrary ("libm.so", shouldBeRemoved: true);
		AddLibrary ("libz.so", shouldBeRemoved: true);

		// Kept: real runtime/application native libraries. The CoreCLR `libxamarin-app*-stub.so`
		// no longer exists (the native host defines its own `assembly_store` symbol with no
		// libxamarin-app.so DT_NEEDED, and the stub-copying/packaging pipeline was removed), so a
		// similarly-named library must not be special-cased by this task anymore.
		AddLibrary ("libxamarin-app-debug-stub.so", shouldBeRemoved: false);
		AddLibrary ("libxamarin-app-release-stub.so", shouldBeRemoved: false);
		AddLibrary ("libmonodroid.so", shouldBeRemoved: false);
		AddLibrary ("libSystem.Security.Cryptography.Native.Android.so", shouldBeRemoved: false);

		var task = new ProcessRuntimePackLibraryDirectories {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedFilesToPublish = items.ToArray (),
		};

		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (1, task.RuntimePackLibraryDirectories.Length, "All matched items share a single runtime identifier, so only one library directory should be reported.");
		Assert.AreEqual (expectedRemoved.Count, task.NativeLibrariesToRemove.Length, "Only the archive DSO stub and system libraries should be removed.");

		var removedNames = new HashSet<string> (
			task.NativeLibrariesToRemove.Select (item => Path.GetFileName (item.ItemSpec)),
			StringComparer.OrdinalIgnoreCase
		);
		foreach (string name in expectedRemoved) {
			Assert.IsTrue (removedNames.Contains (name), $"'{name}' should have been removed.");
		}
		foreach (string name in expectedKept) {
			Assert.IsFalse (removedNames.Contains (name), $"'{name}' should not have been removed.");
		}
	}
}
