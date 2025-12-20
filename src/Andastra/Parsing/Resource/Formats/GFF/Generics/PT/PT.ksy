meta:
  id: pt
  title: BioWare PT (Party Table) File Format
  license: MIT
  endian: le
  file-extension: pt
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/savedata.py
    reone: vendor/reone/src/libs/resource/parser/gff/pt.cpp
    xoreos: vendor/xoreos/src/aurora/gff3file.cpp
    wiki: vendor/PyKotor/wiki/GFF-File-Format.md
doc: |
  PT (Party Table) files are GFF-based format files that store party and game state information
  for Odyssey Engine games (KotOR and KotOR 2). PT files use the GFF (Generic File Format) binary
  structure with file type signature "PT  ".

  PT files are typically named "PARTYTABLE.res" and are stored in savegame.sav ERF archives.
  They contain comprehensive party and game state information including:
  - Party composition (current members, available NPCs, leader)
  - Resources (gold/credits, XP pool, components, chemicals)
  - Journal entries with states, dates, and times
  - Pazaak cards and side decks for the mini-game
  - UI state (last panel, messages, tutorial windows shown)
  - AI state (follow mode, AI enabled, solo mode)
  - K2-specific: Influence values per companion

  Based on swkotor2.exe: SavePartyTable @ 0x0057bd70
  Located via string reference: "PARTYTABLE" @ 0x007c1910
  Original implementation: Creates GFF with "PT  " signature and "V2.0" version string

  References:
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/savedata.py
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - src/Andastra/Runtime/Content/Save/SaveSerializer.cs (SerializePartyTable)

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
    doc: List indices array for LIST type fields (PT_MEMBERS, PT_PUPPETS, PT_AVAIL_PUPS, PT_AVAIL_NPCS, PT_INFLUENCE, etc.)

types:
  # GFF Header (56 bytes) - Standard GFF header structure
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "PT  " for party table files.
        valid: "PT  "

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
          Common PT field names: "PT_PCNAME", "PT_GOLD", "PT_XP_POOL", "PT_PLAYEDSECONDS",
          "PT_CONTROLLED_NPC", "PT_SOLOMODE", "PT_CHEAT_USED", "PT_NUM_MEMBERS", "PT_NUM_PUPPETS",
          "PT_AISTATE", "PT_FOLLOWSTATE", "PT_ITEM_COMPONENT", "PT_ITEM_CHEMICAL",
          "PT_SWOOP1", "PT_SWOOP2", "PT_SWOOP3", "PT_LAST_GUI_PNL", "PT_DISABLEMAP", "PT_DISABLEREGEN",
          "PT_MEMBERS", "PT_PUPPETS", "PT_AVAIL_PUPS", "PT_AVAIL_NPCS", "PT_INFLUENCE",
          "PT_PAZAAKCARDS", "PT_PAZSIDELIST", "PT_TUT_WND_SHOWN",
          "PT_FB_MSG_LIST", "PT_DLG_MSG_LIST", "PT_COM_MSG_LIST", "PT_COST_MULT_LIST",
          "GlxyMap", "GlxyMapNumPnts", "GlxyMapPlntMsk", "GlxyMapSelPnt",
          "PT_MEMBER_ID", "PT_IS_LEADER", "PT_PUPPET_ID", "PT_PUP_AVAIL", "PT_PUP_SELECT",
          "PT_NPC_AVAIL", "PT_NPC_SELECT", "PT_NPC_INFLUENCE", "PT_PAZAAKCOUNT", "PT_PAZSIDECARD",
          "PT_FB_MSG_MSG", "PT_FB_MSG_TYPE", "PT_FB_MSG_COLOR", "PT_DLG_MSG_SPKR", "PT_DLG_MSG_MSG",
          "PT_COM_MSG_MSG", "PT_COM_MSG_TYPE", "PT_COM_MSG_COOR", "PT_COST_MULT_VALUE".
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
          PT files typically have the root struct plus structs for list entries
          (PT_MEMBERS entries, PT_PUPPETS entries, PT_AVAIL_PUPS entries, etc.).

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

  # Field Data Section - Standard GFF field data section
  field_data_section:
    seq:
      - id: data
        type: u1
        repeat: expr
        repeat-expr: _root.gff_header.field_data_count
        doc: Raw field data bytes (strings, ResRefs, binary data, etc.)

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

