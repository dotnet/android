using System.Text;
using Android.App;
using Android.OS;
using Android.Runtime;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace AndroidBenchmark1;

[Instrumentation (Name = "com.companyname.AndroidBenchmark1.BenchmarkInstrumentation")]
public class BenchmarkInstrumentation : Instrumentation
{
	const string BenchmarkArgsKey = "benchmarkArgsBase64";

	Bundle? arguments;

	protected BenchmarkInstrumentation (IntPtr handle, JniHandleOwnership ownership)
		: base (handle, ownership)
	{
	}

	public override void OnCreate (Bundle? arguments)
	{
		base.OnCreate (arguments);
		this.arguments = arguments;
		Start ();
	}

	public override void OnStart ()
	{
		base.OnStart ();

		Task.Run (() => {
			var bundle = new Bundle ();
			try {
				var writablePath = Application.Context.GetExternalFilesDir (null)?.AbsolutePath ?? Path.GetTempPath ();
				var artifactsPath = Path.Combine (writablePath, "BenchmarkDotNet.Artifacts");
				Directory.CreateDirectory (artifactsPath);

				var benchmarkArgs = DecodeBenchmarkArguments (arguments);
				var summaries = RunBenchmarks (benchmarkArgs, artifactsPath);
				var benchmarkCount = summaries.Sum (summary => summary.Reports.Length);
				var failed = summaries.Length == 0 || summaries.Any (summary => summary.HasCriticalValidationErrors || summary.Reports.Any (report => !report.Success));

				bundle.PutInt ("benchmarks", benchmarkCount);
				bundle.PutString ("artifactsPath", artifactsPath);
				bundle.PutString ("summary", $"BenchmarkDotNet completed {benchmarkCount} benchmark report(s).");
				Finish (failed ? Result.Canceled : Result.Ok, bundle);
			} catch (Exception ex) {
				bundle.PutString ("error", ex.ToString ());
				Finish (Result.Canceled, bundle);
			}
		});
	}

	static string [] DecodeBenchmarkArguments (Bundle? arguments)
	{
		var encodedArgs = arguments?.GetString (BenchmarkArgsKey);
		if (string.IsNullOrEmpty (encodedArgs))
			return [];

		try {
			var decodedArgs = Encoding.UTF8.GetString (Convert.FromBase64String (encodedArgs));
			return decodedArgs.Length == 0 ? [] : decodedArgs.Split ('\0');
		} catch (FormatException ex) {
			throw new InvalidOperationException ($"Invalid BenchmarkDotNet argument payload in '{BenchmarkArgsKey}'.", ex);
		}
	}

	static Summary [] RunBenchmarks (string [] benchmarkArgs, string artifactsPath)
	{
		var logger = ConsoleLogger.Default;
		var options = ParseBenchmarkArguments (benchmarkArgs);
		var config = DefaultConfig.Instance
			.AddJob (options.Job.WithToolchain (InProcessNoEmitToolchain.Instance))
			.WithArtifactsPath (artifactsPath);
		if (options.Filters.Count > 0)
			config = config.AddFilter (new GlobFilter (options.Filters.ToArray ()));

		var (allTypesValid, runnableTypes) = TypeFilter.GetTypesWithRunnableBenchmarks ([], [typeof (BenchmarkInstrumentation).Assembly], logger);
		if (!allTypesValid || runnableTypes.Count == 0)
			return [];

		var benchmarkRunInfos = TypeFilter.Filter (config, runnableTypes);
		return BenchmarkRunner.Run (benchmarkRunInfos);
	}

	static BenchmarkOptions ParseBenchmarkArguments (string [] benchmarkArgs)
	{
		var options = new BenchmarkOptions ();
		for (int i = 0; i < benchmarkArgs.Length; i++) {
			var argument = benchmarkArgs [i];
			if (argument == "-f" || argument == "--filter") {
				options.Filters.Add (GetRequiredArgumentValue (benchmarkArgs, ref i, argument));
			} else if (argument.StartsWith ("--filter=", StringComparison.Ordinal)) {
				options.Filters.Add (argument.Substring ("--filter=".Length));
			} else if (argument == "-j" || argument == "--job") {
				options.Job = GetJob (GetRequiredArgumentValue (benchmarkArgs, ref i, argument));
			} else if (argument.StartsWith ("--job=", StringComparison.Ordinal)) {
				options.Job = GetJob (argument.Substring ("--job=".Length));
			} else {
				throw new NotSupportedException ($"Unsupported BenchmarkDotNet argument '{argument}'. The Android BenchmarkDotNet template currently supports --filter/-f and --job/-j.");
			}
		}

		return options;
	}

	static string GetRequiredArgumentValue (string [] benchmarkArgs, ref int index, string argument)
	{
		if (index + 1 >= benchmarkArgs.Length)
			throw new ArgumentException ($"Missing value for '{argument}'.");
		return benchmarkArgs [++index];
	}

	static Job GetJob (string value) =>
		value.ToLowerInvariant () switch {
			"default" => Job.Default,
			"dry" => Job.Dry,
			"short" => Job.ShortRun,
			"medium" => Job.MediumRun,
			"long" => Job.LongRun,
			_ => throw new ArgumentException ($"Unsupported BenchmarkDotNet job '{value}'. Supported jobs are Default, Dry, Short, Medium, and Long."),
		};

	sealed class BenchmarkOptions
	{
		public Job Job { get; set; } = Job.Default;
		public List<string> Filters { get; } = [];
	}
}
