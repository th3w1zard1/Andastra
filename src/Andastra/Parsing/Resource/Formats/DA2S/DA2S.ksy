meta:
  id: da2s
  title: BioWare DA2S (Dragon Age 2 Save) File Format
  license: MIT
  endian: le
  file-extension: da2s
  xref:
    runtime: src/Andastra/Runtime/Games/Eclipse/DragonAge2/Save/DragonAge2SaveSerializer.cs
doc: |
  DA2S (Dragon Age 2 Save) files are binary save game files used by the Eclipse Engine
  (Dragon Age 2). They contain save game metadata and optionally full game state.
  
  DA2S files are binary format files with the following structure:
  - Signature (4 bytes): "DA2S" (Dragon Age 2 Save)
  - Version (int32): Save format version (1 for DA2)
  - Metadata fields (strings, integers, timestamps, etc.)
  - Optional: Full game state (party, inventory, journal, globals)
  
  Based on DragonAge2.exe: SaveGameMessage @ 0x00be37a8, DeleteSaveGameMessage @ 0x00be389c
  Located via string references: "SaveGameMessage" @ 0x00be37a8, "GameModeController::HandleMessage(SaveGameMessage)" @ 0x00d2b330
  Original implementation: UnrealScript message-based save system, binary serialization
  Note: DA2 save format may differ from DA:O format (different game engine version)
  
  References:
  - src/Andastra/Runtime/Games/Eclipse/DragonAge2/Save/DragonAge2SaveSerializer.cs
  - src/Andastra/Runtime/Games/Eclipse/Save/EclipseSaveSerializer.cs (base class)

seq:
  - id: signature
    type: str
    encoding: ASCII
    size: 4
    doc: |
      File signature. Must be "DA2S" for Dragon Age 2 save files.
    valid: "DA2S"
  
  - id: version
    type: s4
    doc: |
      Save format version. Must be 1 for Dragon Age 2.
    valid: 1
  
  - id: save_name
    type: length_prefixed_string
    doc: User-entered save name displayed in UI
  
  - id: module_name
    type: length_prefixed_string
    doc: Current module resource name
  
  - id: area_name
    type: length_prefixed_string
    doc: Current area name for display
  
  - id: time_played_seconds
    type: s4
    doc: Total play time in seconds
  
  - id: timestamp_filetime
    type: s8
    doc: |
      Save creation timestamp as Windows FILETIME (int64).
      Convert using DateTime.FromFileTime().
  
  - id: screenshot_length
    type: s4
    doc: Length of screenshot data in bytes (0 if no screenshot)
  
  - id: screenshot_data
    type: u1
    repeat: expr
    repeat-expr: screenshot_length
    if: screenshot_length > 0
    doc: Screenshot image data (typically TGA or DDS format)
  
  - id: portrait_length
    type: s4
    doc: Length of portrait data in bytes (0 if no portrait)
  
  - id: portrait_data
    type: u1
    repeat: expr
    repeat-expr: portrait_length
    if: portrait_length > 0
    doc: Portrait image data (typically TGA or DDS format)
  
  - id: player_name
    type: length_prefixed_string
    doc: Player character name
  
  - id: party_member_count
    type: s4
    doc: Number of party members (from PartyState)
  
  - id: player_level
    type: s4
    doc: Player character level (from PartyState.PlayerCharacter)

types:
  length_prefixed_string:
    seq:
      - id: length
        type: s4
        doc: |
          String length in bytes (UTF-8 encoding).
          Must be >= 0 and <= 65536 (sanity check).
      
      - id: value
        type: str
        encoding: UTF-8
        size: length
        doc: String value (UTF-8 encoded)
    instances:
      value_trimmed:
        value: value.rstrip('\x00')
        doc: "String value with trailing nulls removed (if any)"

