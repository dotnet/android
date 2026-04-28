using System.Reflection;
using System.Text;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using NUnit.VisualStudio.TestAdapter.TestingPlatformAdapter;

namespace Xamarin.Android.UnitTests;

/// <summary>
/// Base instrumentation class that runs NUnit tests on device via
/// Microsoft Testing Platform (MTP), following the same pattern as the
/// androidtest template.
/// </summary>
public abstract class TestInstrumentation : Instrumentation
{
	const string LogTag = "TestInstrumentation";

	protected TestInstrumentation (IntPtr handle, JniHandleOwnership ownership)
		: base (handle, ownership) { }

	/// <summary>
	/// Override to return categories that should be excluded from test runs.
	/// </summary>
	protected virtual IEnumerable<string>? ExcludedCategories => null;

	/// <summary>
	/// Override to return fully-qualified test names that should be excluded.
	/// Useful for skipping tests from submodules (e.g. Java.Interop) where
	/// adding attributes is not practical.
	/// </summary>
	protected virtual IEnumerable<string>? ExcludedTestNames => null;

	Bundle? instrumentationArguments;

	public override void OnCreate (Bundle? arguments)
	{
		instrumentationArguments = arguments;
		base.OnCreate (arguments);
		Start ();
	}

	public override void OnStart ()
	{
		base.OnStart ();

		Task.Run (async () =>
		{
			var consumer = new ResultConsumer (this);
			var bundle = new Bundle ();
			try {
				var writeablePath = Application.Context.GetExternalFilesDir (null)?.AbsolutePath ?? Path.GetTempPath ();
				var resultsPath = Path.Combine (writeablePath, "TestResults");
				var args = new List<string> {
					"--results-directory", resultsPath,
					"--report-trx"
				};

				var filter = BuildNUnitFilter ();
				if (filter is not null) {
					args.Add ("--treenode-filter");
					args.Add (filter);
					Log.Info (LogTag, $"Using filter: {filter}");
				}

				var builder = await TestApplication.CreateBuilderAsync (args.ToArray ());
				builder.AddNUnit (() => GetTestAssemblies ());
				builder.AddTrxReportProvider ();
				builder.TestHost.AddDataConsumer (_ => consumer);

				using ITestApplication app = await builder.BuildAsync ();
				await app.RunAsync ();

				bundle.PutInt ("passed", consumer.Passed);
				bundle.PutInt ("failed", consumer.Failed);
				bundle.PutInt ("skipped", consumer.Skipped);
				bundle.PutString ("resultsPath", consumer.TrxReportPath);
				Finish (Result.Ok, bundle);
			} catch (Exception ex) {
				bundle.PutString ("error", ex.ToString ());
				Finish (Result.Canceled, bundle);
			}
		});
	}

	/// <summary>
	/// Override to return the assemblies containing NUnit tests to run.
	/// </summary>
	protected abstract IEnumerable<Assembly> GetTestAssemblies ();

	/// <summary>
	/// Builds an NUnit filter expression from excluded categories, excluded test names,
	/// and instrumentation extras (include/exclude).
	/// </summary>
	string? BuildNUnitFilter ()
	{
		bool noExclusions = GetBoolExtra ("noexclusions");
		var parts = new List<string> ();

		// Include categories from extras: am instrument -e include "Cat1,Cat2"
		var includeExtras = GetListExtra ("include");
		foreach (var cat in includeExtras) {
			parts.Add ($"cat == {cat}");
			Log.Info (LogTag, $"Including category: {cat}");
		}

		if (!noExclusions) {
			// Excluded categories from subclass
			if (ExcludedCategories is not null) {
				foreach (var cat in ExcludedCategories) {
					parts.Add ($"cat != {cat}");
					Log.Info (LogTag, $"Excluding category: {cat}");
				}
			}

			// Excluded test names from subclass
			if (ExcludedTestNames is not null) {
				foreach (var name in ExcludedTestNames) {
					parts.Add ($"name !~ {name}");
					Log.Info (LogTag, $"Excluding test: {name}");
				}
			}
		} else {
			Log.Info (LogTag, "Skipping built-in exclusions due to noexclusions=true");
		}

		// Exclude categories from extras: am instrument -e exclude "Cat1,Cat2"
		var excludeExtras = GetListExtra ("exclude");
		foreach (var cat in excludeExtras) {
			parts.Add ($"cat != {cat}");
			Log.Info (LogTag, $"Excluding category (from extras): {cat}");
		}

		if (parts.Count == 0)
			return null;

		// NUnit filter: combine with " and "
		return string.Join (" and ", parts);
	}

	string? GetStringExtra (string key)
	{
		if (instrumentationArguments is null)
			return null;
		return instrumentationArguments.GetString (key);
	}

	bool GetBoolExtra (string key)
	{
		var value = GetStringExtra (key);
		if (value is null)
			return false;
		return string.Equals (value.Trim (), "true", StringComparison.OrdinalIgnoreCase);
	}

	List<string> GetListExtra (string key)
	{
		var value = GetStringExtra (key);
		if (value is null)
			return [];
		return value.Split ([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList ();
	}

	class ResultConsumer (Instrumentation instrumentation) : IDataConsumer
	{
		int _passed, _failed, _skipped;
		public int Passed => _passed;
		public int Failed => _failed;
		public int Skipped => _skipped;
		public string? TrxReportPath;

		public string Uid => nameof (ResultConsumer);
		public string DisplayName => nameof (ResultConsumer);
		public string Description => "";
		public string Version => "1.0";
		public Task<bool> IsEnabledAsync () => Task.FromResult (true);

		public Type[] DataTypesConsumed => [typeof (TestNodeUpdateMessage), typeof (SessionFileArtifact)];

		public Task ConsumeAsync (IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
		{
			if (value is SessionFileArtifact artifact) {
				TrxReportPath = artifact.FileInfo.FullName;
			} else if (value is TestNodeUpdateMessage { TestNode: var node }) {
				var state = node.Properties.SingleOrDefault<TestNodeStateProperty> ();
				string? outcome = state switch {
					PassedTestNodeStateProperty => "passed",
					FailedTestNodeStateProperty or ErrorTestNodeStateProperty
						or TimeoutTestNodeStateProperty => "failed",
					SkippedTestNodeStateProperty => "skipped",
					_ => null
				};
				if (outcome is null)
					return Task.CompletedTask;

				_ = outcome switch { "passed" => Interlocked.Increment (ref _passed), "failed" => Interlocked.Increment (ref _failed), _ => Interlocked.Increment (ref _skipped) };

				var id = node.Properties.SingleOrDefault<TestMethodIdentifierProperty> ();
				var b = new Bundle ();
				b.PutString ("test", id is not null ? $"{id.Namespace}.{id.TypeName}.{id.MethodName}" : node.DisplayName);
				b.PutString ("outcome", outcome);
				instrumentation.SendStatus (0, b);
			}
			return Task.CompletedTask;
		}
	}
}
