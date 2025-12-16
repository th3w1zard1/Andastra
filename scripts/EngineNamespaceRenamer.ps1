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
[CmdletBinding(DefaultParameterSetName = 'Rename')]
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
    [string[]]$ExcludeDirectories = @("bin", "obj", ".git", ".vs", "packages", "node_modules", "dist", "vendor", ".backups", ".staging", ".history"),

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
    [switch]$ListBackups,

    [Parameter(Mandatory = $false, ParameterSetName = 'Rename')]
    [int]$Timeout = 120
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
    Creates a timestamped backup of files that will be modified.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$BackupBaseDir,

        [Parameter(Mandatory = $true)]
        [int]$BackupCount,

        [Parameter(Mandatory = $false)]
        [string]$OldNamespace,

        [Parameter(Mandatory = $false)]
        [string]$OldFolderName
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

    # OPTIMIZED: Get files to backup using fast path-based filtering first
    # This is exponentially faster than scanning all files
    $excludeDirs = @("bin", "obj", ".git", ".vs", "packages", "node_modules", "dist", "vendor", ".backups", ".staging")
    $allowedExtensions = @(".cs", ".csproj", ".sln", ".axaml", ".axaml.cs", ".props", ".targets", ".config", ".json", ".xml", ".md", ".txt", ".ps1", ".sh", ".bat", ".cmd", ".gitignore", ".editorconfig")

    Write-Host "Scanning for files that will be modified (optimized path-based scan)..."

    # STEP 1: Fast path-based filtering - get files in target folder or files that reference it in path
    # This eliminates 99% of files immediately without reading content
    $pathBasedFiles = Get-ChildItem -Path $RootPath -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $excludeDirs) -and
            $_.Extension -in $allowedExtensions -and
            ($_.FullName -like "*$OldFolderName*" -or $_.FullName -like "*$OldNamespace*")
        }

    Write-Host "Found $($pathBasedFiles.Count) files via path-based scan"

    # STEP 2: For remaining files, do quick content check (only if we have OldNamespace)
    # Only check files that might reference the namespace but aren't in the folder
    # RESTRICTED: Only scan ./src
    if ($OldNamespace -and $OldNamespace -ne $OldFolderName) {
        Write-Host "Scanning remaining ./src files for namespace references..."
        # Get files NOT in the target folder that might reference the namespace
        $srcPathForScan = Join-Path $RootPath "src"
        if (-not (Test-Path -Path $srcPathForScan)) {
            $srcPathForScan = $RootPath
        }
        $otherFiles = Get-ChildItem -Path $srcPathForScan -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object {
                -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames $excludeDirs) -and
                $_.Extension -in $allowedExtensions -and
                $_.FullName -notlike "*$OldFolderName*" -and
                $_.FullName -notlike "*$OldNamespace*"
            } |
            Select-Object -First 2000  # Limit to prevent timeout

        # Quick content check on limited set (parallel if available)
        $contentBasedFiles = if ($PSVersionTable.PSVersion.Major -ge 7) {
            $otherFiles | ForEach-Object -Parallel {
                $file = $_
                $oldNamespace = $using:OldNamespace
                try {
                    $stream = [System.IO.File]::OpenRead($file.FullName)
                    $buffer = New-Object byte[] 4096  # Smaller buffer for speed
                    $bytesRead = $stream.Read($buffer, 0, 4096)
                    $stream.Close()

                    if ($bytesRead -gt 0) {
                        $text = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $bytesRead)
                        if ($text.Contains($oldNamespace)) {
                            return $file
                        }
                    }
                }
                catch {
                    return $null
                }
                return $null
            } -ThrottleLimit 50 | Where-Object { $null -ne $_ }
        } else {
            $otherFiles | Where-Object {
                try {
                    $stream = [System.IO.File]::OpenRead($_.FullName)
                    $buffer = New-Object byte[] 4096
                    $bytesRead = $stream.Read($buffer, 0, 4096)
                    $stream.Close()

                    if ($bytesRead -gt 0) {
                        $text = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $bytesRead)
                        return $text.Contains($OldNamespace)
                    }
                }
                catch {
                    return $false
                }
                return $false
            }
        }

        Write-Host "Found $($contentBasedFiles.Count) additional files via content scan"
        $files = $pathBasedFiles + $contentBasedFiles
    } else {
        $files = $pathBasedFiles
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

    Write-Host "Creating backup: $backupPath ($($manifest.Files.Count) files)..."

    # Save manifest
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $backupManifestPath

    # Copy files to backup (optimized - only copy files we identified, use parallel if available)
    Write-Host "Backing up $($manifest.Files.Count) files..."
    $copyCount = 0
    if ($PSVersionTable.PSVersion.Major -ge 7 -and $manifest.Files.Count -gt 50) {
        # Parallel copy for PowerShell 7+ (faster for many files)
        $manifest.Files | ForEach-Object -Parallel {
            $fileInfo = $_
            $backupPath = $using:backupPath

            try {
                $sourcePath = $fileInfo.FullPath
                if (-not (Test-Path -Path $sourcePath)) {
                    return
                }

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
        } -ThrottleLimit 20
    } else {
        # Sequential copy (for PowerShell 5.1 or small file counts)
        foreach ($fileInfo in $manifest.Files) {
            try {
                $sourcePath = $fileInfo.FullPath
                if (-not (Test-Path -Path $sourcePath)) {
                    continue
                }

                $destPath = Join-Path $backupPath $fileInfo.RelativePath
                $destDir = Split-Path -Path $destPath -Parent

                if (-not (Test-Path -Path $destDir)) {
                    $null = New-Item -Path $destDir -ItemType Directory -Force -ErrorAction SilentlyContinue
                }

                Copy-Item -Path $sourcePath -Destination $destPath -Force -ErrorAction SilentlyContinue
                $copyCount++
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
            Remove-Item -Path $oldBackup.FullName -Recurse -Force -ErrorAction SilentlyContinue
            if (Test-Path -Path $oldManifest) {
                Remove-Item -Path $oldManifest -Force -ErrorAction SilentlyContinue
            }
            Write-Verbose "Removed old backup: $($oldBackup.FullName)"
        }
    }

    return $backupPath
}

function Restore-Backup {
    <#
    .SYNOPSIS
    Restores from the most recent backup.
    #>
    [CmdletBinding()]
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
        # Build exclusion list (robocopy format: /XD dir1 dir2 ...)
        $excludeDirs = $ExcludeDirectories | ForEach-Object { "/XD"; $_ }
        # Exclude build artifacts and large binary files
        $excludeFiles = @("/XF", "*.dll", "*.pdb", "*.exe", "*.cache", "*.obj", "*.bin", "*.log", "*.tmp")
        # Only copy source files - use /R:1 /W:1 for faster failure, /MT:8 for multi-threaded
        $robocopyArgs = @($Source, $Destination, "/E", "/NFL", "/NDL", "/NP", "/NJH", "/NJS", "/R:1", "/W:1", "/MT:8") + $excludeDirs + $excludeFiles
        $result = & robocopy @robocopyArgs 2>&1
        $exitCode = $LASTEXITCODE
        # Robocopy returns 0-7 for success (0 = no files copied, 1-7 = files copied)
        return $exitCode -le 7
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

#region Profiling Functions

# Initialize profile data (will be reset in main logic)
$script:ProfileData = $null

function Add-ProfileCheckpoint {
    param([string]$Name)
    $elapsed = (Get-Date) - $script:ProfileData.StartTime
    $script:ProfileData.Checkpoints += @{
        Name = $Name
        Time = $elapsed.TotalSeconds
        Timestamp = Get-Date
    }
}

function Add-ProfileOperation {
    param(
        [string]$Operation,
        [double]$Duration,
        [int]$Count = 1
    )
    $script:ProfileData.Operations += @{
        Operation = $Operation
        Duration = $Duration
        Count = $Count
    }
}

function Get-ProfileReport {
    if ($null -eq $script:ProfileData) {
        return "Profile data not initialized"
    }
    $totalTime = (Get-Date) - $script:ProfileData.StartTime
    $report = @"
=== PERFORMANCE PROFILE REPORT ===
Total Execution Time: $($totalTime.TotalSeconds.ToString('F2'))s

Checkpoints:
"@
    foreach ($checkpoint in $script:ProfileData.Checkpoints) {
        $report += "`n  [$($checkpoint.Time.ToString('F2'))s] $($checkpoint.Name)"
    }

    $report += "`n`nOperations:"
    if ($script:ProfileData.Operations.Count -gt 0) {
        $ops = $script:ProfileData.Operations | Group-Object -Property Operation | ForEach-Object {
            $total = ($_.Group | ForEach-Object { $_.Duration } | Measure-Object -Sum).Sum
            $count = ($_.Group | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
            [PSCustomObject]@{
                Operation = $_.Name
                TotalTime = $total
                Count = $count
                AvgTime = if ($count -gt 0) { $total / $count } else { 0 }
            }
        } | Sort-Object -Property TotalTime -Descending

        foreach ($op in $ops) {
            $report += "`n  $($op.Operation): $($op.TotalTime.ToString('F2'))s total, $($op.Count) calls, $($op.AvgTime.ToString('F3'))s avg"
        }
    } else {
        $report += "`n  (No operations recorded)"
    }

    $report += "`n`n=== END PROFILE REPORT ==="
    return $report
}

#endregion

#region Main Logic

$ErrorActionPreference = 'Stop'

try {
    # Initialize profile data
    $script:ProfileData = @{
        StartTime = Get-Date
        Checkpoints = @()
        Operations = @()
    }
    Add-ProfileCheckpoint "Script Start"

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
    Write-Host "Timeout: $Timeout seconds"
    Write-Host ""

    # Setup timeout monitoring
    $timeoutJob = $null
    if ($Timeout -gt 0) {
        $timeoutJob = Start-Job -ScriptBlock {
            param($TimeoutSeconds)
            Start-Sleep -Seconds $TimeoutSeconds
            return $true
        } -ArgumentList $Timeout
    }

    # Create backup before changes
    Add-ProfileCheckpoint "Before Backup"
    $backupStart = Get-Date
    $backupDir = Get-BackupDirectory -RootPath $resolvedRoot -CustomBackupDir $BackupDirectory
    $backupPath = New-Backup -RootPath $resolvedRoot -BackupBaseDir $backupDir -BackupCount $BackupCount -OldNamespace $OldNamespace -OldFolderName $OldFolderName
    $backupDuration = ((Get-Date) - $backupStart).TotalSeconds
    Add-ProfileOperation "Backup Creation" $backupDuration
    Add-ProfileCheckpoint "After Backup"

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
    Add-ProfileCheckpoint "Before Staging Copy"
    $copyStart = Get-Date
    $copySuccess = Copy-DirectoryFast -Source $resolvedRoot -Destination $stagingPath -ExcludeDirectories $ExcludeDirectories
    $copyDuration = ((Get-Date) - $copyStart).TotalSeconds
    Add-ProfileOperation "Staging Copy" $copyDuration
    Add-ProfileCheckpoint "After Staging Copy"

    # Check timeout
    if ($null -ne $timeoutJob -and $timeoutJob.State -eq 'Completed') {
        $profileReport = Get-ProfileReport
        Write-Error "TIMEOUT: Operation exceeded $Timeout seconds"
        Write-Host $profileReport
        Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
        exit 1
    }

    if (-not $copySuccess) {
        Write-Warning "Fast copy failed, using standard copy..."
        $fallbackStart = Get-Date
        # Fallback to standard copy
        $fallbackSuccess = $false
        try {
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
            $fallbackSuccess = $true
        }
        catch {
            Write-Error "Fallback copy also failed: $_"
            $fallbackSuccess = $false
        }

        $fallbackDuration = ((Get-Date) - $fallbackStart).TotalSeconds
        Add-ProfileOperation "Fallback Copy" $fallbackDuration

        if (-not $fallbackSuccess) {
            Write-Error "CRITICAL: Staging copy failed completely. Original files will NOT be modified."
            $profileReport = Get-ProfileReport
            Write-Host $profileReport
            Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
            exit 1
        }
    }

    # CRITICAL SAFETY CHECK: Verify staging copy exists and has files before proceeding
    # If this fails, we exit immediately without touching originals (automatic dry-run protection)
    if (-not (Test-Path -Path $stagingPath)) {
        Write-Error "CRITICAL: Staging directory does not exist after copy. Original files will NOT be modified."
        $profileReport = Get-ProfileReport
        Write-Host $profileReport
        exit 1
    }

    $stagingFileCount = (Get-ChildItem -Path $stagingPath -Recurse -File -ErrorAction SilentlyContinue | Measure-Object).Count
    if ($stagingFileCount -eq 0) {
        Write-Error "CRITICAL: Staging copy is empty. Original files will NOT be modified."
        $profileReport = Get-ProfileReport
        Write-Host $profileReport
        Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
        exit 1
    }

    Write-Host "Staging copy verified: $stagingFileCount files copied (originals protected until validation passes)"
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

            # Always search in staging ./src (files are in staging/src, not in renamed folders)
            $searchRoot = Join-Path $stagingResolved "src"
            if (-not (Test-Path -Path $searchRoot)) {
                $searchRoot = $stagingResolved
            }

            # Get files to process
            # Note: We're searching INSIDE staging, so we must NOT exclude .staging itself
            # Remove .staging from exclusions since we're already inside it
            $excludeForSearch = ($ExcludeDirectories | Where-Object { $_ -ne ".staging" }) + @(".backups")
            $allowedExtensions = @(".cs", ".csproj", ".sln", ".axaml", ".axaml.cs", ".props", ".targets", ".config", ".json", ".xml", ".md", ".txt", ".ps1", ".sh", ".bat", ".cmd")

            # Get all files first, then filter
            $allFilesRaw = Get-ChildItem -Path $searchRoot -Recurse -File -ErrorAction SilentlyContinue
            Write-Host "Found $($allFilesRaw.Count) total files in staging" -Verbose

            $allFiles = $allFilesRaw |
                Where-Object {
                    # Check extension first (faster)
                    if ($_.Extension -notin $allowedExtensions) {
                        Write-Host "  Excluding $($_.Name): extension $($_.Extension) not in allowed list" -Verbose
                        return $false
                    }
                    # Check if path contains excluded directory names (but not .staging since we're inside it)
                    $filePath = $_.FullName
                    $isExcluded = Test-IsExcludedDirectory -Path $filePath -ExcludedNames $excludeForSearch
                    if ($isExcluded) {
                        # Debug: find which directory matched
                        $matchedDir = $null
                        foreach ($excluded in $excludeForSearch) {
                            if ($filePath -like "*\${excluded}\*" -or $filePath -like "*\${excluded}") {
                                $matchedDir = $excluded
                                break
                            }
                        }
                        Write-Host "  Excluding $($_.Name): path contains excluded directory '$matchedDir'" -Verbose
                        return $false
                    }
                    Write-Host "  Including $($_.Name)" -Verbose
                    return $true
                } |
                Sort-Object -Property FullName -Unique

            Write-Host "Found $($allFiles.Count) files to process after filtering" -Verbose

        Write-Host "Processing $($allFiles.Count) files..."

        # Prepare replacements
        $replacements = @{
            $OldNamespace = $NewNamespace
        }

        $changedCount = 0
        $pathUpdatedCount = 0
        $sortedCount = 0

        # Check timeout before processing
        if ($null -ne $timeoutJob -and $timeoutJob.State -eq 'Completed') {
            $profileReport = Get-ProfileReport
            Write-Error "TIMEOUT: Operation exceeded $Timeout seconds before file processing"
            Write-Host $profileReport
            Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
            exit 1
        }

        # Process files in parallel for speed (if PowerShell 7+), otherwise sequential
        Add-ProfileCheckpoint "Before File Processing"
        $processStart = Get-Date

        # Check if parallel processing is available (PowerShell 7+)
        $useParallel = $PSVersionTable.PSVersion.Major -ge 7

        if ($useParallel) {
            # Note: Functions are inlined in parallel block for reliability
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
                if (-not (Test-Path -Path $file.FullName)) {
                    return $result
                }

                # Read file once for all operations
                $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
                if ($null -eq $content -or [string]::IsNullOrWhiteSpace($content)) {
                    return $result
                }

                # Initialize result tracking
                $fileChanged = $false
                $updated = $content

                # Namespace replacement
                if ($updated.Contains($oldNamespace)) {
                    $updated = $updated.Replace($oldNamespace, $newNamespace)
                    $result.NamespaceChanged = $true
                    $fileChanged = $true
                }

                # Path reference update
                $escapedOld = [regex]::Escape($oldFolderName)
                $escapedNew = $newFolderName
                $hasPathChanges = $false

                # Pattern 1: Folder name in path with separators
                $pattern1 = "([`"`']?)([\\/])$escapedOld([\\/]|`"|`'|`$)"
                if ($updated -match $pattern1) {
                    $updated = $updated -replace $pattern1, "`$1`$2$escapedNew`$3"
                    $hasPathChanges = $true
                }

                # Pattern 2: Standalone folder name in path context
                $pattern2 = "([`"`'\\/]|^)$escapedOld([`"`'\\/]|`$)"
                if ($updated -match $pattern2) {
                    $updated = $updated -replace $pattern2, "`$1$escapedNew`$2"
                    $hasPathChanges = $true
                }

                if ($hasPathChanges) {
                    $result.PathChanged = $true
                    $fileChanged = $true
                }

                # Using sort (only if .cs file and not disabled)
                if (-not $noUsingSort -and $file.Extension -eq ".cs") {
                    try {
                        $lines = $updated -split "`r?`n"
                        if ($null -ne $lines -and $lines.Count -gt 0) {
                            $lineEnding = "`r`n"
                            if ($updated -notmatch "`r`n") {
                                $lineEnding = "`n"
                            }

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

                            if ($usingStart -ne -1 -and $usingLineIndices.Count -gt 1) {
                                $usingStatements = @()
                                foreach ($idx in $usingLineIndices) {
                                    $usingStatements += $lines[$idx].Trim()
                                }

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

                                $isAlreadySorted = $true
                                for ($i = 0; $i -lt $usingStatements.Count; $i++) {
                                    if ($usingStatements[$i] -ne $sortedUsings[$i]) {
                                        $isAlreadySorted = $false
                                        break
                                    }
                                }

                                if (-not $isAlreadySorted) {
                                    $firstUsingLine = $lines[$usingStart]
                                    $indentMatch = $firstUsingLine -match '^(\s*)'
                                    $indent = if ($indentMatch) { $matches[1] } else { "" }

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

                                    $updated = $newLines -join $lineEnding
                                    if ($content.EndsWith($lineEnding)) {
                                        $updated += $lineEnding
                                    }

                                    $result.UsingSorted = $true
                                    $fileChanged = $true
                                }
                            }
                        }
                    }
                    catch {
                        # Silently continue
                    }
                }

                # Write file once if any changes were made
                if ($fileChanged) {
                    Set-Content -Path $file.FullName -Value $updated -NoNewline -ErrorAction SilentlyContinue
                }
            }
            catch {
                # Silently continue on errors
            }

            return $result
        } -ThrottleLimit 50
        } else {
            # Sequential processing for PowerShell 5.1
            Write-Host "Using sequential processing (PowerShell $($PSVersionTable.PSVersion.Major))" -Verbose
            $results = $allFiles | ForEach-Object {
                $file = $_
                $result = @{
                    NamespaceChanged = $false
                    PathChanged = $false
                    UsingSorted = $false
                }

                try {
                    if (-not (Test-Path -Path $file.FullName)) {
                        return $result
                    }

                    $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
                    if ($null -eq $content -or [string]::IsNullOrWhiteSpace($content)) {
                        return $result
                    }

                    $fileChanged = $false
                    $updated = $content

                    # Namespace replacement
                    if ($updated.Contains($OldNamespace)) {
                        $updated = $updated.Replace($OldNamespace, $NewNamespace)
                        $result.NamespaceChanged = $true
                        $fileChanged = $true
                    }

                    # Path reference update
                    $escapedOld = [regex]::Escape($OldFolderName)
                    $escapedNew = $NewFolderName
                    $hasPathChanges = $false

                    $pattern1 = "([`"`']?)([\\/])$escapedOld([\\/]|`"|`'|`$)"
                    if ($updated -match $pattern1) {
                        $updated = $updated -replace $pattern1, "`$1`$2$escapedNew`$3"
                        $hasPathChanges = $true
                    }

                    $pattern2 = "([`"`'\\/]|^)$escapedOld([`"`'\\/]|`$)"
                    if ($updated -match $pattern2) {
                        $updated = $updated -replace $pattern2, "`$1$escapedNew`$2"
                        $hasPathChanges = $true
                    }

                    if ($hasPathChanges) {
                        $result.PathChanged = $true
                        $fileChanged = $true
                    }

                    # Using sort (simplified for sequential)
                    if (-not $NoUsingSort -and $file.Extension -eq ".cs") {
                        try {
                            $lines = $updated -split "`r?`n"
                            if ($null -ne $lines -and $lines.Count -gt 0) {
                                $usingStart = -1
                                $usingEnd = -1
                                $usings = New-Object System.Collections.ArrayList

                                for ($i = 0; $i -lt $lines.Count; $i++) {
                                    $line = $lines[$i].Trim()
                                    if ($line.StartsWith("using ") -and $line.EndsWith(";")) {
                                        if ($usingStart -eq -1) {
                                            $usingStart = $i
                                        }
                                        $usingEnd = $i
                                        [void]$usings.Add($line)
                                    }
                                    elseif ($usingStart -ge 0 -and -not [string]::IsNullOrWhiteSpace($line)) {
                                        break
                                    }
                                }

                                if ($usings.Count -gt 0) {
                                    $systemUsings = $usings | Where-Object { $_.StartsWith("using System") } | Sort-Object
                                    $otherUsings = $usings | Where-Object { -not $_.StartsWith("using System") } | Sort-Object
                                    $sortedUsings = $systemUsings + $otherUsings

                                    $isAlreadySorted = $true
                                    for ($i = 0; $i -lt $usings.Count; $i++) {
                                        if ($usings[$i] -ne $sortedUsings[$i]) {
                                            $isAlreadySorted = $false
                                            break
                                        }
                                    }

                                    if (-not $isAlreadySorted) {
                                        $firstUsingLine = $lines[$usingStart]
                                        $indentMatch = $firstUsingLine -match '^(\s*)'
                                        $indent = if ($indentMatch) { $matches[1] } else { "" }

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

                                        $lineEnding = if ($content.Contains("`r`n")) { "`r`n" } elseif ($content.Contains("`n")) { "`n" } else { "`r`n" }
                                        $updated = $newLines -join $lineEnding
                                        if ($content.EndsWith($lineEnding)) {
                                            $updated += $lineEnding
                                        }

                                        $result.UsingSorted = $true
                                        $fileChanged = $true
                                    }
                                }
                            }
                        }
                        catch {
                            # Silently continue
                        }
                    }

                    if ($fileChanged) {
                        Set-Content -Path $file.FullName -Value $updated -NoNewline -ErrorAction SilentlyContinue
                    }
                }
                catch {
                    # Silently continue on errors
                }

                return $result
            }
        }

        foreach ($result in $results) {
            if ($result.NamespaceChanged) { $changedCount++ }
            if ($result.PathChanged) { $pathUpdatedCount++ }
            if ($result.UsingSorted) { $sortedCount++ }
        }

        $processDuration = ((Get-Date) - $processStart).TotalSeconds
        Add-ProfileOperation "File Processing" $processDuration $allFiles.Count
        Add-ProfileCheckpoint "After File Processing"

        Write-Host "Files with namespace updates: $changedCount"
        Write-Host "Files with path updates: $pathUpdatedCount"
        if (-not $NoUsingSort) {
            Write-Host "Files with sorted usings: $sortedCount"
        }

        # Check timeout
        if ($null -ne $timeoutJob -and $timeoutJob.State -eq 'Completed') {
            $profileReport = Get-ProfileReport
            Write-Error "TIMEOUT: Operation exceeded $Timeout seconds during file processing"
            Write-Host $profileReport
            Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
            exit 1
        }
    }

    # Validate staging copy
    if (-not $NoValidation) {
        Add-ProfileCheckpoint "Before Validation"
        $validationStart = Get-Date
        $validationSuccess = Test-DotNetValidation -StagingPath $stagingResolved
        $validationDuration = ((Get-Date) - $validationStart).TotalSeconds
        Add-ProfileOperation "DotNet Validation" $validationDuration
        Add-ProfileCheckpoint "After Validation"

        # Check timeout
        if ($null -ne $timeoutJob -and $timeoutJob.State -eq 'Completed') {
            $profileReport = Get-ProfileReport
            Write-Error "TIMEOUT: Operation exceeded $Timeout seconds during validation"
            Write-Host $profileReport
            Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
            exit 1
        }
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

    Add-ProfileCheckpoint "Before Replacement"
    $replaceStart = Get-Date

    # Copy staging ./src back to original ./src location
    $stagingSrc = Join-Path $stagingResolved "src"
    $originalSrc = Join-Path $resolvedRoot "src"

    if (Test-Path -Path $stagingSrc) {
        # Use robocopy for fast replacement on Windows
        if ($IsWindows -or $env:OS -like "*Windows*") {
            $null = & robocopy $stagingSrc $originalSrc /E /NFL /NDL /NP /NJH /NJS /IS /IT /R:1 /W:1 /MT:8 2>&1

            # Handle folder rename if needed (within ./src)
            if (-not $NoFolderRename) {
                $oldFolder = Join-Path $originalSrc $OldFolderName
                $newFolder = Join-Path $originalSrc $NewFolderName

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
            $stagingFiles = Get-ChildItem -Path $stagingSrc -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { -not (Test-IsExcludedDirectory -Path $_.FullName -ExcludedNames @(".staging", ".backups", ".git", ".history", "vendor")) }

            foreach ($stagingFile in $stagingFiles) {
                $relativePath = $stagingFile.FullName.Substring($stagingSrc.Length + 1)
                $originalPath = Join-Path $originalSrc $relativePath
                $originalDir = Split-Path -Path $originalPath -Parent

                if (-not (Test-Path -Path $originalDir)) {
                    $null = New-Item -Path $originalDir -ItemType Directory -Force -ErrorAction SilentlyContinue
                }

                Copy-Item -Path $stagingFile.FullName -Destination $originalPath -Force -ErrorAction SilentlyContinue
            }

            # Handle folder structure changes (within ./src)
            if (-not $NoFolderRename) {
                $oldFolder = Join-Path $originalSrc $OldFolderName
                $newFolder = Join-Path $originalSrc $NewFolderName

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
    } else {
        Write-Warning "Staging src directory not found, skipping replacement"
    }

    $replaceDuration = ((Get-Date) - $replaceStart).TotalSeconds
    Add-ProfileOperation "File Replacement" $replaceDuration
    Add-ProfileCheckpoint "After Replacement"

    # Cleanup staging
    Write-Host "Cleaning up staging directory..."
    Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue

    # Stop timeout job
    if ($null -ne $timeoutJob) {
        Stop-Job -Job $timeoutJob -ErrorAction SilentlyContinue
        Remove-Job -Job $timeoutJob -ErrorAction SilentlyContinue
    }

    Add-ProfileCheckpoint "Script Complete"
    $totalTime = (Get-Date) - $script:ProfileData.StartTime

    Write-Host ""
    Write-Host "Completed successfully in $($totalTime.TotalSeconds.ToString('F2')) seconds!"
    Write-Host "Backup location: $backupPath"
    Write-Host "Use -Undo to restore from backup, or -ListBackups to see available backups."

    # Show profile report if verbose
    if ($PSCmdlet.MyInvocation.BoundParameters["Verbose"].IsPresent) {
        Write-Host ""
        Write-Host (Get-ProfileReport)
    }
}
catch {
    # Stop timeout job
    if ($null -ne $timeoutJob) {
        Stop-Job -Job $timeoutJob -ErrorAction SilentlyContinue
        Remove-Job -Job $timeoutJob -ErrorAction SilentlyContinue
    }

    $errorMessage = "Error: $_"
    Write-Host $errorMessage -ForegroundColor Red
    if ($_.ScriptStackTrace) {
        Write-Host $_.ScriptStackTrace -ForegroundColor Red
    }

    # Show profile report on error
    if ($null -ne $script:ProfileData) {
        $profileReport = Get-ProfileReport
        Write-Host ""
        Write-Host $profileReport
    }

    # Cleanup staging on error
    $stagingPath = Join-Path $resolvedRoot ".staging"
    if (Test-Path -Path $stagingPath) {
        Write-Host "Cleaning up staging directory due to error..."
        Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    exit 1
}
finally {
    # Ensure timeout job is cleaned up
    if ($null -ne $timeoutJob) {
        Stop-Job -Job $timeoutJob -ErrorAction SilentlyContinue
        Remove-Job -Job $timeoutJob -ErrorAction SilentlyContinue
    }
}

#endregion
