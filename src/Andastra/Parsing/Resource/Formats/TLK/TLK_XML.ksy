meta:
  id: tlk_xml
  title: BioWare TLK XML Format
  license: MIT
  endian: le
  file-extension: tlk.xml
  encoding: UTF-8
doc: |
  TLK XML format is a human-readable XML representation of TLK (Talk Table) binary files.
  Provides easier editing and translation than binary TLK format.

seq:
  - id: xml_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: XML document content as UTF-8 text

