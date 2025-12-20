meta:
  id: ute_xml
  title: BioWare UTE XML Format
  license: MIT
  endian: le
  file-extension: ute.xml
  encoding: UTF-8
doc: |
  UTE XML format is a human-readable XML representation of UTE (Encounter) binary files.
  Uses GFF XML structure with root element <gff3> containing <struct> elements.
  Each field has a label attribute and appropriate type element (byte, uint32, exostring, etc.).

seq:
  - id: xml_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: XML document content as UTF-8 text

