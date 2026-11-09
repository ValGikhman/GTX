$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo '/out:bin\gtx-csv-regression.exe' '/reference:bin\GTX.dll' 'tests\AppraisalCsvRegression.cs'
    if($LASTEXITCODE -ne 0){throw 'CSV regression compilation failed.'}
    [xml]$web=Get-Content Web.config
    Set-Content bin/gtx-csv-regression.exe.config ('<configuration>'+ $web.configuration.runtime.OuterXml +'</configuration>')
    & ./bin/gtx-csv-regression.exe
    if($LASTEXITCODE -ne 0){throw 'CSV regression failed.'}
} finally {Pop-Location}
