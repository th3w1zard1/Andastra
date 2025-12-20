meta:
  id: gvt
  title: BioWare GVT (Global Variables) File Format
  license: MIT
  endian: le
  file-extension: gvt
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/savedata.py
    reone: vendor/reone/src/libs/resource/parser/gff/gvt.cpp
    xoreos: vendor/xoreos/src/aurora/gff3file.cpp
    wiki: vendor/PyKotor/wiki/GFF-File-Format.md
doc: |
  GVT (Global Variables) files are GFF-based format files that store global script variables used
  throughout the game. GVT files use the GFF (Generic File Format) binary structure with file type
  signature "GVT ".
  
  GVT files are typically named "GLOBALVARS.res" and are stored in savegame.sav ERF archives.
  They contain all global script variables used throughout the game:
  - Boolean variables: Story flags, puzzle states, trigger conditions (packed as bits, 8 per byte, LSB first)
  - Number variables: Counters, percentages, small integers (0-255, stored as single bytes)
  - String variables: Dynamic NPC names, custom text (stored as parallel lists)
  - Location variables: Spawn points, teleport destinations, waypoints (12 floats per location: x, y, z, oriX, oriY, oriZ, padding)
  
  Boolean Packing:
  - Booleans are packed into bytes, 8 per byte, LSB (Least Significant Bit) first
  - Example: Byte 0x53 (binary 01010011) = [1,1,0,0,1,0,1,0] (LSB first)
  - Bit index: i % 8, Byte index: i // 8
  
  Location Format:
  - Each location is stored as 12 floats (48 bytes total)
  - Floats 0-2: Position (x, y, z)
  - Floats 3-5: Orientation (ori_x, ori_y, ori_z) - ori_y and ori_z are typically unused/padding
  - Floats 6-11: Padding (unused, always 0.0)
  
  GVT Root Struct Fields:
  - CatBoolean (List): List of structs with "Name" field (variable names)
  - ValBoolean (Binary): Packed boolean values (8 per byte, LSB first)
  - CatNumber (List): List of structs with "Name" field (variable names)
  - ValNumber (Binary): Number values as single bytes (0-255 range)
  - CatLocation (List): List of structs with "Name" field (variable names)
  - ValLocation (Binary): Location values as float array (12 floats per location)
  - CatString (List): List of structs with "Name" field (variable names)
  - ValString (List): List of structs with "String" field (parallel to CatString)
  
  Based on swkotor2.exe: SaveGlobalVariables @ 0x005ac670 (calls FUN_005ab310 @ 0x005ab310 internally)
  Located via string reference: "GLOBALVARS" @ 0x007c27bc
  Original implementation: Creates GFF with "GVT " signature and "V2.0" version string
  
  References:
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/savedata.py (GlobalVars class)
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - src/Andastra/Runtime/Content/Save/SaveSerializer.cs (SerializeGlobalVariables)
  - src/Andastra/Parsing/Extract/SaveData/GlobalVars.cs

seq:
  - id: gff_header
    type: gff_header
    doc: GFF file header (56 bytes)
  
  - id: label_array
    type: label_array
    if: gff_header.label_count > 0
    pos: gff_header.label_array_offset
    doc: Array of field name labels (16-byte null-terminated strings)
  
  - id: struct_array
    type: struct_array
    if: gff_header.struct_count > 0
    pos: gff_header.struct_array_offset
    doc: Array of struct entries (12 bytes each)
  
  - id: field_array
    type: field_array
    if: gff_header.field_count > 0
    pos: gff_header.field_array_offset
    doc: Array of field entries (12 bytes each)
  
  - id: field_data
    type: field_data_section
    if: gff_header.field_data_count > 0
    pos: gff_header.field_data_offset
    doc: Field data section for complex types (strings, ResRefs, LocalizedStrings, binary data, etc.)
  
  - id: field_indices
    type: field_indices_array
    if: gff_header.field_indices_count > 0
    pos: gff_header.field_indices_offset
    doc: Field indices array (MultiMap) for structs with multiple fields
  
  - id: list_indices
    type: list_indices_array
    if: gff_header.list_indices_count > 0
    pos: gff_header.list_indices_offset
    doc: List indices array for LIST type fields (CatBoolean, CatNumber, CatLocation, CatString, ValString)

types:
  # GFF Header (56 bytes) - Standard GFF header structure
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "GVT " for global variables files.
        valid: "GVT "
      
      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File format version. Typically "V3.2" for KotOR (though original engine writes "V2.0").
          Other versions: "V3.3", "V4.0", "V4.1" for other BioWare games.
      
      - id: struct_array_offset
        type: u4
        doc: Byte offset to struct array from the beginning of the file
      
      - id: struct_count
        type: u4
        doc: Number of structs in the struct array
      
      - id: field_array_offset
        type: u4
        doc: Byte offset to field array from the beginning of the file
      
      - id: field_count
        type: u4
        doc: Number of fields in the field array
      
      - id: label_array_offset
        type: u4
        doc: Byte offset to label array from the beginning of the file
      
      - id: label_count
        type: u4
        doc: Number of labels in the label array
      
      - id: field_data_offset
        type: u4
        doc: Byte offset to field data section from the beginning of the file
      
      - id: field_data_count
        type: u4
        doc: Size of field data section in bytes
      
      - id: field_indices_offset
        type: u4
        doc: Byte offset to field indices array from the beginning of the file
      
      - id: field_indices_count
        type: u4
        doc: Number of field indices (uint32 values) in the field indices array
      
      - id: list_indices_offset
        type: u4
        doc: Byte offset to list indices array from the beginning of the file
      
      - id: list_indices_count
        type: u4
        doc: Number of list indices (uint32 values) in the list indices array

  # Label Array - Standard GFF label array
  label_array:
    seq:
      - id: labels
        type: label_entry
        repeat: expr
        repeat-expr: _root.gff_header.label_count
        doc: Array of label entries (16 bytes each)
  
  label_entry:
    seq:
      - id: name
        type: str
        encoding: ASCII
        size: 16
        doc: |
          Field name label (null-padded to 16 bytes, null-terminated).
          The actual label length is determined by the first null byte.
          Common GVT field names: "CatBoolean", "ValBoolean", "CatNumber", "ValNumber",
          "CatLocation", "ValLocation", "CatString", "ValString", "Name", "String".
    instances:
      name_trimmed:
        value: name.rstrip('\x00')
        doc: "Label name with trailing nulls removed"
  
  # Struct Array - Standard GFF struct array
  struct_array:
    seq:
      - id: entries
        type: struct_entry
        repeat: expr
        repeat-expr: _root.gff_header.struct_count
        doc: Array of struct entries (12 bytes each)
  
  struct_entry:
    seq:
      - id: struct_id
        type: s4
        doc: |
          Structure type identifier.
          Root struct always has struct_id = 0xFFFFFFFF (-1).
          GVT files typically have the root struct plus structs for catalog entries
          (CatBoolean entries, CatNumber entries, CatLocation entries, CatString entries, ValString entries).
      
      - id: data_or_offset
        type: u4
        doc: |
          If field_count = 1: Direct field index into field_array.
          If field_count > 1: Byte offset into field_indices array.
          If field_count = 0: Unused (empty struct).
      
      - id: field_count
        type: u4
        doc: Number of fields in this struct (0, 1, or >1)
    instances:
      has_single_field:
        value: field_count == 1
        doc: True if struct has exactly one field (direct field index in data_or_offset)
      has_multiple_fields:
        value: field_count > 1
        doc: True if struct has multiple fields (offset to field indices in data_or_offset)
      single_field_index:
        value: data_or_offset
        if: has_single_field
        doc: Direct field index when struct has exactly one field
      field_indices_offset:
        value: data_or_offset
        if: has_multiple_fields
        doc: Byte offset into field_indices_array when struct has multiple fields
  
  # Field Array - Standard GFF field array
  field_array:
    seq:
      - id: entries
        type: field_entry
        repeat: expr
        repeat-expr: _root.gff_header.field_count
        doc: Array of field entries (12 bytes each)
  
  field_entry:
    seq:
      - id: field_type
        type: u4
        doc: |
          GFF field data type (enum value):
          - 0: UInt8/Byte
          - 1: Int8/Char
          - 2: UInt16/Word
          - 3: Int16/Short
          - 4: UInt32/DWord
          - 5: Int32/Int
          - 6: UInt64/DWord64
          - 7: Int64/Int64
          - 8: Float/Single
          - 9: Double
          - 10: String/CExoString
          - 11: ResRef
          - 12: LocalizedString/CExoLocString
          - 13: Binary/Void (used for ValBoolean, ValNumber, ValLocation)
          - 14: Struct (nested struct)
          - 15: List (list of structs, used for CatBoolean, CatNumber, CatLocation, CatString, ValString)
          - 16: Vector4/Orientation (quaternion)
          - 17: Vector3/Vector
      
      - id: label_index
        type: u4
        doc: Index into label_array for field name
      
      - id: data_or_offset
        type: u4
        doc: |
          For simple types: Direct data value or offset into field_data section.
          For complex types: Byte offset into field_data section.
          For Struct type: Struct index into struct_array.
          For List type: Byte offset into list_indices_array.
          For Binary type: Byte offset into field_data section.
    instances:
      label_name:
        value: _root.label_array.labels[label_index].name_trimmed
        doc: Field name from label array
  
  # Field Data Section - Standard GFF field data section
  field_data_section:
    seq:
      - id: data
        type: u1
        repeat: expr
        repeat-expr: _root.gff_header.field_data_count
        doc: |
          Raw field data bytes (strings, ResRefs, binary data, etc.).
          Contains:
          - ValBoolean: Packed boolean values (8 per byte, LSB first)
          - ValNumber: Number values as single bytes (0-255 range)
          - ValLocation: Location values as float array (12 floats = 48 bytes per location)
          - String values: UTF-8 encoded strings with length prefixes
          - ResRef values: 16-byte null-terminated resource names
  
  # Field Indices Array - Standard GFF field indices array
  field_indices_array:
    seq:
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.gff_header.field_indices_count
        doc: Array of field indices (uint32 values) for structs with multiple fields
  
  # List Indices Array - Standard GFF list indices array
  list_indices_array:
    seq:
      - id: entries
        type: list_entry
        repeat: until
        repeat-until: _io.pos >= (_root.gff_header.list_indices_offset + _root.gff_header.list_indices_count * 4)
        doc: List entry structures (count + struct indices)
  
  list_entry:
    seq:
      - id: count
        type: u4
        doc: Number of struct indices in this list entry
      
      - id: struct_indices
        type: u4
        repeat: expr
        repeat-expr: count
        doc: Array of struct indices (uint32) pointing to struct_array entries

