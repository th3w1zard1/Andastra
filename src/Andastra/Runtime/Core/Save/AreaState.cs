using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Save
{
    /// <summary>
    /// Saved state for an area.
    /// </summary>
    /// <remarks>
    /// Area State:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) area save system
    /// - Located via string references: Area state serialization in save system
    /// - Original implementation: Area state stored in [module]_s.rim files within savegame.sav ERF archive
    /// - FUN_005226d0 @ 0x005226d0 saves entity states to GFF format
    /// - This tracks changes from the base GIT data:
    ///   - Entity positions (XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation)
    ///   - Door open/locked states (OpenState, IsLocked)
    ///   - Placeable open/locked states
    ///   - Destroyed/removed entities (marked for removal)
    ///   - Spawned entities (not in original GIT, dynamically created)
    /// - Area states stored per-area in save file, loaded when area is entered
    /// </remarks>
    public class AreaState
    {
        /// <summary>
        /// Area ResRef.
        /// </summary>
        public string AreaResRef { get; set; }

        /// <summary>
        /// Whether this area has been visited.
        /// </summary>
        public bool Visited { get; set; }

        /// <summary>
        /// Creature states.
        /// </summary>
        public List<EntityState> CreatureStates { get; set; }

        /// <summary>
        /// Door states.
        /// </summary>
        public List<EntityState> DoorStates { get; set; }

        /// <summary>
        /// Placeable states.
        /// </summary>
        public List<EntityState> PlaceableStates { get; set; }

        /// <summary>
        /// Trigger states.
        /// </summary>
        public List<EntityState> TriggerStates { get; set; }

        /// <summary>
        /// Store states.
        /// </summary>
        public List<EntityState> StoreStates { get; set; }

        /// <summary>
        /// Sound states.
        /// </summary>
        public List<EntityState> SoundStates { get; set; }

        /// <summary>
        /// Waypoint states.
        /// </summary>
        public List<EntityState> WaypointStates { get; set; }

        /// <summary>
        /// Encounter states.
        /// </summary>
        public List<EntityState> EncounterStates { get; set; }

        /// <summary>
        /// Camera states.
        /// </summary>
        public List<EntityState> CameraStates { get; set; }

        /// <summary>
        /// IDs of entities that have been destroyed/removed.
        /// </summary>
        public List<uint> DestroyedEntityIds { get; set; }

        /// <summary>
        /// Dynamically spawned entities not in original GIT.
        /// </summary>
        public List<SpawnedEntityState> SpawnedEntities { get; set; }

        /// <summary>
        /// Local area variables.
        /// </summary>
        public LocalVariableSet LocalVariables { get; set; }

        public AreaState()
        {
            CreatureStates = new List<EntityState>();
            DoorStates = new List<EntityState>();
            PlaceableStates = new List<EntityState>();
            TriggerStates = new List<EntityState>();
            StoreStates = new List<EntityState>();
            SoundStates = new List<EntityState>();
            WaypointStates = new List<EntityState>();
            EncounterStates = new List<EntityState>();
            CameraStates = new List<EntityState>();
            DestroyedEntityIds = new List<uint>();
            SpawnedEntities = new List<SpawnedEntityState>();
            LocalVariables = new LocalVariableSet();
        }
    }

    /// <summary>
    /// Base entity state for saving.
    /// </summary>
    public class EntityState
    {
        /// <summary>
        /// Entity tag.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Object ID (for matching to GIT instances).
        /// </summary>
        public uint ObjectId { get; set; }

        /// <summary>
        /// Object type.
        /// </summary>
        public ObjectType ObjectType { get; set; }

        /// <summary>
        /// Template ResRef.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Current position.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Current facing.
        /// </summary>
        public float Facing { get; set; }

        /// <summary>
        /// Current HP.
        /// </summary>
        public int CurrentHP { get; set; }

        /// <summary>
        /// Maximum HP.
        /// </summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// Whether destroyed.
        /// </summary>
        public bool IsDestroyed { get; set; }

        /// <summary>
        /// Whether plot flagged.
        /// </summary>
        public bool IsPlot { get; set; }

        /// <summary>
        /// Open state (doors, placeables).
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Locked state (doors, placeables).
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Animation state (0=closed, 1=open, 2=destroyed).
        /// </summary>
        public int AnimationState { get; set; }

        /// <summary>
        /// Local object variables.
        /// </summary>
        public LocalVariableSet LocalVariables { get; set; }

        /// <summary>
        /// Active effects on this entity.
        /// </summary>
        public List<SavedEffect> ActiveEffects { get; set; }

        public EntityState()
        {
            LocalVariables = new LocalVariableSet();
            ActiveEffects = new List<SavedEffect>();
        }
    }

    /// <summary>
    /// State for a dynamically spawned entity.
    /// </summary>
    public class SpawnedEntityState : EntityState
    {
        /// <summary>
        /// Blueprint ResRef used to spawn.
        /// </summary>
        public string BlueprintResRef { get; set; }

        /// <summary>
        /// Script that spawned this entity (for debugging).
        /// </summary>
        public string SpawnedBy { get; set; }
    }

    /// <summary>
    /// Local variable storage.
    /// </summary>
    public class LocalVariableSet
    {
        /// <summary>
        /// Integer variables.
        /// </summary>
        public Dictionary<string, int> Ints { get; set; }

        /// <summary>
        /// Float variables.
        /// </summary>
        public Dictionary<string, float> Floats { get; set; }

        /// <summary>
        /// String variables.
        /// </summary>
        public Dictionary<string, string> Strings { get; set; }

        /// <summary>
        /// Object reference variables.
        /// </summary>
        public Dictionary<string, uint> Objects { get; set; }

        /// <summary>
        /// Location variables.
        /// </summary>
        public Dictionary<string, SavedLocation> Locations { get; set; }

        public LocalVariableSet()
        {
            Ints = new Dictionary<string, int>();
            Floats = new Dictionary<string, float>();
            Strings = new Dictionary<string, string>();
            Objects = new Dictionary<string, uint>();
            Locations = new Dictionary<string, SavedLocation>();
        }

        public bool IsEmpty
        {
            get
            {
                return Ints.Count == 0
                    && Floats.Count == 0
                    && Strings.Count == 0
                    && Objects.Count == 0
                    && Locations.Count == 0;
            }
        }
    }

    /// <summary>
    /// Saved effect (buff/debuff).
    /// </summary>
    public class SavedEffect
    {
        /// <summary>
        /// Effect type ID.
        /// </summary>
        public int EffectType { get; set; }

        /// <summary>
        /// Effect subtype.
        /// </summary>
        public int SubType { get; set; }

        /// <summary>
        /// Duration type.
        /// </summary>
        public int DurationType { get; set; }

        /// <summary>
        /// Remaining duration (rounds or seconds).
        /// </summary>
        public float RemainingDuration { get; set; }

        /// <summary>
        /// Creator object ID.
        /// </summary>
        public uint CreatorId { get; set; }

        /// <summary>
        /// Spell ID that created this effect.
        /// </summary>
        public int SpellId { get; set; }

        /// <summary>
        /// Effect parameters.
        /// </summary>
        public List<int> IntParams { get; set; }

        /// <summary>
        /// Float parameters.
        /// </summary>
        public List<float> FloatParams { get; set; }

        /// <summary>
        /// String parameters.
        /// </summary>
        public List<string> StringParams { get; set; }

        /// <summary>
        /// Object reference parameters.
        /// </summary>
        public List<uint> ObjectParams { get; set; }

        public SavedEffect()
        {
            IntParams = new List<int>();
            FloatParams = new List<float>();
            StringParams = new List<string>();
            ObjectParams = new List<uint>();
        }
    }
}
