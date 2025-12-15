using System;

namespace Odyssey.Core.Enums
{
    /// <summary>
    /// Types of game objects in the Odyssey engine.
    /// </summary>
    /// <remarks>
    /// Object Type Enum:
    /// - Based on swkotor2.exe object type system
    /// - Located via string references: ObjectType used throughout entity system for type checking
    /// - Original implementation: Objects have type flags (Creature, Item, Door, Placeable, etc.)
    /// - Flags enum allows combination (e.g., ObjectType.All = all types)
    /// - Used for: Entity filtering, type checking, GFF template loading (UTC, UTD, UTP, etc.)
    /// - Object types correspond to GFF template types: UTC (Creature), UTD (Door), UTP (Placeable), etc.
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

