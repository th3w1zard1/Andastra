using System;
using System.Collections.Generic;
using System.Numerics;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.Loading
{
    /// <summary>
    /// Factory for spawning entities from GFF templates.
    /// </summary>
    /// <remarks>
    /// Spawns entities from GIT instance data and UTC/UTP/UTD/etc. templates.
    /// Each entity type maps to specific GFF fields documented in the wiki specs.
    /// </remarks>
    public class EntityFactory
    {
        private readonly IWorld _world;

        public EntityFactory(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
        }

        #region Creature Spawning

        /// <summary>
        /// Spawns a creature from GIT instance data.
        /// </summary>
        public IEntity SpawnCreature(GFFStruct instance)
        {
            if (instance == null)
            {
                return null;
            }

            // Get position and facing
            var position = new Vector3(
                instance.GetSingle("XPosition"),
                instance.GetSingle("YPosition"),
                instance.GetSingle("ZPosition")
            );
            float facing = (float)Math.Atan2(
                instance.GetSingle("YOrientation"),
                instance.GetSingle("XOrientation")
            );

            // Create entity
            var entity = _world.CreateEntity(ObjectType.Creature, position, facing);

            // Set basic properties
            entity.Tag = instance.GetString("Tag");

            // Template reference for loading full data
            string templateRef = instance.GetResRef("TemplateResRef").Value ?? string.Empty;

            // Add creature component
            var creatureComponent = new CreatureComponent();
            creatureComponent.TemplateResRef = templateRef;
            creatureComponent.AppearanceType = instance.GetInt32("Appearance_Type");

            // Stats from instance (can override template)
            creatureComponent.CurrentHP = instance.GetInt16("CurrentHP");
            creatureComponent.MaxHP = instance.GetInt16("MaxHitPoints");
            creatureComponent.CurrentForce = instance.GetInt16("CurrentForce");
            creatureComponent.MaxForce = instance.GetInt16("MaxForcePoints");

            // Attributes
            creatureComponent.Strength = instance.GetUInt8("Str");
            creatureComponent.Dexterity = instance.GetUInt8("Dex");
            creatureComponent.Constitution = instance.GetUInt8("Con");
            creatureComponent.Intelligence = instance.GetUInt8("Int");
            creatureComponent.Wisdom = instance.GetUInt8("Wis");
            creatureComponent.Charisma = instance.GetUInt8("Cha");

            // Faction
            creatureComponent.FactionId = instance.GetInt32("FactionID");

            // Perception
            creatureComponent.PerceptionRange = instance.GetSingle("PerceptionRange");
            if (creatureComponent.PerceptionRange <= 0)
            {
                creatureComponent.PerceptionRange = 20f; // Default perception range
            }

            entity.AddComponent(creatureComponent);

            // Add transform component
            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent(transform);

            // Add script hooks
            var scripts = new ScriptHooksComponent();
            SetCreatureScripts(scripts, instance);
            entity.AddComponent(scripts);

            return entity;
        }

        private void SetCreatureScripts(ScriptHooksComponent scripts, GFFStruct data)
        {
            SetScript(scripts, data, "ScriptAttacked", ScriptEvent.OnAttacked);
            SetScript(scripts, data, "ScriptDamaged", ScriptEvent.OnDamaged);
            SetScript(scripts, data, "ScriptDeath", ScriptEvent.OnDeath);
            SetScript(scripts, data, "ScriptDialogue", ScriptEvent.OnConversation);
            SetScript(scripts, data, "ScriptDisturbed", ScriptEvent.OnDisturbed);
            SetScript(scripts, data, "ScriptEndDialogu", ScriptEvent.OnEndDialogue);
            SetScript(scripts, data, "ScriptEndRound", ScriptEvent.OnEndRound);
            SetScript(scripts, data, "ScriptHeartbeat", ScriptEvent.OnHeartbeat);
            SetScript(scripts, data, "ScriptOnBlocked", ScriptEvent.OnBlocked);
            SetScript(scripts, data, "ScriptOnNotice", ScriptEvent.OnPerception);
            SetScript(scripts, data, "ScriptRested", ScriptEvent.OnRested);
            SetScript(scripts, data, "ScriptSpawn", ScriptEvent.OnSpawn);
            SetScript(scripts, data, "ScriptSpellAt", ScriptEvent.OnSpellCastAt);
            SetScript(scripts, data, "ScriptUserDefine", ScriptEvent.OnUserDefined);
        }

        #endregion

        #region Placeable Spawning

        /// <summary>
        /// Spawns a placeable from GIT instance data.
        /// </summary>
        public IEntity SpawnPlaceable(GFFStruct instance)
        {
            if (instance == null)
            {
                return null;
            }

            var position = new Vector3(
                instance.GetSingle("X"),
                instance.GetSingle("Y"),
                instance.GetSingle("Z")
            );
            float facing = instance.GetSingle("Bearing");

            var entity = _world.CreateEntity(ObjectType.Placeable, position, facing);
            entity.Tag = instance.GetString("Tag");

            var placeable = new PlaceableComponent();
            placeable.TemplateResRef = instance.GetResRef("TemplateResRef").Value ?? string.Empty;
            placeable.AppearanceType = instance.GetInt32("Appearance");
            placeable.IsUseable = instance.GetUInt8("Useable") != 0;
            placeable.IsLocked = instance.GetUInt8("Locked") != 0;
            placeable.LockDC = instance.GetUInt8("OpenLockDC");
            placeable.KeyRequired = instance.GetUInt8("KeyRequired") != 0;
            placeable.KeyName = instance.GetString("KeyName");
            placeable.CurrentHP = instance.GetInt16("CurrentHP");
            placeable.MaxHP = instance.GetInt16("HP");

            entity.AddComponent(placeable);

            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent(transform);

            var scripts = new ScriptHooksComponent();
            SetPlaceableScripts(scripts, instance);
            entity.AddComponent(scripts);

            return entity;
        }

        private void SetPlaceableScripts(ScriptHooksComponent scripts, GFFStruct data)
        {
            SetScript(scripts, data, "OnClosed", ScriptEvent.OnClose);
            SetScript(scripts, data, "OnDamaged", ScriptEvent.OnDamaged);
            SetScript(scripts, data, "OnDeath", ScriptEvent.OnDeath);
            SetScript(scripts, data, "OnDisarm", ScriptEvent.OnDisarm);
            SetScript(scripts, data, "OnHeartbeat", ScriptEvent.OnHeartbeat);
            SetScript(scripts, data, "OnInvDisturbed", ScriptEvent.OnDisturbed);
            SetScript(scripts, data, "OnLock", ScriptEvent.OnLock);
            SetScript(scripts, data, "OnMeleeAttacked", ScriptEvent.OnAttacked);
            SetScript(scripts, data, "OnOpen", ScriptEvent.OnOpen);
            SetScript(scripts, data, "OnSpellCastAt", ScriptEvent.OnSpellCastAt);
            SetScript(scripts, data, "OnTrapTriggered", ScriptEvent.OnTrapTriggered);
            SetScript(scripts, data, "OnUnlock", ScriptEvent.OnUnlock);
            SetScript(scripts, data, "OnUsed", ScriptEvent.OnUsed);
            SetScript(scripts, data, "OnUserDefined", ScriptEvent.OnUserDefined);
        }

        #endregion

        #region Door Spawning

        /// <summary>
        /// Spawns a door from GIT instance data.
        /// </summary>
        public IEntity SpawnDoor(GFFStruct instance)
        {
            if (instance == null)
            {
                return null;
            }

            var position = new Vector3(
                instance.GetSingle("X"),
                instance.GetSingle("Y"),
                instance.GetSingle("Z")
            );
            float facing = instance.GetSingle("Bearing");

            var entity = _world.CreateEntity(ObjectType.Door, position, facing);
            entity.Tag = instance.GetString("Tag");

            var door = new DoorComponent();
            door.TemplateResRef = instance.GetResRef("TemplateResRef").Value ?? string.Empty;
            door.GenericType = instance.GetInt32("GenericType");
            door.IsOpen = instance.GetUInt8("AnimationState") == 1;
            door.IsLocked = instance.GetUInt8("Locked") != 0;
            door.LockDC = instance.GetUInt8("OpenLockDC");
            door.KeyRequired = instance.GetUInt8("KeyRequired") != 0;
            door.KeyName = instance.GetString("KeyName");
            door.LinkedTo = instance.GetString("LinkedTo");
            door.LinkedToModule = instance.GetResRef("LinkedToModule").Value ?? string.Empty;
            door.LinkedToFlags = instance.GetUInt8("LinkedToFlags");
            door.TransitionDestination = instance.GetString("TransitionDestin");
            door.CurrentHP = instance.GetInt16("CurrentHP");
            door.MaxHP = instance.GetInt16("HP");

            entity.AddComponent(door);

            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent(transform);

            var scripts = new ScriptHooksComponent();
            SetDoorScripts(scripts, instance);
            entity.AddComponent(scripts);

            return entity;
        }

        private void SetDoorScripts(ScriptHooksComponent scripts, GFFStruct data)
        {
            SetScript(scripts, data, "OnClick", ScriptEvent.OnClick);
            SetScript(scripts, data, "OnClosed", ScriptEvent.OnClose);
            SetScript(scripts, data, "OnDamaged", ScriptEvent.OnDamaged);
            SetScript(scripts, data, "OnDeath", ScriptEvent.OnDeath);
            SetScript(scripts, data, "OnDisarm", ScriptEvent.OnDisarm);
            SetScript(scripts, data, "OnHeartbeat", ScriptEvent.OnHeartbeat);
            SetScript(scripts, data, "OnLock", ScriptEvent.OnLock);
            SetScript(scripts, data, "OnMeleeAttacked", ScriptEvent.OnAttacked);
            SetScript(scripts, data, "OnOpen", ScriptEvent.OnOpen);
            SetScript(scripts, data, "OnSpellCastAt", ScriptEvent.OnSpellCastAt);
            SetScript(scripts, data, "OnTrapTriggered", ScriptEvent.OnTrapTriggered);
            SetScript(scripts, data, "OnUnlock", ScriptEvent.OnUnlock);
            SetScript(scripts, data, "OnUserDefined", ScriptEvent.OnUserDefined);
            SetScript(scripts, data, "OnFailToOpen", ScriptEvent.OnFailToOpen);
        }

        #endregion

        #region Trigger Spawning

        /// <summary>
        /// Spawns a trigger from GIT instance data.
        /// </summary>
        public IEntity SpawnTrigger(GFFStruct instance)
        {
            if (instance == null)
            {
                return null;
            }

            var position = new Vector3(
                instance.GetSingle("XPosition"),
                instance.GetSingle("YPosition"),
                instance.GetSingle("ZPosition")
            );

            var entity = _world.CreateEntity(ObjectType.Trigger, position, 0f);
            entity.Tag = instance.GetString("Tag");

            var trigger = new TriggerComponent();
            trigger.TemplateResRef = instance.GetResRef("TemplateResRef").Value ?? string.Empty;
            trigger.Type = instance.GetInt32("Type");
            trigger.LinkedTo = instance.GetString("LinkedTo");
            trigger.LinkedToModule = instance.GetResRef("LinkedToModule").Value ?? string.Empty;
            trigger.LinkedToFlags = instance.GetUInt8("LinkedToFlags");
            trigger.TransitionDestination = instance.GetString("TransitionDestin");

            // Parse geometry
            GFFList geometry;
            if (instance.TryGetList("Geometry", out geometry))
            {
                foreach (var point in geometry)
                {
                    trigger.Vertices.Add(new Vector3(
                        point.GetSingle("PointX"),
                        point.GetSingle("PointY"),
                        point.GetSingle("PointZ")
                    ));
                }
            }

            entity.AddComponent(trigger);

            var transform = new TransformComponent
            {
                Position = position,
                Facing = 0f
            };
            entity.AddComponent(transform);

            var scripts = new ScriptHooksComponent();
            SetTriggerScripts(scripts, instance);
            entity.AddComponent(scripts);

            return entity;
        }

        private void SetTriggerScripts(ScriptHooksComponent scripts, GFFStruct data)
        {
            SetScript(scripts, data, "OnClick", ScriptEvent.OnClick);
            SetScript(scripts, data, "OnDisarm", ScriptEvent.OnDisarm);
            SetScript(scripts, data, "OnEnter", ScriptEvent.OnEnter);
            SetScript(scripts, data, "OnExit", ScriptEvent.OnExit);
            SetScript(scripts, data, "OnHeartbeat", ScriptEvent.OnHeartbeat);
            SetScript(scripts, data, "OnTrapTriggered", ScriptEvent.OnTrapTriggered);
            SetScript(scripts, data, "OnUserDefined", ScriptEvent.OnUserDefined);
        }

        #endregion

        #region Waypoint Spawning

        /// <summary>
        /// Spawns a waypoint from GIT instance data.
        /// </summary>
        public IEntity SpawnWaypoint(GFFStruct instance)
        {
            if (instance == null)
            {
                return null;
            }

            var position = new Vector3(
                instance.GetSingle("XPosition"),
                instance.GetSingle("YPosition"),
                instance.GetSingle("ZPosition")
            );
            float facing = (float)Math.Atan2(
                instance.GetSingle("YOrientation"),
                instance.GetSingle("XOrientation")
            );

            var entity = _world.CreateEntity(ObjectType.Waypoint, position, facing);
            entity.Tag = instance.GetString("Tag");

            var waypoint = new WaypointComponent();
            waypoint.TemplateResRef = instance.GetResRef("TemplateResRef").Value ?? string.Empty;
            waypoint.MapNote = instance.GetString("MapNote");
            waypoint.MapNoteEnabled = instance.GetUInt8("MapNoteEnabled") != 0;
            waypoint.HasMapNote = instance.GetUInt8("HasMapNote") != 0;

            entity.AddComponent(waypoint);

            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent(transform);

            return entity;
        }

        #endregion

        #region Sound Spawning

        /// <summary>
        /// Spawns a sound from GIT instance data.
        /// </summary>
        public IEntity SpawnSound(GFFStruct instance)
        {
            if (instance == null)
            {
                return null;
            }

            var position = new Vector3(
                instance.GetSingle("XPosition"),
                instance.GetSingle("YPosition"),
                instance.GetSingle("ZPosition")
            );

            var entity = _world.CreateEntity(ObjectType.Sound, position, 0f);
            entity.Tag = instance.GetString("Tag");

            var sound = new SoundComponent();
            sound.TemplateResRef = instance.GetResRef("TemplateResRef").Value ?? string.Empty;
            sound.Active = instance.GetUInt8("Active") != 0;
            sound.Continuous = instance.GetUInt8("Continuous") != 0;
            sound.Looping = instance.GetUInt8("Looping") != 0;
            sound.Positional = instance.GetUInt8("Positional") != 0;
            sound.RandomPosition = instance.GetUInt8("RandomPosition") != 0;
            sound.Random = instance.GetUInt8("Random") != 0;
            sound.Volume = instance.GetUInt8("Volume");
            sound.VolumeVrtn = instance.GetUInt8("VolumeVrtn");
            sound.MaxDistance = instance.GetSingle("MaxDistance");
            sound.MinDistance = instance.GetSingle("MinDistance");
            sound.Interval = instance.GetUInt32("Interval");
            sound.IntervalVrtn = instance.GetUInt32("IntervalVrtn");
            sound.PitchVariation = instance.GetSingle("PitchVariation");

            // Sound files
            GFFList sounds;
            if (instance.TryGetList("Sounds", out sounds))
            {
                foreach (var snd in sounds)
                {
                    var soundRef = snd.GetResRef("Sound");
                    if (soundRef != null && !string.IsNullOrEmpty(soundRef.Value))
                    {
                        sound.SoundFiles.Add(soundRef.Value);
                    }
                }
            }

            entity.AddComponent(sound);

            var transform = new TransformComponent
            {
                Position = position,
                Facing = 0f
            };
            entity.AddComponent(transform);

            return entity;
        }

        #endregion

        #region Store Spawning

        /// <summary>
        /// Spawns a store from GIT instance data.
        /// </summary>
        public IEntity SpawnStore(GFFStruct instance)
        {
            if (instance == null)
            {
                return null;
            }

            var position = new Vector3(
                instance.GetSingle("XPosition"),
                instance.GetSingle("YPosition"),
                instance.GetSingle("ZPosition")
            );
            float facing = (float)Math.Atan2(
                instance.GetSingle("YOrientation"),
                instance.GetSingle("XOrientation")
            );

            var entity = _world.CreateEntity(ObjectType.Store, position, facing);
            entity.Tag = instance.GetString("Tag");

            var store = new StoreComponent();
            store.TemplateResRef = instance.GetResRef("ResRef").Value ?? string.Empty;
            store.MarkUp = instance.GetInt32("MarkUp");
            store.MarkDown = instance.GetInt32("MarkDown");
            store.OnOpenStore = instance.GetResRef("OnOpenStore").Value ?? string.Empty;

            entity.AddComponent(store);

            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent(transform);

            return entity;
        }

        #endregion

        #region Encounter Spawning

        /// <summary>
        /// Spawns an encounter from GIT instance data.
        /// </summary>
        public IEntity SpawnEncounter(GFFStruct instance)
        {
            if (instance == null)
            {
                return null;
            }

            var position = new Vector3(
                instance.GetSingle("XPosition"),
                instance.GetSingle("YPosition"),
                instance.GetSingle("ZPosition")
            );

            var entity = _world.CreateEntity(ObjectType.Encounter, position, 0f);
            entity.Tag = instance.GetString("Tag");

            var encounter = new EncounterComponent();
            encounter.TemplateResRef = instance.GetResRef("TemplateResRef").Value ?? string.Empty;
            encounter.Active = instance.GetUInt8("Active") != 0;
            encounter.Difficulty = instance.GetInt32("Difficulty");
            encounter.DifficultyIndex = instance.GetInt32("DifficultyIndex");
            encounter.MaxCreatures = instance.GetInt32("MaxCreatures");
            encounter.RecCreatures = instance.GetInt32("RecCreatures");
            encounter.Reset = instance.GetUInt8("Reset") != 0;
            encounter.ResetTime = instance.GetInt32("ResetTime");
            encounter.SpawnOption = instance.GetInt32("SpawnOption");

            // Parse geometry
            GFFList geometry;
            if (instance.TryGetList("Geometry", out geometry))
            {
                foreach (var point in geometry)
                {
                    encounter.Vertices.Add(new Vector3(
                        point.GetSingle("X"),
                        point.GetSingle("Y"),
                        point.GetSingle("Z")
                    ));
                }
            }

            // Parse spawn points
            GFFList spawnPoints;
            if (instance.TryGetList("SpawnPointList", out spawnPoints))
            {
                foreach (var sp in spawnPoints)
                {
                    encounter.SpawnPoints.Add(new EncounterSpawnPoint
                    {
                        Position = new Vector3(
                            sp.GetSingle("X"),
                            sp.GetSingle("Y"),
                            sp.GetSingle("Z")
                        ),
                        Orientation = sp.GetSingle("Orientation")
                    });
                }
            }

            // Parse creature list
            GFFList creatureList;
            if (instance.TryGetList("CreatureList", out creatureList))
            {
                foreach (var creature in creatureList)
                {
                    encounter.CreatureTemplates.Add(new EncounterCreature
                    {
                        ResRef = creature.GetResRef("ResRef").Value ?? string.Empty,
                        Appearance = creature.GetInt32("Appearance"),
                        CR = creature.GetSingle("CR"),
                        SingleSpawn = creature.GetUInt8("SingleSpawn") != 0
                    });
                }
            }

            entity.AddComponent(encounter);

            var transform = new TransformComponent
            {
                Position = position,
                Facing = 0f
            };
            entity.AddComponent(transform);

            var scripts = new ScriptHooksComponent();
            SetScript(scripts, instance, "OnEntered", ScriptEvent.OnEnter);
            SetScript(scripts, instance, "OnExhausted", ScriptEvent.OnExhausted);
            SetScript(scripts, instance, "OnExit", ScriptEvent.OnExit);
            SetScript(scripts, instance, "OnHeartbeat", ScriptEvent.OnHeartbeat);
            SetScript(scripts, instance, "OnUserDefined", ScriptEvent.OnUserDefined);
            entity.AddComponent(scripts);

            return entity;
        }

        #endregion

        #region Helper Methods

        private void SetScript(ScriptHooksComponent scripts, GFFStruct data, string fieldName, ScriptEvent eventType)
        {
            var script = data.GetResRef(fieldName);
            if (script != null && !string.IsNullOrEmpty(script.Value))
            {
                scripts.SetScript(eventType, script.Value);
            }
        }

        #endregion
    }
}
