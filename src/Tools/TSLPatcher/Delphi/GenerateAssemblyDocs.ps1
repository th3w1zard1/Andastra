# Generate Assembly Documentation from Ghidra Analysis
# This script helps generate inline assembly documentation for each line

param(
    [string]$GhidraProject = "C:\Users\boden\Andastra Ghidra Project.gpr",
    [string]$ProgramPath = "/TSLPatcher.exe"
)

Write-Host "Assembly Documentation Generator" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Function address mappings
$FunctionMappings = @{
    "TMainForm.Initialize" = @{
        Start = 0x00488000
        End = 0x00488100
        StackFrame = "EBP-04h (Self)"
    }
    "TMainForm.LoadConfiguration" = @{
        Start = 0x004880A0
        End = 0x00488200
        StackFrame = "EBP-04h (Self), EBP-08h (ConfigFile), EBP-0Ch (Config)"
    }
    "TMainForm.ValidateGamePath" = @{
        Start = 0x00486000
        End = 0x00486100
        StackFrame = "EBP-04h (Self)"
    }
    "TMainForm.StartPatching" = @{
        Start = 0x004860F0
        End = 0x00486200
        StackFrame = "EBP-04h (Self)"
    }
    "TMainForm.ProcessPatchOperations" = @{
        Start = 0x0047F000
        End = 0x00485000
        StackFrame = "EBP-04h (Self), EBP-08h (Config), EBP-0Ch (IniFile)"
    }
}

Write-Host "Function Address Mappings:" -ForegroundColor Green
foreach ($func in $FunctionMappings.Keys) {
    $info = $FunctionMappings[$func]
    Write-Host "  $func : 0x$($info.Start.ToString('X8')) - 0x$($info.End.ToString('X8'))" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Note: This script requires manual disassembly analysis." -ForegroundColor Yellow
Write-Host "Use Ghidra to disassemble each function and map instructions to Delphi code." -ForegroundColor Yellow

