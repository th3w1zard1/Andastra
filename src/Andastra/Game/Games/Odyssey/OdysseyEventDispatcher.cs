using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Module;
using Andastra.Game.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey
{
    /// <summary>
    /// Base Odyssey Engine event dispatcher implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Event Dispatcher Implementation:
    /// - Common event dispatching functionality for both KOTOR 1 and KOTOR 2
    /// - Maps event IDs to string names for debugging
    /// - Routes events to appropriate handlers based on type
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Event dispatching functions (KOTOR 1)
    /// - swkotor2.exe: DispatchEvent @ 0x004dcfb0 (KOTOR 2)
    /// - Event types: AREA_TRANSITION (0x1a), REMOVE_FROM_AREA (4), etc. (common across both games)
    /// - Script events: ON_HEARTBEAT (0), ON_PERCEPTION (1), etc. (common across both games)
    ///
    /// Event system features:
    /// - Immediate event dispatching
    /// - Queued event processing for script safety
    /// - Event logging and debugging support
    /// - Script hook integration
    ///
    /// Game-specific implementations:
    /// - Kotor1EventDispatcher: KOTOR 1 (swkotor.exe) specific event dispatcher
    /// - Kotor2EventDispatcher: KOTOR 2 (swkotor2.exe) specific event dispatcher
    /// </remarks>
    [PublicAPI]
    public abstract class OdysseyEventDispatcher : BaseEventDispatcher
    {
        private readonly Queue<PendingEvent> _eventQueue = new Queue<PendingEvent>();
        private readonly ILoadingScreen _loadingScreen;
        private readonly Andastra.Runtime.Engines.Odyssey.Loading.ModuleLoader _moduleLoader;

        private struct PendingEvent
        {
            public IEntity SourceEntity;
            public IEntity TargetEntity;
            public int EventType;
            public int EventSubtype;
        }

        /// <summary>
        /// Initializes a new instance of the OdysseyEventDispatcher.
        /// </summary>
        /// <param name="loadingScreen">Optional loading screen for area transitions. If provided, area transitions will display the transition bitmap.</param>
        /// <param name="moduleLoader">Optional module loader for area streaming. If provided, areas will be loaded on-demand during transitions.</param>
        protected OdysseyEventDispatcher(ILoadingScreen loadingScreen = null, Andastra.Runtime.Engines.Odyssey.Loading.ModuleLoader moduleLoader = null)
        {
            _loadingScreen = loadingScreen;
            _moduleLoader = moduleLoader;
        }

        /// <summary>
        /// Dispatches an event immediately.
        /// </summary>
        /// <remarks>
        /// Based on DispatchEvent @ 0x004dcfb0 in swkotor2.exe.
        /// Maps event IDs and routes to appropriate handlers.
        /// May queue events for later processing if needed.
        /// </remarks>
        public override void DispatchEvent(IEntity sourceEntity, IEntity targetEntity, int eventType, int eventSubtype)
        {
            // Log event for debugging
            var eventName = GetEventName(eventType);
            var subtypeName = GetEventSubtypeName(eventSubtype);

            // Log event dispatch with entity information
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DispatchEvent @ 0x004dcfb0 debug format "DRF Event Added: %s(%s) %s(%s) %s %s\n"
            string sourceInfo = sourceEntity != null ? $"{sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId})" : "null";
            string targetInfo = targetEntity != null ? $"{targetEntity.Tag ?? "null"} ({targetEntity.ObjectId})" : "null";
            Console.WriteLine($"[OdysseyEventDispatcher] Dispatching event: {eventName} ({eventType}) from {sourceInfo} to {targetInfo}, subtype: {subtypeName} ({eventSubtype})");

            // Route to appropriate handler based on event type
            switch (eventType)
            {
                case 0x1a: // EVENT_AREA_TRANSITION
                    // Extract target area from sourceEntity (door/trigger) component
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DispatchEvent @ 0x004dcfb0 handles EVENT_AREA_TRANSITION (case 0x1a)
                    // Source entity is the door/trigger that triggered the transition
                    // Target entity is the entity being transitioned (usually player/party)
                    string targetAreaResRef = ExtractTargetAreaFromTransitionSource(sourceEntity, targetEntity);
                    HandleAreaTransition(targetEntity, targetAreaResRef, sourceEntity);
                    break;

                case 4: // EVENT_REMOVE_FROM_AREA (swkotor2.exe: 0x004dcfb0 line 48)
                    // Area removal - entity is being removed from current area
                    // For removal, we don't need a target area, just remove from current
                    HandleAreaTransition(targetEntity, null);
                    break;

                case 6: // EVENT_CLOSE_OBJECT (swkotor2.exe: 0x004dcfb0 line 54)
                case 7: // EVENT_OPEN_OBJECT (swkotor2.exe: 0x004dcfb0 line 57)
                case 0xc: // EVENT_UNLOCK_OBJECT (swkotor2.exe: 0x004dcfb0 line 72)
                case 0xd: // EVENT_LOCK_OBJECT (swkotor2.exe: 0x004dcfb0 line 75)
                    HandleObjectEvent(targetEntity, eventType);
                    break;

                case 0xa: // EVENT_SIGNAL_EVENT (swkotor2.exe: 0x004dcfb0 line 66) - script events, uses eventSubtype
                    if (targetEntity != null)
                    {
                        // eventSubtype 4 = CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED (swkotor2.exe: 0x004dcfb0 line 137)
                        if (eventSubtype == 4)
                        {
                            HandleCombatEvent(targetEntity, eventType, sourceEntity);
                        }
                        else
                        {
                            HandleScriptEvent(targetEntity, eventType, eventSubtype, sourceEntity);
                        }
                    }
                    break;

                case 0xb: // EVENT_DESTROY_OBJECT (swkotor2.exe: 0x004dcfb0 line 69)
                case 0xf: // EVENT_ON_MELEE_ATTACKED (swkotor2.exe: 0x004dcfb0 line 81)
                    if (targetEntity != null)
                        HandleCombatEvent(targetEntity, eventType, sourceEntity);
                    break;

                default:
                    // Unknown event type
                    break;
            }
        }

        /// <summary>
        /// Gets the string name for an event type.
        /// </summary>
        /// <remarks>
        /// Based on event name mapping in DispatchEvent @ 0x004dcfb0.
        /// Returns descriptive names for known events.
        /// </remarks>
        protected override string GetEventName(int eventType)
        {
            switch (eventType)
            {
                case 1: return "EVENT_TIMED_EVENT";
                case 2: return "EVENT_ENTERED_TRIGGER";
                case 3: return "EVENT_LEFT_TRIGGER";
                case 4: return "EVENT_REMOVE_FROM_AREA"; // or EVENT_ON_DAMAGED in different context
                case 5: return "EVENT_APPLY_EFFECT";
                case 6: return "EVENT_CLOSE_OBJECT";
                case 7: return "EVENT_OPEN_OBJECT";
                case 8: return "EVENT_SPELL_IMPACT";
                case 9: return "EVENT_PLAY_ANIMATION";
                case 10: return "EVENT_SIGNAL_EVENT"; // or EVENT_DESTROY_OBJECT in different context
                case 0xb: return "EVENT_DESTROY_OBJECT";
                case 0xc: return "EVENT_UNLOCK_OBJECT";
                case 0xd: return "EVENT_LOCK_OBJECT";
                case 0xe: return "EVENT_REMOVE_EFFECT";
                case 0xf: return "EVENT_ON_MELEE_ATTACKED";
                case 0x10: return "EVENT_DECREMENT_STACKSIZE";
                case 0x11: return "EVENT_SPAWN_BODY_BAG";
                case 0x12: return "EVENT_FORCED_ACTION";
                case 0x13: return "EVENT_ITEM_ON_HIT_SPELL_IMPACT";
                case 0x14: return "EVENT_BROADCAST_AOO";
                case 0x15: return "EVENT_BROADCAST_SAFE_PROJECTILE";
                case 0x16: return "EVENT_FEEDBACK_MESSAGE";
                case 0x17: return "EVENT_ABILITY_EFFECT_APPLIED";
                case 0x18: return "EVENT_SUMMON_CREATURE";
                case 0x19: return "EVENT_ACQUIRE_ITEM";
                case 0x1a: return "EVENT_AREA_TRANSITION";
                case 0x1b: return "EVENT_CONTROLLER_RUMBLE";
                default: return $"Event({eventType})";
            }
        }

        /// <summary>
        /// Gets the string name for an event subtype.
        /// </summary>
        /// <remarks>
        /// Based on script event subtype mapping in DispatchEvent.
        /// Used for SIGNAL_EVENT subtypes.
        /// </remarks>
        protected override string GetEventSubtypeName(int eventSubtype)
        {
            switch (eventSubtype)
            {
                case 0: return "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT";
                case 1: return "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION";
                case 2: return "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT";
                case 4: return "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED";
                case 5: return "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED";
                case 7: return "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE";
                case 8: return "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN";
                case 9: return "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED";
                default: return $"EventType({eventSubtype})";
            }
        }

        /// <summary>
        /// Handles area transition events.
        /// </summary>
        /// <param name="entity">The entity being transitioned.</param>
        /// <param name="targetArea">The target area ResRef.</param>
        /// <param name="sourceEntity">Optional source entity (door/trigger) that triggered the transition. Used to resolve LinkedTo waypoint positioning.</param>
        /// <remarks>
        /// Based on EVENT_AREA_TRANSITION (0x1a) and EVENT_REMOVE_FROM_AREA (4) handling in swkotor2.exe: DispatchEvent @ 0x004dcfb0.
        /// Manages entity movement between areas.
        /// Updates area membership and triggers transition effects.
        ///
        /// Transition flow (based on swkotor2.exe area transition system):
        /// 1. Validate entity and world reference
        /// 2. Get current area from entity's AreaId or world's CurrentArea
        /// 3. If targetArea is null (EVENT_REMOVE_FROM_AREA), remove entity from current area only
        /// 4. If targetArea is specified (EVENT_AREA_TRANSITION), perform full transition:
        ///    a. Remove entity from current area
        ///    b. Load target area if not already loaded (area streaming)
        ///    c. Resolve target waypoint position if LinkedTo field specifies a waypoint
        ///    d. Project entity position to target area walkmesh
        ///    e. Add entity to target area
        ///    f. Update entity's AreaId
        ///    g. Fire OnEnter events for target area
        /// </remarks>
        protected override void HandleAreaTransition(IEntity entity, string targetArea, IEntity sourceEntity = null)
        {
            if (entity == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] HandleAreaTransition: Entity is null, aborting area transition");
                return;
            }

            IWorld world = entity.World;
            if (world == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] HandleAreaTransition: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) has no world, aborting area transition");
                return;
            }

            // Get current area from entity's AreaId or world's CurrentArea
            IArea currentArea = GetCurrentAreaForEntity(entity, world);
            string currentAreaName = currentArea != null ? currentArea.ResRef : "null";

            // If targetArea is null, this is an area removal event (EVENT_REMOVE_FROM_AREA)
            if (string.IsNullOrEmpty(targetArea))
            {
                // Remove entity from current area only
                Console.WriteLine($"[OdysseyEventDispatcher] HandleAreaTransition: Removing entity {entity.Tag ?? "null"} ({entity.ObjectId}) from area {currentAreaName}");
                if (currentArea != null)
                {
                    RemoveEntityFromArea(currentArea, entity);
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleAreaTransition: Successfully removed entity {entity.Tag ?? "null"} ({entity.ObjectId}) from area {currentAreaName}");
                }
                return;
            }

            // Full area transition (EVENT_AREA_TRANSITION)
            Console.WriteLine($"[OdysseyEventDispatcher] HandleAreaTransition: Transitioning entity {entity.Tag ?? "null"} ({entity.ObjectId}) from area {currentAreaName} to area {targetArea}");
            // Load target area if not already loaded (area streaming)
            IArea targetAreaInstance = LoadOrGetTargetArea(world, targetArea);
            if (targetAreaInstance == null)
            {
                // Failed to load target area - transition failed
                // Entity remains in current area
                Console.WriteLine($"[OdysseyEventDispatcher] HandleAreaTransition: Failed to load target area {targetArea}, entity {entity.Tag ?? "null"} ({entity.ObjectId}) remains in area {currentAreaName}");
                return;
            }

            // If target area is different from current, perform full transition
            if (targetAreaInstance != currentArea)
            {
                // Check for stored area transition bitmap on player entity
                // Based on swkotor.exe: SetAreaTransitionBMP stores bitmap on player entity
                // Original implementation: Area transition bitmap is displayed during area transitions
                string transitionBitmap = GetAreaTransitionBitmap(entity);

                // Show loading screen with transition bitmap if available
                if (!string.IsNullOrEmpty(transitionBitmap) && _loadingScreen != null)
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleAreaTransition: Showing area transition bitmap: {transitionBitmap}");
                    _loadingScreen.Show(transitionBitmap);
                }

                // Remove entity from current area first
                if (currentArea != null)
                {
                    RemoveEntityFromArea(currentArea, entity);
                }

                // Resolve target waypoint position if source entity has LinkedTo field
                // This positions the entity at the waypoint specified by the door/trigger
                ResolveAndSetTransitionPosition(entity, world, targetAreaInstance, sourceEntity);

                // Project entity position to target area walkmesh
                ProjectEntityToTargetArea(entity, targetAreaInstance);

                // Add entity to target area
                AddEntityToTargetArea(targetAreaInstance, entity);

                // Update entity's AreaId
                uint targetAreaId = world.GetAreaId(targetAreaInstance);
                if (targetAreaId != 0)
                {
                    entity.AreaId = targetAreaId;
                }

                // Fire transition events (OnEnter script for target area)
                FireAreaTransitionEvents(world, entity, targetAreaInstance);

                // Hide loading screen after transition completes
                if (!string.IsNullOrEmpty(transitionBitmap) && _loadingScreen != null)
                {
                    _loadingScreen.Hide();
                }

                Console.WriteLine($"[OdysseyEventDispatcher] HandleAreaTransition: Successfully transitioned entity {entity.Tag ?? "null"} ({entity.ObjectId}) to area {targetAreaInstance.ResRef}");
            }
            else
            {
                Console.WriteLine($"[OdysseyEventDispatcher] HandleAreaTransition: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is already in target area {targetArea}, no transition needed");
            }
        }

        /// <summary>
        /// Public method to trigger area transition for an entity.
        /// </summary>
        /// <param name="entity">Entity to transition.</param>
        /// <param name="targetAreaResRef">Target area ResRef.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): HandleAreaTransition processes entity movement between areas.
        /// This public wrapper allows areas and other systems to trigger transitions directly.
        /// </remarks>
        public void TransitionEntityToArea(IEntity entity, string targetAreaResRef)
        {
            HandleAreaTransition(entity, targetAreaResRef);
        }

        /// <summary>
        /// Gets the area transition bitmap stored on the entity.
        /// Based on swkotor.exe: SetAreaTransitionBMP stores bitmap on player entity
        /// Original implementation: Bitmap is retrieved from entity data during area transitions
        /// </summary>
        /// <param name="entity">Entity to get transition bitmap from (usually player entity)</param>
        /// <returns>Bitmap ResRef or null if not set</returns>
        private string GetAreaTransitionBitmap(IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            // Check if entity has stored area transition bitmap
            // Key: "AreaTransitionBitmap" - set by SetAreaTransitionBMP function
            if (entity.HasData("AreaTransitionBitmap"))
            {
                string bitmap = entity.GetData<string>("AreaTransitionBitmap", null);
                return bitmap;
            }

            return null;
        }

        /// <summary>
        /// Extracts target area from transition source entity (door/trigger).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Door/trigger transition system
        /// Located via string references: "LinkedTo" @ 0x007bd798, "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788
        /// Original implementation: Doors/triggers with LinkedTo field trigger area transitions
        /// LinkedTo contains waypoint tag - waypoint's AreaId determines target area
        /// For area transitions within module: LinkedToFlags bit 2 = area transition flag
        /// For module transitions: LinkedToFlags bit 1 = module transition flag (handled by ModuleTransitionSystem)
        /// </remarks>
        private string ExtractTargetAreaFromTransitionSource(IEntity sourceEntity, IEntity targetEntity)
        {
            if (sourceEntity == null || targetEntity == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Source or target entity is null");
                return null;
            }

            IWorld world = targetEntity.World;
            if (world == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Target entity {targetEntity.Tag ?? "null"} ({targetEntity.ObjectId}) has no world");
                return null;
            }

            // Check if source entity is a door with area transition
            IDoorComponent doorComponent = sourceEntity.GetComponent<IDoorComponent>();
            if (doorComponent != null && doorComponent.IsAreaTransition)
            {
                // Area transition: LinkedTo contains waypoint tag
                string linkedToTag = doorComponent.LinkedTo;
                Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Door {sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId}) has area transition, LinkedTo: {linkedToTag ?? "null"}");
                if (!string.IsNullOrEmpty(linkedToTag))
                {
                    // Find waypoint entity by tag
                    IEntity waypointEntity = world.GetEntityByTag(linkedToTag, 0);
                    if (waypointEntity != null && waypointEntity.AreaId != 0)
                    {
                        // Get area from waypoint's AreaId
                        IArea targetArea = world.GetArea(waypointEntity.AreaId);
                        if (targetArea != null)
                        {
                            Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Found target area {targetArea.ResRef} from door {sourceEntity.Tag ?? "null"} via waypoint {linkedToTag}");
                            return targetArea.ResRef;
                        }
                        else
                        {
                            Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Waypoint {linkedToTag} has AreaId {waypointEntity.AreaId} but area not found in world");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Waypoint {linkedToTag} not found or has invalid AreaId");
                    }
                }
            }

            // Check if source entity is a trigger with area transition
            ITriggerComponent triggerComponent = sourceEntity.GetComponent<ITriggerComponent>();
            if (triggerComponent != null && triggerComponent.IsAreaTransition)
            {
                // Area transition: LinkedTo contains waypoint tag
                string linkedToTag = triggerComponent.LinkedTo;
                Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Trigger {sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId}) has area transition, LinkedTo: {linkedToTag ?? "null"}");
                if (!string.IsNullOrEmpty(linkedToTag))
                {
                    // Find waypoint entity by tag
                    IEntity waypointEntity = world.GetEntityByTag(linkedToTag, 0);
                    if (waypointEntity != null && waypointEntity.AreaId != 0)
                    {
                        // Get area from waypoint's AreaId
                        IArea targetArea = world.GetArea(waypointEntity.AreaId);
                        if (targetArea != null)
                        {
                            Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Found target area {targetArea.ResRef} from trigger {sourceEntity.Tag ?? "null"} via waypoint {linkedToTag}");
                            return targetArea.ResRef;
                        }
                        else
                        {
                            Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Waypoint {linkedToTag} has AreaId {waypointEntity.AreaId} but area not found in world");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: Waypoint {linkedToTag} not found or has invalid AreaId");
                    }
                }
            }

            // No valid transition source found
            Console.WriteLine($"[OdysseyEventDispatcher] ExtractTargetAreaFromTransitionSource: No valid area transition found for source entity {sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId})");
            return null;
        }

        /// <summary>
        /// Gets the current area for an entity.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity area tracking system
        /// Checks entity's AreaId first, then falls back to world's CurrentArea.
        /// </remarks>
        private IArea GetCurrentAreaForEntity(IEntity entity, IWorld world)
        {
            if (entity == null || world == null)
            {
                return null;
            }

            // Try to get area from entity's AreaId
            if (entity.AreaId != 0)
            {
                IArea area = world.GetArea(entity.AreaId);
                if (area != null)
                {
                    return area;
                }
            }

            // Fall back to world's current area
            return world.CurrentArea;
        }

        /// <summary>
        /// Loads or gets the target area for transition.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area streaming system (swkotor2.exe: LoadAreaProperties @ 0x004e26d0)
        /// Checks if area is already loaded in current module, otherwise loads it via ModuleLoader.
        ///
        /// Area streaming flow (based on swkotor2.exe area transition system):
        /// 1. Check if area is already loaded in current module (via IModule.GetArea)
        /// 2. Check if area is the current area
        /// 3. If not found and ModuleLoader is available:
        ///    a. Get current module name from world.CurrentModule.ResRef
        ///    b. Create Module instance from module name and Installation
        ///    c. Load area using ModuleLoader.LoadArea(module, areaResRef)
        ///    d. Add loaded area to module's Areas collection
        ///    e. Register area with world (assign AreaId)
        ///    f. Return loaded area
        /// 4. If ModuleLoader is not available, return null (area streaming disabled)
        ///
        /// Based on reverse engineering of:
        /// - swkotor2.exe: Area loading during transitions (FUN_004e26d0 @ 0x004e26d0)
        /// - swkotor.exe: Similar area loading system (KOTOR 1)
        /// - Area resources: ARE (properties), GIT (instances), LYT (layout), VIS (visibility)
        /// - Module resource lookup: Areas are loaded from module archives using area ResRef
        /// </remarks>
        private IArea LoadOrGetTargetArea(IWorld world, string targetAreaResRef)
        {
            if (world == null || string.IsNullOrEmpty(targetAreaResRef))
            {
                Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: World is null or target area ResRef is empty");
                return null;
            }

            // First, check if area is already loaded in current module
            if (world.CurrentModule != null)
            {
                // Check if target area is the current area
                if (world.CurrentArea != null && string.Equals(world.CurrentArea.ResRef, targetAreaResRef, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Target area {targetAreaResRef} is already the current area");
                    return world.CurrentArea;
                }

                // Check if area is already loaded in module
                IArea existingArea = world.CurrentModule.GetArea(targetAreaResRef);
                if (existingArea != null)
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Target area {targetAreaResRef} is already loaded in module");
                    return existingArea;
                }
            }

            // Area is not loaded - attempt to load it via ModuleLoader (area streaming)
            if (_moduleLoader == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Target area {targetAreaResRef} is not loaded and ModuleLoader is not available (area streaming disabled)");
                return null;
            }

            if (world.CurrentModule == null || string.IsNullOrEmpty(world.CurrentModule.ResRef))
            {
                Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Cannot load area {targetAreaResRef} - no current module loaded");
                return null;
            }

            // Get current module name for area loading
            string moduleName = world.CurrentModule.ResRef;
            Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Loading area {targetAreaResRef} from module {moduleName} via ModuleLoader");

            try
            {
                // Create Module instance for resource access
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module objects are created per module for resource lookups
                // Module instance provides access to ARE, GIT, LYT, VIS files for areas
                BioWare.NET.Extract.Installation.Installation installation = _moduleLoader.GetInstallation();
                if (installation == null)
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Cannot load area {targetAreaResRef} - ModuleLoader has no Installation");
                    return null;
                }

                var module = new BioWare.NET.Extract.Installation.Module(moduleName, installation);

                // Load area using ModuleLoader
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): LoadAreaProperties @ 0x004e26d0 loads ARE + GIT + LYT + VIS
                // ModuleLoader.LoadArea creates RuntimeArea with all area resources
                RuntimeArea loadedArea = _moduleLoader.LoadArea(module, targetAreaResRef);
                if (loadedArea == null)
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Failed to load area {targetAreaResRef} from module {moduleName}");
                    return null;
                }

                Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Successfully loaded area {targetAreaResRef} from module {moduleName}");

                // Add loaded area to module's Areas collection
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Areas are stored in module's area list for lookup
                if (world.CurrentModule is RuntimeModule runtimeModule)
                {
                    runtimeModule.AddArea(loadedArea);
                    Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Added area {targetAreaResRef} to module {moduleName}");
                }

                // Register area with world (assign AreaId for entity lookup)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Areas are registered with AreaId for GetArea() lookups
                // World.RegisterArea assigns AreaId and stores area for efficient lookup
                world.RegisterArea(loadedArea);
                uint areaId = world.GetAreaId(loadedArea);
                Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Registered area {targetAreaResRef} with world (AreaId: {areaId})");

                return loadedArea;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Exception loading area {targetAreaResRef} from module {moduleName}: {ex.Message}");
                Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Resolves and sets the transition position from source entity's LinkedTo field.
        /// </summary>
        /// <param name="entity">The entity being transitioned.</param>
        /// <param name="world">The world containing the areas.</param>
        /// <param name="targetArea">The target area for the transition.</param>
        /// <param name="sourceEntity">Optional source entity (door/trigger) that triggered the transition.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Transition positioning system (FUN_004e26d0 @ 0x004e26d0)
        /// Located via string references: "LinkedTo" @ 0x007bd7a4 (waypoint tag for positioning after transition)
        /// Original implementation: If source entity (door/trigger) has LinkedTo field, positions entity at that waypoint.
        /// Otherwise, entity position is preserved and projected to target area walkmesh in ProjectEntityToTargetArea.
        ///
        /// Transition positioning logic:
        /// 1. If sourceEntity is null, preserve current position (will be projected to walkmesh later)
        /// 2. Check if sourceEntity is a door with LinkedTo field
        /// 3. Check if sourceEntity is a trigger with LinkedTo field
        /// 4. If LinkedTo is specified, find waypoint with that tag in target area
        /// 5. If waypoint found, position entity at waypoint's position and facing
        /// 6. If waypoint not found or LinkedTo is empty, preserve current position
        /// </remarks>
        private void ResolveAndSetTransitionPosition(IEntity entity, IWorld world, IArea targetArea, IEntity sourceEntity)
        {
            if (entity == null || world == null || targetArea == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Entity, world, or target area is null");
                return;
            }

            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) has no transform component");
                return;
            }

            // If no source entity provided, preserve current position (will be projected to walkmesh later)
            if (sourceEntity == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: No source entity provided, preserving current position for entity {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            string linkedToTag = null;

            // Check if source entity is a door with LinkedTo field
            IDoorComponent doorComponent = sourceEntity.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                linkedToTag = doorComponent.LinkedTo;
                if (!string.IsNullOrEmpty(linkedToTag))
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Door {sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId}) has LinkedTo: {linkedToTag}");
                }
            }

            // Check if source entity is a trigger with LinkedTo field (if door didn't have it)
            if (string.IsNullOrEmpty(linkedToTag))
            {
                ITriggerComponent triggerComponent = sourceEntity.GetComponent<ITriggerComponent>();
                if (triggerComponent != null)
                {
                    linkedToTag = triggerComponent.LinkedTo;
                    if (!string.IsNullOrEmpty(linkedToTag))
                    {
                        Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Trigger {sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId}) has LinkedTo: {linkedToTag}");
                    }
                }
            }

            // If no LinkedTo field, preserve current position (will be projected to walkmesh later)
            if (string.IsNullOrEmpty(linkedToTag))
            {
                Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Source entity {sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId}) has no LinkedTo field, preserving current position for entity {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Find waypoint with LinkedTo tag in target area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetWaypointByTag searches for waypoint by tag in area
            // Located via string references: "GetWaypointByTag" function searches waypoints by tag
            IEntity waypoint = targetArea.GetObjectByTag(linkedToTag, 0);
            if (waypoint == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Waypoint with tag '{linkedToTag}' not found in target area {targetArea.ResRef}, preserving current position for entity {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Verify it's actually a waypoint
            if (waypoint.ObjectType != Core.Enums.ObjectType.Waypoint)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Entity with tag '{linkedToTag}' in target area {targetArea.ResRef} is not a waypoint (type: {waypoint.ObjectType}), preserving current position for entity {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Get waypoint's transform component
            ITransformComponent waypointTransform = waypoint.GetComponent<ITransformComponent>();
            if (waypointTransform == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Waypoint {linkedToTag} has no transform component, preserving current position for entity {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Position entity at waypoint's position and facing
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Transition positioning sets entity position to waypoint position
            // Original implementation: FUN_004e26d0 @ 0x004e26d0 positions entity at waypoint after transition
            Vector3 waypointPosition = waypointTransform.Position;
            float waypointFacing = waypointTransform.Facing;

            transform.Position = waypointPosition;
            transform.Facing = waypointFacing;

            Console.WriteLine($"[OdysseyEventDispatcher] ResolveAndSetTransitionPosition: Positioned entity {entity.Tag ?? "null"} ({entity.ObjectId}) at waypoint '{linkedToTag}' position ({waypointPosition.X:F2}, {waypointPosition.Y:F2}, {waypointPosition.Z:F2}), facing: {waypointFacing:F2} radians");
        }

        /// <summary>
        /// Projects an entity's position to the target area's walkmesh.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Walkmesh projection system (FUN_004f5070 @ 0x004f5070)
        /// Projects position to walkable surface for accurate positioning.
        /// </remarks>
        private void ProjectEntityToTargetArea(IEntity entity, IArea targetArea)
        {
            if (entity == null || targetArea == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] ProjectEntityToTargetArea: Entity or target area is null");
                return;
            }

            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] ProjectEntityToTargetArea: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) has no transform component");
                return;
            }

            // Project position to target area walkmesh
            Vector3 currentPosition = transform.Position;
            Console.WriteLine($"[OdysseyEventDispatcher] ProjectEntityToTargetArea: Projecting entity {entity.Tag ?? "null"} ({entity.ObjectId}) position ({currentPosition.X:F2}, {currentPosition.Y:F2}, {currentPosition.Z:F2}) to walkmesh of area {targetArea.ResRef}");
            Vector3 projectedPosition;
            float height;

            if (targetArea.ProjectToWalkmesh(currentPosition, out projectedPosition, out height))
            {
                transform.Position = projectedPosition;
                Console.WriteLine($"[OdysseyEventDispatcher] ProjectEntityToTargetArea: Successfully projected entity {entity.Tag ?? "null"} ({entity.ObjectId}) to position ({projectedPosition.X:F2}, {projectedPosition.Y:F2}, {projectedPosition.Z:F2}), height: {height:F2}");
            }
            else
            {
                // If projection fails, try to find a valid position near the transition point
                Console.WriteLine($"[OdysseyEventDispatcher] ProjectEntityToTargetArea: Direct projection failed, searching for nearest walkable point");
                Vector3 nearestWalkable = FindNearestWalkablePoint(targetArea, currentPosition);
                transform.Position = nearestWalkable;
                Console.WriteLine($"[OdysseyEventDispatcher] ProjectEntityToTargetArea: Using nearest walkable point ({nearestWalkable.X:F2}, {nearestWalkable.Y:F2}, {nearestWalkable.Z:F2}) for entity {entity.Tag ?? "null"} ({entity.ObjectId})");
            }
        }

        /// <summary>
        /// Finds the nearest walkable point in an area.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Walkmesh search system
        /// Searches in expanding radius when direct projection fails.
        /// </remarks>
        private Vector3 FindNearestWalkablePoint(IArea area, Vector3 searchPoint)
        {
            if (area == null || area.NavigationMesh == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] FindNearestWalkablePoint: Area is null or has no navigation mesh, returning original point");
                return searchPoint;
            }

            Console.WriteLine($"[OdysseyEventDispatcher] FindNearestWalkablePoint: Searching for walkable point near ({searchPoint.X:F2}, {searchPoint.Y:F2}, {searchPoint.Z:F2}) in area {area.ResRef}");
            // Try to project the point first
            Vector3 projected;
            float height;
            if (area.ProjectToWalkmesh(searchPoint, out projected, out height))
            {
                Console.WriteLine($"[OdysseyEventDispatcher] FindNearestWalkablePoint: Found walkable point at search location ({projected.X:F2}, {projected.Y:F2}, {projected.Z:F2})");
                return projected;
            }

            // If projection fails, search in expanding radius
            const float searchRadius = 5.0f;
            const float stepSize = 1.0f;
            const int maxSteps = 10;
            Console.WriteLine($"[OdysseyEventDispatcher] FindNearestWalkablePoint: Initial projection failed, searching in expanding radius (max {maxSteps * stepSize:F1} units)");

            for (int step = 1; step <= maxSteps; step++)
            {
                float radius = step * stepSize;
                for (int angle = 0; angle < 360; angle += 45)
                {
                    float radians = (float)(angle * Math.PI / 180.0);
                    Vector3 testPoint = searchPoint + new Vector3(
                        (float)Math.Cos(radians) * radius,
                        0,
                        (float)Math.Sin(radians) * radius
                    );

                    if (area.ProjectToWalkmesh(testPoint, out projected, out height))
                    {
                        Console.WriteLine($"[OdysseyEventDispatcher] FindNearestWalkablePoint: Found walkable point at radius {radius:F1}, angle {angle}° ({projected.X:F2}, {projected.Y:F2}, {projected.Z:F2})");
                        return projected;
                    }
                }
            }

            // Fallback: return original point
            Console.WriteLine($"[OdysseyEventDispatcher] FindNearestWalkablePoint: No walkable point found within search radius, returning original point ({searchPoint.X:F2}, {searchPoint.Y:F2}, {searchPoint.Z:F2})");
            return searchPoint;
        }

        /// <summary>
        /// Removes an entity from an area.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity removal from area
        /// Calls area's RemoveEntityFromArea method.
        /// </remarks>
        private void RemoveEntityFromArea(IArea area, IEntity entity)
        {
            if (area == null || entity == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] RemoveEntityFromArea: Area or entity is null");
                return;
            }

            Console.WriteLine($"[OdysseyEventDispatcher] RemoveEntityFromArea: Removing entity {entity.Tag ?? "null"} ({entity.ObjectId}) from area {area.ResRef}");
            // Call area's RemoveEntityFromArea method
            // This is implemented in BaseArea and engine-specific subclasses
            if (area is BaseArea baseArea)
            {
                baseArea.RemoveEntityFromArea(entity);
                Console.WriteLine($"[OdysseyEventDispatcher] RemoveEntityFromArea: Successfully removed entity {entity.Tag ?? "null"} ({entity.ObjectId}) from area {area.ResRef}");
            }
            else
            {
                Console.WriteLine($"[OdysseyEventDispatcher] RemoveEntityFromArea: Area {area.ResRef} is not a BaseArea, cannot remove entity");
            }
        }

        /// <summary>
        /// Adds an entity to a target area.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity addition to area
        /// Calls area's AddEntityToArea method.
        /// </remarks>
        private void AddEntityToTargetArea(IArea targetArea, IEntity entity)
        {
            if (targetArea == null || entity == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] AddEntityToTargetArea: Target area or entity is null");
                return;
            }

            Console.WriteLine($"[OdysseyEventDispatcher] AddEntityToTargetArea: Adding entity {entity.Tag ?? "null"} ({entity.ObjectId}) to area {targetArea.ResRef}");
            // Call area's AddEntityToArea method
            // This is implemented in BaseArea and engine-specific subclasses
            if (targetArea is BaseArea baseArea)
            {
                baseArea.AddEntityToArea(entity);
                Console.WriteLine($"[OdysseyEventDispatcher] AddEntityToTargetArea: Successfully added entity {entity.Tag ?? "null"} ({entity.ObjectId}) to area {targetArea.ResRef}");
            }
            else
            {
                Console.WriteLine($"[OdysseyEventDispatcher] AddEntityToTargetArea: Target area {targetArea.ResRef} is not a BaseArea, cannot add entity");
            }
        }

        /// <summary>
        /// Fires area transition events.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area transition event system
        /// Fires OnEnter script events for target area.
        /// </remarks>
        private void FireAreaTransitionEvents(IWorld world, IEntity entity, IArea targetArea)
        {
            if (world == null || world.EventBus == null || entity == null || targetArea == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] FireAreaTransitionEvents: World, EventBus, entity, or target area is null");
                return;
            }

            Console.WriteLine($"[OdysseyEventDispatcher] FireAreaTransitionEvents: Firing OnEnter script for entity {entity.Tag ?? "null"} ({entity.ObjectId}) entering area {targetArea.ResRef}");
            // Fire OnEnter script for target area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area enter script execution
            // Located via string references: "OnEnter" @ 0x007bee60 (area enter script)
            // Original implementation: Fires when entity enters an area
            if (targetArea is Core.Module.RuntimeArea targetRuntimeArea)
            {
                string enterScript = targetRuntimeArea.GetScript(Core.Enums.ScriptEvent.OnEnter);
                if (!string.IsNullOrEmpty(enterScript))
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] FireAreaTransitionEvents: Area {targetArea.ResRef} has OnEnter script: {enterScript}");
                    IEntity areaEntity = world.GetEntityByTag(targetArea.ResRef, 0);
                    if (areaEntity == null)
                    {
                        areaEntity = world.GetEntityByTag(targetArea.Tag, 0);
                    }
                    if (areaEntity != null)
                    {
                        Console.WriteLine($"[OdysseyEventDispatcher] FireAreaTransitionEvents: Firing OnEnter script {enterScript} on area entity {areaEntity.Tag ?? "null"} ({areaEntity.ObjectId})");
                        world.EventBus.FireScriptEvent(areaEntity, Core.Enums.ScriptEvent.OnEnter, entity);
                    }
                    else
                    {
                        Console.WriteLine($"[OdysseyEventDispatcher] FireAreaTransitionEvents: Could not find area entity for {targetArea.ResRef} or {targetArea.Tag}");
                    }
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] FireAreaTransitionEvents: Area {targetArea.ResRef} has no OnEnter script");
                }
            }
            else
            {
                Console.WriteLine($"[OdysseyEventDispatcher] FireAreaTransitionEvents: Target area {targetArea.ResRef} is not a RuntimeArea, cannot fire OnEnter script");
            }
        }

        /// <summary>
        /// Handles object manipulation events.
        /// </summary>
        /// <remarks>
        /// Based on object events: OPEN_OBJECT (7), CLOSE_OBJECT (6), LOCK_OBJECT (0xd), UNLOCK_OBJECT (0xc).
        /// Updates object state and triggers associated scripts.
        /// Handles visual/audio feedback for state changes.
        ///
        /// Implementation based on swkotor2.exe: DispatchEvent @ 0x004dcfb0
        /// - EVENT_OPEN_OBJECT (case 7): Opens door/placeable, fires OnOpen script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN = 0x16)
        /// - EVENT_CLOSE_OBJECT (case 6): Closes door/placeable, fires OnClose script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE = 0x17)
        /// - EVENT_LOCK_OBJECT (case 0xd): Locks door/placeable, fires OnLock script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED = 0x1c)
        /// - EVENT_UNLOCK_OBJECT (case 0xc): Unlocks door/placeable, fires OnUnlock script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED = 0x1d)
        ///
        /// Located via string references:
        /// - "EVENT_OPEN_OBJECT" @ 0x007bcda0 (swkotor2.exe, case 7)
        /// - "EVENT_CLOSE_OBJECT" @ 0x007bcdb4 (swkotor2.exe, case 6)
        /// - "EVENT_LOCK_OBJECT" @ 0x007bcd20 (swkotor2.exe, case 0xd)
        /// - "EVENT_UNLOCK_OBJECT" @ 0x007bcd34 (swkotor2.exe, case 0xc)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844 (swkotor2.exe, 0x16)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820 (swkotor2.exe, 0x17)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (swkotor2.exe, 0x1c)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc72c (swkotor2.exe, 0x1d)
        ///
        /// Object event handling flow (based on swkotor2.exe: DispatchEvent @ 0x004dcfb0):
        /// 1. EVENT_OPEN_OBJECT (7): Opens door/placeable
        ///    - Sets IsOpen=true, OpenState/AnimationState=1 (open)
        ///    - Fires OnOpen script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN = 0x16)
        ///    - Script fires after state is updated
        /// 2. EVENT_CLOSE_OBJECT (6): Closes door/placeable
        ///    - Sets IsOpen=false, OpenState/AnimationState=0 (closed)
        ///    - Fires OnClose script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE = 0x17)
        ///    - Script fires after state is updated
        /// 3. EVENT_LOCK_OBJECT (0xd): Locks door/placeable
        ///    - Sets IsLocked=true (only if Lockable flag is true)
        ///    - Fires OnLock script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED = 0x1c)
        ///    - Script fires after state is updated
        /// 4. EVENT_UNLOCK_OBJECT (0xc): Unlocks door/placeable
        ///    - Sets IsLocked=false
        ///    - Fires OnUnlock script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED = 0x1d)
        ///    - Script fires after state is updated
        ///
        /// Script execution:
        /// - Scripts are retrieved from entity's IScriptHooksComponent for the appropriate ScriptEvent
        /// - Scripts are executed via world's EventBus.FireScriptEvent, which queues and processes script execution
        /// - Source entity (from DispatchEvent call) is passed as triggerer to script execution context
        /// - Scripts can use GetEnteringObject(), GetClickingObject() to retrieve the entity that triggered the event
        ///
        /// Visual representation:
        /// - OpenState (doors): 0=closed, 1=open, 2=destroyed
        /// - AnimationState (placeables): 0=closed, 1=open
        /// - Rendering system updates visual representation based on OpenState/AnimationState changes
        /// - Animation system plays appropriate animations (open/close door animations, container open/close animations)
        ///
        /// Audio feedback:
        /// - Door open/close sounds are handled by audio system based on OpenState changes
        /// - Lock/unlock sounds are handled by audio system based on IsLocked changes
        /// </remarks>
        protected override void HandleObjectEvent(IEntity entity, int eventType)
        {
            if (entity == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] HandleObjectEvent: Entity is null, aborting object event handling");
                return;
            }

            // Get world and event bus
            IWorld world = entity.World;
            if (world == null || world.EventBus == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] HandleObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) has no world or EventBus");
                return;
            }

            string eventName = GetEventName(eventType);
            string entityInfo = $"{entity.Tag ?? "null"} ({entity.ObjectId})";
            Console.WriteLine($"[OdysseyEventDispatcher] HandleObjectEvent: Handling {eventName} ({eventType}) for entity {entityInfo}");

            // Handle different object event types
            switch (eventType)
            {
                case 7: // EVENT_OPEN_OBJECT (swkotor2.exe: 0x004dcfb0 case 7, line 66)
                    HandleOpenObjectEvent(entity, world);
                    break;

                case 6: // EVENT_CLOSE_OBJECT (swkotor2.exe: 0x004dcfb0 case 6, line 63)
                    HandleCloseObjectEvent(entity, world);
                    break;

                case 0xd: // EVENT_LOCK_OBJECT (swkotor2.exe: 0x004dcfb0 case 0xd, line 84)
                    HandleLockObjectEvent(entity, world);
                    break;

                case 0xc: // EVENT_UNLOCK_OBJECT (swkotor2.exe: 0x004dcfb0 case 0xc, line 81)
                    HandleUnlockObjectEvent(entity, world);
                    break;

                default:
                    // Unknown object event type - log and ignore
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleObjectEvent: Unknown object event type {eventType} ({eventName}) for entity {entityInfo}");
                    break;
            }
        }

        /// <summary>
        /// Handles EVENT_OPEN_OBJECT (7) event.
        /// </summary>
        /// <param name="entity">The entity to open (door or placeable).</param>
        /// <param name="world">The world containing the entity.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_OPEN_OBJECT opens door/placeable and fires OnOpen script
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0 case 7, line 66)
        /// </remarks>
        private void HandleOpenObjectEvent(IEntity entity, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleOpenObjectEvent: Opening entity {entity.Tag ?? "null"} ({entity.ObjectId})");

            // Check if entity has door component
            IDoorComponent doorComponent = entity.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                // Open the door
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_OPEN_OBJECT sets IsOpen=true, OpenState=1
                // Located via string references: "EVENT_OPEN_OBJECT" @ 0x007bcda0 (case 7)
                // Original implementation: Opens door, updates OpenState to 1 (open), fires OnOpen script
                if (!doorComponent.IsOpen)
                {
                    doorComponent.Open();
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleOpenObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) opened (IsOpen=true, OpenState={doorComponent.OpenState})");
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleOpenObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) is already open");
                }

                // Fire OnOpen script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN (0x16) fires when door is opened
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844 (0x16), "OnOpen" @ 0x007be1b0, "ScriptOnOpen" @ 0x007beeb8
                // Original implementation: OnOpen script fires on door entity after door is opened
                // Script fires regardless of whether door was already open (allows scripts to react to open attempts)
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnOpen, null);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleOpenObjectEvent: Fired OnOpen script event on door {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Check if entity has placeable component
            IPlaceableComponent placeableComponent = entity.GetComponent<IPlaceableComponent>();
            if (placeableComponent != null)
            {
                // Open the placeable (container)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_OPEN_OBJECT sets IsOpen=true, AnimationState=1
                // Located via string references: "EVENT_OPEN_OBJECT" @ 0x007bcda0 (case 7)
                // Original implementation: Opens placeable container, updates AnimationState to 1 (open), fires OnOpen script
                if (!placeableComponent.IsOpen)
                {
                    placeableComponent.Open();
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleOpenObjectEvent: Placeable {entity.Tag ?? "null"} ({entity.ObjectId}) opened (IsOpen=true, AnimationState={placeableComponent.AnimationState})");
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleOpenObjectEvent: Placeable {entity.Tag ?? "null"} ({entity.ObjectId}) is already open");
                }

                // Fire OnOpen script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN (0x16) fires when placeable is opened
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844 (0x16)
                // Original implementation: OnOpen script fires on placeable entity after placeable is opened
                // Script fires regardless of whether placeable was already open (allows scripts to react to open attempts)
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnOpen, null);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleOpenObjectEvent: Fired OnOpen script event on placeable {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Entity is not a door or placeable - log warning
            Console.WriteLine($"[OdysseyEventDispatcher] HandleOpenObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is not a door or placeable, cannot open");
        }

        /// <summary>
        /// Handles EVENT_CLOSE_OBJECT (6) event.
        /// </summary>
        /// <param name="entity">The entity to close (door or placeable).</param>
        /// <param name="world">The world containing the entity.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_CLOSE_OBJECT closes door/placeable and fires OnClose script
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0 case 6, line 63)
        /// </remarks>
        private void HandleCloseObjectEvent(IEntity entity, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleCloseObjectEvent: Closing entity {entity.Tag ?? "null"} ({entity.ObjectId})");

            // Check if entity has door component
            IDoorComponent doorComponent = entity.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                // Close the door
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_CLOSE_OBJECT sets IsOpen=false, OpenState=0
                // Located via string references: "EVENT_CLOSE_OBJECT" @ 0x007bcdb4 (case 6)
                // Original implementation: Closes door, updates OpenState to 0 (closed), fires OnClose script
                if (doorComponent.IsOpen)
                {
                    doorComponent.Close();
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleCloseObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) closed (IsOpen=false, OpenState={doorComponent.OpenState})");
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleCloseObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) is already closed");
                }

                // Fire OnClose script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE (0x17) fires when door is closed
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820 (0x17), "OnClosed" @ 0x007be1c8, "ScriptOnClose" @ 0x007beeb8
                // Original implementation: OnClose script fires on door entity after door is closed
                // Script fires regardless of whether door was already closed (allows scripts to react to close attempts)
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnClose, null);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleCloseObjectEvent: Fired OnClose script event on door {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Check if entity has placeable component
            IPlaceableComponent placeableComponent = entity.GetComponent<IPlaceableComponent>();
            if (placeableComponent != null)
            {
                // Close the placeable (container)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_CLOSE_OBJECT sets IsOpen=false, AnimationState=0
                // Located via string references: "EVENT_CLOSE_OBJECT" @ 0x007bcdb4 (case 6)
                // Original implementation: Closes placeable container, updates AnimationState to 0 (closed), fires OnClose script
                if (placeableComponent.IsOpen)
                {
                    placeableComponent.Close();
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleCloseObjectEvent: Placeable {entity.Tag ?? "null"} ({entity.ObjectId}) closed (IsOpen=false, AnimationState={placeableComponent.AnimationState})");
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleCloseObjectEvent: Placeable {entity.Tag ?? "null"} ({entity.ObjectId}) is already closed");
                }

                // Fire OnClose script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE (0x17) fires when placeable is closed
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820 (0x17)
                // Original implementation: OnClose script fires on placeable entity after placeable is closed
                // Script fires regardless of whether placeable was already closed (allows scripts to react to close attempts)
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnClose, null);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleCloseObjectEvent: Fired OnClose script event on placeable {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Entity is not a door or placeable - log warning
            Console.WriteLine($"[OdysseyEventDispatcher] HandleCloseObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is not a door or placeable, cannot close");
        }

        /// <summary>
        /// Handles EVENT_LOCK_OBJECT (0xd) event.
        /// </summary>
        /// <param name="entity">The entity to lock (door or placeable).</param>
        /// <param name="world">The world containing the entity.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_LOCK_OBJECT locks door/placeable and fires OnLock script
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0 case 0xd, line 84)
        /// </remarks>
        private void HandleLockObjectEvent(IEntity entity, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Locking entity {entity.Tag ?? "null"} ({entity.ObjectId})");

            // Check if entity has door component
            IDoorComponent doorComponent = entity.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                // Lock the door
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_LOCK_OBJECT sets IsLocked=true (only if Lockable flag is true)
                // Located via string references: "EVENT_LOCK_OBJECT" @ 0x007bcd20 (case 0xd)
                // Original implementation: Locks door if Lockable flag is true, fires OnLock script
                // Lock validation: Only locks if Lockable flag is true (from UTD template)
                if (!doorComponent.IsLocked)
                {
                    // Check if door is lockable
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Lockable flag determines if door can be locked
                    // Located via string references: "Lockable" field in UTD template
                    // Original implementation: Only locks if Lockable flag is true
                    if (doorComponent.LockableByScript)
                    {
                        doorComponent.Lock();
                        Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) locked (IsLocked=true)");
                    }
                    else
                    {
                        Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) is not lockable, cannot lock");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) is already locked");
                }

                // Fire OnLock script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED (0x1c) fires when door is locked
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (0x1c), "OnLock" @ 0x007c1a28, "ScriptOnLock" @ 0x007c1a0c
                // Original implementation: OnLock script fires on door entity after door is locked
                // Script fires regardless of whether door was already locked (allows scripts to react to lock attempts)
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnLock, null);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Fired OnLock script event on door {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Check if entity has placeable component
            IPlaceableComponent placeableComponent = entity.GetComponent<IPlaceableComponent>();
            if (placeableComponent != null)
            {
                // Lock the placeable
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_LOCK_OBJECT sets IsLocked=true
                // Located via string references: "EVENT_LOCK_OBJECT" @ 0x007bcd20 (case 0xd)
                // Original implementation: Locks placeable, fires OnLock script
                // Note: Placeables don't have LockableByScript flag, so we always allow locking
                if (!placeableComponent.IsLocked)
                {
                    placeableComponent.IsLocked = true;
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Placeable {entity.Tag ?? "null"} ({entity.ObjectId}) locked (IsLocked=true)");
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Placeable {entity.Tag ?? "null"} ({entity.ObjectId}) is already locked");
                }

                // Fire OnLock script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED (0x1c) fires when placeable is locked
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (0x1c)
                // Original implementation: OnLock script fires on placeable entity after placeable is locked
                // Script fires regardless of whether placeable was already locked (allows scripts to react to lock attempts)
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnLock, null);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Fired OnLock script event on placeable {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Entity is not a door or placeable - log warning
            Console.WriteLine($"[OdysseyEventDispatcher] HandleLockObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is not a door or placeable, cannot lock");
        }

        /// <summary>
        /// Handles EVENT_UNLOCK_OBJECT (0xc) event.
        /// </summary>
        /// <param name="entity">The entity to unlock (door or placeable).</param>
        /// <param name="world">The world containing the entity.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_UNLOCK_OBJECT unlocks door/placeable and fires OnUnlock script
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0 case 0xc, line 81)
        /// </remarks>
        private void HandleUnlockObjectEvent(IEntity entity, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleUnlockObjectEvent: Unlocking entity {entity.Tag ?? "null"} ({entity.ObjectId})");

            // Check if entity has door component
            IDoorComponent doorComponent = entity.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                // Unlock the door
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_UNLOCK_OBJECT sets IsLocked=false
                // Located via string references: "EVENT_UNLOCK_OBJECT" @ 0x007bcd34 (case 0xc)
                // Original implementation: Unlocks door, fires OnUnlock script
                if (doorComponent.IsLocked)
                {
                    doorComponent.Unlock();
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleUnlockObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) unlocked (IsLocked=false)");
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleUnlockObjectEvent: Door {entity.Tag ?? "null"} ({entity.ObjectId}) is already unlocked");
                }

                // Fire OnUnlock script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED (0x1d) fires when door is unlocked
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc72c (0x1d), "OnUnlock" @ 0x007c1a00, "ScriptOnUnlock" @ 0x007c1a00
                // Original implementation: OnUnlock script fires on door entity after door is unlocked
                // Script fires regardless of whether door was already unlocked (allows scripts to react to unlock attempts)
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnUnlock, null);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleUnlockObjectEvent: Fired OnUnlock script event on door {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Check if entity has placeable component
            IPlaceableComponent placeableComponent = entity.GetComponent<IPlaceableComponent>();
            if (placeableComponent != null)
            {
                // Unlock the placeable
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_UNLOCK_OBJECT sets IsLocked=false
                // Located via string references: "EVENT_UNLOCK_OBJECT" @ 0x007bcd34 (case 0xc)
                // Original implementation: Unlocks placeable, fires OnUnlock script
                if (placeableComponent.IsLocked)
                {
                    placeableComponent.Unlock();
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleUnlockObjectEvent: Placeable {entity.Tag ?? "null"} ({entity.ObjectId}) unlocked (IsLocked=false)");
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleUnlockObjectEvent: Placeable {entity.Tag ?? "null"} ({entity.ObjectId}) is already unlocked");
                }

                // Fire OnUnlock script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED (0x1d) fires when placeable is unlocked
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc72c (0x1d)
                // Original implementation: OnUnlock script fires on placeable entity after placeable is unlocked
                // Script fires regardless of whether placeable was already unlocked (allows scripts to react to unlock attempts)
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnUnlock, null);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleUnlockObjectEvent: Fired OnUnlock script event on placeable {entity.Tag ?? "null"} ({entity.ObjectId})");
                return;
            }

            // Entity is not a door or placeable - log warning
            Console.WriteLine($"[OdysseyEventDispatcher] HandleUnlockObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is not a door or placeable, cannot unlock");
        }

        /// <summary>
        /// Handles combat-related events.
        /// </summary>
        /// <param name="entity">The entity receiving the combat event.</param>
        /// <param name="eventType">The combat event type (EVENT_ON_MELEE_ATTACKED, EVENT_DESTROY_OBJECT, etc.).</param>
        /// <param name="sourceEntity">The entity that triggered the combat event (attacker, damager, etc.). May be null if not available.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DispatchEvent @ 0x004dcfb0 handles combat events by firing appropriate script events.
        /// Located via string references:
        /// - "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4 (case 0xf) - fires OnPhysicalAttacked script
        /// - "EVENT_DESTROY_OBJECT" @ 0x007bcd48 (case 0xb) - fires OnDeath script if entity is dead, or handles object destruction
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (eventSubtype 4) - fires OnDamaged script
        ///
        /// Combat event handling flow (based on swkotor2.exe: DispatchEvent @ 0x004dcfb0):
        /// 1. EVENT_ON_MELEE_ATTACKED (0xf): Fires OnPhysicalAttacked script on target entity with attacker as triggerer
        ///    - Script fires regardless of hit/miss (before damage is applied)
        ///    - Located via "OnMeleeAttacked" @ 0x007c1a5c, "ScriptAttacked" @ 0x007bee80
        /// 2. EVENT_DESTROY_OBJECT (0xb): Handles object destruction or entity death
        ///    - If entity is dead (CurrentHP <= 0), fires OnDeath script with killer as triggerer
        ///    - If entity is not dead, handles object destruction (doors, placeables, etc.)
        ///    - Located via "OnDeath" script field, death event handling
        /// 3. EVENT_SIGNAL_EVENT with ON_DAMAGED (eventSubtype 4): Fires OnDamaged script on target entity
        ///    - Script fires when entity takes damage (after damage is applied)
        ///    - Located via "ScriptDamaged" @ 0x007bee70, "OnDamaged" @ 0x007c1a80
        ///
        /// Script execution:
        /// - Scripts are retrieved from entity's IScriptHooksComponent for the appropriate ScriptEvent
        /// - Scripts are executed via world's EventBus.FireScriptEvent, which queues and processes script execution
        /// - Source entity (attacker/damager/killer) is passed as triggerer to script execution context
        /// - Scripts can use GetLastAttacker(), GetLastDamager(), GetLastHostileActor() to retrieve source entity
        ///
        /// AI behavior:
        /// - Combat events trigger AI responses (aggression, fleeing, etc.)
        /// - AI systems are notified via EventBus events and script execution
        /// - Combat state is updated by CombatManager when attacks/damage occur
        /// </remarks>
        protected override void HandleCombatEvent(IEntity entity, int eventType, IEntity sourceEntity = null)
        {
            if (entity == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] HandleCombatEvent: Entity is null, aborting combat event handling");
                return;
            }

            // Get world and event bus
            IWorld world = entity.World;
            if (world == null || world.EventBus == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] HandleCombatEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) has no world or EventBus");
                return;
            }

            string eventName = GetEventName(eventType);
            string entityInfo = $"{entity.Tag ?? "null"} ({entity.ObjectId})";
            string sourceInfo = sourceEntity != null ? $"{sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId})" : "null";
            Console.WriteLine($"[OdysseyEventDispatcher] HandleCombatEvent: Handling {eventName} ({eventType}) for entity {entityInfo}, source: {sourceInfo}");

            // Handle different combat event types
            switch (eventType)
            {
                case 0xf: // EVENT_ON_MELEE_ATTACKED (swkotor2.exe: 0x004dcfb0 case 0xf, line 89)
                    // Fire OnPhysicalAttacked script event on target entity
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_ON_MELEE_ATTACKED fires OnMeleeAttacked script
                    // Located via string references: "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4 (case 0xf), "OnMeleeAttacked" @ 0x007c1a5c, "ScriptAttacked" @ 0x007bee80
                    // Original implementation: EVENT_ON_MELEE_ATTACKED fires on target entity when attacked (before damage is applied)
                    // Script fires regardless of hit/miss - this allows scripts to react to being targeted
                    // Source entity (attacker) is passed as triggerer to script execution context
                    // Scripts can use GetLastAttacker() to retrieve the attacker if sourceEntity is null
                    HandleMeleeAttackedEvent(entity, sourceEntity, world);
                    break;

                case 0xb: // EVENT_DESTROY_OBJECT (swkotor2.exe: 0x004dcfb0 case 0xb, line 77)
                    // Handle object destruction or entity death
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_DESTROY_OBJECT can indicate entity death or object destruction
                    // Located via string references: "EVENT_DESTROY_OBJECT" @ 0x007bcd48 (case 0xb)
                    // Original implementation: EVENT_DESTROY_OBJECT fires when entity dies or object is destroyed
                    // For creatures: If entity is dead (CurrentHP <= 0), fires OnDeath script with killer as triggerer
                    // For objects (doors, placeables): Handles object destruction and cleanup
                    // Source entity (killer/destroyer) is passed as triggerer to script execution context
                    HandleDestroyObjectEvent(entity, sourceEntity, world);
                    break;

                case 0xa: // EVENT_SIGNAL_EVENT with ON_DAMAGED (eventSubtype 4)
                    // This case is handled in DispatchEvent before calling HandleCombatEvent
                    // But if called directly, fire OnDamaged script event
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
                    // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (0x4), "ScriptDamaged" @ 0x007bee70, "OnDamaged" @ 0x007c1a80
                    // Original implementation: OnDamaged script fires on target entity when damage is dealt (after damage is applied)
                    // Source entity (damager) is passed as triggerer to script execution context
                    // Scripts can use GetLastDamager() to retrieve the damager if sourceEntity is null
                    HandleDamagedEvent(entity, sourceEntity, world);
                    break;

                default:
                    // Unknown combat event type - log and ignore
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleCombatEvent: Unknown combat event type {eventType} ({eventName}) for entity {entityInfo}");
                    break;
            }
        }

        /// <summary>
        /// Handles EVENT_ON_MELEE_ATTACKED (0xf) combat event.
        /// </summary>
        /// <param name="entity">The entity being attacked.</param>
        /// <param name="attacker">The entity attacking (may be null).</param>
        /// <param name="world">The world containing the entities.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_ON_MELEE_ATTACKED fires OnPhysicalAttacked script
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0 case 0xf, line 89)
        /// </remarks>
        private void HandleMeleeAttackedEvent(IEntity entity, IEntity attacker, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleMeleeAttackedEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) attacked by {attacker?.Tag ?? "null"} ({attacker?.ObjectId ?? 0})");

            // Fire OnPhysicalAttacked script event on target entity
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_ON_MELEE_ATTACKED fires OnMeleeAttacked script
            // Script fires regardless of hit/miss - this allows scripts to react to being targeted
            // Source entity (attacker) is passed as triggerer to script execution context
            world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnPhysicalAttacked, attacker);
            Console.WriteLine($"[OdysseyEventDispatcher] HandleMeleeAttackedEvent: Fired OnPhysicalAttacked script event on entity {entity.Tag ?? "null"} ({entity.ObjectId})");
        }

        /// <summary>
        /// Handles EVENT_DESTROY_OBJECT (0xb) combat event.
        /// </summary>
        /// <param name="entity">The entity being destroyed.</param>
        /// <param name="killer">The entity that destroyed/killed (may be null).</param>
        /// <param name="world">The world containing the entities.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_DESTROY_OBJECT handles entity death or object destruction
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0 case 0xb, line 77)
        /// </remarks>
        private void HandleDestroyObjectEvent(IEntity entity, IEntity killer, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleDestroyObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) destroyed by {killer?.Tag ?? "null"} ({killer?.ObjectId ?? 0})");

            // Check if entity is dead (creature death)
            IStatsComponent stats = entity.GetComponent<IStatsComponent>();
            if (stats != null && stats.IsDead)
            {
                // Entity is dead - fire OnDeath script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH fires when entity dies
                // Located via string references: "OnDeath" script field, death event handling
                // Original implementation: OnDeath script fires on victim entity with killer as triggerer
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnDeath, killer);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleDestroyObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is dead, fired OnDeath script event");
            }
            else
            {
                // Entity is not dead - this is object destruction (door, placeable, etc.)
                // Handle object destruction cleanup
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Object destruction removes entity from world and cleans up components
                // Located via string references: "EVENT_DESTROY_OBJECT" @ 0x007bcd48 (case 0xb in DispatchEvent @ 0x004dcfb0)
                // Original implementation: EVENT_DESTROY_OBJECT is logged but doesn't fire script events
                // Object destruction is handled by removing entity from world and cleaning up components
                // The entity destruction system (World.DestroyEntity) handles all cleanup automatically
                Console.WriteLine($"[OdysseyEventDispatcher] HandleDestroyObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is not dead, handling object destruction");

                // Destroy the entity through the world's destruction system
                // This ensures proper cleanup of all components, removal from areas, physics, and event systems
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Object destruction removes entity from world and cleans up all components
                // The world's DestroyEntity method handles:
                // - Component cleanup (removes all component references)
                // - Area removal (removes entity from current area)
                // - Physics cleanup (removes entity from physics system if applicable)
                // - Event system cleanup (unregisters entity from event handlers)
                // - Memory cleanup (disposes entity resources)
                // Note: There is no OnDestroy script event in the original game (swkotor2.exe)
                // EVENT_DESTROY_OBJECT is a world event that triggers entity destruction, not a script event
                if (world != null)
                {
                    world.DestroyEntity(entity.ObjectId);
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleDestroyObjectEvent: Destroyed entity {entity.Tag ?? "null"} ({entity.ObjectId}) through world destruction system");
                }
                else
                {
                    Console.WriteLine($"[OdysseyEventDispatcher] HandleDestroyObjectEvent: Warning - Cannot destroy entity {entity.Tag ?? "null"} ({entity.ObjectId}), world is null");
                }
            }
        }

        /// <summary>
        /// Handles ON_DAMAGED combat event (EVENT_SIGNAL_EVENT with eventSubtype 4).
        /// </summary>
        /// <param name="entity">The entity being damaged.</param>
        /// <param name="damager">The entity that damaged (may be null).</param>
        /// <param name="world">The world containing the entities.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0, eventSubtype 4, line 145)
        /// </remarks>
        private void HandleDamagedEvent(IEntity entity, IEntity damager, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleDamagedEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) damaged by {damager?.Tag ?? "null"} ({damager?.ObjectId ?? 0})");

            // Fire OnDamaged script event on target entity
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
            // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (0x4), "ScriptDamaged" @ 0x007bee70, "OnDamaged" @ 0x007c1a80
            // Original implementation: OnDamaged script fires on target entity when damage is dealt (after damage is applied)
            // Source entity (damager) is passed as triggerer to script execution context
            // Scripts can use GetLastDamager() to retrieve the damager if sourceEntity is null
            world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnDamaged, damager);
            Console.WriteLine($"[OdysseyEventDispatcher] HandleDamagedEvent: Fired OnDamaged script event on entity {entity.Tag ?? "null"} ({entity.ObjectId})");
        }

        /// <summary>
        /// Handles script hook events.
        /// </summary>
        /// <param name="entity">The entity to execute the script event on.</param>
        /// <param name="eventType">The event type (typically EVENT_SIGNAL_EVENT = 10).</param>
        /// <param name="eventSubtype">The script event subtype (ON_HEARTBEAT, ON_PERCEPTION, etc.).</param>
        /// <param name="sourceEntity">The entity that triggered the event (optional, used as triggerer in script execution).</param>
        /// <remarks>
        /// Based on SIGNAL_EVENT (10) with subtypes for different script hooks.
        /// Executes entity-specific scripts based on event type.
        /// Handles heartbeat, perception, dialogue, etc.
        ///
        /// Implementation based on swkotor2.exe: DispatchEvent @ 0x004dcfb0
        /// - When eventType is 10 (EVENT_SIGNAL_EVENT), eventSubtype maps to CSWSSCRIPTEVENT_EVENTTYPE_ON_* constants
        /// - Maps eventSubtype to ScriptEvent enum (0=ON_HEARTBEAT, 1=ON_PERCEPTION, 2=ON_SPELL_CAST_AT, 4=ON_DAMAGED, etc.)
        /// - Gets script ResRef from entity's IScriptHooksComponent for the mapped ScriptEvent
        /// - Fires script event via world's EventBus, which queues and processes script execution
        /// - Source entity (from DispatchEvent call) is passed as triggerer to script execution
        ///
        /// Script execution flow:
        /// 1. Map eventSubtype to ScriptEvent enum
        /// 2. Get world from entity
        /// 3. Fire script event via world.EventBus.FireScriptEvent(entity, scriptEvent, sourceEntity)
        /// 4. EventBus queues the event and processes it during frame update
        /// 5. Script executor executes the script ResRef from entity's IScriptHooksComponent
        /// </remarks>
        protected override void HandleScriptEvent(IEntity entity, int eventType, int eventSubtype, IEntity sourceEntity = null)
        {
            if (entity == null)
            {
                Console.WriteLine("[OdysseyEventDispatcher] HandleScriptEvent: Entity is null, aborting script event handling");
                return;
            }

            // Get world from entity
            IWorld world = entity.World;
            if (world == null || world.EventBus == null)
            {
                Console.WriteLine($"[OdysseyEventDispatcher] HandleScriptEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) has no world or EventBus");
                return;
            }

            // Map eventSubtype to ScriptEvent enum
            // Maps CSWSSCRIPTEVENT_EVENTTYPE_ON_* constants to ScriptEvent enum values
            // Implementation is game-specific (see MapEventSubtypeToScriptEvent in subclasses)
            Core.Enums.ScriptEvent scriptEvent = MapEventSubtypeToScriptEvent(eventSubtype);
            string subtypeName = GetEventSubtypeName(eventSubtype);
            string entityInfo = $"{entity.Tag ?? "null"} ({entity.ObjectId})";
            string sourceInfo = sourceEntity != null ? $"{sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId})" : "null";
            Console.WriteLine($"[OdysseyEventDispatcher] HandleScriptEvent: Firing script event {scriptEvent} ({subtypeName}, subtype {eventSubtype}) on entity {entityInfo}, triggered by {sourceInfo}");

            // Fire script event via EventBus
            // EventBus will queue the event and process it during frame update
            // Script executor will execute the script ResRef from entity's IScriptHooksComponent
            // Source entity is passed as triggerer to script execution context
            world.EventBus.FireScriptEvent(entity, scriptEvent, sourceEntity);
            Console.WriteLine($"[OdysseyEventDispatcher] HandleScriptEvent: Script event {scriptEvent} queued for entity {entityInfo}");
        }

        /// <summary>
        /// Maps event subtype to ScriptEvent enum.
        /// </summary>
        /// <param name="eventSubtype">The event subtype from EVENT_SIGNAL_EVENT.</param>
        /// <returns>The corresponding ScriptEvent enum value.</returns>
        /// <remarks>
        /// Maps CSWSSCRIPTEVENT_EVENTTYPE_ON_* constants to ScriptEvent enum.
        /// Returns ScriptEvent.OnUserDefined for unknown subtypes.
        ///
        /// Event subtype mapping is common across both KOTOR 1 and KOTOR 2:
        /// - 0: ON_HEARTBEAT
        /// - 1: ON_PERCEPTION
        /// - 2: ON_SPELL_CAST_AT
        /// - 4: ON_DAMAGED
        /// - 5: ON_DISTURBED
        /// - 7: ON_CONVERSATION
        /// - 8: ON_SPAWN
        /// - 9: ON_RESTED
        /// - 10: ON_DEATH
        /// - 0xb: ON_USER_DEFINED
        /// - 0xc: ON_ENTER
        /// - 0xd: ON_EXIT
        /// - 0xe: ON_PLAYER_ENTER
        /// - 0xf: ON_PLAYER_EXIT
        /// - 0x10: ON_MODULE_START
        /// - 0x11: ON_MODULE_LOAD
        /// - 0x12: ON_ACTIVATE_ITEM
        /// - 0x13: ON_ACQUIRE_ITEM
        /// - 0x14: ON_UNACQUIRE_ITEM
        /// - 0x15: ON_EXHAUSTED
        /// - 0x16: ON_OPEN
        /// - 0x17: ON_CLOSE
        /// - 0x18: ON_DISARM
        /// - 0x19: ON_USED
        /// - 0x1a: ON_TrapTriggered
        /// - 0x1b: ON_DISTURBED (inventory context)
        /// - 0x1c: ON_LOCK
        /// - 0x1d: ON_UNLOCK
        /// - 0x1e: ON_CLICK
        /// - 0x1f: ON_BLOCKED
        /// - 0x20: ON_PLAYER_DYING
        /// - 0x21: ON_SPAWN_BUTTON_DOWN
        /// - 0x22: ON_FAIL_TO_OPEN
        /// - 0x23: ON_PLAYER_REST
        /// - 0x24: ON_PLAYER_DEATH
        /// - 0x25: ON_PLAYER_LEVEL_UP
        /// - 0x26: ON_EQUIP_ITEM
        ///
        /// Game-specific implementations should document the executable-specific addresses.
        /// </remarks>
        protected abstract Core.Enums.ScriptEvent MapEventSubtypeToScriptEvent(int eventSubtype);

        /// <summary>
        /// Queues an event for later processing.
        /// </summary>
        /// <remarks>
        /// Events are queued to prevent recursive dispatching.
        /// Ensures proper execution order and script safety.
        /// </remarks>
        public override void QueueEvent(IEntity sourceEntity, IEntity targetEntity, int eventType, int eventSubtype)
        {
            string eventName = GetEventName(eventType);
            string subtypeName = GetEventSubtypeName(eventSubtype);
            string sourceInfo = sourceEntity != null ? $"{sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId})" : "null";
            string targetInfo = targetEntity != null ? $"{targetEntity.Tag ?? "null"} ({targetEntity.ObjectId})" : "null";
            Console.WriteLine($"[OdysseyEventDispatcher] QueueEvent: Queuing event {eventName} ({eventType}) from {sourceInfo} to {targetInfo}, subtype: {subtypeName} ({eventSubtype}), queue size: {_eventQueue.Count}");
            _eventQueue.Enqueue(new PendingEvent
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                EventType = eventType,
                EventSubtype = eventSubtype
            });
            Console.WriteLine($"[OdysseyEventDispatcher] QueueEvent: Event queued, new queue size: {_eventQueue.Count}");
        }

        /// <summary>
        /// Processes queued events.
        /// </summary>
        /// <remarks>
        /// Called during script execution phase.
        /// Processes all queued events in order.
        /// Clears queue after processing.
        /// </remarks>
        public override void ProcessQueuedEvents()
        {
            int queueSize = _eventQueue.Count;
            if (queueSize == 0)
            {
                return;
            }

            Console.WriteLine($"[OdysseyEventDispatcher] ProcessQueuedEvents: Processing {queueSize} queued event(s)");
            int processedCount = 0;
            while (_eventQueue.Count > 0)
            {
                var pendingEvent = _eventQueue.Dequeue();
                DispatchEvent(pendingEvent.SourceEntity, pendingEvent.TargetEntity,
                            pendingEvent.EventType, pendingEvent.EventSubtype);
                processedCount++;
            }
            Console.WriteLine($"[OdysseyEventDispatcher] ProcessQueuedEvents: Processed {processedCount} event(s), queue is now empty");
        }
    }

    /// <summary>
    /// KOTOR 1 (swkotor.exe) event dispatcher implementation.
    /// </summary>
    /// <remarks>
    /// KOTOR 1 Event Dispatcher:
    /// - Based on swkotor.exe: DispatchEvent function (FUN_004af630 @ 0x004af630)
    /// - Maps event IDs to string names for debugging
    /// - Routes events to appropriate handlers based on type
    ///
    /// Event subtype mapping implementation is common with KOTOR 2, but addresses differ:
    /// - Constants are located at different addresses in swkotor.exe vs swkotor2.exe
    /// - Event subtype mapping logic is identical (both use same constants and mappings)
    /// </remarks>
    public class Kotor1EventDispatcher : OdysseyEventDispatcher
    {
        /// <summary>
        /// Initializes a new instance of the Kotor1EventDispatcher.
        /// </summary>
        /// <param name="loadingScreen">Optional loading screen for area transitions.</param>
        /// <param name="moduleLoader">Optional module loader for area streaming. If provided, areas will be loaded on-demand during transitions.</param>
        public Kotor1EventDispatcher(ILoadingScreen loadingScreen = null, Andastra.Runtime.Engines.Odyssey.Loading.ModuleLoader moduleLoader = null)
            : base(loadingScreen, moduleLoader)
        {
        }

        /// <summary>
        /// Maps event subtype to ScriptEvent enum.
        /// </summary>
        /// <param name="eventSubtype">The event subtype from EVENT_SIGNAL_EVENT.</param>
        /// <returns>The corresponding ScriptEvent enum value.</returns>
        /// <remarks>
        /// Based on swkotor.exe: FUN_004af630 @ 0x004af630 (DispatchEvent equivalent).
        /// Maps CSWSSCRIPTEVENT_EVENTTYPE_ON_* constants to ScriptEvent enum.
        /// Returns ScriptEvent.OnUserDefined for unknown subtypes.
        ///
        /// Event subtype mapping (based on swkotor.exe: FUN_004af630):
        /// - 0: ON_HEARTBEAT (CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT @ 0x00744958)
        /// - 1: ON_PERCEPTION (CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION @ 0x00744930)
        /// - 2: ON_SPELL_CAST_AT (CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT @ 0x00744904)
        /// - 4: ON_DAMAGED (CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED @ 0x007448dc)
        /// - 5: ON_DISTURBED (CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED @ 0x007448b4)
        /// - 7: ON_CONVERSATION (CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE @ 0x0074488c)
        /// - 8: ON_SPAWN (CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN @ 0x00744864)
        /// - 9: ON_RESTED (CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED @ 0x00744840)
        /// - 10: ON_DEATH (CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH @ 0x0074481c)
        /// - 0xb: ON_USER_DEFINED (CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT @ 0x007447ec)
        /// - 0xc: ON_ENTER (CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER @ 0x007447c0)
        /// - 0xd: ON_EXIT (CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT @ 0x00744794)
        /// - 0xe: ON_PLAYER_ENTER (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER @ 0x00744768)
        /// - 0xf: ON_PLAYER_EXIT (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT @ 0x0074473c)
        /// - 0x10: ON_MODULE_START (CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START @ 0x00744710)
        /// - 0x11: ON_MODULE_LOAD (CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD @ 0x007446e4)
        /// - 0x12: ON_ACTIVATE_ITEM (CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM @ 0x007446b8)
        /// - 0x13: ON_ACQUIRE_ITEM (CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM @ 0x0074468c)
        /// - 0x14: ON_UNACQUIRE_ITEM (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM @ 0x00744664)
        /// - 0x15: ON_EXHAUSTED (CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED @ 0x00744630)
        /// - 0x16: ON_OPEN (CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN @ 0x0074460c)
        /// - 0x17: ON_CLOSE (CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE @ 0x007445e8)
        /// - 0x18: ON_DISARM (CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM @ 0x007445c4)
        /// - 0x19: ON_USED (CSWSSCRIPTEVENT_EVENTTYPE_ON_USED @ 0x007445a0)
        /// - 0x1a: ON_TrapTriggered (CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED @ 0x00744574)
        /// - 0x1b: ON_DISTURBED (CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED @ 0x00744540)
        /// - 0x1c: ON_LOCK (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED @ 0x0074451c)
        /// - 0x1d: ON_UNLOCK (CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED @ 0x007444f4)
        /// - 0x1e: ON_CLICK (CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED @ 0x007444cc)
        /// - 0x1f: ON_BLOCKED (CSWSSCRIPTEVENT_EVENTTYPE_ON_PATH_BLOCKED @ 0x007444a0)
        /// - 0x20: ON_PLAYER_DYING (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING @ 0x00744474)
        /// - 0x21: ON_SPAWN_BUTTON_DOWN (CSWSSCRIPTEVENT_EVENTTYPE_ON_RESPAWN_BUTTON_PRESSED @ 0x00744440)
        /// - 0x22: ON_FAIL_TO_OPEN (CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN @ 0x00744414)
        /// - 0x23: ON_PLAYER_REST (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST @ 0x007443e8)
        /// - 0x24: ON_PLAYER_DEATH (CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE @ 0x007443b4)
        /// - 0x25: ON_PLAYER_LEVEL_UP (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP @ 0x00744384)
        /// - 0x26: ON_EQUIP_ITEM (CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM @ 0x0074435c)
        /// </remarks>
        protected override Core.Enums.ScriptEvent MapEventSubtypeToScriptEvent(int eventSubtype)
        {
            switch (eventSubtype)
            {
                case 0x0: return Core.Enums.ScriptEvent.OnHeartbeat; // CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT
                case 0x1: return Core.Enums.ScriptEvent.OnPerception; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION
                case 0x2: return Core.Enums.ScriptEvent.OnSpellCastAt; // CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT
                                                                       // case 0x3?? (not used)
                case 0x4: return Core.Enums.ScriptEvent.OnDamaged; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED
                case 0x5: return Core.Enums.ScriptEvent.OnDisturbed; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED
                                                                     // case 0x6?? (not used)
                case 0x7: return Core.Enums.ScriptEvent.OnConversation; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE
                case 0x8: return Core.Enums.ScriptEvent.OnSpawn; // CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN
                case 0x9: return Core.Enums.ScriptEvent.OnRested; // CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED
                case 0xa: return Core.Enums.ScriptEvent.OnDeath; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH
                case 0xb: return Core.Enums.ScriptEvent.OnUserDefined; // CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT
                case 0xc: return Core.Enums.ScriptEvent.OnEnter; // CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER
                case 0xd: return Core.Enums.ScriptEvent.OnExit; // CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT
                case 0xe: return Core.Enums.ScriptEvent.OnClientEnter; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER
                case 0xf: return Core.Enums.ScriptEvent.OnClientLeave; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT
                case 0x10: return Core.Enums.ScriptEvent.OnModuleStart; // CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START
                case 0x11: return Core.Enums.ScriptEvent.OnModuleLoad; // CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD
                case 0x12: return Core.Enums.ScriptEvent.OnActivateItem; // CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM
                case 0x13: return Core.Enums.ScriptEvent.OnAcquireItem; // CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM
                case 0x14: return Core.Enums.ScriptEvent.OnUnacquireItem; // CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM
                case 0x15: return Core.Enums.ScriptEvent.OnExhausted; // CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED
                case 0x16: return Core.Enums.ScriptEvent.OnOpen; // CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN
                case 0x17: return Core.Enums.ScriptEvent.OnClose; // CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE
                case 0x18: return Core.Enums.ScriptEvent.OnDisarm; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM
                case 0x19: return Core.Enums.ScriptEvent.OnUsed; // CSWSSCRIPTEVENT_EVENTTYPE_ON_USED
                case 0x1a: return Core.Enums.ScriptEvent.OnTrapTriggered; // CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED
                case 0x1b: return Core.Enums.ScriptEvent.OnDisturbed; // CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED (same enum value as 5, but different context)
                case 0x1c: return Core.Enums.ScriptEvent.OnLock; // CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED
                case 0x1d: return Core.Enums.ScriptEvent.OnUnlock; // CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED
                case 0x1e: return Core.Enums.ScriptEvent.OnClick; // CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED
                case 0x1f: return Core.Enums.ScriptEvent.OnBlocked; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PATH_BLOCKED
                case 0x20: return Core.Enums.ScriptEvent.OnPlayerDying; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING
                case 0x21: return Core.Enums.ScriptEvent.OnSpawnButtonDown; // CSWSSCRIPTEVENT_EVENTTYPE_ON_RESPAWN_BUTTON_PRESSED
                case 0x22: return Core.Enums.ScriptEvent.OnFailToOpen; // CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN
                case 0x23: return Core.Enums.ScriptEvent.OnPlayerRest; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST
                case 0x24: return Core.Enums.ScriptEvent.OnPlayerDeath; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE
                case 0x25: return Core.Enums.ScriptEvent.OnPlayerLevelUp; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP
                case 0x26: return Core.Enums.ScriptEvent.OnAcquireItem; // CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM (Note: OnEquipItem not in enum, using OnAcquireItem)
                default:
                    // Unknown event subtype - use OnUserDefined as fallback
                    return Core.Enums.ScriptEvent.OnUserDefined;
            }
        }
    }

    /// <summary>
    /// KOTOR 2: The Sith Lords (swkotor2.exe) event dispatcher implementation.
    /// </summary>
    /// <remarks>
    /// KOTOR 2 Event Dispatcher:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DispatchEvent @ 0x004dcfb0
    /// - Maps event IDs to string names for debugging
    /// - Routes events to appropriate handlers based on type
    ///
    /// Event subtype mapping implementation is common with KOTOR 1, but addresses differ:
    /// - Constants are located at different addresses in swkotor2.exe vs swkotor.exe
    /// - Event subtype mapping logic is identical (both use same constants and mappings)
    /// </remarks>
    public class Kotor2EventDispatcher : OdysseyEventDispatcher
    {
        /// <summary>
        /// Initializes a new instance of the Kotor2EventDispatcher.
        /// </summary>
        /// <param name="loadingScreen">Optional loading screen for area transitions.</param>
        /// <param name="moduleLoader">Optional module loader for area streaming. If provided, areas will be loaded on-demand during transitions.</param>
        public Kotor2EventDispatcher(ILoadingScreen loadingScreen = null, Andastra.Runtime.Engines.Odyssey.Loading.ModuleLoader moduleLoader = null)
            : base(loadingScreen, moduleLoader)
        {
        }

        /// <summary>
        /// Maps event subtype to ScriptEvent enum.
        /// </summary>
        /// <param name="eventSubtype">The event subtype from EVENT_SIGNAL_EVENT.</param>
        /// <returns>The corresponding ScriptEvent enum value.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DispatchEvent @ 0x004dcfb0 lines 132-246.
        /// Maps CSWSSCRIPTEVENT_EVENTTYPE_ON_* constants to ScriptEvent enum.
        /// Returns ScriptEvent.OnUserDefined for unknown subtypes.
        ///
        /// Event subtype mapping (based on swkotor2.exe: DispatchEvent @ 0x004dcfb0 lines 132-246):
        /// - 0: ON_HEARTBEAT (CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT @ 0x007bcb90)
        /// - 1: ON_PERCEPTION (CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION @ 0x007bcb68)
        /// - 2: ON_SPELL_CAST_AT (CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT @ 0x007bcb3c)
        /// - 4: ON_DAMAGED (CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED @ 0x007bcb14)
        /// - 5: ON_DISTURBED (CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED @ 0x007bcaec)
        /// - 7: ON_CONVERSATION (CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE @ 0x007bcac4)
        /// - 8: ON_SPAWN (CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN @ 0x007bca9c)
        /// - 9: ON_RESTED (CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED @ 0x007bca78)
        /// - 10: ON_DEATH (CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH @ 0x007bca54)
        /// - 0xb: ON_USER_DEFINED (CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT @ 0x007bca24)
        /// - 0xc: ON_ENTER (CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER @ 0x007bc9f8)
        /// - 0xd: ON_EXIT (CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT @ 0x007bc9cc)
        /// - 0xe: ON_PLAYER_ENTER (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER @ 0x007bc9a0)
        /// - 0xf: ON_PLAYER_EXIT (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT @ 0x007bc974)
        /// - 0x10: ON_MODULE_START (CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START @ 0x007bc948)
        /// - 0x11: ON_MODULE_LOAD (CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD @ 0x007bc91c)
        /// - 0x12: ON_ACTIVATE_ITEM (CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM @ 0x007bc8f0)
        /// - 0x13: ON_ACQUIRE_ITEM (CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM @ 0x007bc8c4)
        /// - 0x14: ON_UNACQUIRE_ITEM (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM @ 0x007bc89c)
        /// - 0x15: ON_EXHAUSTED (CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED @ 0x007bc868)
        /// - 0x16: ON_OPEN (CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN @ 0x007bc844)
        /// - 0x17: ON_CLOSE (CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE @ 0x007bc820)
        /// - 0x18: ON_DISARM (CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM @ 0x007bc7fc)
        /// - 0x19: ON_USED (CSWSSCRIPTEVENT_EVENTTYPE_ON_USED @ 0x007bc7d8)
        /// - 0x1a: ON_TrapTriggered (CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED @ 0x007bc778)
        /// - 0x1b: ON_DISTURBED (CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED @ 0x007bc778)
        /// - 0x1c: ON_LOCK (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED @ 0x007bc754)
        /// - 0x1d: ON_UNLOCK (CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED @ 0x007bc72c)
        /// - 0x1e: ON_CLICK (CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED @ 0x007bc704)
        /// - 0x1f: ON_BLOCKED (CSWSSCRIPTEVENT_EVENTTYPE_ON_PATH_BLOCKED @ 0x007bc6d8)
        /// - 0x20: ON_PLAYER_DYING (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING @ 0x007bc6ac)
        /// - 0x21: ON_SPAWN_BUTTON_DOWN (CSWSSCRIPTEVENT_EVENTTYPE_ON_RESPAWN_BUTTON_PRESSED @ 0x007bc678)
        /// - 0x22: ON_FAIL_TO_OPEN (CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN @ 0x007bc64c)
        /// - 0x23: ON_PLAYER_REST (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST @ 0x007bc620)
        /// - 0x24: ON_PLAYER_DEATH (CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE @ 0x007bc5ec)
        /// - 0x25: ON_PLAYER_LEVEL_UP (CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP @ 0x007bc5bc)
        /// - 0x26: ON_EQUIP_ITEM (CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM @ 0x007bc594)
        /// </remarks>
        protected override Core.Enums.ScriptEvent MapEventSubtypeToScriptEvent(int eventSubtype)
        {
            switch (eventSubtype)
            {
                case 0x0: return Core.Enums.ScriptEvent.OnHeartbeat; // CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT
                case 0x1: return Core.Enums.ScriptEvent.OnPerception; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION
                case 0x2: return Core.Enums.ScriptEvent.OnSpellCastAt; // CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT
                                                                       // case 0x3?? (not used)
                case 0x4: return Core.Enums.ScriptEvent.OnDamaged; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED
                case 0x5: return Core.Enums.ScriptEvent.OnDisturbed; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED
                                                                     // case 0x6?? (not used)
                case 0x7: return Core.Enums.ScriptEvent.OnConversation; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE
                case 0x8: return Core.Enums.ScriptEvent.OnSpawn; // CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN
                case 0x9: return Core.Enums.ScriptEvent.OnRested; // CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED
                case 0xa: return Core.Enums.ScriptEvent.OnDeath; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH
                case 0xb: return Core.Enums.ScriptEvent.OnUserDefined; // CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT
                case 0xc: return Core.Enums.ScriptEvent.OnEnter; // CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER
                case 0xd: return Core.Enums.ScriptEvent.OnExit; // CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT
                case 0xe: return Core.Enums.ScriptEvent.OnClientEnter; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER
                case 0xf: return Core.Enums.ScriptEvent.OnClientLeave; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT
                case 0x10: return Core.Enums.ScriptEvent.OnModuleStart; // CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START
                case 0x11: return Core.Enums.ScriptEvent.OnModuleLoad; // CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD
                case 0x12: return Core.Enums.ScriptEvent.OnActivateItem; // CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM
                case 0x13: return Core.Enums.ScriptEvent.OnAcquireItem; // CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM
                case 0x14: return Core.Enums.ScriptEvent.OnUnacquireItem; // CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM
                case 0x15: return Core.Enums.ScriptEvent.OnExhausted; // CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED
                case 0x16: return Core.Enums.ScriptEvent.OnOpen; // CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN
                case 0x17: return Core.Enums.ScriptEvent.OnClose; // CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE
                case 0x18: return Core.Enums.ScriptEvent.OnDisarm; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM
                case 0x19: return Core.Enums.ScriptEvent.OnUsed; // CSWSSCRIPTEVENT_EVENTTYPE_ON_USED
                case 0x1a: return Core.Enums.ScriptEvent.OnTrapTriggered; // CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED
                case 0x1b: return Core.Enums.ScriptEvent.OnDisturbed; // CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED (same enum value as 5, but different context)
                case 0x1c: return Core.Enums.ScriptEvent.OnLock; // CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED
                case 0x1d: return Core.Enums.ScriptEvent.OnUnlock; // CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED
                case 0x1e: return Core.Enums.ScriptEvent.OnClick; // CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED
                case 0x1f: return Core.Enums.ScriptEvent.OnBlocked; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PATH_BLOCKED
                case 0x20: return Core.Enums.ScriptEvent.OnPlayerDying; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING
                case 0x21: return Core.Enums.ScriptEvent.OnSpawnButtonDown; // CSWSSCRIPTEVENT_EVENTTYPE_ON_RESPAWN_BUTTON_PRESSED
                case 0x22: return Core.Enums.ScriptEvent.OnFailToOpen; // CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN
                case 0x23: return Core.Enums.ScriptEvent.OnPlayerRest; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST
                case 0x24: return Core.Enums.ScriptEvent.OnPlayerDeath; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE
                case 0x25: return Core.Enums.ScriptEvent.OnPlayerLevelUp; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP
                case 0x26: return Core.Enums.ScriptEvent.OnAcquireItem; // CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM (Note: OnEquipItem not in enum, using OnAcquireItem)
                default:
                    // Unknown event subtype - use OnUserDefined as fallback
                    return Core.Enums.ScriptEvent.OnUserDefined;
            }
        }
    }
}
