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
  UTI (Item Template) files are GFF-based format files that define item templates for all objects
  in creature inventories, containers, and stores. Items range from weapons and armor to quest items,
  upgrades, and consumables. UTI files use the GFF (Generic File Format) binary structure with
  file type signature "UTI ".
  
  UTI files contain:
  - Root struct with item metadata:
    - TemplateResRef: Item template ResRef (unique identifier, max 16 characters, ResRef type)
    - LocalizedName: Localized item name (LocalizedString/CExoLocString type)
    - Description: Generic description (LocalizedString/CExoLocString type, shown when unidentified)
    - DescIdentified: Description when identified (LocalizedString/CExoLocString type)
    - Tag: Item tag identifier (String/CExoString type, used for scripting references)
    - Comment: Developer comment string (String/CExoString type, not used by game engine)
    - BaseItem: Base item type index (Int32, index into baseitems.2da, defines item type: weapon, armor, quest item, etc.)
    - Cost: Base value in credits (UInt32, base item cost)
    - AddCost: Additional cost from properties (UInt32, cost added by item properties/enchantments)
    - Plot: Plot-critical item flag (UInt8/Byte, 0 = normal item, 1 = cannot be sold/destroyed)
    - Charges: Number of uses remaining (UInt8/Byte, 0 = unlimited/unused)
    - StackSize: Current stack quantity (UInt16, number of items in stack, 1 = single item)
    - ModelVariation: Model variation index (UInt8/Byte, 1-99, determines which model variant to use)
    - BodyVariation: Body variation for armor (UInt8/Byte, 1-9, armor body model variant)
    - TextureVar: Texture variation for armor (UInt8/Byte, 1-9, armor texture variant)
    - PaletteID: Toolset palette category (UInt8/Byte, editor organization, doesn't affect gameplay)
    - Identified: Item identification status (Int32, 0 = unidentified, 1 = identified)
    - Stolen: Stolen item flag (Int32, 0 = not stolen, 1 = stolen)
    - UpgradeLevel: Current upgrade tier (UInt8/Byte, KotOR2 only, 0-2)
    - Upgradable: Can accept upgrades (UInt8/Byte, KotOR1 only, 0 = no, 1 = yes)
    - WeaponColor: Blade color for lightsabers (UInt8/Byte, KotOR2 only, 0-10)
    - WeaponWhoosh: Whoosh sound type (UInt8/Byte, KotOR2 only)
    - ArmorRulesType: Armor class category (UInt8/Byte, KotOR2 only)
    - Cursed: Cannot be unequipped flag (UInt8/Byte, 0 = normal, 1 = cursed item, deprecated in some versions)
  - PropertiesList: Array of UTI_PropertiesList structs containing item properties and enchantments (List type)
    Each property struct contains:
    - PropertyName: Property index (UInt16, index into itempropdef.2da)
    - Subtype: Property subtype/category (UInt16, property subtype identifier)
    - CostTable: Cost table index (UInt8/Byte, index into cost tables for property pricing)
    - CostValue: Cost value (UInt16, property cost multiplier/value)
    - Param1: First parameter (UInt8/Byte, property-specific parameter)
    - Param1Value: First parameter value (UInt8/Byte, value for Param1)
    - ChanceAppear: Percentage chance to appear (UInt8/Byte, 0-100, for random loot generation)
    - UsesPerDay: Daily usage limit (UInt8/Byte, 0 = unlimited, optional/deprecated field)
    - UsesLeft: Remaining uses for today (UInt8/Byte, optional/deprecated field)
    - UpgradeType: Upgrade type (UInt8/Byte, optional, KotOR2 upgrade slot type)
  
  BaseItem types (from baseitems.2da):
  - 0-10: Various weapon types (shortsword, longsword, blaster, etc.)
  - 11-30: Armor types and shields
  - 31-50: Quest items, grenades, medical supplies
  - 51-70: Upgrades, armbands, belts
  - 71-90: Droid equipment, special items
  - 91+: KotOR2-specific items
  
  Common Item Properties:
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
  
  Lightsaber colors (KotOR2 WeaponColor):
  - 0: Blue, 1: Yellow, 2: Green, 3: Red
  - 4: Violet, 5: Orange, 6: Cyan, 7: Silver
  - 8: White, 9: Viridian, 10: Bronze
  
  References:
  - vendor/PyKotor/wiki/GFF-UTI.md
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/uti.py:16-280
  - vendor/reone/src/libs/resource/parser/gff/uti.cpp

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
  # GFF Header (56 bytes)
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "UTI " for item template files.
          Other GFF types: "GFF ", "DLG ", "ARE ", "UTC ", "UTD ", "UTM ", "GIT ", etc.
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
          "UpgradeLevel", "Upgradable", "WeaponColor", "WeaponWhoosh", "ArmorRulesType", "Cursed",
          "PropertiesList", "PropertyName", "Subtype", "CostTable", "CostValue", "Param1",
          "Param1Value", "ChanceAppear", "UsesPerDay", "UsesLeft", "UpgradeType".
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
          UTI-specific struct IDs:
          - 0xFFFFFFFF (-1): Root struct containing item metadata
          - Typically 0 or custom IDs: PropertiesList entry structs (UTI_PropertiesList)
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
        enum: gff_field_type
        doc: |
          Field data type (see gff_field_type enum):
          0 = Byte (UInt8) - Used for: Plot, Charges, ModelVariation, BodyVariation, TextureVar,
              PaletteID, UpgradeLevel, Upgradable, WeaponColor, WeaponWhoosh, ArmorRulesType,
              CostTable, Param1, Param1Value, ChanceAppear, UpgradeType
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
      is_simple_type:
        value: field_type.value >= 0 && field_type.value <= 5 || field_type.value == 8
        doc: "True if field stores data inline (simple types: Byte, Char, UInt16, Int16, UInt32, Int32, Float)"
      is_complex_type:
        value: field_type.value >= 6 && field_type.value <= 13 || field_type.value >= 16 && field_type.value <= 17
        doc: "True if field stores data in field_data section (complex types: UInt64, Int64, Double, String, ResRef, LocalizedString, Binary, Vector3, Vector4)"
      is_struct_type:
        value: field_type.value == 14
        doc: True if field is a nested struct
      is_list_type:
        value: field_type.value == 15
        doc: True if field is a list of structs (e.g., PropertiesList)
      field_data_offset_value:
        value: _root.gff_header.field_data_offset + data_or_offset
        if: is_complex_type
        doc: Absolute file offset to field data for complex types
      struct_index_value:
        value: data_or_offset
        if: is_struct_type
        doc: Struct index for struct type fields
      list_indices_offset_value:
        value: _root.gff_header.list_indices_offset + data_or_offset
        if: is_list_type
        doc: Absolute file offset to list indices for list type fields (e.g., PropertiesList)
  
  # Field Data Section
  field_data_section:
    seq:
      - id: data
        type: str
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
          Common UTI ResRefs: item template names (e.g., "g_w_lghtsbr001", "g_a_robe01"),
          these reference the actual item templates used by the game.
      
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
          Note: To extract the actual string value, trim trailing null bytes from 'data'.
    instances:
      data_trimmed:
        value: data.rstrip('\x00')
        doc: "String data with trailing nulls removed"
  
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
          Example: Root struct with 20 fields stores offset to this array, then 20 consecutive
          u4 values here are the field indices for TemplateResRef, LocalizedName, Description,
          DescIdentified, Tag, Comment, BaseItem, Cost, AddCost, Plot, Charges, StackSize, etc.
  
  # List Indices Array
  list_indices_array:
    seq:
      - id: raw_data
        type: str
        size: _root.gff_header.list_indices_count
        doc: |
          Raw list indices data. List entries are accessed via offsets stored in
          list-type field entries. Each entry starts with a count (u4), followed
          by that many struct indices (u4 each).
          Example: PropertiesList field stores offset to this array. At that offset:
          - First u4: count of properties (number of struct indices)
          - Next N×u4: struct indices (N = count) pointing to PropertiesList entry structs
      
      - id: entries
        type: list_entry
        repeat: until
        repeat-until: _io.pos >= (_root.gff_header.list_indices_offset + _root.gff_header.list_indices_count)
        doc: |
          Array of list entries. In practice, list entries are accessed via offsets
          stored in list-type field entries (field_entry.list_indices_offset_value),
          not as a sequential array. This is a simplified representation for documentation.
          For UTI, the PropertiesList field uses this to store array of PropertiesList entry struct indices.
  
  list_entry:
    seq:
      - id: count
        type: u4
        doc: |
          Number of struct indices in this list.
          For PropertiesList, this is the number of item properties/enchantments.
      
      - id: struct_indices
        type: u4
        repeat: expr
        repeat-expr: count
        doc: |
          Array of struct indices (indices into struct_array).
          For PropertiesList, each struct index points to a PropertiesList entry struct containing
          PropertyName, Subtype, CostTable, CostValue, Param1, Param1Value, ChanceAppear, UpgradeType fields.
  
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
    doc: |
      8-bit unsigned integer (byte).
      Used in UTI for: Plot (plot-critical flag), Charges (uses remaining), ModelVariation (model variant),
      BodyVariation (armor body variant), TextureVar (texture variant), PaletteID (editor category),
      UpgradeLevel (KotOR2 upgrade tier), Upgradable (KotOR1 upgrade flag), WeaponColor (KotOR2 lightsaber color),
      WeaponWhoosh (KotOR2 sound type), ArmorRulesType (KotOR2 armor category), CostTable (property cost table),
      Param1 (property parameter), Param1Value (property parameter value), ChanceAppear (random loot chance),
      UpgradeType (KotOR2 upgrade slot type).
    1: int8
    doc: 8-bit signed integer (char)
    2: uint16
    doc: |
      16-bit unsigned integer (word).
      Used in UTI for: StackSize (stack quantity), PropertyName (property index into itempropdef.2da),
      Subtype (property subtype), CostValue (property cost value).
    3: int16
    doc: 16-bit signed integer (short)
    4: uint32
    doc: |
      32-bit unsigned integer (dword).
      Used in UTI for: Cost (base item cost), AddCost (additional cost from properties).
    5: int32
    doc: |
      32-bit signed integer (int).
      Used in UTI for: BaseItem (base item type index into baseitems.2da), Identified (identification status),
      Stolen (stolen item flag).
    6: uint64
    doc: 64-bit unsigned integer (stored in field_data section)
    7: int64
    doc: 64-bit signed integer (stored in field_data section)
    8: single
    doc: 32-bit floating point (float)
    9: double
    doc: 64-bit floating point (stored in field_data section)
    10: string
    doc: |
      Null-terminated string (CExoString, stored in field_data section).
      Used in UTI for: Tag (item tag identifier), Comment (developer comment).
    11: resref
    doc: |
      Resource reference (ResRef, max 16 chars, stored in field_data section).
      Used in UTI for: TemplateResRef (item template name).
    12: localized_string
    doc: |
      Localized string (CExoLocString, stored in field_data section).
      Used in UTI for: LocalizedName (localized item name with multiple language/gender support),
      Description (generic description shown when unidentified), DescIdentified (description shown when identified).
    13: binary
    doc: Binary data blob (Void, stored in field_data section)
    14: struct
    doc: Nested struct (struct index stored inline)
    15: list
    doc: |
      List of structs (offset to list_indices stored inline).
      Used in UTI for: PropertiesList (array of PropertiesList entry structs containing item properties/enchantments).
    16: vector4
    doc: Quaternion/Orientation (4×float, stored in field_data as Vector4)
    17: vector3
    doc: 3D vector (3×float, stored in field_data)
