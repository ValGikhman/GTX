[CmdletBinding()]
param(
    [string]$BucketName = "gtx",
    [string]$SourceBaseUrl = "https://photos.usedcarscincinnati.com/Images/",
    [string]$SqlServer = "VALS-PC",
    [string]$Database = "GTX",
    [string]$TokenFile = (Join-Path $PSScriptRoot "..\Your API Token.txt"),
    [string]$WorkingDirectory = (Join-Path $PSScriptRoot "..\App_Data\R2Migration"),
    [ValidateRange(1, 64)]
    [int]$Transfers = 16,
    [ValidateRange(0, 2147483647)]
    [int]$MaxFiles = 0,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-ValueAfterLabel {
    param(
        [string[]]$Lines,
        [string]$Label
    )

    for ($index = 0; $index -lt $Lines.Length - 1; $index++) {
        if ($Lines[$index].Trim() -eq $Label) {
            return $Lines[$index + 1].Trim()
        }
    }

    throw "The credential file does not contain '$Label'."
}

function Get-RclonePath {
    $installed = Get-Command rclone -ErrorAction SilentlyContinue
    if ($installed) {
        return $installed.Source
    }

    if (-not [Environment]::Is64BitOperatingSystem) {
        throw "Automatic rclone setup currently requires 64-bit Windows."
    }

    $toolDirectory = Join-Path ([IO.Path]::GetTempPath()) "gtx-r2-migration-tools"
    $rclonePath = Join-Path $toolDirectory "rclone.exe"
    if (Test-Path -LiteralPath $rclonePath) {
        return $rclonePath
    }

    New-Item -ItemType Directory -Force -Path $toolDirectory | Out-Null
    $archivePath = Join-Path $toolDirectory "rclone.zip"
    $extractPath = Join-Path $toolDirectory "extract"
    Invoke-WebRequest -Uri "https://downloads.rclone.org/rclone-current-windows-amd64.zip" -OutFile $archivePath
    if (Test-Path -LiteralPath $extractPath) {
        [IO.Directory]::Delete($extractPath, $true)
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath
    $downloadedPath = Get-ChildItem -LiteralPath $extractPath -Filter rclone.exe -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $downloadedPath) {
        throw "The rclone download did not contain rclone.exe."
    }

    Copy-Item -LiteralPath $downloadedPath -Destination $rclonePath
    return $rclonePath
}

$resolvedTokenFile = (Resolve-Path -LiteralPath $TokenFile).Path
$credentialLines = [IO.File]::ReadAllLines($resolvedTokenFile)
$accessKeyId = Get-ValueAfterLabel -Lines $credentialLines -Label "Access Key ID"
$secretAccessKey = Get-ValueAfterLabel -Lines $credentialLines -Label "Secret Access Key"
$endpoint = Get-ValueAfterLabel -Lines $credentialLines -Label "S3 API endpoint"

New-Item -ItemType Directory -Force -Path $WorkingDirectory | Out-Null
$manifestPath = Join-Path $WorkingDirectory "image-manifest.txt"
$logPath = Join-Path $WorkingDirectory ("rclone-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

$connectionString = "Data Source=$SqlServer;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True"
$sources = New-Object Collections.Generic.List[string]
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT DISTINCT REPLACE(LTRIM(RTRIM(Source)), '\', '/')
FROM dbo.Images
WHERE NULLIF(LTRIM(RTRIM(Source)), '') IS NOT NULL
ORDER BY 1;
"@
    $reader = $command.ExecuteReader()
    while ($reader.Read()) {
        $source = $reader.GetString(0).TrimStart('/')
        if ($source -and -not $source.Contains("..")) {
            $sources.Add($source)
        }
    }
    $reader.Dispose()
}
finally {
    $connection.Dispose()
}

if ($sources.Count -eq 0) {
    throw "No image paths were returned by $SqlServer/$Database."
}

if ($MaxFiles -gt 0 -and $sources.Count -gt $MaxFiles) {
    $sources = [Collections.Generic.List[string]]::new($sources.GetRange(0, $MaxFiles))
}

[IO.File]::WriteAllLines($manifestPath, $sources, [Text.UTF8Encoding]::new($false))
$rclonePath = Get-RclonePath

$environmentNames = @(
    "RCLONE_CONFIG_PHOTOS_TYPE",
    "RCLONE_CONFIG_PHOTOS_URL",
    "RCLONE_CONFIG_R2_TYPE",
    "RCLONE_CONFIG_R2_PROVIDER",
    "RCLONE_CONFIG_R2_ACCESS_KEY_ID",
    "RCLONE_CONFIG_R2_SECRET_ACCESS_KEY",
    "RCLONE_CONFIG_R2_ENDPOINT",
    "RCLONE_CONFIG_R2_REGION",
    "RCLONE_CONFIG_R2_NO_CHECK_BUCKET"
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

try {
    $env:RCLONE_CONFIG_PHOTOS_TYPE = "http"
    $env:RCLONE_CONFIG_PHOTOS_URL = $SourceBaseUrl.TrimEnd('/') + "/"
    $env:RCLONE_CONFIG_R2_TYPE = "s3"
    $env:RCLONE_CONFIG_R2_PROVIDER = "Cloudflare"
    $env:RCLONE_CONFIG_R2_ACCESS_KEY_ID = $accessKeyId
    $env:RCLONE_CONFIG_R2_SECRET_ACCESS_KEY = $secretAccessKey
    $env:RCLONE_CONFIG_R2_ENDPOINT = $endpoint
    $env:RCLONE_CONFIG_R2_REGION = "auto"
    $env:RCLONE_CONFIG_R2_NO_CHECK_BUCKET = "true"

    Write-Host "Manifest: $($sources.Count) database image paths"
    Write-Host "Destination: R2 bucket '$BucketName'"
    Write-Host "Log: $logPath"

    $bucketListing = @(& $rclonePath lsd "r2:" --config "NUL")
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to connect to Cloudflare R2 with the credentials in the token file."
    }
    $bucketExists = $bucketListing | Where-Object {
        (($_ -split "\s+") | Where-Object { $_ })[-1] -eq $BucketName
    }
    if (-not $bucketExists) {
        throw "The R2 bucket '$BucketName' was not found for this account."
    }

    $writeTestObject = ".gtx-migration-write-test-$([guid]::NewGuid().ToString('N'))"
    & $rclonePath touch "r2:$BucketName/$writeTestObject" --config "NUL" --s3-no-check-bucket
    if ($LASTEXITCODE -ne 0) {
        throw "The R2 credentials can read bucket '$BucketName' but cannot write objects. Create an R2 API token with Object Read & Write permission for this bucket, replace 'Your API Token.txt', and run the script again."
    }
    & $rclonePath deletefile "r2:$BucketName/$writeTestObject" --config "NUL" --s3-no-check-bucket
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "The write test succeeded, but its temporary object could not be removed: $writeTestObject"
    }

    $arguments = @(
        "copy",
        "photos:",
        "r2:$BucketName",
        "--config", "NUL",
        "--files-from", $manifestPath,
        "--transfers", $Transfers,
        "--checkers", ([Math]::Max(16, $Transfers * 2)),
        "--ignore-existing",
        "--no-traverse",
        "--s3-no-check-bucket",
        "--retries", "5",
        "--low-level-retries", "10",
        "--stats", "10s",
        "--stats-one-line",
        "--progress",
        "--log-level", "INFO",
        "--log-file", $logPath
    )
    if ($DryRun) {
        $arguments += "--dry-run"
    }

    & $rclonePath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "rclone finished with exit code $LASTEXITCODE. Review $logPath for missing or failed files."
    }
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], "Process")
    }
}

Write-Host "Image migration completed successfully."
