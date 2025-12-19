meta:
  id: ssf
  title: BioWare SSF (Sound Set File) Format
  license: MIT
  endian: le
  file-extension: ssf
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ssf/
    wiki: vendor/PyKotor/wiki/SSF-File-Format.md
doc: |
  SSF (Sound Set File) files store sound string references (StrRefs) for character voice sets.
  Each SSF file contains exactly 28 sound slots, mapping to different game events and actions.

  Binary Format:
  - Header (12 bytes): File type signature, version, and offset to sounds array
  - Sounds Array (112 bytes): 28 uint32 values representing StrRefs (0xFFFFFFFF = -1 = no sound)
  - Padding (12 bytes): 3 uint32 values of 0xFFFFFFFF (reserved/unused)

  Total file size: 136 bytes (12 + 112 + 12)

  Sound Slots (in order):
  0-5: Battle Cry 1-6
  6-8: Select 1-3
  9-11: Attack Grunt 1-3
  12-13: Pain Grunt 1-2
  14: Low Health
  15: Dead
  16: Critical Hit
  17: Target Immune
  18: Lay Mine
  19: Disarm Mine
  20: Begin Stealth
  21: Begin Search
  22: Begin Unlock
  23: Unlock Failed
  24: Unlock Success
  25: Separated From Party
  26: Rejoined Party
  27: Poisoned

  References:
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ssf/ssf_binary_reader.py
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ssf/ssf_binary_writer.py

seq:
  - id: file_type
    type: str
    encoding: ASCII
    size: 4
    doc: |
      File type signature. Must be "SSF " (space-padded).
      Bytes: 0x53 0x53 0x46 0x20
    valid: "SSF "

  - id: file_version
    type: str
    encoding: ASCII
    size: 4
    doc: |
      File format version. Always "V1.1" for KotOR SSF files.
      Bytes: 0x56 0x31 0x2E 0x31
    valid: "V1.1"

  - id: sounds_offset
    type: u4
    doc: |
      Byte offset to the sounds array from the beginning of the file.
      Always 12 (0x0C) in valid SSF files, as the sounds array immediately follows the header.
      This field exists for format consistency, though it's always the same value.
    valid: 12

  - id: sounds
    type: sound_array
    pos: sounds_offset
    doc: Array of 28 sound string references (StrRefs)

  - id: padding
    type: padding
    doc: Reserved padding bytes (12 bytes of 0xFFFFFFFF)

types:
  sound_array:
    seq:
      - id: entries
        type: sound_entry
        repeat: expr
        repeat-expr: 28
        doc: |
          Array of exactly 28 sound entries, one for each SSFSound enum value.
          Each entry is a uint32 representing a StrRef (string reference).
          Value 0xFFFFFFFF (4294967295) represents -1 (no sound assigned).

          Entry indices map to SSFSound enum:
          - 0-5: Battle Cry 1-6
          - 6-8: Select 1-3
          - 9-11: Attack Grunt 1-3
          - 12-13: Pain Grunt 1-2
          - 14: Low Health
          - 15: Dead
          - 16: Critical Hit
          - 17: Target Immune
          - 18: Lay Mine
          - 19: Disarm Mine
          - 20: Begin Stealth
          - 21: Begin Search
          - 22: Begin Unlock
          - 23: Unlock Failed
          - 24: Unlock Success
          - 25: Separated From Party
          - 26: Rejoined Party
          - 27: Poisoned

  sound_entry:
    seq:
      - id: strref_raw
        type: u4
        doc: |
          Raw uint32 value representing the StrRef.
          Value 0xFFFFFFFF (4294967295) represents -1 (no sound assigned).
          All other values are valid StrRefs (typically 0-999999).
          The conversion from 0xFFFFFFFF to -1 is handled by SSFBinaryReader.ReadInt32MaxNeg1().

    instances:
      is_no_sound:
        value: strref_raw == 0xFFFFFFFF
        doc: |
          True if this entry represents "no sound" (0xFFFFFFFF).
          False if this entry contains a valid StrRef value.

  padding:
    seq:
      - id: padding_bytes
        type: u4
        repeat: expr
        repeat-expr: 3
        doc: |
          Reserved padding bytes. Always 3 uint32 values of 0xFFFFFFFF.
          Total size: 12 bytes (3 * 4 bytes).
          These bytes are unused but must be present for format compatibility.
          Each padding byte should be 0xFFFFFFFF (4294967295).

