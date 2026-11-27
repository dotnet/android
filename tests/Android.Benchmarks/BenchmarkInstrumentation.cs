using Android.Runtime;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace Xamarin.Android.Benchmarks;

[Instrumentation (Name = "net.dot.android.benchmarks.BenchmarkInstrumentation")]
public class BenchmarkInstrumentation : Instrumentation
{
	protected BenchmarkInstrumentation (IntPtr handle, JniHandleOwnership ownership)
		: base (handle, ownership)
	{
	}

	public override void OnCreate (Bundle? arguments)
	{
		base.OnCreate (arguments);
		Filter = GetFilter (arguments);
		Start ();
	}

	public override void OnStart ()
	{
		base.OnStart ();

		var results = new Bundle ();
		try {
			var externalFiles = Application.Context.GetExternalFilesDir (null)?.AbsolutePath;
			var artifactsPath = Path.Combine (externalFiles ?? Path.GetTempPath (), "BenchmarkDotNet.Artifacts");
			var config = ManualConfig.CreateEmpty ()
				.AddJob (Job.ShortRun
					.WithToolchain (InProcessNoEmitToolchain.Instance)
					.WithId ("Android"))
				.AddLogger (ConsoleLogger.Default)
				.AddColumnProvider (DefaultColumnProviders.Instance)
				.AddExporter (CsvExporter.Default, MarkdownExporter.GitHub)
				.WithArtifactsPath (artifactsPath)
				.WithOptions (ConfigOptions.DisableOptimizationsValidator);
			if (Filter != null)
				config.AddFilter (new GlobFilter ([Filter]));

			var summaries = BenchmarkRunner.Run (GetType ().Assembly, config);
			var reportCount = summaries.Sum (summary => summary.Reports.Length);
			var hasErrors = summaries.Any (summary => summary.HasCriticalValidationErrors);

			results.PutInt ("reports", reportCount);
			results.PutString ("artifactsPath", artifactsPath);
			Console.WriteLine ($"BENCHMARKS_COMPLETE reports={reportCount} artifacts={artifactsPath}");
			Finish (hasErrors ? Result.Canceled : Result.Ok, results);
		} catch (Exception ex) {
			results.PutString ("error", ex.ToString ());
			Console.WriteLine ($"BENCHMARKS_FAILED {ex}");
			Finish (Result.Canceled, results);
		}
	}

	string? Filter { get; set; }

	static string? GetFilter (Bundle? arguments)
	{
		var filter = arguments?.GetString ("filter");
		if (!string.IsNullOrWhiteSpace (filter))
			return filter;

		var value = arguments?.GetString ("args");
		if (string.IsNullOrWhiteSpace (value))
			return null;

		var values = value.Split (' ', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < values.Length - 1; i++) {
			if (values [i] == "--filter")
				return values [i + 1];
		}
		return null;
	}
}
