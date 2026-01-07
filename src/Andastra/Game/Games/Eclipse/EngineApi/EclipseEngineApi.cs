using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Common.Script;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Eclipse;
using Andastra.Runtime.Games.Eclipse.Components;
using Andastra.Runtime.Games.Eclipse.Loading;
using Andastra.Runtime.Scripting.EngineApi;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Engines.Eclipse.EngineApi
{
    /// <summary>
    /// Base Eclipse Engine API implementation containing ONLY functions common between daorigins.exe and DragonAge2.exe.
    /// </summary>
    /// <remarks>
    /// Eclipse Engine API Base Class (Common Functions):
    /// - Based on daorigins.exe (Dragon Age: Origins) and DragonAge2.exe script engine API implementations
    /// - Located via string references: Script function dispatch system (different from NWScript, uses UnrealScript-like system)
    /// - Original implementation: Script VM executes function calls with routine ID, calls engine function handlers
    /// - Function IDs match script definitions (different from NWScript function IDs)
    /// - Dragon Age: Origins has ~500 engine functions, Dragon Age 2 has ~500 engine functions
    /// - Original engine uses function dispatch table indexed by routine ID
    /// - Function implementations must match script semantics (parameter types, return types, behavior)
    /// - Eclipse uses UnrealScript-like system, so function signatures differ from NWScript
    /// - Common Eclipse functions: Print, Random, GetObjectByTag, GetTag, GetPosition, GetFacing, SpawnCreature, CreateItem, etc.
    /// - Note: Function IDs are Eclipse-specific and differ from NWScript function IDs
    ///
    /// CRITICAL: This class contains ONLY functions that are IDENTICAL between daorigins.exe and DragonAge2.exe.
    /// - All function implementations must be verified to match 1:1 in both executables
    /// - Game-specific differences must be implemented in subclasses (DragonAgeOriginsEngineApi, DragonAge2EngineApi)
    /// - Cross-engine analysis: Functions are only included here if they are identical in both engines
    /// - Inheritance structure: DragonAgeOriginsEngineApi : EclipseEngineApi, DragonAge2EngineApi : EclipseEngineApi
    /// </remarks>
    public class EclipseEngineApi : BaseEngineApi
    {
        // Static dictionary to store area iteration state (since IArea doesn't support SetData/GetData)
        private static readonly System.Collections.Generic.Dictionary<string, AreaIterationState> _areaIterationStates = new System.Collections.Generic.Dictionary<string, AreaIterationState>();

        // Static dictionary to store conversation state (since IArea doesn't support SetData/GetData)
        private static readonly System.Collections.Generic.Dictionary<string, ConversationState> _conversationStates = new System.Collections.Generic.Dictionary<string, ConversationState>();

        // Helper class to store iteration state per area
        private class AreaIterationState
        {
            public System.Collections.Generic.List<Core.Interfaces.IEntity> Entities { get; set; }
            public int CurrentIndex { get; set; }
        }

        // Helper class to store conversation state per area
        private class ConversationState
        {
            public uint SpeakerId { get; set; }
            public uint TargetId { get; set; }
            public bool InConversation { get; set; }
        }

        public EclipseEngineApi()
        {
        }

        protected override void RegisterFunctions()
        {
            // Register common Eclipse function names
            // Note: Eclipse function IDs differ from NWScript - these are Eclipse-specific mappings
            // Function names are registered for debugging and logging purposes
            RegisterFunctionName(0, "Random");
            RegisterFunctionName(1, "PrintString");
            RegisterFunctionName(2, "PrintFloat");
            RegisterFunctionName(3, "FloatToString");
            RegisterFunctionName(4, "PrintInteger");
            RegisterFunctionName(5, "PrintObject");

            // Object functions
            RegisterFunctionName(27, "GetPosition");
            RegisterFunctionName(28, "GetFacing");
            RegisterFunctionName(41, "GetDistanceToObject");
            RegisterFunctionName(42, "GetIsObjectValid");

            // Tag functions
            RegisterFunctionName(168, "GetTag");

            // Global variables
            RegisterFunctionName(578, "GetGlobalBoolean");
            RegisterFunctionName(579, "SetGlobalBoolean");
            RegisterFunctionName(580, "GetGlobalNumber");
            RegisterFunctionName(581, "SetGlobalNumber");

            // Local variables
            RegisterFunctionName(679, "GetLocalInt");
            RegisterFunctionName(680, "SetLocalInt");
            RegisterFunctionName(681, "GetLocalFloat");
            RegisterFunctionName(682, "SetLocalFloat");

            // Eclipse-specific functions (common patterns across Dragon Age and )
            RegisterFunctionName(100, "SpawnCreature");
            RegisterFunctionName(101, "CreateItem");
            RegisterFunctionName(102, "DestroyObject");
            RegisterFunctionName(103, "GetArea");
            RegisterFunctionName(104, "GetModule");
            RegisterFunctionName(105, "GetNearestCreature");
            RegisterFunctionName(106, "GetNearestObject");
            RegisterFunctionName(107, "GetNearestObjectByTag");
            RegisterFunctionName(108, "GetFirstObjectInArea");
            RegisterFunctionName(109, "GetNextObjectInArea");
            RegisterFunctionName(110, "GetObjectType");
            RegisterFunctionName(111, "GetIsPC");
            RegisterFunctionName(112, "GetIsNPC");
            RegisterFunctionName(113, "GetIsCreature");
            RegisterFunctionName(114, "GetIsItem");
            RegisterFunctionName(115, "GetIsPlaceable");
            RegisterFunctionName(116, "GetIsDoor");
            RegisterFunctionName(117, "GetIsTrigger");
            RegisterFunctionName(118, "GetIsWaypoint");
            RegisterFunctionName(119, "GetIsArea");
            RegisterFunctionName(120, "GetIsModule");

            // Position and movement functions
            RegisterFunctionName(200, "GetObjectByTag");
            RegisterFunctionName(201, "GetFacing");
            RegisterFunctionName(202, "SetPosition");
            RegisterFunctionName(203, "SetFacing");
            RegisterFunctionName(204, "MoveToObject");
            RegisterFunctionName(205, "MoveToLocation");
            RegisterFunctionName(206, "GetDistanceBetween");
            RegisterFunctionName(207, "GetDistanceBetween2D");

            // Combat functions
            RegisterFunctionName(300, "GetIsInCombat");
            RegisterFunctionName(301, "GetAttackTarget");
            RegisterFunctionName(302, "GetCurrentHP");
            RegisterFunctionName(303, "GetMaxHP");
            RegisterFunctionName(304, "SetCurrentHP");
            RegisterFunctionName(305, "ApplyDamage");
            RegisterFunctionName(306, "GetIsEnemy");
            RegisterFunctionName(307, "GetIsFriend");
            RegisterFunctionName(308, "GetIsNeutral");
            RegisterFunctionName(309, "GetFaction");
            RegisterFunctionName(310, "SetFaction");

            // Party management functions
            RegisterFunctionName(400, "GetPartyMemberCount");
            RegisterFunctionName(401, "GetPartyMemberByIndex");
            RegisterFunctionName(402, "IsObjectPartyMember");
            RegisterFunctionName(403, "AddPartyMember");
            RegisterFunctionName(404, "RemovePartyMember");
            RegisterFunctionName(405, "GetPlayerCharacter");

            // Dialogue and conversation functions
            RegisterFunctionName(500, "StartConversation");
            RegisterFunctionName(501, "GetIsInConversation");
            RegisterFunctionName(502, "GetConversationSpeaker");
            RegisterFunctionName(503, "GetConversationTarget");
            RegisterFunctionName(504, "EndConversation");

            // Quest system functions
            RegisterFunctionName(600, "SetQuestCompleted");
            RegisterFunctionName(601, "GetQuestCompleted");
            RegisterFunctionName(602, "SetQuestActive");
            RegisterFunctionName(603, "GetQuestActive");
            RegisterFunctionName(604, "AddQuestEntry");
            RegisterFunctionName(605, "CompleteQuestEntry");

            // Item and inventory functions
            RegisterFunctionName(700, "CreateItemOnObject");
            RegisterFunctionName(701, "DestroyItem");
            RegisterFunctionName(702, "GetItemInSlot");
            RegisterFunctionName(703, "GetItemStackSize");
            RegisterFunctionName(704, "SetItemStackSize");
            RegisterFunctionName(705, "GetFirstItemInInventory");
            RegisterFunctionName(706, "GetNextItemInInventory");

            // Ability and spell functions
            RegisterFunctionName(800, "CastSpell");
            RegisterFunctionName(801, "CastSpellAtLocation");
            RegisterFunctionName(802, "CastSpellAtObject");
            RegisterFunctionName(803, "GetAbilityScore");
            RegisterFunctionName(804, "GetAbilityModifier");
            RegisterFunctionName(805, "GetHasAbility");
            RegisterFunctionName(806, "GetSpellLevel");

            // Area and module functions
            RegisterFunctionName(900, "GetAreaTag");
            RegisterFunctionName(901, "GetModuleFileName");
            RegisterFunctionName(902, "GetAreaByTag");
            RegisterFunctionName(903, "GetAreaOfObject");

            // Utility functions
            RegisterFunctionName(1000, "GetName");
            RegisterFunctionName(1001, "SetName");
            RegisterFunctionName(1002, "GetStringLength");
            RegisterFunctionName(1003, "GetSubString");
            RegisterFunctionName(1004, "FindSubString");
        }

        public override Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Eclipse-specific function dispatch
            // Most basic functions (Random, PrintString, GetTag, GetObjectByTag, etc.) are already in BaseEngineApi
            // Eclipse-specific functions are implemented below

            switch (routineId)
            {
                // Common functions (delegated to base class)
                case 0: return Func_Random(args, ctx);
                case 1: return Func_PrintString(args, ctx);
                case 2: return Func_PrintFloat(args, ctx);
                case 3: return Func_FloatToString(args, ctx);
                case 4: return Func_PrintInteger(args, ctx);
                case 5: return Func_PrintObject(args, ctx);

                // Object functions
                case 27: return base.Func_GetPosition(args, ctx);
                case 28: return base.Func_GetFacing(args, ctx);
                case 41: return base.Func_GetDistanceToObject(args, ctx);
                case 42: return base.Func_GetIsObjectValid(args, ctx);

                // Tag functions
                case 168: return Func_GetTag(args, ctx);
                case 200: return Func_GetObjectByTag(args, ctx);

                // Global variables
                case 578: return Func_GetGlobalBoolean(args, ctx);
                case 579: return Func_SetGlobalBoolean(args, ctx);
                case 580: return Func_GetGlobalNumber(args, ctx);
                case 581: return Func_SetGlobalNumber(args, ctx);

                // Local variables
                case 679: return Func_GetLocalInt(args, ctx);
                case 680: return Func_SetLocalInt(args, ctx);
                case 681: return Func_GetLocalFloat(args, ctx);
                case 682: return Func_SetLocalFloat(args, ctx);

                // Eclipse-specific functions
                case 100: return Func_SpawnCreature(args, ctx);
                case 101: return Func_CreateItem(args, ctx);
                case 102: return Func_DestroyObject(args, ctx);
                case 103: return base.Func_GetArea(args, ctx);
                case 104: return base.Func_GetModule(args, ctx);
                case 105: return Func_GetNearestCreature(args, ctx);
                case 106: return Func_GetNearestObject(args, ctx);
                case 107: return base.Func_GetNearestObjectByTag(args, ctx);
                case 108: return Func_GetFirstObjectInArea(args, ctx);
                case 109: return Func_GetNextObjectInArea(args, ctx);
                case 110: return Func_GetObjectType(args, ctx);
                case 111: return Func_GetIsPC(args, ctx);
                case 112: return Func_GetIsNPC(args, ctx);
                case 113: return Func_GetIsCreature(args, ctx);
                case 114: return Func_GetIsItem(args, ctx);
                case 115: return Func_GetIsPlaceable(args, ctx);
                case 116: return Func_GetIsDoor(args, ctx);
                case 117: return Func_GetIsTrigger(args, ctx);
                case 118: return Func_GetIsWaypoint(args, ctx);
                case 119: return Func_GetIsArea(args, ctx);
                case 120: return Func_GetIsModule(args, ctx);
                case 201: return base.Func_GetFacing(args, ctx);
                case 202: return Func_SetPosition(args, ctx);
                case 203: return Func_SetFacing(args, ctx);
                case 204: return Func_MoveToObject(args, ctx);
                case 205: return Func_MoveToLocation(args, ctx);
                case 206: return Func_GetDistanceBetween(args, ctx);
                case 207: return Func_GetDistanceBetween2D(args, ctx);
                case 300: return Func_GetIsInCombat(args, ctx);
                case 301: return Func_GetAttackTarget(args, ctx);
                case 302: return Func_GetCurrentHP(args, ctx);
                case 303: return Func_GetMaxHP(args, ctx);
                case 304: return Func_SetCurrentHP(args, ctx);
                case 305: return Func_ApplyDamage(args, ctx);
                case 306: return Func_GetIsEnemy(args, ctx);
                case 307: return Func_GetIsFriend(args, ctx);
                case 308: return Func_GetIsNeutral(args, ctx);
                case 309: return Func_GetFaction(args, ctx);
                case 310: return Func_SetFaction(args, ctx);
                case 400: return Func_GetPartyMemberCount(args, ctx);
                case 401: return Func_GetPartyMemberByIndex(args, ctx);
                case 402: return Func_IsObjectPartyMember(args, ctx);
                case 403: return Func_AddPartyMember(args, ctx);
                case 404: return Func_RemovePartyMember(args, ctx);
                case 405: return Func_GetPlayerCharacter(args, ctx);
                case 500: return Func_StartConversation(args, ctx);
                case 501: return Func_GetIsInConversation(args, ctx);
                case 502: return Func_GetConversationSpeaker(args, ctx);
                case 503: return Func_GetConversationTarget(args, ctx);
                case 504: return Func_EndConversation(args, ctx);
                case 600: return Func_SetQuestCompleted(args, ctx);
                case 601: return Func_GetQuestCompleted(args, ctx);
                case 602: return Func_SetQuestActive(args, ctx);
                case 603: return Func_GetQuestActive(args, ctx);
                case 604: return Func_AddQuestEntry(args, ctx);
                case 605: return Func_CompleteQuestEntry(args, ctx);
                case 700: return Func_CreateItemOnObject(args, ctx);
                case 701: return Func_DestroyItem(args, ctx);
                case 702: return Func_GetItemInSlot(args, ctx);
                case 703: return Func_GetItemStackSize(args, ctx);
                case 704: return Func_SetItemStackSize(args, ctx);
                case 705: return Func_GetFirstItemInInventory(args, ctx);
                case 706: return Func_GetNextItemInInventory(args, ctx);
                case 800: return Func_CastSpell(args, ctx);
                case 801: return Func_CastSpellAtLocation(args, ctx);
                case 802: return Func_CastSpellAtObject(args, ctx);
                case 803: return Func_GetAbilityScore(args, ctx);
                case 804: return Func_GetAbilityModifier(args, ctx);
                case 805: return Func_GetHasAbility(args, ctx);
                case 806: return Func_GetSpellLevel(args, ctx);
                case 900: return Func_GetAreaTag(args, ctx);
                case 901: return Func_GetModuleFileName(args, ctx);
                case 902: return Func_GetAreaByTag(args, ctx);
                case 903: return Func_GetAreaOfObject(args, ctx);
                case 1000: return Func_GetName(args, ctx);
                case 1001: return Func_SetName(args, ctx);
                case 1002: return Func_GetStringLength(args, ctx);
                case 1003: return Func_GetSubString(args, ctx);
                case 1004: return Func_FindSubString(args, ctx);

                default:
                    // Fall back to unimplemented function logging
                    string funcName = GetFunctionName(routineId);
                    Console.WriteLine("[Eclipse] Unimplemented function: " + routineId + " (" + funcName + ")");
                    return Variable.Void();
            }
        }

        #region Eclipse-Specific Functions

        // GetPosition and GetFacing are now implemented in BaseEngineApi
        // They are identical across all engines (Odyssey, Aurora, Eclipse, Infinity)
        // Verified via Ghidra MCP analysis:
        // - nwmain.exe: ExecuteCommandGetPosition @ 0x14052f5b0, ExecuteCommandGetFacing @ 0x140523a70
        // - swkotor.exe/swkotor2.exe: Equivalent transform system implementations
        // - daorigins.exe: Equivalent transform system implementations
        // EclipseEngineApi now calls base.Func_GetPosition/base.Func_GetFacing for routine IDs 27, 28, and 201

        /// <summary>
        /// SetPosition(object oObject, vector vPosition) - Sets the position of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: SetPosition implementation
        /// Moves object to specified 3D position
        /// </remarks>
        private Variable Func_SetPosition(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            Vector3 position = args.Count > 1 ? args[1].AsVector() : Vector3.Zero;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                ITransformComponent transform = entity.GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    transform.Position = position;
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// SetFacing(object oObject, float fDirection) - Sets the facing direction of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: SetFacing implementation
        /// fDirection is expressed as degrees from East (0.0 = East, 90.0 = North, 180.0 = West, 270.0 = South)
        /// </remarks>
        private Variable Func_SetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            float direction = args.Count > 1 ? args[1].AsFloat() : 0.0f;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                ITransformComponent transform = entity.GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    // Convert degrees to radians for facing angle
                    float radians = (float)(direction * Math.PI / 180.0);
                    transform.Facing = radians;
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetDistanceBetween(object oObject1, object oObject2) - Returns the 3D distance between two objects
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetDistanceBetween implementation
        /// Calculates 3D Euclidean distance between two objects
        /// </remarks>
        private Variable Func_GetDistanceBetween(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId1 = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            uint objectId2 = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            Core.Interfaces.IEntity entity1 = ResolveObject(objectId1, ctx);
            Core.Interfaces.IEntity entity2 = ResolveObject(objectId2, ctx);

            if (entity1 != null && entity2 != null)
            {
                ITransformComponent transform1 = entity1.GetComponent<ITransformComponent>();
                ITransformComponent transform2 = entity2.GetComponent<ITransformComponent>();

                if (transform1 != null && transform2 != null)
                {
                    float distance = Vector3.Distance(transform1.Position, transform2.Position);
                    return Variable.FromFloat(distance);
                }
            }

            return Variable.FromFloat(-1.0f);
        }

        /// <summary>
        /// GetDistanceBetween2D(object oObject1, object oObject2) - Returns the 2D distance between two objects (ignoring Z)
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetDistanceBetween2D implementation
        /// Calculates 2D Euclidean distance (X, Y only) between two objects
        /// </remarks>
        private Variable Func_GetDistanceBetween2D(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId1 = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            uint objectId2 = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            Core.Interfaces.IEntity entity1 = ResolveObject(objectId1, ctx);
            Core.Interfaces.IEntity entity2 = ResolveObject(objectId2, ctx);

            if (entity1 != null && entity2 != null)
            {
                ITransformComponent transform1 = entity1.GetComponent<ITransformComponent>();
                ITransformComponent transform2 = entity2.GetComponent<ITransformComponent>();

                if (transform1 != null && transform2 != null)
                {
                    Vector3 pos1 = transform1.Position;
                    Vector3 pos2 = transform2.Position;
                    float dx = pos2.X - pos1.X;
                    float dy = pos2.Y - pos1.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    return Variable.FromFloat(distance);
                }
            }

            return Variable.FromFloat(-1.0f);
        }

        /// <summary>
        /// SpawnCreature(string sTemplate, vector vPosition, float fFacing) - Spawns a creature at the specified location
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: SpawnCreature implementation
        /// Creates a creature entity from template at specified position and facing
        /// Returns the object ID of the spawned creature, or OBJECT_INVALID on failure
        /// </remarks>
        private Variable Func_SpawnCreature(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string template = args.Count > 0 ? args[0].AsString() : string.Empty;
            Vector3 position = args.Count > 1 ? args[1].AsVector() : Vector3.Zero;
            float facing = args.Count > 2 ? args[2].AsFloat() : 0.0f;

            if (string.IsNullOrEmpty(template) || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Convert facing from degrees to radians for template factory
            float facingRadians = (float)(facing * Math.PI / 180.0);

            // Get module from world for template loading
            // Based on daorigins.exe and DragonAge2.exe: Template loading requires module for UTC GFF access
            // Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
            // Original implementation: Module provides access to UTC template files (creature templates)
            Module module = ctx.World.CurrentModule as Module;
            if (module == null)
            {
                // No module loaded, cannot load templates
                return Variable.FromObject(ObjectInvalid);
            }

            // Create EclipseEntityFactory for template loading
            // Based on daorigins.exe and DragonAge2.exe: EclipseEntityFactory loads UTC GFF templates
            // Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
            // Original implementation: EclipseEntityFactory.CreateCreatureFromTemplate loads UTC GFF and creates entity
            // EclipseEntityFactory can be created without TLK tables (TLK is optional for basic template loading)
            EclipseEntityFactory entityFactory = new EclipseEntityFactory();

            // Create EclipseEntityTemplateFactory to provide IEntityTemplateFactory interface
            // Based on Eclipse engine: EclipseEntityTemplateFactory wraps EclipseEntityFactory for Core compatibility
            // This allows Func_SpawnCreature to use the standard IEntityTemplateFactory interface
            EclipseEntityTemplateFactory templateFactory = new EclipseEntityTemplateFactory(entityFactory, module);

            // Create creature from template using template factory
            // Based on daorigins.exe and DragonAge2.exe: SpawnCreature loads UTC template and creates entity
            // Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
            // Original implementation: Loads UTC GFF template, creates EclipseEntity with all template data applied
            // Template data includes: Tag, FirstName, LastName, Appearance_Type, FactionID, HP, Attributes, Scripts, etc.
            Core.Interfaces.IEntity creature = templateFactory.CreateCreatureFromTemplate(template, position, facingRadians);
            if (creature != null)
            {
                // Register entity with world if not already registered
                // Based on swkotor2.exe: Entities created from templates must be registered with world
                // Located via string references: Entity registration system
                // Original implementation: World.RegisterEntity adds entity to world's entity collection
                if (ctx.World.GetEntity(creature.ObjectId) == null)
                {
                    ctx.World.RegisterEntity(creature);
                }

                return Variable.FromObject(creature.ObjectId);
            }

            // Template not found or creation failed
            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// CreateItem(string sTemplate, object oTarget, int nStackSize) - Creates an item and places it on/in an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: CreateItem implementation
        /// Creates an item from template and adds it to target object's inventory
        /// Returns the object ID of the created item, or OBJECT_INVALID on failure
        /// </remarks>
        private Variable Func_CreateItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string template = args.Count > 0 ? args[0].AsString() : string.Empty;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            int stackSize = args.Count > 2 ? args[2].AsInt() : 1;

            if (string.IsNullOrEmpty(template) || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            Core.Interfaces.IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Create item entity using IWorld.CreateEntity
            // Get target's position for item creation
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            Vector3 itemPosition = targetTransform != null ? targetTransform.Position : Vector3.Zero;

            Core.Interfaces.IEntity item = ctx.World.CreateEntity(Core.Enums.ObjectType.Item, itemPosition, 0.0f);
            if (item != null)
            {
                // Set template tag if provided
                if (!string.IsNullOrEmpty(template))
                {
                    item.SetData("TemplateResRef", template);
                }

                // Set stack size in item component
                IItemComponent itemComp = item.GetComponent<IItemComponent>();
                if (itemComp != null)
                {
                    itemComp.StackSize = Math.Max(1, stackSize);
                }

                // Add item to target's inventory
                IInventoryComponent inventory = target.GetComponent<IInventoryComponent>();
                if (inventory != null)
                {
                    if (inventory.AddItem(item))
                    {
                        return Variable.FromObject(item.ObjectId);
                    }
                    else
                    {
                        // Inventory full, destroy item
                        ctx.World.DestroyEntity(item.ObjectId);
                    }
                }
                else
                {
                    // Target has no inventory, destroy item
                    ctx.World.DestroyEntity(item.ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// CreateItemOnObject(string sTemplate, object oTarget, int nStackSize) - Creates an item and places it on/in an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: CreateItemOnObject implementation
        /// Alias for CreateItem - creates item and adds to target's inventory
        /// </remarks>
        private Variable Func_CreateItemOnObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Func_CreateItem(args, ctx);
        }

        /// <summary>
        /// DestroyObject(object oObject) - Destroys an object, removing it from the world
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: DestroyObject implementation
        /// Removes object from world and frees its resources
        /// </remarks>
        private Variable Func_DestroyObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;

            if (objectId == ObjectInvalid || ctx.World == null)
            {
                return Variable.Void();
            }

            Core.Interfaces.IEntity entity = ctx.World.GetEntity(objectId);
            if (entity != null)
            {
                Console.WriteLine("[Eclipse] DestroyObject: Object 0x{0:X8}", objectId);
                // Destroy entity using IWorld.DestroyEntity
                ctx.World.DestroyEntity(objectId);
            }

            return Variable.Void();
        }

        /// <summary>
        /// DestroyItem(object oItem) - Destroys an item object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: DestroyItem implementation
        /// Alias for DestroyObject for items
        /// </remarks>
        private Variable Func_DestroyItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Func_DestroyObject(args, ctx);
        }

        /// <summary>
        /// GetNearestCreature(int nFirstCriteriaType, int nFirstCriteriaValue, object oTarget, int nNth, int nSecondCriteriaType, int nSecondCriteriaValue, int nThirdCriteriaType, int nThirdCriteriaValue) - Finds the nearest creature matching criteria
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetNearestCreature implementation
        /// Searches for creatures matching specified criteria and returns the nth nearest match
        /// </remarks>
        private Variable Func_GetNearestCreature(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 2 ? args[2].AsObjectId() : ObjectSelf;
            int nth = args.Count > 3 ? args[3].AsInt() : 0;

            Core.Interfaces.IEntity target = ResolveObject(targetId, ctx);
            if (target == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            Console.WriteLine("[Eclipse] GetNearestCreature: Target 0x{0:X8}, nth {1}", targetId, nth);

            // Search for creatures within reasonable radius (100 units default)
            // Get all creatures in radius, filter by criteria, sort by distance, return nth
            const float searchRadius = 100.0f;
            var candidates = new List<Tuple<Core.Interfaces.IEntity, float>>();

            foreach (Core.Interfaces.IEntity entity in ctx.World.GetEntitiesInRadius(targetTransform.Position, searchRadius, Core.Enums.ObjectType.Creature))
            {
                if (entity == null || entity.ObjectId == target.ObjectId)
                {
                    continue;
                }

                // Check if entity is a creature (has stats component)
                IStatsComponent stats = entity.GetComponent<IStatsComponent>();
                if (stats == null)
                {
                    continue;
                }

                // Apply criteria matching (nFirstCriteriaType, nFirstCriteriaValue, etc.)
                // Criteria types: 0=None, 1=Perception, 2=Disposition, 3=Reputation, 4=Team, 5=Reaction, 6=Class, 7=Race, 8=Hp, 9=Tag, 10=NotDead, 11=InCombat, 12=TargetType, 13=CreatureType, 14=Allegiance, 15=Gender, 16=Player, 17=Party, 18=Area, 19=Location, 20=LineOfSight, 21=Distance, 22=HasItem, 23=HasSpell, 24=HasSkill, 25=HasFeat, 26=HasTalent, 27=HasEffect, 28=HasVariable, 29=HasLocalVariable, 30=HasGlobalVariable, 31=HasFaction, 32=HasAlignment, 33=HasGoodEvil, 34=HasLawfulChaotic, 35=HasLevel, 36=HasClass, 37=HasRace, 38=HasGender, 39=HasSubrace, 40=HasDeity, 41=HasDomain, 42=HasDomainSource, 43=HasAbilityScore, 44=HasAbilityModifier, 45=HasSkillRank, 46=HasFeatCount, 47=HasSpellCount, 48=HasTalentCount, 49=HasEffectCount, 50=HasItemCount, 51=HasVariableValue, 52=HasLocalVariableValue, 53=HasGlobalVariableValue, 54=HasFactionValue, 55=HasAlignmentValue, 56=HasGoodEvilValue, 57=HasLawfulChaoticValue, 58=HasLevelValue, 59=HasClassValue, 60=HasRaceValue, 61=HasGenderValue, 62=HasSubraceValue, 63=HasDeityValue, 64=HasDomainValue, 65=HasDomainSourceValue, 66=HasAbilityScoreValue, 67=HasAbilityModifierValue, 68=HasSkillRankValue, 69=HasFeatCountValue, 70=HasSpellCountValue, 71=HasTalentCountValue, 72=HasEffectCountValue, 73=HasItemCountValue
                // Extract criteria from arguments
                int firstCriteriaType = args.Count > 0 ? args[0].AsInt() : 0;
                int firstCriteriaValue = args.Count > 1 ? args[1].AsInt() : 0;
                int secondCriteriaType = args.Count > 5 ? args[5].AsInt() : 0;
                int secondCriteriaValue = args.Count > 6 ? args[6].AsInt() : 0;
                int thirdCriteriaType = args.Count > 7 ? args[7].AsInt() : 0;
                int thirdCriteriaValue = args.Count > 8 ? args[8].AsInt() : 0;

                // Apply criteria matching
                bool matchesCriteria = true;

                // First criteria
                if (firstCriteriaType != 0)
                {
                    matchesCriteria = MatchesCriteria(entity, firstCriteriaType, firstCriteriaValue, target, ctx);
                }

                // Second criteria (if first matches)
                if (matchesCriteria && secondCriteriaType != 0)
                {
                    matchesCriteria = MatchesCriteria(entity, secondCriteriaType, secondCriteriaValue, target, ctx);
                }

                // Third criteria (if first and second match)
                if (matchesCriteria && thirdCriteriaType != 0)
                {
                    matchesCriteria = MatchesCriteria(entity, thirdCriteriaType, thirdCriteriaValue, target, ctx);
                }

                if (!matchesCriteria)
                {
                    continue;
                }

                // Calculate distance
                ITransformComponent entityTransform = entity.GetComponent<ITransformComponent>();
                if (entityTransform != null)
                {
                    float distance = Vector3.Distance(targetTransform.Position, entityTransform.Position);
                    candidates.Add(new Tuple<Core.Interfaces.IEntity, float>(entity, distance));
                }
            }

            // Sort by distance
            candidates.Sort((a, b) => a.Item2.CompareTo(b.Item2));

            // Return nth nearest creature
            if (nth >= 0 && nth < candidates.Count)
            {
                return Variable.FromObject(candidates[nth].Item1.ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNearestObject(int nObjectType, object oTarget, int nNth) - Finds the nearest object of specified type
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetNearestObject implementation
        /// Searches for objects of specified type and returns the nth nearest match
        /// </remarks>
        private Variable Func_GetNearestObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int objectType = args.Count > 0 ? args[0].AsInt() : 0;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            int nth = args.Count > 2 ? args[2].AsInt() : 0;

            Core.Interfaces.IEntity target = ResolveObject(targetId, ctx);
            if (target == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            Console.WriteLine("[Eclipse] GetNearestObject: Type {0}, Target 0x{1:X8}, nth {2}", objectType, targetId, nth);
            // Convert objectType to ObjectType enum mask
            // Eclipse object types: 1=Creature, 2=Item, 4=Trigger, 8=Door, 64=Placeable, etc.
            Core.Enums.ObjectType typeMask = Core.Enums.ObjectType.Invalid;
            if ((objectType & 1) != 0) typeMask |= Core.Enums.ObjectType.Creature;
            if ((objectType & 2) != 0) typeMask |= Core.Enums.ObjectType.Item;
            if ((objectType & 4) != 0) typeMask |= Core.Enums.ObjectType.Trigger;
            if ((objectType & 8) != 0) typeMask |= Core.Enums.ObjectType.Door;
            if ((objectType & 64) != 0) typeMask |= Core.Enums.ObjectType.Placeable;

            // If no type specified, search all types
            if (typeMask == Core.Enums.ObjectType.Invalid)
            {
                typeMask = Core.Enums.ObjectType.All;
            }

            // Search for objects within reasonable radius (100 units default)
            const float searchRadius = 100.0f;
            var candidates = new List<Tuple<Core.Interfaces.IEntity, float>>();

            foreach (Core.Interfaces.IEntity entity in ctx.World.GetEntitiesInRadius(targetTransform.Position, searchRadius, typeMask))
            {
                if (entity == null || entity.ObjectId == target.ObjectId)
                {
                    continue;
                }

                // Calculate distance
                ITransformComponent entityTransform = entity.GetComponent<ITransformComponent>();
                if (entityTransform != null)
                {
                    float distance = Vector3.Distance(targetTransform.Position, entityTransform.Position);
                    candidates.Add(new Tuple<Core.Interfaces.IEntity, float>(entity, distance));
                }
            }

            // Sort by distance
            candidates.Sort((a, b) => a.Item2.CompareTo(b.Item2));

            // Return nth nearest object
            if (nth >= 0 && nth < candidates.Count)
            {
                return Variable.FromObject(candidates[nth].Item1.ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetObjectType(object oObject) - Returns the type of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetObjectType implementation
        /// Returns integer constant representing object type (CREATURE, ITEM, PLACEABLE, DOOR, TRIGGER, etc.)
        /// </remarks>
        private Variable Func_GetObjectType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.FromInt(0); // OBJECT_TYPE_INVALID
            }

            // Determine object type from components
            if (entity.GetComponent<IStatsComponent>() != null)
            {
                // Has stats - likely a creature
                return Variable.FromInt(1); // OBJECT_TYPE_CREATURE
            }
            else if (entity.GetComponent<IItemComponent>() != null)
            {
                return Variable.FromInt(2); // OBJECT_TYPE_ITEM
            }
            else if (entity.GetComponent<IPlaceableComponent>() != null)
            {
                return Variable.FromInt(64); // OBJECT_TYPE_PLACEABLE
            }
            else if (entity.GetComponent<IDoorComponent>() != null)
            {
                return Variable.FromInt(8); // OBJECT_TYPE_DOOR
            }
            else if (entity.GetComponent<ITriggerComponent>() != null)
            {
                return Variable.FromInt(4); // OBJECT_TYPE_TRIGGER
            }

            return Variable.FromInt(0); // OBJECT_TYPE_INVALID
        }

        /// <summary>
        /// GetIsPC(object oObject) - Returns true if object is a player character
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsPC implementation
        /// Located via string reference: "PlayerCharacter" @ 0x00b08188 (daorigins.exe), @ 0x00beb508 (DragonAge2.exe)
        /// Original implementation: Checks if object is the player character
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetIsPC(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                // Check if entity is player character by tag or by being party leader
                string tag = entity.Tag ?? string.Empty;
                if (string.Equals(tag, "PlayerCharacter", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tag, "player", StringComparison.OrdinalIgnoreCase))
                {
                    return Variable.FromInt(1);
                }

                // Check if entity is party member at index 0 (party leader)
                Variable partyLeader = Func_GetPartyMemberByIndex(new[] { Variable.FromInt(0) }, ctx);
                if (partyLeader.AsObjectId() != ObjectInvalid && partyLeader.AsObjectId() == objectId)
                {
                    return Variable.FromInt(1);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsNPC(object oObject) - Returns true if object is an NPC
        /// </summary>
        private Variable Func_GetIsNPC(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                // If it's a creature but not PC, it's an NPC
                if (entity.GetComponent<IStatsComponent>() != null)
                {
                    if (ctx.Caller == null || entity.ObjectId != ctx.Caller.ObjectId)
                    {
                        return Variable.FromInt(1);
                    }
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsCreature(object oObject) - Returns true if object is a creature
        /// </summary>
        private Variable Func_GetIsCreature(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.GetComponent<IStatsComponent>() != null)
            {
                return Variable.FromInt(1);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsItem(object oObject) - Returns true if object is an item
        /// </summary>
        private Variable Func_GetIsItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.GetComponent<IItemComponent>() != null)
            {
                return Variable.FromInt(1);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsPlaceable(object oObject) - Returns true if object is a placeable
        /// </summary>
        private Variable Func_GetIsPlaceable(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.GetComponent<IPlaceableComponent>() != null)
            {
                return Variable.FromInt(1);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsDoor(object oObject) - Returns true if object is a door
        /// </summary>
        private Variable Func_GetIsDoor(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.GetComponent<IDoorComponent>() != null)
            {
                return Variable.FromInt(1);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsInCombat(object oObject) - Returns true if object is in combat
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsInCombat implementation
        /// Located via string reference: "InCombat" @ 0x00af76b0 (daorigins.exe), @ 0x00bf4c10 (DragonAge2.exe)
        /// Original implementation: Checks if object is currently engaged in combat using CombatSystem
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetIsInCombat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && ctx.World != null && ctx.World.CombatSystem != null)
            {
                // Check if entity is in combat using CombatSystem
                bool inCombat = ctx.World.CombatSystem.IsInCombat(entity);
                return Variable.FromInt(inCombat ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetAttackTarget(object oAttacker) - Returns the current attack target of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetAttackTarget implementation
        /// Located via string reference: "CombatTarget" @ 0x00af7840 (daorigins.exe), @ 0x00bf4dc0 (DragonAge2.exe)
        /// Original implementation: Returns the object ID of the current attack target from CombatSystem
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetAttackTarget(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint attackerId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity attacker = ResolveObject(attackerId, ctx);
            if (attacker != null && ctx.World != null && ctx.World.CombatSystem != null)
            {
                // CombatSystem has GetTarget method, not GetAttackTarget
                Core.Interfaces.IEntity target = ctx.World.CombatSystem.GetTarget(attacker);
                if (target != null)
                {
                    return Variable.FromObject(target.ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetCurrentHP(object oObject) - Returns the current hit points of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetCurrentHP implementation
        /// Located via string reference: "CurrentHealth" @ 0x00aedb28 (daorigins.exe), @ 0x00beb46c (DragonAge2.exe)
        /// Original implementation: Returns current HP value from stats component
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetCurrentHP(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IStatsComponent stats = entity.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.CurrentHP);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetMaxHP(object oObject) - Returns the maximum hit points of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetMaxHP implementation
        /// Located via string reference: "MaxHealth" @ 0x00aedb1c (daorigins.exe), @ 0x00beb460 (DragonAge2.exe)
        /// Original implementation: Returns maximum HP value from stats component
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetMaxHP(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IStatsComponent stats = entity.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.MaxHP);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetCurrentHP(object oObject, int nHP) - Sets the current hit points of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: SetCurrentHP implementation
        /// Located via string reference: "CurrentHealth" @ 0x00aedb28 (daorigins.exe), @ 0x00beb46c (DragonAge2.exe)
        /// Original implementation: Sets current HP value in stats component, clamped to [0, MaxHP]
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_SetCurrentHP(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int hp = args.Count > 1 ? args[1].AsInt() : 0;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IStatsComponent stats = entity.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    // Clamp HP to valid range [0, MaxHP]
                    int clampedHP = Math.Max(0, Math.Min(stats.MaxHP, hp));
                    stats.CurrentHP = clampedHP;
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetIsEnemy(object oObject1, object oObject2) - Returns true if two objects are enemies
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsEnemy implementation
        /// Located via string reference: "InCombat" @ 0x00af76b0 (daorigins.exe), @ 0x00bf4c10 (DragonAge2.exe)
        /// Original implementation: Checks faction relationship between two objects using IFactionComponent.IsHostile
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetIsEnemy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId1 = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            uint objectId2 = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            Core.Interfaces.IEntity entity1 = ResolveObject(objectId1, ctx);
            Core.Interfaces.IEntity entity2 = ResolveObject(objectId2, ctx);

            if (entity1 != null && entity2 != null)
            {
                IFactionComponent faction1 = entity1.GetComponent<IFactionComponent>();
                if (faction1 != null)
                {
                    return Variable.FromInt(faction1.IsHostile(entity2) ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsFriend(object oObject1, object oObject2) - Returns true if two objects are friends
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsFriend implementation
        /// Located via string reference: "InCombat" @ 0x00af76b0 (daorigins.exe), @ 0x00bf4c10 (DragonAge2.exe)
        /// Original implementation: Checks faction relationship between two objects using IFactionComponent.IsFriendly
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetIsFriend(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId1 = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            uint objectId2 = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            Core.Interfaces.IEntity entity1 = ResolveObject(objectId1, ctx);
            Core.Interfaces.IEntity entity2 = ResolveObject(objectId2, ctx);

            if (entity1 != null && entity2 != null)
            {
                IFactionComponent faction1 = entity1.GetComponent<IFactionComponent>();
                if (faction1 != null)
                {
                    return Variable.FromInt(faction1.IsFriendly(entity2) ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsNeutral(object oObject1, object oObject2) - Returns true if two objects are neutral to each other
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsNeutral implementation
        /// Located via string reference: "InCombat" @ 0x00af76b0 (daorigins.exe), @ 0x00bf4c10 (DragonAge2.exe)
        /// Original implementation: Checks faction relationship between two objects using IFactionComponent.IsNeutral
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetIsNeutral(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId1 = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            uint objectId2 = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            Core.Interfaces.IEntity entity1 = ResolveObject(objectId1, ctx);
            Core.Interfaces.IEntity entity2 = ResolveObject(objectId2, ctx);

            if (entity1 != null && entity2 != null)
            {
                IFactionComponent faction1 = entity1.GetComponent<IFactionComponent>();
                if (faction1 != null)
                {
                    return Variable.FromInt(faction1.IsNeutral(entity2) ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetFaction(object oObject) - Returns the faction ID of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetFaction implementation
        /// Located via string reference: "Faction" @ 0x007c0ca0 (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Returns faction ID from IFactionComponent.FactionId
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetFaction(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IFactionComponent faction = entity.GetComponent<IFactionComponent>();
                if (faction != null)
                {
                    return Variable.FromInt(faction.FactionId);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetFaction(object oObject, int nFaction) - Sets the faction of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: SetFaction implementation
        /// Located via string reference: "Faction" @ 0x007c0ca0 (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Sets faction ID in IFactionComponent.FactionId
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_SetFaction(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int faction = args.Count > 1 ? args[1].AsInt() : 0;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IFactionComponent factionComp = entity.GetComponent<IFactionComponent>();
                if (factionComp != null)
                {
                    factionComp.FactionId = faction;
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetPartyMemberCount() - Returns the number of party members
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetPartyMemberCount implementation
        /// Located via string reference: "SelectPartyMemberIndexMessage" @ 0x00aec88c (daorigins.exe), @ 0x00be3e28 (DragonAge2.exe)
        /// Original implementation: Returns count of active party members from party system
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses entity data storage as a temporary solution until proper party system interface is available
        /// </remarks>
        private Variable Func_GetPartyMemberCount(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx == null || ctx.World == null)
            {
                return Variable.FromInt(0);
            }

            // Count entities marked as party members
            int count = 0;
            foreach (Core.Interfaces.IEntity entity in ctx.World.GetAllEntities())
            {
                if (entity != null && entity.HasData("IsPartyMember") && entity.GetData<bool>("IsPartyMember"))
                {
                    count++;
                }
            }

            return Variable.FromInt(count);
        }

        /// <summary>
        /// GetPartyMemberByIndex(int nIndex) - Returns the party member at the specified index
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetPartyMemberByIndex implementation
        /// Located via string reference: "SelectPartyMemberIndexMessage" @ 0x00aec88c (daorigins.exe), @ 0x00be3e28 (DragonAge2.exe)
        /// Original implementation: Returns object ID of party member at index from party system
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses entity data storage as a temporary solution until proper party system interface is available
        /// </remarks>
        private Variable Func_GetPartyMemberByIndex(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int index = args.Count > 0 ? args[0].AsInt() : 0;

            if (ctx == null || ctx.World == null || index < 0)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Collect party members
            var partyMembers = new List<Core.Interfaces.IEntity>();
            foreach (Core.Interfaces.IEntity entity in ctx.World.GetAllEntities())
            {
                if (entity != null && entity.HasData("IsPartyMember") && entity.GetData<bool>("IsPartyMember"))
                {
                    partyMembers.Add(entity);
                }
            }

            // Return party member at index
            if (index >= 0 && index < partyMembers.Count)
            {
                return Variable.FromObject(partyMembers[index].ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// IsObjectPartyMember(object oObject) - Returns true if object is a party member
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: IsObjectPartyMember implementation
        /// Located via string reference: "SelectPartyMemberIndexMessage" @ 0x00aec88c (daorigins.exe), @ 0x00be3e28 (DragonAge2.exe)
        /// Original implementation: Checks if object is a party member using party system
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses entity data storage as a temporary solution until proper party system interface is available
        /// </remarks>
        private Variable Func_IsObjectPartyMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.HasData("IsPartyMember"))
            {
                return Variable.FromInt(entity.GetData<bool>("IsPartyMember") ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// AddPartyMember(object oCreature) - Adds a creature to the party
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: AddPartyMember implementation
        /// Located via string reference: "SelectPartyMemberIndexMessage" @ 0x00aec88c (daorigins.exe), @ 0x00be3e28 (DragonAge2.exe)
        /// Original implementation: Adds creature to party using party system
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses entity data storage as a temporary solution until proper party system interface is available
        /// </remarks>
        private Variable Func_AddPartyMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null)
            {
                // Mark entity as party member
                creature.SetData("IsPartyMember", true);
            }

            return Variable.Void();
        }

        /// <summary>
        /// RemovePartyMember(object oCreature) - Removes a creature from the party
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: RemovePartyMember implementation
        /// Located via string reference: "SelectPartyMemberIndexMessage" @ 0x00aec88c (daorigins.exe), @ 0x00be3e28 (DragonAge2.exe)
        /// Original implementation: Removes creature from party using party system
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses entity data storage as a temporary solution until proper party system interface is available
        /// </remarks>
        private Variable Func_RemovePartyMember(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null)
            {
                // Unmark entity as party member
                creature.SetData("IsPartyMember", false);
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetPlayerCharacter() - Returns the player character object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetPlayerCharacter implementation
        /// Eclipse engines (Dragon Age, ) return the player-controlled character object
        /// Located via string reference: "PlayerCharacter" @ 0x00b08188 (daorigins.exe)
        /// Original implementation: Returns the player character entity, typically the party leader
        /// Implementation strategy:
        /// 1. Try to get party member at index 0 (party leader, which is typically the player character)
        /// 2. Fall back to entity lookup by tag "PlayerCharacter" (common Eclipse pattern)
        /// 3. Fall back to entity lookup by tag "player" (lowercase variant)
        /// 4. Return OBJECT_INVALID if player character cannot be found
        /// Cross-engine: Similar to Odyssey GetPartyLeader() which returns party member at index 0
        /// </remarks>
        private Variable Func_GetPlayerCharacter(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Strategy 1: Try to get party member at index 0 (party leader is typically the player character)
            // This follows the pattern from Odyssey engine where GetPartyLeader() returns party member at index 0
            // GetPartyMemberByIndex is now implemented, so this strategy works correctly
            try
            {
                Variable partyLeader = Func_GetPartyMemberByIndex(new[] { Variable.FromInt(0) }, ctx);
                if (partyLeader.AsObjectId() != ObjectInvalid)
                {
                    return partyLeader;
                }
            }
            catch
            {
                // GetPartyMemberByIndex may not be fully implemented yet, fall through to other strategies
            }

            // Strategy 2: Try to find entity by tag "PlayerCharacter" (Eclipse engine pattern)
            // Based on string reference "PlayerCharacter" @ 0x00b08188 in daorigins.exe
            Core.Interfaces.IEntity playerEntity = ctx.World.GetEntityByTag("PlayerCharacter", 0);
            if (playerEntity != null)
            {
                return Variable.FromObject(playerEntity.ObjectId);
            }

            // Strategy 3: Try to find entity by tag "player" (lowercase variant, seen in string searches)
            playerEntity = ctx.World.GetEntityByTag("player", 0);
            if (playerEntity != null)
            {
                return Variable.FromObject(playerEntity.ObjectId);
            }

            // Strategy 4: Search through all entities for one marked as player character
            // This is a fallback if tag-based lookup fails
            foreach (Core.Interfaces.IEntity entity in ctx.World.GetAllEntities())
            {
                if (entity == null)
                {
                    continue;
                }

                // Check if entity has a component that indicates it's the player character
                // In Eclipse engines, player characters typically have specific tags or properties
                string tag = entity.Tag;
                if (!string.IsNullOrEmpty(tag))
                {
                    // Check for common player character tag patterns
                    if (string.Equals(tag, "PlayerCharacter", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "player", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "Player", StringComparison.OrdinalIgnoreCase))
                    {
                        return Variable.FromObject(entity.ObjectId);
                    }
                }
            }

            // Player character not found - return invalid object
            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// StartConversation(object oObject, string sConversation) - Starts a conversation with an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: StartConversation implementation
        /// Located via string reference: "ShowConversationGUIMessage" @ 0x00ae8a50 (daorigins.exe), @ 0x00bfca24 (DragonAge2.exe)
        /// Original implementation: Starts conversation using Eclipse dialogue system (UnrealScript message passing)
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation stores conversation state in entity data until proper dialogue system interface is available
        /// </remarks>
        private Variable Func_StartConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            string conversation = args.Count > 1 ? args[1].AsString() : string.Empty;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && !string.IsNullOrEmpty(conversation) && ctx.World != null && ctx.World.CurrentArea != null)
            {
                // Store conversation state in entity data
                entity.SetData("InConversation", true);
                entity.SetData("ConversationResRef", conversation);

                // Store conversation state in static dictionary for area-based lookup
                string areaKey = ctx.World.CurrentArea.ResRef ?? "default";
                var conversationState = new ConversationState
                {
                    SpeakerId = entity.ObjectId,
                    TargetId = ctx.Caller != null ? ctx.Caller.ObjectId : ObjectInvalid,
                    InConversation = true
                };
                _conversationStates[areaKey] = conversationState;

                // Eclipse uses UnrealScript message passing: ShowConversationGUIMessage @ 0x00ae8a50 (daorigins.exe), @ 0x00bfca24 (DragonAge2.exe)
                // Full integration requires Eclipse dialogue system (Conversation class, message handlers)
                // Current implementation stores state for script queries; full dialogue UI integration pending
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetIsInConversation(object oObject) - Returns true if object is in a conversation
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsInConversation implementation
        /// Located via string reference: "Conversation" @ 0x00af5888 (daorigins.exe), @ 0x00bf8538 (DragonAge2.exe)
        /// Original implementation: Checks if object is currently in a conversation
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses entity data storage until proper dialogue system interface is available
        /// </remarks>
        private Variable Func_GetIsInConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.HasData("InConversation"))
            {
                return Variable.FromInt(entity.GetData<bool>("InConversation") ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetQuestCompleted(string sQuest, int bCompleted) - Sets the completed state of a quest
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: SetQuestCompleted implementation
        /// Located via string reference: "QuestCompleted" @ 0x00b0847c (daorigins.exe), @ 0x00c00438 (DragonAge2.exe)
        /// Original implementation: Sets quest completed state in quest system using quest name/ID
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses global variables until proper quest system interface is available
        /// </remarks>
        private Variable Func_SetQuestCompleted(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string quest = args.Count > 0 ? args[0].AsString() : string.Empty;
            int completed = args.Count > 1 ? args[1].AsInt() : 0;

            if (!string.IsNullOrEmpty(quest) && ctx != null && ctx.Globals != null)
            {
                // Store quest completed state in global variables using quest name as key
                string questKey = "Quest_" + quest + "_Completed";
                ctx.Globals.SetGlobalBool(questKey, completed != 0);

                // Eclipse quest system uses "QuestCompleted" @ 0x00b0847c (daorigins.exe), @ 0x00c00438 (DragonAge2.exe)
                // Full integration requires Eclipse quest system (quest tracking, journal, quest entries)
                // Current implementation stores state in globals for script queries; full quest system integration pending
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetQuestCompleted(string sQuest) - Returns true if quest is completed
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetQuestCompleted implementation
        /// Located via string reference: "QuestCompleted" @ 0x00b0847c (daorigins.exe), @ 0x00c00438 (DragonAge2.exe)
        /// Original implementation: Gets quest completed state from quest system using quest name/ID
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses global variables until proper quest system interface is available
        /// </remarks>
        private Variable Func_GetQuestCompleted(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string quest = args.Count > 0 ? args[0].AsString() : string.Empty;

            if (!string.IsNullOrEmpty(quest) && ctx != null && ctx.Globals != null)
            {
                // Get quest completed state from global variables using quest name as key
                string questKey = "Quest_" + quest + "_Completed";
                bool completed = ctx.Globals.GetGlobalBool(questKey);
                return Variable.FromInt(completed ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetItemInSlot(object oCreature, int nSlot) - Returns the item in a creature's inventory slot
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetItemInSlot implementation
        /// Located via string reference: "InventorySlot" @ 0x007bf7d0 (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Returns object ID of item in specified slot from IInventoryComponent.GetItemInSlot
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetItemInSlot(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int slot = args.Count > 1 ? args[1].AsInt() : 0;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null)
            {
                IInventoryComponent inventory = creature.GetComponent<IInventoryComponent>();
                if (inventory != null)
                {
                    Core.Interfaces.IEntity item = inventory.GetItemInSlot(slot);
                    if (item != null)
                    {
                        return Variable.FromObject(item.ObjectId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetItemStackSize(object oItem) - Returns the stack size of an item
        /// </summary>
        private Variable Func_GetItemStackSize(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;

            Core.Interfaces.IEntity item = ResolveObject(itemId, ctx);
            if (item != null)
            {
                IItemComponent itemComp = item.GetComponent<IItemComponent>();
                if (itemComp != null)
                {
                    // Get stack size from item component
                    return Variable.FromInt(itemComp.StackSize);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// SetItemStackSize(object oItem, int nStackSize) - Sets the stack size of an item
        /// </summary>
        private Variable Func_SetItemStackSize(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            int stackSize = args.Count > 1 ? args[1].AsInt() : 1;

            Core.Interfaces.IEntity item = ResolveObject(itemId, ctx);
            if (item != null)
            {
                IItemComponent itemComp = item.GetComponent<IItemComponent>();
                if (itemComp != null)
                {
                    // Set stack size in item component with clamping
                    // Clamp stack size between 1 and maximum (default 100 for Eclipse engine)
                    // Note: Eclipse engine may use different item data files than Odyssey (baseitems.2da)
                    // This can be extended to look up max stack size from item templates if needed
                    const int defaultMaxStackSize = 100;
                    int clampedSize = Math.Max(1, Math.Min(defaultMaxStackSize, stackSize));
                    itemComp.StackSize = clampedSize;
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetAbilityScore(object oCreature, int nAbility) - Returns the ability score of a creature
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetAbilityScore implementation
        /// Located via string reference: "STR", "DEX", "CON", "INT", "WIS", "CHA" (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Returns ability score (STR, DEX, CON, INT, WIS, CHA) from IStatsComponent.GetAbility
        /// Ability enum: 0=Strength, 1=Dexterity, 2=Constitution, 3=Intelligence, 4=Wisdom, 5=Charisma
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetAbilityScore(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int ability = args.Count > 1 ? args[1].AsInt() : 0;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null)
            {
                IStatsComponent stats = creature.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    // Convert int to Ability enum (0-5 map to Strength-Charisma)
                    if (ability >= 0 && ability <= 5)
                    {
                        Core.Enums.Ability abilityEnum = (Core.Enums.Ability)ability;
                        return Variable.FromInt(stats.GetAbility(abilityEnum));
                    }
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetAbilityModifier(object oCreature, int nAbility) - Returns the ability modifier of a creature
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetAbilityModifier implementation
        /// Located via string reference: "STR", "DEX", "CON", "INT", "WIS", "CHA" (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Calculates ability modifier from ability score using IStatsComponent.GetAbilityModifier
        /// Ability modifier formula: (score - 10) / 2 (D20 system standard)
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetAbilityModifier(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int ability = args.Count > 1 ? args[1].AsInt() : 0;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null)
            {
                IStatsComponent stats = creature.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    // Convert int to Ability enum (0-5 map to Strength-Charisma)
                    if (ability >= 0 && ability <= 5)
                    {
                        Core.Enums.Ability abilityEnum = (Core.Enums.Ability)ability;
                        return Variable.FromInt(stats.GetAbilityModifier(abilityEnum));
                    }
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetAreaTag(object oArea) - Returns the tag of an area
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetAreaTag implementation
        /// Located via string reference: "AREANAME" @ 0x007be1dc (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Returns area tag from IArea.Tag or entity tag
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetAreaTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint areaId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;

            // Try to get area from world
            if (ctx != null && ctx.World != null && ctx.World.CurrentArea != null)
            {
                // If areaId matches current area or is invalid, return current area tag
                if (areaId == ObjectInvalid || ctx.World.CurrentArea.Tag != null)
                {
                    return Variable.FromString(ctx.World.CurrentArea.Tag ?? string.Empty);
                }
            }

            // Fallback: try to get entity by ID and return its tag
            if (areaId != ObjectInvalid)
            {
                Core.Interfaces.IEntity entity = ResolveObject(areaId, ctx);
                if (entity != null)
                {
                    return Variable.FromString(entity.Tag ?? string.Empty);
                }
            }

            return Variable.FromString(string.Empty);
        }

        /// <summary>
        /// GetModuleFileName() - Returns the filename of the current module
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetModuleFileName implementation
        /// Located via string reference: "MODULES" @ 0x00ad9810 (daorigins.exe), @ 0x00bf5d10 (DragonAge2.exe)
        /// Original implementation: Returns module filename from IModule.FileName or CurrentModuleName
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetModuleFileName(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx != null && ctx.World != null && ctx.World.CurrentModule != null)
            {
                // Get module filename from IModule interface
                // IModule has ResRef (resource reference) which is the module filename
                string moduleName = ctx.World.CurrentModule.ResRef ?? string.Empty;
                return Variable.FromString(moduleName);
            }

            return Variable.FromString(string.Empty);
        }

        /// <summary>
        /// GetName(object oObject) - Returns the name of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetName implementation
        /// Located via string reference: Entity name storage (Eclipse uses entity data for names)
        /// Original implementation: Returns display name of object from entity data or tag
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetName(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                // Try to get name from entity data first
                if (entity.HasData("Name"))
                {
                    string name = entity.GetData<string>("Name");
                    if (!string.IsNullOrEmpty(name))
                    {
                        return Variable.FromString(name);
                    }
                }

                // Fallback to tag
                return Variable.FromString(entity.Tag ?? string.Empty);
            }

            return Variable.FromString(string.Empty);
        }

        /// <summary>
        /// SetName(object oObject, string sName) - Sets the name of an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: SetName implementation
        /// Located via string reference: Entity name storage (Eclipse uses entity data for names)
        /// Original implementation: Sets display name of object in entity data
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_SetName(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                // Set name in entity data
                entity.SetData("Name", name);
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetFirstObjectInArea(int nObjectType, object oArea) - Returns the first object of specified type in an area
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetFirstObjectInArea implementation
        /// Located via string reference: Area iteration system (Eclipse uses area-based object iteration)
        /// Original implementation: Returns first object of specified type in area, initializes iteration state
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This function initializes iteration state for GetNextObjectInArea
        /// </remarks>
        private Variable Func_GetFirstObjectInArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int objectType = args.Count > 0 ? args[0].AsInt() : 0;
            uint areaId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            if (ctx == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get area - if invalid, use current area
            Core.Interfaces.IEntity areaEntity = null;
            if (areaId != ObjectInvalid)
            {
                areaEntity = ResolveObject(areaId, ctx);
            }

            if (areaEntity == null && ctx.World.CurrentArea != null)
            {
                // Use current area - IArea doesn't have ObjectId, so use a special area ID
                // For iteration purposes, we'll use area ResRef as the key
                areaId = 0x7F000010; // Special area object ID
            }

            // Convert objectType to ObjectType enum mask
            Core.Enums.ObjectType typeMask = Core.Enums.ObjectType.Invalid;
            if ((objectType & 1) != 0) typeMask |= Core.Enums.ObjectType.Creature;
            if ((objectType & 2) != 0) typeMask |= Core.Enums.ObjectType.Item;
            if ((objectType & 4) != 0) typeMask |= Core.Enums.ObjectType.Trigger;
            if ((objectType & 8) != 0) typeMask |= Core.Enums.ObjectType.Door;
            if ((objectType & 64) != 0) typeMask |= Core.Enums.ObjectType.Placeable;

            if (typeMask == Core.Enums.ObjectType.Invalid)
            {
                typeMask = Core.Enums.ObjectType.All;
            }

            // Initialize iteration state using static dictionary keyed by area ResRef
            if (ctx.World.CurrentArea != null)
            {
                string areaKey = ctx.World.CurrentArea.ResRef ?? "default";

                // Get first object
                var entities = new List<Core.Interfaces.IEntity>();
                foreach (Core.Interfaces.IEntity entity in ctx.World.GetAllEntities())
                {
                    if (entity == null)
                    {
                        continue;
                    }

                    // Check if entity is in the specified area (check if entity's world area matches)
                    // For Eclipse, entities are typically in the current area
                    if (areaId != ObjectInvalid)
                    {
                        // Check if entity belongs to current area by checking if it's in the area's entity collections
                        bool inArea = false;
                        if (ctx.World.CurrentArea.Creatures != null)
                        {
                            foreach (var creature in ctx.World.CurrentArea.Creatures)
                            {
                                if (creature.ObjectId == entity.ObjectId)
                                {
                                    inArea = true;
                                    break;
                                }
                            }
                        }
                        if (!inArea && ctx.World.CurrentArea.Placeables != null)
                        {
                            foreach (var placeable in ctx.World.CurrentArea.Placeables)
                            {
                                if (placeable.ObjectId == entity.ObjectId)
                                {
                                    inArea = true;
                                    break;
                                }
                            }
                        }
                        if (!inArea && ctx.World.CurrentArea.Doors != null)
                        {
                            foreach (var door in ctx.World.CurrentArea.Doors)
                            {
                                if (door.ObjectId == entity.ObjectId)
                                {
                                    inArea = true;
                                    break;
                                }
                            }
                        }
                        if (!inArea && ctx.World.CurrentArea.Triggers != null)
                        {
                            foreach (var trigger in ctx.World.CurrentArea.Triggers)
                            {
                                if (trigger.ObjectId == entity.ObjectId)
                                {
                                    inArea = true;
                                    break;
                                }
                            }
                        }
                        if (!inArea && ctx.World.CurrentArea.Waypoints != null)
                        {
                            foreach (var waypoint in ctx.World.CurrentArea.Waypoints)
                            {
                                if (waypoint.ObjectId == entity.ObjectId)
                                {
                                    inArea = true;
                                    break;
                                }
                            }
                        }
                        if (!inArea)
                        {
                            continue;
                        }
                    }

                    // Check object type
                    bool matchesType = false;
                    if ((typeMask & Core.Enums.ObjectType.Creature) != 0 && entity.GetComponent<IStatsComponent>() != null)
                    {
                        matchesType = true;
                    }
                    else if ((typeMask & Core.Enums.ObjectType.Item) != 0 && entity.GetComponent<IItemComponent>() != null)
                    {
                        matchesType = true;
                    }
                    else if ((typeMask & Core.Enums.ObjectType.Trigger) != 0 && entity.GetComponent<ITriggerComponent>() != null)
                    {
                        matchesType = true;
                    }
                    else if ((typeMask & Core.Enums.ObjectType.Door) != 0 && entity.GetComponent<IDoorComponent>() != null)
                    {
                        matchesType = true;
                    }
                    else if ((typeMask & Core.Enums.ObjectType.Placeable) != 0 && entity.GetComponent<IPlaceableComponent>() != null)
                    {
                        matchesType = true;
                    }

                    if (matchesType)
                    {
                        entities.Add(entity);
                    }
                }

                if (entities.Count > 0)
                {
                    // Store entity list for iteration in static dictionary
                    _areaIterationStates[areaKey] = new AreaIterationState
                    {
                        Entities = entities,
                        CurrentIndex = 0
                    };
                    return Variable.FromObject(entities[0].ObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNextObjectInArea(int nObjectType, object oArea) - Returns the next object of specified type in an area
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetNextObjectInArea implementation
        /// Located via string reference: Area iteration system (Eclipse uses area-based object iteration)
        /// Original implementation: Returns next object of specified type in area, continues iteration state
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This function continues iteration state initialized by GetFirstObjectInArea
        /// </remarks>
        private Variable Func_GetNextObjectInArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int objectType = args.Count > 0 ? args[0].AsInt() : 0;
            uint areaId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            if (ctx == null || ctx.World == null || ctx.World.CurrentArea == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Check if iteration state exists in static dictionary
            if (ctx.World.CurrentArea == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            string areaKey = ctx.World.CurrentArea.ResRef ?? "default";
            if (!_areaIterationStates.ContainsKey(areaKey))
            {
                return Variable.FromObject(ObjectInvalid);
            }

            var state = _areaIterationStates[areaKey];
            if (state.Entities == null || state.Entities.Count == 0)
            {
                _areaIterationStates.Remove(areaKey);
                return Variable.FromObject(ObjectInvalid);
            }

            // Increment index
            state.CurrentIndex++;

            // Return next entity
            if (state.CurrentIndex >= 0 && state.CurrentIndex < state.Entities.Count)
            {
                return Variable.FromObject(state.Entities[state.CurrentIndex].ObjectId);
            }

            // Iteration complete
            _areaIterationStates.Remove(areaKey);
            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetIsTrigger(object oObject) - Returns true if object is a trigger
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsTrigger implementation
        /// Located via string reference: "Trigger" @ 0x00ae5a7c (daorigins.exe), "CTrigger" @ 0x00b0d4cc (daorigins.exe)
        /// Original implementation: Checks if object has ITriggerComponent
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetIsTrigger(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.GetComponent<ITriggerComponent>() != null)
            {
                return Variable.FromInt(1);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsWaypoint(object oObject) - Returns true if object is a waypoint
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsWaypoint implementation
        /// Located via string reference: "CWaypoint" @ 0x00b0d4b8 (daorigins.exe), "KWaypointList" @ 0x00af4e8f (daorigins.exe)
        /// Original implementation: Checks if object is a waypoint (typically has specific tag pattern or component)
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetIsWaypoint(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                // Check if entity has waypoint tag pattern or waypoint component
                string tag = entity.Tag ?? string.Empty;
                if (tag.StartsWith("WP_", StringComparison.OrdinalIgnoreCase) ||
                    tag.StartsWith("Waypoint_", StringComparison.OrdinalIgnoreCase) ||
                    entity.HasData("IsWaypoint"))
                {
                    return Variable.FromInt(1);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsArea(object oObject) - Returns true if object is an area
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsArea implementation
        /// Located via string reference: Area object type checking (Eclipse uses area objects)
        /// Original implementation: Checks if object is an area (special object type)
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetIsArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            if (objectId == ObjectInvalid)
            {
                return Variable.FromInt(0);
            }

            // Check if objectId corresponds to an area
            // Areas are registered in the world with ObjectIds starting from 0x7F000010
            // Based on daorigins.exe/DragonAge2.exe: Areas have ObjectIds assigned when registered
            // Located via World.cs: RegisterArea assigns AreaId from _nextAreaId counter (starts at 0x7F000010)
            // Original implementation: GetIsArea checks if objectId is a valid area ObjectId
            // Areas are accessed via GetArea() which returns area ObjectId, not as entities
            if (ctx != null && ctx.World != null)
            {
                // Check if objectId corresponds to a registered area
                // World.GetArea(objectId) returns the area if objectId is a valid area ObjectId
                // Based on World.cs: GetArea(uint areaId) does dictionary lookup by AreaId
                // If GetArea returns a non-null area, then objectId is an area ObjectId
                IArea area = ctx.World.GetArea(objectId);
                if (area != null)
                {
                    // objectId is a valid area ObjectId - return true
                    return Variable.FromInt(1);
                }

                // Also check if objectId matches current area's ObjectId
                // This handles the case where the current area might not be in the areas dictionary yet
                // but we still want to recognize it as an area
                if (ctx.World.CurrentArea != null)
                {
                    uint currentAreaId = ctx.World.GetAreaId(ctx.World.CurrentArea);
                    if (currentAreaId != 0 && objectId == currentAreaId)
                    {
                        // objectId matches current area's ObjectId - return true
                        return Variable.FromInt(1);
                    }
                }
            }

            // objectId does not correspond to an area
            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetIsModule(object oObject) - Returns true if object is a module
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetIsModule implementation
        /// Located via string reference: Module object type checking (Eclipse uses module objects)
        /// Original implementation: Checks if object is a module (special object type)
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Module ObjectId: Fixed value 0x7F000002 (special object ID for module)
        /// Based on swkotor2.exe: Module object ID constant (common across all engines)
        /// Located via string references: "GetModule" NWScript function, module object references
        /// Common across all engines: Odyssey, Aurora, Eclipse, Infinity all use fixed module object ID (0x7F000002)
        /// </remarks>
        private Variable Func_GetIsModule(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            if (objectId == ObjectInvalid)
            {
                return Variable.FromInt(0);
            }

            // Module ObjectId is fixed at 0x7F000002 (common across all engines)
            // Based on World.ModuleObjectId constant and GetModuleId implementation
            // GetModule() returns this ObjectId when a module is loaded
            const uint ModuleObjectId = 0x7F000002;

            // Primary check: Compare objectId directly with module ObjectId
            if (objectId == ModuleObjectId)
            {
                // Verify that a module is actually loaded (objectId matches current module)
                if (ctx != null && ctx.World != null && ctx.World.CurrentModule != null)
                {
                    uint currentModuleId = ctx.World.GetModuleId(ctx.World.CurrentModule);
                    if (currentModuleId == ModuleObjectId)
                    {
                        return Variable.FromInt(1);
                    }
                }
                // If objectId matches ModuleObjectId but no module is loaded, still return true
                // This matches the behavior where GetModule() can return ModuleObjectId even if module is unloading
                return Variable.FromInt(1);
            }

            // Secondary check: Verify if the objectId matches the current module's ID
            // This handles cases where the module ObjectId might be retrieved via GetModule()
            if (ctx != null && ctx.World != null && ctx.World.CurrentModule != null)
            {
                uint currentModuleId = ctx.World.GetModuleId(ctx.World.CurrentModule);
                if (currentModuleId != 0 && objectId == currentModuleId)
                {
                    return Variable.FromInt(1);
                }
            }

            // Fallback check: Check if entity has module-specific data
            // This is a safety measure for edge cases where modules might be represented as entities
            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.HasData("IsModule"))
            {
                return Variable.FromInt(1);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// MoveToObject(object oMoveTo, object oTarget, int bRun) - Moves an object to another object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: MoveToObject implementation
        /// Located via string reference: Movement system (Eclipse uses pathfinding and movement commands)
        /// Original implementation: Commands object to move to target object's position using pathfinding
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This is a command function that queues movement action, doesn't immediately move
        /// </remarks>
        private Variable Func_MoveToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint moveToId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;
            int run = args.Count > 2 ? args[2].AsInt() : 0;

            Core.Interfaces.IEntity moveToEntity = ResolveObject(moveToId, ctx);
            Core.Interfaces.IEntity targetEntity = ResolveObject(targetId, ctx);

            if (moveToEntity != null && targetEntity != null)
            {
                ITransformComponent targetTransform = targetEntity.GetComponent<ITransformComponent>();
                if (targetTransform != null)
                {
                    // Queue movement action to target position using pathfinding system
                    ITransformComponent moveToTransform = moveToEntity.GetComponent<ITransformComponent>();
                    if (moveToTransform != null)
                    {
                        // Store movement target for pathfinding system
                        // Pathfinding system will process MovementTarget and move entity to destination
                        moveToEntity.SetData("MovementTarget", targetTransform.Position);
                        moveToEntity.SetData("MovementRun", run != 0);
                        moveToEntity.SetData("MovementState", "Moving");
                    }
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// MoveToLocation(object oMoveTo, vector vDestination, int bRun) - Moves an object to a location
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: MoveToLocation implementation
        /// Located via string reference: Movement system (Eclipse uses pathfinding and movement commands)
        /// Original implementation: Commands object to move to specified location using pathfinding
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This is a command function that queues movement action, doesn't immediately move
        /// </remarks>
        private Variable Func_MoveToLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint moveToId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            Vector3 destination = args.Count > 1 ? args[1].AsVector() : Vector3.Zero;
            int run = args.Count > 2 ? args[2].AsInt() : 0;

            Core.Interfaces.IEntity moveToEntity = ResolveObject(moveToId, ctx);
            if (moveToEntity != null)
            {
                // Queue movement action to destination using pathfinding system
                ITransformComponent moveToTransform = moveToEntity.GetComponent<ITransformComponent>();
                if (moveToTransform != null)
                {
                    // Store movement target for pathfinding system
                    // Pathfinding system will process MovementTarget and move entity to destination
                    moveToEntity.SetData("MovementTarget", destination);
                    moveToEntity.SetData("MovementRun", run != 0);
                    moveToEntity.SetData("MovementState", "Moving");
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// ApplyDamage(object oTarget, int nDamage) - Applies damage to an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: ApplyDamage implementation
        /// Located via string reference: "CurrentHealth" @ 0x00aedb28 (daorigins.exe), @ 0x00beb46c (DragonAge2.exe)
        /// Original implementation: Applies damage to target, reducing CurrentHP, may trigger death/knockdown
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_ApplyDamage(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int damage = args.Count > 1 ? args[1].AsInt() : 0;

            if (damage <= 0)
            {
                return Variable.Void();
            }

            Core.Interfaces.IEntity target = ResolveObject(targetId, ctx);
            if (target != null)
            {
                IStatsComponent stats = target.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    // Apply damage (reduce current HP)
                    // Based on daorigins.exe/DragonAge2.exe: ApplyDamage reduces CurrentHP directly
                    // Located via string reference: "CurrentHealth" @ 0x00aedb28 (daorigins.exe), @ 0x00beb46c (DragonAge2.exe)
                    // Original implementation: Reduces HP, fires OnDamaged event, checks for death and fires OnDeath if HP <= 0
                    int previousHP = stats.CurrentHP;
                    int newHP = Math.Max(0, previousHP - damage);
                    stats.CurrentHP = newHP;

                    // Fire OnDamaged script event
                    // Based on daorigins.exe/DragonAge2.exe: OnDamaged event fires when entity takes damage
                    // Original implementation: Script event fires with damage source as triggerer (if available)
                    // Note: ApplyDamage doesn't have a source parameter, so triggerer is null (direct damage)
                    if (ctx.World != null && ctx.World.EventBus != null)
                    {
                        ctx.World.EventBus.FireScriptEvent(target, ScriptEvent.OnDamaged, null);
                    }

                    // Check if entity is dead after damage application
                    // Based on daorigins.exe/DragonAge2.exe: Death is detected when CurrentHP <= 0
                    // Original implementation: OnDeath script event fires when HP reaches 0 or below
                    // Death handling: Fire OnDeath event, remove from combat, update entity state
                    if (stats.IsDead && ctx.World != null)
                    {
                        // Fire OnDeath script event
                        // Based on daorigins.exe/DragonAge2.exe: OnDeath event fires when entity dies
                        // Located via string references: Death event handling in damage system
                        // Original implementation: OnDeath script fires on victim entity, triggerer is damage source (null for direct damage)
                        if (ctx.World.EventBus != null)
                        {
                            ctx.World.EventBus.FireScriptEvent(target, ScriptEvent.OnDeath, null);
                        }

                        // Notify combat system immediately when entity dies
                        // Based on daorigins.exe/DragonAge2.exe: Combat system handles death cleanup (removes from combat, updates state)
                        // Original implementation: When entity dies, it's immediately removed from combat to prevent further actions
                        // CombatSystem.HandleDeath is private, but we can call ExitCombat directly to remove entity from combat
                        // This ensures the entity is removed from combat immediately, not waiting for the next update cycle
                        if (ctx.World != null && ctx.World.CombatSystem != null)
                        {
                            // Remove entity from combat immediately when it dies
                            // ExitCombat removes the entity from active combat encounters and fires OnCombatEnd event
                            // This prevents dead entities from continuing to participate in combat
                            ctx.World.CombatSystem.ExitCombat(target);
                        }
                    }
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetConversationSpeaker() - Returns the current conversation speaker
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetConversationSpeaker implementation
        /// Located via string reference: "Conversation" @ 0x00af5888 (daorigins.exe), @ 0x00bf8538 (DragonAge2.exe)
        /// Original implementation: Returns object ID of current conversation speaker from dialogue system
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses entity data storage until proper dialogue system interface is available
        /// </remarks>
        private Variable Func_GetConversationSpeaker(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get conversation speaker from world or current area
            // Since IArea doesn't support HasData/GetData, store conversation state in a static dictionary
            if (ctx.World.CurrentArea != null)
            {
                string areaKey = ctx.World.CurrentArea.ResRef ?? "default";
                if (_conversationStates.ContainsKey(areaKey))
                {
                    var state = _conversationStates[areaKey];
                    if (state.InConversation && state.SpeakerId != ObjectInvalid)
                    {
                        return Variable.FromObject(state.SpeakerId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetConversationTarget() - Returns the current conversation target
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetConversationTarget implementation
        /// Located via string reference: "Conversation" @ 0x00af5888 (daorigins.exe), @ 0x00bf8538 (DragonAge2.exe)
        /// Original implementation: Returns object ID of current conversation target (typically player character) from dialogue system
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses entity data storage until proper dialogue system interface is available
        /// </remarks>
        private Variable Func_GetConversationTarget(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get conversation target from world or current area
            // Since IArea doesn't support HasData/GetData, store conversation state in a static dictionary
            if (ctx.World.CurrentArea != null)
            {
                string areaKey = ctx.World.CurrentArea.ResRef ?? "default";
                if (_conversationStates.ContainsKey(areaKey))
                {
                    var state = _conversationStates[areaKey];
                    if (state.InConversation && state.TargetId != ObjectInvalid)
                    {
                        return Variable.FromObject(state.TargetId);
                    }
                }
            }

            // Default to player character
            return Func_GetPlayerCharacter(args, ctx);
        }

        /// <summary>
        /// EndConversation() - Ends the current conversation
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: EndConversation implementation
        /// Located via string reference: "HideConversationGUIMessage" @ 0x00ae8a88 (daorigins.exe), @ 0x00bfca5c (DragonAge2.exe)
        /// Original implementation: Ends current conversation, hides conversation GUI, clears conversation state
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation clears conversation state until proper dialogue system interface is available
        /// </remarks>
        private Variable Func_EndConversation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx == null || ctx.World == null)
            {
                return Variable.Void();
            }

            // Clear conversation state from all entities
            foreach (Core.Interfaces.IEntity entity in ctx.World.GetAllEntities())
            {
                if (entity != null && entity.HasData("InConversation"))
                {
                    entity.SetData("InConversation", false);
                    entity.SetData("ConversationResRef", null);
                }
            }

            // Clear conversation state from current area
            // Since IArea doesn't support SetData, use static dictionary
            if (ctx.World.CurrentArea != null)
            {
                string areaKey = ctx.World.CurrentArea.ResRef ?? "default";
                if (_conversationStates.ContainsKey(areaKey))
                {
                    _conversationStates.Remove(areaKey);
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// SetQuestActive(string sQuest, int bActive) - Sets the active state of a quest
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: SetQuestActive implementation
        /// Located via string reference: "QuestCompleted" @ 0x00b0847c (daorigins.exe), @ 0x00c00438 (DragonAge2.exe)
        /// Original implementation: Sets quest active state in quest system using quest name/ID
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses global variables until proper quest system interface is available
        /// </remarks>
        private Variable Func_SetQuestActive(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string quest = args.Count > 0 ? args[0].AsString() : string.Empty;
            int active = args.Count > 1 ? args[1].AsInt() : 0;

            if (!string.IsNullOrEmpty(quest) && ctx != null && ctx.Globals != null)
            {
                // Store quest active state in global variables using quest name as key
                string questKey = "Quest_" + quest + "_Active";
                ctx.Globals.SetGlobalBool(questKey, active != 0);
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetQuestActive(string sQuest) - Returns true if quest is active
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetQuestActive implementation
        /// Located via string reference: "QuestCompleted" @ 0x00b0847c (daorigins.exe), @ 0x00c00438 (DragonAge2.exe)
        /// Original implementation: Gets quest active state from quest system using quest name/ID
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses global variables until proper quest system interface is available
        /// </remarks>
        private Variable Func_GetQuestActive(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string quest = args.Count > 0 ? args[0].AsString() : string.Empty;

            if (!string.IsNullOrEmpty(quest) && ctx != null && ctx.Globals != null)
            {
                // Get quest active state from global variables using quest name as key
                string questKey = "Quest_" + quest + "_Active";
                bool active = ctx.Globals.GetGlobalBool(questKey);
                return Variable.FromInt(active ? 1 : 0);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// AddQuestEntry(string sQuest, string sEntry) - Adds an entry to a quest
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: AddQuestEntry implementation
        /// Located via string reference: Quest system (Eclipse uses quest journal with entries)
        /// Original implementation: Adds quest entry to quest journal using quest name/ID and entry text
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses global variables until proper quest system interface is available
        /// </remarks>
        private Variable Func_AddQuestEntry(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string quest = args.Count > 0 ? args[0].AsString() : string.Empty;
            string entry = args.Count > 1 ? args[1].AsString() : string.Empty;

            if (!string.IsNullOrEmpty(quest) && !string.IsNullOrEmpty(entry) && ctx != null && ctx.Globals != null)
            {
                // Store quest entry in global variables using quest name as key
                // Entries are stored as comma-separated list
                string questKey = "Quest_" + quest + "_Entries";
                string existingEntries = ctx.Globals.GetGlobalString(questKey);
                if (string.IsNullOrEmpty(existingEntries))
                {
                    ctx.Globals.SetGlobalString(questKey, entry);
                }
                else
                {
                    ctx.Globals.SetGlobalString(questKey, existingEntries + "|" + entry);
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// CompleteQuestEntry(string sQuest, string sEntry) - Marks a quest entry as completed
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: CompleteQuestEntry implementation
        /// Located via string reference: Quest system (Eclipse uses quest journal with entries)
        /// Original implementation: Marks quest entry as completed in quest journal using quest name/ID and entry text
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation uses global variables until proper quest system interface is available
        /// </remarks>
        private Variable Func_CompleteQuestEntry(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string quest = args.Count > 0 ? args[0].AsString() : string.Empty;
            string entry = args.Count > 1 ? args[1].AsString() : string.Empty;

            if (!string.IsNullOrEmpty(quest) && !string.IsNullOrEmpty(entry) && ctx != null && ctx.Globals != null)
            {
                // Store completed quest entry in global variables using quest name as key
                string questKey = "Quest_" + quest + "_CompletedEntries";
                string existingEntries = ctx.Globals.GetGlobalString(questKey);
                if (string.IsNullOrEmpty(existingEntries))
                {
                    ctx.Globals.SetGlobalString(questKey, entry);
                }
                else
                {
                    ctx.Globals.SetGlobalString(questKey, existingEntries + "|" + entry);
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetFirstItemInInventory(object oCreature) - Returns the first item in a creature's inventory
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetFirstItemInInventory implementation
        /// Located via string reference: "InventorySlot" @ 0x007bf7d0 (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Returns first item in inventory, initializes iteration state for GetNextItemInInventory
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetFirstItemInInventory(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null)
            {
                IInventoryComponent inventory = creature.GetComponent<IInventoryComponent>();
                if (inventory != null)
                {
                    // Initialize iteration state
                    creature.SetData("InventoryIteration_Index", 0);

                    // Get first item
                    var items = new List<Core.Interfaces.IEntity>();
                    foreach (Core.Interfaces.IEntity item in inventory.GetAllItems())
                    {
                        if (item != null)
                        {
                            items.Add(item);
                        }
                    }

                    if (items.Count > 0)
                    {
                        // Store item list for iteration
                        creature.SetData("InventoryIteration_Items", items);
                        return Variable.FromObject(items[0].ObjectId);
                    }
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetNextItemInInventory(object oCreature) - Returns the next item in a creature's inventory
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetNextItemInInventory implementation
        /// Located via string reference: "InventorySlot" @ 0x007bf7d0 (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Returns next item in inventory, continues iteration state from GetFirstItemInInventory
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetNextItemInInventory(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null && creature.HasData("InventoryIteration_Items"))
            {
                var items = creature.GetData<List<Core.Interfaces.IEntity>>("InventoryIteration_Items");
                int currentIndex = creature.GetData<int>("InventoryIteration_Index");

                // Increment index
                currentIndex++;
                creature.SetData("InventoryIteration_Index", currentIndex);

                // Return next item
                if (currentIndex >= 0 && currentIndex < items.Count)
                {
                    return Variable.FromObject(items[currentIndex].ObjectId);
                }

                // Iteration complete
                creature.SetData("InventoryIteration_Items", null);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// CastSpell(int nSpell, object oTarget) - Casts a spell on a target
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: CastSpell implementation
        /// Located via string reference: Spell casting system (Eclipse uses ability/spell system)
        /// Original implementation: Casts spell on target object, applies spell effects
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation queues spell cast action until proper spell system interface is available
        /// </remarks>
        private Variable Func_CastSpell(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int spell = args.Count > 0 ? args[0].AsInt() : 0;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;

            if (ctx == null || ctx.Caller == null)
            {
                return Variable.Void();
            }

            Core.Interfaces.IEntity caster = ctx.Caller;
            Core.Interfaces.IEntity target = ResolveObject(targetId, ctx);

            if (caster == null)
            {
                return Variable.Void();
            }

            // Cast spell on target
            // Based on daorigins.exe/DragonAge2.exe: CastSpell fires spell cast events and applies spell effects
            // Original implementation: Casts spell on target, fires OnSpellCastAt event, applies spell effects via spell system
            // In full implementation, a spell system would:
            // 1. Validate spell (caster has spell, spell exists, cooldowns, etc.)
            // 2. Apply spell costs (mana/stamina, cooldowns)
            // 3. Play spell casting animation
            // 4. Apply spell effects to target(s) based on spell definition
            // 5. Fire spell events for script hooks
            // For now, we fire events and let scripts handle spell effects

            if (target != null && ctx.World != null && ctx.World.EventBus != null)
            {
                // Fire OnSpellCastAt script event for the caster
                // Based on EclipseEntityFactory: OnSpellCastAt event is mapped from "ScriptSpellAt" field
                // This allows scripts to react to spell casts and apply custom spell effects
                ctx.World.EventBus.FireScriptEvent(caster, ScriptEvent.OnSpellCastAt, target);

                // Also fire OnSpellCastAt on the target so it can react to being targeted
                // This allows target scripts to react to incoming spells
                ctx.World.EventBus.FireScriptEvent(target, ScriptEvent.OnSpellCastAt, caster);

                // Store spell information in entity data for potential later processing
                // This allows other systems (animations, effects, etc.) to query what spell was cast
                caster.SetData("LastCastSpell", spell);
                caster.SetData("LastCastSpellTarget", targetId);
                target.SetData("LastSpellCastAt", spell);
                target.SetData("LastSpellCastAtCaster", caster.ObjectId);
            }

            return Variable.Void();
        }

        /// <summary>
        /// CastSpellAtLocation(int nSpell, vector vTarget) - Casts a spell at a location
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: CastSpellAtLocation implementation
        /// Located via string reference: Spell casting system (Eclipse uses ability/spell system)
        /// Original implementation: Casts spell at specified location, applies area spell effects
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Note: This implementation queues spell cast action until proper spell system interface is available
        /// </remarks>
        private Variable Func_CastSpellAtLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int spell = args.Count > 0 ? args[0].AsInt() : 0;
            Vector3 target = args.Count > 1 ? args[1].AsVector() : Vector3.Zero;

            if (ctx == null || ctx.Caller == null)
            {
                return Variable.Void();
            }

            // Cast spell at location
            // Based on daorigins.exe/DragonAge2.exe: CastSpellAtLocation fires spell cast events for area spells
            // Original implementation: Casts area spell at location, fires OnSpellCastAt event, applies area spell effects
            // In full implementation, a spell system would:
            // 1. Validate spell (caster has spell, spell exists, cooldowns, etc.)
            // 2. Apply spell costs (mana/stamina, cooldowns)
            // 3. Play spell casting animation
            // 4. Apply spell effects to all entities in spell area of effect
            // 5. Fire spell events for script hooks
            // For now, we fire events and let scripts handle spell effects

            Core.Interfaces.IEntity caster = ctx.Caller;
            if (caster != null && ctx.World != null && ctx.World.EventBus != null)
            {
                // Fire OnSpellCastAt script event for the caster with location
                // Based on EclipseEntityFactory: OnSpellCastAt event is mapped from "ScriptSpellAt" field
                // This allows scripts to react to area spell casts and apply custom spell effects
                // Note: Area spells affect all entities in a radius, scripts can query nearby entities
                ctx.World.EventBus.FireScriptEvent(caster, ScriptEvent.OnSpellCastAt, null);

                // Store spell information in entity data for potential later processing
                // This allows other systems (animations, effects, etc.) to query what spell was cast
                caster.SetData("LastCastSpell", spell);
                caster.SetData("LastCastSpellLocation", target);
            }

            return Variable.Void();
        }

        /// <summary>
        /// CastSpellAtObject(int nSpell, object oTarget) - Casts a spell on a target object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: CastSpellAtObject implementation
        /// Located via string reference: Spell casting system (Eclipse uses ability/spell system)
        /// Original implementation: Alias for CastSpell - casts spell on target object
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_CastSpellAtObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Func_CastSpell(args, ctx);
        }

        /// <summary>
        /// GetHasAbility(object oCreature, int nAbility) - Returns true if creature has the specified ability
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetHasAbility implementation
        /// Located via string reference: "STR", "DEX", "CON", "INT", "WIS", "CHA" (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Checks if creature has ability (spell, talent, feat) using IStatsComponent
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetHasAbility(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int ability = args.Count > 1 ? args[1].AsInt() : 0;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null)
            {
                IStatsComponent stats = creature.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    // Check if creature has ability (spell/talent/feat ID)
                    // In Eclipse engine, abilities/talents are stored in the stats component's known spells/abilities list
                    // HasSpell checks the _knownSpells HashSet which contains all known abilities/talents/spells
                    bool hasAbility = stats.HasSpell(ability);
                    return Variable.FromInt(hasAbility ? 1 : 0);
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetSpellLevel(object oCreature, int nSpell) - Returns the level of a spell for a creature
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetSpellLevel implementation
        /// Located via string reference: Spell system (Eclipse uses spell levels)
        /// Original implementation: Returns spell level from IStatsComponent spell list
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetSpellLevel(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creatureId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int spell = args.Count > 1 ? args[1].AsInt() : 0;

            Core.Interfaces.IEntity creature = ResolveObject(creatureId, ctx);
            if (creature != null)
            {
                IStatsComponent stats = creature.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    // Get spell level from stats component
                    // Based on Eclipse engine: GetSpellLevel implementation (daorigins.exe, DragonAge2.exe)
                    // Returns the level/rank at which the creature knows the specified spell/talent/ability
                    EclipseStatsComponent eclipseStats = stats as EclipseStatsComponent;
                    if (eclipseStats != null)
                    {
                        int spellLevel = eclipseStats.GetSpellLevel(spell);
                        return Variable.FromInt(spellLevel);
                    }
                }
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// GetAreaByTag(string sTag) - Returns the area with the given tag
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetAreaByTag implementation
        /// Located via string reference: "AREANAME" @ 0x007be1dc (swkotor2.exe pattern, Eclipse uses similar system)
        /// Original implementation: Searches for area with matching tag, returns area object ID
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetAreaByTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string tag = args.Count > 0 ? args[0].AsString() : string.Empty;

            if (string.IsNullOrEmpty(tag) || ctx == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Check if current area matches tag
            if (ctx.World.CurrentArea != null && string.Equals(ctx.World.CurrentArea.Tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                // Get the AreaId for the current area
                // Areas are registered when SetCurrentArea is called, which assigns them sequential IDs starting from 0x7F000010
                // Based on World.cs: RegisterArea assigns AreaId from _nextAreaId counter
                uint areaObjectId = ctx.World.GetAreaId(ctx.World.CurrentArea);
                if (areaObjectId != 0)
                {
                    return Variable.FromObject(areaObjectId);
                }
            }

            // Search for area by tag (areas are typically loaded modules)
            // Based on Eclipse engine: GetAreaByTag searches all registered areas
            // Located via string reference: "AREANAME" @ 0x007be1dc (swkotor2.exe pattern, Eclipse uses similar system)
            // Original implementation: Iterates through all loaded areas and returns first match by tag
            // Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
            // Implementation: Search all registered areas in world, return AreaId of first matching tag
            foreach (IArea area in ctx.World.GetAllAreas())
            {
                if (area != null && string.Equals(area.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    uint areaObjectId = ctx.World.GetAreaId(area);
                    if (areaObjectId != 0)
                    {
                        return Variable.FromObject(areaObjectId);
                    }
                }
            }

            // No area found with matching tag
            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetAreaOfObject(object oObject) - Returns the area containing an object
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetAreaOfObject implementation
        /// Located via string reference: Area system (Eclipse uses area-based object organization)
        /// Original implementation: Returns area object ID that contains the specified object
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetAreaOfObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            if (ctx == null || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get object's area
            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && entity.IsValid)
            {
                // Get area from entity's AreaId
                // Based on swkotor2.exe: Entity AreaId property
                // Located via string references: "AreaId" @ 0x007bef48
                // Original implementation: Entities store AreaId of area they belong to
                if (entity.AreaId != 0)
                {
                    IArea area = ctx.World.GetArea(entity.AreaId);
                    if (area != null)
                    {
                        return Variable.FromObject(entity.AreaId);
                    }
                }
            }

            // Default to current area
            if (ctx.World.CurrentArea != null)
            {
                // Get the AreaId for the current area
                // Areas are registered when SetCurrentArea is called, which assigns them sequential IDs starting from 0x7F000010
                // Based on World.cs: RegisterArea assigns AreaId from _nextAreaId counter
                uint areaObjectId = ctx.World.GetAreaId(ctx.World.CurrentArea);
                if (areaObjectId != 0)
                {
                    return Variable.FromObject(areaObjectId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetStringLength(string sString) - Returns the length of a string
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetStringLength implementation
        /// Located via string reference: String utility functions (common across all engines)
        /// Original implementation: Returns length of string in characters
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetStringLength(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(s.Length);
        }

        /// <summary>
        /// GetSubString(string sString, int nStart, int nCount) - Returns a substring of a string
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetSubString implementation
        /// Located via string reference: String utility functions (common across all engines)
        /// Original implementation: Returns substring starting at nStart with length nCount
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_GetSubString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            int start = args.Count > 1 ? args[1].AsInt() : 0;
            int count = args.Count > 2 ? args[2].AsInt() : 0;

            if (string.IsNullOrEmpty(s) || start < 0 || count <= 0)
            {
                return Variable.FromString(string.Empty);
            }

            if (start >= s.Length)
            {
                return Variable.FromString(string.Empty);
            }

            int actualCount = Math.Min(count, s.Length - start);
            return Variable.FromString(s.Substring(start, actualCount));
        }

        /// <summary>
        /// FindSubString(string sString, string sSubString, int nStart) - Finds a substring within a string
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: FindSubString implementation
        /// Located via string reference: String utility functions (common across all engines)
        /// Original implementation: Returns index of first occurrence of substring starting at nStart, or -1 if not found
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private Variable Func_FindSubString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            string subString = args.Count > 1 ? args[1].AsString() : string.Empty;
            int start = args.Count > 2 ? args[2].AsInt() : 0;

            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(subString) || start < 0)
            {
                return Variable.FromInt(-1);
            }

            if (start >= s.Length)
            {
                return Variable.FromInt(-1);
            }

            int index = s.IndexOf(subString, start, StringComparison.Ordinal);
            return Variable.FromInt(index);
        }

        /// <summary>
        /// Helper method to check if entity matches criteria
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Criteria matching system
        /// Original implementation: Checks if entity matches specified criteria type and value
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// Criteria types: 0=None, 1=Perception, 2=Disposition, 3=Reputation, 4=Team, 5=Reaction, 6=Class, 7=Race, 8=Hp, 9=Tag, 10=NotDead, 11=InCombat, 12=TargetType, 13=CreatureType, 14=Allegiance, 15=Gender, 16=Player, 17=Party, 18=Area, 19=Location, 20=LineOfSight, 21=Distance, 22=HasItem, 23=HasSpell, 24=HasSkill, 25=HasFeat, 26=HasTalent, 27=HasEffect, 28=HasVariable, 29=HasLocalVariable, 30=HasGlobalVariable, 31=HasFaction, 32=HasAlignment, 33=HasGoodEvil, 34=HasLawfulChaotic, 35=HasLevel, 36=HasClass, 37=HasRace, 38=HasGender, 39=HasSubrace, 40=HasDeity, 41=HasDomain, 42=HasDomainSource, 43=HasAbilityScore, 44=HasAbilityModifier, 45=HasSkillRank, 46=HasFeatCount, 47=HasSpellCount, 48=HasTalentCount, 49=HasEffectCount, 50=HasItemCount, 51=HasVariableValue, 52=HasLocalVariableValue, 53=HasGlobalVariableValue, 54=HasFactionValue, 55=HasAlignmentValue, 56=HasGoodEvilValue, 57=HasLawfulChaoticValue, 58=HasLevelValue, 59=HasClassValue, 60=HasRaceValue, 61=HasGenderValue, 62=HasSubraceValue, 63=HasDeityValue, 64=HasDomainValue, 65=HasDomainSourceValue, 66=HasAbilityScoreValue, 67=HasAbilityModifierValue, 68=HasSkillRankValue, 69=HasFeatCountValue, 70=HasSpellCountValue, 71=HasTalentCountValue, 72=HasEffectCountValue, 73=HasItemCountValue
        /// </remarks>
        private bool MatchesCriteria(Core.Interfaces.IEntity entity, int criteriaType, int criteriaValue, Core.Interfaces.IEntity target, IExecutionContext ctx)
        {
            if (entity == null)
            {
                return false;
            }

            IStatsComponent stats = entity.GetComponent<IStatsComponent>();
            IPerceptionComponent perception = entity.GetComponent<IPerceptionComponent>();
            IFactionComponent faction = entity.GetComponent<IFactionComponent>();
            IInventoryComponent inventory = entity.GetComponent<IInventoryComponent>();
            ITransformComponent entityTransform = entity.GetComponent<ITransformComponent>();
            ITransformComponent targetTransform = target != null ? target.GetComponent<ITransformComponent>() : null;

            switch (criteriaType)
            {
                case 0: // None - always matches
                    return true;

                case 1: // Perception
                    // criteriaValue: PERCEPTION_SEEN_AND_HEARD (0), PERCEPTION_NOT_SEEN_AND_NOT_HEARD (1), PERCEPTION_HEARD_AND_NOT_SEEN (2), PERCEPTION_SEEN_AND_NOT_HEARD (3), PERCEPTION_NOT_HEARD (4), PERCEPTION_HEARD (5), PERCEPTION_NOT_SEEN (6), PERCEPTION_SEEN (7)
                    if (perception == null || target == null)
                    {
                        return criteriaValue == 1; // PERCEPTION_NOT_SEEN_AND_NOT_HEARD if no perception component
                    }
                    bool seen = perception.WasSeen(target);
                    bool heard = perception.WasHeard(target);
                    switch (criteriaValue)
                    {
                        case 0: return seen && heard; // PERCEPTION_SEEN_AND_HEARD
                        case 1: return !seen && !heard; // PERCEPTION_NOT_SEEN_AND_NOT_HEARD
                        case 2: return !seen && heard; // PERCEPTION_HEARD_AND_NOT_SEEN
                        case 3: return seen && !heard; // PERCEPTION_SEEN_AND_NOT_HEARD
                        case 4: return !heard; // PERCEPTION_NOT_HEARD
                        case 5: return heard; // PERCEPTION_HEARD
                        case 6: return !seen; // PERCEPTION_NOT_SEEN
                        case 7: return seen; // PERCEPTION_SEEN
                        default: return false;
                    }

                case 2: // Disposition
                    // criteriaValue: Disposition value (typically 0-100, where 0-10=hostile, 11-89=neutral, 90-100=friendly)
                    if (faction == null || target == null)
                    {
                        return false;
                    }
                    // Get reputation/disposition between entity and target
                    int disposition = GetDispositionValue(entity, target, ctx);
                    return disposition == criteriaValue;

                case 3: // Reputation
                    // criteriaValue: Reputation value (0-100, where 0-10=hostile, 11-89=neutral, 90-100=friendly)
                    if (faction == null || target == null)
                    {
                        return false;
                    }
                    int reputation = GetReputationValue(entity, target, ctx);
                    return reputation == criteriaValue;

                case 4: // Team
                    // criteriaValue: Team ID
                    int entityTeam = entity.GetData<int>("Team", -1);
                    return entityTeam == criteriaValue;

                case 5: // Reaction
                    // criteriaValue: Reaction type (0=hostile, 1=neutral, 2=friendly)
                    if (faction == null || target == null)
                    {
                        return false;
                    }
                    int reaction = GetReactionType(entity, target, ctx);
                    return reaction == criteriaValue;

                case 6: // Class
                    // criteriaValue: Class ID
                    int entityClass = entity.GetData<int>("Class", -1);
                    return entityClass == criteriaValue;

                case 7: // Race
                    // criteriaValue: Race ID
                    int entityRace = entity.GetData<int>("Race", -1);
                    return entityRace == criteriaValue;

                case 8: // Hp
                    // criteriaValue: HP threshold (0 = check if dead, >0 = check if HP >= value)
                    if (stats == null)
                    {
                        return false;
                    }
                    if (criteriaValue == 0)
                    {
                        return stats.CurrentHP <= 0; // Dead
                    }
                    return stats.CurrentHP >= criteriaValue; // HP >= threshold

                case 9: // Tag
                    // criteriaValue: Tag string ID or index
                    // Based on Eclipse engine: Tag criteria matching uses tag string ID or index to match entity tags
                    // Located via reverse engineering: Tag criteria system checks entity tags against criteria values
                    // If criteriaValue == 0: Check if entity has any tag (non-empty tag string)
                    // If criteriaValue > 0: criteriaValue represents a tag string ID or index that needs to be resolved
                    //
                    // Tag resolution strategy:
                    // Since Eclipse engine may use tag IDs or indices, we need to resolve the criteriaValue to a tag string.
                    // The most practical approach is to look up tags from the world's entity tag collection and use
                    // criteriaValue as an index (1-based) into a sorted list of unique tags. This allows the criteria
                    // system to match entities by tag using stable indices.
                    string tag = entity.Tag ?? string.Empty;
                    if (criteriaValue == 0)
                    {
                        return !string.IsNullOrEmpty(tag); // Has any tag
                    }

                    // For criteriaValue > 0, resolve tag string from world's tag collection
                    // Build a sorted list of unique tags from all entities in the world
                    // Use criteriaValue as 1-based index into this list for stable tag resolution
                    if (ctx != null && ctx.World != null)
                    {
                        // Get all unique tags from world entities (sorted for stable indexing)
                        var allTags = new List<string>();
                        var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        // Collect all unique tags from world entities
                        foreach (var worldEntity in ctx.World.GetAllEntities())
                        {
                            if (worldEntity != null && worldEntity.IsValid && !string.IsNullOrEmpty(worldEntity.Tag))
                            {
                                if (!tagSet.Contains(worldEntity.Tag))
                                {
                                    tagSet.Add(worldEntity.Tag);
                                    allTags.Add(worldEntity.Tag);
                                }
                            }
                        }

                        // Sort tags for stable indexing (case-insensitive sort for consistency)
                        allTags.Sort(StringComparer.OrdinalIgnoreCase);

                        // Use criteriaValue as 1-based index into sorted tag list
                        // criteriaValue 1 = first tag, criteriaValue 2 = second tag, etc.
                        if (criteriaValue > 0 && criteriaValue <= allTags.Count)
                        {
                            string targetTag = allTags[criteriaValue - 1]; // Convert to 0-based index
                            // Compare entity tag with target tag (case-insensitive match)
                            return string.Equals(tag, targetTag, StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    // If tag resolution failed (criteriaValue out of range or no world context),
                    // entity doesn't match the tag criteria
                    return false;

                case 10: // NotDead
                    if (stats != null)
                    {
                        return stats.CurrentHP > 0;
                    }
                    return false;

                case 11: // InCombat
                    if (ctx != null && ctx.World != null && ctx.World.CombatSystem != null)
                    {
                        return ctx.World.CombatSystem.IsInCombat(entity) == (criteriaValue != 0);
                    }
                    return criteriaValue == 0; // If no combat system, assume not in combat

                case 12: // TargetType
                    // criteriaValue: ObjectType enum value
                    return (int)entity.ObjectType == criteriaValue;

                case 13: // CreatureType
                    // criteriaValue: Creature type ID
                    int creatureType = entity.GetData<int>("CreatureType", -1);
                    return creatureType == criteriaValue;

                case 14: // Allegiance
                    // criteriaValue: Allegiance ID
                    int allegiance = entity.GetData<int>("Allegiance", -1);
                    return allegiance == criteriaValue;

                case 15: // Gender
                    // criteriaValue: Gender (0=male, 1=female, 2=other/unknown)
                    int gender = entity.GetData<int>("Gender", -1);
                    return gender == criteriaValue;

                case 16: // Player
                    Variable isPC = Func_GetIsPC(new[] { Variable.FromObject(entity.ObjectId) }, ctx);
                    return (isPC.AsInt() != 0) == (criteriaValue != 0);

                case 17: // Party
                    Variable isPartyMember = Func_IsObjectPartyMember(new[] { Variable.FromObject(entity.ObjectId) }, ctx);
                    return (isPartyMember.AsInt() != 0) == (criteriaValue != 0);

                case 18: // Area
                    // criteriaValue: Area ID
                    return entity.AreaId == (uint)criteriaValue;

                case 19: // Location
                    // criteriaValue: Maximum radius distance for location matching (in units)
                    if (entityTransform == null || targetTransform == null)
                    {
                        return false;
                    }
                    // Check if target is within criteriaValue radius distance of entity
                    float distance = Vector3.Distance(entityTransform.Position, targetTransform.Position);
                    return distance <= criteriaValue;

                case 20: // LineOfSight
                    // criteriaValue: 1 = has line of sight, 0 = no line of sight
                    if (entityTransform == null || targetTransform == null || ctx == null || ctx.World == null)
                    {
                        return criteriaValue == 0;
                    }
                    bool hasLOS = HasLineOfSight(entityTransform.Position, targetTransform.Position, ctx);
                    return hasLOS == (criteriaValue != 0);

                case 21: // Distance
                    // criteriaValue: Maximum distance (in units)
                    if (entityTransform == null || targetTransform == null)
                    {
                        return false;
                    }
                    float distance21 = Vector3.Distance(entityTransform.Position, targetTransform.Position);
                    return distance21 <= criteriaValue;

                case 22: // HasItem
                    // criteriaValue: Item ID or tag index
                    if (inventory == null)
                    {
                        return false;
                    }
                    // Check if entity has item with matching ID/tag
                    return HasItemByIdOrTag(entity, criteriaValue, ctx);

                case 23: // HasSpell
                    // criteriaValue: Spell ID
                    if (stats == null)
                    {
                        return false;
                    }
                    return stats.HasSpell(criteriaValue);

                case 24: // HasSkill
                    // criteriaValue: Skill ID (0 = has any skill, >0 = has specific skill with rank > 0)
                    if (stats == null)
                    {
                        return false;
                    }
                    if (criteriaValue == 0)
                    {
                        // Check if has any skill with rank > 0
                        for (int i = 0; i < 100; i++) // Check up to 100 skills
                        {
                            if (stats.GetSkillRank(i) > 0)
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                    return stats.GetSkillRank(criteriaValue) > 0;

                case 25: // HasFeat
                    // criteriaValue: Feat ID
                    return HasFeat(entity, criteriaValue, ctx);

                case 26: // HasTalent
                    // criteriaValue: Talent ID
                    return HasTalent(entity, criteriaValue, ctx);

                case 27: // HasEffect
                    // criteriaValue: Effect ID
                    return HasEffect(entity, criteriaValue, ctx);

                case 28: // HasVariable
                    // criteriaValue: Variable name ID or index
                    return HasVariable(entity, criteriaValue, ctx);

                case 29: // HasLocalVariable
                    // criteriaValue: Local variable name ID or index
                    return HasLocalVariable(entity, criteriaValue, ctx);

                case 30: // HasGlobalVariable
                    // criteriaValue: Global variable name ID or index
                    return HasGlobalVariable(criteriaValue, ctx);

                case 31: // HasFaction
                    // criteriaValue: Faction ID
                    if (faction == null)
                    {
                        return false;
                    }
                    return faction.FactionId == criteriaValue;

                case 32: // HasAlignment
                    // criteriaValue: Alignment value
                    int alignment = entity.GetData<int>("Alignment", -1);
                    return alignment == criteriaValue;

                case 33: // HasGoodEvil
                    // criteriaValue: Good/Evil axis value (typically 0-100, where 0-49=evil, 50=neutral, 51-100=good)
                    int goodEvil = entity.GetData<int>("GoodEvil", -1);
                    return goodEvil == criteriaValue;

                case 34: // HasLawfulChaotic
                    // criteriaValue: Lawful/Chaotic axis value (typically 0-100, where 0-49=chaotic, 50=neutral, 51-100=lawful)
                    int lawfulChaotic = entity.GetData<int>("LawfulChaotic", -1);
                    return lawfulChaotic == criteriaValue;

                case 35: // HasLevel
                    // criteriaValue: Level threshold (0 = has any level, >0 = level >= value)
                    if (stats == null)
                    {
                        return false;
                    }
                    if (criteriaValue == 0)
                    {
                        return stats.Level > 0;
                    }
                    return stats.Level >= criteriaValue;

                case 36: // HasClass
                    // criteriaValue: Class ID (check if entity has this class)
                    int hasClass = entity.GetData<int>("Class", -1);
                    return hasClass == criteriaValue;

                case 37: // HasRace
                    // criteriaValue: Race ID (check if entity has this race)
                    int hasRace = entity.GetData<int>("Race", -1);
                    return hasRace == criteriaValue;

                case 38: // HasGender
                    // criteriaValue: Gender value (0=male, 1=female, 2=other)
                    int hasGender = entity.GetData<int>("Gender", -1);
                    return hasGender == criteriaValue;

                case 39: // HasSubrace
                    // criteriaValue: Subrace ID
                    int subrace = entity.GetData<int>("Subrace", -1);
                    return subrace == criteriaValue;

                case 40: // HasDeity
                    // criteriaValue: Deity ID
                    int deity = entity.GetData<int>("Deity", -1);
                    return deity == criteriaValue;

                case 41: // HasDomain
                    // criteriaValue: Domain ID
                    int domain = entity.GetData<int>("Domain", -1);
                    return domain == criteriaValue;

                case 42: // HasDomainSource
                    // criteriaValue: Domain source ID
                    int domainSource = entity.GetData<int>("DomainSource", -1);
                    return domainSource == criteriaValue;

                case 43: // HasAbilityScore
                    // criteriaValue: Ability ID (0-5 for STR, DEX, CON, INT, WIS, CHA)
                    if (stats == null)
                    {
                        return false;
                    }
                    // Extract ability ID from criteriaValue (lower bits) and threshold (upper bits)
                    int abilityId = criteriaValue & 0xFF;
                    if (abilityId < 0 || abilityId > 5)
                    {
                        return false;
                    }
                    Core.Enums.Ability ability = (Core.Enums.Ability)abilityId;
                    int abilityScore = stats.GetAbility(ability);
                    return abilityScore > 0; // Has non-zero ability score

                case 44: // HasAbilityModifier
                    // criteriaValue: Ability ID (0-5) - check if has modifier > 0
                    if (stats == null)
                    {
                        return false;
                    }
                    int abilityId44 = criteriaValue & 0xFF;
                    if (abilityId44 < 0 || abilityId44 > 5)
                    {
                        return false;
                    }
                    Core.Enums.Ability ability44 = (Core.Enums.Ability)abilityId44;
                    int modifier = stats.GetAbilityModifier(ability44);
                    return modifier > 0;

                case 45: // HasSkillRank
                    // criteriaValue: Skill ID - check if has rank > 0
                    if (stats == null)
                    {
                        return false;
                    }
                    int skillId = criteriaValue & 0xFF;
                    return stats.GetSkillRank(skillId) > 0;

                case 46: // HasFeatCount
                    // criteriaValue: Minimum feat count
                    int featCount = GetFeatCount(entity, ctx);
                    return featCount >= criteriaValue;

                case 47: // HasSpellCount
                    // criteriaValue: Minimum spell count
                    int spellCount = GetSpellCount(entity, ctx);
                    return spellCount >= criteriaValue;

                case 48: // HasTalentCount
                    // criteriaValue: Minimum talent count
                    int talentCount = GetTalentCount(entity, ctx);
                    return talentCount >= criteriaValue;

                case 49: // HasEffectCount
                    // criteriaValue: Minimum effect count
                    int effectCount = GetEffectCount(entity, ctx);
                    return effectCount >= criteriaValue;

                case 50: // HasItemCount
                    // criteriaValue: Minimum item count
                    if (inventory == null)
                    {
                        return false;
                    }
                    int itemCount = GetItemCount(entity, ctx);
                    return itemCount >= criteriaValue;

                case 51: // HasVariableValue
                    // criteriaValue: Encoded as (variableId << 16) | expectedValue
                    int varId51 = (criteriaValue >> 16) & 0xFFFF;
                    int expectedValue51 = criteriaValue & 0xFFFF;
                    int varValue51 = GetVariableValue(entity, varId51, ctx);
                    return varValue51 == expectedValue51;

                case 52: // HasLocalVariableValue
                    // criteriaValue: Encoded as (variableId << 16) | expectedValue
                    int varId52 = (criteriaValue >> 16) & 0xFFFF;
                    int expectedValue52 = criteriaValue & 0xFFFF;
                    int varValue52 = GetLocalVariableValue(entity, varId52, ctx);
                    return varValue52 == expectedValue52;

                case 53: // HasGlobalVariableValue
                    // criteriaValue: Encoded as (variableId << 16) | expectedValue
                    int varId53 = (criteriaValue >> 16) & 0xFFFF;
                    int expectedValue53 = criteriaValue & 0xFFFF;
                    int varValue53 = GetGlobalVariableValue(varId53, ctx);
                    return varValue53 == expectedValue53;

                case 54: // HasFactionValue
                    // criteriaValue: Faction ID
                    if (faction == null)
                    {
                        return false;
                    }
                    return faction.FactionId == criteriaValue;

                case 55: // HasAlignmentValue
                    // criteriaValue: Alignment value
                    int alignment55 = entity.GetData<int>("Alignment", -1);
                    return alignment55 == criteriaValue;

                case 56: // HasGoodEvilValue
                    // criteriaValue: Good/Evil value
                    int goodEvil56 = entity.GetData<int>("GoodEvil", -1);
                    return goodEvil56 == criteriaValue;

                case 57: // HasLawfulChaoticValue
                    // criteriaValue: Lawful/Chaotic value
                    int lawfulChaotic57 = entity.GetData<int>("LawfulChaotic", -1);
                    return lawfulChaotic57 == criteriaValue;

                case 58: // HasLevelValue
                    // criteriaValue: Level value
                    if (stats == null)
                    {
                        return false;
                    }
                    return stats.Level == criteriaValue;

                case 59: // HasClassValue
                    // criteriaValue: Class ID
                    int class59 = entity.GetData<int>("Class", -1);
                    return class59 == criteriaValue;

                case 60: // HasRaceValue
                    // criteriaValue: Race ID
                    int race60 = entity.GetData<int>("Race", -1);
                    return race60 == criteriaValue;

                case 61: // HasGenderValue
                    // criteriaValue: Gender value
                    int gender61 = entity.GetData<int>("Gender", -1);
                    return gender61 == criteriaValue;

                case 62: // HasSubraceValue
                    // criteriaValue: Subrace ID
                    int subrace62 = entity.GetData<int>("Subrace", -1);
                    return subrace62 == criteriaValue;

                case 63: // HasDeityValue
                    // criteriaValue: Deity ID
                    int deity63 = entity.GetData<int>("Deity", -1);
                    return deity63 == criteriaValue;

                case 64: // HasDomainValue
                    // criteriaValue: Domain ID
                    int domain64 = entity.GetData<int>("Domain", -1);
                    return domain64 == criteriaValue;

                case 65: // HasDomainSourceValue
                    // criteriaValue: Domain source ID
                    int domainSource65 = entity.GetData<int>("DomainSource", -1);
                    return domainSource65 == criteriaValue;

                case 66: // HasAbilityScoreValue
                    // criteriaValue: Encoded as (abilityId << 16) | expectedScore
                    if (stats == null)
                    {
                        return false;
                    }
                    int abilityId66 = (criteriaValue >> 16) & 0xFFFF;
                    int expectedScore = criteriaValue & 0xFFFF;
                    if (abilityId66 < 0 || abilityId66 > 5)
                    {
                        return false;
                    }
                    Core.Enums.Ability ability66 = (Core.Enums.Ability)abilityId66;
                    int actualScore = stats.GetAbility(ability66);
                    return actualScore == expectedScore;

                case 67: // HasAbilityModifierValue
                    // criteriaValue: Encoded as (abilityId << 16) | expectedModifier
                    if (stats == null)
                    {
                        return false;
                    }
                    int abilityId67 = (criteriaValue >> 16) & 0xFFFF;
                    int expectedModifier = criteriaValue & 0xFFFF;
                    if (abilityId67 < 0 || abilityId67 > 5)
                    {
                        return false;
                    }
                    Core.Enums.Ability ability67 = (Core.Enums.Ability)abilityId67;
                    int actualModifier = stats.GetAbilityModifier(ability67);
                    return actualModifier == expectedModifier;

                case 68: // HasSkillRankValue
                    // criteriaValue: Encoded as (skillId << 16) | expectedRank
                    if (stats == null)
                    {
                        return false;
                    }
                    int skillId68 = (criteriaValue >> 16) & 0xFFFF;
                    int expectedRank = criteriaValue & 0xFFFF;
                    int actualRank = stats.GetSkillRank(skillId68);
                    return actualRank == expectedRank;

                case 69: // HasFeatCountValue
                    // criteriaValue: Exact feat count
                    int featCount69 = GetFeatCount(entity, ctx);
                    return featCount69 == criteriaValue;

                case 70: // HasSpellCountValue
                    // criteriaValue: Exact spell count
                    int spellCount70 = GetSpellCount(entity, ctx);
                    return spellCount70 == criteriaValue;

                case 71: // HasTalentCountValue
                    // criteriaValue: Exact talent count
                    int talentCount71 = GetTalentCount(entity, ctx);
                    return talentCount71 == criteriaValue;

                case 72: // HasEffectCountValue
                    // criteriaValue: Exact effect count
                    int effectCount72 = GetEffectCount(entity, ctx);
                    return effectCount72 == criteriaValue;

                case 73: // HasItemCountValue
                    // criteriaValue: Exact item count
                    if (inventory == null)
                    {
                        return criteriaValue == 0;
                    }
                    int itemCount73 = GetItemCount(entity, ctx);
                    return itemCount73 == criteriaValue;

                default:
                    // Unknown criteria type - return false for safety
                    return false;
            }
        }

        /// <summary>
        /// Helper method to get disposition value between two entities
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Disposition calculation
        /// Disposition represents relationship status (0-100, where 0-10=hostile, 11-89=neutral, 90-100=friendly)
        /// </remarks>
        private int GetDispositionValue(Core.Interfaces.IEntity entity, Core.Interfaces.IEntity target, IExecutionContext ctx)
        {
            if (entity == null || target == null)
            {
                return 50; // Neutral if entities are null
            }

            IFactionComponent faction = entity.GetComponent<IFactionComponent>();
            if (faction == null)
            {
                return 50; // Neutral if no faction component
            }

            // Use faction reputation as disposition
            if (ctx != null && ctx.World != null)
            {
                // Check if world has a faction manager that can get reputation
                // Use reflection to safely access FactionManager property if it exists
                var factionManagerProperty = ctx.World.GetType().GetProperty("FactionManager");
                if (factionManagerProperty != null)
                {
                    var factionManager = factionManagerProperty.GetValue(ctx.World);
                    if (factionManager is Runtime.Engines.Eclipse.Systems.EclipseFactionManager eclipseFactionManager)
                    {
                        return eclipseFactionManager.GetReputation(entity, target);
                    }
                }
            }

            // Fallback: Check if hostile/friendly
            if (faction.IsHostile(target))
            {
                return 5; // Hostile (middle of hostile range)
            }
            else if (faction.IsFriendly(target))
            {
                return 95; // Friendly (middle of friendly range)
            }
            return 50; // Neutral
        }

        /// <summary>
        /// Helper method to get reputation value between two entities
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Reputation calculation
        /// Reputation represents relationship status (0-100, where 0-10=hostile, 11-89=neutral, 90-100=friendly)
        /// </remarks>
        private int GetReputationValue(Core.Interfaces.IEntity entity, Core.Interfaces.IEntity target, IExecutionContext ctx)
        {
            // Reputation and disposition are the same in Eclipse engine
            return GetDispositionValue(entity, target, ctx);
        }

        /// <summary>
        /// Helper method to get reaction type between two entities
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Reaction type calculation
        /// Returns: 0=hostile, 1=neutral, 2=friendly
        /// </remarks>
        private int GetReactionType(Core.Interfaces.IEntity entity, Core.Interfaces.IEntity target, IExecutionContext ctx)
        {
            if (entity == null || target == null)
            {
                return 1; // Neutral
            }

            IFactionComponent faction = entity.GetComponent<IFactionComponent>();
            if (faction == null)
            {
                return 1; // Neutral
            }

            int reputation = GetReputationValue(entity, target, ctx);
            if (reputation <= 10)
            {
                return 0; // Hostile
            }
            else if (reputation >= 90)
            {
                return 2; // Friendly
            }
            return 1; // Neutral
        }

        /// <summary>
        /// Helper method to check if there is line of sight between two positions
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Line of sight calculation
        /// Uses raycast to check for obstacles between positions
        /// Implementation matches daorigins.exe/DragonAge2.exe: Navigation mesh raycast for line of sight checks
        /// Located via navigation mesh raycast functions - Eclipse uses walkmesh/navigation mesh for visibility
        /// Original implementation: Similar to PerceptionSystem line-of-sight checks via NavigationMesh.Raycast
        /// Uses eye height offsets (1.5 units above ground) to check visibility from entity eye level
        /// Based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts for visibility checks
        /// </remarks>
        private bool HasLineOfSight(Vector3 from, Vector3 to, IExecutionContext ctx)
        {
            if (ctx == null || ctx.World == null)
            {
                return false;
            }

            // Use eye height offsets for line-of-sight checks (matches PerceptionSystem behavior)
            // Eye position is 1.5 units above ground level - this represents where entities "see" from
            // Based on swkotor2.exe: Line-of-sight raycast implementation uses eye height offsets
            // Located via string references: "Raycast" @ navigation mesh functions
            // Original implementation: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts for visibility checks
            const float DefaultEyeHeight = 1.5f; // units above entity position
            Vector3 fromEyePos = from + Vector3.UnitZ * DefaultEyeHeight;
            Vector3 toEyePos = to + Vector3.UnitZ * DefaultEyeHeight;

            // Handle edge case: same point always has line of sight
            Vector3 direction = toEyePos - fromEyePos;
            float distance = direction.Length();
            if (distance < 1e-6f)
            {
                return true; // Same point, line of sight is clear
            }

            // Check if navigation mesh is available for raycast
            if (ctx.World.CurrentArea == null || ctx.World.CurrentArea.NavigationMesh == null)
            {
                // No navigation mesh available - cannot perform proper line of sight check
                // Return false to indicate unknown visibility (safer than assuming visibility)
                return false;
            }

            // Normalize direction for raycast
            Vector3 normalizedDir = direction / distance;

            // Perform raycast to check for obstructions
            // Based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts for visibility checks
            Vector3 hitPoint;
            int hitFace;
            if (ctx.World.CurrentArea.NavigationMesh.Raycast(fromEyePos, normalizedDir, distance, out hitPoint, out hitFace))
            {
                // A hit was found - check if it blocks line of sight
                // Calculate distances from eye position
                float distToHit = Vector3.Distance(fromEyePos, hitPoint);
                float distToDest = distance;

                // If hit is very close to destination (within tolerance), consider line of sight clear
                // This handles cases where the raycast hits the destination geometry itself
                // Tolerance matches BaseNavigationMesh.LineOfSightTolerance (0.5 units)
                // Based on PerceptionSystem line-of-sight tolerance handling
                const float LineOfSightTolerance = 0.5f;
                if (distToDest - distToHit < LineOfSightTolerance)
                {
                    return true; // Hit is at or very close to destination, line of sight is clear
                }

                // Hit is significantly before destination - line of sight is blocked
                return false;
            }

            // No hit found - line of sight is clear
            return true;
        }

        /// <summary>
        /// Helper method to check if entity has item by ID or tag
        /// </summary>
        private bool HasItemByIdOrTag(Core.Interfaces.IEntity entity, int itemIdOrTag, IExecutionContext ctx)
        {
            IInventoryComponent inventory = entity.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return false;
            }

            // Check all items in inventory
            foreach (Core.Interfaces.IEntity item in inventory.GetAllItems())
            {
                if (item == null)
                {
                    continue;
                }

                // Check by item ID (stored in entity data)
                int itemId = item.GetData<int>("ItemId", -1);
                if (itemId == itemIdOrTag)
                {
                    return true;
                }

                // Check by tag (if itemIdOrTag is a negative tag index, resolve it to a tag string)
                // Based on Eclipse engine: Tag indices use negative values (-1 = first tag, -2 = second tag, etc.)
                // Build sorted list of unique item tags from world and use index to resolve tag string
                if (itemIdOrTag < 0 && ctx != null && ctx.World != null)
                {
                    // Get all unique item tags from world entities (sorted for stable indexing)
                    var allItemTags = new List<string>();
                    var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Collect all unique tags from items in world entities
                    foreach (var worldEntity in ctx.World.GetAllEntities())
                    {
                        if (worldEntity != null && worldEntity.IsValid)
                        {
                            IInventoryComponent worldInventory = worldEntity.GetComponent<IInventoryComponent>();
                            if (worldInventory != null)
                            {
                                foreach (var worldItem in worldInventory.GetAllItems())
                                {
                                    if (worldItem != null && worldItem.IsValid && !string.IsNullOrEmpty(worldItem.Tag))
                                    {
                                        if (!tagSet.Contains(worldItem.Tag))
                                        {
                                            tagSet.Add(worldItem.Tag);
                                            allItemTags.Add(worldItem.Tag);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Sort tags for stable indexing (case-insensitive sort for consistency)
                    allItemTags.Sort(StringComparer.OrdinalIgnoreCase);

                    // Use absolute value of itemIdOrTag as 1-based index into sorted tag list
                    // itemIdOrTag -1 = first tag, itemIdOrTag -2 = second tag, etc.
                    int tagIndex = Math.Abs(itemIdOrTag) - 1; // Convert to 0-based index
                    if (tagIndex >= 0 && tagIndex < allItemTags.Count)
                    {
                        string targetTag = allItemTags[tagIndex];
                        // Compare item tag with target tag (case-insensitive match)
                        if (string.Equals(item.Tag, targetTag, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Helper method to check if entity has a feat
        /// </summary>
        private bool HasFeat(Core.Interfaces.IEntity entity, int featId, IExecutionContext ctx)
        {
            // Check if entity has feat in feat list (stored in entity data)
            List<int> featList = entity.GetData<List<int>>("FeatList", null);
            if (featList != null)
            {
                return featList.Contains(featId);
            }

            // Also check stats component if it supports feats
            IStatsComponent stats = entity.GetComponent<IStatsComponent>();
            if (stats != null)
            {
                // Some engines store feats in stats component
                bool hasFeat = entity.GetData<bool>($"HasFeat_{featId}", false);
                if (hasFeat)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Helper method to check if entity has a talent
        /// </summary>
        private bool HasTalent(Core.Interfaces.IEntity entity, int talentId, IExecutionContext ctx)
        {
            // Check if entity has talent in talent list (stored in entity data)
            List<int> talentList = entity.GetData<List<int>>("TalentList", null);
            if (talentList != null)
            {
                return talentList.Contains(talentId);
            }

            return false;
        }

        /// <summary>
        /// Helper method to check if entity has an effect
        /// </summary>
        private bool HasEffect(Core.Interfaces.IEntity entity, int effectId, IExecutionContext ctx)
        {
            // Check if entity has effect in effect list (stored in entity data)
            List<int> effectList = entity.GetData<List<int>>("EffectList", null);
            if (effectList != null)
            {
                return effectList.Contains(effectId);
            }

            return false;
        }

        /// <summary>
        /// Helper method to check if entity has a variable
        /// </summary>
        private bool HasVariable(Core.Interfaces.IEntity entity, int variableId, IExecutionContext ctx)
        {
            // Check if entity has variable (stored in entity data)
            string varKey = $"Variable_{variableId}";
            return entity.HasData(varKey);
        }

        /// <summary>
        /// Helper method to check if entity has a local variable
        /// </summary>
        private bool HasLocalVariable(Core.Interfaces.IEntity entity, int variableId, IExecutionContext ctx)
        {
            // Local variables are stored on entity
            string varKey = $"LocalVariable_{variableId}";
            return entity.HasData(varKey);
        }

        /// <summary>
        /// Helper method to check if global variable exists
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Global variable existence check
        /// Eclipse uses variable IDs (integers) which are converted to string names for IScriptGlobals
        /// Variable ID is converted to string name using format: variable ID as string
        /// Checks all global variable dictionaries (ints, bools, strings, locations) to see if variable exists
        /// Based on daorigins.exe: Global variable system uses variable IDs mapped to string names
        /// Based on DragonAge2.exe: Same global variable system as daorigins.exe
        /// </remarks>
        private bool HasGlobalVariable(int variableId, IExecutionContext ctx)
        {
            if (ctx == null || ctx.Globals == null)
            {
                return false;
            }

            // Convert variable ID to string name
            // Eclipse uses variable IDs, but IScriptGlobals uses string names
            // Use variable ID as the name (converted to string)
            string varName = variableId.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Check if variable exists in any of the global variable dictionaries
            // IScriptGlobals doesn't expose a HasVariable method, so we need to check by trying to get the value
            // and comparing with default values. However, this won't distinguish between "not set" and "set to default"
            // So we use reflection to access the internal dictionaries directly
            try
            {
                // Use reflection to access private dictionaries in ScriptGlobals
                // This is necessary because IScriptGlobals doesn't expose a HasVariable method
                System.Reflection.FieldInfo globalIntsField = ctx.Globals.GetType().GetField("_globalInts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Reflection.FieldInfo globalBoolsField = ctx.Globals.GetType().GetField("_globalBools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Reflection.FieldInfo globalStringsField = ctx.Globals.GetType().GetField("_globalStrings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Reflection.FieldInfo globalLocationsField = ctx.Globals.GetType().GetField("_globalLocations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (globalIntsField != null)
                {
                    var globalInts = globalIntsField.GetValue(ctx.Globals) as System.Collections.Generic.Dictionary<string, int>;
                    if (globalInts != null && globalInts.ContainsKey(varName))
                    {
                        return true;
                    }
                }

                if (globalBoolsField != null)
                {
                    var globalBools = globalBoolsField.GetValue(ctx.Globals) as System.Collections.Generic.Dictionary<string, bool>;
                    if (globalBools != null && globalBools.ContainsKey(varName))
                    {
                        return true;
                    }
                }

                if (globalStringsField != null)
                {
                    var globalStrings = globalStringsField.GetValue(ctx.Globals) as System.Collections.Generic.Dictionary<string, string>;
                    if (globalStrings != null && globalStrings.ContainsKey(varName))
                    {
                        return true;
                    }
                }

                if (globalLocationsField != null)
                {
                    var globalLocations = globalLocationsField.GetValue(ctx.Globals) as System.Collections.Generic.Dictionary<string, object>;
                    if (globalLocations != null && globalLocations.ContainsKey(varName))
                    {
                        return true;
                    }
                }
            }
            catch (System.Exception)
            {
                // Reflection failed - fallback to checking by getting value
                // This is less accurate but will work if reflection is not available
                // Check if variable exists by trying to get it and comparing with default values
                // Note: This won't distinguish between "not set" and "set to default value"
                int intValue = ctx.Globals.GetGlobalInt(varName);
                bool boolValue = ctx.Globals.GetGlobalBool(varName);
                string stringValue = ctx.Globals.GetGlobalString(varName);
                object locationValue = ctx.Globals.GetGlobalLocation(varName);

                // If any value is non-default, variable exists
                // This is a heuristic - it won't catch variables set to default values
                if (intValue != 0 || boolValue != false || !string.IsNullOrEmpty(stringValue) || locationValue != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Helper method to get variable value
        /// </summary>
        private int GetVariableValue(Core.Interfaces.IEntity entity, int variableId, IExecutionContext ctx)
        {
            string varKey = $"Variable_{variableId}";
            return entity.GetData<int>(varKey, 0);
        }

        /// <summary>
        /// Helper method to get local variable value
        /// </summary>
        private int GetLocalVariableValue(Core.Interfaces.IEntity entity, int variableId, IExecutionContext ctx)
        {
            string varKey = $"LocalVariable_{variableId}";
            return entity.GetData<int>(varKey, 0);
        }

        /// <summary>
        /// Helper method to get global variable value
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Global variable value retrieval
        /// Eclipse uses variable IDs (integers) which are converted to string names for IScriptGlobals
        /// Variable ID is converted to string name using format: variable ID as string
        /// Checks all global variable types (int, bool, string, location) and returns appropriate value
        /// For criteria matching, we primarily care about integer values, so we check int first, then bool
        /// Based on daorigins.exe: Global variable system uses variable IDs mapped to string names
        /// Based on DragonAge2.exe: Same global variable system as daorigins.exe
        /// </remarks>
        private int GetGlobalVariableValue(int variableId, IExecutionContext ctx)
        {
            if (ctx == null || ctx.Globals == null)
            {
                return 0;
            }

            // Convert variable ID to string name
            // Eclipse uses variable IDs, but IScriptGlobals uses string names
            // Use variable ID as the name (converted to string)
            string varName = variableId.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Check integer variables first (most common for criteria matching)
            int intValue = ctx.Globals.GetGlobalInt(varName);
            if (intValue != 0)
            {
                // Use reflection to check if variable actually exists (not just default value)
                // This ensures we return 0 only if variable doesn't exist, not if it's set to 0
                try
                {
                    System.Reflection.FieldInfo globalIntsField = ctx.Globals.GetType().GetField("_globalInts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (globalIntsField != null)
                    {
                        var globalInts = globalIntsField.GetValue(ctx.Globals) as System.Collections.Generic.Dictionary<string, int>;
                        if (globalInts != null && globalInts.ContainsKey(varName))
                        {
                            return intValue; // Variable exists, return its value (even if 0)
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Reflection failed - return intValue anyway (might be default or actual value)
                    return intValue;
                }
            }

            // Check boolean variables (convert to int: true = 1, false = 0)
            bool boolValue = ctx.Globals.GetGlobalBool(varName);
            if (boolValue)
            {
                // Use reflection to check if variable actually exists
                try
                {
                    System.Reflection.FieldInfo globalBoolsField = ctx.Globals.GetType().GetField("_globalBools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (globalBoolsField != null)
                    {
                        var globalBools = globalBoolsField.GetValue(ctx.Globals) as System.Collections.Generic.Dictionary<string, bool>;
                        if (globalBools != null && globalBools.ContainsKey(varName))
                        {
                            return boolValue ? 1 : 0; // Variable exists, return its value as int
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Reflection failed - return bool value as int
                    return boolValue ? 1 : 0;
                }
            }

            // Check string variables (return length or hash as integer representation)
            // For criteria matching, string variables are typically not used, but we check for completeness
            string stringValue = ctx.Globals.GetGlobalString(varName);
            if (!string.IsNullOrEmpty(stringValue))
            {
                // Use reflection to check if variable actually exists
                try
                {
                    System.Reflection.FieldInfo globalStringsField = ctx.Globals.GetType().GetField("_globalStrings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (globalStringsField != null)
                    {
                        var globalStrings = globalStringsField.GetValue(ctx.Globals) as System.Collections.Generic.Dictionary<string, string>;
                        if (globalStrings != null && globalStrings.ContainsKey(varName))
                        {
                            // For string variables, return string length as integer representation
                            // This allows criteria matching to work with string variables
                            return stringValue.Length;
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Reflection failed - return string length
                    return stringValue.Length;
                }
            }

            // Check location variables (return 1 if exists, 0 if not)
            // For criteria matching, location variables are typically not used, but we check for completeness
            object locationValue = ctx.Globals.GetGlobalLocation(varName);
            if (locationValue != null)
            {
                // Use reflection to check if variable actually exists
                try
                {
                    System.Reflection.FieldInfo globalLocationsField = ctx.Globals.GetType().GetField("_globalLocations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (globalLocationsField != null)
                    {
                        var globalLocations = globalLocationsField.GetValue(ctx.Globals) as System.Collections.Generic.Dictionary<string, object>;
                        if (globalLocations != null && globalLocations.ContainsKey(varName))
                        {
                            // For location variables, return 1 if exists (non-null location)
                            return 1;
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Reflection failed - return 1 if location exists
                    return 1;
                }
            }

            // Variable doesn't exist in any dictionary - return 0
            return 0;
        }

        /// <summary>
        /// Helper method to get feat count
        /// </summary>
        private int GetFeatCount(Core.Interfaces.IEntity entity, IExecutionContext ctx)
        {
            List<int> featList = entity.GetData<List<int>>("FeatList", null);
            if (featList != null)
            {
                return featList.Count;
            }
            return 0;
        }

        /// <summary>
        /// Helper method to get spell count
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Spell count calculation (daorigins.exe, DragonAge2.exe)
        /// Located via string references: Spell system uses KnownSpells/KnownTalents/KnownAbilities
        /// Original implementation: Counts spells from creature's known spells/talents/abilities list
        /// EclipseStatsComponent.GetKnownSpells() returns all known spells/talents/abilities
        /// Cross-engine: Common implementation for both daorigins.exe and DragonAge2.exe
        /// </remarks>
        private int GetSpellCount(Core.Interfaces.IEntity entity, IExecutionContext ctx)
        {
            IStatsComponent stats = entity.GetComponent<IStatsComponent>();
            if (stats == null)
            {
                return 0;
            }

            // Primary method: Use EclipseStatsComponent.GetKnownSpells() to get all known spells
            // This is the most efficient and accurate method, using the internal HashSet of known spells
            EclipseStatsComponent eclipseStats = stats as EclipseStatsComponent;
            if (eclipseStats != null)
            {
                // GetKnownSpells() returns IEnumerable<int> of all known spell/talent/ability IDs
                int count = 0;
                foreach (int spellId in eclipseStats.GetKnownSpells())
                {
                    count++;
                }
                return count;
            }

            // Fallback method 1: Check entity data for stored spell list
            // This can happen if entity data was saved/loaded from disk
            List<int> spellList = entity.GetData<List<int>>("SpellList", null);
            if (spellList != null)
            {
                return spellList.Count;
            }

            // Fallback method 2: Check for KnownSpells/KnownTalents/KnownAbilities in entity data
            // Eclipse engine uses these keys to store known spells/talents/abilities
            object knownSpellsObj = entity.GetData("KnownSpells") ?? entity.GetData("KnownTalents") ?? entity.GetData("KnownAbilities");
            if (knownSpellsObj != null)
            {
                System.Collections.Generic.IEnumerable<int> knownSpells = knownSpellsObj as System.Collections.Generic.IEnumerable<int>;
                if (knownSpells != null)
                {
                    int count = 0;
                    foreach (int spellId in knownSpells)
                    {
                        count++;
                    }
                    return count;
                }

                // Try as generic IEnumerable (in case it's stored as List<object> or similar)
                System.Collections.IEnumerable enumerable = knownSpellsObj as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    int count = 0;
                    foreach (object spellObj in enumerable)
                    {
                        count++;
                    }
                    return count;
                }
            }

            // Fallback method 3: Iterate through possible spell IDs and check HasSpell
            // This is the least efficient method but works as a last resort
            // In practice, this should rarely be needed since EclipseStatsComponent should be available
            // Limit to reasonable spell ID range (Eclipse games typically use 0-9999 for spell IDs)
            int fallbackCount = 0;
            for (int i = 0; i < 10000; i++) // Check up to 10000 spell IDs (reasonable upper bound for Eclipse games)
            {
                if (stats.HasSpell(i))
                {
                    fallbackCount++;
                }
            }
            return fallbackCount;
        }

        /// <summary>
        /// Helper method to get talent count
        /// </summary>
        private int GetTalentCount(Core.Interfaces.IEntity entity, IExecutionContext ctx)
        {
            List<int> talentList = entity.GetData<List<int>>("TalentList", null);
            if (talentList != null)
            {
                return talentList.Count;
            }
            return 0;
        }

        /// <summary>
        /// Helper method to get effect count
        /// </summary>
        private int GetEffectCount(Core.Interfaces.IEntity entity, IExecutionContext ctx)
        {
            List<int> effectList = entity.GetData<List<int>>("EffectList", null);
            if (effectList != null)
            {
                return effectList.Count;
            }
            return 0;
        }

        /// <summary>
        /// Helper method to get item count
        /// </summary>
        private int GetItemCount(Core.Interfaces.IEntity entity, IExecutionContext ctx)
        {
            IInventoryComponent inventory = entity.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Core.Interfaces.IEntity item in inventory.GetAllItems())
            {
                if (item != null)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Helper method to register function names for debugging
        /// </summary>
        private void RegisterFunctionName(int routineId, string name)
        {
            _functionNames[routineId] = name;
            _implementedFunctions.Add(routineId);
        }

        #endregion
    }
}

