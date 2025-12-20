meta:
  id: utt
  title: "BioWare UTT (Trigger Template) File Format"
  license: MIT
  endian: le
  file-extension: utt
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/utt.py
    reone: vendor/reone/src/libs/resource/parser/gff/utt.cpp
    wiki: vendor/PyKotor/wiki/GFF-UTT.md
    bioware: vendor/PyKotor/wiki/Bioware-Aurora-Trigger-Format.md
doc: |
  UTT (User Template Trigger) files are GFF-based format files that define trigger blueprints.
  UTT files use the GFF (Generic File Format) binary structure with file type signature "UTT ".
  
  Triggers are invisible objects in the game world that detect when creatures enter or exit their area
  and execute scripts. UTT files define the template/blueprint for triggers, which are then instantiated
  in areas (GIT files) with specific positions and orientations.
  
  UTT files contain:
  - Root struct with trigger metadata:
    - ResRef: Trigger template ResRef (unique identifier, max 16 characters, ResRef type)
    - LocName: Localized trigger name (LocalizedString/CExoLocString type)
    - Tag: Trigger tag identifier (String/CExoString type, used for scripting references)
    - Comment: Developer comment string (String/CExoString type, not used by game engine)
    - Type: Trigger type identifier (UInt32/DWord type)
      - 0 = Unknown/Generic trigger
      - 1 = Trap trigger
      - 2 = Proximity trigger
      - 3 = Clickable trigger
      - 4 = Script trigger
      - Other values: Custom trigger types
    - TrapDetectable: Whether trap is detectable (UInt8/Byte type, 0 = not detectable, 1 = detectable)
    - TrapDetectDC: Trap detection difficulty class (UInt8/Byte type, 0-255)
    - DisarmDC: Disarm difficulty class (UInt8/Byte type, 0-255)
    - DetectTrap: Whether trigger detects traps (UInt8/Byte type, 0 = no, 1 = yes)
    - Faction: Faction identifier (UInt32/DWord type, references faction.2da)
    - Cursor: Cursor type when hovering over trigger (UInt32/DWord type, references cursors.2da)
    - HighlightHeight: Height of highlight box (Single/Float32 type, in game units)
    - KeyName: Key required to unlock trigger (ResRef type, max 16 characters, references key item UTI)
    - TriggerOnClick: Whether trigger activates on click (UInt8/Byte type, 0 = no, 1 = yes)
    - Disarmable: Whether trigger can be disarmed (UInt8/Byte type, 0 = no, 1 = yes)
    - Detectable: Whether trigger is detectable (UInt8/Byte type, 0 = no, 1 = yes)
    - IsTrap: Whether trigger is a trap (UInt8/Byte type, 0 = no, 1 = yes)
    - TrapType: Type of trap if IsTrap is true (UInt32/DWord type)
    - ScriptOnEnter: Script ResRef executed when creature enters trigger area (ResRef type, max 16 characters)
    - ScriptOnExit: Script ResRef executed when creature exits trigger area (ResRef type, max 16 characters)
    - ScriptOnHeartbeat: Script ResRef executed periodically while creature is in trigger area (ResRef type, max 16 characters)
    - ScriptOnUserDefined: Script ResRef executed for user-defined events (ResRef type, max 16 characters)
    - ScriptOnDisarm: Script ResRef executed when trap is disarmed (ResRef type, max 16 characters)
    - ScriptOnTrapTriggered: Script ResRef executed when trap is triggered (ResRef type, max 16 characters)
    - LinkedTo: ResRef of object this trigger is linked to (ResRef type, max 16 characters, typically door or placeable)
    - LinkedToFlags: Flags for linked object behavior (UInt32/DWord type)
    - TransitionDestin: Destination area ResRef for transition triggers (ResRef type, max 16 characters)
    - TransitionDestinTag: Destination object tag for transition triggers (String/CExoString type)
    - LinkedToModule: Module ResRef for transition triggers (ResRef type, max 16 characters, references IFO file)
    - LinkedToWaypoint: Waypoint tag for transition triggers (String/CExoString type)
    - LinkedToStrRef: String reference for transition description (UInt32/DWord type, references dialog.tlk)
    - LinkedToTransition: Transition type (UInt32/DWord type)
    - LinkedToTransitionStrRef: String reference for transition text (UInt32/DWord type, references dialog.tlk)
    - LoadScreenID: Loading screen ID for transitions (UInt32/DWord type, references loadscreens.2da)
    - LoadScreenResRef: Loading screen ResRef for transitions (ResRef type, max 16 characters)
    - LoadScreenTransition: Transition effect type (UInt32/DWord type)
    - LoadScreenFade: Fade effect type (UInt32/DWord type)
    - LoadScreenFadeSpeed: Fade speed (Single/Float32 type)
    - LoadScreenFadeColor: Fade color (Vector3 type, RGB values 0.0-1.0)
    - LoadScreenFadeDelay: Fade delay in seconds (Single/Float32 type)
    - LoadScreenMusic: Music ResRef for transitions (ResRef type, max 16 characters)
    - LoadScreenAmbient: Ambient sound ResRef for transitions (ResRef type, max 16 characters)
    - LoadScreenSound: Sound effect ResRef for transitions (ResRef type, max 16 characters)
    - LoadScreenVoice: Voice ResRef for transitions (ResRef type, max 16 characters)
    - LoadScreenMovie: Movie ResRef for transitions (ResRef type, max 16 characters)
    - LoadScreenCamera: Camera animation ResRef for transitions (ResRef type, max 16 characters)
    - LoadScreenCameraEffect: Camera effect type (UInt32/DWord type)
    - LoadScreenCameraFov: Camera field of view (Single/Float32 type)
    - LoadScreenCameraHeight: Camera height (Single/Float32 type)
    - LoadScreenCameraAngle: Camera angle (UInt32/DWord type)
    - LoadScreenCameraAnim: Camera animation ID (UInt32/DWord type)
    - LoadScreenCameraId: Camera ID (UInt32/DWord type)
    - LoadScreenCameraDelay: Camera delay in seconds (Single/Float32 type)
    - LoadScreenCameraSpeed: Camera speed (Single/Float32 type)
    - LoadScreenCameraShake: Camera shake intensity (Single/Float32 type)
    - LoadScreenCameraShakeDuration: Camera shake duration in seconds (Single/Float32 type)
    - LoadScreenCameraShakeFrequency: Camera shake frequency (Single/Float32 type)
    - LoadScreenCameraShakeAmplitude: Camera shake amplitude (Single/Float32 type)
    - LoadScreenCameraShakeDirection: Camera shake direction (Vector3 type)
    - LoadScreenCameraShakeRandomShakeRandomDelay: Random camera shake random delay in seconds (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomSpeed: Random camera shake random speed (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShake: Random camera shake random shake intensity (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeDuration: Random camera shake random shake duration in seconds (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeFrequency: Random camera shake random shake frequency (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeAmplitude: Random camera shake random shake amplitude (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeDirection: Random camera shake random shake direction (Vector3 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandom: Random camera shake random shake random flag (UInt8/Byte type, 0 = no, 1 = yes)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomAmount: Random camera shake random shake random amount (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomFrequency: Random camera shake random shake random frequency (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomAmplitude: Random camera shake random shake random amplitude (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomDirection: Random camera shake random shake random direction (Vector3 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomDuration: Random camera shake random shake random duration in seconds (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomSpeed: Random camera shake random shake random speed (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomDelay: Random camera shake random shake random delay in seconds (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomId: Random camera shake random shake random ID (UInt32/DWord type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomEffect: Random camera shake random shake random effect (UInt32/DWord type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomFov: Random camera shake random shake random field of view (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomHeight: Random camera shake random shake random height (Single/Float32 type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomAngle: Random camera shake random shake random angle (UInt32/DWord type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomAnim: Random camera shake random shake random animation ID (UInt32/DWord type)
    - LoadScreenCameraShakeRandomShakeRandomShakeRandomCameraId: Random camera shake random shake random camera ID (UInt32/DWord type)
  
  References:
  - vendor/PyKotor/wiki/GFF-UTT.md
  - vendor/PyKotor/wiki/Bioware-Aurora-Trigger-Format.md
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - vendor/reone/src/libs/resource/parser/gff/utt.cpp
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/utt.py

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
    doc: Field indices array (MultiMap) for structs with multiple fields
  
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
          File type signature. Must be "UTT " for trigger template files.
          Other GFF types: "GFF ", "DLG ", "ARE ", "UTC ", "UTI ", "UTD ", "UTM ", "UTW ", "GIT ", etc.
      
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
          Common UTT field names: "ResRef", "LocName", "Tag", "Comment", "Type",
          "TrapDetectable", "TrapDetectDC", "DisarmDC", "DetectTrap", "Faction",
          "Cursor", "HighlightHeight", "KeyName", "TriggerOnClick", "Disarmable",
          "Detectable", "IsTrap", "TrapType", "ScriptOnEnter", "ScriptOnExit",
          "ScriptOnHeartbeat", "ScriptOnUserDefined", "ScriptOnDisarm", "ScriptOnTrapTriggered",
          "LinkedTo", "LinkedToFlags", "TransitionDestin", "TransitionDestinTag",
          "LinkedToModule", "LinkedToWaypoint", "LinkedToStrRef", "LinkedToTransition",
          "LinkedToTransitionStrRef", "LoadScreenID", "LoadScreenResRef", etc.
    instances:
      name_trimmed:
        value: name
        doc: "Label name (trailing nulls should be trimmed by parser)"
  
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
          UTT-specific struct IDs:
          - 0xFFFFFFFF (-1): Root struct containing trigger metadata
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
          Field data type (see gff_field_type enum):
          0 = Byte (UInt8) - Used for: TrapDetectable, TrapDetectDC, DisarmDC, DetectTrap,
              TriggerOnClick, Disarmable, Detectable, IsTrap, LoadScreenCameraShakeRandom, etc.
          1 = Char (Int8)
          2 = UInt16
          3 = Int16
          4 = UInt32 - Used for: Type, Faction, Cursor, TrapType, LinkedToFlags,
              LinkedToTransition, LoadScreenID, LoadScreenTransition, LoadScreenFade,
              LoadScreenCameraEffect, LoadScreenCameraAngle, LoadScreenCameraAnim,
              LoadScreenCameraId, LinkedToStrRef, LinkedToTransitionStrRef, etc.
          5 = Int32
          6 = UInt64
          7 = Int64
          8 = Single (Float32) - Used for: HighlightHeight, LoadScreenFadeSpeed,
              LoadScreenFadeDelay, LoadScreenCameraFov, LoadScreenCameraHeight,
              LoadScreenCameraDelay, LoadScreenCameraSpeed, LoadScreenCameraShake, etc.
          9 = Double (Float64)
          10 = CExoString (String) - Used for: Tag, Comment, TransitionDestinTag,
               LinkedToWaypoint
          11 = ResRef - Used for: ResRef, KeyName, ScriptOnEnter, ScriptOnExit,
               ScriptOnHeartbeat, ScriptOnUserDefined, ScriptOnDisarm, ScriptOnTrapTriggered,
               LinkedTo, TransitionDestin, LinkedToModule, LoadScreenResRef,
               LoadScreenMusic, LoadScreenAmbient, LoadScreenSound, LoadScreenVoice,
               LoadScreenMovie, LoadScreenCamera, etc.
          12 = CExoLocString (LocalizedString) - Used for: LocName
          13 = Void (Binary)
          14 = Struct
          15 = List
          16 = Vector4
          17 = Vector3 - Used for: LoadScreenFadeColor, LoadScreenCameraShakeDirection, etc.
      
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
        value: (field_type >= 0 and field_type <= 5) or field_type == 8
        doc: "True if field stores data inline (simple types: Byte, Char, UInt16, Int16, UInt32, Int32, Float)"
      is_complex_type:
        value: (field_type >= 6 and field_type <= 13) or (field_type >= 16 and field_type <= 17)
        doc: "True if field stores data in field_data section (complex types: UInt64, Int64, Double, String, ResRef, LocalizedString, Binary, Vector3, Vector4)"
      is_struct_type:
        value: field_type == 14
        doc: True if field is a nested struct
      is_list_type:
        value: field_type == 15
        doc: True if field is a list of structs
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
        doc: Absolute file offset to list indices for list type fields
  
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
          Common UTT ResRefs: trigger template names, script names (NSS/NCS files),
          area ResRefs (ARE files), module ResRefs (IFO files), waypoint tags,
          key item ResRefs (UTI files), etc.
      
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
          Common UTT strings: Tag (trigger tag for scripting), Comment (developer notes),
          TransitionDestinTag (destination object tag), LinkedToWaypoint (waypoint tag).
          Note: To extract the actual string value, trim trailing null bytes from 'data'.
    instances:
      data_trimmed:
        value: data
        doc: "String data (trailing nulls should be trimmed by parser)"
  
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
          Example: Root struct with 50+ fields stores offset to this array, then 50+ consecutive
          u4 values here are the field indices for ResRef, LocName, Tag, Type, etc.
  
  # List Indices Array
  list_indices_array:
    seq:
      - id: raw_data
        type: str
        encoding: UTF-8
        size: _root.gff_header.list_indices_count
        doc: |
          Raw list indices data. List entries are accessed via offsets stored in
          list-type field entries. Each entry starts with a count (u4), followed
          by that many struct indices (u4 each).
          Note: UTT files typically do not use LIST fields, but the structure is defined
          for completeness and potential future use.
  
  # Complex field data types (used when accessing field_data section)
  
  localized_string_data:
    seq:
      - id: total_size
        type: u4
        doc: |
          Total size of this LocalizedString structure in bytes (not including this count).
          Used for skipping over the structure, but can be calculated from the data.
          Format: total_size = 4 (string_ref) + 4 (num_substrings) + sum(substring sizes)
          Each substring: 4 (string_id) + 4 (string_length) + string_length (string_data)
      
      - id: string_ref
        type: u4
        doc: |
          String reference ID (StrRef) into dialog.tlk file.
          Value 0xFFFFFFFF indicates no string reference (-1).
          If string_ref is valid (not -1), this is the primary string shown to the user.
          Language-specific substrings override this for specific languages/genders.
          For UTT, LocName uses this to reference dialog.tlk entries for trigger names.
      
      - id: num_substrings
        type: u4
        doc: |
          Number of language-specific string substrings.
          Typically 0 if only using string_ref, or 1-10+ for multi-language support.
      
      - id: substrings
        type: localized_substring
        repeat: expr
        repeat-expr: num_substrings
        doc: |
          Array of language-specific string substrings.
          Each substring provides text for a specific language and gender combination.
          Used to override or supplement the string_ref text.
    instances:
      string_ref_value:
        value: 'string_ref == 0xFFFFFFFF ? -1 : string_ref'
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
          For UTT LocName, this contains the trigger name in the specified language/gender.
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
