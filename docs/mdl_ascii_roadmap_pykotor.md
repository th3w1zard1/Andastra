# MDL ASCII Format Implementation Roadmap

## Overview
This document tracks the implementation of ASCII MDL format support in PyKotor, following MDLOps patterns and conventions.

## Status: IN PROGRESS

## Implementation Goals
1. ✅ Ensure `ResourceType.MDL_ASCII` is properly defined with `target_type=ResourceType.MDL`
2. ✅ Verify `detect_format`, `read_mdl`, `write_mdl`, `bytes_mdl` accept `ResourceType.MDL_ASCII`
3. ⏳ Complete ASCII reader/writer implementation matching MDLOps 1:1
4. ⏳ Test round-trip conversion (ASCII -> Binary -> ASCII)

## Current Implementation Status

### ResourceType Definition
- **Status**: ✅ COMPLETE
- **Location**: `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/type.py:271`
- **Definition**: `MDL_ASCII = ResourceTuple(50002, "mdl.ascii", "Models", "plaintext", target_member="MDL")`
- **Note**: ✅ `target_member="MDL"` has been set correctly

### Format Detection
- **Status**: ✅ COMPLETE
- **Location**: `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py:15-60`
- **Implementation**: `detect_mdl()` function checks first 4 bytes
  - `\x00\x00\x00\x00` → `ResourceType.MDL` (binary)
  - Otherwise → `ResourceType.MDL_ASCII`
- **Matches MDLOps**: ✅ Yes (MDLOpsM.pm:412-435)

### Read Functions
- **Status**: ✅ COMPLETE
- **Location**: `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py:63-106`
- **Implementation**:
  - `read_mdl()` accepts `file_format: ResourceType | None`
  - Supports `ResourceType.MDL_ASCII` via `MDLAsciiReader`
  - `read_mdl_fast()` also supports ASCII (falls back to regular loading)

### Write Functions
- **Status**: ✅ COMPLETE
- **Location**: `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py:171-198`
- **Implementation**:
  - `write_mdl()` accepts `file_format: ResourceType = ResourceType.MDL`
  - Supports `ResourceType.MDL_ASCII` via `MDLAsciiWriter`
  - `bytes_mdl()` also supports ASCII format

### ASCII Reader Implementation
- **Status**: ⏳ IN PROGRESS
- **Location**: `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl_ascii.py:528-1609`
- **Reference**: MDLOpsM.pm:3916-5970 (`readasciimdl`)

#### Completed Features
- ✅ Model header parsing (newmodel, setsupermodel, classification, etc.)
- ✅ Node parsing (dummy, trimesh, light, emitter, reference, aabb, saber)
- ✅ Mesh data parsing (verts, faces, tverts, bones, weights, constraints)
- ✅ Controller parsing (position, orientation, scale, alpha, etc.)
- ✅ Light node parsing (flare data, colors, etc.)
- ✅ Emitter node parsing (properties and flags)
- ✅ Reference node parsing
- ✅ Saber node parsing
- ✅ Walkmesh/AABB parsing
- ✅ Animation parsing (newanim, length, events)
- ✅ Node hierarchy building

#### Missing/Incomplete Features
- ⏳ Some edge cases in controller parsing (bezier controllers)
- ⏳ Complete emitter flag handling
- ⏳ Some light flare data parsing edge cases
- ⏳ Validation of vertex data (MDLOps option)
- ⏳ Smoothing group calculations
- ⏳ Adjacent face calculations

### ASCII Writer Implementation
- **Status**: ✅ COMPLETE
- **Location**: `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl_ascii.py:278-600`
- **Reference**: MDLOpsM.pm:3004-3900 (`writeasciimdl`)

#### Completed Features
- ✅ Model header writing
- ✅ Node writing (all node types)
- ✅ Mesh data writing (verts, faces, tverts)
- ✅ Controller writing (keyed and bezier controllers)
- ✅ Light node writing
- ✅ Emitter node writing
- ✅ Reference node writing
- ✅ Saber node writing
- ✅ Walkmesh/AABB writing
- ✅ Skin mesh writing (bones, weights)
- ✅ Dangly mesh writing (constraints)

#### Missing/Incomplete Features
- ✅ Animation writing (newanim blocks) - COMPLETE
- ⏳ Some controller formatting edge cases - Most common cases are handled
- ⏳ Complete emitter flag writing - Basic flags are supported
- ⏳ Some light flare data writing - Basic flare data is supported
- ⏳ Animation node parent mapping - Currently skipped (requires model node index mapping)

## MDLOps Reference Mapping

### Key MDLOps Functions
1. **readasciimdl** (MDLOpsM.pm:3916-5970)
   - Main ASCII reading function
   - Handles all node types and data structures
   - Reference implementation for reader

2. **writeasciimdl** (MDLOpsM.pm:5972-7000+)
   - Main ASCII writing function
   - Handles all node types and data structures
   - Reference implementation for writer

3. **Controller Names** (MDLOpsM.pm:325-407)
   - Maps controller IDs to names
   - Used for both reading and writing

4. **Node Type Constants** (MDLOpsM.pm:313-323)
   - Node type values (dummy=1, light=3, etc.)
   - Used for node type detection

## Testing Checklist

- [ ] Read simple trimesh ASCII MDL
- [ ] Read ASCII MDL with animations
- [ ] Read ASCII MDL with controllers
- [ ] Read ASCII MDL with skin mesh
- [ ] Read ASCII MDL with dangly mesh
- [ ] Read ASCII MDL with light nodes
- [ ] Read ASCII MDL with emitter nodes
- [ ] Read ASCII MDL with reference nodes
- [ ] Read ASCII MDL with saber nodes
- [ ] Read ASCII MDL with walkmesh/AABB
- [ ] Write simple trimesh to ASCII MDL
- [ ] Write MDL with all node types to ASCII
- [ ] Round-trip: Binary → ASCII → Binary
- [ ] Round-trip: ASCII → Binary → ASCII
- [ ] Compare output with MDLOps output

## Notes

- Implementation should follow PyKotor conventions, not Perl idioms
- Logic should match MDLOps 1:1 where possible
- All controller types from MDLOps should be supported
- All node types from MDLOps should be supported
- Edge cases from MDLOps should be handled

## Next Steps

1. Verify ResourceType.MDL_ASCII has target_type set correctly
2. Complete any missing reader features
3. Complete any missing writer features
4. Add comprehensive tests
5. Compare output with MDLOps for validation

