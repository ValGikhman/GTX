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

function Assert-RoleRejected([string]$Label, [scriptblock]$Action) {
    try {
        & $Action
        throw "Role store accepted $Label."
    }
    catch {
        $exception = $_.Exception
        while ($exception.InnerException) { $exception = $exception.InnerException }
        if ($exception -isnot [InvalidOperationException]) { throw }
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
    $owner.Permissions = @('Appraisal')
    $store.Save($owner)

    # Simulate a fresh worker process so the persisted migration marker is exercised.
    $initialized = [GTX.SiteRoleStore].GetField('Initialized', [Reflection.BindingFlags]'NonPublic,Static').GetValue($null)
    [void]$initialized.Remove($testConnectionString)
    $afterRestart = @($store.All())
    $owner = $afterRestart | Where-Object Name -eq 'Owner'
    if (($owner.Permissions -join ',') -cne 'Appraisal') { throw 'Migration rerun replaced a saved regular-role permission edit.' }
    if ([GTX.Common.SiteAccess]::Can($owner, [GTX.Common.MajordomeItem]::VinDecoder) -or
        [GTX.Common.SiteAccess]::Can($owner, [GTX.Common.MajordomeItem]::Profile) -or
        [GTX.Common.SiteAccess]::Can($owner, [GTX.Common.MajordomeItem]::Services)) { throw 'Owner retained implicit privileged access.' }

    $store.Delete($owner.Id)
    if (@($store.All() | Where-Object Name -eq 'Owner').Count) { throw 'Owner could not be deleted as an ordinary role.' }
    $replacement = New-Object GTX.Models.SiteRoleModel
    $replacement.Name = 'Owner'
    $replacement.Password = 'Replacement-Owner-Test44!'
    $replacement.Permissions = [GTX.Common.MajordomeMenu]::Defaults('Owner')
    $store.Save($replacement)
    $replacement = $store.All() | Where-Object Name -eq 'Owner'
    $replacement.Name = 'Former Owner'
    $replacement.Password = $null
    $store.Save($replacement)
    if (!@($store.All() | Where-Object Name -eq 'Former Owner').Count) { throw 'Owner could not be renamed as an ordinary role.' }

    foreach ($reservedName in @('Majordome', 'majordome', ' MAJORDOME ')) {
        $reserved = New-Object GTX.Models.SiteRoleModel
        $reserved.Name = $reservedName
        $reserved.Password = 'Reserved-Name-Test45!'
        Assert-RoleRejected 'creation of the reserved Majordome name' { $store.Save($reserved) }
    }
    $builtIn = [GTX.Common.MajordomeAccount]::Current()
    Assert-RoleRejected 'editing the built-in account' { $store.Save($builtIn) }
    Assert-RoleRejected 'deleting the built-in account' { $store.Delete($builtIn.Id) }
    $impostor = New-Object GTX.Models.SiteRoleModel
    $impostor.Id = [int](Invoke-RoleSql $testConnectionString "SELECT Id FROM dbo.SiteRole WHERE Name=N'Majordome'" -Scalar)
    $impostor.Name = 'Pretend Admin'
    Assert-RoleRejected 'editing an existing reserved-name database record' { $store.Save($impostor) }
    Assert-RoleRejected 'deleting an existing reserved-name database record' { $store.Delete($impostor.Id) }

    $collision = New-Object GTX.Models.SiteRoleModel
    $collision.Name = 'Password Collision'
    $collision.Password = $adminPassword
    Assert-RoleRejected 'assigning the administrator password to a regular role' { $store.Save($collision) }

    $replacement.Permissions = @('Services', 'Appraisal')
    Assert-RoleRejected 'granting Services to a regular role' { $store.Save($replacement) }
    $replacement.Permissions = @('Roles', 'Profile', 'Services', 'Appraisal')
    try { $store.Save($replacement) }
    catch {
        $exception = $_.Exception
        while ($exception.InnerException) { $exception = $exception.InnerException }
        if ($exception -isnot [InvalidOperationException]) { throw }
    }
    $persisted = $store.All() | Where-Object Name -eq 'Former Owner'
    if ([GTX.Common.SiteAccess]::Can($persisted, [GTX.Common.MajordomeItem]::Roles) -or
        [GTX.Common.SiteAccess]::Can($persisted, [GTX.Common.MajordomeItem]::Profile) -or
        [GTX.Common.SiteAccess]::Can($persisted, [GTX.Common.MajordomeItem]::Services)) {
        throw 'A submitted permission array granted a regular role access to reserved pages.'
    }
    Write-Output 'PASS: isolated local migration, hidden built-in identity, restart preservation, editable/deletable Owner, reserved-name/identity protection and reserved-page denial.'
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
