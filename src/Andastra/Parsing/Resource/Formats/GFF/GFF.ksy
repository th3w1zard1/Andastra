meta:
  id: gff
  title: BioWare GFF (Generic File Format) File
  license: MIT
  endian: le
  file-extension: gff
  xref:
    pykotor: Libraries/PyKotor/src/pykotor/resource/formats/gff/
    reone: vendor/reone/src/libs/resource/format/gffreader.cpp
    xoreos: vendor/xoreos/src/aurora/gff3file.cpp
doc: |
  GFF (Generic File Format) is BioWare's universal container format for structured game data.
  It is used by many KotOR file types including UTC (creature), UTI (item), DLG (dialogue),
  ARE (area), GIT (game instance template), IFO (module info), and many others.
  
  GFF uses a hierarchical structure with structs containing fields, which can be simple values,
  nested structs, or lists of structs. The format supports version V3.2 (KotOR) and later
  versions (V3.3, V4.0, V4.1) used in other BioWare games.
  
  References:
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - vendor/xoreos-docs/specs/torlack/itp.html (Tim Smith/Torlack's GFF/ITP documentation)
  - vendor/reone/src/libs/resource/format/gffreader.cpp

seq:
  - id: header
    type: gff_header
    doc: GFF file header (56 bytes total)
  
  - id: label_array
    type: label_array
    if: header.label_count > 0
    pos: header.label_offset
    doc: Array of 16-byte null-padded field name labels
  
  - id: struct_array
    type: struct_array
    if: header.struct_count > 0
    pos: header.struct_offset
    doc: Array of struct entries (12 bytes each)
  
  - id: field_array
    type: field_array
    if: header.field_count > 0
    pos: header.field_offset
    doc: Array of field entries (12 bytes each)
  
  - id: field_data
    type: field_data
    if: header.field_data_count > 0
    pos: header.field_data_offset
    doc: Storage area for complex field types (strings, binary, vectors, etc.)
  
  - id: field_indices_array
    type: field_indices_array
    if: header.field_indices_count > 0
    pos: header.field_indices_offset
    doc: Array of field index arrays (used when structs have multiple fields)
  
  - id: list_indices_array
    type: list_indices_array
    if: header.list_indices_count > 0
    pos: header.list_indices_offset
    doc: Array of list entry structures (count + struct indices)

types:
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature (FourCC). Examples: "GFF ", "UTC ", "UTI ", "DLG ", "ARE ", etc.
          Must match a valid GFFContent enum value.
      
      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File format version. Must be "V3.2" for KotOR games.
          Later BioWare games use "V3.3", "V4.0", or "V4.1".
        valid: ["V3.2", "V3.3", "V4.0", "V4.1"]
      
      - id: struct_offset
        type: u4
        doc: Byte offset to struct array from beginning of file
      
      - id: struct_count
        type: u4
        doc: Number of struct entries in struct array
      
      - id: field_offset
        type: u4
        doc: Byte offset to field array from beginning of file
      
      - id: field_count
        type: u4
        doc: Number of field entries in field array
      
      - id: label_offset
        type: u4
        doc: Byte offset to label array from beginning of file
      
      - id: label_count
        type: u4
        doc: Number of labels in label array
      
      - id: field_data_offset
        type: u4
        doc: Byte offset to field data section from beginning of file
      
      - id: field_data_count
        type: u4
        doc: Size of field data section in bytes
      
      - id: field_indices_offset
        type: u4
        doc: Byte offset to field indices array from beginning of file
      
      - id: field_indices_count
        type: u4
        doc: Number of field indices (total count across all structs with multiple fields)
      
      - id: list_indices_offset
        type: u4
        doc: Byte offset to list indices array from beginning of file
      
      - id: list_indices_count
        type: u4
        doc: Number of list indices entries
  
  label_array:
    seq:
      - id: labels
        type: label_entry
        repeat: expr
        repeat-expr: _root.header.label_count
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
  
  struct_array:
    seq:
      - id: entries
        type: struct_entry
        repeat: expr
        repeat-expr: _root.header.struct_count
        doc: Array of struct entries (12 bytes each)
  
  struct_entry:
    seq:
      - id: struct_id
        type: s4
        doc: |
          Structure type identifier. Often 0xFFFFFFFF (-1) for generic structs.
          Used to identify struct types in schema-aware parsers.
      
      - id: data_or_offset
        type: u4
        doc: |
          Field index (if field_count == 1) or byte offset to field indices array (if field_count > 1).
          If field_count == 0, this value is unused.
      
      - id: field_count
        type: u4
        doc: |
          Number of fields in this struct:
          - 0: No fields
          - 1: Single field, data_or_offset contains the field index directly
          - >1: Multiple fields, data_or_offset contains byte offset into field_indices_array
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
  
  field_array:
    seq:
      - id: entries
        type: field_entry
        repeat: expr
        repeat-expr: _root.header.field_count
        doc: Array of field entries (12 bytes each)
  
  field_entry:
    seq:
      - id: field_type
        type: u4
        enum: gff_field_type
        doc: |
          Field data type (see gff_field_type enum):
          - 0-5, 8: Simple types (stored inline in data_or_offset)
          - 6-7, 9-13, 16-17: Complex types (offset to field_data in data_or_offset)
          - 14: Struct (struct index in data_or_offset)
          - 15: List (offset to list_indices_array in data_or_offset)
      
      - id: label_index
        type: u4
        doc: Index into label_array for field name
      
      - id: data_or_offset
        type: u4
        doc: |
          Inline data (simple types) or offset/index (complex types):
          - Simple types (0-5, 8): Value stored directly (1-4 bytes, sign/zero extended to 4 bytes)
          - Complex types (6-7, 9-13, 16-17): Byte offset into field_data section (relative to field_data_offset)
          - Struct (14): Struct index (index into struct_array)
          - List (15): Byte offset into list_indices_array (relative to list_indices_offset)
    instances:
      is_simple_type:
        value: (field_type.value >= 0 && field_type.value <= 5) || field_type.value == 8
        doc: True if field stores data inline (simple types)
      is_complex_type:
        value: (field_type.value >= 6 && field_type.value <= 13) || (field_type.value >= 16 && field_type.value <= 17)
        doc: True if field stores data in field_data section
      is_struct_type:
        value: field_type.value == 14
        doc: True if field is a nested struct
      is_list_type:
        value: field_type.value == 15
        doc: True if field is a list of structs
      field_data_offset_value:
        value: _root.header.field_data_offset + data_or_offset
        if: is_complex_type
        doc: Absolute file offset to field data for complex types
      struct_index_value:
        value: data_or_offset
        if: is_struct_type
        doc: Struct index for struct type fields
      list_indices_offset_value:
        value: _root.header.list_indices_offset + data_or_offset
        if: is_list_type
        doc: Absolute file offset to list indices for list type fields
  
  field_data:
    seq:
      - id: raw_data
        type: str
        size: _root.header.field_data_count
        doc: |
          Raw field data storage. Individual field data entries are accessed via
          field_entry.field_data_offset_value offsets. The structure of each entry
          depends on the field_type:
          - UInt64/Int64/Double: 8 bytes
          - String: 4-byte length + string bytes
          - ResRef: 1-byte length + string bytes (max 16)
          - LocalizedString: variable (see localized_string_data type)
          - Binary: 4-byte length + binary bytes
          - Vector3: 12 bytes (3×float)
          - Vector4: 16 bytes (4×float)
  
  field_indices_array:
    seq:
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.header.field_indices_count
        doc: |
          Array of field indices. When a struct has multiple fields, it stores an offset
          into this array, and the next N consecutive u4 values (where N = struct.field_count)
          are the field indices for that struct.
  
  list_indices_array:
    seq:
      - id: raw_data
        type: str
        size: _root.header.list_indices_count
        doc: |
          Raw list indices data. List entries are accessed via offsets stored in
          list-type field entries (field_entry.list_indices_offset_value).
          Each entry starts with a count (u4), followed by that many struct indices (u4 each).
          
          Note: This is a raw data block. In practice, list entries are accessed via
          offsets stored in list-type field entries, not as a sequential array.
          Use list_entry type to parse individual entries at specific offsets.
      
  
  list_entry:
    seq:
      - id: count
        type: u4
        doc: Number of struct indices in this list
      - id: struct_indices
        type: u4
        repeat: expr
        repeat-expr: count
        doc: Array of struct indices (indices into struct_array)
  
  # Field data types (used when accessing field_data section)
  
  simple_field_data:
    doc: |
      Simple field types store their values inline in the field_entry.data_or_offset field.
      Values are stored as:
      - UInt8 (type 0): 1 byte, zero-extended to 4 bytes
      - Int8 (type 1): 1 byte, sign-extended to 4 bytes
      - UInt16 (type 2): 2 bytes, zero-extended to 4 bytes
      - Int16 (type 3): 2 bytes, sign-extended to 4 bytes
      - UInt32 (type 4): 4 bytes directly
      - Int32 (type 5): 4 bytes directly
      - Single/Float (type 8): 4 bytes (IEEE 754 single precision)
    
    The actual extraction from the 4-byte data_or_offset value depends on the field_type.
    This type serves as documentation only - actual parsing is handled in application code.
  
  complex_field_data:
    doc: |
      Complex field types store data in the field_data section. The field_entry.data_or_offset
      contains a byte offset (relative to field_data_offset) into the field_data section.
    
    The structure depends on field_type:
    - UInt64 (type 6): 8 bytes (u8, little-endian)
    - Int64 (type 7): 8 bytes (s8, little-endian)
    - Double (type 9): 8 bytes (f8, IEEE 754 double precision, little-endian)
    - String (type 10): 4-byte length (u4) + length bytes (ASCII)
    - ResRef (type 11): 1-byte length (u1, max 16) + length bytes (ASCII, null-padded)
    - LocalizedString (type 12): See localized_string_data type
    - Binary (type 13): 4-byte length (u4) + length bytes
    - Vector4/Orientation (type 16): 16 bytes (4×f4, little-endian)
    - Vector3 (type 17): 12 bytes (3×f4, little-endian)
  
  localized_string_data:
    seq:
      - id: total_size
        type: u4
        doc: |
          Total size of this LocalizedString structure in bytes (not including this count).
          Used for skipping over the structure, but can be calculated from the data.
      
      - id: string_ref
        type: u4
        doc: |
          String reference ID (StrRef) into dialog.tlk file.
          Value 0xFFFFFFFF indicates no string reference (-1).
      
      - id: string_count
        type: u4
        doc: Number of language-specific string substrings
      
      - id: substrings
        type: localized_substring
        repeat: expr
        repeat-expr: string_count
        doc: Array of language-specific string substrings
    instances:
      string_ref_value:
        value: string_ref == 0xFFFFFFFF ? -1 : string_ref
        doc: String reference as signed integer (-1 if none)
  
  localized_substring:
    seq:
      - id: string_id
        type: u4
        doc: |
          String ID encoding language and gender:
          - Bits 0-7: Gender (0 = Male, 1 = Female)
          - Bits 8-15: Language ID (see Language enum)
          - Bits 16-31: Reserved/unused
      
      - id: string_length
        type: u4
        doc: Length of string data in bytes
      
      - id: string_data
        type: str
        size: string_length
        encoding: UTF-8
        doc: |
          String data (encoding depends on language, but UTF-8 is common).
          Trailing null bytes should be trimmed.
    instances:
      language_id:
        value: (string_id >> 8) & 0xFF
        doc: Language ID (extracted from string_id)
      gender_id:
        value: string_id & 0xFF
        doc: Gender ID (0 = Male, 1 = Female)

enums:
  gff_field_type:
    0: uint8
    doc: 8-bit unsigned integer (byte)
    1: int8
    doc: 8-bit signed integer (char)
    2: uint16
    doc: 16-bit unsigned integer (word)
    3: int16
    doc: 16-bit signed integer (short)
    4: uint32
    doc: 32-bit unsigned integer (dword)
    5: int32
    doc: 32-bit signed integer (int)
    6: uint64
    doc: 64-bit unsigned integer (stored in field_data)
    7: int64
    doc: 64-bit signed integer (stored in field_data)
    8: single
    doc: 32-bit floating point (float)
    9: double
    doc: 64-bit floating point (stored in field_data)
    10: string
    doc: Null-terminated string (CExoString, stored in field_data)
    11: resref
    doc: Resource reference (ResRef, max 16 chars, stored in field_data)
    12: localized_string
    doc: Localized string (CExoLocString, stored in field_data)
    13: binary
    doc: Binary data blob (Void, stored in field_data)
    14: struct
    doc: Nested struct (struct index stored inline)
    15: list
    doc: List of structs (offset to list_indices stored inline)
    16: vector4
    doc: Quaternion/Orientation (4×float, stored in field_data as Vector4)
    17: vector3
    doc: 3D vector (3×float, stored in field_data)

