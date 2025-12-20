meta:
  id: uti_xml
  title: BioWare UTI XML Format
  license: MIT
  endian: le
  file-extension: uti.xml
  encoding: UTF-8
doc: |
  UTI XML format is a human-readable XML representation of UTI (Item) binary files.
  Uses GFF XML structure with root element <gff3> containing <struct> elements.
  Each field has a label attribute and appropriate type element (byte, uint32, exostring, etc.).

seq:
  - id: xml_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: XML document content as UTF-8 text

