meta:
  id: lyt
  title: BioWare LYT (Layout) File Format
  license: MIT
  endian: le
  encoding: ASCII
  file-extension: lyt
  ks-version: 0.11
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/lyt/
    reone: vendor/reone/src/libs/resource/format/lytreader.cpp
    xoreos: vendor/xoreos/src/aurora/lytfile.cpp
    kotor_js: vendor/KotOR.js/src/resource/LYTObject.ts
    kotor_unity: vendor/KotOR-Unity/Assets/Scripts/FileObjects/LYTObject.cs
    kotor_net: vendor/Kotor.NET/Kotor.NET/Formats/KotorLYT/LYT.cs
    wiki: vendor/PyKotor/wiki/LYT-File-Format.md
doc: |
  LYT (Layout) files define how area geometry is assembled from room models and where
  interactive elements (doors, tracks, obstacles, art placeables) are positioned.
  The game engine uses LYT files to load and position room models (MDL files) and
  determine door placement points for area transitions.

  Format Overview:
  - LYT files are ASCII text with a deterministic order
  - Structure: beginlayout, optional sections, then donelayout
  - Every section declares a count and then lists entries on subsequent lines
  - Sections (in order): roomcount, trackcount, obstaclecount, doorhookcount, artplaceablecount, walkmeshRooms
  - All sections are optional but must appear in order if present
  - Line endings: \n (Unix) or \r\n (Windows)
  - Whitespace: Leading/trailing whitespace is typically ignored, tokens separated by space/tab
  - Case sensitivity: Room names, door names, and model ResRefs are case-insensitive

  Complete Syntax:
  ```
  beginlayout
  roomcount <N>
    <room_model> <x> <y> <z>
  trackcount <N>
    <track_model> <x> <y> <z>
  obstaclecount <N>
    <obstacle_model> <x> <y> <z>
  doorhookcount <N>
    <room_name> <door_name> [0] <x> <y> <z> <qx> <qy> <qz> <qw> [optional floats]
  artplaceablecount <N>
    <artplaceable_model> <x> <y> <z>
  walkmeshRooms <N>
    <room_model>
  donelayout
  ```

  Coordinate System:
  - Units are meters in the same left-handed coordinate system as MDL models
  - Positions (x, y, z) are world-space coordinates
  - Quaternions (qx, qy, qz, qw) define door orientation in world space
  - Y-axis is typically vertical (up), X and Z form the horizontal plane

  Room Definitions (roomcount):
  - Each room entry specifies a room model (ResRef, max 16 chars, no spaces) and position
  - Room models reference MDL/MDX files that define the room geometry
  - Room names are case-insensitive for matching purposes
  - Format: <room_model> <x> <y> <z>
  - Example: "M17mg_01a 100.0 100.0 0.0"
  - Rooms are the primary building blocks of area layouts
  - Multiple rooms can be placed to create complex area geometry

  Track Definitions (trackcount):
  - Tracks are booster elements used exclusively in swoop racing mini-games (KotOR II)
  - Each track entry specifies a track model (ResRef, max 16 chars, no spaces) and position
  - Format: <track_model> <x> <y> <z>
  - Example: "M17mg_MGT01 0.0 0.0 0.0"
  - Optional section - most modules omit this entirely
  - Used in racing modules like Telos surface racing
  - Each track element represents a booster that provides speed boosts during racing

  Obstacle Definitions (obstaclecount):
  - Obstacles are hazard elements used exclusively in swoop racing mini-games (KotOR II)
  - Each obstacle entry specifies an obstacle model (ResRef, max 16 chars, no spaces) and position
  - Format: <obstacle_model> <x> <y> <z>
  - Example: "M17mg_MGO01 103.309 3691.61 0.0"
  - Optional section - most modules omit this entirely
  - Used in racing modules to create challenges and obstacles for the player
  - Each obstacle element represents a hazard placed along the racing track

  Door Hook Definitions (doorhookcount):
  - Door hooks bind door models to rooms for area transitions
  - Format varies by implementation:
    * PyKotor format: <room_name> <door_name> 0 <x> <y> <z> <qx> <qy> <qz> <qw>
    * xoreos format: <room_name> <door_name> <x> <y> <z> <qx> <qy> <qz> <qw> <unk1> <unk2>
  - Fields:
    - room_name: Target room name (must match a roomcount entry, case-insensitive)
    - door_name: Hook identifier (used in module files to reference this door, case-insensitive)
    - 0: Reserved field (PyKotor format only, always 0, skipped during parsing)
    - x, y, z: Position of door origin in world space (float, meters)
    - qx, qy, qz, qw: Quaternion orientation for door rotation (float)
    - unk1, unk2: Unknown float values (xoreos format only, typically 0.0)
  - Example (PyKotor): "M02ac_02h door_01 0 170.475 66.375 0.0 0.707107 0.0 0.0 -0.707107"
  - Example (xoreos): "Room01 Door01 10.0 11.0 12.0 13.0 14.0 15.0 16.0 17.0"
  - Door hooks define where doors are placed in rooms to create area transitions
  - The quaternion (qx, qy, qz, qw) defines the door's orientation in world space
  - Door hooks are separate from BWM hooks - BWM hooks define interaction points,
    while LYT doorhooks define door placement positions

  Art Placeable Definitions (artplaceablecount):
  - Art placeables are decorative elements that can be placed in areas
  - Format: <artplaceable_model> <x> <y> <z>
  - Each entry specifies an art placeable model (ResRef, max 16 chars, no spaces) and position
  - Optional section - supported by xoreos, not all implementations support this
  - Used for decorative elements that don't affect gameplay
  - Example: "AP01 20.0 21.0 22.0"

  Walkmesh Rooms (walkmeshRooms):
  - Walkmesh rooms section is only relevant for Jade Engine (Dragon Age, Mass Effect)
  - Format: <room_model> (one per line, no position data)
  - Each entry specifies a room model that has walkmesh data
  - Optional section - KotOR implementations typically ignore this
  - Marks rooms that contain walkmesh geometry for pathfinding
  - Example: "WMRoom01"
  - When present, sets the canWalk flag on matching room entries

  Implementation Differences:
  - PyKotor: Reads doorhooks with "0" field (10 tokens: room, door, 0, x, y, z, qx, qy, qz, qw)
  - xoreos: Reads doorhooks without "0" field (10 tokens: room, door, x, y, z, qx, qy, qz, qw, unk1, unk2)
  - reone: Only supports roomcount section (simplified implementation)
  - Most implementations support: roomcount, trackcount, obstaclecount, doorhookcount
  - xoreos additionally supports: artplaceablecount, walkmeshRooms

  File Structure Details:
  - Header: "beginlayout" (required, case-sensitive)
  - Footer: "donelayout" (required, case-sensitive)
  - Section keywords: "roomcount", "trackcount", "obstaclecount", "doorhookcount", "artplaceablecount", "walkmeshRooms" (case-sensitive)
  - Indentation: Some implementations use indentation (typically 3 spaces) for entries, but it's not required
  - Empty lines: Ignored by all implementations
  - Comments: Not officially supported, but some implementations may skip lines starting with #

  Relationship to Other Formats:
  - MDL/MDX: Room models referenced by ResRef in roomcount entries
  - BWM: Walkmeshes loaded alongside LYT rooms, door hooks may reference BWM transition points
  - VIS: Visibility graph for areas with LYT rooms, controls which rooms are visible
  - ARE: Area files that load LYT layouts to assemble the area geometry

  All implementations (vendor/reone, vendor/xoreos, vendor/KotOR.js, vendor/Kotor.NET)
  parse identical tokens for the core sections. KotOR-Unity mirrors the same structure.
  xoreos extends the format with additional sections for Jade Engine compatibility.

  References:
  - vendor/PyKotor/wiki/LYT-File-Format.md - Complete format documentation
  - vendor/reone/src/libs/resource/format/lytreader.cpp:37-77 - Room parsing implementation
  - vendor/xoreos/src/aurora/lytfile.cpp:98-200 - Complete parser with all sections
  - Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:17-165 - PyKotor parser/writer

seq:
  - id: raw_content
    type: str
    size-eos: true
    encoding: ASCII
    doc: |
      Raw ASCII text content of the entire LYT file.
      The file format structure is:
      - Header: "beginlayout" followed by newline (\n or \r\n)
      - Sections (optional, must appear in order if present):
        * roomcount <N> followed by N room entries (<model> <x> <y> <z>)
        * trackcount <N> followed by N track entries (<model> <x> <y> <z>)
        * obstaclecount <N> followed by N obstacle entries (<model> <x> <y> <z>)
        * doorhookcount <N> followed by N doorhook entries
          - PyKotor format: <room> <door> 0 <x> <y> <z> <qx> <qy> <qz> <qw>
          - xoreos format: <room> <door> <x> <y> <z> <qx> <qy> <qz> <qw> <unk1> <unk2>
        * artplaceablecount <N> followed by N art placeable entries (<model> <x> <y> <z>)
        * walkmeshRooms <N> followed by N room model names (<room_model>)
      - Footer: "donelayout"

      Application code should parse raw_content line-by-line to extract structured data.
      See Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py for reference implementation.

      Line format details:
      - Lines are separated by \n or \r\n (Windows/Unix line endings)
      - Leading whitespace is typically ignored but some implementations use indentation (3 spaces)
      - Tokens within a line are separated by whitespace (space or tab)
      - Empty lines are ignored
      - Comments (lines starting with #) may be ignored by some implementations

types:
  # Type definitions document the expected structure of each section
  # These are used for documentation and validation purposes

  room_entry:
    doc: |
      A single room entry in the roomcount section.
      Format: <room_model> <x> <y> <z>

      Fields:
      - room_model: ResRef of the room model (MDL/MDX file, max 16 chars, no spaces, case-insensitive)
      - x: X coordinate in world space (float, meters)
      - y: Y coordinate in world space (float, meters)
      - z: Z coordinate in world space (float, meters)

      Room models reference MDL/MDX files that define the room geometry.
      Room names are case-insensitive for matching purposes.
      Multiple rooms can be placed to create complex area layouts.

      Example: "M17mg_01a 100.0 100.0 0.0"

  track_entry:
    doc: |
      A single track entry in the trackcount section.
      Format: <track_model> <x> <y> <z>

      Fields:
      - track_model: ResRef of the track booster model (MDL file, max 16 chars, no spaces, case-insensitive)
      - x: X coordinate in world space (float, meters)
      - y: Y coordinate in world space (float, meters)
      - z: Z coordinate in world space (float, meters)

      Tracks are booster elements used exclusively in swoop racing mini-games (KotOR II).
      This section is optional - most modules omit it entirely.
      Used in racing modules like Telos surface racing.
      Each track element represents a booster that provides speed boosts during racing.

      Example: "M17mg_MGT01 0.0 0.0 0.0"

  obstacle_entry:
    doc: |
      A single obstacle entry in the obstaclecount section.
      Format: <obstacle_model> <x> <y> <z>

      Fields:
      - obstacle_model: ResRef of the obstacle model (MDL file, max 16 chars, no spaces, case-insensitive)
      - x: X coordinate in world space (float, meters)
      - y: Y coordinate in world space (float, meters)
      - z: Z coordinate in world space (float, meters)

      Obstacles are hazard elements used exclusively in swoop racing mini-games (KotOR II).
      This section is optional - most modules omit it entirely.
      Used in racing modules to create challenges and obstacles for the player.
      Each obstacle element represents a hazard placed along the racing track.

      Example: "M17mg_MGO01 103.309 3691.61 0.0"

  doorhook_entry_pykotor:
    doc: |
      A single doorhook entry in the doorhookcount section (PyKotor format).
      Format: <room_name> <door_name> 0 <x> <y> <z> <qx> <qy> <qz> <qw>

      Fields:
      - room_name: Target room name (must match a roomcount entry, case-insensitive, max 16 chars)
      - door_name: Hook identifier (used in module files to reference this door, case-insensitive, max 16 chars)
      - 0: Reserved field, always 0, skipped during parsing (tokens[2])
      - x: X coordinate of door origin in world space (float, meters)
      - y: Y coordinate of door origin in world space (float, meters)
      - z: Z coordinate of door origin in world space (float, meters)
      - qx: X component of quaternion orientation (float)
      - qy: Y component of quaternion orientation (float)
      - qz: Z component of quaternion orientation (float)
      - qw: W component of quaternion orientation (float)

      Door hooks define where doors are placed in rooms to create area transitions.
      The quaternion (qx, qy, qz, qw) defines the door's orientation in world space.
      Door hooks are separate from BWM hooks - BWM hooks define interaction points,
      while LYT doorhooks define door placement positions.

      Example: "M02ac_02h door_01 0 170.475 66.375 0.0 0.707107 0.0 0.0 -0.707107"

      Reference: Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:97-113

  doorhook_entry_xoreos:
    doc: |
      A single doorhook entry in the doorhookcount section (xoreos format).
      Format: <room_name> <door_name> <x> <y> <z> <qx> <qy> <qz> <qw> <unk1> <unk2>

      Fields:
      - room_name: Target room name (must match a roomcount entry, case-insensitive, max 16 chars)
      - door_name: Hook identifier (used in module files to reference this door, case-insensitive, max 16 chars)
      - x: X coordinate of door origin in world space (float, meters)
      - y: Y coordinate of door origin in world space (float, meters)
      - z: Z coordinate of door origin in world space (float, meters)
      - qx: X component of quaternion orientation (float)
      - qy: Y component of quaternion orientation (float)
      - qz: Z component of quaternion orientation (float)
      - qw: W component of quaternion orientation (float)
      - unk1: Unknown float value (typically 0.0, purpose unknown)
      - unk2: Unknown float value (typically 0.0, purpose unknown)

      This format does not include the "0" reserved field present in PyKotor format.
      xoreos expects exactly 10 tokens per doorhook entry.

      Example: "Room01 Door01 10.0 11.0 12.0 13.0 14.0 15.0 16.0 17.0"

      Reference: vendor/xoreos/src/aurora/lytfile.cpp:161-187

  artplaceable_entry:
    doc: |
      A single art placeable entry in the artplaceablecount section.
      Format: <artplaceable_model> <x> <y> <z>

      Fields:
      - artplaceable_model: ResRef of the art placeable model (MDL file, max 16 chars, no spaces, case-insensitive)
      - x: X coordinate in world space (float, meters)
      - y: Y coordinate in world space (float, meters)
      - z: Z coordinate in world space (float, meters)

      Art placeables are decorative elements that can be placed in areas.
      This section is optional - supported by xoreos, not all implementations support this.
      Used for decorative elements that don't affect gameplay or collision.

      Example: "AP01 20.0 21.0 22.0"

      Reference: vendor/xoreos/src/aurora/lytfile.cpp:120-139

  walkmesh_room_entry:
    doc: |
      A single walkmesh room entry in the walkmeshRooms section.
      Format: <room_model>

      Fields:
      - room_model: ResRef of the room model (must match a roomcount entry, case-insensitive, max 16 chars)

      Walkmesh rooms section is only relevant for Jade Engine (Dragon Age, Mass Effect).
      This section is optional - KotOR implementations typically ignore this.
      Marks rooms that contain walkmesh geometry for pathfinding.
      When present, sets the canWalk flag on matching room entries.

      Example: "WMRoom01"

      Reference: vendor/xoreos/src/aurora/lytfile.cpp:141-159

instances:
  has_valid_header:
    value: raw_content.startswith("beginlayout")
    doc: |
      True if the file starts with "beginlayout" as required by the format.
      This validates that the file follows the expected LYT format structure.
      The header keyword is case-sensitive and must appear at the start of the file.

  has_valid_footer:
    value: raw_content.find("donelayout") >= 0
    doc: |
      True if the file contains "donelayout" as required by the format.
      This validates that the file properly terminates with the expected footer.
      The footer keyword is case-sensitive and must appear at the end of the file.

  has_rooms_section:
    value: raw_content.find("roomcount") >= 0
    doc: |
      True if the file contains a roomcount section.
      The rooms section is optional but common in most LYT files.
      Rooms are the primary building blocks of area layouts.

  has_tracks_section:
    value: raw_content.find("trackcount") >= 0
    doc: |
      True if the file contains a trackcount section.
      The tracks section is optional and typically only present in KotOR II racing modules.
      Tracks are booster elements used in swoop racing mini-games.

  has_obstacles_section:
    value: raw_content.find("obstaclecount") >= 0
    doc: |
      True if the file contains an obstaclecount section.
      The obstacles section is optional and typically only present in KotOR II racing modules.
      Obstacles are hazard elements used in swoop racing mini-games.

  has_doorhooks_section:
    value: raw_content.find("doorhookcount") >= 0
    doc: |
      True if the file contains a doorhookcount section.
      The doorhooks section is optional but common in LYT files that define area transitions.
      Door hooks define where doors are placed in rooms for area transitions.

  has_artplaceables_section:
    value: raw_content.find("artplaceablecount") >= 0
    doc: |
      True if the file contains an artplaceablecount section.
      The art placeables section is optional and only supported by xoreos and Jade Engine implementations.
      Art placeables are decorative elements that don't affect gameplay.

  has_walkmesh_rooms_section:
    value: raw_content.find("walkmeshRooms") >= 0
    doc: |
      True if the file contains a walkmeshRooms section.
      The walkmesh rooms section is optional and only relevant for Jade Engine (Dragon Age, Mass Effect).
      KotOR implementations typically ignore this section.
      Marks rooms that contain walkmesh geometry for pathfinding.

  is_valid_format:
    value: has_valid_header && has_valid_footer
    doc: |
      True if the file has both required header ("beginlayout") and footer ("donelayout").
      This provides basic format validation for LYT files.
      Both keywords are case-sensitive and must appear exactly as specified.
