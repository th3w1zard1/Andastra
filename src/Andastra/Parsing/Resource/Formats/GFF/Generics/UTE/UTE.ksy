meta:
  id: ute
  title: BioWare UTE (Encounter Template) File Format
  license: MIT
  endian: le
  file-extension: ute
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/ute.py
    reone: vendor/reone/src/libs/resource/parser/gff/ute.cpp
    xoreos: vendor/xoreos/src/aurora/gfffile.cpp
    wiki: vendor/PyKotor/wiki/GFF-UTE.md
doc: |
  UTE (Encounter Template) files are GFF-based format files that store encounter definitions including
  creature spawn lists, difficulty, respawn settings, and script hooks. UTE files use the GFF (Generic File Format)
  binary structure with file type signature "UTE ".
  
  UTE files contain:
  - Root struct with encounter metadata (TemplateResRef, Tag, Active, DifficultyIndex, Faction, etc.)
  - CreatureList: List of UTE_CreatureList structs containing spawnable creatures
  - Script hooks (OnEntered, OnExit, OnExhausted, OnHeartbeat, OnUserDefined)
  - Respawn logic (Reset, ResetTime, Respawns)
  - Spawn configuration (MaxCreatures, RecCreatures, SpawnOption, PlayerOnly)
  
  References:
  - vendor/PyKotor/wiki/GFF-UTE.md
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - vendor/reone/include/reone/resource/parser/gff/ute.h:28-59
  - vendor/reone/src/libs/resource/parser/gff/ute.cpp:28-65
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/ute.py:15-222

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
    doc: Field data section for complex types (strings, ResRefs, LocalizedStrings, etc.)
  
  - id: field_indices
    type: field_indices_array
    if: gff_header.field_indices_count > 0
    pos: gff_header.field_indices_offset
    doc: Field indices array (MultiMap) for structs with multiple fields
  
  - id: list_indices
    type: list_indices_array
    if: gff_header.list_indices_count > 0
    pos: gff_header.list_indices_offset
    doc: List indices array for LIST type fields

types:
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: File type signature. Must be "UTE " for encounter template files.
        valid: "UTE "
      
      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: File format version. Typically "V3.2" for KotOR.
        valid: ["V3.2", "V3.3", "V4.0", "V4.1"]
      
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
  
  label_array:
    seq:
      - id: labels
        type: str
        encoding: ASCII
        size: 16
        repeat: expr
        repeat-expr: _root.gff_header.label_count
        doc: Array of 16-byte null-terminated field name labels
  
  struct_array:
    seq:
      - id: entries
        type: struct_entry
        repeat: expr
        repeat-expr: _root.gff_header.struct_count
        doc: Array of struct entries
  
  struct_entry:
    seq:
      - id: struct_id
        type: s4
        doc: Structure type identifier. Root struct always has struct_id = 0xFFFFFFFF (-1).
      
      - id: data_or_offset
        type: u4
        doc: |
          If field_count = 1: Direct field index into field_array.
          If field_count > 1: Byte offset into field_indices array.
          If field_count = 0: Unused (empty struct).
      
      - id: field_count
        type: u4
        doc: Number of fields in this struct (0, 1, or >1)
  
  field_array:
    seq:
      - id: entries
        type: field_entry
        repeat: expr
        repeat-expr: _root.gff_header.field_count
        doc: Array of field entries
  
  field_entry:
    seq:
      - id: field_type
        type: u4
        doc: Field data type (0=Byte, 1=Char, 2=UInt16, 3=Int16, 4=UInt32, 5=Int32, 6=UInt64, 7=Int64, 8=Single, 9=Double, 10=CExoString, 11=ResRef, 12=CExoLocString, 13=Void, 14=Struct, 15=List, 16=Vector3, 17=Vector4)
      
      - id: label_index
        type: u4
        doc: Index into label_array for field name
      
      - id: data_or_offset
        type: u4
        doc: |
          For simple types (Byte, Char, UInt16, Int16, UInt32, Int32, UInt64, Int64, Single, Double): Inline data value.
          For complex types (String, ResRef, LocalizedString, Binary, Vector3, Vector4): Byte offset into field_data section.
          For Struct type: Struct index into struct_array.
          For List type: Byte offset into list_indices array.
  
  field_data_section:
    seq:
      - id: data
        type: str
        size: _root.gff_header.field_data_count
        doc: Raw field data bytes for complex types
  
  field_indices_array:
    seq:
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.gff_header.field_indices_count
        doc: Array of field indices (uint32 values) for structs with multiple fields
  
  list_indices_array:
    seq:
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.gff_header.list_indices_count
        doc: Array of list indices (uint32 values) for LIST type fields