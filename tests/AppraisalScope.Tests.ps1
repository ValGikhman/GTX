# Run after a Debug build with Windows PowerShell (.NET Framework).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'bin/Newtonsoft.Json.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin/GTX.dll'))
$type = $assembly.GetType('GTX.Common.AppraisalComparableScope', $true)
$select = $type.GetMethod('Select', [Reflection.BindingFlags]'NonPublic,Static')
$scope = [Newtonsoft.Json.Linq.JObject]::Parse('{"vin":"SUBJECT","miles":60000,"radius":100}')

function Select-References($rows, $maximumRadius = 5000) {
    $arguments = New-Object object[] 4
    $arguments[0] = [Newtonsoft.Json.Linq.JArray]::Parse((ConvertTo-Json -InputObject @($rows) -Depth 5))
    $arguments[1] = $scope
    $arguments[2] = 'Test'
    $arguments[3] = $maximumRadius
    # JObject implements IEnumerable, so suppress PowerShell's result enumeration.
    return ,$select.Invoke($null, $arguments)
}

$rows = @(1..5 | ForEach-Object { @{ vin = "VIN$_"; miles = 60000; dist = 125; price = 20000 - $_ * 1000 } })
$result = Select-References $rows
if ([int]$result['strictCount'] -ne 0 -or $result['listings'].Count -ne 5 -or
    [int]$result['effectiveRadius'] -ne 150 -or [int]$result['mileageWindow'] -ne 10000) {
    throw 'Radius must expand by 50 before increasing mileage.'
}
if ([decimal]$result['listings'][0]['price'] -ne 15000 -or ![bool]$result['listings'][0]['outsideRadius']) {
    throw 'Expanded references must be identified and sorted by asking price.'
}

$rows = @(1..5 | ForEach-Object { @{ vin = "VIN$_"; miles = 90000; dist = 100; price = 10000 + $_ } })
$result = Select-References $rows
if ([int]$result['mileageWindow'] -ne 35000 -or ![bool]$result['listings'][0]['outsideMileage']) {
    throw 'Mileage must expand by 25,000 and be marked as outside the original scope.'
}
$result = Select-References $rows 100
if ([int]$result['effectiveRadius'] -ne 100 -or $result['listings'].Count -ne 5) {
    throw 'Recent inventory must respect its radius limit while expanding mileage.'
}

$rows += @(
    @{ vin = 'SUBJECT'; miles = 60000; dist = 10; price = 1 },
    @{ vin = 'VIN1'; miles = 90000; dist = 100; price = 50000 },
    @{ vin = 'UNKNOWN'; miles = 60000; price = 500 },
    @{ vin = 'ZERO'; miles = 60000; dist = 10; price = 0 }
)
$result = Select-References $rows
if ($result['listings'].Count -ne 5 -or @($result['listings'] | Where-Object { [string]$_['vin'] -in @('SUBJECT', 'UNKNOWN', 'ZERO') }).Count) {
    throw 'Duplicate VINs, subject VIN, missing distances and invalid prices must be excluded.'
}
$result = Select-References @()
if ($result['listings'].Count -ne 0) { throw 'Empty results must remain empty.' }

$rows = @(1..7 | ForEach-Object { @{ listing_id = "CARS$_"; miles = 60000; dist = 25; price = 20000 - $_ * 1000 } })
$result = Select-References $rows
if ($result['listings'].Count -ne 7 -or [decimal]$result['listings'][0]['price'] -ne 13000) {
    throw 'Listings without a VIN must retain their source listing IDs and numeric price order.'
}
$rows = @(@{ vin = 'STRICT'; miles = 60000; dist = 30; price = 50000 })
$rows += @(1..5 | ForEach-Object { @{ vin = "EXPANDED$_"; miles = 60000; dist = 125; price = 10000 + $_ } })
$result = Select-References $rows
if ([int]$result['strictCount'] -ne 1 -or $result['listings'].Count -ne 5 -or
    @($result['listings'] | Where-Object { [string]$_['vin'] -eq 'STRICT' }).Count -ne 1) {
    throw 'An expanded search must preserve original-scope matches.'
}
'PASS: expansion order, mileage steps, price sorting, scope labels, recent limit, deduplication and sparse results.'
