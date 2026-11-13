using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AndroidTest1;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode.Client;
using NUnit.Framework;

namespace Microsoft.Android.Run.Tests;

[TestFixture]
[NonParallelizable]
public class AndroidTestAdapterTests
{
	string responsePath = "";
	string AdbTarget => $"-s {Convert.ToBase64String (Encoding.UTF8.GetBytes (responsePath))}";

	static string FakeAdbPath => Path.Combine (AppContext.BaseDirectory,
		"Microsoft.Android.Run.Tests" + (OperatingSystem.IsWindows () ? ".exe" : ""));

	[SetUp]
	public void SetUp ()
	{
		responsePath = Path.Combine (TestContext.CurrentContext.WorkDirectory, $"fake adb {Guid.NewGuid ():N}.json");
		Assert.That (File.Exists (FakeAdbPath), Is.True, $"Fake adb apphost not found: {FakeAdbPath}");
	}

	[TearDown]
	public void TearDown ()
	{
		File.Delete (responsePath);
		File.Delete (responsePath + ".args");
		File.Delete (responsePath + ".calls");
		File.Delete (responsePath + ".uids");
	}

	void Respond (string output, string error = "", int exitCode = 0, string? filterDirectory = "/storage/emulated/10/Android/data/test.package/files", int pushExitCode = 0)
		=> File.WriteAllText (responsePath, JsonSerializer.Serialize (new FakeAdb.Response (output, error, exitCode, filterDirectory, pushExitCode)));

	string[] ReadAdbArguments ()
		=> JsonSerializer.Deserialize<string[]> (File.ReadAllText (responsePath + ".args"))
			?? throw new InvalidOperationException ("Missing adb arguments.");

	string[][] ReadAdbCalls ()
		=> File.ReadAllLines (responsePath + ".calls")
			.Select (line => JsonSerializer.Deserialize<string[]> (line) ?? throw new InvalidOperationException ("Missing adb call."))
			.ToArray ();

	async Task<(int ExitCode, TestNode[] Nodes)> RunAsync (params string[] args)
	{
		var builder = await TestApplication.CreateBuilderAsync ([
			"--internal-testingplatform-skipbuildercheck", "--no-banner", .. args,
		]);
		var adapter = new AndroidTestAdapter (FakeAdbPath, AdbTarget, "test.package", "test.Runner", false);
		var consumer = new NodeConsumer ();
		builder.RegisterTestFramework (_ => new AndroidTestCapabilities (), (_, _) => adapter);
		builder.TestHost.AddDataConsumer (_ => consumer);
		builder.AddTrxReportProvider ();
		using var app = await builder.BuildAsync ();
		var exitCode = await app.RunAsync ();
		return (exitCode, consumer.Nodes.ToArray ());
	}

	async Task<MtpTestNodeUpdate[]> DiscoverAsync ()
	{
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (30));
		using var client = await MtpServerClient.LaunchInProcessAsync (async (args, cancellationToken) => {
			var builder = await TestApplication.CreateBuilderAsync ([
				"--internal-testingplatform-skipbuildercheck", "--no-banner", .. args,
			]);
			builder.RegisterTestFramework (_ => new AndroidTestCapabilities (), (_, _) =>
				new AndroidTestAdapter (FakeAdbPath, AdbTarget, "test.package", "test.Runner", false));
			using var app = await builder.BuildAsync ();
			return await app.RunAsync ();
		}, cancellationToken: timeout.Token);
		var nodes = new List<MtpTestNodeUpdate> ();
		client.TestNodesUpdated += (_, update) => nodes.AddRange (update.Changes);
		try {
			await client.InitializeAsync (timeout.Token);
			await client.DiscoverTestsAsync (timeout.Token);
			await client.ExitAsync (timeout.Token);
		} finally {
			await client.ShutdownAsync ();
		}
		Assert.That (client.ServerExitCode, Is.Zero);
		return nodes.ToArray ();
	}

	static string Status (string eventKind, string uid = "Tests.Fixture.Test", string outcome = "")
		=> $"""
			INSTRUMENTATION_STATUS: event={eventKind}
			INSTRUMENTATION_STATUS: test={uid}
			INSTRUMENTATION_STATUS: name=Test
			INSTRUMENTATION_STATUS: class=Tests.Fixture
			INSTRUMENTATION_STATUS: assembly=Tests, Version=1.0.0.0
			INSTRUMENTATION_STATUS: namespace=Tests
			INSTRUMENTATION_STATUS: type=Fixture
			INSTRUMENTATION_STATUS: method=Test
			INSTRUMENTATION_STATUS: method-arity=0
			INSTRUMENTATION_STATUS: parameter-types={InstrumentationProtocol.EncodeTestUids ([])}
			INSTRUMENTATION_STATUS: return-type=System.Void
			INSTRUMENTATION_STATUS: outcome={outcome}
			INSTRUMENTATION_STATUS_CODE: 0

			""";

	[Test]
	public async Task DiscoveryPublishesMetadataWithoutExecutingTests ()
	{
		Respond (Status ("discover") + """
			INSTRUMENTATION_RESULT: discovered=1
			INSTRUMENTATION_CODE: -1
			""", error: "* daemon started successfully\n");

		var nodes = await DiscoverAsync ();
		Assert.That (nodes, Has.Length.EqualTo (1));
		Assert.That (nodes [0].Uid, Is.EqualTo ("Tests.Fixture.Test"));
		Assert.That (nodes [0].ExecutionState, Is.EqualTo ("discovered"));
		Assert.That (nodes [0].Node ["location.type"], Is.EqualTo ("Tests.Fixture"));
		Assert.That (nodes [0].Node ["location.method"], Is.EqualTo ("Test"));
		Assert.That (ReadAdbArguments (), Does.Contain (InstrumentationProtocol.ExtraDiscover));
	}

	[Test]
	public async Task EmptyDiscoverySucceeds ()
	{
		Respond ("INSTRUMENTATION_RESULT: discovered=0\nINSTRUMENTATION_CODE: -1\n", error: "* daemon started successfully\n");
		var nodes = await DiscoverAsync ();
		Assert.That (nodes, Is.Empty);
	}

	[TestCase ("INSTRUMENTATION_RESULT: discovered=1\nINSTRUMENTATION_CODE: -1\n")]
	[TestCase ("INSTRUMENTATION_RESULT: passed=1\nINSTRUMENTATION_CODE: -1\n")]
	[TestCase ("INSTRUMENTATION_RESULT: error=Discovery failed\nINSTRUMENTATION_CODE: 0\n")]
	[TestCase ("INSTRUMENTATION_RESULT: discovered=0\nINSTRUMENTATION_RESULT: error=Discovery failed\nINSTRUMENTATION_CODE: -1\n")]
	[TestCase ("INSTRUMENTATION_RESULT: discovered=0\nINSTRUMENTATION_CODE: 0\n")]
	[TestCase ("INSTRUMENTATION_RESULT: discovered=0\n")]
	[TestCase ("INSTRUMENTATION_FAILED: Process crashed\n")]
	public void InvalidDiscoveryFailsInsteadOfReportingAnEmptySuite (string output)
	{
		Respond (output);
		Assert.ThrowsAsync<InvalidOperationException> (() => RunAsync ("--list-tests"));
	}

	[TestCase ("INSTRUMENTATION_RESULT: passed=1\nINSTRUMENTATION_CODE: -1\n", "does not support test discovery")]
	[TestCase ("INSTRUMENTATION_RESULT: error=Discovery failed\nINSTRUMENTATION_CODE: 0\n", "Android test discovery failed")]
	public void ServerDiscoveryFailureIsReportedToTheClient (string output, string message)
	{
		Respond (output);
		var error = Assert.CatchAsync (() => DiscoverAsync ());
		Assert.That (error?.ToString (), Does.Contain (message));
	}

	[Test]
	public async Task SelectedTestIdsReachInstrumentation ()
	{
		const string uid = "Tests.Fixture.Test(\"a b; 'quoted' & <xml>\")";
		Respond (Status ("finish", uid, "passed") + """
			INSTRUMENTATION_RESULT: passed=1
			INSTRUMENTATION_RESULT: resultsPath=/device/results.trx
			INSTRUMENTATION_CODE: -1
			""");

		var (exitCode, nodes) = await RunAsync ("--filter-uid", uid);
		Assert.That (exitCode, Is.Zero);
		Assert.That (nodes.Select (node => node.Uid.Value), Is.EqualTo (new [] { uid }));
		var args = ReadAdbArguments ();
		int filterIndex = Array.IndexOf (args, InstrumentationProtocol.ExtraTestUids);
		Assert.That (filterIndex, Is.GreaterThanOrEqualTo (0));
		Assert.That (InstrumentationProtocol.DecodeTestUids (args [filterIndex + 1]), Is.EqualTo (new [] { uid }));
		Assert.That (args, Does.Not.Contain (InstrumentationProtocol.ExtraDiscover));
	}

	[Test]
	public async Task LargeSelectionsUseOneInstrumentationRunAndCleanUp ()
	{
		var uids = Enumerable.Range (0, 2_000).Select (i => $"Tests.Fixture.TestWithLongName_{i:D5}").ToArray ();
		Assert.That (InstrumentationProtocol.EncodeTestUids (uids).Length, Is.GreaterThan (32_767));
		Respond (Status ("finish", uids [0], "passed") + """
			INSTRUMENTATION_RESULT: passed=1
			INSTRUMENTATION_RESULT: resultsPath=/device/results.trx
			INSTRUMENTATION_CODE: -1
			""", error: "* daemon started successfully\n");

		var (exitCode, nodes) = await RunAsync (["--filter-uid", .. uids]);
		Assert.That (exitCode, Is.Zero);
		Assert.That (nodes.Select (node => node.Uid.Value), Is.EqualTo (new [] { uids [0] }));
		Assert.That (InstrumentationProtocol.DecodeTestUids (File.ReadAllText (responsePath + ".uids")), Is.EqualTo (uids));

		var calls = ReadAdbCalls ();
		Assert.That (calls, Has.Length.EqualTo (4));
		Assert.That (calls [0], Does.Contain (InstrumentationProtocol.TestUidsDirectoryRequest));
		Assert.That (calls [1] [2], Is.EqualTo ("push"));
		var arguments = ReadAdbArguments ();
		int filterIndex = Array.IndexOf (arguments, InstrumentationProtocol.ExtraTestUids);
		Assert.That (filterIndex, Is.GreaterThanOrEqualTo (0));
		var fileArgument = arguments [filterIndex + 1];
		Assert.That (fileArgument, Does.StartWith (InstrumentationProtocol.TestUidsFilePrefix));
		var token = fileArgument.Substring (InstrumentationProtocol.TestUidsFilePrefix.Length);
		var devicePath = "/storage/emulated/10/Android/data/test.package/files/" + InstrumentationProtocol.GetTestUidsFileName (token);
		Assert.That (calls [1] [4], Is.EqualTo (devicePath));
		Assert.That (calls [2], Is.EqualTo (arguments));
		Assert.That (calls [3] [2..], Is.EqualTo (new [] { "shell", "rm", "-f", $"'{devicePath}'" }));
		Assert.That (calls.All (call => call.Sum (argument => argument.Length + 3) < 32_767), Is.True);
		Assert.That (File.Exists (calls [1] [3]), Is.False, "The local filter file must be removed.");
	}

	[Test]
	public void FailedFilterPushDoesNotRunTestsAndCleansUp ()
	{
		var uids = Enumerable.Range (0, 2_000).Select (i => $"Tests.Fixture.TestWithLongName_{i:D5}").ToArray ();
		Respond ("", pushExitCode: 1);
		var error = Assert.ThrowsAsync<InvalidOperationException> (() => RunAsync (["--filter-uid", .. uids]));
		Assert.That (error?.Message, Does.Contain ("fake adb push failed"));
		var calls = ReadAdbCalls ();
		Assert.That (calls, Has.Length.EqualTo (3));
		Assert.That (calls [1] [2], Is.EqualTo ("push"));
		Assert.That (calls [2] [2..4], Is.EqualTo (new [] { "shell", "rm" }));
		Assert.That (File.Exists (calls [1] [3]), Is.False);
		Assert.That (File.Exists (responsePath + ".args"), Is.False);
	}

	[Test]
	public void OlderInstrumentationRejectsLargeSelectionsWithoutRunningTests ()
	{
		var uids = Enumerable.Range (0, 2_000).Select (i => $"Tests.Fixture.TestWithLongName_{i:D5}").ToArray ();
		Respond ("", filterDirectory: null);
		var error = Assert.ThrowsAsync<InvalidOperationException> (() => RunAsync (["--filter-uid", .. uids]));
		Assert.That (error?.Message, Does.Contain ("File filters are unsupported"));
		Assert.That (ReadAdbCalls (), Has.Length.EqualTo (1));
		Assert.That (File.Exists (responsePath + ".args"), Is.False);
	}

	[Test]
	public async Task UnfilteredExecutionKeepsStreamingResults ()
	{
		Respond (Status ("start") + Status ("finish", outcome: "failed") + """
			INSTRUMENTATION_RESULT: failed=1
			INSTRUMENTATION_RESULT: resultsPath=/device/results.trx
			INSTRUMENTATION_CODE: -1
			""");
		var (exitCode, nodes) = await RunAsync ();
		Assert.That (exitCode, Is.Not.Zero);
		Assert.That (nodes, Has.Length.EqualTo (2));
		Assert.That (nodes [1].Properties.SingleOrDefault<FailedTestNodeStateProperty> (), Is.Not.Null);
		Assert.That (ReadAdbArguments (), Does.Not.Contain (InstrumentationProtocol.ExtraTestUids));
	}

	[Test]
	public void TestIdsRoundTripWithoutLosingWhitespaceOrParameterizedNames ()
	{
		string[] ids = ["Tests.Test(\"with spaces, commas; 'quotes' & <xml>\")", "a\r\nb", "", "unicode-\u00e9"];
		Assert.That (InstrumentationProtocol.DecodeTestUids (InstrumentationProtocol.EncodeTestUids (ids)), Is.EqualTo (ids));
	}

	[Test]
	public void FileFilterArgumentsAreValidatedAndDecoded ()
	{
		var token = Guid.NewGuid ().ToString ("N");
		var file = Path.Combine (TestContext.CurrentContext.WorkDirectory, InstrumentationProtocol.GetTestUidsFileName (token));
		string[] uids = ["Test(\"a b; 'quoted' & <xml>\")", "unicode-\u00e9"];
		try {
			File.WriteAllText (file, InstrumentationProtocol.EncodeTestUids (uids));
			var encoded = InstrumentationProtocol.ReadTestUidsArgument (InstrumentationProtocol.TestUidsFilePrefix + token, TestContext.CurrentContext.WorkDirectory);
			Assert.That (InstrumentationProtocol.DecodeTestUids (encoded), Is.EqualTo (uids));
			Assert.Throws<FormatException> (() => InstrumentationProtocol.ReadTestUidsArgument ("file:../other", TestContext.CurrentContext.WorkDirectory));
			Assert.Throws<InvalidOperationException> (() => InstrumentationProtocol.ReadTestUidsArgument ("file:" + token, null));
			File.Delete (file);
			Assert.Throws<FileNotFoundException> (() => InstrumentationProtocol.ReadTestUidsArgument ("file:" + token, TestContext.CurrentContext.WorkDirectory));
		} finally {
			File.Delete (file);
		}
	}

	[Test]
	public void ManagedMethodNamesDistinguishGenericTypesAndMethodArities ()
	{
		var methods = typeof (ManagedNameSamples).GetMethods (System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
		var list = methods.Single (method => ManagedTestName.Matches (method, "Overload(System.Collections.Generic.List`1<System.String>)", 0));
		Assert.That (list.GetParameters () [0].ParameterType, Is.EqualTo (typeof (List<string>)));
		var nested = methods.Single (method => ManagedTestName.Matches (method,
			"Overload(Microsoft.Android.Run.Tests.ManagedNameOuter`1+Inner`1<System.String,System.Int32>[,])", 0));
		Assert.That (nested.GetParameters () [0].ParameterType, Is.EqualTo (typeof (ManagedNameOuter<string>.Inner<int> [,])));
		var generic = methods.Single (method => ManagedTestName.Matches (method, "Generic(System.Collections.Generic.List`1<!!0>)", 2));
		Assert.That (generic.GetGenericArguments (), Has.Length.EqualTo (2));
		Assert.That (methods.Count (method => ManagedTestName.Matches (method, "Generic(System.Collections.Generic.List`1<!!0>)", 1)), Is.EqualTo (1));
		Assert.That (ManagedTestName.GetTypeName (typeof (ManagedNameOuter<>).GetGenericArguments () [0]), Is.EqualTo ("!0"));
	}

	[Test]
	public void ManagedTypeNamesFollowMSTestIdentifierEscaping ()
	{
		var assembly = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly (
			new System.Reflection.AssemblyName ("ManagedNameSamples"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
		var type = assembly.DefineDynamicModule ("Samples").DefineType ("Example Namespace.Generic<Placeholder>").CreateType ()
			?? throw new InvalidOperationException ("Could not create the sample type.");
		Assert.That (ManagedTestName.GetTypeName (type), Is.EqualTo ("'Example Namespace'.Generic<Placeholder>"));
	}

	[TestCase ("--server")]
	[TestCase ("--server jsonrpc")]
	[TestCase ("--server=jsonrpc")]
	public async Task JsonRpcServerCanDiscoverRepeatedlyAndRunSelectedTests (string serverArguments)
	{
		const string selectedUid = "Tests.Fixture.Test(\"a b; 'quoted' & <xml>\")";
		Respond (Status ("discover") + Status ("discover", selectedUid) + """
			INSTRUMENTATION_RESULT: discovered=2
			INSTRUMENTATION_CODE: -1
			""");

		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (30));
		using var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		var port = ((IPEndPoint) listener.LocalEndpoint).Port;
		var startInfo = new ProcessStartInfo (Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet") {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (var argument in new [] {
			typeof (AndroidTestAdapter).Assembly.Location,
			"--adb", FakeAdbPath, "--adb-target", AdbTarget,
			"--package", "test.package", "--instrument", "test.Runner",
		}.Concat (serverArguments.Split (' ')).Concat (["--client-port", port.ToString (System.Globalization.CultureInfo.InvariantCulture)]))
			startInfo.ArgumentList.Add (argument);

		using var process = Process.Start (startInfo) ?? throw new InvalidOperationException ("Could not start the Android test server.");
		var stdout = process.StandardOutput.ReadToEndAsync ();
		var stderr = process.StandardError.ReadToEndAsync ();
		try {
			using var socket = await listener.AcceptTcpClientAsync (timeout.Token);
			using var connection = MtpServerConnector.CreateConnection (socket,
				MtpServerConnector.CreateFormatter (), NullMtpClientLogger.Instance);
			using var client = new MtpServerClient (connection);
			var nodes = new List<MtpTestNodeUpdate> ();
			client.TestNodesUpdated += (_, update) => nodes.AddRange (update.Changes);
			await client.InitializeAsync (timeout.Token);

			for (int attempt = 0; attempt < 2; attempt++) {
				nodes.Clear ();
				await client.DiscoverTestsAsync (timeout.Token);
				Assert.That (nodes.Select (node => node.Uid), Is.EquivalentTo (new [] { "Tests.Fixture.Test", selectedUid }));
				Assert.That (nodes.All (node => node.ExecutionState == "discovered"), Is.True);
			}

			Respond (Status ("finish", selectedUid, "passed") + """
				INSTRUMENTATION_RESULT: passed=1
				INSTRUMENTATION_RESULT: resultsPath=/device/results.trx
				INSTRUMENTATION_CODE: -1
				""");
			nodes.Clear ();
			await client.RunTestsAsync ([selectedUid], timeout.Token);
			Assert.That (nodes, Has.Count.EqualTo (1));
			Assert.That (nodes [0].Uid, Is.EqualTo (selectedUid));
			Assert.That (nodes [0].ExecutionState, Is.EqualTo ("passed"));
			var args = ReadAdbArguments ();
			int filterIndex = Array.IndexOf (args, InstrumentationProtocol.ExtraTestUids);
			Assert.That (filterIndex, Is.GreaterThanOrEqualTo (0));
			Assert.That (InstrumentationProtocol.DecodeTestUids (args [filterIndex + 1]), Is.EqualTo (new [] { selectedUid }));

			var largeSelection = Enumerable.Range (0, 2_000).Select (i => $"Tests.Fixture.TestWithLongName_{i:D5}").ToArray ();
			Respond (Status ("finish", largeSelection [0], "passed") + """
				INSTRUMENTATION_RESULT: passed=1
				INSTRUMENTATION_RESULT: resultsPath=/device/results.trx
				INSTRUMENTATION_CODE: -1
				""");
			nodes.Clear ();
			await client.RunTestsAsync (largeSelection, timeout.Token);
			Assert.That (nodes.Select (node => node.Uid), Is.EqualTo (new [] { largeSelection [0] }));
			Assert.That (InstrumentationProtocol.DecodeTestUids (File.ReadAllText (responsePath + ".uids")), Is.EqualTo (largeSelection));
			var largeCalls = ReadAdbCalls ().TakeLast (4).ToArray ();
			Assert.That (largeCalls [0], Does.Contain (InstrumentationProtocol.TestUidsDirectoryRequest));
			Assert.That (largeCalls [1] [2], Is.EqualTo ("push"));
			Assert.That (largeCalls [2], Does.Contain (InstrumentationProtocol.ExtraTestUids));
			Assert.That (largeCalls [3] [2..4], Is.EqualTo (new [] { "shell", "rm" }));
			Assert.That (largeCalls.All (call => call.Sum (argument => argument.Length + 3) < 32_767), Is.True);

			Respond (Status ("discover", largeSelection [0]) + """
				INSTRUMENTATION_RESULT: discovered=1
				INSTRUMENTATION_CODE: -1
				""");
			nodes.Clear ();
			await client.DiscoverTestsAsync (largeSelection, timeout.Token);
			Assert.That (nodes.Select (node => node.Uid), Is.EqualTo (new [] { largeSelection [0] }));
			Assert.That (InstrumentationProtocol.DecodeTestUids (File.ReadAllText (responsePath + ".uids")), Is.EqualTo (largeSelection));
			largeCalls = ReadAdbCalls ().TakeLast (4).ToArray ();
			Assert.That (largeCalls [0], Does.Contain (InstrumentationProtocol.TestUidsDirectoryRequest));
			Assert.That (largeCalls [2], Does.Contain (InstrumentationProtocol.ExtraDiscover));
			Assert.That (largeCalls [3] [2..4], Is.EqualTo (new [] { "shell", "rm" }));

			File.Delete (responsePath + ".args");
			nodes.Clear ();
			await client.RunTestsAsync (testNodeUids: [], cancellationToken: timeout.Token);
			Assert.That (nodes, Is.Empty);
			Assert.That (File.Exists (responsePath + ".args"), Is.False, "An empty selection must not run the whole test suite.");

			await client.ExitAsync (timeout.Token);
			await process.WaitForExitAsync (timeout.Token);
			Assert.That (process.ExitCode, Is.Zero, await stderr);
		} finally {
			if (!process.HasExited)
				process.Kill (entireProcessTree: true);
			await process.WaitForExitAsync ();
			TestContext.WriteLine (await stdout);
			TestContext.WriteLine (await stderr);
		}
	}

	sealed class NodeConsumer : IDataConsumer
	{
		public List<TestNode> Nodes { get; } = [];
		public string Uid => "android-runner-test-consumer";
		public string DisplayName => Uid;
		public string Description => Uid;
		public string Version => "1.0";
		public Type[] DataTypesConsumed => [typeof (TestNodeUpdateMessage)];
		public Task<bool> IsEnabledAsync () => Task.FromResult (true);

		public Task ConsumeAsync (IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
		{
			if (value is TestNodeUpdateMessage update)
				Nodes.Add (update.TestNode);
			return Task.CompletedTask;
		}

	}

	sealed class ManagedNameSamples
	{
		public void Overload (List<string> value) { }
		public void Overload (List<int> value) { }
		public void Overload (ManagedNameOuter<string>.Inner<int> [,] value) { }
		public void Generic<T> (List<T> value) { }
		public void Generic<T, U> (List<T> value) { }
	}
}

public class ManagedNameOuter<T>
{
	public class Inner<U> { }
}
