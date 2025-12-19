# TimeManager Ghidra Verification Results

## Executive Summary

This document contains the actual Ghidra MCP verification results for all pending TimeManager verification items. All verifications were performed using Ghidra MCP tools on the actual game executables.

---

## 1. Aurora: CWorldTimer::AddWorldTimes @ 0x140596b40

### ✅ VERIFICATION COMPLETE

#### Function Analysis

**Function Signature:**
```c
uint __thiscall CWorldTimer::AddWorldTimes(
    CWorldTimer *this,
    uint param_1,  // Current days
    uint param_2,  // Current milliseconds
    uint param_3,  // Delta days
    uint param_4,  // Delta milliseconds
    uint *param_5, // Output: Result days
    uint *param_6  // Output: Result milliseconds
)
```

**Decompiled Code:**
```c
uint __thiscall CWorldTimer::AddWorldTimes(...)
{
    if ((param_2 < *(uint *)(this + 0x30)) && (param_4 < *(uint *)(this + 0x30))) {
        *param_5 = param_1 + param_3;  // Add days
        *param_6 = param_2 + param_4;  // Add milliseconds
        if (*(uint *)(this + 0x30) <= param_2 + param_4) {  // Check rollover
            *param_5 = *param_5 + 1;   // Increment day
            *param_6 = *param_6 - *(int *)(this + 0x30);  // Subtract milliseconds per day
        }
        return 0;
    }
    return 0xfffffffe;  // Error: invalid input
}
```

#### Key Findings

1. **AddWorldTimes is NOT the main tick function**
   - It's a helper function that adds time deltas to world time
   - It handles rollover correctly (milliseconds → days)
   - It's called from various systems (UpdateEffectList, AddEventDeltaTime, etc.) to schedule future events

2. **Time advancement pattern**
   - `GetWorldTime()` reads current world time (days + milliseconds)
   - `AddWorldTimes()` adds a delta to world time
   - The actual time advancement happens elsewhere (likely in MainLoop or a timer update function)

3. **Call sites analysis**
   - **UpdateEffectList @ 0x14049dae0**: Calls `AddWorldTimes(this_01, param_1, param_2, 0, 5000, ...)` - adds 5000ms (5 seconds) for effect expiry
   - **AddEventDeltaTime @ 0x1405570b0**: Calls `AddWorldTimes(..., local_40[0], local_44, param_1, param_2, ...)` - adds delta time for AI events
   - **ApplyEffect @ 0x14048e350**: Calls `AddWorldTimes(...)` to set effect expiry time
   - **SetDialogDelay @ 0x14049aec0**: Calls `AddWorldTimes(...)` to schedule dialog delay

4. **Main loop analysis**
   - `Update()` @ 0x1400448b0 calls `MainLoop()` @ 0x14055bcb0
   - `MainLoop()` calls `CServerExoAppInternal::MainLoop()` @ 0x140567380
   - `AIUpdate()` @ 0x14035aed0 calls `GetWorldTime()` and passes it to `UpdateEffectList()`
   - This suggests the main loop gets current world time and passes it to update functions

#### Comparison with BaseTimeManager.Tick()

**❌ MISMATCH FOUND**: `AddWorldTimes` does NOT match `Tick()` logic:

| Aspect | BaseTimeManager.Tick() | CWorldTimer::AddWorldTimes |
|--------|------------------------|---------------------------|
| **Purpose** | Main simulation tick function | Helper function to add time deltas |
| **Fixed timestep** | Advances by FixedTimestep (16.67ms) | Takes arbitrary delta as parameter |
| **Accumulator pattern** | Checks `_accumulator >= FixedTimestep` | No accumulator check |
| **Simulation time** | Advances `_simulationTime` | Does not advance simulation time |
| **Game time advancement** | Advances game time at 1:1 ratio | Adds arbitrary delta to world time |
| **Pause handling** | Only ticks if not paused (checked in Update) | No pause check (caller must check) |

#### Conclusion

**AddWorldTimes is a helper function, not the main tick function.** The actual time advancement mechanism in Aurora is different from our base class pattern:

1. Aurora uses `GetWorldTime()` to read current world time
2. Aurora uses `AddWorldTimes()` to add time deltas (for scheduling events, effects, etc.)
3. The actual time advancement (from real time to world time) happens elsewhere - likely in a timer update function that advances world time based on elapsed real time

**Recommendation**: We need to find the actual time advancement function that:
- Reads elapsed real time
- Advances world time based on real time (with time scale and pause checks)
- This would be the equivalent of our `Tick()` function

---

## 2. Odyssey: Frame Timing Functions

### ✅ VERIFICATION COMPLETE

#### String Analysis

**Found strings:**
- `"frameStart"` @ 0x007ba698 (string constant, not function)
- `"frameEnd"` @ 0x007ba668 (string constant, not function)
- `"frameStartkey"` @ 0x007ba688
- `"frameEndkey"` @ 0x007ba65c
- `"frameStartbezierkey"` @ 0x007ba674
- `"frameEndbezierkey"` @ 0x007ba648

#### Key Findings

1. **frameStart/frameEnd are NOT function addresses**
   - They are string constants in the `.rdata` section
   - They are used as keys in a particle system configuration parser
   - Function `FUN_0047bc30` @ 0x0047bc30 references `frameStart` string
   - This function is a particle system configuration parser, not a frame timing function

2. **No frame timing markers found**
   - The documented addresses (0x007ba698, 0x007ba668) are string data, not executable code
   - No functions were found that use these as frame timing markers
   - The Odyssey documentation appears to be incorrect about these being frame timing functions

#### Conclusion

**❌ MISMATCH FOUND**: The documented frame timing addresses are incorrect:

- `frameStart @ 0x007ba698` is a **string constant**, not a function
- `frameEnd @ 0x007ba668` is a **string constant**, not a function
- These strings are used in particle system configuration, not frame timing

**Recommendation**: 
1. Search for actual frame timing functions in Odyssey (may use different naming)
2. Update documentation to remove incorrect frame timing addresses
3. The `Update()` override in `OdysseyTimeManager` should not reference these addresses

---

## 3. Eclipse: Unreal Engine 3 Integration

### ⚠️ VERIFICATION PENDING

**Status**: Executables are available in Ghidra project:
- `daorigins.exe` ✅ Available
- `DragonAge2.exe` ✅ Available
- `MassEffect.exe` ✅ Available
- `MassEffect2.exe` ✅ Available

**Next Steps**: 
1. Search for time management functions in Eclipse executables
2. Verify Unreal Engine 3 time system integration
3. Compare with base class accumulator pattern

---

## 4. Infinity: GAM File Format

### ⚠️ VERIFICATION PENDING

**Status**: Executables need to be loaded in Ghidra project

**Next Steps**:
1. Load Infinity Engine executables (BaldurGate.exe, IcewindDale.exe, PlanescapeTorment.exe)
2. Search for GAM file format functions
3. Verify game time storage structure
4. Compare with base class game time advancement

---

## Summary of Findings

### ✅ Completed Verifications

1. **Aurora AddWorldTimes**: 
   - ✅ Function decompiled and analyzed
   - ❌ Does NOT match base class Tick() logic (it's a helper function, not the main tick)
   - ⚠️ Need to find actual time advancement function

2. **Odyssey Frame Timing**:
   - ✅ Strings found and analyzed
   - ❌ Documented addresses are incorrect (strings, not functions)
   - ⚠️ Need to find actual frame timing functions

### ⚠️ Pending Verifications

3. **Eclipse Unreal Engine 3**: Executables available, verification in progress
4. **Infinity GAM Format**: Executables need to be loaded

---

## Recommendations

### Immediate Actions

1. **Aurora**: Search for the actual time advancement function that:
   - Updates world time based on elapsed real time
   - Applies time scale and pause checks
   - This would be called from MainLoop or a timer update function

2. **Odyssey**: 
   - Remove incorrect frame timing addresses from documentation
   - Search for actual frame timing functions (if they exist)
   - Update `OdysseyTimeManager` documentation

3. **Eclipse**: Begin verification of Unreal Engine 3 integration

4. **Infinity**: Load executables and begin GAM file format verification

### Code Updates Needed

1. **AuroraTimeManager.cs**: Update documentation to clarify that `AddWorldTimes` is a helper function, not the main tick
2. **OdysseyTimeManager.cs**: Remove incorrect frame timing addresses from documentation
3. **BaseTimeManager.cs**: No changes needed - base class logic is correct

---

## Verification Methodology

All verifications were performed using Ghidra MCP tools:
- `get-decompilation`: Decompiled functions to analyze logic
- `find-cross-references`: Found callers and callees
- `search-strings-regex`: Searched for string references
- `get-functions-by-similarity`: Searched for related functions

All findings are based on actual reverse engineering of the game executables.

