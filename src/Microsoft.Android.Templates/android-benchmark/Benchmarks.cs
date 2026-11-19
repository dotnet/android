#:property TargetFramework=net12.0-android
#:property RootNamespace=Benchmarks
#:property PublishAot=false
#:package BenchmarkDotNet@0.15.*

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

// Android can't launch BenchmarkDotNet's generated benchmark processes, so run in-process.
[assembly: InProcess]

// Run with: dotnet run Benchmarks.cs -c Release
BenchmarkRunner.Run<Benchmark> ();

public class Benchmark
{
	[Benchmark]
	public int Sum ()
	{
		int total = 0;
		for (int i = 0; i < 1000; i++)
			total += i;
		return total;
	}
}
