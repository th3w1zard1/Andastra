#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates ScriptDefs.cs from KOTOR NSS script definition files.

.DESCRIPTION
    Parses k1_nwscript.nss and k2_nwscript.nss to extract constants and function declarations,
    then generates the C# ScriptDefs.cs file with proper formatting.

    This script is completely standalone and does not depend on PyKotor or any external tools.

.EXAMPLE
    .\scripts\Generate-ScriptDefs.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Paths
$RepoRoot = Split-Path -Parent $PSScriptRoot
$K1NssPath = Join-Path $RepoRoot "include\k1_nwscript.nss"
$K2NssPath = Join-Path $RepoRoot "include\k2_nwscript.nss"
$OutputPath = Join-Path $RepoRoot "src\CSharpKOTOR\Common\Script\ScriptDefs.cs"

Write-Host "Generating ScriptDefs.cs from NSS files..." -ForegroundColor Cyan
Write-Host "  K1: $K1NssPath"
Write-Host "  K2: $K2NssPath"
Write-Host "  Output: $OutputPath"

# Type mapping from NSS to C# DataType enum
$TypeMap = @{
    'int'       = 'DataType.Int'
    'float'     = 'DataType.Float'
    'string'    = 'DataType.String'
    'void'      = 'DataType.Void'
    'object'    = 'DataType.Object'
    'vector'    = 'DataType.Vector'
    'location'  = 'DataType.Location'
    'effect'    = 'DataType.Effect'
    'event'     = 'DataType.Event'
    'talent'    = 'DataType.Talent'
    'action'    = 'DataType.Action'
    'object_id' = 'DataType.Object'  # Non-standard type alias
}

function Get-DataType {
    param([string]$TypeName)

    # Normalize to lowercase for lookup
    $normalizedType = $TypeName.ToLower()

    if ($TypeMap.ContainsKey($normalizedType)) {
        return $TypeMap[$normalizedType]
    }

    # Unknown type - return null
    return $null
}

function Parse-NssConstant {
    param([string]$Line)

    # Pattern: type CONSTANT_NAME = value;
    # Constants MUST be all UPPERCASE with underscores (no lowercase letters)
    # This excludes global variables like sLanguage
    if ($Line -match '^\s*(int|float|string)\s+([A-Z_][A-Z0-9_]*)\s*=\s*(.+?)\s*;') {
        $type = $Matches[1]
        $name = $Matches[2]
        $value = $Matches[3].Trim()

        # Verify name is ALL UPPERCASE (no lowercase letters)
        if ($name -cmatch '[a-z]') {
            # Has lowercase letters - skip this as it's a variable, not a constant
            return $null
        }

        # Format value based on type
        if ($type -eq 'string') {
            # String value - keep quotes
            $formattedValue = $value
        }
        elseif ($type -eq 'float') {
            # Float value - ensure it has 'f' suffix
            if ($value -notmatch 'f$') {
                $formattedValue = "${value}f"
            }
            else {
                $formattedValue = $value
            }
        }
        else {
            # Int value - use as-is (could be hex like 0x...)
            if ($value -match '^0x') {
                # Convert hex to decimal
                $formattedValue = [Convert]::ToInt32($value, 16).ToString()
            }
            else {
                $formattedValue = $value
            }
        }

        $dataType = Get-DataType -TypeName $type
        if (-not $dataType) {
            # Unknown type, skip
            return $null
        }

        return @{
            Type  = $dataType
            Name  = $name
            Value = $formattedValue
        }
    }

    return $null
}

function Parse-NssFunction {
    param([string[]]$Lines, [int]$StartIndex)

    $line = $Lines[$StartIndex]

    # Pattern: returnType functionName(params);
    # Must be a forward declaration (ends with semicolon, not opening brace)
    if ($line -match '^\s*(\w+)\s+(\w+)\s*\(([^)]*)\)\s*;') {
        $returnType = $Matches[1]
        $functionName = $Matches[2]
        $paramsString = $Matches[3].Trim()

        # Get documentation (comments above the function)
        $docLines = @()
        for ($i = $StartIndex - 1; $i -ge 0 -and $i -ge ($StartIndex - 50); $i--) {
            $prevLine = $Lines[$i]
            if ($prevLine -match '^\s*//') {
                $docLines = @($prevLine) + $docLines
            }
            elseif ($prevLine -match '^\s*$') {
                # Empty line, continue
            }
            else {
                break
            }
        }

        # Add the function signature line to documentation
        $docLines += $line.TrimEnd()
        # Escape the documentation string for C# string literal
        $documentation = ($docLines -join [Environment]::NewLine)
        $documentation = $documentation.Replace('\', '\\')    # Escape backslashes first
        $documentation = $documentation.Replace('"', '\"')     # Escape quotes
        $documentation = $documentation.Replace("`r", '\r')    # Escape carriage returns
        $documentation = $documentation.Replace("`n", '\n')    # Escape newlines

        # Parse parameters
        $params = @()
        if ($paramsString) {
            # Split by comma, but not inside parentheses or brackets
            $paramParts = @()
            $currentParam = ""
            $depth = 0
            $bracketDepth = 0

            for ($i = 0; $i -lt $paramsString.Length; $i++) {
                $char = $paramsString[$i]
                if ($char -eq '(') { $depth++ }
                elseif ($char -eq ')') { $depth-- }
                elseif ($char -eq '[') { $bracketDepth++ }
                elseif ($char -eq ']') { $bracketDepth-- }
                elseif ($char -eq ',' -and $depth -eq 0 -and $bracketDepth -eq 0) {
                    $paramParts += $currentParam.Trim()
                    $currentParam = ""
                    continue
                }
                $currentParam += $char
            }
            if ($currentParam) {
                $paramParts += $currentParam.Trim()
            }

            foreach ($paramPart in $paramParts) {
                # Pattern: type name [= default]
                if ($paramPart -match '^\s*(\w+)\s+(\w+)\s*(?:=\s*(.+))?\s*$') {
                    $paramType = $Matches[1]
                    $paramName = $Matches[2]
                    $paramDefault = if ($Matches[3]) { $Matches[3].Trim() } else { $null }

                    $paramDataType = Get-DataType -TypeName $paramType
                    if (-not $paramDataType) {
                        # Unknown parameter type, skip this param (function will be incomplete)
                        continue
                    }

                    # Format default value
                    $formattedDefault = if ($paramDefault) {
                        # Check if it's a numeric literal
                        if ($paramDefault -match '^-?\d+$') {
                            # Integer literal
                            $paramDefault
                        }
                        elseif ($paramDefault -match '^-?\d+\.\d+f?$') {
                            # Float literal - ensure 'f' suffix
                            if ($paramDefault -notmatch 'f$') {
                                "${paramDefault}f"
                            }
                            else {
                                $paramDefault
                            }
                        }
                        elseif ($paramDefault -match '^".*"$') {
                            # String literal
                            $paramDefault
                        }
                        elseif ($paramDefault -match '^\[[\d\.,\s]+\]$') {
                            # Vector literal [x, y, z]
                            $vectorParts = $paramDefault -replace '[\[\]]', '' -split ','
                            $x = $vectorParts[0].Trim()
                            $y = $vectorParts[1].Trim()
                            $z = $vectorParts[2].Trim()
                            "new Vector3(${x}f, ${y}f, ${z}f)"
                        }
                        else {
                            # Constant reference - try to resolve to numeric value
                            # Special built-in constants
                            if ($paramDefault -eq 'OBJECT_SELF') {
                                'OBJECT_SELF'
                            }
                            elseif ($paramDefault -eq 'OBJECT_INVALID') {
                                'OBJECT_INVALID'
                            }
                            elseif ($allConstants.ContainsKey($paramDefault)) {
                                # Resolve to numeric value from constants table
                                $allConstants[$paramDefault]
                            }
                            else {
                                # Unknown constant - use as-is (might cause compile error)
                                $paramDefault
                            }
                        }
                    }
                    else {
                        'null'
                    }

                    $params += @{
                        Type    = $paramDataType
                        Name    = $paramName
                        Default = $formattedDefault
                    }
                }
            }
        }

        $returnDataType = Get-DataType -TypeName $returnType
        if (-not $returnDataType) {
            # Unknown return type, skip this function
            return $null
        }

        return @{
            ReturnType    = $returnDataType
            Name          = $functionName
            Params        = $params
            Documentation = $documentation
        }
    }

    return $null
}

function Parse-NssFile {
    param([string]$FilePath)

    Write-Host "Parsing $FilePath..." -ForegroundColor Yellow

    $lines = Get-Content $FilePath
    $constants = @()
    $functions = @()

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        # Skip preprocessor directives
        if ($line -match '^\s*#') {
            continue
        }

        # Try to parse as constant
        $constant = Parse-NssConstant -Line $line
        if ($constant) {
            $constants += $constant
            continue
        }

        # Try to parse as function
        try {
            $function = Parse-NssFunction -Lines $lines -StartIndex $i
            if ($function) {
                $functions += $function
            }
        }
        catch {
            # Skip functions that fail to parse
            $errMsg = $_.Exception.Message
            Write-Warning "Failed to parse function at line $i : $errMsg"
        }
    }

    Write-Host "  Found $($constants.Count) constants and $($functions.Count) functions" -ForegroundColor Green

    return @{
        Constants = $constants
        Functions = $functions
    }
}

function Generate-ConstantCode {
    param($Constant, [bool]$IsLast = $false)

    $comma = if ($IsLast) { "" } else { "," }
    return "        new ScriptConstant($($Constant.Type), `"$($Constant.Name)`", $($Constant.Value))$comma"
}

function Generate-FunctionCode {
    param($Function, [bool]$IsLast = $false)

    # Generate parameter list
    $paramCode = @()
    foreach ($param in $Function.Params) {
        $paramCode += "new ScriptParam($($param.Type), `"$($param.Name)`", $($param.Default))"
    }

    $paramListCode = if ($paramCode.Count -gt 0) {
        "new List<ScriptParam>() { $($paramCode -join ', ') }"
    }
    else {
        "new List<ScriptParam>()"
    }

    $doc = $Function.Documentation
    $comma = if ($IsLast) { "" } else { "," }

    return @"
        new ScriptFunction(
            $($Function.ReturnType),
            "$($Function.Name)",
            $paramListCode,
            "$doc",
            "$doc"
        )$comma
"@
}

# Parse both NSS files
$k1Data = Parse-NssFile -FilePath $K1NssPath
$k2Data = Parse-NssFile -FilePath $K2NssPath

# Build a lookup table of all constants for resolving default values
$allConstants = @{}
foreach ($const in $k1Data.Constants) {
    $allConstants[$const.Name] = $const.Value
}
foreach ($const in $k2Data.Constants) {
    if (-not $allConstants.ContainsKey($const.Name)) {
        $allConstants[$const.Name] = $const.Value
    }
}

# Generate C# code
Write-Host "`nGenerating C# code..." -ForegroundColor Cyan

# Build constant lists
$k1ConstantsCode = ""
for ($i = 0; $i -lt $k1Data.Constants.Count; $i++) {
    $k1ConstantsCode += (Generate-ConstantCode -Constant $k1Data.Constants[$i] -IsLast ($i -eq $k1Data.Constants.Count - 1)) + "`r`n"
}

$k2ConstantsCode = ""
for ($i = 0; $i -lt $k2Data.Constants.Count; $i++) {
    $k2ConstantsCode += (Generate-ConstantCode -Constant $k2Data.Constants[$i] -IsLast ($i -eq $k2Data.Constants.Count - 1)) + "`r`n"
}

# Build function lists
$k1FunctionsCode = ""
for ($i = 0; $i -lt $k1Data.Functions.Count; $i++) {
    $k1FunctionsCode += (Generate-FunctionCode -Function $k1Data.Functions[$i] -IsLast ($i -eq $k1Data.Functions.Count - 1)) + "`r`n"
}

$k2FunctionsCode = ""
for ($i = 0; $i -lt $k2Data.Functions.Count; $i++) {
    $k2FunctionsCode += (Generate-FunctionCode -Function $k2Data.Functions[$i] -IsLast ($i -eq $k2Data.Functions.Count - 1)) + "`r`n"
}

$csCode = @"
using System.Collections.Generic;
using CSharpKOTOR.Common;
using CSharpKOTOR.Common.Script;

namespace CSharpKOTOR.Common.Script
{

    /// <summary>
    /// NWScript constant and function definitions for KOTOR and TSL.
    /// Generated from k1_nwscript.nss and k2_nwscript.nss using Generate-ScriptDefs.ps1.
    /// </summary>
    public static class ScriptDefs
    {
        // Built-in constants (not defined in NSS files but used as defaults or commonly referenced)
        public const int OBJECT_SELF = 0;
        public const int OBJECT_INVALID = 1;
        public const int TRUE = 1;
        public const int FALSE = 0;

        /// <summary>
        /// KOTOR (Knights of the Old Republic) script constants.
        /// </summary>
        public static readonly List<ScriptConstant> KOTOR_CONSTANTS = new List<ScriptConstant>()
        {
$k1ConstantsCode        };

        /// <summary>
        /// TSL (The Sith Lords, also known as K2) script constants.
        /// </summary>
        public static readonly List<ScriptConstant> TSL_CONSTANTS = new List<ScriptConstant>()
        {
$k2ConstantsCode        };

        /// <summary>
        /// KOTOR (Knights of the Old Republic) script functions.
        /// </summary>
        public static readonly List<ScriptFunction> KOTOR_FUNCTIONS = new List<ScriptFunction>()
        {
$k1FunctionsCode        };

        /// <summary>
        /// TSL (The Sith Lords, also known as K2) script functions.
        /// </summary>
        public static readonly List<ScriptFunction> TSL_FUNCTIONS = new List<ScriptFunction>()
        {
$k2FunctionsCode        };
    }
}
"@

# Write output file
Write-Host "Writing output to $OutputPath..." -ForegroundColor Cyan
[System.IO.File]::WriteAllText($OutputPath, $csCode, [System.Text.Encoding]::UTF8)

Write-Host "`nDone! Generated ScriptDefs.cs successfully." -ForegroundColor Green
Write-Host "  Total K1 Constants: $($k1Data.Constants.Count)"
Write-Host "  Total K1 Functions: $($k1Data.Functions.Count)"
Write-Host "  Total K2 Constants: $($k2Data.Constants.Count)"
Write-Host "  Total K2 Functions: $($k2Data.Functions.Count)"

