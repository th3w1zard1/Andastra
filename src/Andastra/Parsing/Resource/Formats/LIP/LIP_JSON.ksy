meta:
  id: lip_json
  title: BioWare LIP JSON Format
  license: MIT
  endian: le
  file-extension: lip.json
  encoding: UTF-8
doc: |
  LIP JSON format is a human-readable JSON representation of LIP (Lipsync) binary files.
  Provides easier editing than binary LIP format.

seq:
  - id: json_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: JSON document content as UTF-8 text

