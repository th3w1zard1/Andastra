meta:
  id: mdx
  title: BioWare MDX (Model Extension) File Format
  license: MIT
  endian: le
  file-extension: mdx
  xref:
    pykotor: Libraries/PyKotor/src/pykotor/resource/formats/mdl/
    reone: vendor/reone/src/libs/graphics/format/mdlmdxreader.cpp
    mdlops: vendor/mdlops/MDLOpsM.pm
    wiki: vendor/PyKotor/wiki/MDL-MDX-File-Format.md
doc: |
  MDX (Model Extension) files contain vertex data that complements MDL files.
  The MDX file contains interleaved vertex attributes based on flags specified in the MDL trimesh header.

  Vertex data is stored in an interleaved format based on the MDX vertex size.
  Each vertex attribute is accessed via its relative offset within the vertex stride.

  References:
  - vendor/PyKotor/wiki/MDL-MDX-File-Format.md (MDX Data Format section)
  - vendor/mdlops/MDLOpsM.pm (MDX data reading)

seq:
  - id: vertex_data
    type: vertex_data_block
    size-eos: true
    doc: |
      Interleaved vertex data blocks.
      The structure depends on the MDX data flags from the corresponding MDL file.
      Each vertex is stored as a contiguous block of bytes (vertex_size bytes per vertex).

      Vertex attributes are interleaved in this order (if flags are set):
      1. Vertex positions (3 floats: X, Y, Z) - if MDX_VERTICES (0x00000001)
      2. Primary texture coordinates (2 floats: U, V) - if MDX_TEX0_VERTICES (0x00000002)
      3. Secondary texture coordinates (2 floats: U, V) - if MDX_TEX1_VERTICES (0x00000004)
      4. Tertiary texture coordinates (2 floats: U, V) - if MDX_TEX2_VERTICES (0x00000008)
      5. Quaternary texture coordinates (2 floats: U, V) - if MDX_TEX3_VERTICES (0x00000010)
      6. Vertex normals (3 floats: X, Y, Z) - if MDX_VERTEX_NORMALS (0x00000020)
      7. Vertex colors (3 floats: R, G, B) - if MDX_VERTEX_COLORS (0x00000040)
      8. Tangent space data (9 floats: tangent XYZ, bitangent XYZ, normal XYZ) - if MDX_TANGENT_SPACE (0x00000080)
      9. Bone weights (4 floats) - if skinmesh (MDX_BONE_WEIGHTS, 0x00000800)
      10. Bone indices (4 floats, cast to uint16) - if skinmesh (MDX_BONE_INDICES, 0x00001000)

      Note: MDX files are typically accessed via offsets from MDL trimesh headers.
      The actual structure depends on the mdx_data_flags and mdx_vertex_size from the MDL file.

types:
  vertex_data_block:
    seq:
      - id: raw_data
        type: str
        size-eos: true
        doc: |
          Raw vertex data bytes.
          Structure depends on MDX data flags from corresponding MDL file.
          Each vertex is vertex_size bytes, with attributes interleaved based on flags.

          Note: Kaitai Struct cannot dynamically parse interleaved vertex data
          without knowing the flags. This field contains raw bytes that must be
          parsed by the application using the MDX data flags from the MDL file.

