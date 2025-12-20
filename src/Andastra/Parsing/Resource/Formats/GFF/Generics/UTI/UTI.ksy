meta:
  id: uti
  title: BioWare UTI (Item Template) File Format
  license: MIT
  endian: le
  file-extension: uti
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/uti.py
    reone: vendor/reone/src/libs/resource/parser/gff/uti.cpp
    xoreos: vendor/xoreos/src/aurora/gfffile.cpp
    wiki: vendor/PyKotor/wiki/GFF-UTI.md
doc: |
  UTI (Item Template) files are GFF-based format files that store item definitions including
  properties, costs, charges, upgrade information, and visual variations. UTI files use the
  GFF (Generic File Format) binary structure with file type signature "UTI ".

  UTI files contain:
  - Root struct with item metadata (TemplateResRef, Tag, LocalizedName, Description, etc.)
  - Base item configuration (BaseItem, Cost, AddCost, Plot, Charges, StackSize)
  - Visual variations (ModelVariation, BodyVariation, TextureVar)
  - Item properties list (PropertiesList) with enchantments and bonuses
  - Upgrade system (Upgradable, UpgradeLevel, WeaponColor, WeaponWhoosh)
  - Quest item flags (Plot, Stolen, Cursed, Identified)
  - KotOR 2 specific fields (UpgradeLevel, WeaponColor, WeaponWhoosh, ArmorRulesType)

  Each field in the root struct contains:
  - Basic item properties (TemplateResRef, Tag, LocalizedName, Description)
  - Base item configuration (BaseItem index, Cost, Charges, StackSize)
  - Item properties (PropertiesList) as a list of property structs
  - Visual appearance (ModelVariation, BodyVariation, TextureVar, PaletteID)
  - Upgrade information (Upgradable, UpgradeLevel)
  - Weapon-specific fields (WeaponColor, WeaponWhoosh for lightsabers)
  - Armor-specific fields (ArmorRulesType)

  PropertiesList is a list of structs, each containing:
  - PropertyName (Word): index into itempropdef.2da
  - Subtype (Word): Property subtype/category
  - CostTable (Byte): Cost table index
  - CostValue (Word): Cost value
  - Param1 (Byte): First parameter
  - Param1Value (Byte): First parameter value
  - ChanceAppear (Byte): Percentage chance to appear (random loot)
  - UsesPerDay (Byte): Daily usage limit (0 = unlimited)
  - UsesLeft (Byte): Remaining uses for today
  - UpgradeType (Byte, optional): Upgrade type (KotOR 2)

  References:
  - vendor/PyKotor/wiki/GFF-UTI.md
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - vendor/reone/src/libs/resource/parser/gff/uti.cpp
  - vendor/xoreos/src/aurora/gfffile.cpp

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
    pos: gff_header.struct_array_offset
    doc: Array of struct entries (12 bytes each)

  - id: field_array
    type: field_array
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
  # GFF Header (56 bytes)
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "UTI " for item template files.
          Other GFF types: "GFF ", "ARE ", "DLG ", "UTC ", "UTD ", etc.
        valid: "UTI "

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
        doc: |
          Field data type (see GFFFieldType enum):
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
