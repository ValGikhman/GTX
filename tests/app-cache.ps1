$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-app-cache-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$exePath = Join-Path $testDirectory 'AppCacheTests.exe'
& (Join-Path $projectRoot 'bin/roslyn/csc.exe') /nologo /target:exe /r:System.Runtime.Caching.dll /r:System.Core.dll "/out:$exePath" (Join-Path $PSScriptRoot 'app-cache.cs') (Join-Path $projectRoot 'Common/AppCache.cs')
if ($LASTEXITCODE -ne 0) { throw 'Cache test compilation failed.' }
& $exePath
if ($LASTEXITCODE -ne 0) { throw 'Cache regression tests failed.' }
