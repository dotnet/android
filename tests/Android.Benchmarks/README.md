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
