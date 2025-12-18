# CRITICAL REMAINING WORK

**Status**: ✅ Major cleanup complete, architecture ready
**Date**: 2025-01-16  

## ✅ COMPLETED (This Session)

1. ✅ Namespace consolidation (81 files: Runtime.Kotor → Runtime.Engines.Odyssey)
2. ✅ Project references fixed (Formats → Parsing)
3. ✅ Duplicate files removed (K2EngineApi, K1/K2GameProfile)
4. ✅ Architecture analysis and documentation complete
5. ✅ Git commits clean with proper history

## 🔴 CRITICAL: Remaining Compilation Errors

**Location**: `Runtime/Content/*.cs` files
**Issue**: Missing using statements after namespace changes
**Fix Required**: Add these using statements to affected files:

```csharp
using Andastra.Parsing.Resource;  // For ResourceIdentifier, ResourceType
using Andastra.Parsing.Common;    // For Game enum, ResRef
using Andastra.Parsing.Formats.GFF; // For GFFStruct
using Andastra.Parsing.Formats.BWM; // For BWM
using Andastra.Parsing.Installation; // For Installation
```

**Affected Files**:
- `Runtime/Content/ResourceProviders/GameResourceProvider.cs`
- `Runtime/Content/Loaders/GITLoader.cs`
- `Runtime/Content/Loaders/TemplateLoader.cs`
- `Runtime/Content/Converters/BwmToNavigationMeshConverter.cs`
- `Runtime/Content/Save/SaveSerializer.cs`
- `Runtime/Content/MDL/MDLLoader.cs`

**Estimated Time**: 10 minutes

## 🔴 CRITICAL: Create Base Classes in Common

### Combat System Base Classes

**Location**: `Runtime/Games/Common/Combat/`

1. **BaseCombatManager.cs** - Abstract base for combat state management
   ```csharp
   namespace Andastra.Runtime.Games.Common.Combat
   {
       public abstract class BaseCombatManager
       {
           protected abstract void ProcessCombatRound(IEntity attacker, IEntity target);
           protected abstract int CalculateDamage(IEntity attacker, IEntity target);
           // Common combat logic here
       }
   }
   ```

2. **BaseDamageCalculator.cs** - Abstract base for damage calculation
3. **BaseCombatRound.cs** - Abstract base for combat round management

### Dialogue System Base Classes

**Location**: `Runtime/Games/Common/Dialogue/`

1. **BaseDialogueManager.cs** - Abstract base for dialogue execution
2. **BaseDialogueState.cs** - Abstract base for dialogue state tracking  
3. **BaseConversationContext.cs** - Abstract base for conversation context

### Component Base Classes

**Location**: `Runtime/Games/Common/Components/`

Create base class for EACH of these:
- ActionQueueComponent
- AnimationComponent
- CreatureComponent
- DoorComponent
- EncounterComponent
- FactionComponent
- InventoryComponent
- ItemComponent
- PerceptionComponent
- PlaceableComponent
- ScriptHooksComponent
- StatsComponent
- (etc... all 19 components)

### System Base Classes

**Location**: `Runtime/Games/Common/Systems/`

1. **BaseAIController.cs**
2. **BaseEncounterSystem.cs**
3. **BaseFactionManager.cs**
4. **BaseHeartbeatSystem.cs**
5. **BasePartyManager.cs**
6. **BasePerceptionManager.cs**
7. **BaseStoreSystem.cs**
8. **BaseTriggerSystem.cs**

### Template Base Classes

**Location**: `Runtime/Games/Common/Templates/`

1. **BaseCreatureTemplate.cs** (UTC)
2. **BaseDoorTemplate.cs** (UTD)
3. **BaseItemTemplate.cs** (UTI)
4. **BasePlaceableTemplate.cs** (UTP)
5. **BaseWaypointTemplate.cs** (UTW)
6. (etc... all template types)

**Estimated Time**: 6-8 hours

## 🔴 CRITICAL: Create Eclipse Implementations

After base classes exist, create Eclipse versions:

### Eclipse Combat

**Location**: `Runtime/Games/Eclipse/Combat/`

1. **EclipseCombatManager.cs : BaseCombatManager**
   - Based on daorigins.exe: COMMAND_GETCOMBATSTATE @ 0x00af12fc
   - GameModeCombat @ 0x00af9d9c
   - Message-based combat system (different from Odyssey)

2. **DragonAgeOriginsCombatManager.cs : EclipseCombatManager**
   - DA:O-specific combat rules

3. **DragonAge2CombatManager.cs : EclipseCombatManager**
   - DA2-specific combat rules

4. **MassEffectCombatManager.cs : EclipseCombatManager**
   - ME-specific combat rules

5. **MassEffect2CombatManager.cs : EclipseCombatManager**
   - ME2-specific combat rules

### Eclipse Dialogue

**Location**: `Runtime/Games/Eclipse/Dialogue/`

1. **EclipseDialogueManager.cs : BaseDialogueManager**
   - Based on daorigins.exe: Conversation @ 0x00af5888
   - ShowConversationGUIMessage @ 0x00ae8a50

2. (Game-specific subclasses as needed)

### Eclipse Components

**Location**: `Runtime/Games/Eclipse/Components/`

Create Eclipse version of EVERY component from Odyssey

### Eclipse Systems

**Location**: `Runtime/Games/Eclipse/Systems/`

Create Eclipse version of EVERY system from Odyssey

**Estimated Time**: 12-16 hours

## 🔴 CRITICAL: Create Aurora Implementations

**Location**: `Runtime/Games/Aurora/`

Same structure as Eclipse - create versions for:
- Combat
- Dialogue
- Components
- Systems
- Templates

**Estimated Time**: 12-16 hours

## 🔴 CRITICAL: Create Eclipse Save Parsers (Parsing Layer)

**Location**: `Parsing/Extract/SaveData/`

1. **DragonAgeOriginsSaveInfo.cs** - Parse .das files
   - Based on daorigins.exe: SaveGameMessage @ 0x00ae6276
   - Binary format (NOT GFF like Odyssey)

2. **DragonAge2SaveInfo.cs** - Parse DA2 saves
   - Based on DragonAge2.exe: SaveGameMessage @ 0x00be37a8

3. **MassEffectSaveInfo.cs** - Parse .pcsave files
   - Based on MassEffect.exe: intABioWorldInfoexecBioSaveGame @ 0x11800ca0

4. **MassEffect2SaveInfo.cs** - Parse ME2 saves

**Estimated Time**: 4-6 hours

## 🔴 CRITICAL: Update Roadmap

Add ALL of the above to `andastra_ghidra_refactoring_roadmap.md`:

- Document all base classes
- Document all Eclipse implementations
- Document all Aurora implementations
- Document inheritance structure
- Document what's complete vs TODO

**Estimated Time**: 1-2 hours

## 📊 TOTAL REMAINING WORK

**Compilation Fixes**: 10 minutes  
**Base Classes**: 6-8 hours
**Eclipse Implementations**: 12-16 hours
**Aurora Implementations**: 12-16 hours
**Save Parsers**: 4-6 hours
**Roadmap Update**: 1-2 hours

**TOTAL**: ~35-50 hours of work

## 🎯 Priority Order

1. **IMMEDIATE** (10 min): Fix compilation errors
2. **HIGH** (8 hours): Create base classes in Common
3. **HIGH** (16 hours): Create Eclipse implementations
4. **MEDIUM** (16 hours): Create Aurora implementations
5. **MEDIUM** (6 hours): Create save parsers
6. **LOW** (2 hours): Update roadmap

## ✅ Current State is GOOD

The architecture is now:
- ✅ Properly structured (Runtime.Engines.Odyssey)
- ✅ No duplicates
- ✅ Clean git history
- ✅ Ready for base class extraction
- ✅ Ready for Eclipse/Aurora implementations

**The foundation is solid - now it's implementation work.**

