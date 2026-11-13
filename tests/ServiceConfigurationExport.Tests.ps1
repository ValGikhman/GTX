# Migration regression with synthetic values only; never opens the application's real configuration.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$exportScript = Join-Path $repoPath 'Scripts/Export-ServiceConfiguration.ps1'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-service-export-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
$raw = ' raw<&>"''`$()=+ '

function New-ExportFixture([string]$Name, [bool]$ExplicitRemoveDisabled = $false) {
    $rootPath = Join-Path $testDirectory $Name
    [void][IO.Directory]::CreateDirectory((Join-Path $rootPath 'App_Data'))
    $sourcePath = Join-Path $rootPath 'Web.config'
    $document = [xml]'<configuration><appSettings/><connectionStrings><add name="Fixture" connectionString="synthetic-only"/></connectionStrings></configuration>'
    $values = [ordered]@{
        'SiteEnvironment' = 'FixtureOnly'; 'OpenAI:ChatBot' = 'true'; 'OpenAI:ApiKey' = $raw;
        'DataOne' = 'false'; 'DataOne:AccessKey' = 'fixture-dataone-access'; 'DataOne:SecretAccessKey' = $raw;
        'CarGurus:Enabled' = 'true'; 'Cloudflare' = 'true'; 'Cloudflare:R2:AccessKeyId' = 'config-r2-access';
        'RemoveBg:ApiKey' = $raw
    }
    if ($ExplicitRemoveDisabled) { $values['RemoveBg:Enabled'] = 'false' }
    foreach ($key in $values.Keys) {
        $add = $document.CreateElement('add')
        $add.SetAttribute('key', $key)
        $add.SetAttribute('value', $values[$key])
        [void]$document.SelectSingleNode('/configuration/appSettings').AppendChild($add)
    }
    $document.Save($sourcePath)
    [IO.File]::WriteAllLines((Join-Path $rootPath 'App_Data/R2Credentials.txt'), @(
        'Access Key ID', 'file-r2-access', 'Secret Access Key', 'file-r2-secret<&>"''`$()=+',
        'S3 API endpoint', 'https://r2-fixture.example.test'
    ))
    return [pscustomobject]@{ Source = $sourcePath; Target = (Join-Path $rootPath 'App_Data/Services.config') }
}

function Invoke-ExportFixture($Fixture, [switch]$Remove) {
    # Expected failures remain child-process exit codes, without printing credential-bearing XML.
    $ErrorActionPreference = 'Continue'
    if ($Remove) {
        $null = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $exportScript -WebConfigPath $Fixture.Source -Path $Fixture.Target -RemoveFromWebConfig 2>&1
    }
    else {
        $null = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $exportScript -WebConfigPath $Fixture.Source -Path $Fixture.Target 2>&1
    }
    return $LASTEXITCODE
}

function Read-ExportValues([string]$Path) {
    $document = [xml]([IO.File]::ReadAllText($Path))
    $values = @{}
    foreach ($add in $document.services.add) {
        if ($values.ContainsKey($add.GetAttribute('key'))) { throw 'The export contains duplicate setting keys.' }
        $values[$add.GetAttribute('key')] = $add.GetAttribute('value')
    }
    return $values
}

try {
    $fixture = New-ExportFixture 'preserve-source'
    $originalSource = [Convert]::ToBase64String([IO.File]::ReadAllBytes($fixture.Source))
    if ((Invoke-ExportFixture $fixture) -ne 0) { throw 'Exporting synthetic legacy settings failed.' }
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($fixture.Source)) -cne $originalSource) { throw 'Export without the removal switch changed its source file.' }
    $exported = Read-ExportValues $fixture.Target
    if ($exported.Count -ne 27) { throw 'The export must include every service switch, field and new default.' }
    foreach ($key in @('OpenAI:ApiKey', 'DataOne:SecretAccessKey', 'RemoveBg:ApiKey')) {
        if ($exported[$key] -cne $raw) { throw 'An XML-significant raw credential changed during export.' }
    }
    if ($exported['OpenAI:ChatBot'] -cne 'true' -or $exported['DataOne'] -cne 'false' -or
        $exported['CarGurus:Enabled'] -cne 'true' -or $exported['Cloudflare'] -cne 'true' -or
        $exported['RemoveBg:Enabled'] -cne 'true') { throw 'Legacy enabled states or derived remove.bg state were not preserved.' }
    if ($exported['Cloudflare:R2:AccessKeyId'] -cne 'config-r2-access' -or
        $exported['Cloudflare:R2:SecretAccessKey'] -cne 'file-r2-secret<&>"''`$()=+' -or
        $exported['Cloudflare:R2:Endpoint'] -cne 'https://r2-fixture.example.test') { throw 'Legacy R2 import or configured-value precedence failed.' }
    if ($exported['OpenAI:ChatModel'] -cne 'gpt-4.1-mini' -or
        $exported['CarGurus:McpServerUrl'] -cne 'https://mcp-apps.api.cargurus.com/chat-mcp/v1/stream' -or
        $exported['RemoveBg:Endpoint'] -cne 'https://api.remove.bg/v1.0/removebg' -or
        $exported['RemoveBg:AccountEndpoint'] -cne 'https://api.remove.bg/v1.0/account') { throw 'New service defaults were not exported.' }

    $fixture = New-ExportFixture 'remove-source' $true
    if ((Invoke-ExportFixture $fixture -Remove) -ne 0) { throw 'Exporting and removing synthetic settings failed.' }
    $cleaned = [xml]([IO.File]::ReadAllText($fixture.Source))
    $remaining = @($cleaned.configuration.appSettings.add)
    if ($remaining.Count -ne 1 -or $remaining[0].GetAttribute('key') -cne 'SiteEnvironment' -or
        $remaining[0].GetAttribute('value') -cne 'FixtureOnly' -or
        $cleaned.configuration.connectionStrings.add.GetAttribute('connectionString') -cne 'synthetic-only') {
        throw 'Removing exported settings changed unrelated infrastructure configuration.'
    }
    $exported = Read-ExportValues $fixture.Target
    if ($exported['RemoveBg:Enabled'] -cne 'false' -or $exported['RemoveBg:ApiKey'] -cne $raw) { throw 'An explicit disabled remove.bg switch must override credential-derived enablement.' }

    $fixture = New-ExportFixture 'existing-output'
    [IO.File]::WriteAllText($fixture.Target, '<services><!-- already managed --></services>')
    $originalSource = [Convert]::ToBase64String([IO.File]::ReadAllBytes($fixture.Source))
    $originalOutput = [Convert]::ToBase64String([IO.File]::ReadAllBytes($fixture.Target))
    if ((Invoke-ExportFixture $fixture -Remove) -eq 0) { throw 'The export must refuse to overwrite an existing managed configuration.' }
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($fixture.Source)) -cne $originalSource -or
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($fixture.Target)) -cne $originalOutput) { throw 'A refused export changed its source or existing output.' }

    # A failed final replacement must retain the original Web.config and the already-exported private settings.
    $fixture = New-ExportFixture 'locked-source'
    $originalSource = [Convert]::ToBase64String([IO.File]::ReadAllBytes($fixture.Source))
    $sourceLock = [IO.File]::Open($fixture.Source, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ((Invoke-ExportFixture $fixture -Remove) -eq 0) { throw 'A locked source must reject the final atomic cleanup.' }
    }
    finally { $sourceLock.Dispose() }
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($fixture.Source)) -cne $originalSource) { throw 'A failed atomic cleanup damaged the original Web.config.' }
    $exported = Read-ExportValues $fixture.Target
    if ($exported['OpenAI:ApiKey'] -cne $raw -or $exported['RemoveBg:Enabled'] -cne 'true') { throw 'A failed source cleanup damaged the completed private export.' }
    if (@(Get-ChildItem -LiteralPath $testDirectory -Filter 'Web.config.*.tmp' -Recurse -File).Count) { throw 'Atomic Web.config cleanup left a temporary file behind.' }
    if (@(Get-ChildItem -LiteralPath $testDirectory -Filter 'Services.config.*.tmp' -Recurse -File).Count) { throw 'Export left a temporary credential file behind.' }
    Write-Output 'PASS: isolated raw-value/flag/default export, R2 legacy import, source preservation, selective atomic Web.config cleanup, existing-output refusal and intact source/export after a locked-source failure.'
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolvedTestDirectory = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolvedTestDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTestDirectory) -notmatch '^gtx-service-export-[a-f0-9]{32}$') {
        throw 'Refusing cleanup because the isolated export-test directory identity changed.'
    }
    Remove-Item -LiteralPath $resolvedTestDirectory -Recurse -Force
}
