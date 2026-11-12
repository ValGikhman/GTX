param(
    [Security.SecureString]$Password,
    [string]$Path = (Join-Path (Split-Path $PSScriptRoot -Parent) 'App_Data/Majordome.config')
)

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/System.Web.Helpers.dll')

function ConvertFrom-PrivatePassword([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

$plain = $null
$confirmation = $null
$temporaryPath = $null
try {
    if ($null -eq $Password) {
        $Password = Read-Host 'New administrator password' -AsSecureString
        $confirmation = ConvertFrom-PrivatePassword (Read-Host 'Confirm password' -AsSecureString)
    }
    $plain = ConvertFrom-PrivatePassword $Password
    if ($null -ne $confirmation -and $plain -cne $confirmation) { throw 'Passwords do not match.' }
    if ($plain.Length -lt 8 -or $plain.Length -gt 32 -or
        $plain -cnotmatch '[A-Z]' -or $plain -cnotmatch '[a-z]' -or
        $plain -notmatch '[0-9]' -or $plain -notmatch '[!-/:-@\[-`{-~]' -or
        $plain -match '[\s\u0085\uFEFF]') {
        throw 'Use 8-32 characters with uppercase, lowercase, a number and a symbol, with no whitespace.'
    }
    $hash = [System.Web.Helpers.Crypto]::HashPassword($plain)
    $targetPath = [IO.Path]::GetFullPath($Path)
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($targetPath))
    $temporaryPath = $targetPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $document = New-Object Xml.XmlDocument
    $element = $document.CreateElement('majordome')
    $element.SetAttribute('passwordHash', $hash)
    [void]$document.AppendChild($element)
    $document.Save($temporaryPath)
    if ([IO.File]::Exists($targetPath)) { [IO.File]::Replace($temporaryPath, $targetPath, [NullString]::Value) }
    else { [IO.File]::Move($temporaryPath, $targetPath) }
    Write-Output "Administrator password hash saved to $targetPath."
    if ($null -ne [Environment]::GetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH')) {
        Write-Output 'The environment password hash overrides this file; update or remove that override to use this file.'
    }
}
finally {
    $plain = $null
    $confirmation = $null
    if ($temporaryPath -and [IO.File]::Exists($temporaryPath)) { [IO.File]::Delete($temporaryPath) }
}
