meta:
  id: key
  title: BioWare KEY (Key Table) File
  license: MIT
  endian: le
  file-extension: key
  xref:
    pykotor: Libraries/PyKotor/src/pykotor/resource/formats/key/
    reone: vendor/reone/src/libs/resource/format/keyreader.cpp
    xoreos: vendor/xoreos/src/aurora/keyfile.cpp
doc: |
  KEY files serve as the master index for all BIF files in the game.
  They map resource names (ResRefs) and types to specific locations within BIF archives.
  
  The KEY file contains:
  - BIF file entries (filename, size, location)
  - KEY entries mapping ResRef + ResourceType to Resource ID
  
  References:
  - vendor/PyKotor/wiki/KEY-File-Format.md
  - vendor/xoreos-docs/specs/torlack/key.html
  - vendor/reone/src/libs/resource/format/keyreader.cpp

seq:
  - id: file_type
    type: str
    encoding: ASCII
    size: 4
    doc: File type signature. Must be "KEY " (space-padded).
    valid: "KEY "
  
  - id: file_version
    type: str
    encoding: ASCII
    size: 4
    doc: File format version. Typically "V1  " or "V1.1".
    valid: ["V1  ", "V1.1"]
  
  - id: bif_count
    type: u4
    doc: Number of BIF files referenced by this KEY file.
  
  - id: key_count
    type: u4
    doc: Number of resource entries in the KEY table.
  
  - id: file_table_offset
    type: u4
    doc: Byte offset to the file table from the beginning of the file.
  
  - id: key_table_offset
    type: u4
    doc: Byte offset to the KEY table from the beginning of the file.
  
  - id: build_year
    type: u4
    doc: Build year (years since 1900).
  
  - id: build_day
    type: u4
    doc: Build day (days since January 1).
  
  - id: reserved
    type: str
    size: 32
    doc: Reserved padding (usually zeros).
  
  - id: file_table
    type: file_table
    if: bif_count > 0
    pos: file_table_offset
    doc: File table containing BIF file entries.
  
  - id: filename_table
    type: filename_table
    if: bif_count > 0
    pos: file_table_offset + (bif_count * 12)
    doc: Filename table containing null-terminated BIF filenames.
  
  - id: key_table
    type: key_table
    if: key_count > 0
    pos: key_table_offset
    doc: KEY table containing resource entries.

types:
  file_table:
    seq:
      - id: entries
        type: file_entry
        repeat: expr
        repeat-expr: _root.bif_count
        doc: Array of BIF file entries.
  
  file_entry:
    seq:
      - id: file_size
        type: u4
        doc: Size of the BIF file on disk in bytes.
      
      - id: filename_offset
        type: u4
        doc: Byte offset into the filename table where this BIF's filename is stored.
      
      - id: filename_size
        type: u2
        doc: Length of the filename in bytes (including null terminator).
      
      - id: drives
        type: u2
        doc: |
          Drive flags indicating which media contains the BIF file.
          Bit flags: 0x0001=HD0, 0x0002=CD1, 0x0004=CD2, 0x0008=CD3, 0x0010=CD4.
          Modern distributions typically use 0x0001 (HD) for all files.
      
      - id: filename
        type: str
        encoding: ASCII
        size: filename_size
        pos: _root.file_table_offset + (_root.bif_count * 12) + filename_offset
        doc: BIF filename (read from filename table at specified offset).
  
  filename_table:
    seq:
      - id: filenames
        type: str
        encoding: ASCII
        size-eos: true
        doc: |
          Null-terminated BIF filenames concatenated together.
          Each filename is read using the filename_offset and filename_size
          from the corresponding file_entry.
  
  key_table:
    seq:
      - id: entries
        type: key_entry
        repeat: expr
        repeat-expr: _root.key_count
        doc: Array of resource entries.
  
  key_entry:
    seq:
      - id: resref
        type: str
        encoding: ASCII
        size: 16
        doc: |
          Resource filename (ResRef) without extension.
          Null-padded, maximum 16 characters.
          The game uses this name to access the resource.
      
      - id: resource_type
        type: u2
        doc: Resource type identifier (see ResourceType enum).
      
      - id: resource_id
        type: u4
        doc: |
          Encoded resource location.
          Bits 31-20: BIF index (top 12 bits) - index into file table
          Bits 19-0: Resource index (bottom 20 bits) - index within the BIF file
          
          Formula: resource_id = (bif_index << 20) | resource_index
          
          Decoding:
          - bif_index = (resource_id >> 20) & 0xFFF
          - resource_index = resource_id & 0xFFFFF

