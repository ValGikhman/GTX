# Run with Windows PowerShell (.NET Framework).
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Runtime.Caching
Add-Type -Path (Join-Path $repoPath 'Common/ChatInventoryResults.cs') -ReferencedAssemblies System.Core,System.Runtime.Caching

$stocks = [string[]]@(1..1000 | ForEach-Object { 'STOCK{0:D15}' -f $_ })
$token = [GTX.Common.ChatInventoryResults]::Store($stocks)
$url = '/Inventory/All?results=' + $token
if ($url.Length -gt 100) { throw 'Result URL is too long.' }
$found = [GTX.Common.ChatInventoryResults]::Find($token)
if ($found.Length -ne 1000 -or (Compare-Object $stocks $found)) {
    throw 'Large result set was truncated or changed.'
}
$found[0] = 'MODIFIED'
if ([GTX.Common.ChatInventoryResults]::Find($token)[0] -ne $stocks[0]) {
    throw 'Returned array mutated the saved results.'
}
$otherToken = [GTX.Common.ChatInventoryResults]::Store([string[]]@(' OTHER ', 'other', '', $null))
$other = [GTX.Common.ChatInventoryResults]::Find($otherToken)
if ($other.Length -ne 1 -or $other[0] -ne 'OTHER') { throw 'Normalization failed.' }
if ([GTX.Common.ChatInventoryResults]::Find($token).Length -ne 1000) { throw 'Searches overwrote one another.' }
foreach ($invalid in @('', 'invalid', ('a' * 5000), [Guid]::NewGuid().ToString('N'))) {
    if ($null -ne [GTX.Common.ChatInventoryResults]::Find($invalid)) { throw 'Invalid or missing token resolved.' }
}
$null = [System.Runtime.Caching.MemoryCache]::Default.Remove('GTX:ChatInventoryResults:' + $token)
if ($null -ne [GTX.Common.ChatInventoryResults]::Find($token)) { throw 'Expired results resolved.' }
if ($null -ne [GTX.Common.ChatInventoryResults]::Store([string[]]@())) { throw 'Empty results created a token.' }
$null = [System.Runtime.Caching.MemoryCache]::Default.Remove('GTX:ChatInventoryResults:' + $otherToken)
Write-Output 'PASS: short URL, 1000 matches, independent snapshots, normalization, invalid and expired tokens.'
