using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Odyssey.Components;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;
using ObjectType = Andastra.Runtime.Core.Enums.ObjectType;
using ScriptEvent = Andastra.Runtime.Core.Enums.ScriptEvent;

namespace Andastra.Game.Games.Odyssey.Loading
{
    /// <summary>
    /// Factory for creating runtime entities from GFF templates.
    /// </summary>
    /// <remarks>
    /// Entity Factory System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) entity creation system
    /// - Located via string references: "TemplateResRef" @ 0x007bd00c, "ScriptHeartbeat" @ 0x007beeb0
    /// - "tmpgit" @ 0x007be618 (temporary GIT structure references during entity loading)
    /// - Template loading: 0x005fb0f0 @ 0x005fb0f0 loads creature templates from GFF, reads TemplateResRef field
    ///   - Original implementation: Loads UTC GFF structure, reads fields in specific order:
    ///     - FirstName, LastName, Description (localized strings)
    ///     - IsPC, Tag, Conversation (ResRef)
    ///     - Interruptable, Gender, StartingPackage, Race, Subrace, SubraceIndex, Deity
    ///     - Str, Dex, Con, Int, Wis, Cha (ability scores, with modifier calculations)
    ///     - NaturalAC, SoundSetFile, Gold, ItemComponent, Chemicals
    ///     - Invulnerable/Plot, Min1HP, PartyInteract, NotReorienting, Hologram, IgnoreCrePath
    ///     - MultiplierSet, PCLevelAtSpawn, WillNotRender, Confused, Disarmable
    ///     - Experience, PortraitId/Portrait, GoodEvil, BaseCNPCAlignment, FuryDamageBonus, CurrentForm
    ///     - Color_Skin, Color_Hair, Color_Tattoo1, Color_Tattoo2, Phenotype, Appearance_Type
    ///     - Appearance_Head, DuplicatingHead, UseBackupHead, FactionID, BlindSpot, ChallengeRating
    ///     - AIState, BodyBag, PerceptionRange (looks up PERCEPTIONDIST from appearance.2da)
    ///     - ClassList (Class, ClassLevel, SpellsPerDayList), HitPoints, ForcePoints, CurrentHitPoints, CurrentForce
    ///     - BonusForcePoints, PlayerCreated, AssignedPup, willbonus, fortbonus, refbonus
    ///     - LvlStatList (for PCs), FeatList, PerceptionList, CombatRoundData, SkillPoints, SkillList
    ///     - MovementRate/WalkRate, SpecAbilityList
    ///   - Returns 0 on success, error codes 0x5f4-0x5f8 on failure (invalid race, perception range, class list, duplicate classes)
    /// - Original implementation: Creates runtime entities from GIT instance data and GFF templates
    /// - Entities created from GIT instances override template values with instance-specific data
    /// - ObjectId assignment: Sequential uint32 starting from 1 (OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001)
    /// - Position/Orientation: GIT instances specify XPosition, YPosition, ZPosition, XOrientation, YOrientation
    /// - Tag: GIT instances can override template Tag field (GIT Struct → "Tag" ResRef field)
    /// - Script hooks: Templates contain script ResRefs (ScriptHeartbeat, ScriptOnNotice, ScriptAttacked, etc.)
    ///
    /// GFF Template Types (GFF signatures):
    /// - UTC → Creature (Appearance_Type, Faction, HP, Attributes, Feats, Scripts)
    /// - UTP → Placeable (Appearance, Useable, Locked, OnUsed)
    /// - UTD → Door (GenericType, Locked, OnOpen, OnClose, LinkedToModule, LinkedTo)
    /// - UTT → Trigger (Geometry polygon, OnEnter, OnExit, LinkedToModule)
    /// - UTW → Waypoint (Tag, position, MapNote, MapNoteEnabled)
    /// - UTS → Sound (Active, Looping, Positional, ResRef)
    /// - UTE → Encounter (Creature list, spawn conditions, Geometry, SpawnPointList)
    /// - UTI → Item (BaseItem, Properties, Charges)
    /// - UTM → Store (merchant inventory)
    /// </remarks>
    public class EntityFactory
    {
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ObjectId assignment system
        // Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c
        // Original implementation: 0x004e5920 @ 0x004e5920 reads ObjectId from GIT with default 0x7f000000 (OBJECT_INVALID)
        // ObjectIds should be unique across all entities. Use high range (1000000+) to avoid conflicts with World.CreateEntity counter
        private uint _nextObjectId = 1000000;

        /// <summary>
        /// Gets the next available object ID.
        /// Uses high range to avoid conflicts with Entity's static counter (used by World.CreateEntity).
        /// </summary>
        private uint GetNextObjectId()
        {
            return _nextObjectId++;
        }

        /// <summary>
        /// Reads ObjectId from GIT struct if present, otherwise generates new one.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00412d40 reads ObjectId field from GIT with default 0x7f000000 (OBJECT_INVALID)
        /// </summary>
        private uint GetObjectIdFromGit(GFFStruct gitStruct)
        {
            // Try to read ObjectId from GIT (may not always be present)
            uint objectId = (uint)GetIntField(gitStruct, "ObjectId", 0);
            if (objectId != 0 && objectId != 0x7F000000) // 0x7F000000 = OBJECT_INVALID
            {
                return objectId;
            }
            // Generate new ObjectId if not present or invalid
            return GetNextObjectId();
        }

        /// <summary>
        /// Creates a creature from GIT instance struct.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: 0x005223a0 @ 0x005223a0 loads creature instance data from GIT struct.
        /// Loads AreaId from GFF at offset 0x90 (via 0x00412d40 with "AreaId" field name).
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// </remarks>
        [CanBeNull]
        public IEntity CreateCreatureFromGit(GFFStruct gitStruct, Module module)
        {
            uint objectId = GetObjectIdFromGit(gitStruct);
            var entity = new Entity(objectId, ObjectType.Creature);

            // Get position
            System.Numerics.Vector3 position = GetPosition(gitStruct);
            float facing = GetFacing(gitStruct);

            // Basic properties
            entity.Tag = GetResRefField(gitStruct, "Tag");

            // Load AreaId from GIT struct (swkotor2.exe: 0x005223a0 loads AreaId from GFF at offset 0x90)
            // Based on FUN_00412d40 call with "AreaId" field name in FUN_005223a0
            uint areaId = (uint)GetIntField(gitStruct, "AreaId", 0);
            entity.AreaId = areaId;

            // Set transform
            entity.Position = position;
            entity.Facing = facing;

            // Load template if specified
            string templateResRef = GetResRefField(gitStruct, "TemplateResRef");
            if (!string.IsNullOrEmpty(templateResRef))
            {
                LoadCreatureTemplate(entity, module, templateResRef);
            }

            return entity;
        }

        /// <summary>
        /// Creates a creature from a template ResRef at a specific position.
        /// </summary>
        [CanBeNull]
        public IEntity CreateCreatureFromTemplate(Module module, string templateResRef, System.Numerics.Vector3 position, float facing)
        {
            if (module == null || string.IsNullOrEmpty(templateResRef))
            {
                return null;
            }

            var entity = new Entity(GetNextObjectId(), ObjectType.Creature);
            entity.Position = position;
            entity.Facing = facing;

            LoadCreatureTemplate(entity, module, templateResRef);

            return entity;
        }

        /// <summary>
        /// Creates an item from a template ResRef at a specific position.
        /// </summary>
        [CanBeNull]
        public IEntity CreateItemFromTemplate(Module module, string templateResRef, System.Numerics.Vector3 position, float facing)
        {
            if (module == null || string.IsNullOrEmpty(templateResRef))
            {
                return null;
            }

            var entity = new Entity(GetNextObjectId(), ObjectType.Item);
            entity.Position = position;
            entity.Facing = facing;

            LoadItemTemplate(entity, module, templateResRef);

            return entity;
        }

        /// <summary>
        /// Creates a placeable from a template ResRef at a specific position.
        /// </summary>
        [CanBeNull]
        public IEntity CreatePlaceableFromTemplate(Module module, string templateResRef, System.Numerics.Vector3 position, float facing)
        {
            if (module == null || string.IsNullOrEmpty(templateResRef))
            {
                return null;
            }

            var entity = new Entity(GetNextObjectId(), ObjectType.Placeable);
            entity.Position = position;
            entity.Facing = facing;

            LoadPlaceableTemplate(entity, module, templateResRef);

            return entity;
        }

        /// <summary>
        /// Creates a door from a template ResRef at a specific position.
        /// </summary>
        [CanBeNull]
        public IEntity CreateDoorFromTemplate(Module module, string templateResRef, System.Numerics.Vector3 position, float facing)
        {
            if (module == null || string.IsNullOrEmpty(templateResRef))
            {
                return null;
            }

            var entity = new Entity(GetNextObjectId(), ObjectType.Door);
            entity.Position = position;
            entity.Facing = facing;

            LoadDoorTemplate(entity, module, templateResRef);

            return entity;
        }

        /// <summary>
        /// Creates a store from a template ResRef at a specific position.
        /// </summary>
        [CanBeNull]
        public IEntity CreateStoreFromTemplate(Module module, string templateResRef, System.Numerics.Vector3 position, float facing)
        {
            if (module == null || string.IsNullOrEmpty(templateResRef))
            {
                return null;
            }

            var entity = new Entity(GetNextObjectId(), ObjectType.Store);
            entity.Position = position;
            entity.Facing = facing;

            LoadStoreTemplate(entity, module, templateResRef);

            return entity;
        }

        /// <summary>
        /// Loads creature template from UTC.
        /// </summary>
        private void LoadCreatureTemplate(Entity entity, Module module, string templateResRef)
        {
            ModuleResource utcResource = module.Creature(templateResRef);
            if (utcResource == null)
            {
                return;
            }

            object utcData = utcResource.Resource();
            if (utcData == null)
            {
                return;
            }

            GFF utcGff = utcData as GFF;
            if (utcGff == null)
            {
                return;
            }

            GFFStruct root = utcGff.Root;

            // Tag (if not set from GIT)
            if (string.IsNullOrEmpty(entity.Tag))
            {
                entity.Tag = GetStringField(root, "Tag");
            }

            // Store template data for components
            entity.SetData("TemplateResRef", templateResRef);
            entity.SetData("FirstName", GetLocStringField(root, "FirstName"));
            entity.SetData("LastName", GetLocStringField(root, "LastName"));
            entity.SetData("RaceId", GetIntField(root, "Race", 0)); // Race field in UTC
            entity.SetData("Appearance_Type", GetIntField(root, "Appearance_Type", 0));
            entity.SetData("FactionID", GetIntField(root, "FactionID", 0));
            entity.SetData("CurrentHitPoints", GetIntField(root, "CurrentHitPoints", 1));
            entity.SetData("MaxHitPoints", GetIntField(root, "MaxHitPoints", 1));
            entity.SetData("ForcePoints", GetIntField(root, "ForcePoints", 0));
            entity.SetData("MaxForcePoints", GetIntField(root, "MaxForcePoints", 0));

            // Load class list from UTC Classes field
            if (root.Exists("ClassList"))
            {
                GFFList classList = root.GetList("ClassList");
                if (classList != null)
                {
                    var classes = new List<Components.CreatureClass>();
                    for (int i = 0; i < classList.Count; i++)
                    {
                        GFFStruct classStruct = classList[i];
                        if (classStruct != null)
                        {
                            int classId = GetIntField(classStruct, "Class", 0);
                            int classLevel = GetIntField(classStruct, "ClassLevel", 1);
                            classes.Add(new Components.CreatureClass { ClassId = classId, Level = classLevel });
                        }
                    }
                    entity.SetData("ClassList", classes);
                }
            }

            // Attributes
            entity.SetData("Str", GetIntField(root, "Str", 10));
            entity.SetData("Dex", GetIntField(root, "Dex", 10));
            entity.SetData("Con", GetIntField(root, "Con", 10));
            entity.SetData("Int", GetIntField(root, "Int", 10));
            entity.SetData("Wis", GetIntField(root, "Wis", 10));
            entity.SetData("Cha", GetIntField(root, "Cha", 10));

            // Scripts
            SetEntityScripts(entity, root, new Dictionary<string, ScriptEvent>
            {
                { "ScriptAttacked", ScriptEvent.OnPhysicalAttacked },
                { "ScriptDamaged", ScriptEvent.OnDamaged },
                { "ScriptDeath", ScriptEvent.OnDeath },
                { "ScriptDialogue", ScriptEvent.OnConversation },
                { "ScriptDisturbed", ScriptEvent.OnDisturbed },
                { "ScriptEndRound", ScriptEvent.OnEndCombatRound },
                { "ScriptHeartbeat", ScriptEvent.OnHeartbeat },
                { "ScriptOnBlocked", ScriptEvent.OnBlocked },
                { "ScriptOnNotice", ScriptEvent.OnPerception },
                { "ScriptRested", ScriptEvent.OnRested },
                { "ScriptSpawn", ScriptEvent.OnSpawn },
                { "ScriptSpellAt", ScriptEvent.OnSpellCastAt },
                { "ScriptUserDefine", ScriptEvent.OnUserDefined }
            });

            // Load ScriptDialogue as Conversation property (dialogue ResRef for BeginConversation)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0050c510 @ 0x0050c510 loads ScriptDialogue field from UTC template
            // ScriptDialogue ResRef is the dialogue file (DLG) used for conversations with this creature
            string scriptDialogue = GetResRefField(root, "ScriptDialogue");
            if (!string.IsNullOrEmpty(scriptDialogue))
            {
                entity.SetData("Conversation", scriptDialogue);
                entity.SetData("ScriptDialogue", scriptDialogue);
            }
        }

        /// <summary>
        /// Creates a door from GIT instance struct.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: 0x005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90.
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// </remarks>
        [CanBeNull]
        public IEntity CreateDoorFromGit(GFFStruct gitStruct, Module module)
        {
            uint objectId = GetObjectIdFromGit(gitStruct);
            var entity = new Entity(objectId, ObjectType.Door);

            System.Numerics.Vector3 position = GetPosition(gitStruct);
            float facing = GetFacing(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
            
            // Load AreaId from GIT struct (swkotor2.exe: 0x005223a0 loads AreaId from GFF at offset 0x90)
            uint areaId = (uint)GetIntField(gitStruct, "AreaId", 0);
            entity.AreaId = areaId;
            
            entity.Position = position;
            entity.Facing = facing;

            // Door-specific GIT properties
            entity.SetData("LinkedToModule", GetResRefField(gitStruct, "LinkedToModule"));
            entity.SetData("LinkedTo", GetResRefField(gitStruct, "LinkedTo"));
            entity.SetData("LinkedToFlags", GetIntField(gitStruct, "LinkedToFlags", 0));
            entity.SetData("TransitionDestin", GetLocStringField(gitStruct, "TransitionDestin"));

            // Load template
            string templateResRef = GetResRefField(gitStruct, "TemplateResRef");
            if (!string.IsNullOrEmpty(templateResRef))
            {
                LoadDoorTemplate(entity, module, templateResRef);
            }

            return entity;
        }

        /// <summary>
        /// Loads door template from UTD.
        /// </summary>
        private void LoadDoorTemplate(Entity entity, Module module, string templateResRef)
        {
            ModuleResource utdResource = module.Door(templateResRef);
            if (utdResource == null)
            {
                return;
            }

            object utdData = utdResource.Resource();
            if (utdData == null)
            {
                return;
            }

            GFF utdGff = utdData as GFF;
            if (utdGff == null)
            {
                return;
            }

            GFFStruct root = utdGff.Root;

            if (string.IsNullOrEmpty(entity.Tag))
            {
                entity.Tag = GetStringField(root, "Tag");
            }

            entity.SetData("TemplateResRef", templateResRef);
            entity.SetData("GenericType", GetIntField(root, "GenericType", 0));
            entity.SetData("Locked", GetIntField(root, "Locked", 0) != 0);
            entity.SetData("Lockable", GetIntField(root, "Lockable", 0) != 0);
            entity.SetData("KeyRequired", GetIntField(root, "KeyRequired", 0) != 0);
            entity.SetData("KeyName", GetStringField(root, "KeyName"));
            entity.SetData("OpenLockDC", GetIntField(root, "OpenLockDC", 0));
            entity.SetData("Hardness", GetIntField(root, "Hardness", 0));
            entity.SetData("HP", GetIntField(root, "HP", 1));
            entity.SetData("CurrentHP", GetIntField(root, "CurrentHP", 1));
            entity.SetData("Static", GetIntField(root, "Static", 0) != 0);

            // Load TSL-specific fields (KotOR2 only)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00584f40 @ 0x00584f40 loads Min1HP and NotBlastable from UTD template
            // Located via UTD field: "Min1HP" (UInt8/Byte, KotOR2 only), "NotBlastable" (UInt8/Byte, KotOR2 only)
            // Original implementation: These fields do not exist in swkotor.exe (KotOR1)
            entity.SetData("Min1HP", GetIntField(root, "Min1HP", 0) != 0);
            entity.SetData("NotBlastable", GetIntField(root, "NotBlastable", 0) != 0);

            // Load Conversation field (dialogue ResRef for BeginConversation)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00580330 @ 0x00580330 saves door data including Conversation field
            // Located via string reference: "Conversation" @ 0x007c1abc
            string conversation = GetResRefField(root, "Conversation");
            if (!string.IsNullOrEmpty(conversation))
            {
                entity.SetData("Conversation", conversation);
            }

            SetEntityScripts(entity, root, new Dictionary<string, ScriptEvent>
            {
                { "OnClick", ScriptEvent.OnClick },
                { "OnClosed", ScriptEvent.OnClose },
                { "OnDamaged", ScriptEvent.OnDamaged },
                { "OnDeath", ScriptEvent.OnDeath },
                { "OnDisarm", ScriptEvent.OnDisarm },
                { "OnFailToOpen", ScriptEvent.OnFailToOpen }, // TSL/KotOR2 only
                { "OnHeartbeat", ScriptEvent.OnHeartbeat },
                { "OnLock", ScriptEvent.OnLock },
                { "OnMeleeAttacked", ScriptEvent.OnPhysicalAttacked },
                { "OnOpen", ScriptEvent.OnOpen },
                { "OnSpellCastAt", ScriptEvent.OnSpellCastAt },
                { "OnTrapTriggered", ScriptEvent.OnTrapTriggered },
                { "OnUnlock", ScriptEvent.OnUnlock },
                { "OnUserDefined", ScriptEvent.OnUserDefined }
            });

            // Load BWM hooks from door walkmesh (DWK)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Doors have walkmesh files (DWK) with hook vectors defining interaction points
            // Located via string references: "USE1", "USE2" hook vectors in BWM format
            // Original implementation: Loads DWK file for door model, extracts RelativeHook1/RelativeHook2 or AbsoluteHook1/AbsoluteHook2
            LoadBWMHooks(entity, module, templateResRef, true);
        }

        /// <summary>
        /// Creates a placeable from GIT instance struct.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: [LoadPlaceablesFromGIT] @ (K1: TODO: Find this address via string reference "Placeable List" in swkotor.exe, TSL: 0x004e5d80) - load placeable instances from GIT "Placeable List"
        /// - Located via string reference: "Placeable List" @ (K1: TODO: Find this address, TSL: 0x007bd260)
        /// - Function signature: `undefined4 __thiscall LoadPlaceablesFromGIT(void *this, void *param_1, uint *param_2, void *param_3, int param_4)`
        /// - Original implementation (from decompiled 0x004e5d80):
        ///   - Iterates through "Placeable List" GFF list field
        ///   - For each placeable struct (type 9 = GFFStruct):
        ///     - Reads "ObjectId" (default 0x7F000000) via FUN_00412d40
        ///     - Allocates placeable object (0x4b0 bytes = 1200 bytes) via operator_new
        ///     - Initializes placeable via FUN_0058a480 with ObjectId
        ///     - If param_4 == 0: Loads placeable data directly from GIT struct via FUN_00588010 (LoadPlaceableFromGFF)
        ///     - If param_4 != 0: Loads template from "TemplateResRef" via FUN_0058a730, then loads "UseTweakColor" and "TweakColor" from GIT
        ///     - Reads "Bearing" (float, rotation around Z axis) and converts to quaternion via FUN_004da020 and FUN_00587d60
        ///     - Sets orientation quaternion via FUN_00587d60
        ///     - If param_3 != 0: Loads script hooks from GIT struct via FUN_0050b650
        ///     - Reads position: "X", "Y", "Z" (float, not XPosition/YPosition/ZPosition) via FUN_00412e20
        ///     - Adds placeable to area via AddToArea
        ///     - Registers with ObjectId list if needed via FUN_00482570
        /// - AreaId loading: 0x005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90
        /// - Located via string reference: "AreaId" @ 0x007bef48
        /// </remarks>
        [CanBeNull]
        public IEntity CreatePlaceableFromGit(GFFStruct gitStruct, Module module)
        {
            uint objectId = GetObjectIdFromGit(gitStruct);
            var entity = new Entity(objectId, ObjectType.Placeable);

            // Placeables use "X", "Y", "Z" fields (not XPosition/YPosition/ZPosition) per 0x004e5d80
            // Fallback to XPosition/YPosition/ZPosition for compatibility
            float x = gitStruct.Exists("X") ? gitStruct.GetSingle("X") : (gitStruct.Exists("XPosition") ? gitStruct.GetSingle("XPosition") : 0f);
            float y = gitStruct.Exists("Y") ? gitStruct.GetSingle("Y") : (gitStruct.Exists("YPosition") ? gitStruct.GetSingle("YPosition") : 0f);
            float z = gitStruct.Exists("Z") ? gitStruct.GetSingle("Z") : (gitStruct.Exists("ZPosition") ? gitStruct.GetSingle("ZPosition") : 0f);
            System.Numerics.Vector3 position = new System.Numerics.Vector3(x, y, z);

            // Placeables use "Bearing" field (rotation around Z axis) per 0x004e5d80
            // Bearing is converted to quaternion in original implementation
            float facing = GetFacing(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
            
            // Load AreaId from GIT struct (swkotor2.exe: 0x005223a0 loads AreaId from GFF at offset 0x90)
            uint areaId = (uint)GetIntField(gitStruct, "AreaId", 0);
            entity.AreaId = areaId;
            
            entity.Position = position;
            entity.Facing = facing;

            // Load UseTweakColor and TweakColor from GIT (used when loading from template, param_4 != 0)
            // Per 0x004e5d80: These fields are loaded when TemplateResRef is used
            bool useTweakColor = GetIntField(gitStruct, "UseTweakColor", 0) != 0;
            int tweakColor = GetIntField(gitStruct, "TweakColor", 0);
            if (useTweakColor)
            {
                entity.SetData("UseTweakColor", true);
                entity.SetData("TweakColor", tweakColor);
            }

            // Load template
            string templateResRef = GetResRefField(gitStruct, "TemplateResRef");
            if (!string.IsNullOrEmpty(templateResRef))
            {
                LoadPlaceableTemplate(entity, module, templateResRef);
            }
            else
            {
                // If no template, load placeable data directly from GIT struct (param_4 == 0 path)
                // This matches FUN_00588010 (LoadPlaceableFromGFF) behavior
                LoadPlaceableFromGitStruct(entity, gitStruct);
            }

            return entity;
        }

        /// <summary>
        /// Loads placeable instance data directly from GIT struct (when no template is used).
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_00588010 @ 0x00588010 (LoadPlaceableFromGFF) loads placeable data from GIT struct
        /// - Called when param_4 == 0 in LoadPlaceablesFromGIT (0x004e5d80)
        /// - Loads all placeable properties from GIT struct instead of from UTP template
        /// - This is used for placeables that don't have a TemplateResRef or when loading from saved state
        /// </remarks>
        private void LoadPlaceableFromGitStruct(Entity entity, GFFStruct gitStruct)
        {
            // Load all placeable properties from GIT struct
            // Based on FUN_00588010 which loads placeable data from GFF struct
            entity.SetData("Appearance", GetIntField(gitStruct, "Appearance", 0));
            entity.SetData("Useable", GetIntField(gitStruct, "Useable", 0) != 0);
            entity.SetData("Locked", GetIntField(gitStruct, "Locked", 0) != 0);
            entity.SetData("Lockable", GetIntField(gitStruct, "Lockable", 0) != 0);
            entity.SetData("KeyRequired", GetIntField(gitStruct, "KeyRequired", 0) != 0);
            entity.SetData("KeyName", GetStringField(gitStruct, "KeyName"));
            entity.SetData("OpenLockDC", GetIntField(gitStruct, "OpenLockDC", 0));
            entity.SetData("Hardness", GetIntField(gitStruct, "Hardness", 0));
            entity.SetData("HP", GetIntField(gitStruct, "HP", 1));
            entity.SetData("CurrentHP", GetIntField(gitStruct, "CurrentHP", 1));
            entity.SetData("HasInventory", GetIntField(gitStruct, "HasInventory", 0) != 0);
            entity.SetData("Static", GetIntField(gitStruct, "Static", 0) != 0);
            entity.SetData("BodyBag", GetIntField(gitStruct, "BodyBag", 0) != 0);

            // Load script hooks from GIT struct (if param_3 != 0 in original, we always load them)
            SetEntityScripts(entity, gitStruct, new Dictionary<string, ScriptEvent>
            {
                { "OnClosed", ScriptEvent.OnClose },
                { "OnDamaged", ScriptEvent.OnDamaged },
                { "OnDeath", ScriptEvent.OnDeath },
                { "OnDisarm", ScriptEvent.OnDisarm },
                { "OnHeartbeat", ScriptEvent.OnHeartbeat },
                { "OnInvDisturbed", ScriptEvent.OnDisturbed },
                { "OnLock", ScriptEvent.OnLock },
                { "OnMeleeAttacked", ScriptEvent.OnPhysicalAttacked },
                { "OnOpen", ScriptEvent.OnOpen },
                { "OnSpellCastAt", ScriptEvent.OnSpellCastAt },
                { "OnTrapTriggered", ScriptEvent.OnTrapTriggered },
                { "OnUnlock", ScriptEvent.OnUnlock },
                { "OnUsed", ScriptEvent.OnUsed },
                { "OnUserDefined", ScriptEvent.OnUserDefined }
            });
        }

        /// <summary>
        /// Loads placeable template from UTP.
        /// </summary>
        private void LoadPlaceableTemplate(Entity entity, Module module, string templateResRef)
        {
            ModuleResource utpResource = module.Placeable(templateResRef);
            if (utpResource == null)
            {
                return;
            }

            object utpData = utpResource.Resource();
            if (utpData == null)
            {
                return;
            }

            GFF utpGff = utpData as GFF;
            if (utpGff == null)
            {
                return;
            }

            GFFStruct root = utpGff.Root;

            if (string.IsNullOrEmpty(entity.Tag))
            {
                entity.Tag = GetStringField(root, "Tag");
            }

            entity.SetData("TemplateResRef", templateResRef);
            entity.SetData("Appearance", GetIntField(root, "Appearance", 0));
            entity.SetData("Useable", GetIntField(root, "Useable", 0) != 0);
            entity.SetData("Locked", GetIntField(root, "Locked", 0) != 0);
            entity.SetData("Lockable", GetIntField(root, "Lockable", 0) != 0);
            entity.SetData("KeyRequired", GetIntField(root, "KeyRequired", 0) != 0);
            entity.SetData("KeyName", GetStringField(root, "KeyName"));
            entity.SetData("OpenLockDC", GetIntField(root, "OpenLockDC", 0));
            entity.SetData("Hardness", GetIntField(root, "Hardness", 0));
            entity.SetData("HP", GetIntField(root, "HP", 1));
            entity.SetData("CurrentHP", GetIntField(root, "CurrentHP", 1));
            entity.SetData("HasInventory", GetIntField(root, "HasInventory", 0) != 0);
            entity.SetData("Static", GetIntField(root, "Static", 0) != 0);
            entity.SetData("BodyBag", GetIntField(root, "BodyBag", 0) != 0);

            SetEntityScripts(entity, root, new Dictionary<string, ScriptEvent>
            {
                { "OnClosed", ScriptEvent.OnClose },
                { "OnDamaged", ScriptEvent.OnDamaged },
                { "OnDeath", ScriptEvent.OnDeath },
                { "OnDisarm", ScriptEvent.OnDisarm },
                { "OnHeartbeat", ScriptEvent.OnHeartbeat },
                { "OnInvDisturbed", ScriptEvent.OnDisturbed },
                { "OnLock", ScriptEvent.OnLock },
                { "OnMeleeAttacked", ScriptEvent.OnPhysicalAttacked },
                { "OnOpen", ScriptEvent.OnOpen },
                { "OnSpellCastAt", ScriptEvent.OnSpellCastAt },
                { "OnTrapTriggered", ScriptEvent.OnTrapTriggered },
                { "OnUnlock", ScriptEvent.OnUnlock },
                { "OnUsed", ScriptEvent.OnUsed },
                { "OnUserDefined", ScriptEvent.OnUserDefined }
            });

            // Load BWM hooks from placeable walkmesh (PWK)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Placeables have walkmesh files (PWK) with hook vectors defining interaction points
            // Located via string references: "USE1", "USE2" hook vectors in BWM format
            // Original implementation: Loads PWK file for placeable model, extracts RelativeHook1/RelativeHook2 or AbsoluteHook1/AbsoluteHook2
            LoadBWMHooks(entity, module, templateResRef, false);
        }

        /// <summary>
        /// Loads item template from UTI.
        /// </summary>
        private void LoadItemTemplate(Entity entity, Module module, string templateResRef)
        {
            ModuleResource utiResource = module.Resource(templateResRef, ResourceType.UTI);
            if (utiResource == null)
            {
                return;
            }

            object utiData = utiResource.Resource();
            if (utiData == null)
            {
                return;
            }

            GFF utiGff = utiData as GFF;
            if (utiGff == null)
            {
                return;
            }

            GFFStruct root = utiGff.Root;

            if (string.IsNullOrEmpty(entity.Tag))
            {
                entity.Tag = GetStringField(root, "Tag");
            }

            entity.SetData("TemplateResRef", templateResRef);

            // Create ItemComponent from UTI template data
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item component creation from UTI template
            // Located via string references: "ItemComponent" @ 0x007c41e4, "BaseItem" @ 0x007c0a78
            // Original implementation: Items loaded from UTI templates have ItemComponent with BaseItem, Properties, Charges, etc.
            var itemComponent = new BaseItemComponent
            {
                BaseItem = GetIntField(root, "BaseItem", 0),
                StackSize = GetIntField(root, "StackSize", 1),
                Charges = GetIntField(root, "Charges", 0),
                Cost = GetIntField(root, "Cost", 0),
                Identified = GetIntField(root, "Identified", 1) != 0,
                TemplateResRef = templateResRef
            };

            // Store additional item data for reference
            entity.SetData("LocalizedName", GetLocStringField(root, "LocalizedName"));
            entity.SetData("Description", GetLocStringField(root, "Description"));
            entity.SetData("MaxCharges", GetIntField(root, "MaxCharges", 0));
            entity.SetData("Stolen", GetIntField(root, "Stolen", 0) != 0);
            entity.SetData("Plot", GetIntField(root, "Plot", 0) != 0);
            entity.SetData("Cursed", GetIntField(root, "Cursed", 0) != 0);

            // Load item properties from PropertiesList
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item properties loaded from UTI PropertiesList array
            // Located via string references: "PropertiesList" @ 0x007c2f3c, "PropertyName" @ 0x007beb58
            // Original implementation: Each property entry has PropertyName, Subtype, CostTable, CostValue, Param1, Param1Value
            if (root.Exists("PropertiesList"))
            {
                GFFList propertiesList = root.GetList("PropertiesList");
                if (propertiesList != null)
                {
                    foreach (GFFStruct propStruct in propertiesList)
                    {
                        int propertyName = GetIntField(propStruct, "PropertyName", 0);
                        int subType = GetIntField(propStruct, "Subtype", 0);
                        int costTable = GetIntField(propStruct, "CostTable", 0);
                        int costValue = GetIntField(propStruct, "CostValue", 0);
                        int param1 = GetIntField(propStruct, "Param1", 0);
                        int param1Value = GetIntField(propStruct, "Param1Value", 0);

                        var property = new Runtime.Core.Interfaces.Components.ItemProperty
                        {
                            PropertyType = propertyName,
                            Subtype = subType,
                            CostTable = costTable,
                            CostValue = costValue,
                            Param1 = param1,
                            Param1Value = param1Value
                        };
                        itemComponent.AddProperty(property);
                    }
                }
            }

            // Add ItemComponent to entity
            entity.AddComponent(itemComponent);

            SetEntityScripts(entity, root, new Dictionary<string, ScriptEvent>
            {
                { "OnUsed", ScriptEvent.OnUsed },
                { "OnUserDefined", ScriptEvent.OnUserDefined }
            });
        }

        /// <summary>
        /// Creates a trigger from GIT instance struct.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: 0x005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90.
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// </remarks>
        [CanBeNull]
        public IEntity CreateTriggerFromGit(GFFStruct gitStruct)
        {
            uint objectId = GetObjectIdFromGit(gitStruct);
            var entity = new Entity(objectId, ObjectType.Trigger);

            System.Numerics.Vector3 position = GetPosition(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
            
            // Load AreaId from GIT struct (swkotor2.exe: 0x005223a0 loads AreaId from GFF at offset 0x90)
            uint areaId = (uint)GetIntField(gitStruct, "AreaId", 0);
            entity.AreaId = areaId;
            
            entity.Position = position;

            // Trigger geometry
            if (gitStruct.Exists("Geometry"))
            {
                GFFList geometryList = gitStruct.GetList("Geometry");
                if (geometryList != null)
                {
                    var points = new List<System.Numerics.Vector3>();
                    foreach (GFFStruct pointStruct in geometryList)
                    {
                        float px = pointStruct.Exists("PointX") ? pointStruct.GetSingle("PointX") : 0f;
                        float py = pointStruct.Exists("PointY") ? pointStruct.GetSingle("PointY") : 0f;
                        float pz = pointStruct.Exists("PointZ") ? pointStruct.GetSingle("PointZ") : 0f;
                        points.Add(new System.Numerics.Vector3(px, py, pz));
                    }
                    entity.SetData("Geometry", points);
                }
            }

            // Scripts
            SetEntityScripts(entity, gitStruct, new Dictionary<string, ScriptEvent>
            {
                { "OnClick", ScriptEvent.OnClick },
                { "OnDisarm", ScriptEvent.OnDisarm },
                { "OnTrapTriggered", ScriptEvent.OnTrapTriggered },
                { "ScriptHeartbeat", ScriptEvent.OnHeartbeat },
                { "ScriptOnEnter", ScriptEvent.OnEnter },
                { "ScriptOnExit", ScriptEvent.OnExit },
                { "ScriptUserDefine", ScriptEvent.OnUserDefined }
            });

            return entity;
        }

        /// <summary>
        /// Creates a waypoint from a template ResRef at a specific position.
        /// </summary>
        [CanBeNull]
        public IEntity CreateWaypointFromTemplate(string templateResRef, System.Numerics.Vector3 position, float facing)
        {
            if (string.IsNullOrEmpty(templateResRef))
            {
                return null;
            }

            var entity = new Entity(GetNextObjectId(), ObjectType.Waypoint);
            entity.Position = position;
            entity.Facing = facing;
            entity.Tag = templateResRef; // For waypoints, template is typically the tag

            return entity;
        }

        /// <summary>
        /// Creates a waypoint from GIT instance struct.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: 0x005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90.
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// </remarks>
        [CanBeNull]
        public IEntity CreateWaypointFromGit(GFFStruct gitStruct)
        {
            uint objectId = GetObjectIdFromGit(gitStruct);
            var entity = new Entity(objectId, ObjectType.Waypoint);

            System.Numerics.Vector3 position = GetPosition(gitStruct);
            float facing = GetFacing(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
            
            // Load AreaId from GIT struct (swkotor2.exe: 0x005223a0 loads AreaId from GFF at offset 0x90)
            uint areaId = (uint)GetIntField(gitStruct, "AreaId", 0);
            entity.AreaId = areaId;
            
            entity.Position = position;
            entity.Facing = facing;

            // Waypoint properties
            entity.SetData("LocalizedName", GetLocStringField(gitStruct, "LocalizedName"));
            entity.SetData("Description", GetLocStringField(gitStruct, "Description"));
            entity.SetData("Appearance", GetIntField(gitStruct, "Appearance", 0));
            entity.SetData("MapNote", GetIntField(gitStruct, "MapNote", 0) != 0);
            entity.SetData("MapNoteEnabled", GetIntField(gitStruct, "MapNoteEnabled", 0) != 0);

            return entity;
        }

        /// <summary>
        /// Creates a sound from GIT instance struct.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: 0x005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90.
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// </remarks>
        [CanBeNull]
        public IEntity CreateSoundFromGit(GFFStruct gitStruct)
        {
            uint objectId = GetObjectIdFromGit(gitStruct);
            var entity = new Entity(objectId, ObjectType.Sound);

            System.Numerics.Vector3 position = GetPosition(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
            
            // Load AreaId from GIT struct (swkotor2.exe: 0x005223a0 loads AreaId from GFF at offset 0x90)
            uint areaId = (uint)GetIntField(gitStruct, "AreaId", 0);
            entity.AreaId = areaId;
            
            entity.Position = position;

            // Sound properties
            entity.SetData("Active", GetIntField(gitStruct, "Active", 1) != 0);
            entity.SetData("Continuous", GetIntField(gitStruct, "Continuous", 0) != 0);
            entity.SetData("Looping", GetIntField(gitStruct, "Looping", 0) != 0);
            entity.SetData("Positional", GetIntField(gitStruct, "Positional", 1) != 0);
            entity.SetData("Random", GetIntField(gitStruct, "Random", 0) != 0);
            entity.SetData("RandomPosition", GetIntField(gitStruct, "RandomPosition", 0) != 0);
            entity.SetData("Volume", GetIntField(gitStruct, "Volume", 100));
            entity.SetData("VolumeVrtn", GetIntField(gitStruct, "VolumeVrtn", 0));
            entity.SetData("MaxDistance", gitStruct.Exists("MaxDistance") ? gitStruct.GetSingle("MaxDistance") : 30f);
            entity.SetData("MinDistance", gitStruct.Exists("MinDistance") ? gitStruct.GetSingle("MinDistance") : 1f);

            // Sound list
            if (gitStruct.Exists("Sounds"))
            {
                GFFList soundList = gitStruct.GetList("Sounds");
                if (soundList != null)
                {
                    var sounds = new List<string>();
                    foreach (GFFStruct soundStruct in soundList)
                    {
                        string sound = GetResRefField(soundStruct, "Sound");
                        if (!string.IsNullOrEmpty(sound))
                        {
                            sounds.Add(sound);
                        }
                    }
                    entity.SetData("Sounds", sounds);
                }
            }

            return entity;
        }

        /// <summary>
        /// Creates a store from GIT instance struct.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: 0x005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90.
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// </remarks>
        [CanBeNull]
        public IEntity CreateStoreFromGit(GFFStruct gitStruct, Module module)
        {
            uint objectId = GetObjectIdFromGit(gitStruct);
            var entity = new Entity(objectId, ObjectType.Store);

            System.Numerics.Vector3 position = GetPosition(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
            
            // Load AreaId from GIT struct (swkotor2.exe: 0x005223a0 loads AreaId from GFF at offset 0x90)
            uint areaId = (uint)GetIntField(gitStruct, "AreaId", 0);
            entity.AreaId = areaId;
            
            entity.Position = position;

            // Load template if specified
            string templateResRef = GetResRefField(gitStruct, "TemplateResRef");
            if (!string.IsNullOrEmpty(templateResRef) && module != null)
            {
                LoadStoreTemplate(entity, module, templateResRef);
            }
            else
            {
                entity.SetData("ResRef", GetResRefField(gitStruct, "ResRef"));
            }

            return entity;
        }

        /// <summary>
        /// Loads store template from UTM.
        /// </summary>
        private void LoadStoreTemplate(Entity entity, Module module, string templateResRef)
        {
            ModuleResource utmResource = module.Store(templateResRef);
            if (utmResource == null)
            {
                return;
            }

            object utmData = utmResource.Resource();
            if (utmData == null)
            {
                return;
            }

            GFF utmGff = utmData as GFF;
            if (utmGff == null)
            {
                return;
            }

            GFFStruct root = utmGff.Root;

            if (string.IsNullOrEmpty(entity.Tag))
            {
                entity.Tag = GetStringField(root, "Tag");
            }

            entity.SetData("TemplateResRef", templateResRef);
            entity.SetData("ResRef", GetStringField(root, "ResRef"));
            entity.SetData("ID", GetIntField(root, "ID", 0));
            entity.SetData("MarkUp", GetIntField(root, "MarkUp", 0));
            entity.SetData("MarkUpRate", GetIntField(root, "MarkUpRate", 0));
            entity.SetData("MarkDown", GetIntField(root, "MarkDown", 0));
            entity.SetData("MarkDownRate", GetIntField(root, "MarkDownRate", 0));

            SetEntityScripts(entity, root, new Dictionary<string, ScriptEvent>
            {
                { "OnOpenStore", ScriptEvent.OnStoreOpen },
                { "OnStoreClosed", ScriptEvent.OnStoreClose },
                { "OnUserDefined", ScriptEvent.OnUserDefined }
            });
        }

        /// <summary>
        /// Creates an encounter from GIT instance struct.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: 0x005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90.
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// </remarks>
        [CanBeNull]
        public IEntity CreateEncounterFromGit(GFFStruct gitStruct)
        {
            uint objectId = GetObjectIdFromGit(gitStruct);
            var entity = new Entity(objectId, ObjectType.Encounter);

            System.Numerics.Vector3 position = GetPosition(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
            
            // Load AreaId from GIT struct (swkotor2.exe: 0x005223a0 loads AreaId from GFF at offset 0x90)
            uint areaId = (uint)GetIntField(gitStruct, "AreaId", 0);
            entity.AreaId = areaId;
            
            entity.Position = position;

            // Encounter properties
            entity.SetData("Active", GetIntField(gitStruct, "Active", 1) != 0);
            entity.SetData("Difficulty", GetIntField(gitStruct, "Difficulty", 0));
            entity.SetData("DifficultyIndex", GetIntField(gitStruct, "DifficultyIndex", 0));
            entity.SetData("MaxCreatures", GetIntField(gitStruct, "MaxCreatures", 1));
            entity.SetData("RecCreatures", GetIntField(gitStruct, "RecCreatures", 1));
            entity.SetData("Faction", GetIntField(gitStruct, "Faction", 0));
            entity.SetData("Reset", GetIntField(gitStruct, "Reset", 0) != 0);
            entity.SetData("ResetTime", GetIntField(gitStruct, "ResetTime", 0));
            entity.SetData("Respawns", GetIntField(gitStruct, "Respawns", 0));
            entity.SetData("SpawnOption", GetIntField(gitStruct, "SpawnOption", 0));

            // Creature list
            if (gitStruct.Exists("CreatureList"))
            {
                GFFList creatureList = gitStruct.GetList("CreatureList");
                if (creatureList != null)
                {
                    var creatures = new List<string>();
                    foreach (GFFStruct creatureStruct in creatureList)
                    {
                        string resRef = GetResRefField(creatureStruct, "ResRef");
                        if (!string.IsNullOrEmpty(resRef))
                        {
                            creatures.Add(resRef);
                        }
                    }
                    entity.SetData("CreatureList", creatures);
                }
            }

            // Geometry
            if (gitStruct.Exists("Geometry"))
            {
                GFFList geometryList = gitStruct.GetList("Geometry");
                if (geometryList != null)
                {
                    var points = new List<System.Numerics.Vector3>();
                    foreach (GFFStruct pointStruct in geometryList)
                    {
                        float px = pointStruct.Exists("X") ? pointStruct.GetSingle("X") : 0f;
                        float py = pointStruct.Exists("Y") ? pointStruct.GetSingle("Y") : 0f;
                        float pz = pointStruct.Exists("Z") ? pointStruct.GetSingle("Z") : 0f;
                        points.Add(new System.Numerics.Vector3(px, py, pz));
                    }
                    entity.SetData("Geometry", points);
                }
            }

            // Scripts
            SetEntityScripts(entity, gitStruct, new Dictionary<string, ScriptEvent>
            {
                { "OnEntered", ScriptEvent.OnEnter },
                { "OnExhausted", ScriptEvent.OnExhausted },
                { "OnExit", ScriptEvent.OnExit },
                { "OnHeartbeat", ScriptEvent.OnHeartbeat },
                { "OnUserDefined", ScriptEvent.OnUserDefined }
            });

            return entity;
        }

        #region Helper Methods

        private static System.Numerics.Vector3 GetPosition(GFFStruct gitStruct)
        {
            float x = gitStruct.Exists("XPosition") ? gitStruct.GetSingle("XPosition") : 0f;
            float y = gitStruct.Exists("YPosition") ? gitStruct.GetSingle("YPosition") : 0f;
            float z = gitStruct.Exists("ZPosition") ? gitStruct.GetSingle("ZPosition") : 0f;
            return new System.Numerics.Vector3(x, y, z);
        }

        private static float GetFacing(GFFStruct gitStruct)
        {
            if (gitStruct.Exists("Bearing"))
            {
                return gitStruct.GetSingle("Bearing");
            }
            // Calculate from XOrientation/YOrientation
            float xOri = gitStruct.Exists("XOrientation") ? gitStruct.GetSingle("XOrientation") : 0f;
            float yOri = gitStruct.Exists("YOrientation") ? gitStruct.GetSingle("YOrientation") : 1f;
            return (float)Math.Atan2(yOri, xOri);
        }

        private static string GetResRefField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                ResRef resRef = root.GetResRef(fieldName);
                if (resRef != null)
                {
                    return resRef.ToString();
                }
            }
            return string.Empty;
        }

        private static string GetStringField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                return root.GetString(fieldName) ?? string.Empty;
            }
            return string.Empty;
        }

        private static string GetLocStringField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                LocalizedString locStr = root.GetLocString(fieldName);
                if (locStr != null)
                {
                    return locStr.ToString();
                }
            }
            return string.Empty;
        }

        private static int GetIntField(GFFStruct root, string fieldName, int defaultValue = 0)
        {
            if (root.Exists(fieldName))
            {
                return root.GetInt32(fieldName);
            }
            return defaultValue;
        }

        private static void SetEntityScripts(Entity entity, GFFStruct root, Dictionary<string, ScriptEvent> mappings)
        {
            foreach (KeyValuePair<string, ScriptEvent> mapping in mappings)
            {
                if (root.Exists(mapping.Key))
                {
                    ResRef scriptRef = root.GetResRef(mapping.Key);
                    if (scriptRef != null && !string.IsNullOrEmpty(scriptRef.ToString()))
                    {
                        entity.SetScript(mapping.Value, scriptRef.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Loads BWM hook vectors from door/placeable walkmesh files (DWK/PWK).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Objects have walkmesh files with hook vectors defining interaction points
        /// Located via string references: "USE1", "USE2" hook vectors in BWM format
        /// Original implementation: Loads DWK/PWK file, extracts RelativeHook1/RelativeHook2 or AbsoluteHook1/AbsoluteHook2
        /// </summary>
        /// <param name="entity">Entity to store hooks in</param>
        /// <param name="module">Module to load BWM from</param>
        /// <param name="templateResRef">Template ResRef (often matches model name)</param>
        /// <param name="isDoor">True for doors (DWK), false for placeables (PWK)</param>
        private void LoadBWMHooks(Entity entity, Module module, string templateResRef, bool isDoor)
        {
            if (module == null || string.IsNullOrEmpty(templateResRef))
            {
                return;
            }

            try
            {
                // Try to load BWM file
                // For doors: try <model>0.dwk (closed state), <model>.dwk
                // For placeables: try <model>.pwk
                string bwmResRef = templateResRef;
                if (isDoor)
                {
                    // Try closed door state first (<model>0.dwk)
                    ModuleResource bwmResource = module.Resource(bwmResRef + "0", ResourceType.DWK);
                    if (bwmResource == null)
                    {
                        // Fallback to <model>.dwk
                        bwmResource = module.Resource(bwmResRef, ResourceType.DWK);
                    }

                    if (bwmResource != null)
                    {
                        object bwmData = bwmResource.Resource();
                        if (bwmData != null && bwmData is BioWare.NET.Resource.Formats.BWM.BWM bwm)
                        {
                            // Extract hook vectors
                            // Prefer absolute hooks if available (world space), otherwise use relative hooks + entity position
                            System.Numerics.Vector3 hook1 = System.Numerics.Vector3.Zero;
                            System.Numerics.Vector3 hook2 = System.Numerics.Vector3.Zero;
                            bool hasHooks = false;

                            // Check if absolute hooks are available (non-zero)
                            // Vector3.FromNull() returns Zero, so check if hook is not zero
                            if (bwm.AbsoluteHook1.X != 0f || bwm.AbsoluteHook1.Y != 0f || bwm.AbsoluteHook1.Z != 0f)
                            {
                                hook1 = new System.Numerics.Vector3(bwm.AbsoluteHook1.X, bwm.AbsoluteHook1.Y, bwm.AbsoluteHook1.Z);
                                hasHooks = true;
                            }
                            else if (bwm.RelativeHook1.X != 0f || bwm.RelativeHook1.Y != 0f || bwm.RelativeHook1.Z != 0f)
                            {
                                // Convert relative hook to world space
                                System.Numerics.Vector3 entityPos = entity.Position;
                                hook1 = entityPos + new System.Numerics.Vector3(bwm.RelativeHook1.X, bwm.RelativeHook1.Y, bwm.RelativeHook1.Z);
                                hasHooks = true;
                            }

                            if (bwm.AbsoluteHook2.X != 0f || bwm.AbsoluteHook2.Y != 0f || bwm.AbsoluteHook2.Z != 0f)
                            {
                                hook2 = new System.Numerics.Vector3(bwm.AbsoluteHook2.X, bwm.AbsoluteHook2.Y, bwm.AbsoluteHook2.Z);
                            }
                            else if (bwm.RelativeHook2.X != 0f || bwm.RelativeHook2.Y != 0f || bwm.RelativeHook2.Z != 0f)
                            {
                                // Convert relative hook to world space
                                System.Numerics.Vector3 entityPos = entity.Position;
                                hook2 = entityPos + new System.Numerics.Vector3(bwm.RelativeHook2.X, bwm.RelativeHook2.Y, bwm.RelativeHook2.Z);
                            }

                            // Store hooks in entity data
                            if (hasHooks)
                            {
                                entity.SetData("BWMHook1", hook1);
                                if (hook2 != System.Numerics.Vector3.Zero || (bwm.RelativeHook2.X != 0f || bwm.RelativeHook2.Y != 0f || bwm.RelativeHook2.Z != 0f))
                                {
                                    entity.SetData("BWMHook2", hook2);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Placeable: try <model>.pwk
                    ModuleResource bwmResource = module.Resource(bwmResRef, ResourceType.PWK);
                    if (bwmResource != null)
                    {
                        object bwmData = bwmResource.Resource();
                        if (bwmData != null && bwmData is BioWare.NET.Resource.Formats.BWM.BWM bwm)
                        {
                            // Extract hook vectors (same logic as doors)
                            System.Numerics.Vector3 hook1 = System.Numerics.Vector3.Zero;
                            System.Numerics.Vector3 hook2 = System.Numerics.Vector3.Zero;
                            bool hasHooks = false;

                            if (bwm.AbsoluteHook1.X != 0f || bwm.AbsoluteHook1.Y != 0f || bwm.AbsoluteHook1.Z != 0f)
                            {
                                hook1 = new System.Numerics.Vector3(bwm.AbsoluteHook1.X, bwm.AbsoluteHook1.Y, bwm.AbsoluteHook1.Z);
                                hasHooks = true;
                            }
                            else if (bwm.RelativeHook1.X != 0f || bwm.RelativeHook1.Y != 0f || bwm.RelativeHook1.Z != 0f)
                            {
                                System.Numerics.Vector3 entityPos = entity.Position;
                                hook1 = entityPos + new System.Numerics.Vector3(bwm.RelativeHook1.X, bwm.RelativeHook1.Y, bwm.RelativeHook1.Z);
                                hasHooks = true;
                            }

                            if (bwm.AbsoluteHook2.X != 0f || bwm.AbsoluteHook2.Y != 0f || bwm.AbsoluteHook2.Z != 0f)
                            {
                                hook2 = new System.Numerics.Vector3(bwm.AbsoluteHook2.X, bwm.AbsoluteHook2.Y, bwm.AbsoluteHook2.Z);
                            }
                            else if (bwm.RelativeHook2.X != 0f || bwm.RelativeHook2.Y != 0f || bwm.RelativeHook2.Z != 0f)
                            {
                                System.Numerics.Vector3 entityPos = entity.Position;
                                hook2 = entityPos + new System.Numerics.Vector3(bwm.RelativeHook2.X, bwm.RelativeHook2.Y, bwm.RelativeHook2.Z);
                            }

                            if (hasHooks)
                            {
                                entity.SetData("BWMHook1", hook1);
                                if (hook2 != System.Numerics.Vector3.Zero || (bwm.RelativeHook2.X != 0f || bwm.RelativeHook2.Y != 0f || bwm.RelativeHook2.Z != 0f))
                                {
                                    entity.SetData("BWMHook2", hook2);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail - hooks are optional, entity will use position as fallback
            }
        }

        #endregion
    }
}
