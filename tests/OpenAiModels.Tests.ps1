# All OpenAI responses are simulated by HttpMessageHandler; no live API calls or credentials.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$binPath = Join-Path $repoPath 'bin'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('gtx-openai-models-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
try {
    $exePath = Join-Path $testDirectory 'OpenAiModelsTests.exe'
    $arguments = @('/nologo', '/target:exe', '/r:System.Web.dll', '/r:System.Configuration.dll', '/r:System.Core.dll')
    $arguments += Get-ChildItem -LiteralPath $binPath -Filter *.dll | ForEach-Object {
        try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); '/r:"' + $_.FullName + '"' }
        catch { } # Skip native libraries.
    }
    $arguments += @('/out:"' + $exePath + '"', '"' + (Join-Path $PSScriptRoot 'openai-models.cs') + '"')
    $responsePath = Join-Path $testDirectory 'compile.rsp'
    [IO.File]::WriteAllLines($responsePath, $arguments)
    & (Join-Path $binPath 'roslyn/csc.exe') ('@' + $responsePath)
    if ($LASTEXITCODE -ne 0) { throw 'OpenAI model-list regression compilation failed. Build the application first.' }
    & $exePath $binPath
    if ($LASTEXITCODE -ne 0) { throw 'OpenAI model-list regressions failed.' }
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolvedTestDirectory = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolvedTestDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTestDirectory) -notmatch '^gtx-openai-models-[a-f0-9]{32}$') {
        throw 'Refusing cleanup because the isolated model-test directory identity changed.'
    }
    Remove-Item -LiteralPath $resolvedTestDirectory -Recurse -Force
}
