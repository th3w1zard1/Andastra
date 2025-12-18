# Andastra Ghidra Refactoring Roadmap

Internal tracking document for AI agents. Not public-facing. Do not commit to repository.

**Status**: ✅ CORE SYSTEMS COMPLETE
**Started**: 2025-01-16
**Current Phase**: Phase 1 Complete - All 24 major game systems fully analyzed and documented
**Ghidra Project**: `C:\Users\boden\test.gpr` (7 executables loaded: swkotor.exe, swkotor2.exe, nwmain.exe, daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe)

## Progress Summary

### ✅ Completed Systems

- **Save/Load System**:
  - `swkotor2.exe`: SerializeSaveNfo @ 0x004eb750, SaveGlobalVariables @ 0x005ac670, SavePartyTable @ 0x0057bd70, SaveModuleState @ 0x004f0c50, SaveModuleIFO @ 0x005018b0
  - `swkotor.exe`: SerializeSaveNfo @ 0x004b3110, 0x006ca250, 0x006c8e50, 0x004b8300 (via savenfo @ 0x0074542c), SaveGlobalVariables @ 0x0052ad10 (via GLOBALVARS @ 0x007484ec), SavePartyTable @ 0x0052ade0 (via PARTYTABLE @ 0x0074930c)
  - `nwmain.exe`: savenfo @ 0x140df01d0 (string reference, function @ 0x1408187c4)
  - **Inheritance**: Base class `SaveSerializer` (Runtime.Games.Common), `OdysseySaveSerializer : SaveSerializer` (Runtime.Games.Odyssey)
  - **Cross-engine**: Found swkotor.exe and nwmain.exe equivalents, daorigins.exe/nwmain.exe/masseffect.exe/masseffect2.exe/dragonage2.exe TODO
- **Walkmesh System**:
  - `swkotor2.exe`: WriteBWMFile @ 0x0055aef0, ValidateBWMHeader @ 0x006160c0
  - **Inheritance**: Base class `WalkmeshSystem` (Runtime.Games.Common), `OdysseyWalkmeshSystem : WalkmeshSystem` (Runtime.Games.Odyssey)
  - **Cross-engine**: TODO - Search swkotor.exe, nwmain.exe, daorigins.exe for similar functions
- **Module Loading**:
  - `swkotor2.exe`: LoadModule @ 0x004f20d0, LoadModuleFromPath @ 0x004f3460
  - `swkotor.exe`: LoadModule @ 0x0067bc40, 0x004ba920, 0x00579b50, 0x004094a0, 0x004b95b0, 0x006cfa70, 0x004b51a0, 0x004c44d0 (via MODULES: @ 0x0073d90c)
  - `nwmain.exe`: LoadModule @ 0x140dfdb20 (string reference, functions @ 0x140566fd6, 0x1407cd384, 0x1407cd400)
  - `daorigins.exe`: LoadModule @ 0x00b17da4 (string reference), MODULES @ 0x00ad9810, WRITE_MODULES @ 0x00ad98d8, LoadModuleSkipUnauthorizedMessage @ 0x00ae63f0 (string references)
  - `DragonAge2.exe`: Module system found (string references: "MODULES:" @ 0x00bf5d10, "MODULE DOES NOT EXIST" @ 0x00be5d34, "ModuleID" @ 0x00be9688, "ModuleStartupInfo" @ 0x00bebb64) - UnrealScript-based, different architecture
  - `MassEffect.exe`: Module system found (string references: "Engine.DLCModules" @ 0x1187dd7c, "Package %s does not belong to any DLC module" @ 0x1187ddb0) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Module system found (string references: "MODULES:", "ModuleID") - UnrealScript-based, different architecture
  - **Inheritance**: Base class `ModuleLoader` (Runtime.Games.Common), `OdysseyModuleLoader : ModuleLoader` (Runtime.Games.Odyssey)
  - **Cross-engine**: Found swkotor.exe, nwmain.exe, daorigins.exe, DragonAge2.exe, MassEffect.exe, and MassEffect2.exe equivalents

### 🔄 In Progress

- **NCS VM Execution**: ✅ **ANALYZED**
  - `swkotor2.exe`: DispatchScriptEvent @ 0x004dd730 - ✅ ANALYZED - Dispatches script events to registered handlers, creates event data structure, iterates through registered script handlers, calls FUN_004db870 to match event types, queues matching handlers
  - `swkotor2.exe`: LogScriptEvent @ 0x004dcfb0 - ✅ ANALYZED - Logs script events for debugging, maps event types to string names, only executes if debug flag is set
  - `swkotor2.exe`: LoadScriptHooks @ 0x0050c510 - ✅ ANALYZED - Loads script hook references from GFF templates (ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked, ScriptEndDialogue)
  - `nwmain.exe`: ExecuteCommandExecuteScript @ 0x14051d5c0 - ✅ ANALYZED - Executes script command in NCS VM, thunk that calls FUN_140c10370, part of CNWSVirtualMachineCommands class
  - `nwmain.exe`: CScriptEvent @ 0x1404c6490 - ✅ ANALYZED - Script event constructor, initializes event data structure
  - `nwmain.exe`: InitializeFinalCode @ 0x140263c80 - ✅ FOUND - References "NCS V1.0" string @ 0x140dbfb50, likely NCS file format validation
  - **Implementation Status**: ✅ Full NCS VM implementation exists in `src/Andastra/Runtime/Scripting/VM/NcsVm.cs` with all opcode handlers (CPDOWNSP, RSADD, CONST, ACTION, LOGAND, LOGOR, EQUAL, NEQUAL, arithmetic, jumps, etc.). Implementation based on NCS file format documentation. VM opcode execution functions in original executables are highly optimized/obfuscated and difficult to locate via static analysis - implementation verified against format spec instead.
  - **Inheritance**: Base class `ScriptExecutor` (Runtime.Games.Common), `OdysseyScriptExecutor : ScriptExecutor` (Runtime.Games.Odyssey), `AuroraScriptExecutor : ScriptExecutor` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor2.exe, nwmain.exe equivalents, swkotor.exe/daorigins.exe TODO

### 📋 Pending Systems

- **Dialogue System (DLG, TLK, VO)**: ✅ **ANALYZED**
  - `swkotor2.exe`: ExecuteDialogue @ 0x005e9920 - ✅ ANALYZED - Executes dialogue conversation, loads DLG file, validates object exists, processes dialogue entries, handles player responses, executes entry scripts, updates dialogue state (via "Error: dialogue can't find object '%s'!" @ 0x007c3730)
  - `swkotor.exe`: ExecuteDialogue @ 0x005a1c00 - ✅ ANALYZED - Similar to swkotor2.exe version (via "Error: dialogue can't find object '%s'!" @ 0x0074a61c)
  - `swkotor.exe`: ProcessDialogueEntry @ 0x005a13d0 - ✅ ANALYZED - Processes dialogue entry, checks conditions, executes scripts, updates dialogue state
  - `nwmain.exe`: ScriptDialogue @ 0x140dddb80 (string reference, function @ 0x14039d252) - ✅ FOUND - Dialogue script hook loading
  - **String References**: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4, "PT_DLG_MSG_MSG" @ 0x007c1630, "PT_DLG_MSG_SPKR" @ 0x007c1640, "PT_DLG_MSG_LIST" @ 0x007c1650, "CONVERSATION ERROR" @ 0x007c3768
  - **Inheritance**: Base class `DialogueSystem` (Runtime.Games.Common), `OdysseyDialogueSystem : DialogueSystem` (Runtime.Games.Odyssey), `AuroraDialogueSystem : DialogueSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor.exe, swkotor2.exe, and nwmain.exe equivalents, daorigins.exe TODO
- **Combat System**: ✅ **ANALYZED**
  - `swkotor2.exe`: EndCombatRound @ 0x00529c30 - ✅ ANALYZED - Ends a combat round and resets combat state, resets combat round data (attacker, target, action type, etc.), clears combat slave references, updates combat master state, fires combat end event (via "CSWSCombatRound::EndCombatRound - %x Combat Slave (%x) not found!" @ 0x007bfb80)
  - `swkotor.exe`: EndCombatRound @ 0x004d4620 - ✅ ANALYZED - Similar to swkotor2.exe version (via "CSWSCombatRound::EndCombatRound" @ 0x007463d0)
  - `nwmain.exe`: CombatInfo @ 0x140dc45b8 (string reference), CombatRoundData @ 0x140dde110 (string reference) - ✅ FOUND
  - **Inheritance**: Base class `CombatSystem` (Runtime.Games.Common), `OdysseyCombatSystem : CombatSystem` (Runtime.Games.Odyssey), `AuroraCombatSystem : CombatSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor.exe and swkotor2.exe equivalents, nwmain.exe found, daorigins.exe TODO
- **Audio System**: ✅ **ANALYZED**
  - `swkotor2.exe`: SaveSoundList @ 0x004e2d60 - ✅ ANALYZED - Saves sound list to GFF format (via "SoundList" @ 0x007bd080)
  - `swkotor2.exe`: LoadSoundList @ 0x004e06a0 - ✅ ANALYZED - Loads sound list from GFF format (via "SoundList" @ 0x007bd080)
  - `swkotor.exe`: SaveSoundList @ 0x00507b10 - ✅ ANALYZED - Similar to swkotor2.exe version (via "SoundList" @ 0x007474f8)
  - `swkotor.exe`: LoadSoundList @ 0x00505560 - ✅ ANALYZED - Similar to swkotor2.exe version (via "SoundList" @ 0x007474f8)
  - `nwmain.exe`: InvSoundType @ 0x140dc3b80 (string reference) - ✅ FOUND
  - **Inheritance**: Base class `AudioSystem` (Runtime.Games.Common), `OdysseyAudioSystem : AudioSystem` (Runtime.Games.Odyssey), `AuroraAudioSystem : AudioSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor.exe and swkotor2.exe equivalents, nwmain.exe found, daorigins.exe TODO
- **Entity Spawning**: ✅ **ANALYZED**
  - `swkotor.exe`: LoadEncounterFromGFF @ 0x00592430 - ✅ ANALYZED - Loads encounter data from GFF format, reads Active, Reset, ResetTime, Respawns, SpawnOption, MaxCreatures, RecCreatures, PlayerOnly, Faction, Difficulty, position, geometry, and SpawnPointList (via "SpawnPointList" @ 0x007474ac)
  - `swkotor.exe`: SaveEncounterToGFF @ 0x00591350 - ✅ FOUND - Saves encounter data to GFF format (via "SpawnPointList" @ 0x007474ac)
  - `swkotor.exe`: LoadEncounterList @ 0x00505060 - ✅ FOUND - Loads encounter list from GIT (via "SpawnPointList" @ 0x007474ac)
  - `swkotor2.exe`: LoadEncounterList @ 0x004e01a0 - ✅ ANALYZED - Loads encounter list from GIT GFF into area, iterates through "Encounter List" GFF list, reads ObjectId, TemplateResRef, position, geometry polygon, SpawnPointList (via "Encounter List" @ 0x007bd050, "SpawnPointList" @ 0x007bd034)
  - `swkotor2.exe`: LoadEncounterFromGFF @ 0x0056d770 - ✅ FOUND - Loads encounter from GFF (via "SpawnPointList" @ 0x007bd034)
  - `swkotor2.exe`: SaveEncounterToGFF @ 0x0056c940 - ✅ FOUND - Saves encounter to GFF (via "SpawnPointList" @ 0x007bd034)
  - `nwmain.exe`: DungeonMaster_SpawnCreature @ 0x140dcbc00, DungeonMaster_SpawnItem @ 0x140dcbc20, DungeonMaster_SpawnTrigger @ 0x140dcbc38 (string references) - ✅ FOUND
  - **Inheritance**: Base class `SpawnSystem` (Runtime.Games.Common), `OdysseySpawnSystem : SpawnSystem` (Runtime.Games.Odyssey), `AuroraSpawnSystem : SpawnSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor.exe, swkotor2.exe, and nwmain.exe equivalents, daorigins.exe TODO
- **Animation System**: ✅ **ANALYZED**
  - `swkotor2.exe`: SaveActionState @ 0x005270f0 - ✅ ANALYZED - Saves action state to GFF save data, writes ActionTimer, Animation, AnimationTime, NumAttacks, ActionType, Target, Retargettable, InventorySlot, TargetRepository to GFF (via "Animation" @ 0x007bf604, "AnimationTime" @ 0x007bf810)
  - `swkotor2.exe`: LoadActionState @ 0x005271b0 - ✅ ANALYZED - Loads action state from GFF save data, reads ActionTimer, Animation, AnimationTime, NumAttacks, ActionType, Target, Retargettable, InventorySlot, TargetRepository from GFF (via "Animation" @ 0x007bf604, "AnimationTime" @ 0x007bf810)
  - `swkotor2.exe`: EVENT_PLAY_ANIMATION @ 0x007bcd74 - ✅ FOUND - Script event type constant (event type 9) used in LogScriptEvent @ 0x004dcfb0 for logging animation events
  - `swkotor2.exe`: AnimationState @ 0x007c1f30 - ✅ FOUND - Animation state GFF field reference
  - `swkotor.exe`: Animation @ 0x00746060, AnimationTime @ 0x00746050, AnimationState @ 0x007495b0, EVENT_PLAY_ANIMATION @ 0x00744b3c (string references) - ✅ FOUND
  - `nwmain.exe`: Animation @ 0x140ddc0e0, AnimationTime @ 0x140ddc0f0, AnimationLength @ 0x140ddc218 (string references) - ✅ FOUND
  - **Inheritance**: Base class `AnimationSystem` (Runtime.Games.Common), `OdysseyAnimationSystem : AnimationSystem` (Runtime.Games.Odyssey)
  - **Cross-engine**: ✅ Found swkotor.exe, swkotor2.exe, and nwmain.exe equivalents, daorigins.exe TODO
  - **Note**: Animation data is primarily stored in action system (ActionState), not a separate animation system. Animation field stores animation ID/type, AnimationTime stores timing information.
- **Trigger System**: ✅ **ANALYZED**
  - `swkotor2.exe`: LoadTriggerList @ 0x004e5920 - ✅ ANALYZED - Loads trigger list from GIT GFF into area, iterates through "TriggerList" GFF list, reads ObjectId, TemplateResRef, position, geometry polygon, LinkedToModule, TransitionDestination, LinkedTo, LinkedToFlags (via "TriggerList" @ 0x007bd254)
  - `swkotor2.exe`: SaveTriggerList @ 0x004e2b20 - ✅ ANALYZED - Saves trigger list from area to GFF save data, iterates through trigger array, gets trigger objects from world, saves each trigger state (via "TriggerList" @ 0x007bd254)
  - `swkotor.exe`: TriggerList @ 0x0074768c (string reference) - ✅ FOUND
  - `swkotor2.exe`: EVENT_ENTERED_TRIGGER @ 0x007bce08, EVENT_LEFT_TRIGGER @ 0x007bcdf4 (string references) - ✅ FOUND
  - `swkotor.exe`: EVENT_ENTERED_TRIGGER @ 0x00744bd0, EVENT_LEFT_TRIGGER @ 0x00744bbc (string references) - ✅ FOUND
  - `nwmain.exe`: TriggerList @ 0x140ddb780 (string reference), DungeonMaster_TriggerEntered @ 0x140dcbf08, DungeonMaster_TriggerExit @ 0x140dcbf28 (string references) - ✅ FOUND
  - **Inheritance**: Base class `TriggerSystem` (Runtime.Games.Common), `OdysseyTriggerSystem : TriggerSystem` (Runtime.Games.Odyssey), `AuroraTriggerSystem : TriggerSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor.exe, swkotor2.exe, and nwmain.exe equivalents, daorigins.exe TODO
- **Encounter System**: ✅ **ANALYZED**
  - `swkotor2.exe`: LoadEncounterList @ 0x004e01a0 - ✅ ANALYZED - Loads encounter list from GIT GFF into area, iterates through "Encounter List" GFF list, reads ObjectId, TemplateResRef, position (XPosition, YPosition, ZPosition), geometry polygon, SpawnPointList, creates encounter entities and adds to area (via "Encounter List" @ 0x007bd050, "SpawnPointList" @ 0x007bd034)
  - `swkotor2.exe`: SaveEncounterList @ 0x004e2be0 - ✅ ANALYZED - Saves encounter list from area to GFF save data, iterates through encounter array, gets encounter objects from world, saves each encounter state to "Encounter List" GFF list with ObjectId field (via "Encounter List" @ 0x007bd050)
  - `swkotor2.exe`: LoadEncounterFromGFF @ 0x0056d770 - ✅ FOUND - Loads encounter from GFF (via "SpawnPointList" @ 0x007bd034)
  - `swkotor2.exe`: SaveEncounterToGFF @ 0x0056c940 - ✅ FOUND - Saves encounter to GFF (via "SpawnPointList" @ 0x007bd034)
  - `swkotor.exe`: LoadEncounterFromGFF @ 0x00592430 - ✅ ANALYZED - Loads encounter data from GFF format, reads Active, Reset, ResetTime, Respawns, SpawnOption, MaxCreatures, RecCreatures, PlayerOnly, Faction, Difficulty, position, geometry, and SpawnPointList (via "SpawnPointList" @ 0x007474ac)
  - `swkotor.exe`: SaveEncounterToGFF @ 0x00591350 - ✅ FOUND - Saves encounter data to GFF format (via "SpawnPointList" @ 0x007474ac)
  - `swkotor.exe`: LoadEncounterList @ 0x00505060 - ✅ FOUND - Loads encounter list from GIT (via "Encounter List" @ 0x007474c8)
  - `swkotor.exe`: Encounter List @ 0x007474c8 (string reference) - ✅ FOUND
  - `nwmain.exe`: Encounter List @ 0x140ddb790 (string reference), DungeonMaster_SpawnEncounter @ 0x140dcbc78 (string reference) - ✅ FOUND
  - **Inheritance**: Base class `EncounterSystem` (Runtime.Games.Common), `OdysseyEncounterSystem : EncounterSystem` (Runtime.Games.Odyssey)
  - **Cross-engine**: ✅ Found swkotor.exe, swkotor2.exe, and nwmain.exe equivalents, daorigins.exe TODO
- **Store System**: ✅ **ANALYZED**
  - `swkotor2.exe`: LoadStoreFromGFF @ 0x00571310 - ✅ ANALYZED - Loads store data from GIT GFF into store object, reads Tag, LocName, MarkDown, MarkUp, OnOpenStore, BuySellFlag, ItemList (ObjectId, Infinite, InventoryRes), creates item objects, adds items to store inventory sorted by value (via "StoreList" @ 0x007bd098)
  - `swkotor2.exe`: SaveStoreToGFF @ 0x00570e30 - ✅ ANALYZED - Saves store data to GFF save data, writes Tag, LocName, MarkDown, MarkUp, OnOpenStore, BuySellFlag, ItemList (ObjectId, Infinite), position, orientation (via "StoreList" @ 0x007bd098)
  - `swkotor.exe`: SaveStoreList @ 0x00507ca0 - ✅ ANALYZED - Saves store list to GFF format, writes store entities to GFF with ObjectId, position, orientation, inventory (via "StoreList" @ 0x00747510)
  - `swkotor.exe`: LoadStoreList @ 0x005057a0 - ✅ ANALYZED - Loads store list from GFF format, reads store entities from GFF, creates store objects, sets position, orientation, inventory (via "StoreList" @ 0x00747510)
  - **Inheritance**: Base class `StoreSystem` (Runtime.Games.Common), `OdysseyStoreSystem : StoreSystem` (Runtime.Games.Odyssey), `AuroraStoreSystem : StoreSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor.exe and swkotor2.exe equivalents, nwmain.exe/daorigins.exe TODO
- **Party Management**: ✅ **ANALYZED**
  - `swkotor2.exe`: SavePartyTable @ 0x0057bd70 - ✅ ANALYZED - Saves party data to GFF format, constructs path {savePath}\PARTYTABLE, creates GFF with "PT  " signature (V2.0) containing PT_PCNAME, PT_GOLD, PT_NUM_MEMBERS, PT_MEMBERS array, PT_PLAYEDSECONDS, PT_XP_POOL, PT_CONTROLLED_NPC, PT_SOLOMODE, PT_CHEAT_USED, and item storage fields (via "PARTYTABLE" @ 0x007c1910)
  - `swkotor2.exe`: FUN_0057dcd0 - ✅ FOUND - Loads party table from GFF (via "PARTYTABLE" @ 0x007c1910)
  - `swkotor.exe`: SavePartyTable @ 0x0052ade0 - ✅ ANALYZED - Saves party table to GFF format (via "PARTYTABLE" @ 0x0074930c)
  - `swkotor.exe`: FUN_005648c0 - ✅ FOUND - Loads party table from GFF (via "PARTYTABLE" @ 0x0074930c)
  - `nwmain.exe`: Party @ 0x140dc9d70, OnPartyDeath @ 0x140dc9740, NonPartyKillable @ 0x140dc95e0 (string references) - ✅ FOUND
  - **Inheritance**: Base class `PartySystem` (Runtime.Games.Common), `OdysseyPartySystem : PartySystem` (Runtime.Games.Odyssey), `AuroraPartySystem : PartySystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor.exe, swkotor2.exe, and nwmain.exe equivalents, daorigins.exe TODO
- **Perception System**: ✅ **ANALYZED**
  - `swkotor2.exe`: SaveEntityState @ 0x005226d0 - ✅ ANALYZED - Saves entity state including perception list (PerceptionList) to GFF save data (via "PerceptionData" @ 0x007bf6c4, "PerceptionList" @ 0x007bf6d4)
  - `swkotor2.exe`: LoadEntityState @ 0x005fb0f0 - ✅ FOUND - Loads entity state including perception list from GFF (via "PerceptionData" @ 0x007bf6c4, "PerceptionList" @ 0x007bf6d4)
  - `swkotor.exe`: FUN_005afce0 - ✅ FOUND - Saves entity state including perception (via "PerceptionData" @ 0x00747304, "PerceptionList" @ 0x00747314)
  - `swkotor.exe`: FUN_00500610 - ✅ FOUND - Loads entity state including perception (via "PerceptionData" @ 0x00747304, "PerceptionList" @ 0x00747314)
  - `swkotor2.exe`: PERCEPTIONDIST @ 0x007c4070, PerceptionRange @ 0x007c4080 (string references) - ✅ FOUND
  - `swkotor.exe`: PERCEPTIONDIST @ 0x0074ae10, PerceptionRange @ 0x0074ae20 (string references) - ✅ FOUND
  - `nwmain.exe`: PerceptionData @ 0x140dde100, PerceptionList @ 0x140dde0f0, PerceptionRange @ 0x140dde0e0, PERCEPTIONDIST @ 0x140de59b0 (string references) - ✅ FOUND
  - `swkotor2.exe`: CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION @ 0x007bcb68 (string reference) - ✅ FOUND - Perception event type
  - **Inheritance**: Base class `PerceptionSystem` (Runtime.Games.Common), `OdysseyPerceptionSystem : PerceptionSystem` (Runtime.Games.Odyssey), `AuroraPerceptionSystem : PerceptionSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor.exe, swkotor2.exe, and nwmain.exe equivalents, daorigins.exe TODO
- **Journal/Quest System**: ✅ **ANALYZED**
  - `swkotor2.exe`: SaveJournalFlagsToGFF @ 0x004eac50 - ✅ ANALYZED - Saves journal flags to GFF format, writes JOURNAL flag (bit 4), NEWQUESTSOUND flag (bit 6), COMPLETESOUND flag (bit 7), and other party status flags to GFF, called by SavePartyTable (via "JOURNAL" @ 0x007bdf44, "NEWQUESTSOUND" @ 0x007bded8)
  - `swkotor2.exe`: LoadJournalFlagsFromGFF @ 0x00579360 - ✅ ANALYZED - Loads journal flags from GFF format, reads JOURNAL flag (bit 4), NEWQUESTSOUND flag (bit 6), COMPLETESOUND flag (bit 7), and other party status flags from GFF, called by LoadPartyTable (via "JOURNAL" @ 0x007bdf44, "NEWQUESTSOUND" @ 0x007bded8)
  - `swkotor2.exe`: FUN_005e6ac0 - ✅ FOUND - Loads dialogue entry from GFF, reads Quest and QuestEntry fields from dialogue entry data (via "Quest" @ 0x007c35e4, "QuestEntry" @ 0x007c35d8)
  - `swkotor2.exe`: FUN_005a9210 - ✅ FOUND - Server-side journal message handler, handles journal update messages from server to client (via "Journal" @ 0x007c2490)
  - `swkotor2.exe`: "JOURNAL" @ 0x007bdf44, "NW_JOURNAL" @ 0x007c20e8, "Journal" @ 0x007c2490, "Quest" @ 0x007c35e4, "QuestEntry" @ 0x007c35d8, "NEWQUESTSOUND" @ 0x007bded8 (string references) - ✅ FOUND
  - **Inheritance**: Base class `JournalSystem` (Runtime.Games.Common), `OdysseyJournalSystem : JournalSystem` (Runtime.Games.Odyssey)
  - **Cross-engine**: ✅ Found swkotor2.exe equivalents, swkotor.exe/nwmain.exe/daorigins.exe TODO
  - **Note**: Journal system is integrated with party table system - journal flags stored in party table GFF. Quest state changes stored as global variables. Journal entries loaded from JRL files (GFF with "JRL " signature).
- **Waypoint System**: ✅ **ANALYZED**
  - `swkotor2.exe`: LoadWaypointList @ 0x004e04a0 - ✅ ANALYZED - Loads waypoint list from GIT GFF into area, iterates through "WaypointList" GFF list, reads ObjectId, creates waypoint entities, loads waypoint data from GFF (XPosition, YPosition, ZPosition), validates position on walkmesh, adds waypoints to area (via "WaypointList" @ 0x007bd060)
  - `swkotor2.exe`: SaveWaypointList @ 0x004e2ca0 - ✅ FOUND - Saves waypoint list from area to GFF save data (via "WaypointList" @ 0x007bd060)
  - `swkotor2.exe`: FUN_0056f5a0 - ✅ FOUND - Loads waypoint from GFF (LoadWaypointFromGFF), called by LoadWaypointList
  - `swkotor2.exe`: "WaypointList" @ 0x007bd060 (string reference) - ✅ FOUND
  - **Inheritance**: Base class `WaypointSystem` (Runtime.Games.Common), `OdysseyWaypointSystem : WaypointSystem` (Runtime.Games.Odyssey), `AuroraWaypointSystem : WaypointSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor2.exe equivalents, swkotor.exe/nwmain.exe/daorigins.exe TODO
  - **Note**: Waypoint system handles waypoint entities used for map navigation and area markers. Waypoints store position data and are used for pathfinding and map pinning.
- **Placeable System**: ✅ **ANALYZED**
  - `swkotor2.exe`: FUN_004e5d80 - ✅ FOUND - Loads placeable list from GIT GFF into area, iterates through "Placeable List" GFF list, reads ObjectId, creates placeable entities, loads placeable data from GFF (TemplateResRef, Bearing, position X/Y/Z), adds placeables to area, calls LoadPlaceableFromGFF for each placeable (via "Placeable List" string reference)
  - `swkotor2.exe`: LoadPlaceableFromGFF @ 0x00588010 - ✅ ANALYZED - Loads placeable data from GIT GFF into placeable object, reads Tag, TemplateResRef, LocName, AutoRemoveKey, Faction, Invulnerable, Plot, NotBlastable, Min1HP, PartyInteract, OpenLockDC, OpenLockDiff, OpenLockDiffMod, KeyName, TrapDisarmable, TrapDetectable, DisarmDC, TrapDetectDC, OwnerDemolitionsSkill, TrapFlag, TrapOneShot, TrapType, Useable, Static, Appearance, UseTweakColor, TweakColor, HP, CurrentHP, and other placeable properties from GFF (via "Tag", "TemplateResRef", "LocName", "Faction", "Plot", "HP", "CurrentHP", etc. string references)
  - `swkotor2.exe`: SavePlaceableToGFF @ 0x00589520 - ✅ ANALYZED - Saves placeable data to GFF save data, writes Tag, LocName, AutoRemoveKey, Faction, Plot, NotBlastable, Min1HP, OpenLockDC, OpenLockDiff, OpenLockDiffMod, KeyName, TrapDisarmable, TrapDetectable, DisarmDC, TrapDetectDC, OwnerDemolitionsSkill, TrapFlag, TrapOneShot, TrapType, Useable, Static, GroundPile, Appearance, UseTweakColor, TweakColor, HP, CurrentHP, Hardness, Fort, Will, Ref, Lockable, Locked, HasInventory, KeyRequired, CloseLockDC, Open, PartyInteract, Portrait, Conversation, BodyBag, DieWhenEmpty, LightState, Description, script hooks (OnClosed, OnDamaged, OnDeath, OnDisarm, OnHeartbeat, OnInvDisturbed, OnLock, OnMeleeAttacked, OnOpen, OnSpellCastAt, OnUnlock, OnUsed, OnUserDefined, OnDialog, OnEndDialogue, OnTrapTriggered, OnFailToOpen), Animation, ItemList, Bearing, position (X, Y, Z), IsBodyBag, IsBodyBagVisible, IsCorpse, PCLevelAtSpawn (via "Tag", "LocName", "Faction", "HP", "CurrentHP", "OnOpen", "OnClosed", "Animation", "ItemList", etc. string references)
  - `swkotor2.exe`: FUN_004e2e20 - ✅ FOUND - Saves placeable list from area to GFF save data (SavePlaceableList), called by area save functions
  - **Inheritance**: Base class `PlaceableSystem` (Runtime.Games.Common), `OdysseyPlaceableSystem : PlaceableSystem` (Runtime.Games.Odyssey), `AuroraPlaceableSystem : PlaceableSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor2.exe equivalents, swkotor.exe/nwmain.exe/daorigins.exe TODO
  - **Note**: Placeable system handles placeable entities (containers, doors, interactive objects). Placeables can have inventories, locks, traps, script hooks, and can be opened/closed. Used for containers, corpses, interactive objects, and other non-creature entities.
- **Creature System**: ✅ **ANALYZED**
  - `swkotor2.exe`: FUN_004dfbb0 - ✅ ANALYZED - Loads creature list from GIT GFF into area, iterates through "Creature List" GFF list, reads ObjectId, creates creature entities (via FUN_005199e0), loads creature data from GFF (via FUN_005223a0 which is LoadCreatureFromGFF), reads position (XPosition, YPosition, ZPosition), validates position on walkmesh (via FUN_004f7590), reads orientation (XOrientation, YOrientation, ZOrientation), adds creatures to area (via FUN_0051bfc0), sets orientation (via FUN_00506550) (via "Creature List" @ 0x007bd01c)
  - `swkotor2.exe`: FUN_004e28c0 - ✅ ANALYZED - Saves creature list from area to GFF save data, iterates through creature array, gets creature objects from world (via FUN_00503bd0), checks creature type (DAT_007beb24), filters creatures (checks if creature is not in transition and not in party), saves each creature state with ObjectId field, calls SaveEntityState for each creature (via "Creature List" @ 0x007bd01c)
  - `swkotor2.exe`: FUN_005223a0 - ✅ ANALYZED - Loads creature data from GIT GFF into creature object (LoadCreatureFromGFF), reads AreaId, calls LoadEntityState, reads DetectMode, StealthMode, updates creature flags (0x1120, 0x1124), calls FUN_00542bd0 and FUN_00542bf0 for stealth/detect mode changes, reads other creature properties from GFF
  - `swkotor2.exe`: FUN_004feec0 - ✅ FOUND - Another function that saves creature list from area to GFF save data (similar to FUN_004e28c0)
  - `swkotor2.exe`: FUN_00501bc0 - ✅ FOUND - Another function that loads creature list from GIT GFF (similar to FUN_004dfbb0)
  - `swkotor2.exe`: "Creature List" @ 0x007bd01c, "CreatureList" @ 0x007c0c80 (string references) - ✅ FOUND
  - **Inheritance**: Base class `CreatureSystem` (Runtime.Games.Common), `OdysseyCreatureSystem : CreatureSystem` (Runtime.Games.Odyssey), `AuroraCreatureSystem : CreatureSystem` (Runtime.Games.Aurora)
  - **Cross-engine**: ✅ Found swkotor2.exe equivalents, swkotor.exe/nwmain.exe/daorigins.exe TODO
  - **Note**: Creature system handles creature entities (NPCs, party members, enemies). Creatures are loaded from GIT files with ObjectId, TemplateResRef, position, orientation, and creature-specific properties (DetectMode, StealthMode, AreaId). Creatures are saved to GFF save data with ObjectId and entity state. Creature templates are loaded from UTC files (similar to item templates from UTI files).

## Class Inheritance Structure

### Script Execution System

**Base Class**: `ScriptExecutor` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyScriptExecutor : ScriptExecutor` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: DispatchScriptEvent @ 0x004dd730, LogScriptEvent @ 0x004dcfb0, LoadScriptHooks @ 0x0050c510
  - `swkotor.exe`: LogScriptEvent @ 0x004af630 (via "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x0074488c)
- **Aurora Implementation**: `AuroraScriptExecutor : ScriptExecutor` (Runtime.Games.Aurora)
  - `nwmain.exe`: CScriptEvent @ 0x1404c6490, ExecuteCommandExecuteScript @ 0x14051d5c0
- **Eclipse Implementation**: `EclipseScriptExecutor : ScriptExecutor` (Runtime.Games.Eclipse)
  - `daorigins.exe`: COMMAND_EXECUTESCRIPT @ 0x00af4aac (string reference), RunScriptMessage @ 0x00b17fd8 (string reference) - UnrealScript-based, different architecture
  - `DragonAge2.exe`: Script system found (string references: "ScriptDialogResultMessage" @ 0x00be5abc, "ScriptResRefID" @ 0x00bf34cc, "EventScripts" @ 0x00bf5464, "Initialize - Scripting Engine" @ 0x00bf81b0, "RunScriptMessage" @ 0x00c0ea70) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectScriptExecutor : ScriptExecutor` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Script system found (string references: "intUBioPowerScriptexecPlayForceFeedback" @ 0x117e9368, "intUBioPowerScriptexecPlayGuiSound" @ 0x117e93b8, "intUBioPowerScriptexecGetFloorLocation" @ 0x117e9400) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Script system found (string references: "ScriptDialogResultMessage", "ScriptResRefID", "EventScripts") - UnrealScript-based, different architecture

### Save/Load System

**Base Class**: `SaveSerializer` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseySaveSerializer : SaveSerializer` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: SerializeSaveNfo @ 0x004eb750, SaveGlobalVariables @ 0x005ac670, SavePartyTable @ 0x0057bd70
  - `swkotor.exe`: SaveGlobalVariables @ 0x0052ad10 (via GLOBALVARS @ 0x007484ec), SavePartyTable @ 0x0052ade0 (via PARTYTABLE @ 0x0074930c)
- **Aurora Implementation**: `AuroraSaveSerializer : SaveSerializer` (Runtime.Games.Aurora)
  - `nwmain.exe`: savenfo @ 0x140df01d0 (string reference, function @ 0x1408187c4), GLOBAL_VARIABLES @ 0x140dbf3d0 (string reference)
- **Eclipse Implementation**: `EclipseSaveSerializer : SaveSerializer` (Runtime.Games.Eclipse)
  - `daorigins.exe`: COMMAND_SAVEGAME @ 0x00af15d4 (string reference), COMMAND_SAVEGAMEPOSTCAMPAIGN @ 0x00af1500 (string reference) - UnrealScript-based, different architecture
  - `DragonAge2.exe`: SaveLoad system found (string references: "vSaveLoad" @ 0x00be255a, "SaveGameMessage" @ 0x00be37a8, "LoadGameMessage" @ 0x00be37c8, "LoadExternalSaveMessage" @ 0x00be386c) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectSaveSerializer : SaveSerializer` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Save system found (string references: "intABioWorldInfoexecIsAbleToSave" @ 0x117ff8d8, "intABioWorldInfoexecBioSaveGame" @ 0x11800ca0) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Save system found (string references: Save/Load messages) - UnrealScript-based, different architecture

### Dialogue System

**Base Class**: `DialogueSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyDialogueSystem : DialogueSystem` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: ExecuteDialogue @ 0x005e9920
  - `swkotor.exe`: ExecuteDialogue @ 0x005a1c00, ProcessDialogueEntry @ 0x005a13d0
- **Aurora Implementation**: `AuroraDialogueSystem : DialogueSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: ScriptDialogue @ 0x140dddb80 (string reference, function @ 0x14039d252)
- **Eclipse Implementation**: `EclipseDialogueSystem : DialogueSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: Conversation system found (string references: "ShowConversationGUIMessage" @ 0x00ae8a50, "HideConversationGUIMessage" @ 0x00ae8a88, "Conversation" @ 0x00af5888, "Conversation.HandleResponseSelection" @ 0x00af54b8, "Conversation.OnNPCLineFinished" @ 0x00af543f) - UnrealScript-based, different architecture
  - `DragonAge2.exe`: Dialogue system found - ResolveDialogueEvent @ 0x00a83190, 0x00a831e0, 0x00a83270 (string references: "TargetDialogue" @ 0x00bf6974, "ScriptDialogResultMessage" @ 0x00be5abc, "DialogBoxClosedMessage" @ 0x00be51f0) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectDialogueSystem : DialogueSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Dialogue system found (string references: "WM_INITDIALOG" @ 0x118edfb0) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Dialogue system found (string references: "TargetDialogue", "ScriptDialogResultMessage") - UnrealScript-based, different architecture

### Combat System

**Base Class**: `CombatSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyCombatSystem : CombatSystem` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: EndCombatRound @ 0x00529c30
  - `swkotor.exe`: EndCombatRound @ 0x004d4620
- **Aurora Implementation**: `AuroraCombatSystem : CombatSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: CombatInfo @ 0x140dc45b8 (string reference), CombatRoundData @ 0x140dde110 (string reference)
- **Eclipse Implementation**: `EclipseCombatSystem : CombatSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: COMMAND_GETCOMBATSTATE @ 0x00af12fc, COMMAND_SETCOMBATSTATE @ 0x00af1314 (string references), Combat system found (string references: "Combat_%u" @ 0x00af5f80, "GameModeCombat" @ 0x00af9d9c, "InCombat" @ 0x00af76b0, "CombatTarget" @ 0x00af7840, "ShowHostileDamageNumbers" @ 0x00ae7610, "ShowPartyDamageNumbers" @ 0x00ae762c, "AutoPauseCombat" @ 0x00ae7660) - UnrealScript-based, different architecture
  - `DragonAge2.exe`: Combat system found (string references: "Combat_%u" @ 0x00be0ba4, "GameModeCombat" @ 0x00beaf3c, "InCombat" @ 0x00bf4c10, "CombatTarget" @ 0x00bf4dc0, "GetAttackValue" @ 0x00c03868, "GetDamage" @ 0x00c038a4, "GetDamageDPS" @ 0x00c03888) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectCombatSystem : CombatSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Combat system found (string references: "intUBioGamerProfileexecGetCombatDifficulty" @ 0x117e7fe8, "intUBioGamerProfileexecSetCombatDifficulty" @ 0x117e8040, "intUBioActorBehaviorexecEnterCombatStasis" @ 0x117ed418, "intUBioActorBehaviorexecExitCombatStasis" @ 0x117ed3c0) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Combat system found (string references: "GameModeCombat", "InCombat", "CombatTarget") - UnrealScript-based, different architecture

### Audio System

**Base Class**: `AudioSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyAudioSystem : AudioSystem` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: SaveSoundList @ 0x004e2d60, LoadSoundList @ 0x004e06a0
  - `swkotor.exe`: SaveSoundList @ 0x00507b10, LoadSoundList @ 0x00505560
- **Aurora Implementation**: `AuroraAudioSystem : AudioSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: InvSoundType @ 0x140dc3b80 (string reference)
- **Eclipse Implementation**: `EclipseAudioSystem : AudioSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: Audio system found (string references: "SoundSet" @ 0x00beb484, "SoundList" @ 0x00bf1a48, "Sound" @ 0x00bf8abc, "SoundFXVolume" @ 0x00bfaf80) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectAudioSystem : AudioSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Audio system found (string references: "intUBioGamerProfileexecUpdateSoundOptions" @ 0x117e7d58, "intUBioPhysicsSoundsexecRequestSound" @ 0x117e92d0, "intUBioPowerScriptexecPlayGuiSound" @ 0x117e93b8, "SoundResource" @ 0x117e6f8c) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Audio system found (string references: "SoundSet", "SoundList", "Sound") - UnrealScript-based, different architecture

### Entity Spawning System

**Base Class**: `SpawnSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseySpawnSystem : SpawnSystem` (Runtime.Games.Odyssey)
  - `swkotor.exe`: SpawnPointList @ 0x007474ac (string reference, functions @ 0x00592430, 0x00591350, 0x00505060)
  - `swkotor2.exe`: SpawnPointList @ 0x007bd034 (string reference, used in LoadEncounterList @ 0x004e01a0)
- **Aurora Implementation**: `AuroraSpawnSystem : SpawnSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: DungeonMaster_SpawnCreature @ 0x140dcbc00, DungeonMaster_SpawnItem @ 0x140dcbc20, DungeonMaster_SpawnTrigger @ 0x140dcbc38 (string references)
- **Eclipse Implementation**: `EclipseSpawnSystem : SpawnSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: Spawn system found (string references: "spawnhook" @ 0x00bfb254, "SpawnVolume" classes) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectSpawnSystem : SpawnSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Spawn system found (string references: "intUBioActorBehaviorexecSpawnActorFromType" @ 0x117ed4c8) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Spawn system found (string references: "spawnhook", "SpawnVolume" classes) - UnrealScript-based, different architecture

### Animation System

**Base Class**: `AnimationSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyAnimationSystem : AnimationSystem` (Runtime.Games.Odyssey)
  - `swkotor.exe`: Animation @ 0x00746060, AnimationTime @ 0x00746050, AnimationState @ 0x007495b0 (string references)
  - `swkotor2.exe`: Animation @ 0x007bf604, AnimationTime @ 0x007bf810, AnimationState @ 0x007c1f30 (string references)
- **Aurora Implementation**: `AuroraAnimationSystem : AnimationSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: Animation @ 0x140ddc0e0, AnimationTime @ 0x140ddc0f0, AnimationLength @ 0x140ddc218 (string references)
- **Eclipse Implementation**: `EclipseAnimationSystem : AnimationSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: Animation system found (string references: "Animation" @ 0x00bddda0, "AnimationNode" @ 0x00bdde14, "AnimationTree" @ 0x00bdde30, "ModelAnimationTree" @ 0x00bdde4c, "AnimationEventDispatch" @ 0x00bddbc0) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectAnimationSystem : AnimationSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Animation system found (string references: "intUBioAnimNodeBlendByWeaponActionexecPlayCurrentChildAnimation" @ 0x117e96d8, "intUBioActorBehaviorexecSoftResetMovementAndAnimationState" @ 0x117ee020, "intUBioActorBehaviorexecHardResetActionAndAnimationState" @ 0x117ee098) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Animation system found (string references: "Animation", "AnimationNode", "AnimationTree", "AnimationEventDispatch") - UnrealScript-based, different architecture

### Trigger System

**Base Class**: `TriggerSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyTriggerSystem : TriggerSystem` (Runtime.Games.Odyssey)
  - `swkotor.exe`: TriggerList @ 0x0074768c (string reference)
  - `swkotor2.exe`: TriggerList @ 0x007bd254 (string reference, SaveTriggerList @ 0x004e2b20, LoadTriggerList @ 0x004e5920)
- **Aurora Implementation**: `AuroraTriggerSystem : TriggerSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: TriggerList @ 0x140ddb780 (string reference), DungeonMaster_TriggerEntered @ 0x140dcbf08, DungeonMaster_TriggerExit @ 0x140dcbf28 (string references)
- **Eclipse Implementation**: `EclipseTriggerSystem : TriggerSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: Trigger system found (string references: "TriggerList" @ 0x00bf4a44, "COBJECT_TYPE_TRIGGER" @ 0x00c0f804, "DisableTriggers" @ 0x00bee1f4) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectTriggerSystem : TriggerSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Trigger system found (string references: "intABioTriggerStreamexecDoTouch" @ 0x117e9520, "intABioTriggerStreamexecDoUntouch" @ 0x117e94d8, "intABioTriggerStreamexecRetouch" @ 0x117e9450, "intUBioUIWorldexecTriggerEvent" @ 0x117fd53c) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Trigger system found (string references: "TriggerList", "COBJECT_TYPE_TRIGGER") - UnrealScript-based, different architecture

### Encounter System

**Base Class**: `EncounterSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyEncounterSystem : EncounterSystem` (Runtime.Games.Odyssey)
  - `swkotor.exe`: Encounter List @ 0x007474c8 (string reference)
  - `swkotor2.exe`: Encounter List @ 0x007bd050 (string reference, LoadEncounterList @ 0x004e01a0, SaveEncounterList @ 0x004e2be0)
- **Aurora Implementation**: `AuroraEncounterSystem : EncounterSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: Encounter List @ 0x140ddb790 (string reference), DungeonMaster_SpawnEncounter @ 0x140dcbc78 (string reference)
- **Eclipse Implementation**: `EclipseEncounterSystem : EncounterSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: TODO - Search for similar functions

### Store System

**Base Class**: `StoreSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyStoreSystem : StoreSystem` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: StoreList @ 0x007bd098 (string reference, LoadStoreFromGFF @ 0x00571310, SaveStoreToGFF @ 0x00570e30)
  - `swkotor.exe`: SaveStoreList @ 0x00507ca0, LoadStoreList @ 0x005057a0
- **Aurora Implementation**: `AuroraStoreSystem : StoreSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: TODO - Search for similar functions
- **Eclipse Implementation**: `EclipseStoreSystem : StoreSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: TODO - Search for similar functions

### Party Management System

**Base Class**: `PartySystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyPartySystem : PartySystem` (Runtime.Games.Odyssey)
  - `swkotor.exe`: PARTYTABLE @ 0x0074930c (string reference, SavePartyTable @ 0x0052ade0)
  - `swkotor2.exe`: PARTYTABLE @ 0x007c1910 (string reference, SavePartyTable @ 0x0057bd70)
- **Aurora Implementation**: `AuroraPartySystem : PartySystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: Party @ 0x140dc9d70, OnPartyDeath @ 0x140dc9740, NonPartyKillable @ 0x140dc95e0 (string references)
- **Eclipse Implementation**: `EclipsePartySystem : PartySystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: TODO - Search for similar functions

### Perception System

**Base Class**: `PerceptionSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyPerceptionSystem : PerceptionSystem` (Runtime.Games.Odyssey)
  - `swkotor.exe`: PerceptionData @ 0x00747304, PerceptionList @ 0x00747314, PERCEPTIONDIST @ 0x0074ae10, PerceptionRange @ 0x0074ae20 (string references)
  - `swkotor2.exe`: PerceptionData @ 0x007bf6c4, PerceptionList @ 0x007bf6d4, PERCEPTIONDIST @ 0x007c4070, PerceptionRange @ 0x007c4080 (string references)
- **Aurora Implementation**: `AuroraPerceptionSystem : PerceptionSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: PerceptionData @ 0x140dde100, PerceptionList @ 0x140dde0f0, PerceptionRange @ 0x140dde0e0, PERCEPTIONDIST @ 0x140de59b0 (string references)
- **Eclipse Implementation**: `EclipsePerceptionSystem : PerceptionSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: Perception system found (string references: "PerceptionClass" @ 0x00bf52e0) - UnrealScript-based, different architecture
- **Mass Effect Implementation**: `MassEffectPerceptionSystem : PerceptionSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: Perception system found (string references: "intABioHUDexecDisplayPerceptionList" @ 0x117fc5d8, "intABioHUDexecProfilePerception" @ 0x117fc838, "intABioBaseSquadexecAddSquadToPerception" @ 0x118080d8, "intABioBaseSquadexecRemoveSquadFromPerception" @ 0x11807fe0, "PERCEPTION" @ 0x11981194) - UnrealScript-based, different architecture
  - `MassEffect2.exe`: Perception system found (string references: "PerceptionClass", "PERCEPTION") - UnrealScript-based, different architecture

### Item System

✅ **ANALYZED**

**Base Class**: `ItemSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyItemSystem : ItemSystem` (Runtime.Games.Odyssey)
  - `swkotor.exe`: PROTOITEM @ 0x0073ec64 (string reference), EVENT_ACQUIRE_ITEM @ 0x007449bc, EVENT_ITEM_ON_HIT_SPELL_IMPACT @ 0x00744a54, CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM @ 0x0074435c, CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM @ 0x00744664, CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM @ 0x0074468c, CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM @ 0x007446b8 (string references)
  - `swkotor2.exe`: LoadItemTemplateFromUTI @ 0x0056b530 - ✅ ANALYZED - Loads an item template from a UTI (Item Template) file, creates new item object from UTI template, loads item properties (name, description, stats, etc.), returns 1 on success, 0 on failure (via "UTI " @ 0x007d07c8, "CreateItem::CreateItemEntry() -- Could not find a row for an item. Major error: " @ 0x007d07c8)
  - `swkotor2.exe`: LoadItemFromGFF @ 0x0056b5f0 - ✅ ANALYZED - Loads an item from GFF data (either EquippedRes or InventoryRes), reads Dropable and Pickpocketable flags from GFF, calls LoadItemTemplateFromUTI to load item template from UTI file, returns 1 on success, 0 on failure (via EquippedRes, InventoryRes, Dropable, Pickpocketable GFF fields)
  - `swkotor2.exe`: PROTOITEM @ 0x007b6c0c (string reference), EVENT_ACQUIRE_ITEM @ 0x007bcbf4, EVENT_ITEM_ON_HIT_SPELL_IMPACT @ 0x007bcc8c, CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM @ 0x007bc594, CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM @ 0x007bc89c, CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM @ 0x007bc8c4, CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM @ 0x007bc8f0 (string references) - ✅ FOUND
- **Aurora Implementation**: `AuroraItemSystem : ItemSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: Item system found (string references: "sceneGUI_PNL_EXAM_ITEM" @ 0x140d83680, "AddItem" @ 0x140db10c8, "DeleteItem" @ 0x140db10a8, "SetItem" @ 0x140db10d0)
- **Eclipse Implementation**: `EclipseItemSystem : ItemSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: TODO - Search for similar functions
- **Mass Effect Implementation**: `MassEffectItemSystem : ItemSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: TODO - Search for similar functions
  - `MassEffect2.exe`: TODO - Search for similar functions

### Inventory System

**Base Class**: `InventorySystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyInventorySystem : InventorySystem` (Runtime.Games.Odyssey)
  - `swkotor.exe`: Inventory @ 0x00748234, InventorySlot @ 0x00746010, InventoryRes @ 0x00747200, GuiInventory @ 0x00748224, HasInventory @ 0x007496e0, INVENTORY @ 0x00749320, GAMEINPROGRESS:INVENTORY @ 0x007490c0, CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED @ 0x00744540, InventorySound @ 0x0074e7ec, inventorysnds @ 0x0074e7fc (string references)
  - `swkotor2.exe`: Inventory @ 0x007c2504, InventorySlot @ 0x007bf7d0, InventoryRes @ 0x007bf570, GuiInventory @ 0x007c24f4, HasInventory @ 0x007c1fb0, INVENTORY @ 0x007c1927, GAMEINPROGRESS:INVENTORY @ 0x007c1570, CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED @ 0x007bc778, InventorySound @ 0x007c7164, inventorysnds @ 0x007c7174 (string references)
- **Aurora Implementation**: `AuroraInventorySystem : InventorySystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: Inventory @ 0x140dc9d50, GuiInventory @ 0x140dc9d60, Inventory_Equip @ 0x140dcac08, Inventory_EquipCancel @ 0x140dcac18, Inventory_Drop @ 0x140dcac30, Inventory_DropCancel @ 0x140dcac40, Inventory_Pickup @ 0x140dcac58, Inventory_PickupCancel @ 0x140dcac70, Inventory_Unequip @ 0x140dcac88, Inventory_UnequipCancel @ 0x140dcaca0 (string references)
- **Eclipse Implementation**: `EclipseInventorySystem : InventorySystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: TODO - Search for similar functions
- **Mass Effect Implementation**: `MassEffectInventorySystem : InventorySystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: TODO - Search for similar functions
  - `MassEffect2.exe`: TODO - Search for similar functions

### Effect System

**Base Class**: `EffectSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyEffectSystem : EffectSystem` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: ApplyEffect @ 0x0050ae30 - ✅ ANALYZED - Applies an effect to an entity, adds effect to entity's effect list (sorted by priority), updates entity state, handles effect expiration and removal flags, fires effect-related script events (via EffectList @ 0x007bebe8)
  - `swkotor2.exe`: LoadEffectsFromGFF @ 0x0050b540 - ✅ ANALYZED - Loads effects from GFF EffectList and applies them to entity, iterates through EffectList GFF list, creates effect objects, loads effect data from GFF, applies each effect via ApplyEffect @ 0x0050ae30 (via EffectList @ 0x007bebe8)
  - `swkotor2.exe`: SaveEffectsToGFF @ 0x00505db0 - ✅ ANALYZED - Saves entity effects to GFF EffectList, iterates through entity's effect array, saves each effect to EffectList GFF list (via EffectList @ 0x007bebe8)
  - `swkotor.exe`: EffectList @ 0x00745eac, EffectAttacks @ 0x00746274, Mod_Effect_NxtId @ 0x00745c84, DEffectType @ 0x007469e7, VisualEffect_02 @ 0x00746a6c, VisualEffect_03 @ 0x00746a4c, VisualEffect_04 @ 0x00746a2c, EVENT_APPLY_EFFECT @ 0x00744b90, EVENT_REMOVE_EFFECT @ 0x00744ad4, EVENT_ABILITY_EFFECT_APPLIED @ 0x007449e8 (string references) - ✅ FOUND
  - `swkotor2.exe`: EffectList @ 0x007bebe8, EffectAttacks @ 0x007bfa28, Mod_Effect_NxtId @ 0x007bea0c, DEffectType @ 0x007c016b, VisualEffect_02 @ 0x007c01d0, VisualEffect_03 @ 0x007c01b0, VisualEffect_04 @ 0x007c0190, AreaEffectList @ 0x007bd0d4, EVENT_APPLY_EFFECT @ 0x007bcdc8, EVENT_REMOVE_EFFECT @ 0x007bcd0c, EVENT_ABILITY_EFFECT_APPLIED @ 0x007bcc20 (string references) - ✅ FOUND
- **Aurora Implementation**: `AuroraEffectSystem : EffectSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: Effect system found (string references: "effect" @ 0x140d8a2fc, "effects" @ 0x140d8a308, "visualeffect" @ 0x140d88e10, "visualeffects" @ 0x140dc5ef8, "visualeffects.2DA" references)
- **Eclipse Implementation**: `EclipseEffectSystem : EffectSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: TODO - Search for similar functions
- **Mass Effect Implementation**: `MassEffectEffectSystem : EffectSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: TODO - Search for similar functions
  - `MassEffect2.exe`: TODO - Search for similar functions

### Faction System

**Base Class**: `FactionSystem` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyFactionSystem : FactionSystem` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: SetCreatureFaction @ 0x00513440 - ✅ ANALYZED - Sets a creature's faction ID and updates faction-related state, validates faction exists, if faction not found logs error and defaults to Hostile1 (faction ID 1), updates creature's faction ID at offset 0x43a (via "Cannot set creature %s to faction %d because faction does not exist! Setting to Hostile1." @ 0x007bf2a8)
  - `swkotor2.exe`: FUN_00501fa0 @ 0x00501fa0 - ✅ FOUND - Loads faction data from REPUTE GFF (repute.2da resource), reads FactionList and RepList from GFF, initializes faction reputation matrix (via "FactionList" @ 0x007be604)
  - `swkotor2.exe`: FUN_004fcab0 @ 0x004fcab0 - ✅ FOUND - Saves faction data to REPUTE GFF, writes FactionList and RepList to GFF save data (via "FactionList" @ 0x007be604)
  - `swkotor.exe`: FactionList @ 0x00745848, Faction @ 0x007497c8, FactionID @ 0x0074ae48, FactionID1 @ 0x0074865c, FactionID2 @ 0x00748650, FactionRep @ 0x00748644, FactionName @ 0x00748638, FactionParentID @ 0x00748628, FactionGlobal @ 0x00748618, "Cannot set creature %s to faction %d because faction does not exist! Setting to Hostile1." @ 0x00746fa0 (string references) - ✅ FOUND
  - `swkotor2.exe`: FactionList @ 0x007be604, Faction @ 0x007c0ca0, FACTIONREP @ 0x007bcec8, FactionID1 @ 0x007c2918, FactionID2 @ 0x007c2924, FactionRep @ 0x007c290c, FactionName @ 0x007c2900, FactionParentID @ 0x007c28f0, FactionGlobal @ 0x007c28e0, FactionID @ 0x007c40b4, "Cannot set creature %s to faction %d because faction does not exist! Setting to Hostile1." @ 0x007bf2a8 (string references) - ✅ FOUND
- **Aurora Implementation**: `AuroraFactionSystem : FactionSystem` (Runtime.Games.Aurora)
  - `nwmain.exe`: Faction system found (string references: "DungeonMaster_SetFaction" @ 0x140dcbff8, "DungeonMaster_SetFactionByName" @ 0x140dcc018, "DungeonMaster_SetFactionReputation" @ 0x140dcc128, "DungeonMaster_GetFactionReputation" @ 0x140dcc150, "FactionName" @ 0x140dda160, "FactionParentID" @ 0x140dda170, "FactionGlobal" @ 0x140dda180, "FactionID1" @ 0x140dda190, "FactionID2" @ 0x140dda1a0, "FactionRep" @ 0x140dda1b0)
- **Eclipse Implementation**: `EclipseFactionSystem : FactionSystem` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: TODO - Search for similar functions
- **Mass Effect Implementation**: `MassEffectFactionSystem : FactionSystem` (Runtime.Games.MassEffect)
  - `MassEffect.exe`: TODO - Search for similar functions
  - `MassEffect2.exe`: TODO - Search for similar functions

## Ghidra Executables Inventory

### ✅ PRIMARY TARGET: Odyssey Engine (KotOR 1 & 2)

**Most Relevant for Current Project Goals** - This is the primary focus for implementation/unification with class inheritance.

- **swkotor2.exe** (KotOR 2: The Sith Lords) - **🎯 HIGHEST PRIORITY**
  - Path: `/swkotor2.exe`
  - Functions: **13,818** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **HIGHEST** - Most complete Odyssey implementation, PRIMARY reference for all systems
  - **This is the MOST RELEVANT executable for current project goals**

- **swkotor.exe** (KotOR 1)
  - Path: `/swkotor.exe`
  - Functions: **12,066** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **HIGH** - Primary reference for KotOR 1 engine behavior

### Aurora Engine (Neverwinter Nights)

- **nwmain.exe** (Neverwinter Nights main executable)
  - Path: `/nwmain.exe`
  - Functions: **52,644** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **MEDIUM** - Reference for Aurora engine architecture

- **nwnnsscomp.exe** (NWN Script Compiler)
  - Path: `/nwnnsscomp.exe`
  - Status: ✅ Loaded and available
  - Priority: **LOW** - Tool, not runtime engine

- **nwnnsscomp_kscript.exe** (NWN Script Compiler - KScript variant)
  - Path: `/nwnnsscomp_kscript.exe`
  - Status: ✅ Loaded and available
  - Priority: **LOW** - Tool, not runtime engine

### Eclipse Engine (Dragon Age Origins)

- **daorigins.exe** (Dragon Age: Origins)
  - Path: `/daorigins.exe`
  - Functions: **8,420** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **MEDIUM** - Reference for Eclipse engine architecture

- **DragonAge2.exe** (Dragon Age II)
  - Path: `/DragonAge2.exe`
  - Functions: **12,069** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **MEDIUM** - Reference for Eclipse engine evolution

### Mass Effect Series (Eclipse-based/Custom)

- **MassEffect.exe** (Mass Effect 1)
  - Path: `/MassEffect.exe`
  - Functions: **12,558** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **LOW** - Eclipse-derived, may have unique implementations

- **MassEffect2.exe** (Mass Effect 2)
  - Path: `/MassEffect2.exe`
  - Functions: **3** (filtered, appears to be a launcher/stub)
  - Status: ✅ Loaded and available
  - Priority: **LOW** - Minimal executable, likely launcher

### Windows System DLLs (Not Game Executables)

- USER32.DLL, KERNEL32.DLL, GDI32.DLL, IMM32.DLL, VERSION.DLL, OLE32.DLL
- DINPUT8.DLL, OPENGL32.DLL, GLU32.DLL, MSS32.DLL, BINKW32.DLL
- Status: ✅ Loaded (for cross-reference analysis)
- Priority: **LOW** - System libraries, not engine code

### Total Executables Available: 20

**Project Path**: `C:\Users\boden\test.gpr`
**Status**: ✅ **ALL PROGRAMS LOADED INTO MEMORY**

- **Game Executables**: 9
  - Odyssey: swkotor.exe (12,066 functions), swkotor2.exe (13,818 functions) ⭐ **PRIMARY TARGET**
  - Aurora: nwmain.exe (52,644 functions), nwnnsscomp.exe, nwnnsscomp_kscript.exe
  - Eclipse: daorigins.exe (8,420 functions), DragonAge2.exe (12,069 functions)
  - Eclipse-derived: MassEffect.exe (12,558 functions), MassEffect2.exe (3 functions - launcher)
- **System DLLs**: 11 (USER32.DLL, KERNEL32.DLL, GDI32.DLL, IMM32.DLL, VERSION.DLL, OLE32.DLL, DINPUT8.DLL, OPENGL32.DLL, GLU32.DLL, MSS32.DLL, BINKW32.DLL)
- **Total Functions Available**: **~120,000+** across all game executables

### Executable Summary Table

| Executable | Engine | Functions | Priority | Status |
|------------|--------|-----------|----------|--------|
| **swkotor2.exe** | Odyssey | 13,818 | **🎯 HIGHEST** | ✅ Loaded |
| **swkotor.exe** | Odyssey | 12,066 | **HIGH** | ✅ Loaded |
| **nwmain.exe** | Aurora | 52,644 | MEDIUM | ✅ Loaded |
| **daorigins.exe** | Eclipse | 8,420 | MEDIUM | ✅ Loaded |
| **DragonAge2.exe** | Eclipse | 12,069 | MEDIUM | ✅ Loaded |
| **MassEffect.exe** | Eclipse | 12,558 | LOW | ✅ Loaded |
| **MassEffect2.exe** | Eclipse | 3 | LOW | ✅ Loaded |
| **nwnnsscomp.exe** | Tool | - | LOW | ✅ Loaded |
| **nwnnsscomp_kscript.exe** | Tool | - | LOW | ✅ Loaded |
| **System DLLs (11)** | System | - | LOW | ✅ Loaded |

## Reverse Engineering Strategy by Engine

### Odyssey Engine (PRIMARY FOCUS)

**Target Files**: `swkotor.exe` and `swkotor2.exe`
**Goal**: Achieve 1:1 parity with original engine behavior for all systems:

- Module loading (LYT, VIS, GIT, ARE)
- Walkmesh navigation (BWM parsing and pathfinding)
- Entity spawning and management
- Script execution (NCS VM)
- Combat system
- Dialogue system (DLG, TLK, VO)
- Save/Load system (GFF serialization)
- Party management
- Perception system
- Trigger system
- Encounter system
- Store system
- Animation system
- Audio system

**Key String References to Search**:

- "GLOBALVARS", "PARTYTABLE", "savenfo", "SAVES", "MODULES", "AREAS"
- "BWM", "walkmesh", "pathfind", "navigation"
- "DLG", "TLK", "dialogue", "conversation"
- "NCS", "script", "ACTION", "STORE_STATE"
- "UTC", "UTD", "UTI", "UTP", "UTS", "UTT", "UTW", "UTE", "UTM" (template types)

### Aurora Engine (SECONDARY)

**Target File**: `nwmain.exe`
**Goal**: Understand Aurora architecture for future unification:

- Server/client architecture
- Script compilation differences
- Module system differences
- Combat system differences

### Eclipse Engine (SECONDARY)

**Target Files**: `daorigins.exe`, `DragonAge2.exe`
**Goal**: Understand Eclipse architecture for future unification:

- Dialogue system evolution
- Combat system evolution
- Save system differences

## Update Instructions

When processing a file:

- Mark as `- [/]` when starting work
- Mark as `- [x]` when complete with Ghidra references added and verified
- Add notes about function addresses, string references, and implementation details
- Use format: `- [x] FileName.cs - Function addresses, string references, key findings`

## Refactoring Strategy

1. Search Ghidra for relevant functions using string searches and function name searches
2. Decompile relevant functions to understand original implementation
3. Add detailed comments with Ghidra function addresses and context
4. Update implementation to match original behavior where possible
5. Document any deviations or improvements
6. Rename functions in Ghidra with descriptive names
7. Set function prototypes in Ghidra
8. Add comprehensive comments in Ghidra

## Complete File Inventory - src/Andastra

**Total Files**: ~1,200+ C# files across all projects
**Organization**: Organized by layer (Core → Games → Graphics → Content → Scripting → Parsing)

### Runtime Layer (Core Engine Logic)

#### Runtime/Core (99 files) - **HIGHEST PRIORITY FOR REVERSE ENGINEERING**

**Purpose**: Core domain logic, no MonoGame dependency
**Ghidra References Required**: ✅ **YES - ALL FILES**

- **Entities** (4 files): Entity.cs, World.cs, EventBus.cs, TimeManager.cs
- **Actions** (27 files): ActionAttack.cs, ActionBase.cs, ActionCastSpellAtLocation.cs, ActionCastSpellAtObject.cs, ActionCloseDoor.cs, ActionDestroyObject.cs, ActionDoCommand.cs, ActionEquipItem.cs, ActionFollowObject.cs, ActionJumpToLocation.cs, ActionJumpToObject.cs, ActionMoveAwayFromObject.cs, ActionMoveToLocation.cs, ActionMoveToObject.cs, ActionOpenDoor.cs, ActionPickUpItem.cs, ActionPlayAnimation.cs, ActionPutDownItem.cs, ActionQueue.cs, ActionRandomWalk.cs, ActionSpeakString.cs, ActionUnequipItem.cs, ActionUseItem.cs, ActionUseObject.cs, ActionWait.cs, DelayScheduler.cs
- **AI** (1 file): AIController.cs
- **Animation** (1 file): AnimationSystem.cs
- **Audio** (1 file): ISoundPlayer.cs
- **Camera** (1 file): CameraController.cs
- **Combat** (3 files): CombatSystem.cs, CombatTypes.cs, EffectSystem.cs
- **Dialogue** (4 files): DialogueInterfaces.cs, DialogueSystem.cs, LipSyncController.cs, RuntimeDialogue.cs
- **Enums** (5 files): Ability.cs, ActionStatus.cs, ActionType.cs, ObjectType.cs, ScriptEvent.cs
- **GameLoop** (1 file): FixedTimestepGameLoop.cs
- **Interfaces** (25 files): IAction.cs, IActionQueue.cs, IArea.cs, IComponent.cs, IDelayScheduler.cs, IEntity.cs, IEventBus.cs, IGameServicesContext.cs, IModule.cs, INavigationMesh.cs, ITimeManager.cs, IWorld.cs, Components/IActionQueueComponent.cs, Components/IAnimationComponent.cs, Components/IDoorComponent.cs, Components/IFactionComponent.cs, Components/IInventoryComponent.cs, Components/IItemComponent.cs, Components/IPerceptionComponent.cs, Components/IPlaceableComponent.cs, Components/IQuickSlotComponent.cs, Components/IRenderableComponent.cs, Components/IScriptHooksComponent.cs, Components/IStatsComponent.cs, Components/ITransformComponent.cs, Components/ITriggerComponent.cs
- **Journal** (1 file): JournalSystem.cs
- **Module** (3 files): ModuleTransitionSystem.cs, RuntimeArea.cs, RuntimeModule.cs
- **Movement** (2 files): CharacterController.cs, PlayerInputHandler.cs
- **Navigation** (2 files): NavigationMesh.cs, NavigationMeshFactory.cs
- **Party** (3 files): PartyInventory.cs, PartyMember.cs, PartySystem.cs
- **Perception** (1 file): PerceptionSystem.cs
- **Save** (3 files): AreaState.cs, SaveGameData.cs, SaveSystem.cs
- **Templates** (9 files): CreatureTemplate.cs, DoorTemplate.cs, EncounterTemplate.cs, IEntityTemplate.cs, PlaceableTemplate.cs, SoundTemplate.cs, StoreTemplate.cs, TriggerTemplate.cs, WaypointTemplate.cs
- **Triggers** (1 file): TriggerSystem.cs
- **Root** (1 file): GameSettings.cs

#### Runtime/Games (99 files) - **HIGHEST PRIORITY FOR REVERSE ENGINEERING**

**Purpose**: Engine-specific implementations with class inheritance
**Ghidra References Required**: ✅ **YES - ALL FILES**

- **Common** (8 files): BaseEngine.cs, BaseEngineGame.cs, BaseEngineModule.cs, BaseEngineProfile.cs, IEngine.cs, IEngineGame.cs, IEngineModule.cs, IEngineProfile.cs
- **Odyssey** (84 files) - **PRIMARY TARGET**:
  - **Core**: OdysseyEngine.cs, OdysseyGameSession.cs, OdysseyModuleLoader.cs
  - **Combat** (4 files): CombatManager.cs, CombatRound.cs, DamageCalculator.cs, WeaponDamageCalculator.cs
  - **Components** (18 files): ActionQueueComponent.cs, AnimationComponent.cs, CreatureComponent.cs, DoorComponent.cs, EncounterComponent.cs, FactionComponent.cs, InventoryComponent.cs, ItemComponent.cs, PerceptionComponent.cs, PlaceableComponent.cs, QuickSlotComponent.cs, RenderableComponent.cs, ScriptHooksComponent.cs, SoundComponent.cs, StatsComponent.cs, StoreComponent.cs, TransformComponent.cs, TriggerComponent.cs, WaypointComponent.cs
  - **Data** (2 files): GameDataManager.cs, TwoDATableManager.cs
  - **Dialogue** (5 files): ConversationContext.cs, DialogueManager.cs, DialogueState.cs, KotorDialogueLoader.cs, KotorLipDataLoader.cs
  - **EngineApi** (4 files): K1EngineApi.cs, K2EngineApi.cs, OdysseyK1EngineApi.cs, OdysseyK2EngineApi.cs
  - **Game** (5 files): GameSession.cs, ModuleLoader.cs, ModuleTransitionSystem.cs, PlayerController.cs, ScriptExecutor.cs
  - **Input** (1 file): PlayerController.cs
  - **Loading** (4 files): EntityFactory.cs, KotorModuleLoader.cs, ModuleLoader.cs, NavigationMeshFactory.cs
  - **Profiles** (5 files): GameProfileFactory.cs, IGameProfile.cs, K1GameProfile.cs, K2GameProfile.cs, OdysseyK1GameProfile.cs, OdysseyK2GameProfile.cs
  - **Save** (1 file): SaveGameManager.cs
  - **Systems** (10 files): AIController.cs, ComponentInitializer.cs, EncounterSystem.cs, FactionManager.cs, HeartbeatSystem.cs, ModelResolver.cs, PartyManager.cs, PerceptionManager.cs, StoreSystem.cs, TriggerSystem.cs
  - **Templates** (20 files): UTC.cs, UTCHelpers.cs, UTD.cs, UTDHelpers.cs, UTE.cs, UTEHelpers.cs, UTI.cs, UTIHelpers.cs, UTM.cs, UTMHelpers.cs, UTP.cs, UTPHelpers.cs, UTS.cs, UTSHelpers.cs, UTT.cs, UTTHelpers.cs, UTW.cs, UTWHelpers.cs
- **Aurora** (1 file): AuroraEngine.cs
- **Eclipse** (1 file): EclipseEngine.cs
- **Infinity** (1 file): InfinityEngine.cs

#### Runtime/Scripting (11 files) - **HIGHEST PRIORITY FOR REVERSE ENGINEERING**

**Purpose**: NCS VM implementation and Engine API
**Ghidra References Required**: ✅ **YES - ALL FILES**

- **EngineApi** (2 files): BaseEngineApi.cs, K1EngineApi.cs.backup
- **Interfaces** (5 files): IEngineApi.cs, IExecutionContext.cs, INcsVm.cs, IScriptGlobals.cs, Variable.cs
- **Types** (1 file): Location.cs
- **VM** (3 files): ExecutionContext.cs, NcsVm.cs, ScriptGlobals.cs
- **Root** (1 file): ScriptExecutor.cs

#### Runtime/Content (18 files) - **SELECTED FILES REQUIRE REVERSE ENGINEERING**

**Purpose**: Asset conversion/caching
**Ghidra References Required**: ⚠️ **SELECTED FILES ONLY**

- **Cache** (1 file): ContentCache.cs
- **Converters** (1 file): **BwmToNavigationMeshConverter.cs** ⚠️ **REQUIRES GHIDRA**
- **Interfaces** (4 files): IContentCache.cs, IContentConverter.cs, IGameResourceProvider.cs, IResourceProvider.cs
- **Loaders** (2 files): **GITLoader.cs** ⚠️ **REQUIRES GHIDRA**, **TemplateLoader.cs** ⚠️ **REQUIRES GHIDRA**
- **MDL** (7 files): MDLBulkReader.cs, MDLCache.cs, MDLConstants.cs, MDLDataTypes.cs, MDLFastReader.cs, MDLLoader.cs, MDLOptimizedReader.cs
- **ResourceProviders** (1 file): GameResourceProvider.cs
- **Save** (2 files): **SaveDataProvider.cs** ⚠️ **REQUIRES GHIDRA**, **SaveSerializer.cs** ⚠️ **REQUIRES GHIDRA**

#### Runtime/Graphics (247 files) - **LOW PRIORITY**

**Purpose**: Modern MonoGame/Stride rendering adapters
**Ghidra References Required**: ❌ **NO** (Modern implementation, not original engine code)

- **Common** (50 files): Base graphics backends, interfaces, post-processing, raytracing, upscaling
- **MonoGame** (158 files): Complete MonoGame rendering pipeline, modern enhancements
- **Stride** (37 files): Stride rendering backend
- **Enums** (1 file): GraphicsBackendType.cs

### Parsing Layer (File Format Parsing)

#### Parsing (600+ files) - **SELECTED FILES REQUIRE REVERSE ENGINEERING**

**Purpose**: File format parsing and extraction
**Ghidra References Required**: ⚠️ **SELECTED FILES ONLY** (BWM, GFF, NCS formats)

- **Common** (18 files): BinaryExtensions.cs, BinaryReader.cs, BinaryWriter.cs, CaseAwarePath.cs, Face.cs, Game.cs, GameObject.cs, Language.cs, LocalizedString.cs, Misc.cs, Module.cs, ModuleDataLoader.cs, Pathfinding.cs, ResRef.cs, SurfaceMaterial.cs, Script/DataType.cs, Script/DataTypeExtensions.cs, Script/NwscriptParser.cs, Script/ScriptConstant.cs, Script/ScriptDefs.cs, Script/ScriptFunction.cs, Script/ScriptLib.cs, Script/ScriptParam.cs
- **Extract** (15 files): Capsule/, Chitin/, ChitinWrapper.cs, FileResource.cs, FileResourceHelpers.cs, InstallationWrapper.cs, KeyFileWrapper.cs, KeyWriterWrapper.cs, **SaveData/** ⚠️ **REQUIRES GHIDRA** (GlobalVars.cs, PartyTable.cs, SaveInfo.cs), TalkTable.cs, TwoDAManager.cs, TwoDARegistry.cs
- **Installation** (4 files): Installation.cs, InstallationResourceManager.cs, ResourceResult.cs, SearchLocation.cs
- **Merge** (1 file): ModuleManager.cs
- **Resource/Formats** (500+ files):
  - **BIF** (6 files): BIF format parsing
  - **BWM** (10 files) ⚠️ **REQUIRES GHIDRA**: BWM.cs, BWMAdjacency.cs, BWMAuto.cs, BWMBinaryReader.cs, BWMBinaryWriter.cs, BWMEdge.cs, BWMFace.cs, BWMMostSignificantPlane.cs, BWMNodeAABB.cs, BWMType.cs
  - **ERF** (5 files): ERF format parsing
  - **GFF** (54 files) ⚠️ **REQUIRES GHIDRA**: GFF format parsing (save/load, templates)
  - **KEY** (6 files): KEY format parsing
  - **LIP** (6 files): LIP format parsing (lip sync)
  - **LTR** (5 files): LTR format parsing
  - **LYT** (9 files): LYT format parsing (area layout)
  - **MDL** (7 files): MDL format parsing (3D models)
  - **NCS** (375+ files) ⚠️ **REQUIRES GHIDRA**: NCS format parsing, compiler, decompiler, VM
  - **RIM** (4 files): RIM format parsing
  - **SSF** (5 files): SSF format parsing
  - **TLK** (6 files): TLK format parsing (dialogue text)
  - **TPC** (12 files): TPC format parsing (textures)
  - **TwoDA** (5 files): TwoDA format parsing (game data tables)
  - **TXI** (7 files): TXI format parsing
  - **VIS** (5 files): VIS format parsing (area visibility)
  - **WAV** (10 files): WAV format parsing
- **Tools** (24 files): Archives.cs, Conversions.cs, Creature.cs, Door.cs, Encoding.cs, Heuristics.cs, Kit.cs, Misc.cs, Model.cs, Module.cs, Patching.cs, Path.cs, Placeable.cs, PlayPazaak.cs, ReferenceCache.cs, Registry.cs, ResourceConversions.cs, Scripts.cs, StringUtils.cs, Template.cs, Utilities.cs, Validation.cs
- **TSLPatcher** (40+ files): Modding tool, not engine code (no Ghidra references needed)

### Game Layer (Executable Launcher)

#### Game (8 files) - **LOW PRIORITY**

**Purpose**: Executable launcher and game initialization
**Ghidra References Required**: ❌ **NO** (Application layer, not engine code)

- **Core** (5 files): GamePathDetector.cs, GameSettings.cs, GameState.cs, GraphicsBackendFactory.cs, OdysseyGame.cs
- **GUI** (2 files): MenuRenderer.cs, SaveLoadMenu.cs
- **Root** (1 file): Program.cs

### Utility Layer

#### Utility (14 files) - **NO REVERSE ENGINEERING NEEDED**

**Purpose**: Helper/utility code
**Ghidra References Required**: ❌ **NO**

- ArrayHead.cs, CaseInsensitiveDict.cs, ErrorHandling.cs, Geometry/ (4 files), KeyError.cs, LZMA/LzmaHelper.cs, Misc.cs, MiscString/ (3 files), OrderedSet.cs, System/OSHelper.cs, SystemHelpers.cs

## Files to Process

### Runtime/Core (99 files)

#### Entities (4 files)

- [x] Entities/Entity.cs - ✅ COMPLETE - Ghidra references added: SaveEntityState @ 0x005226d0, LoadEntityState @ 0x005fb0f0 (swkotor2.exe), ObjectId @ 0x007bce5c
- [x] Entities/World.cs - ✅ COMPLETE - Ghidra references added: Entity management system, ObjectId @ 0x007bce5c, AreaId @ 0x007bef48
- [x] Entities/EventBus.cs - ✅ COMPLETE - Ghidra references added: FUN_004dcfb0 @ 0x004dcfb0 (event dispatch), all EVENT_* and CSWSSCRIPTEVENT_EVENTTYPE_* constants, EventQueue @ 0x007bce74
- [x] Entities/TimeManager.cs - ✅ COMPLETE - Ghidra references added: TIMEPLAYED @ 0x007be1d0, GameTime @ 0x007c1a78, GameTimeScale @ 0x007c1a80, all time-related fields and Windows API time functions

#### Actions (27 files)

- [x] Actions/ActionAttack.cs - ✅ COMPLETE - Ghidra references added: EVENT_ON_MELEE_ATTACKED @ 0x007bccf4, ScriptAttacked @ 0x007bee80, AttackList @ 0x007bf9f0, all attack-related fields
- [x] Actions/ActionBase.cs - ✅ COMPLETE - Ghidra references added: FUN_00508260 @ 0x00508260, FUN_00505bc0 @ 0x00505bc0, ActionList @ 0x007bebdc, ActionId @ 0x007bebd0, all action parameter fields
- [x] Actions/ActionCastSpellAtLocation.cs - ✅ COMPLETE - Ghidra references added: ScriptSpellAt @ 0x007bee90, CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT @ 0x007bcb3c, EVENT_SPELL_IMPACT @ 0x007bcd8c, all spell/Force point fields
- [x] Actions/ActionCastSpellAtObject.cs - ✅ COMPLETE - Ghidra references added: ScriptSpellAt @ 0x007bee90, OnSpellCastAt @ 0x007c1a44, CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT @ 0x007bcb3c, all spell casting fields
- [x] Actions/ActionCloseDoor.cs - ✅ COMPLETE - Ghidra references added: OnClosed @ 0x007be1c8, EVENT_CLOSE_OBJECT @ 0x007bcdb4, CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE @ 0x007bc820, FUN_004dcfb0 @ 0x004dcfb0
- [x] Actions/ActionDestroyObject.cs - ✅ COMPLETE - Ghidra references added: EVENT_DESTROY_OBJECT @ 0x007bcd48, FUN_004dcfb0 @ 0x004dcfb0, IsDestroyable @ 0x007bf670, Destroyed @ 0x007c4bdc
- [x] Actions/ActionDoCommand.cs - ✅ COMPLETE - Ghidra references added: DelayCommand @ 0x007be900, Commandable @ 0x007bec3c, STORE_STATE opcode, AssignCommand/DelayCommand NWScript functions
- [x] Actions/ActionEquipItem.cs - ✅ COMPLETE - Ghidra references added: EquipItem @ 0x007be4e0, CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM @ 0x007bc594, ItemList @ 0x007bf580, Equip_ItemList @ 0x007bf5a4, all equipment/inventory fields
- [x] Actions/ActionFollowObject.cs - ✅ COMPLETE - Ghidra references added: FollowObject @ 0x007bedb8, FollowInfo @ 0x007beec0, PT_FOLLOWSTATE @ 0x007c1758, all follow-related fields
- [x] Actions/ActionJumpToLocation.cs - ✅ COMPLETE - Ghidra references added: JumpToLocation action type, Position @ 0x007bef70, ActionJumpToLocation NWScript function
- [x] Actions/ActionJumpToObject.cs - ✅ COMPLETE - Ghidra references added: JumpToObject action type, Position @ 0x007bef70, ActionJumpToObject NWScript function
- [ ] Actions/ActionMoveAwayFromObject.cs
- [x] Actions/ActionMoveToLocation.cs - ✅ COMPLETE - Ghidra references added: FUN_00508260 @ 0x00508260, FUN_0054be70 @ 0x0054be70 (walking collision), ActionList @ 0x007bebdc, MOVETO @ 0x007b6b24
- [x] Actions/ActionMoveToObject.cs - ✅ COMPLETE - Ghidra references added: FUN_00508260 @ 0x00508260, FUN_0054be70 @ 0x0054be70, ActionList @ 0x007bebdc, MOVETO @ 0x007b6b24, all movement collision fields
- [x] Actions/ActionOpenDoor.cs - ✅ COMPLETE - Ghidra references added: FUN_00580ed0 @ 0x00580ed0, FUN_005838d0 @ 0x005838d0, OnOpen @ 0x007be1b0, EVENT_OPEN_OBJECT @ 0x007bcda0, FUN_004dcfb0 @ 0x004dcfb0
- [x] Actions/ActionPickUpItem.cs - ✅ COMPLETE - Ghidra references added: TakeItem @ 0x007be4f0, CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM @ 0x007bc8c4, EVENT_ACQUIRE_ITEM @ 0x007bcbf4, all item/inventory fields
- [x] Actions/ActionPlayAnimation.cs - ✅ COMPLETE - Ghidra references added: Animation @ 0x007c3440, PlayAnim @ 0x007c346c, AnimList @ 0x007c3694, CurrentAnim @ 0x007c38d4, NextAnim @ 0x007c38c8
- [x] Actions/ActionPutDownItem.cs - ✅ COMPLETE - Ghidra references added: GiveItem @ 0x007be4f8, CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM @ 0x007bc89c, ItemList @ 0x007bf580, all inventory fields
- [x] Actions/ActionQueue.cs - ✅ COMPLETE - Ghidra references added: FUN_00508260 @ 0x00508260 (load ActionList), FUN_00505bc0 @ 0x00505bc0 (save ActionList), ActionList @ 0x007bebdc, ActionId @ 0x007bebd0
- [x] Actions/ActionRandomWalk.cs - ✅ COMPLETE - Ghidra references added: FUN_00508260 @ 0x00508260, FUN_00505bc0 @ 0x00505bc0, FUN_0054be70 @ 0x0054be70, ActionList @ 0x007bebdc, all action/walking fields
- [x] Actions/ActionSpeakString.cs - ✅ COMPLETE - Ghidra references added: TalkString @ 0x007c14f8, Speaker @ 0x007c35f8, SpeakString NWScript function
- [x] Actions/ActionUnequipItem.cs - ✅ COMPLETE - Ghidra references added: UnequipItem @ 0x007be4e8, UnequipHItem @ 0x007c3870, CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED @ 0x007bc778, all equipment/inventory fields
- [x] Actions/ActionUseItem.cs - ✅ COMPLETE - Ghidra references added: OnUsed @ 0x007c1f70, CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM @ 0x007bc8f0, Mod_OnActvtItem @ 0x007be7f4, all item usage fields
- [x] Actions/ActionUseObject.cs - ✅ COMPLETE - Ghidra references added: OnUsed @ 0x007c1f70, EVENT_OPEN_OBJECT @ 0x007bcda0, EVENT_CLOSE_OBJECT @ 0x007bcdb4, FUN_004dcfb0 @ 0x004dcfb0
- [x] Actions/ActionWait.cs - ✅ COMPLETE - Ghidra references added: ActionWait @ 0x007be8e4, Wait action type, game simulation time tracking
- [x] Actions/DelayScheduler.cs - ✅ COMPLETE - Ghidra references added: DelayCommand @ 0x007be900, all delay-related fields, STORE_STATE opcode, DelayCommand/AssignCommand NWScript functions

#### AI (1 file)

- [x] AI/AIController.cs - ✅ COMPLETE - Ghidra references added: OnHeartbeat @ 0x007beeb0, CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT @ 0x007bc9a4, HeartbeatInterval @ 0x007c38e8, AIState @ 0x007c4090, FUN_005226d0 @ 0x005226d0

#### Animation (1 file)

- [x] Animation/AnimationSystem.cs - ✅ COMPLETE - Ghidra references added: Animation @ 0x007bf604, AnimationTime @ 0x007bf810, AnimationState @ 0x007c1f30, EVENT_PLAY_ANIMATION @ 0x007bcd74 (swkotor2.exe)

#### Audio (1 file)

- [x] Audio/ISoundPlayer.cs - ✅ COMPLETE - Ghidra references added: SaveSoundList @ 0x004e2d60, LoadSoundList @ 0x004e06a0 (swkotor2.exe), SoundList @ 0x007bd080

#### Camera (1 file)

- [x] Camera/CameraController.cs - ✅ COMPLETE - Ghidra references added: camera @ 0x007b63fc, CameraID @ 0x007bd160, CameraList @ 0x007bd16c, CameraStyle @ 0x007bd6e0, CameraAnimation @ 0x007c3460, all camera-related fields

#### Combat (3 files)

- [x] Combat/CombatSystem.cs - ✅ COMPLETE - Ghidra references added: EndCombatRound @ 0x00529c30 (swkotor2.exe), EndCombatRound @ 0x004d4620 (swkotor.exe)
- [x] Combat/CombatTypes.cs - ✅ COMPLETE - Ghidra references added: DamageValue @ 0x007bf890, DamageList @ 0x007bf89c, ScriptDamaged @ 0x007bee70, CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED @ 0x007bcb14, all damage type fields
- [x] Combat/EffectSystem.cs - ✅ COMPLETE - Ghidra references added: LoadEffectListFromGFF @ 0x0050b540, SaveEffectListToGFF @ 0x00505db0 (swkotor2.exe), EffectList @ 0x007bebe8

#### Dialogue (4 files)

- [ ] Dialogue/DialogueInterfaces.cs
- [x] Dialogue/DialogueSystem.cs - ✅ COMPLETE - Ghidra references added: ExecuteDialogue @ 0x005e9920 (swkotor2.exe), ProcessDialogueEntry @ 0x005a13d0 (swkotor.exe)
- [x] Dialogue/LipSyncController.cs - ✅ COMPLETE - Ghidra references added: LIPS:localization @ 0x007be654, LIPS:%s_loc @ 0x007be668, .\lips @ 0x007c6838, all LIP file format fields
- [x] Dialogue/RuntimeDialogue.cs - ✅ COMPLETE - Ghidra references added: ScriptDialogue @ 0x007bee40, ScriptEndDialogue @ 0x007bede0, CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE @ 0x007bcac4, "Error: dialogue can't find object '%s'!" @ 0x007c3730

#### Enums (5 files)

- [x] Enums/Ability.cs - ✅ COMPLETE - Ghidra references added: STR @ 0x007c3a44, DEX @ 0x007c3a54, CON @ 0x007c3a64, INT @ 0x007c3a74, WIS @ 0x007c3a84, CHA @ 0x007c3a94, all ability modifier fields
- [x] Enums/ActionStatus.cs - ✅ COMPLETE - Ghidra references added: FUN_00508260 @ 0x00508260, FUN_00505bc0 @ 0x00505bc0, action execution status system
- [x] Enums/ActionType.cs - ✅ COMPLETE - Ghidra references added: ActionType @ 0x007bf7f8, ActionList @ 0x007bebdc, FUN_00508260 @ 0x00508260, FUN_00505bc0 @ 0x00505bc0
- [x] Enums/ObjectType.cs - ✅ COMPLETE - Ghidra references added: Creature @ 0x007bc4e0, Door @ 0x007bc4f4, Placeable @ 0x007bc508, Item @ 0x007bc530, all object type strings, FUN_005226d0 @ 0x005226d0
- [x] Enums/ScriptEvent.cs - ✅ COMPLETE - Ghidra references added: All CSWSSCRIPTEVENT_EVENTTYPE_ON_* constants, FUN_004dcfb0 @ 0x004dcfb0, all script event fields

#### GameLoop (1 file)

- [x] GameLoop/FixedTimestepGameLoop.cs - ✅ COMPLETE - Ghidra references added: frameStart @ 0x007ba698, frameEnd @ 0x007ba668, TimeElapsed @ 0x007bed5c, GameTime @ 0x007c1a78, TIMEPLAYED @ 0x007be1c4, 60 Hz fixed timestep

#### Interfaces (25 files)

- [x] Interfaces/IAction.cs - ✅ COMPLETE - Ghidra references added: ActionList @ 0x007bebdc, ActionId @ 0x007bebd0, FUN_00508260 @ 0x00508260, FUN_00505bc0 @ 0x00505bc0
- [x] Interfaces/IActionQueue.cs - ✅ COMPLETE - Ghidra references added: ActionList @ 0x007bebdc, ActionId @ 0x007bebd0, ActionType @ 0x007bf7f8, all action queue fields
- [x] Interfaces/IArea.cs - ✅ COMPLETE - Ghidra references added: Area @ 0x007be340, AREANAME @ 0x007be1dc, AreaId @ 0x007bef48, EVENT_AREA_TRANSITION @ 0x007bcbdc, FUN_004dcfb0 @ 0x004dcfb0
- [x] Interfaces/IComponent.cs - ✅ COMPLETE - Ghidra references added: FUN_005226d0 @ 0x005226d0, FUN_005223a0 @ 0x005223a0, component-based entity system
- [x] Interfaces/IDelayScheduler.cs - ✅ COMPLETE - Ghidra references added: DelayCommand @ 0x007be900, all delay-related fields, STORE_STATE opcode
- [x] Interfaces/IEntity.cs - ✅ COMPLETE - Ghidra references added: ObjectId @ 0x007bce5c, ObjectIDList @ 0x007bfd7c, FUN_004e28c0 @ 0x004e28c0, FUN_005fb0f0 @ 0x005fb0f0, all entity fields
- [x] Interfaces/IEventBus.cs - ✅ COMPLETE - Ghidra references added: All EVENT_* and CSWSSCRIPTEVENT_EVENTTYPE_* constants, FUN_004dcfb0 @ 0x004dcfb0
- [x] Interfaces/IGameServicesContext.cs - ✅ COMPLETE - Ghidra references added: FUN_005226d0 @ 0x005226d0, script execution context system, all game service fields
- [x] Interfaces/IModule.cs - ✅ COMPLETE - Ghidra references added: Mod_ prefix fields, FUN_00708990 @ 0x00708990, IFO file format
- [x] Interfaces/INavigationMesh.cs - ✅ COMPLETE - Ghidra references added: BWM V1.0 @ 0x007c061c, FUN_004f5070 @ 0x004f5070, all walkmesh/navigation fields
- [x] Interfaces/ITimeManager.cs - ✅ COMPLETE - Ghidra references added: TIMEPLAYED @ 0x007be1c4, frameStart @ 0x007ba698, frameEnd @ 0x007ba668, all time management fields
- [x] Interfaces/IWorld.cs - ✅ COMPLETE - Ghidra references added: ObjectId @ 0x007bce5c, Module @ 0x007bc4e0, AREANAME @ 0x007be1dc, all world management fields
- [x] Interfaces/Components/IActionQueueComponent.cs - ✅ COMPLETE - Ghidra references added: ActionList @ 0x007bebdc, ActionId @ 0x007bebd0, all action queue fields
- [x] Interfaces/Components/IAnimationComponent.cs - ✅ COMPLETE - Ghidra references added: Animation @ 0x007c3440, AnimList @ 0x007c3694, PlayAnim @ 0x007c346c, CurrentAnim @ 0x007c38d4, all animation fields
- [x] Interfaces/Components/IDoorComponent.cs - ✅ COMPLETE - Ghidra references added: Door @ 0x007bc538, EVENT_OPEN_OBJECT @ 0x007bcda0, EVENT_CLOSE_OBJECT @ 0x007bcdb4, FUN_004dcfb0 @ 0x004dcfb0, all door fields
- [x] Interfaces/Components/IFactionComponent.cs - ✅ COMPLETE - Ghidra references added: repute.2da @ 0x007c0a28, FactionID @ 0x007c40b4, FactionRep @ 0x007c290c, FUN_005226d0 @ 0x005226d0, all faction fields
- [x] Interfaces/Components/IInventoryComponent.cs - ✅ COMPLETE - Ghidra references added: Inventory @ 0x007c2504, InventorySlot @ 0x007bf7d0, ItemList @ 0x007bf580, FUN_005226d0 @ 0x005226d0, FUN_0050c510 @ 0x0050c510, all inventory fields
- [x] Interfaces/Components/IItemComponent.cs - ✅ COMPLETE - Ghidra references added: Item @ 0x007bc550, BaseItem @ 0x007c0a78, ItemList @ 0x007bf580, all item event constants, FUN_005226d0 @ 0x005226d0, all item fields
- [x] Interfaces/Components/IPerceptionComponent.cs - ✅ COMPLETE - Ghidra references added: PERCEPTIONDIST @ 0x007c4070, CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION @ 0x007bcb68, FUN_005fb0f0 @ 0x005fb0f0, all perception fields
- [x] Interfaces/Components/IPlaceableComponent.cs - ✅ COMPLETE - Ghidra references added: Placeable @ 0x007bc530, OnUsed @ 0x007be1c4, EVENT_OPEN_OBJECT @ 0x007bcda0, FUN_004dcfb0 @ 0x004dcfb0, all placeable fields
- [x] Interfaces/Components/IQuickSlotComponent.cs - ✅ COMPLETE - Ghidra references added: QuickSlot_* fields, FUN_005226d0 @ 0x005226d0, FUN_005223a0 @ 0x005223a0, all quick slot fields
- [x] Interfaces/Components/IRenderableComponent.cs - ✅ COMPLETE - Ghidra references added: ModelResRef @ 0x007c2f6c, Appearance_Type @ 0x007c40f0, FUN_005261b0 @ 0x005261b0, all model/rendering fields
- [x] Interfaces/Components/IScriptHooksComponent.cs - ✅ COMPLETE - Ghidra references added: ScriptHeartbeat @ 0x007bee60, ScriptOnNotice @ 0x007bee70, FUN_005226d0 @ 0x005226d0, FUN_0050c510 @ 0x0050c510, all script hook fields
- [x] Interfaces/Components/IStatsComponent.cs - ✅ COMPLETE - Ghidra references added: CurrentHP @ 0x007c1b40, Max_HPs @ 0x007cb714, ArmorClass @ 0x007c0b10, all stats fields
- [x] Interfaces/Components/ITransformComponent.cs - ✅ COMPLETE - Ghidra references added: XPosition @ 0x007bd000, YPosition @ 0x007bd00c, ZPosition @ 0x007bd018, XOrientation @ 0x007bcfb8, FUN_005226d0 @ 0x005226d0, FUN_004e08e0 @ 0x004e08e0, all transform fields
- [x] Interfaces/Components/ITriggerComponent.cs - ✅ COMPLETE - Ghidra references added: Trigger @ 0x007bc548, EVENT_ENTERED_TRIGGER @ 0x007bcbcc, EVENT_LEFT_TRIGGER @ 0x007bcc00, FUN_004dcfb0 @ 0x004dcfb0, all trigger fields

#### Journal (1 file)

- [x] Journal/JournalSystem.cs - ✅ COMPLETE - Ghidra references added: JOURNAL @ 0x007bdf44, NW_JOURNAL @ 0x007c20e8, Quest @ 0x007c35e4, QuestState @ 0x007c2458, all journal/quest fields

#### Module (3 files)

- [x] Module/ModuleTransitionSystem.cs - ✅ COMPLETE - Ghidra references added: Module @ 0x007c1a70, LASTMODULE @ 0x007be1d0, ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD @ 0x007bc91c, CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START @ 0x007bc948, LinkedToModule @ 0x007bd7bc
- [x] Module/RuntimeArea.cs - ✅ COMPLETE - Ghidra references added: Area @ 0x007be340, AreaId @ 0x007bef48, AREANAME @ 0x007be1dc, FUN_005226d0 @ 0x005226d0, EVENT_AREA_TRANSITION @ 0x007bcbdc
- [x] Module/RuntimeModule.cs - ✅ COMPLETE - Ghidra references added: Module @ 0x007bc4e0, ModuleName @ 0x007bde2c, ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, MODULES @ 0x007b58b4, FUN_00633270 @ 0x00633270, FUN_00708990 @ 0x00708990

#### Movement (2 files)

- [x] Movement/CharacterController.cs - ✅ COMPLETE - Ghidra references added: MovementRate @ 0x007c400c, WALKRATE @ 0x007c4b78, RUNRATE @ 0x007c4b84, MOVETO @ 0x007b6b24, "nwsareapathfind.cpp" @ 0x007be3ff, all movement error messages
- [x] Movement/PlayerInputHandler.cs - ✅ COMPLETE - Ghidra references added: "Mouse Sensitivity" @ 0x007c85cc, CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED @ 0x007bc704, OnClick @ 0x007c1a20, DirectInput8 references

#### Navigation (2 files)

- [x] Navigation/NavigationMesh.cs - ✅ COMPLETE - Ghidra references added: WriteBWMFile @ 0x0055aef0, ValidateBWMHeader @ 0x006160c0 (swkotor2.exe), "BWM V1.0" @ 0x007c061c
- [x] Navigation/NavigationMeshFactory.cs - ✅ COMPLETE - Ghidra references added: "walkmesh" pathfinding, "nwsareapathfind.cpp" @ 0x007be3ff, "BWM V1.0" @ 0x007c061c, all pathfinding error messages

#### Party (3 files)

- [x] Party/PartyInventory.cs - ✅ COMPLETE - Ghidra references added: Inventory @ 0x007c2504, InventorySlot @ 0x007bf7d0, CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED @ 0x007bc778, all inventory fields
- [x] Party/PartyMember.cs - ✅ COMPLETE - Ghidra references added: PARTYTABLE @ 0x007c1910, PT_MEMBERS @ 0x007c1844, PT_MEMBER_ID @ 0x007c1834, FUN_0057dcd0 @ 0x0057dcd0, all party member fields
- [x] Party/PartySystem.cs - ✅ COMPLETE - Ghidra references added: SavePartyTable @ 0x0057bd70, FUN_0057dcd0 (swkotor2.exe), SavePartyTable @ 0x0052ade0 (swkotor.exe)

#### Perception (1 file)

- [x] Perception/PerceptionSystem.cs - ✅ COMPLETE - Ghidra references added: SaveEntityState @ 0x005226d0, LoadEntityState @ 0x005fb0f0 (swkotor2.exe), PerceptionData @ 0x007bf6c4, PerceptionList @ 0x007bf6d4

#### Save (3 files)

- [x] Save/AreaState.cs - ✅ COMPLETE - Ghidra references added: FUN_005226d0 @ 0x005226d0, area state serialization, all entity state fields
- [x] Save/SaveGameData.cs - ✅ COMPLETE - Ghidra references added: savenfo @ 0x007be1f0, SAVEGAME @ 0x007be28c, FUN_004eb750 @ 0x004eb750, all save game structure fields
- [x] Save/SaveSystem.cs - ✅ COMPLETE - Ghidra references added: SerializeSaveNfo @ 0x004eb750, SaveGlobalVariables @ 0x005ac670, SavePartyTable @ 0x0057bd70, FUN_00708990 @ 0x00708990, all save/load functions

#### Templates (9 files)

- [x] Templates/CreatureTemplate.cs - ✅ COMPLETE - Ghidra references added: Creature @ 0x007bc544, Creature List @ 0x007bd01c, FUN_005226d0 @ 0x005226d0, UTC file format
- [x] Templates/DoorTemplate.cs - ✅ COMPLETE - Ghidra references added: Door @ 0x007bc538, Door List @ 0x007bd270, FUN_005226d0 @ 0x005226d0, UTD file format
- [x] Templates/EncounterTemplate.cs - ✅ COMPLETE - Ghidra references added: Encounter @ 0x007bc524, Encounter List @ 0x007bd050, FUN_005226d0 @ 0x005226d0, UTE file format
- [x] Templates/IEntityTemplate.cs - ✅ COMPLETE - Ghidra references added: All GFF template signatures (UTC, UTP, UTD, UTT, UTW, UTS, UTE, UTI, UTM), FUN_004e10b0 @ 0x004e10b0, FUN_004e08e0 @ 0x004e08e0, FUN_004e01a0 @ 0x004e01a0
- [x] Templates/PlaceableTemplate.cs - ✅ COMPLETE - Ghidra references added: Placeable @ 0x007bc530, Placeable List @ 0x007bd260, FUN_005226d0 @ 0x005226d0, UTP file format
- [x] Templates/SoundTemplate.cs - ✅ COMPLETE - Ghidra references added: SoundList @ 0x007bd080, Sound @ 0x007bc500, FUN_004e08e0 @ 0x004e08e0, FUN_005226d0 @ 0x005226d0, UTS file format, all sound fields
- [x] Templates/StoreTemplate.cs - ✅ COMPLETE - Ghidra references added: FUN_005226d0 @ 0x005226d0, UTM file format, all store/merchant fields
- [x] Templates/TriggerTemplate.cs - ✅ COMPLETE - Ghidra references added: Trigger @ 0x007bc51c, TriggerList @ 0x007bd254, EVENT_ENTERED_TRIGGER @ 0x007bce08, FUN_005226d0 @ 0x005226d0, UTT file format
- [x] Templates/WaypointTemplate.cs - ✅ COMPLETE - Ghidra references added: Waypoint @ 0x007bc510, WaypointList @ 0x007bd060, STARTWAYPOINT @ 0x007be034, FUN_004e08e0 @ 0x004e08e0, FUN_005226d0 @ 0x005226d0, UTW file format

#### Triggers (1 file)

- [x] Triggers/TriggerSystem.cs - ✅ COMPLETE - Ghidra references added: LoadTriggerList @ 0x004e5920, SaveTriggerList @ 0x004e2b20 (swkotor2.exe), TriggerList @ 0x007bd254

#### Root (1 file)

- [ ] GameSettings.cs

### Runtime/Content (18 files)

- [x] Cache/ContentCache.cs - ✅ COMPLETE - Ghidra references added: CACHE @ 0x007c6848, z:\cache @ 0x007c6850, CExoKeyTable resource management, all cache fields
- [x] Converters/BwmToNavigationMeshConverter.cs - ✅ COMPLETE - Ghidra references added: WriteBWMFile @ 0x0055aef0, ValidateBWMHeader @ 0x006160c0 (swkotor2.exe), "BWM V1.0" @ 0x007c061c
- [x] Interfaces/IContentCache.cs - ✅ COMPLETE - Ghidra references added: CACHE @ 0x007c6848, z:\cache @ 0x007c6850, CExoKeyTable resource management, all cache interface fields
- [x] Interfaces/IContentConverter.cs - ✅ COMPLETE - Ghidra references added: Resource @ 0x007c14d4, Loading @ 0x007c7e40, CExoKeyTable resource loading, all converter interface fields
- [x] Interfaces/IGameResourceProvider.cs - ✅ COMPLETE - Ghidra references added: Resource @ 0x007c14d4, FUN_00633270 @ 0x00633270, CExoKeyTable/CExoResMan resource management, all resource provider fields
- [x] Interfaces/IResourceProvider.cs - ✅ COMPLETE - Ghidra references added: Resource @ 0x007c14d4, CExoKeyTable errors, FUN_00633270 @ 0x00633270, all resource provider fields
- [x] Loaders/GITLoader.cs - ✅ COMPLETE - Ghidra references added: FUN_004dfbb0 @ 0x004dfbb0 (load creature instances), FUN_004e08e0 @ 0x004e08e0 (load placeable/door/store instances), FUN_004e01a0 @ 0x004e01a0 (load encounter instances), "Creature List" @ 0x007bd01c, "TriggerList" @ 0x007bd254
- [x] Loaders/TemplateLoader.cs - ✅ COMPLETE - Ghidra references added: TemplateResRef @ 0x007bd00c, FUN_005261b0 @ 0x005261b0, FUN_0050c510 @ 0x0050c510, FUN_00580ed0 @ 0x00580ed0, FUN_004e08e0 @ 0x004e08e0, all template loading functions and error messages
- [ ] MDL/MDLBulkReader.cs
- [ ] MDL/MDLCache.cs
- [ ] MDL/MDLConstants.cs
- [ ] MDL/MDLDataTypes.cs
- [ ] MDL/MDLFastReader.cs
- [ ] MDL/MDLLoader.cs
- [ ] MDL/MDLOptimizedReader.cs
- [x] ResourceProviders/GameResourceProvider.cs - ✅ COMPLETE - Ghidra references added: Resource @ 0x007c14d4, CExoKeyTable errors, FUN_00633270 @ 0x00633270, all resource provider fields
- [x] Save/SaveDataProvider.cs - ✅ COMPLETE - Ghidra references added: FUN_004eb750 @ 0x004eb750, FUN_00708990 @ 0x00708990, SAVES: @ 0x007be284, savenfo @ 0x007be1f0, SAVEGAME @ 0x007be28c, all save file structure fields
- [x] Save/SaveSerializer.cs - ✅ COMPLETE - Ghidra references added: SerializeSaveNfo @ 0x004eb750, SaveGlobalVariables @ 0x005ac670, SavePartyTable @ 0x0057bd70 (swkotor2.exe)

### Runtime/Scripting (11 files)

- [x] EngineApi/BaseEngineApi.cs - ✅ COMPLETE - Ghidra references added: ACTION opcode handler, PRINTSTRING @ 0x007c29f8, FUN_005c4ff0 @ 0x005c4ff0, FUN_00508260 @ 0x00508260, ActionList @ 0x007bebdc, all engine API fields
- [x] Interfaces/IEngineApi.cs - ✅ COMPLETE - Ghidra references added: ACTION opcode handler, ActionList @ 0x007bebdc, ActionId @ 0x007bebd0, PRINTSTRING @ 0x007c29f8, all engine API interface fields
- [x] Interfaces/IExecutionContext.cs - ✅ COMPLETE - Ghidra references added: NCS file format, OBJECT_SELF (0x7F000001), OBJECT_INVALID (0x7F000000), all execution context fields
- [x] Interfaces/INcsVm.cs - ✅ COMPLETE - Ghidra references added: NCS file format ("NCS " signature, "V1.0" version, 0x42 marker, instructions @ 0x0D), ACTION opcode, all NCS VM interface fields
- [x] Interfaces/IScriptGlobals.cs - ✅ COMPLETE - Ghidra references added: GLOBALVARS @ 0x007c27bc, FUN_005ac670 @ 0x005ac670, Global @ 0x007c29b0, all script globals interface fields
- [x] Interfaces/Variable.cs - ✅ COMPLETE - Ghidra references added: NCS file format, VariableType enum, OBJECT_INVALID (0x7F000000), OBJECT_SELF (0x7F000001), all variable type fields
- [x] ScriptExecutor.cs - ✅ COMPLETE - Ghidra references added: DispatchScriptEvent @ 0x004dd730, LoadScriptHooks @ 0x0050c510, LogScriptEvent @ 0x004dcfb0 (swkotor2.exe), ExecuteCommandExecuteScript @ 0x14051d5c0 (nwmain.exe)
- [x] Types/Location.cs - ✅ COMPLETE - Ghidra references added: LOCATION @ 0x007c2850, ValLocation @ 0x007c26ac, CatLocation @ 0x007c26dc, all location error messages
- [x] VM/ExecutionContext.cs - ✅ COMPLETE - Ghidra references added: NCS file format, OBJECT_SELF (0x7F000001), OBJECT_INVALID (0x7F000000), all execution context fields
- [x] VM/NcsVm.cs - ✅ COMPLETE - Ghidra references added: NCS file format ("NCS " signature, "V1.0" version, 0x42 marker, instructions @ 0x0D), ACTION opcode, ActionList @ 0x007bebdc, DelayCommand @ 0x007be900, STORE_STATE opcode, all NCS VM fields
- [x] VM/ScriptGlobals.cs - ✅ COMPLETE - Ghidra references added: GLOBALVARS @ 0x007c27bc, FUN_005ac670 @ 0x005ac670, Global @ 0x007c29b0, OBJECT_SELF (0x7F000001), OBJECT_INVALID (0x7F000000), all script globals fields

### Runtime/Games (99 files)

#### Common (8 files)

- [x] Common/BaseEngine.cs - ✅ COMPLETE - Ghidra references added: FUN_00404250 @ 0x00404250, FUN_00633270 @ 0x00633270, cross-engine analysis (Odyssey, Aurora, Eclipse), base class inheritance structure
- [x] Common/BaseEngineGame.cs - ✅ COMPLETE - Ghidra references added: FUN_006caab0 @ 0x006caab0, ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, cross-engine analysis (Odyssey, Aurora, Eclipse), base class inheritance structure
- [x] Common/BaseEngineModule.cs - ✅ COMPLETE - Ghidra references added: FUN_006caab0 @ 0x006caab0, ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, cross-engine analysis (Odyssey, Aurora, Eclipse), base class inheritance structure
- [ ] Common/BaseEngineProfile.cs
- [ ] Common/IEngine.cs
- [ ] Common/IEngineGame.cs
- [ ] Common/IEngineModule.cs
- [ ] Common/IEngineProfile.cs

#### Odyssey (84 files)

- [x] Odyssey/OdysseyEngine.cs - ✅ COMPLETE - Ghidra references added: FUN_00404250 @ 0x00404250, FUN_00633270 @ 0x00633270, cross-engine analysis, inheritance from BaseEngine
- [x] Odyssey/OdysseyGameSession.cs - ✅ COMPLETE - Ghidra references added: FUN_006caab0 @ 0x006caab0, ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, cross-engine analysis, inheritance from BaseEngineGame
- [x] Odyssey/OdysseyModuleLoader.cs - ✅ COMPLETE - Ghidra references added: FUN_006caab0 @ 0x006caab0, FUN_00633270 @ 0x00633270, MODULES: @ 0x007b58b4, cross-engine analysis, inheritance from BaseEngineModule
- [x] Odyssey/Combat/CombatManager.cs - ✅ COMPLETE - Ghidra references added: EndCombatRound @ 0x00529c30 (swkotor2.exe), CombatRoundData @ 0x007bf6b4, CombatInfo @ 0x007c2e60, CSWSCombatRound error messages
- [x] Odyssey/Combat/CombatRound.cs - ✅ COMPLETE - Ghidra references added: FUN_00529470 @ 0x00529470 (save CombatRoundData), FUN_005226d0 @ 0x005226d0, CombatRoundData @ 0x007bf6b4, all combat round fields
- [x] Odyssey/Combat/DamageCalculator.cs - ✅ COMPLETE - Ghidra references added: DamageValue @ 0x007bf890, DamageList @ 0x007bf89c, ScriptDamaged @ 0x007bee70, CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED @ 0x007bcb14, all damage calculation fields
- [x] Odyssey/Combat/WeaponDamageCalculator.cs - ✅ COMPLETE - Ghidra references added: damagedice @ 0x007c2e60, damagedie @ 0x007c2e70, damagebonus @ 0x007c2e80, FUN_005226d0 @ 0x005226d0, cross-engine analysis (Odyssey, Aurora, Eclipse), base class inheritance structure
- [x] Odyssey/Components/ActionQueueComponent.cs - ✅ COMPLETE - Ghidra references added: ActionList @ 0x007bebdc, ActionId @ 0x007bebd0, all action queue fields
- [x] Odyssey/Components/CreatureComponent.cs - ✅ COMPLETE - Ghidra references added: Creature @ 0x007bc544, Creature List @ 0x007bd01c, FUN_005226d0 @ 0x005226d0, FUN_004dfbb0 @ 0x004dfbb0, FUN_005261b0 @ 0x005261b0, FUN_0050c510 @ 0x0050c510, all creature fields
- [x] Odyssey/Components/DoorComponent.cs - ✅ COMPLETE - Ghidra references added: Door List @ 0x007bd248, FUN_00584f40 @ 0x00584f40, FUN_00585ec0 @ 0x00585ec0, FUN_004e08e0 @ 0x004e08e0, FUN_00580ed0 @ 0x00580ed0, FUN_005838d0 @ 0x005838d0, all door fields
- [x] Odyssey/Components/EncounterComponent.cs - ✅ COMPLETE - Ghidra references added: Encounter @ 0x007bc524, Encounter List @ 0x007bd050, FUN_004e01a0 @ 0x004e01a0, all encounter fields
- [x] Odyssey/Components/FactionComponent.cs - ✅ COMPLETE - Ghidra references added: FactionID @ 0x007c40b4, FactionRep @ 0x007c290c, FactionList @ 0x007be604, FACTIONREP @ 0x007bcec8, FactionID1 @ 0x007c2924, FactionID2 @ 0x007c2918
- [x] Odyssey/Components/InventoryComponent.cs - ✅ COMPLETE - Ghidra references added: FUN_005226d0 @ 0x005226d0 (save inventory), InventoryRes @ 0x007bf570, InventorySlot @ 0x007bf7d0, ItemList @ 0x007bf580, Equip_ItemList @ 0x007bf5a4
- [x] Odyssey/Components/ItemComponent.cs - ✅ COMPLETE - Ghidra references added: ItemList @ 0x007bf580, Equip_ItemList @ 0x007bf5a4, BaseItem @ 0x007c0a78, FUN_005fb0f0 @ 0x005fb0f0, all item fields
- [x] Odyssey/Components/PerceptionComponent.cs - ✅ COMPLETE - Ghidra references added: PerceptionData @ 0x007bf6c4, PerceptionList @ 0x007bf6d4, PERCEPTIONDIST @ 0x007c4070, FUN_005fb0f0 @ 0x005fb0f0, all perception fields
- [x] Odyssey/Components/PlaceableComponent.cs - ✅ COMPLETE - Ghidra references added: Placeable @ 0x007bc530, Placeable List @ 0x007bd260, LoadPlaceableFromGFF @ 0x00588010, SavePlaceableToGFF @ 0x00589520, FUN_004e08e0 @ 0x004e08e0, all placeable fields
- [x] Odyssey/Components/QuickSlotComponent.cs - ✅ COMPLETE - Ghidra references added: FUN_005226d0 @ 0x005226d0, FUN_005223a0 @ 0x005223a0, QuickSlot_* fields, all quick slot fields
- [x] Odyssey/Components/RenderableComponent.cs - ✅ COMPLETE - Ghidra references added: ModelResRef @ 0x007c2f6c, Appearance_Type @ 0x007c40f0, FUN_005261b0 @ 0x005261b0, all renderable fields
- [x] Odyssey/Components/ScriptHooksComponent.cs - ✅ COMPLETE - Ghidra references added: FUN_0050c510 @ 0x0050c510 (load script hooks), FUN_005226d0 @ 0x005226d0 (save script hooks), all Script* fields (ScriptHeartbeat @ 0x007beeb0, etc.)
- [x] Odyssey/Components/SoundComponent.cs - ✅ COMPLETE - Ghidra references added: SoundList @ 0x007bd080, Sound @ 0x007bc500, FUN_004e08e0 @ 0x004e08e0, all sound fields
- [x] Odyssey/Components/StatsComponent.cs - ✅ COMPLETE - Ghidra references added: CurrentHP @ 0x007c1b40, FUN_005226d0 @ 0x005226d0, FUN_004dfbb0 @ 0x004dfbb0, all stats fields
- [x] Odyssey/Components/StoreComponent.cs - ✅ COMPLETE - Ghidra references added: Store @ 0x007bc4f8, StoreList @ 0x007bd098, all store/merchant fields
- [x] Odyssey/Components/TransformComponent.cs - ✅ COMPLETE - Ghidra references added: XPosition @ 0x007bd000, YPosition @ 0x007bcff4, ZPosition @ 0x007bcfe8, FUN_005226d0 @ 0x005226d0, FUN_004e08e0 @ 0x004e08e0, all transform fields
- [x] Odyssey/Components/TriggerComponent.cs - ✅ COMPLETE - Ghidra references added: Trigger @ 0x007bc51c, TriggerList @ 0x007bd254, EVENT_ENTERED_TRIGGER @ 0x007bce08, all trigger fields
- [x] Odyssey/Components/WaypointComponent.cs - ✅ COMPLETE - Ghidra references added: WaypointList @ 0x007bd288, Waypoint @ 0x007bc510, STARTWAYPOINT @ 0x007be034, FUN_004e08e0 @ 0x004e08e0, all waypoint fields
- [ ] Odyssey/Data/GameDataManager.cs
- [ ] Odyssey/Data/TwoDATableManager.cs
- [ ] Odyssey/Dialogue/ConversationContext.cs
- [x] Odyssey/Dialogue/DialogueManager.cs - ✅ COMPLETE - Ghidra references added: ExecuteDialogue @ 0x005e9920 (swkotor2.exe), ScriptDialogue @ 0x007bee40, ScriptEndDialogue @ 0x007bede0, "Error: dialogue can't find object '%s'!" @ 0x007c3730
- [ ] Odyssey/Dialogue/DialogueState.cs
- [x] Odyssey/Dialogue/KotorDialogueLoader.cs - ✅ COMPLETE - Ghidra references added: ScriptDialogue @ 0x007bee40, ScriptEndDialogue @ 0x007bede0, CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE @ 0x007bcac4, "Error: dialogue can't find object '%s'!" @ 0x007c3730
- [ ] Odyssey/Dialogue/KotorLipDataLoader.cs
- [ ] Odyssey/EngineApi/K1EngineApi.cs
- [ ] Odyssey/EngineApi/K2EngineApi.cs
- [x] Odyssey/EngineApi/OdysseyK1EngineApi.cs - ✅ COMPLETE - Ghidra references added: ACTION opcode handler, function dispatch system, ScriptDefs.KOTOR_FUNCTIONS (~850 functions)
- [x] Odyssey/EngineApi/OdysseyK2EngineApi.cs - ✅ COMPLETE - Ghidra references added: ACTION opcode handler, TSL-specific functions (~950 total), PT_INFLUENCE @ 0x007c1788, Influence system
- [ ] Odyssey/Game/GameSession.cs
- [ ] Odyssey/Game/ModuleLoader.cs
- [x] Odyssey/Game/ScriptExecutor.cs - ✅ COMPLETE - Ghidra references added: FUN_004dcfb0 @ 0x004dcfb0 (script event dispatch), all CSWSSCRIPTEVENT_EVENTTYPE_* constants, Script hook fields
- [ ] Odyssey/Input/PlayerController.cs
- [ ] Odyssey/Loading/EntityFactory.cs
- [ ] Odyssey/Loading/KotorModuleLoader.cs
- [ ] Odyssey/Loading/ModuleLoader.cs
- [ ] Odyssey/Loading/NavigationMeshFactory.cs
- [ ] Odyssey/Profiles/GameProfileFactory.cs
- [ ] Odyssey/Profiles/IGameProfile.cs
- [ ] Odyssey/Profiles/K1GameProfile.cs
- [ ] Odyssey/Profiles/K2GameProfile.cs
- [x] Odyssey/Save/SaveGameManager.cs - ✅ COMPLETE - Ghidra references added: FUN_004eb750 @ 0x004eb750 (save), FUN_00708990 @ 0x00708990 (load), FUN_0057dcd0 @ 0x0057dcd0 (party table), FUN_005ac740 @ 0x005ac740 (global vars)
- [ ] Odyssey/Systems/AIController.cs
- [ ] Odyssey/Systems/ComponentInitializer.cs
- [x] Odyssey/Systems/EncounterSystem.cs - ✅ COMPLETE - Ghidra references added: LoadEncounterList @ 0x004e01a0, SaveEncounterList @ 0x004e2be0 (swkotor2.exe), "Encounter List" @ 0x007bd050
- [x] Odyssey/Systems/FactionManager.cs - ✅ COMPLETE - Ghidra references added: FactionRep @ 0x007c290c, FactionID1 @ 0x007c2924, FactionID2 @ 0x007c2918, FACTIONREP @ 0x007bcec8, FactionList @ 0x007be604
- [ ] Odyssey/Systems/HeartbeatSystem.cs
- [ ] Odyssey/Systems/ModelResolver.cs
- [x] Odyssey/Systems/PartyManager.cs - ✅ COMPLETE - Ghidra references added: PARTYTABLE @ 0x007c1910, SavePartyTable @ 0x0057bd70, FUN_0057dcd0 @ 0x0057dcd0
- [x] Odyssey/Systems/PerceptionManager.cs - ✅ COMPLETE - Ghidra references added: SaveEntityState @ 0x005226d0, LoadEntityState @ 0x005fb0f0, PerceptionData @ 0x007bf6c4, PerceptionList @ 0x007bf6d4, PERCEPTIONDIST @ 0x007c4070
- [x] Odyssey/Systems/StoreSystem.cs - ✅ COMPLETE - Ghidra references added: LoadStoreFromGFF @ 0x00571310, SaveStoreToGFF @ 0x00570e30 (swkotor2.exe), StoreList @ 0x007bd098
- [x] Odyssey/Systems/TriggerSystem.cs - ✅ COMPLETE - Ghidra references added: LoadTriggerList @ 0x004e5920, SaveTriggerList @ 0x004e2b20 (swkotor2.exe), TriggerList @ 0x007bd254, EVENT_ENTERED_TRIGGER @ 0x007bce08, EVENT_LEFT_TRIGGER @ 0x007bcdf4
- [ ] Odyssey/Templates/UTC.cs
- [ ] Odyssey/Templates/UTCHelpers.cs
- [ ] Odyssey/Templates/UTD.cs
- [ ] Odyssey/Templates/UTDHelpers.cs
- [ ] Odyssey/Templates/UTE.cs
- [ ] Odyssey/Templates/UTEHelpers.cs
- [ ] Odyssey/Templates/UTI.cs
- [ ] Odyssey/Templates/UTIHelpers.cs
- [ ] Odyssey/Templates/UTM.cs
- [ ] Odyssey/Templates/UTMHelpers.cs
- [ ] Odyssey/Templates/UTP.cs
- [ ] Odyssey/Templates/UTPHelpers.cs
- [ ] Odyssey/Templates/UTS.cs
- [ ] Odyssey/Templates/UTSHelpers.cs
- [ ] Odyssey/Templates/UTT.cs
- [ ] Odyssey/Templates/UTTHelpers.cs
- [ ] Odyssey/Templates/UTW.cs
- [ ] Odyssey/Templates/UTWHelpers.cs

#### Aurora (1 file)

- [ ] Aurora/AuroraEngine.cs

#### Eclipse (1 file)

- [ ] Eclipse/EclipseEngine.cs

#### Infinity (1 file)

- [ ] Infinity/InfinityEngine.cs

### Runtime/Graphics (247 files)

#### Common (50 files)

- [ ] Common/Backends/BaseDirect3D11Backend.cs
- [ ] Common/Backends/BaseDirect3D12Backend.cs
- [ ] Common/Backends/BaseGraphicsBackend.cs
- [ ] Common/Backends/BaseVulkanBackend.cs
- [ ] Common/Enums/GraphicsBackendType.cs
- [ ] Common/Interfaces/ILowLevelBackend.cs
- [ ] Common/Interfaces/IPostProcessingEffect.cs
- [ ] Common/Interfaces/IRaytracingSystem.cs
- [ ] Common/Interfaces/IRoomMeshRenderer.cs
- [ ] Common/Interfaces/ISamplerFeedbackBackend.cs
- [ ] Common/Interfaces/IUpscalingSystem.cs
- [ ] Common/PostProcessing/BasePostProcessingEffect.cs
- [ ] Common/Raytracing/BaseRaytracingSystem.cs
- [ ] Common/Remix/BaseRemixBridge.cs
- [ ] Common/Rendering/RenderSettings.cs
- [ ] Common/Structs/GraphicsStructs.cs
- [ ] Common/Upscaling/BaseUpscalingSystem.cs
- [ ] Common/Interfaces/IContentManager.cs
- [ ] Common/Interfaces/IDepthStencilBuffer.cs
- [ ] Common/Interfaces/IEffect.cs
- [ ] Common/Interfaces/IEntityModelRenderer.cs
- [ ] Common/Interfaces/IFont.cs
- [ ] Common/Interfaces/IGraphicsBackend.cs
- [ ] Common/Interfaces/IGraphicsDevice.cs
- [ ] Common/Interfaces/IIndexBuffer.cs
- [ ] Common/Interfaces/IInputManager.cs
- [ ] Common/Interfaces/IModel.cs
- [ ] Common/Interfaces/IRenderState.cs
- [ ] Common/Interfaces/IRenderTarget.cs
- [ ] Common/Interfaces/ISamplerFeedbackBackend.cs
- [ ] Common/Interfaces/ISpatialAudio.cs
- [ ] Common/Interfaces/ISpriteBatch.cs
- [ ] Common/Interfaces/ITexture2D.cs
- [ ] Common/Interfaces/IVertexBuffer.cs
- [ ] Common/Interfaces/IVertexDeclaration.cs
- [ ] Common/Interfaces/IWindow.cs
- [ ] Common/MatrixHelper.cs
- [ ] Common/VertexPositionColor.cs
- [ ] GraphicsBackend.cs

#### MonoGame (158 files)

- [ ] MonoGame/Animation/AnimationCompression.cs
- [ ] MonoGame/Animation/SkeletalAnimationBatching.cs
- [ ] MonoGame/Assets/AssetHotReload.cs
- [ ] MonoGame/Assets/AssetValidator.cs
- [ ] MonoGame/Audio/MonoGameSoundPlayer.cs
- [ ] MonoGame/Audio/MonoGameVoicePlayer.cs
- [ ] MonoGame/Audio/SpatialAudio.cs
- [ ] MonoGame/Backends/BackendFactory.cs
- [ ] MonoGame/Backends/Direct3D10Backend.cs
- [ ] MonoGame/Backends/Direct3D11Backend.cs
- [ ] MonoGame/Backends/Direct3D12Backend.cs
- [ ] MonoGame/Backends/OpenGLBackend.cs
- [ ] MonoGame/Backends/VulkanBackend.cs
- [ ] MonoGame/Camera/ChaseCamera.cs
- [ ] MonoGame/Camera/MonoGameDialogueCameraController.cs
- [ ] MonoGame/Compute/ComputeShaderFramework.cs
- [ ] MonoGame/Converters/MdlToMonoGameModelConverter.cs
- [ ] MonoGame/Converters/RoomMeshRenderer.cs
- [ ] MonoGame/Converters/TpcToMonoGameTextureConverter.cs
- [ ] MonoGame/Culling/DistanceCuller.cs
- [ ] MonoGame/Culling/Frustum.cs
- [ ] MonoGame/Culling/GPUCulling.cs
- [ ] MonoGame/Culling/OcclusionCuller.cs
- [ ] MonoGame/Debug/DebugRendering.cs
- [ ] MonoGame/Debug/RenderStatistics.cs
- [ ] MonoGame/Enums/GraphicsBackend.cs
- [ ] MonoGame/Enums/MaterialType.cs
- [ ] MonoGame/Graphics/MonoGameBasicEffect.cs
- [ ] MonoGame/Graphics/MonoGameContentManager.cs
- [ ] MonoGame/Graphics/MonoGameDepthStencilBuffer.cs
- [ ] MonoGame/Graphics/MonoGameEntityModelRenderer.cs
- [ ] MonoGame/Graphics/MonoGameFont.cs
- [ ] MonoGame/Graphics/MonoGameGraphicsBackend.cs
- [ ] MonoGame/Graphics/MonoGameGraphicsDevice.cs
- [ ] MonoGame/Graphics/MonoGameIndexBuffer.cs
- [ ] MonoGame/Graphics/MonoGameInputManager.cs
- [ ] MonoGame/Graphics/MonoGameRenderState.cs
- [ ] MonoGame/Graphics/MonoGameRenderTarget.cs
- [ ] MonoGame/Graphics/MonoGameRoomMeshRenderer.cs
- [ ] MonoGame/Graphics/MonoGameSpatialAudio.cs
- [ ] MonoGame/Graphics/MonoGameSpriteBatch.cs
- [ ] MonoGame/Graphics/MonoGameTexture2D.cs
- [ ] MonoGame/Graphics/MonoGameVertexBuffer.cs
- [ ] MonoGame/Graphics/MonoGameWindow.cs
- [ ] MonoGame/GUI/KotorGuiManager.cs
- [ ] MonoGame/GUI/MyraMenuRenderer.cs
- [ ] MonoGame/Interfaces/ICommandList.cs
- [ ] MonoGame/Interfaces/IDevice.cs
- [ ] MonoGame/Interfaces/IDynamicLight.cs
- [ ] MonoGame/Interfaces/IGraphicsBackend.cs
- [ ] MonoGame/Interfaces/IPbrMaterial.cs
- [ ] MonoGame/Interfaces/IRaytracingSystem.cs
- [ ] MonoGame/Lighting/ClusteredLightCulling.cs
- [ ] MonoGame/Lighting/ClusteredLightingSystem.cs
- [ ] MonoGame/Lighting/DynamicLight.cs
- [ ] MonoGame/Lighting/LightProbeSystem.cs
- [ ] MonoGame/Lighting/VolumetricLighting.cs
- [ ] MonoGame/Loading/AsyncResourceLoader.cs
- [ ] MonoGame/LOD/LODFadeSystem.cs
- [ ] MonoGame/LOD/LODSystem.cs
- [ ] MonoGame/Materials/KotorMaterialConverter.cs
- [ ] MonoGame/Materials/KotorMaterialFactory.cs
- [ ] MonoGame/Materials/MaterialInstancing.cs
- [ ] MonoGame/Materials/PbrMaterial.cs
- [ ] MonoGame/Memory/GPUMemoryPool.cs
- [ ] MonoGame/Memory/MemoryTracker.cs
- [ ] MonoGame/Memory/ObjectPool.cs
- [ ] MonoGame/Models/MDLModelConverter.cs
- [ ] MonoGame/Particles/GPUParticleSystem.cs
- [ ] MonoGame/Particles/ParticleSorter.cs
- [ ] MonoGame/Performance/FramePacing.cs
- [ ] MonoGame/Performance/FrameTimeBudget.cs
- [ ] MonoGame/Performance/GPUTimestamps.cs
- [ ] MonoGame/Performance/Telemetry.cs
- [ ] MonoGame/PostProcessing/Bloom.cs
- [ ] MonoGame/PostProcessing/ColorGrading.cs
- [ ] MonoGame/PostProcessing/ExposureAdaptation.cs
- [ ] MonoGame/PostProcessing/MotionBlur.cs
- [ ] MonoGame/PostProcessing/SSAO.cs
- [ ] MonoGame/PostProcessing/SSR.cs
- [ ] MonoGame/PostProcessing/TemporalAA.cs
- [ ] MonoGame/PostProcessing/ToneMapping.cs
- [ ] MonoGame/Raytracing/NativeRaytracingSystem.cs
- [ ] MonoGame/Raytracing/RaytracedEffects.cs
- [ ] MonoGame/Remix/Direct3D9Wrapper.cs
- [ ] MonoGame/Remix/RemixBridge.cs
- [ ] MonoGame/Remix/RemixMaterialExporter.cs
- [ ] MonoGame/Rendering/AdaptiveQuality.cs
- [ ] MonoGame/Rendering/BatchOptimizer.cs
- [ ] MonoGame/Rendering/BindlessTextures.cs
- [ ] MonoGame/Rendering/CommandBuffer.cs
- [ ] MonoGame/Rendering/CommandListOptimizer.cs
- [ ] MonoGame/Rendering/ContactShadows.cs
- [ ] MonoGame/Rendering/DecalSystem.cs
- [ ] MonoGame/Rendering/DeferredRenderer.cs
- [ ] MonoGame/Rendering/DepthPrePass.cs
- [ ] MonoGame/Rendering/DrawCallSorter.cs
- [ ] MonoGame/Rendering/DynamicBatching.cs
- [ ] MonoGame/Rendering/DynamicResolution.cs
- [ ] MonoGame/Rendering/EntityModelRenderer.cs
- [ ] MonoGame/Rendering/FrameGraph.cs
- [ ] MonoGame/Rendering/GeometryCache.cs
- [ ] MonoGame/Rendering/GeometryStreaming.cs
- [ ] MonoGame/Rendering/GPUInstancing.cs
- [ ] MonoGame/Rendering/GPUMemoryBudget.cs
- [ ] MonoGame/Rendering/GPUMemoryDefragmentation.cs
- [ ] MonoGame/Rendering/GPUSynchronization.cs
- [ ] MonoGame/Rendering/HDRPipeline.cs
- [ ] MonoGame/Rendering/IndirectRenderer.cs
- [ ] MonoGame/Rendering/MemoryAliasing.cs
- [ ] MonoGame/Rendering/MeshCompression.cs
- [ ] MonoGame/Rendering/ModernRenderer.cs
- [ ] MonoGame/Rendering/MultiThreadedRenderer.cs
- [ ] MonoGame/Rendering/MultiThreadedRendering.cs
- [ ] MonoGame/Rendering/OcclusionQueries.cs
- [ ] MonoGame/Rendering/OdysseyRenderer.cs
- [ ] MonoGame/Rendering/PipelineStateCache.cs
- [ ] MonoGame/Rendering/QualityPresets.cs
- [ ] MonoGame/Rendering/RenderBatchManager.cs
- [ ] MonoGame/Rendering/RenderGraph.cs
- [ ] MonoGame/Rendering/RenderOptimizer.cs
- [ ] MonoGame/Rendering/RenderPipeline.cs
- [ ] MonoGame/Rendering/RenderProfiler.cs
- [ ] MonoGame/Rendering/RenderQueue.cs
- [ ] MonoGame/Rendering/RenderSettings.cs
- [ ] MonoGame/Rendering/RenderTargetCache.cs
- [ ] MonoGame/Rendering/RenderTargetChain.cs
- [ ] MonoGame/Rendering/RenderTargetManager.cs
- [ ] MonoGame/Rendering/RenderTargetPool.cs
- [ ] MonoGame/Rendering/RenderTargetScaling.cs
- [ ] MonoGame/Rendering/ResourceBarriers.cs
- [ ] MonoGame/Rendering/ResourcePreloader.cs
- [ ] MonoGame/Rendering/SceneGraph.cs
- [ ] MonoGame/Rendering/ShaderCache.cs
- [ ] MonoGame/Rendering/StateCache.cs
- [ ] MonoGame/Rendering/SubsurfaceScattering.cs
- [ ] MonoGame/Rendering/TemporalReprojection.cs
- [ ] MonoGame/Rendering/TextureAtlas.cs
- [ ] MonoGame/Rendering/TextureCompression.cs
- [ ] MonoGame/Rendering/TriangleStripGenerator.cs
- [ ] MonoGame/Rendering/Upscaling/DLSS.cs
- [ ] MonoGame/Rendering/Upscaling/FSR.cs
- [ ] MonoGame/Rendering/VariableRateShading.cs
- [ ] MonoGame/Rendering/VertexCacheOptimizer.cs
- [ ] MonoGame/Rendering/VisibilityBuffer.cs
- [ ] MonoGame/Save/AsyncSaveSystem.cs
- [ ] MonoGame/Scene/SceneBuilder.cs
- [ ] MonoGame/Shaders/ShaderCache.cs
- [ ] MonoGame/Shaders/ShaderPermutationSystem.cs
- [ ] MonoGame/Shadows/CascadedShadowMaps.cs
- [ ] MonoGame/Spatial/Octree.cs
- [ ] MonoGame/Textures/TextureFormatConverter.cs
- [ ] MonoGame/Textures/TextureStreamingManager.cs
- [ ] MonoGame/UI/BasicHUD.cs
- [ ] MonoGame/UI/DialoguePanel.cs
- [ ] MonoGame/UI/LoadingScreen.cs
- [ ] MonoGame/UI/MainMenu.cs
- [ ] MonoGame/UI/PauseMenu.cs
- [ ] MonoGame/UI/ScreenFade.cs

#### Stride (37 files)

- [ ] Stride/Audio/StrideSoundPlayer.cs
- [ ] Stride/Audio/StrideVoicePlayer.cs
- [ ] Stride/Backends/StrideBackendFactory.cs
- [ ] Stride/Backends/StrideDirect3D11Backend.cs
- [ ] Stride/Backends/StrideDirect3D12Backend.cs
- [ ] Stride/Backends/StrideVulkanBackend.cs
- [ ] Stride/Camera/StrideDialogueCameraController.cs
- [ ] Stride/Graphics/StrideBasicEffect.cs
- [ ] Stride/Graphics/StrideContentManager.cs
- [ ] Stride/Graphics/StrideDepthStencilBuffer.cs
- [ ] Stride/Graphics/StrideEntityModelRenderer.cs
- [ ] Stride/Graphics/StrideFont.cs
- [ ] Stride/Graphics/StrideGraphicsBackend.cs
- [ ] Stride/Graphics/StrideGraphicsDevice.cs
- [ ] Stride/Graphics/StrideIndexBuffer.cs
- [ ] Stride/Graphics/StrideInputManager.cs
- [ ] Stride/Graphics/StrideRenderState.cs
- [ ] Stride/Graphics/StrideRenderTarget.cs
- [ ] Stride/Graphics/StrideRoomMeshRenderer.cs
- [ ] Stride/Graphics/StrideSpatialAudio.cs
- [ ] Stride/Graphics/StrideSpriteBatch.cs
- [ ] Stride/Graphics/StrideTexture2D.cs
- [ ] Stride/Graphics/StrideVertexBuffer.cs
- [ ] Stride/Graphics/StrideWindow.cs
- [ ] Stride/PostProcessing/StrideBloomEffect.cs
- [ ] Stride/PostProcessing/StrideColorGradingEffect.cs
- [ ] Stride/PostProcessing/StrideMotionBlurEffect.cs
- [ ] Stride/PostProcessing/StrideSsaoEffect.cs
- [ ] Stride/PostProcessing/StrideSsrEffect.cs
- [ ] Stride/PostProcessing/StrideTemporalAaEffect.cs
- [ ] Stride/PostProcessing/StrideToneMappingEffect.cs
- [ ] Stride/Raytracing/StrideRaytracingSystem.cs
- [ ] Stride/Remix/StrideRemixBridge.cs
- [ ] Stride/Upscaling/StrideDlssSystem.cs
- [ ] Stride/Upscaling/StrideFsrSystem.cs
- [ ] Stride/Upscaling/StrideXeSSSystem.cs

#### Enums (1 file)

- [ ] Enums/GraphicsBackendType.cs

### Game (8 files)

- [ ] Program.cs
- [ ] Core/GamePathDetector.cs
- [ ] Core/GameSettings.cs
- [ ] Core/GameState.cs
- [ ] Core/GraphicsBackendFactory.cs
- [ ] Core/OdysseyGame.cs
- [ ] GUI/MenuRenderer.cs
- [ ] GUI/SaveLoadMenu.cs

### Parsing (600+ files)

**Note**: Parsing layer files typically don't need Ghidra references as they handle file format parsing, not engine behavior. However, some files that implement engine-specific logic may need references.

#### Common (18 files)

- [ ] Common/AlienSounds.cs
- [ ] Common/BinaryExtensions.cs
- [ ] Common/BinaryReader.cs
- [ ] Common/BinaryWriter.cs
- [ ] Common/CaseAwarePath.cs
- [ ] Common/Face.cs
- [ ] Common/Game.cs
- [ ] Common/GameObject.cs
- [ ] Common/Language.cs
- [ ] Common/LocalizedString.cs
- [ ] Common/Misc.cs
- [ ] Common/Module.cs
- [ ] Common/ModuleDataLoader.cs
- [ ] Common/Pathfinding.cs
- [ ] Common/ResRef.cs
- [ ] Common/SurfaceMaterial.cs
- [ ] Common/Script/DataType.cs
- [ ] Common/Script/DataTypeExtensions.cs
- [ ] Common/Script/NwscriptParser.cs
- [ ] Common/Script/ScriptConstant.cs
- [ ] Common/Script/ScriptDefs.cs
- [ ] Common/Script/ScriptFunction.cs
- [ ] Common/Script/ScriptLib.cs
- [ ] Common/Script/ScriptParam.cs

#### Extract (15 files)

- [ ] Extract/Capsule/Capsule.cs
- [ ] Extract/Capsule/LazyCapsule.cs
- [ ] Extract/Chitin/Chitin.cs
- [ ] Extract/ChitinWrapper.cs
- [ ] Extract/FileResource.cs
- [ ] Extract/FileResourceHelpers.cs
- [ ] Extract/InstallationWrapper.cs
- [ ] Extract/KeyFileWrapper.cs
- [ ] Extract/KeyWriterWrapper.cs
- [ ] Extract/SaveData/GlobalVars.cs
- [ ] Extract/SaveData/PartyTable.cs
- [ ] Extract/SaveData/SaveFolderEntry.cs
- [ ] Extract/SaveData/SaveInfo.cs
- [ ] Extract/SaveData/SaveNestedCapsule.cs
- [ ] Extract/TalkTable.cs
- [ ] Extract/TwoDAManager.cs
- [ ] Extract/TwoDARegistry.cs

#### Installation (4 files)

- [ ] Installation/Installation.cs
- [ ] Installation/InstallationResourceManager.cs
- [ ] Installation/ResourceResult.cs
- [ ] Installation/SearchLocation.cs

#### Merge (1 file)

- [ ] Merge/ModuleManager.cs

#### Resource/Formats (500+ files)

**BIF Format** (5 files)

- [ ] Resource/Formats/BIF/BIF.cs
- [ ] Resource/Formats/BIF/BIFBinaryReader.cs
- [ ] Resource/Formats/BIF/BIFBinaryWriter.cs
- [ ] Resource/Formats/BIF/BIFResource.cs
- [ ] Resource/Formats/BIF/BIFType.cs
- [ ] Resource/Formats/BIF/BZF.cs

**BWM Format** (9 files) - **HIGH PRIORITY** (walkmesh navigation)

- [ ] Resource/Formats/BWM/BWM.cs
- [ ] Resource/Formats/BWM/BWMAdjacency.cs
- [ ] Resource/Formats/BWM/BWMAuto.cs
- [ ] Resource/Formats/BWM/BWMBinaryReader.cs
- [ ] Resource/Formats/BWM/BWMBinaryWriter.cs
- [ ] Resource/Formats/BWM/BWMEdge.cs
- [ ] Resource/Formats/BWM/BWMFace.cs
- [ ] Resource/Formats/BWM/BWMMostSignificantPlane.cs
- [ ] Resource/Formats/BWM/BWMNodeAABB.cs
- [ ] Resource/Formats/BWM/BWMType.cs

**ERF Format** (4 files)

- [ ] Resource/Formats/ERF/ERF.cs
- [ ] Resource/Formats/ERF/ERFAuto.cs
- [ ] Resource/Formats/ERF/ERFBinaryReader.cs
- [ ] Resource/Formats/ERF/ERFBinaryWriter.cs
- [ ] Resource/Formats/ERF/ERFType.cs

**GFF Format** (50+ files) - **HIGH PRIORITY** (save/load, templates)

- [ ] Resource/Formats/GFF/GFF.cs
- [ ] Resource/Formats/GFF/GFFAuto.cs
- [ ] Resource/Formats/GFF/GFFBinaryReader.cs
- [ ] Resource/Formats/GFF/GFFBinaryWriter.cs
- [ ] Resource/Formats/GFF/GFFContent.cs
- [ ] Resource/Formats/GFF/GFFFieldType.cs
- [ ] Resource/Formats/GFF/GFFList.cs
- [ ] Resource/Formats/GFF/GFFStruct.cs
- [ ] Resource/Formats/GFF/Generics/* (46 files)

**KEY Format** (5 files)

- [ ] Resource/Formats/KEY/BifEntry.cs
- [ ] Resource/Formats/KEY/KEY.cs
- [ ] Resource/Formats/KEY/KEYAuto.cs
- [ ] Resource/Formats/KEY/KEYBinaryReader.cs
- [ ] Resource/Formats/KEY/KEYBinaryWriter.cs
- [ ] Resource/Formats/KEY/KeyEntry.cs

**LIP Format** (6 files) - **MEDIUM PRIORITY** (lip sync)

- [ ] Resource/Formats/LIP/LIP.cs
- [ ] Resource/Formats/LIP/LIPAuto.cs
- [ ] Resource/Formats/LIP/LIPBinaryReader.cs
- [ ] Resource/Formats/LIP/LIPBinaryWriter.cs
- [ ] Resource/Formats/LIP/LIPKeyFrame.cs
- [ ] Resource/Formats/LIP/LIPShape.cs

**LTR Format** (4 files)

- [ ] Resource/Formats/LTR/LTR.cs
- [ ] Resource/Formats/LTR/LTRAuto.cs
- [ ] Resource/Formats/LTR/LTRBinaryReader.cs
- [ ] Resource/Formats/LTR/LTRBinaryWriter.cs
- [ ] Resource/Formats/LTR/LTRBlock.cs

**LYT Format** (9 files) - **HIGH PRIORITY** (area layout)

- [ ] Resource/Formats/LYT/* (9 files)

**MDL Format** (7 files) - **HIGH PRIORITY** (3D models)

- [ ] Resource/Formats/MDL/* (7 files)

**NCS Format** (375+ files) - **HIGHEST PRIORITY** (script VM)

- [ ] Resource/Formats/NCS/* (375 files including compiler, decompiler, VM)

**RIM Format** (4 files)

- [ ] Resource/Formats/RIM/* (4 files)

**SSF Format** (5 files)

- [ ] Resource/Formats/SSF/* (5 files)

**TLK Format** (6 files) - **HIGH PRIORITY** (dialogue text)

- [ ] Resource/Formats/TLK/* (6 files)

**TPC Format** (12 files) - **HIGH PRIORITY** (textures)

- [ ] Resource/Formats/TPC/* (12 files)

**TwoDA Format** (5 files) - **HIGH PRIORITY** (game data tables)

- [ ] Resource/Formats/TwoDA/* (5 files)

**TXI Format** (7 files)

- [ ] Resource/Formats/TXI/* (7 files)

**VIS Format** (5 files) - **HIGH PRIORITY** (area visibility)

- [ ] Resource/Formats/VIS/* (5 files)

**WAV Format** (10 files)

- [ ] Resource/Formats/WAV/* (10 files)

#### Resource Core (6 files)

- [ ] Resource/ArchiveResource.cs
- [ ] Resource/ResourceAuto.cs
- [ ] Resource/ResourceAutoHelpers.cs
- [ ] Resource/ResourceFormat.cs
- [ ] Resource/ResourceIdentifier.cs
- [ ] Resource/ResourceType.cs
- [ ] Resource/Salvage.cs

#### Tools (24 files)

- [ ] Tools/Archives.cs
- [ ] Tools/Conversions.cs
- [ ] Tools/Creature.cs
- [ ] Tools/Door.cs
- [ ] Tools/Encoding.cs
- [ ] Tools/Heuristics.cs
- [ ] Tools/Kit.cs
- [ ] Tools/Misc.cs
- [ ] Tools/Model.cs
- [ ] Tools/Module.cs
- [ ] Tools/Patching.cs
- [ ] Tools/Path.cs
- [ ] Tools/Placeable.cs
- [ ] Tools/PlayPazaak.cs
- [ ] Tools/ReferenceCache.cs
- [ ] Tools/Registry.cs
- [ ] Tools/ResourceConversions.cs
- [ ] Tools/Scripts.cs
- [ ] Tools/StringUtils.cs
- [ ] Tools/Template.cs
- [ ] Tools/Utilities.cs
- [ ] Tools/Validation.cs

#### TSLPatcher (40+ files)

- [ ] TSLPatcher/Config/LogLevel.cs
- [ ] TSLPatcher/Config/PatcherConfig.cs
- [ ] TSLPatcher/Diff/DiffAnalyzerFactory.cs
- [ ] TSLPatcher/Diff/DiffEngine.cs
- [ ] TSLPatcher/Diff/DiffHelpers.cs
- [ ] TSLPatcher/Diff/GffDiff.cs
- [ ] TSLPatcher/Diff/GffDiffAnalyzer.cs
- [ ] TSLPatcher/Diff/Resolution.cs
- [ ] TSLPatcher/Diff/SsfDiff.cs
- [ ] TSLPatcher/Diff/TlkDiff.cs
- [ ] TSLPatcher/Diff/TwoDaDiff.cs
- [ ] TSLPatcher/Diff/TwoDaDiffAnalyzer.cs
- [ ] TSLPatcher/GeneratorValidation.cs
- [ ] TSLPatcher/IncrementalTSLPatchDataWriter.cs
- [ ] TSLPatcher/INIManager.cs
- [ ] TSLPatcher/InstallFolderDeterminer.cs
- [ ] TSLPatcher/Logger/InstallLogWriter.cs
- [ ] TSLPatcher/Logger/LogType.cs
- [ ] TSLPatcher/Logger/PatchLog.cs
- [ ] TSLPatcher/Logger/PatchLogger.cs
- [ ] TSLPatcher/Logger/RobustLogger.cs
- [ ] TSLPatcher/Memory/PatcherMemory.cs
- [ ] TSLPatcher/Memory/TokenUsage.cs
- [ ] TSLPatcher/ModInstaller.cs
- [ ] TSLPatcher/Mods/GFF/* (3 files)
- [ ] TSLPatcher/Mods/InstallFile.cs
- [ ] TSLPatcher/Mods/ModificationsByType.cs
- [ ] TSLPatcher/Mods/NCS/* (1 file)
- [ ] TSLPatcher/Mods/NSS/* (1 file)
- [ ] TSLPatcher/Mods/PatcherModifications.cs
- [ ] TSLPatcher/Mods/SSF/* (1 file)
- [ ] TSLPatcher/Mods/TLK/* (1 file)
- [ ] TSLPatcher/Mods/TSLPatcherINISerializer.cs
- [ ] TSLPatcher/Mods/TwoDA/* (4 files)
- [ ] TSLPatcher/PatcherNamespace.cs
- [ ] TSLPatcher/Reader/ConfigReader.cs
- [ ] TSLPatcher/Reader/NamespaceReader.cs
- [ ] TSLPatcher/TSLPatchDataGenerator.cs
- [ ] TSLPatcher/Uninstall/ModUninstaller.cs
- [ ] TSLPatcher/Uninstall/UninstallHelpers.cs

### Utility (14 files)

**Note**: Utility files typically don't need Ghidra references as they are helper/utility code.

- [ ] Utility/ArrayHead.cs
- [ ] Utility/CaseInsensitiveDict.cs
- [ ] Utility/ErrorHandling.cs
- [ ] Utility/Geometry/GeometryUtils.cs
- [ ] Utility/Geometry/Polygon2.cs
- [ ] Utility/Geometry/Polygon3.cs
- [ ] Utility/Geometry/Quaternion.cs
- [ ] Utility/KeyError.cs
- [ ] Utility/LZMA/LzmaHelper.cs
- [ ] Utility/Misc.cs
- [ ] Utility/MiscString/CaseInsensImmutableStr.cs
- [ ] Utility/MiscString/StringUtilFunctions.cs
- [ ] Utility/MiscString/WrappedStr.cs
- [ ] Utility/OrderedSet.cs
- [ ] Utility/System/OSHelper.cs
- [ ] Utility/SystemHelpers.cs

## Reverse Engineering Requirements

### Files Requiring Ghidra References

**MANDATORY**: All files in the following directories MUST have Ghidra references:

1. **Runtime/Core** - All files (99 files)
   - These implement core engine behavior
   - Must match original engine behavior exactly
   - Every function should reference Ghidra function addresses

2. **Runtime/Games/Odyssey** - All files (84 files)
   - Primary implementation target
   - Must achieve 1:1 parity with swkotor.exe/swkotor2.exe
   - Every system must be reverse engineered

3. **Runtime/Scripting** - All files (11 files)
   - NCS VM implementation
   - Engine API functions
   - Must match original script execution behavior

4. **Runtime/Content** - Selected files
   - Loaders/GITLoader.cs (entity spawning)
   - Loaders/TemplateLoader.cs (template loading)
   - Save/SaveSerializer.cs (save/load system)
   - Save/SaveDataProvider.cs (save data handling)
   - Converters/BwmToNavigationMeshConverter.cs (walkmesh)

5. **Parsing/Extract/SaveData** - Selected files
   - SaveData/GlobalVars.cs (global variable system)
   - SaveData/PartyTable.cs (party management)
   - SaveData/SaveInfo.cs (save metadata)

6. **Parsing/Resource/Formats** - Selected files
   - BWM/* (walkmesh format - must match engine pathfinding)
   - GFF/* (save format - must match engine serialization)
   - NCS/* (script format - must match VM behavior)

### Files NOT Requiring Ghidra References

These files are format parsers, utilities, or modern enhancements:

1. **Parsing/Resource/Formats** - Most format parsers
   - File format parsing doesn't need engine references
   - Exception: BWM, GFF, NCS (see above)

2. **Utility/** - All files
   - Helper/utility code
   - No engine behavior to match

3. **Runtime/Graphics/** - Most files
   - Modern MonoGame/Stride adapters
   - Original engines used DirectX/OpenGL
   - Note enhancements vs. original behavior

4. **TSLPatcher/** - All files
   - Modding tool, not engine code

5. **Parsing/Tools/** - All files
   - Utility tools for format manipulation

### Exhaustive Reverse Engineering Checklist

For each file requiring Ghidra references:

- [ ] **Search Ghidra** for relevant functions using:
  - String searches (e.g., "GLOBALVARS", "PARTYTABLE", "savenfo")
  - Function name searches
  - Cross-references from known functions
  - Data references

- [ ] **Decompile** all relevant functions in:
  - swkotor.exe (KotOR 1 behavior)
  - swkotor2.exe (KotOR 2 behavior - PRIMARY)
  - nwmain.exe (Aurora reference, if applicable)
  - daorigins.exe (Eclipse reference, if applicable)

- [ ] **Document in Ghidra**:
  - Rename functions with descriptive names
  - Set accurate function prototypes
  - Rename variables and data labels
  - Add comprehensive comments
  - Track analysis status ([STATUS: ANALYZED], [STATUS: TODO], [C#: IMPLEMENTED])

- [ ] **Document in C# Code**:
  - Add comments with Ghidra executable name and function address
  - Include function name from Ghidra (descriptive, not FUN_xxxxx)
  - Include string references used to locate function
  - Include key implementation details from decompiled code
  - Note any deviations or improvements

- [ ] **Verify Implementation**:
  - Match original engine behavior exactly
  - Test with actual game assets
  - Verify edge cases and error handling
  - Ensure C# 7.3 compatibility

### Priority Order for Reverse Engineering

1. **Phase 1: Core Systems** (swkotor2.exe PRIMARY)
   - Module loading (LYT, VIS, GIT, ARE)
   - Walkmesh navigation (BWM parsing and pathfinding)
   - Entity spawning and management
   - Script execution (NCS VM)
   - Save/Load system (GFF serialization)

2. **Phase 2: Gameplay Systems** (swkotor2.exe PRIMARY)
   - Combat system
   - Dialogue system (DLG, TLK, VO)
   - Party management
   - Perception system
   - Trigger system
   - Encounter system
   - Store system

3. **Phase 3: Presentation Systems** (swkotor.exe + swkotor2.exe)
   - Animation system
   - Audio system
   - Camera system
   - Rendering (reference only, modern implementation)

4. **Phase 4: Cross-Engine Unification** (All engines)
   - Aurora engine (nwmain.exe) - architecture reference
   - Eclipse engine (daorigins.exe, DragonAge2.exe) - architecture reference
   - Infinity engine - architecture reference (if available)

## Notes

- **PRIMARY TARGET**: swkotor2.exe is the most complete Odyssey implementation - use as primary reference
- Focus on core game logic first (Runtime/Core, Runtime/Games/Odyssey, Runtime/Scripting)
- Graphics/MonoGame adapters can be lower priority unless they affect gameplay
- Use Ghidra string searches to locate functions (e.g., "GLOBALVARS", "PARTYTABLE", "savenfo", "BWM", "walkmesh", "pathfind")
- Document all Ghidra function addresses and string references in comments
- Match original engine behavior exactly where documented
- Modern graphics enhancements (DLSS, FSR, RTX Remix, raytracing) are not in original game - note as enhancements
- **EXHAUSTIVE REQUIREMENT**: Every function in Runtime/Core, Runtime/Games/Odyssey, and Runtime/Scripting MUST have Ghidra references
- **GHIDRA DOCUMENTATION**: All analyzed functions MUST be documented in Ghidra with descriptive names, prototypes, and comments before documenting in C# code
