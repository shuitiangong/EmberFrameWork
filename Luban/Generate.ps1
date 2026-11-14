param(
    [ValidateSet('Json', 'Bin')]
    [string] $Format = 'Json'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$repoRoot = Split-Path -Parent $PSScriptRoot
$unityRoot = Join-Path $repoRoot 'EmberFrameWork'
$archive = Join-Path $PSScriptRoot 'Tools/Luban-5.1.0.7z'
$config = Join-Path $PSScriptRoot 'Config/luban.conf'
$manifestPath = Join-Path $PSScriptRoot "GeneratedManifest.$Format.json"
$cacheRoot = Join-Path $unityRoot 'Library/Luban'
$toolRoot = Join-Path $cacheRoot '5.1.0'
$codeDestination = Join-Path $unityRoot "Assets/ScriptsGenerate/Generate${Format}Cfg"
$dataDestination = Join-Path $unityRoot "Assets/RawAssets/Configs/$Format"
$utilityPath = Join-Path $unityRoot 'Assets/Scripts/LubanUtility/LubanUtility.cs'
$target = "client-$($Format.ToLowerInvariant())"
$codeTarget = if ($Format -eq 'Json') { 'cs-simple-json' } else { 'cs-bin' }
$dataTarget = if ($Format -eq 'Json') { 'json' } else { 'bin' }
$dataExtension = if ($Format -eq 'Json') { '.json' } else { '.bytes' }
$expectedHash = 'bac9a1b8d69cfeaa7ef1d8c67b34a007324d1cd158e0b14294521b3879ba5213'

function Get-Sha256([string] $path) {
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($path)
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Luban requires .NET 8 or later. Install the .NET 8 SDK or runtime.'
}
if (-not (Get-Command tar.exe -ErrorAction SilentlyContinue)) {
    throw 'tar.exe is required to extract the bundled Luban release.'
}
if (-not (Test-Path -LiteralPath $archive)) {
    throw "Luban archive is missing: $archive"
}
if ((Get-Sha256 $archive) -ne $expectedHash) {
    throw 'The Luban archive SHA-256 does not match the pinned official release.'
}

New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $toolRoot 'Luban.dll'))) {
    if (Test-Path -LiteralPath $toolRoot) {
        throw "Incomplete Luban cache at $toolRoot. Remove that cache directory and retry."
    }
    New-Item -ItemType Directory -Path $toolRoot | Out-Null
    & tar.exe -xf $archive -C $toolRoot --strip-components 1
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $toolRoot 'Luban.dll'))) {
        throw 'Failed to extract the Luban release.'
    }
}

function Get-OwnedPath([string] $root, [string] $relativePath) {
    if ([string]::IsNullOrWhiteSpace($relativePath) -or [System.IO.Path]::IsPathRooted($relativePath)) {
        throw "Invalid generated path: $relativePath"
    }
    $fullRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $root $relativePath))
    if (-not $fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Generated path is outside its destination: $relativePath"
    }
    return $fullPath
}

function Sync-GeneratedFiles([string] $source, [string] $destination, [string[]] $previous, [string] $extension, [switch] $flatten) {
    $sourceRoot = [System.IO.Path]::GetFullPath($source).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $files = @(Get-ChildItem -LiteralPath $source -File -Recurse)
    # Validate the complete file set before writing any output.
    $outputNames = @{}
    foreach ($file in $files) {
        if ($file.Extension -ne $extension) {
            throw "Unexpected generated file: $($file.FullName)"
        }
        if ($flatten) {
            if ($outputNames.ContainsKey($file.Name)) {
                throw "Flat output filename collision: $($file.Name). Use unique type names across modules."
            }
            $outputNames[$file.Name] = $true
        }
    }
    $paths = @()
    foreach ($file in $files) {
        if ($file.Extension -ne $extension) {
            throw "Unexpected generated file: $($file.FullName)"
        }
        $relative = if ($flatten) { $file.Name } else { $file.FullName.Substring($sourceRoot.Length).Replace('\', '/') }
        $target = Get-OwnedPath $destination $relative
        $targetDirectory = Split-Path -Parent $target
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        # Preserve Unity GUIDs when migrating a manifest-owned file to the flat layout.
        if ($flatten -and -not (Test-Path -LiteralPath ($target + '.meta'))) {
            $oldPaths = @($previous | Where-Object { $_ -ne $relative -and [System.IO.Path]::GetFileName($_) -eq $file.Name })
            if ($oldPaths.Count -eq 1) {
                $oldTarget = Get-OwnedPath $destination $oldPaths[0]
                if (Test-Path -LiteralPath ($oldTarget + '.meta') -PathType Leaf) {
                    Move-Item -LiteralPath ($oldTarget + '.meta') -Destination ($target + '.meta')
                }
            }
        }
        if (-not (Test-Path -LiteralPath $target) -or (Get-Sha256 $file.FullName) -ne (Get-Sha256 $target)) {
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
        $paths += $relative
    }
    foreach ($relative in $previous) {
        if ([string]::IsNullOrWhiteSpace($relative) -or $paths -contains $relative) { continue }
        if ([System.IO.Path]::GetExtension($relative) -ne $extension) {
            throw "Unexpected path in generated manifest: $relative"
        }
        $target = Get-OwnedPath $destination $relative
        if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target }
        if (Test-Path -LiteralPath ($target + '.meta') -PathType Leaf) { Remove-Item -LiteralPath ($target + '.meta') }
    }
    # Remove only empty directories previously used by generated files.
    $destinationRoot = [System.IO.Path]::GetFullPath($destination).TrimEnd('\', '/')
    foreach ($relative in $previous) {
        $directory = Split-Path -Parent (Get-OwnedPath $destination $relative)
        while ($directory -and $directory -ne $destinationRoot) {
            if (Test-Path -LiteralPath $directory -PathType Container) {
                if (@(Get-ChildItem -LiteralPath $directory -Force).Count -ne 0) { break }
                Remove-Item -LiteralPath $directory
                if (Test-Path -LiteralPath ($directory + '.meta') -PathType Leaf) {
                    Remove-Item -LiteralPath ($directory + '.meta')
                }
            }
            $directory = Split-Path -Parent $directory
        }
    }
    return $paths
}

function Sync-VectorUtility([string] $source, [string] $formatName) {
    $parts = @()
    $vectorPaths = @()
    foreach ($name in @('vector2', 'vector3', 'vector4')) {
        $path = Join-Path $source "$name.cs"
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing generated utility type: $path"
        }
        $content = [System.IO.File]::ReadAllText($path)
        # Imports must precede all namespace declarations in the combined file.
        $content = [regex]::Replace($content, '(?m)^using Luban(?:\.SimpleJSON)?;\r?\n', '')
        if ($content -notmatch "namespace EmberFrameWork\.Cfg\.$formatName\b") {
            throw "Unexpected namespace in utility type: $path"
        }
        $parts += $content.Trim()
        $vectorPaths += $path
    }
    $text = @'
// <auto-generated>Managed by Luban/Generate.ps1. Put hand-written extensions in another file.</auto-generated>
using Luban;
using Luban.SimpleJSON;

// BEGIN LUBAN VECTORS Json
// END LUBAN VECTORS Json

// BEGIN LUBAN VECTORS Bin
// END LUBAN VECTORS Bin
'@
    if (Test-Path -LiteralPath $utilityPath) {
        $text = [System.IO.File]::ReadAllText($utilityPath)
    }
    $begin = "// BEGIN LUBAN VECTORS $formatName"
    $end = "// END LUBAN VECTORS $formatName"
    $startIndex = $text.IndexOf($begin, [StringComparison]::Ordinal)
    $endIndex = $text.IndexOf($end, [StringComparison]::Ordinal)
    if ($startIndex -lt 0 -or $endIndex -le $startIndex) {
        throw "Missing generated section markers in $utilityPath. Preserve the Json and Bin markers."
    }
    $replacement = $begin + "`n" + ($parts -join "`n`n") + "`n" + $end
    $updated = $text.Substring(0, $startIndex) + $replacement + $text.Substring($endIndex + $end.Length)
    New-Item -ItemType Directory -Path (Split-Path -Parent $utilityPath) -Force | Out-Null
    if (-not (Test-Path -LiteralPath $utilityPath) -or $updated -cne $text) {
        [System.IO.File]::WriteAllText($utilityPath, $updated, [System.Text.UTF8Encoding]::new($false))
    }
    # The normal manifest sync removes the previous standalone vector scripts.
    foreach ($path in $vectorPaths) { Remove-Item -LiteralPath $path }
}

$previous = [pscustomobject]@{ code = @(); data = @() }
if (Test-Path -LiteralPath $manifestPath) {
    $previous = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}

$runRoot = Join-Path $cacheRoot ('run-' + [guid]::NewGuid().ToString('N'))
$stageCode = Join-Path $runRoot 'code'
$stageData = Join-Path $runRoot 'data'
New-Item -ItemType Directory -Path $stageCode, $stageData | Out-Null
try {
    & dotnet (Join-Path $toolRoot 'Luban.dll') --conf $config -t $target -c $codeTarget -d $dataTarget `
        -x "outputCodeDir=$stageCode" -x "outputDataDir=$stageData"
    if ($LASTEXITCODE -ne 0) { throw "Luban generation failed with exit code $LASTEXITCODE." }

    Sync-VectorUtility $stageCode $Format
    $code = @(Sync-GeneratedFiles $stageCode $codeDestination @($previous.code) '.cs' -flatten)
    $data = @(Sync-GeneratedFiles $stageData $dataDestination @($previous.data) $dataExtension)
    $manifest = [ordered]@{ code = $code; data = $data } | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($manifestPath, $manifest + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Generated $($code.Count) C# files and $($data.Count) $Format data files."
    Write-Host "Code: $codeDestination"
    Write-Host "Data: $dataDestination"
    Write-Host "Vector utility ($Format section): $utilityPath"
}
finally {
    $resolvedRunRoot = [System.IO.Path]::GetFullPath($runRoot)
    $allowedCacheRoot = [System.IO.Path]::GetFullPath($cacheRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedRunRoot.StartsWith($allowedCacheRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Temporary output is outside the Luban cache: $resolvedRunRoot"
    }
    if (Test-Path -LiteralPath $resolvedRunRoot) { Remove-Item -LiteralPath $resolvedRunRoot -Recurse -Force }
}
