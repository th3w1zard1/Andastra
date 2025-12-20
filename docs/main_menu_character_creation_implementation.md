# Main Menu and Character Creation Implementation Plan

## Ghidra Reverse Engineering Findings

### KOTOR 1 (swkotor.exe)

**New Game Button Handler:**
- Function: `FUN_0067c4c0` calls `UndefinedFunction_0067afb0` @ 0x0067afb0
- Module Loading: Line 26 uses `"END_M01AA"` (Endar Spire)
- String Reference: `"END_M01AA"` @ 0x00752f58
- Cross-references: 0x0067b01b, 0x0067b0b9

**Main Menu:**
- GUI Panel: `"mainmenu16x12"` (K1 uses single panel, no variants)
- Music: `"mus_theme_cult"` (from vendor/reone code)

**Character Creation:**
- GUI Panel: `"maincg"` (character generation)
- Music: `"mus_theme_rep"` (from vendor/reone code)
- Load Screen: `"load_chargen"`

### KOTOR 2 (swkotor2.exe)

**New Game Button Handler:**
- Function: `FUN_006d0b00` @ 0x006d0b00
- Module Loading: Line 29 uses `"001ebo"` (Prologue/Ebon Hawk)
- String Reference: `"001ebo"` @ 0x007cc028
- Cross-references: 0x006d0b7d, 0x006d0c5e

**Main Menu:**
- GUI Panel: `"mainmenu8x6_p"` (K2 uses 8x6 panel)
- Music: `"mus_sion"` (from vendor/reone code)
- Variants: mainmenu01-05 (K2 has multiple menu variants)

**Character Creation:**
- GUI Panel: `"maincg"` (character generation)
- Music: `"mus_main"` (from vendor/reone code)
- Load Screen: `"load_default"`

## Current Implementation Status

### ✅ Implemented
- Basic main menu rendering (text-based, not GUI-based)
- Module loading system
- Game session management

### ❌ Missing
1. **Character Creation Screen** - Completely missing
   - Should show after "New Game" button click
   - Should allow class selection, attributes, skills, feats, portrait, name
   - Should create player character before loading module

2. **Main Menu Music** - Not implemented
   - K1: `"mus_theme_cult"`
   - K2: `"mus_sion"`

3. **Character Creation Music** - Not implemented
   - K1: `"mus_theme_rep"`
   - K2: `"mus_main"`

4. **Proper GUI Rendering** - Using text instead of GUI files
   - K1: Should load `"mainmenu16x12"` GUI panel
   - K2: Should load `"mainmenu8x6_p"` GUI panel
   - Character creation: Should load `"maincg"` GUI panel

5. **New Game Flow** - Incorrect
   - Current: Main Menu → Directly to Module
   - Should be: Main Menu → Character Creation → Module

## Implementation Plan

### Phase 1: Fix Module Names
- [x] Verify K1 uses "end_m01aa" (confirmed via Ghidra)
- [ ] Verify if "end_m01ae" exists (user mentioned it, but not found in Ghidra)

### Phase 2: Implement Character Creation
- [ ] Create `CharacterCreationScreen` class
- [ ] Load `"maincg"` GUI panel
- [ ] Implement class selection
- [ ] Implement attribute allocation
- [ ] Implement skill points
- [ ] Implement feat selection
- [ ] Implement portrait selection
- [ ] Implement name entry
- [ ] Create player character entity
- [ ] Transition to module after completion

### Phase 3: Implement Music System
- [ ] Load and play main menu music
- [ ] Load and play character creation music
- [ ] Stop music when transitioning screens
- [ ] Support music streaming from game files

### Phase 4: Implement Proper GUI Rendering
- [ ] Load GUI files (GUI format parser)
- [ ] Render GUI panels instead of text
- [ ] Handle GUI button clicks
- [ ] Support GUI animations and transitions

### Phase 5: Fix New Game Flow
- [ ] Main Menu "New Game" → Character Creation
- [ ] Character Creation "Finish" → Module Load
- [ ] Pass created character to module loader
- [ ] Set player entity in game session

## Module Loading Flow (Correct)

```
Main Menu
  ↓ [New Game Button]
Character Creation
  ↓ [Finish Button]
Module Load (with created character)
  ↓
Game Play
```

## Current Flow (Incorrect)

```
Main Menu
  ↓ [New Game Button]
Module Load (no character!)
  ↓
Game Play (broken - no player!)
```

