# UpdateSolution.ps1
# Updates the solution file to reflect consolidated projects
# Usage: .\scripts\UpdateSolution.ps1 [-DryRun]

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$script:RootPath = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $script:RootPath

$solutionPath = Join-Path $script:RootPath "Andastra.sln"

Write-Host "Updating Solution File" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "DRY RUN MODE" -ForegroundColor Yellow
}

# Define projects that should be in the solution
$projects = @(
    @{ Name = "Andastra"; Path = "src\Andastra\Andastra.csproj"; Guid = "3259AD72-321E-4A96-B20B-234B0F6416E9" },
    @{ Name = "GenerateScriptDefs"; Path = "scripts\GenerateScriptDefs\GenerateScriptDefs.csproj"; Guid = "95A87843-C444-4BDF-944A-DDCBF6B741BA" },
    @{ Name = "NCSDecomp"; Path = "src\Tools\NCSDecomp\NCSDecomp.csproj"; Guid = "3264EF53-941C-4B2A-A740-9E6CBA6D239C" },
    @{ Name = "KotorDiff"; Path = "src\Tools\KotorDiff\KotorDiff.csproj"; Guid = "0922E83D-EF35-4C77-9A26-6DC3D4B54A41" },
    @{ Name = "NSSComp"; Path = "src\Tools\NSSComp\NSSComp.csproj"; Guid = "60778EB1-8FD8-46F0-9719-1BF10E48B3EA" },
    @{ Name = "HoloPatcher"; Path = "src\Tools\HoloPatcher\HoloPatcher.csproj"; Guid = "FE5139A3-4BEF-48A9-A23E-4A045318830B" },
    @{ Name = "HoloPatcher.UI"; Path = "src\Tools\HoloPatcher.UI\HoloPatcher.UI.csproj"; Guid = "47B7732C-EA7D-446C-8309-C86FBDD871CB" },
    @{ Name = "HolocronToolset"; Path = "src\Tools\HolocronToolset\HolocronToolset.csproj"; Guid = "0538F16A-7412-4766-88F1-7B533E68FAAD" },
    @{ Name = "NCSDecomp.Tests"; Path = "src\Tests\NCSDecomp.Tests\NCSDecomp.Tests.csproj"; Guid = "088E9969-4906-41E6-BB56-3A99DE28D5A2" },
    @{ Name = "KotorDiff.Tests"; Path = "src\Tests\KotorDiff.Tests\KotorDiff.Tests.csproj"; Guid = "6B28A294-E047-4B32-B828-1EE18ECD2149" },
    @{ Name = "HolocronToolset.Tests"; Path = "src\Tests\HolocronToolset.Tests\HolocronToolset.Tests.csproj"; Guid = "6025114D-89C8-453E-8BAA-B5D0DD1D49C3" },
    @{ Name = "DLGFormatTests.Standalone"; Path = "src\Tests\DLGFormatTests.Standalone\DLGFormatTests.Standalone.csproj"; Guid = "CAE53519-2F30-4388-92DC-890873FE2F99" },
    @{ Name = "RtfDomParserAv"; Path = "src\RtfDomParserAvalonia\RtfDomParserAvalonia\RtfDomParserAv.csproj"; Guid = "09D95C63-3831-4BF5-9306-FF43432C07AC" },
    @{ Name = "DemoApp_AvRichTextBox"; Path = "src\AvRichTextBox\DemoApp_AvRichtextBox\DemoApp_AvRichTextBox.csproj"; Guid = "7E93C0CA-A4AB-40E8-9C6A-A6C25D43350D" },
    @{ Name = "TSLPatcher.Tests"; Path = "src\Andastra\Tests\TSLPatcher.Tests.csproj"; Guid = "3B56D123-7153-41FA-86F6-F5EAD5CB42F0" }
)

# Filter to only projects that exist
$existingProjects = @()
foreach ($proj in $projects) {
    $fullPath = Join-Path $script:RootPath $proj.Path
    if (Test-Path $fullPath) {
        $existingProjects += $proj
        Write-Host "  ✓ $($proj.Name)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $($proj.Name) - not found" -ForegroundColor Red
    }
}

Write-Host "`nFound $($existingProjects.Count) projects to include" -ForegroundColor White

# Read current solution
$solutionContent = Get-Content $solutionPath -Raw

# Generate new solution content
$sb = New-Object System.Text.StringBuilder
$sb.AppendLine("") | Out-Null
$sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00") | Out-Null
$sb.AppendLine("# Visual Studio Version 17") | Out-Null
$sb.AppendLine("VisualStudioVersion = 17.0.31903.59") | Out-Null
$sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1") | Out-Null

# Add projects
foreach ($proj in $existingProjects) {
    $sb.AppendLine("Project(`"{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`") = `"$($proj.Name)`", `"$($proj.Path)`", `"{$($proj.Guid)}`"") | Out-Null
    $sb.AppendLine("EndProject") | Out-Null
}

# Add solution folders
$sb.AppendLine("Project(`"{2150E333-8FDC-42A3-9474-1A3956D46DE8}`") = `"src`", `"src`", `"{827E0CD3-B72D-47B6-A68D-7590B98EB39B}`"") | Out-Null
$sb.AppendLine("EndProject") | Out-Null

# Global sections
$sb.AppendLine("Global") | Out-Null
$sb.AppendLine("	GlobalSection(SolutionConfigurationPlatforms) = preSolution") | Out-Null
$sb.AppendLine("		Debug|Any CPU = Debug|Any CPU") | Out-Null
$sb.AppendLine("		Debug|x64 = Debug|x64") | Out-Null
$sb.AppendLine("		Debug|x86 = Debug|x86") | Out-Null
$sb.AppendLine("		Release|Any CPU = Release|Any CPU") | Out-Null
$sb.AppendLine("		Release|x64 = Release|x64") | Out-Null
$sb.AppendLine("		Release|x86 = Release|x86") | Out-Null
$sb.AppendLine("	EndGlobalSection") | Out-Null
$sb.AppendLine("	GlobalSection(ProjectConfigurationPlatforms) = postSolution") | Out-Null

# Add configuration for each project
foreach ($proj in $existingProjects) {
    $guid = $proj.Guid
    $configs = @("Debug|Any CPU", "Debug|x64", "Debug|x86", "Release|Any CPU", "Release|x64", "Release|x86")
    foreach ($config in $configs) {
        $sb.AppendLine("		{$guid}.$config.ActiveCfg = $config") | Out-Null
        $sb.AppendLine("		{$guid}.$config.Build.0 = $config") | Out-Null
    }
}

$sb.AppendLine("	EndGlobalSection") | Out-Null
$sb.AppendLine("	GlobalSection(SolutionProperties) = preSolution") | Out-Null
$sb.AppendLine("		HideSolutionNode = FALSE") | Out-Null
$sb.AppendLine("	EndGlobalSection") | Out-Null
$sb.AppendLine("	GlobalSection(ExtensibilityGlobals) = postSolution") | Out-Null
$sb.AppendLine("		SolutionGuid = {02ABA7B4-132C-4F35-BBFC-1B1E935240DE}") | Out-Null
$sb.AppendLine("	EndGlobalSection") | Out-Null
$sb.AppendLine("EndGlobal") | Out-Null

# Write solution
if (-not $DryRun) {
    $sb.ToString() | Set-Content $solutionPath -Encoding UTF8
    Write-Host "`n✓ Solution file updated" -ForegroundColor Green
} else {
    Write-Host "`n[DRY RUN] Would update solution file" -ForegroundColor Yellow
    Write-Host $sb.ToString()
}

