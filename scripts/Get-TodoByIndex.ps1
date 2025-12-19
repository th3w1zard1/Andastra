#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory=$true)]
    [int]$Index
)

$ErrorActionPreference = "Stop"

$files = Get-ChildItem -Path . -Recurse -Include *.cs,*.csx,*.axaml.cs,*.ps1,*.py -File
$todos = Select-String -Path $files -Pattern 'TODO: ' | Select-Object -First 1000

$count = ($todos | Measure-Object).Count
Write-Host "Found $count TODOs"

if ($count -ge $Index) {
    $todo = $todos[$Index - 1]
    Write-Output "$($todo.Path):$($todo.LineNumber):$($todo.Line)"
} else {
    Write-Error "Index $Index is out of range. Found $count TODOs."
    exit 1
}

