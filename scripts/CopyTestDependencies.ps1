# Script to copy all required DLLs from NuGet packages to test output directory
# This is needed because .NET 9.0 test host doesn't properly resolve dependencies
# for packages that only target older frameworks (net6.0, netstandard2.0, etc.)

param(
    [string]$DepsJsonPath,
    [string]$OutputPath,
    [string]$NuGetPackageRoot = "$env:USERPROFILE\.nuget\packages"
)

if (-not (Test-Path $DepsJsonPath)) {
    Write-Error "Deps.json file not found: $DepsJsonPath"
    exit 1
}

if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

$depsJson = Get-Content $DepsJsonPath | ConvertFrom-Json
$copiedCount = 0
$missingCount = 0

# Function to copy DLL from NuGet cache
function Copy-DllFromNuGet {
    param(
        [string]$PackageName,
        [string]$Version,
        [string]$RelativePath,
        [string]$OutputDir
    )
    
    $packagePath = Join-Path $NuGetPackageRoot $PackageName.ToLower()
    $versionPath = Join-Path $packagePath $Version
    $dllPath = Join-Path $versionPath $RelativePath
    
    if (Test-Path $dllPath) {
        $destPath = Join-Path $OutputDir (Split-Path $RelativePath -Leaf)
        Copy-Item $dllPath -Destination $destPath -Force
        return $true
    }
    return $false
}

# Iterate through all targets and copy runtime DLLs
foreach ($target in $depsJson.targets.PSObject.Properties) {
    foreach ($package in $target.Value.PSObject.Properties) {
        $packageName = $package.Name -replace '/.*$', ''
        $packageVersion = $package.Name -replace '^.*/', ''
        
        if ($package.Value.runtime) {
            foreach ($runtime in $package.Value.runtime.PSObject.Properties) {
                $relativePath = $runtime.Name
                if (Copy-DllFromNuGet -PackageName $packageName -Version $packageVersion -RelativePath $relativePath -OutputDir $OutputPath) {
                    $copiedCount++
                } else {
                    $missingCount++
                    Write-Warning "Could not find: $packageName\$packageVersion\$relativePath"
                }
            }
        }
        
        if ($package.Value.runtimeTargets) {
            foreach ($runtimeTarget in $package.Value.runtimeTargets.PSObject.Properties) {
                $relativePath = $runtimeTarget.Name
                $rid = $runtimeTarget.Value.rid
                
                # Create RID-specific directory structure if needed
                if ($rid -and $relativePath -like "runtimes/*") {
                    $ridPath = Join-Path $OutputPath (Split-Path $relativePath -Parent)
                    if (-not (Test-Path $ridPath)) {
                        New-Item -ItemType Directory -Path $ridPath -Force | Out-Null
                    }
                    $outputDir = $ridPath
                } else {
                    $outputDir = $OutputPath
                }
                
                if (Copy-DllFromNuGet -PackageName $packageName -Version $packageVersion -RelativePath $relativePath -OutputDir $outputDir) {
                    $copiedCount++
                } else {
                    $missingCount++
                    Write-Warning "Could not find: $packageName\$packageVersion\$relativePath"
                }
            }
        }
    }
}

Write-Host "Copied $copiedCount DLLs, $missingCount missing"
