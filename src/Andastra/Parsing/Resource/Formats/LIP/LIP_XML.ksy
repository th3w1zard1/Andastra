meta:
  id: lip_xml
  title: BioWare LIP XML Format
  license: MIT
  endian: le
  file-extension: lip.xml
  encoding: UTF-8
doc: |
  LIP XML format is a human-readable XML representation of LIP (Lipsync) binary files.
  Provides easier editing than binary LIP format.

seq:
  - id: xml_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: XML document content as UTF-8 text

