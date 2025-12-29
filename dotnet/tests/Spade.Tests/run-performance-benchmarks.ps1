# Performance Benchmark Runner for CI/CD Integration
# This script runs performance benchmarks and validates results against thresholds

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "BenchmarkDotNet.Artifacts/results",
    [switch]$FailOnRegression = $false,
    [string]$BaselineFile = $null
)

Write-Host "Running Spade Performance Benchmarks..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Output Path: $OutputPath" -ForegroundColor Yellow

# Ensure we're in the test directory
$testDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $testDir

# Build in release mode for accurate performance measurements
Write-Host "Building project in $Configuration mode..." -ForegroundColor Yellow
dotnet build -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Run performance regression tests first (fast validation)
Write-Host "Running performance regression tests..." -ForegroundColor Yellow
dotnet test --filter "PerformanceRegressionTests" -c $Configuration --no-build --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Performance regression tests failed"
    exit 1
}

# Run detailed benchmarks
Write-Host "Running detailed performance benchmarks..." -ForegroundColor Yellow
dotnet run -c $Configuration --no-build -- --filter "*BulkInsert*" --exporters json html csv

if ($LASTEXITCODE -ne 0) {
    Write-Error "Benchmark execution failed"
    exit 1
}

# Parse results and validate against thresholds
$resultsFile = Join-Path $OutputPath "Spade.Tests.TriangulationBenchmarks-report.json"
if (Test-Path $resultsFile) {
    Write-Host "Benchmark completed successfully. Results saved to: $OutputPath" -ForegroundColor Green
    
    # Basic result validation
    $results = Get-Content $resultsFile | ConvertFrom-Json
    $benchmarks = $results.Benchmarks
    
    Write-Host "Performance Summary:" -ForegroundColor Cyan
    foreach ($benchmark in $benchmarks) {
        $name = $benchmark.DisplayInfo
        $mean = [math]::Round($benchmark.Statistics.Mean / 1000000, 2) # Convert ns to ms
        $allocated = [math]::Round($benchmark.Memory.BytesAllocatedPerOperation / 1024 / 1024, 2) # Convert to MB
        Write-Host "  $name`: ${mean}ms, ${allocated}MB allocated" -ForegroundColor White
    }
    
    # Validate key performance thresholds (lenient for CI)
    $bulkInsert1K = $benchmarks | Where-Object { $_.DisplayInfo -like "*BulkInsert_1K_Points*" } | Select-Object -First 1
    $bulkInsert10K = $benchmarks | Where-Object { $_.DisplayInfo -like "*BulkInsert_10K_Points*" } | Select-Object -First 1
    
    $failures = @()
    
    if ($bulkInsert1K) {
        $time1K = $bulkInsert1K.Statistics.Mean / 1000000 # Convert to ms
        if ($time1K -gt 100) { # 100ms threshold for 1K points
            $failures += "1K points took ${time1K}ms (threshold: 100ms)"
        }
    }
    
    if ($bulkInsert10K) {
        $time10K = $bulkInsert10K.Statistics.Mean / 1000000 # Convert to ms
        if ($time10K -gt 2000) { # 2s threshold for 10K points
            $failures += "10K points took ${time10K}ms (threshold: 2000ms)"
        }
    }
    
    if ($failures.Count -gt 0 -and $FailOnRegression) {
        Write-Error "Performance regression detected:"
        foreach ($failure in $failures) {
            Write-Error "  - $failure"
        }
        exit 1
    } elseif ($failures.Count -gt 0) {
        Write-Warning "Performance warnings (not failing build):"
        foreach ($failure in $failures) {
            Write-Warning "  - $failure"
        }
    }
    
    Write-Host "Performance validation completed successfully!" -ForegroundColor Green
} else {
    Write-Warning "Benchmark results file not found: $resultsFile"
}

Write-Host "Benchmark artifacts available in: $OutputPath" -ForegroundColor Cyan
Write-Host "View detailed results in the HTML report for analysis." -ForegroundColor Cyan