using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class LinkerCacheTargetsTests : BaseTest
{
	[Test]
	public void RuntimeChangesInvalidateEveryRidAndUnchangedBuildsSkipLinking ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string tasks = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks");
		var cache = LoadTarget (Path.Combine (tasks, "Xamarin.Android.Common.targets"), "_CreatePropertiesCache");
		string linker = typeof (LinkerCacheTargetsTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Single (a => a.Key == "ILLinkPath").Value ?? throw new InvalidOperationException ("Missing ILLinkPath.");
		string linkerDirectory = Path.GetDirectoryName (linker) ?? throw new InvalidOperationException ("Missing linker directory.");
		var link = LoadTarget (Path.Combine (linkerDirectory, "..", "..", "build", "Microsoft.NET.ILLink.targets"), "_RunILLink");
		// Keep the SDK's incremental contract, without running the trimmer.
		link.RemoveNodes ();
		link.Add (new XElement ("WriteLinesToFile",
			new XAttribute ("File", "$(_LinkSemaphore)"),
			new XAttribute ("Lines", "$(_AndroidRuntime)"),
			new XAttribute ("Overwrite", "true")));

		string project = Path.Combine (directory, "linker.proj");
		string properties = Path.Combine (directory, "build.props");
		string assembly = Path.Combine (directory, "app.dll");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("_XamarinAndroidBuildTasksAssembly", typeof (Microsoft.Android.Tasks.GenerateNativeAotBootstrapSources).Assembly.Location),
				new XElement ("MSBuildAllProjects", "$(MSBuildProjectFullPath)"),
				new XElement ("_AndroidBuildPropertiesCache", properties),
				new XElement ("_AdbPropertiesCache", Path.Combine (directory, "adb.props")),
				new XElement ("_LinkSemaphore", Path.Combine (directory, "$(RuntimeIdentifier)", "linked", "Link.semaphore"))),
			new XElement ("ItemGroup",
				new XElement ("ManagedAssemblyToLink", new XAttribute ("Include", assembly))),
			new XElement ("Import", new XAttribute ("Project",
				Path.Combine (tasks, "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.AssemblyResolution.targets"))),
			cache, link,
			new XElement ("Target", new XAttribute ("Name", "_SetupDesignTimeBuildForBuild")),
			new XElement ("Target", new XAttribute ("Name", "_SetLatestTargetFrameworkVersion")),
			new XElement ("Target", new XAttribute ("Name", "_ResolveMonoAndroidSdks")),
			new XElement ("Target", new XAttribute ("Name", "_ComputeManagedAssemblyToLink")),
			new XElement ("Target", new XAttribute ("Name", "PrepareForILLink"))))
			.Save (project);
		File.WriteAllText (assembly, "unchanged assembly");
		DateTime inputTimestamp = DateTime.UtcNow.AddMinutes (-5);
		File.SetLastWriteTimeUtc (project, inputTimestamp);
		File.SetLastWriteTimeUtc (assembly, inputTimestamp);

		string [] rids = ["android-arm64", "android-x64"];
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		foreach (string runtime in new [] { "CoreCLR", "MonoVM", "CoreCLR" }) {
			if (File.Exists (properties)) {
				File.SetLastWriteTimeUtc (properties, DateTime.UtcNow.AddMinutes (-2));
			}
			foreach (string rid in rids) {
				string semaphore = Path.Combine (directory, rid, "linked", "Link.semaphore");
				Directory.CreateDirectory (Path.GetDirectoryName (semaphore) ?? throw new InvalidOperationException ());
				if (File.Exists (semaphore)) {
					File.SetLastWriteTimeUtc (semaphore, DateTime.UtcNow.AddMinutes (-1));
				}
			}

			for (int run = 0; run < 2; run++) {
				foreach (string rid in rids) {
					string semaphore = Path.Combine (directory, rid, "linked", "Link.semaphore");
					DateTime cacheTimestamp = File.GetLastWriteTimeUtc (properties);
					DateTime linkTimestamp = File.GetLastWriteTimeUtc (semaphore);
					var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project,
						"-t:_RunILLink", "-p:_AndroidRuntime=" + runtime, "-p:RuntimeIdentifier=" + rid, "-v:normal", "-nr:false");
					Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
					Assert.AreEqual (runtime, File.ReadAllText (semaphore).Trim (), $"{rid} must link for {runtime}.");
					CollectionAssert.Contains (File.ReadAllLines (properties), "_androidruntime=" + runtime.ToLowerInvariant ());
					if (run == 0) {
						StringAssert.DoesNotContain ("Skipping target \"_RunILLink\"", result.Output);
					} else {
						StringAssert.Contains ("Skipping target \"_AndroidInvalidateLinkerCache\"", result.Output);
						StringAssert.Contains ("Skipping target \"_RunILLink\"", result.Output);
						Assert.AreEqual (cacheTimestamp, File.GetLastWriteTimeUtc (properties));
						Assert.AreEqual (linkTimestamp, File.GetLastWriteTimeUtc (semaphore));
					}
				}
			}
		}
	}

	static XElement LoadTarget (string file, string name)
	{
		var target = new XElement (XDocument.Load (file).Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == name));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		return target;
	}
}
