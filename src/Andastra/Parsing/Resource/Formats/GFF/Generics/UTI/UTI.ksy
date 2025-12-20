meta:
  id: uti
  title: BioWare UTI (Item Template) File Format
  license: MIT
  endian: le
  file-extension: uti
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/uti.py
    reone: vendor/reone/src/libs/resource/parser/gff/uti.cpp
    wiki: vendor/PyKotor/wiki/GFF-UTI.md
doc: |
  UTI (Item Template) files are GFF-based format files that store item definitions including
  properties, costs, charges, and upgrade information. UTI files use the GFF (Generic File Format)
  binary structure with file type signature "UTI ".
  
  UTI files contain:
  - Root struct with item metadata:
    - TemplateResRef: Item template ResRef (unique identifier, max 16 characters, ResRef type)
    - LocalizedName: Localized item name (LocalizedString/CExoLocString type)
    - Description: Generic description when unidentified (LocalizedString/CExoLocString type)
    - DescIdentified: Description when item is identified (LocalizedString/CExoLocString type)
    - Tag: Item tag identifier (String/CExoString type, used for scripting references)
    - Comment: Developer comment string (String/CExoString type, toolset only, not used by game engine)
  - Base Item Configuration:
    - BaseItem: Index into baseitems.2da (Int32, defines item type: weapon, armor, upgrade, etc.)
    - Cost: Base value in credits (UInt32, base item cost)
    - AddCost: Additional cost from properties (UInt32, added to base cost for final value)
    - Plot: Plot-critical item flag (UInt8/Byte, 0 = normal item, 1 = cannot be sold/destroyed)
    - Charges: Number of uses remaining (UInt8/Byte, 0 = unlimited/not applicable)
    - StackSize: Current stack quantity (UInt16, how many items in this stack)
    - ModelVariation: Model variation index (UInt8/Byte, 1-99, determines which model variant to use)
    - BodyVariation: Body variation for armor (UInt8/Byte, 1-9, body model variation)
    - TextureVar: Texture variation for armor (UInt8/Byte, 1-9, texture variant)
    - PaletteID: Toolset palette category (UInt8/Byte, editor organization, doesn't affect gameplay)
  - Quest & Special Item flags:
    - Identified: Player has identified the item (Int32, 0 = unidentified, 1 = identified)
    - Stolen: Marked as stolen (Int32, deprecated, 0 = not stolen, 1 = stolen)
  - Item Properties:
    - PropertiesList: List of item properties and enchantments (List type, contains property structs)
    Each property struct contains:
      - PropertyName: Index into itempropdef.2da (UInt16, property type identifier)
      - Subtype: Property subtype/category (UInt16, property category)
      - CostTable: Cost table index (UInt8/Byte, which cost table to use)
      - CostValue: Cost value (UInt16, property cost value)
      - Param1: First parameter (UInt8/Byte, property parameter)
      - Param1Value: First parameter value (UInt8/Byte, parameter value)
      - ChanceAppear: Percentage chance to appear (UInt8/Byte, 0-100, for random loot)
      - UpgradeType: Upgrade type (UInt8/Byte, optional, KotOR2 only, upgrade slot type)
  - KotOR1-specific fields:
    - Upgradable: Item accepts upgrade items (UInt8/Byte, 0 = no upgrades, 1 = can upgrade)
  - KotOR2-specific fields:
    - UpgradeLevel: Current upgrade tier (UInt8/Byte, 0-2, number of upgrade slots used)
    - WeaponColor: Blade color for lightsabers (UInt8/Byte, 0-10, color index)
    - WeaponWhoosh: Whoosh sound type (UInt8/Byte, sound variant for weapon swings)
    - ArmorRulesType: Armor class category (UInt8/Byte, armor restriction category)
  
  BaseItem Types (from baseitems.2da):
  - 0-10: Various weapon types (shortsword, longsword, blaster, etc.)
  - 11-30: Armor types and shields
  - 31-50: Quest items, grenades, medical supplies
  - 51-70: Upgrades, armbands, belts
  - 71-90: Droid equipment, special items
  - 91+: KotOR2-specific items
  
  Common Item Properties (from itempropdef.2da):
  - Attack Bonus: +1 to +12 attack rolls
  - Damage Bonus: Additional damage dice
  - Ability Bonus: +1 to +12 to ability scores
  - Damage Resistance: Reduce damage by amount/percentage
  - Saving Throw Bonus: +1 to +20 to saves
  - Skill Bonus: +1 to +50 to skills
  - Immunity: Immunity to damage type or condition
  - On Hit: Cast spell/effect on successful hit
  - Keen: Expanded critical threat range
  - Massive Criticals: Bonus damage on critical hit
  
  Lightsaber Colors (KotOR2 WeaponColor):
  - 0: Blue, 1: Yellow, 2: Green, 3: Red
  - 4: Violet, 5: Orange, 6: Cyan, 7: Silver
  - 8: White, 9: Viridian, 10: Bronze
  
  References:
  - vendor/PyKotor/wiki/GFF-UTI.md
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/uti.py:20-400
  - vendor/reone/src/libs/resource/parser/gff/uti.cpp

seq:
  - id: gff_header
    type: gff_header
    doc: GFF file header (56 bytes)
  
  - id: label_array
    type: label_array
    if: gff_header.label_count > 0
    doc: |
      Array of field name labels (16-byte null-terminated strings).
      Located at offset gff_header.label_array_offset from start of file.
      Note: Position-based reading handled by application code due to Kaitai Struct limitations.
  
  - id: struct_array
    type: struct_array
    if: gff_header.struct_count > 0
    doc: |
      Array of struct entries (12 bytes each).
      Located at offset gff_header.struct_array_offset from start of file.
      Note: Position-based reading handled by application code due to Kaitai Struct limitations.
  
  - id: field_array
    type: field_array
    if: gff_header.field_count > 0
    doc: |
      Array of field entries (12 bytes each).
      Located at offset gff_header.field_array_offset from start of file.
      Note: Position-based reading handled by application code due to Kaitai Struct limitations.
  
  - id: field_data
    type: field_data_section
    if: gff_header.field_data_count > 0
    doc: |
      Field data section for complex types (strings, ResRefs, LocalizedStrings, etc.).
      Located at offset gff_header.field_data_offset from start of file.
      Note: Position-based reading handled by application code due to Kaitai Struct limitations.
  
  - id: field_indices
    type: field_indices_array
    if: gff_header.field_indices_count > 0
    doc: |
      Field indices array (MultiMap) for structs with multiple fields.
      Located at offset gff_header.field_indices_offset from start of file.
      Note: Position-based reading handled by application code due to Kaitai Struct limitations.
  
  - id: list_indices
    type: list_indices_array
    if: gff_header.list_indices_count > 0
    doc: |
      List indices array for LIST type fields (used for PropertiesList).
      Located at offset gff_header.list_indices_offset from start of file.
      Note: Position-based reading handled by application code due to Kaitai Struct limitations.

types:
  # GFF Header (56 bytes)
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "UTI " (space-padded) for item template files.
          Other GFF types: "GFF ", "DLG ", "ARE ", "UTC ", "UTD ", "UTM ", "GIT ", etc.
          Validation handled by application code.
      
      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File format version. Valid values: "V3.2" (KotOR), "V3.3", "V4.0", "V4.1" (other BioWare games).
      
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
          Common UTI field names: "TemplateResRef", "LocalizedName", "Description", "DescIdentified",
          "Tag", "Comment", "BaseItem", "Cost", "AddCost", "Plot", "Charges", "StackSize",
          "ModelVariation", "BodyVariation", "TextureVar", "PaletteID", "Identified", "Stolen",
          "PropertiesList", "Upgradable" (KotOR1), "UpgradeLevel" (KotOR2), "WeaponColor" (KotOR2),
          "WeaponWhoosh" (KotOR2), "ArmorRulesType" (KotOR2).
          Note: Trailing nulls should be trimmed by application code.
  
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
          UTI-specific struct IDs:
          - 0xFFFFFFFF (-1): Root struct containing item metadata
          - PropertiesList entries: Each property in PropertiesList is a struct (typically struct_id = 0)
      
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
        enum: gff_field_type
        doc: |
          Field data type (see gff_field_type enum):
          0 = Byte (UInt8) - Used for: Plot, Charges, ModelVariation, BodyVariation, TextureVar,
              PaletteID, Upgradable (KotOR1), UpgradeLevel (KotOR2), WeaponColor (KotOR2),
              WeaponWhoosh (KotOR2), ArmorRulesType (KotOR2), CostTable, Param1, Param1Value,
              ChanceAppear, UpgradeType (KotOR2)
          1 = Char (Int8)
          2 = UInt16 - Used for: StackSize, PropertyName, Subtype, CostValue
          3 = Int16
          4 = UInt32 - Used for: Cost, AddCost
          5 = Int32 - Used for: BaseItem, Identified, Stolen
          6 = UInt64
          7 = Int64
          8 = Single (Float32)
          9 = Double (Float64)
          10 = CExoString (String) - Used for: Tag, Comment
          11 = ResRef - Used for: TemplateResRef
          12 = CExoLocString (LocalizedString) - Used for: LocalizedName, Description, DescIdentified
          13 = Void (Binary)
          14 = Struct
          15 = List - Used for: PropertiesList
          16 = Vector4
          17 = Vector3
      
      - id: label_index
        type: u4
        doc: |
          Index into label_array for field name.
          The label_array[label_index].name_trimmed gives the field name.
      
      - id: data_or_offset
        type: u4
        doc: |
          For simple types (Byte, Char, UInt16, Int16, UInt32, Int32, UInt64, Int64, Single, Double):
            Inline data value (stored directly in this field)
          For complex types (String, ResRef, LocalizedString, Binary, Vector3, Vector4):
            Byte offset into field_data section (relative to field_data_offset)
          For Struct type:
            Struct index into struct_array
          For List type:
            Byte offset into list_indices array (relative to list_indices_offset)
    instances:
      field_data_offset_value:
        value: _root.gff_header.field_data_offset + data_or_offset
        doc: |
          Absolute file offset to field data for complex types.
          Note: Field type checking (simple vs complex vs struct vs list) handled by application code.
      struct_index_value:
        value: data_or_offset
        doc: |
          Struct index for struct type fields (when field_type is struct/14).
          Note: Field type checking handled by application code.
      list_indices_offset_value:
        value: _root.gff_header.list_indices_offset + data_or_offset
        doc: |
          Absolute file offset to list indices for list type fields (when field_type is list/15).
          Note: Field type checking handled by application code. Used for PropertiesList.
  
  # Field Data Section
  field_data_section:
    seq:
      - id: data
        type: str
        encoding: UTF-8
        size: _root.gff_header.field_data_count
        doc: |
          Raw field data bytes for complex types. Individual field data entries are accessed via
          field_entry.field_data_offset_value offsets. The structure of each entry depends on the field_type:
          - UInt64/Int64/Double: 8 bytes (stored in field_data section)
          - String (CExoString): 4-byte length (u4) + string bytes (ASCII, null-terminated)
          - ResRef: 1-byte length (u1, max 16) + string bytes (ASCII, null-padded to 16 bytes total)
          - LocalizedString (CExoLocString): variable (see localized_string_data type)
          - Binary (Void): 4-byte length (u4) + binary bytes
          - Vector3: 12 bytes (3×f4, little-endian: X, Y, Z)
          - Vector4: 16 bytes (4×f4, little-endian: W, X, Y, Z)
  
  # Helper types for parsing field_data section entries (documentation only)
  # Note: These types are conceptual - actual parsing accesses field_data via offsets
  
  resref_data:
    seq:
      - id: length
        type: u1
        doc: |
          Length of ResRef string (0-16). ResRef strings are limited to 16 characters.
          Common UTI ResRefs: item template names (e.g., "w_sword001", "g_w_lghtsbr01").
      
      - id: name
        type: str
        size: 16
        encoding: ASCII
        doc: |
          ResRef string (null-padded to 16 bytes).
          The actual string length is given by the length field above.
          Trailing null bytes should be ignored. To extract the actual ResRef name,
          take the first 'length' characters from 'name' (trim trailing nulls).
          Note: This is a documentation type - actual parsing accesses field_data via offsets.
  
  string_data:
    seq:
      - id: length
        type: u4
        doc: Length of string data in bytes (not including null terminator)
      
      - id: data
        type: str
        size: length
        encoding: ASCII
        doc: |
          String data (ASCII encoded, null-terminated).
          Trailing null bytes should be trimmed.
          Common UTI strings: Tag (item tag for scripting), Comment (developer notes).
          Note: To extract the actual string value, trim trailing null bytes from 'data' in application code.
  
  # Field Indices Array (MultiMap)
  field_indices_array:
    seq:
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.gff_header.field_indices_count
        doc: |
          Array of field indices. When a struct has multiple fields, it stores an offset
          into this array, and the next N consecutive u4 values (where N = struct.field_count)
          are the field indices for that struct.
          Example: Root struct with 20+ fields stores offset to this array, then 20+ consecutive
          u4 values here are the field indices for TemplateResRef, LocalizedName, Description,
          DescIdentified, Tag, Comment, BaseItem, Cost, AddCost, Plot, Charges, StackSize, etc.
  
  # List Indices Array (used for PropertiesList)
  list_indices_array:
    seq:
      - id: raw_data
        type: str
        encoding: UTF-8
        size: _root.gff_header.list_indices_count
        doc: |
          Raw list indices data. List entries are accessed via offsets stored in
          list-type field entries (e.g., PropertiesList field). Each entry starts with a count (u4),
          followed by that many struct indices (u4 each).
          For PropertiesList: count = number of properties, then that many struct indices pointing
          to property structs in struct_array. Each property struct contains PropertyName, Subtype,
          CostTable, CostValue, Param1, Param1Value, ChanceAppear, UpgradeType (optional).
      
      - id: entries
        type: list_entry
        repeat: until
        repeat-until: _io.pos >= (_root.gff_header.list_indices_offset + _root.gff_header.list_indices_count)
        doc: |
          Array of list entries. In practice, list entries are accessed via offsets
          stored in list-type field entries (field_entry.list_indices_offset_value),
          not as a sequential array. This is a simplified representation for documentation.
          For PropertiesList: typically contains one list_entry with count = number of properties.
  
  list_entry:
    seq:
      - id: count
        type: u4
        doc: |
          Number of struct indices in this list.
          For PropertiesList: count = number of item properties.
      
      - id: struct_indices
        type: u4
        repeat: expr
        repeat-expr: count
        doc: |
          Array of struct indices (indices into struct_array).
          For PropertiesList: each struct index points to a property struct containing
          PropertyName, Subtype, CostTable, CostValue, Param1, Param1Value, ChanceAppear, UpgradeType.
  
  # Property struct (documentation only - actual parsing accesses via struct_array)
  # Each property in PropertiesList is a struct in struct_array with fields:
  property_struct:
    doc: |
      Property struct fields (accessed via struct_array entry):
      - PropertyName (UInt16): Index into itempropdef.2da, property type identifier
      - Subtype (UInt16): Property subtype/category
      - CostTable (UInt8): Cost table index
      - CostValue (UInt16): Cost value
      - Param1 (UInt8): First parameter
      - Param1Value (UInt8): First parameter value
      - ChanceAppear (UInt8): Percentage chance to appear (0-100, for random loot)
      - UpgradeType (UInt8, optional, KotOR2 only): Upgrade slot type
    seq:
      - id: property_name
        type: u2
        doc: Index into itempropdef.2da (UInt16, property type identifier)
      
      - id: subtype
        type: u2
        doc: Property subtype/category (UInt16)
      
      - id: cost_table
        type: u1
        doc: Cost table index (UInt8)
      
      - id: cost_value
        type: u2
        doc: Cost value (UInt16)
      
      - id: param1
        type: u1
        doc: First parameter (UInt8)
      
      - id: param1_value
        type: u1
        doc: First parameter value (UInt8)
      
      - id: chance_appear
        type: u1
        doc: Percentage chance to appear (UInt8, 0-100, for random loot)
      
      - id: upgrade_type
        type: u1
        doc: Upgrade type (UInt8, optional, KotOR2 only, upgrade slot type)
  
  # Complex field data types (used when accessing field_data section)
  
  localized_string_data:
    seq:
      - id: total_size
        type: u4
        doc: |
          Total size of this LocalizedString structure in bytes (not including this count).
          Used for skipping over the structure, but can be calculated from the data.
          Format: total_size = 4 (string_ref) + 4 (string_count) + sum(substring sizes)
          Each substring: 4 (string_id) + 4 (string_length) + string_length (string_data)
      
      - id: string_ref
        type: u4
        doc: |
          String reference ID (StrRef) into dialog.tlk file.
          Value 0xFFFFFFFF indicates no string reference (-1).
          If string_ref is valid (not -1), this is the primary string shown to the user.
          Language-specific substrings override this for specific languages/genders.
          For UTI, LocalizedName, Description, and DescIdentified use this to reference dialog.tlk entries.
      
      - id: string_count
        type: u4
        doc: |
          Number of language-specific string substrings.
          Typically 0 if only using string_ref, or 1-10+ for multi-language support.
      
      - id: substrings
        type: localized_substring
        repeat: expr
        repeat-expr: string_count
        doc: |
          Array of language-specific string substrings.
          Each substring provides text for a specific language and gender combination.
          Used to override or supplement the string_ref text.
    instances:
      string_ref_value:
        value: 'string_ref == 0xFFFFFFFF ? -1 : string_ref'
        doc: "String reference as signed integer (-1 if none)"
  
  localized_substring:
    seq:
      - id: string_id
        type: u4
        doc: |
          String ID encoding language and gender:
          - Bits 0-7: Gender (0 = Male, 1 = Female)
          - Bits 8-15: Language ID (see Language enum)
          - Bits 16-31: Reserved/unused
          Common languages: 0 = English, 1 = French, 2 = German, 3 = Italian, 4 = Spanish,
          5 = Polish, 6 = Korean, 7 = Chinese (Traditional), 8 = Chinese (Simplified), 9 = Japanese
      
      - id: string_length
        type: u4
        doc: Length of string data in bytes (UTF-8 encoded)
      
      - id: string_data
        type: str
        size: string_length
        encoding: UTF-8
        doc: |
          String data (encoding depends on language, but UTF-8 is common).
          Trailing null bytes should be trimmed.
          For UTI LocalizedName, Description, and DescIdentified, this contains the item name/description
          in the specified language/gender.
    instances:
      language_id:
        value: (string_id >> 8) & 0xFF
        doc: "Language ID (extracted from string_id, bits 8-15)"
      gender_id:
        value: string_id & 0xFF
        doc: "Gender ID (extracted from string_id, bits 0-7: 0 = Male, 1 = Female)"

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
