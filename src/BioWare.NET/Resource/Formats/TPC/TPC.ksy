meta:
  id: tpc
  file-extension: tpc
  endian: le
  license: MIT

seq:
  - id: data_size
    type: u4
    doc: "Data size (0 for uncompressed RGB; compressed textures store total bytes)"
  - id: alpha_test
    type: f4
    doc: "Alpha test/threshold (float, commonly 0.0 or 0.5)"
  - id: width
    type: u2
    doc: "Texture width (uint16)"
  - id: height
    type: u2
    doc: "Texture height (uint16)"
  - id: pixel_type
    type: u1
    doc: "Pixel encoding flag (0x01=Greyscale, 0x02=RGB/DXT1, 0x04=RGBA/DXT5, 0x0C=BGRA)"
  - id: mipmap_count
    type: u1
    doc: "Number of mipmap levels per layer (minimum 1)"
  - id: reserved
    size: 0x72
    type: str
    encoding: ASCII
    pad-right: 0
    doc: "Reserved/padding (0x72 bytes)"
  - id: texture_data
    type: texture_data_block
    doc: "Texture data (per layer, per mipmap)"

types:
  texture_data_block:
    doc: "Texture data structure (simplified - actual parsing depends on format and mipmap count)"
    seq:
      - id: raw_data
        type: u1
        doc: "Raw texture data bytes (exact size depends on format, dimensions, mipmaps, and layers)"
        repeat: eos

