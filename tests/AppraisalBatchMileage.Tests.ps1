# Run after a Debug build. Tests the exact outbound URL projection; no HTTP or database calls.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Web
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Mvc.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$controllerType = $assembly.GetType('GTX.Controllers.MajordomeController', $true)
$buildPath = $controllerType.GetMethod('BuildBatchAppraisalEstimatePath', [Reflection.BindingFlags]'NonPublic,Static')
if (!$buildPath) { throw 'The batch appraisal estimate URL projection is missing.' }
$previousCulture = [Threading.Thread]::CurrentThread.CurrentCulture
try {
    [Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('fr-FR')
    foreach ($case in @(
        @{ Actual = 0; Search = 100000 }, @{ Actual = 1; Search = 100000 },
        @{ Actual = 89675; Search = 100000 }, @{ Actual = 99999; Search = 100000 },
        @{ Actual = 100000; Search = 100000 }, @{ Actual = 100001; Search = 150000 },
        @{ Actual = 116589; Search = 150000 }, @{ Actual = 149999; Search = 150000 },
        @{ Actual = 150000; Search = 150000 }, @{ Actual = 150001; Search = 200000 },
        @{ Actual = 199999; Search = 200000 }, @{ Actual = 200000; Search = 200000 },
        @{ Actual = 200001; Search = 250000 }, @{ Actual = 1999999; Search = 2000000 },
        @{ Actual = 2000000; Search = 2000000 }
    )) {
        $arguments = [object[]]@('1FTBR3X81MKA59855', [int]$case.Actual, '01234', 500)
        $path = $buildPath.Invoke($null, $arguments)
        $uri = [Uri]::new('https://appraisal.example.test' + $path)
        $query = [System.Web.HttpUtility]::ParseQueryString($uri.Query)
        if ($uri.AbsolutePath -cne '/1FTBR3X81MKA59855/json' -or $query['zip'] -cne '01234' -or $query['radius'] -cne '500') {
            throw 'Applying a batch search-mileage bucket changed the VIN, ZIP or radius.'
        }
        if ($query['miles'] -cne ([int]$case.Search).ToString([Globalization.CultureInfo]::InvariantCulture)) {
            throw "Incorrect outgoing batch search mileage for actual odometer $($case.Actual)."
        }
    }
    Write-Output 'PASS: actual batch estimate URL uses 100K/50K mileage boundaries through 2M and retains VIN, leading-zero ZIP, radius and invariant numeric formatting.'
}
finally { [Threading.Thread]::CurrentThread.CurrentCulture = $previousCulture }
