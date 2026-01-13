using System;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Game.Games.Odyssey.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey.Systems.PerceptionManager
{
    /// <summary>
    /// Initializes default components for entities based on their object type.
    /// </summary>
    /// <remarks>
    /// Component Initializer:
    /// - Based on common component initialization logic between swkotor.exe (KOTOR 1) and swkotor2.exe (KOTOR 2)
    /// - Component initialization occurs during entity creation from GIT instances and GFF templates
    /// - All entity types receive TransformComponent for position/orientation (XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation)
    /// - Renderable entities (creatures, doors, placeables, items) receive RenderableComponent
    /// - Animated entities (creatures, doors, placeables) receive AnimationComponent
    /// - Action-capable entities (creatures, doors, placeables) receive ActionQueueComponent
    /// - All entities receive ScriptHooksComponent for script execution (ScriptHeartbeat, ScriptOnNotice, etc.)
    /// - Type-specific components are added based on ObjectType (CreatureComponent, DoorComponent, PlaceableComponent, etc.)
    ///
    /// Original Implementation (swkotor.exe):
    /// - GIT loading: [TODO: Function name] @ (K1: 0x0050dd80, TSL: 0x004e9440) - loads GIT file and creates entities from lists
    ///   - [TODO: Function name] @ (K1: 0x00504a70, TSL: 0x004dff20) - creates creatures from "Creature List"
    ///   - [TODO: Function name] @ (K1: 0x00504de0, TSL: 0x004e56b0) - creates doors from "Door List"
    ///   - [TODO: Function name] @ (K1: 0x0050a0e0, TSL: 0x004e5920) - creates placeables from "Placeable List"
    ///   - [TODO: Function name] @ (K1: 0x0050a350, TSL: 0x004e01a0) - creates triggers from "Trigger List"
    ///   - [TODO: Function name] @ (K1: 0x00505060, TSL: 0x004e04a0) - creates encounters from "Encounter List"
    ///   - [TODO: Function name] @ (K1: 0x00505360, TSL: 0x004e06a0) - creates waypoints from "Waypoint List"
    ///   - [TODO: Function name] @ (K1: 0x00505560, TSL: 0x004e08e0) - creates sounds from "Sound List"
    ///   - [TODO: Function name] @ (K1: 0x0050a7b0, TSL: 0x004e5d80) - creates stores from "Store List"
    /// - Creature template loading: Functions that load UTC templates initialize creature components
    /// - Component attachment: Components are attached during entity creation, not separately initialized
    ///
    /// - Creature template loading: [TODO: Function name] @ (K1: 0x00504a70, TSL: 0x005fb0f0) loads creature data from UTC templates and initializes components
    ///   - Called by [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x005261b0) (creature creation from template)
    ///   - Called by [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x005223a0) (creature creation from GIT)
    ///   - Called by [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x00595ca0) (player character creation)
    /// - Component attachment: Components are attached during entity creation, not separately initialized
    ///
    /// Common Patterns (both executables):
    /// - Transform component: All entities have position/orientation data loaded from GIT or templates
    /// - Renderable component: Entities with visual models (creatures, doors, placeables, items) have renderable components
    /// - Animation component: Entities that can play animations (creatures, doors, placeables) have animation components
    /// - Action queue component: Entities that can perform actions (creatures, doors, placeables) have action queue components
    /// - Script hooks component: All entities can have script hooks (ScriptHeartbeat, ScriptOnNotice, ScriptOnAttacked, etc.)
    /// - Type-specific components: Each object type has its own component (CreatureComponent, DoorComponent, PlaceableComponent, etc.)
    /// - Component initialization: Components are initialized from GFF template data (UTC for creatures, UTD for doors, UTP for placeables, etc.)
    /// </remarks>
    public static class ComponentInitializer
    {
        /// <summary>
        /// Initializes default components for an entity based on its object type.
        /// </summary>
        /// <remarks>
        /// This method implements the common component initialization logic from both swkotor.exe and swkotor2.exe.
        /// Components are added based on the entity's ObjectType, matching the behavior of the original engine.
        ///
        /// Component initialization order:
        /// 1. TransformComponent - All entities (position/orientation)
        /// 2. RenderableComponent - Entities with visual models (creatures, doors, placeables, items)
        /// 3. AnimationComponent - Entities that can play animations (creatures, doors, placeables)
        /// 4. Type-specific components - Based on ObjectType (CreatureComponent, DoorComponent, etc.)
        /// 5. ScriptHooksComponent - All entities (script execution)
        /// 6. ActionQueueComponent - Entities that can perform actions (creatures, doors, placeables)
        ///
        /// Based on:
        /// - [TODO: Function name] @ (K1: 0x0050dd80, TSL: 0x004e9440) (GIT loading)
        /// - [TODO: Function name] @ (K1: 0x00504a70, TSL: 0x005fb0f0) (creature creation)
        /// </remarks>
        /// <param name="entity">The entity to initialize components for.</param>
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
                transform.Position = Vector3.Zero;
                transform.Facing = 0.0f;
                entity.AddComponent(transform);
            }

            // Add RenderableComponent for renderable entity types
            if (ShouldHaveRenderableComponent(entity.ObjectType))
            {
                if (!entity.HasComponent<IRenderableComponent>())
                {
                    var renderable = new OdysseyRenderableComponent();
                    entity.AddComponent(renderable);
                }
            }

            // Add AnimationComponent for entities that can play animations (creatures, doors, placeables)
            if (ShouldHaveAnimationComponent(entity.ObjectType))
            {
                if (!entity.HasComponent<IAnimationComponent>())
                {
                    entity.AddComponent(new OdysseyAnimationComponent());
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
                        var factionComponent = new Andastra.Runtime.Engines.Odyssey.Components.OdysseyFactionComponent();
                        // Set FactionID from entity data if available (loaded from UTC template)
                        if (entity.GetData("FactionID") is int factionId)
                        {
                            factionComponent.FactionId = factionId;
                        }
                        entity.AddComponent(factionComponent);
                    }
                    break;

                case ObjectType.Door:
                    if (!entity.HasComponent<OdysseyDoorComponent>())
                    {
                        entity.AddComponent(new OdysseyDoorComponent());
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
                    if (!entity.HasComponent<IWaypointComponent>())
                    {
                        entity.AddComponent(new OdysseyWaypointComponent());
                    }
                    break;

                case ObjectType.Sound:
                    if (!entity.HasComponent<ISoundComponent>())
                    {
                        var soundComponent = new SoundComponent();
                        soundComponent.Owner = entity;

                        // Initialize sound component properties from entity data if available (loaded from GIT)
                        // Based on EntityFactory.CreateSoundFromGit: Sound properties are stored in entity data
                        if (entity.GetData("Active") is bool active)
                        {
                            soundComponent.Active = active;
                        }
                        if (entity.GetData("Continuous") is bool continuous)
                        {
                            soundComponent.Continuous = continuous;
                        }
                        if (entity.GetData("Looping") is bool looping)
                        {
                            soundComponent.Looping = looping;
                        }
                        if (entity.GetData("Positional") is bool positional)
                        {
                            soundComponent.Positional = positional;
                        }
                        if (entity.GetData("Random") is bool random)
                        {
                            soundComponent.Random = random;
                        }
                        if (entity.GetData("RandomPosition") is bool randomPosition)
                        {
                            soundComponent.RandomPosition = randomPosition;
                        }
                        if (entity.GetData("Volume") is int volume)
                        {
                            soundComponent.Volume = volume;
                        }
                        if (entity.GetData("VolumeVrtn") is int volumeVrtn)
                        {
                            soundComponent.VolumeVrtn = volumeVrtn;
                        }
                        if (entity.GetData("MaxDistance") is float maxDistance)
                        {
                            soundComponent.MaxDistance = maxDistance;
                        }
                        if (entity.GetData("MinDistance") is float minDistance)
                        {
                            soundComponent.MinDistance = minDistance;
                        }
                        if (entity.GetData("Sounds") is System.Collections.Generic.List<string> sounds)
                        {
                            soundComponent.SoundFiles = sounds;
                        }

                        entity.AddComponent(soundComponent);
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
                        entity.AddComponent(new OdysseyItemComponent());
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
        /// <remarks>
        /// Based on common logic from both swkotor.exe and swkotor2.exe:
        /// - Creatures can perform actions (movement, combat, item use, etc.)
        /// - Doors can perform actions (opening, closing, locking, etc.)
        /// - Placeables can perform actions (opening, closing, using, etc.)
        /// - Other entity types do not have action queues
        ///
        /// Verified against:
        /// - swkotor.exe: Action queue system for creatures, doors, placeables
        /// - swkotor2.exe: Action queue system for creatures, doors, placeables
        /// </remarks>
        /// <param name="objectType">The object type to check.</param>
        /// <returns>True if the entity type should have an ActionQueueComponent, false otherwise.</returns>
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
        /// <remarks>
        /// Based on common logic from both swkotor.exe and swkotor2.exe:
        /// - Creatures have visual models (MDL files) and are rendered in the world
        /// - Doors have visual models and are rendered when open/closed
        /// - Placeables have visual models and are rendered in the world
        /// - Items have visual models when dropped or in inventory
        /// - Other entity types (triggers, waypoints, sounds, stores, encounters) do not have visual models
        ///
        /// Verified against:
        /// - swkotor.exe: Renderable entities include creatures, doors, placeables, items
        /// - swkotor2.exe: Renderable entities include creatures, doors, placeables, items
        /// </remarks>
        /// <param name="objectType">The object type to check.</param>
        /// <returns>True if the entity type should have a RenderableComponent, false otherwise.</returns>
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
        /// <remarks>
        /// Based on common logic from both swkotor.exe and swkotor2.exe:
        /// - Creatures play animations (walking, combat, idle, etc.)
        /// - Doors play animations (opening, closing, etc.)
        /// - Placeables play animations (opening, closing, using, etc.)
        /// - Other entity types do not play animations
        ///
        /// Verified against:
        /// - swkotor.exe: Animated entities include creatures, doors, placeables
        /// - swkotor2.exe: Animated entities include creatures, doors, placeables
        /// </remarks>
        /// <param name="objectType">The object type to check.</param>
        /// <returns>True if the entity type should have an AnimationComponent, false otherwise.</returns>
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

