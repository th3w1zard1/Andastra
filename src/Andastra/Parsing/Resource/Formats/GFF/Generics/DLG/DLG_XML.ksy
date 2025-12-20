meta:
  id: dlg_xml
  title: BioWare DLG XML Format
  license: MIT
  endian: le
  file-extension: dlg.xml
  encoding: UTF-8
doc: |
  DLG XML format is a human-readable XML representation of DLG (Dialogue) binary files.
  Uses GFF XML structure with root element <gff3> containing <struct> elements.
  Each field has a label attribute and appropriate type element (byte, uint32, exostring, etc.).

seq:
  - id: xml_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: XML document content as UTF-8 text

