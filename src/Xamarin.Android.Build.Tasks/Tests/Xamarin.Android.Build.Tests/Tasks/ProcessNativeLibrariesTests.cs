using Microsoft.Build.Utilities;
using NUnit.Framework;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ProcessNativeLibrariesTests : BaseTest
{
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
			KnownRuntimeNativeLibraries = [],
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
