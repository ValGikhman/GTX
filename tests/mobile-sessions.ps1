$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../Utility/MobileSessionStore.cs')
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-mobile-sessions-' + [guid]::NewGuid().ToString('N'))
$store = New-Object GTX.Helpers.MobileSessionStore($testDirectory)
$token = $store.Create('test-owner-password')
$otherDevice = $store.Create('test-owner-password')
$reopened = New-Object GTX.Helpers.MobileSessionStore($testDirectory)
if (!$reopened.IsValid($token, 'test-owner-password')) { throw 'Session did not survive reopening storage' }
if ($reopened.IsValid($token, 'changed-password')) { throw 'Password change did not invalidate session' }
if ($reopened.IsValid($token, '')) { throw 'Blank Owner password accepted' }
if ($reopened.IsValid('../invalid', 'test-owner-password')) { throw 'Malformed token accepted' }
if ($reopened.IsValid([Convert]::ToBase64String((New-Object byte[] 32)), 'test-owner-password')) { throw 'Unknown token accepted' }
$reopened.Revoke($token)
if ($store.IsValid($token, 'test-owner-password')) { throw 'Revoked session accepted' }
if (!$store.IsValid($otherDevice, 'test-owner-password')) { throw 'Other device was signed out' }
$reopened.Revoke($otherDevice)
Remove-Item -LiteralPath $testDirectory
Write-Output 'PASS: durable sessions, password changes, invalid tokens, logout, device isolation'
