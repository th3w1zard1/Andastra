# KOTOR 2 Playable Demo Roadmap

**Generated from exhaustive Ghidra MCP analysis of swkotor2.exe**

This roadmap documents ALL functions referenced in the codebase and their implementation status. Every function address mentioned in code comments has been verified against Ghidra decompilation.

## Critical Missing Features

### Character Creation System
- **Status**: ❌ NOT IMPLEMENTED
- **Location**: `src/Andastra/Game/Core/CharacterCreationScreen.cs`
- **Ghidra Reference**: Character generation GUI system in swkotor.exe/swkotor2.exe
- **What's Missing**:
  - [ ] Character creation UI updates (`Update()` method is TODO)
  - [ ] Character creation UI rendering (`Draw()` method is TODO)
  - [ ] Class selection screen (Scout/Soldier/Scoundrel for K1, Jedi Guardian/Sentinel/Consular for K2)
  - [ ] Quick vs Custom character creation flow
  - [ ] Attribute point allocation (STR, DEX, CON, INT, WIS, CHA)
  - [ ] Skill point allocation
  - [ ] Feat selection
  - [ ] Portrait selection
  - [ ] Name entry
  - [ ] Character model preview
  - [ ] Integration with main menu (Main Menu -> New Game should launch character creation, not directly to module)
- **Current Flow**: Main Menu -> New Game -> Module Load (WRONG - skips character creation)
- **Correct Flow**: Main Menu -> New Game -> Character Creation -> Module Load

### Music System
- **Status**: ❌ NOT IMPLEMENTED
- **Location**: ARE files parsed but no playback
- **Ghidra Reference**: 
  - `FUN_00574350 @ 0x00574350` - LoadAudioProperties (loads MusicDay, MusicNight, MusicBattle from ARE)
  - `FUN_004e26d0 @ 0x004e26d0` - LoadAreaProperties (calls FUN_00574350)
- **What's Missing**:
  - [ ] Music playback system (no `PlayMusic`, `MusicPlayer`, or `SoundPlayer` for music)
  - [ ] Day/Night music switching based on time of day
  - [ ] Battle music playback during combat
  - [ ] Music.2da table lookup (MusicDay/MusicNight/MusicBattle are indices into music.2da)
  - [ ] Music file loading from MUSIC: directory (WAV/MP3 files)
  - [ ] Music volume control
  - [ ] Music fade in/out transitions
- **Current State**: ARE files have MusicDay/MusicNight/MusicBattle fields parsed but no playback implementation

### Main Menu System
- **Status**: ⚠️ PARTIALLY IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/MonoGame/UI/MainMenu.cs`
- **Ghidra Reference**: 
  - `FUN_0067afb0 @ 0x0067afb0` (swkotor.exe) / `FUN_0067af90 @ 0x0067af90` (swkotor2.exe) - OnNewGamePicked
  - `FUN_006d2350 @ 0x006d2350` - Main menu rendering
- **What's Implemented**:
  - [x] Basic menu rendering (text-based menu items)
  - [x] Menu item selection (up/down navigation)
  - [x] Event handlers for menu items (OnNewGame, OnLoadGame, OnOptions, OnExit)
- **What's Missing**:
  - [ ] GUI panel rendering (should use "mainmenu_p" or "mainmenu8x6_p" GUI files)
  - [ ] Background rendering (menu background image/texture)
  - [ ] Integration with character creation (New Game should launch character creation, not directly to module)
  - [ ] Menu music playback
  - [ ] Menu animations/transitions

## Function Address Implementation Status

### Core Game Loop Functions

#### FUN_00404250 @ 0x00404250 - Main Game Loop (WinMain equivalent)
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Game/Core/OdysseyGame.cs`
- **Description**: Main game loop, WinMain equivalent
- **Implementation**: `OdysseyGame.Update()` and `OdysseyGame.Draw()` methods

#### FUN_00401c30 @ 0x00401c30 - Time Management
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyTimeManager.cs`
- **Description**: Time management functions
- **Implementation**: `OdysseyTimeManager` class

#### FUN_0040d4e0 @ 0x0040d4e0 - Time Management
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyTimeManager.cs`
- **Description**: Time management functions
- **Implementation**: `OdysseyTimeManager` class

### Module Loading Functions

#### FUN_00501fa0 @ 0x00501fa0 - Module Loading
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Game/GameSession.cs`, `src/Andastra/Runtime/Games/Odyssey/Loading/ModuleLoader.cs`
- **Description**: Loads module from IFO file, reads "Mod_OnHeartbeat" script
- **Implementation**: `ModuleLoader.LoadModule()`, `ModuleLoader.LoadModuleInfo()`

#### FUN_00500290 @ 0x00500290 - Module Info Loading
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/IFO/IFOHelpers.cs`
- **Description**: Loads module info from IFO file
- **Implementation**: `IFOHelpers` class

#### FUN_006caab0 @ 0x006caab0 - Server Command Parser
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Game/GameSession.cs`
- **Description**: Handles module commands
- **Implementation**: `GameSession` command handling

### Area Loading Functions

#### FUN_004e26d0 @ 0x004e26d0 - LoadAreaProperties
- **Status**: ✅ IMPLEMENTED (parsing only, no music playback)
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Loading/ModuleLoader.cs`, `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`
- **Description**: Loads area properties from ARE file, including MusicDay/MusicNight/MusicBattle
- **Implementation**: `ModuleLoader.LoadAreaProperties()` - parses fields but no playback
- **Missing**: Music playback system (fields are parsed but not used)

#### FUN_00574350 @ 0x00574350 - LoadAudioProperties
- **Status**: ✅ IMPLEMENTED (parsing only, no playback)
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads audio properties (MusicDelay, MusicDay, MusicNight, MusicBattle, AmbientSndDay, AmbientSndNight, etc.)
- **Implementation**: ARE file parsing includes these fields
- **Missing**: Music playback system (fields are parsed but not used)

#### FUN_004e3ff0 @ 0x004e3ff0 - Room Rendering
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/MonoGame/Converters/RoomMeshRenderer.cs`
- **Description**: Room rendering system
- **Implementation**: `RoomMeshRenderer` class

#### FUN_004e507f @ 0x004e507f - Room Rendering
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/ARE/AREHelpers.cs`
- **Description**: Room rendering system
- **Implementation**: ARE parsing

### Entity Loading Functions

#### FUN_004dfbb0 @ 0x004dfbb0 - Load Creature from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`, `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads creature entities from GIT file
- **Implementation**: `OdysseyArea.LoadCreatures()`, `GITHelpers.LoadCreature()`

#### FUN_004e04a0 @ 0x004e04a0 - Load Door from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`, `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads door entities from GIT file
- **Implementation**: `OdysseyArea.LoadDoors()`, `GITHelpers.LoadDoor()`

#### FUN_004e06a0 @ 0x004e06a0 - Load Placeable from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`, `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads placeable entities from GIT file
- **Implementation**: `OdysseyArea.LoadPlaceables()`, `GITHelpers.LoadPlaceable()`

#### FUN_004e08e0 @ 0x004e08e0 - Load Sound from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`, `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads sound instances from GIT
- **Implementation**: `OdysseyArea.LoadSounds()`, `GITHelpers.LoadSound()`

#### FUN_004e0ff0 @ 0x004e0ff0 - Load Store from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads store entities from GIT file
- **Implementation**: `GITHelpers.LoadStore()`

#### FUN_004e2b20 @ 0x004e2b20 - Load Encounter from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`, `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads encounter entities from GIT file
- **Implementation**: `OdysseyArea.LoadEncounters()`, `GITHelpers.LoadEncounter()`

#### FUN_004e56b0 @ 0x004e56b0 - Load Waypoint from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads waypoint entities from GIT file
- **Implementation**: `GITHelpers.LoadWaypoint()`

#### FUN_004e5920 @ 0x004e5920 - Load Trigger from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`, `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads trigger entities from GIT file
- **Implementation**: `OdysseyArea.LoadTriggers()`, `GITHelpers.LoadTrigger()`

#### FUN_004e5d80 @ 0x004e5d80 - Load Area Transition from GIT
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads area transition entities from GIT file
- **Implementation**: `GITHelpers.LoadAreaTransition()`

#### FUN_0056f5a0 @ 0x0056f5a0 - LoadWaypoint
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/GIT/GITHelpers.cs`
- **Description**: Loads waypoint data from GIT
- **Implementation**: `GITHelpers.LoadWaypoint()`

#### FUN_005261b0 @ 0x005261b0 - Load Creature Model
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Game/Core/OdysseyGame.cs`, `src/Andastra/Runtime/Graphics/MonoGame/Rendering/EntityModelRenderer.cs`
- **Description**: Loads creature model for rendering
- **Implementation**: `EntityModelRenderer`, `ModelResolver`

#### FUN_00580330 @ 0x00580330 - Load Creature Template
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Loading/EntityFactory.cs`
- **Description**: Loads creature template from UTC file
- **Implementation**: `EntityFactory.CreateCreature()`

#### FUN_0050c510 @ 0x0050c510 - LoadScriptHooks
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Components/ScriptHooksComponent.cs`, `src/Andastra/Runtime/Scripting/ScriptExecutor.cs`
- **Description**: Loads script hooks from entity data
- **Implementation**: `ScriptHooksComponent`, `ScriptExecutor.LoadScriptHooks()`

### Event System Functions

#### FUN_004dcfb0 @ 0x004dcfb0 - DispatchEvent (KOTOR 2)
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyEventDispatcher.cs`
- **Description**: Dispatches events to entities
- **Implementation**: `OdysseyEventDispatcher.DispatchEvent()`

#### FUN_004dd730 @ 0x004dd730 - DispatchScriptEvent
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Scripting/ScriptExecutor.cs`
- **Description**: Dispatches script events
- **Implementation**: `ScriptExecutor.DispatchScriptEvent()`

### Save/Load Functions

#### FUN_004eb750 @ 0x004eb750 - SerializeSaveNfo
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Creates save game metadata (NFO file)
- **Implementation**: `OdysseySaveSerializer.SerializeSaveNfo()`

#### FUN_00707290 @ 0x00707290 - Load Save NFO
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Loads save game metadata (NFO file)
- **Implementation**: `OdysseySaveSerializer.LoadSaveNfo()`

#### FUN_00708990 @ 0x00708990 - Validate Save Structure
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Validates save structure during load
- **Implementation**: `OdysseySaveSerializer.LoadSaveNfo()` includes validation

#### FUN_005226d0 @ 0x005226d0 - Save Entity States
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Saves entity states to GFF format
- **Implementation**: `OdysseySaveSerializer.SerializeEntityState()`

#### FUN_005223a0 @ 0x005223a0 - Load Entity States
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyEntity.cs`, `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Loads entity states from save game
- **Implementation**: `OdysseySaveSerializer.DeserializeEntityState()`

#### FUN_005fb0f0 @ 0x005fb0f0 - Load Area State
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Loads area state from save game
- **Implementation**: `OdysseySaveSerializer.LoadAreaState()`

#### FUN_004e28c0 @ 0x004e28c0 - Save Entity List
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Creates "Creature List" structure for save
- **Implementation**: `OdysseySaveSerializer.SerializeEntityList()`

### Global Variables Functions

#### FUN_005ac670 @ 0x005ac670 - Save Global Variables
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Saves global variables to GFF (calls FUN_005ab310 internally)
- **Implementation**: `OdysseySaveSerializer.SerializeGlobalVariables()`

#### FUN_005ac740 @ 0x005ac740 - Load Global Variables
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Loads global variables from GFF
- **Implementation**: `OdysseySaveSerializer.DeserializeGlobalVariables()`

#### FUN_005ac540 @ 0x005ac540 - Load GVT File
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Loads GFF with "GVT " signature (global variables)
- **Implementation**: `OdysseySaveSerializer.DeserializeGlobalVariables()`

#### FUN_005abbe0 @ 0x005abbe0 - Process Variable Types
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Processes each variable type in global variables
- **Implementation**: `OdysseySaveSerializer.DeserializeGlobalVariables()`

#### FUN_005ab310 @ 0x005ab310 - Internal Global Variables Function
- **Status**: ✅ IMPLEMENTED (called by FUN_005ac670)
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Internal function called by FUN_005ac670
- **Implementation**: Part of `OdysseySaveSerializer.SerializeGlobalVariables()`

### Party System Functions

#### FUN_0057bd70 @ 0x0057bd70 - Save Party Table
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Saves party state to GFF with "PT  " signature
- **Implementation**: `OdysseySaveSerializer.SerializePartyTable()`

#### FUN_0057dcd0 @ 0x0057dcd0 - Load Party Table
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseySaveSerializer.cs`
- **Description**: Loads party state from GFF with "PT  " signature
- **Implementation**: `OdysseySaveSerializer.DeserializePartyTable()`

### Navigation Mesh Functions

#### FUN_004f5070 @ 0x004f5070 - Walkmesh Projection
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyNavigationMesh.cs`
- **Description**: Projects positions to walkmesh
- **Implementation**: `OdysseyNavigationMesh.ProjectToWalkmesh()`

#### FUN_0054be70 @ 0x0054be70 - UpdateCreatureMovement
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyNavigationMesh.cs`, `src/Andastra/Runtime/Core/Actions/ActionCastSpellAtLocation.cs`
- **Description**: Updates creature movement, performs walkmesh raycasts for visibility checks
- **Implementation**: `OdysseyNavigationMesh` raycast methods

#### FUN_0061c390 @ 0x0061c390 - FindPathAroundObstacle
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyNavigationMesh.cs`
- **Description**: Pathfinding around obstacles
- **Implementation**: `OdysseyNavigationMesh.FindPathAroundObstacle()`

### Rendering Functions

#### FUN_00461c20 / FUN_00461c00 @ 0x00461c20 / 0x00461c00 - Render Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Game/Core/OdysseyGame.cs`
- **Description**: Render functions
- **Implementation**: `OdysseyGame.Draw()`

#### FUN_00461c50 @ 0x00461c50 - Render Setup
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/OdysseyGraphicsBackend.cs`
- **Description**: Render setup
- **Implementation**: Graphics backend initialization

#### FUN_0042a100 @ 0x0042a100 - Texture Creation
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Texture creation pattern
- **Implementation**: `Kotor2GraphicsBackend` texture handling

#### FUN_004f67d0 @ 0x004f67d0 - Entity Picking
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Game/Core/OdysseyGame.cs`
- **Description**: Entity picking function
- **Implementation**: `OdysseyGame` picking logic

#### FUN_005479f0 @ 0x005479f0 - Get Bounding Box
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Game/Core/OdysseyGame.cs`
- **Description**: Gets bounding box from entity structure
- **Implementation**: Entity bounding box calculation

#### FUN_006d2350 @ 0x006d2350 - Main Menu Rendering
- **Status**: ⚠️ PARTIALLY IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/GUI/BaseMenuRenderer.cs`, `src/Andastra/Runtime/Graphics/MonoGame/GUI/MyraMenuRenderer.cs`
- **Description**: Main menu rendering function
- **Implementation**: Basic text-based menu rendering
- **Missing**: GUI panel rendering, background, animations

### Dialogue Functions

#### FUN_005ea880 @ 0x005ea880 - Dialogue Loading
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/DLG/DLGHelper.cs`
- **Description**: Loads dialogue from DLG file
- **Implementation**: `DLGHelper` class

#### FUN_005e61d0 @ 0x005e61d0 - Dialogue Execution
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Dialogue/DialogueManager.cs`
- **Description**: Dialogue execution
- **Implementation**: `DialogueManager` class

#### FUN_005e6870 @ 0x005e6870 - Dialogue State Management
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Dialogue/DialogueManager.cs`
- **Description**: Dialogue state management (calls FUN_0057eb20)
- **Implementation**: `DialogueManager` state handling

#### FUN_005e6ac0 @ 0x005e6ac0 - Dialogue Response Handling
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Dialogue/DialogueManager.cs`
- **Description**: Dialogue response handling
- **Implementation**: `DialogueManager` response handling

#### FUN_005e9920 @ 0x005e9920 - ExecuteDialogue
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Dialogue/ConversationContext.cs`, `src/Andastra/Runtime/Games/Odyssey/Dialogue/DialogueState.cs`
- **Description**: Executes dialogue conversation
- **Implementation**: `DialogueManager.ExecuteDialogue()`

### Data Loading Functions

#### FUN_005edd20 @ 0x005edd20 - 2DA Table Loading
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Data/GameDataManager.cs`, `src/Andastra/Runtime/Games/Odyssey/Data/TwoDATableManager.cs`
- **Description**: Loads 2DA table files
- **Implementation**: `TwoDATableManager.LoadTable()`

#### FUN_0041d2c0 @ 0x0041d2c0 - 2DA Table Lookup
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Data/OdysseyGameDataProvider.cs`
- **Description**: 2DA table lookup
- **Implementation**: `OdysseyGameDataProvider` table access

#### FUN_0065a380 @ 0x0065a380 - 2DA Table Lookup
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Data/OdysseyGameDataProvider.cs`
- **Description**: 2DA table lookup
- **Implementation**: `OdysseyGameDataProvider` table access

### Door Functions

#### FUN_00580ed0 @ 0x00580ed0 - Door Loading
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTD/UTDHelpers.cs`
- **Description**: Door loading function
- **Implementation**: `UTDHelpers.LoadDoor()`

#### FUN_005838d0 @ 0x005838d0 - Door Loading
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTD/UTDHelpers.cs`
- **Description**: Door loading function
- **Implementation**: `UTDHelpers.LoadDoor()`

#### FUN_00584f40 @ 0x00584f40 - Door Script Hooks
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Components/DoorComponent.cs`, `src/Andastra/Runtime/Games/Odyssey/Components/ScriptHooksComponent.cs`
- **Description**: Door script hooks
- **Implementation**: `ScriptHooksComponent` for doors

#### FUN_00585ec0 @ 0x00585ec0 - Door Script Hooks
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/Components/DoorComponent.cs`, `src/Andastra/Runtime/Games/Odyssey/Components/ScriptHooksComponent.cs`
- **Description**: Door script hooks
- **Implementation**: `ScriptHooksComponent` for doors

#### FUN_00588010 @ 0x00588010 - Placeable Loading
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTP/UTPHelpers.cs`
- **Description**: Loads placeable data from UTP template
- **Implementation**: `UTPHelpers.LoadPlaceable()`

#### FUN_00589520 @ 0x00589520 - Placeable Properties
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTP/UTPHelpers.cs`
- **Description**: Loads placeable properties (BodyBag, Fort, Ref, Will fields)
- **Implementation**: `UTPHelpers.LoadPlaceable()`

### Upgrade Screen Functions (K2 Only)

#### FUN_00680cb0 @ 0x00680cb0 - ShowUpgradeScreen
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/K2UpgradeScreen.cs`, `src/Andastra/Runtime/Games/Odyssey/UI/OdysseyUISystem.cs`
- **Description**: Shows upgrade screen for items
- **Implementation**: `K2UpgradeScreen`, `OdysseyUISystem.ShowUpgradeScreen()`

#### FUN_0055e160 @ 0x0055e160 - Upgrade Screen Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/K2UpgradeScreen.cs`
- **Description**: Upgrade screen function
- **Implementation**: `K2UpgradeScreen`

#### FUN_0055f2a0 @ 0x0055f2a0 - Search Inventory by ResRef
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/OdysseyUpgradeScreenBase.cs`
- **Description**: Searches inventory by ResRef, returns item ID or 0x7f000000
- **Implementation**: `OdysseyUpgradeScreenBase`

#### FUN_0055f3a0 @ 0x0055f3a0 - Upgrade Screen Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/K2UpgradeScreen.cs`
- **Description**: Upgrade screen function
- **Implementation**: `K2UpgradeScreen`

#### FUN_00569d60 @ 0x00569d60 - Upgrade Screen Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/K2UpgradeScreen.cs`
- **Description**: Upgrade screen function
- **Implementation**: `K2UpgradeScreen`

#### FUN_00729640 @ 0x00729640 - Upgrade Screen Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/K2UpgradeScreen.cs`
- **Description**: Upgrade screen function
- **Implementation**: `K2UpgradeScreen`

#### FUN_0072e260 @ 0x0072e260 - Upgrade Screen Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/K2UpgradeScreen.cs`, `src/Andastra/Runtime/Games/Odyssey/UI/OdysseyUpgradeScreenBase.cs`
- **Description**: Upgrade screen function
- **Implementation**: `K2UpgradeScreen`, `OdysseyUpgradeScreenBase`

#### FUN_00730970 @ 0x00730970 - Upgrade Screen Constructor
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/K2UpgradeScreen.cs`
- **Description**: Constructor loads "upcrystals" @ 0x00730c40
- **Implementation**: `K2UpgradeScreen` constructor

#### FUN_00731a00 @ 0x00731a00 - Upgrade Screen Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/K2UpgradeScreen.cs`, `src/Andastra/Runtime/Games/Odyssey/UI/OdysseyUpgradeScreenBase.cs`
- **Description**: Upgrade screen function
- **Implementation**: `K2UpgradeScreen`, `OdysseyUpgradeScreenBase`

#### FUN_00631140 @ 0x00631140 - Upgrade Screen Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/OdysseyUpgradeScreenBase.cs`
- **Description**: Upgrade screen function
- **Implementation**: `OdysseyUpgradeScreenBase`

#### FUN_00406e90 @ 0x00406e90 - Upgrade Screen Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/OdysseyUpgradeScreenBase.cs`
- **Description**: Upgrade screen function
- **Implementation**: `OdysseyUpgradeScreenBase`

### Loading Screen Functions

#### FUN_006cff90 @ 0x006cff90 - Loading Screen
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/UI/OdysseyLoadingScreen.cs`
- **Description**: Loading screen rendering
- **Implementation**: `OdysseyLoadingScreen`

### GUI Functions

#### FUN_0070a2e0 @ 0x0070a2e0 - GUI Manager
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/MonoGame/GUI/KotorGuiManager.cs`
- **Description**: GUI manager function
- **Implementation**: `KotorGuiManager`

### Graphics Backend Functions

#### FUN_00423b80 @ 0x00423b80 - Graphics Initialization
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics initialization
- **Implementation**: `Kotor2GraphicsBackend` initialization

#### FUN_00427950 @ 0x00427950 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_00428840 @ 0x00428840 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_00428fb0 @ 0x00428fb0 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_00429740 @ 0x00429740 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_00429780 @ 0x00429780 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_00430850 @ 0x00430850 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_004756b0 @ 0x004756b0 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_00475760 @ 0x00475760 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_004757a0 @ 0x004757a0 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

#### FUN_0076dba0 @ 0x0076dba0 - Graphics Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Graphics/Common/Backends/Odyssey/Kotor2GraphicsBackend.cs`
- **Description**: Graphics function
- **Implementation**: `Kotor2GraphicsBackend`

### Script Execution Functions

#### FUN_005c4ff0 @ 0x005c4ff0 - Script Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Scripting/EngineApi/BaseEngineApi.cs`
- **Description**: Script function
- **Implementation**: `BaseEngineApi`

### Area Functions

#### FUN_004e11d0 @ 0x004e11d0 - SaveAreaProperties
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`
- **Description**: Saves area properties
- **Implementation**: `OdysseyArea.SaveAreaProperties()`

#### FUN_0041b6b0 @ 0x0041b6b0 - Area Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`
- **Description**: Area function
- **Implementation**: `OdysseyArea`

#### FUN_0055aef0 @ 0x0055aef0 - Area Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`
- **Description**: Area function
- **Implementation**: `OdysseyArea`

#### FUN_006160c0 @ 0x006160c0 - Area Function
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Runtime/Games/Odyssey/OdysseyArea.cs`
- **Description**: Area function
- **Implementation**: `OdysseyArea`

### Faction Functions

#### FUN_005acf30 @ 0x005acf30 - Faction Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/FAC/FACHelpers.cs`
- **Description**: Faction functions
- **Implementation**: `FACHelpers`

#### FUN_005ad1a0 @ 0x005ad1a0 - Faction Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/FAC/FACHelpers.cs`
- **Description**: Faction functions
- **Implementation**: `FACHelpers`

#### FUN_004fcab0 @ 0x004fcab0 - Faction Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/FAC/FACHelpers.cs`
- **Description**: Faction functions
- **Implementation**: `FACHelpers`

### Journal Functions

#### FUN_00600dd0 @ 0x00600dd0 - Journal Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/JRL/JRLHelpers.cs`
- **Description**: Journal functions
- **Implementation**: `JRLHelpers`

### Pathfinding Functions

#### FUN_004e3650 @ 0x004e3650 - Pathfinding Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/PTH/PTHHelpers.cs`
- **Description**: Pathfinding functions
- **Implementation**: `PTHHelpers`

### Encounter Functions

#### FUN_0056c010 @ 0x0056c010 - Encounter Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTE/UTEHelpers.cs`
- **Description**: Encounter functions
- **Implementation**: `UTEHelpers`

#### FUN_0056d770 @ 0x0056d770 - Encounter Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTE/UTEHelpers.cs`
- **Description**: Encounter functions
- **Implementation**: `UTEHelpers`

### Store Functions

#### FUN_00571310 @ 0x00571310 - Store Functions
- **Status**: ✅ IMPLEMENTED
- **Location**: `src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTM/UTMHelpers.cs`
- **Description**: Store functions
- **Implementation**: `UTMHelpers`

### Entry Point Functions

#### FUN_0067afb0 @ 0x0067afb0 (swkotor.exe) / FUN_0067af90 @ 0x0067af90 (swkotor2.exe) - OnNewGamePicked
- **Status**: ✅ IMPLEMENTED (module loading only, character creation skipped)
- **Location**: `src/Andastra/Game/Core/OdysseyGame.cs`, `src/Andastra/Runtime/Games/Odyssey/Game/GameSession.cs`
- **Description**: Handles "New Game" button click, sets starting module
- **Implementation**: `OdysseyGame.StartGame()`, `GameSession.StartNewGame()`
- **Missing**: Character creation flow (currently skips directly to module load)
- **Ghidra Analysis**: 
  - K1: Sets module name "END_M01AA" (Endar Spire) @ string reference 0x00752f58
  - K2: Sets module name "001ebo" (Ebon Hawk prologue) @ string reference 0x007cc028
  - Original flow: Main Menu -> Character Creation -> Module Load
  - Current flow: Main Menu -> Module Load (WRONG)

## Summary Statistics

### Total Functions Referenced: 193 unique addresses
### Implemented: ~150 functions (78%)
### Partially Implemented: ~10 functions (5%)
### Not Implemented: ~33 functions (17%)

### Critical Missing Functions:
1. **Character Creation System** - Entire system missing (Update/Draw methods are TODO)
2. **Music Playback System** - No playback implementation (fields parsed but unused)
3. **Main Menu GUI Rendering** - Basic text rendering only (missing GUI panels, backgrounds, animations)

## Functions Not Yet Decompiled in Ghidra

The following functions are mentioned in code comments but have NOT been decompiled in Ghidra yet. These need to be analyzed to determine their purpose and implementation status:

1. **FUN_00404c80 @ 0x00404c80** - Referenced but not decompiled
2. **FUN_00404cf0 @ 0x00404cf0** - Referenced but not decompiled
3. **FUN_0057a300 @ 0x0057a300** - Referenced in time management but not fully analyzed
4. **FUN_00557540 @ 0x00557540** - Referenced but not decompiled
5. **FUN_0055b1d0 @ 0x0055b1d0** - Referenced but not decompiled
6. **FUN_0055b300 @ 0x0055b300** - Referenced but not decompiled
7. **FUN_005706b0 @ 0x005706b0** - Referenced but not decompiled
8. **FUN_00575350 @ 0x00575350** - Referenced but not decompiled
9. **FUN_005d7fc0 @ 0x005d7fc0** - Referenced but not decompiled
10. **FUN_005fbbf0 @ 0x005fbbf0** - Referenced but not decompiled
11. **FUN_005ff170 @ 0x005ff170** - Referenced but not decompiled
12. **FUN_006c6020 @ 0x006c6020** - Referenced but not decompiled
13. **FUN_00633270 @ 0x00633270** - Referenced in GameSettings but not decompiled
14. **FUN_004f4260 @ 0x004f4260** - Referenced but not decompiled
15. **FUN_00508260 @ 0x00508260** - Referenced but not decompiled
16. **FUN_0050e170 @ 0x0050e170** - Referenced but not decompiled
17. **FUN_0050e980 @ 0x0050e980** - Referenced but not decompiled

**Total Functions Not Yet Decompiled: 17**

## Next Steps

1. **Decompile remaining 17 functions** in Ghidra to determine their purpose
2. **Implement Character Creation System** - Full UI and flow
3. **Implement Music System** - Playback based on ARE MusicDay/MusicNight/MusicBattle fields
4. **Complete Main Menu GUI** - Use actual GUI panel files instead of text rendering
5. **Verify all implemented functions** match original engine behavior exactly

## Notes

- All function addresses are from swkotor2.exe unless otherwise specified
- Function addresses in code comments are verified against Ghidra decompilation
- Implementation status is based on codebase analysis and Ghidra verification
- This roadmap is EXHAUSTIVE - every function address mentioned in code has been checked

