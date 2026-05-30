param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$IndexPath = "/Inventory",
    [string]$Stock = "",
    [ValidateRange(1, 500)]
    [int]$MaxImagesPerPage = 120,
    [ValidateRange(1, 120)]
    [int]$RequestTimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

function Get-SafeString {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return [string]$Value
}

function New-AbsoluteUrl {
    param(
        [Parameter(Mandatory = $true)][string]$BasePageUrl,
        [Parameter(Mandatory = $true)][string]$Candidate
    )

    $value = [System.Net.WebUtility]::HtmlDecode((Get-SafeString $Candidate).Trim())
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    if ($value.StartsWith("data:", [System.StringComparison]::OrdinalIgnoreCase) -or
        $value.StartsWith("javascript:", [System.StringComparison]::OrdinalIgnoreCase) -or
        $value.StartsWith("#")) {
        return $null
    }

    if ($value.StartsWith("//")) {
        $baseUri = [System.Uri]$BasePageUrl
        return ($baseUri.Scheme + ":" + $value)
    }

    try {
        $uri = [System.Uri]$value
        if ($uri.IsAbsoluteUri) {
            return $uri.AbsoluteUri
        }
    }
    catch {
        # Fall through and resolve as relative.
    }

    try {
        $baseUri = [System.Uri]$BasePageUrl
        $resolved = New-Object System.Uri($baseUri, $value)
        return $resolved.AbsoluteUri
    }
    catch {
        return $null
    }
}

function Test-IsLikelyImageUrl {
    param(
        [Parameter(Mandatory = $true)][string]$Url
    )

    if ($Url -match '(?i)/InventoryImages/Get\?path=') {
        return $true
    }

    return ($Url -match '(?i)\.(jpg|jpeg|png|webp|avif|gif|bmp|svg)(\?|#|$)')
}

function Get-ImageUrlsFromHtml {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$PageUrl
    )

    $urls = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

    $attributeMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $Html,
        '(?is)\b(?:src|href|data-lcl-thumb)\s*=\s*["''](?<u>[^"''>]+)["'']'
    )

    foreach ($m in $attributeMatches) {
        $raw = $m.Groups["u"].Value
        $absolute = New-AbsoluteUrl -BasePageUrl $PageUrl -Candidate $raw
        if ($absolute -and (Test-IsLikelyImageUrl -Url $absolute)) {
            [void]$urls.Add($absolute)
        }
    }

    $styleMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $Html,
        '(?is)url\((?<u>[^)]+)\)'
    )

    foreach ($m in $styleMatches) {
        $raw = (Get-SafeString $m.Groups["u"].Value).Trim()
        $raw = $raw -replace '^\s*["'']', ''
        $raw = $raw -replace '["'']\s*$', ''
        $absolute = New-AbsoluteUrl -BasePageUrl $PageUrl -Candidate $raw
        if ($absolute -and (Test-IsLikelyImageUrl -Url $absolute)) {
            [void]$urls.Add($absolute)
        }
    }

    return @($urls)
}

function Get-FirstStockFromIndexHtml {
    param(
        [Parameter(Mandatory = $true)][string]$Html
    )

    $dataStockMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Html,
        '(?is)\bdata-stock\s*=\s*["''](?<stock>[^"''\s>]+)["'']'
    )
    if ($dataStockMatch.Success) {
        return $dataStockMatch.Groups["stock"].Value.Trim()
    }

    $detailsMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Html,
        '(?is)/Inventory/Details\?stock=(?<stock>[^"&''\s>]+)'
    )
    if ($detailsMatch.Success) {
        return [System.Uri]::UnescapeDataString($detailsMatch.Groups["stock"].Value.Trim())
    }

    return $null
}

function Get-UrlWithoutWidth {
    param(
        [Parameter(Mandatory = $true)][string]$Url
    )

    if ($Url -notmatch '(?i)/InventoryImages/Get\?path=') {
        return $null
    }

    if ($Url -notmatch '(?i)(\?|&)width=') {
        return $null
    }

    $fragment = ""
    $base = $Url

    $hashIndex = $Url.IndexOf("#")
    if ($hashIndex -ge 0) {
        $fragment = $Url.Substring($hashIndex)
        $base = $Url.Substring(0, $hashIndex)
    }

    $withoutWidth = [System.Text.RegularExpressions.Regex]::Replace(
        $base,
        '(?i)([?&])width=[^&#]*&?',
        '$1'
    )
    $withoutWidth = $withoutWidth -replace '\?&', '?'
    $withoutWidth = $withoutWidth -replace '[?&]$', ''

    return ($withoutWidth + $fragment)
}

function Get-ResponseByteCount {
    param(
        [Parameter(Mandatory = $true)][System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory = $true)][string]$Url
    )

    $result = [ordered]@{
        Url         = $Url
        StatusCode  = 0
        Bytes       = 0L
        ContentType = ""
        Error       = ""
        Success     = $false
    }

    try {
        $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Get, $Url)
        $response = $Client.SendAsync($request, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).Result
        $result.StatusCode = [int]$response.StatusCode

        if (-not $response.IsSuccessStatusCode) {
            $result.Error = "HTTP " + $result.StatusCode
            $response.Dispose()
            $request.Dispose()
            return [pscustomobject]$result
        }

        if ($response.Content -and $response.Content.Headers.ContentType) {
            $result.ContentType = [string]$response.Content.Headers.ContentType.MediaType
        }

        $length = $null
        if ($response.Content -and $response.Content.Headers.ContentLength) {
            $length = [long]$response.Content.Headers.ContentLength
        }

        if ($null -ne $length -and $length -ge 0) {
            $result.Bytes = $length
        }
        else {
            $stream = $response.Content.ReadAsStreamAsync().Result
            $buffer = New-Object byte[] 81920
            $total = 0L
            while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $total += [long]$read
            }
            $result.Bytes = $total
            $stream.Dispose()
        }

        $result.Success = $true
        $response.Dispose()
        $request.Dispose()
        return [pscustomobject]$result
    }
    catch {
        $result.Error = $_.Exception.Message
        return [pscustomobject]$result
    }
}

function Format-Size {
    param([long]$Bytes)

    if ($Bytes -lt 1KB) { return ("{0} B" -f $Bytes) }
    if ($Bytes -lt 1MB) { return ("{0:N1} KB" -f ($Bytes / 1KB)) }
    if ($Bytes -lt 1GB) { return ("{0:N2} MB" -f ($Bytes / 1MB)) }
    return ("{0:N2} GB" -f ($Bytes / 1GB))
}

function Measure-PageImagePayload {
    param(
        [Parameter(Mandatory = $true)][string]$PageName,
        [Parameter(Mandatory = $true)][string]$PageUrl,
        [Parameter(Mandatory = $true)][System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory = $true)][int]$MaxImages
    )

    Write-Host ""
    Write-Host ("[{0}] Fetching page: {1}" -f $PageName, $PageUrl)
    $response = Invoke-WebRequest -Uri $PageUrl -Method Get
    $html = [string]$response.Content

    $allImageUrls = Get-ImageUrlsFromHtml -Html $html -PageUrl $PageUrl
    $selected = @($allImageUrls | Select-Object -First $MaxImages)

    Write-Host ("[{0}] Found {1} image URLs (measuring first {2})." -f $PageName, $allImageUrls.Count, $selected.Count)

    $details = New-Object System.Collections.Generic.List[object]
    $totalBytes = 0L
    $failed = 0
    $internalOptimizedBytes = 0L
    $internalUnoptimizedBytes = 0L
    $comparedInternalCount = 0

    foreach ($url in $selected) {
        $measurement = Get-ResponseByteCount -Client $Client -Url $url
        $totalBytes += [long]$measurement.Bytes
        if (-not $measurement.Success) {
            $failed++
        }

        $unoptimizedUrl = Get-UrlWithoutWidth -Url $url
        $unoptimizedBytes = 0L

        if ($unoptimizedUrl) {
            $unoptimizedMeasurement = Get-ResponseByteCount -Client $Client -Url $unoptimizedUrl
            if ($measurement.Success) {
                $internalOptimizedBytes += [long]$measurement.Bytes
            }
            if ($unoptimizedMeasurement.Success) {
                $internalUnoptimizedBytes += [long]$unoptimizedMeasurement.Bytes
                $unoptimizedBytes = [long]$unoptimizedMeasurement.Bytes
                $comparedInternalCount++
            }
        }

        $details.Add([pscustomobject]@{
                Page              = $PageName
                Url               = $url
                Bytes             = [long]$measurement.Bytes
                StatusCode        = [int]$measurement.StatusCode
                Success           = [bool]$measurement.Success
                InternalBaseline  = $unoptimizedBytes
                Error             = $measurement.Error
            }) | Out-Null
    }

    $savings = $internalUnoptimizedBytes - $internalOptimizedBytes
    $savingsPct = 0.0
    if ($internalUnoptimizedBytes -gt 0) {
        $savingsPct = [math]::Round(($savings * 100.0) / $internalUnoptimizedBytes, 2)
    }

    return [pscustomobject]@{
        PageName                = $PageName
        PageUrl                 = $PageUrl
        Html                    = $html
        ImageUrlsFound          = $allImageUrls.Count
        ImagesMeasured          = $selected.Count
        FailedRequests          = $failed
        TotalImageBytes         = $totalBytes
        InternalComparedCount   = $comparedInternalCount
        InternalOptimizedBytes  = $internalOptimizedBytes
        InternalUnoptimizedBytes = $internalUnoptimizedBytes
        EstimatedSavingsBytes   = $savings
        EstimatedSavingsPercent = $savingsPct
        Details                 = @($details)
    }
}

$normalizedBase = (Get-SafeString $BaseUrl).Trim().TrimEnd("/")
if ([string]::IsNullOrWhiteSpace($normalizedBase)) {
    throw "BaseUrl is required."
}

$indexPathValue = (Get-SafeString $IndexPath).Trim()
if ([string]::IsNullOrWhiteSpace($indexPathValue)) {
    $indexPathValue = "/Inventory"
}
if (-not $indexPathValue.StartsWith("/")) {
    $indexPathValue = "/" + $indexPathValue
}

$indexUrl = $normalizedBase + $indexPathValue

$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $true
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [System.TimeSpan]::FromSeconds($RequestTimeoutSeconds)
$client.DefaultRequestHeaders.UserAgent.ParseAdd("GTX-ImageBenchmark/1.0")

Write-Host ("Base URL: {0}" -f $normalizedBase)
Write-Host ("Index URL: {0}" -f $indexUrl)
Write-Host ("Max images per page: {0}" -f $MaxImagesPerPage)

$indexResult = Measure-PageImagePayload -PageName "Index" -PageUrl $indexUrl -Client $client -MaxImages $MaxImagesPerPage

$stockValue = (Get-SafeString $Stock).Trim()
if ([string]::IsNullOrWhiteSpace($stockValue)) {
    $stockValue = Get-FirstStockFromIndexHtml -Html $indexResult.Html
}

if ([string]::IsNullOrWhiteSpace($stockValue)) {
    throw "Could not determine a stock value. Re-run with -Stock <stockNumber>."
}

$detailsUrl = "{0}/Inventory/Details?stock={1}" -f $normalizedBase, [System.Uri]::EscapeDataString($stockValue)
Write-Host ("Details stock: {0}" -f $stockValue)
Write-Host ("Details URL: {0}" -f $detailsUrl)

$detailsResult = Measure-PageImagePayload -PageName "Details" -PageUrl $detailsUrl -Client $client -MaxImages $MaxImagesPerPage

$client.Dispose()
$handler.Dispose()

$all = @($indexResult, $detailsResult)

Write-Host ""
Write-Host "==== Image Payload Summary ===="
$summaryRows = $all | ForEach-Object {
    [pscustomobject]@{
        Page                          = $_.PageName
        ImageUrlsFound                = $_.ImageUrlsFound
        ImagesMeasured                = $_.ImagesMeasured
        FailedRequests                = $_.FailedRequests
        TotalImageBytes               = $_.TotalImageBytes
        TotalImageSize                = (Format-Size -Bytes $_.TotalImageBytes)
        InternalComparedCount         = $_.InternalComparedCount
        InternalOptimizedBytes        = $_.InternalOptimizedBytes
        InternalUnoptimizedBytes      = $_.InternalUnoptimizedBytes
        EstimatedSavingsBytes         = $_.EstimatedSavingsBytes
        EstimatedSavingsSize          = (Format-Size -Bytes $_.EstimatedSavingsBytes)
        EstimatedSavingsPercent       = ("{0:N2}%" -f $_.EstimatedSavingsPercent)
    }
}

$summaryRows | Format-Table -AutoSize

$allDetails = @($indexResult.Details + $detailsResult.Details)
$topLargest = $allDetails | Where-Object { $_.Success } | Sort-Object Bytes -Descending | Select-Object -First 12

if ($topLargest.Count -gt 0) {
    Write-Host ""
    Write-Host "Top measured images by payload:"
    $topLargest | Select-Object Page, Bytes, Url | Format-Table -AutoSize
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outDir = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "App_Data"
if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$jsonPath = Join-Path $outDir ("image-benchmark-" + $timestamp + ".json")
$csvPath = Join-Path $outDir ("image-benchmark-details-" + $timestamp + ".csv")

[pscustomobject]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString("o")
    BaseUrl = $normalizedBase
    Index = $indexResult
    Details = $detailsResult
} | ConvertTo-Json -Depth 8 | Out-File -LiteralPath $jsonPath -Encoding utf8

$allDetails | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding utf8

Write-Host ""
Write-Host ("Saved summary JSON: {0}" -f $jsonPath)
Write-Host ("Saved details CSV:  {0}" -f $csvPath)
