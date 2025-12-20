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
    valid: "NCS "
  
  - id: file_version
    type: str
    encoding: ASCII
    size: 4
    doc: File format version. Must be "V1.0" (0x56 0x31 0x2E 0x30).
    valid: "V1.0"
  
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
    repeat-until: _io.pos >= total_file_size
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
      
      - id: args
        type: instruction_args
        doc: |
          Instruction arguments. Format varies by instruction type.
          All multi-byte values are big-endian.
          
          Note: Due to Kaitai Struct limitations with dynamic field sizes based on parent values,
          this structure provides a basic framework. The actual argument parsing must be handled
          by application code based on bytecode and qualifier values.
          
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

  instruction_args:
    doc: |
      Instruction arguments placeholder.
      Actual argument parsing must be done by application code based on bytecode and qualifier.
      This type exists for structure documentation purposes.
    seq: []

  instruction_arg_data:
    doc: Base type for instruction argument data.
    seq: []

  no_args:
    doc: |
      Instructions with no arguments (just opcode + qualifier, 2 bytes total).
      Examples: RSADDx, LOGANDxx, LOGORxx, RETN, SAVEBP, RESTOREBP, NOP
    seq: []

  const_args:
    doc: |
      Constant instruction arguments. Format depends on qualifier:
      - 0x03 (Int): 4-byte unsigned integer (big-endian)
      - 0x04 (Float): 4-byte IEEE 754 float (big-endian)
      - 0x05 (String): 2-byte length (big-endian) + ASCII string data
      - 0x06 (Object): 4-byte signed integer (big-endian)
    seq:
      - id: const_value
        type: const_value_data
        switch-on: _parent._parent.qualifier
        cases:
          0x03: const_int_value
          0x04: const_float_value
          0x05: const_string_value
          0x06: const_object_value
        doc: Constant value based on qualifier type.

  const_int_value:
    doc: Integer constant (4-byte unsigned, big-endian).
    seq:
      - id: value
        type: u4
        doc: Unsigned 32-bit integer value (can represent signed -2³¹ to 2³¹-1).

  const_float_value:
    doc: Float constant (4-byte IEEE 754, big-endian).
    seq:
      - id: value
        type: f4be
        doc: 32-bit IEEE 754 floating-point value (big-endian).

  const_string_value:
    doc: String constant (2-byte length + ASCII string data).
    seq:
      - id: length
        type: u2
        doc: String length in bytes (big-endian).
      - id: value
        type: str
        size: length
        encoding: ASCII
        doc: String data (no null terminator).

  const_object_value:
    doc: Object constant (4-byte signed integer, big-endian).
    seq:
      - id: value
        type: s4be
        doc: |
          Signed 32-bit object ID (big-endian).
          Special values:
          - 0x00000000: OBJECT_SELF
          - 0x00000001 or 0xFFFFFFFF: OBJECT_INVALID

  stack_copy_args:
    doc: |
      Stack copy operation arguments (8 bytes total):
      - 4-byte signed integer offset (big-endian)
      - 2-byte unsigned integer size (big-endian)
      
      Used by: CPDOWNSP, CPTOPSP, CPDOWNBP, CPTOPBP
    seq:
      - id: offset
        type: s4be
        doc: |
          Signed 32-bit stack offset (big-endian).
          Negative offsets access stack elements (e.g., -4 = position 1, -8 = position 2).
      - id: size
        type: u2
        doc: |
          Number of bytes to copy (big-endian).
          Must be a multiple of 4 for alignment.

  movsp_args:
    doc: |
      MOVSP (Move Stack Pointer) arguments (4 bytes total):
      - 4-byte signed integer offset (big-endian)
    seq:
      - id: offset
        type: s4be
        doc: |
          Signed 32-bit stack pointer adjustment offset (big-endian).
          Positive = deallocate, negative = allocate.

  jump_args:
    doc: |
      Jump instruction arguments (4 bytes total):
      - 4-byte signed integer relative offset (big-endian)
      
      Used by: JMP, JSR, JZ, JNZ
      
      The offset is relative to the start of the jump instruction itself,
      not the next instruction. Allows both forward and backward jumps.
    seq:
      - id: offset
        type: s4be
        doc: |
          Signed 32-bit relative jump offset (big-endian).
          Calculated as: target_address - instruction_address.

  action_args:
    doc: |
      ACTION (engine function call) arguments (3 bytes total):
      - 2-byte unsigned integer routine number (big-endian)
      - 1-byte unsigned integer argument count
      
      Routine number indexes into the engine's function table.
      Argument count specifies how many stack elements (not bytes) to pass.
    seq:
      - id: routine
        type: u2
        doc: |
          Unsigned 16-bit routine number (big-endian).
          Index into engine function table (actions data).
      - id: arg_count
        type: u1
        doc: |
          Unsigned 8-bit argument count.
          Number of stack elements (not bytes) to pass to the function.

  destruct_args:
    doc: |
      DESTRUCT (destroy stack elements) arguments (6 bytes total):
      - 2-byte unsigned integer size (big-endian)
      - 2-byte signed integer stack offset (big-endian)
      - 2-byte unsigned integer size_no_destroy (big-endian)
      
      Performs complex stack cleanup by removing size bytes from the stack
      starting at stackOffset, but preserves sizeNoDestroy bytes within that range.
    seq:
      - id: size
        type: u2
        doc: |
          Total number of bytes to destroy from stack (big-endian).
      - id: stack_offset
        type: s2be
        doc: |
          Signed 16-bit offset from stack pointer (big-endian).
          Converted to position by dividing by 4.
      - id: size_no_destroy
        type: u2
        doc: |
          Number of bytes to preserve (not destroyed) within the destruction range (big-endian).

  incdec_args:
    doc: |
      Increment/Decrement instruction arguments (4 bytes total):
      - 4-byte signed integer offset (big-endian)
      
      Used by: INCxSP, DECxSP, INCxBP, DECxBP
    seq:
      - id: offset
        type: s4be
        doc: |
          Signed 32-bit stack/base pointer offset (big-endian).
          Negative offsets access stack elements.

  store_state_args:
    doc: |
      STORE_STATE (save stack state) arguments (8 bytes total):
      - 4-byte signed integer size (big-endian)
      - 4-byte signed integer size_locals (big-endian)
      
      Used with DelayCommand. Separates temp values from persistent locals.
    seq:
      - id: size
        type: s4be
        doc: |
          Total number of bytes of stack state to store (big-endian).
      - id: size_locals
        type: s4be
        doc: |
          Number of bytes of local variables to preserve (big-endian).

  comparison_args:
    doc: |
      Comparison instruction arguments. Format depends on qualifier:
      - For TT (structure) qualifier (0x24): 2-byte unsigned integer size (big-endian)
      - For other qualifiers: no arguments
      
      Used by: EQUALxx, NEQUALxx
    seq:
      - id: struct_size
        type: u2
        if: _parent._parent.qualifier == 0x24
        doc: |
          Number of bytes to compare for structure equality (big-endian).
          Only present when qualifier is 0x24 (structure, structure).
          Must be a multiple of 4 for alignment.


