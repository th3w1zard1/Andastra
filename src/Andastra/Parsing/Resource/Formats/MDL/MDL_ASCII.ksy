meta:
  id: mdl_ascii
  title: BioWare MDL ASCII Format
  license: MIT
  endian: le
  file-extension: mdl.ascii
  encoding: UTF-8
doc: |
  MDL ASCII format is a human-readable ASCII text representation of MDL (Model) binary files.
  Used by modding tools for easier editing than binary MDL format.
  
  The ASCII format represents the model structure using plain text with keyword-based syntax.

seq:
  - id: ascii_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: ASCII text content representing the MDL model structure

