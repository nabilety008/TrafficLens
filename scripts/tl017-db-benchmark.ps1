param(
    [string]$TestDbPath = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\tl017-db-bench\test-history.db",
    [int]$FlushCount = 100,
    [int]$BucketsPerFlush = 60,
    [switch]$LargeDb = $false,
    [int]$LargeDbDays = 90
)
$ErrorActionPreference = 'Stop'

$repoRoot = "C:\Users\ali\Documents\New folder\TrafficLens"
$projPath = Join-Path $repoRoot "src\TrafficLens.Infrastructure\TrafficLens.Infrastructure.csproj"
$testDir = Join-Path $repoRoot "artifacts\tl017-db-bench"

# Build the project
Write-Host "Building Infrastructure project..."
& "C:\dotnet\dotnet.exe" build $projPath -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Create test directory
if (Test-Path $testDir) { Remove-Item -Recurse -Force $testDir }
New-Item -ItemType Directory -Force -Path $testDir | Out-Null

Write-Host "Running database benchmark..."
Write-Host "Test DB: $TestDbPath"
Write-Host "Flush iterations: $FlushCount"
Write-Host "Buckets per flush: $BucketsPerFlush"

# Run the benchmark via a test program
$benchCode = @"
using System;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.Infrastructure.History;
using TrafficLens.Core.History;
using TrafficLens.Core.Models;

class DbBenchmark
{
    static void Main(string[] args)
    {
        var dbPath = args[0];
        var flushCount = int.Parse(args[1]);
        var bucketsPerFlush = int.Parse(args[2]);
        var largeDb = bool.Parse(args[3]);
        var largeDbDays = int.Parse(args[4]);

        var logger = NullLogger<SqliteTrafficHistoryRepository>.Instance;
        var repo = new SqliteTrafficHistoryRepository(dbPath, logger);
        repo.InitializeAsync(default).Wait();

        var sw = Stopwatch.StartNew();
        var rnd = new Random(42);
        var timeZone = TimeZoneInfo.Local;
        var baseTime = DateTime.UtcNow.Date.AddDays(-largeDbDays);

        if (largeDb)
        {
            Console.WriteLine($"Pre-populating {largeDbDays} days of data...");
            var buckets = new List<TrafficHistoryBucket>();
            for (int day = 0; day < largeDbDays; day++)
            {
                for (int minute = 0; minute < 1440; minute += 5) // every 5 minutes
                {
                    var bucketTime = baseTime.AddDays(day).AddMinutes(minute);
                    buckets.Add(new TrafficHistoryBucket(
                        bucketTime,
                        300,
                        (long)(rnd.NextDouble() * 10_000_000),
                        (long)(rnd.NextDouble() * 5_000_000)));
                }
            }
            var insertSw = Stopwatch.StartNew();
            repo.AppendBucketsAsync(buckets, timeZone, default).Wait();
            Console.WriteLine($"Pre-populated {buckets.Count} buckets in {insertSw.ElapsedMilliseconds} ms");
            Console.WriteLine($"DB size: {new System.IO.FileInfo(dbPath).Length / 1024.0:F1} KB");
        }

        // Benchmark writes
        Console.WriteLine($"\n--- Write Benchmark ({flushCount} flushes x {bucketsPerFlush} buckets) ---");
        var writeTimes = new List<long>();
        for (int i = 0; i < flushCount; i++)
        {
            var buckets = new List<TrafficHistoryBucket>();
            var now = DateTime.UtcNow;
            for (int b = 0; b < bucketsPerFlush; b++)
            {
                var bucketTime = now.AddMinutes(-flushCount + i).AddMinutes(b);
                buckets.Add(new TrafficHistoryBucket(
                    bucketTime,
                    60,
                    (long)(rnd.NextDouble() * 10_000_000),
                    (long)(rnd.NextDouble() * 5_000_000)));
            }

            var flushSw = Stopwatch.StartNew();
            repo.AppendBucketsAsync(buckets, timeZone, default).Wait();
            writeTimes.Add(flushSw.ElapsedMilliseconds);
        }

        Console.WriteLine($"Write: avg {writeTimes.Average():F1} ms, min {writeTimes.Min()} ms, max {writeTimes.Max()} ms, p95 {Percentile(writeTimes, 95):F1} ms");
        Console.WriteLine($"DB size after writes: {new System.IO.FileInfo(dbPath).Length / 1024.0:F1} KB");

        // Check WAL size
        var walPath = dbPath + "-wal";
        if (System.IO.File.Exists(walPath))
        {
            Console.WriteLine($"WAL size: {new System.IO.FileInfo(walPath).Length / 1024.0:F1} KB");
        }

        // Benchmark queries
        Console.WriteLine($"\n--- Query Benchmark ---");
        var queryTimes = new List<long>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        // Query 30 days
        var q30Sw = Stopwatch.StartNew();
        var daily30 = repo.QueryDailyAsync(today.AddDays(-30), today.AddDays(1), default).Result;
        queryTimes.Add(q30Sw.ElapsedMilliseconds);
        Console.WriteLine($"QueryDaily 30 days: {q30Sw.ElapsedMilliseconds} ms ({daily30.Count} rows)");

        // Query 7 days
        var q7Sw = Stopwatch.StartNew();
        var daily7 = repo.QueryDailyAsync(today.AddDays(-7), today.AddDays(1), default).Result;
        queryTimes.Add(q7Sw.ElapsedMilliseconds);
        Console.WriteLine($"QueryDaily 7 days: {q7Sw.ElapsedMilliseconds} ms ({daily7.Count} rows)");

        // Query 1 day
        var q1Sw = Stopwatch.StartNew();
        var daily1 = repo.QueryDailyAsync(today, today.AddDays(1), default).Result;
        queryTimes.Add(q1Sw.ElapsedMilliseconds);
        Console.WriteLine($"QueryDaily 1 day: {q1Sw.ElapsedMilliseconds} ms ({daily1.Count} rows)");

        // Query lifetime
        var qlSw = Stopwatch.StartNew();
        var lifetime = repo.QueryLifetimeAsync(default).Result;
        queryTimes.Add(qlSw.ElapsedMilliseconds);
        Console.WriteLine($"QueryLifetime: {qlSw.ElapsedMilliseconds} ms ({lifetime.TotalBytes:N0} bytes)");

        // Query all (full history)
        if (largeDb)
        {
            var qaSw = Stopwatch.StartNew();
            var dailyAll = repo.QueryDailyAsync(DateOnly.MinValue, today.AddDays(1), default).Result;
            queryTimes.Add(qaSw.ElapsedMilliseconds);
            Console.WriteLine($"QueryDaily ALL: {qaSw.ElapsedMilliseconds} ms ({dailyAll.Count} rows)");
        }

        Console.WriteLine($"\nQuery: avg {queryTimes.Average():F1} ms, min {queryTimes.Min()} ms, max {queryTimes.Max()} ms");

        // Prune benchmark
        Console.WriteLine($"\n--- Prune Benchmark ---");
        var pruneSw = Stopwatch.StartNew();
        var deleted = repo.PruneRawSamplesBeforeAsync(DateTime.UtcNow.AddDays(-1), default).Result;
        Console.WriteLine($"Prune 1 day: {pruneSw.ElapsedMilliseconds} ms ({deleted} rows deleted)");

        repo.Dispose();
    }

    static double Percentile(List<long> values, int percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, index)];
    }
}
"@

# Compile and run benchmark
$exePath = Join-Path $testDir "DbBenchmark.exe"
& "C:\dotnet\dotnet.exe" build $projPath -c Release --no-restore --nologo 2>&1 | Out-Null

# Create a small console app project for benchmarking
$benchProj = Join-Path $testDir "DbBenchmark.csproj"
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$projPath" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path $benchProj -Encoding UTF8

$benchCode | Set-Content -Path (Join-Path $testDir "Program.cs") -Encoding UTF8

$largeDbStr = if ($LargeDb) { "True" } else { "False" }
& "C:\dotnet\dotnet.exe" run --project $benchProj -c Release -- $TestDbPath $FlushCount $BucketsPerFlush $largeDbStr $LargeDbDays 2>&1

# Cleanup
Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue