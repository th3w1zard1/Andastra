meta:
  id: gff_xml
  title: BioWare GFF XML Format
  license: MIT
  endian: le
  file-extension: gff.xml
  encoding: UTF-8
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/gff/io_gff_xml.py
    xoreos: vendor/xoreos-tools/src/xml/gffdumper.cpp
doc: |
  GFF XML format is a human-readable XML representation of GFF (Generic File Format) binary files.
  Used by xoreos-tools and other modding tools for easier editing than binary GFF format.
  
  The XML format represents the hierarchical GFF structure using XML elements:
  - Root element: <gff3>
  - Contains a <struct> element with id attribute
  - Struct contains field elements (byte, uint32, exostring, locstring, resref, list, etc.)
  - Each field has a label attribute
  - Lists contain nested <struct> elements
  
  References:
  - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/gff/io_gff_xml.py
  - vendor/xoreos-tools/src/xml/gffdumper.cpp
  - vendor/xoreos-tools/src/xml/gffcreator.cpp

seq:
  - id: xml_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: |
      XML document content as UTF-8 text.
      Structure: <gff3><struct id="...">...</struct></gff3>
      Note: Kaitai Struct has limited XML parsing capabilities. For full XML parsing,
      use an XML parser library. This definition serves as a format identifier.

