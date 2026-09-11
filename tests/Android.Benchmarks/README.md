# Android benchmarks

This instrumentation-only app runs BenchmarkDotNet in-process on an Android
device. It has no activity.

Run every benchmark:

```sh
./dotnet-local.sh run --project tests/Android.Benchmarks/Android.Benchmarks.csproj -c Release
```

To filter benchmarks, install the app on a specific device and pass the filter
as an instrumentation argument:

```sh
./dotnet-local.sh build tests/Android.Benchmarks/Android.Benchmarks.csproj \
    -c Release -t:Install -p:Device=DEVICE_SERIAL
adb -s DEVICE_SERIAL shell am instrument -w \
    -e filter '*JniMethodInfoBenchmarks*' \
    net.dot.android.benchmarks/.BenchmarkInstrumentation
```

Peer lookup investigation benchmark groups can be run with:

```sh
adb -s DEVICE_SERIAL shell am instrument -w \
    -e filter '*Peer*' \
    net.dot.android.benchmarks/.BenchmarkInstrumentation
adb -s DEVICE_SERIAL shell am instrument -w \
    -e filter '*ExportRoundtrip*' \
    net.dot.android.benchmarks/.BenchmarkInstrumentation
```

`PeerLookupGcBridgeBenchmarks` deliberately runs collections on a background
thread. Treat it as a stress comparison that includes GC suspension and
scheduling effects, rather than a steady-state microbenchmark or a direct
measurement of GC-bridge code. It uses more measurement iterations than the
other benchmark groups and reports median and maximum iteration time; these
statistics still describe iteration-level throughput rather than individual
lookup latency. Each measured lookup batch waits for the benchmark's GC worker
to complete a forced collection, so the pressure result also includes any
remaining collection wait at the end of the batch.

The recent-peer lookup benchmark keeps its peers alive. It measures the normal
live-peer hit and locality tradeoff, including the added cost when consecutive
lookups target different peers; it does not model stale weak references.

The instrumentation result reports the on-device artifacts directory. Results
are also streamed through logcat by `dotnet run`.
