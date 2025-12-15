using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using CSharpKOTOR.Common;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Actions;
using Odyssey.Core.Audio;
using Odyssey.Core.Combat;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Kotor.Combat;
using Odyssey.Kotor.Components;
using Odyssey.Kotor.Dialogue;
using Odyssey.Kotor.Game;
using Odyssey.Kotor.Loading;
using Odyssey.Kotor.Systems;
using Odyssey.Scripting.Interfaces;
using Odyssey.Scripting.Types;
using Odyssey.Scripting.VM;
using Vector3 = System.Numerics.Vector3;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace Odyssey.Scripting.EngineApi
{
    /// <summary>
    /// KOTOR 1 engine API implementation.
    /// </summary>
    /// <remarks>
    /// Engine API (NWScript Functions):
    /// - Based on swkotor2.exe NWScript engine API implementation
    /// - Located via string references: Script function dispatch system handles ACTION opcodes in NCS VM
    /// - Original implementation: NCS VM executes ACTION opcode (0x2A) with routine ID, calls engine function handlers
    /// - Function IDs match nwscript.nss definitions (ScriptDefs.KOTOR_FUNCTIONS)
    /// - K1 has ~850 engine functions, K2 has ~950 engine functions
    /// - Original engine uses function dispatch table indexed by routine ID
    /// - Function implementations must match NWScript semantics (parameter types, return types, behavior)
    /// </remarks>
    public class K1EngineApi : BaseEngineApi
    {
        private readonly NcsVm _vm;
        
        // Iteration state for GetFirstFactionMember/GetNextFactionMember
        // Key: caller entity ID, Value: list of faction members and current index
        private readonly Dictionary<uint, FactionMemberIteration> _factionMemberIterations;
        
        // Iteration state for GetFirstObjectInArea/GetNextObjectInArea
        // Key: caller entity ID, Value: list of area objects and current index
        private readonly Dictionary<uint, AreaObjectIteration> _areaObjectIterations;
        
        // Iteration state for GetFirstEffect/GetNextEffect
        // Key: caller entity ID, Value: list of effects and current index
        private readonly Dictionary<uint, EffectIteration> _effectIterations;

        // Iteration state for GetFirstInPersistentObject/GetNextInPersistentObject
        // Key: caller entity ID, Value: list of persistent objects and current index
        private readonly Dictionary<uint, PersistentObjectIteration> _persistentObjectIterations;

        // Track last spell target for GetSpellTargetObject
        // Key: caster entity ID, Value: target entity ID
        private readonly Dictionary<uint, uint> _lastSpellTargets;

        // Track last equipped item for GetLastItemEquipped
        // Key: creature entity ID, Value: item entity ID
        private readonly Dictionary<uint, uint> _lastEquippedItems;

        // Track player restriction state
        private bool _playerRestricted;

        // Track last spell cast metamagic type for GetMetaMagicFeat
        // Key: caster entity ID, Value: metamagic feat type (METAMAGIC_* constants)
        private readonly Dictionary<uint, int> _lastMetamagicTypes;

        // Track last spell ID for GetSpellId
        // Key: caster entity ID, Value: spell ID
        private readonly Dictionary<uint, int> _lastSpellIds;

        // Track last spell caster for GetLastSpellCaster
        // Key: target entity ID, Value: caster entity ID
        private readonly Dictionary<uint, uint> _lastSpellCasters;

        // Track last spell target location for GetSpellTargetLocation
        // Key: caster entity ID, Value: target location
        private readonly Dictionary<uint, Location> _lastSpellTargetLocations;

        // Track user-defined event number for GetUserDefinedEventNumber
        private int _userDefinedEventNumber;

        // Track run script variable for GetRunScriptVar
        private Variable _runScriptVar;

        public K1EngineApi()
        {
            _vm = new NcsVm();
            _factionMemberIterations = new Dictionary<uint, FactionMemberIteration>();
            _areaObjectIterations = new Dictionary<uint, AreaObjectIteration>();
            _effectIterations = new Dictionary<uint, EffectIteration>();
            _persistentObjectIterations = new Dictionary<uint, PersistentObjectIteration>();
            _lastSpellTargets = new Dictionary<uint, uint>();
            _lastEquippedItems = new Dictionary<uint, uint>();
            _lastMetamagicTypes = new Dictionary<uint, int>();
            _lastSpellIds = new Dictionary<uint, int>();
            _lastSpellCasters = new Dictionary<uint, uint>();
            _lastSpellTargetLocations = new Dictionary<uint, Location>();
            _userDefinedEventNumber = 0;
            _runScriptVar = Variable.Void();
            _playerRestricted = false; // Initialize player restriction state
        }
        
        private class FactionMemberIteration
        {
            public List<IEntity> Members { get; set; }
            public int CurrentIndex { get; set; }
        }
        
        private class AreaObjectIteration
        {
            public List<IEntity> Objects { get; set; }
            public int CurrentIndex { get; set; }
        }
        
        private class EffectIteration
        {
            public List<Odyssey.Core.Combat.ActiveEffect> Effects { get; set; }
            public int CurrentIndex { get; set; }
        }

        private class PersistentObjectIteration
        {
            public List<IEntity> Objects { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected override void RegisterFunctions()
        {
            // Register function names from ScriptDefs
            int idx = 0;
            foreach (ScriptFunction func in ScriptDefs.KOTOR_FUNCTIONS)
            {
                _functionNames[idx] = func.Name;
                idx++;
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
                case 0: return Func_Random(args, ctx);
                case 1: return Func_PrintString(args, ctx);
                case 2: return Func_PrintFloat(args, ctx);
                case 3: return Func_FloatToString(args, ctx);
                case 4: return Func_PrintInteger(args, ctx);
                case 5: return Func_PrintObject(args, ctx);
                case 6: return Func_AssignCommand(args, ctx);
                case 7: return Func_DelayCommand(args, ctx);
                case 8: return Func_ExecuteScript(args, ctx);
                case 9: return Func_ClearAllActions(args, ctx);
                case 10: return Func_SetFacing(args, ctx);
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
                case 24: return Func_GetArea(args, ctx);
                case 25: return Func_GetEnteringObject(args, ctx);
                case 26: return Func_GetExitingObject(args, ctx);
                case 27: return Func_GetPosition(args, ctx);
                case 28: return Func_GetFacing(args, ctx);
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
                case 41: return Func_GetDistanceToObject(args, ctx);
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

                // GetAbilityScore (routine 139)
                case 139: return Func_GetAbilityScore(args, ctx);
                
                // GetItemInSlot (routine 155)
                case 155: return Func_GetItemInSlot(args, ctx);
                
                // GetItemStackSize (routine 138)
                case 138: return Func_GetItemStackSize(args, ctx);
                
                // PrintVector
                case 141: return Func_PrintVector(args, ctx);
                
                // ApplyEffectToObject (routine 220)
                case 220: return Func_ApplyEffectToObject(args, ctx);

                // Global string (restricted functions)
                case 160: return Func_SetGlobalString(args, ctx);
                case 194: return Func_GetGlobalString(args, ctx);

                // Location functions
                case 213: return Func_GetLocation(args, ctx);
                case 214: return Func_ActionJumpToLocation(args, ctx);
                case 215: return Func_Location(args, ctx);

                // Core object functions (correct IDs from nwscript.nss)
                case 168: return Func_GetTag(args, ctx);
                case 197: return Func_GetWaypointByTag(args, ctx);
                case 200: return Func_GetObjectByTag(args, ctx);
                case 226: return Func_GetNearestCreatureToLocation(args, ctx);
                case 227: return Func_GetNearestObject(args, ctx);
                case 229: return Func_GetNearestObjectByTag(args, ctx);
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
                case 242: return Func_GetModule(args, ctx);
                case 251: return Func_GetLoadFromSaveGame(args, ctx);
                case 272: return Func_ObjectToString(args, ctx);
                
                // Faction manipulation
                case 173: return Func_ChangeFaction(args, ctx);

                // Combat functions
                case 316: return Func_GetAttackTarget(args, ctx);
                case 319: return Func_GetDistanceBetween2D(args, ctx);
                case 320: return Func_GetIsInCombat(args, ctx);

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
                case 235: return Func_GetIsEnemy(args, ctx);
                case 236: return Func_GetIsFriend(args, ctx);
                case 237: return Func_GetIsNeutral(args, ctx);
                case 380: return Func_GetFirstFactionMember(args, ctx);
                case 381: return Func_GetNextFactionMember(args, ctx);
                
                // Global variables (KOTOR specific - different from standard NWN)
                case 578: return Func_GetGlobalBoolean(args, ctx);
                case 579: return Func_SetGlobalBoolean(args, ctx);
                case 580: return Func_GetGlobalNumber(args, ctx);
                case 581: return Func_SetGlobalNumber(args, ctx);

                // Local variables (KOTOR uses index-based, not name-based like NWN)
                // 679: GetLocalBoolean(object, int index) - index 0-63
                // 680: SetLocalBoolean(object, int index, int value)
                // 681: GetLocalNumber(object, int index) - index 0
                // 682: SetLocalNumber(object, int index, int value)
                case 679: return Func_GetLocalBoolean(args, ctx);
                case 680: return Func_SetLocalBoolean(args, ctx);
                case 681: return Func_GetLocalNumber(args, ctx);
                case 682: return Func_SetLocalNumber(args, ctx);

                default:
                    // Return default value for unimplemented functions
                    string funcName = GetFunctionName(routineId);
                    Console.WriteLine("[NCS] Unimplemented function: " + routineId + " (" + funcName + ")");
                    return Variable.Void();
            }
        }

        #region Basic Utility Functions

        private new Variable Func_Random(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Random(int nMaxInteger) - returns 0 to nMaxInteger-1
            int maxValue = args.Count > 0 ? args[0].AsInt() : 0;
            if (maxValue <= 0)
            {
                return Variable.FromInt(0);
            }
            return Variable.FromInt(_random.Next(maxValue));
        }

        private new Variable Func_PrintString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // PrintString(string sString) - outputs to console/log
            string message = args.Count > 0 ? args[0].AsString() : "";
            Console.WriteLine("[SCRIPT] " + message);
            return Variable.Void();
        }

        /// <summary>
        /// GetTag(object oObject) - returns the tag string of an object
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Object tag system
        /// Located via string references: "Tag" string used throughout engine for object identification
        /// Original implementation: Every entity has a unique tag string used for lookup (GetObjectByTag, GetWaypointByTag)
        /// Tag usage: Used in scripts to find objects by tag, stored in entity's Tag property
        /// Tag format: Alphanumeric string, typically matches template ResRef or custom identifier
        /// Returns: Tag string or empty string if object is invalid
        /// </remarks>
        private new Variable Func_GetTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetTag(object oObject) - returns the tag string
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromString(entity.Tag ?? "");
            }
            return Variable.FromString("");
        }

        private new Variable Func_GetLocalInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLocalInt(object oObject, string sVarName) - get local integer variable
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string varName = args.Count > 1 ? args[1].AsString() : "";
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && !string.IsNullOrEmpty(varName))
            {
                return Variable.FromInt(ctx.Globals.GetLocalInt(entity, varName));
            }
            return Variable.FromInt(0);
        }

        private new Variable Func_SetLocalInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetLocalInt(object oObject, string sVarName, int nValue) - set local integer variable
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string varName = args.Count > 1 ? args[1].AsString() : "";
            int value = args.Count > 2 ? args[2].AsInt() : 0;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && !string.IsNullOrEmpty(varName))
            {
                ctx.Globals.SetLocalInt(entity, varName, value);
            }
            return Variable.Void();
        }

        #endregion

        #region Action Functions

        private Variable Func_AssignCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // AssignCommand(object oActionSubject, action aActionToAssign)
            // This pushes an action onto the target's action queue
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

        private Variable Func_DelayCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // DelayCommand(float fSeconds, action aActionToDelay)
            float delay = args.Count > 0 ? args[0].AsFloat() : 0f;
            IAction action = args.Count > 1 ? args[1].ComplexValue as IAction : null;

            if (action != null && ctx.Caller != null && ctx.World != null)
            {
                // Schedule the action in the world's delay scheduler
                ctx.World.DelayScheduler.ScheduleDelay(delay, action, ctx.Caller);
            }

            return Variable.Void();
        }

        private Variable Func_ExecuteScript(IReadOnlyList<Variable> args, IExecutionContext ctx)
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
                    int result = _vm.ExecuteScript(scriptName, scriptCtx);
                    return Variable.FromInt(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[K1EngineApi] Error executing script {scriptName}: {ex.Message}");
            }

            return Variable.Void();
        }

        private Variable Func_ClearAllActions(IReadOnlyList<Variable> args, IExecutionContext ctx)
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
            // ActionMoveToLocation(location lDestination, int bRun=FALSE)
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
        /// Based on swkotor2.exe: Speak string action system
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

        private Variable Func_PrintObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            Console.WriteLine("[Script] Object: 0x" + objectId.ToString("X8"));
            return Variable.Void();
        }

        private Variable Func_SetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float facing = args.Count > 0 ? args[0].AsFloat() : 0f;
            if (ctx.Caller != null)
            {
                Core.Interfaces.Components.ITransformComponent transform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    transform.Facing = facing * (float)Math.PI / 180f;
                }
            }
            return Variable.Void();
        }

        private Variable Func_GetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.ITransformComponent transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    return Variable.FromFloat(transform.Facing * 180f / (float)Math.PI);
                }
            }
            return Variable.FromFloat(0f);
        }

        private Variable Func_GetPosition(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.ITransformComponent transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    return Variable.FromVector(transform.Position);
                }
            }
            return Variable.FromVector(System.Numerics.Vector3.Zero);
        }

        private Variable Func_GetDistanceToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            IEntity target = ResolveObject(targetId, ctx);

            if (ctx.Caller != null && target != null)
            {
                Core.Interfaces.Components.ITransformComponent callerTransform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                Core.Interfaces.Components.ITransformComponent targetTransform = target.GetComponent<Core.Interfaces.Components.ITransformComponent>();

                if (callerTransform != null && targetTransform != null)
                {
                    float dist = Vector3.Distance(callerTransform.Position, targetTransform.Position);
                    return Variable.FromFloat(dist);
                }
            }

            return Variable.FromFloat(-1f); // Invalid
        }

        private Variable Func_GetObjectType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromInt((int)entity.ObjectType);
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetEnteringObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Triggerer != null)
            {
                return Variable.FromObject(ctx.Triggerer.ObjectId);
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetExitingObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Triggerer != null)
            {
                return Variable.FromObject(ctx.Triggerer.ObjectId);
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

            Core.Interfaces.Components.ITransformComponent targetTransform = target.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (targetTransform == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get all creatures in radius
            IEnumerable<IEntity> entities = ctx.World.GetEntitiesInRadius(targetTransform.Position, 100f, Core.Enums.ObjectType.Creature);
            
            // Filter and sort by criteria
            List<IEntity> matchingCreatures = new List<IEntity>();
            
            foreach (IEntity entity in entities)
            {
                if (entity == target) continue;
                if (entity.ObjectType != Core.Enums.ObjectType.Creature) continue;

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
                    Core.Interfaces.Components.ITransformComponent aTransform = a.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                    Core.Interfaces.Components.ITransformComponent bTransform = b.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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
        /// Based on swkotor2.exe: Creature search system with criteria filtering
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
            IEnumerable<IEntity> entities = ctx.World.GetEntitiesInRadius(locationPos, 100f, Core.Enums.ObjectType.Creature);
            
            // Filter and sort by criteria
            List<IEntity> matchingCreatures = new List<IEntity>();
            
            foreach (IEntity entity in entities)
            {
                if (entity.ObjectType != Core.Enums.ObjectType.Creature) continue;

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
                    Core.Interfaces.Components.ITransformComponent aTransform = a.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                    Core.Interfaces.Components.ITransformComponent bTransform = b.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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

            Core.Interfaces.Components.ITransformComponent targetTransform = target.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (targetTransform == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Convert object type constant to ObjectType enum
            Core.Enums.ObjectType typeMask = Core.Enums.ObjectType.All;
            if (objectType != 32767) // Not OBJECT_TYPE_ALL
            {
                typeMask = (Core.Enums.ObjectType)objectType;
            }

            // Get all entities of the specified type
            var candidates = new List<(IEntity entity, float distance)>();
            foreach (IEntity entity in ctx.World.GetEntitiesOfType(typeMask))
            {
                if (entity == target) continue;

                Core.Interfaces.Components.ITransformComponent entityTransform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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
                    if (creature is Core.Entities.Entity entity)
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
                        if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                        {
                            return services.PlayerEntity != null && services.PlayerEntity.ObjectId == creature.ObjectId;
                        }
                    }
                    else if (criteriaValue == 1)
                    {
                        // Not PC
                        if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                        {
                            return services.PlayerEntity == null || services.PlayerEntity.ObjectId != creature.ObjectId;
                        }
                    }
                    return false;

                case 2: // CREATURE_TYPE_CLASS
                    // Check class type from creature component
                    // Using dynamic to avoid dependency on Odyssey.Kotor.Components
                    dynamic creatureComp = creature.GetComponent<object>();
                    if (creatureComp != null)
                    {
                        var classList = creatureComp.ClassList as System.Collections.IEnumerable;
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
                    return false;

                case 3: // CREATURE_TYPE_REPUTATION
                    // REPUTATION_TYPE_FRIEND = 0, REPUTATION_TYPE_ENEMY = 1, REPUTATION_TYPE_NEUTRAL = 2
                    if (ctx is VM.ExecutionContext execCtxRep && execCtxRep.AdditionalContext is IGameServicesContext servicesRep)
                    {
                        if (servicesRep.FactionManager != null && servicesRep.PlayerEntity != null)
                        {
                            switch (criteriaValue)
                            {
                                case 0: // FRIEND
                                    return servicesRep.FactionManager.IsFriendly(servicesRep.PlayerEntity, creature);
                                case 1: // ENEMY
                                    return servicesRep.FactionManager.IsHostile(servicesRep.PlayerEntity, creature);
                                case 2: // NEUTRAL
                                    return servicesRep.FactionManager.IsNeutral(servicesRep.PlayerEntity, creature);
                                default:
                                    return false;
                            }
                        }
                    }
                    return false;

                case 4: // CREATURE_TYPE_IS_ALIVE
                    // TRUE = alive, FALSE = dead
                    Core.Interfaces.Components.IStatsComponent stats = creature.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                    // Perception types are typically handled by PerceptionManager
                    if (ctx is VM.ExecutionContext execCtxPer && execCtxPer.AdditionalContext is IGameServicesContext servicesPer)
                    {
                        if (servicesPer.PerceptionManager != null && servicesPer.PlayerEntity != null)
                        {
                            // criteriaValue is the perception type to check
                            // This would need PerceptionManager to check if creature matches perception type
                            // For now, return true as a placeholder - full implementation would check perception flags
                            return true;
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
        /// Based on swkotor2.exe: Global variable system
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
        /// Based on swkotor2.exe: Global variable system
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
        /// Based on swkotor2.exe: Global variable system
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
        /// Based on swkotor2.exe: Global variable system
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
        /// Based on swkotor2.exe: Global variable system
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
        /// Based on swkotor2.exe: Global variable system
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
        /// Based on swkotor2.exe: Local variable system (index-based, not name-based like NWN)
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
        /// Based on swkotor2.exe: Local variable system (index-based, not name-based like NWN)
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
        /// Based on swkotor2.exe: Local variable system (index-based, not name-based like NWN)
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
        /// Based on swkotor2.exe: Local variable system (index-based, not name-based like NWN)
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

        private Variable Func_SwitchPlayerCharacter(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        /// <summary>
        /// SetTime(int nHour, int nMinute, int nSecond, int nMillisecond) - Sets the game time
        /// Based on swkotor2.exe: Sets the in-game time (not real time)
        /// </summary>
        private Variable Func_SetTime(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = args.Count > 0 ? args[0].AsInt() : 0;
            int minute = args.Count > 1 ? args[1].AsInt() : 0;
            int second = args.Count > 2 ? args[2].AsInt() : 0;
            int millisecond = args.Count > 3 ? args[3].AsInt() : 0;
            
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.GameSession != null)
                {
                    services.GameSession.SetGameTime(hour, minute, second, millisecond);
                }
            }
            return Variable.Void();
        }

        private Variable Func_GetPartyMemberCount(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetPartyMemberCount()
            // Returns a count of how many members are in the party including the player character
            if (ctx.AdditionalContext is IGameServicesContext services && services.PartyManager != null)
            {
                return Variable.FromInt(services.PartyManager.ActiveMemberCount);
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetPartyMemberByIndex(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetPartyMemberByIndex(int nIndex)
            // Returns the party member at a given index in the party (0 = leader)
            int index = args.Count > 0 ? args[0].AsInt() : 0;

            if (ctx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
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

            if (ctx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
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

            if (ctx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
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

            if (ctx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
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

            if (ctx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
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
        /// Based on swkotor2.exe: Controls whether players can leave the area
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
        /// Based on swkotor2.exe: Returns the unescapable flag for the current area
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
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.GameSession is Odyssey.Kotor.Game.GameSession gameSession)
                {
                    return Variable.FromInt(gameSession.GetGameTimeHours());
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetTimeMinute() - Returns the current game time minute (0-59)
        /// </summary>
        private Variable Func_GetTimeMinute(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.GameSession is Odyssey.Kotor.Game.GameSession gameSession)
                {
                    return Variable.FromInt(gameSession.GetGameTimeMinutes());
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetTimeSecond() - Returns the current game time second (0-59)
        /// </summary>
        private Variable Func_GetTimeSecond(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.GameSession is Odyssey.Kotor.Game.GameSession gameSession)
                {
                    return Variable.FromInt(gameSession.GetGameTimeSeconds());
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetTimeMillisecond() - Returns the current game time millisecond (0-999)
        /// </summary>
        private Variable Func_GetTimeMillisecond(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.GameSession is Odyssey.Kotor.Game.GameSession gameSession)
                {
                    return Variable.FromInt(gameSession.GetGameTimeMilliseconds());
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsDay() - Returns TRUE if it is currently day time (6:00 AM to 8:00 PM)
        /// Based on swkotor2.exe: Time of day check for day period
        /// </summary>
        private Variable Func_GetIsDay(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = Func_GetTimeHour(new List<Variable>(), ctx).AsInt();
            // Day is typically 6:00 AM (6) to 8:00 PM (20)
            return Variable.FromInt((hour >= 6 && hour < 20) ? 1 : 0);
        }

        /// <summary>
        /// GetIsNight() - Returns TRUE if it is currently night time (8:00 PM to 6:00 AM)
        /// Based on swkotor2.exe: Time of day check for night period
        /// </summary>
        private Variable Func_GetIsNight(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = Func_GetTimeHour(new List<Variable>(), ctx).AsInt();
            // Night is typically 8:00 PM (20) to 6:00 AM (6)
            return Variable.FromInt((hour >= 20 || hour < 6) ? 1 : 0);
        }

        /// <summary>
        /// GetIsDawn() - Returns TRUE if it is currently dawn (5:00 AM to 7:00 AM)
        /// Based on swkotor2.exe: Time of day check for dawn period
        /// </summary>
        private Variable Func_GetIsDawn(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int hour = Func_GetTimeHour(new List<Variable>(), ctx).AsInt();
            // Dawn is typically 5:00 AM (5) to 7:00 AM (7)
            return Variable.FromInt((hour >= 5 && hour < 7) ? 1 : 0);
        }

        /// <summary>
        /// GetIsDusk() - Returns TRUE if it is currently dusk (7:00 PM to 9:00 PM)
        /// Based on swkotor2.exe: Time of day check for dusk period
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
        /// Based on swkotor2.exe: Tracks last entity that used an object
        /// </summary>
        private Variable Func_GetLastUsedBy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastUsedBy from entity data
            if (ctx.Caller is Core.Entities.Entity entity)
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
        /// Based on swkotor2.exe: Tracks last entity that opened a door/placeable
        /// </summary>
        private Variable Func_GetLastOpenedBy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastOpenedBy from entity data
            if (ctx.Caller is Core.Entities.Entity entity)
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
        /// Based on swkotor2.exe: Tracks last entity that closed a door/placeable
        /// </summary>
        private Variable Func_GetLastClosedBy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastClosedBy from entity data
            if (ctx.Caller is Core.Entities.Entity entity)
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
        /// Based on swkotor2.exe: Tracks last entity that locked a door/placeable
        /// </summary>
        private Variable Func_GetLastLocked(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastLocked from entity data
            if (ctx.Caller is Core.Entities.Entity entity)
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
        /// Based on swkotor2.exe: Tracks last entity that unlocked a door/placeable
        /// </summary>
        private Variable Func_GetLastUnlocked(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Caller == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get LastUnlocked from entity data
            if (ctx.Caller is Core.Entities.Entity entity)
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
        /// Based on swkotor2.exe: Plot flag prevents objects from being removed or modified
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
            if (entity is Core.Entities.Entity concreteEntity)
            {
                bool plotFlag = concreteEntity.GetData<bool>("PlotFlag", false);
                return Variable.FromInt(plotFlag ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetPlotFlag(object oTarget, int nPlotFlag) - Sets the plot flag on oTarget
        /// Based on swkotor2.exe: Plot flag prevents objects from being removed or modified
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
            if (entity is Core.Entities.Entity concreteEntity)
            {
                concreteEntity.SetData("PlotFlag", plotFlag != 0);
            }

            return Variable.Void();
        }

        #endregion

        private new Variable Func_GetArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_GetArea(args, ctx);
        }

        private new Variable Func_GetObjectByTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_GetObjectByTag(args, ctx);
        }

        private new Variable Func_GetNearestObjectByTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_GetNearestObjectByTag(args, ctx);
        }

        #endregion

        private new Variable Func_GetModule(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetModule() - Get the module object
            // Returns OBJECT_INVALID on error
            // In KOTOR, modules are special objects with a fixed object ID
            // The module object ID is typically 0x7F000002 (as per base implementation)
            // However, we should verify the current module exists
            if (ctx.World != null && ctx.World.CurrentModule != null)
            {
                // Return the standard module object ID
                // This matches the base implementation and KOTOR conventions
                return Variable.FromObject(0x7F000002);
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private new Variable Func_ObjectToString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_ObjectToString(args, ctx);
        }

        /// <summary>
        /// GetItemPossessor(object oItem) - returns the creature/placeable that possesses the item
        /// </summary>
        private Variable Func_GetItemPossessor(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity item = ResolveObject(itemId, ctx);
            
            if (item == null || item.ObjectType != Core.Enums.ObjectType.Item)
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
                    // Fallback to CSharpKOTOR Installation provider
                    else if (ctx.ResourceProvider is CSharpKOTOR.Installation.Installation installation)
                    {
                        CSharpKOTOR.Installation.ResourceResult result = installation.Resource(itemTemplate, ResourceType.UTI, null, null);
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
                    Console.WriteLine($"[K1EngineApi] Error loading UTI template '{itemTemplate}': {ex.Message}");
                }
            }

            // Create item entity
            IEntity itemEntity = ctx.World.CreateEntity(ObjectType.Item, Vector3.Zero, 0f);
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
                    var itemComponent = new ItemComponent
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
                        var prop = new Odyssey.Core.Interfaces.Components.ItemProperty
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
                        var upgrade = new Odyssey.Core.Interfaces.Components.ItemUpgrade
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
        /// Based on swkotor2.exe: Equips item from inventory to specified equipment slot
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
                Core.Interfaces.Components.ITransformComponent transform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.CombatManager is CombatManager combatManager)
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
            if (ctx.AdditionalContext is IGameServicesContext services && services.CombatManager is CombatManager combatManager)
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

            Core.Interfaces.Components.ITransformComponent transformA = objectA.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            Core.Interfaces.Components.ITransformComponent transformB = objectB.GetComponent<Core.Interfaces.Components.ITransformComponent>();

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
            if (ctx.AdditionalContext is IGameServicesContext services && services.CombatManager is CombatManager combatManager)
            {
                bool inCombat = combatManager.IsInCombat(creature);
                // Note: bOnlyCountReal parameter is not yet implemented - would need to distinguish
                // between "real" combat (actively fighting) vs "fake" combat (just targeted)
                return Variable.FromInt(inCombat ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetCameraFacing(float fDirection) - Sets the camera facing direction
        /// Based on swkotor2.exe: Camera facing controls camera yaw rotation in chase mode
        /// The direction is in radians (0 = east, PI/2 = north, PI = west, 3*PI/2 = south)
        /// </summary>
        private Variable Func_SetCameraFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float direction = args.Count > 0 ? args[0].AsFloat() : 0f;
            
            // Access CameraController through GameServicesContext
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.CameraController is Core.Camera.CameraController cameraController)
                {
                    // Set camera facing using SetFacing method (handles both chase and free camera modes)
                    cameraController.SetFacing(direction);
                }
            }
            
            return Variable.Void();
        }

        /// <summary>
        /// PlaySound(string sSoundName) - Plays a sound effect
        /// </summary>
        /// <summary>
        /// PlaySound(string sSoundName) - Plays a sound effect
        /// Based on swkotor2.exe: PlaySound plays WAV files as sound effects
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.SoundPlayer != null)
                {
                    // Get caller's position for 3D spatial audio
                    System.Numerics.Vector3? position = null;
                    Core.Interfaces.Components.ITransformComponent transform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                    if (transform != null)
                    {
                        // Convert CSharpKOTOR Vector3 to System.Numerics.Vector3
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
        /// Based on swkotor2.exe: Returns the target object ID of the last spell cast by the caller
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
        /// Based on swkotor2.exe: Queues spell casting action, tracks target and metamagic type
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
            if (targetId != ObjectInvalid)
            {
                _lastSpellTargets[ctx.Caller.ObjectId] = targetId;
            }

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
        /// GetCurrentHitPoints(object oCreature=OBJECT_SELF) - returns current hit points
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Hit point system
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
        /// Based on swkotor2.exe: Maximum hit point system
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (entity != null && entity.ObjectType == ObjectType.Creature)
            {
                // Check if entity is NOT the player entity
                if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
        /// Based on swkotor2.exe: TLK (talk table) string lookup system
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.DialogueManager is DialogueManager dialogueManager && dialogueManager.IsConversationActive)
                {
                    var state = dialogueManager.CurrentState;
                    if (state != null && state.CurrentNode != null)
                    {
                        // Get text from current node using DialogueManager's GetNodeText method
                        string text = dialogueManager.GetNodeText(state.CurrentNode);
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
                    Core.Interfaces.Components.IScriptHooksComponent hooks = target.GetComponent<Core.Interfaces.Components.IScriptHooksComponent>();
                    if (hooks != null)
                    {
                        CSharpKOTOR.Common.ResRef dialogueResRef = hooks.GetScript(Core.Enums.ScriptEvent.OnConversation);
                        if (dialogueResRef != null && !dialogueResRef.IsBlank())
                        {
                            resRef = dialogueResRef.ToString();
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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

        private Variable Func_GetLastPerceived(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLastPerceived() - Get the object that was perceived in an OnPerception script
            // Returns OBJECT_INVALID if the caller is not a valid creature
            if (ctx.Caller == null || ctx.Caller.ObjectType != Core.Enums.ObjectType.Creature)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (ctx.Caller == null || ctx.Caller.ObjectType != Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (ctx.Caller == null || ctx.Caller.ObjectType != Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (ctx.Caller == null || ctx.Caller.ObjectType != Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            if (ctx.Caller == null || ctx.Caller.ObjectType != Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            Core.Interfaces.Components.IInventoryComponent inventory = persistentObject.GetComponent<Core.Interfaces.Components.IInventoryComponent>();
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.GetSkillRank(skill));
                }
                return Variable.FromInt(-1); // No stats component = invalid target
            }
            return Variable.FromInt(-1); // Invalid target
        }

        /// <summary>
        /// GetAbilityScore(object oCreature=OBJECT_SELF, int nAbilityType) - returns ability score
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: D20 ability score system
        /// Located via string references: "KeyAbility" @ 0x007c2cbc, "LvlStatAbility" @ 0x007c3f48, "SpecAbilityList" @ 0x007c3ed4
        /// Original implementation: Returns base ability score + modifiers from effects, equipment, etc.
        /// Ability types: ABILITY_STRENGTH (0), ABILITY_DEXTERITY (1), ABILITY_CONSTITUTION (2),
        ///   ABILITY_INTELLIGENCE (3), ABILITY_WISDOM (4), ABILITY_CHARISMA (5)
        /// Ability scores: Range from 1-50 typically, base scores from character creation/template
        /// Modifiers: Effects, equipment, feats can modify ability scores
        /// Returns: Ability score value (1-50 typically) or 0 if entity is invalid
        /// </remarks>
        private Variable Func_GetAbilityScore(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetAbilityScore(object oCreature, int nAbilityType)
            // nAbilityType: ABILITY_STRENGTH (0), ABILITY_DEXTERITY (1), ABILITY_CONSTITUTION (2),
            //              ABILITY_INTELLIGENCE (3), ABILITY_WISDOM (4), ABILITY_CHARISMA (5)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int abilityType = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
        /// Based on swkotor2.exe: Inventory slot system
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
                Core.Interfaces.Components.IInventoryComponent inventory = entity.GetComponent<Core.Interfaces.Components.IInventoryComponent>();
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

            if (item == null || item.ObjectType != Core.Enums.ObjectType.Item)
            {
                return Variable.FromInt(0);
            }

            Core.Interfaces.Components.IItemComponent itemComponent = item.GetComponent<Core.Interfaces.Components.IItemComponent>();
            if (itemComponent != null)
            {
                return Variable.FromInt(itemComponent.StackSize);
            }

            return Variable.FromInt(1); // Default stack size
        }

        private Variable Func_ApplyEffectToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ApplyEffectToObject(int nDurationType, effect eEffect, object oTarget, float fDuration=0.0f)
            // nDurationType: DURATION_TYPE_INSTANT (0), DURATION_TYPE_TEMPORARY (1), DURATION_TYPE_PERMANENT (2)
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

            // Convert effect object to Effect
            Effect effect = null;
            if (effectObj is Effect directEffect)
            {
                effect = directEffect;
            }
            else if (effectObj != null)
            {
                // Try to extract Effect from Variable wrapper or other container
                // For now, if it's not an Effect, we can't process it
                Console.WriteLine($"[K1EngineApi] ApplyEffectToObject: Invalid effect type: {effectObj.GetType().Name}");
                return Variable.Void();
            }
            else
            {
                return Variable.Void();
            }

            // Map duration type
            EffectDurationType effectDurationType;
            switch (durationType)
            {
                case 0: // DURATION_TYPE_INSTANT
                    effectDurationType = EffectDurationType.Instant;
                    break;
                case 1: // DURATION_TYPE_TEMPORARY
                    effectDurationType = EffectDurationType.Temporary;
                    if (duration > 0f)
                    {
                        // Convert seconds to rounds (assuming 6 seconds per round)
                        effect.DurationRounds = (int)Math.Ceiling(duration / 6f);
                    }
                    break;
                case 2: // DURATION_TYPE_PERMANENT
                    effectDurationType = EffectDurationType.Permanent;
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

        // Effect placeholders
        /// <summary>
        /// EffectAssuredHit() - Creates an effect that guarantees the next attack will hit
        /// </summary>
        private Variable Func_EffectAssuredHit(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // EffectAssuredHit creates an effect that makes the next attack automatically hit
            // This is typically used for special abilities or Force powers
            // In KOTOR, this might be represented as an attack bonus effect or a special flag
            var effect = new Combat.Effect(Combat.EffectType.AttackIncrease)
            {
                Amount = 1000, // Very high bonus to guarantee hit
                DurationType = Combat.EffectDurationType.Temporary,
                Duration = 1 // Lasts for 1 round (next attack only)
            };
            return Variable.FromEffect(effect);
        }

        /// <summary>
        /// GetLastItemEquipped() - Returns the last item that was equipped by the caller
        /// Based on swkotor2.exe: Tracks the last item equipped via ActionEquipItem
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.MaxFP);
                }
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// PauseGame(int bPause) - Pauses or unpauses the game
        /// Based on swkotor2.exe: PauseGame pauses all game systems except UI
        /// When paused, combat, movement, scripts, and other game logic are suspended
        /// </summary>
        private Variable Func_PauseGame(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int pause = args.Count > 0 ? args[0].AsInt() : 0;
            bool shouldPause = pause != 0;
            
            // Access GameSession through GameServicesContext
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.GameSession != null)
                {
                    if (shouldPause)
                    {
                        services.GameSession.Pause();
                    }
                    else
                    {
                        services.GameSession.Resume();
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
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            // Based on swkotor2.exe: Player restriction mode prevents player movement/actions
            return Variable.FromInt(_playerRestricted ? 1 : 0);
        }

        /// <summary>
        /// GetCasterLevel(object oCreature=OBJECT_SELF) - Returns the caster level of a creature
        /// Based on swkotor2.exe: Caster level is typically the total character level or Force user class levels
        /// For Force powers, caster level = total level of Force-using classes (Jedi Consular, Guardian, Sentinel, Master, Lord)
        /// </summary>
        private Variable Func_GetCasterLevel(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            
            if (entity == null || entity.ObjectType != Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }
            
            // Get caster level from CreatureComponent class list
            // In KOTOR, caster level for Force powers is typically the sum of Force-using class levels
            CreatureComponent creatureComp = entity.GetComponent<CreatureComponent>();
            if (creatureComp != null)
            {
                // For now, return total character level as caster level
                // Full implementation would filter for Force-using classes only
                // Force-using classes: Jedi Consular (2), Jedi Guardian (3), Jedi Sentinel (4), 
                //                     Jedi Master (8), Sith Lord (9), etc.
                int totalLevel = creatureComp.GetTotalLevel();
                return Variable.FromInt(totalLevel);
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
            List<Combat.ActiveEffect> effects = new List<Combat.ActiveEffect>();
            foreach (Combat.ActiveEffect effect in ctx.World.EffectSystem.GetEffects(entity))
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

            // Convert effect object to Effect
            Combat.Effect effect = null;
            if (effectObj is Combat.Effect directEffect)
            {
                effect = directEffect;
            }
            else if (effectObj != null)
            {
                // Try to extract from Variable wrapper
                Console.WriteLine($"[K1EngineApi] RemoveEffect: Invalid effect type: {effectObj.GetType().Name}");
                return Variable.Void();
            }
            else
            {
                return Variable.Void();
            }

            // Find and remove matching effect from entity
            foreach (Combat.ActiveEffect activeEffect in ctx.World.EffectSystem.GetEffects(entity))
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

            // Check if effect is a valid Effect object
            if (effectObj is Combat.Effect)
            {
                return Variable.FromInt(1);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetEffectDurationType(effect eEffect) - returns duration type of effect
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Effect duration type system
        /// Original implementation: Effects have duration types (Instant, Temporary, Permanent)
        /// DURATION_TYPE_INSTANT = 0: Effect applies once and ends
        /// DURATION_TYPE_TEMPORARY = 1: Effect has duration and expires after time
        /// DURATION_TYPE_PERMANENT = 2: Effect persists until removed manually
        /// </remarks>
        private Variable Func_GetEffectDurationType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            object effectObj = args.Count > 0 ? args[0].ComplexValue : null;
            
            if (effectObj is Combat.Effect effect)
            {
                // Map EffectDurationType to NWScript constants
                // DURATION_TYPE_INSTANT = 0, DURATION_TYPE_TEMPORARY = 1, DURATION_TYPE_PERMANENT = 2
                switch (effect.DurationType)
                {
                    case Combat.EffectDurationType.Instant:
                        return Variable.FromInt(0);
                    case Combat.EffectDurationType.Temporary:
                        return Variable.FromInt(1);
                    case Combat.EffectDurationType.Permanent:
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
            
            if (effectObj is Combat.Effect effect)
            {
                return Variable.FromInt(effect.SubType);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetEffectCreator(effect eEffect) - returns creator of effect
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Effect creator tracking system
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

            // Find matching effect and return creator
            if (effectObj is Combat.Effect effect)
            {
                foreach (Combat.ActiveEffect activeEffect in ctx.World.EffectSystem.GetEffects(entity))
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
        /// Based on swkotor2.exe: Object iteration system for area entities
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
                if (entity.ObjectType == Core.Enums.ObjectType.Creature)
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
                else if (entity.ObjectType == Core.Enums.ObjectType.Placeable)
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
                else if (entity.ObjectType == Core.Enums.ObjectType.Door)
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
                else if (entity.ObjectType == Core.Enums.ObjectType.Trigger)
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
                else if (entity.ObjectType == Core.Enums.ObjectType.Waypoint)
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
                else if (entity.ObjectType == Core.Enums.ObjectType.Sound)
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
            
            if (ctx.Caller == null || ctx.Caller.ObjectType != Core.Enums.ObjectType.Creature)
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
        /// Based on swkotor2.exe: FUN_005261b0 @ 0x005261b0 (load creature from UTC template)
        /// Racial type is stored in CreatureComponent.RaceId (from UTC template)
        /// </summary>
        private Variable Func_GetRacialType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity creature = ResolveObject(creatureId, ctx);
            
            if (creature == null || creature.ObjectType != Core.Enums.ObjectType.Creature)
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                Core.Interfaces.Components.IStatsComponent stats = caster.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    // Spell save DC = 10 + caster level + ability modifier (typically Wisdom or Charisma for Force powers)
                    // Base DC is 10, plus caster level, plus relevant ability modifier
                    // Wisdom for Jedi, Charisma for Sith
                    int casterLevel = Func_GetCasterLevel(new List<Variable> { Variable.FromObject(caster.ObjectId) }, ctx).AsInt();
                    // Default to Wisdom modifier (Jedi), but could check class to determine if Sith (Charisma)
                    int abilityMod = stats.GetAbilityModifier(Odyssey.Core.Enums.Ability.Wisdom);
                    int saveDC = 10 + casterLevel + abilityMod;
                    return Variable.FromInt(saveDC);
                }
            }
            
            // Default save DC if caster not found
            return Variable.FromInt(10);
        }

        /// <summary>
        /// MagicalEffect(effect eEffect) - Wraps an effect as a magical effect (can be dispelled)
        /// Based on swkotor2.exe: Sets effect subtype to SUBTYPE_MAGICAL (8)
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
            Combat.Effect effect = effectObj as Combat.Effect;
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
        /// Based on swkotor2.exe: Sets effect subtype to SUBTYPE_SUPERNATURAL (16)
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
            Combat.Effect effect = effectObj as Combat.Effect;
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
            Combat.Effect effect = effectObj as Combat.Effect;
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
        /// Based on swkotor2.exe: D20 armor class system
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
                    if (ctx.AdditionalContext is IGameServicesContext services)
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
                    if (ctx.AdditionalContext is IGameServicesContext services)
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
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
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
        /// Based on swkotor2.exe: Faction member iteration system
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
                    if (ctx.AdditionalContext is IGameServicesContext services)
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
                if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
                if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
                {
                    if (services.FactionManager is FactionManager factionManager)
                    {
                        bool isFriendly = factionManager.IsFriendly(source, target);
                        return Variable.FromInt(isFriendly ? 1 : 0);
                    }
                }
                
                // Fallback: Simple faction check if FactionManager not available
                IFactionComponent sourceFaction = source.GetComponent<IFactionComponent>();
                IFactionComponent targetFaction = target.GetComponent<IFactionComponent>();
                
                if (sourceFaction != null && targetFaction != null)
                {
                    // Check if same faction (simplified - would need FactionManager for proper friendliness)
                    if (sourceFaction.FactionId == targetFaction.FactionId)
                    {
                        // Same faction are friends by default
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
        /// Based on swkotor2.exe: Waypoint lookup system
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
                if (waypoint != null && waypoint.ObjectType == Core.Enums.ObjectType.Waypoint)
                {
                    return Variable.FromObject(waypoint.ObjectId);
                }
            }

            // Fallback to world search
            IEntity found = ctx.World.GetEntityByTag(waypointTag, 0);
            if (found != null && found.ObjectType == Core.Enums.ObjectType.Waypoint)
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
            if (entity is Odyssey.Core.Entities.Entity concreteEntity)
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
                // For now, we'll just set the data - proper component creation would require EntityFactory
                if (objectToChange is Odyssey.Core.Entities.Entity concreteEntity)
                {
                    concreteEntity.SetData("FactionID", memberFaction.FactionId);
                }
            }
            else
            {
                // Change faction ID
                targetFaction.FactionId = memberFaction.FactionId;
            }

            return Variable.Void();
        }

        #endregion

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

        #endregion

        #region String and Object Functions

        /// <summary>
        /// ActionSpeakStringByStrRef(int nStrRef, int nTalkVolume=TALKVOLUME_TALK) - Causes the creature to speak a translated string
        /// </summary>
        /// <summary>
        /// ActionSpeakStringByStrRef(int nStrRef, int nTalkVolume=TALKVOLUME_TALK) - action to speak string from TLK
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Speak string by StrRef action system
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
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
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
            
            // Cannot destroy modules or areas
            if (entity.ObjectType == Core.Enums.ObjectType.Module || entity.ObjectType == Core.Enums.ObjectType.Area)
            {
                return Variable.Void();
            }
            
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
            var destroyAction = new Odyssey.Core.Actions.ActionDestroyObject(entity.ObjectId, delay, noFade != 0, delayUntilFade);
            
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
        /// Based on swkotor2.exe: Runtime object creation system
        /// Located via string references: Object creation functions handle template loading and entity spawning
        /// Original implementation: Creates runtime entities from GFF templates at specified location
        /// Object types: OBJECT_TYPE_CREATURE (1), OBJECT_TYPE_ITEM (2), OBJECT_TYPE_PLACEABLE (4),
        ///   OBJECT_TYPE_STORE (5), OBJECT_TYPE_WAYPOINT (6)
        /// Template loading: Loads UTC/UTI/UTP/UTM/UTW templates from installation, applies to entity
        /// Location: Extracts position and facing from location object
        /// Appear animation: bUseAppearAnimation flag controls whether spawn animation plays (not yet implemented)
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
            Core.Enums.ObjectType odyObjectType = Core.Enums.ObjectType.Creature; // Default
            ResourceType resourceType = ResourceType.UTC; // Default
            
            // Map NWScript object type constants to Odyssey ObjectType
            // OBJECT_TYPE_CREATURE = 1, OBJECT_TYPE_ITEM = 2, OBJECT_TYPE_PLACEABLE = 4, OBJECT_TYPE_STORE = 5, OBJECT_TYPE_WAYPOINT = 6
            switch (objectType)
            {
                case 1: // OBJECT_TYPE_CREATURE
                    odyObjectType = Core.Enums.ObjectType.Creature;
                    resourceType = ResourceType.UTC;
                    break;
                case 2: // OBJECT_TYPE_ITEM
                    odyObjectType = Core.Enums.ObjectType.Item;
                    resourceType = ResourceType.UTI;
                    break;
                case 4: // OBJECT_TYPE_PLACEABLE
                    odyObjectType = Core.Enums.ObjectType.Placeable;
                    resourceType = ResourceType.UTP;
                    break;
                case 5: // OBJECT_TYPE_STORE
                    odyObjectType = Core.Enums.ObjectType.Store;
                    resourceType = ResourceType.UTM;
                    break;
                case 6: // OBJECT_TYPE_WAYPOINT
                    odyObjectType = Core.Enums.ObjectType.Waypoint;
                    resourceType = ResourceType.UTW;
                    break;
                default:
                    return Variable.FromObject(ObjectInvalid);
            }
            
            // Create entity using EntityFactory
            IEntity entity = null;
            
            // Access ModuleLoader via GameServicesContext to get EntityFactory
            if (ctx is Odyssey.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services)
            {
                if (services.ModuleLoader is ModuleLoader moduleLoader && moduleLoader.EntityFactory != null)
                {
                    CSharpKOTOR.Common.Module csharpKotorModule = moduleLoader.GetCurrentModule();
                    if (csharpKotorModule == null)
                    {
                        return Variable.FromObject(ObjectInvalid);
                    }

                    // Convert position to System.Numerics.Vector3
                    System.Numerics.Vector3 entityPosition = new System.Numerics.Vector3(position.X, position.Y, position.Z);

                    // Create entity from template using EntityFactory
                    switch (odyObjectType)
                    {
                        case Core.Enums.ObjectType.Creature:
                            if (!string.IsNullOrEmpty(template))
                            {
                                entity = moduleLoader.EntityFactory.CreateCreatureFromTemplate(csharpKotorModule, template, entityPosition, facing);
                            }
                            break;
                        case Core.Enums.ObjectType.Item:
                            if (!string.IsNullOrEmpty(template))
                            {
                                entity = moduleLoader.EntityFactory.CreateItemFromTemplate(csharpKotorModule, template, entityPosition, facing);
                            }
                            break;
                        case Core.Enums.ObjectType.Placeable:
                            if (!string.IsNullOrEmpty(template))
                            {
                                entity = moduleLoader.EntityFactory.CreatePlaceableFromTemplate(csharpKotorModule, template, entityPosition, facing);
                            }
                            break;
                        case Core.Enums.ObjectType.Store:
                            if (!string.IsNullOrEmpty(template))
                            {
                                entity = moduleLoader.EntityFactory.CreateStoreFromTemplate(csharpKotorModule, template, entityPosition, facing);
                            }
                            break;
                        case Core.Enums.ObjectType.Waypoint:
                            // Waypoints don't have templates in the same way, their "template" is often just their tag
                            entity = moduleLoader.EntityFactory.CreateWaypointFromTemplate(template, entityPosition, facing);
                            break;
                    }
                }
            }
            
            // Fallback: Create basic entity if EntityFactory not available or template creation failed
            if (entity == null)
            {
                // Convert System.Numerics.Vector3 to CSharpKOTOR Vector3 for World.CreateEntity
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
            if (ctx.World.CurrentArea is Core.Module.RuntimeArea runtimeArea)
            {
                runtimeArea.AddEntity(entity);
            }
            
            // Implement appear animation if bUseAppearAnimation is TRUE
            // Based on swkotor2.exe: Objects created with appear animation play a fade-in effect
            // This is typically handled by setting a flag that the rendering system uses to fade in the object
            if (useAppearAnimation != 0)
            {
                // Set flag on entity to indicate it should fade in
                if (entity is Core.Entities.Entity entityImpl)
                {
                    entityImpl.SetData("AppearAnimation", true);
                    
                    // Optionally, queue an animation action for entities that support it
                    // Most objects in KOTOR just fade in visually rather than playing a specific animation
                    // The rendering system should handle the fade-in based on the AppearAnimation flag
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

        /// <summary>
        /// StringToInt(string sNumber) - Convert sNumber into an integer
        /// </summary>
        private new Variable Func_StringToInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string numberStr = args.Count > 0 ? args[0].AsString() : "";
            if (int.TryParse(numberStr, out int result))
            {
                return Variable.FromInt(result);
            }
            return Variable.FromInt(0);
        }

        /// <summary>
        /// StringToFloat(string sNumber) - Convert sNumber into a floating point number
        /// </summary>
        private new Variable Func_StringToFloat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string numberStr = args.Count > 0 ? args[0].AsString() : "";
            if (float.TryParse(numberStr, out float result))
            {
                return Variable.FromFloat(result);
            }
            return Variable.FromFloat(0f);
        }

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
            Core.Enums.ObjectType typeMask = Core.Enums.ObjectType.All;
            if (objectType != 32767) // Not OBJECT_TYPE_ALL
            {
                // Map NWScript object type constants
                typeMask = (Core.Enums.ObjectType)objectType;
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
    }
}
    }
}



    }
}


