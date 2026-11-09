param([Parameter(Mandatory=$true)][string]$PdfPath)
$ErrorActionPreference='Stop'
$repoPath=Split-Path $PSScriptRoot -Parent
Push-Location $repoPath
try {
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo '/out:bin\gtx-pdf-regression.exe' '/reference:bin\GTX.dll' '/reference:bin\UglyToad.PdfPig.Core.dll' 'tests\AppraisalPdfRegression.cs'
    if($LASTEXITCODE -ne 0){throw 'PDF regression compilation failed.'}
    [xml]$web=Get-Content Web.config
    Set-Content bin/gtx-pdf-regression.exe.config ('<configuration>'+ $web.configuration.runtime.OuterXml +'</configuration>')
    & ./bin/gtx-pdf-regression.exe $PdfPath
    if($LASTEXITCODE -ne 0){throw 'PDF regression failed.'}
} finally { Pop-Location }
