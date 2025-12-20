# Main Menu Reverse Engineering - Complete Ghidra Analysis

## KOTOR 1 (swkotor.exe) Main Menu System

### Main Menu Initialization
**Function:** `FUN_0067c4c0` @ 0x0067c4c0 (Main Menu Constructor)

**Initialization Sequence:**
1. **Line 62:** Loads "MAINMENU" GUI file
   ```c
   FUN_00406d80(local_1c,"MAINMENU");
   iVar3 = FUN_00408bc0(DAT_007a39e8,local_1c,0xbba,(undefined4 *)0x0);
   ```
2. **Line 65:** If GUI load succeeds, loads "RIMS:MAINMENU" RIM file
   ```c
   FUN_005e5a90(&local_28,"RIMS:MAINMENU");
   FUN_004087c0(DAT_007a39e8,(int *)&local_28);
   ```
3. **Line 71:** Clears menu flag (bit 2 = 0xfd mask)
   ```c
   *(byte *)((int)DAT_007a39e8 + 0x34) = *(byte *)((int)DAT_007a39e8 + 0x34) & 0xfd;
   ```
4. **Line 72:** Calls button setup function
   ```c
   FUN_0067ace0(this);
   ```
5. **Lines 76-99:** Sets up button event handlers
   - Event 0x27 (hover/enter): Various button handlers
   - Event 0x2d (leave): Various button handlers
   - Event 0 (click): FUN_0067b450 (button click handler)
   - Event 1 (release): FUN_0067b470 (button release handler)
6. **Lines 109-120:** Loads 3D room and character model
   ```c
   (**(code **)(**(int **)((int)this + 0x3d0) + 0x70))("gui3D_room",&local_28,local_1c);
   FUN_005e5a90(&local_28,"mainmenu");
   piVar4 = (int *)FUN_00417620((void *)((int)this + 0x3bc),&local_28,-1);
   (**(code **)(*piVar4 + 0x18))(&DAT_0073df6c,0x3f800000,0,0);  // Set animation
   (**(code **)(*piVar4 + 0x14))(0x3f800000);  // Set scale
   (**(code **)(*piVar2 + 0x74))(piVar4,"camerahook",0);  // Attach to camera hook
   (**(code **)(*piVar2 + 0x44))(0x41b5ced9);  // Set camera distance (~22.7)
   ```

### Button Setup
**Function:** `FUN_0067ace0` @ 0x0067ace0

**Buttons Configured:**
- **Line 15:** Loads "mainmenu" GUI panel
- **Line 38-40:** BTN_NEWGAME (offset 0x3f0)
- **Line 43-45:** BTN_LOADGAME (offset 0x5b4)
- **Line 48-50:** BTN_MOVIES (offset 0x778)
- **Line 53-55:** BTN_OPTIONS (offset 0x93c)
- **Line 23-25:** BTN_EXIT (offset 0x1084)
- **Line 33-35:** LBL_3DVIEW (offset 0x360) - 3D view label for character model
- **Line 18-20:** LB_MODULES (offset 0x64) - Module list (hidden)
- **Line 28-30:** BTN_WARP (offset 0x1248) - Warp button (hidden)
- **Line 58-60:** LBL_NEWCONTENT (offset 0xcc4) - New content label (hidden)
- **Line 63-65:** LBL_GAMELOGO (offset 0xe04) - Game logo label
- **Line 68-70:** LBL_MENUBG (offset 0xf44) - Menu background label

### New Game Button Handler
**Function:** `UndefinedFunction_0067afb0` @ 0x0067afb0 (called from event handler 0x67afb0)

**Behavior:**
- **Line 26:** Sets module name to "END_M01AA" (Endar Spire)
- **Line 28:** Prepares "MODULES:" path
- **Lines 34-40:** Checks if module exists, if not sets to "END_M01AA"
- **Line 56:** Creates module loader object
- **Line 57:** Calls transition function
- **Line 58:** Sets menu state flag (0x400)

**Note:** This function loads the module directly - it does NOT go to character creation first. However, the original game may have character creation happen before this, or this may be a warp function. Need to verify the actual New Game flow.

### Music System
**Function:** `FUN_005f9af0` @ 0x005f9af0

**Behavior:**
- **Line 20:** If param_1 == 0, uses "mus_theme_rep" (character creation music)
- **Line 20:** Otherwise uses "mus_theme_cult" (main menu music)
- **Line 64:** Calls music playback function `FUN_005d5b10` with music file path

**Music Files:**
- Main Menu: "mus_theme_cult"
- Character Creation: "mus_theme_rep"

## KOTOR 2 (swkotor2.exe) Main Menu System

### Main Menu Initialization
**Function:** `FUN_006d2350` @ 0x006d2350 (Main Menu Constructor)

**Initialization Sequence:**
1. **Line 73:** Loads "MAINMENU" GUI file
   ```c
   FUN_00406e90(local_6c + 5,"MAINMENU");
   iVar3 = FUN_00408df0(DAT_008283c0,local_6c + 5,0xbba,(undefined4 *)0x0);
   ```
2. **Line 76:** If GUI load succeeds, loads "RIMS:MAINMENU" RIM file
   ```c
   FUN_00630a90(local_6c,"RIMS:MAINMENU");
   FUN_004089f0(DAT_008283c0,local_6c);
   ```
3. **Line 82:** Clears menu flag (bit 2 = 0xfd mask)
   ```c
   *(byte *)((int)DAT_008283c0 + 0x34) = *(byte *)((int)DAT_008283c0 + 0x34) & 0xfd;
   ```
4. **Line 83:** Calls button setup function
   ```c
   FUN_006d0790(this);
   ```
5. **Lines 89-116:** Sets up button event handlers
   - Event 0x27 (hover/enter): Various button handlers
   - Event 0x2d (leave): Various button handlers
   - Event 0 (click): FUN_006d1160 (button click handler)
   - Event 1 (release): FUN_005ee1a0 (button release handler)
6. **Lines 127-156:** Loads 3D room and selects menu variant
   ```c
   (**(code **)(*piVar7 + 0x70))("gui3D_room",local_6c + 2,local_6c + 5);
   // Selects menu variant based on cVar1 (0-4):
   // 0: "mainmenu01" (default)
   // 1: "mainmenu02"
   // 2: "mainmenu03"
   // 3: "mainmenu04"
   // 4: "mainmenu05"
   FUN_00630d10(local_6c,pcVar9);
   piVar4 = (int *)FUN_00416e60((void *)((int)this + 0x3d8),local_6c,-1);
   (**(code **)(*piVar4 + 0x18))("default",0x3f800000,0,0);  // Set animation
   (**(code **)(*piVar4 + 0x14))(0x3f800000);  // Set scale
   (**(code **)(*piVar2 + 0x74))(piVar4,"camerahook",0);  // Attach to camera hook
   (**(code **)(*piVar2 + 0x44))(0x41b5ced9);  // Set camera distance (~22.7)
   ```
7. **Lines 157-200:** Special handling for mainmenu05 variant
   - Creates character model object
   - Loads character appearance from save/globals
   - Attaches character model to 3D view
   - Sets character animation

### Button Setup
**Function:** `FUN_006d0790` @ 0x006d0790

**Buttons Configured:**
- **Line 20:** Tries to load "mainmenu8x6_p" first, falls back to "mainmenu_p"
- **Line 59-61:** BTN_NEWGAME (offset 0x40c)
- **Line 64-66:** BTN_LOADGAME (offset 0x5dc)
- **Line 69-71:** BTN_MOVIES (offset 0x7ac)
- **Line 74-76:** BTN_MUSIC (offset 0x97c) - K2 only
- **Line 79-81:** BTN_OPTIONS (offset 0xb4c)
- **Line 44-46:** BTN_EXIT (offset 0x1554)
- **Line 54-56:** LBL_3DVIEW (offset 0x378) - 3D view label
- **Line 39-41:** LB_MODULES (offset 0x6c) - Module list (hidden)
- **Line 49-51:** BTN_WARP (offset 0x1724) - Warp button (hidden)
- **Line 84-86:** LBL_NEWCONTENT (offset 0xeec) - New content label (hidden)
- **Line 89-91:** LBL_GAMELOGO (offset 0x1034) - Game logo label
- **Line 94-96:** LBL_MENUBG (offset 0x117c) - Menu background label

### New Game Button Handler
**Function:** `FUN_006d0b00` @ 0x006d0b00 (called from event handler 0x6d0b00)

**Behavior:**
- **Line 29:** Sets module name to "001ebo" (Prologue/Ebon Hawk)
- **Line 37:** Prepares "MODULES:" path
- **Lines 43-49:** Checks if module exists, if not sets to "001ebo"
- **Line 62:** Creates module loader object
- **Line 65:** Calls transition function
- **Line 67:** Sets menu state flag (0x200)

**Note:** This function also loads the module directly. Need to verify if character creation happens before this or if this is called after character creation completes.

### Music System
**Function:** `FUN_006456b0` @ 0x006456b0

**Behavior:**
- **Line 21:** Uses "mus_sion" (main menu music)
- **Line 60:** Calls music playback function `FUN_00621730` with music file path

**Music Files:**
- Main Menu: "mus_sion"

## 3D Character Model on Main Menu

### KOTOR 1
- **Model:** "mainmenu" (loaded from "gui3D_room")
- **Animation:** DAT_0073df6c (default animation)
- **Scale:** 1.0 (0x3f800000)
- **Camera Hook:** "camerahook"
- **Camera Distance:** ~22.7 (0x41b5ced9)

### KOTOR 2
- **Model:** Menu variant (mainmenu01-05) loaded from "gui3D_room"
- **Animation:** "default"
- **Scale:** 1.0 (0x3f800000)
- **Camera Hook:** "camerahook"
- **Camera Distance:** ~22.7 (0x41b5ced9)
- **Special:** mainmenu05 variant creates actual character model from save/globals

## Button Event System

### Event Types
- **0x27:** Hover/Enter event
- **0x2d:** Leave event
- **0:** Click event (mouse down)
- **1:** Release event (mouse up)

### Button Click Handlers
- **K1:** FUN_0067b450 (click), FUN_0067b470 (release)
- **K2:** FUN_006d1160 (click), FUN_005ee1a0 (release)

## Implementation Requirements

1. **GUI Loading:**
   - Load "MAINMENU" GUI file
   - Load "RIMS:MAINMENU" RIM file
   - Load appropriate GUI panel ("mainmenu" for K1, "mainmenu8x6_p" or "mainmenu_p" for K2)

2. **3D Model Rendering:**
   - Load "gui3D_room" model
   - Load menu variant model ("mainmenu" for K1, "mainmenu01-05" for K2)
   - Attach model to "camerahook" camera hook
   - Set camera distance to ~22.7
   - Animate model rotation

3. **Music Playback:**
   - K1: Play "mus_theme_cult" on main menu
   - K2: Play "mus_sion" on main menu
   - Loop music continuously

4. **Button Setup:**
   - Configure all buttons (BTN_NEWGAME, BTN_LOADGAME, BTN_OPTIONS, BTN_EXIT, etc.)
   - Set up event handlers for hover, click, release
   - Handle button visibility (hide LB_MODULES, BTN_WARP, LBL_NEWCONTENT)

5. **Button Click Handlers:**
   - BTN_NEWGAME: Transition to character creation (NOT directly to module)
   - BTN_LOADGAME: Show load game menu
   - BTN_OPTIONS: Show options menu
   - BTN_EXIT: Exit game

