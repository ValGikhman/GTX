# Run after a Debug build with Windows PowerShell. No database or network calls.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Web
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Mvc.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
$bucketType = $assembly.GetType('GTX.Common.AppraisalSearchMileage', $true)
$bucket = $bucketType.GetMethod('Bucket', $flags)
$chatType = $assembly.GetType('GTX.Controllers.ChatController', $true)
$parse = $chatType.GetMethod('CarGurusMileage', $flags)
$searchUrl = $chatType.GetMethod('CarGurusSearchUrl', $flags)
if (!$bucket -or !$parse -or !$searchUrl) { throw 'CarGurus mileage projection helpers are missing.' }
$previousCulture = [Threading.Thread]::CurrentThread.CurrentCulture
try {
    [Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('fr-FR')
    foreach ($case in @(
        @{ Actual = 0; Expected = 100000 }, @{ Actual = 1; Expected = 100000 },
        @{ Actual = 99999; Expected = 100000 }, @{ Actual = 100000; Expected = 100000 },
        @{ Actual = 100001; Expected = 150000 }, @{ Actual = 120000; Expected = 150000 },
        @{ Actual = 149999; Expected = 150000 }, @{ Actual = 150000; Expected = 150000 },
        @{ Actual = 150001; Expected = 200000 }, @{ Actual = 200000; Expected = 200000 },
        @{ Actual = 200001; Expected = 250000 }, @{ Actual = 1999999; Expected = 2000000 },
        @{ Actual = 2000000; Expected = 2000000 }, @{ Actual = $null; Expected = $null },
        @{ Actual = -1; Expected = $null }, @{ Actual = 2000001; Expected = $null },
        @{ Actual = [int]::MaxValue; Expected = $null }
    )) {
        $actual = $bucket.Invoke($null, [object[]]@($case.Actual))
        if ($actual -ne $case.Expected) { throw "Incorrect search mileage bucket for '$($case.Actual)'." }
    }
    $message = 'Check CarGurus for VIN 5J8TB1H25BA008242 with less than 120,000 odometer miles within 100 miles of ZIP 45040'
    foreach ($case in @(
        @{ Text = $message; Miles = 120000; Search = 150000 },
        @{ Text = 'CarGurus RDX under 99k miles within 100 miles of ZIP 45040'; Miles = 99000; Search = 100000 },
        @{ Text = 'CarGurus RDX with 107,201 miles within 100 miles of ZIP 45040'; Miles = 107201; Search = 150000 },
        @{ Text = 'CarGurus RDX within 100 miles of ZIP 45040'; Miles = $null; Search = $null }
    )) {
        $parsed = $parse.Invoke($null, [object[]]@($case.Text))
        if ($parsed.Item1 -ne $case.Miles) { throw 'Odometer parsing confused mileage with radius or ZIP.' }
        $maximum = $bucket.Invoke($null, [object[]]@($parsed.Item1))
        if ($maximum -ne $case.Search) { throw 'CarGurus did not apply the appraisal mileage bucket.' }
        $url = $searchUrl.Invoke($null, [object[]]@('45040', '100', 'm4/d921', '2011', $maximum, 'AWD', 'PRICE'))
        $query = [System.Web.HttpUtility]::ParseQueryString(([Uri]$url).Query)
        if ($query['zip'] -cne '45040' -or $query['distance'] -cne '100' -or
            $query['startYear'] -cne '2011' -or $query['endYear'] -cne '2011') {
            throw 'Mileage bucketing changed the search ZIP, radius or vehicle year.'
        }
        if ($null -eq $maximum) {
            if ($null -ne $query['maxMileage']) { throw 'A request without odometer mileage gained a mileage ceiling.' }
        } elseif ($query['maxMileage'] -cne $maximum.ToString([Globalization.CultureInfo]::InvariantCulture)) {
            throw 'The CarGurus link does not use the effective search-mileage bucket.'
        }
    }
    Write-Output 'PASS: shared 100K/50K mileage boundaries, exact VIN request parsing, unchanged ZIP/radius and bucketed CarGurus link.'
}
finally { [Threading.Thread]::CurrentThread.CurrentCulture = $previousCulture }
