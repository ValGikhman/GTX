# Creates and removes one uniquely named local fixture database; never uses application connections or APIs.
param([string]$Server = 'localhost\MSSQLSERVER01')
$ErrorActionPreference = 'Stop'
$machine = [Regex]::Escape($env:COMPUTERNAME)
if ($Server -notmatch ('^(?:\.|localhost|127\.0\.0\.1|' + $machine + ')(?:\\[A-Za-z0-9_.$-]+)?$') -and
    $Server -notmatch '^\(localdb\)\\[A-Za-z0-9_.$-]+$') { throw 'VIN decoder storage regressions only allow local SQL Server.' }
$repoPath = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $repoPath 'bin'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-vin-storage-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
try {
    $exePath = Join-Path $testDirectory 'VinDecoderStorageTests.exe'
    $arguments = @('/nologo', '/target:exe', '/r:System.Core.dll', '/r:System.Data.dll', '/r:System.Data.Linq.dll', '/r:System.Xml.dll', '/r:System.Xml.Linq.dll')
    $arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
        try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
        catch { }
    }
    $arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'vin-decoder-storage.cs') + '"')
    [IO.File]::WriteAllLines((Join-Path $testDirectory 'compile.rsp'), $arguments)
    & (Join-Path $binPath 'roslyn/csc.exe') ('@' + (Join-Path $testDirectory 'compile.rsp'))
    if ($LASTEXITCODE -ne 0) { throw 'VIN storage regression compilation failed. Build the application first.' }
    & $exePath $binPath $Server
    if ($LASTEXITCODE -ne 0) { throw 'VIN storage regressions failed.' }
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolved = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notmatch '^gtx-vin-storage-[a-f0-9]{32}$') { throw 'Unexpected VIN storage test directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
