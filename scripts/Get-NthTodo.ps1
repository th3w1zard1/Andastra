#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory=$true)]
    [int]$Index
)

$allTodos = @()
Get-ChildItem -Path . -Recurse -Include *.cs,*.csx,*.axaml.cs -File | 
    Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\|\\\.git\\' } | 
    ForEach-Object {
        $file = $_
        Select-String -Path $file.FullName -Pattern 'TODO: ' | 
            ForEach-Object {
                $allTodos += [PSCustomObject]@{
                    File = $file.FullName
                    Line = $_.LineNumber
                    Content = $_.Line
                }
            }
    }

if ($Index -lt 1 -or $Index -gt $allTodos.Count) {
    Write-Error "Index $Index is out of range (1-$($allTodos.Count))"
    exit 1
}

$todo = $allTodos[$Index - 1]
Write-Output "File: $($todo.File)"
Write-Output "Line: $($todo.Line)"
Write-Output "Content: $($todo.Content)"
