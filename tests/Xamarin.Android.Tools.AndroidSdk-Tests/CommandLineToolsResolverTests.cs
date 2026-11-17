// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests;

[TestClass]
public class CommandLineToolsResolverTests
{
	string sdkDirectory = "";

	string SdkDirectory => sdkDirectory;
	string ExecutableExtension => OS.IsWindows ? ".bat" : "";

	[TestInitialize]
	public void SetUp ()
	{
		sdkDirectory = Path.Combine (Path.GetTempPath (), $"cmdline-tools-test-{Path.GetRandomFileName ()}");
		Directory.CreateDirectory (sdkDirectory);
	}

	[TestCleanup]
	public void TearDown ()
	{
		if (Directory.Exists (sdkDirectory))
			Directory.Delete (sdkDirectory, recursive: true);
		sdkDirectory = "";
	}

	[TestMethod]
	public void CommandLineTool_NullPath_Throws ()
	{
		Assert.ThrowsExactly<ArgumentNullException> (() => new CommandLineTool (null));
	}

	[TestMethod]
	public void FindCommandLineTool_LatestHasHigherPackageRevision_SelectsLatest ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		CreateCommandLineTool ("19.0", "avdmanager", "19.0");
		var expectedSdkManager = CreateCommandLineTool ("latest", "sdkmanager", "22.0");
		var expectedAvdManager = CreateCommandLineTool ("latest", "avdmanager", "22.0");

		using var manager = CreateSdkManager ();
		var selected = manager.FindSdkManager ();

		Assert.AreEqual (expectedSdkManager, selected?.Path);
		Assert.AreEqual ("22.0", selected?.Revision);
		Assert.AreEqual (expectedSdkManager, manager.FindSdkManagerPath ());
		Assert.AreEqual (expectedAvdManager, ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension));
	}

	[TestMethod]
	public void FindCommandLineTool_NumericHasHigherPackageRevision_SelectsLatest ()
	{
		CreateCommandLineTool ("22.0", "sdkmanager", "22.0");
		CreateCommandLineTool ("22.0", "avdmanager", "22.0");
		var expectedSdkManager = CreateCommandLineTool ("latest", "sdkmanager", "19.0");
		var expectedAvdManager = CreateCommandLineTool ("latest", "avdmanager", "19.0");

		using var manager = CreateSdkManager ();
		var selected = manager.FindSdkManager ();

		Assert.AreEqual (expectedSdkManager, selected?.Path);
		Assert.AreEqual ("19.0", selected?.Revision);
		Assert.AreEqual (expectedAvdManager, ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension));
	}

	[TestMethod]
	public void FindCommandLineTool_DirectoryNamesDisagreeWithPackageRevision_UsesPackageRevision ()
	{
		CreateCommandLineTool ("99.0", "sdkmanager", "19.0");
		CreateCommandLineTool ("99.0", "avdmanager", "19.0");
		var expectedSdkManager = CreateCommandLineTool ("1.0", "sdkmanager", "22.0");
		var expectedAvdManager = CreateCommandLineTool ("1.0", "avdmanager", "22.0");

		using var manager = CreateSdkManager ();

		Assert.AreEqual (expectedSdkManager, manager.FindSdkManagerPath ());
		Assert.AreEqual (expectedAvdManager, ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension));
	}

	[TestMethod]
	public void FindCommandLineTool_LatestHasMalformedPackageRevision_SelectsLatest ()
	{
		CreateCommandLineTool ("22.0", "sdkmanager");
		CreateCommandLineTool ("22.0", "avdmanager");
		var expectedSdkManager = CreateCommandLineTool ("latest", "sdkmanager", "not-a-version");
		var expectedAvdManager = CreateCommandLineTool ("latest", "avdmanager", "not-a-version");

		using var manager = CreateSdkManager ();
		var selected = manager.FindSdkManager ();

		Assert.AreEqual (expectedSdkManager, selected?.Path);
		Assert.IsNull (selected?.Revision);
		Assert.AreEqual (expectedAvdManager, ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension));
	}

	[TestMethod]
	public void FindCommandLineTool_LatestMissingTool_FallsBackToHighestVersioned ()
	{
		var expectedSdkManager = CreateCommandLineTool ("22.0", "sdkmanager", "22.0");
		var expectedAvdManager = CreateCommandLineTool ("22.0", "avdmanager", "22.0");
		CreateCommandLineTool ("latest", "unrelated", "23.0");

		using var manager = CreateSdkManager ();

		Assert.AreEqual (expectedSdkManager, manager.FindSdkManagerPath ());
		Assert.AreEqual (expectedAvdManager, ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension));
	}

	[TestMethod]
	public void FindCommandLineTool_LegacyToolsBin_IsNotSupported ()
	{
		var binDirectory = Path.Combine (SdkDirectory, "tools", "bin");
		Directory.CreateDirectory (binDirectory);
		File.WriteAllText (Path.Combine (binDirectory, "sdkmanager" + ExecutableExtension), "");
		File.WriteAllText (Path.Combine (binDirectory, "avdmanager" + ExecutableExtension), "");

		using var manager = CreateSdkManager ();

		Assert.IsNull (manager.FindSdkManagerPath ());
		Assert.IsNull (ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension));
	}

	[TestMethod]
	public void FindCommandLineTool_StableAndPrereleaseHaveSameCoreRevision_PrefersStable ()
	{
		var expectedSdkManager = CreateCommandLineTool ("stable", "sdkmanager", "22.0");
		var expectedAvdManager = CreateCommandLineTool ("stable", "avdmanager", "22.0");
		CreateCommandLineTool ("preview", "sdkmanager", "22.0 rc1");
		CreateCommandLineTool ("preview", "avdmanager", "22.0 rc1");

		using var manager = CreateSdkManager ();

		Assert.AreEqual (expectedSdkManager, manager.FindSdkManagerPath ());
		Assert.AreEqual (expectedAvdManager, ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension));
	}

	[TestMethod]
	public void FindCommandLineTool_PrereleaseRevisionsHaveNumericSuffix_SelectsHighest ()
	{
		CreateCommandLineTool ("rc2", "sdkmanager", "22.0 rc2");
		var expectedSdkManager = CreateCommandLineTool ("rc10", "sdkmanager", "22.0 rc10");

		using var manager = CreateSdkManager ();

		Assert.AreEqual (expectedSdkManager, manager.FindSdkManagerPath ());
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_MissingManager_BootstrapsAndInstallsLatest ()
	{
		var bootstrapCalls = 0;
		var installCalls = 0;
		var progress = new ProgressCollector ();
		using var manager = CreateSdkManager ();

		var selected = await manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			progress,
			CancellationToken.None,
			(targetPath, bootstrapProgress, cancellationToken) => {
				cancellationToken.ThrowIfCancellationRequested ();
				Assert.AreEqual (SdkDirectory, targetPath);
				bootstrapCalls++;
				bootstrapProgress?.Report (new SdkBootstrapProgress (SdkBootstrapPhase.ReadingManifest));
				bootstrapProgress?.Report (new SdkBootstrapProgress (SdkBootstrapPhase.Complete));
				CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
				return Task.CompletedTask;
			},
			_ => CreatePackageList ("22.0"),
			(packages, acceptLicenses, cancellationToken) => {
				cancellationToken.ThrowIfCancellationRequested ();
				string[] expectedPackages = [LatestPackage];
				CollectionAssert.AreEqual (expectedPackages, packages.ToArray ());
				Assert.IsTrue (acceptLicenses);
				installCalls++;
				CreateCommandLineTool ("latest", "sdkmanager", "22.0");
				return Task.CompletedTask;
			});

		Assert.AreEqual ("22.0", selected.Revision);
		Assert.Contains (Path.Combine ("latest", "bin"), selected.Path);
		Assert.AreEqual (1, bootstrapCalls);
		Assert.AreEqual (1, installCalls);
		Assert.Contains (SdkBootstrapPhase.ReadingManifest, progress.Phases);
		Assert.Contains (SdkBootstrapPhase.CheckingForUpdates, progress.Phases);
		Assert.Contains (SdkBootstrapPhase.Installing, progress.Phases);
		Assert.Contains (SdkBootstrapPhase.Complete, progress.Phases);
		Assert.AreEqual (1, progress.Phases.Count (phase => phase == SdkBootstrapPhase.Complete));
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_StaleManager_InstallsLatestWithoutBootstrap ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		var bootstrapCalls = 0;
		var installCalls = 0;
		using var manager = CreateSdkManager ();

		var selected = await manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => {
				bootstrapCalls++;
				return Task.CompletedTask;
			},
			_ => CreatePackageList ("22.0"),
			(_, _, _) => {
				installCalls++;
				CreateCommandLineTool ("latest", "sdkmanager", "22.0");
				return Task.CompletedTask;
			});

		Assert.AreEqual ("22.0", selected.Revision);
		Assert.AreEqual (0, bootstrapCalls);
		Assert.AreEqual (1, installCalls);
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_CurrentManager_DoesNotInstall ()
	{
		CreateCommandLineTool ("latest", "sdkmanager", "22.0");
		var bootstrapCalls = 0;
		var installCalls = 0;
		var progress = new ProgressCollector ();
		using var manager = CreateSdkManager ();

		var selected = await manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			progress,
			CancellationToken.None,
			(_, _, _) => {
				bootstrapCalls++;
				return Task.CompletedTask;
			},
			_ => CreatePackageList ("22.0", isInstalled: true),
			(_, _, _) => {
				installCalls++;
				return Task.CompletedTask;
			});

		Assert.AreEqual ("22.0", selected.Revision);
		Assert.AreEqual (0, bootstrapCalls);
		Assert.AreEqual (0, installCalls);
		Assert.DoesNotContain (SdkBootstrapPhase.Installing, progress.Phases);
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_InstalledPackageHasAvailableUpdate_InstallsLatest ()
	{
		CreateCommandLineTool ("latest", "sdkmanager", "19.0");
		var installCalls = 0;
		using var manager = CreateSdkManager ();

		var selected = await manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => {
				IReadOnlyList<SdkPackage> installed = [new SdkPackage (LatestPackage, "19.0", IsInstalled: true)];
				IReadOnlyList<SdkPackage> available = [new SdkPackage (LatestPackage, "22.0")];
				return Task.FromResult ((installed, available));
			},
			(_, _, _) => {
				installCalls++;
				CreateCommandLineTool ("latest", "sdkmanager", "22.0");
				return Task.CompletedTask;
			});

		Assert.AreEqual ("22.0", selected.Revision);
		Assert.AreEqual (1, installCalls);
	}

	[TestMethod]
	[DataRow (@"Available Packages:
  Path                   | Version | Description
  cmdline-tools;latest   | 22.0    | Android SDK Command-line Tools (latest)
")]
	[DataRow (@"Available packages:
  cmdline-tools/latest    22.0    Android SDK Command-line Tools (latest)
")]
	public async Task EnsureLatestCommandLineToolsAsync_ParsedPackageFormats_RecognizesLatest (string output)
	{
		CreateCommandLineTool ("latest", "sdkmanager", "22.0");
		var installCalls = 0;
		using var manager = CreateSdkManager ();

		var selected = await manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => Task.FromResult (SdkManager.ParseSdkManagerList (output)),
			(_, _, _) => {
				installCalls++;
				return Task.CompletedTask;
			});

		Assert.AreEqual ("22.0", selected.Revision);
		Assert.AreEqual (0, installCalls);
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_MissingCatalogPackage_Throws ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		using var manager = CreateSdkManager ();

		var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => {
				IReadOnlyList<SdkPackage> installed = [];
				IReadOnlyList<SdkPackage> available = [];
				return Task.FromResult ((installed, available));
			},
			(_, _, _) => Task.CompletedTask));

		Assert.Contains ("Could not determine the latest", exception?.Message);
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_InstallDoesNotUpdateManager_Throws ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		var installCalls = 0;
		using var manager = CreateSdkManager ();

		var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, _) => {
				installCalls++;
				return Task.CompletedTask;
			}));

		Assert.Contains ("older than the catalog revision", exception?.Message);
		Assert.AreEqual (1, installCalls);
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_BootstrapDoesNotInstallManager_Throws ()
	{
		using var manager = CreateSdkManager ();

		var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, _) => Task.CompletedTask));

		Assert.Contains ("without installing sdkmanager", exception?.Message);
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_Disposed_Throws ()
	{
		using var manager = CreateSdkManager ();
		manager.Dispose ();

		await Assert.ThrowsExactlyAsync<ObjectDisposedException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, _) => Task.CompletedTask));
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_Canceled_PropagatesCancellation ()
	{
		using var manager = CreateSdkManager ();
		using var cancellation = new CancellationTokenSource ();
		cancellation.Cancel ();

		await Assert.ThrowsExactlyAsync<OperationCanceledException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			cancellation.Token,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, _) => Task.CompletedTask));
	}

	[TestMethod]
	public async Task EnsureLatestCommandLineToolsAsync_CanceledDuringInstall_PropagatesCancellation ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		using var manager = CreateSdkManager ();
		using var cancellation = new CancellationTokenSource ();

		await Assert.ThrowsExactlyAsync<OperationCanceledException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			cancellation.Token,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, cancellationToken) => {
				cancellation.Cancel ();
				cancellationToken.ThrowIfCancellationRequested ();
				return Task.CompletedTask;
			}));
	}

	const string LatestPackage = "cmdline-tools;latest";

	SdkManager CreateSdkManager ()
	{
		return new SdkManager {
			AndroidSdkPath = SdkDirectory,
		};
	}

	string CreateCommandLineTool (string directoryName, string toolName, string revision = null)
	{
		var commandLineToolsDirectory = Path.Combine (SdkDirectory, "cmdline-tools", directoryName);
		var binDirectory = Path.Combine (commandLineToolsDirectory, "bin");
		Directory.CreateDirectory (binDirectory);

		var toolPath = Path.Combine (binDirectory, toolName + ExecutableExtension);
		File.WriteAllText (toolPath, "");
		if (revision is not null)
			File.WriteAllText (Path.Combine (commandLineToolsDirectory, "source.properties"), $"Pkg.Revision = {revision}");
		return toolPath;
	}

	static Task<(IReadOnlyList<SdkPackage> Installed, IReadOnlyList<SdkPackage> Available)> CreatePackageList (
		string latestVersion,
		bool isInstalled = false)
	{
		var package = new SdkPackage (LatestPackage, latestVersion, IsInstalled: isInstalled);
		IReadOnlyList<SdkPackage> installed = isInstalled ? [package] : [];
		IReadOnlyList<SdkPackage> available = isInstalled ? [] : [package];
		return Task.FromResult ((installed, available));
	}

	sealed class ProgressCollector : IProgress<SdkBootstrapProgress>
	{
		public List<SdkBootstrapPhase> Phases { get; } = [];

		public void Report (SdkBootstrapProgress value)
		{
			Phases.Add (value.Phase);
		}
	}
}
