# Run after a Debug build with Windows PowerShell (.NET Framework). No network or database calls.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Mvc.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/Newtonsoft.Json.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$script:vin = '1ABCDEFGH23456789'
$script:caseCount = 0

function New-Style([hashtable]$Values = @{}, [string]$Name = 'Touring 4dr Sedan') {
    $basic = @{ year = '2021'; make = 'Example'; model = 'Roadcar'; trim = 'Touring'; body_type = 'Sedan'; oem_body_style = '4dr Sedan'; drive_type = 'FWD' }
    foreach ($key in $Values.Keys) { $basic[$key] = $Values[$key] }
    return @{ '@name' = $Name; basic_data = $basic }
}

function New-Payload([object[]]$Styles, [switch]$Array, [string]$ResponseVin = $script:vin) {
    $styleValue = $Styles[0]
    if ($Array -or $Styles.Count -ne 1) { $styleValue = @($Styles) }
    $value = @{ vin = $ResponseVin; data = @{ query_responses = @{ query_response = @{ us_market_data = @{ us_styles = @{ style = $styleValue } } } } } }
    # JObject implements IEnumerable; keep it a single function result.
    return ,([Newtonsoft.Json.Linq.JObject]::Parse((ConvertTo-Json -InputObject $value -Depth 12)))
}

function Assert-Identity([string]$Case, [object]$Payload, [hashtable]$Expected, [string]$RequestedVin = $script:vin) {
    $identity = [GTX.Common.AppraisalVehicleIdentity]::FromAutoDealerJson($RequestedVin, $Payload)
    foreach ($key in $Expected.Keys) {
        if ($identity.$key -cne $Expected[$key]) { throw "Incorrect $key in '$Case'." }
    }
    $script:caseCount++
    return $identity
}

function Assert-Rejected([string]$Case, [object]$Payload, [string]$RequestedVin = $script:vin) {
    try {
        [void][GTX.Common.AppraisalVehicleIdentity]::FromAutoDealerJson($RequestedVin, $Payload)
        throw "Invalid vehicle identity was accepted in '$Case'."
    }
    catch {
        $exception = $_.Exception
        while ($exception.InnerException) { $exception = $exception.InnerException }
        if ($exception -isnot [InvalidOperationException]) { throw }
    }
    $script:caseCount++
}

$full = @{ Vin = $script:vin; Year = '2021'; Make = 'Example'; Model = 'Roadcar'; Trim = 'Touring'; Style = '4dr Sedan'; BodyStyle = 'Sedan'; DriveType = 'FWD' }
[void](Assert-Identity 'Single object style' (New-Payload -Styles @((New-Style))) $full)
[void](Assert-Identity 'Single array style' (New-Payload -Styles @((New-Style)) -Array) $full)
[void](Assert-Identity 'Case-insensitive matching VIN' (New-Payload -Styles @((New-Style)) -ResponseVin $script:vin.ToLowerInvariant()) $full)
[void](Assert-Identity 'Requested VIN normalization' (New-Payload -Styles @((New-Style))) $full -RequestedVin ('  ' + $script:vin.ToLowerInvariant() + '  '))
Assert-Rejected 'Different response VIN' (New-Payload -Styles @((New-Style)) -ResponseVin '2ABCDEFGH23456789')
Assert-Rejected 'Invalid requested VIN' (New-Payload -Styles @((New-Style))) -RequestedVin 'INVALID'
Assert-Rejected 'Missing payload' $null
Assert-Rejected 'No decoded styles' ([Newtonsoft.Json.Linq.JObject]::Parse('{"vin":"1ABCDEFGH23456789","data":{}}'))

foreach ($missing in @('year', 'make', 'model')) {
    Assert-Rejected ('Missing ' + $missing) (New-Payload -Styles @((New-Style @{ $missing = $null })))
    Assert-Rejected ('Partial style with missing ' + $missing) (New-Payload -Styles @((New-Style), (New-Style @{ $missing = $null })) -Array)
}
foreach ($conflict in @(@{ year = '2020' }, @{ make = 'Other' }, @{ model = 'Other' })) {
    Assert-Rejected 'Conflicting decoded identity' (New-Payload -Styles @((New-Style), (New-Style $conflict)) -Array)
}
foreach ($year in @('0', '202A', '02021', '1885', '2101')) {
    Assert-Rejected ('Invalid year ' + $year) (New-Payload -Styles @((New-Style @{ year = $year })))
}

[void](Assert-Identity 'Equivalent YMM and detail values ignore case and outer whitespace' (New-Payload -Styles @(
    (New-Style), (New-Style @{ year = ' 2021 '; make = ' example '; model = ' ROADCAR '; trim = ' touring '; body_type = ' sedan '; oem_body_style = ' 4dr sedan '; drive_type = ' fwd ' })
) -Array) $full)

$optionalFields = @{ trim = 'Trim'; drive_type = 'DriveType'; body_type = 'BodyStyle' }
foreach ($field in $optionalFields.Keys) {
    [void](Assert-Identity ('Conflicting ' + $field + ' is omitted') (New-Payload -Styles @((New-Style), (New-Style @{ $field = 'Different' })) -Array) @{ $optionalFields[$field] = $null; Year = '2021'; Make = 'Example'; Model = 'Roadcar' })
    [void](Assert-Identity ('Missing ' + $field + ' in one style is omitted') (New-Payload -Styles @((New-Style), (New-Style @{ $field = $null })) -Array) @{ $optionalFields[$field] = $null })
    [void](Assert-Identity ('Blank ' + $field + ' in one style is omitted') (New-Payload -Styles @((New-Style), (New-Style @{ $field = ' ' })) -Array) @{ $optionalFields[$field] = $null })
}
[void](Assert-Identity 'Conflicting OEM style falls back to shared body type' (New-Payload -Styles @((New-Style), (New-Style @{ oem_body_style = 'Other' })) -Array) @{ Style = 'Sedan' })
[void](Assert-Identity 'Missing OEM style falls back to shared body type' (New-Payload -Styles @((New-Style @{ oem_body_style = $null }))) @{ Style = 'Sedan' })
[void](Assert-Identity 'Unique style name fallback' (New-Payload -Styles @((New-Style @{ oem_body_style = $null; body_type = $null }))) @{ Style = 'Touring 4dr Sedan'; BodyStyle = $null })
[void](Assert-Identity 'Ambiguous styles never choose first style name' (New-Payload -Styles @(
    (New-Style @{ oem_body_style = $null; body_type = $null } -Name 'Style A'),
    (New-Style @{ oem_body_style = $null; body_type = $null } -Name 'Style B')
) -Array) @{ Style = $null; BodyStyle = $null })

$controller = $assembly.GetType('GTX.Controllers.MajordomeController', $true)
$method = $controller.GetMethod('AppraisalDecodeVehicle')
if (!$method) { throw 'Appraisal VIN decoding endpoint is missing.' }
if (!$method.GetCustomAttributes([System.Web.Mvc.HttpPostAttribute], $true).Length) { throw 'VIN decoding endpoint must be POST-only.' }
if (!$method.GetCustomAttributes([System.Web.Mvc.ValidateAntiForgeryTokenAttribute], $true).Length) { throw 'VIN decoding endpoint requires anti-forgery validation.' }
if (!$controller.GetCustomAttributes([GTX.Common.RequireAdminRole], $true).Length) { throw 'Majordome controller must enforce menu permissions.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Majordome', 'AppraisalDecodeVehicle') -ne [GTX.Common.MajordomeItem]::Appraisal) { throw 'VIN decoding must use Appraisal permission.' }
$allowed = New-Object GTX.Models.SiteRoleModel
$allowed.Name = 'Research Team'
$allowed.Permissions = @('Appraisal')
if (![GTX.Common.SiteAccess]::Can($allowed, [GTX.Common.MajordomeItem]::Appraisal) -or [GTX.Common.SiteAccess]::Can($allowed, [GTX.Common.MajordomeItem]::VinDecoder)) { throw 'An Appraisal-only role must be able to decode vehicles for appraisal without VinDecoder access.' }

Write-Output "PASS: $script:caseCount offline decoded identity cases, VIN matching, shared YMMST values, ambiguity handling and protected Appraisal permission mapping."
