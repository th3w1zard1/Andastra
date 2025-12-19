meta:
  id: are
  title: BioWare ARE (Area) File Format
  license: MIT
  endian: le
  file-extension: are
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/are.py
    reone: vendor/reone/src/libs/resource/parser/gff/are.cpp
    xoreos: vendor/xoreos/src/aurora/arefile.cpp
    wiki: vendor/PyKotor/wiki/GFF-File-Format.md
doc: |
  ARE (Area) files are GFF-based format files that store static area information including
  lighting, fog, grass, weather, script hooks, and map data. ARE files use the GFF (Generic File Format)
  binary structure with file type signature "ARE ".
  
  ARE files contain:
  - Root struct with area properties (Tag, Name, AlphaTest, CameraStyle, DefaultEnvMap, etc.)
  - Map struct: Nested struct containing map data (NorthAxis, MapZoom, MapResX, MapPt1X/Y, MapPt2X/Y, WorldPt1X/Y, WorldPt2X/Y)
  - Lighting properties: SunAmbientColor, SunDiffuseColor, DynAmbientColor, SunFogColor
  - Grass properties: Grass_TexName, Grass_Density, Grass_QuadSize, Grass_Prob_LL/LR/UL/UR, Grass_Ambient, Grass_Diffuse, Grass_Emissive (KotOR 2 only)
  - Fog properties: SunFogOn, SunFogNear, SunFogFar
  - Weather properties: WindPower, ShadowOpacity, ChancesOfRain/Snow/Lightning/Fog (KotOR 2)
  - Script hooks: OnEnter, OnExit, OnHeartbeat, OnUserDefined (and OnEnter2, OnExit2, OnHeartbeat2, OnUserDefined2)
  - Lists: Rooms, AreaList, MapList
  - KotOR 2-specific fields: DirtyFormulaOne/Two/Thre, Grass_Emissive
  - Aurora/NWN-specific fields: EnvAudio, DisplayName, MoonFogColor, MoonFogAmount, MoonAmbientColor, MoonDiffuseColor
  
  References:
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - src/Andastra/Parsing/Resource/Formats/GFF/Generics/ARE.cs
  - src/Andastra/Parsing/Resource/Formats/GFF/Generics/AREHelpers.cs

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
          File type signature. Must be "ARE " for area files.
          Other GFF types: "GFF ", "DLG ", "UTC ", "UTI ", etc.
        valid: "ARE "
      
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
          ARE files typically have root struct (struct_id = -1) and Map struct (struct_id varies).
      
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
          0 = Byte (UInt8) - used for boolean flags like Unescapable, DisableTransit, StealthXPEnabled, SunFogOn
          1 = Char (Int8)
          2 = UInt16 - used for LoadScreenID
          3 = Int16
          4 = UInt32 - used for color values (SunAmbientColor, Grass_Ambient, etc.)
          5 = Int32 - used for AlphaTest, CameraStyle, WindPower, StealthXPLoss/Max, DirtyFormulaOne/Two/Thre
          6 = UInt64
          7 = Int64
          8 = Single (Float32) - used for fog distances, grass properties, map coordinates
          9 = Double (Float64)
          10 = CExoString (String) - used for Tag, Comments
          11 = ResRef - used for DefaultEnvMap, Grass_TexName, script hooks (OnEnter, OnExit, etc.)
          12 = CExoLocString (LocalizedString) - used for Name, DisplayName (Aurora)
          13 = Void (Binary)
          14 = Struct - used for Map struct
          15 = List - used for Rooms, AreaList, MapList
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


