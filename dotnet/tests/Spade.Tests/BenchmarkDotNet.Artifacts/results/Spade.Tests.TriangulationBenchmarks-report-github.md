```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7462)
AMD Ryzen 5 5600X, 1 CPU, 12 logical and 6 physical cores
.NET SDK 9.0.307
  [Host]   : .NET 9.0.11 (9.0.1125.51716), X64 RyuJIT AVX2
  .NET 9.0 : .NET 9.0.11 (9.0.1125.51716), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.11 (9.0.1125.51716), X64 RyuJIT AVX2

Runtime=.NET 9.0  Concurrent=True  

```
| Method                | Job        | Server | Toolchain              | IterationCount | LaunchCount | WarmupCount | Mean     | Error     | StdDev   | Gen0      | Gen1      | Gen2     | Allocated |
|---------------------- |----------- |------- |----------------------- |--------------- |------------ |------------ |---------:|----------:|---------:|----------:|----------:|---------:|----------:|
| BulkInsert_10K_Points | Job-XSAYRG | True   | InProcessEmitToolchain | 10             | Default     | 3           | 51.04 ms |  2.419 ms | 1.439 ms | 5100.0000 | 1400.0000 | 700.0000 |  72.96 MB |
| BulkInsert_10K_Points | .NET 9.0   | False  | Default                | Default        | Default     | Default     | 50.33 ms |  2.018 ms | 5.951 ms | 5090.9091 | 1363.6364 | 636.3636 |  72.96 MB |
| BulkInsert_10K_Points | ShortRun   | False  | Default                | 3              | 1           | 3           | 53.55 ms | 21.593 ms | 1.184 ms | 5100.0000 | 1400.0000 | 700.0000 |  72.96 MB |
