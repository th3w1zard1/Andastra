meta:
  id: lyt
  title: BioWare LYT (Layout) File Format
  license: MIT
  endian: le
  encoding: ASCII
  file-extension: lyt
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
  interactive elements (doors, tracks, obstacles) are positioned. The game engine
  uses LYT files to load and position room models (MDL files) and determine
  door placement points for area transitions.

  Format Overview:
  - LYT files are ASCII text with a deterministic order
  - Structure: beginlayout, optional sections, then donelayout
  - Every section declares a count and then lists entries on subsequent lines
  - Sections: roomcount, trackcount, obstaclecount, doorhookcount
  - All sections are optional but must appear in order if present

  Syntax:
  ```
  beginlayout
  roomcount <N>
    <room_model> <x> <y> <z>
  trackcount <N>
    <track_model> <x> <y> <z>
  obstaclecount <N>
    <obstacle_model> <x> <y> <z>
  doorhookcount <N>
    <room_name> <door_name> 0 <x> <y> <z> <qx> <qy> <qz> <qw> [optional floats]
  donelayout
  ```

  Coordinate System:
  - Units are meters in the same left-handed coordinate system as MDL models
  - Positions (x, y, z) are world-space coordinates
  - Quaternions (qx, qy, qz, qw) define door orientation in world space

  Room Definitions:
  - Each room entry specifies a room model (ResRef, max 16 chars) and position
  - Room models reference MDL/MDX files that define the room geometry
  - Room names are case-insensitive

  Track Definitions:
  - Tracks are booster elements used exclusively in swoop racing mini-games (KotOR II)
  - Each track entry specifies a track model (ResRef, max 16 chars) and position
  - Optional section - most modules omit this entirely

  Obstacle Definitions:
  - Obstacles are hazard elements used exclusively in swoop racing mini-games (KotOR II)
  - Each obstacle entry specifies an obstacle model (ResRef, max 16 chars) and position
  - Optional section - most modules omit this entirely

  Door Hook Definitions:
  - Door hooks bind door models to rooms for area transitions
  - Each entry specifies: room name, door name, position (x,y,z), quaternion (qx,qy,qz,qw)
  - Room name must match a roomcount entry (case-insensitive)
  - Door name is a hook identifier used in module files
  - Some implementations include 5 additional optional float values after the quaternion

  Note: Since LYT is a line-based ASCII text format, this Kaitai Struct definition
  provides the raw text content. Actual parsing of rooms, tracks, obstacles, and
  doorhooks should be done by the application code, splitting by lines and parsing tokens.

  All implementations (vendor/reone, vendor/xoreos, vendor/KotOR.js, vendor/Kotor.NET)
  parse identical tokens. KotOR-Unity mirrors the same structure.

  References:
  - vendor/PyKotor/wiki/LYT-File-Format.md
  - vendor/reone/src/libs/resource/format/lytreader.cpp:37-77
  - vendor/xoreos/src/aurora/lytfile.cpp
  - Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:17-165

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
        * doorhookcount <N> followed by N doorhook entries (<room> <door> 0 <x> <y> <z> <qx> <qy> <qz> <qw> [optional floats])
      - Footer: "donelayout"

      Application code should parse raw_content line-by-line to extract structured data.
      See Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py for reference implementation.

      Line format details:
      - Lines are separated by \n or \r\n (Windows/Unix line endings)
      - Leading whitespace is typically ignored but some implementations use indentation
      - Tokens within a line are separated by whitespace (space or tab)
      - Empty lines are ignored

types:
  # Type definitions document the expected structure of each section
  # These are used for documentation and validation purposes

  room_entry:
    doc: |
      A single room entry in the roomcount section.
      Format: <room_model> <x> <y> <z>

      Fields:
      - room_model: ResRef of the room model (MDL/MDX file, max 16 chars, no spaces)
      - x: X coordinate in world space (float, meters)
      - y: Y coordinate in world space (float, meters)
      - z: Z coordinate in world space (float, meters)

      Room models reference MDL/MDX files that define the room geometry.
      Room names are case-insensitive for matching purposes.

  track_entry:
    doc: |
      A single track entry in the trackcount section.
      Format: <track_model> <x> <y> <z>

      Fields:
      - track_model: ResRef of the track booster model (MDL file, max 16 chars, no spaces)
      - x: X coordinate in world space (float, meters)
      - y: Y coordinate in world space (float, meters)
      - z: Z coordinate in world space (float, meters)

      Tracks are booster elements used exclusively in swoop racing mini-games (KotOR II).
      This section is optional - most modules omit it entirely.

  obstacle_entry:
    doc: |
      A single obstacle entry in the obstaclecount section.
      Format: <obstacle_model> <x> <y> <z>

      Fields:
      - obstacle_model: ResRef of the obstacle model (MDL file, max 16 chars, no spaces)
      - x: X coordinate in world space (float, meters)
      - y: Y coordinate in world space (float, meters)
      - z: Z coordinate in world space (float, meters)

      Obstacles are hazard elements used exclusively in swoop racing mini-games (KotOR II).
      This section is optional - most modules omit it entirely.

  doorhook_entry:
    doc: |
      A single doorhook entry in the doorhookcount section.
      Format: <room_name> <door_name> 0 <x> <y> <z> <qx> <qy> <qz> <qw> [optional floats]

      Fields:
      - room_name: Target room name (must match a roomcount entry, case-insensitive)
      - door_name: Hook identifier (used in module files to reference this door, case-insensitive)
      - 0: Reserved field, always 0
      - x: X coordinate of door origin in world space (float, meters)
      - y: Y coordinate of door origin in world space (float, meters)
      - z: Z coordinate of door origin in world space (float, meters)
      - qx: X component of quaternion orientation (float)
      - qy: Y component of quaternion orientation (float)
      - qz: Z component of quaternion orientation (float)
      - qw: W component of quaternion orientation (float)
      - [optional floats]: Some implementations (xoreos/KotOR-Unity) record 5 extra floats; PyKotor ignores them

      Door hooks define where doors are placed in rooms to create area transitions.
      The quaternion (qx, qy, qz, qw) defines the door's orientation in world space.
      Door hooks are separate from BWM hooks - BWM hooks define interaction points,
      while LYT doorhooks define door placement positions.

instances:
  has_valid_header:
    value: raw_content.startswith("beginlayout")
    doc: |
      True if the file starts with "beginlayout" as required by the format.
      This validates that the file follows the expected LYT format structure.

  has_valid_footer:
    value: "donelayout" in raw_content
    doc: |
      True if the file contains "donelayout" as required by the format.
      This validates that the file properly terminates with the expected footer.

  has_rooms_section:
    value: "roomcount" in raw_content
    doc: |
      True if the file contains a roomcount section.
      The rooms section is optional but common in most LYT files.

  has_tracks_section:
    value: "trackcount" in raw_content
    doc: |
      True if the file contains a trackcount section.
      The tracks section is optional and typically only present in KotOR II racing modules.

  has_obstacles_section:
    value: "obstaclecount" in raw_content
    doc: |
      True if the file contains an obstaclecount section.
      The obstacles section is optional and typically only present in KotOR II racing modules.

  has_doorhooks_section:
    value: "doorhookcount" in raw_content
    doc: |
      True if the file contains a doorhookcount section.
      The doorhooks section is optional but common in LYT files that define area transitions.

  is_valid_format:
    value: has_valid_header && has_valid_footer
    doc: |
      True if the file has both required header ("beginlayout") and footer ("donelayout").
      This provides basic format validation for LYT files.
