# Run after a Debug build with Windows PowerShell (.NET Framework).
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.ComponentModel.DataAnnotations
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Mvc.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))

function New-ValidBusinessHours {
    $hours = New-Object 'System.Collections.Generic.List[GTX.Models.SiteBusinessHoursModel]'
    foreach ($day in 0..6) {
        $row = New-Object GTX.Models.SiteBusinessHoursModel
        $row.DayOfWeek = $day
        $row.IsClosed = $false
        $row.OpenTime = '09:00'
        $row.CloseTime = '17:00'
        [void]$hours.Add($row)
    }
    return ,$hours
}

function New-ValidProfile {
    $profile = New-Object GTX.Models.SiteProfileModel
    $profile.CompanyName = 'Example Auto'
    $profile.Email = 'sales@example.com'
    $profile.Phone = '+1 (513) 555-0123'
    $profile.StreetAddress = '123 Test Street'
    $profile.City = 'Test City'
    $profile.State = 'OH'
    $profile.ZipCode = '01234'
    $profile.ResearchRadiusMiles = 100
    $profile.DocumentaryFee = 398
    $profile.BusinessHours = New-ValidBusinessHours
    return $profile
}

$script:caseCount = 0
function Assert-ProfileValidation {
    param([string]$Case, [hashtable]$Values, [bool]$ExpectedValid, [string]$ExpectedErrorMember = '')
    $profile = New-ValidProfile
    foreach ($key in $Values.Keys) { $profile.$key = $Values[$key] }
    $results = New-Object 'System.Collections.Generic.List[System.ComponentModel.DataAnnotations.ValidationResult]'
    $context = New-Object System.ComponentModel.DataAnnotations.ValidationContext($profile)
    $valid = [System.ComponentModel.DataAnnotations.Validator]::TryValidateObject($profile, $context, $results, $true)
    if ($valid -ne $ExpectedValid) {
        throw "Incorrect validation for '$Case': expected valid=$ExpectedValid, got valid=$valid."
    }
    if ($ExpectedErrorMember -and !@($results | Where-Object { @($_.MemberNames) -contains $ExpectedErrorMember }).Count) {
        throw "Validation for '$Case' did not identify $ExpectedErrorMember."
    }
    $script:caseCount++
}

Assert-ProfileValidation 'Valid profile with optional fields omitted' @{} $true
Assert-ProfileValidation 'Valid full address and map coordinates' @{ AddressLine2 = 'Suite 4'; Latitude = 39.32; Longitude = -84.46 } $true
foreach ($property in @('CompanyName', 'Email', 'Phone', 'StreetAddress', 'City', 'State', 'ZipCode')) {
    Assert-ProfileValidation "Missing $property" @{ $property = '' } $false $property
    Assert-ProfileValidation "Whitespace $property" @{ $property = '   ' } $false $property
}
foreach ($item in @(
    @{ Property = 'CompanyName'; Maximum = 120 },
    @{ Property = 'StreetAddress'; Maximum = 160 },
    @{ Property = 'AddressLine2'; Maximum = 120 },
    @{ Property = 'City'; Maximum = 80 }
)) {
    Assert-ProfileValidation "$($item.Property) maximum length" @{ $item.Property = ('x' * $item.Maximum) } $true
    Assert-ProfileValidation "$($item.Property) too long" @{ $item.Property = ('x' * ($item.Maximum + 1)) } $false $item.Property
}
Assert-ProfileValidation 'Invalid email' @{ Email = 'not-an-email' } $false Email
Assert-ProfileValidation 'Oversized email' @{ Email = (('x' * 250) + '@example.com') } $false Email
Assert-ProfileValidation 'Oversized phone' @{ Phone = ('1' * 31) } $false Phone
foreach ($phone in @('(513) 555-0123', '5135550123', '+15135550123', '1 (513) 555-0123', '513.555.0123', '513 555 0123')) {
    Assert-ProfileValidation 'Supported US primary phone format' @{ Phone = $phone } $true
    $phoneProfile = New-ValidProfile
    $phoneProfile.Phone = $phone
    if ($phoneProfile.PhoneDigits -ne '15135550123' -or $phoneProfile.PhoneUri -ne 'tel:+15135550123') { throw 'A valid primary phone format did not normalize to the same dialable US number.' }
}
foreach ($phone in @('123', '(513) 555-0123 x123', '(513) 555-0123 ext. 45', '151355501234', '25135550123')) {
    Assert-ProfileValidation 'Invalid or extension-bearing primary phone' @{ Phone = $phone } $false Phone
}
Assert-ProfileValidation 'Short state' @{ State = 'O' } $false State
Assert-ProfileValidation 'Long state' @{ State = 'Ohio' } $false State
Assert-ProfileValidation 'Numeric state' @{ State = '12' } $false State
Assert-ProfileValidation 'Short ZIP' @{ ZipCode = '1234' } $false ZipCode
Assert-ProfileValidation 'Long ZIP' @{ ZipCode = '123456' } $false ZipCode
Assert-ProfileValidation 'ZIP plus four is not a five-digit default' @{ ZipCode = '12345-6789' } $false ZipCode
Assert-ProfileValidation 'Non-numeric ZIP' @{ ZipCode = '12A45' } $false ZipCode
Assert-ProfileValidation 'Minimum radius' @{ ResearchRadiusMiles = 1 } $true
Assert-ProfileValidation 'Maximum radius' @{ ResearchRadiusMiles = 500 } $true
Assert-ProfileValidation 'Zero radius' @{ ResearchRadiusMiles = 0 } $false ResearchRadiusMiles
Assert-ProfileValidation 'Negative radius' @{ ResearchRadiusMiles = -1 } $false ResearchRadiusMiles
Assert-ProfileValidation 'Radius over maximum' @{ ResearchRadiusMiles = 501 } $false ResearchRadiusMiles
Assert-ProfileValidation 'Minimum coordinates' @{ Latitude = -90; Longitude = -180 } $true
Assert-ProfileValidation 'Maximum coordinates' @{ Latitude = 90; Longitude = 180 } $true
Assert-ProfileValidation 'Latitude too low' @{ Latitude = -90.01; Longitude = 0 } $false Latitude
Assert-ProfileValidation 'Latitude too high' @{ Latitude = 90.01; Longitude = 0 } $false Latitude
Assert-ProfileValidation 'Longitude too low' @{ Latitude = 0; Longitude = -180.01 } $false Longitude
Assert-ProfileValidation 'Longitude too high' @{ Latitude = 0; Longitude = 180.01 } $false Longitude
Assert-ProfileValidation 'Latitude without longitude' @{ Latitude = 39.32 } $false
Assert-ProfileValidation 'Longitude without latitude' @{ Longitude = -84.46 } $false
Assert-ProfileValidation 'No documentary fee' @{ DocumentaryFee = 0 } $true
Assert-ProfileValidation 'Maximum documentary fee' @{ DocumentaryFee = 10000 } $true
Assert-ProfileValidation 'Negative documentary fee' @{ DocumentaryFee = -1 } $false DocumentaryFee
Assert-ProfileValidation 'Documentary fee above maximum' @{ DocumentaryFee = 10001 } $false DocumentaryFee
Assert-ProfileValidation 'Missing weekly schedule' @{ BusinessHours = $null } $false BusinessHours
$hours = New-ValidBusinessHours
$hours.RemoveAt(6)
Assert-ProfileValidation 'Missing one day' @{ BusinessHours = $hours } $false
$hours = New-ValidBusinessHours
$hours[6].DayOfWeek = 5
Assert-ProfileValidation 'Duplicate day' @{ BusinessHours = $hours } $false
$hours = New-ValidBusinessHours
$hours[0].OpenTime = '25:00'
Assert-ProfileValidation 'Invalid nested opening time' @{ BusinessHours = $hours } $false
$hours = New-ValidBusinessHours
$hours[0].IsClosed = $true
$hours[0].OpenTime = $null
$hours[0].CloseTime = $null
Assert-ProfileValidation 'Sunday closed without times' @{ BusinessHours = $hours } $true

function Assert-HoursValidation {
    param([string]$Case, [hashtable]$Values, [bool]$ExpectedValid)
    $row = (New-ValidBusinessHours)[0]
    foreach ($key in $Values.Keys) { $row.$key = $Values[$key] }
    $results = New-Object 'System.Collections.Generic.List[System.ComponentModel.DataAnnotations.ValidationResult]'
    $context = New-Object System.ComponentModel.DataAnnotations.ValidationContext($row)
    $valid = [System.ComponentModel.DataAnnotations.Validator]::TryValidateObject($row, $context, $results, $true)
    if ($valid -ne $ExpectedValid) { throw "Incorrect business hours validation for '$Case': expected valid=$ExpectedValid, got valid=$valid." }
    $script:caseCount++
}
Assert-HoursValidation 'Open day with valid times' @{} $true
Assert-HoursValidation 'Closed day without times' @{ IsClosed = $true; OpenTime = ''; CloseTime = '' } $true
Assert-HoursValidation 'Day below range' @{ DayOfWeek = -1 } $false
Assert-HoursValidation 'Day above range' @{ DayOfWeek = 7 } $false
Assert-HoursValidation 'Opening time required on open day' @{ OpenTime = '' } $false
Assert-HoursValidation 'Closing time required on open day' @{ CloseTime = '' } $false
Assert-HoursValidation 'Opening time requires two-digit hour' @{ OpenTime = '9:00' } $false
Assert-HoursValidation 'Invalid 24-hour time' @{ OpenTime = '24:00' } $false
Assert-HoursValidation 'Invalid minute' @{ CloseTime = '17:60' } $false
Assert-HoursValidation 'Opening and closing times cannot match' @{ CloseTime = '09:00' } $false
Assert-HoursValidation 'Closing time must follow opening time' @{ CloseTime = '08:59' } $false
Assert-HoursValidation 'Overnight hours are not supported' @{ OpenTime = '22:00'; CloseTime = '02:00' } $false

$hours = New-ValidBusinessHours
if ($hours[0].Day -ne 'Sunday' -or $hours[6].Day -ne 'Saturday') { throw 'Business day names do not follow Sunday=0 through Saturday=6.' }
$description = $hours[0].Description.Replace(' ', '')
if (!$description.Contains('9:00AM') -or !$description.Contains('5:00PM')) { throw 'Business hours description did not preserve formatted opening and closing times.' }
$hours[0].IsClosed = $true
if ($hours[0].Description -ne 'Closed') { throw 'Closed day description must say Closed.' }

$profile = New-ValidProfile
$profile.BusinessHours[0].OpenTime = '09:30'
$profile.BusinessHours[0].CloseTime = '17:15'
$openHours = @($profile.GetOpenHours())
if ($openHours.Count -ne 7) { throw 'Legacy hours projection must retain every weekday.' }
$sunday = @($openHours | Where-Object { $_.Day -eq 'Sunday' })[0]
if ($sunday.OpenTime -ne [TimeSpan]::FromMinutes(570) -or $sunday.CloseTime -ne [TimeSpan]::FromMinutes(1035)) { throw 'Legacy projection lost opening or closing minutes.' }
if ($sunday.From -ne 9 -or $sunday.To -ne 17) { throw 'Legacy projection did not retain hour fields.' }
foreach ($check in @(
    @{ At = [datetime]::new(2026, 11, 8, 9, 29, 0); Expected = $false },
    @{ At = [datetime]::new(2026, 11, 8, 9, 30, 0); Expected = $true },
    @{ At = [datetime]::new(2026, 11, 8, 17, 14, 0); Expected = $true },
    @{ At = [datetime]::new(2026, 11, 8, 17, 15, 0); Expected = $false }
)) {
    if ($profile.IsOpenAt($check.At) -ne $check.Expected) { throw 'Profile open status must include opening minute and exclude closing minute.' }
    if ($sunday.IsOpenAt($check.At) -ne $check.Expected) { throw 'Legacy open status must include opening minute and exclude closing minute.' }
}
$profile.BusinessHours[0].IsClosed = $true
if ($profile.IsOpenAt([datetime]::new(2026, 11, 8, 12, 0, 0))) { throw 'Profile open status ignored a closed day.' }
$closedSunday = @($profile.GetOpenHours() | Where-Object { $_.Day -eq 'Sunday' })[0]
if ($null -ne $closedSunday.OpenTime -or $null -ne $closedSunday.CloseTime -or $closedSunday.IsOpenAt([datetime]::new(2026, 11, 8, 12, 0, 0))) { throw 'Closed legacy day must have no opening times or open status.' }

$profile = New-ValidProfile
foreach ($part in @($profile.StreetAddress, $profile.City, $profile.State, $profile.ZipCode)) {
    if (!$profile.FullAddress.Contains($part)) { throw 'FullAddress omitted a populated address component.' }
}
$profile.AddressLine2 = 'Suite 4'
if (!$profile.FullAddress.Contains('Suite 4')) { throw 'FullAddress omitted AddressLine2.' }
if ($profile.PhoneDigits -ne '15135550123') { throw 'PhoneDigits did not normalize phone punctuation.' }
if ($profile.PhoneUri -ne 'tel:+15135550123') { throw 'PhoneUri did not produce a dialable telephone link.' }

$controller = $assembly.GetType('GTX.Controllers.ProfileController', $true)
if (!$controller.GetCustomAttributes([GTX.Common.RequireAdminRole], $true).Length) { throw 'Profile controller lacks Majordome role authorization.' }
$methods = @($controller.GetMethods() | Where-Object { $_.Name -eq 'Index' -and $_.DeclaringType -eq $controller })
$get = @($methods | Where-Object { $_.GetParameters().Length -eq 0 })
$post = @($methods | Where-Object { $_.GetParameters().Length -eq 1 })
if ($get.Count -ne 1 -or $post.Count -ne 1) { throw 'Profile must expose its GET editor and POST save action.' }
if (!$get[0].GetCustomAttributes([System.Web.Mvc.HttpGetAttribute], $true).Length) { throw 'Profile editor must be GET-only.' }
if (!$post[0].GetCustomAttributes([System.Web.Mvc.HttpPostAttribute], $true).Length) { throw 'Profile save must be POST-only.' }
if (!$post[0].GetCustomAttributes([System.Web.Mvc.ValidateAntiForgeryTokenAttribute], $true).Length) { throw 'Profile save lacks anti-forgery validation.' }
$bind = @($post[0].GetParameters()[0].GetCustomAttributes([System.Web.Mvc.BindAttribute], $true))
if ($bind.Count -ne 1) { throw 'Profile save must explicitly bind editable fields.' }
$boundFields = $bind[0].Include.Split(',') | ForEach-Object { $_.Trim() }
foreach ($property in @('CompanyName', 'Email', 'Phone', 'StreetAddress', 'AddressLine2', 'City', 'State', 'ZipCode', 'ResearchRadiusMiles', 'DocumentaryFee', 'BusinessHours')) {
    if ($boundFields -notcontains $property) { throw "Profile save does not bind $property." }
}
if (@($boundFields | Where-Object { $_ -in @('Id', 'UpdatedUtc', 'FullAddress', 'PhoneUri', 'PhoneDigits', 'Latitude', 'Longitude') }).Count) { throw 'Profile save binds noneditable fields, including read-only map coordinates.' }

[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/Newtonsoft.Json.dll'))
$researchType = $assembly.GetType('GTX.Common.ChatAuctionAppraisal', $true)
$applyDefaults = $researchType.GetMethod('ApplyProfileDefaults', [Reflection.BindingFlags]'NonPublic,Static')
$profile = New-ValidProfile
foreach ($json in @('{}', '{"zip":null,"radius":null}', '{"zip":"   "}')) {
    $inputValues = [Newtonsoft.Json.Linq.JObject]::Parse($json)
    $arguments = New-Object object[] 2
    $arguments[0] = $inputValues
    $arguments[1] = $profile.PSObject.BaseObject
    [void]$applyDefaults.Invoke($null, $arguments)
    if ([string]$inputValues['zip'] -ne $profile.ZipCode -or [int]$inputValues['radius'] -ne $profile.ResearchRadiusMiles) {
        throw 'Omitted research location did not use the company profile.'
    }
}
$inputValues = [Newtonsoft.Json.Linq.JObject]::Parse('{"zip":"90210","radius":25}')
$arguments[0] = $inputValues
[void]$applyDefaults.Invoke($null, $arguments)
if ([string]$inputValues['zip'] -ne '90210' -or [int]$inputValues['radius'] -ne 25) { throw 'Explicit research location was overwritten.' }

Add-Type -AssemblyName System.Web
$previousContext = [System.Web.HttpContext]::Current
$responseWriter = New-Object System.IO.StringWriter
try {
    $request = [System.Web.HttpRequest]::new('', 'http://localhost/', '')
    $response = [System.Web.HttpResponse]::new($responseWriter)
    $context = [System.Web.HttpContext]::new($request, $response)
    [System.Web.HttpContext]::Current = $context
    $profile = New-ValidProfile
    $profile.CompanyName = 'Example {Group} $1 $$ [[Phone]]'
    $context.Items['GTX.SiteProfile'] = $profile.PSObject.BaseObject
    $i18n = $assembly.GetType('GTX.Helpers.I18n', $true)
    $resolveTokens = $i18n.GetMethod('ResolveProfileTokens', [Reflection.BindingFlags]'NonPublic,Static')
    $template = '[[CompanyName]] | [[FullAddress]] | [[StreetAddress]] | [[City]] | [[State]] | [[Phone]] | [[Email]]'
    $expected = @($profile.CompanyName, $profile.FullAddress, $profile.StreetAddress, $profile.City, $profile.State, $profile.Phone, $profile.Email) -join ' | '
    $resolved = $resolveTokens.Invoke($null, @($template))
    if ($resolved -cne $expected) { throw 'Branding tokens must use one request profile snapshot and preserve literal braces, dollars and embedded token text.' }
    $literal = 'Literal {0} $1 [[UnknownToken]]'
    if ($resolveTokens.Invoke($null, @($literal)) -cne $literal) { throw 'Unrecognized branding token text must remain unchanged.' }
}
finally {
    [System.Web.HttpContext]::Current = $previousContext
    $responseWriter.Dispose()
}

Write-Output "PASS: $script:caseCount profile validation cases, business hours and fee validation, address/phone formatting, role authorization, protected saving, research defaults and literal-safe branding tokens."
