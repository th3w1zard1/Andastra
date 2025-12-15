using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for encounter spawner entities.
    /// </summary>
    /// <remarks>
    /// Encounter Component:
    /// - Based on swkotor2.exe encounter system
    /// - Located via string references: "Encounter" @ 0x007bc524, "Encounter List" @ 0x007bd050
    /// - Original implementation: FUN_004e01a0 @ 0x004e01a0 (load encounter instances from GIT)
    /// - Encounters spawn creatures when hostile creatures enter encounter polygon area
    /// - UTE file format: GFF with "UTE " signature containing encounter data
    /// - Encounters have polygon geometry defining spawn area (Geometry field), creature templates (SpawnPointList)
    /// - SpawnOption 0 = on enter, 1 = on reset, 2 = continuous spawn
    /// - FUN_004e01a0 reads XPosition, YPosition, ZPosition, Geometry, SpawnPointList from GFF
    /// - Based on UTE file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class EncounterComponent : IComponent
    {
        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public EncounterComponent()
        {
            TemplateResRef = string.Empty;
            Vertices = new List<Vector3>();
            SpawnPoints = new List<EncounterSpawnPoint>();
            CreatureTemplates = new List<EncounterCreature>();
            SpawnedCreatures = new List<uint>();
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Whether the encounter is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Difficulty index.
        /// </summary>
        public int Difficulty { get; set; }

        /// <summary>
        /// Difficulty type index.
        /// </summary>
        public int DifficultyIndex { get; set; }

        /// <summary>
        /// Maximum creatures to spawn.
        /// </summary>
        public int MaxCreatures { get; set; }

        /// <summary>
        /// Recommended number of creatures.
        /// </summary>
        public int RecCreatures { get; set; }

        /// <summary>
        /// Whether encounter resets.
        /// </summary>
        public bool Reset { get; set; }

        /// <summary>
        /// Reset time in seconds.
        /// </summary>
        public int ResetTime { get; set; }

        /// <summary>
        /// Spawn option (0 = on enter, 1 = on reset, 2 = continuous).
        /// </summary>
        public int SpawnOption { get; set; }

        /// <summary>
        /// Player only trigger.
        /// </summary>
        public bool PlayerOnly { get; set; }

        /// <summary>
        /// Faction to assign spawned creatures.
        /// </summary>
        public int Faction { get; set; }

        /// <summary>
        /// Encounter geometry vertices.
        /// </summary>
        public List<Vector3> Vertices { get; set; }

        /// <summary>
        /// Spawn point locations.
        /// </summary>
        public List<EncounterSpawnPoint> SpawnPoints { get; set; }

        /// <summary>
        /// Creature templates to spawn.
        /// </summary>
        public List<EncounterCreature> CreatureTemplates { get; set; }

        /// <summary>
        /// IDs of currently spawned creatures.
        /// </summary>
        public List<uint> SpawnedCreatures { get; set; }

        /// <summary>
        /// Whether encounter has spawned.
        /// </summary>
        public bool HasSpawned { get; set; }

        /// <summary>
        /// Time since spawn for reset timing.
        /// </summary>
        public float TimeSinceSpawn { get; set; }

        /// <summary>
        /// Whether encounter is exhausted (spawned max creatures).
        /// </summary>
        public bool IsExhausted { get; set; }

        /// <summary>
        /// Tests if a point is inside the encounter area.
        /// </summary>
        public bool ContainsPoint(Vector3 point)
        {
            if (Vertices.Count < 3)
            {
                return false;
            }

            // Ray casting algorithm for point-in-polygon test
            int crossings = 0;
            for (int i = 0; i < Vertices.Count; i++)
            {
                int j = (i + 1) % Vertices.Count;
                Vector3 v1 = Vertices[i];
                Vector3 v2 = Vertices[j];

                if ((v1.Y <= point.Y && v2.Y > point.Y) ||
                    (v2.Y <= point.Y && v1.Y > point.Y))
                {
                    float x = v1.X + (point.Y - v1.Y) / (v2.Y - v1.Y) * (v2.X - v1.X);
                    if (point.X < x)
                    {
                        crossings++;
                    }
                }
            }

            return (crossings % 2) == 1;
        }
    }

    /// <summary>
    /// Spawn point for encounter creatures.
    /// </summary>
    public class EncounterSpawnPoint
    {
        /// <summary>
        /// Spawn position.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Spawn orientation (radians).
        /// </summary>
        public float Orientation { get; set; }
    }

    /// <summary>
    /// Creature template for encounters.
    /// </summary>
    public class EncounterCreature
    {
        public EncounterCreature()
        {
            ResRef = string.Empty;
        }

        /// <summary>
        /// Creature template resource reference.
        /// </summary>
        public string ResRef { get; set; }

        /// <summary>
        /// Appearance override.
        /// </summary>
        public int Appearance { get; set; }

        /// <summary>
        /// Challenge rating.
        /// </summary>
        public float CR { get; set; }

        /// <summary>
        /// Whether creature spawns only once.
        /// </summary>
        public bool SingleSpawn { get; set; }
    }
}
