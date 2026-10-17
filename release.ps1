$ErrorActionPreference = 'Stop'
$repoUrl = 'https://github.com/t3sco/Barhopper'
$rawUrl = 'https://raw.githubusercontent.com/t3sco/Barhopper/main'
$root = $PSScriptRoot

dotnet build (Join-Path $root 'Barhopper.csproj') -c Release
if ($LASTEXITCODE -ne 0) {
    throw 'Release build failed'
}

$output = Join-Path $root 'bin\Release\Barhopper'
$manifest = Get-Content (Join-Path $output 'Barhopper.json') -Raw | ConvertFrom-Json
$version = $manifest.AssemblyVersion
$download = "$repoUrl/releases/download/v$version/latest.zip"
$manifest | Add-Member -Force NoteProperty RepoUrl $repoUrl
$manifest | Add-Member -Force NoteProperty DownloadLinkInstall $download
$manifest | Add-Member -Force NoteProperty DownloadLinkUpdate $download
$manifest | Add-Member -Force NoteProperty LastUpdate ([string][DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
if (Test-Path (Join-Path $root 'images\icon.png')) {
    $manifest | Add-Member -Force NoteProperty IconUrl "$rawUrl/images/icon.png"
}

$json = ConvertTo-Json -InputObject @($manifest) -Depth 5
[IO.File]::WriteAllText((Join-Path $root 'repo.json'), $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "repo.json now points at v$version."
Write-Host "Create a GitHub release tagged v$version and attach $(Join-Path $output 'latest.zip'), then push repo.json."
