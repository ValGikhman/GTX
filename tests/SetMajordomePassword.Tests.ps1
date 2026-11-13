# Synthetic password rotation only; never opens the real private administrator file.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$passwordScript = Join-Path $repoPath 'Scripts/Set-MajordomePassword.ps1'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-password-script-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
Add-Type -Path (Join-Path $repoPath 'bin/System.Web.Helpers.dll')

function Invoke-TestPassword([string]$Value, [string]$Path) {
    $secure = ConvertTo-SecureString $Value -AsPlainText -Force
    return @(& $passwordScript -Password $secure -Path $Path) -join "`n"
}
function Read-TestHash([string]$Path) {
    $document = [xml]([IO.File]::ReadAllText($Path))
    if ($document.DocumentElement.Name -cne 'majordome') { throw 'Password script produced an invalid private file.' }
    return $document.DocumentElement.GetAttribute('passwordHash')
}

try {
    $path = Join-Path $testDirectory 'App_Data/Majordome.config'
    $firstPassword = 'Synthetic-Initial43!'
    $secondPassword = 'Synthetic-Rotated44!'
    $firstOutput = Invoke-TestPassword $firstPassword $path
    $firstHash = Read-TestHash $path
    if (![System.Web.Helpers.Crypto]::VerifyHashedPassword($firstHash, $firstPassword)) { throw 'The initial private hash does not match the synthetic password.' }

    # The second invocation exercises Windows PowerShell's null-string interop with File.Replace.
    $secondOutput = Invoke-TestPassword $secondPassword $path
    $secondHash = Read-TestHash $path
    if ($secondHash -ceq $firstHash -or
        ![System.Web.Helpers.Crypto]::VerifyHashedPassword($secondHash, $secondPassword) -or
        [System.Web.Helpers.Crypto]::VerifyHashedPassword($secondHash, $firstPassword)) {
        throw 'Replacing an existing private file did not rotate the administrator credential.'
    }
    $thirdOutput = Invoke-TestPassword $secondPassword $path
    $thirdHash = Read-TestHash $path
    if ($thirdHash -ceq $secondHash -or ![System.Web.Helpers.Crypto]::VerifyHashedPassword($thirdHash, $secondPassword)) {
        throw 'A repeated password-file replacement failed or did not generate a fresh salt.'
    }

    $contents = [IO.File]::ReadAllText($path)
    $output = $firstOutput + $secondOutput + $thirdOutput
    foreach ($privateValue in @($firstPassword, $secondPassword)) {
        if ($contents.Contains($privateValue) -or $output.Contains($privateValue)) { throw 'The password script wrote or printed a plaintext password.' }
    }
    foreach ($privateHash in @($firstHash, $secondHash, $thirdHash)) {
        if ($output.Contains($privateHash)) { throw 'The password script printed its private hash.' }
    }
    $unchanged = [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
    foreach ($invalid in @('short', 'MissingNumber!', 'NoSymbolPassword43', 'Invalid Password43!')) {
        $rejected = $false
        try { $null = Invoke-TestPassword $invalid $path }
        catch {
            if ($_.Exception.Message -notlike '*Use 8-32 characters*') { throw }
            $rejected = $true
        }
        if (!$rejected -or [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -cne $unchanged) {
            throw 'An invalid password was accepted or changed the existing private file.'
        }
    }
    if (@(Get-ChildItem -LiteralPath $testDirectory -Filter 'Majordome.config.*.tmp' -Recurse -File).Count) { throw 'Password rotation left a temporary credential file behind.' }
    Write-Output 'PASS: isolated initial hash creation, repeated Windows PowerShell password rotation, old-password rejection, fresh salts, no private output and invalid-policy preservation.'
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolvedTestDirectory = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolvedTestDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTestDirectory) -notmatch '^gtx-password-script-[a-f0-9]{32}$') {
        throw 'Refusing cleanup because the isolated password-test directory identity changed.'
    }
    Remove-Item -LiteralPath $resolvedTestDirectory -Recurse -Force
}
