using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GetNativeAotRuntimeProvidersTests : BaseTest
{
	[Test]
	public void ReadsOnlyAdditionalNativeAotProvidersInManifestOrder ()
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string manifestFile = Path.Combine (path, "AndroidManifest.xml");
		XDocument.Parse ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android">
			  <application>
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider" />
			    <provider android:name="mono.MonoRuntimeProvider_1" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_2" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_1" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_2" />
			    <provider />
			  </application>
			</manifest>
			""").Save (manifestFile);
		var task = new GetNativeAotRuntimeProviders {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ManifestFile = manifestFile,
		};
		Assert.IsTrue (task.Execute ());
		CollectionAssert.AreEqual (new [] { "NativeAotRuntimeProvider_2", "NativeAotRuntimeProvider_1" }, task.AdditionalProviderSources);
	}

	[Test]
	public void NoAdditionalProvidersIsValid ()
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string manifestFile = Path.Combine (path, "AndroidManifest.xml");
		XDocument.Parse ("<manifest />").Save (manifestFile);
		var task = new GetNativeAotRuntimeProviders {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ManifestFile = manifestFile,
		};
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (task.AdditionalProviderSources);
	}

	[Test]
	public void MissingManifestIsAnError ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GetNativeAotRuntimeProviders {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			ManifestFile = Path.Combine (Root, "temp", TestName, "missing.xml"),
		};
		Assert.IsFalse (task.Execute ());
		Assert.IsNotEmpty (errors);
	}
}
