# Run after building GTX.csproj, using Windows PowerShell (.NET Framework).
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
Add-Type -Path (Join-Path $repoPath 'bin/GTX.dll')

function New-TestVehicle($stock, $visibility) {
    $vehicle = New-Object Common.GTXDTO
    $vehicle.Stock = $stock
    $vehicle.Year = 2024
    $vehicle.Make = 'Land Rover'
    $vehicle.Model = 'Range Rover'
    $vehicle.VehicleStyle = 'Sport/Utility'
    $vehicle.SetToUpload = $visibility
    return $vehicle
}

$updated = [DateTime]::SpecifyKind([DateTime]'2026-09-23 12:00:00', [DateTimeKind]::Utc)
$vehicles = [Common.GTXDTO[]]@(
    (New-TestVehicle 'NEW123' 'Y'),
    (New-TestVehicle 'HIDDEN' 'N'),
    (New-TestVehicle '' 'Y')
)
$xml = [xml]([SitemapWriter]::Build($vehicles, $updated).ToString())
$locations = @($xml.urlset.url | ForEach-Object { $_.loc })
if ($locations.Count -ne 10) { throw 'Expected nine static pages and one visible vehicle.' }
if ($locations -notcontains 'https://usedcarscincinnati.com/Inventory/2024-Land-Rover-Range-Rover-Sport-Utility-NEW123') {
    throw 'Visible vehicle URL missing or incorrectly formatted.'
}
if (@($xml.urlset.url | Where-Object { $_.lastmod -ne '2026-09-23T12:00:00Z' }).Count) {
    throw 'Incorrect sitemap timestamp.'
}
# A rebuilt snapshot must remove vehicles absent from the restored inventory.
$restored = [xml]([SitemapWriter]::Build([Common.GTXDTO[]]@((New-TestVehicle 'PREVIOUS' 'Y')), $updated).ToString())
if ($restored.OuterXml.Contains('NEW123') -or !$restored.OuterXml.Contains('PREVIOUS')) {
    throw 'Rebuild retained vehicles from the superseded inventory.'
}
$empty = [xml]([SitemapWriter]::Build([Common.GTXDTO[]]@(), $updated).ToString())
if (@($empty.urlset.url).Count -ne 9) { throw 'Empty inventory should preserve static pages only.' }
Write-Output 'PASS: visible vehicles, URL formatting, timestamps, replacement snapshot, and empty inventory.'
