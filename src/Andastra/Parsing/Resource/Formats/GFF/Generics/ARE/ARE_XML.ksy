meta:
  id: are_xml
  title: BioWare ARE XML Format
  license: MIT
  endian: le
  file-extension: are.xml
  encoding: UTF-8
doc: |
  ARE XML format is a human-readable XML representation of ARE (Area) binary files.
  Uses GFF XML structure with root element <gff3> containing <struct> elements.
  Each field has a label attribute and appropriate type element (byte, uint32, exostring, etc.).

seq:
  - id: xml_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: XML document content as UTF-8 text

