using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tools.Benchmarks;

[MemoryDiagnoser]
public class FilesHashBenchmarks
{
	const int OneMB = 1024 * 1024;

	byte [] _data = Array.Empty<byte> ();
	MemoryStream _stream = new MemoryStream ();
	string _tempFile1 = string.Empty;
	string _tempFile2 = string.Empty;

	[GlobalSetup]
	public void Setup ()
	{
		// 1MB byte array with reproducible random data
		_data = new byte [OneMB];
		new Random (42).NextBytes (_data);
		_stream.Dispose ();
		_stream = new MemoryStream (_data);

		// Two identical 1MB temp files
		_tempFile1 = Path.GetTempFileName ();
		_tempFile2 = Path.GetTempFileName ();
		File.WriteAllBytes (_tempFile1, _data);
		File.WriteAllBytes (_tempFile2, _data);
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		_stream?.Dispose ();
		if (File.Exists (_tempFile1))
			File.Delete (_tempFile1);
		if (File.Exists (_tempFile2))
			File.Delete (_tempFile2);
	}

	[Benchmark]
	public string HashBytes () => Files.HashBytes (_data);

	[Benchmark]
	public string HashStream () => Files.HashStream (_stream);

	[Benchmark]
	public string HashFile () => Files.HashFile (_tempFile1);

	[Benchmark]
	public bool HasFileChanged () => Files.HasFileChanged (_tempFile1, _tempFile2);
}
