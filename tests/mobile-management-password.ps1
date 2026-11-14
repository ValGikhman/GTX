# Run after a Debug build with Windows PowerShell. Synthetic credentials and isolated temp files only.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Helpers.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-mobile-password-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
$path = Join-Path $testDirectory 'MobileManagement.config'
$previousAdministratorHash = [Environment]::GetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', 'Process')

function Write-TestCredential([string]$Hash) {
    [IO.File]::WriteAllText($path, '<mobileManagement passwordHash="' + $Hash + '" />')
}

try {
    if ($null -ne [GTX.Common.MobileManagementPassword]::Read($path)) { throw 'A missing private file was accepted.' }
    $firstPassword = 'Isolated-Mobile-Password43!'
    $firstHash = [System.Web.Helpers.Crypto]::HashPassword($firstPassword)
    Write-TestCredential $firstHash
    $loaded = [GTX.Common.MobileManagementPassword]::Read($path)
    if ($loaded -cne $firstHash -or
        ![System.Web.Helpers.Crypto]::VerifyHashedPassword($loaded, $firstPassword) -or
        [System.Web.Helpers.Crypto]::VerifyHashedPassword($loaded, 'Wrong-Mobile-Password44!')) {
        throw 'The private mobile credential did not validate only its own password.'
    }
    $sessions = [GTX.Helpers.MobileSessionStore]::new((Join-Path $testDirectory 'sessions'))
    $token = $sessions.Create($loaded)
    $reopened = [GTX.Helpers.MobileSessionStore]::new((Join-Path $testDirectory 'sessions'))
    if (!$reopened.IsValid($token, [GTX.Common.MobileManagementPassword]::Read($path))) {
        throw 'The mobile session did not survive reopening its storage.'
    }
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', 'unrelated-website-credential-change', 'Process')
    [IO.File]::WriteAllText((Join-Path $testDirectory 'WebsiteRoles.config'), '<roles><role name="RenamedOwner" /></roles>')
    if ([GTX.Common.MobileManagementPassword]::Read($path) -cne $firstHash -or
        !$reopened.IsValid($token, [GTX.Common.MobileManagementPassword]::Read($path))) {
        throw 'Unrelated website credentials changed the mobile password or session.'
    }
    $secondPassword = 'Rotated-Mobile-Password44!'
    $secondHash = [System.Web.Helpers.Crypto]::HashPassword($secondPassword)
    Write-TestCredential $secondHash
    $rotated = [GTX.Common.MobileManagementPassword]::Read($path)
    if ($reopened.IsValid($token, $rotated) -or
        [System.Web.Helpers.Crypto]::VerifyHashedPassword($rotated, $firstPassword) -or
        ![System.Web.Helpers.Crypto]::VerifyHashedPassword($rotated, $secondPassword)) {
        throw 'Rotating the private mobile credential did not revoke the old password and session.'
    }
    $newToken = $sessions.Create($rotated)
    if (!$reopened.IsValid($newToken, $rotated)) { throw 'A session for the rotated credential was rejected.' }
    $sessions.Revoke($newToken)
    if ($reopened.IsValid($newToken, $rotated)) { throw 'A logged-out mobile session was accepted.' }
    $invalidVersion = New-Object byte[] 49
    $invalidVersion[0] = 1
    foreach ($invalid in @('<mobileManagement', ('<roles passwordHash="' + $firstHash + '" />'),
        '<mobileManagement />', '<mobileManagement passwordHash="invalid-base64" />',
        ('<mobileManagement passwordHash="' + [Convert]::ToBase64String($invalidVersion) + '" />'),
        ('<mobileManagement passwordHash="' + [Convert]::ToBase64String((New-Object byte[] 48)) + '" />'),
        '<!DOCTYPE mobileManagement [<!ENTITY x "test">]><mobileManagement passwordHash="&x;" />')) {
        [IO.File]::WriteAllText($path, $invalid)
        if ($null -ne [GTX.Common.MobileManagementPassword]::Read($path)) { throw 'An invalid private credential file was accepted.' }
    }
    Write-TestCredential $firstHash
    $locked = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    try {
        if ($null -ne [GTX.Common.MobileManagementPassword]::Read($path)) { throw 'An unreadable private credential was accepted.' }
    }
    finally { $locked.Dispose() }
    Write-Output 'PASS: private mobile password, website credential independence, durable sessions, rotation and logout revocation, missing/malformed/DTD/unreadable file denial.'
}
finally {
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $previousAdministratorHash, 'Process')
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolvedTestDirectory = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolvedTestDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTestDirectory) -notmatch '^gtx-mobile-password-[a-f0-9]{32}$') {
        throw 'Refusing cleanup because the isolated mobile-password test directory identity changed.'
    }
    Remove-Item -LiteralPath $resolvedTestDirectory -Recurse -Force
}
