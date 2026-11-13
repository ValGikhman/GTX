# Offline regression: credentials and files are synthetic and confined to a temporary application directory.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $repoPath 'bin'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-service-config-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
try {
    $exePath = Join-Path $testDirectory 'ServiceConfigurationTests.exe'
    $arguments = @('/nologo', '/target:exe', '/r:System.Web.dll', '/r:System.Configuration.dll', '/r:System.Core.dll', '/r:System.ComponentModel.DataAnnotations.dll')
    $arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
        try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
        catch { } # Skip native libraries.
    }
    $arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'service-configuration.cs') + '"')
    $responsePath = Join-Path $testDirectory 'compile.rsp'
    [IO.File]::WriteAllLines($responsePath, $arguments)
    & (Join-Path $binPath 'roslyn/csc.exe') ('@' + $responsePath)
    if ($LASTEXITCODE -ne 0) { throw 'Service configuration regression compilation failed. Build the application first.' }

    [IO.File]::WriteAllText(($exePath + '.config'), '<configuration><appSettings /></configuration>')
    & $exePath $binPath defaults
    if ($LASTEXITCODE -ne 0) { throw 'Service configuration defaults regression failed.' }

    $legacy = New-Object Xml.XmlDocument
    $root = $legacy.CreateElement('configuration')
    [void]$legacy.AppendChild($root)
    $appSettings = $legacy.CreateElement('appSettings')
    [void]$root.AppendChild($appSettings)
    $values = @{
        'OpenAI:ChatBot' = 'true'; 'OpenAI:ApiKey' = 'legacy-openai-test-key';
        'DataOne' = 'false'; 'DataOne:AccessKey' = 'legacy-dataone-test-key'; 'DataOne:SecretAccessKey' = 'legacy-dataone-test-secret';
        'CarGurus:Enabled' = 'false'; 'Cloudflare' = 'false'; 'RemoveBg:ApiKey' = 'legacy-removebg-test-key'
    }
    foreach ($key in $values.Keys) {
        $add = $legacy.CreateElement('add')
        $add.SetAttribute('key', $key)
        $add.SetAttribute('value', $values[$key])
        [void]$appSettings.AppendChild($add)
    }
    $legacy.Save(($exePath + '.config'))
    & $exePath $binPath legacy
    if ($LASTEXITCODE -ne 0) { throw 'Service configuration persistence regression failed.' }
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolvedTestDirectory = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolvedTestDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTestDirectory) -notmatch '^gtx-service-config-[a-f0-9]{32}$') {
        throw 'Refusing cleanup because the isolated service-test directory identity changed.'
    }
    Remove-Item -LiteralPath $resolvedTestDirectory -Recurse -Force
}
