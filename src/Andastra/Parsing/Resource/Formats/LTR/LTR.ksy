meta:
  id: ltr
  file-extension: ltr
  endian: le
  license: MIT

seq:
  - id: file_type
    type: str
    size: 4
    encoding: ASCII
    doc: File type signature, always "LTR " (space-padded)
  - id: file_version
    type: str
    size: 4
    encoding: ASCII
    doc: File version, always "V1.0" for KotOR
  - id: letter_count
    type: u1
    doc: "Number of characters in alphabet (28 for KotOR: a-z plus ' and -)"
  - id: single_letter_block
    type: letter_block
    doc: Single-letter probability block (context length 0)
  - id: double_letter_blocks
    type: double_letter_block
    repeat: expr
    repeat-expr: letter_count
    doc: Double-letter probability blocks (context length 1, one per character)
  - id: triple_letter_blocks
    type: triple_letter_block
    repeat: expr
    repeat-expr: letter_count
    doc: Triple-letter probability blocks (context length 2, organized as 28x28 matrix)

types:
  letter_block:
    seq:
      - id: start_probabilities
        type: f4
        repeat: expr
        repeat-expr: _root.letter_count
        doc: Probability of each letter appearing at start position (28 floats)
      - id: middle_probabilities
        type: f4
        repeat: expr
        repeat-expr: _root.letter_count
        doc: Probability of each letter appearing at middle position (28 floats)
      - id: end_probabilities
        type: f4
        repeat: expr
        repeat-expr: _root.letter_count
        doc: Probability of each letter appearing at end position (28 floats)

  double_letter_block:
    seq:
      - id: block
        type: letter_block
        doc: Letter block for this double-letter context

  triple_letter_block:
    seq:
      - id: blocks
        type: letter_block
        repeat: expr
        repeat-expr: _root.letter_count
        doc: Letter blocks for this triple-letter context row (28 blocks)

