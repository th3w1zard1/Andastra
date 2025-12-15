# Dialogue Timing Bug Analysis - swkotor2.exe

## Problem Description

Dialogue skips extremely fast (no audio, immediately advances to end) after the game has been running for a while. This is presumably caused by a memory leak that corrupts timing calculations.

## Root Cause Analysis

### Critical Function: FUN_005e8f80 @ 0x005e8f80

This function handles dialogue timing and object synchronization. The bug is in the timer decrement logic.

### Timer Structure

The dialogue system uses a countdown timer stored at offset `0x188` in the dialogue object:
- `0x188`: Current timer value (milliseconds)
- `0x18c`: Previous time snapshot
- `0x190`: Current time snapshot

### The Bug

**Location**: `0x005e93d2` - `0x005e9406` in FUN_005e8f80

**Assembly Code**:
```asm
005e93d2: MOV EAX,dword ptr [EBX + 0x188]  ; Load timer from offset 0x188
005e93d8: CMP EAX,EDI                      ; Compare with 0 (EDI = 0)
005e93e2: MOV dword ptr [EBX + 0x18c],ECX  ; Store new time at 0x18c
005e93e8: MOV dword ptr [EBX + 0x190],EDX  ; Store new time at 0x190
005e93ee: JBE 0x005e94cc                   ; If timer <= 0, jump (SKIPS TIMER LOGIC!)
005e93f4: MOV ECX,dword ptr [ESP + 0x40]   ; Load elapsed time
005e93f8: CMP EAX,ECX                      ; Compare timer with elapsed
005e93fa: JA 0x005e94c4                    ; If timer > elapsed, decrement
005e9400: MOV dword ptr [EBX + 0x188],EDI  ; Set timer to 0
005e9406: MOV dword ptr [ESP + 0x80],0x1   ; Set bVar1 = TRUE (immediate advance!)
```

**The Problem**:

1. **Timer Corruption**: If the timer at `0x188` gets corrupted to 0 or negative due to memory leak/corruption, the check at `0x005e93ee` (JBE - jump if below or equal) will always jump, skipping the timer decrement logic.

2. **Immediate Advancement**: When `bVar1` is set to `1` (true), it causes dialogue to advance immediately without waiting for the delay.

3. **Timer Reset Logic**: Later at `0x005e95b7`, if timer is 0, it's reset to `0x5dc` (1500ms):
   ```asm
   005e95b5: TEST EAX,EAX                   ; Check if timer is 0
   005e95b7: JNZ 0x005e95c7                 ; If not zero, skip reset
   005e95b7: MOV dword ptr [EBX + 0x188],0x5dc  ; Reset to 1500ms
   ```
   However, if the timer is already corrupted and the elapsed time calculation is also wrong, this reset may not help.

### Memory Leak Connection

The memory leak likely causes:
1. **Timer corruption**: The timer value at `0x188` gets overwritten with 0 or garbage
2. **Time calculation corruption**: The elapsed time calculation (`uStack_24` at `ESP+0x40`) becomes incorrect
3. **Object pointer corruption**: The dialogue object structure gets corrupted, causing timer checks to fail

### Why It Happens After Long Play

After extended play:
- Memory fragmentation increases
- Heap corruption accumulates
- Timer values get overwritten by other allocations
- Time snapshots (`0x18c`, `0x190`) may become invalid

## Fix Strategy

### Option 1: Add Timer Validation (Recommended)

Add a check to ensure the timer value is reasonable before using it:

**Location**: Before `0x005e93d2`

**Patch**:
```asm
; Validate timer is in reasonable range (0 to 60000ms = 60 seconds)
MOV EAX,dword ptr [EBX + 0x188]
CMP EAX,0x0
JL fix_timer_zero          ; If negative, fix it
CMP EAX,0xea60             ; Compare with 60000ms
JA fix_timer_max           ; If > 60 seconds, cap it
JMP continue_normal

fix_timer_zero:
MOV dword ptr [EBX + 0x188],0x5dc  ; Reset to 1500ms
JMP continue_normal

fix_timer_max:
MOV dword ptr [EBX + 0x188],0x5dc  ; Reset to 1500ms
JMP continue_normal

continue_normal:
; Original code continues...
```

### Option 2: Force Timer Reset on Corruption

**Location**: At `0x005e93ee` (the JBE instruction)

**Patch**: Instead of jumping when timer <= 0, reset it:
```asm
005e93ee: JBE reset_timer_instead  ; Change jump target
...
reset_timer_instead:
MOV dword ptr [EBX + 0x188],0x5dc  ; Reset to 1500ms
JMP 0x005e94cc                      ; Continue normal flow
```

### Option 3: Validate Time Snapshots

**Location**: Before time calculation at `0x005e9396`

Add validation that `0x18c` and `0x190` are valid before calculating elapsed time:
```asm
; Validate time snapshots are reasonable
MOV EAX,dword ptr [EBX + 0x18c]
MOV ECX,dword ptr [EBX + 0x190]
CMP EAX,ECX
JA invalid_time            ; If old > new, time went backwards (corruption)
; Continue normal calculation...
```

## Implementation Notes

1. **Offset 0x188**: Dialogue timer (milliseconds)
2. **Offset 0x18c**: Previous time snapshot
3. **Offset 0x190**: Current time snapshot  
4. **0x5dc**: 1500 milliseconds (1.5 seconds) - default delay
5. **FUN_004db710**: Gets current time
6. **FUN_004dbd30**: Calculates elapsed time

## Testing

To verify the fix:
1. Play game for extended period (2+ hours)
2. Trigger multiple dialogues
3. Verify dialogue timing remains correct
4. Check that timer values stay in valid range

## Related Functions

- `FUN_005e8f80` @ 0x005e8f80: Main timing/synchronization function
- `FUN_005eb910` @ 0x005eb910: Dialogue reply processing
- `FUN_005e9920` @ 0x005e9920: Dialogue node processing
- `FUN_005ec340` @ 0x005ec340: Dialogue entry processing
- `FUN_005068e0` @ 0x005068e0: Dialogue update loop

## References

- String "DelayEntry" @ 0x007c38fc
- String "DelayReply" @ 0x007c38f0
- String "Skippable" @ 0x007c38c4
- String "NodeUnskippable" @ 0x007c3600
- String "Error: dialogue can't find object '%s'!" @ 0x007c3730

