#nullable enable

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GenerateR8JniManifestProguardConfigurationTests : BaseTest
{
	string temp = "";
	string manifest = "";
	string output = "";

	[SetUp]
	public void SetUp ()
	{
		temp = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (temp);
		manifest = Path.Combine (temp, "AndroidManifest.xml");
		output = Path.Combine (temp, "manifest_rules.txt");
	}

	[TearDown]
	public void TearDown ()
	{
		Directory.Delete (temp, recursive: true);
	}

	[Test]
	public void WritesDeterministicManifestClassRulesWithoutResolvingResources ()
	{
		File.WriteAllText (manifest, """
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			  <application
			      android:name=".App"
			      android:backupAgent="Backup"
			      android:appComponentFactory="other.Factory"
			      android:zygotePreloadName=".Zygote"
			      android:icon="@drawable/icon">
			    <activity android:name="MainActivity" />
			    <activity android:name="com.example.MainActivity" />
			    <activity-alias android:name=".Alias" android:targetActivity="MainActivity" />
			    <service android:name=".Service" />
			    <service android:name=".Service" />
			    <receiver />
			    <receiver android:name="Receiver" />
			    <provider android:name="other.Provider" />
			    <process android:name=".AppProcess" />
			  </application>
			  <instrumentation android:name="Instrumentation" />
			</manifest>
			""");

		var task = CreateTask ();
		Assert.IsTrue (task.Execute (), "Task should succeed without resolving @drawable/icon.");
		string rules = File.ReadAllText (output);
		Assert.AreEqual ("""
			-keep class com.example.App { <init>(); }
			-keep class com.example.AppProcess { <init>(); }
			-keep class com.example.Backup { <init>(); }
			-keep class com.example.Instrumentation { <init>(); }
			-keep class com.example.MainActivity { <init>(); }
			-keep class com.example.Receiver { <init>(); }
			-keep class com.example.Service { <init>(); }
			-keep class com.example.Zygote { <init>(); }
			-keep class other.Factory { <init>(); }
			-keep class other.Provider { <init>(); }
			""" + "\n", rules);
		StringAssert.DoesNotContain ("com.example.Alias", rules, "An activity alias name is not a Java class.");
		StringAssert.DoesNotContain ("\r", rules, "Manifest rules should use deterministic LF line endings.");
	}

	[TestCase (".Relative", "com.example.Relative")]
	[TestCase ("Relative", "com.example.Relative")]
	[TestCase ("other.FullyQualified", "other.FullyQualified")]
	public void NormalizesManifestClassName (string name, string expected)
	{
		File.WriteAllText (manifest, $$"""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example">
			  <application>
			    <activity android:name="{{name}}" />
			  </application>
			</manifest>
			""");

		Assert.IsTrue (CreateTask ().Execute ());
		Assert.AreEqual ($"-keep class {expected} {{ <init>(); }}\n", File.ReadAllText (output));
	}

	[Test]
	public void InvalidManifestUsesXA4327 ()
	{
		File.WriteAllText (manifest, "<manifest>");
		var errors = new List<BuildErrorEventArgs> ();

		Assert.IsFalse (CreateTask (errors).Execute ());
		Assert.That (errors, Has.Count.EqualTo (1));
		Assert.AreEqual ("XA4327", errors [0].Code);
	}

	[Test]
	public void MissingPackageUsesXA4327 ()
	{
		File.WriteAllText (manifest, "<manifest />");
		var errors = new List<BuildErrorEventArgs> ();

		Assert.IsFalse (CreateTask (errors).Execute ());
		Assert.That (errors, Has.Count.EqualTo (1));
		Assert.AreEqual ("XA4327", errors [0].Code);
	}

	GenerateR8JniManifestProguardConfiguration CreateTask (IList<BuildErrorEventArgs>? errors = null) =>
		new GenerateR8JniManifestProguardConfiguration {
			BuildEngine = errors == null ? new MockBuildEngine (TestContext.Out) : new MockBuildEngine (TestContext.Out, errors),
			AndroidManifestFile = manifest,
			OutputFile = output,
		};
}
