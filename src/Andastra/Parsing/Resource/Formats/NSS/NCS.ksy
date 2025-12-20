meta:
  id: ncs
  title: BioWare NCS (NWScript Compiled Script) File
  license: MIT
  endian: be
  file-extension: ncs
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/
    reone: vendor/reone/src/libs/script/format/ncsreader.cpp
    xoreos: vendor/xoreos/src/aurora/nwscript/ncsfile.cpp
    wiki: vendor/PyKotor/wiki/NCS-File-Format.md
doc: |
  NCS (NWScript Compiled Script) files contain compiled bytecode for NWScript,
  the scripting language used in KotOR and TSL. Scripts run inside a stack-based
  virtual machine shared across Aurora engine games.

  The format consists of:
  - Header (13 bytes): File signature, version, size marker, and total file size
  - Instruction stream: Variable-length bytecode instructions

  All multi-byte values are stored in big-endian (network byte order).

  References:
  - vendor/PyKotor/wiki/NCS-File-Format.md
  - vendor/reone/src/libs/script/format/ncsreader.cpp:28-195
  - vendor/xoreos/src/aurora/nwscript/ncsfile.cpp:333-1649
  - vendor/xoreos-docs/specs/torlack/ncs.html

seq:
  - id: file_type
    type: str
    encoding: ASCII
    size: 4
    doc: File type signature. Must be "NCS " (0x4E 0x43 0x53 0x20).

  - id: file_version
    type: str
    encoding: ASCII
    size: 4
    doc: File format version. Must be "V1.0" (0x56 0x31 0x2E 0x30).

  - id: size_marker
    type: u1
    doc: |
      Program size marker opcode. Must be 0x42 (not a real instruction).
      This is a metadata field that all implementations validate before reading the size field.
    valid: 0x42

  - id: total_file_size
    type: u4
    doc: |
      Total file size in bytes (big-endian).
      This includes the header (13 bytes) plus all instructions.
      The actual instruction stream begins at offset 13 (0x0D).

  - id: instructions
    type: instruction
    repeat: until
    repeat-until: _io.pos >= _root.total_file_size
    doc: |
      Stream of bytecode instructions.
      Each instruction consists of a bytecode opcode, qualifier, and variable arguments.
      Instructions are parsed sequentially until the total_file_size is reached.

types:
  instruction:
    seq:
      - id: bytecode
        type: u1
        doc: |
          Instruction opcode byte. Identifies the fundamental instruction type
          (stack manipulation, arithmetic, logic, control flow, etc.).

          Common opcodes:
          - 0x01: CPDOWNSP (Copy Down Stack Pointer)
          - 0x02: RSADDx (Reserve Stack Add)
          - 0x03: CPTOPSP (Copy To Stack Pointer)
          - 0x04: CONSTx (Constant - type determined by qualifier)
          - 0x05: ACTION (Engine function call)
          - 0x06: LOGANDxx (Logical AND)
          - 0x07: LOGORxx (Logical OR)
          - 0x08: INCORxx (Inclusive OR)
          - 0x09: EXCORxx (Exclusive OR)
          - 0x0A: BOOLANDxx (Boolean AND)
          - 0x0B: EQUALxx (Equality test)
          - 0x0C: NEQUALxx (Inequality test)
          - 0x0D: GEQxx (Greater than or equal)
          - 0x0E: GTxx (Greater than)
          - 0x0F: LTxx (Less than)
          - 0x10: LEQxx (Less than or equal)
          - 0x11: SHLEFTxx (Shift left)
          - 0x12: SHRIGHTxx (Shift right signed)
          - 0x13: USHRIGHTxx (Shift right unsigned)
          - 0x1A: COMPx (One's complement)
          - 0x1B: MOVSP (Move Stack Pointer)
          - 0x1D: JMP (Unconditional jump)
          - 0x1E: JSR (Jump to subroutine)
          - 0x1F: JZ (Jump if zero)
          - 0x20: RETN (Return from subroutine)
          - 0x21: DESTRUCT (Destroy stack elements)
          - 0x22: NOTx (Logical NOT)
          - 0x23: DECxSP (Decrement Stack Pointer)
          - 0x24: INCxSP (Increment Stack Pointer)
          - 0x25: JNZ (Jump if not zero)
          - 0x26: CPDOWNBP (Copy Down Base Pointer)
          - 0x27: CPTOPBP (Copy To Base Pointer)
          - 0x28: DECxBP (Decrement Base Pointer)
          - 0x29: INCxBP (Increment Base Pointer)
          - 0x2A: SAVEBP (Save Base Pointer)
          - 0x2B: RESTOREBP (Restore Base Pointer)
          - 0x2C: STORE_STATE (Store stack state)
          - 0x2D: NOP (No operation)

      - id: qualifier
        type: u1
        doc: |
          Type qualifier byte. Refines the instruction to specific operand types.

          Unary types (single operand):
          - 0x03: Integer (I) - 4 bytes
          - 0x04: Float (F) - 4 bytes
          - 0x05: String (S) - 4 bytes (pointer)
          - 0x06: Object (O) - 4 bytes (object ID)
          - 0x10-0x1F: Engine types (Effect, Event, Location, Talent, etc.)

          Binary types (two operands):
          - 0x20: Integer, Integer (II)
          - 0x21: Float, Float (FF)
          - 0x22: Object, Object (OO)
          - 0x23: String, String (SS)
          - 0x24: Structure, Structure (TT)
          - 0x25: Integer, Float (IF)
          - 0x26: Float, Integer (FI)
          - 0x3A: Vector, Vector (VV) - 12 bytes each
          - 0x3B: Vector, Float (VF)
          - 0x3C: Float, Vector (FV)

    doc-ref: |
      Instruction arguments follow the qualifier byte. Format varies by instruction type.
      All multi-byte values are big-endian.

      Note: Kaitai Struct cannot easily handle variable-length fields based on parent field values.
      Application code (NCSBinaryReader) handles argument parsing based on bytecode and qualifier.

      Common argument formats:
      - No args (0 bytes): RSADDx, LOGANDxx, RETN, SAVEBP, RESTOREBP, NOP
      - 4 bytes signed int: MOVSP, INCxSP, DECxSP, INCxBP, DECxBP, JMP, JSR, JZ, JNZ
      - 4 bytes unsigned int: CONSTI (integer constant)
      - 4 bytes float: CONSTF (float constant)
      - 2 bytes length + string: CONSTS (string constant)
      - 4 bytes signed int: CONSTO (object constant)
      - 4 bytes offset + 2 bytes size: CPDOWNSP, CPTOPSP, CPDOWNBP, CPTOPBP
      - 2 bytes routine + 1 byte argCount: ACTION
      - 2 bytes size + 2 bytes offset + 2 bytes sizeNoDestroy: DESTRUCT
      - 4 bytes size + 4 bytes sizeLocals: STORE_STATE
      - 2 bytes size (if qualifier 0x24): EQUALxx, NEQUALxx (structure comparison)


