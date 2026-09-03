<#
.SYNOPSIS
  Thin wrapper around the Weaver commands used by this sample (the official
  examples use a Makefile for the same purpose). Run from this directory.

.EXAMPLE
  ./weaver.ps1 check        # validate registry/
  ./weaver.ps1 generate     # regenerate TodoApi/Telemetry/Generated/*.g.cs
  ./weaver.ps1 docs         # regenerate docs/ (markdown) with the official semconv templates
  ./weaver.ps1 live-check   # start an OTLP listener that validates what the app emits
  ./weaver.ps1 verify       # generate + fail if the generated code is out of date (CI)
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet('check', 'generate', 'docs', 'live-check', 'verify')]
    [string]$Task = 'check'
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$semconv = 'https://github.com/open-telemetry/semantic-conventions/archive/refs/tags/v1.37.0.zip'
$generated = 'TodoApi/Telemetry/Generated'

switch ($Task) {
    'check' {
        weaver registry check -r registry
    }
    'generate' {
        weaver registry generate -r registry -t templates csharp $generated
    }
    'docs' {
        weaver registry generate -r registry -t "$semconv[templates]" markdown docs
    }
    'live-check' {
        # Listens for OTLP/gRPC on 127.0.0.1:4317, prints a report when stopped
        # (Ctrl+C, 60 s of silence, or POST http://localhost:4320/stop).
        weaver registry live-check -r registry --otlp-grpc-port 4317 --admin-port 4320 --inactivity-timeout 60
    }
    'verify' {
        weaver registry generate -r registry -t templates csharp $generated
        git diff --exit-code -- $generated
        if ($LASTEXITCODE -ne 0) { throw "Generated code is out of date. Run ./weaver.ps1 generate and commit." }
        Write-Host 'OK: generated code is up to date'
    }
}
exit $LASTEXITCODE
