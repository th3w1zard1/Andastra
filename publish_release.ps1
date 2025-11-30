# PowerShell version of publish_release.bat
# Comprehensive logging and optimizations

param(
    [string]$HpVersion = "v2.0.0",
    [string]$ProjectFile = "src\HoloPatcher\HoloPatcher.csproj",
    [string]$PublishProfilesDir = "src\HoloPatcher\Properties\PublishProfiles",
    [string]$SevenZipPath = "C:\Program Files\7-Zip\7z.exe",
    [string]$OutputDir = "dist",
    [switch]$Verbose,
    [switch]$Debug,
    [ValidateSet("SilentlyContinue", "Stop", "Continue", "Inquire", "Ignore", "Suspend")]
    [string]$ErrorAction = "Inquire"
)

# Set up logging with timestamped build directory
$BuildTimestamp = Get-Date -Format 'yyyy-MM-yy-HH-mm'
$LogFile = "publish_release_$BuildTimestamp.log"

# Configure error handling based on ErrorAction parameter
$ErrorActionPreference = $ErrorAction

function Write-Log {
    [CmdletBinding()]
    param(
        [string]$Message,
        [string]$Level = "INFO",
        [hashtable]$Variables = @{}
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"

    if ($Variables.Count -gt 0) {
        $logMessage += " | Variables: " + ($Variables.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ", "
    }

    # Use PowerShell's built-in debug/verbose streams for appropriate levels
    # Console output remains unchanged, file logging is added
    switch ($Level.ToUpper()) {
        "ERROR" {
            Write-Host $logMessage -ForegroundColor "Red"
            Add-Content -Path $LogFile -Value $logMessage
        }
        "WARN"  {
            Write-Host $logMessage -ForegroundColor "Yellow"
            Add-Content -Path $LogFile -Value $logMessage
        }
        "INFO"  {
            Write-Host $logMessage -ForegroundColor "White"
            Add-Content -Path $LogFile -Value $logMessage
        }
        "DEBUG" {
            # Override debug output color to DarkGray
            $originalDebugColor = $Host.UI.RawUI.ForegroundColor
            $Host.UI.RawUI.ForegroundColor = "DarkGray"
            Write-Debug $logMessage
            $Host.UI.RawUI.ForegroundColor = $originalDebugColor
            Add-Content -Path $LogFile -Value $logMessage
        }
        "VERBOSE" {
            # Override verbose output color to Gray
            $originalVerboseColor = $Host.UI.RawUI.ForegroundColor
            $Host.UI.RawUI.ForegroundColor = "Gray"
            Write-Verbose $logMessage
            $Host.UI.RawUI.ForegroundColor = $originalVerboseColor
            Add-Content -Path $LogFile -Value $logMessage
        }
        default {
            Write-Host $logMessage -ForegroundColor "Gray"
            Add-Content -Path $LogFile -Value $logMessage
        }
    }
}

# Configure debug output if Debug flag is set
if ($Debug) {
    $DebugPreference = "Continue"
    $VerbosePreference = "Continue"  # Debug automatically enables verbose
    Write-Log "Debug logging enabled (includes verbose)" -Level "INFO"
}
elseif ($Verbose) {
    $VerbosePreference = "Continue"
    Write-Log "Verbose logging enabled" -Level "INFO"
}

function Test-RequiredTools {
    Write-Log "Checking required tools" -Level "DEBUG" -Variables @{ "SevenZipPath" = $SevenZipPath }

    if (-not (Test-Path $SevenZipPath)) {
        Write-Log "7-Zip compression tool not found" -Level "ERROR" -Variables @{ "SevenZipPath" = $SevenZipPath }
        throw "7-Zip not found at: $SevenZipPath"
    }

    try {
        $dotnetVersion = dotnet --version
        Write-Log "Dotnet found" -Level "DEBUG" -Variables @{ "Version" = $dotnetVersion }
        Write-Log "Validating .NET SDK installation" -Level "VERBOSE" -Variables @{ "Version" = $dotnetVersion }
    }
    catch {
        Write-Log ".NET SDK not found in system PATH" -Level "ERROR"
        throw "Dotnet CLI not found"
    }
}


function Get-PublishProfileInfo {
    param([string]$FileName)

    $parts = $FileName -split "_"
    $framework = $parts[0]
    $rid = $parts[1]
    $lastSection = if ($parts.Count -gt 2) { $parts[2] } else { "" }

    $cpu = if ($rid -match "-") { ($rid -split "-")[1] } else { $rid }

    return @{
        Framework   = $framework
        Rid         = $rid
        Cpu         = $cpu
        LastSection = $lastSection
        FullName    = $FileName
    }
}

function Get-PublishProfileSortOrder {
    param([string]$Rid)

    # Define sort order priority
    # Lower numbers = earlier in sequence
    switch ($Rid) {
        "win-x64"   { return 1 }
        "win-x86"   { return 2 }
        "win7-x64"  { return 3 }
        "win7-x86"  { return 4 }
        default {
            # Linux RIDs come after Windows
            if ($Rid -like "linux-*") {
                return 5
            }
            # macOS RIDs come after Linux
            elseif ($Rid -like "osx-*") {
                return 6
            }
            # Unknown RIDs go last
            else {
                return 99
            }
        }
    }
}

function Invoke-DotnetPublish {
    param(
        [hashtable]$ProfileInfo,
        [string]$ProjectFile
    )

    $framework = $ProfileInfo.Framework
    $rid = $ProfileInfo.Rid
    $lastSection = $ProfileInfo.LastSection
    $fileName = $ProfileInfo.FullName

    # Determine user-friendly platform name
    $platformName = switch ($rid) {
        "win7-x64" { "Windows 64-bit" }
        "win7-x86" { "Windows 32-bit" }
        "linux-x64" { "Linux 64-bit" }
        "osx-x64" { "macOS Intel" }
        "osx-arm64" { "macOS Apple Silicon" }
        default { $rid }
    }

    Write-Log "Building for $platformName" -Level "INFO"
    Write-Log "Starting publish for profile" -Level "DEBUG" -Variables @{
        "Framework"   = $framework;
        "Rid"         = $rid;
        "Cpu"         = $ProfileInfo.Cpu;
        "LastSection" = $lastSection;
        "ProfileFile" = $fileName
    }
    Write-Log "Preparing build environment for $platformName" -Level "VERBOSE" -Variables @{ "Framework" = $framework; "Rid" = $rid }

    # Build publish command
    if ($framework -eq "net48") {
        $publishCommand = "dotnet publish $ProjectFile -c Release --framework $framework /p:PublishProfile=$fileName.pubxml /p:RuntimeIdentifier="
        Write-Log "Using .NET Framework publish command" -Level "DEBUG" -Variables @{ "Command" = $publishCommand }
    }
    else {
        $publishCommand = "dotnet publish $ProjectFile -c Release --framework $framework /p:PublishProfile=$fileName.pubxml"
        Write-Log "Using .NET Core publish command" -Level "DEBUG" -Variables @{ "Command" = $publishCommand }
    }

    Write-Log "Compiling application..." -Level "INFO"
    Write-Log "Executing publish command" -Level "DEBUG" -Variables @{ "Command" = $publishCommand }
    Write-Log "Starting dotnet publish process" -Level "VERBOSE" -Variables @{ "Platform" = $platformName; "Framework" = $framework }
    Invoke-Expression $publishCommand

    if ($LASTEXITCODE -ne 0) {
        Write-Log "Build failed for $platformName" -Level "ERROR" -Variables @{ "ExitCode" = $LASTEXITCODE; "Command" = $publishCommand }
        throw "Publish failed with exit code: $LASTEXITCODE"
    }

    Write-Log "Build completed successfully for $platformName" -Level "INFO"
    Write-Log "Publish completed successfully" -Level "DEBUG" -Variables @{ "Framework" = $framework; "Rid" = $rid }

    # Move build output to timestamped directory
    $projectDir = Split-Path $ProjectFile -Parent
    $defaultPublishFolder = if ($framework -eq "net48") {
        if ([string]::IsNullOrEmpty($lastSection)) {
            ".\$OutputDir\build\$framework\$rid"
        } else {
            ".\$OutputDir\build\$lastSection\$framework\$rid"
        }
    } else {
        if ([string]::IsNullOrEmpty($lastSection)) {
            "$projectDir\$OutputDir\build\$framework\$rid"
        } else {
            "$projectDir\$OutputDir\build\$lastSection\$framework\$rid"
        }
    }

    $timestampedFolder = if ([string]::IsNullOrEmpty($lastSection)) {
        ".\$OutputDir\build_$BuildTimestamp\$framework\$rid"
    } else {
        ".\$OutputDir\build_$BuildTimestamp\$lastSection\$framework\$rid"
    }

    if (-not (Test-Path $defaultPublishFolder)) {
        Write-Log "Default publish folder not found after build" -Level "ERROR" -Variables @{ "Path" = $defaultPublishFolder }
        throw "Default publish folder not found: $defaultPublishFolder"
    }

    # Ensure parent directory exists
    $timestampedParent = Split-Path $timestampedFolder -Parent
    if (-not (Test-Path $timestampedParent)) {
        New-Item -ItemType Directory -Path $timestampedParent -Force | Out-Null
        Write-Log "Created parent directory for timestamped folder" -Level "DEBUG" -Variables @{ "Path" = $timestampedParent }
    }

    # If timestamped folder already exists, error out (no removes allowed)
    if (Test-Path $timestampedFolder) {
        throw "Timestamped folder already exists: $timestampedFolder"
    }

    # Move the default to timestamped
    Move-Item $defaultPublishFolder $timestampedFolder
    Write-Log "Moved build output to timestamped directory" -Level "INFO" -Variables @{ "From" = $defaultPublishFolder; "To" = $timestampedFolder }
    }

function Remove-LeftoverFiles {
    param(
        [string]$BuildFolder,
        [string]$CurrentRid
    )

    Write-Log "Checking for leftover files from previous builds" -Level "DEBUG" -Variables @{ "BuildFolder" = $BuildFolder }

    # Get all subdirectories in the build folder
    $subdirs = Get-ChildItem $BuildFolder -Directory -ErrorAction SilentlyContinue
    foreach ($subdir in $subdirs) {
        # Skip the current RID folder
        if ($subdir.Name -eq $CurrentRid) {
            continue
        }

        # Check if this looks like an old build folder (contains framework names)
        $isOldBuild = $subdir.Name -match "(net48|net9\.0|win7-|linux-|osx-|selfcontained)"
        if ($isOldBuild) {
            Write-Log "Removing leftover build folder: $($subdir.Name)" -Level "DEBUG" -Variables @{ "Folder" = $subdir.FullName }
            try {
                Remove-Item $subdir.FullName -Recurse -Force
            }
            catch {
                Write-Log "Failed to remove leftover folder: $($subdir.Name)" -Level "WARN" -Variables @{ "Error" = $_.Exception.Message }
            }
        }
    }
}

function New-Archive {
    param(
        [hashtable]$ProfileInfo,
        [string]$HpVersion,
        [string]$SevenZipPath,
        [string]$OutputDir
    )

    $framework = $ProfileInfo.Framework
    $rid = $ProfileInfo.Rid
    $lastSection = $ProfileInfo.LastSection
    $topLevelFolder = "HoloPatcher $HpVersion-$rid"

    # Determine publish folder path using timestamped build directory
    if ([string]::IsNullOrEmpty($lastSection)) {
        $publishFolder = ".\$OutputDir\build_$BuildTimestamp\$framework\$rid"
    }
    else {
        # For self-contained builds, the output goes to the timestamped build folder
        $publishFolder = ".\$OutputDir\build_$BuildTimestamp\$lastSection\$framework\$rid"
    }

    # Determine user-friendly platform name for archive
    $platformName = switch ($rid) {
        "win7-x64" { "Windows 64-bit" }
        "win7-x86" { "Windows 32-bit" }
        "linux-x64" { "Linux 64-bit" }
        "osx-x64" { "macOS Intel" }
        "osx-arm64" { "macOS Apple Silicon" }
        default { $rid }
    }

    # Clean up leftover files from previous builds (only within current timestamped build)
    #$buildRoot = if ([string]::IsNullOrEmpty($lastSection)) {
    #    ".\$OutputDir\build_$BuildTimestamp\$framework"
    #}
    #else {
    #    ".\$OutputDir\build_$BuildTimestamp\$lastSection\$framework"
    #}
    # Remove-LeftoverFiles -BuildFolder $buildRoot -CurrentRid $rid  # Commented: No removes allowed

    Write-Log "Creating archive for $platformName" -Level "INFO"
    Write-Log "Preparing archive" -Level "DEBUG" -Variables @{
        "PublishFolder"  = $publishFolder;
        "TopLevelFolder" = $topLevelFolder;
        "Rid"            = $rid
    }

    # Rename folder for archive
    if (Test-Path $publishFolder) {
        $renamedFolder = if ([string]::IsNullOrEmpty($lastSection)) {
            ".\$OutputDir\build_$BuildTimestamp\$framework\$topLevelFolder"
        }
        else {
            ".\$OutputDir\build_$BuildTimestamp\$lastSection\$framework\$topLevelFolder"
        }

        Write-Log "Renaming folder for archive" -Level "DEBUG" -Variables @{
            "From" = $publishFolder;
            "To"   = $renamedFolder
        }

        # Check if target already exists and error out (no removes allowed)
        if (Test-Path $renamedFolder) {
            throw "Target folder already exists: $renamedFolder"
        }

        # Ensure parent directory exists
        $parentDir = Split-Path $renamedFolder -Parent
        if (-not (Test-Path $parentDir)) {
            Write-Log "Creating parent directory" -Level "DEBUG" -Variables @{ "Path" = $parentDir }
            New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
        }

        # Validate that source and target are different
        if ($publishFolder -eq $renamedFolder) {
            Write-Log "Source and target folders are the same, skipping rename" -Level "DEBUG" -Variables @{ "Path" = $publishFolder }
        }
        else {
            try {
                # Use Move-Item instead of Rename-Item for full path operations
                Move-Item $publishFolder $renamedFolder
                Write-Log "Folder renamed successfully" -Level "DEBUG" -Variables @{ "From" = $publishFolder; "To" = $renamedFolder }
                $publishFolder = $renamedFolder
            }
            catch {
                Write-Log "Failed to rename folder" -Level "ERROR" -Variables @{
                    "From"  = $publishFolder;
                    "To"    = $renamedFolder;
                    "Error" = $_.Exception.Message
                }
                throw "Failed to rename folder from '$publishFolder' to '$renamedFolder': $($_.Exception.Message)"
            }
        }
    }
    else {
        Write-Log "Build output folder not found for $platformName" -Level "ERROR" -Variables @{ "Path" = $publishFolder }
        throw "Publish folder not found: $publishFolder"
    }

    # Create docs folder and copy files
    $docsFolder = Join-Path $publishFolder "docs"
    Write-Log "Adding documentation files" -Level "INFO"
    Write-Log "Creating docs folder" -Level "DEBUG" -Variables @{ "Path" = $docsFolder }
    New-Item -ItemType Directory -Path $docsFolder -Force | Out-Null

    $filesToCopy = @(
        @{ Source = "LICENSE.TXT"; Dest = "LICENSE.TXT" }
    )

    foreach ($file in $filesToCopy) {
        if (Test-Path $file.Source) {
            $destPath = Join-Path $docsFolder $file.Dest
            Write-Log "Copying file to docs" -Level "DEBUG" -Variables @{
                "Source"      = $file.Source;
                "Destination" = $destPath
            }
            Copy-Item $file.Source $destPath -Force
        }
        else {
            Write-Log "Source file not found, skipping" -Level "WARN" -Variables @{ "File" = $file.Source }
        }
    }

    # Create archive
    $archiveFile = "$OutputDir\$rid.zip"
    # Delete existing archive to avoid updates/removals in 7-Zip
    if (Test-Path $archiveFile) {
        Remove-Item $archiveFile -Force
        Write-Log "Deleted existing archive" -Level "DEBUG" -Variables @{ "Path" = $archiveFile }
    }
    Write-Log "Compressing files..." -Level "INFO"
    Write-Log "Creating archive" -Level "DEBUG" -Variables @{
        "ArchiveFile"  = $archiveFile;
        "SourceFolder" = $publishFolder;
        "SevenZipPath" = $SevenZipPath
    }

    try {
        $sevenZipArgs = @("a", "-tzip", $archiveFile, "$publishFolder\*")
        Write-Log "Executing 7-Zip command" -Level "DEBUG" -Variables @{
            "Executable" = $SevenZipPath;
            "Arguments"  = ($sevenZipArgs -join " ")
        }

        & $SevenZipPath $sevenZipArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Log "Archive creation failed for $platformName" -Level "ERROR" -Variables @{ "ExitCode" = $LASTEXITCODE; "Command" = ($sevenZipArgs -join " ") }
            throw "Archive creation failed with exit code: $LASTEXITCODE"
        }

        $archiveSize = (Get-Item $archiveFile).Length
        $archiveSizeMB = [math]::Round($archiveSize / 1MB, 1)
        Write-Log "Archive created successfully for $platformName ($archiveSizeMB MB)" -Level "INFO"
        Write-Log "Archive created successfully" -Level "DEBUG" -Variables @{
            "ArchiveFile" = $archiveFile;
            "Size"        = $archiveSize
        }
    }
    catch {
        Write-Log "Archive creation failed" -Level "ERROR" -Variables @{ "Error" = $_.Exception.Message }
        throw
    }

    # Preserve built files in timestamped build folder for future reference
    Write-Log "Built files preserved in dist/build_$BuildTimestamp folder" -Level "INFO"
    Write-Log "Archive process completed" -Level "DEBUG" -Variables @{
        "Framework"   = $framework;
        "Rid"         = $rid;
        "ArchiveFile" = $archiveFile
    }
}

# Main execution
try {
    Write-Log "Error action preference set" -Level "DEBUG" -Variables @{ "ErrorAction" = $ErrorAction }
    Write-Log "Starting HoloPatcher release build process" -Level "INFO" -Variables @{
        "HpVersion"         = $HpVersion;
        "ProjectFile"        = $ProjectFile;
        "PublishProfilesDir" = $PublishProfilesDir
    }

    Write-Log "Checking system requirements..." -Level "INFO"
    Test-RequiredTools

    # Ensure dist folder structure exists with timestamped build directory
    Write-Log "Setting up output directories..." -Level "INFO"
    $distDir = ".\$OutputDir"
    $buildDir = ".\$OutputDir\build_$BuildTimestamp"

    if (-not (Test-Path $distDir)) {
        Write-Log "Creating dist directory" -Level "DEBUG" -Variables @{ "Path" = $distDir }
        New-Item -ItemType Directory -Path $distDir -Force | Out-Null
    }

    if (-not (Test-Path $buildDir)) {
        Write-Log "Creating timestamped build directory" -Level "DEBUG" -Variables @{ "Path" = $buildDir }
        New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
    }

    # Create log file in the timestamped build directory
    $buildLogFile = Join-Path $buildDir "build.log"
    Write-Log "Log file created in build directory" -Level "DEBUG" -Variables @{ "LogFile" = $buildLogFile }

    $publishProfiles = Get-ChildItem $PublishProfilesDir -Filter "*.pubxml"
    Write-Log "Found $($publishProfiles.Count) build targets" -Level "INFO"
    Write-Log "Found publish profiles" -Level "DEBUG" -Variables @{ "Count" = $publishProfiles.Count; "Profiles" = ($publishProfiles.Name -join ", ") }

    # Sort profiles according to desired order: win-x64, win-x86, win7-x64, win7-x86, linux, mac
    $sortedProfiles = $publishProfiles | ForEach-Object {
        $profileInfo = Get-PublishProfileInfo -FileName $_.BaseName
        $sortOrder = Get-PublishProfileSortOrder -Rid $profileInfo.Rid
        # Add sort properties to the object for sorting
        $_ | Add-Member -MemberType NoteProperty -Name "SortOrder" -Value $sortOrder -Force
        $_ | Add-Member -MemberType NoteProperty -Name "Framework" -Value $profileInfo.Framework -Force
        $_
    } | Sort-Object -Property SortOrder, Framework, FullName

    Write-Log "Sorted profiles in build order" -Level "DEBUG" -Variables @{ "Order" = ($sortedProfiles | ForEach-Object { $_.BaseName }) -join " -> " }

    $successCount = 0
    $failureCount = 0
    $totalCount = $sortedProfiles.Count

    foreach ($publishProfile in $sortedProfiles) {
        try {
            Write-Log "Starting profile processing" -Level "DEBUG" -Variables @{ "Profile" = $publishProfile.BaseName }

            $profileInfo = Get-PublishProfileInfo -FileName $publishProfile.BaseName
            Invoke-DotnetPublish -ProfileInfo $profileInfo -ProjectFile $ProjectFile
            New-Archive -ProfileInfo $profileInfo -HpVersion $HpVersion -SevenZipPath $SevenZipPath -OutputDir $OutputDir
            $successCount++

            Write-Log "Profile completed successfully" -Level "DEBUG" -Variables @{
                "Profile"      = $publishProfile.BaseName;
                "SuccessCount" = $successCount;
                "FailureCount" = $failureCount;
                "TotalCount"   = $totalCount
            }
        }
        catch {
            $failureCount++
            Write-Log "Build failed for profile $($publishProfile.BaseName)" -Level "ERROR" -Variables @{
                "Profile"      = $publishProfile.BaseName;
                "Error"        = $_.Exception.Message;
                "ErrorType"    = $_.Exception.GetType().Name;
                "StackTrace"   = $_.ScriptStackTrace;
                "SuccessCount" = $successCount;
                "FailureCount" = $failureCount;
                "TotalCount"   = $totalCount
            }
            Write-Log "Continuing to next profile" -Level "WARN" -Variables @{ "RemainingProfiles" = ($totalCount - $successCount - $failureCount) }
        }
    }

    if ($failureCount -eq 0) {
        Write-Log "All builds completed successfully! ($successCount/$totalCount)" -Level "INFO"
    } else {
        Write-Log "Build process completed with $failureCount failures ($successCount/$totalCount successful)" -Level "WARN"
    }
    Write-Log "All builds completed" -Level "DEBUG" -Variables @{
        "SuccessCount" = $successCount;
        "FailureCount" = $failureCount;
        "TotalCount"   = $totalCount;
        "LogFile"      = $LogFile
    }
}
catch {
    Write-Log "Build process failed" -Level "ERROR" -Variables @{
        "Error"   = $_.Exception.Message;
        "LogFile" = $LogFile
    }
    throw
}

Write-Host "Press any key to continue..."
#$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
