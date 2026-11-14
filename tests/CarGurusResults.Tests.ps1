# Run after a Debug build with Windows PowerShell. No network, database or private settings reads.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/Newtonsoft.Json.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$helperType = $assembly.GetType('GTX.Common.CarGurusAuctionResearch', $true)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
$helper = $helperType.GetMethod('IsNoListings', $flags)
$parse = $helperType.GetMethod('ParseListings', $flags)
$parseDetails = $helperType.GetMethod('ParseDetails', $flags)
if (!$helper -or !$parse -or !$parseDetails) { throw 'The CarGurus response helpers are missing.' }
$listing = '- Listing ID 123456: 2022 BMW X4 xDrive30i AWD — $32,000 · Cincinnati, OH'
foreach ($case in @(
    @{ Text = 'No vehicles found matching your search.'; Expected = $true },
    @{ Text = " `r`nNO VEHICLES FOUND MATCHING YOUR SEARCH. `r`n"; Expected = $true },
    @{ Text = 'Found 0 vehicles'; Expected = $true },
    @{ Text = 'Found 0 vehicles matching your search.'; Expected = $true },
    @{ Text = 'No listings found.'; Expected = $true },
    @{ Text = $listing; Expected = $false },
    @{ Text = "Found 1 vehicles`n$listing"; Expected = $false },
    @{ Text = "No vehicles found matching your search.`n$listing"; Expected = $false },
    @{ Text = 'Provider returned an unrecognized response.'; Expected = $false },
    @{ Text = 'CarGurus service is temporarily unavailable.'; Expected = $false },
    @{ Text = 'Error: No listings found.'; Expected = $false },
    @{ Text = ''; Expected = $false },
    @{ Text = " `r`n`t "; Expected = $false },
    @{ Text = $null; Expected = $false }
)) {
    $actual = $helper.Invoke($null, [object[]]@($case.Text))
    if ($actual -ne $case.Expected) { throw "Incorrect CarGurus no-listings classification for '$($case.Text)'." }
}
$cards = @'
Found 20 vehicles:

### 1. 2022 BMW X4
Listing ID: 456673514
$33,397 · Includes dealer fees
Deal: Overpriced · 74,256 miles · West Chester, OH
[View on CarGurus](https://www.cargurus.com/details/456673514?pid=ChatGPTApp)

### 2. 2022 BMW X4
Listing ID: 462741096
$32,590 · Includes dealer fees
Deal: Fair Deal · 67,371 miles · Fishers, IN
[View on CarGurus](https://www.cargurus.com/details/462741096?pid=ChatGPTApp)
'@
$rows = @($parse.Invoke($null, [object[]]@($cards)))
if ($rows.Count -ne 2 -or $rows[0].Groups['id'].Value -cne '456673514' -or
    $rows[0].Groups['vehicle'].Value.Trim() -cne '2022 BMW X4' -or
    $rows[0].Groups['ask'].Value.Trim() -cne '$33,397' -or
    $rows[0].Groups['miles'].Value -cne '74,256' -or
    $rows[0].Groups['location'].Value.Trim() -cne 'West Chester, OH' -or
    $rows[1].Groups['id'].Value -cne '462741096') { throw 'Captured BMW cards lost their listing fields or order.' }
$paddedCards = ($cards -replace "`r?`n", "`r`n") -replace '(?m)^', " `t"
if (@($parse.Invoke($null, [object[]]@($paddedCards))).Count -ne 2) { throw 'Indented CRLF cards were not parsed.' }
$mixed = @($parse.Invoke($null, [object[]]@($listing + "`n" + $cards + "`n" + $cards)))
if ($mixed.Count -ne 3 -or $mixed[0].Groups['id'].Value -cne '123456' -or
    $mixed[0].Groups['miles'].Success) { throw 'Legacy rows, first-result order or duplicate removal regressed.' }
$unpriced = @($parse.Invoke($null, [object[]]@($cards.Replace('$33,397', 'Price unavailable'))))
if ($unpriced.Count -ne 2 -or $unpriced[0].Groups['ask'].Value.Trim() -cne 'Price unavailable') {
    throw 'An unavailable price was invented or discarded.'
}
foreach ($text in @($null, '', 'Unexpected provider response', '### 1. BMW X4',
    "Listing ID: 123`n`$30,000`nDeal: Good Deal · 10,000 miles · Test City")) {
    if (@($parse.Invoke($null, [object[]]@($text))).Count -ne 0) { throw 'Malformed text produced fabricated listings.' }
}
$many = (1..7 | ForEach-Object { "- Listing ID ${_}: Test Vehicle — `$20,000 · Test City" }) -join "`n"
if (@($parse.Invoke($null, [object[]]@($many))).Count -ne 5) { throw 'Search results did not retain the five-listing limit.' }
$detailCards = @'
Fetched details for 2 vehicle listing(s):
# 2022 BMW X4 xDrive30i AWD
$33,397 · Includes dealer fees
Deal: OVERPRICED
Dealer rating: 4.1★ · West Chester, OH (9 mi away)

## Photos
![Photo1](https://www.cargurus.com/images/example.jpg)

## Specs
Mileage: 74,256 mi

## Details
VIN: 5UX33DT06N9N08661

## Links
[View on CarGurus](https://www.cargurus.com/details/456673514?pid=ChatGPTApp)

# 2022 BMW X4 xDrive30i AWD
$32,410 · Includes dealer fees
Dealer rating: 3.7★ · Fishers, IN (101 mi away)

## Specs
Mileage: 67,371 mi

## Details
VIN: 5UX33DT03N9K73927

## Links
[View on CarGurus](https://www.cargurus.com/details/462741096?pid=ChatGPTApp)
'@
$details = $parseDetails.Invoke($null, [object[]]@($detailCards))
$firstDetail = $details['456673514']
$vinProperty = $assembly.GetType('GTX.Models.ChatMarketListing').GetProperty('Vin', [Reflection.BindingFlags]'Instance,NonPublic')
if ($details.Count -ne 2 -or $firstDetail.Vehicle -cne '2022 BMW X4 xDrive30i AWD' -or
    $firstDetail.Ask -cne '$33,397' -or $firstDetail.Odometer -cne '74,256' -or
    $firstDetail.Location -cne 'West Chester, OH' -or $firstDetail.Distance -cne '9' -or
    $vinProperty.GetValue($firstDetail) -cne '5UX33DT06N9N08661' -or
    $details['462741096'].Distance -cne '101') { throw 'Captured BMW detail cards lost their fields or separate records.' }
$legacyDetail = @'
2022 BMW X4 xDrive30i AWD
VIN: 5UX33DT06N9N08661
Price: $33,397 | Mileage: 74,256
Dealer: West Chester, OH · 9 mi
Listing URL: https://www.cargurus.com/details/456673514
'@
$legacyDetails = $parseDetails.Invoke($null, [object[]]@($legacyDetail))
if ($legacyDetails.Count -ne 1 -or $legacyDetails['456673514'].Distance -cne '9' -or
    $legacyDetails['456673514'].Ask -cne '$33,397' -or $legacyDetails['456673514'].Odometer -cne '74,256') {
    throw 'Legacy listing details stopped working.'
}
$missingFields = $detailCards.Replace('$33,397 · Includes dealer fees', 'Price unavailable').Replace(' (9 mi away)', '')
$unknown = $parseDetails.Invoke($null, [object[]]@($missingFields))
if ($null -ne $unknown['456673514'].Ask -or $null -ne $unknown['456673514'].Distance -or
    $unknown['456673514'].Location -cne 'West Chester, OH') { throw 'Missing price or distance was invented.' }
if ($parseDetails.Invoke($null, [object[]]@($detailCards + "`n" + $detailCards)).Count -ne 2) {
    throw 'Duplicate detail records were not removed.'
}
foreach ($text in @($null, '', 'Unexpected details', $detailCards.Replace('/details/', '/unknown/'))) {
    if ($parseDetails.Invoke($null, [object[]]@($text)).Count -ne 0) { throw 'Invalid listing identifiers produced fabricated details.' }
}
Write-Output 'PASS: captured BMW search/detail cards, legacy formats, order/deduplication/limit, absent fields, malformed text and explicit no-results classification.'
