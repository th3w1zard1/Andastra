# GIT (Game Instance Template) Kaitai Struct Definition

This directory contains the Kaitai Struct format definition for BioWare GIT (Game Instance Template) files.

## Files

- `GIT.ksy` - Comprehensive Kaitai Struct format definition for GIT files

## Format Overview

GIT files are GFF-based format files that store dynamic area information including:
- Creatures
- Doors
- Placeables
- Triggers
- Waypoints
- Stores
- Encounters
- Sounds
- Cameras

The format uses the GFF (Generic File Format) binary structure with file type signature "GIT ".

## Testing

Comprehensive test scripts are available in the `scripts` directory:

### Validation Script
```powershell
.\scripts\validate_kaitai_git.ps1 [-Verbose] [-SkipCompilation]
```
Validates the KSY file syntax, structure, and optionally tests compilation.

### Multi-Language Compilation Test
```powershell
.\scripts\test_kaitai_multilang.ps1 [-Verbose] [-Quick]
```
Tests compilation to at least 12 languages:
- Python
- Java
- JavaScript
- C#
- C++ STL
- Ruby
- PHP
- Perl
- Go
- Lua
- Nim
- Rust
- Swift
- TypeScript

### Quick Test
```powershell
.\scripts\test_kaitai_git.ps1 [-Verbose]
```
Quick compilation test for all available languages.

## Requirements

- Kaitai Struct Compiler (ksc)
  - Download from: https://kaitai.io/#download
  - Or install via package manager:
    ```bash
    wget https://packages.kaitai.io/dists/unstable/main/binary-amd64/kaitai-struct-compiler_0.10_all.deb
    sudo dpkg -i kaitai-struct-compiler_0.10_all.deb
    ```

## Usage

### Compile to a specific language:
```bash
kaitai-struct-compiler -t python -d output/ GIT.ksy
kaitai-struct-compiler -t java -d output/ GIT.ksy
kaitai-struct-compiler -t javascript -d output/ GIT.ksy
# ... etc
```

### Use in Python:
```python
from git import Git

with open('area.git', 'rb') as f:
    git_file = Git.from_file('area.git')
    print(f"File type: {git_file.gff_header.file_type}")
    print(f"Version: {git_file.gff_header.file_version}")
    print(f"Struct count: {git_file.gff_header.struct_count}")
```

## Structure

The GIT.ksy file defines:
- GFF header structure (56 bytes)
- Label array (field names)
- Struct array (structure definitions)
- Field array (field definitions)
- Field data section (complex type data)
- Field indices array (MultiMap for structs with multiple fields)
- List indices array (for LIST type fields)
- All GFF field types (uint8, int32, string, ResRef, LocalizedString, Vector3, Vector4, etc.)

## References

- PyKotor: `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/git.py`
- Reone: `vendor/reone/src/libs/resource/parser/gff/git.cpp`
- Xoreos: `vendor/xoreos/src/aurora/gitfile.cpp`
- Wiki: `vendor/PyKotor/wiki/GFF-File-Format.md`

## Notes

- GIT files use GFF format version "V3.2" for KotOR
- The format is self-describing through the GFF structure
- All GIT-specific structs (GITCamera, GITCreature, GITDoor, etc.) are defined by their struct_id values in the GFF structure
- Struct IDs:
  - 1: GITTrigger, GITTriggerGeometry
  - 2: GITEncounterSpawnPoint
  - 3: GITTriggerGeometry
  - 4: GITCreature
  - 5: GITWaypoint
  - 6: GITSound
  - 7: GITEncounter
  - 8: GITDoor
  - 9: GITPlaceable
  - 11: GITStore
  - 14: GITCamera
  - 100: AreaProperties

