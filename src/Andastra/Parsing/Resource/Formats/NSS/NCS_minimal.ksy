meta:
  id: ncs
  title: BioWare NCS File
  license: MIT
  endian: be
  file-extension: ncs

seq:
  - id: file_type
    type: str
    encoding: ASCII
    size: 4
  
  - id: file_version
    type: str
    encoding: ASCII
    size: 4
  
  - id: size_marker
    type: u1
  
  - id: total_file_size
    type: u4
  
  - id: instructions
    type: instruction
    repeat: until
    repeat-until: _io.pos >= _root.total_file_size

types:
  instruction:
    seq:
      - id: bytecode
        type: u1
      - id: qualifier
        type: u1

