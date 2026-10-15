using BenchmarkDotNet.Running;
using Microsoft.Android.ResourceDesigner.Benchmarks;

if (args is ["--verify"]) {
	foreach (bool application in new [] { false, true }) {
		var benchmark = new ResourceDesignerBenchmarks { IsApplication = application };
		try {
			benchmark.Setup ();
		} finally {
			benchmark.Cleanup ();
		}
	}
} else {
	BenchmarkSwitcher.FromAssembly (typeof (Program).Assembly).Run (args);
}
