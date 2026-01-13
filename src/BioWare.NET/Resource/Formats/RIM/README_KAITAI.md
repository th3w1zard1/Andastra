# RIM Kaitai Struct Definition

This directory contains the Kaitai Struct definition for the BioWare RIM (Resource Information Manager) format.

## Files

- `RIM.ksy` - Kaitai Struct format definition for RIM files

## Compiling to C#

To compile the `.ksy` file to C# code, you need the Kaitai Struct compiler:

### Installation

**Windows:**
```powershell
# Download and install from https://kaitai.io/
# Or use Chocolatey:
choco install kaitai-struct-compiler
```

**Linux/macOS:**
```bash
# Debian/Ubuntu
wget https://packages.kaitai.io/dists/unstable/main/binary-amd64/kaitai-struct-compiler_0.10_all.deb
sudo dpkg -i kaitai-struct-compiler_0.10_all.deb

# Or use package manager
# macOS: brew install kaitai-struct-compiler
```

### Compilation

```bash
# Compile to C#
kaitai-struct-compiler -t csharp RIM.ksy -d ./

# Or specify output directory
kaitai-struct-compiler -t csharp RIM.ksy -d ./Generated/
```

### Using the Generated Code

After compilation, you'll get a C# file (typically `Rim.cs`) that you can use:

```csharp
using Kaitai;

// Read from file
var rim = Rim.FromFile("module001.rim");

// Read from byte array
byte[] data = File.ReadAllBytes("module001.rim");
var rim = new Rim(new KaitaiStream(data));

// Access header
string fileType = rim.Header.FileType; // "RIM "
string fileVersion = rim.Header.FileVersion; // "V1.0"
uint resourceCount = rim.Header.ResourceCount;

// Access resource entries
foreach (var entry in rim.ResourceEntryTable.Entries)
{
    string resref = entry.ResrefTrimmed;
    uint resourceType = entry.ResourceType;
    uint resourceSize = entry.ResourceSize;
    byte[] data = entry.Data;
}
```

## Format Structure

The RIM format consists of:

1. **Header (20 bytes)**
   - File Type: "RIM " (4 bytes)
   - File Version: "V1.0" (4 bytes)
   - Reserved: 0x00000000 (4 bytes)
   - Resource Count: uint32 (4 bytes)
   - Offset to Resource Table: uint32 (4 bytes)

2. **Extended Header (100 bytes)**
   - Reserved padding (typically all zeros)

3. **Resource Entry Table (32 bytes per entry)**
   - ResRef: 16 bytes (ASCII, null-padded)
   - Resource Type: uint32 (4 bytes)
   - Resource ID: uint32 (4 bytes)
   - Offset to Data: uint32 (4 bytes)
   - Resource Size: uint32 (4 bytes)

4. **Resource Data Section**
   - Raw binary data for each resource

## References

- PyKotor: `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/rim/`
- reone: `vendor/reone/src/libs/resource/format/rimreader.cpp`
- xoreos: `vendor/xoreos/src/aurora/rimfile.cpp`
- Kaitai Struct: https://kaitai.io/

## Testing

Comprehensive tests are available in:
- `src/Andastra/Tests/Formats/RIMFormatTests.cs` - Basic I/O tests
- `src/Andastra/Tests/Formats/RIMFormatComprehensiveTests.cs` - Exhaustive field and edge case tests

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~RIMFormat"
```


