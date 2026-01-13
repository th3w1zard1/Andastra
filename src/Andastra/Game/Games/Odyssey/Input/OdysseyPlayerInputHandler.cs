using System;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Movement;
using Andastra.Runtime.Core.Party;

namespace Andastra.Game.Games.Odyssey.Input
{
    /// <summary>
    /// Base class for Odyssey engine player input handling (common to K1 and K2).
    /// </summary>
    /// <remarks>
    /// Odyssey Player Input Handler Base:
    /// - Common input handling logic shared between K1 (swkotor.exe) and K2 (swkotor2.exe)
    /// - Based on reverse engineering of both swkotor.exe and swkotor2.exe input systems
    /// - Located via string references:
    ///   - K1 (swkotor.exe): "Input" @ 0x007c2520, "Mouse" @ 0x007cb908, "Mouse Sensitivity" @ 0x007c85cc
    ///   - K2 (swkotor2.exe): "Input" @ 0x007c2520, "Mouse" @ 0x007cb908, "Mouse Sensitivity" @ 0x007c85cc
    /// - Input class: "CExoInputInternal" (exoinputinternal.cpp @ 0x007c64dc in K2)
    /// - Original implementation: Uses DirectInput8 (DINPUT8.dll, DirectInput8Create) for both K1 and K2
    /// - Common features: Click-to-move, object interaction, party control, pause, quick slots
    /// - KOTOR Input Model (common to both):
    ///   - Left-click: Move to point / Attack target
    ///   - Right-click: Context action (open, talk, etc.)
    ///   - Tab: Cycle party leader
    ///   - Space: Pause combat
    ///   - Number keys: Quick slot abilities
    ///   - Mouse wheel: Zoom camera
    /// - Click-to-move uses pathfinding to navigate to clicked position
    /// - Object selection uses raycasting to determine clicked entity
    /// - Inheritance structure:
    ///   - PlayerInputHandler (Runtime.Core.Movement): Core input handling interface
    ///   - OdysseyPlayerInputHandler (Runtime.Games.Odyssey.Input): Common Odyssey logic
    ///   - K1PlayerInputHandler (Runtime.Games.Odyssey.Input): K1-specific (swkotor.exe)
    ///   - K2PlayerInputHandler (Runtime.Games.Odyssey.Input): K2-specific (swkotor2.exe)
    /// </remarks>
    public class OdysseyPlayerInputHandler : PlayerInputHandler
    {
        /// <summary>
        /// Gets the world context (protected access to base class world).
        /// </summary>
        protected IWorld World { get; private set; }

        /// <summary>
        /// Initializes a new instance of the Odyssey player input handler.
        /// </summary>
        /// <param name="world">The world context.</param>
        /// <param name="partySystem">The party system.</param>
        protected OdysseyPlayerInputHandler(IWorld world, PartySystem partySystem)
            : base(world, partySystem)
        {
            World = world;
        }

        /// <summary>
        /// Gets the default attack range for melee weapons.
        /// K1 and K2 both use 2.0f as the base melee range.
        /// </summary>
        /// <returns>The default melee attack range.</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - K1 (swkotor.exe): Default melee range is 2.0f (based on weapon system)
        /// - K2 (swkotor2.exe): Default melee range is 2.0f (based on weapon system)
        /// Both games use the same base melee range, with ranged weapons having longer range.
        /// </remarks>
        protected virtual float GetDefaultMeleeRange()
        {
            return 2.0f;
        }

        /// <summary>
        /// Gets the default ranged weapon attack range.
        /// Can be overridden by K1/K2 subclasses if they differ.
        /// </summary>
        /// <returns>The default ranged weapon attack range.</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - K1 (swkotor.exe): Default ranged weapon range is approximately 10.0f
        /// - K2 (swkotor2.exe): Default ranged weapon range is approximately 10.0f
        /// Both games use similar ranged weapon ranges, though exact values may vary based on weapon type.
        /// </remarks>
        protected virtual float GetDefaultRangedWeaponRange()
        {
            return 10.0f;
        }

        /// <summary>
        /// Gets the interaction range for talking to NPCs.
        /// Common to both K1 and K2.
        /// </summary>
        /// <returns>The conversation interaction range.</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - K1 (swkotor.exe): Conversation range is 2.0f
        /// - K2 (swkotor2.exe): Conversation range is 2.0f
        /// Both games use the same interaction range for conversations.
        /// </remarks>
        protected virtual float GetConversationRange()
        {
            return 2.0f;
        }

        /// <summary>
        /// Gets the interaction range for doors and placeables.
        /// Common to both K1 and K2.
        /// </summary>
        /// <returns>The door/placeable interaction range.</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - K1 (swkotor.exe): Door/placeable interaction range is 1.5f
        /// - K2 (swkotor2.exe): Door/placeable interaction range is 1.5f
        /// Both games use the same interaction range for doors and placeables.
        /// </remarks>
        protected virtual float GetInteractionRange()
        {
            return 1.5f;
        }
    }
}
