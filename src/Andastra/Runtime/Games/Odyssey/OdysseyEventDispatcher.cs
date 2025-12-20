using System;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Odyssey
{
    /// <summary>
    /// Odyssey Engine event dispatcher implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Event Dispatcher Implementation:
    /// - Based on DispatchEvent @ 0x004dcfb0 in swkotor2.exe
    /// - Maps event IDs to string names for debugging
    /// - Routes events to appropriate handlers based on type
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Event dispatching functions
    /// - swkotor2.exe: DispatchEvent @ 0x004dcfb0 with comprehensive event mapping
    /// - Event types: AREA_TRANSITION (0x1a), REMOVE_FROM_AREA (4), etc.
    /// - Script events: ON_HEARTBEAT (0), ON_PERCEPTION (1), etc.
    ///
    /// Event system features:
    /// - Immediate event dispatching
    /// - Queued event processing for script safety
    /// - Event logging and debugging support
    /// - Script hook integration
    /// </remarks>
    [PublicAPI]
    public class OdysseyEventDispatcher : BaseEventDispatcher
    {
        private readonly Queue<PendingEvent> _eventQueue = new Queue<PendingEvent>();
        private readonly ILoadingScreen _loadingScreen;

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
        public OdysseyEventDispatcher(ILoadingScreen loadingScreen = null)
        {
            _loadingScreen = loadingScreen;
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
            // Based on swkotor2.exe: DispatchEvent @ 0x004dcfb0 debug format "DRF Event Added: %s(%s) %s(%s) %s %s\n"
            string sourceInfo = sourceEntity != null ? $"{sourceEntity.Tag ?? "null"} ({sourceEntity.ObjectId})" : "null";
            string targetInfo = targetEntity != null ? $"{targetEntity.Tag ?? "null"} ({targetEntity.ObjectId})" : "null";
            Console.WriteLine($"[OdysseyEventDispatcher] Dispatching event: {eventName} ({eventType}) from {sourceInfo} to {targetInfo}, subtype: {subtypeName} ({eventSubtype})");

            // Route to appropriate handler based on event type
            switch (eventType)
            {
                case 0x1a: // EVENT_AREA_TRANSITION
                    // Extract target area from sourceEntity (door/trigger) component
                    // Based on swkotor2.exe: DispatchEvent @ 0x004dcfb0 handles EVENT_AREA_TRANSITION (case 0x1a)
                    // Source entity is the door/trigger that triggered the transition
                    // Target entity is the entity being transitioned (usually player/party)
                    string targetAreaResRef = ExtractTargetAreaFromTransitionSource(sourceEntity, targetEntity);
                    HandleAreaTransition(targetEntity, targetAreaResRef);
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
        protected override void HandleAreaTransition(IEntity entity, string targetArea)
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
                ResolveAndSetTransitionPosition(entity, world, targetAreaInstance);

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
        /// Based on swkotor2.exe: Door/trigger transition system
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
        /// Based on swkotor2.exe: Entity area tracking system
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
        /// Based on swkotor2.exe: Area streaming system
        /// Checks if area is already loaded in current module, otherwise loads it.
        /// For now, simplified implementation - full area streaming would require IModuleLoader integration.
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

                // In a full implementation, this would query IModule.GetAreas() to find the area
                // For now, we check if it's the current area or search through module areas
                // TODO: Full area streaming implementation would load area via IModuleLoader if not found
            }

            // For now, return current area if target matches, or null if not found
            // Full implementation would integrate with module loading system
            if (world.CurrentArea != null && string.Equals(world.CurrentArea.ResRef, targetAreaResRef, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Target area {targetAreaResRef} matches current area");
                return world.CurrentArea;
            }

            // If target area is not current area and not loaded, we would need to load it
            // This requires IModuleLoader which may not be available in this context
            // For now, return null to indicate area not found/not loaded
            // Full implementation would integrate with module loading system
            Console.WriteLine($"[OdysseyEventDispatcher] LoadOrGetTargetArea: Target area {targetAreaResRef} is not loaded (area streaming not yet implemented)");
            return null;
        }

        /// <summary>
        /// Resolves and sets the transition position from source entity's LinkedTo field.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Transition positioning system
        /// If source entity (door/trigger) has LinkedTo field, positions entity at that waypoint.
        /// Otherwise, entity position is preserved and projected to target area walkmesh.
        /// </remarks>
        private void ResolveAndSetTransitionPosition(IEntity entity, IWorld world, IArea targetArea)
        {
            if (entity == null || world == null || targetArea == null)
            {
                return;
            }

            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Try to find transition source (door/trigger) that triggered this transition
            // This would typically be stored in event data, but for now we'll use current position
            // In a full implementation, the source entity would be passed through event data
            // and we'd check its LinkedTo field to position at waypoint

            // For now, position is preserved and will be projected to walkmesh in ProjectEntityToTargetArea
        }

        /// <summary>
        /// Projects an entity's position to the target area's walkmesh.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Walkmesh projection system (FUN_004f5070 @ 0x004f5070)
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
        /// Based on swkotor2.exe: Walkmesh search system
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
        /// Based on swkotor2.exe: Entity removal from area
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
        /// Based on swkotor2.exe: Entity addition to area
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
        /// Based on swkotor2.exe: Area transition event system
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
            // Based on swkotor2.exe: Area enter script execution
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
        /// </remarks>
        protected override void HandleObjectEvent(IEntity entity, int eventType)
        {
            string eventName = GetEventName(eventType);
            string entityInfo = entity != null ? $"{entity.Tag ?? "null"} ({entity.ObjectId})" : "null";
            Console.WriteLine($"[OdysseyEventDispatcher] HandleObjectEvent: Handling {eventName} ({eventType}) for entity {entityInfo}");
            // TODO: Implement object event handling
            // Update door/placeable state based on event type
            // Trigger associated scripts and effects
            // Update visual representation
        }

        /// <summary>
        /// Handles combat-related events.
        /// </summary>
        /// <param name="entity">The entity receiving the combat event.</param>
        /// <param name="eventType">The combat event type (EVENT_ON_MELEE_ATTACKED, EVENT_DESTROY_OBJECT, etc.).</param>
        /// <param name="sourceEntity">The entity that triggered the combat event (attacker, damager, etc.). May be null if not available.</param>
        /// <remarks>
        /// Based on swkotor2.exe: DispatchEvent @ 0x004dcfb0 handles combat events by firing appropriate script events.
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
                    // Based on swkotor2.exe: EVENT_ON_MELEE_ATTACKED fires OnMeleeAttacked script
                    // Located via string references: "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4 (case 0xf), "OnMeleeAttacked" @ 0x007c1a5c, "ScriptAttacked" @ 0x007bee80
                    // Original implementation: EVENT_ON_MELEE_ATTACKED fires on target entity when attacked (before damage is applied)
                    // Script fires regardless of hit/miss - this allows scripts to react to being targeted
                    // Source entity (attacker) is passed as triggerer to script execution context
                    // Scripts can use GetLastAttacker() to retrieve the attacker if sourceEntity is null
                    HandleMeleeAttackedEvent(entity, sourceEntity, world);
                    break;

                case 0xb: // EVENT_DESTROY_OBJECT (swkotor2.exe: 0x004dcfb0 case 0xb, line 77)
                    // Handle object destruction or entity death
                    // Based on swkotor2.exe: EVENT_DESTROY_OBJECT can indicate entity death or object destruction
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
                    // Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
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
        /// Based on swkotor2.exe: EVENT_ON_MELEE_ATTACKED fires OnPhysicalAttacked script
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0 case 0xf, line 89)
        /// </remarks>
        private void HandleMeleeAttackedEvent(IEntity entity, IEntity attacker, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleMeleeAttackedEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) attacked by {attacker?.Tag ?? "null"} ({attacker?.ObjectId ?? 0})");

            // Fire OnPhysicalAttacked script event on target entity
            // Based on swkotor2.exe: EVENT_ON_MELEE_ATTACKED fires OnMeleeAttacked script
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
        /// Based on swkotor2.exe: EVENT_DESTROY_OBJECT handles entity death or object destruction
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
                // Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH fires when entity dies
                // Located via string references: "OnDeath" script field, death event handling
                // Original implementation: OnDeath script fires on victim entity with killer as triggerer
                world.EventBus.FireScriptEvent(entity, Core.Enums.ScriptEvent.OnDeath, killer);
                Console.WriteLine($"[OdysseyEventDispatcher] HandleDestroyObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is dead, fired OnDeath script event");
            }
            else
            {
                // Entity is not dead - this is object destruction (door, placeable, etc.)
                // Handle object destruction cleanup
                // Based on swkotor2.exe: Object destruction removes entity from world and cleans up components
                // For doors/placeables: May fire OnDestroy script if available
                Console.WriteLine($"[OdysseyEventDispatcher] HandleDestroyObjectEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) is not dead, handling object destruction");
                
                // Check if entity has OnDestroy script (if supported by object type)
                // For now, object destruction is handled by the entity's destruction system
                // Future: May fire OnDestroy script event if entity type supports it
            }
        }

        /// <summary>
        /// Handles ON_DAMAGED combat event (EVENT_SIGNAL_EVENT with eventSubtype 4).
        /// </summary>
        /// <param name="entity">The entity being damaged.</param>
        /// <param name="damager">The entity that damaged (may be null).</param>
        /// <param name="world">The world containing the entities.</param>
        /// <remarks>
        /// Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
        /// (swkotor2.exe: DispatchEvent @ 0x004dcfb0, eventSubtype 4, line 145)
        /// </remarks>
        private void HandleDamagedEvent(IEntity entity, IEntity damager, IWorld world)
        {
            Console.WriteLine($"[OdysseyEventDispatcher] HandleDamagedEvent: Entity {entity.Tag ?? "null"} ({entity.ObjectId}) damaged by {damager?.Tag ?? "null"} ({damager?.ObjectId ?? 0})");

            // Fire OnDamaged script event on target entity
            // Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
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
            // Based on swkotor2.exe: DispatchEvent @ 0x004dcfb0 lines 132-246
            // Maps CSWSSCRIPTEVENT_EVENTTYPE_ON_* constants to ScriptEvent enum values
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
        /// Based on swkotor2.exe: DispatchEvent @ 0x004dcfb0 lines 132-246.
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
        private Core.Enums.ScriptEvent MapEventSubtypeToScriptEvent(int eventSubtype)
        {
            switch (eventSubtype)
            {
                case 0x0: return Core.Enums.ScriptEvent.OnHeartbeat; // CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT
                case 0x1: return Core.Enums.ScriptEvent.OnPerception; // CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION
                case 0x2: return Core.Enums.ScriptEvent.OnSpellCastAt; // CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT
                    // case 0x3??
                case 0x4: return Core.Enums.ScriptEvent.OnDamaged; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED
                case 0x5: return Core.Enums.ScriptEvent.OnDisturbed; // CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED
                    // case 0x6??
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
                case 0x1b: return Core.Enums.ScriptEvent.OnDisturbed; // CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED (same as 5, but different context)
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
}
