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
	[TestCase ("arm64-v8a", "android-x64", "x86/libfoo.so", "arm64-v8a")]
	[TestCase ("", "android-x64", "x86/libfoo.so", "x86_64")]
	[TestCase ("", "", "x86/libfoo.so", "x86")]
	[TestCase ("", "invalid", "x86/libfoo.so", "armeabi-v7a")]
	[TestCase ("", "", "unknown/libfoo.so", "armeabi-v7a")]
	[TestCase ("", "", "", "armeabi-v7a")]
	public void MetadataPrecedence (string abi, string rid, string link, string expected)
	{
		var library = new TaskItem (Path.Combine ("armeabi-v7a", "libfoo.so"));
		library.SetMetadata ("Abi", abi);
		library.SetMetadata ("RuntimeIdentifier", rid);
		library.SetMetadata ("Link", link);
		var task = new ProcessNativeLibraries {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (
				Root, new Dictionary<string, string> ()),
			InputLibraries = [library],
		};

		Assert.IsTrue (task.Execute ());
		CollectionAssert.AreEqual (new [] { library }, task.OutputLibraries);
		Assert.AreEqual (expected, Microsoft.Android.Build.Tasks.AndroidRidAbiHelper.GetNativeLibraryAbi (library, task.TaskEnvironment));
		Assert.AreEqual (abi, library.GetMetadata ("Abi"), "ABI inference must not replace explicit metadata.");
	}

	[Test]
	public void ConcurrentProjectDirectories ()
	{
		Parallel.ForEach (new [] { "arm64-v8a", "x86_64" }, abi => {
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
			CollectionAssert.AreEqual (task.InputLibraries, task.OutputLibraries);
			foreach (var library in task.InputLibraries) {
				Assert.AreEqual (abi, Microsoft.Android.Build.Tasks.AndroidRidAbiHelper.GetNativeLibraryAbi (library, task.TaskEnvironment));
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
