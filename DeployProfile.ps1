param(
    [Parameter(Mandatory = $true)][string]$Profile,
    [Parameter(Mandatory = $true)][string]$Package,
    [Parameter(Mandatory = $true)][string]$Dll
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Profile -PathType Container)) {
    throw 'The selected r2modman profile does not exist. No fallback profile will be used.'
}
$profileRoot = [IO.Path]::GetFullPath($Profile).TrimEnd('\')
$dataRoot = [IO.Directory]::GetParent([IO.Directory]::GetParent($profileRoot).FullName).FullName
$registryPath = Join-Path $profileRoot 'mods.yml'
if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) { throw 'The selected profile registry is missing.' }
$blocking = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(valheim|r2modman.*|Thunderstore.*)$' })
if ($blocking.Count) { throw 'Close Valheim and r2modman (and Thunderstore Mod Manager if running) before changing the profile.' }
$manifest = Get-Content -LiteralPath (Join-Path $Package 'manifest.json') -Raw | ConvertFrom-Json
$version = [Version]$manifest.version_number
if ($version.Build -lt 0) { throw 'Package version must have major, minor and patch components.' }
$assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($Dll).Version
if ($assemblyVersion.Major -ne $version.Major -or $assemblyVersion.Minor -ne $version.Minor -or $assemblyVersion.Build -ne $version.Build) {
    throw 'Built DLL version differs from package manifest.'
}
$modsPath = Join-Path $Profile 'mods.yml'
$updated = $null
$enabled = $true
if (Test-Path -LiteralPath $modsPath) {
    $raw = [System.IO.File]::ReadAllText($modsPath)
    $entries = @([regex]::Matches($raw, '(?ms)^- manifestVersion:.*?(?=^- manifestVersion:|\z)') |
        Where-Object { $_.Value -match '(?m)^  name: Slikfoul-AchievementTracker\r?$' })
    if ($entries.Count -gt 1) { throw 'Multiple AchievementTracker entries in the profile.' }
    if ($entries.Count -ne 1) { throw 'The canonical AchievementTracker registration is missing; leave the profile untouched.' }
    if ($entries.Count -eq 1) {
        $entry = $entries[0]
        $enabled = $entry.Value -notmatch '(?m)^  enabled: false\r?$'
        $installed = [regex]::Match($entry.Value, '(?m)^  versionNumber:\r?\n    major: (\d+)\r?\n    minor: (\d+)\r?\n    patch: (\d+)')
        if (-not $installed.Success) { throw 'Cannot determine installed version; leave the profile untouched.' }
        $installedVersion = [version]('{0}.{1}.{2}' -f $installed.Groups[1].Value,$installed.Groups[2].Value,$installed.Groups[3].Value)
        if ($installedVersion -gt $version) { throw 'Refusing to downgrade a newer local version.' }
        $pattern = '(?m)^  versionNumber:\r?\n    major: \d+\r?\n    minor: \d+\r?\n    patch: \d+'
        if (-not [regex]::IsMatch($entry.Value, $pattern)) { throw 'Mod manager version entry missing.' }
        $replacement = "  versionNumber:`r`n    major: $($version.Major)`r`n    minor: $($version.Minor)`r`n    patch: $($version.Build)"
        $newEntry = [regex]::Replace($entry.Value, $pattern, $replacement)
        if ($newEntry -match '(?m)^  onlineSource:') {
            $newEntry = [regex]::Replace($newEntry, '(?m)^  onlineSource:.*\r?$', '  onlineSource: true')
        } else { $newEntry = $newEntry.TrimEnd()+"`r`n  onlineSource: true`r`n" }
        $managerWebsite = 'https://thunderstore.io/c/valheim/p/Slikfoul/AchievementTracker/'
        if ($newEntry -match '(?m)^  websiteUrl:') {
            $newEntry = [regex]::Replace($newEntry, '(?m)^  websiteUrl:.*\r?$', "  websiteUrl: '$managerWebsite'")
        } else { $newEntry = $newEntry.TrimEnd()+"`r`n  websiteUrl: '$managerWebsite'`r`n" }
        $descriptionPattern = '(?ms)^  description: >-\r?\n.*?(?=^  gameVersion:)'
        if ([regex]::IsMatch($newEntry, $descriptionPattern)) {
            $description = "  description: >-`r`n    $($manifest.description)`r`n"
            $newEntry = [regex]::Replace($newEntry, $descriptionPattern, $description)
        }
        $updated = $raw.Substring(0, $entry.Index) + $newEntry + $raw.Substring($entry.Index + $entry.Length)
        if ($updated -ne $raw) {
            $backup = "$modsPath.before-AchievementTracker-$($manifest.version_number).bak"
            if (-not (Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $modsPath -Destination $backup }
        }
    }
}

$destination = Join-Path $Profile 'BepInEx\plugins\Slikfoul-AchievementTracker'
$cacheDirectory = [IO.Path]::GetFullPath((Join-Path $dataRoot ('cache\Slikfoul-AchievementTracker\' + $manifest.version_number)))
if (-not $cacheDirectory.StartsWith($dataRoot.TrimEnd('\') + '\cache\Slikfoul-AchievementTracker\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The cache target escaped the selected package directory.'
}
$cacheMetadataPath = Join-Path $cacheDirectory 'mm_v2_manifest.json'
if (Test-Path -LiteralPath $cacheMetadataPath) {
    $cacheMetadata = Get-Content -LiteralPath $cacheMetadataPath -Raw | ConvertFrom-Json
    if ($cacheMetadata.name -cne 'Slikfoul-AchievementTracker') { throw 'Cache identity mismatch.' }
} else {
    $cacheMetadata = [pscustomobject][ordered]@{
        manifestVersion = 1
        name = 'Slikfoul-AchievementTracker'
        authorName = 'Slikfoul'
        displayName = 'AchievementTracker'
        gameVersion = '0'
        networkMode = 'both'
        packageType = 'other'
        installMode = 'managed'
        installedAtTime = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        loaders = @()
        incompatibilities = @()
        optionalDependencies = @()
        trustedPackage = ($entry.Value -match '(?m)^  trustedPackage: true\r?$')
    }
}
foreach ($field in @{
    websiteUrl = $managerWebsite
    description = $manifest.description
    dependencies = @($manifest.dependencies)
    versionNumber = @{ major = $version.Major; minor = $version.Minor; patch = $version.Build }
    enabled = $enabled
    icon = (Join-Path $cacheDirectory 'icon.png')
    onlineSource = $true
}.GetEnumerator()) {
    $cacheMetadata | Add-Member -NotePropertyName $field.Key -NotePropertyValue $field.Value -Force
}
$backupDirectory = Join-Path $PSScriptRoot ('backups\before-profile-update-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
Copy-Item -LiteralPath $modsPath -Destination (Join-Path $backupDirectory 'mods.yml')
if (Test-Path -LiteralPath $cacheDirectory) {
    Copy-Item -LiteralPath $cacheDirectory -Destination (Join-Path $backupDirectory 'previous-cache') -Recurse
}
if (Test-Path -LiteralPath $destination) {
    [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
    Copy-Item -LiteralPath $destination -Destination (Join-Path $backupDirectory 'previous-managed') -Recurse
}
[System.IO.Directory]::CreateDirectory($destination) | Out-Null
$dllName = if ($enabled) { 'AchievementTracker.dll' } else { 'AchievementTracker.dll.old' }
Copy-Item -LiteralPath $Dll -Destination (Join-Path $destination $dllName) -Force
$oppositeName = if ($enabled) { 'AchievementTracker.dll.old' } else { 'AchievementTracker.dll' }
if (Test-Path -LiteralPath (Join-Path $destination $oppositeName)) { Remove-Item -LiteralPath (Join-Path $destination $oppositeName) }
foreach ($file in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
    Copy-Item -LiteralPath (Join-Path $Package $file) -Destination (Join-Path $destination $file) -Force
}
[IO.Directory]::CreateDirectory($cacheDirectory) | Out-Null
Copy-Item -LiteralPath $Dll -Destination (Join-Path $cacheDirectory 'AchievementTracker.dll') -Force
foreach ($file in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
    Copy-Item -LiteralPath (Join-Path $Package $file) -Destination (Join-Path $cacheDirectory $file) -Force
}
[IO.File]::WriteAllText($cacheMetadataPath, ($cacheMetadata | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding $false))
if ($null -ne $updated -and $updated -ne $raw) {
    [System.IO.File]::WriteAllText($modsPath, $updated, (New-Object System.Text.UTF8Encoding($false)))
}
Write-Output "AchievementTracker $($manifest.version_number) deployed to $destination"
