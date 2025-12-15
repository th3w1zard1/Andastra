using System;
using System.Collections.Generic;
using System.Numerics;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;
using Odyssey.Core.Entities;
using Odyssey.Core.Interfaces;
using ObjectType = Odyssey.Core.Enums.ObjectType;
using ScriptEvent = Odyssey.Core.Enums.ScriptEvent;

namespace Odyssey.Kotor.Loading
{
    /// <summary>
    /// Factory for creating runtime entities from GFF templates.
    /// </summary>
    /// <remarks>
    /// Entity Factory System:
    /// - Based on swkotor2.exe entity creation system
    /// - Located via string references: "TemplateResRef" @ 0x007bd00c, "ScriptHeartbeat" @ 0x007beeb0
    /// - "tmpgit" @ 0x007be618 (temporary GIT structure references)
    /// - Original implementation: Creates runtime entities from GIT instance data and GFF templates
    /// - Entities created from GIT instances override template values with instance-specific data
    ///
    /// GFF Template Types:
    /// - UTC → Creature (Appearance_Type, Faction, HP, Attributes, Feats, Scripts)
    /// - UTP → Placeable (Appearance, Useable, Locked, OnUsed)
    /// - UTD → Door (GenericType, Locked, OnOpen, OnClose)
    /// - UTT → Trigger (Geometry polygon, OnEnter, OnExit)
    /// - UTW → Waypoint (Tag, position)
    /// - UTS → Sound (Active, Looping, Positional, ResRef)
    /// - UTE → Encounter (Creature list, spawn conditions)
    /// - UTI → Item (BaseItem, Properties, Charges)
    /// </remarks>
    public class EntityFactory
    {
        private uint _nextObjectId = 1;

        /// <summary>
        /// Gets the next available object ID.
        /// </summary>
        private uint GetNextObjectId()
        {
            return _nextObjectId++;
        }

        /// <summary>
        /// Creates a creature from GIT instance struct.
        /// </summary>
        [CanBeNull]
        public IEntity CreateCreatureFromGit(GFFStruct gitStruct, Module module)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Creature);

            // Get position
            System.Numerics.Vector3 position = GetPosition(gitStruct);
            float facing = GetFacing(gitStruct);

            // Basic properties
            entity.Tag = GetResRefField(gitStruct, "Tag");

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
        }

        /// <summary>
        /// Creates a door from GIT instance struct.
        /// </summary>
        [CanBeNull]
        public IEntity CreateDoorFromGit(GFFStruct gitStruct, Module module)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Door);

            System.Numerics.Vector3 position = GetPosition(gitStruct);
            float facing = GetFacing(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
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

            SetEntityScripts(entity, root, new Dictionary<string, ScriptEvent>
            {
                { "OnClick", ScriptEvent.OnClick },
                { "OnClosed", ScriptEvent.OnClose },
                { "OnDamaged", ScriptEvent.OnDamaged },
                { "OnDeath", ScriptEvent.OnDeath },
                { "OnDisarm", ScriptEvent.OnDisarm },
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
            // Based on swkotor2.exe: Doors have walkmesh files (DWK) with hook vectors defining interaction points
            // Located via string references: "USE1", "USE2" hook vectors in BWM format
            // Original implementation: Loads DWK file for door model, extracts RelativeHook1/RelativeHook2 or AbsoluteHook1/AbsoluteHook2
            LoadBWMHooks(entity, module, templateResRef, true);
        }

        /// <summary>
        /// Creates a placeable from GIT instance struct.
        /// </summary>
        [CanBeNull]
        public IEntity CreatePlaceableFromGit(GFFStruct gitStruct, Module module)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Placeable);

            System.Numerics.Vector3 position = GetPosition(gitStruct);
            float facing = GetFacing(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
            entity.Position = position;
            entity.Facing = facing;

            // Load template
            string templateResRef = GetResRefField(gitStruct, "TemplateResRef");
            if (!string.IsNullOrEmpty(templateResRef))
            {
                LoadPlaceableTemplate(entity, module, templateResRef);
            }

            return entity;
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
            // Based on swkotor2.exe: Placeables have walkmesh files (PWK) with hook vectors defining interaction points
            // Located via string references: "USE1", "USE2" hook vectors in BWM format
            // Original implementation: Loads PWK file for placeable model, extracts RelativeHook1/RelativeHook2 or AbsoluteHook1/AbsoluteHook2
            LoadBWMHooks(entity, module, templateResRef, false);
        }

        /// <summary>
        /// Creates a trigger from GIT instance struct.
        /// </summary>
        [CanBeNull]
        public IEntity CreateTriggerFromGit(GFFStruct gitStruct)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Trigger);

            System.Numerics.Vector3 position = GetPosition(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
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
        /// Creates a waypoint from GIT instance struct.
        /// </summary>
        [CanBeNull]
        public IEntity CreateWaypointFromGit(GFFStruct gitStruct)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Waypoint);

            System.Numerics.Vector3 position = GetPosition(gitStruct);
            float facing = GetFacing(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
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
        [CanBeNull]
        public IEntity CreateSoundFromGit(GFFStruct gitStruct)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Sound);

            System.Numerics.Vector3 position = GetPosition(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
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
        [CanBeNull]
        public IEntity CreateStoreFromGit(GFFStruct gitStruct, Module module)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Store);

            System.Numerics.Vector3 position = GetPosition(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
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
        [CanBeNull]
        public IEntity CreateEncounterFromGit(GFFStruct gitStruct)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Encounter);

            System.Numerics.Vector3 position = GetPosition(gitStruct);

            entity.Tag = GetResRefField(gitStruct, "Tag");
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
        /// Based on swkotor2.exe: Objects have walkmesh files with hook vectors defining interaction points
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
                        if (bwmData != null && bwmData is CSharpKOTOR.Formats.BWM.BWM bwm)
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
                        if (bwmData != null && bwmData is CSharpKOTOR.Formats.BWM.BWM bwm)
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
