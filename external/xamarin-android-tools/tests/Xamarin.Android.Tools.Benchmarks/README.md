```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8246)
Intel Core i9-14900KF, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2


```
| Method         | Mean      | Error    | StdDev   | Allocated |
|--------------- |----------:|---------:|---------:|----------:|
| HashBytes      |  23.34 μs | 0.123 μs | 0.115 μs |      56 B |
| HashStream     |  23.07 μs | 0.075 μs | 0.070 μs |     120 B |
| HashFile       |  52.98 μs | 0.766 μs | 0.716 μs |     360 B |
| HasFileChanged | 118.44 μs | 2.285 μs | 2.138 μs |     720 B |
