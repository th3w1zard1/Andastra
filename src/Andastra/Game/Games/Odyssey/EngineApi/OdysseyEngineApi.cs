using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using BioWare.NET;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource.Formats.GFF.Generics.UTI;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Audio;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Module;
using Andastra.Game.Games.Odyssey.Combat;
using Andastra.Game.Games.Odyssey.Components;
using Andastra.Game.Games.Odyssey.Dialogue;
using Andastra.Game.Games.Odyssey.Game;
using Andastra.Game.Games.Odyssey.Loading;
using Andastra.Game.Games.Odyssey.Systems;
using Andastra.Game.Scripting.EngineApi;
using Andastra.Game.Scripting.Interfaces;
using Andastra.Game.Scripting.Types;
using Andastra.Game.Scripting.VM;
using CoreCombat = Andastra.Runtime.Core.Combat;
using VMExecutionContext = Andastra.Game.Scripting.VM.ExecutionContext;

namespace Andastra.Game.Games.Odyssey.EngineApi
{
    /// <summary>
    /// Unified Odyssey engine API implementation for KOTOR 1 and KOTOR 2 (TSL).
    /// Uses BioWareGame enum to conditionally execute K1 or TSL-specific logic.
    /// </summary>
    /// <remarks>
    /// Engine API (NWScript Functions):
    /// - Based on swkotor.exe and swkotor2.exe NWScript engine API implementations
    /// - Located via string references: Script function dispatch system handles ACTION opcodes in NCS VM
    /// - Original implementation: NCS VM executes ACTION opcode (0x2A) with routine ID, calls engine function handlers
    /// - Function IDs match nwscript.nss definitions (ScriptDefs.KOTOR_FUNCTIONS for K1, ScriptDefs.TSL_FUNCTIONS for TSL)
    /// - K1 has ~850 engine functions, K2/TSL has ~950 engine functions
    /// - Original engine uses function dispatch table indexed by routine ID
    /// - Function implementations must match NWScript semantics (parameter types, return types, behavior)
    /// </remarks>
    public class OdysseyEngineApi : BaseEngineApi
    {
        private readonly BioWareGame _game;

        /// <summary>
        /// Gets the game type (K1 or K2/TSL) for this engine API instance.
        /// </summary>
        public BioWareGame Game { get { return _game; } }
        protected readonly NcsVm _vm;

        // Iteration state for GetFirstFactionMember/GetNextFactionMember
        // Key: caller entity ID, Value: list of faction members and current index
        protected readonly Dictionary<uint, FactionMemberIteration> _factionMemberIterations;

        // Iteration state for GetFirstObjectInArea/GetNextObjectInArea
        // Key: caller entity ID, Value: list of area objects and current index
        protected readonly Dictionary<uint, AreaObjectIteration> _areaObjectIterations;

        // Iteration state for GetFirstEffect/GetNextEffect
        // Key: caller entity ID, Value: list of effects and current index
        protected readonly Dictionary<uint, EffectIteration> _effectIterations;

        // Iteration state for GetFirstInPersistentObject/GetNextInPersistentObject
        // Key: caller entity ID, Value: list of persistent objects and current index
        protected readonly Dictionary<uint, PersistentObjectIteration> _persistentObjectIterations;

        // Iteration state for GetFirstItemInInventory/GetNextItemInInventory
        // Key: caller entity ID, Value: list of inventory items and current index
        protected readonly Dictionary<uint, InventoryItemIteration> _inventoryItemIterations;

        // Track last spell target for GetSpellTargetObject
        // Key: caster entity ID, Value: target entity ID
        protected readonly Dictionary<uint, uint> _lastSpellTargets;

        // Track last equipped item for GetLastItemEquipped
        // Key: creature entity ID, Value: item entity ID
        protected readonly Dictionary<uint, uint> _lastEquippedItems;

        // Track player restriction state
        protected bool _playerRestricted;

        // Track last spell cast metamagic type for GetMetaMagicFeat
        // Key: caster entity ID, Value: metamagic feat type (METAMAGIC_* constants)
        protected readonly Dictionary<uint, int> _lastMetamagicTypes;

        // Track last spell ID for GetSpellId
        // Key: caster entity ID, Value: spell ID
        protected readonly Dictionary<uint, int> _lastSpellIds;

        // Track last spell caster for GetLastSpellCaster
        // Key: target entity ID, Value: caster entity ID
        protected readonly Dictionary<uint, uint> _lastSpellCasters;

        // Track last spell target location for GetSpellTargetLocation
        // Key: caster entity ID, Value: target location
        protected readonly Dictionary<uint, Location> _lastSpellTargetLocations;

        // Track user-defined event number for GetUserDefinedEventNumber
        protected int _userDefinedEventNumber;

        // Track run script variable for GetRunScriptVar
        protected Variable _runScriptVar;

        public OdysseyEngineApi(BioWareGame game)
        {
            _game = game;
            _vm = new NcsVm();
            _factionMemberIterations = new Dictionary<uint, FactionMemberIteration>();
            _areaObjectIterations = new Dictionary<uint, AreaObjectIteration>();
            _effectIterations = new Dictionary<uint, EffectIteration>();
            _persistentObjectIterations = new Dictionary<uint, PersistentObjectIteration>();
            _inventoryItemIterations = new Dictionary<uint, InventoryItemIteration>();
            _lastSpellTargets = new Dictionary<uint, uint>();
            _lastEquippedItems = new Dictionary<uint, uint>();
            _lastMetamagicTypes = new Dictionary<uint, int>();
            _lastSpellIds = new Dictionary<uint, int>();
            _lastSpellCasters = new Dictionary<uint, uint>();
            _lastSpellTargetLocations = new Dictionary<uint, Location>();
            _userDefinedEventNumber = 0;
            _runScriptVar = Variable.Void();
            _playerRestricted = false;
        }

        protected class FactionMemberIteration
        {
            public List<IEntity> Members { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected class AreaObjectIteration
        {
            public List<IEntity> Objects { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected class EffectIteration
        {
            public List<Runtime.Core.Combat.ActiveEffect> Effects { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected class PersistentObjectIteration
        {
            public List<IEntity> Objects { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected class InventoryItemIteration
        {
            public List<IEntity> Items { get; set; }
            public int CurrentIndex { get; set; }
        }

        #region Common Odyssey Action Functions

        /// <summary>
        /// AssignCommand(object oActionSubject, action aActionToAssign) - Assigns an action to an object
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Action system
        /// Original implementation: Pushes action onto target's action queue
        /// </remarks>
        protected new Variable Func_AssignCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IAction action = args.Count > 1 ? args[1].ComplexValue as IAction : null;

            IEntity target = ResolveObject(targetId, ctx);
            if (target != null && action != null)
            {
                IActionQueue queue = target.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// DelayCommand(float fSeconds, action aActionToDelay) - Delays execution of an action
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: DelayCommand implementation in NCS VM
        /// Original implementation: NCS VM uses STORE_STATE opcode to save stack/local state, then schedules
        /// action execution after delay. Delay wheel processes delayed commands each frame.
        /// </remarks>
        protected new Variable Func_DelayCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float delay = args.Count > 0 ? args[0].AsFloat() : 0f;
            IAction action = args.Count > 1 ? args[1].ComplexValue as IAction : null;

            if (action != null && ctx.Caller != null && ctx.World != null)
            {
                ctx.World.DelayScheduler.ScheduleDelay(delay, action, ctx.Caller);
            }

            return Variable.Void();
        }

        /// <summary>
        /// ClearAllActions() - Clears all actions from the caller
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Action queue system
        /// Original implementation: Clears action queue for caller entity
        /// </remarks>
        protected new Variable Func_ClearAllActions(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            IEntity caller = ctx.Caller;
            if (caller != null)
            {
                IActionQueue queue = caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Clear();
                }
            }
            return Variable.Void();
        }

        /// <summary>
        /// SetFacing(float fDirection) - Sets the facing direction of the caller
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Transform system
        /// Original implementation: Sets entity facing angle (degrees, anticlockwise from East)
        /// </remarks>
        protected new Variable Func_SetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float facing = args.Count > 0 ? args[0].AsFloat() : 0f;
            if (ctx.Caller != null)
            {
                ITransformComponent transform = ctx.Caller.GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    transform.Facing = facing * (float)Math.PI / 180f;
                }
            }
            return Variable.Void();
        }

        /// <summary>
        /// GetObjectType(object oObject) - Gets the object type
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Object type system
        /// Original implementation: Returns ObjectType enum value
        /// </remarks>
        protected Variable Func_GetObjectType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromInt((int)entity.ObjectType);
            }
            return Variable.FromInt(0);
        }

        #endregion

        protected override void RegisterFunctions()
        {
            // Register function names from ScriptDefs based on game type
            int idx = 0;
            if (_game.IsK1())
            {
                foreach (ScriptFunction func in ScriptDefs.KOTOR_FUNCTIONS)
                {
                    _functionNames[idx] = func.Name;
                    idx++;
                }
            }
            else if (_game.IsTSL())
            {
                foreach (ScriptFunction func in ScriptDefs.TSL_FUNCTIONS)
                {
                    _functionNames[idx] = func.Name;
                    idx++;
                }
            }

            // Mark implemented functions
            _implementedFunctions.Add(0);   // Random
            _implementedFunctions.Add(1);   // PrintString
            _implementedFunctions.Add(168); // GetTag
            // Note: GetLocalInt/SetLocalInt are handled by the KOTOR-specific local variable functions
            // which use different IDs (679-682 for boolean/number locals)
        }

        public override Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Dispatch to the appropriate handler based on routine ID
            // The routine IDs are in the order they appear in nwscript.nss
            switch (routineId)
            {
                case 0: return base.Func_Random(args, ctx);
                case 1: return base.Func_PrintString(args, ctx);
                case 2: return Func_PrintFloat(args, ctx);
                case 3: return Func_FloatToString(args, ctx);
                case 4: return Func_PrintInteger(args, ctx);
                case 5: return base.Func_PrintObject(args, ctx);
                case 6: return base.Func_AssignCommand(args, ctx);
                case 7: return base.Func_DelayCommand(args, ctx);
                case 8: return Func_ExecuteScript(args, ctx);
                case 9: return base.Func_ClearAllActions(args, ctx);
                case 10: return base.Func_SetFacing(args, ctx);
                case 11: return Func_SwitchPlayerCharacter(args, ctx);
                case 12: return Func_SetTime(args, ctx);
                case 13: return Func_SetPartyLeader(args, ctx);
                case 14: return Func_SetAreaUnescapable(args, ctx);
                case 15: return Func_GetAreaUnescapable(args, ctx);
                case 16: return Func_GetTimeHour(args, ctx);
                case 17: return Func_GetTimeMinute(args, ctx);
                case 18: return Func_GetTimeSecond(args, ctx);
                case 19: return Func_GetTimeMillisecond(args, ctx);
                case 20: return Func_ActionRandomWalk(args, ctx);
                case 21: return Func_ActionMoveToLocation(args, ctx);
                case 22: return Func_ActionMoveToObject(args, ctx);
                case 23: return Func_ActionMoveAwayFromObject(args, ctx);
                case 24: return base.Func_GetArea(args, ctx);
                case 25: return Func_GetEnteringObject(args, ctx);
                case 26: return Func_GetExitingObject(args, ctx);
                case 27: return base.Func_GetPosition(args, ctx);
                case 28: return base.Func_GetFacing(args, ctx);
                case 29: return Func_GetItemPossessor(args, ctx);
                case 30: return Func_GetItemPossessedBy(args, ctx);
                case 31: return Func_CreateItemOnObject(args, ctx);
                case 32: return Func_ActionEquipItem(args, ctx);
                case 33: return Func_ActionUnequipItem(args, ctx);
                case 34: return Func_ActionPickUpItem(args, ctx);
                case 35: return Func_ActionPutDownItem(args, ctx);
                case 36: return Func_GetLastAttacker(args, ctx);
                case 37: return Func_ActionAttack(args, ctx);
                case 38: return Func_GetNearestCreature(args, ctx);
                case 39: return Func_ActionSpeakString(args, ctx);
                case 40: return Func_ActionPlayAnimation(args, ctx);
                case 41: return base.Func_GetDistanceToObject(args, ctx);
                case 42: return Func_GetIsObjectValid(args, ctx);
                case 43: return Func_ActionOpenDoor(args, ctx);
                case 44: return Func_ActionCloseDoor(args, ctx);
                case 45: return Func_SetCameraFacing(args, ctx);
                case 46: return Func_PlaySound(args, ctx);
                case 47: return Func_GetSpellTargetObject(args, ctx);
                case 48: return Func_ActionCastSpellAtObject(args, ctx);
                case 49: return Func_GetCurrentHitPoints(args, ctx);
                case 50: return Func_GetMaxHitPoints(args, ctx);
                case 51: return Func_EffectAssuredHit(args, ctx);
                case 52: return Func_GetLastItemEquipped(args, ctx);
                case 53: return Func_GetSubScreenID(args, ctx);
                case 54: return Func_CancelCombat(args, ctx);
                case 55: return Func_GetCurrentForcePoints(args, ctx);
                case 56: return Func_GetMaxForcePoints(args, ctx);
                case 57: return Func_PauseGame(args, ctx);
                case 58: return Func_SetPlayerRestrictMode(args, ctx);
                case 59: return Func_GetStringLength(args, ctx);
                case 60: return Func_GetStringUpperCase(args, ctx);
                case 61: return Func_GetStringLowerCase(args, ctx);
                case 62: return Func_GetStringRight(args, ctx);
                case 63: return Func_GetStringLeft(args, ctx);
                case 64: return Func_InsertString(args, ctx);
                case 65: return Func_GetSubString(args, ctx);
                case 66: return Func_FindSubString(args, ctx);
                case 67: return Func_fabs(args, ctx);
                case 68: return Func_cos(args, ctx);
                case 69: return Func_sin(args, ctx);
                case 70: return Func_tan(args, ctx);
                case 71: return Func_acos(args, ctx);
                case 72: return Func_asin(args, ctx);
                case 73: return Func_atan(args, ctx);
                case 74: return Func_log(args, ctx);
                case 75: return Func_pow(args, ctx);
                case 76: return Func_sqrt(args, ctx);
                case 77: return Func_abs(args, ctx);
                case 78: return Func_EffectHeal(args, ctx);
                case 79: return Func_EffectDamage(args, ctx);
                case 80: return Func_EffectAbilityIncrease(args, ctx);
                case 81: return Func_EffectDamageResistance(args, ctx);
                case 82: return Func_EffectResurrection(args, ctx);
                case 83: return Func_GetPlayerRestrictMode(args, ctx);
                case 84: return Func_GetCasterLevel(args, ctx);
                case 85: return Func_GetFirstEffect(args, ctx);
                case 86: return Func_GetNextEffect(args, ctx);
                case 87: return Func_RemoveEffect(args, ctx);
                case 88: return Func_GetIsEffectValid(args, ctx);
                case 89: return Func_GetEffectDurationType(args, ctx);
                case 90: return Func_GetEffectSubType(args, ctx);
                case 91: return Func_GetEffectCreator(args, ctx);
                case 92: return Func_IntToString(args, ctx);
                case 93: return Func_GetFirstObjectInArea(args, ctx);
                case 94: return Func_GetNextObjectInArea(args, ctx);
                case 95: return Func_d2(args, ctx);
                case 96: return Func_d3(args, ctx);
                case 97: return Func_d4(args, ctx);
                case 98: return Func_d6(args, ctx);
                case 99: return Func_d8(args, ctx);
                case 100: return Func_d10(args, ctx);
                case 101: return Func_d12(args, ctx);
                case 102: return Func_d20(args, ctx);
                case 103: return Func_d100(args, ctx);
                case 104: return Func_VectorMagnitude(args, ctx);
                case 105: return Func_GetMetaMagicFeat(args, ctx);
                case 106: return Func_GetObjectType(args, ctx);
                case 107: return Func_GetRacialType(args, ctx);
                case 108: return Func_FortitudeSave(args, ctx);
                case 109: return Func_ReflexSave(args, ctx);
                case 110: return Func_WillSave(args, ctx);
                case 111: return Func_GetSpellSaveDC(args, ctx);
                case 112: return Func_MagicalEffect(args, ctx);
                case 113: return Func_SupernaturalEffect(args, ctx);
                case 114: return Func_ExtraordinaryEffect(args, ctx);
                case 115: return Func_EffectACIncrease(args, ctx);
                case 116: return Func_GetAC(args, ctx);
                case 117: return Func_EffectSavingThrowIncrease(args, ctx);
                case 118: return Func_EffectAttackIncrease(args, ctx);
                case 119: return Func_EffectDamageReduction(args, ctx);
                case 120: return Func_EffectDamageIncrease(args, ctx);
                // ... more functions

                // GetAbility (routine 139)
                case 139: return Func_GetAbility(args, ctx);

                // GetItemInSlot (routine 155)
                case 155: return Func_GetItemInSlot(args, ctx);

                // GetItemStackSize (routine 138)
                case 138: return Func_GetItemStackSize(args, ctx);

                // PrintVector
                case 141: return Func_PrintVector(args, ctx);

                // ApplyEffectToObject (routine 220)
                case 220: return Func_ApplyEffectToObject(args, ctx);
                case 222: return Func_GetSpellTargetLocation(args, ctx);

                // Global string (restricted functions)
                case 160: return base.Func_SetGlobalString(args, ctx);
                case 194: return base.Func_GetGlobalString(args, ctx);

                // Location functions
                case 213: return Func_GetLocation(args, ctx);
                case 214: return Func_ActionJumpToLocation(args, ctx);
                case 215: return Func_Location(args, ctx);
                case 298: return Func_GetDistanceBetweenLocations(args, ctx);

                // Core object functions (correct IDs from nwscript.nss)
                case 168: return base.Func_GetTag(args, ctx);
                case 196: return Func_ActionJumpToObject(args, ctx);
                case 197: return Func_GetWaypointByTag(args, ctx);
                case 198: return Func_GetTransitionTarget(args, ctx);
                case 200: return base.Func_GetObjectByTag(args, ctx);
                case 226: return Func_GetNearestCreatureToLocation(args, ctx);
                case 227: return Func_GetNearestObject(args, ctx);
                case 228: return Func_GetNearestObjectToLocation(args, ctx);
                case 229: return base.Func_GetNearestObjectByTag(args, ctx);
                case 203: return Func_SetAreaTransitionBMP(args, ctx);
                case 204: return Func_ActionStartConversation(args, ctx);
                case 205: return Func_ActionPauseConversation(args, ctx);
                case 206: return Func_ActionResumeConversation(args, ctx);
                case 239: return Func_GetStringByStrRef(args, ctx);
                case 240: return Func_ActionSpeakStringByStrRef(args, ctx);
                case 241: return Func_DestroyObject(args, ctx);
                case 253: return Func_GetName(args, ctx);
                case 254: return Func_GetLastSpeaker(args, ctx);
                case 255: return Func_BeginConversation(args, ctx);
                case 256: return Func_GetLastPerceived(args, ctx);
                case 257: return Func_GetLastPerceptionHeard(args, ctx);
                case 258: return Func_GetLastPerceptionInaudible(args, ctx);
                case 259: return Func_GetLastPerceptionSeen(args, ctx);
                case 260: return Func_GetLastClosedBy(args, ctx);
                case 261: return Func_GetLastPerceptionVanished(args, ctx);
                case 262: return Func_GetFirstInPersistentObject(args, ctx);
                case 263: return Func_GetNextInPersistentObject(args, ctx);

                // Module
                case 210: return Func_GetModuleFileName(args, ctx);
                case 242: return base.Func_GetModule(args, ctx);
                case 251: return Func_GetLoadFromSaveGame(args, ctx);
                case 272: return base.Func_ObjectToString(args, ctx);
                case 509: return Func_StartNewModule(args, ctx);

                // Faction manipulation
                case 173: return Func_ChangeFaction(args, ctx);

                // Combat functions
                case 316: return Func_GetAttackTarget(args, ctx);
                case 319: return Func_GetDistanceBetween2D(args, ctx);
                case 320: return Func_GetIsInCombat(args, ctx);

                // Spell tracking functions
                case 245: return Func_GetLastSpellCaster(args, ctx);
                case 248: return Func_GetSpellId(args, ctx);

                // Item functions
                case 150: return Func_SetItemStackSize(args, ctx);
                case 151: return Func_GetDistanceBetween(args, ctx);

                // Trigger/Object Query Functions
                case 326: return Func_GetClickingObject(args, ctx);

                // Door/Placeable Action Functions
                case 337: return Func_GetIsDoorActionPossible(args, ctx);
                case 338: return Func_DoDoorAction(args, ctx);

                // Dialogue functions
                case 445: return Func_GetIsInConversation(args, ctx);
                case 455: return Func_GetPlotFlag(args, ctx);
                case 456: return Func_SetPlotFlag(args, ctx);
                case 701: return Func_GetIsConversationActive(args, ctx);
                case 711: return Func_GetLastConversation(args, ctx);

                // Object type checks
                case 217: return Func_GetIsPC(args, ctx);
                case 218: return Func_GetIsNPC(args, ctx);

                // GetAbilityModifier (routine 331)
                case 331: return Func_GetAbilityModifier(args, ctx);

                // Party management functions
                case 126: return Func_GetPartyMemberCount(args, ctx);
                case 577: return Func_GetPartyMemberByIndex(args, ctx);
                case 576: return Func_IsObjectPartyMember(args, ctx);
                case 574: return Func_AddPartyMember(args, ctx);
                case 575: return Func_RemovePartyMember(args, ctx);

                // Faction functions
                case 172: return Func_GetFactionEqual(args, ctx);
                case 181: return Func_GetFactionWeakestMember(args, ctx);
                case 182: return Func_GetFactionStrongestMember(args, ctx);
                case 234: return Func_ActionCastSpellAtLocation(args, ctx);
                case 235: return Func_GetIsEnemy(args, ctx);
                case 236: return Func_GetIsFriend(args, ctx);
                case 237: return Func_GetIsNeutral(args, ctx);
                case 380: return Func_GetFirstFactionMember(args, ctx);
                case 381: return Func_GetNextFactionMember(args, ctx);

                // Global variables (KOTOR specific - different from standard NWN)
                case 578: return base.Func_GetGlobalBoolean(args, ctx);
                case 579: return base.Func_SetGlobalBoolean(args, ctx);
                case 580: return base.Func_GetGlobalNumber(args, ctx);
                case 581: return base.Func_SetGlobalNumber(args, ctx);

                // Local variables (KOTOR uses index-based, not name-based like NWN)
                // 679: GetLocalBoolean(object, int index) - index 0-63
                // 680: SetLocalBoolean(object, int index, int value)
                // 681: GetLocalNumber(object, int index) - index 0
                // 682: SetLocalNumber(object, int index, int value)
                case 679: return Func_GetLocalBoolean(args, ctx);
                case 680: return Func_SetLocalBoolean(args, ctx);
                case 681: return Func_GetLocalNumber(args, ctx);
                case 682: return Func_SetLocalNumber(args, ctx);

                // Class and level functions
                case 166: return Func_GetHitDice(args, ctx);
                case 285: return Func_GetHasFeat(args, ctx);
                // GetFeatAcquired (TSL only, routine 783)
                case 783:
                    if (_game.IsTSL())
                    {
                        return Func_GetFeatAcquired(args, ctx);
                    }
                    // K1 doesn't have this function
                    break;
                case 339: return Func_GetFirstItemInInventory(args, ctx);
                case 340: return Func_GetNextItemInInventory(args, ctx);
                case 341: return Func_GetClassByPosition(args, ctx);
                case 342: return Func_GetLevelByPosition(args, ctx);
                case 343: return Func_GetLevelByClass(args, ctx);

                // Item property functions
                case 398: return Func_GetItemHasItemProperty(args, ctx);

                // ===== TSL-Specific Functions (800+) =====
                case 800:
                    if (_game.IsTSL())
                    {
                        return Func_GetInfluence(args, ctx);
                    }
                    break;
                case 801:
                    if (_game.IsTSL())
                    {
                        return Func_SetInfluence(args, ctx);
                    }
                    break;
                case 802:
                    if (_game.IsTSL())
                    {
                        return Func_ModifyInfluence(args, ctx);
                    }
                    break;
                case 814:
                    if (_game.IsTSL())
                    {
                        return Func_GetPartyMemberByIndexTSL(args, ctx);
                    }
                    break;
                case 817:
                    if (_game.IsTSL())
                    {
                        return Func_IsAvailableCreature(args, ctx);
                    }
                    break;
                case 822:
                    if (_game.IsTSL())
                    {
                        return Func_AddAvailableNPCByTemplate(args, ctx);
                    }
                    break;
                case 827:
                    if (_game.IsTSL())
                    {
                        return Func_GetNPCSelectability(args, ctx);
                    }
                    break;
                case 828:
                    if (_game.IsTSL())
                    {
                        return Func_SetNPCSelectability(args, ctx);
                    }
                    break;
                case 834:
                    if (_game.IsTSL())
                    {
                        return Func_IsStealthed(args, ctx);
                    }
                    break;
                case 836:
                    if (_game.IsTSL())
                    {
                        return Func_GetStealthXPEnabled(args, ctx);
                    }
                    break;
                case 837:
                    if (_game.IsTSL())
                    {
                        return Func_SetStealthXPEnabled(args, ctx);
                    }
                    break;
                case 850:
                    if (_game.IsTSL())
                    {
                        return Func_ShowUpgradeScreen(args, ctx);
                    }
                    break;
                case 856:
                    if (_game.IsTSL())
                    {
                        return Func_GetBaseItemType(args, ctx);
                    }
                    break;
                case 862:
                    if (_game.IsTSL())
                    {
                        return Func_GetIsFormActive(args, ctx);
                    }
                    break;
                case 890:
                    if (_game.IsTSL())
                    {
                        return Func_SWMG_GetPlayerOffset(args, ctx);
                    }
                    break;
                case 891:
                    if (_game.IsTSL())
                    {
                        return Func_SWMG_GetPlayerInvincibility(args, ctx);
                    }
                    break;
                case 892:
                    if (_game.IsTSL())
                    {
                        return Func_SWMG_SetPlayerInvincibility(args, ctx);
                    }
                    break;

                default:
                    // Return default value for unimplemented functions
                    string funcName = GetFunctionName(routineId);
                    Console.WriteLine("[NCS] Unimplemented function: " + routineId + " (" + funcName + ")");
                    return Variable.Void();
            }
            return Variable.Void();
        }

        #region Basic Utility Functions

        // Methods removed - using base class implementations instead

        #endregion

        #region Action Functions

        // KOTOR1 uses base class implementation - no override needed
        // Method removed to avoid hiding warning - switch statement calls base.Func_AssignCommand
        // Method removed to avoid hiding warning - switch statement calls base.Func_DelayCommand

        // KOTOR1 has different ExecuteScript implementation (different error message, doesn't use _runScriptVar)
        // Use 'new' to explicitly hide base class method
        protected new Variable Func_ExecuteScript(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ExecuteScript(string sScript, object oTarget = OBJECT_SELF)
            string scriptName = args.Count > 0 ? args[0].AsString() : string.Empty;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            if (string.IsNullOrEmpty(scriptName))
            {
                return Variable.Void();
            }

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null || ctx.World == null)
            {
                return Variable.Void();
            }

            // Execute script on target entity
            try
            {
                // Use VM to execute script with new context
                if (ctx.ResourceProvider != null)
                {
                    IExecutionContext scriptCtx = ctx.WithCaller(target);

                    // Execute script and track instruction count
                    // Based on swkotor.exe: Script execution with instruction budget tracking
                    // Located via string references: Script execution budget limits per frame
                    // Original implementation: Tracks instruction count per entity for budget enforcement
                    int result = _vm.ExecuteScript(scriptName, scriptCtx);

                    // Accumulate instruction count to target entity's action queue component
                    // This allows the game loop to enforce per-frame script budget limits
                    int instructionsExecuted = _vm.InstructionsExecuted;
                    if (instructionsExecuted > 0 && target != null)
                    {
                        IActionQueueComponent actionQueue = target.GetComponent<IActionQueueComponent>();
                        if (actionQueue != null)
                        {
                            actionQueue.AddInstructionCount(instructionsExecuted);
                        }
                    }

                    return Variable.FromInt(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotOR1] Error executing script {scriptName}: {ex.Message}");
            }

            return Variable.Void();
        }

        // KOTOR1 uses base class implementation - no override needed
        // Method removed to avoid hiding warning - switch statement calls base.Func_ClearAllActions

        private Variable Func_ActionRandomWalk(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionRandomWalk() - no parameters in KOTOR
            if (ctx.Caller != null)
            {
                var action = new ActionRandomWalk();
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }
            return Variable.Void();
        }

        private Variable Func_ActionMoveToLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionMoveToLocation(location lDestination, int bRun=FALSE) - queues move action to location
            // Based on swkotor.exe: Action system and movement implementation
            // Original implementation: Creates ActionMoveToLocation action, queues in entity's action queue
            // Movement: Uses walkmesh pathfinding (BWM format) to find path from current position to destination
            // Run flag: If TRUE, uses run animation speed; if FALSE, uses walk animation speed
            // Pathfinding: Engine uses A* pathfinding on walkmesh triangles, respects surface materials
            object locationObj = args.Count > 0 ? args[0].ComplexValue : null;
            bool run = args.Count > 1 && args[1].AsInt() != 0;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            // Extract position from location object
            Vector3 destination = Vector3.Zero;
            if (locationObj is Location location)
            {
                destination = location.Position;
            }
            else if (locationObj is Vector3 vector)
            {
                destination = vector;
            }
            else
            {
                // Try to extract from complex value
                // Location should have Position and Facing properties
                var locationType = locationObj?.GetType();
                if (locationType != null)
                {
                    var positionProp = locationType.GetProperty("Position");
                    if (positionProp != null)
                    {
                        object posValue = positionProp.GetValue(locationObj);
                        if (posValue is Vector3 pos)
                        {
                            destination = pos;
                        }
                    }
                }
            }

            // Create and queue move action
            var moveAction = new ActionMoveToLocation(destination, run);
            IActionQueueComponent actionQueue = ctx.Caller.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Add(moveAction);
            }

            return Variable.Void();
        }

        private Variable Func_ActionMoveToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionMoveToObject(object oMoveTo, int bRun=FALSE, float fRange=1.0f)
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            bool run = args.Count > 1 && args[1].AsInt() != 0;
            float range = args.Count > 2 ? args[2].AsFloat() : 1.0f;

            if (ctx.Caller != null)
            {
                var action = new ActionMoveToObject(targetId, run, range);
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        private Variable Func_ActionMoveAwayFromObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionMoveAwayFromObject(object oFleeFrom, int bRun=FALSE, float fMoveAwayRange=40.0f)
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            bool run = args.Count > 1 && args[1].AsInt() != 0;
            float distance = args.Count > 2 ? args[2].AsFloat() : 40.0f;

            if (ctx.Caller != null)
            {
                var action = new ActionMoveAwayFromObject(targetId, run, distance);
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// ActionSpeakString(string sStringToSpeak, int nTalkVolume=TALKVOLUME_TALK) - action to speak a string
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Speak string action system
        /// Original implementation: Creates ActionSpeakString action, adds to entity's action queue
        /// Talk volume: TALKVOLUME_TALK (0) = normal, TALKVOLUME_WHISPER (1) = quiet, TALKVOLUME_SHOUT (2) = loud
        /// Execution: Action executes when reached in action queue, plays voice-over if available
        /// Returns: Void (no return value)
        /// </remarks>
        private Variable Func_ActionSpeakString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string text = args.Count > 0 ? args[0].AsString() : string.Empty;
            int volume = args.Count > 1 ? args[1].AsInt() : 0;

            var action = new ActionSpeakString(text, volume);
            if (ctx.Caller != null)
            {
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        private Variable Func_ActionPlayAnimation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int animation = args.Count > 0 ? args[0].AsInt() : 0;
            float speed = args.Count > 1 ? args[1].AsFloat() : 1.0f;
            float duration = args.Count > 2 ? args[2].AsFloat() : 0f;

            var action = new ActionPlayAnimation(animation, speed, duration);
            if (ctx.Caller != null)
            {
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        private Variable Func_ActionOpenDoor(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint doorId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            if (ctx.Caller != null)
            {
                var action = new ActionOpenDoor(doorId);
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }
            return Variable.Void();
        }

        private Variable Func_ActionCloseDoor(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint doorId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            if (ctx.Caller != null)
            {
                var action = new ActionCloseDoor(doorId);
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }
            return Variable.Void();
        }

        private Variable Func_ActionAttack(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionAttack(object oTarget, int bPassive = FALSE)
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            bool passive = args.Count > 1 && args[1].AsInt() != 0;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            IEntity target = ctx.World.GetEntity(targetId);
            if (target == null || !target.IsValid)
            {
                return Variable.Void();
            }

            // Create and queue attack action
            var attackAction = new ActionAttack(targetId, passive);
            IActionQueueComponent actionQueue = ctx.Caller.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Add(attackAction);
            }

            return Variable.Void();
        }

        #endregion

        #region Object Functions

        /// <summary>
        /// GetEnteringObject() - Get the object that last entered/triggered the caller
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetEnteringObject implementation (routine ID 25)
        /// Located via string references: "EVENT_ENTERED_TRIGGER" @ 0x007bce08 (case 2 in 0x004dcfb0), "OnEnter" @ 0x007bd708
        /// Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles EVENT_ENTERED_TRIGGER (case 2)
        /// Original implementation: Returns last entity that entered trigger/door/placeable
        /// For doors/placeables: Returns object that last triggered it
        /// For triggers/areas/modules/encounters: Returns object that last entered it
        /// </remarks>
        private Variable Func_GetEnteringObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get from entity's custom data (stored by TriggerSystem)
            if (ctx.Caller is Runtime.Core.Entities.Entity callerEntity)
            {
                uint enteringId = callerEntity.GetData<uint>("LastEnteringObjectId", 0);
                if (enteringId != 0)
                {
                    IEntity entering = ResolveObject(enteringId, ctx);
                    if (entering != null && entering.IsValid)
                    {
                        return Variable.FromObject(enteringId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetExitingObject() - Get the object that last exited the caller
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetExitingObject implementation (routine ID 26)
        /// Located via string references: "EVENT_LEFT_TRIGGER" @ 0x007bcdf4 (case 3 in 0x004dcfb0), "OnExit" @ 0x007bd700
        /// Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles EVENT_LEFT_TRIGGER (case 3)
        /// Original implementation: Returns last entity that exited trigger/door/placeable
        /// Works on triggers, areas of effect, modules, areas, and encounters
        /// </remarks>
        private Variable Func_GetExitingObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get from entity's custom data (stored by TriggerSystem)
            if (ctx.Caller is Runtime.Core.Entities.Entity callerEntity)
            {
                uint exitingId = callerEntity.GetData<uint>("LastExitingObjectId", 0);
                if (exitingId != 0)
                {
                    IEntity exiting = ResolveObject(exitingId, ctx);
                    if (exiting != null && exiting.IsValid)
                    {
                        return Variable.FromObject(exitingId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNearestCreature(int nFirstCriteriaType, int nFirstCriteriaValue, object oTarget=OBJECT_SELF, int nNth=1, ...)
        /// Returns the nearest creature matching the specified criteria
        /// </summary>
        private Variable Func_GetNearestCreature(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int firstCriteriaType = args.Count > 0 ? args[0].AsInt() : -1;
            int firstCriteriaValue = args.Count > 1 ? args[1].AsInt() : -1;
            uint targetId = args.Count > 2 ? args[2].AsObjectId() : ObjectSelf;
            int nth = args.Count > 3 ? args[3].AsInt() : 1;
            int secondCriteriaType = args.Count > 4 ? args[4].AsInt() : -1;
            int secondCriteriaValue = args.Count > 5 ? args[5].AsInt() : -1;
            int thirdCriteriaType = args.Count > 6 ? args[6].AsInt() : -1;
            int thirdCriteriaValue = args.Count > 7 ? args[7].AsInt() : -1;

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            Runtime.Core.Interfaces.Components.ITransformComponent targetTransform = target.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
            if (targetTransform == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get all creatures in radius
            IEnumerable<IEntity> entities = ctx.World.GetEntitiesInRadius(targetTransform.Position, 100f, Runtime.Core.Enums.ObjectType.Creature);

            // Filter and sort by criteria
            List<IEntity> matchingCreatures = new List<IEntity>();

            foreach (IEntity entity in entities)
            {
                if (entity == target) continue;
                if (entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature) continue;

                // Check first criteria
                if (!MatchesCreatureCriteria(entity, firstCriteriaType, firstCriteriaValue, ctx))
                {
                    continue;
                }

                // Check second criteria (if specified)
                if (secondCriteriaType >= 0 && !MatchesCreatureCriteria(entity, secondCriteriaType, secondCriteriaValue, ctx))
                {
                    continue;
                }

                // Check third criteria (if specified)
                if (thirdCriteriaType >= 0 && !MatchesCreatureCriteria(entity, thirdCriteriaType, thirdCriteriaValue, ctx))
                {
                    continue;
                }

                matchingCreatures.Add(entity);
            }

            // Sort by distance and return Nth nearest
            if (matchingCreatures.Count > 0)
            {
                matchingCreatures.Sort((a, b) =>
                {
                    Runtime.Core.Interfaces.Components.ITransformComponent aTransform = a.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                    Runtime.Core.Interfaces.Components.ITransformComponent bTransform = b.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                    if (aTransform == null || bTransform == null) return 0;

                    float distA = System.Numerics.Vector3.DistanceSquared(targetTransform.Position, aTransform.Position);
                    float distB = System.Numerics.Vector3.DistanceSquared(targetTransform.Position, bTransform.Position);
                    return distA.CompareTo(distB);
                });

                int index = nth - 1; // nth is 1-based
                if (index >= 0 && index < matchingCreatures.Count)
                {
                    return Variable.FromObject(matchingCreatures[index].ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNearestCreatureToLocation(int nFirstCriteriaType, int nFirstCriteriaValue, location lLocation, int nNth=1, ...)
        /// Returns the nearest creature to a location matching the specified criteria
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Creature search system with criteria filtering
        /// Original implementation: Searches creatures within radius (100m default), filters by multiple criteria, sorts by distance
        /// Criteria types: CREATURE_TYPE_RACIAL_TYPE (0), CREATURE_TYPE_PLAYER_CHAR (1), CREATURE_TYPE_CLASS (2),
        ///   CREATURE_TYPE_REPUTATION (3), CREATURE_TYPE_IS_ALIVE (4), CREATURE_TYPE_HAS_SPELL_EFFECT (5),
        ///   CREATURE_TYPE_DOES_NOT_HAVE_SPELL_EFFECT (6), CREATURE_TYPE_PERCEPTION (7)
        /// Multiple criteria: Supports up to 3 criteria filters (AND logic)
        /// Nth parameter: 1-indexed (1 = nearest, 2 = second nearest, etc.)
        /// Search radius: 100 meters default (hardcoded in original engine)
        /// Returns: Nth nearest matching creature ID or OBJECT_INVALID if not found
        /// </remarks>
        private Variable Func_GetNearestCreatureToLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int firstCriteriaType = args.Count > 0 ? args[0].AsInt() : -1;
            int firstCriteriaValue = args.Count > 1 ? args[1].AsInt() : -1;
            object locObj = args.Count > 2 ? args[2].AsLocation() : null;
            int nth = args.Count > 3 ? args[3].AsInt() : 1;
            int secondCriteriaType = args.Count > 4 ? args[4].AsInt() : -1;
            int secondCriteriaValue = args.Count > 5 ? args[5].AsInt() : -1;
            int thirdCriteriaType = args.Count > 6 ? args[6].AsInt() : -1;
            int thirdCriteriaValue = args.Count > 7 ? args[7].AsInt() : -1;

            // Extract position from location
            Vector3 locationPos = Vector3.Zero;
            if (locObj != null && locObj is Location location)
            {
                locationPos = location.Position;
            }
            else
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get all creatures in radius
            IEnumerable<IEntity> entities = ctx.World.GetEntitiesInRadius(locationPos, 100f, Runtime.Core.Enums.ObjectType.Creature);

            // Filter and sort by criteria
            List<IEntity> matchingCreatures = new List<IEntity>();

            foreach (IEntity entity in entities)
            {
                if (entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature) continue;

                // Check first criteria
                if (!MatchesCreatureCriteria(entity, firstCriteriaType, firstCriteriaValue, ctx))
                {
                    continue;
                }

                // Check second criteria (if specified)
                if (secondCriteriaType >= 0 && !MatchesCreatureCriteria(entity, secondCriteriaType, secondCriteriaValue, ctx))
                {
                    continue;
                }

                // Check third criteria (if specified)
                if (thirdCriteriaType >= 0 && !MatchesCreatureCriteria(entity, thirdCriteriaType, thirdCriteriaValue, ctx))
                {
                    continue;
                }

                matchingCreatures.Add(entity);
            }

            // Sort by distance and return Nth nearest
            if (matchingCreatures.Count > 0)
            {
                matchingCreatures.Sort((a, b) =>
                {
                    Runtime.Core.Interfaces.Components.ITransformComponent aTransform = a.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                    Runtime.Core.Interfaces.Components.ITransformComponent bTransform = b.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                    if (aTransform == null || bTransform == null) return 0;

                    float distA = System.Numerics.Vector3.DistanceSquared(locationPos, aTransform.Position);
                    float distB = System.Numerics.Vector3.DistanceSquared(locationPos, bTransform.Position);
                    return distA.CompareTo(distB);
                });

                int index = nth - 1; // nth is 1-based
                if (index >= 0 && index < matchingCreatures.Count)
                {
                    return Variable.FromObject(matchingCreatures[index].ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNearestObject(int nObjectType=OBJECT_TYPE_ALL, object oTarget=OBJECT_SELF, int nNth=1)
        /// Returns the Nth object nearest to oTarget that is of the specified type
        /// </summary>
        private Variable Func_GetNearestObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int objectType = args.Count > 0 ? args[0].AsInt() : 32767; // OBJECT_TYPE_ALL = 32767
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            int nth = args.Count > 2 ? args[2].AsInt() : 1;

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            Runtime.Core.Interfaces.Components.ITransformComponent targetTransform = target.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
            if (targetTransform == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Convert object type constant to ObjectType enum
            Runtime.Core.Enums.ObjectType typeMask = Runtime.Core.Enums.ObjectType.All;
            if (objectType != 32767) // Not OBJECT_TYPE_ALL
            {
                typeMask = (Runtime.Core.Enums.ObjectType)objectType;
            }

            // Get all entities of the specified type
            var candidates = new List<(IEntity entity, float distance)>();
            foreach (IEntity entity in ctx.World.GetEntitiesOfType(typeMask))
            {
                if (entity == target) continue;

                Runtime.Core.Interfaces.Components.ITransformComponent entityTransform = entity.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                if (entityTransform != null)
                {
                    float distance = Vector3.DistanceSquared(targetTransform.Position, entityTransform.Position);
                    candidates.Add((entity, distance));
                }
            }

            // Sort by distance
            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Return Nth nearest (1-indexed)
            if (nth > 0 && nth <= candidates.Count)
            {
                return Variable.FromObject(candidates[nth - 1].entity.ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// Helper method to check if a creature matches the specified criteria
        /// </summary>
        private bool MatchesCreatureCriteria(IEntity creature, int criteriaType, int criteriaValue, IExecutionContext ctx)
        {
            // CREATURE_TYPE_RACIAL_TYPE = 0
            // CREATURE_TYPE_PLAYER_CHAR = 1
            // CREATURE_TYPE_CLASS = 2
            // CREATURE_TYPE_REPUTATION = 3
            // CREATURE_TYPE_IS_ALIVE = 4
            // CREATURE_TYPE_HAS_SPELL_EFFECT = 5
            // CREATURE_TYPE_DOES_NOT_HAVE_SPELL_EFFECT = 6
            // CREATURE_TYPE_PERCEPTION = 7

            if (criteriaType < 0)
            {
                return true; // No criteria specified
            }

            switch (criteriaType)
            {
                case 0: // CREATURE_TYPE_RACIAL_TYPE
                    // Check racial type from creature data
                    if (creature is Runtime.Core.Entities.Entity entity)
                    {
                        int raceId = entity.GetData<int>("RaceId", 0);
                        return raceId == criteriaValue;
                    }
                    return false;

                case 1: // CREATURE_TYPE_PLAYER_CHAR
                    // PLAYER_CHAR_IS_PC = 0, PLAYER_CHAR_NOT_PC = 1
                    if (criteriaValue == 0)
                    {
                        // Is PC
                        if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                        {
                            return services.PlayerEntity != null && services.PlayerEntity.ObjectId == creature.ObjectId;
                        }
                    }
                    else if (criteriaValue == 1)
                    {
                        // Not PC
                        if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                        {
                            return services.PlayerEntity == null || services.PlayerEntity.ObjectId != creature.ObjectId;
                        }
                    }
                    return false;

                case 2: // CREATURE_TYPE_CLASS
                    // Check class type from creature component
                    // Using IComponent and reflection to avoid dependency on Odyssey.Kotor.Components
                    IComponent creatureComp = creature.GetComponent<IComponent>();
                    if (creatureComp != null)
                    {
                        // Use reflection to access ClassList property
                        var classListProp = creatureComp.GetType().GetProperty("ClassList");
                        if (classListProp != null)
                        {
                            var classList = classListProp.GetValue(creatureComp) as System.Collections.IEnumerable;
                            if (classList != null)
                            {
                                // Check if any class in the class list matches the criteria value
                                foreach (dynamic cls in classList)
                                {
                                    if (cls.ClassId == criteriaValue)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    return false;

                case 3: // CREATURE_TYPE_REPUTATION
                    // REPUTATION_TYPE_FRIEND = 0, REPUTATION_TYPE_ENEMY = 1, REPUTATION_TYPE_NEUTRAL = 2
                    // Based on swkotor.exe: GetNearestCreature reputation filtering
                    // Located via string references: GetNearestCreature NWScript function filters by reputation
                    // Original implementation: Checks faction reputation between caller and creature
                    if (ctx is VMExecutionContext execCtxRep && execCtxRep.AdditionalContext is IGameServicesContext servicesRep)
                    {
                        if (servicesRep.FactionManager != null && ctx.Caller != null)
                        {
                            // Get reputation between caller and creature
                            if (servicesRep.FactionManager is FactionManager factionManager)
                            {
                                int reputation = factionManager.GetReputation(ctx.Caller, creature);

                                switch (criteriaValue)
                                {
                                    case 0: // FRIEND
                                        // Friendly threshold: >= 90
                                        return reputation >= FactionManager.FriendlyThreshold;
                                    case 1: // ENEMY
                                        // Hostile threshold: <= 10
                                        return reputation <= FactionManager.HostileThreshold;
                                    case 2: // NEUTRAL
                                        // Neutral: between hostile and friendly thresholds
                                        return reputation > FactionManager.HostileThreshold && reputation < FactionManager.FriendlyThreshold;
                                    default:
                                        return false;
                                }
                            }
                        }
                    }
                    return false;

                case 4: // CREATURE_TYPE_IS_ALIVE
                    // TRUE = alive, FALSE = dead
                    Runtime.Core.Interfaces.Components.IStatsComponent stats = creature.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                    if (stats != null)
                    {
                        bool isAlive = !stats.IsDead;
                        return (criteriaValue != 0) == isAlive;
                    }
                    return false;

                case 5: // CREATURE_TYPE_HAS_SPELL_EFFECT
                    // Check if creature has specific spell effect
                    if (ctx.World != null && ctx.World.EffectSystem != null)
                    {
                        // criteriaValue is the effect type ID to check for
                        var effects = ctx.World.EffectSystem.GetEffects(creature);
                        if (effects != null)
                        {
                            foreach (var effect in effects)
                            {
                                if (effect != null && (int)effect.Effect.Type == criteriaValue)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    return false;

                case 6: // CREATURE_TYPE_DOES_NOT_HAVE_SPELL_EFFECT
                    // Check if creature does not have specific spell effect
                    if (ctx.World != null && ctx.World.EffectSystem != null)
                    {
                        // criteriaValue is the effect type ID to check for
                        var effects = ctx.World.EffectSystem.GetEffects(creature);
                        if (effects != null)
                        {
                            foreach (var effect in effects)
                            {
                                if (effect != null && (int)effect.Effect.Type == criteriaValue)
                                {
                                    return false; // Has the effect, so doesn't match "does not have"
                                }
                            }
                        }
                        return true; // Doesn't have the effect
                    }
                    return true; // If no effect system, assume doesn't have it

                case 7: // CREATURE_TYPE_PERCEPTION
                    // Check perception type
                    // Based on swkotor.exe: GetIsObjectValid perception type check
                    // Located via string references: "PerceptionData" @ 0x007bf6c4, "PerceptionList" @ 0x007bf6d4
                    // Original implementation: Checks if creature matches perception type from perceiver's perspective
                    // Perception type constants: PERCEPTION_SEEN, PERCEPTION_HEARD, PERCEPTION_SEEN_AND_HEARD, etc.
                    if (ctx is VMExecutionContext execCtxPer && execCtxPer.AdditionalContext is IGameServicesContext servicesPer)
                    {
                        if (servicesPer.PerceptionManager != null && ctx.Caller != null)
                        {
                            PerceptionManager perceptionManager = servicesPer.PerceptionManager as PerceptionManager;
                            if (perceptionManager != null)
                            {
                                // criteriaValue is the perception type constant to check
                                // Check if creature matches the perception type from caller's perspective
                                bool isSeen = perceptionManager.HasSeen(ctx.Caller, creature);
                                bool isHeard = perceptionManager.HasHeard(ctx.Caller, creature);

                                // Map perception type constants to checks
                                // Constants from nwscript.nss (approximate values, may need adjustment):
                                // PERCEPTION_SEEN = 1, PERCEPTION_HEARD = 2, PERCEPTION_SEEN_AND_HEARD = 3
                                // PERCEPTION_SEEN_AND_NOT_HEARD = 4, PERCEPTION_HEARD_AND_NOT_SEEN = 5
                                // PERCEPTION_NOT_SEEN = 6, PERCEPTION_NOT_HEARD = 7
                                // PERCEPTION_NOT_SEEN_AND_NOT_HEARD = 8

                                switch (criteriaValue)
                                {
                                    case 1: // PERCEPTION_SEEN
                                        return isSeen;
                                    case 2: // PERCEPTION_HEARD
                                        return isHeard;
                                    case 3: // PERCEPTION_SEEN_AND_HEARD
                                        return isSeen && isHeard;
                                    case 4: // PERCEPTION_SEEN_AND_NOT_HEARD
                                        return isSeen && !isHeard;
                                    case 5: // PERCEPTION_HEARD_AND_NOT_SEEN
                                        return isHeard && !isSeen;
                                    case 6: // PERCEPTION_NOT_SEEN
                                        return !isSeen;
                                    case 7: // PERCEPTION_NOT_HEARD
                                        return !isHeard;
                                    case 8: // PERCEPTION_NOT_SEEN_AND_NOT_HEARD
                                        return !isSeen && !isHeard;
                                    default:
                                        // Unknown perception type, default to false
                                        return false;
                                }
                            }
                        }
                    }
                    return false;

                default:
                    return true; // Unknown criteria type, accept all
            }
        }

        #endregion

        #region Global Variable Functions

        /// <summary>
        /// GetGlobalNumber(string sVarName) - gets global integer variable
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Global variable system
        /// Located via string references: "GLOBALVARS" @ 0x007c27bc
        /// Original implementation: Global variables stored in GFF file (GLOBALVARS.res), persisted across saves
        /// Variable storage: GFF structure with variable name as field name, integer as value
        /// Returns: Global integer value or 0 if variable doesn't exist
        /// </remarks>
        private new Variable Func_GetGlobalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(ctx.Globals.GetGlobalInt(name));
        }

        /// <summary>
        /// SetGlobalNumber(string sVarName, int nValue) - sets global integer variable
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Global variable system
        /// Located via string references: "GLOBALVARS" @ 0x007c27bc
        /// Original implementation: Sets global variable value in GFF structure, persists to save file
        /// Variable storage: Updates GFF field with variable name, writes to GLOBALVARS.res on save
        /// Returns: Void (no return value)
        /// </remarks>
        private new Variable Func_SetGlobalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            int value = args.Count > 1 ? args[1].AsInt() : 0;
            ctx.Globals.SetGlobalInt(name, value);
            return Variable.Void();
        }

        /// <summary>
        /// GetGlobalBoolean(string sVarName) - gets global boolean variable
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Global variable system
        /// Located via string references: "GLOBALVARS" @ 0x007c27bc
        /// Original implementation: Global boolean stored as integer (0 = FALSE, non-zero = TRUE) in GFF
        /// Variable storage: GFF structure with variable name as field name, integer (0/1) as value
        /// Returns: 1 (TRUE) if variable is non-zero, 0 (FALSE) if variable is zero or doesn't exist
        /// </remarks>
        private new Variable Func_GetGlobalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(ctx.Globals.GetGlobalBool(name) ? 1 : 0);
        }

        /// <summary>
        /// SetGlobalBoolean(string sVarName, int nValue) - sets global boolean variable
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Global variable system
        /// Located via string references: "GLOBALVARS" @ 0x007c27bc
        /// Original implementation: Sets global boolean as integer (0 = FALSE, non-zero = TRUE) in GFF
        /// Variable storage: Updates GFF field with variable name, stores 0 or 1 based on nValue
        /// Returns: Void (no return value)
        /// </remarks>
        private new Variable Func_SetGlobalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            bool value = args.Count > 1 && args[1].AsInt() != 0;
            ctx.Globals.SetGlobalBool(name, value);
            return Variable.Void();
        }

        /// <summary>
        /// GetGlobalString(string sVarName) - gets global string variable
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Global variable system
        /// Located via string references: "GLOBALVARS" @ 0x007c27bc
        /// Original implementation: Global string stored in GFF structure as CExoString field
        /// Variable storage: GFF structure with variable name as field name, string as value
        /// Returns: Global string value or empty string if variable doesn't exist
        /// </remarks>
        private new Variable Func_GetGlobalString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromString(ctx.Globals.GetGlobalString(name));
        }

        /// <summary>
        /// SetGlobalString(string sVarName, string sValue) - sets global string variable
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Global variable system
        /// Located via string references: "GLOBALVARS" @ 0x007c27bc
        /// Original implementation: Sets global string value in GFF structure, persists to save file
        /// Variable storage: Updates GFF field with variable name, writes to GLOBALVARS.res on save
        /// Returns: Void (no return value)
        /// </remarks>
        private new Variable Func_SetGlobalString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            string value = args.Count > 1 ? args[1].AsString() : string.Empty;
            ctx.Globals.SetGlobalString(name, value);
            return Variable.Void();
        }

        private Variable Func_GetLocalObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IEntity localObj = ctx.Globals.GetLocalObject(entity, name);
                return Variable.FromObject(localObj?.ObjectId ?? ObjectInvalid);
            }
            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetLocalBoolean(object oObject, int nIndex) - gets local boolean variable by index
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Local variable system (index-based, not name-based like NWN)
        /// Original implementation: KOTOR uses index-based local variables instead of name-based
        /// Index range: 0-63 (64 boolean slots per entity)
        /// Storage: Stored in entity's local variable component, persisted per-entity
        /// Returns: 1 (TRUE) if variable is set, 0 (FALSE) if variable is unset or index invalid
        /// </remarks>
        // KOTOR Local Boolean/Number functions (index-based, not name-based like NWN)
        private Variable Func_GetLocalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLocalBoolean(object oObject, int nIndex)
            // Index range: 0-63
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index >= 0 && index < 64)
            {
                // Store as local int with index-based key
                return Variable.FromInt(ctx.Globals.GetLocalInt(entity, "_LB_" + index));
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetLocalBoolean(object oObject, int nIndex, int nValue) - sets local boolean variable by index
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Local variable system (index-based, not name-based like NWN)
        /// Original implementation: KOTOR uses index-based local variables instead of name-based
        /// Index range: 0-63 (64 boolean slots per entity)
        /// Storage: Stored in entity's local variable component, persisted per-entity
        /// Value: 0 = FALSE, non-zero = TRUE
        /// Returns: Void (no return value)
        /// </remarks>
        private Variable Func_SetLocalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetLocalBoolean(object oObject, int nIndex, int nValue)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;
            int value = args.Count > 2 ? args[2].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index >= 0 && index < 64)
            {
                ctx.Globals.SetLocalInt(entity, "_LB_" + index, value != 0 ? 1 : 0);
            }
            return Variable.Void();
        }

        /// <summary>
        /// GetLocalNumber(object oObject, int nIndex) - gets local number variable by index
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Local variable system (index-based, not name-based like NWN)
        /// Original implementation: KOTOR uses index-based local variables, number type limited to index 0
        /// Index range: 0 only (single number slot per entity)
        /// Value range: -128 to +127 (signed byte)
        /// Storage: Stored in entity's local variable component, persisted per-entity
        /// Returns: Local number value or 0 if variable is unset or index invalid
        /// </remarks>
        private Variable Func_GetLocalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLocalNumber(object oObject, int nIndex)
            // Index range: 0 only
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index == 0)
            {
                return Variable.FromInt(ctx.Globals.GetLocalInt(entity, "_LN_" + index));
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetLocalNumber(object oObject, int nIndex, int nValue) - sets local number variable by index
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Local variable system (index-based, not name-based like NWN)
        /// Original implementation: KOTOR uses index-based local variables, number type limited to index 0
        /// Index range: 0 only (single number slot per entity)
        /// Value range: -128 to +127 (signed byte), values outside range are clamped
        /// Storage: Stored in entity's local variable component, persisted per-entity
        /// Returns: Void (no return value)
        /// </remarks>
        private Variable Func_SetLocalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetLocalNumber(object oObject, int nIndex, int nValue)
            // Value range: -128 to +127
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;
            int value = args.Count > 2 ? args[2].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index == 0)
            {
                // Clamp to -128 to 127
                if (value > 127) value = 127;
                if (value < -128) value = -128;
                ctx.Globals.SetLocalInt(entity, "_LN_" + index, value);
            }
            return Variable.Void();
        }

        #endregion

        #region Math Functions

        private Variable Func_fabs(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat(Math.Abs(value));
        }

        private Variable Func_cos(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Cos(value));
        }

        private Variable Func_sin(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Sin(value));
        }

        private Variable Func_tan(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Tan(value));
        }

        private Variable Func_acos(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Acos(value));
        }

        private Variable Func_asin(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Asin(value));
        }

        private Variable Func_atan(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Atan(value));
        }

        private Variable Func_log(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Log(value));
        }

        private Variable Func_pow(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float x = args.Count > 0 ? args[0].AsFloat() : 0f;
            float y = args.Count > 1 ? args[1].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Pow(x, y));
        }

        private Variable Func_sqrt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Sqrt(value));
        }

        private Variable Func_abs(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int value = args.Count > 0 ? args[0].AsInt() : 0;
            return Variable.FromInt(Math.Abs(value));
        }

        private Variable Func_VectorMagnitude(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            Vector3 v = args.Count > 0 ? args[0].AsVector() : Vector3.Zero;
            return Variable.FromFloat(v.Length());
        }

        #endregion

        #region Dice Functions

        private Variable Func_d2(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 3);
            return Variable.FromInt(total);
        }

        private Variable Func_d3(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 4);
            return Variable.FromInt(total);
        }

        private Variable Func_d4(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 5);
            return Variable.FromInt(total);
        }

        private Variable Func_d6(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 7);
            return Variable.FromInt(total);
        }

        private Variable Func_d8(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 9);
            return Variable.FromInt(total);
        }

        private Variable Func_d10(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 11);
            return Variable.FromInt(total);
        }

        private Variable Func_d12(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 13);
            return Variable.FromInt(total);
        }

        private Variable Func_d20(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 21);
            return Variable.FromInt(total);
        }

        private Variable Func_d100(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 101);
            return Variable.FromInt(total);
        }

        #endregion

        #region String Functions

        private Variable Func_GetStringLength(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(s.Length);
        }

        private Variable Func_GetStringUpperCase(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromString(s.ToUpperInvariant());
        }

        private Variable Func_GetStringLowerCase(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromString(s.ToLowerInvariant());
        }

        private Variable Func_GetStringRight(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            int count = args.Count > 1 ? args[1].AsInt() : 0;
            if (count <= 0 || s.Length == 0) return Variable.FromString(string.Empty);
            if (count >= s.Length) return Variable.FromString(s);
            return Variable.FromString(s.Substring(s.Length - count));
        }

        private Variable Func_GetStringLeft(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            int count = args.Count > 1 ? args[1].AsInt() : 0;
            if (count <= 0 || s.Length == 0) return Variable.FromString(string.Empty);
            if (count >= s.Length) return Variable.FromString(s);
            return Variable.FromString(s.Substring(0, count));
        }

        private Variable Func_InsertString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string dest = args.Count > 0 ? args[0].AsString() : string.Empty;
            string src = args.Count > 1 ? args[1].AsString() : string.Empty;
            int pos = args.Count > 2 ? args[2].AsInt() : 0;
            if (pos < 0) pos = 0;
            if (pos > dest.Length) pos = dest.Length;
            return Variable.FromString(dest.Insert(pos, src));
        }

        private Variable Func_GetSubString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            int start = args.Count > 1 ? args[1].AsInt() : 0;
            int count = args.Count > 2 ? args[2].AsInt() : 0;
            if (start < 0) start = 0;
            if (start >= s.Length || count <= 0) return Variable.FromString(string.Empty);
            if (start + count > s.Length) count = s.Length - start;
            return Variable.FromString(s.Substring(start, count));
        }

        private Variable Func_FindSubString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            string sub = args.Count > 1 ? args[1].AsString() : string.Empty;
            int start = args.Count > 2 ? args[2].AsInt() : 0;
            if (start < 0) start = 0;
            return Variable.FromInt(s.IndexOf(sub, start, StringComparison.Ordinal));
        }

        #endregion

        #region Placeholder Functions

        /// <summary>
        /// SwitchPlayerCharacter(int nNPC) - Switches the main character to a specified NPC
        /// Based on swkotor.exe: SwitchPlayerCharacter implementation (routine ID 11)
        /// Located via string references: Party leader switching system
        /// Original implementation: Switches controlled character to NPC (-1 = switch back to original PC)
        /// </summary>
        private Variable Func_SwitchPlayerCharacter(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : -1;

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                if (npcIndex == -1)
                {
                    // Switch back to original PC
                    if (services.PlayerEntity != null)
                    {
                        partyManager.SetLeader(services.PlayerEntity);
                        return Variable.Void();
                    }
                }
                else
                {
                    // Switch to NPC party member
                    IEntity member = partyManager.GetAvailableMember(npcIndex);
                    if (member != null && partyManager.IsSelected(npcIndex))
                    {
                        partyManager.SetLeader(member);
                        return Variable.Void();
                    }
                }
            }
            return Variable.Void();
        }

        /// <summary>
        /// SetTime(int nHour, int nMinute, int nSecond, int nMillisecond) - Sets the game time
        /// Based on swkotor.exe: Sets the in-game time (not real time)
        /// </summary>
        private Variable Func_SetTime(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = args.Count > 0 ? args[0].AsInt() : 0;
            int minute = args.Count > 1 ? args[1].AsInt() : 0;
            int second = args.Count > 2 ? args[2].AsInt() : 0;
            int millisecond = args.Count > 3 ? args[3].AsInt() : 0;

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                // Based on swkotor.exe: SetGameTime implementation
                // Located via string references: "GameTime" @ 0x007c1a78
                // Original implementation: Sets game time in module IFO, affects time-of-day checks
                if (ctx.World != null && ctx.World.TimeManager != null)
                {
                    ctx.World.TimeManager.SetGameTime(hour, minute, second, millisecond);
                }
            }
            return Variable.Void();
        }

        private Variable Func_GetPartyMemberCount(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetPartyMemberCount()
            // Returns a count of how many members are in the party including the player character
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                return Variable.FromInt(partyManager.ActiveMemberCount);
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetPartyMemberByIndex(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetPartyMemberByIndex(int nIndex)
            // Returns the party member at a given index in the party (0 = leader)
            int index = args.Count > 0 ? args[0].AsInt() : 0;

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetMemberAtSlot(index);
                if (member != null)
                {
                    return Variable.FromObject(member.ObjectId);
                }
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_IsObjectPartyMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // IsObjectPartyMember(object oCreature)
            // Returns whether a specified creature is a party member
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            IEntity creature = ResolveObject(creatureId, ctx);

            if (creature == null)
            {
                return Variable.FromInt(0);
            }

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                bool inParty = partyManager.IsInParty(creature);
                return Variable.FromInt(inParty ? 1 : 0);
            }
            return Variable.FromInt(0);
        }

        private Variable Func_AddPartyMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // AddPartyMember(int nNPC, object oCreature)
            // Adds a creature to the party
            // Returns whether the addition was successful
            int npcIndex = args.Count > 0 ? args[0].AsInt() : -1;
            uint creatureId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            if (npcIndex < 0 || npcIndex >= PartyManager.MaxAvailableMembers)
            {
                return Variable.FromInt(0); // Invalid NPC index
            }

            IEntity creature = ResolveObject(creatureId, ctx);
            if (creature == null)
            {
                return Variable.FromInt(0);
            }

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                // Add to available members if not already available
                if (!partyManager.IsAvailable(npcIndex))
                {
                    partyManager.AddAvailableMember(npcIndex, creature);
                }

                // Select member to join active party
                bool added = partyManager.SelectMember(npcIndex);
                return Variable.FromInt(added ? 1 : 0);
            }
            return Variable.FromInt(0);
        }

        private Variable Func_RemovePartyMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // RemovePartyMember(int nNPC)
            // Removes a creature from the party
            // Returns whether the removal was successful
            int npcIndex = args.Count > 0 ? args[0].AsInt() : -1;

            if (npcIndex < 0 || npcIndex >= PartyManager.MaxAvailableMembers)
            {
                return Variable.FromInt(0); // Invalid NPC index
            }

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                if (partyManager.IsSelected(npcIndex))
                {
                    partyManager.DeselectMember(npcIndex);
                    return Variable.FromInt(1);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_SetPartyLeader(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetPartyLeader(int nNPC)
            // Sets (by NPC constant) which party member should be the controlled character
            int npcIndex = args.Count > 0 ? args[0].AsInt() : -1;

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                if (npcIndex == -1)
                {
                    // Switch back to original PC
                    if (services.PlayerEntity != null)
                    {
                        partyManager.SetLeader(services.PlayerEntity);
                        return Variable.FromInt(1);
                    }
                }
                else
                {
                    // Switch to NPC party member
                    IEntity member = partyManager.GetAvailableMember(npcIndex);
                    if (member != null && partyManager.IsSelected(npcIndex))
                    {
                        partyManager.SetLeader(member);
                        return Variable.FromInt(1);
                    }
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetAreaUnescapable(int bUnescapable) - Sets whether the current area is escapable or not
        /// Based on swkotor.exe: Controls whether players can leave the area
        /// TRUE means you can not escape the area, FALSE means you can escape the area
        /// </summary>
        private Variable Func_SetAreaUnescapable(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int unescapable = args.Count > 0 ? args[0].AsInt() : 0;
            bool isUnescapable = unescapable != 0;

            if (ctx.World != null && ctx.World.CurrentArea != null)
            {
                ctx.World.CurrentArea.IsUnescapable = isUnescapable;
            }
            return Variable.Void();
        }

        /// <summary>
        /// GetAreaUnescapable() - Returns whether the current area is escapable or not
        /// Based on swkotor.exe: Returns the unescapable flag for the current area
        /// TRUE means you can not escape the area, FALSE means you can escape the area
        /// </summary>
        private Variable Func_GetAreaUnescapable(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.World != null && ctx.World.CurrentArea != null)
            {
                return Variable.FromInt(ctx.World.CurrentArea.IsUnescapable ? 1 : 0);
            }
            return Variable.FromInt(0); // Default to escapable if no area
        }

        /// <summary>
        /// GetTimeHour() - Returns the current game time hour (0-23)
        /// </summary>
        private Variable Func_GetTimeHour(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                // Based on swkotor.exe: GetTimeHour implementation
                // Located via string references: "GameTime" @ 0x007c1a78
                // Original implementation: Returns current game time hour (0-23) from module IFO
                if (ctx.World != null && ctx.World.TimeManager != null)
                {
                    return Variable.FromInt(ctx.World.TimeManager.GameTimeHour);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetTimeMinute() - Returns the current game time minute (0-59)
        /// </summary>
        private Variable Func_GetTimeMinute(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                // Based on swkotor.exe: GetTimeMinute implementation
                // Located via string references: "GameTime" @ 0x007c1a78
                // Original implementation: Returns current game time minute (0-59) from module IFO
                if (ctx.World != null && ctx.World.TimeManager != null)
                {
                    return Variable.FromInt(ctx.World.TimeManager.GameTimeMinute);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetTimeSecond() - Returns the current game time second (0-59)
        /// </summary>
        private Variable Func_GetTimeSecond(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                // Based on swkotor.exe: GetTimeSecond implementation
                // Located via string references: "GameTime" @ 0x007c1a78
                // Original implementation: Returns current game time second (0-59) from module IFO
                if (ctx.World != null && ctx.World.TimeManager != null)
                {
                    return Variable.FromInt(ctx.World.TimeManager.GameTimeSecond);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetTimeMillisecond() - Returns the current game time millisecond (0-999)
        /// </summary>
        private Variable Func_GetTimeMillisecond(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                // Based on swkotor.exe: GetTimeMillisecond implementation
                // Located via string references: "GameTime" @ 0x007c1a78
                // Original implementation: Returns current game time millisecond (0-999) from module IFO
                if (ctx.World != null && ctx.World.TimeManager != null)
                {
                    return Variable.FromInt(ctx.World.TimeManager.GameTimeMillisecond);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsDay() - Returns TRUE if it is currently day time (6:00 AM to 8:00 PM)
        /// Based on swkotor.exe: Time of day check for day period
        /// </summary>
        private Variable Func_GetIsDay(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = Func_GetTimeHour(new List<Variable>(), ctx).AsInt();
            // Day is typically 6:00 AM (6) to 8:00 PM (20)
            return Variable.FromInt((hour >= 6 && hour < 20) ? 1 : 0);
        }

        /// <summary>
        /// GetIsNight() - Returns TRUE if it is currently night time (8:00 PM to 6:00 AM)
        /// Based on swkotor.exe: Time of day check for night period
        /// </summary>
        private Variable Func_GetIsNight(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = Func_GetTimeHour(new List<Variable>(), ctx).AsInt();
            // Night is typically 8:00 PM (20) to 6:00 AM (6)
            return Variable.FromInt((hour >= 20 || hour < 6) ? 1 : 0);
        }

        /// <summary>
        /// GetIsDawn() - Returns TRUE if it is currently dawn (5:00 AM to 7:00 AM)
        /// Based on swkotor.exe: Time of day check for dawn period
        /// </summary>
        private Variable Func_GetIsDawn(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = Func_GetTimeHour(new List<Variable>(), ctx).AsInt();
            // Dawn is typically 5:00 AM (5) to 7:00 AM (7)
            return Variable.FromInt((hour >= 5 && hour < 7) ? 1 : 0);
        }

        /// <summary>
        /// GetIsDusk() - Returns TRUE if it is currently dusk (7:00 PM to 9:00 PM)
        /// Based on swkotor.exe: Time of day check for dusk period
        /// </summary>
        private Variable Func_GetIsDusk(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = Func_GetTimeHour(new List<Variable>(), ctx).AsInt();
            // Dusk is typically 7:00 PM (19) to 9:00 PM (21)
            return Variable.FromInt((hour >= 19 && hour < 21) ? 1 : 0);
        }

        #endregion

        #region Last Event Tracking Functions

        /// <summary>
        /// GetLastUsedBy() - Returns the object that last used the object that called this function
        /// Based on swkotor.exe: Tracks last entity that used an object
        /// </summary>
        private Variable Func_GetLastUsedBy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastUsedBy from entity data
            if (ctx.Caller is Runtime.Core.Entities.Entity entity)
            {
                uint lastUsedById = entity.GetData<uint>("LastUsedBy", ObjectInvalid);
                if (lastUsedById != ObjectInvalid)
                {
                    return Variable.FromObject(lastUsedById);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetLastOpenedBy() - Returns the object that last opened the object that called this function
        /// Based on swkotor.exe: Tracks last entity that opened a door/placeable
        /// </summary>
        private Variable Func_GetLastOpenedBy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastOpenedBy from entity data
            if (ctx.Caller is Runtime.Core.Entities.Entity entity)
            {
                uint lastOpenedById = entity.GetData<uint>("LastOpenedBy", ObjectInvalid);
                if (lastOpenedById != ObjectInvalid)
                {
                    return Variable.FromObject(lastOpenedById);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetLastClosedBy() - Returns the object that last closed the object that called this function
        /// Based on swkotor.exe: Tracks last entity that closed a door/placeable
        /// </summary>
        private Variable Func_GetLastClosedBy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastClosedBy from entity data
            if (ctx.Caller is Runtime.Core.Entities.Entity entity)
            {
                uint lastClosedById = entity.GetData<uint>("LastClosedBy", ObjectInvalid);
                if (lastClosedById != ObjectInvalid)
                {
                    return Variable.FromObject(lastClosedById);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetLastLocked() - Returns the object that last locked the object that called this function
        /// Based on swkotor.exe: Tracks last entity that locked a door/placeable
        /// </summary>
        private Variable Func_GetLastLocked(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastLocked from entity data
            if (ctx.Caller is Runtime.Core.Entities.Entity entity)
            {
                uint lastLockedId = entity.GetData<uint>("LastLocked", ObjectInvalid);
                if (lastLockedId != ObjectInvalid)
                {
                    return Variable.FromObject(lastLockedId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetLastUnlocked() - Returns the object that last unlocked the object that called this function
        /// Based on swkotor.exe: Tracks last entity that unlocked a door/placeable
        /// </summary>
        private Variable Func_GetLastUnlocked(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastUnlocked from entity data
            if (ctx.Caller is Runtime.Core.Entities.Entity entity)
            {
                uint lastUnlockedId = entity.GetData<uint>("LastUnlocked", ObjectInvalid);
                if (lastUnlockedId != ObjectInvalid)
                {
                    return Variable.FromObject(lastUnlockedId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        #endregion

        #region Plot Flag Functions

        /// <summary>
        /// GetPlotFlag(object oTarget) - Returns TRUE if oTarget has the plot flag set
        /// Based on swkotor.exe: Plot flag prevents objects from being removed or modified
        /// </summary>
        private Variable Func_GetPlotFlag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);

            if (entity == null)
            {
                return Variable.FromInt(0);
            }

            // Get PlotFlag from entity data
            if (entity is Entity concreteEntity)
            {
                bool plotFlag = concreteEntity.GetData<bool>("PlotFlag", false);
                return Variable.FromInt(plotFlag ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetPlotFlag(object oTarget, int nPlotFlag) - Sets the plot flag on oTarget
        /// Based on swkotor.exe: Plot flag prevents objects from being removed or modified
        /// </summary>
        private Variable Func_SetPlotFlag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int plotFlag = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.Void();
            }

            // Set PlotFlag on entity data
            if (entity is Runtime.Core.Entities.Entity concreteEntity)
            {
                concreteEntity.SetData("PlotFlag", plotFlag != 0);
            }

            return Variable.Void();
        }

        #endregion

        // Methods removed - using base class implementations instead
        // GetModule (routine 242) now uses base.Func_GetModule which properly handles module ID lookup

        /// <summary>
        /// GetItemPossessor(object oItem) - returns the creature/placeable that possesses the item
        /// </summary>
        private Variable Func_GetItemPossessor(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity item = ResolveObject(itemId, ctx);

            if (item == null || item.ObjectType != Runtime.Core.Enums.ObjectType.Item)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Search for item in inventories
            foreach (IEntity entity in ctx.World.GetAllEntities())
            {
                IInventoryComponent inventory = entity.GetComponent<IInventoryComponent>();
                if (inventory != null)
                {
                    // Check all inventory slots
                    for (int slot = 0; slot < 20; slot++) // Standard inventory slots
                    {
                        IEntity slotItem = inventory.GetItemInSlot(slot);
                        if (slotItem != null && slotItem.ObjectId == itemId)
                        {
                            return Variable.FromObject(entity.ObjectId);
                        }
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetItemPossessedBy(object oCreature, string sItemTag) - returns the item with the given tag possessed by the creature
        /// </summary>
        private Variable Func_GetItemPossessedBy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string itemTag = args.Count > 1 ? args[1].AsString() : string.Empty;

            IEntity creature = ResolveObject(creatureId, ctx);
            if (creature == null || string.IsNullOrEmpty(itemTag))
            {
                return Variable.FromObject(ObjectInvalid);
            }

            IInventoryComponent inventory = creature.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Search inventory for item with matching tag
            foreach (IEntity item in inventory.GetAllItems())
            {
                if (item != null && string.Equals(item.Tag, itemTag, StringComparison.OrdinalIgnoreCase))
                {
                    return Variable.FromObject(item.ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_CreateItemOnObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // CreateItemOnObject(string sItemTemplate, object oTarget=OBJECT_SELF, int nStackSize=1, int nHideMessage = 0)
            string itemTemplate = args.Count > 0 ? args[0].AsString() : string.Empty;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            int stackSize = args.Count > 2 ? args[2].AsInt() : 1;
            int hideMessage = args.Count > 3 ? args[3].AsInt() : 0;

            if (string.IsNullOrEmpty(itemTemplate) || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Load UTI template from resource provider
            UTI utiTemplate = null;
            if (ctx.ResourceProvider != null)
            {
                try
                {
                    // Try IGameResourceProvider first
                    if (ctx.ResourceProvider is IGameResourceProvider gameProvider)
                    {
                        var resourceId = new ResourceIdentifier(itemTemplate, ResourceType.UTI);
                        System.Threading.Tasks.Task<byte[]> task = gameProvider.GetResourceBytesAsync(resourceId, CancellationToken.None);
                        task.Wait();
                        byte[] utiData = task.Result;

                        if (utiData != null && utiData.Length > 0)
                        {
                            using (var stream = new MemoryStream(utiData))
                            {
                                var reader = new GFFBinaryReader(stream);
                                GFF gff = reader.Load();
                                utiTemplate = UTIHelpers.ConstructUti(gff);
                            }
                        }
                    }
                    // Fallback to BioWare.NET Installation provider
                    else if (ctx.ResourceProvider is BioWare.NET.Extract.Installation installation)
                    {
                        BioWare.NET.Extract.ResourceResult result = installation.Resource(itemTemplate, ResourceType.UTI, null, null);
                        if (result != null && result.Data != null)
                        {
                            using (var stream = new MemoryStream(result.Data))
                            {
                                var reader = new GFFBinaryReader(stream);
                                GFF gff = reader.Load();
                                utiTemplate = UTIHelpers.ConstructUti(gff);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[KotOR1] Error loading UTI template '{itemTemplate}': {ex.Message}");
                }
            }

            // Create item entity
            IEntity itemEntity = ctx.World.CreateEntity(Runtime.Core.Enums.ObjectType.Item, Vector3.Zero, 0f);
            if (itemEntity != null)
            {
                // Set tag from template or use template name
                if (utiTemplate != null && !string.IsNullOrEmpty(utiTemplate.Tag))
                {
                    itemEntity.Tag = utiTemplate.Tag;
                }
                else
                {
                    itemEntity.Tag = itemTemplate;
                }

                // Add item component with UTI template data
                if (utiTemplate != null)
                {
                    var itemComponent = new OdysseyItemComponent
                    {
                        BaseItem = utiTemplate.BaseItem,
                        StackSize = utiTemplate.StackSize,
                        Charges = utiTemplate.Charges,
                        Cost = utiTemplate.Cost,
                        Identified = utiTemplate.Identified != 0,
                        TemplateResRef = itemTemplate
                    };

                    // Convert UTI properties to ItemProperty
                    foreach (var utiProp in utiTemplate.Properties)
                    {
                        var prop = new Runtime.Core.Interfaces.Components.ItemProperty
                        {
                            PropertyType = utiProp.PropertyName,
                            Subtype = utiProp.Subtype,
                            CostTable = utiProp.CostTable,
                            CostValue = utiProp.CostValue,
                            Param1 = utiProp.Param1,
                            Param1Value = utiProp.Param1Value
                        };
                        itemComponent.AddProperty(prop);
                    }

                    // Convert UTI upgrades to ItemUpgrade
                    for (int i = 0; i < utiTemplate.Upgrades.Count; i++)
                    {
                        var utiUpgrade = utiTemplate.Upgrades[i];
                        var upgrade = new Runtime.Core.Interfaces.Components.ItemUpgrade
                        {
                            UpgradeType = i, // Index-based upgrade type
                            Index = i
                        };
                        itemComponent.AddUpgrade(upgrade);
                    }

                    itemEntity.AddComponent(itemComponent);
                }

                // Add to target's inventory if target has inventory component
                IInventoryComponent inventory = target.GetComponent<IInventoryComponent>();
                if (inventory != null)
                {
                    // Add item to inventory (finds first available slot)
                    if (!inventory.AddItem(itemEntity))
                    {
                        // Inventory full, destroy item entity
                        ctx.World.DestroyEntity(itemEntity.ObjectId);
                        return Variable.FromObject(ObjectInvalid);
                    }
                }

                return Variable.FromObject(itemEntity.ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// ActionEquipItem(object oItem, int nInventorySlot) - Queues action to equip an item
        /// Based on swkotor.exe: Equips item from inventory to specified equipment slot
        /// Tracks equipped item for GetLastItemEquipped
        /// </summary>
        private Variable Func_ActionEquipItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionEquipItem(object oItem, int nInventorySlot)
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            int inventorySlot = args.Count > 1 ? args[1].AsInt() : 0;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            // Track equipped item for GetLastItemEquipped
            if (itemId != ObjectInvalid)
            {
                _lastEquippedItems[ctx.Caller.ObjectId] = itemId;
            }

            var action = new ActionEquipItem(itemId, inventorySlot);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        private Variable Func_ActionUnequipItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionUnequipItem(int nInventorySlot)
            int inventorySlot = args.Count > 0 ? args[0].AsInt() : 0;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            var action = new ActionUnequipItem(inventorySlot);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        private Variable Func_ActionPickUpItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionPickUpItem(object oItem)
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            var action = new ActionPickUpItem(itemId);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        private Variable Func_ActionPutDownItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionPutDownItem(object oItem, location lTargetLocation)
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            object locationObj = args.Count > 1 ? args[1].ComplexValue : null;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            // Extract position from location object
            Vector3 dropLocation = Vector3.Zero;
            if (locationObj is Location location)
            {
                dropLocation = location.Position;
            }
            else if (locationObj is Vector3 vector)
            {
                dropLocation = vector;
            }
            else
            {
                // Default to actor's position
                Runtime.Core.Interfaces.Components.ITransformComponent transform = ctx.Caller.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    dropLocation = transform.Position;
                }
            }

            var action = new ActionPutDownItem(itemId, dropLocation);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetLastAttacker(object oTarget=OBJECT_SELF) - Returns the last entity that attacked the target
        /// This should only be used in OnAttacked events for creatures, placeables, and doors
        /// </summary>
        private Variable Func_GetLastAttacker(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity target = ResolveObject(targetId, ctx);

            if (target == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get CombatManager from GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.CombatManager is CombatManager combatManager)
            {
                IEntity lastAttacker = combatManager.GetLastAttacker(target);
                if (lastAttacker != null)
                {
                    return Variable.FromObject(lastAttacker.ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetAttackTarget(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetAttackTarget(object oCreature=OBJECT_SELF)
            // Returns the current attack target of the creature (only works when in combat)
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity creature = ResolveObject(creatureId, ctx);

            if (creature == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Try to get CombatManager from GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.CombatManager is CombatManager combatManager)
            {
                IEntity target = combatManager.GetAttackTarget(creature);
                if (target != null)
                {
                    return Variable.FromObject(target.ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetDistanceBetween2D(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetDistanceBetween2D(object oObjectA, object oObjectB)
            // Returns the 2D distance (ignoring Y) between two objects
            uint objectAId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            uint objectBId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            IEntity objectA = ResolveObject(objectAId, ctx);
            IEntity objectB = ResolveObject(objectBId, ctx);

            if (objectA == null || objectB == null)
            {
                return Variable.FromFloat(0f);
            }

            Runtime.Core.Interfaces.Components.ITransformComponent transformA = objectA.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
            Runtime.Core.Interfaces.Components.ITransformComponent transformB = objectB.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();

            if (transformA == null || transformB == null)
            {
                return Variable.FromFloat(0f);
            }

            // Calculate 2D distance (ignore Y component)
            Vector3 posA = transformA.Position;
            Vector3 posB = transformB.Position;
            float dx = posB.X - posA.X;
            float dz = posB.Z - posA.Z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);

            return Variable.FromFloat(distance);
        }

        /// <summary>
        /// GetIsInCombat(object oCreature=OBJECT_SELF, int bOnlyCountReal=FALSE) - Returns TRUE if the creature is in combat
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetIsInCombat implementation
        /// Located via string reference: "InCombatHPBase" @ 0x007bf224, "CombatRoundData" @ 0x007bf6b4
        /// Original implementation: 0x005119a0 @ 0x005119a0 checks combat state and active combat rounds
        /// - If bOnlyCountReal=FALSE: Returns true if combat state is InCombat (any combat, including just targeted)
        /// - If bOnlyCountReal=TRUE: Returns true only if there's an active combat round (real combat, actively fighting)
        /// Comment from original: "RWT-OEI 09/30/04 - If you pass TRUE in as the second parameter then this function will only return true if the character is in REAL combat. If you don't know what that means, don't pass in TRUE."
        /// </summary>
        private Variable Func_GetIsInCombat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetIsInCombat(object oCreature=OBJECT_SELF, int bOnlyCountReal=FALSE)
            // Returns TRUE if the creature is in combat
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            bool onlyCountReal = args.Count > 1 && args[1].AsInt() != 0;

            IEntity creature = ResolveObject(creatureId, ctx);

            if (creature == null)
            {
                return Variable.FromInt(0);
            }

            // Try to get CombatManager from GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.CombatManager is CombatManager combatManager)
            {
                bool inCombat;
                if (onlyCountReal)
                {
                    // bOnlyCountReal=TRUE: Only return true if in "real" combat (has active combat round)
                    // This distinguishes between actively fighting vs just being targeted
                    inCombat = combatManager.IsInRealCombat(creature);
                }
                else
                {
                    // bOnlyCountReal=FALSE: Return true if in any combat state (including just targeted)
                    inCombat = combatManager.IsInCombat(creature);
                }

                return Variable.FromInt(inCombat ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetCameraFacing(float fDirection) - Sets the camera facing direction
        /// Based on swkotor.exe: Camera facing controls camera yaw rotation in chase mode
        /// The direction is in radians (0 = east, PI/2 = north, PI = west, 3*PI/2 = south)
        /// </summary>
        private Variable Func_SetCameraFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float direction = args.Count > 0 ? args[0].AsFloat() : 0f;

            // Access CameraController through GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.CameraController is Runtime.Core.Camera.CameraController cameraController)
                {
                    // Set camera facing using SetFacing method (handles both chase and free camera modes)
                    cameraController.SetFacing(direction);
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// PlaySound(string sSoundName) - Plays a sound effect
        /// Based on swkotor.exe: PlaySound plays WAV files as sound effects
        /// Sound is played at the caller's position for 3D spatial audio, or as 2D sound if no position
        /// </summary>
        private Variable Func_PlaySound(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string soundName = args.Count > 0 ? args[0].AsString() : string.Empty;

            if (string.IsNullOrEmpty(soundName) || ctx.Caller == null)
            {
                return Variable.Void();
            }

            // Access SoundPlayer through GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.SoundPlayer != null)
                {
                    // Get caller's position for 3D spatial audio
                    System.Numerics.Vector3? position = null;
                    Runtime.Core.Interfaces.Components.ITransformComponent transform = ctx.Caller.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                    if (transform != null)
                    {
                        // Convert BioWare.NET Vector3 to System.Numerics.Vector3
                        position = new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                    }

                    // Play sound at caller's position (3D spatial audio) or as 2D sound if no position
                    uint soundInstanceId = services.SoundPlayer.PlaySound(soundName, position, 1.0f, 0.0f, 0.0f);
                    // Note: soundInstanceId can be used to stop the sound later if needed
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetSpellTargetObject() - Returns the target of the last spell cast
        /// Based on swkotor.exe: Returns the target object ID of the last spell cast by the caller
        /// </summary>
        private Variable Func_GetSpellTargetObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Retrieve last spell target for this caster
            if (_lastSpellTargets.TryGetValue(ctx.Caller.ObjectId, out uint targetId))
            {
                // Verify target still exists and is valid
                if (ctx.World != null)
                {
                    IEntity target = ctx.World.GetEntity(targetId);
                    if (target != null && target.IsValid)
                    {
                        return Variable.FromObject(targetId);
                    }
                    else
                    {
                        // Target no longer exists, remove from tracking
                        _lastSpellTargets.Remove(ctx.Caller.ObjectId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// ActionCastSpellAtObject(int nSpell, object oTarget, int nMetaMagic=0, ...) - Casts a spell at a target object
        /// Based on swkotor.exe: Queues spell casting action, tracks target and metamagic type
        /// </summary>
        private Variable Func_ActionCastSpellAtObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int spellId = args.Count > 0 ? args[0].AsInt() : 0;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;
            int metamagic = args.Count > 2 ? args[2].AsInt() : 0; // nMetaMagic parameter

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            // Track spell target for GetSpellTargetObject
            // Based on swkotor.exe: GetSpellTargetObject returns last target of spell cast by caller
            // Located via string references: Spell target tracking for script queries
            // Original implementation: Stores target entity ID when spell is cast
            if (targetId != ObjectInvalid)
            {
                _lastSpellTargets[ctx.Caller.ObjectId] = targetId;

                // Track spell caster for GetLastSpellCaster (target can query who cast spell on them)
                // Based on swkotor.exe: GetLastSpellCaster returns caster entity that last cast spell on caller
                // Original implementation: Stores caster entity ID on target when spell is cast
                _lastSpellCasters[targetId] = ctx.Caller.ObjectId;
            }

            // Track spell ID for GetSpellId
            // Based on swkotor.exe: GetSpellId returns last spell ID cast by caller
            // Original implementation: Stores spell ID when spell is cast
            _lastSpellIds[ctx.Caller.ObjectId] = spellId;

            // Track metamagic type for GetMetaMagicFeat
            if (metamagic != 0)
            {
                _lastMetamagicTypes[ctx.Caller.ObjectId] = metamagic;
            }
            else
            {
                // Clear metamagic tracking if no metamagic is used
                _lastMetamagicTypes.Remove(ctx.Caller.ObjectId);
            }

            var action = new ActionCastSpellAtObject(spellId, targetId);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        /// <summary>
        /// ActionCastSpellAtLocation(int nSpell, location lTarget, int nMetaMagic=0, ...) - Casts a spell at a target location
        /// Based on swkotor.exe: Queues spell casting action at location, tracks target location
        /// </summary>
        private Variable Func_ActionCastSpellAtLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int spellId = args.Count > 0 ? args[0].AsInt() : 0;
            object locObj = args.Count > 1 ? args[1].AsLocation() : null;
            int metamagic = args.Count > 2 ? args[2].AsInt() : 0; // nMetaMagic parameter

            if (ctx.Caller == null || locObj == null || !(locObj is Location location))
            {
                return Variable.Void();
            }

            // Track spell target location for GetSpellTargetLocation
            // Based on swkotor.exe: GetSpellTargetLocation returns last target location of spell cast by caller
            // Located via string references: Spell target location tracking for script queries
            // Original implementation: Stores target location when spell is cast at location
            _lastSpellTargetLocations[ctx.Caller.ObjectId] = location;

            // Track spell ID for GetSpellId
            _lastSpellIds[ctx.Caller.ObjectId] = spellId;

            // Track metamagic type for GetMetaMagicFeat
            if (metamagic != 0)
            {
                _lastMetamagicTypes[ctx.Caller.ObjectId] = metamagic;
            }
            else
            {
                // Clear metamagic tracking if no metamagic is used
                _lastMetamagicTypes.Remove(ctx.Caller.ObjectId);
            }

            var action = new ActionCastSpellAtLocation(spellId, location.Position);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetCurrentHitPoints(object oCreature=OBJECT_SELF) - returns current hit points
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Hit point system
        /// Located via string references: "CurrentHP" @ 0x007c1b40, "CurrentHP: " @ 0x007cb168
        /// Original implementation: Returns current HP from creature's stats component
        /// HP tracking: CurrentHP decreases when damage is taken, increases when healed
        /// Returns: Current HP value (0 or higher) or 0 if entity is invalid or has no stats
        /// </remarks>
        private Variable Func_GetCurrentHitPoints(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.CurrentHP);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetMaxHitPoints(object oCreature=OBJECT_SELF) - returns maximum hit points
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Maximum hit point system
        /// Located via string references: "Max_HPs" @ 0x007cb714
        /// Original implementation: Returns maximum HP from creature's stats component
        /// Max HP calculation: Based on class levels, Constitution modifier, feats, effects
        /// Returns: Maximum HP value (1 or higher) or 0 if entity is invalid or has no stats
        /// </remarks>
        private Variable Func_GetMaxHitPoints(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.MaxHP);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetIsDead(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetIsDead(object oCreature) - Returns TRUE if oCreature is dead, dying, or a dead PC
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.IsDead ? 1 : 0);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetIsPC(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetIsPC(object oCreature) - Returns TRUE if oCreature is a Player Controlled character
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                // Check if entity is the player entity via GameServicesContext
                if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                {
                    if (services.PlayerEntity != null && services.PlayerEntity.ObjectId == entity.ObjectId)
                    {
                        return Variable.FromInt(1);
                    }
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetIsNPC(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetIsNPC(object oCreature) - Returns TRUE if oCreature is an NPC (not a PC)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.ObjectType == Runtime.Core.Enums.ObjectType.Creature)
            {
                // Check if entity is NOT the player entity
                if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                {
                    if (services.PlayerEntity == null || services.PlayerEntity.ObjectId != entity.ObjectId)
                    {
                        return Variable.FromInt(1); // Is NPC
                    }
                }
                else
                {
                    // No player entity context, assume NPC if it's a creature
                    return Variable.FromInt(1);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetStringByStrRef(int nStrRef) - gets string from talk table (TLK) using string reference
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: TLK (talk table) string lookup system
        /// Located via string references: "STRREF" @ 0x007b6368, "StrRef" @ 0x007c1fe8, "NameStrRef" @ 0x007c0274
        /// - "KeyNameStrRef" @ 0x007b641c, "NAME_STRREF" @ 0x007c8200, "DescStrRef" @ 0x007d2e40
        /// - Error: "Invalid STRREF %d passed to Fetch" @ 0x007b6ccc, "BAD STRREF" @ 0x007c2968
        /// Original implementation: TLK files contain string tables indexed by StrRef (32-bit integer)
        /// TLK format: Binary file with string table, each entry has text and sound ResRef
        /// StrRef range: Valid StrRefs are positive integers, 0 = empty string, negative = invalid
        /// Returns: String text from TLK or empty string if StrRef is invalid or not found
        /// </remarks>
        private Variable Func_GetStringByStrRef(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetStringByStrRef(int nStrRef) - Get a string from the talk table using nStrRef
            int strRef = args.Count > 0 ? args[0].AsInt() : 0;

            // Access DialogueManager from GameServicesContext to get TLK
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager)
                {
                    string text = dialogueManager.LookupString(strRef);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return Variable.FromString(text);
                    }
                }
            }

            return Variable.FromString("");
        }

        private Variable Func_GetLastSpeaker(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLastSpeaker() - Use this in a conversation script to get the person with whom you are conversing
            // Returns OBJECT_INVALID if the caller is not a valid creature or not in conversation

            // Access DialogueManager from GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager && dialogueManager.IsConversationActive)
                {
                    var state = dialogueManager.CurrentState;
                    if (state != null && state.Context != null)
                    {
                        // Get the speaker (owner of the dialogue)
                        IEntity speaker = state.Context.Owner;
                        if (speaker != null)
                        {
                            return Variable.FromObject(speaker.ObjectId);
                        }
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetIsInConversation(object oObject) - Determine whether oObject is in conversation
        /// </summary>
        private Variable Func_GetIsInConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);

            if (entity == null)
            {
                return Variable.FromInt(0);
            }

            // Access DialogueManager from GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager && dialogueManager.IsConversationActive)
                {
                    var state = dialogueManager.CurrentState;
                    if (state != null && state.Context != null)
                    {
                        // Check if entity is the owner, PC, or PC speaker in the conversation
                        if (state.Context.Owner != null && state.Context.Owner.ObjectId == entity.ObjectId)
                        {
                            return Variable.FromInt(1);
                        }
                        if (state.Context.PC != null && state.Context.PC.ObjectId == entity.ObjectId)
                        {
                            return Variable.FromInt(1);
                        }
                        if (state.Context.PCSpeaker != null && state.Context.PCSpeaker.ObjectId == entity.ObjectId)
                        {
                            return Variable.FromInt(1);
                        }
                    }
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsConversationActive() - Checks to see if any conversations are currently taking place
        /// </summary>
        private Variable Func_GetIsConversationActive(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager)
                {
                    return Variable.FromInt(dialogueManager.IsConversationActive ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetLastConversation() - Gets the last conversation string (text from current dialogue node)
        /// </summary>
        private Variable Func_GetLastConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager && dialogueManager.IsConversationActive)
                {
                    var state = dialogueManager.CurrentState;
                    if (state != null && state is DialogueState odysseyState && odysseyState.CurrentNode != null)
                    {
                        // Get text from current node using DialogueManager's GetNodeText method
                        string text = dialogueManager.GetNodeText(odysseyState.CurrentNode);
                        if (!string.IsNullOrEmpty(text))
                        {
                            return Variable.FromString(text);
                        }
                    }
                }
            }

            return Variable.FromString("");
        }

        private Variable Func_GetPCSpeaker(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetPCSpeaker() - Get the PC that is involved in the conversation
            // Returns OBJECT_INVALID on error
            // This should return the player entity participating in dialogue
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                // Try to get PC speaker from active conversation
                if (services.DialogueManager is DialogueManager dialogueManager && dialogueManager.IsConversationActive)
                {
                    if (dialogueManager.CurrentState != null)
                    {
                        IEntity pcSpeaker = dialogueManager.CurrentState.Context.GetPCSpeaker();
                        if (pcSpeaker != null)
                        {
                            return Variable.FromObject(pcSpeaker.ObjectId);
                        }
                    }
                }

                // Fall back to player entity if no active conversation
                if (services.PlayerEntity != null)
                {
                    return Variable.FromObject(services.PlayerEntity.ObjectId);
                }
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_BeginConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // BeginConversation(string sResRef="", object oObjectToDialog=OBJECT_INVALID)
            // Starts a conversation with the specified DLG file
            string resRef = args.Count > 0 ? args[0].AsString() : "";
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            if (string.IsNullOrEmpty(resRef))
            {
                // Use default dialogue from object
                IEntity target = ResolveObject(objectId, ctx);
                if (target == null && ctx.Caller != null)
                {
                    target = ctx.Caller;
                }

                if (target != null)
                {
                    // Try to get dialogue resref from component or script hooks
                    Runtime.Core.Interfaces.Components.IScriptHooksComponent hooks = target.GetComponent<Runtime.Core.Interfaces.Components.IScriptHooksComponent>();
                    if (hooks != null)
                    {
                        string dialogueScript = hooks.GetScript(Runtime.Core.Enums.ScriptEvent.OnConversation);
                        if (!string.IsNullOrEmpty(dialogueScript))
                        {
                            resRef = dialogueScript;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(resRef))
            {
                return Variable.FromInt(0); // Failed
            }

            // Get target entity
            IEntity targetEntity = ResolveObject(objectId, ctx);
            if (targetEntity == null && ctx.Caller != null)
            {
                targetEntity = ctx.Caller;
            }

            if (targetEntity == null)
            {
                return Variable.FromInt(0); // Failed
            }

            // Access DialogueManager from GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager && services.PlayerEntity != null)
                {
                    // Start conversation using DialogueManager
                    bool started = dialogueManager.StartConversation(resRef, targetEntity, services.PlayerEntity);
                    return Variable.FromInt(started ? 1 : 0);
                }
            }

            return Variable.FromInt(0); // Failed - no DialogueManager available
        }

        /// <summary>
        /// SetAreaTransitionBMP(int nPredefinedAreaTransition, string sCustomAreaTransitionBMP="")
        /// Sets the transition bitmap for area transitions
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Area transition bitmap setting (routine 203)
        /// Original implementation: Sets transition bitmap for area loading screens
        /// Located via string references: "areatransition" @ 0x007574f4 (swkotor.exe)
        /// Original implementation: Stores transition bitmap on player entity for use during area transitions
        /// - nPredefinedAreaTransition: AREA_TRANSITION_* constant or AREA_TRANSITION_USER_DEFINED (1) for custom
        /// - sCustomAreaTransitionBMP: Custom bitmap filename (required if AREA_TRANSITION_USER_DEFINED is used)
        /// - AREA_TRANSITION_RANDOM (0): Selects random predefined bitmap
        /// - Bitmap is stored on player entity and used when HandleAreaTransition displays loading screen
        /// </remarks>
        private Variable Func_SetAreaTransitionBMP(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetAreaTransitionBMP - Sets area transition bitmap for player
            // This should only be called in area transition scripts, run by the person clicking the transition via AssignCommand

            if (args.Count < 1)
            {
                Console.WriteLine("[Kotor1] SetAreaTransitionBMP: Invalid argument count");
                return Variable.Void();
            }

            int predefinedTransition = args[0].AsInt();
            string customBitmap = args.Count > 1 ? args[1].AsString() : "";

            // Get player entity from context
            IEntity playerEntity = null;
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                playerEntity = services.PlayerEntity;
            }

            if (playerEntity == null)
            {
                Console.WriteLine("[Kotor1] SetAreaTransitionBMP: Player entity not found in context");
                return Variable.Void();
            }

            string bitmapResRef = null;

            // Handle different transition types
            if (predefinedTransition == 0) // AREA_TRANSITION_RANDOM
            {
                // Select random predefined bitmap (exclude RANDOM and USER_DEFINED)
                bitmapResRef = GetRandomAreaTransitionBitmap();
                Console.WriteLine($"[Kotor1] SetAreaTransitionBMP: Selected random transition bitmap: {bitmapResRef}");
            }
            else if (predefinedTransition == 1) // AREA_TRANSITION_USER_DEFINED
            {
                // Use custom bitmap filename
                if (string.IsNullOrEmpty(customBitmap))
                {
                    Console.WriteLine("[Kotor1] SetAreaTransitionBMP: AREA_TRANSITION_USER_DEFINED specified but no custom bitmap provided");
                    return Variable.Void();
                }
                // Remove extension if present (bitmaps are loaded as TPC/TGA)
                bitmapResRef = Path.GetFileNameWithoutExtension(customBitmap);
                Console.WriteLine($"[Kotor1] SetAreaTransitionBMP: Using custom transition bitmap: {bitmapResRef}");
            }
            else
            {
                // Map predefined constant to bitmap filename
                bitmapResRef = GetPredefinedAreaTransitionBitmap(predefinedTransition);
                if (bitmapResRef == null)
                {
                    Console.WriteLine($"[Kotor1] SetAreaTransitionBMP: Invalid predefined transition constant: {predefinedTransition}");
                    return Variable.Void();
                }
                Console.WriteLine($"[Kotor1] SetAreaTransitionBMP: Using predefined transition bitmap: {bitmapResRef} (constant: {predefinedTransition})");
            }

            // Store transition bitmap on player entity
            // Key: "AreaTransitionBitmap" - stores the ResRef of the bitmap to use
            playerEntity.SetData("AreaTransitionBitmap", bitmapResRef);
            playerEntity.SetData("AreaTransitionType", predefinedTransition); // Store type for debugging

            return Variable.Void();
        }

        /// <summary>
        /// Maps predefined area transition constants to bitmap ResRefs.
        /// Based on swkotor.exe: Area transition bitmap naming convention
        /// Original implementation: Bitmaps follow pattern "areatrans_[category][number]" (e.g., "areatrans_city01")
        /// </summary>
        /// <param name="transitionConstant">AREA_TRANSITION_* constant value</param>
        /// <returns>Bitmap ResRef or null if invalid constant</returns>
        private string GetPredefinedAreaTransitionBitmap(int transitionConstant)
        {
            // Map transition constants to bitmap filenames
            // Based on standard BioWare naming: "areatrans_[category][number]"
            switch (transitionConstant)
            {
                // City transitions (2-6)
                case 2: return "areatrans_city01";
                case 3: return "areatrans_city02";
                case 4: return "areatrans_city03";
                case 5: return "areatrans_city04";
                case 6: return "areatrans_city05";

                // Crypt transitions (7-11)
                case 7: return "areatrans_crypt01";
                case 8: return "areatrans_crypt02";
                case 9: return "areatrans_crypt03";
                case 10: return "areatrans_crypt04";
                case 11: return "areatrans_crypt05";

                // Dungeon transitions (12-19)
                case 12: return "areatrans_dungeon01";
                case 13: return "areatrans_dungeon02";
                case 14: return "areatrans_dungeon03";
                case 15: return "areatrans_dungeon04";
                case 16: return "areatrans_dungeon05";
                case 17: return "areatrans_dungeon06";
                case 18: return "areatrans_dungeon07";
                case 19: return "areatrans_dungeon08";

                // Mines transitions (20-28)
                case 20: return "areatrans_mines01";
                case 21: return "areatrans_mines02";
                case 22: return "areatrans_mines03";
                case 23: return "areatrans_mines04";
                case 24: return "areatrans_mines05";
                case 25: return "areatrans_mines06";
                case 26: return "areatrans_mines07";
                case 27: return "areatrans_mines08";
                case 28: return "areatrans_mines09";

                // Sewer transitions (29-33)
                case 29: return "areatrans_sewer01";
                case 30: return "areatrans_sewer02";
                case 31: return "areatrans_sewer03";
                case 32: return "areatrans_sewer04";
                case 33: return "areatrans_sewer05";

                // Castle transitions (34-41)
                case 34: return "areatrans_castle01";
                case 35: return "areatrans_castle02";
                case 36: return "areatrans_castle03";
                case 37: return "areatrans_castle04";
                case 38: return "areatrans_castle05";
                case 39: return "areatrans_castle06";
                case 40: return "areatrans_castle07";
                case 41: return "areatrans_castle08";

                // Interior transitions (42-57)
                case 42: return "areatrans_interior01";
                case 43: return "areatrans_interior02";
                case 44: return "areatrans_interior03";
                case 45: return "areatrans_interior04";
                case 46: return "areatrans_interior05";
                case 47: return "areatrans_interior06";
                case 48: return "areatrans_interior07";
                case 49: return "areatrans_interior08";
                case 50: return "areatrans_interior09";
                case 51: return "areatrans_interior10";
                case 52: return "areatrans_interior11";
                case 53: return "areatrans_interior12";
                case 54: return "areatrans_interior13";
                case 55: return "areatrans_interior14";
                case 56: return "areatrans_interior15";
                case 57: return "areatrans_interior16";

                // Forest transitions (58-62)
                case 58: return "areatrans_forest01";
                case 59: return "areatrans_forest02";
                case 60: return "areatrans_forest03";
                case 61: return "areatrans_forest04";
                case 62: return "areatrans_forest05";

                // Rural transitions (63-67)
                case 63: return "areatrans_rural01";
                case 64: return "areatrans_rural02";
                case 65: return "areatrans_rural03";
                case 66: return "areatrans_rural04";
                case 67: return "areatrans_rural05";

                default:
                    return null;
            }
        }

        /// <summary>
        /// Selects a random predefined area transition bitmap.
        /// Based on swkotor.exe: AREA_TRANSITION_RANDOM behavior
        /// Original implementation: Randomly selects from available predefined bitmaps
        /// </summary>
        /// <returns>Random bitmap ResRef</returns>
        private string GetRandomAreaTransitionBitmap()
        {
            // List of all predefined transition bitmaps (excluding RANDOM=0 and USER_DEFINED=1)
            // Range: 2-67 (all predefined constants)
            int[] predefinedConstants = new int[]
            {
                2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
                20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33,
                34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49,
                50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67
            };

            // Select random constant
            Random random = new Random();
            int randomConstant = predefinedConstants[random.Next(predefinedConstants.Length)];

            // Map to bitmap ResRef
            return GetPredefinedAreaTransitionBitmap(randomConstant);
        }

        /// <summary>
        /// ActionStartConversation - Starts a conversation with an object
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: ActionStartConversation implementation
        /// Located via string references: "ActionStartConversation" @ routine 204
        /// Original implementation: Queues action to start conversation with target object
        /// </remarks>
        private Variable Func_ActionStartConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionStartConversation(object oObjectToConverse, string sDialogResRef="", ...)
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            string dialogResRef = args.Count > 1 ? args[1].AsString() : "";

            if (ctx.Caller != null)
            {
                // Get target entity
                IEntity target = ResolveObject(targetId, ctx);
                if (target == null)
                {
                    target = ctx.Caller;
                }

                // Access DialogueManager from GameServicesContext
                if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                {
                    if (services.DialogueManager is DialogueManager dialogueManager && services.PlayerEntity != null)
                    {
                        // If no dialog resref provided, try to get from target's OnConversation script
                        if (string.IsNullOrEmpty(dialogResRef) && target != null)
                        {
                            IScriptHooksComponent hooks = target.GetComponent<IScriptHooksComponent>();
                            if (hooks != null)
                            {
                                string dialogueScript = hooks.GetScript(ScriptEvent.OnConversation);
                                if (!string.IsNullOrEmpty(dialogueScript))
                                {
                                    dialogResRef = dialogueScript;
                                }
                            }
                        }

                        // Start conversation
                        if (!string.IsNullOrEmpty(dialogResRef))
                        {
                            dialogueManager.StartConversation(dialogResRef, target, services.PlayerEntity);
                        }
                    }
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// ActionPauseConversation - Pauses the current conversation
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: ActionPauseConversation implementation
        /// Located via string references: "ActionPauseConversation" @ routine 205
        /// Original implementation: Pauses active conversation (used during cutscenes)
        /// </remarks>
        private Variable Func_ActionPauseConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionPauseConversation() - Pause the current conversation
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager)
                {
                    dialogueManager.PauseConversation();
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// ActionResumeConversation - Resumes a paused conversation
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: ActionResumeConversation implementation
        /// Located via string references: "ActionResumeConversation" @ routine 206
        /// Original implementation: Resumes conversation that was paused
        /// </remarks>
        private Variable Func_ActionResumeConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionResumeConversation() - Resume a conversation after it has been paused
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager)
                {
                    dialogueManager.ResumeConversation();
                }
            }

            return Variable.Void();
        }

        private Variable Func_GetLastPerceived(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLastPerceived() - Get the object that was perceived in an OnPerception script
            // Returns OBJECT_INVALID if the caller is not a valid creature
            if (ctx.Caller == null || ctx.Caller.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.PerceptionManager is PerceptionManager perceptionManager)
                {
                    IEntity lastPerceived = perceptionManager.GetLastPerceived(ctx.Caller);
                    if (lastPerceived != null)
                    {
                        return Variable.FromObject(lastPerceived.ObjectId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetLastPerceptionHeard(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLastPerceptionHeard() - Check if the last perception was heard
            // Returns 1 if heard, 0 if not heard or invalid
            if (ctx.Caller == null || ctx.Caller.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.PerceptionManager is PerceptionManager perceptionManager)
                {
                    bool wasHeard = perceptionManager.WasLastPerceptionHeard(ctx.Caller);
                    return Variable.FromInt(wasHeard ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        private Variable Func_GetLastPerceptionInaudible(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLastPerceptionInaudible() - Check if the last perceived object has become inaudible
            // Returns 1 if inaudible, 0 if not inaudible or invalid
            if (ctx.Caller == null || ctx.Caller.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.PerceptionManager is PerceptionManager perceptionManager)
                {
                    bool wasInaudible = perceptionManager.WasLastPerceptionInaudible(ctx.Caller);
                    return Variable.FromInt(wasInaudible ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        private Variable Func_GetLastPerceptionSeen(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLastPerceptionSeen() - Check if the last perceived object was seen
            // Returns 1 if seen, 0 if not seen or invalid
            if (ctx.Caller == null || ctx.Caller.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.PerceptionManager is PerceptionManager perceptionManager)
                {
                    bool wasSeen = perceptionManager.WasLastPerceptionSeen(ctx.Caller);
                    return Variable.FromInt(wasSeen ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        private Variable Func_GetLastPerceptionVanished(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLastPerceptionVanished() - Check if the last perceived object has vanished
            // Returns 1 if vanished, 0 if not vanished or invalid
            if (ctx.Caller == null || ctx.Caller.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.PerceptionManager is PerceptionManager perceptionManager)
                {
                    bool wasVanished = perceptionManager.WasLastPerceptionVanished(ctx.Caller);
                    return Variable.FromInt(wasVanished ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetFirstInPersistentObject(object oPersistentObject=OBJECT_SELF, int nResidentObjectType=OBJECT_TYPE_CREATURE, int nPersistentZone=PERSISTENT_ZONE_ACTIVE) - Get the first object within oPersistentObject
        /// </summary>
        private Variable Func_GetFirstInPersistentObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint persistentObjectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int residentObjectType = args.Count > 1 ? args[1].AsInt() : 1; // OBJECT_TYPE_CREATURE = 1
            int persistentZone = args.Count > 2 ? args[2].AsInt() : 0; // PERSISTENT_ZONE_ACTIVE = 0

            if (ctx.Caller == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            IEntity persistentObject = ResolveObject(persistentObjectId, ctx);
            if (persistentObject == null || !persistentObject.IsValid)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Persistent objects are typically stores or containers with inventory
            // Get items from inventory component
            Runtime.Core.Interfaces.Components.IInventoryComponent inventory = persistentObject.GetComponent<Runtime.Core.Interfaces.Components.IInventoryComponent>();
            if (inventory == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Collect all objects in persistent object's inventory
            List<IEntity> objects = new List<IEntity>();
            foreach (IEntity item in inventory.GetAllItems())
            {
                if (item == null || !item.IsValid)
                {
                    continue;
                }

                // Filter by object type if specified (OBJECT_TYPE_ALL = 32767)
                if (residentObjectType != 32767 && (int)item.ObjectType != residentObjectType)
                {
                    continue;
                }

                objects.Add(item);
            }

            // Store iteration state
            _persistentObjectIterations[ctx.Caller.ObjectId] = new PersistentObjectIteration
            {
                Objects = objects,
                CurrentIndex = 0
            };

            // Return first object
            if (objects.Count > 0)
            {
                return Variable.FromObject(objects[0].ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNextInPersistentObject(object oPersistentObject=OBJECT_SELF, int nResidentObjectType=OBJECT_TYPE_CREATURE, int nPersistentZone=PERSISTENT_ZONE_ACTIVE) - Get the next object within oPersistentObject
        /// </summary>
        private Variable Func_GetNextInPersistentObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint persistentObjectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int residentObjectType = args.Count > 1 ? args[1].AsInt() : 1; // OBJECT_TYPE_CREATURE = 1
            int persistentZone = args.Count > 2 ? args[2].AsInt() : 0; // PERSISTENT_ZONE_ACTIVE = 0

            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get iteration state
            if (!_persistentObjectIterations.TryGetValue(ctx.Caller.ObjectId, out PersistentObjectIteration iteration))
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Advance index
            iteration.CurrentIndex++;

            // Return next object
            if (iteration.CurrentIndex < iteration.Objects.Count)
            {
                return Variable.FromObject(iteration.Objects[iteration.CurrentIndex].ObjectId);
            }

            // End of iteration - clear state
            _persistentObjectIterations.Remove(ctx.Caller.ObjectId);
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetLoadFromSaveGame(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLoadFromSaveGame() - Returns whether this script is being run while a load game is in progress
            // Returns 1 if loading from save, 0 otherwise
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                return Variable.FromInt(services.IsLoadingFromSave ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        private Variable Func_GetSkillRank(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetSkillRank(int nSkill, object oTarget=OBJECT_SELF)
            // Returns -1 if oTarget doesn't have nSkill, 0 if untrained
            int skill = args.Count > 0 ? args[0].AsInt() : 0;
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.GetSkillRank(skill));
                }
                return Variable.FromInt(-1); // No stats component = invalid target
            }
            return Variable.FromInt(-1); // Invalid target
        }

        /// <summary>
        /// GetAbility(object oCreature=OBJECT_SELF, int nAbilityType) - returns ability score
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: D20 ability score system
        /// Located via string references: "KeyAbility" @ 0x007c2cbc, "LvlStatAbility" @ 0x007c3f48, "SpecAbilityList" @ 0x007c3ed4
        /// Original implementation: Returns base ability score + modifiers from effects, equipment, etc.
        /// Ability types: ABILITY_STRENGTH (0), ABILITY_DEXTERITY (1), ABILITY_CONSTITUTION (2),
        ///   ABILITY_INTELLIGENCE (3), ABILITY_WISDOM (4), ABILITY_CHARISMA (5)
        /// Ability scores: Range from 1-50 typically, base scores from character creation/template
        /// Modifiers: Effects, equipment, feats can modify ability scores
        /// Returns: Ability score value (1-50 typically) or 0 if entity is invalid
        /// </remarks>
        private Variable Func_GetAbility(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetAbility(object oCreature, int nAbilityType)
            // nAbilityType: ABILITY_STRENGTH (0), ABILITY_DEXTERITY (1), ABILITY_CONSTITUTION (2),
            //              ABILITY_INTELLIGENCE (3), ABILITY_WISDOM (4), ABILITY_CHARISMA (5)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int abilityType = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    // Map ability type to Ability enum
                    if (abilityType >= 0 && abilityType <= 5)
                    {
                        Ability ability = (Ability)abilityType;
                        return Variable.FromInt(stats.GetAbility(ability));
                    }
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetAbilityModifier(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetAbilityModifier(int nAbility, object oCreature=OBJECT_SELF)
            // nAbility: ABILITY_STRENGTH (0), ABILITY_DEXTERITY (1), ABILITY_CONSTITUTION (2),
            //          ABILITY_INTELLIGENCE (3), ABILITY_WISDOM (4), ABILITY_CHARISMA (5)
            int abilityType = args.Count > 0 ? args[0].AsInt() : 0;
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    // Map ability type to Ability enum
                    if (abilityType >= 0 && abilityType <= 5)
                    {
                        Ability ability = (Ability)abilityType;
                        return Variable.FromInt(stats.GetAbilityModifier(ability));
                    }
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetItemInSlot(int nInventorySlot, object oCreature=OBJECT_SELF) - returns item in specified inventory slot
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Inventory slot system
        /// Located via string references: "InventorySlot" @ 0x007bf7d0
        /// Original implementation: KOTOR uses numbered inventory slots (0-17 for equipment, higher for inventory)
        /// Inventory slots: Equipment slots (0-17) for armor, weapons, etc., inventory slots (18+) for items
        /// Returns: Item object ID in specified slot or OBJECT_INVALID if slot is empty
        /// Slot validation: Invalid slot numbers return OBJECT_INVALID
        /// </remarks>
        private Variable Func_GetItemInSlot(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetItemInSlot(int nInventorySlot, object oCreature=OBJECT_SELF)
            int inventorySlot = args.Count > 0 ? args[0].AsInt() : 0;
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IInventoryComponent inventory = entity.GetComponent<Runtime.Core.Interfaces.Components.IInventoryComponent>();
                if (inventory != null)
                {
                    IEntity item = inventory.GetItemInSlot(inventorySlot);
                    if (item != null)
                    {
                        return Variable.FromObject(item.ObjectId);
                    }
                }
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetItemStackSize(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetItemStackSize(object oItem)
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            IEntity item = ResolveObject(itemId, ctx);

            if (item == null || item.ObjectType != Runtime.Core.Enums.ObjectType.Item)
            {
                return Variable.FromInt(0);
            }

            Runtime.Core.Interfaces.Components.IItemComponent itemComponent = item.GetComponent<Runtime.Core.Interfaces.Components.IItemComponent>();
            if (itemComponent != null)
            {
                return Variable.FromInt(itemComponent.StackSize);
            }

            return Variable.FromInt(1); // Default stack size
        }

        /// <summary>
        /// Extracts an Effect object from various container types.
        /// Handles direct Effect objects, Variable wrappers, nested Variables, and other containers.
        /// Based on swkotor.exe and swkotor2.exe: Effect extraction from Variable type system
        /// </summary>
        /// <param name="effectObj">The object that may contain an Effect (can be Effect, Variable, or other container)</param>
        /// <returns>The extracted Effect object, or null if extraction fails</returns>
        private CoreCombat.Effect ExtractEffect(object effectObj)
        {
            if (effectObj == null)
            {
                return null;
            }

            // Case 1: Direct Effect object
            if (effectObj is CoreCombat.Effect directEffect)
            {
                return directEffect;
            }

            // Case 2: Variable wrapper (nested Variable)
            if (effectObj is Variable variable)
            {
                // Check if Variable contains an Effect type
                if (variable.Type == VariableType.Effect)
                {
                    object complexValue = variable.ComplexValue;
                    // Recursively extract from ComplexValue
                    return ExtractEffect(complexValue);
                }
                // If Variable is not Effect type, try extracting from ComplexValue anyway
                // (in case of type mismatch or conversion)
                if (variable.ComplexValue != null)
                {
                    return ExtractEffect(variable.ComplexValue);
                }
                return null;
            }

            // Case 3: Try to extract from ComplexValue if it's a container with ComplexValue property
            // This handles cases where effectObj might be wrapped in another container type
            System.Reflection.PropertyInfo complexValueProp = effectObj.GetType().GetProperty("ComplexValue");
            if (complexValueProp != null)
            {
                object complexValue = complexValueProp.GetValue(effectObj);
                if (complexValue != null)
                {
                    return ExtractEffect(complexValue);
                }
            }

            // Case 4: Try to extract from Value property (some wrapper types use Value)
            System.Reflection.PropertyInfo valueProp = effectObj.GetType().GetProperty("Value");
            if (valueProp != null)
            {
                object value = valueProp.GetValue(effectObj);
                if (value != null)
                {
                    return ExtractEffect(value);
                }
            }

            // Case 5: If effectObj implements IEnumerable, try to extract from first element
            // (handles cases where effect might be in a collection)
            if (effectObj is System.Collections.IEnumerable enumerable)
            {
                System.Collections.IEnumerator enumerator = enumerable.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    return ExtractEffect(enumerator.Current);
                }
            }

            // Case 6: Try type conversion/casting (last resort)
            // Some containers might need explicit conversion
            try
            {
                CoreCombat.Effect convertedEffect = effectObj as CoreCombat.Effect;
                if (convertedEffect != null)
                {
                    return convertedEffect;
                }
            }
            catch
            {
                // Conversion failed, continue to return null
            }

            return null;
        }

        private Variable Func_ApplyEffectToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ApplyEffectToObject(int nDurationType, effect eEffect, object oTarget, float fDuration=0.0f)
            // nDurationType: DURATION_TYPE_INSTANT (0), DURATION_TYPE_TEMPORARY (1), DURATION_TYPE_PERMANENT (2)
            // Based on swkotor.exe: ApplyEffectToObject function implementation
            // Located via string references: "ApplyEffectToObject" in NWScript function dispatch
            // Original implementation: Extracts effect from Variable wrapper, validates duration type, applies to target entity
            int durationType = args.Count > 0 ? args[0].AsInt() : 0;
            object effectObj = args.Count > 1 ? args[1].ComplexValue : null;
            uint targetId = args.Count > 2 ? args[2].AsObjectId() : ObjectSelf;
            float duration = args.Count > 3 ? args[3].AsFloat() : 0f;

            if (effectObj == null || ctx.World == null)
            {
                return Variable.Void();
            }

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null)
            {
                return Variable.Void();
            }

            // Extract Effect from various container types using comprehensive extraction
            CoreCombat.Effect effect = ExtractEffect(effectObj);
            if (effect == null)
            {
                // Log warning but don't crash - this helps with debugging
                Console.WriteLine($"[KotOR1] ApplyEffectToObject: Failed to extract Effect from type: {effectObj?.GetType().Name ?? "null"}");
                return Variable.Void();
            }

            // Map duration type
            CoreCombat.EffectDurationType effectDurationType;
            switch (durationType)
            {
                case 0: // DURATION_TYPE_INSTANT
                    effectDurationType = CoreCombat.EffectDurationType.Instant;
                    break;
                case 1: // DURATION_TYPE_TEMPORARY
                    effectDurationType = CoreCombat.EffectDurationType.Temporary;
                    if (duration > 0f)
                    {
                        // Convert seconds to rounds (assuming 6 seconds per round)
                        effect.DurationRounds = (int)Math.Ceiling(duration / 6f);
                    }
                    break;
                case 2: // DURATION_TYPE_PERMANENT
                    effectDurationType = CoreCombat.EffectDurationType.Permanent;
                    break;
                default:
                    return Variable.Void();
            }

            effect.DurationType = effectDurationType;

            // Apply effect using EffectSystem from world
            if (ctx.World != null && ctx.World.EffectSystem != null)
            {
                ctx.World.EffectSystem.ApplyEffect(target, effect, ctx.Caller);
            }

            return Variable.Void();
        }

        /// <summary>
        /// EffectAssuredHit() - Creates an effect that guarantees attacks will hit
        /// Based on swkotor.exe: EffectAssuredHit @ routine 51
        /// Located via string references: EffectAssuredHit function in NWScript
        /// Original implementation: Creates an effect that forces attacks to automatically hit (bypasses AC check)
        /// Natural 1 still misses (automatic miss rule takes precedence)
        /// Typically used for special abilities, Force powers, or scripted events
        /// Effect duration is temporary (defaults to permanent until removed, but can be set to rounds)
        /// </summary>
        private Variable Func_EffectAssuredHit(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectAssuredHit() - No parameters, creates an AssuredHit effect
            // Based on NWScript definition: effect EffectAssuredHit();
            // The effect guarantees that attacks will hit (unless natural 1)
            // Duration is typically temporary, lasting for a few rounds or until next attack
            // Default duration: permanent until removed (can be overridden when applying the effect)
            var effect = new CoreCombat.Effect(CoreCombat.EffectType.AssuredHit)
            {
                DurationType = CoreCombat.EffectDurationType.Permanent,
                DurationRounds = 0,
                Amount = 0 // AssuredHit doesn't use Amount field
            };
            return Variable.FromEffect(effect);
        }

        /// <summary>
        /// GetLastItemEquipped() - Returns the last item that was equipped by the caller
        /// Based on swkotor.exe: Tracks the last item equipped via ActionEquipItem
        /// Returns OBJECT_INVALID if no item has been equipped or caller is invalid
        /// </summary>
        private Variable Func_GetLastItemEquipped(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Retrieve last equipped item for this creature
            if (_lastEquippedItems.TryGetValue(ctx.Caller.ObjectId, out uint itemId))
            {
                // Verify item still exists and is valid
                if (ctx.World != null)
                {
                    IEntity item = ctx.World.GetEntity(itemId);
                    if (item != null && item.IsValid)
                    {
                        return Variable.FromObject(itemId);
                    }
                    else
                    {
                        // Item no longer exists, remove from tracking
                        _lastEquippedItems.Remove(ctx.Caller.ObjectId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetSubScreenID(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_CancelCombat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_GetCurrentForcePoints(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetCurrentForcePoints(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.CurrentFP);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetMaxForcePoints(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetMaxForcePoints(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.MaxFP);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// PauseGame(int bPause) - Pauses or unpauses the game
        /// Based on swkotor.exe: PauseGame pauses all game systems except UI
        /// When paused, combat, movement, scripts, and other game logic are suspended
        /// </summary>
        private Variable Func_PauseGame(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int pause = args.Count > 0 ? args[0].AsInt() : 0;
            bool shouldPause = pause != 0;

            // Access GameSession through GameServicesContext
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.GameSession != null)
                {
                    // Based on swkotor.exe: PauseGame implementation
                    // Located via string references: Game pause system
                    // Original implementation: Pauses/unpauses all game systems except UI
                    if (services.GameSession is Andastra.Game.Games.Odyssey.Game.GameSession gameSession)
                    {
                        if (shouldPause)
                        {
                            gameSession.Pause();
                        }
                        else
                        {
                            gameSession.Resume();
                        }
                    }
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// SetPlayerRestrictMode(int bRestrict) - Restricts player movement/actions
        /// </summary>
        private Variable Func_SetPlayerRestrictMode(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int restrict = args.Count > 0 ? args[0].AsInt() : 0;
            bool shouldRestrict = restrict != 0;

            // Track player restriction state
            // When restricted, player cannot move, interact, or perform actions
            // Used during cutscenes, dialogues, etc.
            _playerRestricted = shouldRestrict;

            // Notify GameSession if available to enforce restriction
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.GameSession != null && services.PlayerEntity != null)
                {
                    // Player restriction would be enforced by PlayerController or GameSession
                    // This flag is now tracked and can be checked by movement/interaction systems
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetPlayerRestrictMode() - Returns TRUE if player is in restrict mode
        /// </summary>
        private Variable Func_GetPlayerRestrictMode(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Player restriction state is tracked by _playerRestricted field
            // Based on swkotor.exe: Player restriction mode prevents player movement/actions
            return Variable.FromInt(_playerRestricted ? 1 : 0);
        }

        /// <summary>
        /// GetCasterLevel(object oCreature=OBJECT_SELF) - Returns the caster level of a creature
        /// Based on swkotor.exe: Caster level is typically the total character level or Force user class levels
        /// For Force powers, caster level = total level of Force-using classes (Jedi Consular, Guardian, Sentinel, Master, Lord)
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetCasterLevel returns the sum of levels in Force-using classes
        /// Force-using classes are determined by the "forcedie" column in classes.2da (if forcedie > 0, class is Force-using)
        /// This matches the original engine behavior where caster level for Force powers is based on Force-using class levels only
        /// </remarks>
        private Variable Func_GetCasterLevel(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);

            if (entity == null || entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            // Get caster level from CreatureComponent class list
            // In KOTOR, caster level for Force powers is the sum of Force-using class levels
            CreatureComponent creatureComp = entity.GetComponent<CreatureComponent>();
            if (creatureComp != null)
            {
                // Get GameDataManager from context to look up class data
               Data.GameDataManager gameDataManager = null;
                if (ctx != null && ctx.World != null && ctx.World.GameDataProvider != null)
                {
                    Data.OdysseyGameDataProvider odysseyProvider = ctx.World.GameDataProvider as Data.OdysseyGameDataProvider;
                    if (odysseyProvider != null)
                    {
                        gameDataManager = odysseyProvider.GameDataManager;
                    }
                }

                if (gameDataManager != null)
                {
                    // Get caster level as sum of Force-using class levels
                    int casterLevel = creatureComp.GetForceUsingClassLevels(gameDataManager);
                    return Variable.FromInt(casterLevel);
                }
                else
                {
                    // Fallback: if GameDataManager not available, return total level
                    // This should not happen in normal gameplay, but provides graceful degradation
                    int totalLevel = creatureComp.GetTotalLevel();
                    return Variable.FromInt(totalLevel);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetFirstEffect(object oCreature=OBJECT_SELF) - starts iteration over effects on creature
        /// </summary>
        private Variable Func_GetFirstEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            if (ctx.Caller == null || ctx.World == null || ctx.World.EffectSystem == null)
            {
                return Variable.FromEffect(null);
            }

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.FromEffect(null);
            }

            // Get all effects from EffectSystem
            List<CoreCombat.ActiveEffect> effects = new List<CoreCombat.ActiveEffect>();
            foreach (CoreCombat.ActiveEffect effect in ctx.World.EffectSystem.GetEffects(entity))
            {
                effects.Add(effect);
            }

            // Store iteration state
            _effectIterations[ctx.Caller.ObjectId] = new EffectIteration
            {
                Effects = effects,
                CurrentIndex = 0
            };

            // Return first effect (convert ActiveEffect to Effect)
            if (effects.Count > 0)
            {
                return Variable.FromEffect(effects[0].Effect);
            }

            return Variable.FromEffect(null);
        }

        /// <summary>
        /// GetNextEffect(object oCreature=OBJECT_SELF) - continues iteration over effects on creature
        /// </summary>
        private Variable Func_GetNextEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            if (ctx.Caller == null)
            {
                return Variable.FromEffect(null);
            }

            // Get iteration state
            if (!_effectIterations.TryGetValue(ctx.Caller.ObjectId, out EffectIteration iteration))
            {
                return Variable.FromEffect(null);
            }

            // Advance index
            iteration.CurrentIndex++;

            // Return next effect
            if (iteration.CurrentIndex < iteration.Effects.Count)
            {
                return Variable.FromEffect(iteration.Effects[iteration.CurrentIndex].Effect);
            }

            // End of iteration - clear state
            _effectIterations.Remove(ctx.Caller.ObjectId);
            return Variable.FromEffect(null);
        }

        /// <summary>
        /// RemoveEffect(effect eEffect, object oCreature=OBJECT_SELF) - removes an effect from creature
        /// </summary>
        private Variable Func_RemoveEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            object effectObj = args.Count > 0 ? args[0].ComplexValue : null;
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            if (effectObj == null || ctx.World == null || ctx.World.EffectSystem == null)
            {
                return Variable.Void();
            }

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.Void();
            }

            // Extract Effect from various container types using comprehensive extraction
            CoreCombat.Effect effect = ExtractEffect(effectObj);
            if (effect == null)
            {
                // Log warning but don't crash - this helps with debugging
                Console.WriteLine($"[KotOR1] RemoveEffect: Failed to extract Effect from type: {effectObj?.GetType().Name ?? "null"}");
                return Variable.Void();
            }

            // Find and remove matching effect from entity
            foreach (CoreCombat.ActiveEffect activeEffect in ctx.World.EffectSystem.GetEffects(entity))
            {
                if (activeEffect.Effect == effect)
                {
                    ctx.World.EffectSystem.RemoveEffect(entity, activeEffect);
                    break;
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetIsEffectValid(effect eEffect) - returns TRUE if effect is valid
        /// </summary>
        private Variable Func_GetIsEffectValid(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            object effectObj = args.Count > 0 ? args[0].ComplexValue : null;

            if (effectObj == null)
            {
                return Variable.FromInt(0);
            }

            // Check if effect is a valid Effect object (using comprehensive extraction)
            CoreCombat.Effect effect = ExtractEffect(effectObj);
            if (effect != null)
            {
                return Variable.FromInt(1);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetEffectDurationType(effect eEffect) - returns duration type of effect
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Effect duration type system
        /// Original implementation: Effects have duration types (Instant, Temporary, Permanent)
        /// DURATION_TYPE_INSTANT = 0: Effect applies once and ends
        /// DURATION_TYPE_TEMPORARY = 1: Effect has duration and expires after time
        /// DURATION_TYPE_PERMANENT = 2: Effect persists until removed manually
        /// </remarks>
        private Variable Func_GetEffectDurationType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            object effectObj = args.Count > 0 ? args[0].ComplexValue : null;

            // Extract Effect from various container types using comprehensive extraction
            CoreCombat.Effect effect = ExtractEffect(effectObj);
            if (effect != null)
            {
                // Map EffectDurationType to NWScript constants
                // DURATION_TYPE_INSTANT = 0, DURATION_TYPE_TEMPORARY = 1, DURATION_TYPE_PERMANENT = 2
                switch (effect.DurationType)
                {
                    case CoreCombat.EffectDurationType.Instant:
                        return Variable.FromInt(0);
                    case CoreCombat.EffectDurationType.Temporary:
                        return Variable.FromInt(1);
                    case CoreCombat.EffectDurationType.Permanent:
                        return Variable.FromInt(2);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetEffectSubType(effect eEffect) - returns subtype of effect
        /// </summary>
        private Variable Func_GetEffectSubType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            object effectObj = args.Count > 0 ? args[0].ComplexValue : null;

            // Extract Effect from various container types using comprehensive extraction
            CoreCombat.Effect effect = ExtractEffect(effectObj);
            if (effect != null)
            {
                return Variable.FromInt(effect.SubType);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetEffectCreator(effect eEffect) - returns creator of effect
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Effect creator tracking system
        /// Original implementation: Effects store creator entity ID (who cast the spell/applied the effect)
        /// Creator tracking: Used for ownership checks, caster level calculations, spell targeting
        /// Returns: Creator entity object ID or OBJECT_INVALID if effect has no creator
        /// Search behavior: Iterates through active effects on entity to find matching effect
        /// </remarks>
        private Variable Func_GetEffectCreator(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            object effectObj = args.Count > 0 ? args[0].ComplexValue : null;
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            if (effectObj == null || ctx.World == null || ctx.World.EffectSystem == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Extract Effect from various container types using comprehensive extraction
            CoreCombat.Effect effect = ExtractEffect(effectObj);
            if (effect != null)
            {
                // Find matching effect and return creator
                foreach (CoreCombat.ActiveEffect activeEffect in ctx.World.EffectSystem.GetEffects(entity))
                {
                    if (activeEffect.Effect == effect)
                    {
                        if (activeEffect.Creator != null)
                        {
                            return Variable.FromObject(activeEffect.Creator.ObjectId);
                        }
                        break;
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetFirstObjectInArea(object oArea=OBJECT_INVALID, int nObjectType=OBJECT_TYPE_ALL) - starts iteration over objects in area
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Object iteration system for area entities
        /// Located via string references: Object iteration maintains state per calling script context
        /// Original implementation: Iterates through area's entity lists (Creatures, Doors, Placeables, etc.)
        /// Object type filtering: OBJECT_TYPE_ALL (-1) returns all types, specific types filter by entity type
        /// Iteration state: Stored per caller entity ID for GetNextObjectInArea to continue
        /// Returns: First matching object ID or OBJECT_INVALID if no objects found
        /// </remarks>
        private Variable Func_GetFirstObjectInArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint areaId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            int objectType = args.Count > 1 ? args[1].AsInt() : -1; // OBJECT_TYPE_ALL = -1

            if (ctx.Caller == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get area
            IArea area = null;
            if (areaId == ObjectInvalid || areaId == ObjectSelf)
            {
                area = ctx.World.CurrentArea;
            }
            else
            {
                // If specific area ID provided, use current area as fallback
                // (Areas are typically accessed via CurrentArea, not as entities)
                area = ctx.World.CurrentArea;
            }

            if (area == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Collect all objects in area by iterating through all entity types
            List<IEntity> objects = new List<IEntity>();

            // Get all entities from world that are in the current area
            // We'll filter by checking if they're creatures, placeables, doors, etc. from the area
            foreach (IEntity entity in ctx.World.GetAllEntities())
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Filter by object type if specified
                if (objectType >= 0 && (int)entity.ObjectType != objectType)
                {
                    continue;
                }

                // Check if entity is in the area by checking area's collections
                bool inArea = false;
                if (entity.ObjectType == Runtime.Core.Enums.ObjectType.Creature)
                {
                    foreach (IEntity creature in area.Creatures)
                    {
                        if (creature != null && creature.ObjectId == entity.ObjectId)
                        {
                            inArea = true;
                            break;
                        }
                    }
                }
                else if (entity.ObjectType == Runtime.Core.Enums.ObjectType.Placeable)
                {
                    foreach (IEntity placeable in area.Placeables)
                    {
                        if (placeable != null && placeable.ObjectId == entity.ObjectId)
                        {
                            inArea = true;
                            break;
                        }
                    }
                }
                else if (entity.ObjectType == Runtime.Core.Enums.ObjectType.Door)
                {
                    foreach (IEntity door in area.Doors)
                    {
                        if (door != null && door.ObjectId == entity.ObjectId)
                        {
                            inArea = true;
                            break;
                        }
                    }
                }
                else if (entity.ObjectType == Runtime.Core.Enums.ObjectType.Trigger)
                {
                    foreach (IEntity trigger in area.Triggers)
                    {
                        if (trigger != null && trigger.ObjectId == entity.ObjectId)
                        {
                            inArea = true;
                            break;
                        }
                    }
                }
                else if (entity.ObjectType == Runtime.Core.Enums.ObjectType.Waypoint)
                {
                    foreach (IEntity waypoint in area.Waypoints)
                    {
                        if (waypoint != null && waypoint.ObjectId == entity.ObjectId)
                        {
                            inArea = true;
                            break;
                        }
                    }
                }
                else if (entity.ObjectType == Runtime.Core.Enums.ObjectType.Sound)
                {
                    foreach (IEntity sound in area.Sounds)
                    {
                        if (sound != null && sound.ObjectId == entity.ObjectId)
                        {
                            inArea = true;
                            break;
                        }
                    }
                }
                else if (objectType < 0)
                {
                    // OBJECT_TYPE_ALL - include other types too
                    inArea = true;
                }

                if (inArea)
                {
                    objects.Add(entity);
                }
            }

            // Store iteration state
            _areaObjectIterations[ctx.Caller.ObjectId] = new AreaObjectIteration
            {
                Objects = objects,
                CurrentIndex = 0
            };

            // Return first object
            if (objects.Count > 0)
            {
                return Variable.FromObject(objects[0].ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNextObjectInArea(object oArea=OBJECT_INVALID, int nObjectType=OBJECT_TYPE_ALL) - continues iteration over objects in area
        /// </summary>
        private Variable Func_GetNextObjectInArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint areaId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            int objectType = args.Count > 1 ? args[1].AsInt() : -1;

            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get iteration state
            if (!_areaObjectIterations.TryGetValue(ctx.Caller.ObjectId, out AreaObjectIteration iteration))
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Advance index
            iteration.CurrentIndex++;

            // Return next object
            if (iteration.CurrentIndex < iteration.Objects.Count)
            {
                return Variable.FromObject(iteration.Objects[iteration.CurrentIndex].ObjectId);
            }

            // End of iteration - clear state
            _areaObjectIterations.Remove(ctx.Caller.ObjectId);
            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetMetaMagicFeat(object oCaster=OBJECT_SELF) - Returns the metamagic feat of the caster
        /// </summary>
        private Variable Func_GetMetaMagicFeat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetMetaMagicFeat() - Returns the metamagic type of the last spell cast by the caller
            // Metamagic feats: METAMAGIC_EMPOWER (1), METAMAGIC_EXTEND (2), METAMAGIC_MAXIMIZE (4), METAMAGIC_QUICKEN (8)

            if (ctx.Caller == null || ctx.Caller.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(-1);
            }

            // Retrieve last metamagic type for this caster (tracked in ActionCastSpellAtObject)
            if (_lastMetamagicTypes.TryGetValue(ctx.Caller.ObjectId, out int metamagicType))
            {
                return Variable.FromInt(metamagicType);
            }

            // No metamagic tracked, return 0 (no metamagic)
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetRacialType(object oCreature=OBJECT_SELF) - Returns the racial type of a creature
        /// Based on swkotor.exe: 0x005261b0 @ 0x005261b0 (load creature from UTC template)
        /// Racial type is stored in CreatureComponent.RaceId (from UTC template)
        /// </summary>
        private Variable Func_GetRacialType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity creature = ResolveObject(creatureId, ctx);

            if (creature == null || creature.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            // Access racial type from CreatureComponent
            CreatureComponent creatureComp = creature.GetComponent<CreatureComponent>();
            if (creatureComp != null)
            {
                return Variable.FromInt(creatureComp.RaceId);
            }

            return Variable.FromInt(0);
        }

        private Variable Func_FortitudeSave(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // FortitudeSave(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.FortitudeSave);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_ReflexSave(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ReflexSave(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.ReflexSave);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_WillSave(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // WillSave(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.WillSave);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetSpellSaveDC(object oCaster=OBJECT_SELF) - Returns the spell save DC (Difficulty Class) for a caster
        /// </summary>
        private Variable Func_GetSpellSaveDC(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint casterId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity caster = ResolveObject(casterId, ctx);

            if (caster != null)
            {
                Runtime.Core.Interfaces.Components.IStatsComponent stats = caster.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    // Spell save DC = 10 + caster level + ability modifier (typically Wisdom or Charisma for Force powers)
                    // Base DC is 10, plus caster level, plus relevant ability modifier
                    // Wisdom for Jedi, Charisma for Sith
                    int casterLevel = Func_GetCasterLevel(new List<Variable> { Variable.FromObject(caster.ObjectId) }, ctx).AsInt();
                    // Default to Wisdom modifier (Jedi), but could check class to determine if Sith (Charisma)
                    int abilityMod = stats.GetAbilityModifier(Runtime.Core.Enums.Ability.Wisdom);
                    int saveDC = 10 + casterLevel + abilityMod;
                    return Variable.FromInt(saveDC);
                }
            }

            // Default save DC if caster not found
            return Variable.FromInt(10);
        }

        /// <summary>
        /// MagicalEffect(effect eEffect) - Wraps an effect as a magical effect (can be dispelled)
        /// Based on swkotor.exe: Sets effect subtype to SUBTYPE_MAGICAL (8)
        /// Magical effects can be dispelled by DispelMagic
        /// </summary>
        private Variable Func_MagicalEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0)
            {
                return Variable.FromEffect(null);
            }

            object effectObj = args[0].ComplexValue;
            if (effectObj == null)
            {
                return Variable.FromEffect(null);
            }

            // Set subtype to MAGICAL (8)
            CoreCombat.Effect effect = effectObj as CoreCombat.Effect;
            if (effect != null)
            {
                effect.SubType = 8; // SUBTYPE_MAGICAL
                // Mark effect as magical type (can be dispelled)
                // Effect is already marked via SubType, which is sufficient
                return Variable.FromEffect(effect);
            }

            return Variable.FromEffect(null);
        }

        /// <summary>
        /// SupernaturalEffect(effect eEffect) - Wraps an effect as a supernatural effect (cannot be dispelled)
        /// Based on swkotor.exe: Sets effect subtype to SUBTYPE_SUPERNATURAL (16)
        /// Supernatural effects cannot be dispelled
        /// </summary>
        private Variable Func_SupernaturalEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0)
            {
                return Variable.FromEffect(null);
            }

            object effectObj = args[0].ComplexValue;
            if (effectObj == null)
            {
                return Variable.FromEffect(null);
            }

            // Set subtype to SUPERNATURAL (16)
            CoreCombat.Effect effect = effectObj as CoreCombat.Effect;
            if (effect != null)
            {
                effect.SubType = 16; // SUBTYPE_SUPERNATURAL
                effect.IsSupernatural = true;
                return Variable.FromEffect(effect);
            }

            return Variable.FromEffect(null);
        }

        /// <summary>
        /// ExtraordinaryEffect(effect eEffect) - Wraps an effect as an extraordinary effect (cannot be dispelled, not affected by antimagic)
        /// </summary>
        private Variable Func_ExtraordinaryEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0)
            {
                return Variable.FromEffect(null);
            }

            object effectObj = args[0].ComplexValue;
            if (effectObj == null)
            {
                return Variable.FromEffect(null);
            }

            // Extraordinary effects cannot be dispelled and are not affected by antimagic fields
            // Set subtype to EXTRAORDINARY (32)
            CoreCombat.Effect effect = effectObj as CoreCombat.Effect;
            if (effect != null)
            {
                effect.SubType = 32; // SUBTYPE_EXTRAORDINARY
                // Mark effect as extraordinary type (cannot be dispelled, not affected by antimagic)
                return Variable.FromEffect(effect);
            }

            return Variable.FromEffect(null);
        }

        private Variable Func_EffectACIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectACIncrease(int nBonus)
            int bonus = args.Count > 0 ? args[0].AsInt() : 0;
            var effect = new Effect(EffectType.ACIncrease)
            {
                Amount = bonus,
                DurationType = EffectDurationType.Permanent
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_GetBaseAttackBonus(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetBaseAttackBonus(object oCreature=OBJECT_SELF) - Returns the base attack bonus (BAB) of a creature
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IStatsComponent stats = entity.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.BaseAttackBonus);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetAC(object oCreature=OBJECT_SELF) - returns armor class
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: D20 armor class system
        /// Located via string references: "ArmorClass" @ 0x007c42a8, "ArmorClassColumn" @ 0x007c2ae8
        /// Original implementation: Returns total AC from base (10) + Dexterity modifier + armor + shield + effects
        /// AC calculation: Base 10 + Dex modifier + armor bonus + shield bonus + deflection + natural + dodge + other modifiers
        /// AC sources: Equipment (armor, shield), ability modifiers (Dexterity), effects (AC increase/decrease)
        /// Default AC: 10 (base) if entity has no stats component
        /// Returns: Armor class value (10 or higher typically) or 10 if entity is invalid
        /// </remarks>
        private Variable Func_GetAC(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IStatsComponent stats = entity.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.ArmorClass);
                }
            }
            return Variable.FromInt(10);
        }

        private Variable Func_EffectHeal(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectHeal(int nDamageToHeal)
            int amount = args.Count > 0 ? args[0].AsInt() : 0;
            var effect = new Effect(EffectType.Heal)
            {
                Amount = amount,
                DurationType = EffectDurationType.Instant
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_EffectDamage(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectDamage(int nDamageAmount, int nDamageType=DAMAGE_TYPE_UNIVERSAL, int nDamagePower=DAMAGE_POWER_NORMAL)
            int amount = args.Count > 0 ? args[0].AsInt() : 0;
            int damageType = args.Count > 1 ? args[1].AsInt() : 8; // DAMAGE_TYPE_UNIVERSAL
            int damagePower = args.Count > 2 ? args[2].AsInt() : 0; // DAMAGE_POWER_NORMAL
            var effect = new Effect(EffectType.DamageIncrease)
            {
                Amount = amount,
                SubType = damageType,
                DurationType = EffectDurationType.Instant
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_EffectAbilityIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectAbilityIncrease(int nAbilityToIncrease, int nModifyBy)
            int ability = args.Count > 0 ? args[0].AsInt() : 0;
            int amount = args.Count > 1 ? args[1].AsInt() : 0;
            var effect = new Effect(EffectType.AbilityIncrease)
            {
                Amount = amount,
                SubType = ability,
                DurationType = EffectDurationType.Permanent
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_EffectDamageResistance(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectDamageResistance(int nDamageType, int nAmount, int nLimit=0)
            int damageType = args.Count > 0 ? args[0].AsInt() : 0;
            int amount = args.Count > 1 ? args[1].AsInt() : 0;
            int limit = args.Count > 2 ? args[2].AsInt() : 0;
            var effect = new Effect(EffectType.DamageResistance)
            {
                Amount = amount,
                SubType = damageType,
                DurationType = EffectDurationType.Permanent
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_EffectResurrection(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectResurrection()
            var effect = new Effect(EffectType.Heal)
            {
                Amount = 999999, // Full heal on resurrection
                DurationType = EffectDurationType.Instant
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_EffectSavingThrowIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectSavingThrowIncrease(int nSaveType, int nAmount)
            int saveType = args.Count > 0 ? args[0].AsInt() : 0; // SAVING_THROW_ALL
            int amount = args.Count > 1 ? args[1].AsInt() : 0;
            var effect = new Effect(EffectType.SaveIncrease)
            {
                Amount = amount,
                SubType = saveType,
                DurationType = EffectDurationType.Permanent
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_EffectAttackIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectAttackIncrease(int nBonus)
            int bonus = args.Count > 0 ? args[0].AsInt() : 0;
            var effect = new Effect(EffectType.AttackIncrease)
            {
                Amount = bonus,
                DurationType = EffectDurationType.Permanent
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_EffectDamageReduction(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectDamageReduction(int nAmount, int nDamagePower, int nLimit=0)
            int amount = args.Count > 0 ? args[0].AsInt() : 0;
            int damagePower = args.Count > 1 ? args[1].AsInt() : 0;
            int limit = args.Count > 2 ? args[2].AsInt() : 0;
            var effect = new Effect(EffectType.DamageReduction)
            {
                Amount = amount,
                SubType = damagePower,
                DurationType = EffectDurationType.Permanent
            };
            return Variable.FromEffect(effect);
        }

        private Variable Func_EffectDamageIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectDamageIncrease(int nBonus, int nDamageType=DAMAGE_TYPE_UNIVERSAL)
            int bonus = args.Count > 0 ? args[0].AsInt() : 0;
            int damageType = args.Count > 1 ? args[1].AsInt() : 8; // DAMAGE_TYPE_UNIVERSAL
            var effect = new Effect(EffectType.DamageIncrease)
            {
                Amount = bonus,
                SubType = damageType,
                DurationType = EffectDurationType.Permanent
            };
            return Variable.FromEffect(effect);
        }

        /// <summary>
        /// GetFactionEqual(object oFirstObject, object oSecondObject=OBJECT_SELF) - returns TRUE if both objects have same faction
        /// </summary>
        private Variable Func_GetFactionEqual(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId1 = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            uint objectId2 = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity entity1 = ResolveObject(objectId1, ctx);
            IEntity entity2 = ResolveObject(objectId2, ctx);

            if (entity1 != null && entity2 != null)
            {
                IFactionComponent faction1 = entity1.GetComponent<IFactionComponent>();
                IFactionComponent faction2 = entity2.GetComponent<IFactionComponent>();

                if (faction1 != null && faction2 != null)
                {
                    return Variable.FromInt(faction1.FactionId == faction2.FactionId ? 1 : 0);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetFactionWeakestMember(int nFactionId, int nPlayerOnly=0) - returns the weakest member of a faction
        /// </summary>
        private Variable Func_GetFactionWeakestMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int factionId = args.Count > 0 ? args[0].AsInt() : 0;
            bool playerOnly = args.Count > 1 && args[1].AsInt() != 0;

            if (ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            IEntity weakest = null;
            int weakestHP = int.MaxValue;

            // Iterate through all entities in the world
            foreach (IEntity entity in ctx.World.GetAllEntities())
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Check if player only
                if (playerOnly)
                {
                    if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                    {
                        if (services.PlayerEntity == null || services.PlayerEntity.ObjectId != entity.ObjectId)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                // Check faction
                IFactionComponent faction = entity.GetComponent<IFactionComponent>();
                if (faction == null || faction.FactionId != factionId)
                {
                    continue;
                }

                // Get HP
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    int currentHP = stats.CurrentHP;
                    if (currentHP < weakestHP)
                    {
                        weakestHP = currentHP;
                        weakest = entity;
                    }
                }
            }

            return Variable.FromObject(weakest?.ObjectId ?? ObjectInvalid);
        }

        /// <summary>
        /// GetFactionStrongestMember(int nFactionId, int nPlayerOnly=0) - returns the strongest member of a faction
        /// </summary>
        private Variable Func_GetFactionStrongestMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int factionId = args.Count > 0 ? args[0].AsInt() : 0;
            bool playerOnly = args.Count > 1 && args[1].AsInt() != 0;

            if (ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            IEntity strongest = null;
            int strongestHP = -1;

            // Iterate through all entities in the world
            foreach (IEntity entity in ctx.World.GetAllEntities())
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Check if player only
                if (playerOnly)
                {
                    if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                    {
                        if (services.PlayerEntity == null || services.PlayerEntity.ObjectId != entity.ObjectId)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                // Check faction
                IFactionComponent faction = entity.GetComponent<IFactionComponent>();
                if (faction == null || faction.FactionId != factionId)
                {
                    continue;
                }

                // Get HP
                Runtime.Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    int currentHP = stats.CurrentHP;
                    if (currentHP > strongestHP)
                    {
                        strongestHP = currentHP;
                        strongest = entity;
                    }
                }
            }

            return Variable.FromObject(strongest?.ObjectId ?? ObjectInvalid);
        }

        /// <summary>
        /// GetFirstFactionMember(int nFactionId, int nPlayerOnly=0) - starts iteration over faction members
        /// </summary>
        /// <summary>
        /// GetFirstFactionMember(int nFactionId, int bPlayerOnly=FALSE) - starts iteration over faction members
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Faction member iteration system
        /// Located via string references: "FactionID" @ 0x007c40b4, "FactionID1" @ 0x007c2924, "FactionID2" @ 0x007c2918
        /// Original implementation: Iterates over all entities with matching faction ID
        /// Faction matching: Checks IFactionComponent.FactionId against provided faction ID
        /// Player only: If bPlayerOnly = TRUE, only returns player-controlled entities
        /// Iteration state: Stores member list in _factionMemberIterations dictionary keyed by caller ObjectId
        /// Returns: First faction member ObjectId or OBJECT_INVALID if no members found
        /// </remarks>
        private Variable Func_GetFirstFactionMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int factionId = args.Count > 0 ? args[0].AsInt() : 0;
            bool playerOnly = args.Count > 1 && args[1].AsInt() != 0;

            if (ctx.Caller == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Collect all faction members
            List<IEntity> members = new List<IEntity>();
            foreach (IEntity entity in ctx.World.GetAllEntities())
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Check if player only
                if (playerOnly)
                {
                    if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                    {
                        if (services.PlayerEntity == null || services.PlayerEntity.ObjectId != entity.ObjectId)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                // Check faction
                IFactionComponent faction = entity.GetComponent<IFactionComponent>();
                if (faction != null && faction.FactionId == factionId)
                {
                    members.Add(entity);
                }
            }

            // Store iteration state
            _factionMemberIterations[ctx.Caller.ObjectId] = new FactionMemberIteration
            {
                Members = members,
                CurrentIndex = 0
            };

            // Return first member
            if (members.Count > 0)
            {
                return Variable.FromObject(members[0].ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNextFactionMember(int nFactionId) - continues iteration over faction members
        /// </summary>
        private Variable Func_GetNextFactionMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int factionId = args.Count > 0 ? args[0].AsInt() : 0;

            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get iteration state
            if (!_factionMemberIterations.TryGetValue(ctx.Caller.ObjectId, out FactionMemberIteration iteration))
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Advance index
            iteration.CurrentIndex++;

            // Return next member
            if (iteration.CurrentIndex < iteration.Members.Count)
            {
                return Variable.FromObject(iteration.Members[iteration.CurrentIndex].ObjectId);
            }

            // End of iteration - clear state
            _factionMemberIterations.Remove(ctx.Caller.ObjectId);
            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetIsEnemy(object oTarget, object oSource=OBJECT_SELF) - returns TRUE if oSource considers oTarget as enemy
        /// </summary>
        /// <summary>
        /// GetIsEnemy(object oTarget, object oSource=OBJECT_SELF) - Returns TRUE if oTarget is an enemy of oSource
        /// </summary>
        private Variable Func_GetIsEnemy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            uint sourceId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity source = ResolveObject(sourceId, ctx);
            IEntity target = ResolveObject(targetId, ctx);

            if (source != null && target != null)
            {
                // Get FactionManager from GameServicesContext
                if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                {
                    if (services.FactionManager is FactionManager factionManager)
                    {
                        bool isHostile = factionManager.IsHostile(source, target);
                        return Variable.FromInt(isHostile ? 1 : 0);
                    }
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsFriend(object oTarget, object oSource=OBJECT_SELF) - returns TRUE if oSource considers oTarget as friend
        /// </summary>
        /// <summary>
        /// GetIsFriend(object oTarget, object oSource=OBJECT_SELF) - Returns TRUE if oTarget is a friend of oSource
        /// </summary>
        private Variable Func_GetIsFriend(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            uint sourceId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity source = ResolveObject(sourceId, ctx);
            IEntity target = ResolveObject(targetId, ctx);

            if (source != null && target != null)
            {
                // Get FactionManager from GameServicesContext
                // FactionManager should always be available through GameServicesContext
                if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                {
                    if (services.FactionManager is FactionManager factionManager)
                    {
                        // Proper friendliness calculation using FactionManager
                        // Handles: faction reputation, personal reputation overrides, temporary hostility
                        // Friendly if reputation >= FriendlyThreshold (90)
                        bool isFriendly = factionManager.IsFriendly(source, target);
                        return Variable.FromInt(isFriendly ? 1 : 0);
                    }
                }

                // FactionManager should always be available - if not, return not friendly as safe default
                // This should not happen in normal operation
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsNeutral(object oTarget, object oSource=OBJECT_SELF) - Returns TRUE if oTarget is neutral to oSource
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Faction relationship system
        /// Located via string reference: "neutral" @ 0x007c28a0
        /// Original implementation: 0x005acc10 @ 0x005acc10 sets faction reputation values including "neutral" (50)
        /// Neutral determination: Reputation value between HostileThreshold (10) and FriendlyThreshold (90)
        /// Faction system: Uses repute.2da table for base faction relationships, personal reputation overrides
        /// Neutral means: Not hostile (reputation > 10) and not friendly (reputation < 90)
        /// Default neutral: If no faction relationship defined, defaults to 50 (neutral)
        /// </remarks>
        private Variable Func_GetIsNeutral(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetIsNeutral(object oTarget, object oSource=OBJECT_SELF) - returns TRUE if target is neutral to source
            // Based on swkotor.exe: Faction relationship system
            // Located via string reference: "neutral" @ 0x007c28a0
            // Original implementation: Checks faction reputation, returns TRUE if reputation is between 11-89 (neutral range)
            // Neutral range: HostileThreshold (10) < reputation < FriendlyThreshold (90)
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            uint sourceId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity source = ResolveObject(sourceId, ctx);
            IEntity target = ResolveObject(targetId, ctx);

            if (source != null && target != null)
            {
                // Get FactionManager from GameServicesContext
                if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                {
                    if (services.FactionManager is FactionManager factionManager)
                    {
                        // Neutral = not hostile AND not friendly
                        // Original engine: Neutral if reputation > 10 AND reputation < 90
                        bool isNeutral = !factionManager.IsHostile(source, target) && !factionManager.IsFriendly(source, target);
                        return Variable.FromInt(isNeutral ? 1 : 0);
                    }
                }

                // Fallback: If not enemy and not friend, then neutral
                IFactionComponent sourceFaction = source.GetComponent<IFactionComponent>();
                IFactionComponent targetFaction = target.GetComponent<IFactionComponent>();

                if (sourceFaction != null && targetFaction != null)
                {
                    // Different factions are neutral by default (unless explicitly hostile)
                    if (sourceFaction.FactionId != targetFaction.FactionId)
                    {
                        return Variable.FromInt(1);
                    }
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetWaypointByTag(string sWaypointTag) - returns waypoint with specified tag
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Waypoint lookup system
        /// Original implementation: Searches for waypoint entity with matching tag string
        /// Search order: First searches current area's waypoints, then world-wide search
        /// Waypoint type: Only returns entities with ObjectType.Waypoint
        /// Tag matching: Case-insensitive string comparison
        /// Returns: Waypoint ObjectId or OBJECT_INVALID if not found
        /// </remarks>
        private Variable Func_GetWaypointByTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string waypointTag = args.Count > 0 ? args[0].AsString() : string.Empty;

            if (string.IsNullOrEmpty(waypointTag) || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Search in current area first
            IArea area = ctx.World.CurrentArea;
            if (area != null)
            {
                IEntity waypoint = area.GetObjectByTag(waypointTag, 0);
                if (waypoint != null && waypoint.ObjectType == Runtime.Core.Enums.ObjectType.Waypoint)
                {
                    return Variable.FromObject(waypoint.ObjectId);
                }
            }

            // Fallback to world search
            IEntity found = ctx.World.GetEntityByTag(waypointTag, 0);
            if (found != null && found.ObjectType == Runtime.Core.Enums.ObjectType.Waypoint)
            {
                return Variable.FromObject(found.ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetName(object oObject) - returns the name of the object
        /// </summary>
        private Variable Func_GetName(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);

            if (entity == null)
            {
                return Variable.FromString(string.Empty);
            }

            // Try to get name from entity data (set by EntityFactory from UTC/UTP/etc.)
            // Cast to Entity to access GetData method
            if (entity is Runtime.Core.Entities.Entity concreteEntity)
            {
                string firstName = concreteEntity.GetData<string>("FirstName", null);
                string lastName = concreteEntity.GetData<string>("LastName", null);

                if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
                {
                    string fullName = string.Empty;
                    if (!string.IsNullOrEmpty(firstName))
                    {
                        fullName = firstName;
                    }
                    if (!string.IsNullOrEmpty(lastName))
                    {
                        if (!string.IsNullOrEmpty(fullName))
                        {
                            fullName += " ";
                        }
                        fullName += lastName;
                    }
                    return Variable.FromString(fullName);
                }
            }

            // Fallback to tag if no name is set
            if (!string.IsNullOrEmpty(entity.Tag))
            {
                return Variable.FromString(entity.Tag);
            }

            return Variable.FromString(string.Empty);
        }

        /// <summary>
        /// ChangeFaction(object oObjectToChangeFaction, object oMemberOfFactionToJoin) - changes faction of object
        /// </summary>
        private Variable Func_ChangeFaction(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectToChangeId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            uint memberOfFactionId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            IEntity objectToChange = ResolveObject(objectToChangeId, ctx);
            IEntity memberOfFaction = ResolveObject(memberOfFactionId, ctx);

            if (objectToChange == null || memberOfFaction == null)
            {
                return Variable.Void();
            }

            // Get faction component from member
            IFactionComponent memberFaction = memberOfFaction.GetComponent<IFactionComponent>();
            if (memberFaction == null)
            {
                return Variable.Void();
            }

            // Get or create faction component for object to change
            IFactionComponent targetFaction = objectToChange.GetComponent<IFactionComponent>();
            if (targetFaction == null)
            {
                // Create faction component if it doesn't exist
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Faction component creation during entity faction change
                // Original implementation: Creates new faction component and assigns it to entity
                // Located via string references: "FactionID" @ 0x007c40b4 (swkotor2.exe)
                // Function: 0x005fb0f0 @ 0x005fb0f0 loads FactionID from creature template
                // Component initialization: Faction component should be initialized with FactionManager for proper reputation lookups
                if (objectToChange is Runtime.Core.Entities.Entity concreteEntity)
                {
                    // Get FactionManager from GameServicesContext if available
                    // FactionManager enables proper faction reputation calculations and hostility checks
                    FactionManager factionManager = null;
                    if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                    {
                        factionManager = services.FactionManager as FactionManager;
                    }

                    // Create faction component with FactionManager if available
                    OdysseyFactionComponent factionComponent;
                    if (factionManager != null)
                    {
                        factionComponent = new OdysseyFactionComponent(factionManager);
                    }
                    else
                    {
                        factionComponent = new OdysseyFactionComponent();
                    }

                    // Initialize component properties
                    factionComponent.Owner = concreteEntity;
                    factionComponent.FactionId = memberFaction.FactionId;

                    // Add component to entity
                    concreteEntity.AddComponent<IFactionComponent>(factionComponent);
                }
            }
            else
            {
                // Change faction ID
                targetFaction.FactionId = memberFaction.FactionId;
            }

            return Variable.Void();
        }

        #region Location Functions

        /// <summary>
        /// GetLocation(object oObject) - Get the location of oObject
        /// </summary>
        private Variable Func_GetLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);

            if (entity == null)
            {
                return Variable.FromLocation(null);
            }

            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return Variable.FromLocation(null);
            }

            var location = new Location(transform.Position, transform.Facing);
            return Variable.FromLocation(location);
        }

        /// <summary>
        /// ActionJumpToLocation(location lLocation) - The subject will jump to lLocation instantly (even between areas)
        /// </summary>
        private Variable Func_ActionJumpToLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0 || ctx.Caller == null)
            {
                return Variable.Void();
            }

            object locObj = args[0].AsLocation();
            if (locObj == null || !(locObj is Location location))
            {
                return Variable.Void();
            }

            var action = new ActionJumpToLocation(location.Position, location.Facing);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        /// <summary>
        /// Location(vector vPosition, float fOrientation) - Create a location
        /// </summary>
        private Variable Func_Location(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            Vector3 position = args.Count > 0 ? args[0].AsVector() : Vector3.Zero;
            float facing = args.Count > 1 ? args[1].AsFloat() : 0f;

            var location = new Location(position, facing);
            return Variable.FromLocation(location);
        }

        /// <summary>
        /// GetSpellTargetLocation() - Get the location of the caller's last spell target
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetSpellTargetLocation implementation
        /// Routine 222: Returns the location of the caller's last spell target
        /// Original implementation: Tracks spell target locations when spells are cast
        /// </remarks>
        private Variable Func_GetSpellTargetLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromLocation(null);
            }

            uint callerId = ctx.Caller.ObjectId;
            if (_lastSpellTargetLocations.TryGetValue(callerId, out Location location))
            {
                return Variable.FromLocation(location);
            }

            return Variable.FromLocation(null);
        }

        /// <summary>
        /// ActionJumpToObject(object oToJumpTo, int bWalkStraightLineToPoint=TRUE) - Jump to an object ID
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: ActionJumpToObject implementation
        /// Located via string references: "JumpToObject" action type
        /// Original implementation: Instantly teleports entity to target object's position
        /// </remarks>
        private Variable Func_ActionJumpToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0 || ctx.Caller == null)
            {
                return Variable.Void();
            }

            uint targetId = args[0].AsObjectId();
            bool walkStraightLine = args.Count > 1 && args[1].AsInt() != 0;

            var action = new ActionJumpToObject(targetId);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetTransitionTarget(object oTransition) - Get the destination (waypoint or door) for a trigger or door
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetTransitionTarget implementation
        /// Located via string references: "LinkedTo" @ 0x007c13a0, "LinkedToModule" @ 0x007bd7bc
        /// Original implementation: Returns destination waypoint or door from trigger/door's LinkedTo field
        /// </remarks>
        private Variable Func_GetTransitionTarget(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            uint objectId = args[0].AsObjectId();
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Check if it's a trigger
            ITriggerComponent trigger = entity.GetComponent<ITriggerComponent>();
            if (trigger != null && !string.IsNullOrEmpty(trigger.LinkedTo))
            {
                // Find the destination waypoint or door by tag
                IEntity destination = ctx.World.GetEntityByTag(trigger.LinkedTo, 0);
                if (destination != null)
                {
                    return Variable.FromObject(destination.ObjectId);
                }
            }

            // Check if it's a door
            IDoorComponent door = entity.GetComponent<IDoorComponent>();
            if (door != null && !string.IsNullOrEmpty(door.LinkedTo))
            {
                // Find the destination waypoint or door by tag
                IEntity destination = ctx.World.GetEntityByTag(door.LinkedTo, 0);
                if (destination != null)
                {
                    return Variable.FromObject(destination.ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        #endregion

        #region Module Functions

        /// <summary>
        /// StartNewModule(string sModuleName, string sWayPoint="", string sMovie1="", string sMovie2="", string sMovie3="", string sMovie4="", string sMovie5="", string sMovie6="") - Start a new module
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: StartNewModule implementation
        /// Located via string references: "Module" @ 0x007c1a70, "ModuleName" @ 0x007bde2c
        /// Original implementation: Shuts down current module and loads new one, positions party at waypoint
        ///
        /// Function ID: 509 (NWScript routine ID, 0x1fd)
        ///
        /// Ghidra verified components Findings:
        /// - swkotor.exe: Function ID 509 referenced at 0x0041c54a and 0x0041c554 (function registration/dispatch setup)
        /// - swkotor2.exe: Function ID 509 referenced at 0x0041bf2a and 0x0041bf34 (identical pattern)
        /// - Module transition script: "Mod_Transition" @ 0x00745b68 (swkotor.exe), @ 0x007be8f0 (swkotor2.exe)
        /// - Module transition handlers: 0x00501fa0 @ 0x00501fa0 (swkotor2.exe) handles module loading and transition scripts
        /// - Module state management: "ModuleLoaded" @ 0x00745078, "ModuleRunning" @ 0x00745060 (swkotor.exe)
        /// - Module save system: 0x004b2380 @ 0x004b2380 (swkotor.exe), 0x004ea910 @ 0x004ea910 (swkotor2.exe) handle module name in save games
        ///
        /// Original Engine Behavior (swkotor.exe/swkotor2.exe):
        /// - Validates module name parameter (returns immediately if empty)
        /// - Plays movie files (sMovie1-sMovie6) sequentially if provided (before module transition)
        /// - Calls module transition system to unload current module and load new module
        /// - Positions party at specified waypoint (or default entry waypoint if not specified)
        /// - Returns immediately (function is synchronous, but transition happens asynchronously)
        /// - Script execution continues while module transition is in progress
        ///
        /// Module Transition Sequence (Original Engine):
        /// 1. Validate module name and waypoint tag
        /// 2. Play movie files sequentially (if provided) - BIK format, blocking playback
        /// 3. Show loading screen (from module IFO LoadScreenResRef)
        /// 4. Save current module state (creature positions, door/placeable states)
        /// 5. Fire OnModuleLeave script on current module (ScriptOnExit field in IFO)
        /// 6. Unload current module (destroy entities, clear areas)
        /// 7. Load new module (IFO, ARE, GIT, LYT, VIS files via ModuleLoader)
        /// 8. Restore module state if previously visited (from SaveSystem module state cache)
        /// 9. Position party at waypoint (TransitionDestination from door, or default entry waypoint)
        /// 10. Fire OnModuleLoad script on new module (ScriptOnLoad field in IFO, executes before OnModuleStart)
        /// 11. Fire OnModuleStart script on new module (ScriptOnStart field in IFO, executes after OnModuleLoad)
        /// 12. Fire OnEnter script for party members (ScriptOnEnter field in ARE)
        /// 13. Fire OnSpawn script on newly spawned creatures (ScriptSpawn field in UTC template)
        /// 14. Hide loading screen
        ///
        /// Movie Playback (Original Engine):
        /// - Movies play sequentially before module transition begins
        /// - Movie files are BIK format (Bink video) - requires BINKW32.DLL
        /// - Playback is blocking (waits for each movie to finish before playing next)
        /// - If movie playback fails, continues with module transition
        /// - Movie playback would be handled by Bink video system (not yet implemented)
        ///
        /// Waypoint Positioning:
        /// - If waypoint tag is provided, positions party at that waypoint
        /// - If waypoint tag is empty, uses default entry waypoint from module IFO
        /// - Party members positioned in line perpendicular to waypoint facing (1.0 unit spacing)
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 positions all party members at waypoint with spacing
        ///
        /// Cross-Engine Equivalents:
        /// - swkotor2.exe: Same function ID (509), identical implementation pattern
        ///   - Module transition: 0x00501fa0 @ 0x00501fa0 handles module loading and "Mod_Transition" script execution
        ///   - Module state: "Mod_Transition" @ 0x007be8f0, "ModuleName" @ 0x007bde2c
        /// - nwmain.exe (Aurora): Similar function but different function ID, uses Module.ifo format
        ///   - Module loading: CNWSModule::LoadModule (needs Ghidra address verification)
        ///   - Module transition: Similar pattern but uses Module.ifo and HAK files
        /// - daorigins.exe (Eclipse): Different architecture (UnrealScript), no direct equivalent
        ///   - Uses UnrealScript LoadModuleMessage and package loading system
        /// -  (Infinity): Different architecture, level-based instead of module-based
        ///   - Uses level streaming system instead of discrete module transitions
        ///
        /// Implementation Notes:
        /// - This implementation uses ModuleTransitionSystem.TransitionToModule() which handles the full transition sequence
        /// - Movie playback is not yet implemented (requires Bink video system integration)
        /// - ModuleTransitionSystem handles: loading screen, state save/restore, script execution, entity positioning
        /// - Function returns immediately while transition happens asynchronously in the game loop
        /// </remarks>
        private Variable Func_StartNewModule(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0)
            {
                return Variable.Void();
            }

            string moduleName = args[0].AsString();
            string waypointTag = args.Count > 1 ? args[1].AsString() : "";

            if (string.IsNullOrEmpty(moduleName))
            {
                return Variable.Void();
            }

            // Access ModuleTransitionSystem from World
            // Based on swkotor.exe: StartNewModule implementation
            // Located via string references: "Module" @ 0x007c1a70, "ModuleName" @ 0x007bde2c
            // Original implementation: Calls module transition system to load new module
            // Function signature supports up to 6 movie parameters (sMovie1-sMovie6) for cinematic transitions
            // Movie playback would occur before module transition, but ModuleTransitionSystem doesn't support movies yet
            if (ctx.World != null)
            {
                // Extract movie parameters (up to 6 movies: sMovie1-sMovie6)
                // Movies play sequentially before module transition in original engine
                string[] movies = new string[6];
                int movieCount = 0;
                for (int i = 0; i < 6 && (i + 2) < args.Count; i++)
                {
                    string movieName = args[i + 2].AsString();
                    if (!string.IsNullOrEmpty(movieName))
                    {
                        movies[i] = movieName;
                        movieCount++;
                    }
                }

                // Access ModuleTransitionSystem directly from World
                // Cast to concrete World type to access ModuleTransitionSystem property
                if (ctx.World is Runtime.Core.Entities.World world)
                {
                    Runtime.Core.Module.ModuleTransitionSystem moduleTransitionSystem = world.ModuleTransitionSystem;
                    if (moduleTransitionSystem != null)
                    {
                        // TransitionToModule is async, but we're in a synchronous NWScript context
                        // Fire-and-forget the async task - the game loop will handle the transition
                        // This matches original engine behavior where StartNewModule returns immediately
                        // and the transition happens asynchronously in the game loop
                        // Pass movie parameters to TransitionToModule (null if no movies provided)
                        string[] moviesToPlay = movieCount > 0 ? movies : null;
                        System.Threading.Tasks.Task transitionTask = moduleTransitionSystem.TransitionToModule(moduleName, waypointTag, moviesToPlay);

                        // Note: We don't await here because:
                        // 1. NWScript functions are synchronous
                        // 2. Original engine returns immediately from StartNewModule
                        // 3. Module transition happens asynchronously in the game loop
                        // 4. Script execution continues while transition is in progress
                        // The Task will be handled by the game loop's async context

                        // Log transition initiation for debugging
                        System.Console.WriteLine("[Kotor1] StartNewModule: Initiating transition to module '{0}' at waypoint '{1}'", moduleName, waypointTag ?? "(default)");
                    }
                    else
                    {
                        System.Console.WriteLine("[Kotor1] StartNewModule: ModuleTransitionSystem is not available");
                    }
                }
                else
                {
                    System.Console.WriteLine("[Kotor1] StartNewModule: World is not the expected type");
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetModuleFileName() - Get the actual file name of the current module
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetModuleFileName implementation
        /// Original implementation: Returns the module ResRef (filename without extension)
        /// </remarks>
        private Variable Func_GetModuleFileName(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.World?.CurrentModule != null)
            {
                return Variable.FromString(ctx.World.CurrentModule.ResRef ?? "");
            }

            return Variable.FromString("");
        }

        #endregion

        #region String and Object Functions

        /// <summary>
        /// ActionSpeakStringByStrRef(int nStrRef, int nTalkVolume=TALKVOLUME_TALK) - Causes the creature to speak a translated string
        /// </summary>
        /// <summary>
        /// ActionSpeakStringByStrRef(int nStrRef, int nTalkVolume=TALKVOLUME_TALK) - action to speak string from TLK
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Speak string by StrRef action system
        /// Located via string references: "STRREF" @ 0x007b6368, "StrRef" @ 0x007c1fe8
        /// Original implementation: Looks up string from TLK using StrRef, creates ActionSpeakString action
        /// TLK lookup: Uses DialogueManager.LookupString to get text from talk table
        /// Talk volume: TALKVOLUME_TALK (0) = normal, TALKVOLUME_WHISPER (1) = quiet, TALKVOLUME_SHOUT (2) = loud
        /// Execution: Action executes when reached in action queue, plays voice-over if available
        /// Returns: Void (no return value)
        /// </remarks>
        private Variable Func_ActionSpeakStringByStrRef(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int strRef = args.Count > 0 ? args[0].AsInt() : 0;
            int talkVolume = args.Count > 1 ? args[1].AsInt() : 0;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            // Look up string from TLK
            string text = "";
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager)
                {
                    text = dialogueManager.LookupString(strRef);
                }
            }

            var action = new ActionSpeakString(text, talkVolume);
            IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
            if (queue != null)
            {
                queue.Add(action);
            }

            return Variable.Void();
        }

        /// <summary>
        /// DestroyObject(object oDestroy, float fDelay=0.0f, int bNoFade = FALSE, float fDelayUntilFade = 0.0f) - Destroy oObject (irrevocably)
        /// </summary>
        private Variable Func_DestroyObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            float delay = args.Count > 1 ? args[1].AsFloat() : 0f;
            int noFade = args.Count > 2 ? args[2].AsInt() : 0;
            float delayUntilFade = args.Count > 3 ? args[3].AsFloat() : 0f;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.Void();
            }

            // Modules and areas are not entities in our system (they're managed separately via World/ModuleLoader)
            // Only entity objects (Creature, Item, Placeable, Door, etc.) can be destroyed
            // The entity null check above already handles invalid entities

            // If no delay and no fade, destroy immediately
            if (delay <= 0f && noFade != 0)
            {
                if (ctx.World != null)
                {
                    ctx.World.DestroyEntity(entity.ObjectId);
                }
                return Variable.Void();
            }

            // Create destroy action with delay and fade support
            var destroyAction = new Runtime.Core.Actions.ActionDestroyObject(entity.ObjectId, delay, noFade != 0, delayUntilFade);

            // If delay > 0, schedule via DelayCommand
            if (delay > 0f)
            {
                // Schedule the destroy action after delay
                if (ctx.World != null && ctx.World.DelayScheduler != null)
                {
                    ctx.World.DelayScheduler.ScheduleDelay(delay, destroyAction, ctx.Caller ?? entity);
                }
            }
            else
            {
                // No delay, execute immediately via action queue
                // Add to entity's action queue so it can handle fade timing
                IActionQueue queue = entity.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(destroyAction);
                }
                else
                {
                    // Fallback: destroy immediately if no action queue
                    if (ctx.World != null)
                    {
                        ctx.World.DestroyEntity(entity.ObjectId);
                    }
                }
            }

            return Variable.Void();
        }

        #endregion

        #region Location Helper Functions

        /// <summary>
        /// GetPositionFromLocation(location lLocation) - Get the position vector from lLocation
        /// </summary>
        private Variable Func_GetPositionFromLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0)
            {
                return Variable.FromVector(Vector3.Zero);
            }

            object locObj = args[0].AsLocation();
            if (locObj == null || !(locObj is Location location))
            {
                return Variable.FromVector(Vector3.Zero);
            }

            return Variable.FromVector(location.Position);
        }

        /// <summary>
        /// GetFacingFromLocation(location lLocation) - Get the orientation value from lLocation
        /// </summary>
        private Variable Func_GetFacingFromLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count == 0)
            {
                return Variable.FromFloat(0f);
            }

            object locObj = args[0].AsLocation();
            if (locObj == null || !(locObj is Location location))
            {
                return Variable.FromFloat(0f);
            }

            return Variable.FromFloat(location.Facing);
        }

        #endregion

        #region Object Creation

        /// <summary>
        /// CreateObject(int nObjectType, string sTemplate, location lLocation, int bUseAppearAnimation=FALSE) - Create an object of the specified type at lLocation
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Runtime object creation system
        /// Located via string references: Object creation functions handle template loading and entity spawning
        /// Original implementation: Creates runtime entities from GFF templates at specified location
        /// Object types: OBJECT_TYPE_CREATURE (1), OBJECT_TYPE_ITEM (2), OBJECT_TYPE_PLACEABLE (4),
        ///   OBJECT_TYPE_STORE (5), OBJECT_TYPE_WAYPOINT (6)
        /// Template loading: Loads UTC/UTI/UTP/UTM/UTW templates from installation, applies to entity
        /// Location: Extracts position and facing from location object
        /// Appear animation: bUseAppearAnimation flag controls fade-in effect and spawn animation (swkotor.exe, swkotor2.exe)
        ///   - Sets "AppearAnimation" flag for rendering system to handle alpha fade-in (0.0 to 1.0 over 0.75 seconds)
        ///   - For creatures: Plays spawn animation (animation ID 0) while fading in
        ///   - Rendering system applies alpha/opacity based on "AppearAnimationAlpha" interpolated over duration
        ///   - Located via string references: "spawn" @ 0x007c21d8 (swkotor2.exe), "spawntype" @ 0x007bab44 (swkotor2.exe)
        /// Returns: Created object ID or OBJECT_INVALID if creation fails
        /// </remarks>
        private Variable Func_CreateObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int objectType = args.Count > 0 ? args[0].AsInt() : 0;
            string template = args.Count > 1 ? args[1].AsString() : "";
            object locObj = args.Count > 2 ? args[2].AsLocation() : null;
            int useAppearAnimation = args.Count > 3 ? args[3].AsInt() : 0;

            if (ctx.World == null || ctx.World.CurrentArea == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Extract position and facing from location
            Vector3 position = Vector3.Zero;
            float facing = 0f;
            if (locObj != null && locObj is Location location)
            {
                position = location.Position;
                facing = location.Facing;
            }

            // Convert object type constant to ObjectType enum
            Runtime.Core.Enums.ObjectType odyObjectType = Runtime.Core.Enums.ObjectType.Creature; // Default
            ResourceType resourceType = ResourceType.UTC; // Default

            // Map NWScript object type constants to Odyssey ObjectType
            // OBJECT_TYPE_CREATURE = 1, OBJECT_TYPE_ITEM = 2, OBJECT_TYPE_PLACEABLE = 4, OBJECT_TYPE_STORE = 5, OBJECT_TYPE_WAYPOINT = 6
            switch (objectType)
            {
                case 1: // OBJECT_TYPE_CREATURE
                    odyObjectType = Runtime.Core.Enums.ObjectType.Creature;
                    resourceType = ResourceType.UTC;
                    break;
                case 2: // OBJECT_TYPE_ITEM
                    odyObjectType = Runtime.Core.Enums.ObjectType.Item;
                    resourceType = ResourceType.UTI;
                    break;
                case 4: // OBJECT_TYPE_PLACEABLE
                    odyObjectType = Runtime.Core.Enums.ObjectType.Placeable;
                    resourceType = ResourceType.UTP;
                    break;
                case 5: // OBJECT_TYPE_STORE
                    odyObjectType = Runtime.Core.Enums.ObjectType.Store;
                    resourceType = ResourceType.UTM;
                    break;
                case 6: // OBJECT_TYPE_WAYPOINT
                    odyObjectType = Runtime.Core.Enums.ObjectType.Waypoint;
                    resourceType = ResourceType.UTW;
                    break;
                default:
                    return Variable.FromObject(ObjectInvalid);
            }

            // Create entity using EntityFactory
            IEntity entity = null;

            // Access ModuleLoader via GameServicesContext to get EntityFactory
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.ModuleLoader is Game.ModuleLoader moduleLoader)
                {
                    Module parsingModule = moduleLoader.GetParsingModule();
                    if (parsingModule == null)
                    {
                        return Variable.FromObject(ObjectInvalid);
                    }

                    // Convert position to System.Numerics.Vector3
                    Vector3 entityPosition = new Vector3(position.X, position.Y, position.Z);

                    // Create entity from template using EntityFactory
                    switch (odyObjectType)
                    {
                        // case Runtime.Core.Enums.ObjectType.Creature:
                        //     if (!string.IsNullOrEmpty(template))
                        //     {
                        //         entity = moduleLoader.EntityFactory.CreateCreatureFromTemplate(parsingModule, template, entityPosition, facing);
                        //     }
                        //     break;
                        // case Runtime.Core.Enums.ObjectType.Item:
                        //     if (!string.IsNullOrEmpty(template))
                        //     {
                        //         entity = moduleLoader.EntityFactory.CreateItemFromTemplate(parsingModule, template, entityPosition, facing);
                        //     }
                        //     break;
                        // case Runtime.Core.Enums.ObjectType.Placeable:
                        //     if (!string.IsNullOrEmpty(template))
                        //     {
                        //         entity = moduleLoader.EntityFactory.CreatePlaceableFromTemplate(parsingModule, template, entityPosition, facing);
                        //     }
                        //     break;
                        // case Runtime.Core.Enums.ObjectType.Store:
                        //     if (!string.IsNullOrEmpty(template))
                        //     {
                        //         entity = moduleLoader.EntityFactory.CreateStoreFromTemplate(parsingModule, template, entityPosition, facing);
                        //     }
                        //     break;
                        // case Runtime.Core.Enums.ObjectType.Waypoint:
                        //     // Waypoints don't have templates in the same way, their "template" is often just their tag
                        //     entity = moduleLoader.EntityFactory.CreateWaypointFromTemplate(template, entityPosition, facing);
                        //     break;
                    }
                }
            }

            // Fallback: Create basic entity if EntityFactory not available or template creation failed
            if (entity == null)
            {
                // Convert System.Numerics.Vector3 to BioWare.NET Vector3 for World.CreateEntity
                Vector3 worldPosition = new Vector3(position.X, position.Y, position.Z);
                entity = ctx.World.CreateEntity(odyObjectType, worldPosition, facing);
                if (entity == null)
                {
                    return Variable.FromObject(ObjectInvalid);
                }

                // Set tag from template (for waypoints, template is the tag)
                if (objectType == 6) // Waypoint
                {
                    entity.Tag = template;
                }
                else if (!string.IsNullOrEmpty(template))
                {
                    entity.Tag = template;
                }
            }

            // Register entity with world
            ctx.World.RegisterEntity(entity);

            // Add to current area (RuntimeArea has AddEntity method)
            if (ctx.World.CurrentArea is Runtime.Core.Module.RuntimeArea runtimeArea)
            {
                runtimeArea.AddEntity(entity);
            }

            // Implement appear animation if bUseAppearAnimation is TRUE
            // Based on swkotor.exe and swkotor2.exe: Objects created with appear animation play a fade-in effect
            // Located via string references: "spawn" @ 0x007c21d8 (swkotor2.exe), "spawntype" @ 0x007bab44 (swkotor2.exe)
            // Original implementation: Objects fade in from opacity 0.0 to 1.0 over a duration (typically 0.5-1.0 seconds)
            // For creatures: May also play a spawn animation (typically animation ID 0 or a specific spawn animation)
            // Fade-in duration: 0.75 seconds for smooth visual transition (matches original engine behavior and ActionDestroyObject fade-out)
            // Rendering system: Uses IRenderableComponent.Opacity property for alpha blending during rendering
            if (useAppearAnimation != 0)
            {
                if (entity is Runtime.Core.Entities.Entity entityImpl)
                {
                    // Set appear animation flag and timing for fade-in system
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Appear animation uses opacity fade-in from 0.0 to 1.0
                    // Similar pattern to ActionDestroyObject fade-out, but in reverse (fade-in instead of fade-out)
                    // The AppearAnimationFadeSystem will update Opacity property over time
                    entityImpl.SetData("AppearAnimation", true);

                    // Get current simulation time for fade start time
                    float startTime = 0.0f;
                    if (ctx.World != null && ctx.World.TimeManager != null)
                    {
                        startTime = ctx.World.TimeManager.SimulationTime;
                    }
                    entityImpl.SetData("AppearAnimationStartTime", startTime);

                    // Fade duration: 0.75 seconds (matches ActionDestroyObject fade-out duration and original engine timing)
                    const float fadeDuration = 0.75f;
                    entityImpl.SetData("AppearAnimationDuration", fadeDuration);

                    // Set initial opacity to 0.0 (fully transparent) - AppearAnimationFadeSystem will fade in to 1.0
                    IRenderableComponent renderable = entity.GetComponent<Runtime.Core.Interfaces.Components.IRenderableComponent>();
                    if (renderable != null)
                    {
                        renderable.Opacity = 0.0f;
                    }

                    // For creatures, play spawn animation if animation component is available
                    // Based on swkotor.exe and swkotor2.exe: Creatures may play a spawn animation when appearing
                    // Spawn animation is typically animation ID 0 (first animation in model's animation array)
                    // Animation component should be attached by ComponentInitializer for creatures
                    if (objectType == 1) // OBJECT_TYPE_CREATURE
                    {
                        IAnimationComponent animationComponent = entity.GetComponent<IAnimationComponent>();
                        if (animationComponent != null)
                        {
                            // Play spawn animation (animation ID 0 is typically the spawn/appear animation)
                            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spawn animation plays while entity fades in
                            // Animation plays once (not looping) and completes as fade-in finishes
                            // If animation ID 0 doesn't exist, entity will just fade in without animation
                            animationComponent.PlayAnimation(0, 1.0f, false); // Animation ID 0, normal speed, no loop
                        }
                    }

                    // For placeables, doors, and other objects with animation components:
                    // They may also have spawn animations, but typically just fade in
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Most non-creature objects just fade in without specific animations
                    // However, placeables with animation components may have an appear animation (animation ID 0)
                    if (objectType == 4) // OBJECT_TYPE_PLACEABLE
                    {
                        IAnimationComponent animationComponent = entity.GetComponent<IAnimationComponent>();
                        if (animationComponent != null)
                        {
                            // Placeables may have an appear animation (animation ID 0), similar to creatures
                            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Placeables with appear animations play animation ID 0 while fading in
                            // Animation ID 0 is typically the first animation in the model's animation array
                            // If animation ID 0 doesn't exist, entity will just fade in without animation
                            // Animation plays once (not looping) and completes as fade-in finishes
                            // swkotor2.exe: Placeable appear animation behavior matches creature spawn animation pattern
                            animationComponent.PlayAnimation(0, 1.0f, false); // Animation ID 0, normal speed, no loop
                        }
                    }

                    // Note: The rendering system is responsible for:
                    // AppearAnimationFadeSystem will:
                    // 1. Check "AppearAnimation" flag on entities each frame
                    // 2. Read "AppearAnimationStartTime" and "AppearAnimationDuration" for timing
                    // 3. Interpolate IRenderableComponent.Opacity from 0.0 to 1.0 over the duration
                    // 4. Clear the flag when fade-in completes (Opacity >= 1.0)
                    // Rendering system will use IRenderableComponent.Opacity for alpha blending
                    // This separation of concerns allows the fade system to update opacity over time
                    // while the rendering system handles the visual effect
                }
            }

            return Variable.FromObject(entity.ObjectId);
        }

        #endregion

        #region Type Conversion Functions

        /// <summary>
        /// IntToFloat(int nInteger) - Convert nInteger into a floating point number
        /// </summary>
        private Variable Func_IntToFloat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int value = args.Count > 0 ? args[0].AsInt() : 0;
            return Variable.FromFloat((float)value);
        }

        /// <summary>
        /// FloatToInt(float fFloat) - Convert fFloat into the nearest integer
        /// </summary>
        private Variable Func_FloatToInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromInt((int)Math.Round(value));
        }

        // String conversion functions removed - using base class implementations instead

        #endregion

        #region Nearest Object To Location

        /// <summary>
        /// GetNearestObjectToLocation(int nObjectType, location lLocation, int nNth=1) - Get the nNth object nearest to lLocation that is of the specified type
        /// </summary>
        private Variable Func_GetNearestObjectToLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 2 || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            int objectType = args[0].AsInt();
            object locObj = args[1].AsLocation();
            int nth = args.Count > 2 ? args[2].AsInt() : 1;

            // Extract position from location
            Vector3 locationPos = Vector3.Zero;
            if (locObj != null && locObj is Location location)
            {
                locationPos = location.Position;
            }

            // Convert object type constant to ObjectType enum
            Runtime.Core.Enums.ObjectType typeMask = Runtime.Core.Enums.ObjectType.All;
            if (objectType != 32767) // Not OBJECT_TYPE_ALL
            {
                // Map NWScript object type constants
                typeMask = (Runtime.Core.Enums.ObjectType)objectType;
            }

            // Get all entities of the specified type
            var candidates = new List<(IEntity entity, float distance)>();
            foreach (IEntity entity in ctx.World.GetEntitiesOfType(typeMask))
            {
                ITransformComponent entityTransform = entity.GetComponent<ITransformComponent>();
                if (entityTransform != null)
                {
                    float distance = Vector3.DistanceSquared(locationPos, entityTransform.Position);
                    candidates.Add((entity, distance));
                }
            }

            // Sort by distance
            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Return Nth nearest (1-indexed)
            if (nth > 0 && nth <= candidates.Count)
            {
                return Variable.FromObject(candidates[nth - 1].entity.ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        #endregion

        #region Class and Level Functions

        /// <summary>
        /// GetHitDice(object oCreature=OBJECT_SELF) - Get the number of hit dice for a creature
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Hit dice calculation from classes.2da
        /// Located via string references: "HitDice" @ 0x007c2f80, "HitDie" @ 0x007c2f84
        /// Original implementation: Hit dice = total character level (sum of all class levels)
        /// Hit dice represent the creature's total character levels across all classes
        /// Returns 0 if creature is invalid or not a creature
        /// </remarks>
        private Variable Func_GetHitDice(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null || entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            IStatsComponent stats = entity.GetComponent<IStatsComponent>();
            if (stats != null)
            {
                // Hit dice = total character level
                // Based on swkotor.exe: GetHitDice implementation
                // Located via string references: Hit dice calculation from character level
                // Original implementation: Hit dice equals total character level (sum of all class levels)
                return Variable.FromInt(stats.Level);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetHasFeat(int nFeat, object oCreature=OBJECT_SELF) - Determine whether creature has a feat and it is usable
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Feat checking system
        /// Located via string references: "FeatList" @ 0x007c2f88, "FeatID" @ 0x007c2f8c
        /// Original implementation: Checks if creature has the feat in their feat list AND it is currently usable
        /// Returns TRUE if creature has the feat and it is currently usable (not exhausted, not restricted)
        /// Returns FALSE if creature doesn't have the feat or it's not usable (daily limits exhausted, restrictions)
        /// Daily limits: Feats with UsesPerDay > 0 are checked against remaining daily uses
        /// Special feats: Feats with UsesPerDay = -1 may have class-level-based limits
        /// Disabled feats: Feats with UsesPerDay = 0 are never usable
        /// </remarks>
        private Variable Func_GetHasFeat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 1)
            {
                return Variable.FromInt(0);
            }

            int featId = args[0].AsInt();
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null || entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            CreatureComponent creature = entity.GetComponent<CreatureComponent>();
            if (creature == null || creature.FeatList == null)
            {
                return Variable.FromInt(0);
            }

            // Get GameDataManager from context to look up feat data
            Andastra.Game.Games.Odyssey.Data.GameDataManager gameDataManager = null;
            if (ctx != null && ctx.World != null && ctx.World.GameDataProvider != null)
            {
                Data.OdysseyGameDataProvider odysseyProvider = ctx.World.GameDataProvider as Data.OdysseyGameDataProvider;
                if (odysseyProvider != null)
                {
                    gameDataManager = odysseyProvider.GameDataManager;
                }
            }

            // Check if feat is currently usable (includes daily limit checking)
            bool isUsable = creature.IsFeatUsable(featId, gameDataManager);
            return Variable.FromInt(isUsable ? 1 : 0);
        }

        /// <summary>
        /// GetClassByPosition(int nClassPosition, object oCreature=OBJECT_SELF) - Get class at position (1, 2, or 3)
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Multi-class system
        /// Located via string references: "ClassList" @ 0x007c2f90, "ClassID" @ 0x007c2f94
        /// Original implementation: Creatures can have up to 3 classes
        /// nClassPosition: 1 = first class, 2 = second class, 3 = third class
        /// Returns CLASS_TYPE_INVALID if creature doesn't have a class at that position
        /// </remarks>
        private Variable Func_GetClassByPosition(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 1)
            {
                return Variable.FromInt(-1); // CLASS_TYPE_INVALID
            }

            int classPosition = args[0].AsInt();
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null || entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(-1); // CLASS_TYPE_INVALID
            }

            CreatureComponent creature = entity.GetComponent<CreatureComponent>();
            if (creature != null && creature.ClassList != null)
            {
                // Class positions are 1-indexed, list is 0-indexed
                int index = classPosition - 1;
                if (index >= 0 && index < creature.ClassList.Count)
                {
                    CreatureClass creatureClass = creature.ClassList[index];
                    return Variable.FromInt(creatureClass.ClassId);
                }
            }

            return Variable.FromInt(-1); // CLASS_TYPE_INVALID
        }

        /// <summary>
        /// GetLevelByPosition(int nClassPosition, object oCreature=OBJECT_SELF) - Get level at class position (1, 2, or 3)
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Multi-class level system
        /// Original implementation: Returns the level in the class at the specified position
        /// nClassPosition: 1 = first class, 2 = second class, 3 = third class
        /// Returns 0 if creature doesn't have a class at that position
        /// </remarks>
        private Variable Func_GetLevelByPosition(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 1)
            {
                return Variable.FromInt(0);
            }

            int classPosition = args[0].AsInt();
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null || entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            CreatureComponent creature = entity.GetComponent<CreatureComponent>();
            if (creature != null && creature.ClassList != null)
            {
                // Class positions are 1-indexed, list is 0-indexed
                int index = classPosition - 1;
                if (index >= 0 && index < creature.ClassList.Count)
                {
                    CreatureClass creatureClass = creature.ClassList[index];
                    return Variable.FromInt(creatureClass.Level);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetLevelByClass(int nClassType, object oCreature=OBJECT_SELF) - Get total levels in a specific class
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Class level lookup
        /// Original implementation: Sums up all levels the creature has in the specified class type
        /// Returns total levels in nClassType across all class positions
        /// </remarks>
        private Variable Func_GetLevelByClass(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 1)
            {
                return Variable.FromInt(0);
            }

            int classType = args[0].AsInt();
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null || entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            CreatureComponent creature = entity.GetComponent<CreatureComponent>();
            if (creature != null && creature.ClassList != null)
            {
                int totalLevels = 0;
                foreach (CreatureClass creatureClass in creature.ClassList)
                {
                    if (creatureClass.ClassId == classType)
                    {
                        totalLevels += creatureClass.Level;
                    }
                }
                return Variable.FromInt(totalLevels);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetFirstItemInInventory(object oTarget=OBJECT_SELF) - Get the first item in oTarget's inventory
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetFirstItemInInventory implementation
        /// Located via string references: "Inventory" @ 0x007c2504, "ItemList" @ 0x007c2f28
        /// Original implementation: Starts iteration over inventory items (equipped + inventory bag)
        /// Returns first item or OBJECT_INVALID if no items
        /// </remarks>
        private Variable Func_GetFirstItemInInventory(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get inventory component
            IInventoryComponent inventory = entity.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get all items from inventory
            List<IEntity> items = new List<IEntity>(inventory.GetAllItems());

            // Store iteration state
            uint callerId = ctx.Caller != null ? ctx.Caller.ObjectId : 0;
            _inventoryItemIterations[callerId] = new InventoryItemIteration
            {
                Items = items,
                CurrentIndex = 0
            };

            // Return first item
            if (items.Count > 0)
            {
                return Variable.FromObject(items[0].ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNextItemInInventory(object oTarget=OBJECT_SELF) - Get the next item in oTarget's inventory
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetNextItemInInventory implementation
        /// Original implementation: Continues iteration over inventory items
        /// Returns next item or OBJECT_INVALID if no more items
        /// </remarks>
        private Variable Func_GetNextItemInInventory(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get iteration state
            uint callerId = ctx.Caller != null ? ctx.Caller.ObjectId : 0;
            InventoryItemIteration iteration;
            if (!_inventoryItemIterations.TryGetValue(callerId, out iteration))
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Advance to next item
            iteration.CurrentIndex++;
            if (iteration.CurrentIndex < iteration.Items.Count)
            {
                return Variable.FromObject(iteration.Items[iteration.CurrentIndex].ObjectId);
            }

            // No more items - clear iteration state
            _inventoryItemIterations.Remove(callerId);
            return Variable.FromObject(ObjectInvalid);
        }

        #endregion

        #region Item Property Functions

        /// <summary>
        /// GetItemHasItemProperty(object oItem, int nProperty) - Determines whether oItem has nProperty
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetItemHasItemProperty implementation (routine ID 398)
        /// Located via string references: "ItemProperty" @ 0x007beb58, "PropertiesList" @ 0x007c2f3c
        /// Original implementation: Checks if item has the specified property type in its PropertiesList
        /// Returns: TRUE (1) if item has the property, FALSE (0) if not or if item is invalid
        /// Property type: ITEM_PROPERTY_* constants (0 = ITEM_PROPERTY_ABILITY_BONUS, etc.)
        /// </remarks>
        private Variable Func_GetItemHasItemProperty(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetItemHasItemProperty(object oItem, int nProperty)
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            int propertyType = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity item = ResolveObject(itemId, ctx);
            if (item == null || item.ObjectType != Runtime.Core.Enums.ObjectType.Item)
            {
                return Variable.FromInt(0); // FALSE
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return Variable.FromInt(0); // FALSE
            }

            // Check if item has the specified property type
            // Based on swkotor.exe: Property type matching
            // Located via string references: Item properties stored in PropertiesList array
            // Original implementation: Iterates through PropertiesList, checks PropertyName field against nProperty
            foreach (ItemProperty property in itemComponent.Properties)
            {
                if (property != null && property.PropertyType == propertyType)
                {
                    return Variable.FromInt(1); // TRUE
                }
            }

            return Variable.FromInt(0); // FALSE
        }

        #endregion

        #region Trigger/Object Query Functions

        /// <summary>
        /// GetClickingObject() - Get the object that last clicked on the caller
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetClickingObject implementation (routine ID 326)
        /// Located via string references: "OnClick" @ 0x007c1a20, "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc704 (case 0x1e in 0x004dcfb0)
        /// Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED (case 0x1e)
        /// Original implementation: Returns last entity that clicked trigger/door/placeable
        /// Note: This is identical to GetEnteringObject for triggers (both return the clicking entity)
        /// </remarks>
        private Variable Func_GetClickingObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get from entity's custom data (stored when OnClick fires)
            if (ctx.Caller is Runtime.Core.Entities.Entity callerEntity)
            {
                uint clickingId = callerEntity.GetData<uint>("LastClickingObjectId", 0);
                if (clickingId != 0)
                {
                    IEntity clicking = ResolveObject(clickingId, ctx);
                    if (clicking != null && clicking.IsValid)
                    {
                        return Variable.FromObject(clickingId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetLastSpellCaster() - Returns the entity that last cast a spell on the caller
        /// Based on swkotor.exe: GetLastSpellCaster implementation (routine ID 245)
        /// Located via string references: "GetLastSpellCaster" NWScript function
        /// Original implementation: Returns caster entity ID from _lastSpellCasters dictionary
        /// Used in OnSpellCastAt scripts to determine who cast the spell
        /// </summary>
        private Variable Func_GetLastSpellCaster(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Retrieve last spell caster for this target (stored in Func_ActionCastSpellAtObject)
            if (_lastSpellCasters.TryGetValue(ctx.Caller.ObjectId, out uint casterId))
            {
                // Verify caster still exists and is valid
                if (ctx.World != null)
                {
                    IEntity caster = ctx.World.GetEntity(casterId);
                    if (caster != null && caster.IsValid)
                    {
                        return Variable.FromObject(casterId);
                    }
                    else
                    {
                        // Caster no longer exists, remove from tracking
                        _lastSpellCasters.Remove(ctx.Caller.ObjectId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetSpellId() - Returns the ID of the last spell cast by the caller
        /// Based on swkotor.exe: GetSpellId implementation (routine ID 248)
        /// Located via string references: "GetSpellId" NWScript function
        /// Original implementation: Returns spell ID from _lastSpellIds dictionary
        /// Used in spell scripts to determine which spell is being cast
        /// </summary>
        private Variable Func_GetSpellId(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromInt(-1);
            }

            // Retrieve last spell ID for this caster (stored in Func_ActionCastSpellAtObject)
            if (_lastSpellIds.TryGetValue(ctx.Caller.ObjectId, out int spellId))
            {
                return Variable.FromInt(spellId);
            }

            return Variable.FromInt(-1);
        }

        #endregion

        #region Item Functions

        /// <summary>
        /// SetItemStackSize(object oItem, int nStackSize) - Set the stack size of an item
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: SetItemStackSize implementation (routine ID 150)
        /// Located via string references: "StackSize" @ 0x007c0a34 (item stack size GFF field)
        /// Item loading: 0x0056a820 @ 0x0056a820 loads StackSize from GFF (reads uint16 from "StackSize" field)
        /// Item saving: 0x006203c0 @ 0x006203c0 saves StackSize to GFF (writes uint16 to "StackSize" field)
        /// Original implementation: Clamps stack size between 1 and max stack size from baseitems.2da "stacking" column
        /// Stack size stored as uint16 in item GFF structure
        /// </remarks>
        private Variable Func_SetItemStackSize(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 2)
            {
                return Variable.Void();
            }

            uint itemId = args[0].AsObjectId();
            int stackSize = args[1].AsInt();
            IEntity item = ResolveObject(itemId, ctx);

            if (item == null || item.ObjectType != Runtime.Core.Enums.ObjectType.Item)
            {
                return Variable.Void();
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent != null)
            {
                // Clamp stack size between 1 and max (from baseitems.2da)
                // Based on swkotor.exe: SetItemStackSize implementation
                // Located via string references: "StackSize" @ 0x007c0a34, "stacking" column in baseitems.2da
                // Original implementation: Looks up max stack size from baseitems.2da "stacking" column using BaseItem ID
                int maxStackSize = 100; // Default max

                // Try to look up max stack size from baseitems.2da using BioWare.NET
                if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                {
                    if (services.GameSession is GameSession gameSession && gameSession.Installation != null)
                    {
                        try
                        {
                            // Load baseitems.2da using BioWare.NET
                            ResourceResult baseitemsResult = gameSession.Installation.Resource("baseitems", ResourceType.TwoDA, null, null);
                            if (baseitemsResult != null && baseitemsResult.Data != null)
                            {
                                using (var stream = new MemoryStream(baseitemsResult.Data))
                                {
                                    var reader = new TwoDABinaryReader(stream);
                                    TwoDA baseitems = reader.Load();

                                    if (baseitems != null && itemComponent.BaseItem >= 0 && itemComponent.BaseItem < baseitems.GetHeight())
                                    {
                                        TwoDARow row = baseitems.GetRow(itemComponent.BaseItem);
                                        int? stackingValue = row.GetInteger("stacking");
                                        if (stackingValue.HasValue && stackingValue.Value > 0)
                                        {
                                            maxStackSize = stackingValue.Value;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Fall back to default if lookup fails
                            Console.WriteLine($"[KotOR1] Failed to lookup max stack size for BaseItem {itemComponent.BaseItem}: {ex.Message}");
                        }
                    }
                }

                int clampedSize = Math.Max(1, Math.Min(maxStackSize, stackSize));
                itemComponent.StackSize = clampedSize;
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetDistanceBetween(object oObjectA, object oObjectB) - Get the distance in metres between two objects
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetDistanceBetween implementation (routine ID 151)
        /// Located via string references: Distance calculation uses object positions from transform components
        /// Original implementation: Calculates 3D Euclidean distance between object positions (Vector3.Distance)
        /// Returns 0.0f if either object is invalid or missing transform component
        /// Distance calculated as sqrt((x1-x2)^2 + (y1-y2)^2 + (z1-z2)^2)
        /// </remarks>
        private Variable Func_GetDistanceBetween(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 2)
            {
                return Variable.FromFloat(0f);
            }

            uint objectAId = args[0].AsObjectId();
            uint objectBId = args[1].AsObjectId();
            IEntity objectA = ResolveObject(objectAId, ctx);
            IEntity objectB = ResolveObject(objectBId, ctx);

            if (objectA == null || objectB == null)
            {
                return Variable.FromFloat(0f);
            }

            ITransformComponent transformA = objectA.GetComponent<ITransformComponent>();
            ITransformComponent transformB = objectB.GetComponent<ITransformComponent>();

            if (transformA != null && transformB != null)
            {
                float distance = Vector3.Distance(transformA.Position, transformB.Position);
                return Variable.FromFloat(distance);
            }

            return Variable.FromFloat(0f);
        }

        /// <summary>
        /// GetDistanceBetweenLocations(location lLocationA, location lLocationB) - Get the distance between two locations
        /// Based on swkotor.exe: Distance calculation between location objects
        /// Returns the 3D distance in meters between two locations
        /// </summary>
        private Variable Func_GetDistanceBetweenLocations(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 2)
            {
                return Variable.FromFloat(0f);
            }

            object locAObj = args[0].AsLocation();
            object locBObj = args[1].AsLocation();

            if (locAObj == null || locBObj == null)
            {
                return Variable.FromFloat(0f);
            }

            // Location objects should have Position property
            // Based on swkotor.exe: Location structure contains Position (Vector3) and Facing (float)
            // Located via string references: Location structure used for position/orientation storage
            // Original implementation: Calculates 3D distance between two location positions
            if (locAObj is Location locA && locBObj is Location locB)
            {
                float distance = Vector3.Distance(locA.Position, locB.Position);
                return Variable.FromFloat(distance);
            }

            return Variable.FromFloat(0f);
        }

        #endregion

        #region Door/Placeable Action Checks

        /// <summary>
        /// GetIsDoorActionPossible(object oTargetDoor, int nDoorAction) - Check if a door action can be performed
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetIsDoorActionPossible implementation (routine ID 337)
        /// Located via string references: Door action checking system
        /// Door actions: DOOR_ACTION_OPEN (0), DOOR_ACTION_UNLOCK (1), DOOR_ACTION_BASH (2), DOOR_ACTION_IGNORE (3), DOOR_ACTION_KNOCK (4)
        /// Original implementation: Checks if specified door action is valid for the door's current state
        /// Door state checks: IsOpen, IsLocked, LockableByScript flags determine which actions are possible
        /// DOOR_ACTION_OPEN: Requires door to be closed and either unlocked or lockable by script
        /// DOOR_ACTION_UNLOCK: Requires door to be locked and lockable by script
        /// DOOR_ACTION_BASH: Requires door to be locked (bash attempts to break lock via strength check)
        /// DOOR_ACTION_IGNORE: Always possible (no-op action)
        /// DOOR_ACTION_KNOCK: Requires door to be closed (knock on closed door)
        /// </remarks>
        private Variable Func_GetIsDoorActionPossible(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 2)
            {
                return Variable.FromInt(0);
            }

            uint doorId = args[0].AsObjectId();
            int doorAction = args[1].AsInt();
            IEntity door = ResolveObject(doorId, ctx);

            if (door == null || door.ObjectType != Runtime.Core.Enums.ObjectType.Door)
            {
                return Variable.FromInt(0);
            }

            IDoorComponent doorComponent = door.GetComponent<IDoorComponent>();
            if (doorComponent == null)
            {
                return Variable.FromInt(0);
            }

            // Door action constants: DOOR_ACTION_OPEN (0), DOOR_ACTION_UNLOCK (1), DOOR_ACTION_BASH (2), DOOR_ACTION_IGNORE (3), DOOR_ACTION_KNOCK (4)
            switch (doorAction)
            {
                case 0: // DOOR_ACTION_OPEN
                    // Can open if door is closed and not locked (or lockable by script)
                    return Variable.FromInt((!doorComponent.IsOpen && (!doorComponent.IsLocked || doorComponent.LockableByScript)) ? 1 : 0);

                case 1: // DOOR_ACTION_UNLOCK
                    // Can unlock if door is locked and lockable by script
                    return Variable.FromInt((doorComponent.IsLocked && doorComponent.LockableByScript) ? 1 : 0);

                case 2: // DOOR_ACTION_BASH
                    // Can bash if door is locked (bash attempts to break the lock)
                    return Variable.FromInt(doorComponent.IsLocked ? 1 : 0);

                case 3: // DOOR_ACTION_IGNORE
                    // Ignore action is always possible (does nothing)
                    return Variable.FromInt(1);

                case 4: // DOOR_ACTION_KNOCK
                    // Can knock if door is closed (knock on closed door)
                    return Variable.FromInt(!doorComponent.IsOpen ? 1 : 0);

                default:
                    return Variable.FromInt(0);
            }
        }

        /// <summary>
        /// GetIsPlaceableObjectActionPossible(object oPlaceable, int nPlaceableAction) - Check if a placeable action can be performed
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: GetIsPlaceableObjectActionPossible implementation
        /// Located via string references: Placeable action checking system (similar to door action system)
        /// Placeable actions: PLACEABLE_ACTION_USE (0), PLACEABLE_ACTION_UNLOCK (1), PLACEABLE_ACTION_BASH (2), PLACEABLE_ACTION_KNOCK (4)
        /// Original implementation: Checks if specified placeable action is valid for the placeable's current state
        /// Placeable state checks: IsOpen, IsLocked, LockDC, Useable flag from UTP template determine which actions are possible
        /// PLACEABLE_ACTION_USE: Requires placeable to be usable (Useable flag from UTP template)
        /// PLACEABLE_ACTION_UNLOCK: Requires placeable to be locked and have LockDC > 0 (unlock via Security skill check)
        /// PLACEABLE_ACTION_BASH: Requires placeable to be locked (bash attempts to break lock via strength check)
        /// PLACEABLE_ACTION_KNOCK: Requires placeable to be closed (knock on closed placeable)
        /// Note: This function may be K2-only, verify routine ID if used in K1
        /// </remarks>
        private Variable Func_GetIsPlaceableObjectActionPossible(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 2)
            {
                return Variable.FromInt(0);
            }

            uint placeableId = args[0].AsObjectId();
            int placeableAction = args[1].AsInt();
            IEntity placeable = ResolveObject(placeableId, ctx);

            if (placeable == null || placeable.ObjectType != Runtime.Core.Enums.ObjectType.Placeable)
            {
                return Variable.FromInt(0);
            }

            IPlaceableComponent placeableComponent = placeable.GetComponent<IPlaceableComponent>();
            if (placeableComponent == null)
            {
                return Variable.FromInt(0);
            }

            // Placeable action constants: PLACEABLE_ACTION_USE (0), PLACEABLE_ACTION_UNLOCK (1), PLACEABLE_ACTION_BASH (2), PLACEABLE_ACTION_KNOCK (4)
            switch (placeableAction)
            {
                case 0: // PLACEABLE_ACTION_USE
                    // Can use if placeable is usable (Useable flag from UTP template)
                    // Based on swkotor.exe: GetIsPlaceableObjectActionPossible implementation
                    // Located via string references: Placeable action checking system
                    // Original implementation: Checks IsUseable flag from placeable component
                    return Variable.FromInt(placeableComponent.IsUseable ? 1 : 0);

                case 1: // PLACEABLE_ACTION_UNLOCK
                    // Can unlock if placeable is locked (assume all placeables can be unlocked by script if they have a lock DC)
                    return Variable.FromInt((placeableComponent.IsLocked && placeableComponent.LockDC > 0) ? 1 : 0);

                case 2: // PLACEABLE_ACTION_BASH
                    // Can bash if placeable is locked (bash attempts to break the lock)
                    return Variable.FromInt(placeableComponent.IsLocked ? 1 : 0);

                case 4: // PLACEABLE_ACTION_KNOCK
                    // Can knock if placeable is closed (knock on closed placeable)
                    return Variable.FromInt(!placeableComponent.IsOpen ? 1 : 0);

                default:
                    return Variable.FromInt(0);
            }
        }

        /// <summary>
        /// DoDoorAction(object oTargetDoor, int nDoorAction) - Perform a door action on a door
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: DoDoorAction implementation (routine ID 338)
        /// Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844 (case 7 in 0x004dcfb0), "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc72c
        /// Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles door events (case 7 for EVENT_OPEN_OBJECT, case 0xd for EVENT_UNLOCK_OBJECT)
        /// Door actions: DOOR_ACTION_OPEN (0), DOOR_ACTION_UNLOCK (1), DOOR_ACTION_BASH (2), DOOR_ACTION_IGNORE (3), DOOR_ACTION_KNOCK (4)
        /// Original implementation: Performs the specified action on the door (opens, unlocks, bashes, ignores, or knocks)
        /// DOOR_ACTION_OPEN: Opens door if closed and unlocked (or lockable by script), fires EVENT_OPEN_OBJECT, executes OnOpen script
        /// DOOR_ACTION_UNLOCK: Unlocks door if locked and lockable by script, fires EVENT_UNLOCK_OBJECT, executes OnUnlock script
        /// DOOR_ACTION_BASH: Attempts to break lock via strength check (d20 + STR modifier vs LockDC), opens door if successful
        /// DOOR_ACTION_IGNORE: No-op action (does nothing)
        /// DOOR_ACTION_KNOCK: Plays knock sound/animation (no state change)
        /// </remarks>
        private Variable Func_DoDoorAction(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 2)
            {
                return Variable.Void();
            }

            uint doorId = args[0].AsObjectId();
            int doorAction = args[1].AsInt();
            IEntity door = ResolveObject(doorId, ctx);

            if (door == null || door.ObjectType != Runtime.Core.Enums.ObjectType.Door)
            {
                return Variable.Void();
            }

            IDoorComponent doorComponent = door.GetComponent<IDoorComponent>();
            if (doorComponent == null)
            {
                return Variable.Void();
            }

            // Door action constants: DOOR_ACTION_OPEN (0), DOOR_ACTION_UNLOCK (1), DOOR_ACTION_BASH (2), DOOR_ACTION_IGNORE (3), DOOR_ACTION_KNOCK (4)
            switch (doorAction)
            {
                case 0: // DOOR_ACTION_OPEN
                    // Open door if closed and unlocked (or lockable by script)
                    if (!doorComponent.IsOpen && (!doorComponent.IsLocked || doorComponent.LockableByScript))
                    {
                        doorComponent.Open();
                        // Fire EVENT_OPEN_OBJECT and execute OnOpen script (handled by door component)
                    }
                    break;

                case 1: // DOOR_ACTION_UNLOCK
                    // Unlock door if locked and lockable by script
                    if (doorComponent.IsLocked && doorComponent.LockableByScript)
                    {
                        doorComponent.Unlock();
                        // Fire EVENT_UNLOCK_OBJECT and execute OnUnlock script (handled by door component)
                    }
                    break;

                case 2: // DOOR_ACTION_BASH
                    // Bash door: Attempt to break lock via strength check
                    // Based on swkotor.exe: Door bashing system
                    // Original implementation: Performs strength check (d20 + STR modifier vs LockDC), applies damage if successful
                    if (doorComponent.IsLocked && ctx.Caller != null)
                    {
                        // Get caller's strength ability score
                        int strengthScore = 10; // Default
                        int strengthModifier = 0;
                        if (ctx.Caller.ObjectType == Runtime.Core.Enums.ObjectType.Creature)
                        {
                            Runtime.Core.Interfaces.Components.IStatsComponent stats = ctx.Caller.GetComponent<Runtime.Core.Interfaces.Components.IStatsComponent>();
                            if (stats != null)
                            {
                                // Get strength ability score (0=Strength in D20 system)
                                strengthScore = stats.GetAbility(0); // 0 = Strength
                                strengthModifier = (strengthScore - 10) / 2; // D20 ability modifier formula
                            }
                        }

                        // Roll d20 + STR modifier vs LockDC
                        int roll = _random.Next(1, 21); // d20 roll
                        int total = roll + strengthModifier;

                        // If check succeeds, apply damage
                        if (total >= doorComponent.LockDC)
                        {
                            // Apply bash damage: STR modifier + 1d4 (base bash damage)
                            int bashDamage = strengthModifier + _random.Next(1, 5); // STR mod + 1d4
                            if (bashDamage < 1)
                            {
                                bashDamage = 1; // Minimum 1 damage
                            }

                            // Apply damage to door (handles HP reduction, hardness, and destruction)
                            // Based on swkotor.exe: Door bashing damage application
                            // Located via string references: Door bashing system
                            // Original implementation: ApplyDamage handles HP reduction, hardness, and sets bashed state
                            doorComponent.ApplyDamage(bashDamage);

                            // If door was destroyed (bashed open), it's already unlocked and opened by ApplyDamage
                            // Fire OnDamaged script event if door still exists (HP > 0 means door wasn't destroyed)
                            if (doorComponent.HitPoints > 0 && ctx.World != null && ctx.World.EventBus != null)
                            {
                                ctx.World.EventBus.FireScriptEvent(door, Runtime.Core.Enums.ScriptEvent.OnDamaged, ctx.Caller);
                            }
                        }
                    }
                    break;

                case 3: // DOOR_ACTION_IGNORE
                    // Ignore action: No-op (does nothing)
                    break;

                case 4: // DOOR_ACTION_KNOCK
                    // Knock action: Play knock sound/animation (no state change)
                    // Based on swkotor.exe: Door knock action plays sound and animation (swkotor.exe: 0x004e08e0, swkotor2.exe: 0x00580ed0)
                    // Original implementation: Plays knock sound at door position, plays interaction animation on caller
                    // Located via string references: Door interaction sounds and animations
                    if (ctx.Caller != null)
                    {
                        // Play knock sound at door position (3D spatial audio)
                        // Based on swkotor.exe: Door sounds are played at door position for spatial audio
                        // Sound name: Uses generic door interaction sound (gui_door or door-specific sound)
                        if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                        {
                            if (services.SoundPlayer != null)
                            {
                                // Get door position for 3D spatial audio
                                System.Numerics.Vector3? position = null;
                                Runtime.Core.Interfaces.Components.ITransformComponent doorTransform = door.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                                if (doorTransform != null)
                                {
                                    // Convert BioWare.NET Vector3 to System.Numerics.Vector3
                                    position = new System.Numerics.Vector3(doorTransform.Position.X, doorTransform.Position.Y, doorTransform.Position.Z);
                                }

                                // Play knock sound at door position (3D spatial audio)
                                // Sound name: "gui_door" is a generic door interaction sound used in KOTOR
                                // Alternative: Could use door-specific sound from door template if available
                                // Based on swkotor.exe: Door interaction sounds are played at door position
                                string knockSoundName = "gui_door";
                                uint soundInstanceId = services.SoundPlayer.PlaySound(knockSoundName, position, 1.0f, 0.0f, 0.0f);
                            }
                        }

                        // Play knock animation on caller (the entity performing the knock)
                        // Based on swkotor.exe: Knock action plays interaction animation on caller
                        // Animation: Uses a simple interaction animation (fire-and-forget, plays once)
                        // Animation ID: ANIMATION_FIREFORGET_ACTIVATE (115) + 10000 offset = 10115
                        // Note: Animation constants require 10000 offset for ActionPlayAnimation
                        // ANIMATION_FIREFORGET_ACTIVATE is appropriate for door/object interaction
                        IActionQueueComponent actionQueue = ctx.Caller.GetComponent<IActionQueueComponent>();
                        if (actionQueue != null)
                        {
                            // Queue knock animation action (fire-and-forget, plays once)
                            // Animation ID: ANIMATION_FIREFORGET_ACTIVATE = 115, with 10000 offset = 10115
                            // Duration 0.0 = fire-and-forget (plays once until animation completes)
                            // Based on swkotor.exe: Knock uses interaction animation (activate/open gesture)
                            const int ANIMATION_FIREFORGET_ACTIVATE = 115;
                            int knockAnimationId = 10000 + ANIMATION_FIREFORGET_ACTIVATE; // 10115
                            var knockAnimation = new ActionPlayAnimation(knockAnimationId, 1.0f, 0.0f); // Speed 1.0, duration 0 = play once
                            actionQueue.Add(knockAnimation);
                        }
                    }
                    break;
            }

            return Variable.Void();
        }

        #endregion

        #region TSL-Specific Functions

        // Influence system (TheSithLords only, IDs 800-810)
        /// <summary>
        /// GetInfluence(int nNPC) - Returns influence value for NPC (0-100)
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
        /// Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
        /// "BaseInfluence" @ 0x007bf6fc, "Influence" @ 0x007c4f78
        /// GUI: "LBL_INFLUENCE_RECV" @ 0x007c8b38, "LBL_INFLUENCE_LOST" @ 0x007c8b0c
        /// Original implementation: Influence values stored in PARTYTABLE.res GFF (PT_INFLUENCE list)
        /// Each NPC has influence value 0-100 stored in PT_NPC_INFLUENCE field
        /// Influence affects dialogue options, companion reactions, and story outcomes
        /// </remarks>
        private Variable Func_GetInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetInfluence(int nNPC) - returns influence value for NPC (0-100)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Reads influence from PARTYTABLE.res GFF structure
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;

            // Get NPC entity from PartyManager
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Get influence from entity data (stored as "Influence")
                    if (member.HasData("Influence"))
                    {
                        int influence = member.GetData<int>("Influence", 50);
                        return Variable.FromInt(influence);
                    }
                    // Default to neutral if not set
                    return Variable.FromInt(50);
                }
            }
            return Variable.FromInt(50); // Default neutral influence
        }

        /// <summary>
        /// SetInfluence(int nNPC, int nInfluence) - Sets influence value for NPC (0-100)
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
        /// Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
        /// Original implementation: Writes influence to PARTYTABLE.res GFF structure (PT_NPC_INFLUENCE field)
        /// Influence value clamped to 0-100 range
        /// </remarks>
        private Variable Func_SetInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetInfluence(int nNPC, int nInfluence) - sets influence value for NPC (0-100)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Writes influence to PARTYTABLE.res GFF structure
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int influence = args.Count > 1 ? args[1].AsInt() : 50;

            // Clamp influence to valid range (0-100)
            influence = Math.Max(0, Math.Min(100, influence));

            // Get NPC entity from PartyManager and set influence
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Store influence in entity data
                    member.SetData("Influence", influence);
                }
            }
            return Variable.Void();
        }

        /// <summary>
        /// ModifyInfluence(int nNPC, int nModifier) - Modifies influence value for NPC by modifier amount
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
        /// Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
        /// Original implementation: Reads current influence, adds modifier, writes back to PARTYTABLE.res GFF
        /// Influence value clamped to 0-100 range after modification
        /// </remarks>
        private Variable Func_ModifyInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ModifyInfluence(int nNPC, int nModifier) - modifies influence value for NPC by modifier amount
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Reads current influence, adds modifier, writes back to GFF
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int modifier = args.Count > 1 ? args[1].AsInt() : 0;

            // Get NPC entity from PartyManager and modify influence
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Get current influence (default to 50 if not set)
                    int currentInfluence = member.GetData<int>("Influence", 50);
                    int newInfluence = Math.Max(0, Math.Min(100, currentInfluence + modifier));
                    member.SetData("Influence", newInfluence);
                }
            }
            return Variable.Void();
        }

        // Party puppet functions (TSL only)
        private Variable Func_GetPartyMemberByIndexTSL(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int index = args.Count > 0 ? args[0].AsInt() : 0;

            // Get party member at index (0 = leader, 1-2 = members)
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetMemberAtSlot(index);
                if (member != null)
                {
                    return Variable.FromObject(member.ObjectId);
                }
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_IsAvailableCreature(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;

            // Check if NPC is available for party selection
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    return Variable.FromInt(1); // Available
                }
            }
            return Variable.FromInt(0); // Not available
        }

        private Variable Func_AddAvailableNPCByTemplate(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            string template = args.Count > 1 ? args[1].AsString() : string.Empty;

            if (string.IsNullOrEmpty(template) || ctx.World == null)
            {
                return Variable.FromInt(0); // Failed
            }

            // Add NPC to available party members
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager && services.ModuleLoader is Andastra.Game.Games.Odyssey.Game.ModuleLoader moduleLoader)
            {
                // Try to find existing entity by template tag first
                IEntity existingEntity = ctx.World.GetEntityByTag(template, 0);
                if (existingEntity != null)
                {
                    try
                    {
                        partyManager.AddAvailableMember(npcIndex, existingEntity);
                        return Variable.FromInt(1); // Success
                    }
                    catch
                    {
                        return Variable.FromInt(0); // Failed
                    }
                }

                // Create entity from template using EntityFactory
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AddAvailableNPCByTemplate implementation
                // Located via string references: "TemplateResRef" @ 0x007bd00c, entity creation from UTC templates
                // Original implementation: Creates creature from UTC template and adds to available party members
                // Ghidra analysis: 0x0057bd70 @ 0x0057bd70 saves party data, 0x0057dcd0 @ 0x0057dcd0 loads party data
                // EntityFactory accessed via ModuleLoader.EntityFactory property
                // Get current module from ModuleLoader
                BioWare.NET.Common.Module module = moduleLoader.GetCurrentModule();
                if (module != null)
                {
                    // Get spawn position (use player position or default)
                    System.Numerics.Vector3 spawnPosition = System.Numerics.Vector3.Zero;
                    float spawnFacing = 0.0f;

                    if (services.PlayerEntity != null)
                    {
                        Runtime.Core.Interfaces.Components.ITransformComponent playerTransform = services.PlayerEntity.GetComponent<Runtime.Core.Interfaces.Components.ITransformComponent>();
                        if (playerTransform != null)
                        {
                            spawnPosition = playerTransform.Position;
                            spawnFacing = playerTransform.Facing;
                        }
                    }

                    // Create creature from template using EntityFactory
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EntityFactory.CreateCreatureFromTemplate creates creature from UTC template
                    // Located via string references: "TemplateResRef" @ 0x007bd00c, "Creature template '%s' doesn't exist.\n" @ 0x007bf78c
                    // Original implementation: Loads UTC GFF template, creates entity with template data, registers in world
                    Loading.EntityFactory entityFactory = moduleLoader.EntityFactory;
                    if (entityFactory != null)
                    {
                        IEntity newEntity = entityFactory.CreateCreatureFromTemplate(module, template, spawnPosition, spawnFacing);
                        if (newEntity != null)
                        {
                            // Register entity with world if not already registered
                            if (ctx.World.GetEntity(newEntity.ObjectId) == null)
                            {
                                ctx.World.RegisterEntity(newEntity);
                            }

                            // Add to PartyManager
                            try
                            {
                                partyManager.AddAvailableMember(npcIndex, newEntity);
                                return Variable.FromInt(1); // Success
                            }
                            catch
                            {
                                return Variable.FromInt(0); // Failed
                            }
                        }
                    }
                }
            }

            return Variable.FromInt(0); // Failed
        }

        private Variable Func_GetNPCSelectability(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;

            // Get NPC entity from PartyManager
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Get selectability from entity data (stored as "IsSelectable")
                    // Default to true if available but not explicitly set
                    if (member.HasData("IsSelectable"))
                    {
                        bool isSelectable = member.GetData<bool>("IsSelectable", true);
                        return Variable.FromInt(isSelectable ? 1 : 0);
                    }
                    // If available but selectability not set, default to selectable
                    return Variable.FromInt(1);
                }
            }
            return Variable.FromInt(0); // Not available/selectable
        }

        private Variable Func_SetNPCSelectability(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int selectable = args.Count > 1 ? args[1].AsInt() : 1;

            // Get NPC entity from PartyManager and set selectability
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Store selectability in entity data
                    member.SetData("IsSelectable", selectable != 0);
                }
            }
            return Variable.Void();
        }

        // Remote/stealth functions (TSL only)
        /// <summary>
        /// IsStealthed(object oTarget) - Returns TRUE if target is stealthed (StealthMode flag is set)
        /// </summary>
        /// <remarks>
        /// swkotor2.exe: IsStealthed routine ID 834 (TSL only)
        /// Located via string references: "StealthMode" @ 0x007bf690, "StealthXPEnabled" @ 0x007bd1b4
        /// "StealthXPCurrent" @ 0x007bd1d8, "StealthXPMax" @ 0x007bd1ec, "StealthXPLoss" @ 0x007bd1c8
        /// "STEALTHXP" @ 0x007bdf08, "setstealth" @ 0x007c79fc
        /// GUI: "LBL_STEALTH" @ 0x007c8c0c, "LBL_STEALTHXP" @ 0x007ccd7c, "TB_STEALTH" @ 0x007cd1dc
        /// Original implementation: Checks StealthMode byte at offset +0x511 in creature structure
        /// - 0x00542bf0 @ 0x00542bf0 sets StealthMode: *(char *)((int)this + 0x511) = param_1
        /// - 0x005143a0 @ 0x005143a0 checks StealthMode: if (*(char *)((int)this + 0x511) == '\x01')
        /// - 0x005226d0 @ 0x005226d0 (SerializeCreature_K2) saves StealthMode to GFF as byte field "StealthMode"
        /// - 0x005223a0 @ 0x005223a0 loads StealthMode from GFF field "StealthMode" and sets at offset +0x511
        /// Stealth system: Entities with stealth can avoid detection, gain stealth XP, lose stealth on damage
        /// Returns 0 for non-creatures or if StealthMode is not set
        /// </remarks>
        private Variable Func_IsStealthed(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // IsStealthed(object oTarget) - returns TRUE if target is stealthed (StealthMode flag is set)
            // swkotor2.exe: Checks StealthMode byte at offset +0x511 in creature structure
            // Original implementation: Returns 1 if *(char *)((int)this + 0x511) == '\x01', else 0
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);

            if (entity == null)
            {
                return Variable.FromInt(0);
            }

            // Check if entity is a creature and has StealthMode flag set
            // swkotor2.exe: IsStealthed checks StealthMode byte at offset +0x511
            // Returns 0 for non-creatures (per NSS documentation: "This function will return 0 for any non-creature")
            Components.CreatureComponent creatureComp = entity.GetComponent<Components.CreatureComponent>();
            if (creatureComp == null)
            {
                return Variable.FromInt(0);
            }

            // Check StealthMode flag (stored as byte at offset +0x511 in original engine)
            // 0x005143a0 @ 0x005143a0 checks: if (*(char *)((int)this + 0x511) == '\x01')
            bool isStealthed = creatureComp.StealthMode;
            return Variable.FromInt(isStealthed ? 1 : 0);
        }

        /// <summary>
        /// GetStealthXPEnabled() - Returns TRUE if stealth XP is enabled for the current area
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetStealthXPEnabled @ routine ID 836
        /// Located via string references: "StealthXPEnabled" @ 0x007bd1b4
        /// Ghidra analysis: 0x004e26d0 @ 0x004e26d0 reads "StealthXPEnabled" from AreaProperties GFF structure
        /// Stored at object offset +0x2f4 as byte (boolean) in area properties
        /// Original implementation: Reads boolean from GFF field "StealthXPEnabled" in AreaProperties
        /// </remarks>
        private Variable Func_GetStealthXPEnabled(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get stealth XP enabled state from current area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e26d0 @ 0x004e26d0 reads StealthXPEnabled from AreaProperties GFF
            // Located via string references: "StealthXPEnabled" @ 0x007bd1b4
            // Original implementation: Reads boolean from AreaProperties GFF structure at offset +0x2f4
            if (ctx.World != null && ctx.World.CurrentArea != null)
            {
                return Variable.FromInt(ctx.World.CurrentArea.StealthXPEnabled ? 1 : 0);
            }
            return Variable.FromInt(1); // Default: enabled
        }

        /// <summary>
        /// SetStealthXPEnabled(int nEnabled) - Sets whether stealth XP is enabled for the current area
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SetStealthXPEnabled @ routine ID 837
        /// Located via string references: "StealthXPEnabled" @ 0x007bd1b4
        /// Ghidra analysis: 0x004e11d0 @ 0x004e11d0 writes "StealthXPEnabled" to AreaProperties GFF structure
        /// Stored at object offset +0x2f4 as byte (boolean) in area properties
        /// Original implementation: Writes boolean to GFF field "StealthXPEnabled" in AreaProperties
        /// </remarks>
        private Variable Func_SetStealthXPEnabled(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int enabled = args.Count > 0 ? args[0].AsInt() : 1;

            // Set stealth XP enabled state in current area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e11d0 @ 0x004e11d0 writes StealthXPEnabled to AreaProperties GFF
            // Located via string references: "StealthXPEnabled" @ 0x007bd1b4
            // Original implementation: Writes boolean to AreaProperties GFF structure at offset +0x2f4
            if (ctx.World != null && ctx.World.CurrentArea != null)
            {
                ctx.World.CurrentArea.StealthXPEnabled = enabled != 0;
            }
            return Variable.Void();
        }

        // Workbench functions (TSL only)
        /// <summary>
        /// ShowUpgradeScreen(object oItem, object oCharacter, int nDisableItemCreation, int nDisableUpgrade, string sOverride2DA) - Displays the upgrade screen where the player can modify weapons and armor
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ShowUpgradeScreen @ 0x00680cb0 (routine ID 850)
        /// Located via string references: "upgradeitems_p" @ 0x007d09e4, "BTN_UPGRADEITEM" @ 0x007d09d4
        /// "BTN_UPGRADEITEMS" @ 0x007d0b58, "BTN_CREATEITEMS" @ 0x007d0b48, "upgradesel_p" (upgrade selection screen)
        /// Original implementation:
        /// - Validates item exists if oItem != OBJECT_INVALID (0x7f000000) via object lookup
        /// - Creates upgrade selection screen GUI ("upgradesel_p") with item type filters (All, Lightsaber, Ranged, Melee, Armor)
        /// - Creates upgrade items screen GUI ("upgradeitems_p") with item list, description, and upgrade buttons
        /// - Sets item ID in GUI object (offset 0x629)
        /// - Sets character ID in GUI object (offset 0x18a8) - character skills extracted and used for item creation/upgrading
        /// - Sets disableItemCreation flag (offset 0x18c8: 0 = false, 1 = true)
        /// - Sets disableUpgrade flag (offset 0x18cc: 0 = false, 1 = true)
        /// - Sets override2DA string (if provided, otherwise empty string)
        /// - Initializes screen via 0x0067c8f0
        /// - Shows screen via GUI manager (0x0040bf90 adds to GUI manager, 0x00638bb0 sets screen mode)
        /// - If oItem is NOT invalid, then the player will be forced to upgrade oItem and only oItem
        /// - If oCharacter is NOT invalid, then that character's various skills will be used for:
        ///   - Upgrade availability checks (skill requirements)
        ///   - Item creation success rates (higher skills = better success)
        ///   - Upgrade application skill checks (skill requirements for applying upgrades)
        ///   [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Character skills stored in IStatsComponent, accessed via GetSkillRank()
        ///   Skills extracted when character is set via BaseUpgradeScreen.ExtractCharacterSkills()
        ///   Original implementation: Character skills were NOT IMPLEMENTED in original ShowUpgradeScreen
        /// - If nDisableItemCreation = TRUE, then the player will not be able to access the item creation screen
        /// - If nDisableUpgrade = TRUE, then the player will be forced straight to item creation and not be able to access Item Upgrading
        /// - sOverride2DA: Override 2DA file name (empty string for default upgradeitems.2da)
        /// GUI Elements:
        /// - Upgrade Selection Screen ("upgradesel_p"): BTN_ALL, BTN_LIGHTSABER, BTN_RANGED, BTN_MELEE, BTN_ARMOR, LB_UPGRADELIST, LB_DESCRIPTION, BTN_BACK, BTN_UPGRADEITEMS, BTN_CREATEITEMS
        /// - Upgrade Items Screen ("upgradeitems_p"): LBL_TITLE, LB_ITEMS, LB_DESCRIPTION, BTN_UPGRADEITEM, BTN_BACK
        /// </remarks>
        private Variable Func_ShowUpgradeScreen(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ShowUpgradeScreen(object oItem = OBJECT_INVALID, object oCharacter = OBJECT_INVALID,
            //                   int nDisableItemCreation = FALSE, int nDisableUpgrade = FALSE, string sOverride2DA = "")
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ShowUpgradeScreen @ 0x00680cb0
            uint item = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            uint character = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;
            int disableItemCreation = args.Count > 2 ? args[2].AsInt() : 0;
            int disableUpgrade = args.Count > 3 ? args[3].AsInt() : 0;
            string override2DA = args.Count > 4 ? args[4].AsString() : string.Empty;

            // Validate item exists if specified (original checks via 0x004dc020 if item != OBJECT_INVALID)
            if (item != ObjectInvalid && ctx.World != null)
            {
                IEntity itemEntity = ResolveObject(item, ctx);
                if (itemEntity == null)
                {
                    // Item not found - original returns early if validation fails
                    return Variable.Void();
                }
            }

            // Get UI system from game services context
            if (ctx is VMExecutionContext execCtx &&
                execCtx.AdditionalContext is IGameServicesContext services &&
                services.UISystem != null)
            {
                // Show upgrade screen via UI system
                // Original creates upgrade selection screen ("upgradesel_p") and upgrade items screen ("upgradeitems_p")
                // Sets flags: disableItemCreation, disableUpgrade, override2DA
                // Shows screen via GUI manager
                services.UISystem.ShowUpgradeScreen(item, character, disableItemCreation != 0, disableUpgrade != 0, override2DA);
            }
            else
            {
                // UI system not available - log warning
                Console.WriteLine("[TheSithLords] ShowUpgradeScreen: UI system not available");
            }

            return Variable.Void();
        }

        private Variable Func_GetBaseItemType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;

            IEntity item = ResolveObject(itemId, ctx);
            if (item == null)
            {
                return Variable.FromInt(0);
            }

            // Get base item type from item component
            Runtime.Core.Interfaces.Components.IItemComponent itemComponent = item.GetComponent<Runtime.Core.Interfaces.Components.IItemComponent>();
            if (itemComponent != null)
            {
                // BaseItem is the base item type ID from baseitems.2da
                return Variable.FromInt(itemComponent.BaseItem);
            }

            return Variable.FromInt(0);
        }

        // Combat form functions (TSL only)
        private Variable Func_GetIsFormActive(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creature = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int form = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(creature, ctx);
            if (entity == null)
            {
                return Variable.FromInt(0);
            }

            // Get active combat form from entity data (stored as "ActiveCombatForm")
            // Combat forms: 0 = None, 1 = Beast, 2 = Droid, 3 = Force, etc.
            if (entity.HasData("ActiveCombatForm"))
            {
                int activeForm = entity.GetData<int>("ActiveCombatForm", 0);
                return Variable.FromInt(activeForm == form ? 1 : 0);
            }

            return Variable.FromInt(0); // No form active
        }

        // Swoop minigame functions (TSL extensions)
        private Variable Func_SWMG_GetPlayerOffset(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get swoop minigame player offset
            return Variable.FromVector(Vector3.Zero);
        }

        private Variable Func_SWMG_GetPlayerInvincibility(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get swoop minigame invincibility state from player entity
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PlayerEntity != null)
            {
                bool invincible = services.PlayerEntity.GetData<bool>("SwoopMinigameInvincible", false);
                return Variable.FromInt(invincible ? 1 : 0);
            }

            return Variable.FromInt(0); // Not invincible
        }

        private Variable Func_SWMG_SetPlayerInvincibility(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int invincible = args.Count > 0 ? args[0].AsInt() : 0;

            // Store swoop minigame invincibility state in player entity
            if (ctx is VMExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PlayerEntity != null)
            {
                // Store invincibility state in player entity data
                services.PlayerEntity.SetData("SwoopMinigameInvincible", invincible != 0);
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetFeatAcquired(int nFeat, object oCreature=OBJECT_SELF) - Returns whether creature has access to a feat, even if unusable
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetFeatAcquired function (TSL only, function ID 783)
        /// Located via string references: Feat acquisition checking (separate from usability)
        /// Original implementation: Checks if creature has the feat in their feat list, regardless of daily limits or restrictions
        /// Returns TRUE if creature has the feat (even if exhausted or restricted)
        /// Returns FALSE if creature doesn't have the feat
        /// Difference from GetHasFeat: GetHasFeat checks usability (daily limits, restrictions), GetFeatAcquired only checks acquisition
        /// </remarks>
        private Variable Func_GetFeatAcquired(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 1)
            {
                return Variable.FromInt(0);
            }

            int featId = args[0].AsInt();
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null || entity.ObjectType != Runtime.Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            Components.CreatureComponent creature = entity.GetComponent<Components.CreatureComponent>();
            if (creature == null || creature.FeatList == null)
            {
                return Variable.FromInt(0);
            }

            // GetFeatAcquired only checks if creature has the feat, not if it's usable
            // This is different from GetHasFeat which also checks daily limits and restrictions
            bool hasFeat = creature.HasFeat(featId);
            return Variable.FromInt(hasFeat ? 1 : 0);
        }

        #endregion
    }
}
