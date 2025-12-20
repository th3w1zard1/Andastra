meta:
  id: fac
  title: BioWare FAC (Faction) File Format
  license: MIT
  endian: le
  file-extension: fac
  xref:
    pykotor: vendor/PyKotor/wiki/Bioware-Aurora-Faction.md
    reone: vendor/reone/src/libs/resource/parser/gff/fac.cpp
    xoreos: vendor/xoreos/src/aurora/gff3file.cpp
    bioware_docs: vendor/xoreos-docs/specs/bioware/Faction_Format.pdf
doc: |
  FAC (Faction) files are GFF-based format files that define faction relationships for determining
  how game objects interact with each other in terms of friendly, neutral, and hostile reactions.
  FAC files use the GFF (Generic File Format) binary structure with file type signature "FAC ".

  FAC files are typically named "repute.fac" and are stored in module archives or savegames.
  They define:
  - FactionList: List of factions that exist in the module
  - RepList: Reputation matrix defining how each faction perceives every other faction

  Faction System:
  - Reputation values range from 0-100:
    - 0-10: Hostile (will attack on sight)
    - 11-89: Neutral (will not attack, but not friendly)
    - 90-100: Friendly (allied, will assist in combat)
  - Global Effect: Some factions have a global flag - if one member changes standings,
    all members immediately update their standings (e.g., Guards faction)
  - Parent Factions: Factions can inherit from parent factions (standard factions use 0xFFFFFFFF)
  - Standard factions: PC (0), Hostile (1), Commoner (2), Merchant (3), Gizka (4), etc.

  The reputation matrix should contain N*N entries where N = number of factions, but entries
  where FactionID2 == 0 (PC faction) are often omitted since PC behavior is player-controlled.

  References:
  - vendor/PyKotor/wiki/Bioware-Aurora-Faction.md (official BioWare documentation)
  - vendor/xoreos-docs/specs/bioware/Faction_Format.pdf (original PDF)
  - vendor/xoreos/src/aurora/gff3file.cpp
  - vendor/reone/src/libs/resource/parser/gff/fac.cpp

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
    doc: List indices array for LIST type fields (FactionList and RepList)

types:
  # GFF Header (56 bytes)
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "FAC " for faction files.
        valid: "FAC "

      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File format version. Typically "V3.2" for KotOR.
          Other versions: "V3.3", "V4.0", "V4.1" for other BioWare games.
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

  # Label Array
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
          Common FAC field names: "FactionList", "RepList", "FactionName", "FactionGlobal",
          "FactionParentID", "FactionID1", "FactionID2", "FactionRep".
    instances:
      name_trimmed:
        value: name.rstrip('\x00')
        doc: "Label name with trailing nulls removed"

  # Struct Array
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
          FAC-specific struct IDs:
          - 0xFFFFFFFF (-1): Root struct containing FactionList and RepList
          - List indices: Faction structs and Reputation structs (referenced via LIST fields)
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

  # Field Array
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
          - 13: Binary/Void
          - 14: Struct (nested struct)
          - 15: List (list of structs)
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
    instances:
      label_name:
        value: _root.label_array.labels[label_index].name_trimmed
        doc: Field name from label array

  # Field Data Section
  field_data_section:
    seq:
      - id: data
        type: u1
        repeat: expr
        repeat-expr: _root.gff_header.field_data_count
        doc: Raw field data bytes (strings, ResRefs, binary data, etc.)

  # Field Indices Array
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
      - id: entries
        type: list_entry
        repeat: until
        repeat-until: _io.pos >= (_root.gff_header.list_indices_offset + _root.gff_header.list_indices_count)
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

