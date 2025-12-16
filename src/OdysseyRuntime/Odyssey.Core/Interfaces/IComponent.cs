namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Base interface for all entity components.
    /// </summary>
    /// <remarks>
    /// Component Interface:
    /// - Based on swkotor2.exe component-based entity system
    /// - Located via string references: Component system used throughout entity management
    /// - Original implementation: Entities use component-based architecture for modular functionality
    /// - Components attached to entities provide specific functionality (Transform, Stats, Inventory, etc.)
    /// - Components have lifecycle hooks: OnAttach (when added to entity), OnDetach (when removed)
    /// - Component system allows flexible entity composition without inheritance hierarchies
    /// - Entity component management: FUN_005226d0 @ 0x005226d0 saves entity components to GFF, FUN_005223a0 @ 0x005223a0 loads entity components from GFF
    /// - Component types: Transform, Stats, Inventory, ScriptHooks, Faction, Perception, etc.
    /// </remarks>
    public interface IComponent
    {
        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        IEntity Owner { get; set; }
        
        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        void OnAttach();
        
        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        void OnDetach();
    }
}

