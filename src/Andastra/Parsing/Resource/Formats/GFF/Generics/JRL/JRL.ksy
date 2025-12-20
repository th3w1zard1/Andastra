meta:
  id: jrl
  title: BioWare JRL (Journal) File Format
  license: MIT
  endian: le
  file-extension: jrl
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/jrl.py
    reone: vendor/reone/src/libs/resource/parser/gff/jrl.cpp
    xoreos: vendor/xoreos/src/aurora/jrlfile.cpp
    wiki: vendor/PyKotor/wiki/GFF-JRL.md
doc: |
  JRL (Journal) files are GFF-based format files that store journal/quest data including
  quest entries, priorities, and planet associations. JRL files use the GFF (Generic File Format)
  binary structure with file type signature "JRL ".

  JRL files are used by BioWare games (KotOR, KotOR 2, Neverwinter Nights) to define quest
  journal entries. The journal system tracks player progress through quests, displaying
  appropriate text entries based on quest state.

  File Structure:
  JRL files follow the standard GFF binary format with the following logical structure:
  - Root struct contains a "Categories" field (List type) containing quest definitions
  - Each quest category (JRLQuest) is a struct in the Categories list with fields:
    - Name (CExoLocString): Quest title displayed in journal
    - PlanetID (Int32): Planet association (legacy field, typically unused)
    - PlotIndex (Int32): Legacy plot index for sorting
    - Priority (UInt32): Sorting priority (0=Highest, 4=Lowest)
    - Tag (CExoString): Unique quest identifier (used by scripts/dialogue)
    - Comment (CExoString): Developer comment (toolset only, not shown in game)
    - EntryList (List): Array of quest entry structs (journal states)
  - Each quest entry (JRLQuestEntry) is a struct in EntryList with fields:
    - ID (UInt32): State identifier (referenced by AddJournalQuestEntry script function)
    - Text (CExoLocString): Journal text displayed for this quest state
    - End (UInt16): 1 if this state completes the quest (moves to Completed tab), 0 otherwise
    - XP_Percentage (Single): XP reward multiplier (scales journal.2da XP value)

  Priority Levels:
  - 0 (Highest): Main quest line (always shown at top)
  - 1 (High): Important side quests
  - 2 (Medium): Standard side quests
  - 3 (Low): Minor tasks
  - 4 (Lowest): Completed/Archived quests (moved to Completed tab)

  Usage:
  - Scripts use AddJournalQuestEntry("Tag", ID) to update quest states
  - Dialogues use Quest and QuestEntry fields to trigger journal updates
  - Only the highest ID reached is typically displayed (unless AllowOverrideHigher is set)
  - End=1 moves the quest to the "Completed" tab
  - global.jrl is the master journal file for the entire game

  References:
  - vendor/PyKotor/wiki/GFF-JRL.md
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/jrl.py
  - vendor/reone/src/libs/resource/parser/gff/jrl.cpp
  - vendor/xoreos/src/aurora/jrlfile.cpp

seq:
  - id: gff_header
    type: gff_header
    doc: GFF file header (56 bytes)
  
  - id: label_array
    type: label_array
    if: gff_header.label_count > 0
    doc: Array of field name labels (16-byte null-terminated strings)
  
  - id: struct_array
    type: struct_array
    doc: Array of struct entries (12 bytes each)
  
  - id: field_array
    type: field_array
    doc: Array of field entries (12 bytes each)
  
  - id: field_data
    type: field_data_section
    if: gff_header.field_data_count > 0
    doc: Field data section for complex types (strings, ResRefs, LocalizedStrings, etc.)
  
  - id: field_indices
    type: field_indices_array
    if: gff_header.field_indices_count > 0
    doc: Field indices array (MultiMap) for structs with multiple fields)
  
  - id: list_indices
    type: list_indices_array
    if: gff_header.list_indices_count > 0
    doc: List indices array for LIST type fields

types:
  # GFF Header (56 bytes)
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "JRL " for journal files.
          Other GFF types: "GFF ", "ARE ", "UTC ", "UTI ", "DLG ", etc.

      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File format version. Typically "V3.2" for KotOR.
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
        doc: Number of field indices (uint32 values) in the field_indices array

      - id: list_indices_offset
        type: u4
        doc: Byte offset to list indices array from the beginning of the file

      - id: list_indices_count
        type: u4
        doc: Number of list indices (uint32 values) in the list_indices array

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
      - id: data
        type: str
        encoding: UTF-8
        size: _root.gff_header.field_data_count
        doc: Raw field data bytes for complex types

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
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.gff_header.list_indices_count
        doc: Array of list indices (uint32 values) for LIST type fields

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


