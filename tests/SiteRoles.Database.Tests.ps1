# Run after a Debug build with Windows PowerShell (.NET Framework).
# Creates and removes its own uniquely named database on local SQL Server only.
param([string]$Server = 'localhost\MSSQLSERVER01')

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Data
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Helpers.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))

function Assert-LocalServer([string]$DataSource) {
    $machineName = [Regex]::Escape($env:COMPUTERNAME)
    $allowed = '^(?:\.|localhost|127\.0\.0\.1|' + $machineName + ')(?:\\[A-Za-z0-9_.$-]+)?$'
    if ($DataSource -notmatch $allowed -and $DataSource -notmatch '^\(localdb\)\\[A-Za-z0-9_.$-]+$') {
        throw 'Role database regression tests only allow a local SQL Server.'
    }
}

function Invoke-RoleSql([string]$ConnectionString, [string]$Sql, [string]$Hash = '', [switch]$Scalar) {
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        try {
            $command.CommandText = $Sql
            $command.CommandTimeout = 30
            if ($Hash) { [void]$command.Parameters.AddWithValue('@hash', $Hash) }
            if ($Scalar) { return $command.ExecuteScalar() }
            [void]$command.ExecuteNonQuery()
        }
        finally { $command.Dispose() }
    }
    finally { $connection.Dispose() }
}

function Assert-RoleRejected([string]$Label, [scriptblock]$Action, [Type]$ExpectedType = [InvalidOperationException]) {
    try {
        & $Action
        throw "Role store accepted $Label."
    }
    catch {
        $exception = $_.Exception
        while ($exception.InnerException) { $exception = $exception.InnerException }
        if (!$ExpectedType.IsInstanceOfType($exception)) { throw }
        if ($exception.Message -eq "Role store accepted $Label.") { throw }
    }
}

Assert-LocalServer $Server
$databaseName = 'GTX_Roles_Test_' + [Guid]::NewGuid().ToString('N')
$expectedDatabaseName = $databaseName
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder.set_DataSource($Server)
$builder.set_IntegratedSecurity($true)
$builder.set_InitialCatalog('master')
$builder.set_ConnectTimeout(10)
$builder.set_ApplicationName('GTX SiteRoles Database Regression')
$masterConnectionString = $builder.ConnectionString
$created = $false
$previousHash = [Environment]::GetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', 'Process')
try {
    Invoke-RoleSql $masterConnectionString ("CREATE DATABASE [$databaseName]")
    $created = $true
    $builder.set_InitialCatalog($databaseName)
    $testConnectionString = $builder.ConnectionString
    $ordinaryPassword = 'Ordinary-Role-Test42!'
    $ordinaryHash = [System.Web.Helpers.Crypto]::HashPassword($ordinaryPassword)
    Invoke-RoleSql $testConnectionString @'
CREATE TABLE dbo.SiteRole(Id int IDENTITY(100,1) PRIMARY KEY, Name nvarchar(40) NOT NULL UNIQUE, PasswordHash nvarchar(256) NOT NULL, Revision nvarchar(32) NOT NULL, Permissions nvarchar(1000) NOT NULL);
CREATE TABLE dbo.SiteRoleSetup(Id int PRIMARY KEY);
INSERT dbo.SiteRoleSetup(Id) VALUES(1);
INSERT dbo.SiteRole(Name,PasswordHash,Revision,Permissions) VALUES
(N'Owner',@hash,N'owner-before-migration',N'Roles,Profile'),
(N'Manager',@hash,N'manager-before-migration',N'Appraisal'),
(N'Detail Team',@hash,N'custom-before-migration',N'VinDecoder'),
(N'Majordome',@hash,N'impostor-before-migration',N'Roles,Profile');
'@ -Hash $ordinaryHash

    $adminPassword = 'Isolated-Admin-Test43!'
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', [System.Web.Helpers.Crypto]::HashPassword($adminPassword), 'Process')
    $builtIn = [GTX.Common.MajordomeAccount]::Current()
    # Inject this connection without invoking the constructor's app-config lookup.
    $store = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([GTX.SiteRoleStore])
    $store.connectionString = $testConnectionString
    $roles = @($store.All())
    if ($roles.Count -ne 3 -or @($roles | Where-Object { $_.Name -ieq 'Majordome' }).Count) {
        throw 'The built-in account or an impostor database record appeared in editable roles.'
    }
    $expectedPermissions = [GTX.Common.MajordomeMenu]::Defaults('Owner') -join ','
    foreach ($role in $roles) {
        if (($role.Permissions -join ',') -cne $expectedPermissions) { throw 'Migration did not enable every ordinary page for existing roles.' }
    }
    $owner = $roles | Where-Object Name -eq 'Owner'
    if ($owner.Revision -cne 'owner-before-migration') { throw 'Permission migration unnecessarily changed the Owner password revision.' }
    $initialized = [GTX.SiteRoleStore].GetField('Initialized', [Reflection.BindingFlags]'NonPublic,Static').GetValue($null)
    $locationKey = [GTX.Common.MajordomeMenu]::DashboardSectionKey([GTX.Common.DashboardPermission]::LocationCode)
    $profilePermission = [GTX.Common.MajordomeItem]::Profile
    $profileKey = [GTX.Common.MajordomeMenu]::PermissionKey($profilePermission)

    # Simulate an existing database that already completed the ordinary menu and Dashboard migrations.
    Invoke-RoleSql $testConnectionString @'
UPDATE dbo.SiteRole SET Permissions=N'Appraisal,Roles,Roles' WHERE Name=N'Manager';
UPDATE dbo.SiteRole SET Permissions=N'Users,Users,VinDecoder' WHERE Name=N'Detail Team';
UPDATE dbo.SiteRole SET Permissions=N'Profile,Appraisal' WHERE Name=N'Owner';
DELETE FROM dbo.SiteRoleSetup WHERE Id=4;
'@
    [void]$initialized.Remove($testConnectionString)
    $roles = @($store.All())
    $manager = $roles | Where-Object Name -eq 'Manager'
    $detailTeam = $roles | Where-Object Name -eq 'Detail Team'
    $owner = $roles | Where-Object Name -eq 'Owner'
    if (($manager.Permissions -join ',') -cne 'Appraisal' -or ($detailTeam.Permissions -join ',') -cne 'VinDecoder' -or
        [GTX.Common.SiteAccess]::Can($manager, [GTX.Common.MajordomeItem]::Roles) -or
        [GTX.Common.SiteAccess]::Can($detailTeam, [GTX.Common.MajordomeItem]::Roles)) {
        throw 'Legacy Roles or Users values granted access, or migration removed an ordinary permission.'
    }
    if (($owner.Permissions -join ',') -cne 'Profile,Appraisal' -or [GTX.Common.SiteAccess]::Can($owner, $profilePermission)) {
        throw 'Legacy Profile data was removed or activated without an explicit grant.'
    }
    $owner.Permissions = @('Appraisal', $profileKey)
    $store.Save($owner, $builtIn)
    $owner = $store.All() | Where-Object Name -eq 'Owner'
    if (![GTX.Common.SiteAccess]::Can($owner, $profilePermission)) { throw 'Majordome could not grant Profile to the existing Owner.' }
    $owner.Permissions = @('VinDecoder', 'InventoryDashboard')
    $store.Save($owner, $builtIn)
    $owner = $store.All() | Where-Object Name -eq 'Owner'
    if ([GTX.Common.SiteAccess]::Can($owner, $profilePermission)) { throw 'Revoked Profile access remained active for Owner.' }
    $manager.Permissions = @('Appraisal', $locationKey)
    $store.Save($manager, $builtIn)

    # Emulate an existing database where the original menu migration already ran.
    Invoke-RoleSql $testConnectionString 'DELETE FROM dbo.SiteRoleSetup WHERE Id=3'
    [void]$initialized.Remove($testConnectionString)
    $roles = @($store.All())
    $owner = $roles | Where-Object Name -eq 'Owner'
    $manager = $roles | Where-Object Name -eq 'Manager'
    if (($owner.Permissions -join ',') -cne "VinDecoder,InventoryDashboard,$locationKey") {
        throw 'Location Code migration did not preserve the existing Dashboard role grants.'
    }
    if (($manager.Permissions -join ',') -cne 'Appraisal') { throw 'The role store retained a Location Code grant without its parent.' }

    $owner.Permissions = @('VinDecoder', 'InventoryDashboard')
    $store.Save($owner, $builtIn)
    [void]$initialized.Remove($testConnectionString)
    $owner = $store.All() | Where-Object Name -eq 'Owner'
    if (($owner.Permissions -join ',') -cne 'VinDecoder,InventoryDashboard') { throw 'A restart restored a revoked Location Code grant.' }

    $owner.Permissions = @('Appraisal')
    $store.Save($owner, $builtIn)

    # Simulate a fresh worker process so the persisted migration marker is exercised.
    [void]$initialized.Remove($testConnectionString)
    $afterRestart = @($store.All())
    $owner = $afterRestart | Where-Object Name -eq 'Owner'
    if (($owner.Permissions -join ',') -cne 'Appraisal') { throw 'Migration rerun replaced a saved regular-role permission edit.' }
    if ([GTX.Common.SiteAccess]::Can($owner, [GTX.Common.MajordomeItem]::VinDecoder) -or
        [GTX.Common.SiteAccess]::Can($owner, [GTX.Common.MajordomeItem]::Profile) -or
        [GTX.Common.SiteAccess]::Can($owner, [GTX.Common.MajordomeItem]::Services)) { throw 'Owner retained implicit privileged access.' }

    $store.Delete($owner.Id, $builtIn)
    if (@($store.All() | Where-Object Name -eq 'Owner').Count) { throw 'Owner could not be deleted as an ordinary role.' }
    $replacement = New-Object GTX.Models.SiteRoleModel
    $replacement.Name = 'Owner'
    $replacement.Password = 'Replacement-Owner-Test44!'
    $replacement.Permissions = [GTX.Common.MajordomeMenu]::Defaults('Owner')
    $store.Save($replacement, $builtIn)
    $replacement = $store.All() | Where-Object Name -eq 'Owner'
    $replacement.Name = 'Former Owner'
    $replacement.Password = $null
    $store.Save($replacement, $builtIn)
    if (!@($store.All() | Where-Object Name -eq 'Former Owner').Count) { throw 'Owner could not be renamed as an ordinary role.' }

    foreach ($reservedName in @('Majordome', 'majordome', ' MAJORDOME ')) {
        $reserved = New-Object GTX.Models.SiteRoleModel
        $reserved.Name = $reservedName
        $reserved.Password = 'Reserved-Name-Test45!'
        Assert-RoleRejected 'creation of the reserved Majordome name' { $store.Save($reserved, $builtIn) }
    }
    Assert-RoleRejected 'editing the built-in account' { $store.Save($builtIn, $builtIn) }
    Assert-RoleRejected 'deleting the built-in account' { $store.Delete($builtIn.Id, $builtIn) }
    $impostor = New-Object GTX.Models.SiteRoleModel
    $impostor.Id = [int](Invoke-RoleSql $testConnectionString "SELECT Id FROM dbo.SiteRole WHERE Name=N'Majordome'" -Scalar)
    $impostor.Name = 'Pretend Admin'
    Assert-RoleRejected 'editing an existing reserved-name database record' { $store.Save($impostor, $builtIn) }
    Assert-RoleRejected 'deleting an existing reserved-name database record' { $store.Delete($impostor.Id, $builtIn) }

    $collision = New-Object GTX.Models.SiteRoleModel
    $collision.Name = 'Password Collision'
    $collision.Password = $adminPassword
    Assert-RoleRejected 'assigning the administrator password to a regular role' { $store.Save($collision, $builtIn) }

    foreach ($reservedPermission in @('Profile', 'Services')) {
        $replacement.Permissions = @($reservedPermission, 'Appraisal')
        Assert-RoleRejected "granting $reservedPermission to a regular role" { $store.Save($replacement, $builtIn) }
    }
    $persisted = $store.All() | Where-Object Name -eq 'Former Owner'
    if ([GTX.Common.SiteAccess]::Can($persisted, [GTX.Common.MajordomeItem]::Roles) -or
        [GTX.Common.SiteAccess]::Can($persisted, [GTX.Common.MajordomeItem]::Profile) -or
        [GTX.Common.SiteAccess]::Can($persisted, [GTX.Common.MajordomeItem]::Services)) {
        throw 'A submitted permission array granted a regular role access to reserved pages.'
    }

    $profileEditor = New-Object GTX.Models.SiteRoleModel -Property @{
        Name = 'Company Editor'
        Password = 'Company-Editor-Test48!'
        Permissions = @('Appraisal', $profileKey)
    }
    $store.Save($profileEditor, $builtIn)
    [void]$initialized.Remove($testConnectionString)
    $profileEditor = $store.All() | Where-Object Name -eq 'Company Editor'
    if (![GTX.Common.SiteAccess]::Can($profileEditor, $profilePermission)) { throw 'A Profile grant did not persist for a new custom role.' }
    $profileEditor.Permissions = @('Appraisal')
    $store.Save($profileEditor, $builtIn)
    $profileEditor = $store.All() | Where-Object Name -eq 'Company Editor'
    if ([GTX.Common.SiteAccess]::Can($profileEditor, $profilePermission)) { throw 'Revoked Profile access remained active for the custom role.' }

    $manager = $store.All() | Where-Object Name -eq 'Manager'
    $manager.Permissions = @('Appraisal', 'Roles', $profileKey)
    $store.Save($manager, $builtIn)
    [void]$initialized.Remove($testConnectionString)
    $delegate = $store.All() | Where-Object Name -eq 'Manager'
    if (![GTX.Common.SiteAccess]::Can($delegate, [GTX.Common.MajordomeItem]::Roles) -or
        [GTX.Common.SiteAccess]::IsMajordome($delegate)) {
        throw 'An explicit Roles grant failed to persist or changed the built-in identity boundary.'
    }

    $child = New-Object GTX.Models.SiteRoleModel -Property @{
        Name = 'Delegated Appraiser'
        Password = 'Delegated-Appraiser-Test46!'
        Permissions = @('Appraisal')
    }
    $store.Save($child, $delegate)
    $child = $store.All() | Where-Object Name -eq 'Delegated Appraiser'
    if (!$child -or ($child.Permissions -join ',') -cne 'Appraisal') { throw 'Delegated role creation did not persist.' }

    foreach ($unavailablePermission in @('Roles', 'VinDecoder', $profileKey)) {
        $child.Permissions = @('Appraisal', $unavailablePermission)
        Assert-RoleRejected "delegating $unavailablePermission outside the manager's scope" {
            $store.Save($child, $delegate)
        } -ExpectedType ([UnauthorizedAccessException])
    }
    Assert-RoleRejected 'deleting the acting role' { $store.Delete($delegate.Id, $delegate) } -ExpectedType ([UnauthorizedAccessException])
    Assert-RoleRejected 'deleting a role with wider permissions' { $store.Delete($detailTeam.Id, $delegate) } -ExpectedType ([UnauthorizedAccessException])

    # Re-read the target at write time, including when the editor has an older snapshot.
    $child.Permissions = @('Appraisal')
    $promoted = $store.All() | Where-Object Id -eq $child.Id
    $promoted.Permissions += $profileKey
    $store.Save($promoted, $builtIn)
    Assert-RoleRejected 'editing a role with Profile access' { $store.Save($child, $delegate) } -ExpectedType ([UnauthorizedAccessException])
    Assert-RoleRejected 'deleting a role with Profile access' { $store.Delete($child.Id, $delegate) } -ExpectedType ([UnauthorizedAccessException])
    $promoted.Permissions = @('Appraisal')
    $promoted.Permissions += 'Roles'
    $store.Save($promoted, $builtIn)
    Assert-RoleRejected 'editing a newly promoted role' { $store.Save($child, $delegate) } -ExpectedType ([UnauthorizedAccessException])
    Assert-RoleRejected 'deleting a delegated role manager' { $store.Delete($child.Id, $delegate) } -ExpectedType ([UnauthorizedAccessException])
    $store.Save($child, $builtIn)
    $child.Name = 'Renamed Appraiser'
    $store.Save($child, $delegate)
    if (!($store.All() | Where-Object Name -eq 'Renamed Appraiser')) { throw 'Delegated role editing did not persist.' }
    $store.Delete($child.Id, $delegate)
    if ($store.All() | Where-Object Id -eq $child.Id) { throw 'Delegated role deletion did not persist.' }

    $manager.Permissions = @('Appraisal')
    $store.Save($manager, $builtIn)
    $pendingRole = New-Object GTX.Models.SiteRoleModel -Property @{
        Name = 'Revoked Manager Test'
        Password = 'Revoked-Manager-Test47!'
        Permissions = @('Appraisal')
    }
    Assert-RoleRejected 'writing with a revoked role-manager snapshot' { $store.Save($pendingRole, $delegate) } -ExpectedType ([UnauthorizedAccessException])
    $manager = $store.All() | Where-Object Name -eq 'Manager'
    if ([GTX.Common.SiteAccess]::Can($manager, [GTX.Common.MajordomeItem]::Roles)) { throw 'Revoked Roles access remained active.' }

    Write-Output 'PASS: isolated legacy migrations, editable Owner, reserved identity, and delegated role grants, scope, and revocation.'
}
finally {
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $previousHash, 'Process')
    if ($created) {
        Assert-LocalServer $builder.DataSource
        if ($databaseName -cne $expectedDatabaseName -or $databaseName -notmatch '^GTX_Roles_Test_[a-f0-9]{32}$') {
            throw 'Refusing cleanup because the temporary database identity changed.'
        }
        [System.Data.SqlClient.SqlConnection]::ClearAllPools()
        Invoke-RoleSql $masterConnectionString ("ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]")
    }
}
