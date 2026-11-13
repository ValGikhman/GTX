# Run after a Debug build with Windows PowerShell (.NET Framework). No database or network calls.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Mvc.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$items = [GTX.Common.MajordomeMenu]::Items
if ($items.Count -ne 15) { throw 'Catalog must cover every Majordome item including Roles, Profile and Services.' }
if (@($items | Group-Object Item | Where-Object Count -ne 1).Count) { throw 'Duplicate menu keys.' }
$regularItems = @($items | Where-Object { $_.Item.ToString() -notin @('Roles', 'Profile', 'Services') })
$regularDefaults = ($regularItems | ForEach-Object { $_.Item.ToString() }) -join ','
foreach ($name in @('Owner', 'Tech', 'Manager', 'Sales', 'Support', 'Blogger', 'Detail Team')) {
    $role = New-Object GTX.Models.SiteRoleModel
    $role.Id = 123
    $role.Name = $name
    $role.Permissions = [GTX.Common.MajordomeMenu]::Defaults($name)
    if (($role.Permissions -join ',') -ne $regularDefaults) { throw "Incorrect regular defaults for $name." }
    foreach ($item in $items) {
        $expected = $item.Item.ToString() -notin @('Roles', 'Profile', 'Services')
        if ([GTX.Common.SiteAccess]::Can($role, $item.Item) -ne $expected) { throw "Incorrect permission for $name / $($item.Item)." }
    }
    # Existing names, including Owner, must respect permission edits.
    $role.Permissions = @()
    foreach ($item in $items) {
        if ([GTX.Common.SiteAccess]::Can($role, $item.Item)) { throw "Role $name bypassed an edited permission." }
    }
}
$custom = New-Object GTX.Models.SiteRoleModel
$custom.Id = 124
$custom.Name = 'Detail Team'
$custom.Permissions = @('VinDecoder','Appraisal','Roles','Profile','Services')
if (![GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::VinDecoder)) { throw 'Custom role grant failed.' }
if (![GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Appraisal)) { throw 'Custom appraisal grant failed.' }
foreach ($reserved in @([GTX.Common.MajordomeItem]::Roles, [GTX.Common.MajordomeItem]::Profile, [GTX.Common.MajordomeItem]::Services)) {
    if ([GTX.Common.SiteAccess]::Can($custom, $reserved)) { throw "Regular role obtained reserved $reserved access through a stored grant." }
}
$custom.Permissions = $null
if ([GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Appraisal)) { throw 'Null permission list must deny access.' }

$administrator = New-Object GTX.Models.SiteRoleModel
$administrator.Id = [GTX.Common.MajordomeAccount]::Id
$administrator.Name = [GTX.Common.MajordomeAccount]::Name
if (![GTX.Common.SiteAccess]::IsMajordome($administrator)) { throw 'Built-in Majordome identity was not recognized.' }
foreach ($item in $items) {
    if (![GTX.Common.SiteAccess]::Can($administrator, $item.Item)) { throw "Majordome lacks $($item.Item) access." }
}
if ([GTX.Common.SiteAccess]::Can($administrator, [Enum]::ToObject([GTX.Common.MajordomeItem], 999))) { throw 'An undefined menu item must not become authorized.' }
$custom.Name = 'Majordome'
$custom.Permissions = @('Roles', 'Profile', 'Services')
if ([GTX.Common.SiteAccess]::IsMajordome($custom) -or
    [GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Roles) -or
    [GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Profile) -or
    [GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Services)) { throw 'A database role named Majordome impersonated the built-in identity.' }
$administrator.Name = 'Owner'
if ([GTX.Common.SiteAccess]::IsMajordome($administrator)) { throw 'Reserved numeric identity alone must not grant Majordome access.' }
if ([GTX.Common.SiteAccess]::Can($administrator, [GTX.Common.MajordomeItem]::Appraisal)) { throw 'A non-Majordome role must not use the reserved numeric identity.' }
if ([GTX.Common.SiteAccess]::IsMajordome($null)) { throw 'Anonymous request recognized as Majordome.' }
foreach ($item in $items) {
    if ([GTX.Common.SiteAccess]::Can($null, $item.Item)) { throw 'Anonymous access allowed.' }
    if ([GTX.Common.MajordomeMenu]::ForRequest($item.Controller, $item.Action) -ne $item.Item) { throw "Wrong route permission for $($item.Item)" }
}
if ([GTX.Common.MajordomeMenu]::ForRequest('Majordome','AppraisalResearch') -ne [GTX.Common.MajordomeItem]::Appraisal) { throw 'Research bypasses appraisal permission.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Majordome','GetUpdatedItems') -ne [GTX.Common.MajordomeItem]::Inventory) { throw 'Inventory refresh bypasses permission.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('InventoryManagement','GetInventoryDashboardVehicleDetails') -ne [GTX.Common.MajordomeItem]::InventoryDashboard) { throw 'Dashboard AJAX permission mismatch.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Profile','Index') -ne [GTX.Common.MajordomeItem]::Profile) { throw 'Profile GET/POST Index permission mismatch.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Profile','Save') -ne [GTX.Common.MajordomeItem]::Profile) { throw 'Profile write permission mismatch.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Services','Index') -ne [GTX.Common.MajordomeItem]::Services) { throw 'Services GET permission mismatch.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Services','Save') -ne [GTX.Common.MajordomeItem]::Services) { throw 'Services write permission mismatch.' }
if ([int][GTX.Common.MajordomeItem]::Roles -ne 12 -or [int][GTX.Common.MajordomeItem]::Profile -ne 13) { throw 'Existing menu identifiers must remain stable.' }
if ([int][GTX.Common.MajordomeItem]::Services -ne 14) { throw 'Services must append to the existing menu identifiers.' }

# Command rows must hide the private account name without altering audit records.
$commandController = $assembly.GetType('GTX.Controllers.ChatBotCommandsController', $true)
$buildRow = $commandController.GetMethod('BuildRow', [Reflection.BindingFlags]'NonPublic,Static')
if (!$buildRow -or $buildRow.GetParameters().Count -ne 2) { throw 'Command-row authorization projection is missing.' }
$lesson = New-Object Services.ChatBotNavigationLesson
$lesson.ActionKey = 'all_inventory'
$lesson.Phrase = 'Show inventory'
foreach ($actorName in @('Majordome', 'majordome', ' MAJORDOME ')) {
    $lesson.CreatedByRole = $actorName
    $regularRow = $buildRow.Invoke($null, [object[]]@($lesson.PSObject.BaseObject, $false))
    $administratorRow = $buildRow.Invoke($null, [object[]]@($lesson.PSObject.BaseObject, $true))
    if ($regularRow.CreatedByRole -cne 'Administrator') { throw 'A regular-role command row disclosed the hidden administrator name.' }
    if ($administratorRow.CreatedByRole -cne $actorName -or $lesson.CreatedByRole -cne $actorName) {
        throw 'Administrator display or stored audit identity was changed.'
    }
}
foreach ($actorName in @('Owner', 'Manager', 'Detail Team', $null)) {
    $lesson.CreatedByRole = $actorName
    $expectedActorName = $lesson.CreatedByRole
    foreach ($revealAdministrator in @($false, $true)) {
        $row = $buildRow.Invoke($null, [object[]]@($lesson.PSObject.BaseObject, $revealAdministrator))
        if ($row.CreatedByRole -cne $expectedActorName -or $lesson.CreatedByRole -cne $expectedActorName) {
            throw 'An ordinary or missing audit role name was changed.'
        }
    }
}
Write-Output 'PASS: regular defaults and edited permissions, reserved-page/identity protection, anonymous denial, route mapping and hidden command-row administrator identity with preserved audit values.'
