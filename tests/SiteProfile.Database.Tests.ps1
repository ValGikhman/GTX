# Run after a Debug build with Windows PowerShell (.NET Framework).
# Creates and removes its own temporary database on a local SQL Server only.
param([string]$Server = 'localhost\MSSQLSERVER01')

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.ComponentModel.DataAnnotations
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))

function Assert-LocalServer([string]$DataSource) {
    $machineName = [Regex]::Escape($env:COMPUTERNAME)
    $localServer = '^(?:\.|localhost|127\.0\.0\.1|' + $machineName + ')(?:\\[A-Za-z0-9_.$-]+)?$'
    if ($DataSource -notmatch $localServer -and $DataSource -notmatch '^\(localdb\)\\[A-Za-z0-9_.$-]+$') {
        throw 'Database regression tests only allow a local SQL Server.'
    }
}

function Invoke-ProfileSql([string]$ConnectionString, [string]$Sql, [switch]$Scalar) {
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        try {
            $command.CommandText = $Sql
            $command.CommandTimeout = 30
            if ($Scalar) { return $command.ExecuteScalar() }
            [void]$command.ExecuteNonQuery()
        }
        finally { $command.Dispose() }
    }
    finally { $connection.Dispose() }
}

function Assert-ConstraintRejected([string]$Label, [string]$Sql) {
    try {
        Invoke-ProfileSql $script:testConnectionString $Sql
        throw "Database accepted invalid $Label."
    }
    catch {
        $exception = $_.Exception
        while ($exception.InnerException) { $exception = $exception.InnerException }
        if ($exception -isnot [System.Data.SqlClient.SqlException] -or $exception.Number -ne 547) { throw }
    }
}

function Assert-ModelRejected([string]$Label, [scriptblock]$Change) {
    $profile = $script:store.Get()
    & $Change $profile
    try {
        $script:store.Save($profile)
        throw "Profile store accepted invalid $Label."
    }
    catch {
        $exception = $_.Exception
        while ($exception.InnerException) { $exception = $exception.InnerException }
        if ($exception -isnot [System.ComponentModel.DataAnnotations.ValidationException]) { throw }
    }
}

Assert-LocalServer $Server
$databaseName = 'GTX_Profile_Test_' + [Guid]::NewGuid().ToString('N')
$expectedDatabaseName = $databaseName
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder.set_DataSource($Server)
$builder.set_IntegratedSecurity($true)
$builder.set_InitialCatalog('master')
$builder.set_ConnectTimeout(10)
$builder.set_ApplicationName('GTX SiteProfile Database Regression')
$masterConnectionString = $builder.ConnectionString
$created = $false

try {
    Invoke-ProfileSql $masterConnectionString ("CREATE DATABASE [$databaseName]")
    $created = $true
    $builder.set_InitialCatalog($databaseName)
    $script:testConnectionString = $builder.ConnectionString
    $migration = [IO.File]::ReadAllText((Join-Path $repoPath 'Scripts/Sql/CreateSiteProfile.sql'))
    Invoke-ProfileSql $script:testConnectionString $migration

    # The web-host constructor reads app settings. Inject only this test connection
    # so the regression cannot select a configured development or production database.
    $script:store = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject([GTX.SiteProfileStore])
    $script:store.connectionString = $script:testConnectionString
    $profile = $script:store.Get()
    if ($profile.CompanyName -ne 'GTX Autogroup' -or $profile.Email -ne 'gtxagroup@gmail.com' -or
        $profile.ZipCode -ne '45011' -or $profile.ResearchRadiusMiles -ne 100 -or $profile.DocumentaryFee -ne 398 -or
        $profile.BusinessHours.Count -ne 7) {
        throw 'The migration did not seed the intended company and research defaults.'
    }
    if ($null -eq $profile.Latitude -or $null -eq $profile.Longitude) { throw 'Expected seeded map coordinates.' }
    $seededLatitude = $profile.Latitude
    $seededLongitude = $profile.Longitude

    $profile.CompanyName = '  Profile Roundtrip Company  '
    $profile.Email = 'profile-test@example.com'
    $profile.Phone = '(212) 555-0100'
    $profile.StreetAddress = '  10 Test Street  '
    $profile.AddressLine2 = '  Suite 2  '
    $profile.City = '  Test City  '
    $profile.State = 'ny'
    $profile.ZipCode = '00501'
    $profile.ResearchRadiusMiles = 500
    $profile.DocumentaryFee = 499
    $monday = $profile.BusinessHours | Where-Object { $_.DayOfWeek -eq 1 }
    $monday.IsClosed = $false
    $monday.OpenTime = '08:30'
    $monday.CloseTime = '17:45'
    $sunday = $profile.BusinessHours | Where-Object { $_.DayOfWeek -eq 0 }
    $sunday.IsClosed = $true
    $sunday.OpenTime = $null
    $sunday.CloseTime = $null
    $profile.Latitude = [decimal]40.8154
    $profile.Longitude = [decimal]-73.0451
    $script:store.Save($profile)
    $saved = $script:store.Get()
    if ($saved.CompanyName -ne 'Profile Roundtrip Company' -or $saved.StreetAddress -ne '10 Test Street' -or
        $saved.AddressLine2 -ne 'Suite 2' -or $saved.City -ne 'Test City' -or $saved.State -ne 'NY' -or
        $saved.ZipCode -ne '00501' -or $saved.ResearchRadiusMiles -ne 500 -or $saved.DocumentaryFee -ne 499 -or
        $saved.Latitude -ne $seededLatitude -or $saved.Longitude -ne $seededLongitude) {
        throw 'Company profile save/read roundtrip did not preserve normalized values, leading ZIP zeroes, radius or read-only coordinates.'
    }
    $savedMonday = $saved.BusinessHours | Where-Object { $_.DayOfWeek -eq 1 }
    $savedSunday = $saved.BusinessHours | Where-Object { $_.DayOfWeek -eq 0 }
    if ($savedMonday.IsClosed -or $savedMonday.OpenTime -ne '08:30' -or $savedMonday.CloseTime -ne '17:45' -or
        !$savedSunday.IsClosed -or $null -ne $savedSunday.OpenTime -or $null -ne $savedSunday.CloseTime) {
        throw 'Business hours did not preserve minute precision or closed-day NULL times.'
    }

    # A second production-script execution must not replace edited values.
    Invoke-ProfileSql $script:testConnectionString $migration
    $preserved = $script:store.Get()
    if ($preserved.CompanyName -ne $saved.CompanyName -or $preserved.ZipCode -ne '00501' -or
        $preserved.Email -ne 'profile-test@example.com' -or $preserved.ResearchRadiusMiles -ne 500 -or
        $preserved.DocumentaryFee -ne 499 -or $preserved.Latitude -ne $seededLatitude -or $preserved.Longitude -ne $seededLongitude -or
        ($preserved.BusinessHours | Where-Object { $_.DayOfWeek -eq 1 }).OpenTime -ne '08:30') {
        throw 'Rerunning the migration replaced existing company settings.'
    }
    if ((Invoke-ProfileSql $script:testConnectionString 'SELECT COUNT(*) FROM dbo.SiteProfile' -Scalar) -ne 1) {
        throw 'Migration must retain exactly one company profile.'
    }

    # Blank optional address values become NULL; omitted coordinates remain unchanged.
    $saved.AddressLine2 = '  '
    $saved.Latitude = $null
    $saved.Longitude = $null
    $saved.ResearchRadiusMiles = 1
    $script:store.Save($saved)
    $optional = $script:store.Get()
    if ($null -ne $optional.AddressLine2 -or $optional.Latitude -ne $seededLatitude -or $optional.Longitude -ne $seededLongitude -or
        $optional.ResearchRadiusMiles -ne 1) { throw 'Blank optional address or minimum radius did not roundtrip, or omitted map coordinates were overwritten.' }

    Assert-ModelRejected 'ZIP' { param($p) $p.ZipCode = '4501x' }
    Assert-ModelRejected 'radius' { param($p) $p.ResearchRadiusMiles = 501 }
    Assert-ModelRejected 'email' { param($p) $p.Email = 'invalid' }
    Assert-ModelRejected 'company name' { param($p) $p.CompanyName = ' ' }
    Assert-ModelRejected 'documentary fee' { param($p) $p.DocumentaryFee = -1 }
    Assert-ModelRejected 'missing business hours' { param($p) $p.BusinessHours.Clear() }
    Assert-ModelRejected 'closing before opening' { param($p) $p.BusinessHours[0].IsClosed = $false; $p.BusinessHours[0].OpenTime = '18:00'; $p.BusinessHours[0].CloseTime = '09:00' }
    Assert-ModelRejected 'partial map coordinates' { param($p) $p.Latitude = [decimal]40; $p.Longitude = $null }
    Assert-ModelRejected 'coordinate range' { param($p) $p.Latitude = [decimal]91; $p.Longitude = [decimal]20 }
    Assert-ConstraintRejected 'singleton ID' 'UPDATE dbo.SiteProfile SET Id=2 WHERE Id=1'
    Assert-ConstraintRejected 'ZIP' "UPDATE dbo.SiteProfile SET ZipCode='12x45' WHERE Id=1"
    Assert-ConstraintRejected 'minimum radius' 'UPDATE dbo.SiteProfile SET ResearchRadiusMiles=0 WHERE Id=1'
    Assert-ConstraintRejected 'maximum radius' 'UPDATE dbo.SiteProfile SET ResearchRadiusMiles=501 WHERE Id=1'
    Assert-ConstraintRejected 'documentary fee' 'UPDATE dbo.SiteProfile SET DocumentaryFee=-1 WHERE Id=1'
    Assert-ConstraintRejected 'partial coordinates' 'UPDATE dbo.SiteProfile SET Latitude=40, Longitude=NULL WHERE Id=1'
    Assert-ConstraintRejected 'coordinate range' 'UPDATE dbo.SiteProfile SET Latitude=91, Longitude=20 WHERE Id=1'
    Assert-ConstraintRejected 'business-hours day' 'UPDATE dbo.SiteProfileBusinessHours SET DayOfWeek=7 WHERE ProfileId=1 AND DayOfWeek=6'
    Assert-ConstraintRejected 'business-hours times' "UPDATE dbo.SiteProfileBusinessHours SET IsClosed=0, OpenTime='18:00', CloseTime='09:00' WHERE ProfileId=1 AND DayOfWeek=1"

    # A missing hours row must roll back the company update in the same transaction.
    $beforeFailure = $script:store.Get()
    $beforeFailure.CompanyName = 'This company update must roll back'
    Invoke-ProfileSql $script:testConnectionString 'DELETE dbo.SiteProfileBusinessHours WHERE ProfileId=1 AND DayOfWeek=6'
    try {
        $script:store.Save($beforeFailure)
        throw 'Profile store accepted an incomplete hours table.'
    }
    catch {
        $exception = $_.Exception
        while ($exception.InnerException) { $exception = $exception.InnerException }
        if ($exception -isnot [System.InvalidOperationException]) { throw }
    }
    if ((Invoke-ProfileSql $script:testConnectionString 'SELECT CompanyName FROM dbo.SiteProfile WHERE Id=1' -Scalar) -ne $optional.CompanyName) {
        throw 'Company profile update was not rolled back after a business-hours failure.'
    }

    Write-Output 'PASS: local isolated database migration, rerun preservation, profile/hours/fee roundtrip, read-only coordinate preservation, atomic rollback, leading-zero ZIP, optional values, model validation and data constraints.'
}
finally {
    if ($created) {
        # Never clean up any database other than the exact unique one created above.
        Assert-LocalServer $builder.DataSource
        if ($databaseName -cne $expectedDatabaseName -or $databaseName -notmatch '^GTX_Profile_Test_[a-f0-9]{32}$') {
            throw 'Refusing cleanup because the temporary database identity changed.'
        }
        [System.Data.SqlClient.SqlConnection]::ClearAllPools()
        Invoke-ProfileSql $masterConnectionString ("ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]")
    }
}
