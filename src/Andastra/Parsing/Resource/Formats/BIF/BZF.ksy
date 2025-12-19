meta:
  id: bzf
  title: BioWare BZF (Compressed BIF) File
  license: MIT
  endian: le
  file-extension: bzf
  xref:
    pykotor: Libraries/PyKotor/src/pykotor/resource/formats/bif/
    reone: vendor/reone/src/libs/resource/format/bifreader.cpp
    xoreos: vendor/xoreos/src/aurora/biffile.cpp
doc: |
  BZF files are LZMA-compressed BIF files used primarily in iOS/Android ports of KotOR.
  The BZF header contains "BZF " + "V1.0", followed by LZMA-compressed BIF data.
  
  Decompression reveals a standard BIF structure that can be parsed as a BIF file.
  
  References:
  - vendor/PyKotor/wiki/BIF-File-Format.md (BZF Compression section)
  - vendor/reone/src/libs/resource/format/bifreader.cpp

seq:
  - id: file_type
    type: str
    encoding: ASCII
    size: 4
    doc: File type signature. Must be "BZF " for compressed BIF files.
    valid: "BZF "
  
  - id: version
    type: str
    encoding: ASCII
    size: 4
    doc: File format version. Must be "V1.0" for BZF files.
    valid: "V1.0"
  
  - id: compressed_data
    type: str
    size-eos: true
    doc: |
      LZMA-compressed BIF file data.
      This entire block (from offset 8 to end of file) is compressed using LZMA.
      Decompression yields a standard BIF file that can be parsed as a BIF.
      
      Note: Kaitai Struct does not natively support LZMA decompression.
      This field contains the raw compressed bytes. Decompression must be
      performed by the application using an LZMA library before parsing as BIF.

