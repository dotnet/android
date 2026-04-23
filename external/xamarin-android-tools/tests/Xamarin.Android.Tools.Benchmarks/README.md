```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8246)
Intel Core i9-14900KF, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2


```
| Method         | Mean      | Error    | StdDev   | Allocated |
|--------------- |----------:|---------:|---------:|----------:|
| HashBytes      |  46.00 μs | 0.401 μs | 0.375 μs |      56 B |
| HashStream     |  44.98 μs | 0.117 μs | 0.104 μs |     184 B |
| HashFile       |  75.83 μs | 0.605 μs | 0.566 μs |     424 B |
| HasFileChanged | 163.23 μs | 0.705 μs | 0.589 μs |     848 B |
