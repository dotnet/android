using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace AndroidMSTestProof;

[Instrumentation (Name = "net.dot.android.mstestproof.Instrumentation")]
public class TestInstrumentation : Instrumentation
{
	const string LogTag = "D.MSTest";
	Bundle? arguments;

	protected TestInstrumentation (IntPtr handle, JniHandleOwnership ownership) : base (handle, ownership) { }

	public override void OnCreate (Bundle? arguments)
	{
		this.arguments = arguments;
		AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error (LogTag, $"Unhandled exception: {e.ExceptionObject}");
		TaskScheduler.UnobservedTaskException += (_, e) => Log.Error (LogTag, $"Unobserved exception: {e.Exception}");
		base.OnCreate (arguments);
		Start ();
	}

	public override void OnStart ()
	{
		base.OnStart ();
		_ = RunAsync ();
	}

	async Task RunAsync ()
	{
		var bundle = new Bundle ();
		try {
			var directory = Application.Context.GetExternalFilesDir (null)?.AbsolutePath
				?? throw new InvalidOperationException ("Android external results directory is unavailable.");
			var results = Path.Combine (directory, "TestResults");
			if (Directory.Exists (results))
				Directory.Delete (results, recursive: true);
			Directory.CreateDirectory (results);

			Type [] fixtures = [
				typeof (ProofTests),
				typeof (JavaInterop.InteropTests),
				typeof (TestLibrary.LibraryTests),
			];
			Assembly [] assemblies = fixtures.Select (fixture => fixture.Assembly).Distinct ().ToArray ();
			foreach (var assembly in assemblies)
				Log.Info (LogTag, "[D REGISTER] " + assembly.GetName ().Name);

			var commandLine = new List<string> {
				"--results-directory", results, "--report-trx", "--progress", "off", "--output", "normal",
			};
			if (arguments?.ContainsKey ("list") == true)
				throw new NotSupportedException ("This representative runner records native IDs during execution; standalone listing is not part of its proven contract.");
			var include = arguments?.GetString ("include") ?? AppContext.GetData ("IncludeCategories") as string ?? "proof";
			var categories = include.Split ([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (categories.Length == 0)
				throw new InvalidOperationException ("An explicit nonempty proof category selection is required.");
			if (categories.Any (category => category.Any (character => !char.IsAsciiLetterOrDigit (character) && character != '-')))
				throw new ArgumentException ("This bounded proof accepts only simple alphanumeric category names.");
			commandLine.Add ("--filter");
			commandLine.Add ("(" + string.Join ("|", categories.Select (category => "TestCategory=" + category)) + ")");
			var consumer = new ResultConsumer (this, results, fixtures);
			var builder = await TestApplication.CreateBuilderAsync (commandLine.ToArray (), new TestApplicationOptions {
				EnableTelemetry = false,
			});
			builder.AddMSTest (() => assemblies);
			builder.AddTrxReportProvider ();
			builder.TestHost.AddDataConsumer (_ => consumer);
			using ITestApplication application = await builder.BuildAsync ();
			int exit = await application.RunAsync ();
			bundle.PutString ("nodesPath", consumer.SaveNodes ());
			foreach (var file in Directory.EnumerateFiles (results, "*", SearchOption.AllDirectories)) {
				using var exported = new Java.IO.File (file);
				if (!exported.SetReadable (true, ownerOnly: false))
					throw new IOException ($"Cannot export proof result file: {file}");
			}
			if (consumer.Passed + consumer.Failed + consumer.Skipped == 0)
				throw new InvalidOperationException ($"No results from native MSTest (exit {exit}).");
			if (exit != 0 && (exit != 2 || consumer.Failed == 0))
				throw new InvalidOperationException ($"Native MSTest infrastructure failed with exit {exit}.");
			if (consumer.TrxPath is not string trx || !File.Exists (trx))
				throw new InvalidOperationException ("Native MSTest did not produce its TRX.");
			bundle.PutString (InstrumentationProtocol.KeyResultsPath, trx);
			bundle.PutInt (InstrumentationProtocol.KeyPassedCount, consumer.Passed);
			bundle.PutInt (InstrumentationProtocol.KeyFailedCount, consumer.Failed);
			bundle.PutInt (InstrumentationProtocol.KeySkippedCount, consumer.Skipped);
			bundle.PutInt ("nativeExit", exit);
			Finish (Result.Ok, bundle);
		} catch (Exception ex) {
			Log.Error (LogTag, ex.ToString ());
			bundle.PutString (InstrumentationProtocol.KeyError, ex.ToString ());
			Finish (Result.Canceled, bundle);
		}
	}

	sealed class ResultConsumer (Instrumentation instrumentation, string resultsDirectory, Type [] fixtures) : IDataConsumer
	{
		readonly List<XElement> nodes = [];
		readonly Dictionary<string, Type> fixtureTypes = fixtures.ToDictionary (fixture =>
			fixture.FullName ?? throw new InvalidOperationException ("A proof fixture has no full name."));
		int passed, failed, skipped;
		public int Passed => passed;
		public int Failed => failed;
		public int Skipped => skipped;
		public string? TrxPath { get; private set; }
		public string Uid => nameof (ResultConsumer);
		public string DisplayName => nameof (ResultConsumer);
		public string Description => "D native Android result and artifact evidence";
		public string Version => "1.0";
		public Task<bool> IsEnabledAsync () => Task.FromResult (true);
		public Type [] DataTypesConsumed => [typeof (TestNodeUpdateMessage), typeof (SessionFileArtifact)];

		public string SaveNodes ()
		{
			var path = Path.Combine (resultsDirectory, "D.Nodes.xml");
			lock (nodes)
				new XDocument (new XElement ("nodes", nodes)).Save (path);
			return path;
		}

		public Task ConsumeAsync (IDataProducer producer, IData data, CancellationToken cancellationToken)
		{
			if (data is SessionFileArtifact artifact) {
				Log.Info (LogTag, "[D ARTIFACT] " + artifact.FileInfo.FullName);
				if (artifact.FileInfo.Extension == ".trx")
					TrxPath = artifact.FileInfo.FullName;
				return Task.CompletedTask;
			}
			if (data is not TestNodeUpdateMessage { TestNode: var node })
				throw new InvalidOperationException ($"Unexpected native message: {data.GetType ().FullName}");

			var state = node.Properties.SingleOrDefault<TestNodeStateProperty> ();
			var method = node.Properties.SingleOrDefault<TestMethodIdentifierProperty> ();
			string? outcome = state switch {
				PassedTestNodeStateProperty => InstrumentationProtocol.OutcomePassed,
				FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty => InstrumentationProtocol.OutcomeFailed,
				SkippedTestNodeStateProperty => InstrumentationProtocol.OutcomeSkipped,
				_ => null,
			};
			string qualified = method is null ? "" : $"{method.Namespace}.{method.TypeName}.{method.MethodName}";
			string definingAssembly = "";
			if (method is not null) {
				string typeName = $"{method.Namespace}.{method.TypeName}";
				if (!fixtureTypes.TryGetValue (typeName, out var fixture))
					throw new InvalidOperationException ($"Native metadata names an unregistered proof fixture: {typeName}");
				definingAssembly = fixture.Assembly.FullName ?? throw new InvalidOperationException ("A proof assembly has no full name.");
				if (!string.IsNullOrEmpty (method.AssemblyFullName) && method.AssemblyFullName != definingAssembly)
					throw new InvalidOperationException ($"Native assembly metadata does not match the registered fixture: {typeName}");
			}
			lock (nodes) {
				nodes.Add (new XElement ("node",
					new XAttribute ("uid", node.Uid.Value),
					new XAttribute ("assembly", definingAssembly),
					new XAttribute ("nativeAssembly", method?.AssemblyFullName ?? ""),
					new XAttribute ("namespace", method?.Namespace ?? ""),
					new XAttribute ("type", method?.TypeName ?? ""),
					new XAttribute ("method", method?.MethodName ?? ""),
					new XAttribute ("display", node.DisplayName),
					new XAttribute ("state", state?.GetType ().Name ?? ""),
					node.Properties.OfType<TestMetadataProperty> ().Select (trait =>
						new XElement ("trait", new XAttribute ("key", trait.Key), new XAttribute ("value", trait.Value)))));
			}
			Log.Info (LogTag, $"[D NODE] uid={node.Uid.Value} method={qualified} display={node.DisplayName} state={state?.GetType ().Name}");
			if (outcome is null && state is not InProgressTestNodeStateProperty)
				return Task.CompletedTask;
			if (outcome == InstrumentationProtocol.OutcomePassed)
				Interlocked.Increment (ref passed);
			else if (outcome == InstrumentationProtocol.OutcomeFailed)
				Interlocked.Increment (ref failed);
			else if (outcome == InstrumentationProtocol.OutcomeSkipped)
				Interlocked.Increment (ref skipped);
			var status = new Bundle ();
			status.PutString (InstrumentationProtocol.KeyEvent, outcome is null ? InstrumentationProtocol.EventStart : InstrumentationProtocol.EventFinish);
			status.PutString (InstrumentationProtocol.KeyTest, node.Uid.Value);
			status.PutString (InstrumentationProtocol.KeyName, node.DisplayName);
			status.PutString (InstrumentationProtocol.KeyClass, method is null ? "" : $"{method.Namespace}.{method.TypeName}");
			if (outcome is not null)
				status.PutString (InstrumentationProtocol.KeyOutcome, outcome);
			var error = node.Properties.SingleOrDefault<TrxExceptionProperty> ();
			string? message = error?.Message ?? state switch {
				FailedTestNodeStateProperty failure => failure.Exception?.Message ?? failure.Explanation,
				ErrorTestNodeStateProperty failure => failure.Exception?.Message ?? failure.Explanation,
				TimeoutTestNodeStateProperty failure => failure.Exception?.Message ?? failure.Explanation,
				SkippedTestNodeStateProperty skip => skip.Explanation,
				_ => null,
			};
			if (message is not null)
				status.PutString (InstrumentationProtocol.KeyMessageBase64, Convert.ToBase64String (Encoding.UTF8.GetBytes (message)));
			if (error?.StackTrace is string stack)
				status.PutString (InstrumentationProtocol.KeyStackBase64, Convert.ToBase64String (Encoding.UTF8.GetBytes (stack)));
			instrumentation.SendStatus (0, status);
			return Task.CompletedTask;
		}
	}
}
