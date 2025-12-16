using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Kotor.Components;
using JetBrains.Annotations;
using Vector3 = System.Numerics.Vector3;

namespace Odyssey.Kotor.Systems
{
    /// <summary>
    /// Initializes default components for entities based on their object type.
    /// </summary>
    /// <remarks>
    /// Component Initializer:
    /// - Based on swkotor2.exe component initialization system
    /// - Located via string references: Component system used throughout entity creation
    /// - Original implementation: Components are added during entity creation from templates
    /// - Entity creation: FUN_005fb0f0 @ 0x005fb0f0 (creature creation), FUN_004e08e0 @ 0x004e08e0 (door/placeable creation)
    /// - Component initialization: Components initialized from GFF template data (UTC, UTD, UTP, etc.)
    /// - Transform component: All entities have position/orientation (XPosition, YPosition, ZPosition, XOrientation, YOrientation)
    /// - Renderable component: Entities with visual models (creatures, doors, placeables, items) have renderable components
    /// - Type-specific components: Creatures have StatsComponent, Doors have DoorComponent, Placeables have PlaceableComponent, etc.
    /// - Script hooks component: All entities can have script hooks (ScriptHeartbeat, ScriptOnNotice, etc.)
    /// - Component attachment: Components attached during entity creation from GIT instances and templates
    /// </remarks>
    public static class ComponentInitializer
    {
        /// <summary>
        /// Initializes default components for an entity based on its object type.
        /// </summary>
        public static void InitializeComponents([NotNull] IEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            // Add TransformComponent if not present
            if (!entity.HasComponent<ITransformComponent>())
            {
                var transform = new TransformComponent();
                // Position and Facing will be set from template data during entity creation
                // Default to zero if not set from template
                transform.Position = System.Numerics.Vector3.Zero;
                transform.Facing = 0.0f;
                entity.AddComponent(transform);
            }

            // Add RenderableComponent for renderable entity types
            if (ShouldHaveRenderableComponent(entity.ObjectType))
            {
                if (!entity.HasComponent<IRenderableComponent>())
                {
                    var renderable = new RenderableComponent();
                    entity.AddComponent(renderable);
                }
            }

            // Add AnimationComponent for entities that can play animations (creatures, doors, placeables)
            if (ShouldHaveAnimationComponent(entity.ObjectType))
            {
                if (!entity.HasComponent<IAnimationComponent>())
                {
                    entity.AddComponent(new AnimationComponent());
                }
            }

            // Add type-specific components
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    if (!entity.HasComponent<CreatureComponent>())
                    {
                        entity.AddComponent(new CreatureComponent());
                    }
                    if (!entity.HasComponent<IStatsComponent>())
                    {
                        entity.AddComponent(new StatsComponent());
                    }
                    if (!entity.HasComponent<IInventoryComponent>())
                    {
                        entity.AddComponent(new InventoryComponent(entity));
                    }
                    if (!entity.HasComponent<IQuickSlotComponent>())
                    {
                        entity.AddComponent(new QuickSlotComponent(entity));
                    }
                    if (!entity.HasComponent<IFactionComponent>())
                    {
                        var factionComponent = new FactionComponent();
                        // Set FactionID from entity data if available (loaded from UTC template)
                        if (entity.GetData("FactionID") is int factionId)
                        {
                            factionComponent.FactionId = factionId;
                        }
                        entity.AddComponent(factionComponent);
                    }
                    break;

                case ObjectType.Door:
                    if (!entity.HasComponent<DoorComponent>())
                    {
                        entity.AddComponent(new DoorComponent());
                    }
                    break;

                case ObjectType.Placeable:
                    if (!entity.HasComponent<PlaceableComponent>())
                    {
                        entity.AddComponent(new PlaceableComponent());
                    }
                    break;

                case ObjectType.Trigger:
                    if (!entity.HasComponent<TriggerComponent>())
                    {
                        entity.AddComponent(new TriggerComponent());
                    }
                    break;

                case ObjectType.Waypoint:
                    if (!entity.HasComponent<WaypointComponent>())
                    {
                        entity.AddComponent(new WaypointComponent());
                    }
                    break;

                case ObjectType.Sound:
                    if (!entity.HasComponent<SoundComponent>())
                    {
                        entity.AddComponent(new SoundComponent());
                    }
                    break;

                case ObjectType.Store:
                    if (!entity.HasComponent<StoreComponent>())
                    {
                        entity.AddComponent(new StoreComponent());
                    }
                    break;

                case ObjectType.Encounter:
                    if (!entity.HasComponent<EncounterComponent>())
                    {
                        entity.AddComponent(new EncounterComponent());
                    }
                    break;

                case ObjectType.Item:
                    if (!entity.HasComponent<IItemComponent>())
                    {
                        entity.AddComponent(new ItemComponent());
                    }
                    break;
            }

            // Add ScriptHooksComponent for all entities (most entities have scripts)
            if (!entity.HasComponent<IScriptHooksComponent>())
            {
                entity.AddComponent(new ScriptHooksComponent());
            }

            // Add ActionQueueComponent for entities that can perform actions (creatures, placeables, doors)
            if (ShouldHaveActionQueue(entity.ObjectType))
            {
                if (!entity.HasComponent<IActionQueueComponent>())
                {
                    var actionQueue = new ActionQueueComponent();
                    actionQueue.Owner = entity;
                    entity.AddComponent(actionQueue);
                }
            }
        }

        /// <summary>
        /// Determines if an entity type should have an ActionQueueComponent.
        /// </summary>
        private static bool ShouldHaveActionQueue(ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.Creature:
                case ObjectType.Door:
                case ObjectType.Placeable:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines if an entity type should have a RenderableComponent.
        /// </summary>
        private static bool ShouldHaveRenderableComponent(ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.Creature:
                case ObjectType.Door:
                case ObjectType.Placeable:
                case ObjectType.Item:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines if an entity type should have an AnimationComponent.
        /// </summary>
        private static bool ShouldHaveAnimationComponent(ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.Creature:
                case ObjectType.Door:
                case ObjectType.Placeable:
                    return true;
                default:
                    return false;
            }
        }
    }
}

