$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$items = [GTX.Common.MajordomeMenu]::Items
if ($items.Count -ne 14) { throw 'Catalog must cover every Majordome item including Roles and Profile.' }
if (@($items | Group-Object Item | Where-Object Count -ne 1).Count) { throw 'Duplicate menu keys.' }
$defaults = @{
    Owner='InventoryDashboard,InventoryManagement,Inventory,VinDecoder,Appraisal,Announcements,Blogs,Specials,Employees,Health,Sitemap,ChatBotCommands,Roles,Profile'
    Tech='Inventory,VinDecoder,Sitemap'
    Manager='VinDecoder,Appraisal,Announcements,Blogs,Specials,Employees,Sitemap'
    Sales='Sitemap'; Support='Blogs,Specials,Sitemap'; Blogger='Blogs,Specials'
}
foreach ($name in $defaults.Keys) {
    $role = New-Object GTX.Models.SiteRoleModel
    $role.Name = $name
    $role.Permissions = [GTX.Common.MajordomeMenu]::Defaults($name)
    if (($role.Permissions -join ',') -ne $defaults[$name]) { throw "Incorrect defaults for $name" }
    foreach ($item in $items) {
        $expected = $defaults[$name].Split(',') -contains $item.Item.ToString()
        if ([GTX.Common.SiteAccess]::Can($role, $item.Item) -ne $expected) { throw "Incorrect permission for $name / $($item.Item)" }
    }
}
$custom = New-Object GTX.Models.SiteRoleModel
$custom.Name = 'Detail Team'
$custom.Permissions = @('VinDecoder','Appraisal','Roles')
if (![GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::VinDecoder)) { throw 'Custom role grant failed.' }
if (![GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Appraisal)) { throw 'Custom appraisal grant failed.' }
if ([GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Roles)) { throw 'Non-owner Roles access allowed.' }
if ([GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Profile)) { throw 'Profile access allowed without a grant.' }
$custom.Permissions += 'Profile'
if (![GTX.Common.SiteAccess]::Can($custom, [GTX.Common.MajordomeItem]::Profile)) { throw 'Custom Profile grant failed.' }
foreach ($item in $items) {
    if ([GTX.Common.SiteAccess]::Can($null, $item.Item)) { throw 'Anonymous access allowed.' }
    if ([GTX.Common.MajordomeMenu]::ForRequest($item.Controller, $item.Action) -ne $item.Item) { throw "Wrong route permission for $($item.Item)" }
}
if ([GTX.Common.MajordomeMenu]::ForRequest('Majordome','AppraisalResearch') -ne [GTX.Common.MajordomeItem]::Appraisal) { throw 'Research bypasses appraisal permission.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Majordome','GetUpdatedItems') -ne [GTX.Common.MajordomeItem]::Inventory) { throw 'Inventory refresh bypasses permission.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('InventoryManagement','GetInventoryDashboardVehicleDetails') -ne [GTX.Common.MajordomeItem]::InventoryDashboard) { throw 'Dashboard AJAX permission mismatch.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Profile','Index') -ne [GTX.Common.MajordomeItem]::Profile) { throw 'Profile GET/POST Index permission mismatch.' }
if ([GTX.Common.MajordomeMenu]::ForRequest('Profile','Save') -ne [GTX.Common.MajordomeItem]::Profile) { throw 'Profile write permission mismatch.' }
if ([int][GTX.Common.MajordomeItem]::Roles -ne 12 -or [int][GTX.Common.MajordomeItem]::Profile -ne 13) { throw 'Existing menu identifiers must remain stable.' }
Write-Output 'PASS: catalog, all legacy defaults, custom roles, Profile opt-in, anonymous denial, Owner protection, page and AJAX permission mapping.'
