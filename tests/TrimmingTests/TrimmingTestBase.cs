#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using BuildLog = Microsoft.Build.Logging.StructuredLogger.Build;
using NUnit.Framework;
using Xamarin.Android.Build.Tests;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Microsoft.Android.TrimmingTests;

public enum TrimmingConfiguration
{
	/// <summary>CoreCLR with ILLink and <c>TrimMode=full</c>: every assembly, including the app's, is trimmed.</summary>
	CoreClrFullTrimming,
	/// <summary>NativeAOT: ILC trims and compiles every assembly.</summary>
	NativeAot,
	/// <summary>
	/// CoreCLR with ILLink and <c>TrimMode=partial</c>, the CoreCLR Release default: only assemblies marked trimmable (such
	/// as Mono.Android and the BCL) are trimmed, the app's own assemblies are kept whole, and trimming warnings are suppressed.
	/// </summary>
	CoreClrPartialTrimming,
}

/// <summary>
/// The configurations a test runs in: all of them by default, or the ones listed.
/// </summary>
[AttributeUsage (AttributeTargets.Parameter)]
public sealed class TrimmingConfigurationsAttribute : ValuesAttribute
{
	public TrimmingConfigurationsAttribute ()
		: this (TrimmingConfiguration.CoreClrFullTrimming, TrimmingConfiguration.NativeAot, TrimmingConfiguration.CoreClrPartialTrimming)
	{
	}

	public TrimmingConfigurationsAttribute (params TrimmingConfiguration [] configurations)
		: base (configurations.Cast<object?> ().ToArray ())
	{
	}
}

/// <summary>A case app that <see cref="TrimmingTestBase.BuildAndRun"/> built and ran.</summary>
public sealed record TrimmedApp (string CaseFile, TrimmingConfiguration Configuration, BuildLog Build, string IntermediateDirectory);

/// <summary>
/// Builds a single-file case app from <c>Cases/</c>, installs and launches it, and checks its result and its build warnings.
/// Tests check the trimming expectations the case declares with <see cref="AssertTrimmingExpectations"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each case is a file-based app (<c>#:property</c> directives) purpose-built for one scenario, so nothing else can keep the
/// types and members under test alive. A case must declare <c>#:property ApplicationId=...</c>, reference the expectations
/// with <c>#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj</c>, and log exactly one
/// <c>TRIMMING_TEST_RESULT: PASS</c> or <c>TRIMMING_TEST_RESULT: FAIL ...</c> line on startup. Never add trimmer descriptors,
/// rooted assemblies, or <c>[DynamicDependency]</c> to a case unless that is the behavior under test. A case that needs more
/// than one file lives in its own directory, <c>Cases/&lt;Case&gt;/&lt;Case&gt;.cs</c>, next to its Android resources, its
/// <c>AndroidManifest.xml</c>, or a <c>Case.targets</c> with MSBuild items. Cases that reproduce a reported bug link to it.
/// </para>
/// <para>
/// Tests in a fixture may run in parallel (<c>[Parallelizable (ParallelScope.Children)]</c>), also on the device: each test builds in
/// its own directory, installs its own package (<c>ApplicationId</c> plus the configuration), and reads only its app's logcat output.
/// Launches run one at a time: an activity started in another task before the app's process attaches covers the app's activity,
/// and Android then never creates it. Fixtures run one after another, so the one-time device setup of <see cref="DeviceTest"/> never runs concurrently.
/// </para>
/// <para>
/// <see cref="BuildAndRun"/> requires a build without warnings, unless the test lists the expected warning codes, and a launch
/// that reports PASS. Tests then call <see cref="AssertTrimmingExpectations"/> to check <c>[Kept]</c>, <c>[Removed]</c> and the
/// other expectation attributes against ILLink's output (CoreCLR) or the methods and types ILC compiled (NativeAOT mstat).
/// </para>
/// </remarks>
public abstract class TrimmingTestBase : DeviceTest
{
	const string ResultTag = "TRIMMING_TEST_RESULT:";
	const string ExpectationsProject = "TrimmingTests.Expectations";
	static readonly object LaunchLock = new ();

	// Tests share the device, so check it once per fixture, where restarting it can't disrupt a running test.
	[OneTimeSetUp]
	public void AssertDeviceAttached ()
	{
		if (!IsDeviceAttached (refreshCachedValue: true)) {
			RestartDevice ();
			DeviceSetup ();
		}
		AssertHasDevices ();
		AssertPackageManagerResponsive ();
	}

	// DeviceTest checks (and may restart) the device and clears logcat before every test, which would discard the output of
	// apps that tests running in parallel are waiting for.
	public override void SetupTest ()
	{
	}

	/// <summary>Builds the case without unexpected warnings, then installs and launches it and requires it to report PASS.</summary>
	/// <param name="casePath">The case file relative to the test project, e.g. <c>Cases/GenericPeerArrayElement.cs</c>.</param>
	protected TrimmedApp BuildAndRun (string casePath, TrimmingConfiguration configuration, string []? expectedWarnings = null)
	{
		var caseFile = Path.GetFileName (casePath);
		var directory = Path.Combine (Root, "temp", TestName);
		// BaseTest deletes it after a passing test and attaches its logs and binlogs to a failing one.
		TestOutputDirectories [TestContext.CurrentContext.Test.ID] = directory;
		var source = CopyCase (casePath, directory);
		// Each configuration gets its own package, so concurrent tests can install and run their apps side by side.
		var packageName = $"{GetApplicationId (source)}.{configuration.ToString ().ToLowerInvariant ()}";
		var runtimeIdentifier = MonoAndroidHelper.AbiToRid (DeviceAbi);
		var artifacts = Path.Combine (directory, "artifacts");

		// App-only properties become directives: global properties would also apply to the referenced expectations project.
		SetProperties (source, new Dictionary<string, string> {
			["TargetFramework"] = $"{XABuildConfig.LatestDotNetTargetFramework}-android",
			["ApplicationId"] = packageName,
			// File-based apps default to PublishAot=true, so always set it explicitly.
			["PublishAot"] = configuration == TrimmingConfiguration.NativeAot ? "true" : "false",
			[configuration == TrimmingConfiguration.NativeAot ? "IlcGenerateMstatFile" : "TrimMode"] = configuration switch {
				TrimmingConfiguration.NativeAot => "true",
				TrimmingConfiguration.CoreClrPartialTrimming => "partial",
				_ => "full",
			},
			// Report each trimming/AOT warning instead of one IL2104/IL3053 per assembly.
			["TrimmerSingleWarn"] = "false",
			// An APK can be installed with `adb install` outside the build, which keeps the build off the device.
			["AndroidPackageFormat"] = "apk",
		});

		var dotnet = new DotNetCLI (source) { ProjectDirectory = directory };
		bool built = dotnet.Build ("SignAndroidPackage", runtimeIdentifier, new [] { "Configuration=Release" },
			new [] { "--artifacts-path", $"\"{artifacts}\"" });
		var binlog = Path.Combine (directory, "SignAndroidPackage.binlog");
		var build = BinaryLog.ReadBuild (binlog);
		Assert.IsTrue (built, $"{caseFile} should build. See {dotnet.ProcessLogFile}{Environment.NewLine}" +
			string.Join (Environment.NewLine, build.FindChildrenRecursive<Error> ().Select (Format).Distinct ()));
		AssertWarnings (build, expectedWarnings ?? [], binlog);

		var apk = Directory.GetFiles (artifacts, $"{packageName}-Signed.apk", SearchOption.AllDirectories).SingleOrDefault ();
		Assert.IsNotNull (apk, $"{caseFile} should produce {packageName}-Signed.apk under {artifacts}.");
		var install = RunAdbCommand ($"install -r \"{apk}\"", timeout: 120);
		StringAssert.Contains ("Success", install, $"{caseFile} should install.");
		try {
			lock (LaunchLock) {
				AssertLaunchReportsPass (caseFile, configuration, packageName, directory);
			}
		} finally {
			RunAdbCommand ($"uninstall {packageName}");
		}

		return new TrimmedApp (caseFile, configuration, build, Path.Combine (artifacts, "obj", caseFile, $"release_{runtimeIdentifier}"));
	}

	static string CopyCase (string casePath, string directory)
	{
		var source = Path.Combine (TestContext.CurrentContext.TestDirectory, casePath);
		FileAssert.Exists (source, $"{casePath} should be copied next to the test assembly.");
		var cases = Path.Combine (TestContext.CurrentContext.TestDirectory, "Cases");
		var caseDirectory = Path.GetDirectoryName (source) ?? throw new InvalidOperationException ($"{casePath} has no directory.");
		if (Directory.Exists (directory)) {
			Directory.Delete (directory, recursive: true);
		}
		Directory.CreateDirectory (directory);
		foreach (var file in Directory.GetFiles (cases, "Directory.Build.*")) {
			File.Copy (file, Path.Combine (directory, Path.GetFileName (file)));
		}
		// On CI the test directory is outside the repository, so bring the repository's package feeds along.
		File.Copy (Path.Combine (XABuildPaths.TopDirectory, "NuGet.config"), Path.Combine (directory, "NuGet.config"));
		CopyDirectory (Path.Combine (cases, ExpectationsProject), Path.Combine (directory, ExpectationsProject));
		// A case in its own directory (Cases/<Case>/<Case>.cs) brings the files next to it, such as Android resources,
		// AndroidManifest.xml, or a Case.targets with MSBuild items.
		if (Path.GetFullPath (caseDirectory) == Path.GetFullPath (cases)) {
			File.Copy (source, Path.Combine (directory, Path.GetFileName (source)));
		} else {
			CopyDirectory (caseDirectory, directory);
		}
		return Path.Combine (directory, Path.GetFileName (source));
	}

	static void CopyDirectory (string source, string destination)
	{
		foreach (var file in Directory.GetFiles (source, "*", SearchOption.AllDirectories)) {
			var target = Path.Combine (destination, Path.GetRelativePath (source, file));
			Directory.CreateDirectory (Path.GetDirectoryName (target) ?? destination);
			File.Copy (file, target);
		}
	}

	static void SetProperties (string source, IDictionary<string, string> properties)
	{
		var lines = File.ReadAllLines (source)
			.Where (line => !properties.Keys.Any (key => Regex.IsMatch (line, $@"^#:property\s+{Regex.Escape (key)}\s*=")));
		File.WriteAllLines (source, properties.Select (p => $"#:property {p.Key}={p.Value}").Concat (lines));
	}

	static void AssertWarnings (BuildLog build, string [] expectedWarnings, string binlog)
	{
		var warnings = build.FindChildrenRecursive<Warning> ().Select (w => (w.Code, Text: Format (w))).Distinct ().ToList ();
		var unexpected = warnings.Where (w => !expectedWarnings.Contains (w.Code, StringComparer.Ordinal)).Select (w => w.Text).ToList ();
		var missing = expectedWarnings.Where (code => !warnings.Any (w => w.Code == code)).ToList ();
		Assert.IsEmpty (unexpected, $"Unexpected build warnings (pass their codes as expectedWarnings when intended). See {binlog}");
		Assert.IsEmpty (missing, $"Expected build warnings were not reported. See {binlog}");
	}

	// Streams logcat from before the launch, so none of the app's output is missed. Matches only this app's process ID and
	// never clears logcat, so tests can share the device.
	static void AssertLaunchReportsPass (string caseFile, TrimmingConfiguration configuration, string packageName, string directory)
	{
		var activity = RunAdbCommand ($"shell cmd package resolve-activity --brief {packageName}")
			.Split ('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.LastOrDefault ();
		Assert.IsNotNull (activity, $"{packageName} should have a launcher activity.");

		var logcat = Path.Combine (directory, "logcat.log");
		string? pid = null;
		string? result = null;
		MonitorAdbLogcat (line => {
			// threadtime format: "<date> <time> <pid> <tid> <level> <tag>: <message>"
			if (pid is null || !line.Contains (ResultTag, StringComparison.Ordinal)) {
				return false;
			}
			var fields = line.Split (' ', 4, StringSplitOptions.RemoveEmptyEntries);
			if (fields.Length < 3 || fields [2] != pid) {
				return false;
			}
			result = line;
			return true;
		}, logcat, timeout: ActivityStartTimeoutInSeconds, onMonitoringStarted: () => {
			var started = RunAdbCommand ($"shell am start -W -n {activity}", timeout: ActivityStartTimeoutInSeconds);
			StringAssert.Contains ("Complete", started, $"{caseFile} should start on {configuration}.");
			// `am start -W` can return before the app's process exists, so give it a moment to appear.
			var deadline = DateTime.UtcNow.AddSeconds (10);
			while ((pid = RunAdbCommand ($"shell pidof {packageName}").Split (' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault ()) is null &&
					DateTime.UtcNow < deadline) {
				Thread.Sleep (200);
			}
			if (string.IsNullOrEmpty (pid)) {
				Assert.Fail ($"{caseFile} is not running after launch on {configuration}; it probably crashed. See {logcat}");
			}
		});
		Assert.IsNotNull (result, $"{caseFile} did not report a result from process {pid}. See {logcat}");
		StringAssert.Contains ($"{ResultTag} PASS", result, $"{caseFile} failed on {configuration}. See {logcat}");
	}

	/// <summary>Checks the expectation attributes the case declares against the trimmed output of <paramref name="app"/>.</summary>
	protected static void AssertTrimmingExpectations (TrimmedApp app)
	{
		var (caseFile, configuration, build, intermediate) = app;
		var name = Path.GetFileNameWithoutExtension (caseFile);
		// The untrimmed assemblies come from runtime packs (such as Mono.Android) and NuGet packages.
		var inputAssemblies = build.FindChildrenRecursive<Item> (i => i.Parent is NamedNode { Name: "RuntimePackAsset" or "ReferenceCopyLocalPaths" })
			.Select (i => i.Text)
			.Where (p => p.EndsWith (".dll", StringComparison.OrdinalIgnoreCase))
			.Distinct ()
			.ToList ();
		string? ResolveInput (string assemblyName)
			=> inputAssemblies.FirstOrDefault (p => string.Equals (Path.GetFileName (p), $"{assemblyName}.dll", StringComparison.OrdinalIgnoreCase));

		ITrimmedOutput output = configuration == TrimmingConfiguration.NativeAot
			? new IlcCompiledOutput (Path.Combine (intermediate, "native", $"{name}.mstat"))
			: new LinkedAssemblies (Path.Combine (intermediate, "linked"));
		var failures = TrimmingExpectations.Validate (Path.Combine (intermediate, $"{name}.dll"), configuration, output, ResolveInput,
			GetTrimmedAssemblies (build, configuration));
		Assert.IsEmpty (failures, $"{caseFile} does not meet its trimming expectations on {configuration}.");
	}

	// Partial mode trims an assembly only when it's marked trimmable, as ILLink decides: IsTrimmable or TrimMode metadata on
	// its ManagedAssemblyToLink item, or [assembly: AssemblyMetadata ("IsTrimmable", "True")].
	static Func<AssemblyMetadata, bool> GetTrimmedAssemblies (BuildLog build, TrimmingConfiguration configuration)
	{
		if (configuration != TrimmingConfiguration.CoreClrPartialTrimming) {
			return _ => true;
		}
		var markedTrimmable = build.FindChildrenRecursive<Item> (i => i.Parent is NamedNode { Name: "ManagedAssemblyToLink" or "AssemblyPaths" })
			.Where (i => i.Children.OfType<Metadata> ().Any (m =>
				(m.Name == "IsTrimmable" && string.Equals (m.Value, "true", StringComparison.OrdinalIgnoreCase)) ||
				(m.Name == "TrimMode" && m.Value is "link" or "full")))
			.Select (i => Path.GetFileNameWithoutExtension (i.Text))
			.ToHashSet (StringComparer.OrdinalIgnoreCase);
		return assembly => assembly.DeclaresIsTrimmable || markedTrimmable.Contains (assembly.Name);
	}

	static string Format (AbstractDiagnostic diagnostic)
		=> $"{diagnostic.Code}: {diagnostic.Text} ({diagnostic.File}({diagnostic.LineNumber}))";

	static string GetApplicationId (string source)
	{
		foreach (var line in File.ReadLines (source)) {
			var match = Regex.Match (line, @"^#:property\s+ApplicationId\s*=\s*(\S+)\s*$");
			if (match.Success) {
				return match.Groups [1].Value;
			}
		}
		throw new InvalidOperationException ($"{source} must declare '#:property ApplicationId=...'.");
	}
}
