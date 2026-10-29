using System;
using System.IO;
using System.Reflection;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests;

[TestFixture]
[Category ("AssemblyStore")]
[Category ("NativeAOTIgnore")]
public class AssemblyStoreTests
{
	[TestCase ("System.Xml.ReaderWriter")]
	[TestCase ("System.Xml.XDocument")]
	public void RejectedVersionsDoNotPreventLoadingBundledAssembly (string name)
	{
		for (int probe = 0; probe < 2000; ++probe) {
			// Different identities bypass the binder's failed-bind cache and probe the same image.
			var identity = new AssemblyName {
				Name = name,
				Version = new Version (99 + probe / 65536, 0, 0, probe % 65536),
			};
			var error = Assert.Catch (() => Assembly.Load (identity), $"Unexpected successful load: {identity}");
			Assert.IsTrue (error is FileNotFoundException or FileLoadException, $"Unexpected error: {error}");
		}
		Assert.AreEqual (name, Assembly.Load (name).GetName ().Name);
	}
}
