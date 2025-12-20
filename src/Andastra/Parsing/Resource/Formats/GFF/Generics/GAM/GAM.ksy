meta:
  id: gam
  title: BioWare GAM (Game State) File Format
  license: MIT
  endian: le
  file-extension: gam
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/
    reone: vendor/reone/src/libs/resource/parser/gff/
    xoreos: vendor/xoreos/src/aurora/
    wiki: vendor/PyKotor/wiki/Bioware-Aurora-GFF.md
doc: |
  GAM (Game State) files are GFF-based format files that store game state information
  including party members, global variables, game time, and time played.
  
  GAM files are used by:
  - Aurora Engine (Neverwinter Nights, Neverwinter Nights 2)
  - Infinity Engine (Mass Effect, Dragon Age Origins, Dragon Age 2)
  
  NOTE: Odyssey Engine (Knights of the Old Republic, Knights of the Old Republic 2)
  does NOT use GAM format - it uses NFO format for save games instead.
  
  GAM files use the GFF (Generic File Format) binary structure with file type signature "GAM ".
  
  Root struct fields:
  - GameTimeHour (Int32): Current game time hour (0-23)
  - GameTimeMinute (Int32): Current game time minute (0-59)
  - GameTimeSecond (Int32): Current game time second (0-59)
  - GameTimeMillisecond (Int32): Current game time millisecond (0-999)
  - TimePlayed (Int32): Total time played in seconds
  
  - PartyList (List): Array of party member structs
    - PartyMember (ResRef): Resource reference to party member creature file
  
  - GlobalBooleans (List): Array of boolean global variable structs
    - Name (String): Variable name
    - Value (UInt8): Variable value (0 = false, 1 = true)
  
  - GlobalNumbers (List): Array of numeric global variable structs
    - Name (String): Variable name
    - Value (Int32): Variable value
  
  - GlobalStrings (List): Array of string global variable structs
    - Name (String): Variable name
    - Value (String): Variable value
  
  Aurora Engine-specific fields (nwmain.exe, nwn2main.exe):
  - ModuleName (String): Current module name
  - CurrentArea (ResRef): Resource reference to current area file
  - PlayerCharacter (ResRef): Resource reference to player character creature file
  
  Infinity Engine-specific fields (daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe):
  - GameName (String): Game name/identifier
  - Chapter (Int32): Current chapter number
  - JournalEntries (List): Array of journal entry structs
    - TextStrRef (Int32): String reference ID for journal entry text
    - Completed (UInt8): Whether journal entry is completed (0 = false, 1 = true)
    - Category (Int32): Journal entry category ID
  
  References:
  - vendor/PyKotor/wiki/Bioware-Aurora-GFF.md
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - src/Andastra/Parsing/Resource/Formats/GFF/Generics/GAM.cs
  - src/Andastra/Parsing/Resource/Formats/GFF/Generics/GAMHelpers.cs

seq:
  - id: gff_header
    type: gff_header
    doc: |
      GFF file header (56 bytes).
      Contains offsets to other sections (label_array, struct_array, field_array, etc.).
      Note: Other sections are at absolute file offsets specified in the header.
      Due to Kaitai Struct limitations with absolute positioning in seq, these sections
      should be accessed via instances or manual seeking in application code.

types:
  # GFF Header (56 bytes)
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "GAM " (space-padded) for game state files.
          Other GFF types: "GFF ", "ARE ", "UTC ", "UTI ", "DLG ", etc.
      
      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File format version. Typically "V3.2" for KotOR-era games.
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
  
  # Label Array
  label_array:
    seq:
      - id: labels
        type: str
        encoding: ASCII
        size: 16
        repeat: expr
        repeat-expr: _root.gff_header.label_count
        doc: Array of 16-byte null-terminated field name labels
  
  # Struct Array
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
        doc: |
          Structure type identifier.
          Root struct always has struct_id = 0xFFFFFFFF (-1).
          Other structs have programmer-defined IDs.
      
      - id: data_or_offset
        type: u4
        doc: |
          If field_count = 1: Direct field index into field_array.
          If field_count > 1: Byte offset into field_indices array.
          If field_count = 0: Unused (empty struct).
      
      - id: field_count
        type: u4
        doc: Number of fields in this struct (0, 1, or >1)
  
  # Field Array
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
        enum: gff_field_type
        doc: |
          Field data type (see gff_field_type enum):
          0 = Byte (UInt8)
          1 = Char (Int8)
          2 = UInt16
          3 = Int16
          4 = UInt32
          5 = Int32
          6 = UInt64
          7 = Int64
          8 = Single (Float32)
          9 = Double (Float64)
          10 = CExoString (String)
          11 = ResRef
          12 = CExoLocString (LocalizedString)
          13 = Void (Binary)
          14 = Struct
          15 = List
          16 = Vector3
          17 = Vector4
      
      - id: label_index
        type: u4
        doc: Index into label_array for field name
      
      - id: data_or_offset
        type: u4
        doc: |
          For simple types (Byte, Char, UInt16, Int16, UInt32, Int32, UInt64, Int64, Single, Double):
            Inline data value (stored directly in this field)
          For complex types (String, ResRef, LocalizedString, Binary, Vector3, Vector4):
            Byte offset into field_data section
          For Struct type:
            Struct index into struct_array
          For List type:
            Byte offset into list_indices array
  
  # Field Data Section
  field_data_section:
    seq:
      - id: raw_data
        type: str
        size: _root.gff_header.field_data_count
        encoding: UTF-8
        doc: |
          Raw field data storage. Individual field data entries are accessed via
          field_entry.field_data_offset_value offsets. The structure of each entry
          depends on the field_type:
          - UInt64/Int64/Double: 8 bytes
          - String: 4-byte length (u4) + string bytes (ASCII)
          - ResRef: 1-byte length (u1, max 16) + string bytes (ASCII, null-padded)
          - LocalizedString: variable (see localized_string_data type)
          - Binary: 4-byte length (u4) + binary bytes
          - Vector3: 12 bytes (3×f4, little-endian)
          - Vector4: 16 bytes (4×f4, little-endian)
  
  # Field Indices Array (MultiMap)
  field_indices_array:
    seq:
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.gff_header.field_indices_count
        doc: Array of field indices (uint32 values) for structs with multiple fields
  
  # List Indices Array
  list_indices_array:
    seq:
      - id: raw_data
        type: str
        size: _root.gff_header.list_indices_count
        encoding: UTF-8
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

  # Complex field data types (used when accessing field_data section)
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
    1: int8
    2: uint16
    3: int16
    4: uint32
    5: int32
    6: uint64
    7: int64
    8: single
    9: double
    10: string
    11: resref
    12: localized_string
    13: binary
    14: struct
    15: list
    16: vector4
    17: vector3


