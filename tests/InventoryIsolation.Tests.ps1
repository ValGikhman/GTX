# Run after a Debug build with Windows PowerShell (.NET Framework). No database or network calls.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $repoPath 'bin'
$compiler = Join-Path $binPath 'roslyn/csc.exe'
$assemblyPath = Join-Path $binPath 'GTX.dll'
if (!(Test-Path -LiteralPath $assemblyPath)) { throw 'Build GTX before running inventory isolation tests.' }
$assemblyDate = (Get-Item -LiteralPath $assemblyPath).LastWriteTimeUtc
foreach ($source in @('Controllers/InventoryController.cs', 'Controllers/BaseController.cs', 'Models/GTXClasses.cs', 'Common/InventorySearchResults.cs')) {
    if ((Get-Item -LiteralPath (Join-Path $repoPath $source)).LastWriteTimeUtc -gt $assemblyDate) {
        throw 'GTX.dll predates inventory changes. Rebuild GTX before running inventory isolation tests.'
    }
}
$testExecutable = Join-Path ([IO.Path]::GetTempPath()) ('GTX.InventoryIsolation.' + [Guid]::NewGuid().ToString('N') + '.exe')
$references = @('System.Core.dll', 'System.Web.dll', 'System.Runtime.Caching.dll')
$references += @('Common.dll', 'Services.dll', 'GTX.dll', 'System.Web.Mvc.dll') | ForEach-Object { Join-Path $binPath $_ }
$compilerArguments = @('/nologo', '/target:exe', ('/out:' + $testExecutable))
$compilerArguments += $references | ForEach-Object { '/reference:' + $_ }
$compilerArguments += Join-Path $PSScriptRoot 'InventoryIsolation.Tests.cs'
try {
    & $compiler @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Inventory isolation test compilation failed. Build GTX first.' }
    & $testExecutable $binPath
    if ($LASTEXITCODE -ne 0) { throw 'Inventory isolation regression tests failed.' }
}
finally {
    if (Test-Path -LiteralPath $testExecutable) { Remove-Item -LiteralPath $testExecutable }
}
