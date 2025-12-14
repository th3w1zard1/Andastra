using System;
using System.Collections.Generic;
using System.Numerics;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using JetBrains.Annotations;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Loading
{
    /// <summary>
    /// Factory for creating runtime entities from GFF templates.
    /// </summary>
    /// <remarks>
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
            Vector3 position = GetPosition(gitStruct);
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
        /// Loads creature template from UTC.
        /// </summary>
        private void LoadCreatureTemplate(Entity entity, Module module, string templateResRef)
        {
            var utcResource = module.Creature(templateResRef);
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
            entity.SetData("Appearance_Type", GetIntField(root, "Appearance_Type", 0));
            entity.SetData("FactionID", GetIntField(root, "FactionID", 0));
            entity.SetData("CurrentHitPoints", GetIntField(root, "CurrentHitPoints", 1));
            entity.SetData("MaxHitPoints", GetIntField(root, "MaxHitPoints", 1));
            entity.SetData("ForcePoints", GetIntField(root, "ForcePoints", 0));
            entity.SetData("MaxForcePoints", GetIntField(root, "MaxForcePoints", 0));

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
            
            Vector3 position = GetPosition(gitStruct);
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
            var utdResource = module.Door(templateResRef);
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
        }

        /// <summary>
        /// Creates a placeable from GIT instance struct.
        /// </summary>
        [CanBeNull]
        public IEntity CreatePlaceableFromGit(GFFStruct gitStruct, Module module)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Placeable);
            
            Vector3 position = GetPosition(gitStruct);
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
            var utpResource = module.Placeable(templateResRef);
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
        }

        /// <summary>
        /// Creates a trigger from GIT instance struct.
        /// </summary>
        [CanBeNull]
        public IEntity CreateTriggerFromGit(GFFStruct gitStruct)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Trigger);
            
            Vector3 position = GetPosition(gitStruct);
            
            entity.Tag = GetResRefField(gitStruct, "Tag");
            entity.Position = position;

            // Trigger geometry
            if (gitStruct.Exists("Geometry"))
            {
                var geometryList = gitStruct.GetList("Geometry");
                if (geometryList != null)
                {
                    var points = new List<Vector3>();
                    foreach (var pointStruct in geometryList)
                    {
                        float px = pointStruct.Exists("PointX") ? pointStruct.GetFloat("PointX") : 0f;
                        float py = pointStruct.Exists("PointY") ? pointStruct.GetFloat("PointY") : 0f;
                        float pz = pointStruct.Exists("PointZ") ? pointStruct.GetFloat("PointZ") : 0f;
                        points.Add(new Vector3(px, py, pz));
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
            
            Vector3 position = GetPosition(gitStruct);
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
            
            Vector3 position = GetPosition(gitStruct);
            
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
            entity.SetData("MaxDistance", gitStruct.Exists("MaxDistance") ? gitStruct.GetFloat("MaxDistance") : 30f);
            entity.SetData("MinDistance", gitStruct.Exists("MinDistance") ? gitStruct.GetFloat("MinDistance") : 1f);

            // Sound list
            if (gitStruct.Exists("Sounds"))
            {
                var soundList = gitStruct.GetList("Sounds");
                if (soundList != null)
                {
                    var sounds = new List<string>();
                    foreach (var soundStruct in soundList)
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
        public IEntity CreateStoreFromGit(GFFStruct gitStruct)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Store);
            
            Vector3 position = GetPosition(gitStruct);
            
            entity.Tag = GetResRefField(gitStruct, "Tag");
            entity.Position = position;

            entity.SetData("ResRef", GetResRefField(gitStruct, "ResRef"));

            return entity;
        }

        /// <summary>
        /// Creates an encounter from GIT instance struct.
        /// </summary>
        [CanBeNull]
        public IEntity CreateEncounterFromGit(GFFStruct gitStruct)
        {
            var entity = new Entity(GetNextObjectId(), ObjectType.Encounter);
            
            Vector3 position = GetPosition(gitStruct);
            
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
                var creatureList = gitStruct.GetList("CreatureList");
                if (creatureList != null)
                {
                    var creatures = new List<string>();
                    foreach (var creatureStruct in creatureList)
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
                var geometryList = gitStruct.GetList("Geometry");
                if (geometryList != null)
                {
                    var points = new List<Vector3>();
                    foreach (var pointStruct in geometryList)
                    {
                        float px = pointStruct.Exists("X") ? pointStruct.GetFloat("X") : 0f;
                        float py = pointStruct.Exists("Y") ? pointStruct.GetFloat("Y") : 0f;
                        float pz = pointStruct.Exists("Z") ? pointStruct.GetFloat("Z") : 0f;
                        points.Add(new Vector3(px, py, pz));
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

        private static Vector3 GetPosition(GFFStruct gitStruct)
        {
            float x = gitStruct.Exists("XPosition") ? gitStruct.GetFloat("XPosition") : 0f;
            float y = gitStruct.Exists("YPosition") ? gitStruct.GetFloat("YPosition") : 0f;
            float z = gitStruct.Exists("ZPosition") ? gitStruct.GetFloat("ZPosition") : 0f;
            return new Vector3(x, y, z);
        }

        private static float GetFacing(GFFStruct gitStruct)
        {
            if (gitStruct.Exists("Bearing"))
            {
                return gitStruct.GetFloat("Bearing");
            }
            // Calculate from XOrientation/YOrientation
            float xOri = gitStruct.Exists("XOrientation") ? gitStruct.GetFloat("XOrientation") : 0f;
            float yOri = gitStruct.Exists("YOrientation") ? gitStruct.GetFloat("YOrientation") : 1f;
            return (float)Math.Atan2(yOri, xOri);
        }

        private static string GetResRefField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                var resRef = root.GetResRef(fieldName);
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
                var locStr = root.GetLocString(fieldName);
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
            foreach (var mapping in mappings)
            {
                if (root.Exists(mapping.Key))
                {
                    var scriptRef = root.GetResRef(mapping.Key);
                    if (scriptRef != null && !string.IsNullOrEmpty(scriptRef.ToString()))
                    {
                        entity.SetScript(mapping.Value, scriptRef.ToString());
                    }
                }
            }
        }

        #endregion
    }
}
