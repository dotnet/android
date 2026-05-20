using BenchmarkDotNet.Attributes;

namespace AndroidBenchmark1;

[MemoryDiagnoser]
public class StringBenchmarks
{
	readonly string [] values = Enumerable.Range (0, 100).Select (i => i.ToString ()).ToArray ();

	[Benchmark]
	public int ParseIntegers ()
	{
		var sum = 0;
		foreach (var value in values)
			sum += int.Parse (value);
		return sum;
	}
}
