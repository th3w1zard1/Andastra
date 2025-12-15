using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Templates
{
    /// <summary>
    /// Encounter template implementation for spawning encounters from UTE data.
    /// </summary>
    /// <remarks>
    /// Encounter Template:
    /// - Based on swkotor2.exe encounter system
    /// - Located via string references: "Encounter" @ 0x007bc524, "Encounter List" @ 0x007bd050
    /// - Template loading: FUN_005226d0 @ 0x005226d0 (entity serialization references encounter templates)
    /// - Original implementation: UTE (Encounter) GFF templates define encounter properties
    /// - UTE file format: GFF with "UTE " signature containing encounter data
    /// - Encounters spawn creatures when hostile creatures enter encounter polygon area
    /// - Encounters have polygon geometry defining spawn area, creature templates, spawn options
    /// - SpawnOption 0 = continuous spawn, 1 = single-shot spawn
    /// - Based on UTE file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class EncounterTemplate : IEncounterTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Encounter; } }
        public bool Active { get; set; }
        public int Difficulty { get; set; }
        public int MaxCreatures { get; set; }
        public bool Respawn { get; set; }

        // Additional properties
        public string DisplayName { get; set; }
        public int FactionId { get; set; }
        public int RecreateTime { get; set; }
        public int SpawnOption { get; set; }
        public int ResetTime { get; set; }
        public bool PlayerOnly { get; set; }

        // Creatures to spawn
        public List<EncounterCreature> CreatureList { get; set; }

        // Geometry for encounter area
        public List<Vector3> Geometry { get; set; }

        // Spawn points
        public List<SpawnPoint> SpawnPoints { get; set; }

        // Script hooks
        public string OnEntered { get; set; }
        public string OnExhausted { get; set; }
        public string OnExit { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnUserDefined { get; set; }

        #endregion

        public EncounterTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            DisplayName = string.Empty;
            Active = true;
            CreatureList = new List<EncounterCreature>();
            Geometry = new List<Vector3>();
            SpawnPoints = new List<SpawnPoint>();

            OnEntered = string.Empty;
            OnExhausted = string.Empty;
            OnExit = string.Empty;
            OnHeartbeat = string.Empty;
            OnUserDefined = string.Empty;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var entity = new Entity(ObjectType.Encounter, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = position;
                transform.Facing = facing;
            }

            // Apply script hooks
            Interfaces.Components.IScriptHooksComponent scripts = entity.GetComponent<Interfaces.Components.IScriptHooksComponent>();
            if (scripts != null)
            {
                if (!string.IsNullOrEmpty(OnEntered))
                    scripts.SetScript(ScriptEvent.OnEnter, OnEntered);
                if (!string.IsNullOrEmpty(OnExit))
                    scripts.SetScript(ScriptEvent.OnExit, OnExit);
                if (!string.IsNullOrEmpty(OnHeartbeat))
                    scripts.SetScript(ScriptEvent.OnHeartbeat, OnHeartbeat);
                if (!string.IsNullOrEmpty(OnUserDefined))
                    scripts.SetScript(ScriptEvent.OnUserDefined, OnUserDefined);
            }

            // Register in world
            world.RegisterEntity(entity);

            return entity;
        }
    }

    /// <summary>
    /// Creature entry in an encounter.
    /// </summary>
    public class EncounterCreature
    {
        public string ResRef { get; set; }
        public int Appearance { get; set; }
        public float ChallengeRating { get; set; }
        public bool SingleSpawn { get; set; }

        public EncounterCreature()
        {
            ResRef = string.Empty;
        }
    }

    /// <summary>
    /// Spawn point in an encounter.
    /// </summary>
    public class SpawnPoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Orientation { get; set; }
    }
}
