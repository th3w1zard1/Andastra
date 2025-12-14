#Requires -Version 5.1

<#
.SYNOPSIS
    Helper functions for Stride game engine build validation and asset management.

.DESCRIPTION
    Provides functions for validating Stride-specific build requirements including:
    - Asset compilation verification
    - Data folder structure validation
    - Shader compilation checking
    - Content-addressable storage validation
#>

# Stride-specific file patterns
$script:StrideAssetPatterns = @(
    "*.sd",           # Stride asset definition files
    "*.sdsl",         # Stride shader files
    "*.sdfx",         # Stride effect files
    "*.sdpkg"         # Stride package files
)

$script:StrideDataPatterns = @(
    "*.deps",         # Dependency files
    "*.index",        # Index map files (content-addressable storage)
    "*.db"            # Asset database files
)

function Test-StrideAssetCompilation {
    <#
    .SYNOPSIS
        Verifies that Stride assets have been properly compiled.
    
    .DESCRIPTION
        Checks for compiled assets in the Data folder, validates content-addressable
        storage structure, and verifies shader compilation.
    
    .PARAMETER BuildPath
        Path to the build output directory.
    
    .PARAMETER Platform
        Platform name (Windows, Linux, macOS).
    
    .RETURNS
        PSCustomObject with validation results.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildPath,
        
        [Parameter(Mandatory = $true)]
        [string]$Platform
    )
    
    $Result = [PSCustomObject]@{
        HasAssets = $false
        HasDataFolder = $false
        DataFolderPath = $null
        AssetCount = 0
        HasIndexFiles = $false
        HasShaders = $false
        Warnings = @()
        Errors = @()
    }
    
    # Check for Data folder (Stride's compiled asset output)
    $DataFolders = @(
        Join-Path $BuildPath "Data",
        Join-Path $BuildPath "data",
        Join-Path $BuildPath "Assets",
        Join-Path $BuildPath "assets"
    )
    
    foreach ($DataFolder in $DataFolders) {
        if (Test-Path $DataFolder -PathType Container) {
            $Result.HasDataFolder = $true
            $Result.DataFolderPath = $DataFolder
            
            # Count asset files
            $AssetFiles = Get-ChildItem -Path $DataFolder -Recurse -File -ErrorAction SilentlyContinue
            $Result.AssetCount = $AssetFiles.Count
            
            if ($AssetFiles.Count -gt 0) {
                $Result.HasAssets = $true
            }
            
            # Check for index files (content-addressable storage)
            $IndexFiles = Get-ChildItem -Path $DataFolder -Filter "*.index" -Recurse -ErrorAction SilentlyContinue
            if ($IndexFiles.Count -gt 0) {
                $Result.HasIndexFiles = $true
            }
            
            # Check for shader-related files
            $ShaderFiles = Get-ChildItem -Path $DataFolder -Include "*.sdsl", "*.sdfx" -Recurse -ErrorAction SilentlyContinue
            if ($ShaderFiles.Count -gt 0) {
                $Result.HasShaders = $true
            }
            
            break
        }
    }
    
    # If no Data folder but project might not use Stride assets, that's OK
    if (-not $Result.HasDataFolder) {
        $Result.Warnings += "No Data/Assets folder found. This is normal if the project doesn't use Stride assets."
    }
    
    return $Result
}

function Get-StrideBuildInfo {
    <#
    .SYNOPSIS
        Gathers information about Stride build configuration.
    
    .PARAMETER ProjectPath
        Path to the .csproj file.
    
    .RETURNS
        PSCustomObject with build information.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )
    
    $Info = [PSCustomObject]@{
        HasStridePackages = $false
        StrideVersion = $null
        HasAssetCompiler = $false
        UsesStrideAssets = $false
    }
    
    if (-not (Test-Path $ProjectPath)) {
        return $Info
    }
    
    $ProjectContent = Get-Content $ProjectPath -Raw -ErrorAction SilentlyContinue
    if (-not $ProjectContent) {
        return $Info
    }
    
    # Check for Stride package references
    if ($ProjectContent -match 'PackageReference.*Stride\.(Engine|Core|Graphics|Rendering)') {
        $Info.HasStridePackages = $true
        
        # Extract Stride version
        if ($ProjectContent -match 'Stride\.Engine.*Version="([^"]+)"') {
            $Info.StrideVersion = $matches[1]
        }
    }
    
    # Check for asset compiler package
    if ($ProjectContent -match 'Stride\.Core\.Assets\.CompilerApp') {
        $Info.HasAssetCompiler = $true
        $Info.UsesStrideAssets = $true
    }
    
    # Check for Assets folder in project directory
    $ProjectDir = Split-Path -Parent $ProjectPath
    $AssetsFolder = Join-Path $ProjectDir "Assets"
    if (Test-Path $AssetsFolder -PathType Container) {
        $AssetFiles = Get-ChildItem -Path $AssetsFolder -Filter "*.sd" -Recurse -ErrorAction SilentlyContinue
        if ($AssetFiles.Count -gt 0) {
            $Info.UsesStrideAssets = $true
        }
    }
    
    return $Info
}

function Test-StrideShaderCompilation {
    <#
    .SYNOPSIS
        Verifies that shaders have been precompiled.
    
    .PARAMETER BuildPath
        Path to the build output directory.
    
    .RETURNS
        Boolean indicating if shaders appear to be compiled.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildPath
    )
    
    # Check for compiled shader files in Data folder
    $DataFolders = @(
        Join-Path $BuildPath "Data",
        Join-Path $BuildPath "data"
    )
    
    foreach ($DataFolder in $DataFolders) {
        if (Test-Path $DataFolder -PathType Container) {
            # Look for shader compilation artifacts
            # Stride compiles shaders to platform-specific formats
            $ShaderArtifacts = Get-ChildItem -Path $DataFolder -Include "*.csb", "*.sdbin", "*.shader" -Recurse -ErrorAction SilentlyContinue
            if ($ShaderArtifacts.Count -gt 0) {
                return $true
            }
        }
    }
    
    return $false
}

function Get-StridePackageStructure {
    <#
    .SYNOPSIS
        Validates Stride package folder structure.
    
    .PARAMETER BuildPath
        Path to the build output directory.
    
    .PARAMETER Platform
        Platform name.
    
    .RETURNS
        PSCustomObject with structure validation results.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildPath,
        
        [Parameter(Mandatory = $true)]
        [string]$Platform
    )
    
    $Structure = [PSCustomObject]@{
        IsValid = $true
        HasExecutable = $false
        HasDataFolder = $false
        HasExpectedFolders = $false
        MissingFolders = @()
        UnexpectedFolders = @()
    }
    
    # Expected folder structure based on platform
    $ExpectedFolders = @()
    if ($Platform -match "Windows") {
        $ExpectedFolders = @("x64", "x86")
    } elseif ($Platform -match "Linux") {
        $ExpectedFolders = @("x64", "x86")
    } elseif ($Platform -match "macOS") {
        $ExpectedFolders = @("x64", "arm64")
    }
    
    # Check for executable
    if ($Platform -match "Windows") {
        $Executables = Get-ChildItem -Path $BuildPath -Filter "*.exe" -Recurse -ErrorAction SilentlyContinue
    } else {
        $AllFiles = Get-ChildItem -Path $BuildPath -File -Recurse -ErrorAction SilentlyContinue
        $Executables = $AllFiles | Where-Object { 
            $_.Extension -eq "" -or 
            $_.Name -like "*Game*" -or
            ($_.Attributes -band [System.IO.FileAttributes]::Executable)
        }
    }
    
    if ($Executables.Count -gt 0) {
        $Structure.HasExecutable = $true
    }
    
    # Check for Data folder
    $DataFolders = @("Data", "data", "Assets", "assets")
    foreach ($Folder in $DataFolders) {
        $DataPath = Join-Path $BuildPath $Folder
        if (Test-Path $DataPath -PathType Container) {
            $Structure.HasDataFolder = $true
            break
        }
    }
    
    # Validate folder structure
    $RootFolders = Get-ChildItem -Path $BuildPath -Directory -ErrorAction SilentlyContinue
    $FoundFolders = $RootFolders.Name
    
    foreach ($Expected in $ExpectedFolders) {
        if ($FoundFolders -notcontains $Expected) {
            # Check if files are in root (flat structure)
            if (-not $Structure.HasExecutable) {
                $Structure.MissingFolders += $Expected
            }
        }
    }
    
    if ($Structure.MissingFolders.Count -gt 0) {
        $Structure.IsValid = $false
    } else {
        $Structure.HasExpectedFolders = $true
    }
    
    return $Structure
}

function Write-StrideBuildReport {
    <#
    .SYNOPSIS
        Writes a comprehensive Stride build validation report.
    
    .PARAMETER ValidationResults
        Results from Stride validation functions.
    
    .PARAMETER BuildPath
        Path to the build output.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$ValidationResults,
        
        [Parameter(Mandatory = $true)]
        [string]$BuildPath
    )
    
    Write-Host "`nStride Build Validation Report" -ForegroundColor Cyan
    Write-Host ("=" * 50) -ForegroundColor Cyan
    
    if ($ValidationResults.HasDataFolder) {
        Write-Host "Data Folder: $($ValidationResults.DataFolderPath)" -ForegroundColor Green
        Write-Host "Asset Files: $($ValidationResults.AssetCount)" -ForegroundColor $(if ($ValidationResults.AssetCount -gt 0) { "Green" } else { "Yellow" })
        
        if ($ValidationResults.HasIndexFiles) {
            Write-Host "Content-addressable storage: Detected" -ForegroundColor Green
        }
        
        if ($ValidationResults.HasShaders) {
            Write-Host "Shaders: Detected" -ForegroundColor Green
        }
    } else {
        Write-Host "Data Folder: Not found (project may not use Stride assets)" -ForegroundColor Yellow
    }
    
    if ($ValidationResults.Warnings.Count -gt 0) {
        Write-Host "`nWarnings:" -ForegroundColor Yellow
        foreach ($Warning in $ValidationResults.Warnings) {
            Write-Host "  - $Warning" -ForegroundColor Yellow
        }
    }
    
    if ($ValidationResults.Errors.Count -gt 0) {
        Write-Host "`nErrors:" -ForegroundColor Red
        foreach ($Error in $ValidationResults.Errors) {
            Write-Host "  - $Error" -ForegroundColor Red
        }
    }
    
    Write-Host ""
}

export-modulemember -Function Test-StrideAssetCompilation, Get-StrideBuildInfo, Test-StrideShaderCompilation, Get-StridePackageStructure, Write-StrideBuildReport

