[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    [Parameter(Mandatory = $true)]
    [string]$BepInExCorePath,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedGameDir = (Resolve-Path -LiteralPath $GameDir).Path
$resolvedBepInExCore = (Resolve-Path -LiteralPath $BepInExCorePath).Path
$manifestPath = Join-Path $projectRoot 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

if ($manifest.name -ne 'BetterFreeCam' -or $manifest.name -notmatch '^[A-Za-z0-9_]+$') {
    throw 'manifest.json must use the package name BetterFreeCam.'
}
if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') {
    throw 'manifest.json version_number must contain three numeric components.'
}
if ([string]::IsNullOrWhiteSpace($manifest.description) -or $manifest.description.Length -gt 250) {
    throw 'manifest.json description must contain 1 to 250 characters.'
}
if ($manifest.website_url -ne 'https://github.com/LeonYew-Ley/BetterFreeCam') {
    throw 'manifest.json website_url does not match the project repository.'
}
if (@($manifest.dependencies).Count -ne 1 -or $manifest.dependencies[0] -ne 'BepInEx-BepInExPack_PEAK-5.4.75301') {
    throw 'manifest.json must declare BepInEx-BepInExPack_PEAK-5.4.75301.'
}

$iconPath = Join-Path $projectRoot 'icon.png'
$iconBytes = [IO.File]::ReadAllBytes($iconPath)
if ($iconBytes.Length -lt 24 -or [BitConverter]::ToString($iconBytes[0..7]) -ne '89-50-4E-47-0D-0A-1A-0A') {
    throw 'icon.png must be a PNG image.'
}
$width = ($iconBytes[16] * 16777216) + ($iconBytes[17] * 65536) + ($iconBytes[18] * 256) + $iconBytes[19]
$height = ($iconBytes[20] * 16777216) + ($iconBytes[21] * 65536) + ($iconBytes[22] * 256) + $iconBytes[23]
if ($width -ne 256 -or $height -ne 256) { throw 'icon.png must be exactly 256 x 256 pixels.' }

$buildArguments = @(
    'build', (Join-Path $projectRoot 'BetterFreeCam.csproj'),
    '-c', 'Release',
    "-p:GameDir=$resolvedGameDir",
    "-p:BepInExCorePath=$resolvedBepInExCore",
    '--ignore-failed-sources'
)
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

$dllPath = Join-Path $projectRoot 'bin\Release\netstandard2.1\BetterFreeCam.dll'
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dllPath).Version.ToString(3)
if ($assemblyVersion -ne $manifest.version_number) {
    throw "DLL version $assemblyVersion differs from manifest version $($manifest.version_number)."
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$packagePath = Join-Path $outputRoot "BetterFreeCam-$($manifest.version_number).zip"
$packageFiles = [ordered]@{
    'manifest.json' = $manifestPath
    'README.md' = (Join-Path $projectRoot 'README.md')
    'CHANGELOG.md' = (Join-Path $projectRoot 'CHANGELOG.md')
    'LICENSE' = (Join-Path $projectRoot 'LICENSE')
    'icon.png' = $iconPath
    'BepInEx/plugins/BetterFreeCam/BetterFreeCam.dll' = $dllPath
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($packagePath, [IO.FileMode]::Create)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($entry in $packageFiles.GetEnumerator()) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $entry.Value, $entry.Key, [IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    }
    finally { $archive.Dispose() }
}
finally { $stream.Dispose() }

$check = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entryNames = @($check.Entries | ForEach-Object { $_.FullName })
    if ($entryNames.Count -ne $packageFiles.Count) { throw 'Unexpected ZIP entry count.' }
    foreach ($name in $packageFiles.Keys) {
        if ($name -notin $entryNames) { throw "The package is missing $name." }
    }
}
finally { $check.Dispose() }

Write-Output "Ready for Thunderstore: $packagePath"
Get-FileHash -LiteralPath $packagePath -Algorithm SHA256
