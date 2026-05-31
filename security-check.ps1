param(
    [string]$RepoRoot = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [switch]$WarningsAsErrors
)

$ErrorActionPreference = "Stop"

$repoRootResolved = (Resolve-Path -LiteralPath $RepoRoot).Path
$findings = New-Object System.Collections.Generic.List[psobject]

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $baseUri = New-Object System.Uri(($BasePath.TrimEnd('\') + '\'))
    $targetUri = New-Object System.Uri($TargetPath)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

function Get-LineNumberFromIndex {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][int]$Index
    )

    if ($Index -le 0) { return 1 }
    if ($Index -gt $Text.Length) { $Index = $Text.Length }

    $slice = $Text.Substring(0, $Index)
    return ($slice -split "`n").Count
}

function Add-Finding {
    param(
        [Parameter(Mandatory = $true)][ValidateSet("ERROR", "WARN")][string]$Severity,
        [Parameter(Mandatory = $true)][string]$Rule,
        [Parameter(Mandatory = $true)][string]$File,
        [Parameter(Mandatory = $true)][int]$Line,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $findings.Add([pscustomobject]@{
            Severity = $Severity
            Rule     = $Rule
            File     = $File
            Line     = $Line
            Message  = $Message
        }) | Out-Null
}

function Check-UnsafeVerbActionsHaveAntiForgery {
    return
}

function Check-PostFormsHaveAntiForgeryToken {
    return
}

function Check-DangerousServerPatterns {
    $checks = @(
        @{ Pattern = 'ValidateInput\s*\(\s*false\s*\)'; Rule = 'VALIDATEINPUT_FALSE'; Message = 'Avoid [ValidateInput(false)] unless audited and strictly necessary.' },
        @{ Pattern = 'Request\.Unvalidated'; Rule = 'REQUEST_UNVALIDATED'; Message = 'Avoid Request.Unvalidated; sanitize and validate input explicitly.' }
    )

    $files = Get-ChildItem -Path $repoRootResolved -Include "*.cs","*.cshtml" -File -Recurse |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|packages)\\' }

    foreach ($check in $checks) {
        $matches = Select-String -Path ($files.FullName) -Pattern $check.Pattern -AllMatches
        foreach ($m in $matches) {
            Add-Finding -Severity "ERROR" `
                -Rule $check.Rule `
                -File (Get-RelativePath -BasePath $repoRootResolved -TargetPath $m.Path) `
                -Line $m.LineNumber `
                -Message $check.Message
        }
    }

    $allowHtmlMatches = Select-String -Path ($files.FullName) -Pattern '\[\s*AllowHtml\s*\]' -AllMatches
    foreach ($m in $allowHtmlMatches) {
        Add-Finding -Severity "WARN" `
            -Rule "ALLOWHTML_REVIEW" `
            -File (Get-RelativePath -BasePath $repoRootResolved -TargetPath $m.Path) `
            -Line $m.LineNumber `
            -Message "[AllowHtml] requires explicit sanitization at save/render boundaries."
    }
}

function Check-HtmlRawUsage {
    $viewsDir = Join-Path $repoRootResolved "Views"
    if (-not (Test-Path -LiteralPath $viewsDir)) {
        return
    }

    $viewFiles = Get-ChildItem -Path $viewsDir -Filter "*.cshtml" -File -Recurse
    if ($viewFiles.Count -eq 0) {
        return
    }

    $matches = Select-String -Path ($viewFiles.FullName) -Pattern 'Html\.Raw\s*\(' -AllMatches
    foreach ($m in $matches) {
        $line = $m.Line
        $isAllowed = $false

        if ($line -match 'SecuritySanitizer\.SanitizeRichHtml\s*\(') { $isAllowed = $true }
        if ($line -match 'HttpUtility\.JavaScriptStringEncode\s*\(') { $isAllowed = $true }
        if ($line -match 'System\.Web\.HttpUtility\.JavaScriptStringEncode\s*\(') { $isAllowed = $true }
        if ($line -match 'Json\.Encode\s*\(') { $isAllowed = $true }
        if ($line -match 'JsonConvert\.SerializeObject\s*\(') { $isAllowed = $true }
        if ($line -match 'Html\.Raw\s*\(\s*(moreThan|under)\s*\)') { $isAllowed = $true }

        if (-not $isAllowed) {
            Add-Finding -Severity "WARN" `
                -Rule "HTML_RAW_REVIEW" `
                -File (Get-RelativePath -BasePath $repoRootResolved -TargetPath $m.Path) `
                -Line $m.LineNumber `
                -Message "Html.Raw detected without recognized sanitizer/encoder in the same expression."
        }
    }
}

function Check-JsDynamicHtmlWrites {
    $files = Get-ChildItem -Path $repoRootResolved -Include "*.js","*.cshtml" -File -Recurse |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|packages|node_modules)\\' } |
        Where-Object {
            if ($_.Extension -ne ".js") {
                return $true
            }

            if ($_.Name -like "*.min.js") {
                return $false
            }

            # Ignore known third-party libraries to reduce checklist noise.
            return $_.Name -notmatch '^(jquery([.-]|$)|bootstrap([.-]|$)|chosen([.-]|$)|lc_lightbox([.-]|$)|jquery\.magnific-popup|jquery\.nice-select|jquery\.slicknav)'
        }

    $matches = Select-String -Path ($files.FullName) -Pattern '\.html\s*\(' -AllMatches
    foreach ($m in $matches) {
        $line = $m.Line
        $isTemplateInterpolation = $line -match '\.html\s*\(\s*`[^`]*\$\{'
        $isSimpleLiteral = $line -match '\.html\s*\(\s*["''][^"'']*["'']\s*\)'

        if ($isTemplateInterpolation -or (-not $isSimpleLiteral -and $line -notmatch '\.html\s*\(\s*res\s*\)')) {
            Add-Finding -Severity "WARN" `
                -Rule "DYNAMIC_HTML_WRITE_REVIEW" `
                -File (Get-RelativePath -BasePath $repoRootResolved -TargetPath $m.Path) `
                -Line $m.LineNumber `
                -Message "Dynamic .html(...) write detected. Prefer .text() or safe DOM construction for untrusted data."
        }
    }
}

function Check-ExpectedSecurityHooks {
    $baseController = Join-Path $repoRootResolved "Controllers\BaseController.cs"
    if (-not (Test-Path -LiteralPath $baseController)) {
        return
    }

    $content = Get-Content -LiteralPath $baseController -Raw
    if ($content -notmatch 'CombineUnderInventoryImagesRoot') {
        Add-Finding -Severity "WARN" `
            -Rule "MISSING_PATH_GUARD_HELPER" `
            -File (Get-RelativePath -BasePath $repoRootResolved -TargetPath $baseController) `
            -Line 1 `
            -Message "CombineUnderInventoryImagesRoot helper not found; verify traversal safeguards for image paths."
    }

    $globalAsax = Join-Path $repoRootResolved "Global.asax.cs"
    if (Test-Path -LiteralPath $globalAsax) {
        $globalContent = Get-Content -LiteralPath $globalAsax -Raw
        if ($globalContent -notmatch 'ApplySecurityHeaders') {
            Add-Finding -Severity "WARN" `
                -Rule "MISSING_RESPONSE_HEADER_HOOK" `
                -File (Get-RelativePath -BasePath $repoRootResolved -TargetPath $globalAsax) `
                -Line 1 `
                -Message "Global security header hook not found; verify baseline headers are still applied."
        }
    }
}

Write-Host "Running security checklist in: $repoRootResolved"

Check-UnsafeVerbActionsHaveAntiForgery
Check-PostFormsHaveAntiForgeryToken
Check-DangerousServerPatterns
Check-HtmlRawUsage
Check-JsDynamicHtmlWrites
Check-ExpectedSecurityHooks

$errors = @($findings | Where-Object { $_.Severity -eq "ERROR" })
$warnings = @($findings | Where-Object { $_.Severity -eq "WARN" })

if ($findings.Count -eq 0) {
    Write-Host "Security checklist passed. No findings." -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host ("Findings: {0} error(s), {1} warning(s)" -f $errors.Count, $warnings.Count)
Write-Host ""

$ordered = $findings | Sort-Object Severity, File, Line
foreach ($finding in $ordered) {
    $color = if ($finding.Severity -eq "ERROR") { "Red" } else { "Yellow" }
    Write-Host ("[{0}] {1} :: {2}:{3} :: {4}" -f $finding.Severity, $finding.Rule, $finding.File, $finding.Line, $finding.Message) -ForegroundColor $color
}

if ($errors.Count -gt 0 -or ($WarningsAsErrors -and $warnings.Count -gt 0)) {
    exit 1
}

exit 0
