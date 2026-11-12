using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.Properties;

namespace Xamarin.Android.Build.Tests;

[TestFixture (false)]
[TestFixture (true)]
public class RuntimeIdentifierToAbiTargetsTests : BaseTest
{
	readonly bool plural;

	public RuntimeIdentifierToAbiTargetsTests (bool plural) => this.plural = plural;

	string directory => Path.Combine (Root, "temp", TestName, plural.ToString ());

	[TestCase ("android-arm", "armeabi-v7a")]
	[TestCase ("android-arm64", "arm64-v8a")]
	[TestCase ("android-x86", "x86")]
	[TestCase ("android-x64", "x86_64")]
	[TestCase ("ANDROID35-ARM64", "arm64-v8a")]
	public void MapsRuntimeIdentifiers (string rid, string expected)
	{
		var result = Build (plural ? "" : rid, plural ? rid : "");
		Assert.AreEqual (0, result.ExitCode, result.Output);
		Assert.AreEqual (expected, File.ReadAllText (Path.Combine (directory, "abis.txt")).Trim ());
		StringAssert.DoesNotContain ("XA0035", result.Output);
		StringAssert.DoesNotContain ("XA0036", result.Output);
	}

	[Test]
	public void PluralIdentifiersTakePrecedenceAndPreserveOrder ()
	{
		var result = Build ("invalid-single-rid", "android-arm64;android-arm;android-x86;android-x64");
		Assert.AreEqual (0, result.ExitCode, result.Output);
		CollectionAssert.AreEqual (new [] { "arm64-v8a", "armeabi-v7a", "x86", "x86_64" },
			File.ReadAllLines (Path.Combine (directory, "abis.txt")));
		StringAssert.DoesNotContain ("XA0035", result.Output);
	}

	[TestCase ("linux-arm64")]
	[TestCase ("win-x64")]
	[TestCase ("android-riscv64")]
	[TestCase ("android-arm64-extra")]
	[TestCase ("android")]
	[TestCase ("")]
	[TestCase ("*")]
	[TestCase ("?")]
	[TestCase ("android-*")]
	[TestCase ("?ndroid-arm64")]
	[TestCase ("linux-arm64;win-x64")]
	[TestCase ("android-arm64;linux-arm64;win-x64;android-x86")]
	public void ReportsEveryInvalidIdentifier (string rids)
	{
		var result = Build (plural ? "" : rids, plural ? rids : "");
		Assert.AreNotEqual (0, result.ExitCode, result.Output);
		var invalid = rids.Split (';').Where (rid => rid != "android-arm64" && rid != "android-x86").ToArray ();
		var errors = result.Output.Split ('\n').Where (line => line.Contains ("error XA0035:", StringComparison.Ordinal)).ToArray ();
		Assert.AreEqual (invalid.Length, errors.Length, result.Output);
		foreach (string rid in invalid) {
			string message = string.Format (CultureInfo.InvariantCulture, Resources.XA0035, rid);
			Assert.AreEqual (1, errors.Count (line => line.Contains (message, StringComparison.Ordinal)), result.Output);
		}
	}

	[Test]
	public void PresetAbisWarnAndAreReplaced ()
	{
		var result = Build (plural ? "" : "android-arm64", plural ? "android-arm64" : "", supportedAbis: "x86");
		Assert.AreEqual (0, result.ExitCode, result.Output);
		StringAssert.Contains ("warning XA0036: " + Resources.XA0036, result.Output);
		Assert.AreEqual ("arm64-v8a", File.ReadAllText (Path.Combine (directory, "abis.txt")).Trim ());
	}

	[Test]
	public void PresetAbisWithoutIdentifiersArePreserved ()
	{
		var result = Build ("", "", supportedAbis: "x86");
		Assert.AreEqual (0, result.ExitCode, result.Output);
		StringAssert.Contains ("warning XA0036: " + Resources.XA0036, result.Output);
		Assert.AreEqual ("x86", File.ReadAllText (Path.Combine (directory, "abis.txt")).Trim ());
	}

	[TestCase ("", "", "")]
	[TestCase ("linux-arm64", "win-x64", "x86")]
	public void LibrariesDoNotMapOrDiagnoseIdentifiers (string rid, string rids, string abis)
	{
		var result = Build (rid, rids, supportedAbis: abis, application: false);
		Assert.AreEqual (0, result.ExitCode, result.Output);
		Assert.AreEqual (abis, File.ReadAllText (Path.Combine (directory, "abis.txt")).Trim ());
		StringAssert.DoesNotContain ("XA0035", result.Output);
		StringAssert.DoesNotContain ("XA0036", result.Output);
	}

	(int ExitCode, string Output) Build (string rid, string rids, string supportedAbis = "", bool application = true)
	{
		Directory.CreateDirectory (directory);
		foreach (string file in new [] { "android-arm64", "android-x86", "a" }) {
			File.WriteAllText (Path.Combine (directory, file), "");
		}
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks",
			"Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Tooling.targets");
		string assembly = typeof (AndroidError).Assembly.Location;
		string project = Path.Combine (directory, "tooling.proj");
		// Skip unrelated SDK/JDK discovery while exercising the production RID mapping and diagnostics.
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("AndroidApplication", application),
				new XElement ("RuntimeIdentifier", rid),
				new XElement ("RuntimeIdentifiers", rids),
				new XElement ("AndroidSupportedAbis", supportedAbis),
				new XElement ("_XamarinAndroidBuildTasksAssembly", assembly),
				new XElement ("_AndroidUseResolvedTooling", "true")),
			new XElement ("Import", new XAttribute ("Project", targets)),
			new XElement ("UsingTask", new XAttribute ("TaskName", typeof (AndroidError).FullName ?? throw new InvalidOperationException ()),
				new XAttribute ("AssemblyFile", assembly)),
			new XElement ("UsingTask", new XAttribute ("TaskName", typeof (AndroidWarning).FullName ?? throw new InvalidOperationException ()),
				new XAttribute ("AssemblyFile", assembly)),
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", "_ResolveAndroidTooling"),
				new XElement ("WriteLinesToFile",
					new XAttribute ("File", Path.Combine (directory, "abis.txt")),
					new XAttribute ("Lines", "$(AndroidSupportedAbis)"),
					new XAttribute ("Overwrite", "true")))))
			.Save (project);
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-clp:NoSummary", "-nr:false");
		return (result.ExitCode, result.Output + result.Error);
	}
}
