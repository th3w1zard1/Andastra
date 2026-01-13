using System;

namespace Andastra.Runtime.Core.Enums
{
    /// <summary>
    /// Types of game objects in the Odyssey engine.
    /// </summary>
    /// <remarks>
    /// Object Type Enum:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) object type system
    /// - Located via string references: ObjectType used throughout entity system for type checking
    /// - Object type strings: "Creature" @ 0x007bc4e0, "Door" @ 0x007bc4f4, "Placeable" @ 0x007bc508
    /// - "Trigger" @ 0x007bc51c, "Item" @ 0x007bc530, "Waypoint" @ 0x007bc544, "Sound" @ 0x007bc558
    /// - "Store" @ 0x007bc56c, "Encounter" @ 0x007bc524, "Module" @ 0x007c1a70
    /// - GFF template types: "UTC" (Creature), "UTD" (Door), "UTP" (Placeable), "UTT" (Trigger), "UTW" (Waypoint)
    /// - "UTS" (Sound), "UTE" (Encounter), "UTI" (Item), "UTM" (Store)
    /// - Original implementation: Objects have type flags (Creature, Item, Door, Placeable, etc.)
    /// - Flags enum allows combination (e.g., ObjectType.All = all types)
    /// - Used for: Entity filtering, type checking, GFF template loading (UTC, UTD, UTP, etc.)
    /// - Object types correspond to GFF template types: UTC (Creature), UTD (Door), UTP (Placeable), etc.
    /// - Object type checking: FUN_005226d0 @ 0x005226d0 uses object type for entity serialization
    /// - Entity creation: FUN_004e10b0 @ 0x004e10b0 creates entities by type from GIT instances
    /// </remarks>
    [Flags]
    public enum ObjectType
    {
        Invalid = 0,
        Creature = 1,
        Item = 2,
        Trigger = 4,
        Door = 8,
        AreaOfEffect = 16,
        Waypoint = 32,
        Placeable = 64,
        Store = 128,
        Encounter = 256,
        Sound = 512,
        All = Creature | Item | Trigger | Door | AreaOfEffect | Waypoint | Placeable | Store | Encounter | Sound
    }
}

