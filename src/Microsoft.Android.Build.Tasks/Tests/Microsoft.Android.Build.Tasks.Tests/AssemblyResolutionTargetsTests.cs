using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class AssemblyResolutionTargetsTests : BaseTest
{
	string directory => Path.Combine (Root, "temp", TestName);

	[SetUp]
	public void SetUp () => Directory.CreateDirectory (directory);

	[Test]
	public void HostCoverageCollectorArtifactsAreExcludedFromAndroidPublish (
		[Values ("MonoVM", "CoreCLR", "NativeAOT")] string runtime,
		[Values (false, true)] bool publishTrimmed,
		[Values ("android-arm64", "android-x64")] string rid,
		[Values (false, true)] bool hasCollector)
	{
		var targets = Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.AssemblyResolution.targets");
		var collectorTarget = hasCollector ? """
			  <Target Name="CopyTraceDataCollectorArtifacts" AfterTargets="ComputeFilesToPublish">
			    <ItemGroup>
			      <TraceDataCollectorArtifacts Include="collector/CodeCoverage/amd64/msdia140.dll;collector/CodeCoverage/arm64/msdia140.dll;collector/CodeCoverage/msdia140.dll;collector/CodeCoverage/coreclr/Microsoft.VisualStudio.CodeCoverage.Shim.dll;collector/System.Text.Json.dll;collector/fr/Microsoft.CodeCoverage.Core.resources.dll" />
			      <ResolvedFileToPublish Include="@(TraceDataCollectorArtifacts)">
			        <RelativePath>Microsoft.CodeCoverage/%(Identity)</RelativePath>
			        <CopyToPublishDirectory>Always</CopyToPublishDirectory>
			        <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
			      </ResolvedFileToPublish>
			    </ItemGroup>
			  </Target>
			""" : "";
		var project = Write ("collector.proj", $"""
			<Project>
			  <PropertyGroup>
			    <_XamarinAndroidBuildTasksAssembly>unused.dll</_XamarinAndroidBuildTasksAssembly>
			    <_ComputeFilesToPublishForRuntimeIdentifiers>true</_ComputeFilesToPublishForRuntimeIdentifiers>
			    <_AndroidRuntime>{runtime}</_AndroidRuntime>
			    <PublishTrimmed>{publishTrimmed}</PublishTrimmed>
			    <RuntimeIdentifier>{rid}</RuntimeIdentifier>
			  </PropertyGroup>
			  <Import Project="{SecurityElement.Escape (targets)}" />
			  <Target Name="BuildOnlySettings" />
			  <Target Name="_CheckForInvalidConfigurationAndPlatform" />
			  <Target Name="ResolveReferences" />
			  <Target Name="_AndroidAot" />
			  <Target Name="ComputeFilesToPublish">
			    <ItemGroup>
			      <ResolvedFileToPublish Include="app.dll;Microsoft.Testing.Platform.dll;fr/app.resources.dll;runtimes/$(RuntimeIdentifier)/native/libandroid.so;Microsoft.VisualStudio.CodeCoverage.Shim.dll;System.Text.Json.dll">
			        <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
			      </ResolvedFileToPublish>
			      <ResolvedFileToPublish Update="@(ResolvedFileToPublish)" RelativePath="%(ResolvedFileToPublish.Identity)" />
			    </ItemGroup>
			  </Target>
			  {collectorTarget}
			  <Target Name="Build" DependsOnTargets="_ComputeFilesToPublishForRuntimeIdentifiers">
			    <WriteLinesToFile File="$(MSBuildProjectDirectory)/collector-count.txt"
			                      Lines="@(TraceDataCollectorArtifacts->Count())" Overwrite="true" />
			    <WriteLinesToFile File="$(MSBuildProjectDirectory)/published.txt"
			                      Lines="@(ResolvedFileToPublish->'%(Identity)|%(RelativePath)|%(RuntimeIdentifier)|%(CopyToPublishDirectory)')"
			                      Overwrite="true" />
			  </Target>
			</Project>
			""");
		Build (project);
		Assert.AreEqual (hasCollector ? "6" : "0", File.ReadAllText (Path.Combine (directory, "collector-count.txt")).Trim (),
			"The collector target should have populated its host artifacts before Android filters them.");

		string [] applicationFiles = [
			"app.dll",
			"Microsoft.Testing.Platform.dll",
			"fr/app.resources.dll",
			$"runtimes/{rid}/native/libandroid.so",
			"Microsoft.VisualStudio.CodeCoverage.Shim.dll",
			"System.Text.Json.dll",
		];
		CollectionAssert.AreEquivalent (
			applicationFiles.Select (file => $"{file}|{file}|{rid}|PreserveNewest"),
			File.ReadAllLines (Path.Combine (directory, "published.txt")),
			"Only application dependencies should be returned, with their publish metadata preserved.");
	}

	static string RepositoryDirectory ()
	{
		var repository = new DirectoryInfo (AppContext.BaseDirectory);
		while (repository != null && !File.Exists (Path.Combine (repository.FullName, "Configuration.props"))) {
			repository = repository.Parent;
		}
		return repository?.FullName ?? throw new InvalidOperationException ("Could not locate the repository.");
	}

	void Build (string project)
	{
		var start = new ProcessStartInfo ("dotnet") {
			WorkingDirectory = directory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		start.Environment ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
		foreach (var argument in new [] { "msbuild", project, "-t:Build", "-nologo", "-v:minimal", "-nr:false" }) {
			start.ArgumentList.Add (argument);
		}
		using var process = Process.Start (start) ?? throw new InvalidOperationException ("Could not start MSBuild.");
		var stdout = process.StandardOutput.ReadToEndAsync ();
		var stderr = process.StandardError.ReadToEndAsync ();
		bool completed = process.WaitForExit (120000);
		if (!completed) {
			process.Kill (entireProcessTree: true);
			process.WaitForExit ();
		}
		var output = stdout.GetAwaiter ().GetResult () + stderr.GetAwaiter ().GetResult ();
		Assert.IsTrue (completed, "MSBuild did not finish." + Environment.NewLine + output);
		Assert.AreEqual (0, process.ExitCode, output);
	}

	string Write (string name, string content)
	{
		var path = Path.Combine (directory, name);
		File.WriteAllText (path, content, new UTF8Encoding (false));
		return path;
	}
}
