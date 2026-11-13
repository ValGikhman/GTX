# Run after a Debug build with Windows PowerShell (.NET Framework). No database or network calls.
# Temporary credentials exist only in this process and are restored even on failure.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Web
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Helpers.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
Add-Type -ReferencedAssemblies System.Web -TypeDefinition @'
using System.Web;
using System.Collections.Generic;
public sealed class MajordomeTestRequest : HttpRequestBase {
    private readonly HttpCookieCollection cookies = new HttpCookieCollection();
    public MajordomeTestRequest(string value) {
        if (value != null) cookies.Add(new HttpCookie("gtx_access", value));
    }
    public override HttpCookieCollection Cookies { get { return cookies; } }
    public override bool IsSecureConnection { get { return true; } }
}
public sealed class MajordomeTestSession : HttpSessionStateBase {
    private readonly Dictionary<string, object> values = new Dictionary<string, object>();
    public override object this[string name] {
        get { object value; return values.TryGetValue(name, out value) ? value : null; }
        set { values[name] = value; }
    }
}
'@

function New-AccessRequest([string]$Ticket) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Ticket)
    $protected = [System.Web.Security.MachineKey]::Protect($bytes, [string[]]@('GTX.SiteRole'))
    return [MajordomeTestRequest]::new([Convert]::ToBase64String($protected))
}
function New-AccessContext {
    return [System.Web.HttpContext]::new(
        [System.Web.HttpRequest]::new('', 'https://roles.example.test/', ''),
        [System.Web.HttpResponse]::new([IO.StringWriter]::new()))
}

$previousHash = [Environment]::GetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', 'Process')
$previousContext = [System.Web.HttpContext]::Current
try {
    [System.Web.HttpContext]::Current = $null
    $password = 'Isolated-Majordome-Test42!'
    $firstHash = [System.Web.Helpers.Crypto]::HashPassword($password)
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $firstHash, 'Process')
    $account = [GTX.Common.MajordomeAccount]::Current()
    if (!$account -or $account.Id -ne -1 -or $account.Name -cne 'Majordome' -or
        $account.PasswordHash -cne $firstHash -or ![GTX.Common.SiteAccess]::IsMajordome($account)) {
        throw 'The configured credential did not produce the protected built-in account.'
    }
    if (![System.Web.Helpers.Crypto]::VerifyHashedPassword($account.PasswordHash, $password) -or
        [System.Web.Helpers.Crypto]::VerifyHashedPassword($account.PasswordHash, 'wrong-password')) {
        throw 'The built-in account did not retain the configured password hash.'
    }
    $firstRevision = $account.Revision
    if ([string]::IsNullOrWhiteSpace($firstRevision) -or
        [GTX.Common.MajordomeAccount]::Current().Revision -cne $firstRevision) {
        throw 'The same credential must retain a stable nonempty cookie revision.'
    }
    $request = New-AccessRequest ($account.Id.ToString() + ':' + $firstRevision)
    if (![GTX.Common.SiteAccess]::IsMajordome([GTX.Common.SiteAccess]::Current($request))) {
        throw 'A protected current Majordome cookie was not accepted.'
    }
    foreach ($ticket in @('-1:wrong-revision', '-1', '-1:a:b', 'invalid:revision')) {
        if ($null -ne [GTX.Common.SiteAccess]::Current((New-AccessRequest $ticket))) {
            throw 'A malformed or stale built-in cookie was accepted.'
        }
    }
    foreach ($value in @($null, 'invalid-base64', '')) {
        if ($null -ne [GTX.Common.SiteAccess]::Current([MajordomeTestRequest]::new($value))) {
            throw 'An absent or invalid cookie was accepted.'
        }
    }

    # This store has no usable connection: built-in authentication must never need SQL.
    $store = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([GTX.SiteRoleStore])
    $store.connectionString = 'not a connection string'
    if (![GTX.Common.SiteAccess]::IsMajordome($store.Authenticate($password))) {
        throw 'Built-in authentication must work independently of ordinary SQL roles.'
    }

    $secondHash = [System.Web.Helpers.Crypto]::HashPassword('Rotated-Majordome-Test43!')
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $secondHash, 'Process')
    $changed = [GTX.Common.MajordomeAccount]::Current()
    if (!$changed -or $changed.Revision -ceq $firstRevision -or
        [System.Web.Helpers.Crypto]::VerifyHashedPassword($changed.PasswordHash, $password)) {
        throw 'Changing the configured credential did not revoke the old password/revision.'
    }
    if ($null -ne [GTX.Common.SiteAccess]::Current($request)) {
        throw 'A password change must revoke an already-issued Majordome cookie.'
    }
    $newRequest = New-AccessRequest ($changed.Id.ToString() + ':' + $changed.Revision)
    if (![GTX.Common.SiteAccess]::IsMajordome([GTX.Common.SiteAccess]::Current($newRequest))) {
        throw 'The new credential revision was not accepted.'
    }

    # A present invalid override must fail closed rather than use the local credential file.
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', 'invalid-hash', 'Process')
    if ($null -ne [GTX.Common.MajordomeAccount]::Current() -or
        $null -ne [GTX.Common.SiteAccess]::Current($newRequest)) {
        throw 'Invalid administrator configuration must disable the account and its cookies.'
    }
    # A single web request reuses its resolved identity. Later requests must reload it.
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $firstHash, 'Process')
    [System.Web.HttpContext]::Current = New-AccessContext
    $cached = [GTX.Common.SiteAccess]::Current($request)
    if (![object]::ReferenceEquals($cached, [GTX.Common.SiteAccess]::Current($request))) {
        throw 'Repeated reads in one request must reuse the resolved role.'
    }
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $secondHash, 'Process')
    if (![object]::ReferenceEquals($cached, [GTX.Common.SiteAccess]::Current($request))) {
        throw 'A credential change must not replace a role snapshot already used by this request.'
    }
    if (![GTX.Common.SiteAccess]::IsMajordome([GTX.Common.SiteAccess]::Current($newRequest)) -or
        $null -ne [GTX.Common.SiteAccess]::Current($request)) {
        throw 'A different supplied cookie must not inherit the cached identity.'
    }
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $firstHash, 'Process')
    if ($null -ne [GTX.Common.SiteAccess]::Current($request)) {
        throw 'A denied cookie resolution must remain cached for the rest of the same request.'
    }
    [System.Web.HttpContext]::Current = New-AccessContext
    if (![GTX.Common.SiteAccess]::IsMajordome([GTX.Common.SiteAccess]::Current($request))) {
        throw 'A new request must reload credentials after a cached denial.'
    }
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $secondHash, 'Process')
    [GTX.Common.SiteAccess]::SignIn($changed, $request,
        [System.Web.HttpResponseWrapper]::new([System.Web.HttpContext]::Current.Response),
        [MajordomeTestSession]::new(), $true)
    if ($null -ne [GTX.Common.SiteAccess]::Current($request)) {
        throw 'Signing in must invalidate the request cache without substituting the response cookie for the supplied request.'
    }

    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $firstHash, 'Process')
    [System.Web.HttpContext]::Current = New-AccessContext
    [void][GTX.Common.SiteAccess]::Current($request)
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $secondHash, 'Process')
    [GTX.Common.SiteAccess]::Clear([System.Web.HttpResponseWrapper]::new([System.Web.HttpContext]::Current.Response))
    if ($null -ne [GTX.Common.SiteAccess]::Current($request)) {
        throw 'Signing out must invalidate an already-resolved request identity.'
    }
    [System.Web.HttpContext]::Current = New-AccessContext
    if ($null -ne [GTX.Common.SiteAccess]::Current($request) -or
        ![GTX.Common.SiteAccess]::IsMajordome([GTX.Common.SiteAccess]::Current($newRequest))) {
        throw 'The next request must reject the old credential revision and accept its replacement.'
    }
    Write-Output 'PASS: private configured account, SQL-independent authentication, protected cookies, request-scoped role reuse/denial caching, changed-cookie isolation, sign-in/out invalidation, next-request password revocation and fail-closed configuration.'
}
finally {
    [System.Web.HttpContext]::Current = $previousContext
    [Environment]::SetEnvironmentVariable('GTX_MAJORDOME_PASSWORD_HASH', $previousHash, 'Process')
}

# Exercise the XML fallback with ApplicationBase pointing at a disposable directory.
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-majordome-config-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
try {
    $binPath = Join-Path $repoPath 'bin'
    $exePath = Join-Path $testDirectory 'MajordomeFileConfigTests.exe'
    $arguments = @('/nologo', '/target:exe', '/r:System.Web.dll', '/r:System.Configuration.dll', '/r:System.Core.dll')
    $arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
        try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
        catch { } # Skip native libraries.
    }
    $arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'majordome-file-config.cs') + '"')
    $responsePath = Join-Path $testDirectory 'compile.rsp'
    [IO.File]::WriteAllLines($responsePath, $arguments)
    & (Join-Path $binPath 'roslyn/csc.exe') ('@' + $responsePath)
    if ($LASTEXITCODE -ne 0) { throw 'Majordome private-file test compilation failed. Build the application first.' }
    & $exePath $binPath
    if ($LASTEXITCODE -ne 0) { throw 'Majordome private-file configuration regression tests failed.' }
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolvedTestDirectory = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolvedTestDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTestDirectory) -notmatch '^gtx-majordome-config-[a-f0-9]{32}$') {
        throw 'Refusing cleanup because the isolated test directory identity changed.'
    }
    Remove-Item -LiteralPath $resolvedTestDirectory -Recurse -Force
}
