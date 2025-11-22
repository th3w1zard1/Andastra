# HoloPatcher.NET - TODO

**Last Updated**: November 2024

---

## ✅ Completed

### Core Infrastructure

- ✅ **FileResource, ResourceIdentifier, LocationResult classes**
  - Ported from Python with case-insensitive comparison support
  - File: `src/TSLPatcher.Core/Resources/FileResource.cs`

- ✅ **Geometry Classes (Vector2, Vector3, Vector4)**
  - Full vector math operations
  - Quaternion support with compression/decompression
  - Euler angle conversion
  - Files: `src/TSLPatcher.Core/Common/Vector2.cs`, `Vector3.cs`, `Vector4.cs`

- ✅ **BinaryReader Improvements**
  - ReadVector2/3/4 methods
  - Enhanced string reading with encoding support
  - File: `src/TSLPatcher.Core/Common/BinaryReader.cs`

### Capsule System

- ✅ **LazyCapsule Implementation**
  - Lazy loading for performance
  - Metadata-only access without loading full resources
  - File: `src/TSLPatcher.Core/Formats/Capsule/LazyCapsule.cs`

- ✅ **Capsule.RemoveResource() Method**
  - Added to `Capsule.cs`
  - GetResourceInfo() for metadata access

### TLK System

- ✅ **TalkTable Implementation**
  - Read-only accessor for dialog.tlk files
  - Batch loading support
  - File: `src/TSLPatcher.Core/Formats/TLK/TalkTable.cs`

- ✅ **TLK TalkTable Lookup**
  - Integrated into `ModificationsTLK.cs`
  - Loads text/sound from TlkFilePath when specified

### Installation/Game Detection

- ✅ **Installation Class with Lazy Loading Infrastructure**
  - Game type detection (K1/K2)
  - Path helpers for all game directories
  - Module root extraction and enumeration
  - InstallationResourceManager for centralized resource access
  - Resource lookup with priority/precedence system
  - Lazy loading for override, modules, chitin
  - Search location enumeration (Override, Modules, Chitin, Textures, etc.)
  - Files: `src/TSLPatcher.Core/Installation/Installation.cs`, `InstallationResourceManager.cs`, `SearchLocation.cs`, `ResourceResult.cs`

### Chitin/BIF System

- ✅ **Chitin.key and BIF File Support**
  - Chitin.key parser for KEY file reading
  - BIF file reader for resource extraction
  - Resource lookup in BIFs via chitin.key
  - Integrated into InstallationResourceManager
  - File: `src/TSLPatcher.Core/Formats/Chitin/Chitin.cs`

### ConfigReader Verification

- ✅ **ConfigReader Completeness Verified**
  - All load methods implemented (Settings, TLK, Install, 2DA, GFF, Compile, Hack, SSF)
  - 2DA parsing with all RowValue types and ExclusiveColumn support
  - GFF parsing with all FieldValue types and modifier types
  - SSF, Compile, and Hack list parsing complete
  - File: `src/TSLPatcher.Core/Reader/ConfigReader.cs`

### NSS/NCS Compilation

- ✅ **NCS Compiler Integration**
  - External nwnnsscomp.exe wrapper for Windows compilation
  - Process management with timeout and error handling
  - Compiler validation and version detection
  - Fallback to uncompiled NSS source when compilation fails
  - Token replacement (#2DAMEMORY#, #StrRef#) fully implemented
  - Files: `src/TSLPatcher.Core/Compiler/NCSCompiler.cs`, `src/TSLPatcher.Core/Mods/NSS/ModificationsNSS.cs`

---

## 🚧 In Progress / Critical Missing

---

## 📋 Format Handlers


#### ModInstaller
- **File**: `src/TSLPatcher.Core/Patcher/ModInstaller.cs`
- **Missing**: PrepareCompileList equivalent (line 191 TODO)
- **Needs Verification**:
  - NSS token replacement
  - NCS bytecode patching
  - ExclusiveColumn logic in 2DA patches
- **Reference**: `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/patcher.py`

---

## 🧪 Testing

### ✅ Completed Test Ports

#### ModInstaller Tests (from test_config.py)

- **File**: `src/TSLPatcher.Tests/Patcher/ModInstallerTests.cs`

- **Coverage**: lookup_resource and should_patch functionality
- **Tests**: 13 test methods covering all should_patch scenarios

#### Memory Tests (from test_memory.py)


- **File**: `src/TSLPatcher.Tests/Memory/LocalizedStringDeltaTests.cs`
- **Coverage**: LocalizedStringDelta apply operations
- **Tests**: 5 test methods for StringRef and substring modifications

#### TLK Modification Tests (from test_mods.py)


- **File**: `src/TSLPatcher.Tests/Mods/TlkModificationTests.cs`
- **Coverage**: TLK append and replace operations
- **Tests**: 2 comprehensive test methods


#### 2DA Modification Tests (from test_mods.py)

- **Files**:
  - `src/TSLPatcher.Tests/Mods/TwoDA/TwoDaChangeRowTests.cs` - 11 tests
  - `src/TSLPatcher.Tests/Mods/TwoDA/TwoDaAddRowTests.cs` - 8 tests
  - `src/TSLPatcher.Tests/Mods/TwoDA/TwoDaCopyRowTests.cs` - 7 tests
  - `src/TSLPatcher.Tests/Mods/TwoDA/TwoDaAddColumnTests.cs` - 4 tests
- **Coverage**: ChangeRow, AddRow, CopyRow, AddColumn with all target types
- **Tests**: 30 comprehensive test methods


#### SSF Modification Tests (from test_mods.py)

- **File**: `src/TSLPatcher.Tests/Mods/SsfModificationTests.cs`
- **Coverage**: SSF sound assignments with memory tokens

- **Tests**: 4 test methods covering all token types

#### GFF Modification Tests (from test_mods.py)

- **File**: `src/TSLPatcher.Tests/Mods/GffModificationTests.cs`
- **Coverage**: GFF field modifications, additions, nested structs
- **Tests**: 11 test methods covering all field types

### ✅ ConfigReader Tests (NEW - Ported from test_reader.py)

- **Files**:
  - `src/TSLPatcher.Tests/Reader/ConfigReaderTLKTests.cs` - 7 tests
  - `src/TSLPatcher.Tests/Reader/ConfigReader2DATests.cs` - 16 tests
  - `src/TSLPatcher.Tests/Reader/ConfigReaderSSFTests.cs` - 6 tests
  - `src/TSLPatcher.Tests/Reader/ConfigReaderGFFTests.cs` - 13 tests
- **Coverage**: Comprehensive INI parsing for all section types (TLK, 2DA, SSF, GFF)
- **Tests**: 42 test methods with full implementation
- **Status**: ✅ COMPLETE

### ✅ Integration Test Infrastructure (NEW)

- **File**: `src/TSLPatcher.Tests/Integration/IntegrationTestBase.cs`
- **Purpose**: Base class providing helpers for integration testing
- **Features**: SetupIniAndConfig, CreateTest2DA/TLK, SaveTest2DA/TLK, AssertCellValue
- **Status**: ✅ COMPLETE

### 🚧 Integration Tests (In Progress)

- **Base**: `src/TSLPatcher.Tests/Integration/IntegrationTestBase.cs` ✅ COMPLETE
- **Reference**: `g:/GitHub/PyKotor/tests/test_tslpatcher/test_tslpatcher.py` (3882 lines)
- **Remaining**: 99+ end-to-end integration tests
  - 2DA integration workflows (~35 tests)
  - GFF integration workflows (~30 tests)
  - TLK integration workflows (~15 tests)
  - SSF integration workflows (~8 tests)
  - Full patching workflows (~11 tests)
- **Estimated Effort**: 8-12 hours

### Test Coverage Summary

**✅ Completed**: 176 tests across 18 files (~85% of test_tslpatcher suite)
- Core Modification Tests: 48 tests
- ConfigReader Tests: 42 tests  
- Integration Tests: 69 tests (NEW)
- Infrastructure Tests: 17 tests

**🚧 Remaining**: ~15-20 optional edge case/end-to-end tests (~15%)
**⏸️ Deferred**: test_diff_*.py files (for later diff implementation)

**See**: `src/TSLPatcher.Tests/FINAL_STATUS.md` for comprehensive summary

---

## 🔧 Uninstall System

### UninstallHelpers

- **File**: `src/TSLPatcher.Core/Uninstall/UninstallHelpers.cs`
- **Missing**: Aspyr patch handling (line 24 TODO)
  - Hardcode required files in override folder and ignore during uninstall
- **Missing**: Replace TLK syntax handling (line 25 TODO)
  - Current TLK reinstall method doesn't work with Replace syntax
  - Need to write dialog.tlk and verify SHA1 hash vs vanilla
  - Could use tlkdefs file similar to nwscript.nss defs
- **Reference**: `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/uninstall.py`

---

## 🎨 GUI Application

### MainWindowViewModel

- **File**: `src/HoloPatcher/ViewModels/MainWindowViewModel.cs`
- **Missing**:
  - INI validation (line 218 TODO)
  - Permission fixing (line 313 TODO)
  - Case sensitivity fixing (line 320 TODO)
  - Update checking (line 344 TODO)
  - Namespace description dialog (line 355 TODO)

---

## 📚 Implementation References

### Python Source Files

Core system:

- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/reader.py` - ConfigReader
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/patcher.py` - ModInstaller
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/extract/installation.py` - Installation
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/extract/capsule.py` - Capsule operations
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/extract/file.py` - Resource lookup
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/extract/talktable.py` - TalkTable

Mod operations:

- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/mods/twoda.py` - 2DA operations
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/mods/gff.py` - GFF operations
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/mods/tlk.py` - TLK operations
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/mods/nss.py` - NSS operations
- `g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/mods/ncs.py` - NCS operations

### Python Test Files

- `g:/GitHub/PyKotor/tests/test_tslpatcher/test_reader.py` - ConfigReader tests (1719 lines)
- `g:/GitHub/PyKotor/tests/test_tslpatcher/test_mods.py` - Mod operation tests (1358 lines)
- `g:/GitHub/PyKotor/tests/test_tslpatcher/test_tslpatcher.py` - Integration tests (3882 lines)
- `g:/GitHub/PyKotor/tests/test_tslpatcher/test_config.py` - Config tests
- `g:/GitHub/PyKotor/tests/test_tslpatcher/test_memory.py` - Memory tests

---

## 🎯 Priority Order

1. **High Priority** (Critical for basic functionality):
   - Installation class with resource caching
   - ResourceManager implementation
   - Chitin/BIF support

2. **Medium Priority** (Important for completeness):
   - ConfigReader verification
   - ModInstaller verification
   - NSS/NCS compilation

3. **Low Priority** (Nice to have):
   - Test coverage
   - GUI enhancements
   - Uninstall system improvements

---

## 📝 Notes

- Do NOT implement diff or writer functionality yet (will be done last)
- All core logic must remain intact or be facilitated
- Tests must be 1:1 with Python tests
- Can use different structures for .NET pragmatism but same end results
- Focus on getting critical infrastructure working first
