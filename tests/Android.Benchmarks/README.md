# Android benchmarks

This instrumentation-only app runs BenchmarkDotNet in-process on an Android
device. It has no activity.

The project opts into caller-unsafe memory safety rules with
`MemorySafetyRulesVersion=2` and the preview C# compiler. Benchmark entry points
remain safe to invoke; JNI argument buffers are scoped to synchronous calls, and
setup/cleanup own the Java references used by those calls.

Run every benchmark:

```sh
./dotnet-local.sh run --project tests/Android.Benchmarks/Android.Benchmarks.csproj -c Release
```

Filter benchmarks by passing BenchmarkDotNet's `--filter` argument:

```sh
./dotnet-local.sh run --project tests/Android.Benchmarks/Android.Benchmarks.csproj -c Release -- --filter '*JniMethodInfoBenchmarks*'
```

The instrumentation result reports the on-device artifacts directory. Results
are also streamed through logcat by `dotnet run`.

## Console projects

Generate a file-based benchmark app with the .NET 12 Android workload:

```sh
dotnet new android-benchmark -n Benchmarks
dotnet run --file Benchmarks.cs -c Release
```

The template selects CoreCLR and BenchmarkDotNet's in-process runner. Use
`android-console` instead for a plain file-based console app.

With CoreCLR, a normal C# console project can run BenchmarkDotNet on Android
without an activity, manifest, or custom instrumentation. Keep `OutputType=Exe`,
target `net12.0-android`, and reference BenchmarkDotNet:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net12.0-android</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.15.8" />
  </ItemGroup>
</Project>
```

Use the direct runner and opt into in-process execution:

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<SampleBenchmarks> ();

[InProcess]
public class SampleBenchmarks
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
```

Run with `dotnet run -c Release`. When using this repository's local SDK, run
`./dotnet-local.sh run --project <path-to-project> -c Release` instead.
Benchmark progress and the summary stream to the terminal, but the report files
are **not** copied to the host. The default CSV, Markdown, and HTML reports stay
in the app's private `files/BenchmarkDotNet.Artifacts/results` directory on the
device; retrieve them manually with `adb` (using `run-as` for the app's private
directory).
Unhandled exceptions return Android's Unix crash exit code, 134, including when
`dotnet run` is invoked from Windows.

For a file-based app, prepend these directives to the C# source. File-based apps
default to NativeAOT, so opt out to use the JIT-based in-process runner:

```csharp
#:property TargetFramework=net12.0-android
#:property RootNamespace=BenchmarksApp
#:property PublishAot=false
#:package BenchmarkDotNet@0.15.*
```

The explicit root namespace avoids collisions between the benchmark class and
Android's generated resource namespace when the file has the same name.

BenchmarkDotNet 0.15.x's `BenchmarkSwitcher` subscribes to
`Console.CancelKeyPress`, which throws `PlatformNotSupportedException` on
Android. Use `BenchmarkRunner.Run<T> ()` without its optional `args` argument;
passing `args` also creates a switcher. `[InProcess]` uses Reflection.Emit, so
this example is not suitable for NativeAOT.
