# Run after a Debug build. Provider and AI HTTP calls are simulated; no database or paid requests.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $projectRoot 'bin'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-chat-appraisal-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$exePath = Join-Path $testDirectory 'ChatMarketAppraisalTests.exe'
$arguments = @('/nologo', '/target:exe', '/r:System.Web.dll', '/r:System.Configuration.dll', '/r:System.Core.dll', '/r:System.Runtime.Caching.dll')
$arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
    catch { } # Skip native DLLs.
}
$arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'chat-market-appraisal.cs') + '"')
$responsePath = Join-Path $testDirectory 'compile.rsp'
[IO.File]::WriteAllLines($responsePath, $arguments)
& (Join-Path $binPath 'roslyn/csc.exe') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Market appraisal test compilation failed. Build the application first.' }
& $exePath $binPath
if ($LASTEXITCODE -ne 0) { throw 'Market appraisal regression tests failed.' }
