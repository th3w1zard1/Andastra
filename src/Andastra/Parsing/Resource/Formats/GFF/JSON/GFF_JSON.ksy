meta:
  id: gff_json
  title: BioWare GFF JSON Format
  license: MIT
  endian: le
  file-extension: gff.json
  encoding: UTF-8
doc: |
  GFF JSON format is a human-readable JSON representation of GFF (Generic File Format) binary files.
  Provides easier editing and interoperability with modern tools than binary GFF format.

seq:
  - id: json_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: JSON document content as UTF-8 text

