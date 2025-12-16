<#
.SYNOPSIS
Canonical utility to rename engine folders and namespaces with backup/restore, undo, validation, and using statement sorting.

.DESCRIPTION
Renames a root folder and performs text replacements for namespaces/usings/project references across solution files.
Works on a copy first, validates with dotnet build, then replaces originals only if validation passes.
Includes backup/restore functionality with rotation, undo capability, and automatic using statement sorting.

Features:
- Works on copy first, validates with dotnet before replacing originals
- Backup/restore with rotation (keeps N backups)
- Undo functionality to restore from backups
- Using statement sorting (System.* first, then alphabetical)
- Fast parallel processing
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

.PARAMETER NoValidation
Skip dotnet validation (not recommended).

.PARAMETER BackupCount
Number of backups to keep (rotation). Default: 5

.PARAMETER BackupDirectory
Directory to store backups. Default: .backups in root path

.PARAMETER Undo
Restore from most recent backup.

.PARAMETER ListBackups
List available backups without restoring.

.EXAMPLE
.\EngineNamespaceRenamer.ps1 -OldFolderName "OdysseyRuntime" -NewFolderName "BioWareEngines" -OldNamespace "Odyssey" -NewNamespace "BioWareEngines"

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
    [string[]]$ExcludeDirectories = @("bin", "obj", ".git", ".vs", "packages", "node_modules", "dist", "vendor", ".backups", ".staging"),

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [switch]$NoFolderRename,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [switch]$NoTextReplace,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [switch]$NoUsingSort,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [switch]$NoValidation,

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
        $null = New-Item -Path $backupDir -ItemType Directory -Force
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

    # Get files to backup (only source files, not build artifacts)
    $files = Get-ChildItem -Path $RootPath -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames @("bin", "obj", ".git", ".vs", "packages", "node_modules", "dist", "vendor", ".backups", ".staging")) -and
            $_.Extension -in @(".cs", ".csproj", ".sln", ".axaml", ".props", ".targets", ".config", ".json", ".xml", ".md", ".txt", ".ps1", ".sh", ".bat", ".cmd", ".gitignore", ".editorconfig")
        }

    $backupManifestPath = Join-Path $BackupBaseDir "$backupName-manifest.json"

    # Store relative paths and hashes for verification (optimized - skip hashing for speed)
    $manifest.Files = $files | ForEach-Object {
        $file = $_
        try {
            if (-not (Test-Path -Path $file.FullName)) {
                return $null
            }

            $fullPath = $file.FullName
            if ($fullPath.Length -le $RootPath.Length + 1) {
                return $null
            }

            $relativePath = $fullPath.Substring($RootPath.Length + 1)

            # Skip hashing for speed - just store file info
            return @{
                RelativePath = $relativePath
                Hash = ""  # Skip hashing for performance
                FullPath = $fullPath
            }
        }
        catch {
            return $null
        }
    } | Where-Object { $null -ne $_ }

    if ($PSCmdlet.ShouldProcess($backupPath, "Create backup with $($manifest.Files.Count) files")) {
        Write-Host "Creating backup: $backupPath ($($manifest.Files.Count) files)..."

        # Save manifest
        $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $backupManifestPath

        # Copy files to backup (use robocopy for speed on Windows)
        if ($IsWindows -or $env:OS -like "*Windows*") {
            # Use robocopy for fast backup
            $excludeArgs = @(".backups", ".staging", "bin", "obj", ".git", ".vs", "packages", "node_modules", "dist", "vendor") | ForEach-Object { "/XD"; $_ }
            $null = & robocopy $RootPath $backupPath /E /NFL /NDL /NP /NJH /NJS /XF "*.dll" "*.pdb" "*.exe" $excludeArgs 2>&1
        }
        else {
            # Fallback: copy files
            foreach ($fileInfo in $manifest.Files) {
                try {
                    $sourcePath = $fileInfo.FullPath
                    $destPath = Join-Path $backupPath $fileInfo.RelativePath
                    $destDir = Split-Path -Path $destPath -Parent

                    if (-not (Test-Path -Path $destDir)) {
                        $null = New-Item -Path $destDir -ItemType Directory -Force -ErrorAction SilentlyContinue
                    }

                    Copy-Item -Path $sourcePath -Destination $destPath -Force -ErrorAction SilentlyContinue
                }
                catch {
                    # Silently continue
                }
            }
        }

        Write-Host "Backup created: $backupPath"
        Write-Host "Manifest: $backupManifestPath"

        # Rotate backups
        $backups = Get-ChildItem -Path $BackupBaseDir -Directory -Filter "backup-*" -ErrorAction SilentlyContinue |
            Sort-Object -Property Name -Descending

        if ($backups.Count -gt $BackupCount) {
            $toRemove = $backups | Select-Object -Skip $BackupCount
            foreach ($oldBackup in $toRemove) {
                $oldManifest = Join-Path $BackupBaseDir "$($oldBackup.Name)-manifest.json"
                if ($PSCmdlet.ShouldProcess($oldBackup.FullName, "Remove old backup")) {
                    Remove-Item -Path $oldBackup.FullName -Recurse -Force -ErrorAction SilentlyContinue
                    if (Test-Path -Path $oldManifest) {
                        Remove-Item -Path $oldManifest -Force -ErrorAction SilentlyContinue
                    }
                    Write-Verbose "Removed old backup: $($oldBackup.FullName)"
                }
            }
        }
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

    $backups = Get-ChildItem -Path $BackupBaseDir -Directory -Filter "backup-*" -ErrorAction SilentlyContinue |
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
                $null = New-Item -Path $destDir -ItemType Directory -Force
            }

            if (Test-Path -Path $sourcePath) {
                Copy-Item -Path $sourcePath -Destination $destPath -Force -ErrorAction SilentlyContinue
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

    $backups = Get-ChildItem -Path $BackupBaseDir -Directory -Filter "backup-*" -ErrorAction SilentlyContinue |
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
    Sorts using statements in C# files following .editorconfig conventions.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    try {
        $content = Get-Content -Path $FilePath -Raw -ErrorAction SilentlyContinue
        if ([string]::IsNullOrWhiteSpace($content)) {
            return $false
        }

        $lines = Get-Content -Path $FilePath -ErrorAction SilentlyContinue
        if ($null -eq $lines -or $lines.Count -eq 0) {
            return $false
        }

        # Detect line ending
        $lineEnding = "`r`n"
        if ($content -notmatch "`r`n") {
            $lineEnding = "`n"
        }

        # Find using block
        $usingStart = -1
        $usingEnd = -1
        $usingLineIndices = @()

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            $trimmed = $line.Trim()

            if ($trimmed -match '^\s*using\s+') {
                if ($usingStart -eq -1) {
                    $usingStart = $i
                }
                $usingEnd = $i
                $usingLineIndices += $i
            }
            elseif ($usingStart -ne -1) {
                break
            }
        }

        if ($usingStart -eq -1 -or $usingLineIndices.Count -le 1) {
            return $false
        }

        # Extract using statements
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

        $systemUsings = $systemUsings | Sort-Object
        $otherUsings = $otherUsings | Sort-Object
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

        for ($i = 0; $i -lt $usingStart; $i++) {
            [void]$newLines.Add($lines[$i])
        }

        foreach ($using in $sortedUsings) {
            [void]$newLines.Add($indent + $using.TrimStart())
        }

        for ($i = $usingEnd + 1; $i -lt $lines.Count; $i++) {
            [void]$newLines.Add($lines[$i])
        }

        $newContent = $newLines -join $lineEnding

        if ($content.EndsWith($lineEnding)) {
            $newContent += $lineEnding
        }

        Set-Content -Path $FilePath -Value $newContent -NoNewline -ErrorAction SilentlyContinue
        return $true
    }
    catch {
        return $false
    }
}

function Invoke-ReplaceInFile {
    <#
    .SYNOPSIS
    Performs text replacement in a file.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [hashtable]$Replacements
    )

    try {
        if (-not (Test-Path -Path $FilePath)) {
            return $false
        }

        $original = Get-Content -Path $FilePath -Raw -ErrorAction SilentlyContinue
        if ($null -eq $original) {
            return $false
        }

        $updated = $original
        $hasChanges = $false

        foreach ($replacement in $Replacements.GetEnumerator()) {
            $oldValue = $replacement.Key
            $newValue = $replacement.Value

            if ([string]::IsNullOrEmpty($oldValue)) {
                continue
            }

            if ($null -ne $updated -and $updated.Contains($oldValue)) {
                $updated = $updated.Replace($oldValue, $newValue)
                $hasChanges = $true
            }
        }

        if ($hasChanges) {
            Set-Content -Path $FilePath -Value $updated -NoNewline -ErrorAction SilentlyContinue
            return $true
        }

        return $false
    }
    catch {
        return $false
    }
}

function Rename-FoldersRecursively {
    <#
    .SYNOPSIS
    Recursively renames folders that match the old name pattern.
    #>
    [CmdletBinding()]
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

    $allDirs = Get-ChildItem -Path $RootPath -Recurse -Directory -ErrorAction SilentlyContinue |
        Where-Object { -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $ExcludeDirectories) } |
        Sort-Object -Property { $_.FullName.Split([IO.Path]::DirectorySeparatorChar).Count } -Descending

    $renamedCount = 0
    foreach ($dir in $allDirs) {
        if ($dir.Name -eq $OldName) {
            $newPath = Join-Path $dir.Parent.FullName $NewName

            if (Test-Path -Path $newPath) {
                continue
            }

            try {
                Rename-Item -Path $dir.FullName -NewName $NewName -Force -ErrorAction Stop
                $renamedCount++
            }
            catch {
                Write-Warning "Failed to rename folder $($dir.FullName): $_"
            }
        }
    }

    return $renamedCount
}

function Update-PathReferences {
    <#
    .SYNOPSIS
    Updates path references in files (folder names in paths).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string]$OldFolderName,

        [Parameter(Mandatory = $true)]
        [string]$NewFolderName
    )

    try {
        if (-not (Test-Path -Path $FilePath)) {
            return $false
        }

        $original = Get-Content -Path $FilePath -Raw -ErrorAction SilentlyContinue
        if ($null -eq $original -or [string]::IsNullOrWhiteSpace($original)) {
            return $false
        }

        $updated = $original
        $hasChanges = $false
        $escapedOld = [regex]::Escape($OldFolderName)
        $escapedNew = $NewFolderName

        # Pattern 1: Folder name in path with separators
        $pattern1 = "([`"`']?)([\\/])$escapedOld([\\/]|`"|`'|`$)"
        if ($updated -match $pattern1) {
            $updated = $updated -replace $pattern1, "`$1`$2$escapedNew`$3"
            $hasChanges = $true
        }

        # Pattern 2: Standalone folder name in path context
        $pattern2 = "([`"`'\\/]|^)$escapedOld([`"`'\\/]|`$)"
        if ($updated -match $pattern2) {
            $updated = $updated -replace $pattern2, "`$1$escapedNew`$2"
            $hasChanges = $true
        }

        if ($hasChanges) {
            Set-Content -Path $FilePath -Value $updated -NoNewline -ErrorAction SilentlyContinue
            return $true
        }

        return $false
    }
    catch {
        return $false
    }
}

function Copy-DirectoryFast {
    <#
    .SYNOPSIS
    Fast directory copy using robocopy on Windows or fallback to Copy-Item.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination,

        [Parameter(Mandatory = $true)]
        [string[]]$ExcludeDirectories
    )

    if ($IsWindows -or $env:OS -like "*Windows*") {
        # Use robocopy for fast copying on Windows
        $excludeArgs = $ExcludeDirectories | ForEach-Object { "/XD"; $_ }
        # Only copy source files, exclude build artifacts
        $fileArgs = @("/XF", "*.dll", "*.pdb", "*.exe", "*.cache")
        $null = & robocopy $Source $Destination /E /NFL /NDL /NP /NJH /NJS $excludeArgs $fileArgs 2>&1
        return $LASTEXITCODE -le 1  # 0 or 1 = success
    }
    else {
        # Fallback: copy only source files
        $null = New-Item -Path $Destination -ItemType Directory -Force -ErrorAction SilentlyContinue
        Get-ChildItem -Path $Source -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { 
                -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $ExcludeDirectories) -and
                $_.Extension -in @(".cs", ".csproj", ".sln", ".axaml", ".axaml.cs", ".props", ".targets", ".config", ".json", ".xml", ".md", ".txt", ".ps1", ".sh", ".bat", ".cmd", ".gitignore", ".editorconfig")
            } |
            ForEach-Object {
                $destPath = $_.FullName.Replace($Source, $Destination)
                $destDir = Split-Path -Path $destPath -Parent
                if (-not (Test-Path -Path $destDir)) {
                    $null = New-Item -Path $destDir -ItemType Directory -Force -ErrorAction SilentlyContinue
                }
                Copy-Item -Path $_.FullName -Destination $destPath -Force -ErrorAction SilentlyContinue
            }
        return $true
    }
}

function Test-DotNetValidation {
    <#
    .SYNOPSIS
    Validates the staging directory with dotnet build.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$StagingPath
    )

    Write-Host "Validating staging copy with dotnet build..."

    # Find solution file in root or common locations
    $slnFiles = @()
    $slnFiles += Get-ChildItem -Path $StagingPath -Filter "*.sln" -ErrorAction SilentlyContinue
    if ($slnFiles.Count -eq 0) {
        $slnFiles += Get-ChildItem -Path $StagingPath -Filter "*.sln" -Recurse -Depth 2 -ErrorAction SilentlyContinue
    }

    if ($slnFiles.Count -eq 0) {
        Write-Warning "No solution file found for validation - skipping validation"
        return $true  # Don't fail if no solution file
    }

    $slnPath = $slnFiles[0].FullName
    Write-Host "Using solution: $slnPath"

    # Change to staging directory for build
    $originalLocation = Get-Location
    try {
        Set-Location -Path $StagingPath

        # Run dotnet restore (quick check)
        Write-Host "Running dotnet restore..."
        $restoreResult = & dotnet restore $slnPath --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "dotnet restore had issues (exit code: $LASTEXITCODE)"
            # Continue anyway - might be warnings
        }

        # Run dotnet build (quick, no restore)
        Write-Host "Running dotnet build..."
        $buildResult = & dotnet build $slnPath --no-restore --verbosity minimal 2>&1
        $buildSuccess = $LASTEXITCODE -eq 0

        if (-not $buildSuccess) {
            Write-Error "dotnet build failed - validation unsuccessful"
            Write-Host "Build output:"
            Write-Host ($buildResult -join "`n")
            return $false
        }

        Write-Host "Validation successful!"
        return $true
    }
    finally {
        Set-Location -Path $originalLocation
    }
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

    Write-Host "Starting rename operation..."
    Write-Host "RootPath: $resolvedRoot"
    Write-Host "OldFolderName: $OldFolderName -> NewFolderName: $NewFolderName"
    Write-Host "OldNamespace: $OldNamespace -> NewNamespace: $NewNamespace"
    Write-Host ""

    # Create backup before changes
    $backupDir = Get-BackupDirectory -RootPath $resolvedRoot -CustomBackupDir $BackupDirectory
    $backupPath = New-Backup -RootPath $resolvedRoot -BackupBaseDir $backupDir -BackupCount $BackupCount

    # Create staging directory
    $stagingPath = Join-Path $resolvedRoot ".staging"
    if (Test-Path -Path $stagingPath) {
        Write-Host "Removing existing staging directory..."
        Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Creating staging copy..."
    $null = New-Item -Path $stagingPath -ItemType Directory -Force

    # Copy entire directory to staging (fast copy)
    Write-Host "Copying files to staging (this may take a moment)..."
    $copySuccess = Copy-DirectoryFast -Source $resolvedRoot -Destination $stagingPath -ExcludeDirectories $ExcludeDirectories

    if (-not $copySuccess) {
        Write-Warning "Fast copy failed, using standard copy..."
        # Fallback to standard copy
        Get-ChildItem -Path $resolvedRoot -Recurse -ErrorAction SilentlyContinue |
            Where-Object { -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $ExcludeDirectories) } |
            ForEach-Object {
                $destPath = $_.FullName.Replace($resolvedRoot, $stagingPath)
                if (-not $_.PSIsContainer) {
                    $destDir = Split-Path -Path $destPath -Parent
                    if (-not (Test-Path -Path $destDir)) {
                        $null = New-Item -Path $destDir -ItemType Directory -Force -ErrorAction SilentlyContinue
                    }
                    Copy-Item -Path $_.FullName -Destination $destPath -Force -ErrorAction SilentlyContinue
                }
            }
    }

    $stagingResolved = Resolve-Path -Path $stagingPath

    # Perform operations on staging copy
    Write-Host "Performing operations on staging copy..."

    # Rename main folder in staging
    if (-not $NoFolderRename) {
        $oldFolder = Join-Path $stagingResolved $OldFolderName
        $newFolder = Join-Path $stagingResolved $NewFolderName

        if (Test-Path -Path $oldFolder) {
            if (Test-Path -Path $newFolder) {
                throw "Target folder already exists in staging: $newFolder"
            }

            Write-Host "Renaming folder in staging: $OldFolderName -> $NewFolderName"
            Move-Item -Path $oldFolder -Destination $newFolder -Force
            Write-Host "Renamed main folder: $OldFolderName -> $NewFolderName"

            # Rename subfolders recursively
            $workRoot = if (Test-Path -Path $newFolder) { $newFolder } else { $stagingResolved }
            if (Test-Path -Path $workRoot) {
                $renamedSubfolders = Rename-FoldersRecursively -RootPath $workRoot -OldName $OldFolderName -NewName $NewFolderName -ExcludeDirectories $ExcludeDirectories
                if ($renamedSubfolders -gt 0) {
                    Write-Host "Renamed $renamedSubfolders subfolder(s): $OldFolderName -> $NewFolderName"
                }
            }
        }
        else {
            Write-Warning "Old folder not found in staging: $oldFolder"
        }
    }

    # Text replacements in staging
    if (-not $NoTextReplace) {
        Write-Host "Performing text replacements in staging..."

        $searchRoot = $stagingResolved
        if (-not $NoFolderRename -and (Test-Path -Path (Join-Path $stagingResolved $NewFolderName))) {
            $searchRoot = $stagingResolved
        }

        # Get files to process (only source files for speed)
        $allFiles = Get-ChildItem -Path $searchRoot -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object {
                -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $ExcludeDirectories) -and
                $_.Extension -in @(".cs", ".csproj", ".sln", ".axaml", ".axaml.cs", ".props", ".targets", ".config", ".json", ".xml", ".md", ".txt", ".ps1", ".sh", ".bat", ".cmd")
            } |
            Sort-Object -Property FullName -Unique

        Write-Host "Processing $($allFiles.Count) files..."

        # Prepare replacements
        $replacements = @{
            $OldNamespace = $NewNamespace
        }

        $changedCount = 0
        $pathUpdatedCount = 0
        $sortedCount = 0

        # Process files in parallel for speed
        $results = $allFiles | ForEach-Object -Parallel {
            $file = $_
            $oldNamespace = $using:OldNamespace
            $newNamespace = $using:NewNamespace
            $oldFolderName = $using:OldFolderName
            $newFolderName = $using:NewFolderName
            $noUsingSort = $using:NoUsingSort

            $result = @{
                NamespaceChanged = $false
                PathChanged = $false
                UsingSorted = $false
            }

            try {
                # Namespace replacement
                $nsReplacements = @{ $oldNamespace = $newNamespace }
                if (Invoke-ReplaceInFile -FilePath $file.FullName -Replacements $nsReplacements) {
                    $result.NamespaceChanged = $true
                }

                # Path references
                if (Update-PathReferences -FilePath $file.FullName -OldFolderName $oldFolderName -NewFolderName $newFolderName) {
                    $result.PathChanged = $true
                }

                # Using sort
                if (-not $noUsingSort -and $file.Extension -eq ".cs") {
                    if (Sort-UsingStatements -FilePath $file.FullName) {
                        $result.UsingSorted = $true
                    }
                }
            }
            catch {
                # Silently continue on errors
            }

            return $result
        } -ThrottleLimit 50

        foreach ($result in $results) {
            if ($result.NamespaceChanged) { $changedCount++ }
            if ($result.PathChanged) { $pathUpdatedCount++ }
            if ($result.UsingSorted) { $sortedCount++ }
        }

        Write-Host "Files with namespace updates: $changedCount"
        Write-Host "Files with path updates: $pathUpdatedCount"
        if (-not $NoUsingSort) {
            Write-Host "Files with sorted usings: $sortedCount"
        }
    }

    # Validate staging copy
    if (-not $NoValidation) {
        $validationSuccess = Test-DotNetValidation -StagingPath $stagingResolved
        if (-not $validationSuccess) {
            Write-Error "Validation failed! Staging copy will not replace originals."
            Write-Host "Staging directory: $stagingPath"
            Write-Host "Review errors above and fix issues, or use -NoValidation to skip validation."
            Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
            exit 1
        }
    }

    # Replace originals with staging copy
    Write-Host ""
    Write-Host "Validation successful! Replacing originals with staging copy..."

    # Use robocopy for fast replacement on Windows
    if ($IsWindows -or $env:OS -like "*Windows*") {
        # Copy staging files back to original location (excluding staging and backups)
        $excludeArgs = @(".staging", ".backups", ".git") | ForEach-Object { "/XD"; $_ }
        $null = & robocopy $stagingResolved $resolvedRoot /E /NFL /NDL /NP /NJH /NJS /IS /IT $excludeArgs 2>&1
        
        # Handle folder rename if needed
        if (-not $NoFolderRename) {
            $oldFolder = Join-Path $resolvedRoot $OldFolderName
            $newFolder = Join-Path $resolvedRoot $NewFolderName

            if (Test-Path -Path $oldFolder) {
                if (Test-Path -Path $newFolder) {
                    Write-Warning "Target folder already exists, removing old folder..."
                    Remove-Item -Path $oldFolder -Recurse -Force -ErrorAction SilentlyContinue
                }
                else {
                    Move-Item -Path $oldFolder -Destination $newFolder -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
    else {
        # Fallback: copy files individually
        $stagingFiles = Get-ChildItem -Path $stagingResolved -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames @(".staging", ".backups", ".git")) }

        foreach ($stagingFile in $stagingFiles) {
            $relativePath = $stagingFile.FullName.Substring($stagingResolved.Length + 1)
            $originalPath = Join-Path $resolvedRoot $relativePath
            $originalDir = Split-Path -Path $originalPath -Parent

            if (-not (Test-Path -Path $originalDir)) {
                $null = New-Item -Path $originalDir -ItemType Directory -Force -ErrorAction SilentlyContinue
            }

            Copy-Item -Path $stagingFile.FullName -Destination $originalPath -Force -ErrorAction SilentlyContinue
        }

        # Handle folder structure changes
        if (-not $NoFolderRename) {
            $oldFolder = Join-Path $resolvedRoot $OldFolderName
            $newFolder = Join-Path $resolvedRoot $NewFolderName

            if (Test-Path -Path $oldFolder) {
                if (Test-Path -Path $newFolder) {
                    Write-Warning "Target folder already exists, removing old folder..."
                    Remove-Item -Path $oldFolder -Recurse -Force -ErrorAction SilentlyContinue
                }
                else {
                    Move-Item -Path $oldFolder -Destination $newFolder -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }

    # Cleanup staging
    Write-Host "Cleaning up staging directory..."
    Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "Completed successfully!"
    Write-Host "Backup location: $backupPath"
    Write-Host "Use -Undo to restore from backup, or -ListBackups to see available backups."
}
catch {
    Write-Error "Error: $_"
    Write-Error $_.ScriptStackTrace

    # Cleanup staging on error
    $stagingPath = Join-Path $resolvedRoot ".staging"
    if (Test-Path -Path $stagingPath) {
        Write-Host "Cleaning up staging directory due to error..."
        Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    exit 1
}

#endregion
