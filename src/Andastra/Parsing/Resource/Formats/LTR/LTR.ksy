meta:
  id: ltr
  title: BioWare LTR (Letter) File
  license: MIT
  endian: le
  file-extension: ltr
  xref:
    pykotor: Libraries/PyKotor/src/pykotor/resource/formats/ltr/
    reone: vendor/reone/src/libs/resource/format/ltrreader.cpp
    xoreos: vendor/xoreos/src/aurora/ltrfile.cpp
doc: |
  LTR (Letter) resources store third-order Markov chain probability tables that the game uses
  to procedurally generate NPC names. The data encodes likelihoods for characters appearing at
  the start, middle, and end of names given zero, one, or two-character context.
  
  KotOR always uses the 28-character alphabet (a-z plus ' and -). Neverwinter Nights (NWN) used
  26 characters; the header explicitly stores the count. This is a KotOR-specific difference from NWN.
  
  LTR files are binary and consist of a short header followed by three probability tables
  (singles, doubles, triples) stored as contiguous float arrays.
  
  References:
  - vendor/PyKotor/wiki/LTR-File-Format.md
  - vendor/reone/src/libs/resource/format/ltrreader.cpp:27-74
  - vendor/xoreos/src/aurora/ltrfile.cpp:135-168
  - vendor/KotOR.js/src/resource/LTRObject.ts:61-117

seq:
  - id: file_type
    type: str
    encoding: ASCII
    size: 4
    doc: File type signature. Must be "LTR " (space-padded) for LTR files.
  
  - id: file_version
    type: str
    encoding: ASCII
    size: 4
    doc: File format version. Must be "V1.0" for LTR files.
  
  - id: letter_count
    type: u1
    doc: |
      Number of characters in the alphabet. Must be 26 (NWN) or 28 (KotOR).
      KotOR uses 28 characters: "abcdefghijklmnopqrstuvwxyz'-"
      NWN uses 26 characters: "abcdefghijklmnopqrstuvwxyz"
  
  - id: single_letter_block
    type: letter_block
    doc: |
      Single-letter probability block (no context).
      Used for generating the first character of names.
      Contains start/middle/end probability arrays, each with letter_count floats.
      Total size: letter_count × 3 × 4 bytes = 336 bytes for KotOR (28 chars).
  
  - id: double_letter_blocks
    type: double_letter_blocks_array
    doc: |
      Double-letter probability blocks (1-character context).
      Array of letter_count blocks, each indexed by the previous character.
      Used for generating the second character based on the first character.
      Each block contains start/middle/end probability arrays.
      Total size: letter_count × 3 × letter_count × 4 bytes = 9,408 bytes for KotOR.
  
  - id: triple_letter_blocks
    type: triple_letter_blocks_array
    doc: |
      Triple-letter probability blocks (2-character context).
      Two-dimensional array of letter_count × letter_count blocks.
      Each block is indexed by the previous two characters.
      Used for generating third and subsequent characters.
      Each block contains start/middle/end probability arrays.
      Total size: letter_count × letter_count × 3 × letter_count × 4 bytes = 73,472 bytes for KotOR.

types:
  letter_block:
    doc: |
      A probability block containing three arrays of probabilities (start, middle, end).
      Each array has letter_count floats representing cumulative probabilities for each character
      in the alphabet appearing at that position (start, middle, or end of name).
      
      Blocks store cumulative probabilities (monotonically increasing floats) that are compared
      against random roll values during name generation.
    seq:
      - id: start_probabilities
        type: f4
        repeat: expr
        repeat-expr: _root.letter_count
        doc: |
          Array of start probabilities. One float per character in alphabet.
          Probability of each letter starting a name (no context for singles,
          after previous character for doubles, after previous two for triples).
      
      - id: middle_probabilities
        type: f4
        repeat: expr
        repeat-expr: _root.letter_count
        doc: |
          Array of middle probabilities. One float per character in alphabet.
          Probability of each letter appearing in the middle of a name.
      
      - id: end_probabilities
        type: f4
        repeat: expr
        repeat-expr: _root.letter_count
        doc: |
          Array of end probabilities. One float per character in alphabet.
          Probability of each letter ending a name.

  double_letter_blocks_array:
    doc: |
      Array of double-letter blocks. One block per character in the alphabet.
      Each block is indexed by the previous character (context length 1).
    seq:
      - id: blocks
        type: letter_block
        repeat: expr
        repeat-expr: _root.letter_count
        doc: |
          Array of letter_count blocks, each containing start/middle/end probability arrays.
          Block index corresponds to the previous character in the alphabet.

  triple_letter_blocks_array:
    doc: |
      Two-dimensional array of triple-letter blocks. letter_count × letter_count blocks total.
      Each block is indexed by the previous two characters (context length 2).
    seq:
      - id: rows
        type: triple_letter_row
        repeat: expr
        repeat-expr: _root.letter_count
        doc: |
          Array of letter_count rows, each containing letter_count blocks.
          First index corresponds to the second-to-last character.
          Second index corresponds to the last character.

  triple_letter_row:
    doc: |
      A row in the triple-letter blocks array. Contains letter_count blocks,
      each indexed by the last character in the two-character context.
    seq:
      - id: blocks
        type: letter_block
        repeat: expr
        repeat-expr: _root.letter_count
        doc: |
          Array of letter_count blocks, each containing start/middle/end probability arrays.
          Block index corresponds to the last character in the two-character context.


