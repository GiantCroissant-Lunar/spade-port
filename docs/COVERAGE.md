# Spade Code Coverage Report

This directory contains code coverage reports for the Spade .NET port.

## Quick Start

### Generate Coverage Data
```bash
cd dotnet
dotnet test tests/Spade.Tests/Spade.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

### View Coverage (Option 1: Summary)
```powershell
$xml = [xml](Get-Content "tests/Spade.Tests/coverage.cobertura.xml")
$lineRate = [math]::Round([double]$xml.coverage.'line-rate' * 100, 2)
Write-Host "Line Coverage: $lineRate%"
```

### View Coverage (Option 2: HTML Report)
```bash
# Install ReportGenerator globally (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator `
  -reports:"tests/Spade.Tests/coverage.cobertura.xml" `
  -targetdir:"docs/coverage-report" `
  -reporttypes:Html

# Open: docs/coverage-report/index.html
```

## Coverage Goals

For a computational geometry library:
- **Target**: 70-80% line coverage
- **Core algorithms** (DCEL, Delaunay, Voronoi): Aim for >85%
- **Utility code**: 70-75% is fine
- **Error paths**: Don't chase 100%

## Latest Coverage

Run the commands above to see current coverage metrics.

## CI/CD Integration

```bash
# Fail build if coverage drops below threshold
dotnet test /p:CollectCoverage=true /p:Threshold=70 /p:ThresholdType=line
```
