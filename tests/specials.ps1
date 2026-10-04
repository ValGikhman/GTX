# Build GTX first. Integration checks use only SiteDEV on localhost\MSSQLSERVER01.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $projectRoot 'bin'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-specials-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$exePath = Join-Path $testDirectory 'SpecialsTests.exe'
$arguments = @('/nologo', '/target:exe', '/r:System.Web.dll', '/r:System.Configuration.dll', '/r:System.Core.dll', '/r:System.Data.dll', '/r:System.Transactions.dll', '/r:System.Xml.Linq.dll')
$arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
    catch { }
}
$arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'specials.cs') + '"')
$responsePath = Join-Path $testDirectory 'compile.rsp'
[IO.File]::WriteAllLines($responsePath, $arguments)
& (Join-Path $binPath 'roslyn/csc.exe') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Specials test compilation failed.' }
[IO.File]::WriteAllText($exePath + '.config', '<configuration><appSettings><add key="SiteResponsibility" value="Site"/><add key="SiteEnvironment" value="Dev"/></appSettings></configuration>')
& $exePath $binPath (Join-Path $projectRoot '../Services/Properties/Settings.settings')
if ($LASTEXITCODE -ne 0) { throw 'Specials regression tests failed.' }
