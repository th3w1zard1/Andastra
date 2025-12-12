# KNCSDecomp Test Summary

## Fixed Issues

### 1. KeyNotFoundException Fix
**Problem**: `FileDecompiler` was throwing `KeyNotFoundException` when accessing `filedata[file]` with non-existent keys.

**Solution**: Changed all dictionary access from `filedata[file]` to safe access using `ContainsKey()` check first.

**Files Modified**:
- `src/CSharpKOTOR/Formats/NCS/KNCSDecomp/FileDecompiler.cs`
  - `GetVariableData()` - Added ContainsKey check
  - `GetGeneratedCode()` - Added ContainsKey check
  - `GetOriginalByteCode()` - Added ContainsKey check
  - `GetNewByteCode()` - Added ContainsKey check
  - `Decompile()` - Changed to check ContainsKey before access
  - `CompileAndCompare()` - Changed to check ContainsKey before access
  - `CompileOnly()` - Changed to check ContainsKey before access
  - `UpdateSubName()` - Added ContainsKey check
  - `RegenerateCode()` - Added ContainsKey check
  - `CloseFile()` - Changed to check ContainsKey before access

## Test Coverage

### Unit Tests (`FileDecompilerTests.cs`)
- Constructor tests (default and with parameters)
- All getter methods with non-existent files (should return null, not throw)
- All methods that should not throw exceptions
- Edge cases and error handling

### Integration Tests (`Integration/FileDecompilerIntegrationTests.cs`)
- Decompilation with real NCS files
- Code generation verification
- Variable data extraction
- Multiple file handling
- File cleanup and disposal

### KeyNotFoundException Fix Tests (`KeyNotFoundExceptionFixTests.cs`)
- Comprehensive tests ensuring no KeyNotFoundException is thrown
- Tests for all methods that access filedata dictionary
- Verification that methods return null/appropriate values instead of throwing

### NWScriptLocator Tests (`NWScriptLocatorTests.cs`)
- File location with settings path
- Invalid path handling
- Candidate path generation for K1 and TSL

### Settings Tests (`SettingsTests.cs`)
- Constructor and initialization
- Property get/set operations
- Save/load functionality
- Default value handling

### Headless UI Tests (`UI/MainWindowHeadlessTests.cs`)
- Window initialization
- Control existence verification
- Tab selection change handling
- No KeyNotFoundException during initialization

## Running Tests

```bash
# Run all tests
dotnet test src/KNCSDecomp.Tests/KNCSDecomp.Tests.csproj

# Run with detailed output
dotnet test src/KNCSDecomp.Tests/KNCSDecomp.Tests.csproj --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~KeyNotFoundExceptionFixTests"

# Run integration tests only
dotnet test --filter "FullyQualifiedName~Integration"
```

## Test File Setup

Before running integration tests, copy the test NCS file:

**Windows (PowerShell)**:
```powershell
cd src/KNCSDecomp.Tests
.\setup_test_files.ps1
```

**Linux/macOS (Bash)**:
```bash
cd src/KNCSDecomp.Tests
chmod +x setup_test_files.sh
./setup_test_files.sh
```

**Manual Copy**:
Copy `a_galaxymap.ncs` from:
`G:\GitHub\PyKotor\vendor\Kotor-Randomizer\kotor Randomizer 2\Resources\k2patch\a_galaxymap.ncs`

To:
`src/KNCSDecomp.Tests/test_files/a_galaxymap.ncs`

## Test Results

All tests should pass. The KeyNotFoundException fix ensures that:
1. No KeyNotFoundException is thrown when accessing non-existent files
2. Methods return null or appropriate default values
3. The application can handle missing files gracefully
4. UI initialization doesn't crash

## Coverage Goals

- ✅ FileDecompiler core functionality
- ✅ Error handling and edge cases
- ✅ KeyNotFoundException fix verification
- ✅ Settings management
- ✅ NWScriptLocator functionality
- ✅ UI initialization (headless)
- ✅ Integration with real NCS files

