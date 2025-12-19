meta:
  id: erf
  title: BioWare ERF (Encapsulated Resource File) Format
  license: MIT
  endian: le
  file-extension:
    - erf
    - mod
    - sav
    - hak
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/erf/
    reone: vendor/reone/src/libs/resource/format/erfreader.cpp
    xoreos: vendor/xoreos/src/aurora/erffile.cpp
    wiki: vendor/PyKotor/wiki/ERF-File-Format.md
doc: |
  ERF (Encapsulated Resource File) files are self-contained archives used for modules,
  save games, texture packs, and hak paks. Unlike BIF files which require a KEY file
  for filename lookups, ERF files store both resource names (ResRefs) and data in the
  same file. They also support localized strings for descriptions in multiple languages.
  
  Format Variants:
  - ERF: Generic encapsulated resource file (texture packs, etc.)
  - MOD: Module file (game areas/levels)
  - SAV: Save game file (contains saved game state)
  - HAK: Hak pak file (contains override resources)
  
  All variants use the same binary format structure, differing only in the file type signature.
  
  Binary Format:
  - Header (160 bytes): File type, version, entry counts, offsets, build date, description
  - Localized String List (optional, variable size): Multi-language descriptions
  - Key List (24 bytes per entry): ResRef to resource index mapping
  - Resource List (8 bytes per entry): Resource offset and size
  - Resource Data (variable size): Raw binary data for each resource
  
  References:
  - vendor/PyKotor/wiki/ERF-File-Format.md
  - vendor/reone/src/libs/resource/format/erfreader.cpp:24-106
  - vendor/xoreos/src/aurora/erffile.cpp:44-229
  - vendor/Kotor.NET/Kotor.NET/Formats/KotorERF/ERFBinaryStructure.cs:11-170

seq:
  - id: header
    type: erf_header
    doc: ERF file header (160 bytes)
  
  - id: localized_string_list
    type: localized_string_list
    if: header.language_count > 0
    pos: header.offset_to_localized_string_list
    doc: Optional localized string entries for multi-language descriptions
  
  - id: key_list
    type: key_list
    pos: header.offset_to_key_list
    doc: Array of key entries mapping ResRefs to resource indices
  
  - id: resource_list
    type: resource_list
    pos: header.offset_to_resource_list
    doc: Array of resource entries containing offset and size information

types:
  erf_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be one of:
          - "ERF " (0x45 0x52 0x46 0x20) - Generic ERF archive
          - "MOD " (0x4D 0x4F 0x44 0x20) - Module file
          - "SAV " (0x53 0x41 0x56 0x20) - Save game file
          - "HAK " (0x48 0x41 0x4B 0x20) - Hak pak file
        valid: ["ERF ", "MOD ", "SAV ", "HAK "]
      
      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File format version. Always "V1.0" for KotOR ERF files.
          Other versions may exist in Neverwinter Nights but are not supported in KotOR.
        valid: "V1.0"
      
      - id: language_count
        type: u4
        doc: |
          Number of localized string entries. Typically 0 for most ERF files.
          MOD files may include localized module names for the load screen.
      
      - id: localized_string_size
        type: u4
        doc: |
          Total size of localized string data in bytes.
          Includes all language entries (language_id + string_size + string_data for each).
      
      - id: entry_count
        type: u4
        doc: |
          Number of resources in the archive. This determines:
          - Number of entries in key_list
          - Number of entries in resource_list
          - Number of resource data blocks stored at various offsets
      
      - id: offset_to_localized_string_list
        type: u4
        doc: |
          Byte offset to the localized string list from the beginning of the file.
          Typically 160 (right after header) if present, or 0 if not present.
      
      - id: offset_to_key_list
        type: u4
        doc: |
          Byte offset to the key list from the beginning of the file.
          Typically 160 (right after header) if no localized strings, or after localized strings.
      
      - id: offset_to_resource_list
        type: u4
        doc: |
          Byte offset to the resource list from the beginning of the file.
          Located after the key list.
      
      - id: build_year
        type: u4
        doc: |
          Build year (years since 1900).
          Example: 103 = year 2003
          Primarily informational, used by development tools to track module versions.
      
      - id: build_day
        type: u4
        doc: |
          Build day (days since January 1, with January 1 = day 1).
          Example: 247 = September 4th (the 247th day of the year)
          Primarily informational, used by development tools to track module versions.
      
      - id: description_strref
        type: s4
        doc: |
          Description StrRef (TLK string reference) for the archive description.
          Values vary by file type:
          - MOD files: -1 (0xFFFFFFFF, uses localized strings instead)
          - SAV files: 0 (typically no description)
          - ERF/HAK files: Unpredictable (may contain valid StrRef or -1)
      
      - id: reserved
        type: str
        size: 116
        doc: |
          Reserved padding (usually zeros).
          Total header size is 160 bytes:
          file_type (4) + file_version (4) + language_count (4) + localized_string_size (4) +
          entry_count (4) + offset_to_localized_string_list (4) + offset_to_key_list (4) +
          offset_to_resource_list (4) + build_year (4) + build_day (4) + description_strref (4) +
          reserved (116) = 160 bytes
    
    instances:
      is_save_file:
        value: file_type == "MOD " && description_strref == 0
        doc: |
          Heuristic to detect save game files.
          Save games use MOD signature but typically have description_strref = 0.
  
  localized_string_list:
    seq:
      - id: entries
        type: localized_string_entry
        repeat: expr
        repeat-expr: _root.header.language_count
        doc: Array of localized string entries, one per language
  
  localized_string_entry:
    seq:
      - id: language_id
        type: u4
        doc: |
          Language identifier:
          - 0 = English
          - 1 = French
          - 2 = German
          - 3 = Italian
          - 4 = Spanish
          - 5 = Polish
          - Additional languages for Asian releases
      
      - id: string_size
        type: u4
        doc: Length of string data in bytes
      
      - id: string_data
        type: str
        size: string_size
        encoding: UTF-8
        doc: UTF-8 encoded text string
  
  key_list:
    seq:
      - id: entries
        type: key_entry
        repeat: expr
        repeat-expr: _root.header.entry_count
        doc: Array of key entries mapping ResRefs to resource indices
  
  key_entry:
    seq:
      - id: resref
        type: str
        encoding: ASCII
        size: 16
        doc: |
          Resource filename (ResRef), null-padded to 16 bytes.
          Maximum 16 characters. If exactly 16 characters, no null terminator exists.
          Resource names can be mixed case, though most are lowercase in practice.
      
      - id: resource_id
        type: u4
        doc: |
          Resource ID (index into resource_list).
          Maps this key entry to the corresponding resource entry.
      
      - id: resource_type
        type: u2
        doc: |
          Resource type identifier (see ResourceType enum).
          Examples: 0x000B (TPC/texture), 0x000A (MOD/module), 0x0000 (RES/unknown)
      
      - id: unused
        type: u2
        doc: Padding/unused field (typically 0)
    
    instances:
      resref_trimmed:
        value: resref.rstrip("\0")
        doc: ResRef with trailing null bytes removed
  
  resource_list:
    seq:
      - id: entries
        type: resource_entry
        repeat: expr
        repeat-expr: _root.header.entry_count
        doc: Array of resource entries containing offset and size information
  
  resource_entry:
    seq:
      - id: offset_to_data
        type: u4
        doc: |
          Byte offset to resource data from the beginning of the file.
          Points to the actual binary data for this resource.
      
      - id: resource_size
        type: u4
        doc: |
          Size of resource data in bytes.
          Uncompressed size of the resource.
    
    instances:
      data:
        pos: offset_to_data
        type: str
        size: resource_size
        doc: Raw binary data for this resource
  

