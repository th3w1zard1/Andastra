using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Templates
{
    /// <summary>
    /// Interface for entity templates loaded from GFF files (UTC, UTP, UTD, etc.).
    /// Templates define the blueprint for spawning entities at runtime.
    /// </summary>
    /// <remarks>
    /// Entity Template System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) template loading system
    /// - Located via string references: Template loading from GFF files with signatures
    /// - GFF template signatures: "UTC " (Creature), "UTP " (Placeable), "UTD " (Door), "UTT " (Trigger)
    /// - "UTW " (Waypoint), "UTS " (Sound), "UTE " (Encounter), "UTI " (Item), "UTM " (Store)
    /// - Template loading: 0x004e10b0 @ 0x004e10b0 loads creature instances from GIT
    /// - 0x004e08e0 @ 0x004e08e0 loads placeable/door instances from GIT
    /// - 0x004e01a0 @ 0x004e01a0 loads encounter instances from GIT
    /// - Template ResRef: Template resource reference (e.g., "n_darthmalak" for creature template)
    /// - Template Tag: Unique identifier for script lookups (GetObjectByTag)
    /// - Original implementation: Templates define entity properties, stats, scripts, appearance
    /// - Templates loaded from module archives or override directory
    /// - Entity spawning: Creates runtime entity from template with position/facing
    /// </remarks>
    public interface IEntityTemplate
    {
        /// <summary>
        /// Gets the template resource reference.
        /// </summary>
        string ResRef { get; }

        /// <summary>
        /// Gets the template tag.
        /// </summary>
        string Tag { get; }

        /// <summary>
        /// Gets the object type this template creates.
        /// </summary>
        ObjectType ObjectType { get; }

        /// <summary>
        /// Spawns an entity from this template at the specified position.
        /// </summary>
        /// <param name="world">The world to spawn into.</param>
        /// <param name="position">The spawn position.</param>
        /// <param name="facing">The facing direction in radians.</param>
        /// <returns>The spawned entity.</returns>
        IEntity Spawn(IWorld world, Vector3 position, float facing);
    }

    /// <summary>
    /// Interface for creature templates (UTC).
    /// </summary>
    public interface ICreatureTemplate : IEntityTemplate
    {
        /// <summary>
        /// Gets the first name.
        /// </summary>
        string FirstName { get; }

        /// <summary>
        /// Gets the last name.
        /// </summary>
        string LastName { get; }

        /// <summary>
        /// Gets the appearance ID from appearances.2da.
        /// </summary>
        int AppearanceId { get; }

        /// <summary>
        /// Gets the faction ID.
        /// </summary>
        int FactionId { get; }

        /// <summary>
        /// Gets the current hit points.
        /// </summary>
        int CurrentHp { get; }

        /// <summary>
        /// Gets the maximum hit points.
        /// </summary>
        int MaxHp { get; }

        /// <summary>
        /// Gets the conversation reference.
        /// </summary>
        string Conversation { get; }

        /// <summary>
        /// Gets whether the creature is a plot character (unkillable).
        /// </summary>
        bool IsPlot { get; }
    }

    /// <summary>
    /// Interface for placeable templates (UTP).
    /// </summary>
    public interface IPlaceableTemplate : IEntityTemplate
    {
        /// <summary>
        /// Gets the display name.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the appearance ID from placeables.2da.
        /// </summary>
        int AppearanceId { get; }

        /// <summary>
        /// Gets whether the placeable is useable.
        /// </summary>
        bool IsUseable { get; }

        /// <summary>
        /// Gets whether the placeable has inventory.
        /// </summary>
        bool HasInventory { get; }

        /// <summary>
        /// Gets whether the placeable is static (no interaction).
        /// </summary>
        bool IsStatic { get; }

        /// <summary>
        /// Gets the current hit points.
        /// </summary>
        int CurrentHp { get; }

        /// <summary>
        /// Gets the maximum hit points.
        /// </summary>
        int MaxHp { get; }
    }

    /// <summary>
    /// Interface for door templates (UTD).
    /// </summary>
    public interface IDoorTemplate : IEntityTemplate
    {
        /// <summary>
        /// Gets the display name.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the generic type (door appearance).
        /// </summary>
        int AppearanceId { get; }

        /// <summary>
        /// Gets whether the door is locked.
        /// </summary>
        bool IsLocked { get; }

        /// <summary>
        /// Gets the unlock DC.
        /// </summary>
        int UnlockDc { get; }

        /// <summary>
        /// Gets whether a key is required.
        /// </summary>
        bool KeyRequired { get; }

        /// <summary>
        /// Gets the key tag.
        /// </summary>
        string KeyName { get; }

        /// <summary>
        /// Gets the current hit points.
        /// </summary>
        int CurrentHp { get; }

        /// <summary>
        /// Gets the maximum hit points.
        /// </summary>
        int MaxHp { get; }
    }

    /// <summary>
    /// Interface for trigger templates (UTT).
    /// </summary>
    public interface ITriggerTemplate : IEntityTemplate
    {
        /// <summary>
        /// Gets the trigger type.
        /// </summary>
        int TriggerType { get; }

        /// <summary>
        /// Gets the linked destination tag.
        /// </summary>
        string LinkedTo { get; }

        /// <summary>
        /// Gets the linked module resref.
        /// </summary>
        string LinkedToModule { get; }
    }

    /// <summary>
    /// Interface for waypoint templates (UTW).
    /// </summary>
    public interface IWaypointTemplate : IEntityTemplate
    {
        /// <summary>
        /// Gets the display name.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets whether this waypoint has a map note.
        /// </summary>
        bool HasMapNote { get; }

        /// <summary>
        /// Gets the map note text.
        /// </summary>
        string MapNote { get; }

        /// <summary>
        /// Gets whether the map note is visible.
        /// </summary>
        bool MapNoteEnabled { get; }
    }

    /// <summary>
    /// Interface for sound templates (UTS).
    /// </summary>
    public interface ISoundTemplate : IEntityTemplate
    {
        /// <summary>
        /// Gets the sound list to play.
        /// </summary>
        string[] Sounds { get; }

        /// <summary>
        /// Gets the volume (0-127).
        /// </summary>
        int Volume { get; }

        /// <summary>
        /// Gets whether the sound is active.
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// Gets whether the sound is positional.
        /// </summary>
        bool IsPositional { get; }

        /// <summary>
        /// Gets the minimum distance for attenuation.
        /// </summary>
        float MinDistance { get; }

        /// <summary>
        /// Gets the maximum distance for attenuation.
        /// </summary>
        float MaxDistance { get; }
    }

    /// <summary>
    /// Interface for encounter templates (UTE).
    /// </summary>
    public interface IEncounterTemplate : IEntityTemplate
    {
        /// <summary>
        /// Gets whether the encounter is active.
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// Gets the difficulty.
        /// </summary>
        int Difficulty { get; }

        /// <summary>
        /// Gets the maximum number of creatures to spawn.
        /// </summary>
        int MaxCreatures { get; }

        /// <summary>
        /// Gets whether the encounter respawns.
        /// </summary>
        bool Respawn { get; }
    }

    /// <summary>
    /// Interface for store templates (UTM).
    /// </summary>
    public interface IStoreTemplate : IEntityTemplate
    {
        /// <summary>
        /// Gets the store markup buy percent.
        /// </summary>
        int MarkupBuy { get; }

        /// <summary>
        /// Gets the store markdown sell percent.
        /// </summary>
        int MarkdownSell { get; }

        /// <summary>
        /// Gets the store identification price.
        /// </summary>
        int IdentifyPrice { get; }
    }
}
