using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class AndroidSdkRuntimePackTargetsTests : BaseTest
{
	static readonly string [] AndroidFrameworks = ["Microsoft.Android", "Microsoft.Android.Runtimes"];

	[TestCase ("CoreCLR")]
	[TestCase ("NativeAOT")]
	public void MixedRuntimeIdentifiersDoNotDownloadAnX86Pack (string runtime)
	{
		using var document = ResolveFrameworkReferences (runtime, "android-arm64;android-x86");
		var items = document.RootElement.GetProperty ("Items");
		var unavailable = items.GetProperty ("UnavailableRuntimePack").EnumerateArray ()
			.Where (item => AndroidFrameworks.Contains (item.GetProperty ("Identity").GetString ()))
			.ToArray ();
		CollectionAssert.AreEquivalent (AndroidFrameworks, unavailable.Select (item => item.GetProperty ("Identity").GetString ()));
		Assert.That (unavailable.Select (item => item.GetProperty ("RuntimeIdentifier").GetString ()), Is.All.EqualTo ("android-x86"));

		var downloads = items.GetProperty ("PackageDownload").EnumerateArray ()
			.Select (item => item.GetProperty ("Identity").GetString () ?? throw new InvalidOperationException ("A package has no ID."))
			.ToArray ();
		Assert.IsTrue (downloads.Any (name => name.StartsWith ($"Microsoft.Android.Runtime.{runtime}.", StringComparison.Ordinal) && name.EndsWith (".android-arm64", StringComparison.Ordinal)));
		Assert.IsFalse (downloads.Any (name => name.StartsWith ("Microsoft.Android.Runtime.", StringComparison.Ordinal) && name.EndsWith (".android-x86", StringComparison.Ordinal)));
	}

	JsonDocument ResolveFrameworkReferences (string runtime, string runtimeIdentifiers)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks",
			"Microsoft.Android.Sdk", "in", "Microsoft.Android.Sdk.BundledVersions.in.targets");
		string project = Path.Combine (directory, "runtime-packs.proj");
		string frameworkName = typeof (AndroidSdkRuntimePackTargetsTests).Assembly.GetCustomAttribute<TargetFrameworkAttribute> ()?.FrameworkName
			?? throw new InvalidOperationException ("The test assembly has no target framework.");
		var framework = new FrameworkName (frameworkName);
		string targetFramework = $"net{framework.Version.Major}.{framework.Version.Minor}";
		new XDocument (
			new XElement ("Project", new XAttribute ("Sdk", "Microsoft.NET.Sdk"),
				new XElement ("PropertyGroup",
					new XElement ("TargetFramework", targetFramework),
					new XElement ("SelfContained", "true"),
					new XElement ("UseAppHost", "false"),
					new XElement ("EnableDefaultItems", "false"),
					new XElement ("PublishAot", runtime == "NativeAOT"),
					new XElement ("PublishAotUsingRuntimePack", runtime == "NativeAOT"),
					new XElement ("AllowPublishAotWithoutRuntimeIdentifier", "true"),
					new XElement ("_AndroidRuntimePackRuntime", runtime),
					new XElement ("RuntimeIdentifiers", runtimeIdentifiers)),
				new XElement ("ItemGroup",
					new XElement ("FrameworkReference", new XAttribute ("Include", "Microsoft.Android")),
					new XElement ("FrameworkReference", new XAttribute ("Include", "Microsoft.Android.Runtimes"))),
				new XElement ("Import", new XAttribute ("Project", targets)),
				// Replace generated version and TFM tokens so the SDK resolver can run without a workload.
				new XElement ("ItemGroup",
					new XElement ("KnownFrameworkReference",
						new XAttribute ("Update", "Microsoft.Android;Microsoft.Android.Runtimes"),
						new XAttribute ("TargetFramework", targetFramework),
						new XAttribute ("TargetingPackVersion", "1.0.0"),
						new XAttribute ("LatestRuntimeFrameworkVersion", "1.0.0"),
						new XAttribute ("RuntimePackLatestVersion", "1.0.0"),
						new XAttribute ("RuntimePackVersion", "1.0.0")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var startInfo = new ProcessStartInfo (dotnet) {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		startInfo.Environment ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
		foreach (string argument in new [] {
			"msbuild", project, "-t:ProcessFrameworkReferences",
			"-getItem:RuntimePack,UnavailableRuntimePack,PackageDownload",
			"-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false",
			"-v:quiet", "-nologo", "-nr:false",
		}) {
			startInfo.ArgumentList.Add (argument);
		}
		var result = NativeToolTestHelper.Capture (startInfo, TimeSpan.FromMinutes (2));
		Assert.AreEqual (0, result.ExitCode, result.StandardOutput + result.StandardError);
		return JsonDocument.Parse (result.StandardOutput);
	}
}
