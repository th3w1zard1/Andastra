# Main Menu Complete Implementation Plan - 1:1 Parity with Original

## Overview
This document outlines the complete implementation required to achieve 1:1 parity with the original KOTOR main menu system as reverse-engineered from swkotor.exe and swkotor2.exe using Ghidra.

## Components Required

### 1. Music Player System ✅ (Partially Complete)
- **Status**: IMusicPlayer interface created, MonoGameMusicPlayer implemented
- **Remaining**:
  - Add `CreateMusicPlayer` to IGraphicsBackend interface
  - Implement MonoGameGraphicsBackend.CreateMusicPlayer
  - Implement StrideGraphicsBackend.CreateMusicPlayer (StrideMusicPlayer)
  - Add IMusicPlayer to GameServicesContext
  - Initialize music player in OdysseyGame

### 2. GUI Panel Loading System
- **Status**: KotorGuiManager exists but not integrated into main menu
- **Required**:
  - Load "MAINMENU" GUI file (K1: mainmenu16x12, K2: mainmenu8x6_p or mainmenu_p)
  - Load "RIMS:MAINMENU" RIM file for additional resources
  - Parse GUI panel structure (buttons, labels, backgrounds)
  - Render GUI elements using KotorGuiManager

### 3. Button System
- **Status**: Basic button handling exists but not using GUI system
- **Required**:
  - Set up all buttons from GUI file:
    - BTN_NEWGAME (offset 0x3f0 K1, 0x40c K2)
    - BTN_LOADGAME (offset 0x5b4 K1, 0x5dc K2)
    - BTN_OPTIONS (offset 0x93c K1, 0xb4c K2)
    - BTN_EXIT (offset 0x1084 K1, 0x1554 K2)
    - BTN_MOVIES (offset 0x778 K1, 0x7ac K2)
    - BTN_MUSIC (K2 only, offset 0x97c)
  - Handle button events:
    - Event 0x27: Hover/Enter
    - Event 0x2d: Leave
    - Event 0: Click (mouse down)
    - Event 1: Release (mouse up)
  - Button click handlers:
    - BTN_NEWGAME → Character Creation (NOT directly to module)
    - BTN_LOADGAME → Load Game Menu
    - BTN_OPTIONS → Options Menu
    - BTN_EXIT → Exit Game

### 4. 3D Character Model Rendering
- **Status**: Not implemented
- **Required**:
  - Load "gui3D_room" model
  - Load menu variant model:
    - K1: "mainmenu"
    - K2: "mainmenu01" through "mainmenu05" (select based on game state)
  - Attach model to "camerahook" camera hook
  - Set camera distance to ~22.7 (0x41b5ced9)
  - Set model animation (K1: DAT_0073df6c, K2: "default")
  - Set model scale to 1.0 (0x3f800000)
  - Rotate model continuously (character rotation animation)

### 5. Music Playback
- **Status**: Interface created, needs integration
- **Required**:
  - K1: Play "mus_theme_cult" on main menu entry
  - K2: Play "mus_sion" on main menu entry
  - Loop music continuously
  - Stop music when leaving main menu
  - Resume music when returning to main menu

### 6. Background Rendering
- **Status**: Basic background exists
- **Required**:
  - Load and render LBL_MENUBG (menu background label)
  - Load and render LBL_GAMELOGO (game logo label)
  - Proper layering: Background → 3D Model → GUI Panel → Buttons

## Implementation Steps

### Step 1: Complete Music Player Integration
1. Add `CreateMusicPlayer` method to IGraphicsBackend
2. Implement in MonoGameGraphicsBackend and StrideGraphicsBackend
3. Add IMusicPlayer property to GameServicesContext
4. Initialize music player in OdysseyGame constructor
5. Start music playback when entering main menu state

### Step 2: Integrate GUI System
1. Initialize KotorGuiManager in OdysseyGame
2. Load MAINMENU GUI file on main menu entry
3. Load RIMS:MAINMENU RIM file
4. Parse GUI panel structure
5. Render GUI elements in DrawMainMenu()

### Step 3: Implement Button System
1. Set up button event handlers from GUI file
2. Map button names to actions (BTN_NEWGAME → Character Creation)
3. Handle mouse hover, click, and keyboard navigation
4. Implement button click handlers

### Step 4: Implement 3D Model Rendering
1. Load gui3D_room model
2. Load menu variant model (mainmenu or mainmenu01-05)
3. Set up camera hook system
4. Position and rotate model
5. Render model in DrawMainMenu()

### Step 5: Integration and Testing
1. Integrate all components
2. Test button interactions
3. Test music playback
4. Test 3D model rendering
5. Verify 1:1 parity with original

## Ghidra Function Addresses

### KOTOR 1 (swkotor.exe)
- Main Menu Constructor: FUN_0067c4c0 @ 0x0067c4c0
- Button Setup: FUN_0067ace0 @ 0x0067ace0
- New Game Handler: UndefinedFunction_0067afb0 @ 0x0067afb0
- Music Playback: FUN_005f9af0 @ 0x005f9af0

### KOTOR 2 (swkotor2.exe)
- Main Menu Constructor: FUN_006d2350 @ 0x006d2350
- Button Setup: FUN_006d0790 @ 0x006d0790
- New Game Handler: FUN_006d0b00 @ 0x006d0b00
- Music Playback: FUN_006456b0 @ 0x006456b0

## File Locations

### Music Files
- K1 Main Menu: "mus_theme_cult" (WAV resource)
- K1 Character Creation: "mus_theme_rep" (WAV resource)
- K2 Main Menu: "mus_sion" (WAV resource)

### GUI Files
- K1: "mainmenu16x12" (GUI file)
- K2: "mainmenu8x6_p" or "mainmenu_p" (GUI file)
- RIM: "RIMS:MAINMENU" (RIM file)

### 3D Models
- Room: "gui3D_room" (MDL file)
- K1 Menu Variant: "mainmenu" (MDL file)
- K2 Menu Variants: "mainmenu01" through "mainmenu05" (MDL files)

## Testing Checklist

- [ ] Music plays on main menu entry
- [ ] Music loops continuously
- [ ] GUI panel loads and displays correctly
- [ ] All buttons are visible and positioned correctly
- [ ] Button hover effects work
- [ ] Button click handlers work correctly
- [ ] 3D character model renders
- [ ] Character model rotates
- [ ] New Game button goes to character creation
- [ ] Load Game button shows load game menu
- [ ] Options button shows options menu
- [ ] Exit button exits game
- [ ] Keyboard navigation works
- [ ] Mouse interaction works
- [ ] Visual parity with original (pixel-perfect)

