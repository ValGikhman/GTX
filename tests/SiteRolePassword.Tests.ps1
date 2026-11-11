$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.ComponentModel.DataAnnotations
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))

$script:caseCount = 0
function Assert-PasswordValidation {
    param(
        [string]$Case,
        [AllowNull()][object]$Password,
        [AllowNull()][object]$Confirmation,
        [bool]$ExpectedValid,
        [int]$RoleId = 123,
        [string]$ExpectedErrorMember = ''
    )

    $role = New-Object GTX.Models.SiteRoleModel
    $role.Id = $RoleId
    $role.Name = 'Test Role'
    # MVC converts empty posted strings to null; preserve that behavior (and avoid
    # PowerShell coercing null into an empty string when assigning a string property).
    if (![string]::IsNullOrEmpty($Password)) { $role.Password = $Password }
    if (![string]::IsNullOrEmpty($Confirmation)) { $role.ConfirmPassword = $Confirmation }
    $results = New-Object 'System.Collections.Generic.List[System.ComponentModel.DataAnnotations.ValidationResult]'
    $context = New-Object System.ComponentModel.DataAnnotations.ValidationContext($role)
    $valid = [System.ComponentModel.DataAnnotations.Validator]::TryValidateObject($role, $context, $results, $true)
    if ($valid -ne $ExpectedValid) {
        throw "Incorrect validation for '$Case': expected valid=$ExpectedValid, got valid=$valid."
    }
    if ($ExpectedErrorMember -and !@($results | Where-Object { @($_.MemberNames) -contains $ExpectedErrorMember }).Count) {
        # Framework CompareAttribute omits MemberNames; validate its owning property separately.
        $propertyResults = New-Object 'System.Collections.Generic.List[System.ComponentModel.DataAnnotations.ValidationResult]'
        $propertyContext = New-Object System.ComponentModel.DataAnnotations.ValidationContext($role)
        $propertyContext.MemberName = $ExpectedErrorMember
        $propertyValid = [System.ComponentModel.DataAnnotations.Validator]::TryValidateProperty($role.$ExpectedErrorMember, $propertyContext, $propertyResults)
        if ($propertyValid -or !$propertyResults.Count) {
            throw "Validation for '$Case' did not identify $ExpectedErrorMember."
        }
    }
    $script:caseCount++
}

$validPassword = 'RolePassword42!'
Assert-PasswordValidation 'Valid mixed password' $validPassword $validPassword $true
Assert-PasswordValidation '8-character minimum' 'Aa7!xxxx' 'Aa7!xxxx' $true
Assert-PasswordValidation 'New role with valid password' $validPassword $validPassword $true -RoleId 0
Assert-PasswordValidation '7 characters' 'Aa7!xxx' 'Aa7!xxx' $false -ExpectedErrorMember Password
Assert-PasswordValidation 'Legacy six-character password cannot be newly assigned' 'Aa7!xy' 'Aa7!xy' $false -ExpectedErrorMember Password
$maximumPassword = 'Aa7!' + ('x' * 28)
Assert-PasswordValidation '32-character maximum' $maximumPassword $maximumPassword $true
$tooLongPassword = $maximumPassword + 'x'
Assert-PasswordValidation '33 characters' $tooLongPassword $tooLongPassword $false -ExpectedErrorMember Password

foreach ($case in @(
    @{ Label = 'Missing uppercase'; Value = 'rolepassword42!' },
    @{ Label = 'Missing lowercase'; Value = 'ROLEPASSWORD42!' },
    @{ Label = 'Missing digit'; Value = 'RolePasswordxx!' },
    @{ Label = 'Missing symbol'; Value = 'RolePassword42x' },
    @{ Label = 'Unicode letter is not an ASCII symbol'; Value = ('RolePassword42' + [char]0x00E9) }
)) {
    Assert-PasswordValidation $case.Label $case.Value $case.Value $false -ExpectedErrorMember Password
}

foreach ($codePoint in @(0x0020, 0x0009, 0x000A, 0x000D, 0x0085, 0x00A0, 0x2003, 0x2028, 0xFEFF)) {
    $withWhitespace = 'Role' + [char]$codePoint + 'Password42!'
    Assert-PasswordValidation ('Internal whitespace U+{0:X4}' -f $codePoint) $withWhitespace $withWhitespace $false -ExpectedErrorMember Password
}
foreach ($withWhitespace in @(' RolePassword42!', 'RolePassword42! ')) {
    Assert-PasswordValidation 'Leading or trailing whitespace' $withWhitespace $withWhitespace $false -ExpectedErrorMember Password
}

foreach ($codePoint in @((33..47) + (58..64) + (91..96) + (123..126))) {
    $withSymbol = 'RolePassword42' + [char]$codePoint
    Assert-PasswordValidation ('ASCII symbol U+{0:X4}' -f $codePoint) $withSymbol $withSymbol $true
}

Assert-PasswordValidation 'Missing confirmation' $validPassword $null $false -ExpectedErrorMember ConfirmPassword
Assert-PasswordValidation 'Mismatched confirmation' $validPassword 'OtherPassword42!' $false -ExpectedErrorMember ConfirmPassword
Assert-PasswordValidation 'Confirmation is case-sensitive' $validPassword 'rolePassword42!' $false -ExpectedErrorMember ConfirmPassword
Assert-PasswordValidation 'Confirmation without password' $null $validPassword $false -ExpectedErrorMember ConfirmPassword
Assert-PasswordValidation 'Null password edit preserves current password' $null $null $true
Assert-PasswordValidation 'Empty password edit preserves current password' '' '' $true
Assert-PasswordValidation 'Null password is invalid for new role' $null $null $false -RoleId 0 -ExpectedErrorMember Password
Assert-PasswordValidation 'Empty password is invalid for new role' '' '' $false -RoleId 0 -ExpectedErrorMember Password

Write-Output "PASS: $script:caseCount password policy cases covering boundaries, character rules, whitespace, confirmation and unchanged edits."
