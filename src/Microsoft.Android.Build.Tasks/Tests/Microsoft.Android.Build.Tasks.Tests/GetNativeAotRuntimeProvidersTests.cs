using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			    <provider name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_3" />
			    <provider xmlns:other="urn:other" other:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_4" />
			    <other:provider xmlns:other="urn:other" android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_5" />
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

	[TestCase ("0")]
	[TestCase ("1")]
	[TestCase ("10")]
	[TestCase ("001")]
	[TestCase ("2147483647")]
	[TestCase ("12345678901234567890")]
	public void RecoversNonemptyAsciiDecimalSuffixes (string suffix)
	{
		string path = Path.Combine (Root, "temp", TestName);
		var task = RecoverProviders (path, "net.dot.jni.nativeaot.NativeAotRuntimeProvider_" + suffix);
		CollectionAssert.AreEqual (new [] { "NativeAotRuntimeProvider_" + suffix }, task.AdditionalProviderSources);
	}

	[TestCase ("", TestName = "IgnoresEmptyProviderSuffix")]
	[TestCase ("+1", TestName = "IgnoresPlusProviderSuffix")]
	[TestCase ("-1", TestName = "IgnoresMinusProviderSuffix")]
	[TestCase (" 1", TestName = "IgnoresLeadingProviderWhitespace")]
	[TestCase ("1 ", TestName = "IgnoresTrailingProviderWhitespace")]
	[TestCase ("1\t", TestName = "IgnoresProviderTab")]
	[TestCase ("1\n", TestName = "IgnoresProviderNewline")]
	[TestCase ("1.0", TestName = "IgnoresFractionalProviderSuffix")]
	[TestCase ("1extra", TestName = "IgnoresAlphabeticProviderSuffix")]
	[TestCase ("\u0661", TestName = "IgnoresArabicIndicProviderDigit")]
	[TestCase ("\uff11", TestName = "IgnoresFullWidthProviderDigit")]
	[TestCase ("1\u0662", TestName = "IgnoresMixedAsciiAndUnicodeProviderDigits")]
	[TestCase ("1/2", TestName = "IgnoresForwardSlashProviderSuffix")]
	[TestCase ("1\\2", TestName = "IgnoresBackslashProviderSuffix")]
	[TestCase ("../outside", TestName = "IgnoresParentDirectoryProviderSuffix")]
	[TestCase ("1/../../../../../escaped", TestName = "IgnoresForwardSlashProviderTraversal")]
	[TestCase ("1\\..\\..\\..\\..\\..\\escaped", TestName = "IgnoresBackslashProviderTraversal")]
	[TestCase ("/tmp/outside", TestName = "IgnoresUnixAbsoluteProviderSuffix")]
	[TestCase ("C:\\outside", TestName = "IgnoresWindowsAbsoluteProviderSuffix")]
	[TestCase ("\\\\server\\share\\outside", TestName = "IgnoresUncProviderSuffix")]
	public void IgnoresNonDecimalProviderSuffixes (string suffix)
	{
		string path = Path.Combine (Root, "temp", TestName);
		var task = RecoverProviders (path,
			"net.dot.jni.nativeaot.NativeAotRuntimeProvider_" + suffix,
			"net.dot.jni.nativeaot.NativeAotRuntimeProvider_1");
		CollectionAssert.AreEqual (new [] { "NativeAotRuntimeProvider_1" }, task.AdditionalProviderSources);
	}

	[TestCase ("net.dot.jni.nativeaot.NativeAotRuntimeProvider")]
	[TestCase ("net.dot.jni.nativeaot.NativeAotRuntimeProvider_1.extra")]
	[TestCase ("net.dot.jni.nativeaot.extra.NativeAotRuntimeProvider_1")]
	[TestCase ("Net.dot.jni.nativeaot.NativeAotRuntimeProvider_1")]
	[TestCase ("net.dot.jni.nativeaot.nativeAotRuntimeProvider_1")]
	[TestCase ("NativeAotRuntimeProvider_1")]
	[TestCase (".NativeAotRuntimeProvider_1")]
	[TestCase ("mono.MonoRuntimeProvider_1")]
	public void IgnoresOtherProviderTypes (string name)
	{
		var task = RecoverProviders (Path.Combine (Root, "temp", TestName), name);
		Assert.IsEmpty (task.AdditionalProviderSources);
	}

	[Test]
	public void RecoveredProvidersWriteOnlyInsideNativeAotSourceDirectory ()
	{
		string path = Path.Combine (Root, "temp", TestName);
		var recovered = RecoverProviders (path,
			"net.dot.jni.nativeaot.NativeAotRuntimeProvider_2",
			"net.dot.jni.nativeaot.NativeAotRuntimeProvider_1/../../../../../escaped",
			"net.dot.jni.nativeaot.NativeAotRuntimeProvider_1\\..\\..\\..\\..\\..\\escaped",
			"net.dot.jni.nativeaot.NativeAotRuntimeProvider_1",
			"net.dot.jni.nativeaot.NativeAotRuntimeProvider_2");
		CollectionAssert.AreEqual (new [] { "NativeAotRuntimeProvider_2", "NativeAotRuntimeProvider_1" }, recovered.AdditionalProviderSources);

		string outputDirectory = Path.Combine (path, "android");
		var bootstrap = new GenerateNativeAotBootstrapSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputDirectory = outputDirectory,
			TargetName = "App",
			AdditionalProviderSources = recovered.AdditionalProviderSources,
		};
		Assert.IsTrue (bootstrap.Execute ());
		string sourceDirectory = Path.GetFullPath (Path.Combine (outputDirectory, "src", "net", "dot", "jni", "nativeaot"));
		foreach (string source in bootstrap.GeneratedSources) {
			Assert.AreEqual (sourceDirectory, Path.GetDirectoryName (Path.GetFullPath (source)));
			FileAssert.Exists (source);
		}
		CollectionAssert.AreEquivalent (
			bootstrap.GeneratedSources.Select (Path.GetFullPath),
			Directory.GetFiles (path, "*.java", SearchOption.AllDirectories).Select (Path.GetFullPath));
		foreach (string provider in recovered.AdditionalProviderSources) {
			string source = File.ReadAllText (Path.Combine (sourceDirectory, provider + ".java"));
			StringAssert.Contains ($"class {provider}", source);
			StringAssert.Contains ("NativeAotEnvironmentVars.Initialize ();", source);
			StringAssert.Contains ("JavaInteropRuntime.loadLibrary(context);", source);
		}
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

	static GetNativeAotRuntimeProviders RecoverProviders (string path, params string [] names)
	{
		Directory.CreateDirectory (path);
		string manifestFile = Path.Combine (path, "AndroidManifest.xml");
		XNamespace android = "http://schemas.android.com/apk/res/android";
		new XDocument (
			new XElement ("manifest",
				new XAttribute (XNamespace.Xmlns + "android", android),
				new XElement ("application",
					names.Select (name => new XElement ("provider", new XAttribute (android + "name", name))))))
			.Save (manifestFile);
		var task = new GetNativeAotRuntimeProviders {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ManifestFile = manifestFile,
		};
		Assert.IsTrue (task.Execute ());
		return task;
	}
}
