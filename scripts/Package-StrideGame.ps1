#Requires -Version 5.1

<#
.SYNOPSIS
    Packages a Stride game build for distribution following industry best practices.

.DESCRIPTION
    Cleans up release builds by removing debug files, unnecessary files, and organizing
    the distribution according to Stride game engine best practices. Creates archives
    for distribution with proper validation and error handling.

.PARAMETER BuildPath
    Path to the build output directory. Defaults to "dist"

.PARAMETER GameName
    Name of the game for packaging. Defaults to "Odyssey"

.PARAMETER Version
    Version string for the package. Defaults to "1.0.0"

.PARAMETER CreateArchive
    Whether to create archives. Defaults to $true

.PARAMETER ArchiveFormat
    Archive format. Options: zip, tar, tar.gz. Defaults to "zip"

.PARAMETER ArchiveCompression
    Compression level for ZIP archives. Options: Optimal, Fastest, NoCompression
    Defaults to "Optimal"

.PARAMETER IncludeDocumentation
    Whether to include documentation files. Defaults to $true

.PARAMETER IncludeLicenses
    Whether to include license files. Defaults to $true

.PARAMETER DocumentationPath
    Path to documentation directory. Defaults to "docs"

.PARAMETER KeepDebugSymbols
    Whether to keep .pdb files. Defaults to $false

.PARAMETER ValidateStructure
    Whether to validate folder structure matches Stride best practices.
    Defaults to $true

.PARAMETER CreateChecksums
    Whether to create checksum files (SHA256) for archives.
    Defaults to $false

.PARAMETER Verbose
    Show verbose output during packaging.

.EXAMPLE
    .\Package-StrideGame.ps1 -BuildPath "dist" -GameName "Odyssey" -Version "1.0.0"

.EXAMPLE
    .\Package-StrideGame.ps1 -BuildPath "dist" -CreateArchive $false -KeepDebugSymbols $true

.EXAMPLE
    .\Package-StrideGame.ps1 -ArchiveFormat tar.gz -CreateChecksums -Verbose
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$BuildPath = "dist",
    [string]$GameName = "Odyssey",
    [string]$Version = "1.0.0",
    [bool]$CreateArchive = $true,
    [ValidateSet("zip", "tar", "tar.gz")]
    [string]$ArchiveFormat = "zip",
    [ValidateSet("Optimal", "Fastest", "NoCompression")]
    [string]$ArchiveCompression = "Optimal",
    [bool]$IncludeDocumentation = $true,
    [bool]$IncludeLicenses = $true,
    [string]$DocumentationPath = "docs",
    [bool]$KeepDebugSymbols = $false,
    [bool]$ValidateStructure = $true,
    [bool]$CreateChecksums = $false,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Cross-platform path handling
$IsWindowsPlatform = ($IsWindows -or $env:OS -eq "Windows_NT")

function Normalize-Path {
    param([string]$Path)
    if ($IsWindowsPlatform) {
        return $Path -replace '/', '\'
    } else {
        return $Path -replace '\\', '/'
    }
}

function Write-VerboseOutput {
    param([string]$Message)
    if ($Verbose) {
        Write-Host $Message -ForegroundColor Gray
    }
}

function Get-FileSize {
    param([string]$Path)
    $Item = Get-Item $Path -ErrorAction SilentlyContinue
    if ($Item) {
        return $Item.Length
    }
    return 0
}

function Format-FileSize {
    param([long]$Bytes)
    $Units = @("B", "KB", "MB", "GB")
    $Index = 0
    $Size = [double]$Bytes
    while ($Size -ge 1024 -and $Index -lt ($Units.Length - 1)) {
        $Size /= 1024
        $Index++
    }
    return "{0:N2} {1}" -f $Size, $Units[$Index]
}

function New-ZipArchive {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$CompressionLevel
    )
    
    if ($IsWindowsPlatform) {
        # Use .NET compression on Windows
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
        if (-not ([System.IO.Compression.ZipFile]::GetType())) {
            Write-Error "System.IO.Compression.FileSystem not available. Cannot create ZIP archive."
            return $false
        }
        
        try {
            $CompressionLevelEnum = [System.IO.Compression.CompressionLevel]::Optimal
            switch ($CompressionLevel) {
                "Fastest" { $CompressionLevelEnum = [System.IO.Compression.CompressionLevel]::Fastest }
                "NoCompression" { $CompressionLevelEnum = [System.IO.Compression.CompressionLevel]::NoCompression }
                default { $CompressionLevelEnum = [System.IO.Compression.CompressionLevel]::Optimal }
            }
            
            if (Test-Path $DestinationPath) {
                Remove-Item $DestinationPath -Force
            }
            
            [System.IO.Compression.ZipFile]::CreateFromDirectory($SourcePath, $DestinationPath, $CompressionLevelEnum, $false)
            return $true
        } catch {
            Write-Error "Failed to create ZIP archive: $_"
            return $false
        }
    } else {
        # Use zip command on Unix-like systems
        $ZipCmd = Get-Command zip -ErrorAction SilentlyContinue
        if (-not $ZipCmd) {
            Write-Error "zip command not found. Cannot create ZIP archive."
            return $false
        }
        
        try {
            $SourceDir = Split-Path -Parent $SourcePath
            $SourceName = Split-Path -Leaf $SourcePath
            Push-Location $SourceDir
            
            $CompressionFlag = "-9"  # Maximum compression
            switch ($CompressionLevel) {
                "Fastest" { $CompressionFlag = "-1" }
                "NoCompression" { $CompressionFlag = "-0" }
                default { $CompressionFlag = "-9" }
            }
            
            if (Test-Path $DestinationPath) {
                Remove-Item $DestinationPath -Force
            }
            
            & zip -r $CompressionFlag $DestinationPath $SourceName
            $Success = ($LASTEXITCODE -eq 0)
            Pop-Location
            return $Success
        } catch {
            Pop-Location
            Write-Error "Failed to create ZIP archive: $_"
            return $false
        }
    }
}

function New-TarArchive {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [bool]$Compress
    )
    
    $TarCmd = Get-Command tar -ErrorAction SilentlyContinue
    if (-not $TarCmd) {
        Write-Error "tar command not found. Cannot create TAR archive."
        return $false
    }
    
    try {
        $SourceDir = Split-Path -Parent $SourcePath
        $SourceName = Split-Path -Leaf $SourcePath
        Push-Location $SourceDir
        
        $TarArgs = @("cf", $DestinationPath, $SourceName)
        if ($Compress) {
            $TarArgs = @("czf", $DestinationPath, $SourceName)
        }
        
        if (Test-Path $DestinationPath) {
            Remove-Item $DestinationPath -Force
        }
        
        & tar $TarArgs
        $Success = ($LASTEXITCODE -eq 0)
        Pop-Location
        return $Success
    } catch {
        Pop-Location
        Write-Error "Failed to create TAR archive: $_"
        return $false
    }
}

function Get-FileHash {
    param([string]$FilePath)
    
    if ($IsWindowsPlatform) {
        try {
            $Hash = Get-FileHash -Path $FilePath -Algorithm SHA256 -ErrorAction Stop
            return $Hash.Hash
        } catch {
            Write-Warning "Failed to compute hash: $_"
            return $null
        }
    } else {
        try {
            $HashOutput = & shasum -a 256 $FilePath 2>$null
            if ($LASTEXITCODE -eq 0 -and $HashOutput) {
                return ($HashOutput -split '\s+')[0]
            }
            return $null
        } catch {
            Write-Warning "Failed to compute hash: $_"
            return $null
        }
    }
}

$BuildPath = Normalize-Path $BuildPath
$DocumentationPath = Normalize-Path $DocumentationPath

if (-not (Test-Path $BuildPath)) {
    Write-Error "Build path not found: $BuildPath"
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Stride Game Packaging" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Game: $GameName v$Version" -ForegroundColor White
Write-Host "Build Path: $BuildPath" -ForegroundColor White
Write-Host "Archive Format: $ArchiveFormat" -ForegroundColor White
Write-Host "Create Archives: $CreateArchive" -ForegroundColor White
Write-Host "========================================`n" -ForegroundColor Cyan

# Get all platform directories (exclude archive files)
$PlatformDirs = Get-ChildItem -Path $BuildPath -Directory -ErrorAction SilentlyContinue | 
    Where-Object { $_.Name -notmatch '\.(zip|tar|gz|sha256)$' }

if ($PlatformDirs.Count -eq 0) {
    Write-Error "No platform directories found in: $BuildPath"
    exit 1
}

$ProcessedPlatforms = @()
$TotalSizeBefore = 0
$TotalSizeAfter = 0

foreach ($PlatformDir in $PlatformDirs) {
    $PlatformName = $PlatformDir.Name
    Write-Host "`nProcessing: $PlatformName" -ForegroundColor Green
    Write-Host ("-" * 50) -ForegroundColor Gray
    
    $PlatformPath = $PlatformDir.FullName
    $SizeBefore = (Get-ChildItem -Path $PlatformPath -Recurse -File -ErrorAction SilentlyContinue | 
        Measure-Object -Property Length -Sum).Sum
    $TotalSizeBefore += $SizeBefore
    
    Write-VerboseOutput "Initial size: $(Format-FileSize $SizeBefore)"
    
    # Files and patterns to remove (Stride best practices)
    $FilesToRemove = @(
        "*.pdb",           # Debug symbols
        "*.xml",           # XML documentation
        "*vshost*",        # Visual Studio host process files
        "*.vshost.exe",
        "*.vshost.exe.manifest",
        "*.vshost.exe.config",
        "*.deps.json.bak", # Backup dependency files
        "*.cache"          # Build cache files
    )
    
    if (-not $KeepDebugSymbols) {
        Write-Host "Removing debug files..." -ForegroundColor Yellow
        $RemovedCount = 0
        foreach ($Pattern in $FilesToRemove) {
            $Files = Get-ChildItem -Path $PlatformPath -Filter $Pattern -Recurse -ErrorAction SilentlyContinue
            foreach ($File in $Files) {
                if ($PSCmdlet.ShouldProcess($File.FullName, "Remove debug file")) {
                    Remove-Item -Path $File.FullName -Force -ErrorAction SilentlyContinue
                    $RemovedCount++
                }
            }
        }
        if ($RemovedCount -gt 0) {
            Write-Host "Removed $RemovedCount debug file(s)" -ForegroundColor Gray
        }
    }
    
    # Validate and organize folder structure
    if ($ValidateStructure) {
        Write-Host "Validating folder structure..." -ForegroundColor Yellow
        
        # Determine expected folders based on platform
        $ExpectedFolders = @()
        if ($PlatformName -match "Windows") {
            $ExpectedFolders = @("x64", "x86")
        } elseif ($PlatformName -match "Linux") {
            $ExpectedFolders = @("x64", "x86")
        } elseif ($PlatformName -match "macOS") {
            $ExpectedFolders = @("x64", "arm64")
        }
        
        # Always expect Data folder for Stride assets
        $DataFolders = @("Data", "data", "Assets", "Resources")
        $FoundDataFolder = $false
        
        $RootDirs = Get-ChildItem -Path $PlatformPath -Directory -ErrorAction SilentlyContinue
        foreach ($Dir in $RootDirs) {
            $DirName = $Dir.Name
            $IsValidFolder = $ExpectedFolders -contains $DirName -or $DataFolders -contains $DirName
            
            if (-not $IsValidFolder) {
                # Check if it contains executables or DLLs (might be flat structure)
                $HasExecutable = (Get-ChildItem -Path $Dir.FullName -Filter "*.exe" -ErrorAction SilentlyContinue).Count -gt 0
                $HasDll = (Get-ChildItem -Path $Dir.FullName -Filter "*.dll" -ErrorAction SilentlyContinue).Count -gt 0
                $HasAppHost = (Get-ChildItem -Path $Dir.FullName -Filter "*" -File -ErrorAction SilentlyContinue | 
                    Where-Object { $_.Extension -eq "" }).Count -gt 0
                
                if (-not $HasExecutable -and -not $HasDll -and -not $HasAppHost) {
                    Write-VerboseOutput "Removing unnecessary folder: $DirName"
                    if ($PSCmdlet.ShouldProcess($Dir.FullName, "Remove unnecessary folder")) {
                        Remove-Item -Path $Dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
                    }
                }
            }
            
            if ($DataFolders -contains $DirName) {
                $FoundDataFolder = $true
            }
        }
        
        if ($FoundDataFolder) {
            Write-Host "Data folder structure validated." -ForegroundColor Green
        } else {
            Write-VerboseOutput "No Data/Assets folder found (assets may be embedded or not present)"
        }
    }
    
    # Add documentation if requested
    if ($IncludeDocumentation -and (Test-Path $DocumentationPath)) {
        $DocDestPath = Join-Path $PlatformPath "Documentation"
        $DocDestPath = Normalize-Path $DocDestPath
        
        if ($PSCmdlet.ShouldProcess($DocDestPath, "Copy documentation")) {
            if (-not (Test-Path $DocDestPath)) {
                New-Item -Path $DocDestPath -ItemType Directory -Force | Out-Null
            }
            
            Write-Host "Copying documentation..." -ForegroundColor Yellow
            Copy-Item -Path (Join-Path $DocumentationPath "*") -Destination $DocDestPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Documentation copied." -ForegroundColor Green
        }
    }
    
    # Add license files if requested
    if ($IncludeLicenses) {
        $LicenseFiles = @("LICENSE", "LICENSE.txt", "LICENSE.md", "COPYING", "COPYING.txt")
        $FoundLicense = $false
        
        foreach ($LicenseFile in $LicenseFiles) {
            $LicensePath = Normalize-Path $LicenseFile
            if (Test-Path $LicensePath) {
                if ($PSCmdlet.ShouldProcess($LicensePath, "Copy license file")) {
                    Write-Host "Copying license file: $LicenseFile" -ForegroundColor Yellow
                    Copy-Item -Path $LicensePath -Destination $PlatformPath -Force -ErrorAction SilentlyContinue
                    $FoundLicense = $true
                    break
                }
            }
        }
        
        # Check repository root for license files
        if (-not $FoundLicense) {
            $RepoRoot = Split-Path -Parent $BuildPath
            $RootLicensePath = Join-Path $RepoRoot "LICENSE*"
            $RootLicenses = Get-ChildItem -Path $RootLicensePath -ErrorAction SilentlyContinue
            if ($RootLicenses) {
                if ($PSCmdlet.ShouldProcess($RootLicenses[0].FullName, "Copy license file from root")) {
                    Write-Host "Copying license file from root: $($RootLicenses[0].Name)" -ForegroundColor Yellow
                    Copy-Item -Path $RootLicenses[0].FullName -Destination $PlatformPath -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
    
    # Include version files if they exist in build path
    $VersionFiles = @("version.txt", "VERSION", "version.json")
    foreach ($VersionFile in $VersionFiles) {
        $VersionSourcePath = Join-Path (Split-Path -Parent $BuildPath) $VersionFile
        if (Test-Path $VersionSourcePath) {
            Copy-Item -Path $VersionSourcePath -Destination $PlatformPath -Force -ErrorAction SilentlyContinue
            Write-VerboseOutput "Copied version file: $VersionFile"
        }
    }
    
    # Create README for distribution
    $ReadmePath = Join-Path $PlatformPath "README.txt"
    $ReadmeContent = @"
$GameName v$Version
Platform: $PlatformName

SYSTEM REQUIREMENTS:
- $(if ($PlatformName -match "Windows") { ".NET Runtime (if framework-dependent) or Windows 7+" } elseif ($PlatformName -match "Linux") { ".NET Runtime (if framework-dependent) or compatible Linux distribution" } else { ".NET Runtime (if framework-dependent) or macOS 10.12+" })
- DirectX 11, OpenGL 4.2, or Vulkan 1.0 compatible graphics card
- 2GB+ RAM recommended

INSTALLATION:
Extract this archive to your desired installation directory.

RUNNING:
$(if ($PlatformName -match "Windows") { "Run the .exe file in the x64 or x86 folder (depending on your system)." } elseif ($PlatformName -match "Linux") { "Run the executable file in the x64 or x86 folder. You may need to make it executable first: chmod +x <executable>" } else { "Run the executable file in the x64 or arm64 folder (depending on your Mac)." })

For more information, see Documentation/ folder.

$(if ($IncludeLicenses) { "LICENSE: See LICENSE file for license information." })
"@
    
    if ($PSCmdlet.ShouldProcess($ReadmePath, "Create README.txt")) {
        Set-Content -Path $ReadmePath -Value $ReadmeContent -Encoding UTF8
        Write-Host "Created README.txt" -ForegroundColor Green
    }
    
    # Calculate size after cleanup
    $SizeAfter = (Get-ChildItem -Path $PlatformPath -Recurse -File -ErrorAction SilentlyContinue | 
        Measure-Object -Property Length -Sum).Sum
    $TotalSizeAfter += $SizeAfter
    $SizeSaved = $SizeBefore - $SizeAfter
    
    Write-Host "Final size: $(Format-FileSize $SizeAfter)" -ForegroundColor Green
    if ($SizeSaved -gt 0) {
        Write-Host "Size saved: $(Format-FileSize $SizeSaved)" -ForegroundColor Gray
    }
    
    # Create archive if requested
    if ($CreateArchive) {
        $ArchiveName = "$GameName-v$Version-$PlatformName.$ArchiveFormat"
        $ArchivePath = Join-Path $BuildPath $ArchiveName
        $ArchivePath = Normalize-Path $ArchivePath
        
        Write-Host "Creating archive: $ArchiveName" -ForegroundColor Yellow
        
        $ArchiveCreated = $false
        if ($ArchiveFormat -eq "zip") {
            $ArchiveCreated = New-ZipArchive -SourcePath $PlatformPath -DestinationPath $ArchivePath -CompressionLevel $ArchiveCompression
        } elseif ($ArchiveFormat -eq "tar" -or $ArchiveFormat -eq "tar.gz") {
            $Compress = ($ArchiveFormat -eq "tar.gz")
            $ArchiveCreated = New-TarArchive -SourcePath $PlatformPath -DestinationPath $ArchivePath -Compress $Compress
        }
        
        if ($ArchiveCreated -and (Test-Path $ArchivePath)) {
            $ArchiveSize = Get-FileSize $ArchivePath
            Write-Host "Archive created: $ArchiveName ($(Format-FileSize $ArchiveSize))" -ForegroundColor Green
            
            # Create checksum if requested
            if ($CreateChecksums) {
                $ChecksumPath = "$ArchivePath.sha256"
                $Hash = Get-FileHash -FilePath $ArchivePath
                if ($Hash) {
                    $ChecksumContent = "$Hash  $ArchiveName"
                    Set-Content -Path $ChecksumPath -Value $ChecksumContent -NoNewline
                    Write-Host "Checksum created: $(Split-Path -Leaf $ChecksumPath)" -ForegroundColor Green
                }
            }
        } else {
            Write-Warning "Failed to create archive: $ArchiveName"
        }
    }
    
    $ProcessedPlatforms += $PlatformName
}

Write-Host "`n" + ("=" * 50) -ForegroundColor Cyan
Write-Host "Packaging Summary" -ForegroundColor Cyan
Write-Host ("=" * 50) -ForegroundColor Cyan
Write-Host "Processed platforms: $($ProcessedPlatforms.Count)" -ForegroundColor Green
foreach ($Platform in $ProcessedPlatforms) {
    Write-Host "  - $Platform" -ForegroundColor Gray
}

if ($TotalSizeBefore -gt 0 -and $TotalSizeAfter -gt 0) {
    $TotalSaved = $TotalSizeBefore - $TotalSizeAfter
    Write-Host "`nTotal size before: $(Format-FileSize $TotalSizeBefore)" -ForegroundColor White
    Write-Host "Total size after: $(Format-FileSize $TotalSizeAfter)" -ForegroundColor White
    if ($TotalSaved -gt 0) {
        Write-Host "Total saved: $(Format-FileSize $TotalSaved)" -ForegroundColor Green
    }
}

Write-Host "`nPackaging completed!" -ForegroundColor Cyan
