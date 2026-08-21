# Android benchmarks

This instrumentation-only app runs BenchmarkDotNet in-process on an Android
device. It has no activity.

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
