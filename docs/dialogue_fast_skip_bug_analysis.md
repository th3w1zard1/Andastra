# Dialogue Fast-Skip Bug Analysis

## Problem Description
In swkotor2.exe, there is a bug where dialogue skips extremely fast in random arbitrary scenarios, especially when the game has been running for a long time. This is presumably caused by a memory leak.

## Investigation Findings

### String References Found
- `"ScriptDialogue"` @ 0x007bee40
- `"ScriptEndDialogue"` @ 0x007bede0
- `"OnDialog"` @ 0x007c1a04
- `"OnEndDialogue"` @ 0x007c1f60
- `"NodeUnskippable"` @ 0x007c3600
- `"Delay"` @ 0x007c35b0
- `"WaitFlags"` @ 0x007c35ec
- `"FadeDelay"` @ 0x007c358c
- `"DelayEntry"` @ 0x007c38fc
- `"DelayReply"` @ 0x007c38f0

### Key Functions Identified

1. **FUN_005e6ac0** @ 0x005e6ac0
   - Loads dialogue node data from GFF
   - Reads `Delay` field (stored at offset 0x70)
   - Reads `NodeUnskippable` (stored at offset 0x68)
   - Reads `WaitFlags` (stored at offset 0x6c)
   - Reads `FadeDelay` (stored at offset 0xc8)
   - Logic for setting delay:
     - If `Delay == -1` and no voiceover: uses default delay from `*param_4`
     - If `Delay == -1` and voiceover exists: sets delay based on voiceover duration
     - If `WaitFlags == 0` and no voiceover: clears voiceover ResRef
     - Otherwise: sets delay from `Delay` field or voiceover duration

2. **FUN_005ea880** @ 0x005ea880
   - Loads dialogue file (DLG) structure
   - Reads `DelayEntry` and `DelayReply` from root DLG structure
   - These appear to be default delays for entries/replies

### Potential Bug Sources

1. **Memory Leak in Delay Calculation**
   - The delay calculation in `FUN_005e6ac0` uses `*param_4` as a default delay value
   - If this pointer becomes corrupted or points to invalid memory after extended play, it could cause timing issues
   - The delay field is stored as an `int` at offset 0x70, but timing calculations may use floating-point

2. **WaitFlags Accumulation**
   - `WaitFlags` is stored at offset 0x6c and controls waiting behavior
   - If flags accumulate incorrectly over time (memory leak), it could cause premature advancement

3. **Voiceover Timing Issues**
   - When voiceover exists, delay is calculated based on voiceover duration
   - If voiceover playback state becomes corrupted, timing calculations could fail
   - The code checks `SoundExists` flag (offset 0xcc) which might become stale

4. **Timer Not Being Reset**
   - If dialogue node timers are not properly reset between conversations or nodes, they could accumulate
   - The `Delay` field of -1 triggers special logic that might not reset properly

### Recommended Fixes

1. **Ensure Proper Timer Reset**
   - Always reset delay/timer when entering a new dialogue node
   - Clear voiceover state when transitioning between nodes

2. **Validate Delay Values**
   - Check that delay values are within reasonable bounds (e.g., 0-60 seconds)
   - Clamp invalid values to defaults

3. **Memory Management**
   - Ensure dialogue node structures are properly cleaned up
   - Check for memory leaks in dialogue loading/unloading

4. **Voiceover State Management**
   - Properly track voiceover playback state
   - Reset voiceover state when dialogue ends or is aborted

## Implementation Notes for OdysseyRuntime

The current implementation in `DialogueManager.cs` has a potential bug at line 408:
```csharp
CurrentState.TimeRemaining = _voicePlayer.CurrentTime + 0.5f;
```

This sets the timer to `CurrentTime + 0.5f`, but `CurrentTime` is the current playback position, not the remaining time. This should be:
```csharp
CurrentState.TimeRemaining = _voicePlayer.Duration - _voicePlayer.CurrentTime + 0.5f;
```

Or better yet, wait for the voiceover completion callback instead of using a timer.

