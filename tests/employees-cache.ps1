# Compiles the actual employee controller against existing application dependencies.
# Tests use only fake services and isolated temporary photo files; no database or API calls.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $projectRoot 'bin'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-employees-cache-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
try {
    $exePath = Join-Path $testDirectory 'EmployeesCacheTests.exe'
    $arguments = @('/nologo', '/target:exe', '/nowarn:0436', '/r:System.Web.dll', '/r:System.Configuration.dll', '/r:System.Core.dll', '/r:System.Data.dll', '/r:System.Data.Linq.dll', '/r:System.Runtime.Caching.dll')
    $arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
        try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
        catch { } # Skip native DLLs.
    }
    $arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'employees-cache.cs') + '"', '"' + (Join-Path $projectRoot 'Controllers/EmployeesController.cs') + '"')
    $responsePath = Join-Path $testDirectory 'compile.rsp'
    [IO.File]::WriteAllLines($responsePath, $arguments)
    & (Join-Path $binPath 'roslyn/csc.exe') ('@' + $responsePath)
    if ($LASTEXITCODE -ne 0) { throw 'Employee cache regression compilation failed. Build GTX first.' }
    & $exePath $binPath
    if ($LASTEXITCODE -ne 0) { throw 'Employee cache regressions failed.' }
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolved = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notmatch '^gtx-employees-cache-[a-f0-9]{32}$') { throw 'Unexpected employee test directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
