param(
    [string]$WebConfigPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Web.config'),
    [string]$Path = (Join-Path (Split-Path $PSScriptRoot -Parent) 'App_Data/Services.config'),
    [switch]$RemoveFromWebConfig
)

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))
$sourcePath = [IO.Path]::GetFullPath($WebConfigPath)
$targetPath = [IO.Path]::GetFullPath($Path)
if ([IO.File]::Exists($targetPath)) { throw 'The service configuration file already exists. Keep its saved settings; do not overwrite it with an older Web.config.' }
$source = New-Object Xml.XmlDocument
$source.PreserveWhitespace = $true
$readerSettings = New-Object Xml.XmlReaderSettings
$readerSettings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
$readerSettings.XmlResolver = $null
$reader = [Xml.XmlReader]::Create($sourcePath, $readerSettings)
try { $source.Load($reader) }
finally { $reader.Dispose() }
$values = [ordered]@{}
foreach ($service in [GTX.Common.ServiceConfiguration]::Services) {
    $values[$service.EnabledKey] = 'false'
    foreach ($field in $service.Fields) { $values[$field.Key] = $field.DefaultValue }
}
$legacy = @{}
foreach ($setting in $source.SelectNodes('/configuration/appSettings/add')) {
    $key = $setting.GetAttribute('key')
    if ($values.Contains($key)) { $legacy[$key] = $setting.GetAttribute('value'); $values[$key] = $legacy[$key] }
}
if (!$legacy.ContainsKey('RemoveBg:Enabled')) { $values['RemoveBg:Enabled'] = if ([string]::IsNullOrWhiteSpace($values['RemoveBg:ApiKey'])) { 'false' } else { 'true' } }

# Preserve earlier R2 credential-file installations before removing that runtime fallback.
$appRoot = [IO.Path]::GetDirectoryName($sourcePath)
$credentialPaths = @((Join-Path $appRoot 'App_Data/R2Credentials.txt'), (Join-Path $appRoot 'Your API Token.txt'))
$credentialFile = $credentialPaths | Where-Object { [IO.File]::Exists($_) } | Select-Object -First 1
if ($credentialFile) {
    $lines = [IO.File]::ReadAllLines($credentialFile)
    $labels = @{'Cloudflare:R2:AccessKeyId'='Access Key ID';'Cloudflare:R2:SecretAccessKey'='Secret Access Key';'Cloudflare:R2:Endpoint'='S3 API endpoint'}
    foreach ($key in $labels.Keys) {
        if (![string]::IsNullOrWhiteSpace($values[$key])) { continue }
        for ($index=0; $index -lt $lines.Length-1; $index++) {
            if ($lines[$index].Trim() -ieq $labels[$key]) { $values[$key] = $lines[$index+1].Trim(); break }
        }
    }
}

[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($targetPath))
$temporary = $targetPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    $document = New-Object Xml.XmlDocument
    $root = $document.CreateElement('services')
    [void]$document.AppendChild($root)
    foreach ($key in $values.Keys) {
        $element = $document.CreateElement('add')
        $element.SetAttribute('key', $key)
        $element.SetAttribute('value', $values[$key])
        [void]$root.AppendChild($element)
    }
    $document.Save($temporary)
    [IO.File]::Move($temporary, $targetPath)
}
finally { if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) } }
Write-Output "Exported $($values.Count) service settings to the private configuration file."

if ($RemoveFromWebConfig) {
    foreach ($setting in @($source.SelectNodes('/configuration/appSettings/add'))) {
        if ($values.Contains($setting.GetAttribute('key'))) { [void]$setting.ParentNode.RemoveChild($setting) }
    }
    $sourceTemporary = $sourcePath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $source.Save($sourceTemporary)
        [IO.File]::Replace($sourceTemporary, $sourcePath, [NullString]::Value)
    }
    finally { if ([IO.File]::Exists($sourceTemporary)) { [IO.File]::Delete($sourceTemporary) } }
    Write-Output 'Removed the exported service settings from Web.config.'
}
