using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse engine-specific door component implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Door Component:
    /// - Inherits from BaseDoorComponent for common door functionality
    /// - Eclipse-specific: May use different door systems or may not support doors
    /// - Based on daorigins.exe and DragonAge2.exe (Dragon Age: Origins, Dragon Age II)
    /// - Note: Eclipse engines may not have traditional door systems like Odyssey/Aurora
    /// - If doors are supported, they would use Eclipse-specific file formats and systems
    /// - Original implementation: Needs reverse engineering from daorigins.exe and DragonAge2.exe
    /// </remarks>
    public class EclipseDoorComponent : BaseDoorComponent
    {
        /// <summary>
        /// Linked flags for transitions (Eclipse-specific implementation, if supported).
        /// </summary>
        public int LinkedToFlags { get; set; }

        /// <summary>
        /// Whether this door is a module transition.
        /// </summary>
        /// <remarks>
        /// Module Transition Check:
        /// - Eclipse engines may not support module transitions via doors
        /// - If supported, implementation would be Eclipse-specific
        /// - Original implementation: Needs reverse engineering from daorigins.exe and DragonAge2.exe
        /// </remarks>
        public override bool IsModuleTransition
        {
            get { return (LinkedToFlags & 1) != 0 && !string.IsNullOrEmpty(LinkedToModule); }
        }

        /// <summary>
        /// Whether this door is an area transition.
        /// </summary>
        /// <remarks>
        /// Area Transition Check:
        /// - Eclipse engines may not support area transitions via doors
        /// - If supported, implementation would be Eclipse-specific
        /// - Original implementation: Needs reverse engineering from daorigins.exe and DragonAge2.exe
        /// </remarks>
        public override bool IsAreaTransition
        {
            get { return (LinkedToFlags & 2) != 0 && !string.IsNullOrEmpty(LinkedTo); }
        }
    }
}

