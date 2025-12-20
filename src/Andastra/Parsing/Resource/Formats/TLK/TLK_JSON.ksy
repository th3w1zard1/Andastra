meta:
  id: tlk_json
  title: BioWare TLK JSON Format
  license: MIT
  endian: le
  file-extension: tlk.json
  encoding: UTF-8
doc: |
  TLK JSON format is a human-readable JSON representation of TLK (Talk Table) binary files.
  Provides easier editing and translation than binary TLK format.

seq:
  - id: json_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: JSON document content as UTF-8 text

