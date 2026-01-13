namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Base interface for all entity components.
    /// </summary>
    /// <remarks>
    /// Component Interface:
    /// Common component-based architecture shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity).
    ///
    /// Common structure across all engines:
    /// - Owner (IEntity): The entity this component is attached to
    /// - OnAttach(): Lifecycle hook called when component is attached to an entity
    /// - OnDetach(): Lifecycle hook called when component is detached from an entity
    /// - Component system allows flexible entity composition without inheritance hierarchies
    /// - Components provide specific functionality (Transform, Stats, Inventory, ScriptHooks, Faction, Perception, etc.)
    /// - Components are attached/detached via entity's AddComponent/RemoveComponent methods
    /// - Component state is serialized/deserialized as part of entity save/load operations
    ///
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe, swkotor2.exe): Component serialization via GFF format
    ///   - swkotor.exe: Component save/load functions (exact addresses to be determined via reverse engineering)
    ///   - swkotor2.exe: 0x005226d0 @ 0x005226d0 saves entity components to GFF, 0x005223a0 @ 0x005223a0 loads entity components from GFF
    /// - Aurora (nwmain.exe, nwn2main.exe): Component serialization via GFF format
    ///   - nwmain.exe: SaveCreature @ 0x1403a0a60, LoadCreatures @ 0x140360570 (component save/load)
    ///   - Component system similar to Odyssey with CExoString-based string handling
    /// - Eclipse (daorigins.exe, DragonAge2.exe): Enhanced component system
    ///   - Component serialization format similar to Odyssey/Aurora (exact addresses to be determined via reverse engineering)
    ///   - Enhanced component interactions and dependencies
    /// - Infinity (, ): Streamlined component system
    ///   - Component serialization format similar to other engines (exact addresses to be determined via reverse engineering)
    ///   - Streamlined component architecture for performance
    ///
    /// Base component implementations:
    /// - Common components inherit from base classes in Runtime.Games.Common.Components
    /// - Base classes contain only functionality identical across ALL engines
    /// - Engine-specific subclasses override or extend base functionality as needed
    /// - Component types: Transform, Stats, Inventory, ScriptHooks, Faction, Perception, Animation, etc.
    ///
    /// All engine-specific details (function addresses, serialization formats, implementation specifics) are in base component classes or engine-specific subclasses.
    /// This interface defines only the common contract shared across all engines.
    /// </remarks>
    public interface IComponent
    {
        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Reference to the owning entity.
        /// Set automatically when component is attached via entity's AddComponent method.
        /// Cleared when component is detached via entity's RemoveComponent method.
        /// </remarks>
        IEntity Owner { get; set; }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Lifecycle hook called after component is attached to an entity.
        /// Use this to initialize component state, register event handlers, or perform setup operations.
        /// Called automatically by entity's AddComponent method after setting Owner property.
        /// </remarks>
        void OnAttach();

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Lifecycle hook called before component is detached from an entity.
        /// Use this to clean up component state, unregister event handlers, or perform teardown operations.
        /// Called automatically by entity's RemoveComponent method before clearing Owner property.
        /// </remarks>
        void OnDetach();
    }
}

