// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class CommandLineToolsResolverTests
{
	string sdkDirectory = "";

	string SdkDirectory => sdkDirectory;
	string ExecutableExtension => OS.IsWindows ? ".bat" : "";

	[SetUp]
	public void SetUp ()
	{
		sdkDirectory = Path.Combine (Path.GetTempPath (), $"cmdline-tools-test-{Path.GetRandomFileName ()}");
		Directory.CreateDirectory (sdkDirectory);
	}

	[TearDown]
	public void TearDown ()
	{
		if (Directory.Exists (sdkDirectory))
			Directory.Delete (sdkDirectory, recursive: true);
		sdkDirectory = "";
	}

	[Test]
	public void CommandLineTool_NullPath_Throws ()
	{
		Assert.Throws<ArgumentNullException> (() => new CommandLineTool (null));
	}

	[Test]
	public void FindCommandLineTool_LatestHasHigherPackageRevision_SelectsLatest ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		CreateCommandLineTool ("19.0", "avdmanager", "19.0");
		var expectedSdkManager = CreateCommandLineTool ("latest", "sdkmanager", "22.0");
		var expectedAvdManager = CreateCommandLineTool ("latest", "avdmanager", "22.0");

		using var manager = CreateSdkManager ();
		var selected = manager.FindSdkManager ();

		Assert.That (selected?.Path, Is.EqualTo (expectedSdkManager));
		Assert.That (selected?.Revision, Is.EqualTo ("22.0"));
		Assert.That (manager.FindSdkManagerPath (), Is.EqualTo (expectedSdkManager));
		Assert.That (
			ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension),
			Is.EqualTo (expectedAvdManager));
	}

	[Test]
	public void FindCommandLineTool_NumericHasHigherPackageRevision_SelectsNumeric ()
	{
		var expectedSdkManager = CreateCommandLineTool ("22.0", "sdkmanager", "22.0");
		var expectedAvdManager = CreateCommandLineTool ("22.0", "avdmanager", "22.0");
		CreateCommandLineTool ("latest", "sdkmanager", "19.0");
		CreateCommandLineTool ("latest", "avdmanager", "19.0");

		using var manager = CreateSdkManager ();
		var selected = manager.FindSdkManager ();

		Assert.That (selected?.Path, Is.EqualTo (expectedSdkManager));
		Assert.That (selected?.Revision, Is.EqualTo ("22.0"));
		Assert.That (
			ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension),
			Is.EqualTo (expectedAvdManager));
	}

	[Test]
	public void FindCommandLineTool_DirectoryNamesDisagreeWithPackageRevision_UsesPackageRevision ()
	{
		CreateCommandLineTool ("99.0", "sdkmanager", "19.0");
		CreateCommandLineTool ("99.0", "avdmanager", "19.0");
		var expectedSdkManager = CreateCommandLineTool ("1.0", "sdkmanager", "22.0");
		var expectedAvdManager = CreateCommandLineTool ("1.0", "avdmanager", "22.0");

		using var manager = CreateSdkManager ();

		Assert.That (manager.FindSdkManagerPath (), Is.EqualTo (expectedSdkManager));
		Assert.That (
			ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension),
			Is.EqualTo (expectedAvdManager));
	}

	[Test]
	public void FindCommandLineTool_MissingAndMalformedPackageRevision_UsesDirectoryVersion ()
	{
		var expectedSdkManager = CreateCommandLineTool ("22.0", "sdkmanager");
		var expectedAvdManager = CreateCommandLineTool ("22.0", "avdmanager");
		CreateCommandLineTool ("latest", "sdkmanager", "not-a-version");
		CreateCommandLineTool ("latest", "avdmanager", "not-a-version");

		using var manager = CreateSdkManager ();
		var selected = manager.FindSdkManager ();

		Assert.That (selected?.Path, Is.EqualTo (expectedSdkManager));
		Assert.That (selected?.Revision, Is.EqualTo ("22.0"));
		Assert.That (
			ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension),
			Is.EqualTo (expectedAvdManager));
	}

	[Test]
	public void FindCommandLineTool_StableAndPrereleaseHaveSameCoreRevision_PrefersStable ()
	{
		var expectedSdkManager = CreateCommandLineTool ("stable", "sdkmanager", "22.0.0");
		var expectedAvdManager = CreateCommandLineTool ("stable", "avdmanager", "22.0.0");
		CreateCommandLineTool ("preview", "sdkmanager", "22.0.0 rc1");
		CreateCommandLineTool ("preview", "avdmanager", "22.0.0 rc1");

		using var manager = CreateSdkManager ();

		Assert.That (manager.FindSdkManagerPath (), Is.EqualTo (expectedSdkManager));
		Assert.That (
			ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension),
			Is.EqualTo (expectedAvdManager));
	}

	[Test]
	public void FindCommandLineTool_PrereleaseRevisionsHaveNumericSuffix_SelectsHighest ()
	{
		CreateCommandLineTool ("rc2", "sdkmanager", "22.0.0 rc2");
		var expectedSdkManager = CreateCommandLineTool ("rc10", "sdkmanager", "22.0.0 rc10");

		using var manager = CreateSdkManager ();

		Assert.That (manager.FindSdkManagerPath (), Is.EqualTo (expectedSdkManager));
	}

	[Test]
	public void FindCommandLineTool_LegacyToolsDirectory_UsedOnlyForSdkManager ()
	{
		var legacyDirectory = Path.Combine (SdkDirectory, "tools", "bin");
		Directory.CreateDirectory (legacyDirectory);
		var expectedSdkManager = Path.Combine (legacyDirectory, "sdkmanager" + ExecutableExtension);
		File.WriteAllText (expectedSdkManager, "");
		File.WriteAllText (Path.Combine (legacyDirectory, "avdmanager" + ExecutableExtension), "");

		using var manager = CreateSdkManager ();

		Assert.That (manager.FindSdkManagerPath (), Is.EqualTo (expectedSdkManager));
		Assert.That (
			ProcessUtils.FindCmdlineTool (SdkDirectory, "avdmanager", ExecutableExtension),
			Is.Null);
	}

	[Test]
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
				Assert.That (targetPath, Is.EqualTo (SdkDirectory));
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
				Assert.That (acceptLicenses, Is.True);
				installCalls++;
				CreateCommandLineTool ("latest", "sdkmanager", "22.0");
				return Task.CompletedTask;
			});

		Assert.That (selected.Revision, Is.EqualTo ("22.0"));
		Assert.That (selected.Path, Does.Contain (Path.Combine ("latest", "bin")));
		Assert.That (bootstrapCalls, Is.EqualTo (1));
		Assert.That (installCalls, Is.EqualTo (1));
		Assert.That (progress.Phases, Does.Contain (SdkBootstrapPhase.ReadingManifest));
		Assert.That (progress.Phases, Does.Contain (SdkBootstrapPhase.CheckingForUpdates));
		Assert.That (progress.Phases, Does.Contain (SdkBootstrapPhase.Installing));
		Assert.That (progress.Phases, Does.Contain (SdkBootstrapPhase.Complete));
		Assert.That (progress.Phases.Count (phase => phase == SdkBootstrapPhase.Complete), Is.EqualTo (1));
	}

	[Test]
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

		Assert.That (selected.Revision, Is.EqualTo ("22.0"));
		Assert.That (bootstrapCalls, Is.Zero);
		Assert.That (installCalls, Is.EqualTo (1));
	}

	[Test]
	public async Task EnsureLatestCommandLineToolsAsync_CurrentManager_DoesNotInstall ()
	{
		CreateCommandLineTool ("latest", "sdkmanager", "22.0.0");
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

		Assert.That (selected.Revision, Is.EqualTo ("22.0.0"));
		Assert.That (bootstrapCalls, Is.Zero);
		Assert.That (installCalls, Is.Zero);
		Assert.That (progress.Phases, Does.Not.Contain (SdkBootstrapPhase.Installing));
	}

	[Test]
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

		Assert.That (selected.Revision, Is.EqualTo ("22.0"));
		Assert.That (installCalls, Is.EqualTo (1));
	}

	[Test]
	public void EnsureLatestCommandLineToolsAsync_MissingCatalogPackage_Throws ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		using var manager = CreateSdkManager ();

		var exception = Assert.ThrowsAsync<InvalidOperationException> (() => manager.EnsureLatestCommandLineToolsAsync (
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

		Assert.That (exception?.Message, Does.Contain ("Could not determine the latest"));
	}

	[Test]
	public void EnsureLatestCommandLineToolsAsync_InstallDoesNotUpdateManager_Throws ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		var installCalls = 0;
		using var manager = CreateSdkManager ();

		var exception = Assert.ThrowsAsync<InvalidOperationException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, _) => {
				installCalls++;
				return Task.CompletedTask;
			}));

		Assert.That (exception?.Message, Does.Contain ("older than the catalog revision"));
		Assert.That (installCalls, Is.EqualTo (1));
	}

	[Test]
	public void EnsureLatestCommandLineToolsAsync_BootstrapDoesNotInstallManager_Throws ()
	{
		using var manager = CreateSdkManager ();

		var exception = Assert.ThrowsAsync<InvalidOperationException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, _) => Task.CompletedTask));

		Assert.That (exception?.Message, Does.Contain ("without installing sdkmanager"));
	}

	[Test]
	public void EnsureLatestCommandLineToolsAsync_Disposed_Throws ()
	{
		using var manager = CreateSdkManager ();
		manager.Dispose ();

		Assert.ThrowsAsync<ObjectDisposedException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			CancellationToken.None,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, _) => Task.CompletedTask));
	}

	[Test]
	public void EnsureLatestCommandLineToolsAsync_Canceled_PropagatesCancellation ()
	{
		using var manager = CreateSdkManager ();
		using var cancellation = new CancellationTokenSource ();
		cancellation.Cancel ();

		Assert.ThrowsAsync<OperationCanceledException> (() => manager.EnsureLatestCommandLineToolsAsync (
			SdkDirectory,
			new ProgressCollector (),
			cancellation.Token,
			(_, _, _) => Task.CompletedTask,
			_ => CreatePackageList ("22.0"),
			(_, _, _) => Task.CompletedTask));
	}

	[Test]
	public void EnsureLatestCommandLineToolsAsync_CanceledDuringInstall_PropagatesCancellation ()
	{
		CreateCommandLineTool ("19.0", "sdkmanager", "19.0");
		using var manager = CreateSdkManager ();
		using var cancellation = new CancellationTokenSource ();

		Assert.ThrowsAsync<OperationCanceledException> (() => manager.EnsureLatestCommandLineToolsAsync (
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
