# Run after a Debug build with Windows PowerShell (.NET Framework). No database calls.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$projection = $assembly.GetType('GTX.InventoryDashboardProjection', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$projectSummary = $projection.GetMethod('WithoutLocationTotals', $flags)

$day = [datetime]'2026-11-25'
$summary = [Services.InventoryDashboardSummary]@{
    Days = 7
    PeriodStartUtc = $day.AddDays(-7)
    PeriodEndUtc = $day
    AddedCount = 2
    StaleCount = 3
    RemovedCount = 1
    LocationCounts = @([Services.InventoryDashboardLocationCount]@{ LocationCode = 'X'; VehicleCount = 2 })
    StatusTrend = @(
        [Services.InventoryDashboardStatusTrendPoint]@{ DateUtc = $day; DateKey = '2026-11-25'; Status = 1; StatusText = 'Added'; LocationCode = 'X'; VehicleCount = 2 },
        [Services.InventoryDashboardStatusTrendPoint]@{ DateUtc = $day; DateKey = '2026-11-25'; Status = 1; StatusText = 'Added'; LocationCode = 'Y'; VehicleCount = 3 },
        [Services.InventoryDashboardStatusTrendPoint]@{ DateUtc = $day; DateKey = '2026-11-25'; Status = 2; StatusText = 'Sold'; LocationCode = 'Y'; VehicleCount = 1 }
    )
    Vehicles = @([Services.InventoryDashboardVehicle]@{
        Stock = 'ABC123'; Make = 'Ford'; LocationCode = 'X'; DetailsCounter = 4; DaysInInventory = 3
        StatusEvents = @([Services.InventoryDashboardVehicleStatusEvent]@{ DateKey = '2026-11-25'; Status = 1; StatusText = 'Added'; LocationCode = 'X' })
    })
}
$restricted = $projectSummary.Invoke($null, @($summary))
if ($restricted.LocationCounts.Count -ne 0 -or $restricted.StatusTrend.Count -ne 2 -or
    $restricted.StatusTrend[0].VehicleCount -ne 5 -or $restricted.StatusTrend[0].DateKey -ne '2026-11-25' -or
    $restricted.StatusTrend[1].VehicleCount -ne 1 -or @($restricted.StatusTrend | Where-Object LocationCode).Count -ne 0) {
    throw 'Restricted trends must aggregate by date and status without exposing location counts.'
}
$vehicle = $restricted.Vehicles[0]
if ($restricted.StatusTotal -ne 5 -or $restricted.RemovedCount -ne 1 -or $restricted.PeriodEndUtc -ne $day -or
    $vehicle.Stock -ne 'ABC123' -or $vehicle.Make -ne 'Ford' -or $vehicle.DetailsCounter -ne 4 -or $vehicle.DaysInInventory -ne 3 -or
    $vehicle.LocationCode -ne 'X' -or $vehicle.StatusEvents[0].LocationCode -ne 'X' -or $vehicle.StatusEvents[0].StatusText -ne 'Added') {
    throw 'Restricted dashboard must preserve the location code for each vehicle.'
}
if ($summary.LocationCounts[0].LocationCode -ne 'X' -or $summary.StatusTrend.Count -ne 3 -or
    $summary.Vehicles[0].LocationCode -ne 'X' -or $summary.Vehicles[0].StatusEvents[0].LocationCode -ne 'X') {
    throw 'The restricted response mutated the shared dashboard summary.'
}
Write-Output 'PASS: restricted dashboard preserves vehicle location codes and omits per-location totals without mutating shared data.'
