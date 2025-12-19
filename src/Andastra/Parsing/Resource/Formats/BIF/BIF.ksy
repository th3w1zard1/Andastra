meta:
  id: bif
  title: BioWare BIF (Binary Index Format) File
  license: MIT
  endian: le
  file-extension: bif
  xref:
    pykotor: Libraries/PyKotor/src/pykotor/resource/formats/bif/
    reone: vendor/reone/src/libs/resource/format/bifreader.cpp
    xoreos: vendor/xoreos/src/aurora/biffile.cpp
doc: |
  BIF (BioWare Index File) files are archive containers that store game resources.
  They work in tandem with KEY files which provide the filename-to-resource mappings.
  
  BIF files contain only resource IDs, types, and data - the actual filenames (ResRefs)
  are stored in the KEY file and matched via the resource ID.
  
  References:
  - vendor/PyKotor/wiki/BIF-File-Format.md
  - vendor/xoreos-docs/specs/torlack/bif.html
  - vendor/reone/src/libs/resource/format/bifreader.cpp

seq:
  - id: file_type
    type: str
    encoding: ASCII
    size: 4
    doc: File type signature. Must be "BIFF" for BIF files.
    valid: "BIFF"
  
  - id: version
    type: str
    encoding: ASCII
    size: 4
    doc: File format version. Typically "V1  " or "V1.1".
    valid: ["V1  ", "V1.1"]
  
  - id: var_res_count
    type: u4
    doc: Number of variable-size resources in this file.
  
  - id: fixed_res_count
    type: u4
    doc: Number of fixed-size resources (always 0 in KotOR, legacy from NWN).
    valid: 0
  
  - id: var_table_offset
    type: u4
    doc: Byte offset to the variable resource table from the beginning of the file.
  
  - id: var_resource_table
    type: var_resource_table
    if: var_res_count > 0
    pos: var_table_offset
    doc: Variable resource table containing entries for each resource.

types:
  var_resource_table:
    seq:
      - id: entries
        type: var_resource_entry
        repeat: expr
        repeat-expr: _root.var_res_count
        doc: Array of variable resource entries.
  
  var_resource_entry:
    seq:
      - id: resource_id
        type: u4
        doc: |
          Resource ID (matches KEY file entry).
          Encodes BIF index (bits 31-20) and resource index (bits 19-0).
          Formula: resource_id = (bif_index << 20) | resource_index
      
      - id: offset
        type: u4
        doc: Byte offset to resource data in file (absolute file offset).
      
      - id: file_size
        type: u4
        doc: Uncompressed size of resource data in bytes.
      
      - id: resource_type
        type: u4
        doc: Resource type identifier (see ResourceType enum).
      
      - id: data
        type: str
        size: file_size
        pos: offset
        doc: Raw binary data for the resource (read at specified offset).

