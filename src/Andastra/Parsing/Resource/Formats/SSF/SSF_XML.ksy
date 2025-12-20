meta:
  id: ssf_xml
  title: BioWare SSF XML Format
  license: MIT
  endian: le
  file-extension: ssf.xml
  encoding: UTF-8
doc: |
  SSF XML format is a human-readable XML representation of SSF (Soundset) binary files.
  Provides easier editing than binary SSF format.

seq:
  - id: xml_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: XML document content as UTF-8 text

