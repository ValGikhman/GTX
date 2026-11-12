# Run after a Debug build. All HTTP responses are simulated; no decoder or AI requests are made.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $projectRoot 'bin'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-chat-vin-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$exePath = Join-Path $testDirectory 'ChatVinDecodeTests.exe'
$arguments = @('/nologo', '/target:exe', '/r:System.Web.dll', '/r:System.Configuration.dll', '/r:System.Core.dll')
$arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
    catch { } # Skip native DLLs.
}
$arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'chat-vin-decode.cs') + '"')
$responsePath = Join-Path $testDirectory 'compile.rsp'
[IO.File]::WriteAllLines($responsePath, $arguments)
& (Join-Path $binPath 'roslyn/csc.exe') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Chat VIN decoder test compilation failed. Build the application first.' }
& $exePath $binPath
if ($LASTEXITCODE -ne 0) { throw 'Chat VIN decoder regression tests failed.' }
