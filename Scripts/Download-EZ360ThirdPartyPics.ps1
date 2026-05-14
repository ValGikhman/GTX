[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [string]$TableName = "dbo.EZ360",
    [string]$StockColumn = "Stock",
    [string]$JsonColumn = "EZ360",
    [string[]]$StockNumbers,
    [string]$OutputRoot = "D:\GTX",
    [int]$TimeoutSec = 120,
    [switch]$SkipExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Convert-JsonCompat {
    param(
        [Parameter(Mandatory = $true)][string]$JsonText,
        [int]$Depth = 100
    )

    $convertFromJson = Get-Command -Name ConvertFrom-Json -ErrorAction Stop
    if ($convertFromJson.Parameters.ContainsKey("Depth")) {
        return ($JsonText | ConvertFrom-Json -Depth $Depth)
    }

    return ($JsonText | ConvertFrom-Json)
}

function Convert-ToSqlIdentifier {
    param([Parameter(Mandatory = $true)][string]$Identifier)

    if ($Identifier -notmatch '^[A-Za-z0-9_]+(\.[A-Za-z0-9_]+){0,2}$') {
        throw "Invalid SQL identifier: '$Identifier'. Allowed format: schema.table or table using letters/numbers/_ only."
    }

    return (($Identifier -split '\.') | ForEach-Object { "[{0}]" -f $_ }) -join "."
}

function Get-SafePathPart {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "_UNKNOWN_STOCK_"
    }

    $invalidChars = [System.IO.Path]::GetInvalidFileNameChars()
    $safe = -join ($Value.ToCharArray() | ForEach-Object {
            if ($invalidChars -contains $_) { "_" } else { $_ }
        })

    $safe = $safe.Trim().TrimEnd('.')
    if ([string]::IsNullOrWhiteSpace($safe)) { return "_UNKNOWN_STOCK_" }
    return $safe
}

function Get-FileExtensionFromUrl {
    param([string]$Url)

    try {
        $uri = [Uri]$Url
        $ext = [System.IO.Path]::GetExtension($uri.AbsolutePath)
        if ([string]::IsNullOrWhiteSpace($ext) -or $ext.Length -gt 10) {
            return ".jpg"
        }
        return $ext.ToLowerInvariant()
    } catch {
        return ".jpg"
    }
}

function Get-UrlsFromValue {
    param([object]$Value)

    $result = New-Object System.Collections.Generic.List[string]

    if ($null -eq $Value) {
        return $result
    }

    if ($Value -is [string]) {
        $candidate = $Value.Trim()
        if ($candidate -match '^https?://') {
            $result.Add($candidate)
        }
        return $result
    }

    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        foreach ($item in $Value) {
            foreach ($u in (Get-UrlsFromValue -Value $item)) {
                $result.Add($u)
            }
        }
        return $result
    }

    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            $name = [string]$key
            if ($name -match '^(Url|URL|Src|Source|Href|Link)$') {
                foreach ($u in (Get-UrlsFromValue -Value $Value[$key])) {
                    $result.Add($u)
                }
            }
        }
        return $result
    }

    if ($Value -is [pscustomobject]) {
        foreach ($p in $Value.PSObject.Properties) {
            if ($p.Name -match '^(Url|URL|Src|Source|Href|Link)$') {
                foreach ($u in (Get-UrlsFromValue -Value $p.Value)) {
                    $result.Add($u)
                }
            }
        }
    }

    return $result
}

function Get-ThirdPartyPictureUrlsFromJson {
    param([string]$JsonText)

    $urls = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    if ([string]::IsNullOrWhiteSpace($JsonText)) {
        return @()
    }

    try {
        $root = Convert-JsonCompat -JsonText $JsonText -Depth 100
    } catch {
        Write-Warning "Skipping invalid JSON payload."
        return @()
    }

    $stack = New-Object System.Collections.Stack
    $stack.Push([pscustomobject]@{ Name = ""; Value = $root })

    while ($stack.Count -gt 0) {
        $node = $stack.Pop()
        $name = [string]$node.Name
        $value = $node.Value

        if ($null -eq $value) { continue }

        if ($name -match '(?i)third.*(pic|image)') {
            foreach ($candidateUrl in (Get-UrlsFromValue -Value $value)) {
                if ($candidateUrl -match '^https?://') {
                    [void]$urls.Add($candidateUrl)
                }
            }
        }

        if ($value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
            foreach ($item in $value) {
                if ($null -eq $item) { continue }
                if ($item -is [string]) { continue }
                $stack.Push([pscustomobject]@{ Name = ""; Value = $item })
            }
            continue
        }

        if ($value -is [System.Collections.IDictionary]) {
            foreach ($key in $value.Keys) {
                $stack.Push([pscustomobject]@{ Name = [string]$key; Value = $value[$key] })
            }
            continue
        }

        if ($value -is [pscustomobject]) {
            foreach ($p in $value.PSObject.Properties) {
                $stack.Push([pscustomobject]@{ Name = $p.Name; Value = $p.Value })
            }
            continue
        }
    }

    return @($urls)
}

function Try-DownloadFile {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$Destination,
        [int]$Timeout = 120,
        [int]$MaxAttempts = 3
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            Invoke-WebRequest -Uri $Url -OutFile $Destination -TimeoutSec $Timeout -ErrorAction Stop | Out-Null
            return $true
        } catch {
            if ($attempt -ge $MaxAttempts) {
                Write-Warning "Failed after $MaxAttempts attempts: $Url"
                return $false
            }
            Start-Sleep -Seconds ([Math]::Min(5, $attempt * 2))
        }
    }

    return $false
}

$tableId = Convert-ToSqlIdentifier -Identifier $TableName
$stockId = Convert-ToSqlIdentifier -Identifier $StockColumn
$jsonId = Convert-ToSqlIdentifier -Identifier $JsonColumn

$stockFilter = @(
    $StockNumbers |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { $_.Trim().ToUpperInvariant() } |
    Select-Object -Unique
)

if (-not (Test-Path -LiteralPath $OutputRoot)) {
    New-Item -Path $OutputRoot -ItemType Directory -Force | Out-Null
}

$conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$cmd = $conn.CreateCommand()
$cmd.CommandTimeout = 120

try {
    $conn.Open()

    $query = "SELECT $stockId AS StockNo, $jsonId AS JsonPayload FROM $tableId WHERE $jsonId IS NOT NULL"

    if ($stockFilter.Count -gt 0) {
        $paramNames = New-Object System.Collections.Generic.List[string]
        for ($i = 0; $i -lt $stockFilter.Count; $i++) {
            $paramName = "@s$i"
            $param = $cmd.Parameters.Add($paramName, [System.Data.SqlDbType]::NVarChar, 100)
            $param.Value = $stockFilter[$i]
            $paramNames.Add($paramName) | Out-Null
        }
        $query += " AND UPPER($stockId) IN (" + ($paramNames -join ",") + ")"
    }

    $cmd.CommandText = $query

    $reader = $cmd.ExecuteReader()
    try {
        $totalRows = 0
        $stocksWithPics = 0
        $downloaded = 0
        $failed = 0
        $skipped = 0
        $noPics = 0

        while ($reader.Read()) {
            $totalRows++

            $stock = if ($reader.IsDBNull(0)) { "" } else { [string]$reader.GetValue(0) }
            $jsonPayload = if ($reader.IsDBNull(1)) { "" } else { [string]$reader.GetValue(1) }

            $urls = @(Get-ThirdPartyPictureUrlsFromJson -JsonText $jsonPayload)
            if ($urls.Count -eq 0) {
                $noPics++
                continue
            }

            if ([string]::IsNullOrWhiteSpace($stock) -and -not [string]::IsNullOrWhiteSpace($jsonPayload)) {
                try {
                    $obj = Convert-JsonCompat -JsonText $jsonPayload -Depth 30
                    if ($obj.PSObject.Properties["StockNo"]) { $stock = [string]$obj.StockNo }
                    elseif ($obj.PSObject.Properties["Stock"]) { $stock = [string]$obj.Stock }
                } catch {
                    # Ignore fallback parse failures
                }
            }

            $stockSafe = Get-SafePathPart -Value $stock
            $stockDir = Join-Path $OutputRoot $stockSafe
            New-Item -Path $stockDir -ItemType Directory -Force | Out-Null

            $stocksWithPics++
            $index = 1
            $seenInStock = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

            foreach ($url in $urls) {
                if (-not $seenInStock.Add($url)) { continue }

                $ext = Get-FileExtensionFromUrl -Url $url
                $fileName = "{0:D3}{1}" -f $index, $ext
                $dest = Join-Path $stockDir $fileName

                if ($SkipExisting -and (Test-Path -LiteralPath $dest)) {
                    $skipped++
                    $index++
                    continue
                }

                if (Try-DownloadFile -Url $url -Destination $dest -Timeout $TimeoutSec) {
                    $downloaded++
                } else {
                    $failed++
                }

                $index++
            }

            Write-Host ("[{0}] URLs found: {1}" -f $stockSafe, $seenInStock.Count)
        }

        Write-Host ""
        Write-Host "Done."
        Write-Host ("Rows read:            {0}" -f $totalRows)
        Write-Host ("Stocks with pictures: {0}" -f $stocksWithPics)
        Write-Host ("Rows without pictures:{0}" -f $noPics)
        Write-Host ("Downloaded files:     {0}" -f $downloaded)
        Write-Host ("Skipped existing:     {0}" -f $skipped)
        Write-Host ("Failed downloads:     {0}" -f $failed)
        Write-Host ("Output root:          {0}" -f $OutputRoot)
    } finally {
        if ($reader) { $reader.Close() }
    }
} finally {
    if ($conn.State -ne [System.Data.ConnectionState]::Closed) {
        $conn.Close()
    }
}
