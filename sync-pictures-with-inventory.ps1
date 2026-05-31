[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "High")]
param(
    [string]$PicturesRoot = "",
    [string]$ConnectionString = "",
    [string]$ServerInstance = ".\MSSQLSERVER2022",
    [string]$Database = "GTX",
    [switch]$UseIntegratedSecurity,
    [string]$SqlUser = "",
    [string]$SqlPassword = "",
    [string]$InventoryTable = "dbo.Inventory",
    [string]$ImagesTable = "dbo.Images"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Data

if ([string]::IsNullOrWhiteSpace($PicturesRoot)) {
    $scriptDirectory = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($scriptDirectory)) {
        $scriptPath = $MyInvocation.MyCommand.Path
        if (-not [string]::IsNullOrWhiteSpace($scriptPath)) {
            $scriptDirectory = Split-Path -Parent $scriptPath
        }
    }

    if ([string]::IsNullOrWhiteSpace($scriptDirectory)) {
        $scriptDirectory = (Get-Location).Path
    }

    $PicturesRoot = Join-Path $scriptDirectory "..\Pictures"
}

function Write-Log {
    param(
        [Parameter(Mandatory = $true)][string]$Message
    )

    Write-Host ("[{0}] {1}" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"), $Message)
}

function Get-SafeTwoPartName {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$ParameterName
    )

    if ($Name -notmatch '^(?<schema>[A-Za-z_][A-Za-z0-9_]*)\.(?<table>[A-Za-z_][A-Za-z0-9_]*)$') {
        throw "Parameter '$ParameterName' must be in 'schema.table' format and contain only letters, numbers, and underscore."
    }

    return ("[{0}].[{1}]" -f $Matches["schema"], $Matches["table"])
}

function Get-EffectiveConnectionString {
    param(
        [string]$RawConnectionString,
        [string]$DbServer,
        [string]$DbName,
        [bool]$IntegratedSecurity,
        [string]$UserName,
        [string]$Password
    )

    if (-not [string]::IsNullOrWhiteSpace($RawConnectionString)) {
        return $RawConnectionString
    }

    if ($IntegratedSecurity -or [string]::IsNullOrWhiteSpace($UserName)) {
        return "Data Source=$DbServer;Initial Catalog=$DbName;Integrated Security=True;TrustServerCertificate=True;"
    }

    if ([string]::IsNullOrWhiteSpace($Password)) {
        throw "SqlPassword is required when SqlUser is provided without UseIntegratedSecurity."
    }

    return "Data Source=$DbServer;Initial Catalog=$DbName;User ID=$UserName;Password=$Password;TrustServerCertificate=True;"
}

function Get-NormalizedStock {
    param(
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    return $Value.Trim().ToUpperInvariant()
}

$picturesRootResolved = [System.IO.Path]::GetFullPath($PicturesRoot)
if (-not (Test-Path -LiteralPath $picturesRootResolved -PathType Container)) {
    throw "PicturesRoot does not exist or is not a directory: $picturesRootResolved"
}

$inventoryTableName = Get-SafeTwoPartName -Name $InventoryTable -ParameterName "InventoryTable"
$imagesTableName = Get-SafeTwoPartName -Name $ImagesTable -ParameterName "ImagesTable"

$effectiveConnectionString = Get-EffectiveConnectionString `
    -RawConnectionString $ConnectionString `
    -DbServer $ServerInstance `
    -DbName $Database `
    -IntegratedSecurity $UseIntegratedSecurity.IsPresent `
    -UserName $SqlUser `
    -Password $SqlPassword

$inventoryStockSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$connection = New-Object System.Data.SqlClient.SqlConnection($effectiveConnectionString)

$candidateFolderCount = 0
$deletedFolderCount = 0
$deletedImageRows = 0
$foldersToDelete = New-Object System.Collections.Generic.List[string]

try {
    Write-Log "Connecting to SQL Server..."
    $connection.Open()
    Write-Log "Connected."

    $selectStocksSql = @"
SELECT DISTINCT UPPER(LTRIM(RTRIM([Stock]))) AS [Stock]
FROM $inventoryTableName
WHERE [Stock] IS NOT NULL
  AND LTRIM(RTRIM([Stock])) <> ''
"@

    $selectStocksCmd = $connection.CreateCommand()
    $selectStocksCmd.CommandText = $selectStocksSql
    $selectStocksCmd.CommandTimeout = 120

    $reader = $selectStocksCmd.ExecuteReader()
    try {
        while ($reader.Read()) {
            $stock = Get-NormalizedStock -Value ([string]$reader["Stock"])
            if (-not [string]::IsNullOrWhiteSpace($stock)) {
                [void]$inventoryStockSet.Add($stock)
            }
        }
    }
    finally {
        $reader.Close()
    }

    Write-Log ("Inventory stocks loaded: {0}" -f $inventoryStockSet.Count)

    $pictureDirectories = Get-ChildItem -LiteralPath $picturesRootResolved -Directory -Force
    foreach ($dir in $pictureDirectories) {
        $folderName = Get-NormalizedStock -Value $dir.Name
        if ([string]::IsNullOrWhiteSpace($folderName)) {
            continue
        }

        if (-not $inventoryStockSet.Contains($folderName)) {
            $candidateFolderCount++
            [void]$foldersToDelete.Add($dir.FullName)
        }
    }

    Write-Log ("Picture folders not in inventory: {0}" -f $candidateFolderCount)

    foreach ($path in $foldersToDelete) {
        if ($PSCmdlet.ShouldProcess($path, "Delete picture folder not present in inventory")) {
            Remove-Item -LiteralPath $path -Recurse -Force
            $deletedFolderCount++
        }
    }

    $deleteStaleImagesSql = @"
DELETE I
FROM $imagesTableName AS I
WHERE NOT EXISTS (
    SELECT 1
    FROM $inventoryTableName AS V
    WHERE UPPER(LTRIM(RTRIM(V.[Stock]))) = UPPER(LTRIM(RTRIM(I.[Stock])))
);
"@

    if ($PSCmdlet.ShouldProcess($imagesTableName, "Delete image rows whose stock does not exist in inventory")) {
        $deleteStaleImagesCmd = $connection.CreateCommand()
        $deleteStaleImagesCmd.CommandText = $deleteStaleImagesSql
        $deleteStaleImagesCmd.CommandTimeout = 120
        $deletedImageRows = $deleteStaleImagesCmd.ExecuteNonQuery()
    }

    Write-Log ("Folders deleted: {0}" -f $deletedFolderCount)
    Write-Log ("Image rows deleted: {0}" -f $deletedImageRows)

    [pscustomobject]@{
        PicturesRoot          = $picturesRootResolved
        InventoryStocks       = $inventoryStockSet.Count
        CandidateFolders      = $candidateFolderCount
        DeletedFolders        = $deletedFolderCount
        DeletedImageRows      = $deletedImageRows
        WhatIfMode            = [bool]$WhatIfPreference
    }
}
finally {
    if ($connection.State -ne [System.Data.ConnectionState]::Closed) {
        $connection.Close()
    }
    $connection.Dispose()
}
