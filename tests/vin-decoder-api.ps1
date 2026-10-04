# Uses existing bin dependencies to compile the current controller and run offline API regression tests.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $projectRoot 'bin'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-vin-api-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$exePath = Join-Path $testDirectory 'VinDecoderApiTests.exe'
$arguments = @('/nologo', '/target:exe', '/nowarn:0436', '/r:System.Web.dll', '/r:System.Configuration.dll', '/r:System.Core.dll')
$arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
    catch { } # Skip native DLLs.
}
$arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'vin-decoder-api.cs') + '"', '"' + (Join-Path $projectRoot 'Controllers/VinDecoderController.cs') + '"')
$responsePath = Join-Path $testDirectory 'compile.rsp'
[IO.File]::WriteAllLines($responsePath, $arguments)
& (Join-Path $binPath 'roslyn/csc.exe') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'VIN decoder test compilation failed.' }
& $exePath $binPath
if ($LASTEXITCODE -ne 0) { throw 'VIN decoder API regression tests failed.' }
