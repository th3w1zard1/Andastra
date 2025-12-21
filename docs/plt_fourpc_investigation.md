# PLT and FourPC File Format Investigation

## Summary

This document summarizes findings from reverse engineering `swkotor.exe` and `swkotor2.exe` regarding PLT and FourPC file formats.

## PLT Files

### Status: **NOT USED IN KOTOR**

**Critical Finding**: Despite having a resource type identifier (0x0006), **PLT files are NOT actually used or loaded in KotOR games**.

### Evidence

1. **Handler Address Confusion**: The documentation previously listed handler at `swkotor.exe: 0x0070c350`, but this function actually loads **LIP files** (lip sync data, resource type 0xbbc/3004), not PLT files.

2. **Function Analysis** (`swkotor.exe: 0x0070c350`):
   - Function name: `LoadLIPFile`
   - Resource type loaded: `0xbbc` (3004 decimal) = LIP
   - Validates file header: Checks for `"LIP V1.0"` string
   - **NOT** a PLT file loader

3. **Resource Type Mapping**: While the resource system has a mapping for "plt" → type 6 (see `FUN_005e6d20` at `swkotor.exe: 0x005e6d20`), **no actual handler exists** that processes PLT files.

4. **Documentation Confirmation**: The PyKotor wiki explicitly states:
   > **⚠️ NOT USED IN KOTOR**: This format is **Neverwinter Nights-specific** and is **NOT used in KotOR games**.

### What PLT Files Are (NWN Context)

PLT (Palette Texture) files are used in **Neverwinter Nights** for:
- Runtime color palette selection for character customization
- Dynamic color changes (skin, hair, armor colors)
- References external `.pal` palette files
- Stores palette group indices (0-9) and color indices (0-255)

**KotOR uses standard TPC/TGA/DDS textures instead.**

### Can You Modify PLT Files in KotOR?

**No** - PLT files are not loaded or processed by KotOR. Modifying them will have **no effect** in the game.

---

## FourPC Files

### Status: **USED IN KOTOR**

**FourPC** (resource type 0x80b / 2059 decimal) is a **texture format** used in KotOR games.

### Handler Information

- **swkotor.exe**: Handler at `0x00710910` (`LoadFourPCTexture`)
- **swkotor2.exe**: Handler at `0x00783f10` (`LoadFourPCTexture`)

### Format Details

Based on code analysis and resource type definition:

- **Format**: RGBA 16-bit texture
- **Resource Type ID**: `0x80b` (2059 decimal)
- **File Extension**: `.4pc`
- **Category**: Textures

### How FourPC Files Are Loaded

1. **Resource Loading** (`LoadFourPCTexture`):
   - Searches resource system using `FUN_004074d0` / `FUN_004075a0`
   - Creates texture object (56 bytes = 0x38 allocation)
   - Stores resource type `0x80b` in texture metadata
   - Can be loaded from modules, override, or BIF files

2. **Texture Processing** (`FUN_00710100`):
   - Processes the loaded FourPC data
   - Converts/decodes the 16-bit RGBA format
   - Prepares texture for rendering

3. **Usage Context** (`FUN_0070e220`):
   - FourPC textures are checked when loading other texture resources
   - Can be used as fallback or alternative texture format
   - Integrated into the texture loading pipeline

### File Structure (Inferred)

Based on the code structure and typical 16-bit RGBA formats:

```
[Header - size unknown]
[Pixel Data - 16-bit RGBA]
```

**16-bit RGBA** typically means:
- 5 bits Red
- 5 bits Green  
- 5 bits Blue
- 1 bit Alpha (or 4/4/4/4 RGBA)

**Note**: The exact binary format structure needs further reverse engineering of the decoding functions.

### What Can You Modify in FourPC Files?

**Modifiable Elements**:
1. **Pixel Colors**: Change individual pixel RGB values (within 16-bit constraints)
2. **Texture Dimensions**: Width/height (if header allows)
3. **Alpha Channel**: Transparency values

**Effects in Game**:
- Visual changes to textures using FourPC format
- Color modifications will be visible in-game
- Alpha channel changes affect transparency
- Must maintain valid 16-bit RGBA format or game may crash/display incorrectly

### Usage in KotOR

FourPC appears to be used as an alternative texture format, likely for:
- Specific texture types requiring 16-bit color depth
- Memory-efficient texture storage
- Compatibility with certain rendering paths

**Note**: Most KotOR textures use TPC format (DXT compression or 32-bit RGBA). FourPC is less common.

---

## Comparison: PLT vs FourPC

| Aspect | PLT | FourPC |
|--------|-----|--------|
| **Used in KotOR?** | ❌ No | ✅ Yes |
| **Resource Type** | 0x0006 (not actually handled) | 0x80b (2059) |
| **Purpose** | Palette-based textures (NWN) | 16-bit RGBA textures |
| **Handler Exists?** | ❌ No | ✅ Yes |
| **Can Modify?** | ❌ No effect | ✅ Yes, affects visuals |
| **File Extension** | `.plt` | `.4pc` |

---

## Technical Details

### Handler Function Signatures

**FourPC Loader** (`swkotor.exe: 0x00710910`):
```c
void __thiscall LoadFourPCTexture(
    void* this,
    int* resourceName,
    int loadImmediately
)
```

**LIP Loader** (previously misidentified as PLT) (`swkotor.exe: 0x0070c350`):
```c
void __thiscall LoadLIPFile(
    void* this,
    int* resourceName,
    int loadImmediately
)
```

### Resource Loading Flow

1. Resource name lookup via `FUN_004074d0` / `FUN_004075a0`
2. Check cache for existing resource
3. If not cached:
   - Allocate object (0x38 bytes for FourPC, 0x34 bytes for LIP)
   - Initialize with constructor
   - Register in resource cache
4. If `loadImmediately != 0`: Call `FUN_00408620` to load data
5. Store resource reference in object

### Key Functions

- `FUN_004074d0` / `FUN_004075a0`: Resource search/lookup
- `FUN_00407680` / `FUN_00407750`: Resource cache registration
- `FUN_00408620`: Immediate data loading
- `FUN_00710100`: FourPC texture processing
- `FUN_00710870`: Get texture metadata pointer

---

## Recommendations

### For Modders

1. **PLT Files**: Ignore them - they don't work in KotOR. Use TPC/TGA/DDS instead.

2. **FourPC Files**: 
   - Can be modified for texture replacement
   - Maintain 16-bit RGBA format constraints
   - Test thoroughly as format is less common
   - Consider converting to/from TPC for easier editing

### For Developers

1. **PLT Support**: Not needed for KotOR compatibility - format is NWN-specific.

2. **FourPC Support**: 
   - Implement 16-bit RGBA decoder
   - Reverse engineer exact binary format structure
   - Handle width/height extraction from header
   - Support conversion to/from standard formats

---

## References

- **Ghidra Analysis**: `swkotor.exe` and `swkotor2.exe` reverse engineering
- **Resource Type Definitions**: `src/Andastra/Parsing/Resource/ResourceType.cs`
- **Documentation**: `vendor/PyKotor/wiki/PLT-File-Format.md`
- **Module Resource Types**: `docs/module_resource_types_reverse_engineering.md`

---

## Conclusion

- **PLT**: Not used in KotOR - ignore for KotOR modding/development
- **FourPC**: Active texture format - can be modified, affects game visuals
- **Handler Confusion**: Previous documentation incorrectly identified LIP loader as PLT loader

