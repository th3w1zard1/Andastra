<#
.SYNOPSIS
Canonical utility to rename engine folders and namespaces with backup/restore, undo, and using statement sorting.

.DESCRIPTION
Renames a root folder and performs text replacements for namespaces/usings/project references across solution files.
Includes backup/restore functionality with rotation, undo capability, and automatic using statement sorting
following .editorconfig conventions (System directives first, no separation between groups).

Features:
- Backup/restore with rotation (keeps N backups)
- Undo functionality to restore from backups
- Using statement sorting (System.* first, then alphabetical)
- Dry-run support with -WhatIf
- C# 7.3 compatible processing
- Canonical PowerShell workflows

.PARAMETER RootPath
Root directory path. Defaults to current location.

.PARAMETER OldFolderName
Name of folder to rename (e.g., "OdysseyRuntime").

.PARAMETER NewFolderName
New folder name (e.g., "BioWareEngines").

.PARAMETER OldNamespace
Old namespace prefix to replace (e.g., "Odyssey").

.PARAMETER NewNamespace
New namespace prefix (e.g., "BioWareEngines").

.PARAMETER IncludeFiles
File patterns to process. Default: *.cs, *.csproj, *.sln, *.axaml, *.axaml.cs

.PARAMETER ExcludeDirectories
Directory names to exclude. Default: bin, obj, .git, .vs, packages, node_modules, dist, vendor

.PARAMETER NoFolderRename
Skip folder renaming, only perform text replacements.

.PARAMETER NoTextReplace
Skip text replacements, only rename folder.

.PARAMETER NoUsingSort
Skip using statement sorting.

.PARAMETER BackupCount
Number of backups to keep (rotation). Default: 5

.PARAMETER BackupDirectory
Directory to store backups. Default: .backups in root path

.PARAMETER Undo
Restore from most recent backup.

.PARAMETER ListBackups
List available backups without restoring.

.EXAMPLE
.\EngineNamespaceRenamer.ps1 -OldFolderName "OdysseyRuntime" -NewFolderName "BioWareEngines" -OldNamespace "Odyssey" -NewNamespace "BioWareEngines" -WhatIf

.EXAMPLE
.\EngineNamespaceRenamer.ps1 -OldFolderName "OldEngine" -NewFolderName "NewEngine" -OldNamespace "OldEngine" -NewNamespace "NewEngine" -Verbose

.EXAMPLE
.\EngineNamespaceRenamer.ps1 -Undo

.EXAMPLE
.\EngineNamespaceRenamer.ps1 -ListBackups
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High', DefaultParameterSetName = 'Rename')]
param(
    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [Parameter(Mandatory = $false, ParameterSetName = 'Undo')]
    [Parameter(Mandatory = $false, ParameterSetName = 'ListBackups')]
    [string]$RootPath = (Get-Location).Path,

    [Parameter(Mandatory = $true, ParameterSetName = 'Rename')]
    [string]$OldFolderName,

    [Parameter(Mandatory = $true, ParameterSetName = 'Rename')]
    [string]$NewFolderName,

    [Parameter(Mandatory = $true, ParameterSetName = 'Rename')]
    [string]$OldNamespace,

    [Parameter(Mandatory = $true, ParameterSetName = 'Rename')]
    [string]$NewNamespace,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [string[]]$IncludeFiles = @("*.cs", "*.csproj", "*.sln", "*.axaml", "*.axaml.cs", "*.props", "*.targets", "*.config", "*.json", "*.xml", "*.md", "*.txt", "*.ps1", "*.sh", "*.bat", "*.cmd"),

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [string[]]$ExcludeDirectories = @("bin", "obj", ".git", ".vs", "packages", "node_modules", "dist", "vendor", ".backups"),

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [switch]$NoFolderRename,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [switch]$NoTextReplace,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [switch]$NoUsingSort,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [int]$BackupCount = 5,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [Parameter(Mandatory = $false, ParameterSetName = 'Undo')]
    [string]$BackupDirectory,

    [Parameter(Mandatory = $true, ParameterSetName = 'Undo')]
    [switch]$Undo,

    [Parameter(Mandatory = $true, ParameterSetName = 'ListBackups')]
    [switch]$ListBackups
)

#region Helper Functions

function Test-IsExcludedDirectory {
    <#
    .SYNOPSIS
    Tests if a path contains any excluded directory names.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string[]]$ExcludedNames
    )

    foreach ($name in $ExcludedNames) {
        if ($Path -like "*\${name}\*" -or $Path -like "*\${name}") {
            return $true
        }
    }

    return $false
}

function Get-BackupDirectory {
    <#
    .SYNOPSIS
    Gets or creates the backup directory.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $false)]
        [string]$CustomBackupDir
    )

    if ($CustomBackupDir) {
        $backupDir = $CustomBackupDir
    }
    else {
        $backupDir = Join-Path $RootPath ".backups"
    }

    if (-not (Test-Path -Path $backupDir)) {
        if ($PSCmdlet.ShouldProcess($backupDir, "Create backup directory")) {
            $null = New-Item -Path $backupDir -ItemType Directory -Force
        }
    }

    return $backupDir
}

function New-Backup {
    <#
    .SYNOPSIS
    Creates a timestamped backup of the root directory.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$BackupBaseDir,

        [Parameter(Mandatory = $true)]
        [int]$BackupCount
    )

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupName = "backup-$timestamp"
    $backupPath = Join-Path $BackupBaseDir $backupName

    Write-Verbose "Preparing backup: $backupPath"

    # Create backup manifest
    $manifest = @{
        Timestamp = $timestamp
        RootPath = $RootPath
        BackupPath = $backupPath
        Files = @()
    }

    # Copy files that match include patterns
    $files = Get-ChildItem -Path $RootPath -Recurse -File |
        Where-Object { -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames @("bin", "obj", ".git", ".vs", "packages", "node_modules", "dist", "vendor", ".backups")) }

    $backupManifestPath = Join-Path $BackupBaseDir "$backupName-manifest.json"

    # Store relative paths and hashes for verification
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($RootPath.Length + 1)
        $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash
        $manifest.Files += @{
            RelativePath = $relativePath
            Hash = $hash
            FullPath = $file.FullName
        }
    }

    if ($PSCmdlet.ShouldProcess($backupPath, "Create backup with $($manifest.Files.Count) files")) {
        Write-Verbose "Creating backup: $backupPath"

        # Save manifest
        $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $backupManifestPath

        # Copy files to backup
        foreach ($fileInfo in $manifest.Files) {
            $sourcePath = $fileInfo.FullPath
            $destPath = Join-Path $backupPath $fileInfo.RelativePath
            $destDir = Split-Path -Path $destPath -Parent

            if (-not (Test-Path -Path $destDir)) {
                New-Item -Path $destDir -ItemType Directory -Force | Out-Null
            }

            Copy-Item -Path $sourcePath -Destination $destPath -Force
        }

        Write-Host "Backup created: $backupPath"
        Write-Host "Manifest: $backupManifestPath"

        # Rotate backups
        $backups = Get-ChildItem -Path $BackupBaseDir -Directory -Filter "backup-*" |
            Sort-Object -Property Name -Descending

        if ($backups.Count -gt $BackupCount) {
            $toRemove = $backups | Select-Object -Skip $BackupCount
            foreach ($oldBackup in $toRemove) {
                $oldManifest = Join-Path $BackupBaseDir "$($oldBackup.Name)-manifest.json"
                if ($PSCmdlet.ShouldProcess($oldBackup.FullName, "Remove old backup")) {
                    Remove-Item -Path $oldBackup.FullName -Recurse -Force
                    if (Test-Path -Path $oldManifest) {
                        Remove-Item -Path $oldManifest -Force
                    }
                    Write-Verbose "Removed old backup: $($oldBackup.FullName)"
                }
            }
        }
    }
    else {
        Write-Verbose "WhatIf: Would create backup with $($manifest.Files.Count) files"
    }

    return $backupPath
}

function Restore-Backup {
    <#
    .SYNOPSIS
    Restores from the most recent backup.
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$BackupBaseDir
    )

    $backups = Get-ChildItem -Path $BackupBaseDir -Directory -Filter "backup-*" |
        Sort-Object -Property Name -Descending

    if ($backups.Count -eq 0) {
        Write-Error "No backups found in $BackupBaseDir"
        return
    }

    $latestBackup = $backups[0]
    $manifestPath = Join-Path $BackupBaseDir "$($latestBackup.Name)-manifest.json"

    if (-not (Test-Path -Path $manifestPath)) {
        Write-Error "Manifest not found: $manifestPath"
        return
    }

    $manifest = Get-Content -Path $manifestPath | ConvertFrom-Json

    if ($PSCmdlet.ShouldProcess($RootPath, "Restore from backup $($latestBackup.Name)")) {
        Write-Host "Restoring from backup: $($latestBackup.FullName)"

        foreach ($fileInfo in $manifest.Files) {
            $sourcePath = Join-Path $latestBackup.FullName $fileInfo.RelativePath
            $destPath = Join-Path $RootPath $fileInfo.RelativePath
            $destDir = Split-Path -Path $destPath -Parent

            if (-not (Test-Path -Path $destDir)) {
                New-Item -Path $destDir -ItemType Directory -Force | Out-Null
            }

            if (Test-Path -Path $sourcePath) {
                Copy-Item -Path $sourcePath -Destination $destPath -Force
                Write-Verbose "Restored: $($fileInfo.RelativePath)"
            }
            else {
                Write-Warning "Source file not found in backup: $sourcePath"
            }
        }

        Write-Host "Restore completed from: $($latestBackup.Name)"
    }
}

function Get-BackupList {
    <#
    .SYNOPSIS
    Lists available backups.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BackupBaseDir
    )

    if (-not (Test-Path -Path $BackupBaseDir)) {
        Write-Host "No backup directory found: $BackupBaseDir"
        return
    }

    $backups = Get-ChildItem -Path $BackupBaseDir -Directory -Filter "backup-*" |
        Sort-Object -Property Name -Descending

    if ($backups.Count -eq 0) {
        Write-Host "No backups found."
        return
    }

    Write-Host "Available backups:"
    Write-Host ""

    foreach ($backup in $backups) {
        $manifestPath = Join-Path $BackupBaseDir "$($backup.Name)-manifest.json"
        if (Test-Path -Path $manifestPath) {
            $manifest = Get-Content -Path $manifestPath | ConvertFrom-Json
            $fileCount = $manifest.Files.Count
            Write-Host "  $($backup.Name) - $fileCount files - $($manifest.Timestamp)"
        }
        else {
            Write-Host "  $($backup.Name) - (manifest missing)"
        }
    }
}

function Sort-UsingStatements {
    <#
    .SYNOPSIS
    Sorts using statements in C# files following .editorconfig conventions:
    - System.* directives first
    - Then other namespaces alphabetically
    - No separation between groups (dotnet_separate_import_directive_groups = false)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    try {
        $content = Get-Content -Path $FilePath -Raw
        if ([string]::IsNullOrWhiteSpace($content)) {
            return $false
        }

        $lines = Get-Content -Path $FilePath
        if ($lines.Count -eq 0) {
            return $false
        }

        # Detect line ending
        $lineEnding = "`r`n"
        if ($content -notmatch "`r`n") {
            $lineEnding = "`n"
        }

        # Find using block (from first "using" to first non-using/non-empty line or namespace)
        $usingStart = -1
        $usingEnd = -1
        $usingLineIndices = @()

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            $trimmed = $line.Trim()

            # Skip file header comments
            if ($trimmed -match '^/\*' -or $trimmed -match '^//' -or $trimmed -eq '') {
                continue
            }

            if ($trimmed -match '^\s*using\s+') {
                if ($usingStart -eq -1) {
                    $usingStart = $i
                }
                $usingEnd = $i
                $usingLineIndices += $i
            }
            elseif ($usingStart -ne -1) {
                # Found end of using block
                break
            }
        }

        if ($usingStart -eq -1 -or $usingLineIndices.Count -le 1) {
            return $false
        }

        # Extract using statements with their original indentation
        $usingStatements = @()
        foreach ($idx in $usingLineIndices) {
            $line = $lines[$idx]
            $trimmed = $line.Trim()
            if ($trimmed -match '^\s*using\s+') {
                $usingStatements += $trimmed
            }
        }

        if ($usingStatements.Count -le 1) {
            return $false
        }

        # Sort: System.* first, then alphabetical
        $systemUsings = @()
        $otherUsings = @()

        foreach ($using in $usingStatements) {
            if ($using -match '^\s*using\s+System\.') {
                $systemUsings += $using
            }
            else {
                $otherUsings += $using
            }
        }

        # Sort each group alphabetically
        $systemUsings = $systemUsings | Sort-Object
        $otherUsings = $otherUsings | Sort-Object

        # Combine (no blank line between groups per .editorconfig)
        $sortedUsings = $systemUsings + $otherUsings

        # Check if already sorted
        $isAlreadySorted = $true
        for ($i = 0; $i -lt $usingStatements.Count; $i++) {
            if ($usingStatements[$i] -ne $sortedUsings[$i]) {
                $isAlreadySorted = $false
                break
            }
        }

        if ($isAlreadySorted) {
            return $false
        }

        # Get indentation from first using statement
        $firstUsingLine = $lines[$usingStart]
        $indentMatch = $firstUsingLine -match '^(\s*)'
        $indent = if ($indentMatch) { $matches[1] } else { "" }

        # Rebuild file content
        $newLines = New-Object System.Collections.ArrayList

        # Lines before using block
        for ($i = 0; $i -lt $usingStart; $i++) {
            [void]$newLines.Add($lines[$i])
        }

        # Sorted using statements (preserve indentation)
        foreach ($using in $sortedUsings) {
            [void]$newLines.Add($indent + $using.TrimStart())
        }

        # Lines after using block
        for ($i = $usingEnd + 1; $i -lt $lines.Count; $i++) {
            [void]$newLines.Add($lines[$i])
        }

        # Write back with original line endings
        $newContent = $newLines -join $lineEnding
        
        # Preserve trailing newline if original had one
        if ($content.EndsWith($lineEnding)) {
            $newContent += $lineEnding
        }

        Set-Content -Path $FilePath -Value $newContent -NoNewline
        return $true
    }
    catch {
        Write-Warning "Failed to sort using statements in $FilePath : $_"
        return $false
    }
}

function Invoke-ReplaceInFile {
    <#
    .SYNOPSIS
    Performs text replacement in a file with ShouldProcess support.
    Supports multiple replacements and path-aware replacements.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [hashtable]$Replacements
    )

    $original = Get-Content -Path $FilePath -Raw
    $updated = $original

    $hasChanges = $false
    foreach ($replacement in $Replacements.GetEnumerator()) {
        $oldValue = $replacement.Key
        $newValue = $replacement.Value
        if ($updated.Contains($oldValue)) {
            $updated = $updated.Replace($oldValue, $newValue)
            $hasChanges = $true
        }
    }

    if ($hasChanges) {
        if ($PSCmdlet.ShouldProcess($FilePath, "Apply text replacements")) {
            Set-Content -Path $FilePath -Value $updated -NoNewline
        }
        return $true
    }

    return $false
}

function Rename-FoldersRecursively {
    <#
    .SYNOPSIS
    Recursively renames folders that match the old name pattern.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$OldName,

        [Parameter(Mandatory = $true)]
        [string]$NewName,

        [Parameter(Mandatory = $true)]
        [string[]]$ExcludeDirectories
    )

    # Get all directories, sorted by depth (deepest first) to avoid path conflicts
    $allDirs = Get-ChildItem -Path $RootPath -Recurse -Directory |
        Where-Object { -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $ExcludeDirectories) } |
        Sort-Object -Property { $_.FullName.Split([IO.Path]::DirectorySeparatorChar).Count } -Descending

    $renamedCount = 0
    foreach ($dir in $allDirs) {
        if ($dir.Name -eq $OldName) {
            $newPath = Join-Path $dir.Parent.FullName $NewName

            if (Test-Path -Path $newPath) {
                Write-Warning "Target folder already exists: $newPath"
                continue
            }

            if ($PSCmdlet.ShouldProcess($dir.FullName, "Rename folder to $NewName")) {
                Rename-Item -Path $dir.FullName -NewName $NewName -Force
                $renamedCount++
                Write-Verbose "Renamed folder: $($dir.FullName) -> $newPath"
            }
        }
    }

    return $renamedCount
}

function Update-PathReferences {
    <#
    .SYNOPSIS
    Updates path references in files (folder names in paths).
    Handles both forward slashes and backslashes, and relative/absolute paths.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string]$OldFolderName,

        [Parameter(Mandatory = $true)]
        [string]$NewFolderName
    )

    $original = Get-Content -Path $FilePath -Raw
    $updated = $original

    # Replace folder name in paths (handle both / and \)
    $patterns = @(
        "\$OldFolderName\",      # Backslash: \OldName\
        "/$OldFolderName/",      # Forward slash: /OldName/
        "\$OldFolderName`"",     # Backslash at end: \OldName"
        "/$OldFolderName`"",     # Forward slash at end: /OldName"
        "`"$OldFolderName\",     # Quote backslash: "OldName\
        "`"$OldFolderName/",     # Quote forward slash: "OldName/
        "`"$OldFolderName`"",   # Quoted: "OldName"
        "`'$OldFolderName`'"    # Single quoted: 'OldName'
    )

    $replacements = @(
        "\$NewFolderName\",
        "/$NewFolderName/",
        "\$NewFolderName`"",
        "/$NewFolderName`"",
        "`"$NewFolderName\",
        "`"$NewFolderName/",
        "`"$NewFolderName`"",
        "`'$NewFolderName`'"
    )

    $hasChanges = $false
    for ($i = 0; $i -lt $patterns.Count; $i++) {
        if ($updated -match [regex]::Escape($patterns[$i])) {
            $updated = $updated -replace [regex]::Escape($patterns[$i]), $replacements[$i]
            $hasChanges = $true
        }
    }

    # Also handle folder name as standalone word in paths (with word boundaries)
    $standalonePattern = "\b$([regex]::Escape($OldFolderName))\b"
    if ($updated -match $standalonePattern) {
        # Only replace if it looks like a path component (preceded/followed by path separators or quotes)
        $pathPattern = "([`"`'\\/]|^)$([regex]::Escape($OldFolderName))([`"`'\\/]|$)"
        if ($updated -match $pathPattern) {
            $updated = $updated -replace $pathPattern, "`$1$NewFolderName`$3"
            $hasChanges = $true
        }
    }

    if ($hasChanges) {
        if ($PSCmdlet.ShouldProcess($FilePath, "Update path references: $OldFolderName -> $NewFolderName")) {
            Set-Content -Path $FilePath -Value $updated -NoNewline
        }
        return $true
    }

    return $false
}

#endregion

#region Main Logic

$ErrorActionPreference = 'Stop'

try {
    $resolvedRoot = Resolve-Path -Path $RootPath -ErrorAction Stop

    # Handle undo
    if ($Undo) {
        $backupDir = Get-BackupDirectory -RootPath $resolvedRoot -CustomBackupDir $BackupDirectory
        Restore-Backup -RootPath $resolvedRoot -BackupBaseDir $backupDir
        exit 0
    }

    # Handle list backups
    if ($ListBackups) {
        $backupDir = Get-BackupDirectory -RootPath $resolvedRoot -CustomBackupDir $BackupDirectory
        Get-BackupList -BackupBaseDir $backupDir
        exit 0
    }

    # Validate parameters
    if ([string]::IsNullOrWhiteSpace($OldFolderName) -or [string]::IsNullOrWhiteSpace($NewFolderName)) {
        throw "OldFolderName and NewFolderName are required"
    }

    if ([string]::IsNullOrWhiteSpace($OldNamespace) -or [string]::IsNullOrWhiteSpace($NewNamespace)) {
        throw "OldNamespace and NewNamespace are required"
    }

    Write-Verbose "RootPath: $resolvedRoot"
    Write-Verbose "OldFolderName: $OldFolderName"
    Write-Verbose "NewFolderName: $NewFolderName"
    Write-Verbose "OldNamespace: $OldNamespace"
    Write-Verbose "NewNamespace: $NewNamespace"
    Write-Verbose "Includes: $($IncludeFiles -join ', ')"
    Write-Verbose "Excludes: $($ExcludeDirectories -join ', ')"

    # Create backup before changes
    $backupDir = Get-BackupDirectory -RootPath $resolvedRoot -CustomBackupDir $BackupDirectory
    $backupPath = New-Backup -RootPath $resolvedRoot -BackupBaseDir $backupDir -BackupCount $BackupCount

    # Rename main folder
    if (-not $NoFolderRename) {
        $oldFolder = Join-Path $resolvedRoot $OldFolderName
        $newFolder = Join-Path $resolvedRoot $NewFolderName

        if (Test-Path -Path $oldFolder) {
            if (Test-Path -Path $newFolder) {
                throw "Target folder already exists: $newFolder"
            }

            if ($PSCmdlet.ShouldProcess($oldFolder, "Rename to $newFolder")) {
                Move-Item -Path $oldFolder -Destination $newFolder -Force
                Write-Host "Renamed main folder: $OldFolderName -> $NewFolderName"
            }
        }
        else {
            Write-Warning "Old folder not found: $oldFolder"
        }

        # Rename subfolders recursively (after main folder rename, work in new location)
        $workRoot = if (Test-Path -Path $newFolder) { $newFolder } else { $resolvedRoot }
        $renamedSubfolders = Rename-FoldersRecursively -RootPath $workRoot -OldName $OldFolderName -NewName $NewFolderName -ExcludeDirectories $ExcludeDirectories
        if ($renamedSubfolders -gt 0) {
            Write-Host "Renamed $renamedSubfolders subfolder(s): $OldFolderName -> $NewFolderName"
        }
    }

    # Text replacements
    if (-not $NoTextReplace) {
        # Get all files matching include patterns
        $files = Get-ChildItem -Path $resolvedRoot -Recurse -File -Include $IncludeFiles |
            Where-Object { -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $ExcludeDirectories) }

        # Also get .sln, .csproj, .props, .targets files for path updates (even if not in IncludeFiles)
        $pathFiles = Get-ChildItem -Path $resolvedRoot -Recurse -File -Include "*.sln", "*.csproj", "*.props", "*.targets", "*.config", "*.json", "*.xml" |
            Where-Object { -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $ExcludeDirectories) }

        # Combine and deduplicate
        $allFiles = ($files + $pathFiles) | Sort-Object -Property FullName -Unique

        $changedCount = 0
        $pathUpdatedCount = 0
        $sortedCount = 0

        foreach ($file in $allFiles) {
            $didChange = $false
            $didPathChange = $false

            # Prepare replacements hashtable
            $replacements = @{
                $OldNamespace = $NewNamespace
            }

            # Perform namespace replacement
            $didChange = Invoke-ReplaceInFile -FilePath $file.FullName -Replacements $replacements
            if ($didChange) {
                $changedCount++
                Write-Verbose "Updated namespace: $($file.FullName)"
            }

            # Update path references (folder names in paths) for relevant file types
            if ($file.Extension -in @(".sln", ".csproj", ".props", ".targets", ".config", ".json", ".xml", ".md", ".txt", ".ps1", ".sh", ".bat", ".cmd")) {
                $didPathChange = Update-PathReferences -FilePath $file.FullName -OldFolderName $OldFolderName -NewFolderName $NewFolderName
                if ($didPathChange) {
                    $pathUpdatedCount++
                    Write-Verbose "Updated paths: $($file.FullName)"
                }
            }

            # Sort using statements for .cs files
            if (-not $NoUsingSort -and $file.Extension -eq ".cs") {
                if (Sort-UsingStatements -FilePath $file.FullName) {
                    $sortedCount++
                    Write-Verbose "Sorted usings: $($file.FullName)"
                }
            }
        }

        Write-Host "Files with namespace updates: $changedCount"
        Write-Host "Files with path updates: $pathUpdatedCount"
        if (-not $NoUsingSort) {
            Write-Host "Files with sorted usings: $sortedCount"
        }
    }

    Write-Host ""
    Write-Host "Completed successfully."
    Write-Host "Backup location: $backupPath"
    Write-Host "Use -Undo to restore from backup, or -ListBackups to see available backups."
}
catch {
    Write-Error "Error: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}

#endregion
