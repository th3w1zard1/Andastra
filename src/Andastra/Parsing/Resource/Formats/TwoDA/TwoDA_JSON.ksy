meta:
  id: twoda_json
  title: BioWare TwoDA JSON Format
  license: MIT
  endian: le
  file-extension: 2da.json
  encoding: UTF-8
doc: |
  TwoDA JSON format is a human-readable JSON representation of TwoDA files.
  Provides easier editing and interoperability with modern tools than binary TwoDA format.

seq:
  - id: json_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: JSON document content as UTF-8 text

