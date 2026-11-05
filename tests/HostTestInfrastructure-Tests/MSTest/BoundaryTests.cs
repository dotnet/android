using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.ProjectTools;

[assembly: DoNotParallelize]

namespace Xamarin.Android.Build.Tests;

[TestClass]
public abstract class BoundaryTestBase
{
	int cleanupCalls;

	public TestContext? TestContext { get; set; }

	protected TestContext Context => TestContext ?? throw new InvalidOperationException ("The runner must supply TestContext.");

	protected bool RetainArtifacts { get; set; }
	protected string RowName { get; set; } = "";
	protected string DirectoryPath => Path.Combine (AppContext.BaseDirectory, "temp", Context.TestName + RowName);

	[TestCleanup]
	public void Cleanup ()
	{
		Assert.AreEqual (1, ++cleanupCalls, "Inherited cleanup must run exactly once per test instance.");
		if (!RetainArtifacts) {
			FileSystemUtils.DeleteDirectoryWithRetry (DirectoryPath);
			Assert.IsFalse (Directory.Exists (DirectoryPath), "Inherited cleanup must remove non-retained row output.");
		}
		Context.WriteLine ($"[BOUNDARY CLEANUP] {Context.TestName}; row={RowName}; calls={cleanupCalls}; retained={RetainArtifacts}");
	}
}

[TestClass]
public class BoundaryTests : BoundaryTestBase
{
	DownloadedCache CreateCache (string directory) => new DownloadedCache (directory,
		message => Context.WriteLine (message), message => Assert.Inconclusive (message));

	[TestMethod]
	public void ProjectToolsIsLibrary () => BoundaryChecks.ProjectToolsIsLibrary ();

	[TestMethod]
	public void SharedSourcesDoNotReferenceNUnit ()
	{
		Assert.IsFalse (GetType ().Assembly.GetReferencedAssemblies ().Any (reference => reference.Name == "nunit.framework"));
	}

	[TestMethod]
	public void CachedDownload () => BoundaryChecks.CachedDownload (DirectoryPath, CreateCache);

	[TestMethod]
	public void ProcessEnvironment () => BoundaryChecks.ProcessEnvironment ((info, key, value) =>
		info.SetEnvironmentVariable (key, value, message => Assert.Inconclusive (message)));

	[TestMethod]
	public void BuildEngineOutput () => BoundaryChecks.BuildEngineOutput (message => Context.WriteLine (message));

	[TestMethod]
	public void Bootstrap () => BoundaryChecks.Bootstrap (DirectoryPath);

	[TestMethod]
	public void InvalidBootstrapUsesFrameworkAssertion ()
	{
		BoundaryChecks.WriteBootstrap (DirectoryPath);
		File.WriteAllBytes (Path.Combine (DirectoryPath, "coreclr-bootstrap.bin"), new byte [84]);
		Assert.ThrowsExactly<AssertFailedException> (() => EnvironmentHelper.ReadCoreClrBootstrap (DirectoryPath));
	}

	[TestMethod]
	[DataRow (0, 0u)]
	[DataRow (4, 3u)]
	[DataRow (8, 0u)]
	[DataRow (80, 0u)]
	[DataRow (80, 112u)]
	[DataRow (80, 111u)]
	[DataRow (84, 0u)]
	[DataRow (32, 1u)]
	public void InvalidBootstrapFields (int offset, uint value)
	{
		RowName = $"{offset}-{value}";
		BoundaryChecks.CorruptBootstrap (DirectoryPath, offset, value);
		Assert.ThrowsExactly<AssertFailedException> (() => EnvironmentHelper.ReadCoreClrBootstrap (DirectoryPath));
	}

	[TestMethod]
	public void MissingNativeAotBootstrap ()
	{
		Directory.CreateDirectory (DirectoryPath);
		Assert.AreEqual (0, EnvironmentHelper.ReadNativeAotEnvironmentVariables (DirectoryPath, required: false).Count);
		Assert.ThrowsExactly<AssertFailedException> (() => EnvironmentHelper.ReadNativeAotEnvironmentVariables (DirectoryPath));
	}

	[TestMethod]
	public void TestNames () => BoundaryChecks.TestNames ();

	[TestMethod]
	[DataRow (true)]
	[DataRow (false)]
	public void CleanupArtifacts (bool delete)
	{
		RowName = delete.ToString ();
		RetainArtifacts = !delete;
		BoundaryChecks.Cleanup (DirectoryPath, delete, (file, description) => Context.AddResultFile (file));
	}

	[TestMethod]
	public async Task ExplicitDownloadSkip ()
	{
		await BoundaryChecks.TransientDownload (DirectoryPath, CreateCache);
	}

	[TestMethod]
	public async Task ExplicitDownloadDiagnostics ()
	{
		var messages = new List<string> ();
		var exception = await Assert.ThrowsExactlyAsync<AssertInconclusiveException> (async () =>
			await BoundaryChecks.TransientDownload (DirectoryPath, path => new DownloadedCache (path,
				messages.Add, message => Assert.Inconclusive (message))));
		Assert.Contains ("Test skipped due to transient network error:", exception.Message);
		Assert.IsTrue (messages.Exists (message => message.Contains ("HTTP Status Code: 503 (ServiceUnavailable)")));
	}

	[TestMethod]
	public void ExplicitEnvironmentSkip ()
	{
		Assert.ThrowsExactly<AssertInconclusiveException> (() => new ProcessStartInfo ().SetEnvironmentVariable (
			null, "value", message => Assert.Inconclusive (message)));
	}

	[TestMethod]
	public async Task SuccessfulDownload ()
	{
		string file = await BoundaryChecks.DownloadResponse (DirectoryPath, CreateCache, 200, "downloaded");
		Assert.AreEqual ("downloaded", File.ReadAllText (file));
	}

	[TestMethod]
	public async Task NonTransientDownloadFails ()
	{
		await Assert.ThrowsExactlyAsync<HttpRequestException> (async () =>
			await BoundaryChecks.DownloadResponse (DirectoryPath, CreateCache, 404, ""));
	}

	[TestMethod]
	public void ReturningSkipCallbackFailsExplicitly ()
	{
		Assert.ThrowsExactly<InvalidOperationException> (() => new ProcessStartInfo ().SetEnvironmentVariable (null, "value", _ => { }));
	}

	[TestMethod]
	public Task IsolatedDownloadCallbacks () => BoundaryChecks.IsolatedDownloadCallbacks (DirectoryPath);

	[TestMethod]
	[DataRow ("one", 1)]
	[DataRow ("two", 2)]
	public async Task RowContextSurvivesAwait (string row, int number)
	{
		var context = Context;
		string name = TestNameUtilities.GetDirectoryName ($"{context.TestName}({row},{number})");
		await Task.Yield ();
		Assert.AreSame (context, Context);
		Assert.AreEqual (name, TestNameUtilities.GetDirectoryName ($"{Context.TestName}({row},{number})"));
	}

	[TestMethod]
	public void NativeToolDiagnostics ()
	{
		var startInfo = new ProcessStartInfo (OperatingSystem.IsWindows () ? "powershell.exe" : "/bin/sh") {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		startInfo.ArgumentList.Add (OperatingSystem.IsWindows () ? "-Command" : "-c");
		startInfo.ArgumentList.Add (OperatingSystem.IsWindows () ?
			"[Console]::Out.Write('output'); [Console]::Error.Write('error'); exit 7" :
			"printf output; printf error >&2; exit 7");
		var result = NativeToolTestHelper.Capture (startInfo, TimeSpan.FromSeconds (10));
		Assert.AreEqual (7, result.ExitCode);
		Assert.AreEqual ("output", result.StandardOutput);
		Assert.AreEqual ("error", result.StandardError);
	}
}
