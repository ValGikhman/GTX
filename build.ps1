param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$Solution = "GTX.sln",

    [string]$Target = "Build",

    [switch]$NoMultiProc
)

$ErrorActionPreference = "Stop"

function Resolve-MSBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"

    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
        if ($found) {
            return $found
        }

        $installPath = & $vswhere -latest -products * -property installationPath
        if ($installPath) {
            $candidate = Join-Path $installPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    $fallbacks = @(
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($path in $fallbacks) {
        if (Test-Path $path) {
            return $path
        }
    }

    throw "MSBuild.exe not found. Install Visual Studio Build Tools (Web workload) or Visual Studio with MSBuild."
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = if ([System.IO.Path]::IsPathRooted($Solution)) { $Solution } else { Join-Path $repoRoot $Solution }

if (-not (Test-Path $solutionPath)) {
    throw "Solution not found: $solutionPath"
}

$msbuild = Resolve-MSBuildPath

$args = @(
    $solutionPath,
    "/t:$Target",
    "/p:Configuration=$Configuration"
)

if (-not $NoMultiProc) {
    $args += "/m"
}

Write-Host "MSBuild: $msbuild"
Write-Host "Solution: $solutionPath"
Write-Host "Configuration: $Configuration"
Write-Host "Target: $Target"

Push-Location $repoRoot
try {
    & $msbuild @args
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

