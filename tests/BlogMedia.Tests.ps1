# Run after a Debug build with Windows PowerShell. No network or database calls.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Web
Add-Type -Path (Join-Path $repoPath 'bin/Common.dll')
Add-Type -Path (Join-Path $repoPath 'bin/Services.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/System.Web.Mvc.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoPath 'bin/GTX.dll'))

$cases = @(
    @{ Url = 'https://cdn.example.com/car.mp4'; Type = 'video/mp4' },
    @{ Url = ' https://cdn.example.com/car.MP4?token=A%2BB#preview '; Type = 'video/mp4' },
    @{ Url = 'http://cdn.example.com/car.m4v'; Type = 'video/mp4' },
    @{ Url = 'https://cdn.example.com/car.webm?download=1'; Type = 'video/webm' },
    @{ Url = 'https://cdn.example.com/car.OGV#preview'; Type = 'video/ogg' },
    @{ Url = '/uploads/car.mp4?token=unchanged'; Type = 'video/mp4' },
    @{ Url = '/uploads/car.WEBM#preview'; Type = 'video/webm' },
    @{ Url = 'https://cdn.example.com/car.jpg'; Type = $null },
    @{ Url = 'https://cdn.example.com/car.webp?name=car.mp4'; Type = $null },
    @{ Url = 'https://cdn.example.com/photo.png#car.mp4'; Type = $null },
    @{ Url = 'https://car.mp4'; Type = $null },
    @{ Url = 'https://cdn.example.com/car.mp4/image.jpg'; Type = $null },
    @{ Url = 'https://www.youtube.com/watch?v=example'; Type = $null },
    @{ Url = '//cdn.example.com/car.mp4'; Type = $null },
    @{ Url = 'javascript:alert(1).mp4'; Type = $null },
    @{ Url = 'data:video/mp4;base64,AAAA'; Type = $null },
    @{ Url = 'file:///C:/car.mp4'; Type = $null },
    @{ Url = 'ftp://cdn.example.com/car.mp4'; Type = $null },
    @{ Url = ''; Type = $null },
    @{ Url = $null; Type = $null }
)
foreach ($case in $cases) {
    $model = New-Object GTX.Models.BlogPostModel
    $model.MediaURL = $case.Url
    if ($model.VideoContentType -cne $case.Type) {
        throw ('Incorrect video classification for: ' + $case.Url)
    }
}

foreach ($url in @('https://cdn.example.com/car.MP4?signature=A%2BB&expires=123#preview', 'https://cdn.example.com/photo.webp?signature=A%2BB')) {
    $model = New-Object GTX.Models.BlogPostModel
    $model.Title = 'Media regression'
    $model.Content = '<p>Vehicle details</p>'
    $model.MediaURL = $url
    $entity = [GTX.Models.BlogPostModel]::ToEntity($model)
    $roundTrip = [GTX.Models.BlogPostModel]::FromEntity($entity)
    if ($entity.MediaURL -cne $url -or $roundTrip.MediaURL -cne $url -or $roundTrip.VideoContentType -cne $model.VideoContentType) {
        throw 'Image/video URL or classification changed during model/entity round trip.'
    }
}
Write-Output 'PASS: blog image/video classification, query/fragment handling, unsafe URL rejection and unchanged signed URLs through save/load.'
