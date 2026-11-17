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

namespace Xamarin.Android.UnitTests;

public abstract class MSTestInstrumentation : Instrumentation
{
	const string LogTag = "MSTestInstrumentation";
	static string [] explicitlyIncludedCategories = [];
	Bundle? instrumentationArguments;

	protected MSTestInstrumentation (IntPtr handle, JniHandleOwnership ownership)
		: base (handle, ownership) { }

	protected virtual IEnumerable<string>? ExcludedCategories => null;
	protected virtual IEnumerable<string>? IncludedCategories => null;
	protected abstract IEnumerable<Assembly> GetTestAssemblies ();

	public static bool IsCategoryExplicitlyIncluded (string category)
		=> Array.IndexOf (Volatile.Read (ref explicitlyIncludedCategories), category) >= 0;

	protected static bool IsOnlyIncludedCategory (string category)
	{
		var categories = Volatile.Read (ref explicitlyIncludedCategories);
		return categories.Length == 1 && string.Equals (categories [0], category, StringComparison.Ordinal);
	}

	public override void OnCreate (Bundle? arguments)
	{
		instrumentationArguments = arguments;
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			Log.Error (LogTag, $"Unhandled exception (terminating={e.IsTerminating}): {e.ExceptionObject}");
		TaskScheduler.UnobservedTaskException += (_, e) =>
			Log.Error (LogTag, $"Unobserved task exception: {e.Exception}");
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
			if (instrumentationArguments?.ContainsKey ("list") == true)
				throw new NotSupportedException ("Standalone listing is not supported by this Android instrumentation. Native test identities are recorded during execution.");
			var directory = Application.Context.GetExternalFilesDir (null)?.AbsolutePath
				?? throw new InvalidOperationException ("Android external results directory is unavailable.");
			var results = Path.Combine (directory, "TestResults");
			if (Directory.Exists (results))
				Directory.Delete (results, recursive: true);
			Directory.CreateDirectory (results);

			var assemblies = GetTestAssemblies ().Distinct ().ToArray ();
			if (assemblies.Length == 0)
				throw new InvalidOperationException ("No test assemblies were registered.");
			foreach (var assembly in assemblies)
				Log.Info (LogTag, $"Registering native MSTest assembly: {assembly.FullName}");
			var commandLine = new List<string> {
				"--results-directory", results, "--report-trx", "--progress", "off", "--output", "normal",
			};
			var filter = BuildFilter ();
			if (filter is not null) {
				commandLine.Add ("--filter");
				commandLine.Add (filter);
			}
			var consumer = new ResultConsumer (this, results, assemblies);
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
					throw new IOException ($"Cannot export test result file: {file}");
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

	string? BuildFilter ()
	{
		var clauses = new List<string> ();
		var includes = GetListExtra ("include").Concat (IncludedCategories ?? []).Distinct ().ToArray ();
		Volatile.Write (ref explicitlyIncludedCategories, includes);
		if (includes.Length > 0)
			clauses.Add ("(" + string.Join ("|", includes.Select (category => "TestCategory=" + FilterValue (category))) + ")");
		var excludes = GetListExtra ("exclude");
		if (!string.Equals (instrumentationArguments?.GetString ("noexclusions"), "true", StringComparison.OrdinalIgnoreCase))
			excludes = excludes.Concat (ExcludedCategories ?? []);
		clauses.AddRange (excludes.Distinct ().Select (category => "TestCategory!=" + FilterValue (category)));
		var filter = clauses.Count == 0 ? null : string.Join ("&", clauses);
		Log.Info (LogTag, $"Native MSTest filter: {filter ?? "(all tests)"}");
		return filter;
	}

	IEnumerable<string> GetListExtra (string key)
		=> instrumentationArguments?.GetString (key)?.Split ([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

	static string FilterValue (string value)
	{
		if (value.Length == 0 || value.Any (character => !char.IsAsciiLetterOrDigit (character) && character is not '-' and not '_' and not '.'))
			throw new ArgumentException ($"Unsupported category filter value: '{value}'.");
		return value;
	}

	sealed class ResultConsumer (Instrumentation instrumentation, string resultsDirectory, Assembly [] assemblies) : IDataConsumer
	{
		readonly List<XElement> nodes = [];
		int passed, failed, skipped;
		public int Passed => passed;
		public int Failed => failed;
		public int Skipped => skipped;
		public string? TrxPath { get; private set; }
		public string Uid => nameof (ResultConsumer);
		public string DisplayName => nameof (ResultConsumer);
		public string Description => "Native Android test results and identity evidence";
		public string Version => "1.0";
		public Task<bool> IsEnabledAsync () => Task.FromResult (true);
		public Type [] DataTypesConsumed => [typeof (TestNodeUpdateMessage), typeof (SessionFileArtifact)];

		public string SaveNodes ()
		{
			var path = Path.Combine (resultsDirectory, "NativeTestNodes.xml");
			lock (nodes)
				new XDocument (new XElement ("run",
					new XElement ("assemblies", assemblies.Select (assembly => new XElement ("assembly", assembly.FullName))),
					new XElement ("nodes", nodes))).Save (path);
			return path;
		}

		public Task ConsumeAsync (IDataProducer producer, IData data, CancellationToken cancellationToken)
		{
			if (data is SessionFileArtifact artifact) {
				Log.Info (LogTag, $"Test artifact: {artifact.FileInfo.FullName}");
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
			lock (nodes) {
				nodes.Add (new XElement ("node",
					new XAttribute ("uid", node.Uid.Value),
					new XAttribute ("nativeAssembly", method?.AssemblyFullName ?? ""),
					new XAttribute ("namespace", method?.Namespace ?? ""),
					new XAttribute ("type", method?.TypeName ?? ""),
					new XAttribute ("method", method?.MethodName ?? ""),
					new XAttribute ("display", node.DisplayName),
					new XAttribute ("state", state?.GetType ().Name ?? ""),
					node.Properties.OfType<TestMetadataProperty> ().Select (trait =>
						new XElement ("trait", new XAttribute ("key", trait.Key), new XAttribute ("value", trait.Value)))));
			}
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
			var stack = error?.StackTrace ?? state switch {
				FailedTestNodeStateProperty failure => failure.Exception?.StackTrace,
				ErrorTestNodeStateProperty failure => failure.Exception?.StackTrace,
				TimeoutTestNodeStateProperty failure => failure.Exception?.StackTrace,
				_ => null,
			};
			if (stack is not null)
				status.PutString (InstrumentationProtocol.KeyStackBase64, Convert.ToBase64String (Encoding.UTF8.GetBytes (stack)));
			instrumentation.SendStatus (0, status);
			return Task.CompletedTask;
		}
	}
}
