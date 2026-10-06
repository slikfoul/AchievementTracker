param(
    [Parameter(Mandatory = $true)][string]$Profile,
    [Parameter(Mandatory = $true)][string]$Package,
    [Parameter(Mandatory = $true)][string]$Dll
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath (Join-Path $Package 'manifest.json') -Raw | ConvertFrom-Json
$version = [Version]$manifest.version_number
if ($version.Build -lt 0) { throw 'Package version must have major, minor and patch components.' }
$assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($Dll).Version
if ($assemblyVersion.Major -ne $version.Major -or $assemblyVersion.Minor -ne $version.Minor -or $assemblyVersion.Build -ne $version.Build) {
    throw 'Built DLL version differs from package manifest.'
}
$modsPath = Join-Path $Profile 'mods.yml'
$updated = $null
if (Test-Path -LiteralPath $modsPath) {
    $raw = [System.IO.File]::ReadAllText($modsPath)
    $entries = @([regex]::Matches($raw, '(?ms)^- manifestVersion:.*?(?=^- manifestVersion:|\z)') |
        Where-Object { $_.Value -match '(?m)^  name: Slikfoul-AchievementTracker\r?$' })
    if ($entries.Count -gt 1) { throw 'Multiple AchievementTracker entries in the profile.' }
    if ($entries.Count -eq 1) {
        $entry = $entries[0]
        $pattern = '(?m)^  versionNumber:\r?\n    major: \d+\r?\n    minor: \d+\r?\n    patch: \d+'
        if (-not [regex]::IsMatch($entry.Value, $pattern)) { throw 'Mod manager version entry missing.' }
        $replacement = "  versionNumber:`r`n    major: $($version.Major)`r`n    minor: $($version.Minor)`r`n    patch: $($version.Build)"
        $newEntry = [regex]::Replace($entry.Value, $pattern, $replacement)
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
[System.IO.Directory]::CreateDirectory($destination) | Out-Null
Copy-Item -LiteralPath $Dll -Destination (Join-Path $destination 'AchievementTracker.dll') -Force
foreach ($file in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
    Copy-Item -LiteralPath (Join-Path $Package $file) -Destination (Join-Path $destination $file) -Force
}
if ($null -ne $updated -and $updated -ne $raw) {
    [System.IO.File]::WriteAllText($modsPath, $updated, (New-Object System.Text.UTF8Encoding($false)))
}
Write-Output "AchievementTracker $($manifest.version_number) deployed to $destination"
