using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ProcessNativeLibrariesTests : BaseTest
{
	[Test]
	public void ConcurrentProjectDirectories ()
	{
		Parallel.ForEach (new [] { "arm64-v8a", "unknown" }, abi => {
			var task = new ProcessNativeLibraries {
				BuildEngine = new MockBuildEngine (TextWriter.Null),
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (
					Path.Combine (Root, abi), new Dictionary<string, string> ()),
				InputLibraries = [
					new TaskItem ("libSystem.Native.so"),
					new TaskItem (Path.Combine (Root, "unknown", "liblinked.so"), new Dictionary<string, string> {
						{ "Link", "liblinked.so" },
					}),
				],
			};

			Assert.IsTrue (task.Execute ());
			if (abi == "unknown") {
				Assert.IsEmpty (task.OutputLibraries);
			} else {
				CollectionAssert.AreEqual (task.InputLibraries, task.OutputLibraries);
			}
		});
	}

	[Test]
	public void ExcludesMonoRuntimeComponents ()
	{
		var component = new TaskItem ("libmono-component-debugger.so");
		component.SetMetadata ("Abi", "arm64-v8a");
		var runtimeLibrary = new TaskItem ("libSystem.Native.so");
		runtimeLibrary.SetMetadata ("Abi", "arm64-v8a");

		var task = new ProcessNativeLibraries {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			InputLibraries = [component, runtimeLibrary],
		};

		Assert.IsTrue (task.Execute ());
		var outputLibraries = task.OutputLibraries;
		Assert.IsNotNull (outputLibraries);
		if (outputLibraries == null) {
			return;
		}
		Assert.AreEqual (1, outputLibraries.Length);
		Assert.AreEqual (runtimeLibrary.ItemSpec, outputLibraries [0].ItemSpec);
	}
}
