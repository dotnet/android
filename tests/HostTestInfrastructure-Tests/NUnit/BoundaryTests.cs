using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class BoundaryTests
{
	bool retainArtifacts;
	string Root => Path.Combine (TestContext.CurrentContext.WorkDirectory, "HostTestInfrastructure.NUnit.Tests");
	string TestName => TestNameUtilities.GetDirectoryName (TestContext.CurrentContext.Test.Name);
	string DirectoryPath => Path.Combine (Root, "temp", TestName);

	[SetUp]
	public void Setup () => retainArtifacts = false;

	[TearDown]
	public void Cleanup ()
	{
		if (!retainArtifacts) {
			FileSystemUtils.DeleteDirectoryWithRetry (DirectoryPath);
		}
	}

	[Test]
	public void ProjectToolsIsLibrary () => BoundaryChecks.ProjectToolsIsLibrary ();

	[Test]
	public void CachedDownload () => BoundaryChecks.CachedDownload (DirectoryPath, path => new DownloadedCache (path));

	[Test]
	public void ProcessEnvironment () => BoundaryChecks.ProcessEnvironment ((info, key, value) => info.SetEnvironmentVariable (key, value));

	[Test]
	public void BuildEngineOutput () => BoundaryChecks.BuildEngineOutput (message => TestContext.WriteLine (message));

	[Test]
	public void Bootstrap () => BoundaryChecks.Bootstrap (DirectoryPath);

	[Test]
	public void InvalidBootstrapUsesFrameworkAssertion ()
	{
		BoundaryChecks.WriteBootstrap (DirectoryPath);
		File.WriteAllBytes (Path.Combine (DirectoryPath, "coreclr-bootstrap.bin"), new byte [84]);
		Assert.Throws<AssertionException> (() => EnvironmentHelper.ReadCoreClrBootstrap (DirectoryPath));
	}

	[TestCase (0, 0u)]
	[TestCase (4, 3u)]
	[TestCase (8, 0u)]
	[TestCase (80, 0u)]
	[TestCase (80, 112u)]
	[TestCase (80, 111u)]
	[TestCase (84, 0u)]
	[TestCase (32, 1u)]
	public void InvalidBootstrapFields (int offset, uint value)
	{
		BoundaryChecks.CorruptBootstrap (DirectoryPath, offset, value);
		Assert.Throws<AssertionException> (() => EnvironmentHelper.ReadCoreClrBootstrap (DirectoryPath));
	}

	[Test]
	public void MissingNativeAotBootstrap ()
	{
		Directory.CreateDirectory (DirectoryPath);
		Assert.IsEmpty (EnvironmentHelper.ReadNativeAotEnvironmentVariables (DirectoryPath, required: false));
		Assert.Throws<AssertionException> (() => EnvironmentHelper.ReadNativeAotEnvironmentVariables (DirectoryPath));
	}

	[Test]
	public void TestNames () => BoundaryChecks.TestNames ();

	[TestCase (true)]
	[TestCase (false)]
	public void CleanupArtifacts (bool delete)
	{
		retainArtifacts = !delete;
		BoundaryChecks.Cleanup (DirectoryPath, delete, (file, description) => TestContext.AddTestAttachment (file, description));
	}

	[Test]
	public async Task DefaultDownloadSkip ()
	{
		await BoundaryChecks.TransientDownload (DirectoryPath, path => new DownloadedCache (path));
	}

	[Test]
	public void ExplicitDownloadDiagnostics ()
	{
		var messages = new List<string> ();
		var exception = Assert.ThrowsAsync<InconclusiveException> (async () =>
			await BoundaryChecks.TransientDownload (DirectoryPath, path => new DownloadedCache (path,
				messages.Add, message => Assert.Inconclusive (message))));
		StringAssert.Contains ("Test skipped due to transient network error:", exception.Message);
		Assert.That (messages, Has.Some.Contains ("HTTP Status Code: 503 (ServiceUnavailable)"));
	}

	[Test]
	public void DefaultEnvironmentSkip ()
	{
		Assert.Throws<InconclusiveException> (() => new ProcessStartInfo ().SetEnvironmentVariable (null, "value"));
	}

	[Test]
	public async Task SuccessfulDownload ()
	{
		string file = await BoundaryChecks.DownloadResponse (DirectoryPath, path => new DownloadedCache (path), 200, "downloaded");
		Assert.AreEqual ("downloaded", File.ReadAllText (file));
	}

	[Test]
	public void NonTransientDownloadFails ()
	{
		Assert.ThrowsAsync<HttpRequestException> (async () =>
			await BoundaryChecks.DownloadResponse (DirectoryPath, path => new DownloadedCache (path), 404, ""));
	}

	[Test]
	public void ReturningSkipCallbackFailsExplicitly ()
	{
		Assert.Throws<InvalidOperationException> (() => new ProcessStartInfo ().SetEnvironmentVariable (null, "value", _ => { }));
	}

	[Test]
	public Task IsolatedDownloadCallbacks () => BoundaryChecks.IsolatedDownloadCallbacks (DirectoryPath);

	[TestCase ("one", 1)]
	[TestCase ("two", 2)]
	public async Task RowContextSurvivesAwait (string row, int number)
	{
		var context = TestContext.CurrentContext;
		string id = context.Test.ID;
		string name = TestName;
		await Task.Yield ();
		Assert.AreEqual (id, TestContext.CurrentContext.Test.ID);
		Assert.AreEqual (name, TestName);
		Assert.AreEqual (new object [] { row, number }, TestContext.CurrentContext.Test.Arguments);
		Assert.AreEqual (TestNameUtilities.GetDirectoryName (context.Test.Name), name);
	}
}
