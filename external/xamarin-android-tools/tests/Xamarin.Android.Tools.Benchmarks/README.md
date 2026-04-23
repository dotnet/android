```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8246)
Intel Core i9-14900KF, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2


```
| Method         | Mean       | Error   | StdDev  | Allocated |
|--------------- |-----------:|--------:|--------:|----------:|
| HashBytes      |   468.2 μs | 2.01 μs | 1.57 μs |     232 B |
| HashStream     |   482.9 μs | 7.47 μs | 6.99 μs |     232 B |
| HashFile       |   764.9 μs | 8.08 μs | 7.56 μs |     474 B |
| HasFileChanged | 1,496.6 μs | 5.74 μs | 5.08 μs |     945 B |
