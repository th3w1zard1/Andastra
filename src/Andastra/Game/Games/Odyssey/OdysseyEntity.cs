using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Andastra.Parsing.Formats.GFF;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Odyssey.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Odyssey
{
    /// <summary>
    /// Odyssey Engine (KotOR/KotOR2) specific entity implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Entity Implementation:
    /// - Based on swkotor.exe and swkotor2.exe entity systems
    /// - Implements ObjectId, Tag, ObjectType structure
    /// - Component-based architecture for modular functionality
    /// - Script hooks for events and behaviors
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Entity creation and management
    ///   - ObjectId string reference: "ObjectId" @ 0x00744c24
    ///   - ObjectIDList string reference: "ObjectIDList" @ 0x007465cc
    ///   - Object events: "EVENT_DESTROY_OBJECT" @ 0x00744b10, "EVENT_OPEN_OBJECT" @ 0x00744b68, "EVENT_CLOSE_OBJECT" @ 0x00744b7c
    ///   - "EVENT_LOCK_OBJECT" @ 0x00744ae8, "EVENT_UNLOCK_OBJECT" @ 0x00744afc
    /// - swkotor2.exe: ObjectId at offset +4, FUN_004e28c0 (save), FUN_005fb0f0 (load)
    ///   - ObjectId string reference: "ObjectId" @ 0x007bce5c
    ///   - ObjectIDList string reference: "ObjectIDList" @ 0x007bfd7c
    ///   - Object logging format: "OID: %08x, Tag: %s, %s" @ 0x007c76b8 used for debug/error logging
    ///   - Object list handling: "ObjectList" @ 0x007bfdbc, "ObjectValue" @ 0x007bfd70
    ///   - Entity serialization: FUN_004e28c0 @ 0x004e28c0 saves Creature List with ObjectId fields (offset +4 in object structure)
    ///   - Entity deserialization: FUN_005fb0f0 @ 0x005fb0f0 loads creature data from GFF, reads ObjectId at offset +4
    ///   - AreaId: FUN_005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90, "AreaId" @ 0x007bef48
    /// - Entity structure: ObjectId (uint32) at offset +4, Tag (string), ObjectType (enum), AreaId (uint32)
    /// - Component system: Transform, stats, inventory, script hooks, etc.
    ///
    /// Entity lifecycle:
    /// - Created from GIT file templates or script instantiation
    /// - Assigned sequential ObjectId for uniqueness (OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001)
    /// - Components attached based on ObjectType
    /// - Registered with area and world systems
    /// - Updated each frame, destroyed when no longer needed
    /// </remarks>
    [PublicAPI]
    public class OdysseyEntity : BaseEntity
    {
        private uint _objectId;
        private string _tag;
        private readonly ObjectType _objectType;
        private IWorld _world;
        private bool _isValid = true;
        private uint _areaId;
        private string _displayName;

        /// <summary>
        /// Creates a new Odyssey entity.
        /// </summary>
        /// <param name="objectId">Unique object identifier.</param>
        /// <param name="objectType">The type of object this entity represents.</param>
        /// <param name="tag">Tag string for script lookups.</param>
        /// <remarks>
        /// Based on entity creation in swkotor2.exe.
        /// ObjectId must be unique within the game session.
        /// ObjectType determines available components and behaviors.
        /// </remarks>
        public OdysseyEntity(uint objectId, ObjectType objectType, string tag = null)
        {
            _objectId = objectId;
            _objectType = objectType;
            _tag = tag ?? string.Empty;

            Initialize();
        }

        /// <summary>
        /// Unique object ID for this entity.
        /// </summary>
        /// <remarks>
        /// Based on ObjectId field at offset +4 in entity structure.
        /// Located via string references: "ObjectId" @ 0x007bce5c (swkotor2.exe), "ObjectId" @ 0x00744c24 (swkotor.exe)
        /// Assigned sequentially and must be unique across all entities.
        /// Used for script references and save game serialization.
        /// OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001
        /// </remarks>
        public override uint ObjectId => _objectId;

        /// <summary>
        /// Tag string for script lookups.
        /// </summary>
        /// <remarks>
        /// Script-accessible identifier for GetObjectByTag functions.
        /// Located via string references in swkotor.exe and swkotor2.exe (various locations).
        /// Object logging format: "OID: %08x, Tag: %s, %s" @ 0x007c76b8 (swkotor2.exe) used for debug/error logging
        /// Can be changed at runtime for dynamic lookups.
        /// </remarks>
        public override string Tag
        {
            get => _tag;
            set => _tag = value ?? string.Empty;
        }

        /// <summary>
        /// The type of this object.
        /// </summary>
        /// <remarks>
        /// Determines available components and behaviors.
        /// Cannot be changed after entity creation.
        /// </remarks>
        public override ObjectType ObjectType => _objectType;

        /// <summary>
        /// Whether this entity is valid and not destroyed.
        /// </summary>
        /// <remarks>
        /// Entity validity prevents use-after-free issues.
        /// Becomes invalid when Destroy() is called.
        /// </remarks>
        public override bool IsValid => _isValid;

        /// <summary>
        /// The world this entity belongs to.
        /// </summary>
        /// <remarks>
        /// Reference to containing world for cross-entity operations.
        /// Set when entity is added to an area.
        /// </remarks>
        public override IWorld World
        {
            get => _world;
            set => _world = value;
        }

        /// <summary>
        /// Gets or sets the area ID this entity belongs to.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// AreaId identifies which area the entity is located in.
        /// Set when entity is registered to an area in the world.
        /// </remarks>
        public override uint AreaId
        {
            get => _areaId;
            set => _areaId = value;
        }

        /// <summary>
        /// Gets or sets the display name for this entity.
        /// </summary>
        /// <remarks>
        /// Display name is used for UI display and can be set from template data or GIT instances.
        /// Based on swkotor2.exe: Entity names are stored in templates and can be overridden by GIT instances.
        /// </remarks>
        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        /// <summary>
        /// Initializes the entity after creation.
        /// </summary>
        /// <remarks>
        /// Attaches default components based on ObjectType.
        /// Registers with necessary systems.
        /// Called automatically in constructor.
        /// </remarks>
        protected override void Initialize()
        {
            // Attach components based on object type
            switch (_objectType)
            {
                case ObjectType.Creature:
                    AttachCreatureComponents();
                    break;
                case ObjectType.Door:
                    AttachDoorComponents();
                    break;
                case ObjectType.Placeable:
                    AttachPlaceableComponents();
                    break;
                case ObjectType.Trigger:
                    AttachTriggerComponents();
                    break;
                case ObjectType.Waypoint:
                    AttachWaypointComponents();
                    break;
                case ObjectType.Sound:
                    AttachSoundComponents();
                    break;
            }

            // All entities get transform and script hooks
            AttachCommonComponents();
        }

        /// <summary>
        /// Attaches components common to all entity types.
        /// </summary>
        /// <remarks>
        /// Common components attached to all entities:
        /// - TransformComponent: Position, orientation, scale for all entities
        /// - ScriptHooksComponent: Script event hooks and local variables for all entities
        ///
        /// Based on swkotor2.exe: All entities have transform and script hooks capability.
        /// Script hooks are loaded from GFF templates (UTC, UTD, UTP, etc.) and can be
        /// set at runtime via SetScript functions.
        /// </remarks>
        private void AttachCommonComponents()
        {
            // Attach transform component for all entities
            // Based on swkotor.exe and swkotor2.exe: All entities have transform data (position, orientation, scale)
            // Transform data is loaded from GIT files (FUN_004e08e0 @ 0x004e08e0 loads placeable/door position from GIT)
            // Position stored as XPosition, YPosition, ZPosition in GFF structures
            // Orientation stored as XOrientation, YOrientation, ZOrientation in GFF structures
            if (!HasComponent<ITransformComponent>())
            {
                var transformComponent = new TransformComponent();
                AddComponent<ITransformComponent>(transformComponent);
            }

            // Attach script hooks component for all entities
            // Based on swkotor2.exe: All entities support script hooks (ScriptHeartbeat, ScriptOnNotice, etc.)
            // ComponentInitializer also attaches this, but we ensure it's attached here for consistency
            // Script hooks are loaded from GFF templates and can be set/modified at runtime
            if (!HasComponent<IScriptHooksComponent>())
            {
                var scriptHooksComponent = new ScriptHooksComponent();
                AddComponent<IScriptHooksComponent>(scriptHooksComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to creatures.
        /// </summary>
        /// <remarks>
        /// Creatures have stats, inventory, combat capabilities, etc.
        /// Based on creature component structure in swkotor.exe and swkotor2.exe.
        ///
        /// Component attachment pattern:
        /// - Based on swkotor.exe and swkotor2.exe: Creature components are attached during entity creation from UTC templates
        /// - ComponentInitializer also handles this, but we ensure it's attached here for consistency
        /// - Component provides: Stats (HP, abilities, skills, saves), Inventory (equipped items and inventory bag),
        ///   Faction (hostility relationships), QuickSlots (quick-use items/abilities), Creature (appearance, classes, feats, force powers)
        /// - Odyssey-specific: Uses CreatureComponent, StatsComponent, InventoryComponent, QuickSlotComponent, OdysseyFactionComponent
        /// - Component initialization: Properties loaded from entity template files (UTC) and can be modified at runtime
        ///
        /// Based on reverse engineering of:
        /// - swkotor.exe: Creature initialization (FUN_004af630 @ 0x004af630 handles creature events)
        /// - swkotor2.exe: FUN_005261b0 @ 0x005261b0 loads creature from UTC template
        ///   - Calls FUN_005fb0f0 @ 0x005fb0f0 to load creature data from GFF
        ///   - Calls FUN_0050c510 @ 0x0050c510 to load script hooks
        ///   - Calls FUN_00521d40 @ 0x00521d40 to initialize equipment and items
        /// - FUN_004dfbb0 @ 0x004dfbb0 loads creature instances from GIT "Creature List"
        /// - Located via string references: "Creature List" @ 0x007bd01c (swkotor2.exe), "CreatureList" @ 0x007c0c80 (swkotor2.exe)
        /// - Component attachment: Components are attached during entity creation from GIT instances and UTC templates
        /// - ComponentInitializer @ Odyssey/Systems/ComponentInitializer.cs attaches these components
        ///
        /// Cross-engine analysis:
        /// - Odyssey (swkotor.exe, swkotor2.exe): Uses CreatureComponent, StatsComponent, InventoryComponent, QuickSlotComponent, OdysseyFactionComponent
        /// - Aurora (nwmain.exe, nwn2main.exe): Similar component structure with AuroraCreatureComponent, StatsComponent, InventoryComponent, AuroraFactionComponent
        /// - Eclipse (daorigins.exe, DragonAge2.exe): Enhanced component system with StatsComponent, InventoryComponent, EclipseFactionComponent, EclipseAnimationComponent
        /// - Infinity (, ): Streamlined component system (to be reverse engineered)
        /// </remarks>
        private void AttachCreatureComponents()
        {
            // Attach creature component if not already present
            // Based on swkotor.exe and swkotor2.exe: Creature component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            // Component provides: TemplateResRef, Tag, Conversation, Appearance, Classes, Feats, KnownPowers, EquippedItems
            if (!HasComponent<CreatureComponent>())
            {
                var creatureComponent = new CreatureComponent();
                creatureComponent.Owner = this;
                AddComponent<CreatureComponent>(creatureComponent);
            }

            // Attach stats component if not already present
            // Based on swkotor.exe and swkotor2.exe: Stats component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            // Component provides: CurrentHP, MaxHP, CurrentFP, MaxFP, Abilities (STR, DEX, CON, INT, WIS, CHA), Skills, Saves, BAB, AC, Level
            if (!HasComponent<IStatsComponent>())
            {
                var statsComponent = new StatsComponent();
                AddComponent<IStatsComponent>(statsComponent);
            }

            // Attach inventory component if not already present
            // Based on swkotor.exe and swkotor2.exe: Inventory component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            // Component provides: Equipped items (slots 0-17), Inventory bag (slots 18+), GetItemInSlot, AddItem, RemoveItem, EquipItem, UnequipItem
            if (!HasComponent<IInventoryComponent>())
            {
                var inventoryComponent = new InventoryComponent(this);
                AddComponent<IInventoryComponent>(inventoryComponent);
            }

            // Attach quick slot component if not already present
            // Based on swkotor.exe and swkotor2.exe: Quick slot component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            // Component provides: Quick slots 0-11 (12 slots total) for storing items or abilities (spells/feats) for quick use
            if (!HasComponent<IQuickSlotComponent>())
            {
                var quickSlotComponent = new QuickSlotComponent(this);
                AddComponent<IQuickSlotComponent>(quickSlotComponent);
            }

            // Attach faction component if not already present
            // Based on swkotor.exe and swkotor2.exe: Faction component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            // Component provides: FactionId, IsHostile, GetReputation, SetReputation, TemporaryHostileTargets
            // Set FactionID from entity data if available (loaded from UTC template)
            if (!HasComponent<IFactionComponent>())
            {
                var factionComponent = new OdysseyFactionComponent();
                factionComponent.Owner = this;
                // Set FactionID from entity data if available (loaded from UTC template)
                // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 loads FactionID from GFF at offset in creature structure
                // Located via string references: "FactionID" @ 0x007c40b4 (swkotor2.exe) / 0x0074ae48 (swkotor.exe)
                if (GetData("FactionID") is int factionId)
                {
                    factionComponent.FactionId = factionId;
                }
                AddComponent<IFactionComponent>(factionComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to doors.
        /// </summary>
        /// <remarks>
        /// Doors have open/close state, lock state, transition logic.
        /// Based on door component structure in swkotor2.exe.
        /// - FUN_005838d0 @ 0x005838d0: Door initialization from GIT/GFF
        /// - FUN_00580ed0 @ 0x00580ed0: Door loading function that loads door properties
        /// - Door component attached during entity creation from UTD templates
        /// - Doors support: open/closed states, locks, traps, module/area transitions
        /// - Component provides: IsOpen, IsLocked, LockDC, KeyTag, LinkedTo, LinkedToModule
        /// </remarks>
        private void AttachDoorComponents()
        {
            // Attach door component if not already present
            // Based on swkotor2.exe: Door component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            if (!HasComponent<IDoorComponent>())
            {
                var doorComponent = new OdysseyDoorComponent();
                doorComponent.Owner = this;
                AddComponent<IDoorComponent>(doorComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to placeables.
        /// </summary>
        /// <remarks>
        /// Placeables have interaction state, inventory, use logic.
        /// Based on placeable component structure in swkotor2.exe.
        /// - LoadPlaceableFromGFF @ 0x00588010 (swkotor2.exe) - Loads placeable data from GIT GFF into placeable object
        ///   - Located via string reference: "Placeable List" @ 0x007bd260 (GFF list field in GIT)
        ///   - Reads Tag, TemplateResRef, LocName, AutoRemoveKey, Faction, Invulnerable, Plot, NotBlastable, Min1HP, PartyInteract, OpenLockDC, OpenLockDiff, OpenLockDiffMod, KeyName, TrapDisarmable, TrapDetectable, DisarmDC, TrapDetectDC, OwnerDemolitionsSkill, TrapFlag, TrapOneShot, TrapType, Useable, Static, Appearance, UseTweakColor, TweakColor, HP, CurrentHP, and other placeable properties from GFF
        /// - SavePlaceableToGFF @ 0x00589520 (swkotor2.exe) - Saves placeable data to GFF save data
        ///   - Located via string reference: "Placeable List" @ 0x007bd260
        ///   - Writes Tag, LocName, AutoRemoveKey, Faction, Plot, NotBlastable, Min1HP, OpenLockDC, OpenLockDiff, OpenLockDiffMod, KeyName, TrapDisarmable, TrapDetectable, DisarmDC, TrapDetectDC, OwnerDemolitionsSkill, TrapFlag, TrapOneShot, TrapType, Useable, Static, GroundPile, Appearance, UseTweakColor, TweakColor, HP, CurrentHP, Hardness, Fort, Will, Ref, Lockable, Locked, HasInventory, KeyRequired, CloseLockDC, Open, PartyInteract, Portrait, Conversation, BodyBag, DieWhenEmpty, LightState, Description, OnClosed, OnDamaged, OnDeath, OnDisarm, OnHeartbeat, OnInvDisturbed, OnLock, OnMeleeAttacked, OnOpen, OnSpellCastAt, OnUnlock, OnUsed, OnUserDefined, OnDialog, OnEndDialogue, OnTrapTriggered, OnFailToOpen, Animation, ItemList (ObjectId) for each item in placeable inventory, Bearing, position (X, Y, Z), IsBodyBag, IsBodyBagVisible, IsCorpse, PCLevel
        /// - Original implementation: FUN_004e08e0 @ 0x004e08e0 (load placeable instances from GIT)
        /// - Placeables have appearance, useability, locks, inventory, HP, traps
        /// - Based on UTP file format (GFF with "UTP " signature)
        /// - Script events: OnUsed (CSWSSCRIPTEVENT_EVENTTYPE_ON_USED @ 0x007bc7d8, 0x19), OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
        /// - Containers (HasInventory=true) can store items, open/close states (AnimationState 0=closed, 1=open)
        /// - Lock system: KeyRequired flag, KeyName tag, LockDC difficulty class (checked via Security skill)
        /// - Use distance: ~2.0 units (InteractRange), checked before OnUsed script fires
        /// - Odyssey-specific: Fort/Will/Ref saves, BodyBag, Plot flag, FactionId, AppearanceType, trap system
        ///
        /// Component attachment pattern:
        /// - Based on swkotor2.exe: Placeable components are attached during entity creation from GIT templates
        /// - ComponentInitializer also handles this, but we ensure it's attached here for consistency
        /// - Component provides: IsUseable, HasInventory, IsStatic, IsOpen, IsLocked, LockDC, KeyTag, HitPoints, MaxHitPoints, Hardness, AnimationState, Conversation
        /// - Odyssey-specific properties: AppearanceType, CurrentHP, MaxHP, Fort, Will, Reflex, KeyRequired, KeyName, IsContainer, FactionId, BodyBag, Plot
        /// </remarks>
        private void AttachPlaceableComponents()
        {
            // Attach placeable component if not already present
            // Based on swkotor2.exe: Placeable component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            if (!HasComponent<IPlaceableComponent>())
            {
                var placeableComponent = new PlaceableComponent();
                placeableComponent.Owner = this;
                AddComponent<IPlaceableComponent>(placeableComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to triggers.
        /// </summary>
        /// <remarks>
        /// Triggers have enter/exit detection, script firing.
        /// Based on trigger component structure in swkotor.exe and swkotor2.exe.
        /// - FUN_004e5920 @ 0x004e5920 (swkotor2.exe) loads trigger instances from GIT TriggerList, reads UTT templates
        ///   - Function signature: `undefined4 FUN_004e5920(void *param_1, uint *param_2, int param_3, int param_4)`
        ///   - Reads "TriggerList" list from GFF structure
        ///   - For each trigger entry in TriggerList:
        ///     - Checks trigger type (must be type 1 = Trigger)
        ///     - Reads ObjectId (uint32) via "ObjectId" field name (default 0x7f000000)
        ///     - Reads Tag, TemplateResRef, LinkedTo, LinkedToModule, LinkedToFlags, TransitionDestination
        ///     - Reads Geometry vertices from GIT Geometry field
        ///     - Reads trap properties: TrapFlag, TrapType, TrapDetectable, TrapDetectDC, TrapDisarmable, TrapDisarmDC, TrapOneShot
        ///     - Reads script hooks: OnEnter, OnExit, OnHeartbeat, OnClick, OnDisarm, OnTrapTriggered
        /// - Located via string references: "Trigger" @ 0x007bc51c, "TriggerList" @ 0x007bd254 (swkotor2.exe)
        /// - "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "EVENT_LEFT_TRIGGER" @ 0x007bcdf4 (swkotor2.exe)
        /// - "OnTrapTriggered" @ 0x007c1a34, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED" @ 0x007bc7ac (swkotor2.exe)
        /// - Transition fields: "LinkedTo" @ 0x007bd798, "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788 (swkotor2.exe)
        /// - "TransitionDestination" @ 0x007bd7a4 (waypoint tag for positioning after transition) (swkotor2.exe)
        /// - Original implementation: UTT (Trigger) GFF templates define trigger properties and geometry
        /// - Triggers are invisible polygonal volumes that fire scripts on enter/exit
        /// - Trigger types: Generic (0), Transition (1), Trap (2)
        /// - Transition triggers: LinkedTo, LinkedToModule, LinkedToFlags for area/module transitions
        /// - Trap triggers: OnTrapTriggered script fires when trap is activated
        /// - Geometry: Triggers have polygon geometry (Geometry field in GIT) defining trigger volume
        /// - ComponentInitializer also handles this, but we ensure it's attached here for consistency
        /// - Component provides: Geometry, IsEnabled, TriggerType, LinkedTo, LinkedToModule, IsTrap, TrapActive, TrapDetected, TrapDisarmed, TrapDetectDC, TrapDisarmDC, FireOnce, HasFired, ContainsPoint, ContainsEntity
        /// </remarks>
        private void AttachTriggerComponents()
        {
            // Attach trigger component if not already present
            // Based on swkotor.exe and swkotor2.exe: Trigger component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            if (!HasComponent<ITriggerComponent>())
            {
                var triggerComponent = new TriggerComponent();
                triggerComponent.Owner = this;
                AddComponent<ITriggerComponent>(triggerComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to waypoints.
        /// </summary>
        /// <remarks>
        /// Waypoints have position data, pathfinding integration.
        /// Based on waypoint component structure in swkotor2.exe.
        /// FUN_004e08e0 @ 0x004e08e0 loads waypoint instances from GIT
        /// </remarks>
        private void AttachWaypointComponents()
        {
            if (!HasComponent<IWaypointComponent>())
            {
                var waypointComponent = new OdysseyWaypointComponent();
                AddComponent<IWaypointComponent>(waypointComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to sounds.
        /// </summary>
        /// <remarks>
        /// Sounds have audio playback, spatial positioning.
        /// Based on sound component structure in swkotor.exe and swkotor2.exe.
        /// - FUN_004e08e0 @ 0x004e08e0 (swkotor2.exe) loads sound instances from GIT SoundList
        ///   - Located via string reference: "SoundList" @ 0x007bd080 (GIT sound list), "Sound" @ 0x007bc500 (sound entity type)
        ///   - Reads ObjectId, Tag, TemplateResRef, position (XPosition, YPosition, ZPosition)
        ///   - Reads sound properties: Active, Continuous, Looping, Positional, Random, RandomPosition
        ///   - Reads volume and distance: Volume, VolumeVrtn, MaxDistance, MinDistance
        ///   - Reads timing: Interval, IntervalVrtn, PitchVariation
        ///   - Reads sound files: Sounds list containing Sound ResRefs
        ///   - Reads hours: Hours bitmask for time-based activation
        /// - Sound component attached during entity creation from GIT instances
        /// - ComponentInitializer also handles this, but we ensure it's attached here for consistency
        /// - Component provides: Active, Continuous, Looping, Positional, Random, RandomPosition, Volume, VolumeVrtn, MaxDistance, MinDistance, Interval, IntervalVrtn, PitchVariation, SoundFiles, Hours, IsPlaying, TimeSinceLastPlay
        /// - Based on UTS file format: GFF with "UTS " signature containing sound template data
        /// - Sound entities emit positional audio in the game world (Positional field for 3D audio)
        /// - Volume: 0-127 range (Volume field), distance falloff: MinDistance (full volume) to MaxDistance (zero volume)
        /// - Continuous sounds: Play continuously when active (Continuous field)
        /// - Random sounds: Can play random sounds from SoundFiles list (Random field), randomize position (RandomPosition field)
        /// - Interval: Time between plays for non-looping sounds (Interval field, IntervalVrtn for variation)
        /// - Volume variation: VolumeVrtn field for random volume variation
        /// - Hours: Bitmask for time-based activation (Hours field, 0-23 hour range)
        /// - Pitch variation: PitchVariation field for random pitch variation in sound playback
        /// </remarks>
        private void AttachSoundComponents()
        {
            // Attach sound component if not already present
            // Based on swkotor.exe and swkotor2.exe: Sound component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            if (!HasComponent<ISoundComponent>())
            {
                var soundComponent = new Components.SoundComponent();
                soundComponent.Owner = this;

                // Initialize sound component properties from entity data if available (loaded from GIT)
                // Based on EntityFactory.CreateSoundFromGit: Sound properties are stored in entity data
                if (GetData("Active") is bool active)
                {
                    soundComponent.Active = active;
                }
                if (GetData("Continuous") is bool continuous)
                {
                    soundComponent.Continuous = continuous;
                }
                if (GetData("Looping") is bool looping)
                {
                    soundComponent.Looping = looping;
                }
                if (GetData("Positional") is bool positional)
                {
                    soundComponent.Positional = positional;
                }
                if (GetData("Random") is bool random)
                {
                    soundComponent.Random = random;
                }
                if (GetData("RandomPosition") is bool randomPosition)
                {
                    soundComponent.RandomPosition = randomPosition;
                }
                if (GetData("Volume") is int volume)
                {
                    soundComponent.Volume = volume;
                }
                if (GetData("VolumeVrtn") is int volumeVrtn)
                {
                    soundComponent.VolumeVrtn = volumeVrtn;
                }
                if (GetData("MaxDistance") is float maxDistance)
                {
                    soundComponent.MaxDistance = maxDistance;
                }
                if (GetData("MinDistance") is float minDistance)
                {
                    soundComponent.MinDistance = minDistance;
                }
                if (GetData("Sounds") is List<string> sounds)
                {
                    soundComponent.SoundFiles = sounds;
                }

                AddComponent<ISoundComponent>(soundComponent);
            }
        }

        /// <summary>
        /// Updates the entity each frame.
        /// </summary>
        /// <remarks>
        /// Updates all attached components.
        /// Processes any pending script events.
        /// Handles component interactions.
        ///
        /// Based on swkotor2.exe: Entity update loop processes components in dependency order.
        /// Component update order:
        /// 1. TransformComponent (position, orientation updates)
        /// 2. ActionQueueComponent (action execution, may modify transform)
        /// 3. StatsComponent (HP regeneration, stat updates)
        /// 4. PerceptionComponent (perception checks, uses transform position)
        /// 5. Other components (in arbitrary order)
        ///
        /// Component interactions:
        /// - Transform changes trigger perception updates
        /// - HP changes trigger death state updates
        /// - Action queue execution may modify transform
        /// - Inventory changes affect encumbrance and movement speed
        /// </remarks>
        public override void Update(float deltaTime)
        {
            if (!IsValid)
                return;

            // Update components in dependency order
            // 1. TransformComponent first (position/orientation)
            var transformComponent = GetComponent<ITransformComponent>();
            if (transformComponent is IUpdatableComponent updatableTransform)
            {
                updatableTransform.Update(deltaTime);
            }

            // 2. ActionQueueComponent (may modify transform through movement actions)
            var actionQueueComponent = GetComponent<IActionQueueComponent>();
            if (actionQueueComponent != null)
            {
                actionQueueComponent.Update(this, deltaTime);
            }

            // 3. StatsComponent (HP regeneration, stat updates)
            var statsComponent = GetComponent<IStatsComponent>();
            if (statsComponent is IUpdatableComponent updatableStats)
            {
                updatableStats.Update(deltaTime);
            }

            // 4. PerceptionComponent (uses transform position)
            var perceptionComponent = GetComponent<IPerceptionComponent>();
            if (perceptionComponent is IUpdatableComponent updatablePerception)
            {
                updatablePerception.Update(deltaTime);
            }

            // 5. Other components (in arbitrary order)
            foreach (var component in GetAllComponents())
            {
                // Skip already-updated components
                if (component == transformComponent ||
                    component == actionQueueComponent ||
                    component == statsComponent ||
                    component == perceptionComponent)
                {
                    continue;
                }

                if (component is IUpdatableComponent updatable)
                {
                    updatable.Update(deltaTime);
                }
            }

            // Handle component interactions after all components are updated
            HandleComponentInteractions(deltaTime);

            // Process script events and hooks
            // Script events are processed by the game loop, not here
            // But we could fire heartbeat events here if needed
        }

        /// <summary>
        /// Destroys the entity and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Removes from world and area systems.
        /// Cleans up all components and resources.
        /// Marks entity as invalid.
        ///
        /// Entity Destruction Sequence (Odyssey):
        /// 1. Mark entity as invalid (prevents further use)
        /// 2. Remove from area collections (if area is available)
        /// 3. Unregister from world collections (ObjectId, Tag, ObjectType indices)
        /// 4. Dispose all components that implement IDisposable
        /// 5. Clear all component references
        ///
        /// Based on swkotor2.exe: Entity destruction pattern
        /// - Located via string references: "EVENT_DESTROY_OBJECT" @ 0x00744b10 (destroy object event)
        /// - Original implementation: Entities are removed from all lookup indices when destroyed
        /// - World maintains indices: ObjectId dictionary, Tag dictionary, ObjectType dictionary, AllEntities list
        /// - Areas maintain indices: Type-specific lists (Creatures, Placeables, Doors, etc.), Tag dictionary
        /// - Entity cleanup: Components are disposed, resources freed, entity marked invalid
        ///
        /// Note: World.DestroyEntity calls UnregisterEntity before calling entity.Destroy(),
        /// but this method handles unregistration directly for safety and completeness.
        /// </remarks>
        public override void Destroy()
        {
            if (!IsValid)
                return;

            _isValid = false;

            // Remove from world and area collections
            if (_world != null)
            {
                // Remove from area collections first (if entity belongs to an area)
                // Based on swkotor2.exe: Entities belong to areas and must be removed from area collections
                // Located via string references: "AreaId" @ 0x007bef48, "EVENT_REMOVE_FROM_AREA" @ 0x007bcddc
                // Original implementation: Area.RemoveEntity removes entity from area's type-specific and tag collections
                if (_areaId != 0)
                {
                    IArea area = _world.GetArea(_areaId);
                    if (area != null)
                    {
                        // Remove entity from area's collections
                        // Based on swkotor2.exe: Area.RemoveEntity removes from type-specific lists and tag index
                        // RuntimeArea.RemoveEntity handles removal from Creatures, Placeables, Doors, Triggers, Waypoints, Sounds, Stores, Encounters lists
                        // and from tag index for GetEntityByTag lookups
                        if (area is Core.Module.RuntimeArea runtimeArea)
                        {
                            runtimeArea.RemoveEntity(this);
                        }
                    }
                }

                // Unregister from world collections
                // Based on swkotor2.exe: World.UnregisterEntity removes entity from all world lookup indices
                // Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c
                // Original implementation: UnregisterEntity removes from:
                // - _entitiesById dictionary (ObjectId lookup)
                // - _allEntities list (GetAllEntities enumeration)
                // - _entitiesByTag dictionary (Tag-based lookup, case-insensitive)
                // - _entitiesByType dictionary (ObjectType-based lookup)
                // This ensures entity is no longer accessible via GetEntity, GetEntityByTag, GetEntitiesOfType, GetAllEntities
                _world.UnregisterEntity(this);
            }

            // Clean up components
            // Based on swkotor2.exe: Component cleanup when entity is destroyed
            // Components that implement IDisposable are disposed to free resources
            // Component disposal order: Components are disposed in arbitrary order (no dependencies)
            foreach (var component in GetAllComponents())
            {
                if (component is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            // Clear component references
            // Based on swkotor2.exe: Component references are cleared after disposal
            // Component removal: All components are removed from entity to prevent access after destruction
            // Uses reflection to call RemoveComponent<T> for each component type
            var componentTypes = GetAllComponents().Select(c => c.GetType()).ToArray();
            foreach (var componentType in componentTypes)
            {
                // Remove component by type - this is a bit hacky but works
                var method = GetType().GetMethod("RemoveComponent")?.MakeGenericMethod(componentType);
                method?.Invoke(this, null);
            }
        }

        /// <summary>
        /// Serializes entity data for save games.
        /// </summary>
        /// <remarks>
        /// Based on FUN_004e28c0 @ 0x004e28c0 in swkotor2.exe.
        /// Serializes ObjectId, Tag, components, and custom data.
        /// Uses GFF format for structured data storage.
        ///
        /// Serialized data includes:
        /// - Basic entity properties (ObjectId, Tag, ObjectType, AreaId)
        /// - Transform component (position, facing, scale)
        /// - Stats component (HP, FP, abilities, skills, saves)
        /// - Door component (open/locked state, HP, transitions)
        /// - Placeable component (open/locked state, HP, useability)
        /// - Inventory component (equipped items and inventory bag)
        /// - Script hooks component (script ResRefs and local variables)
        /// - Custom data dictionary (arbitrary key-value pairs)
        /// </remarks>
        public override byte[] Serialize()
        {
            // Create GFF structure for entity data
            // Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 saves entity to GFF
            // Uses generic GFF format (GFFContent.GFF) for entity serialization
            var gff = new GFF(GFFContent.GFF);
            var root = gff.Root;

            // Serialize basic entity properties
            root.SetUInt32("ObjectId", _objectId);
            root.SetString("Tag", _tag ?? "");
            root.SetInt32("ObjectType", (int)_objectType);
            root.SetUInt32("AreaId", _areaId);
            root.SetUInt8("IsValid", _isValid ? (byte)1 : (byte)0);

            // Serialize transform component
            var transformComponent = GetComponent<ITransformComponent>();
            if (transformComponent != null)
            {
                root.SetSingle("X", transformComponent.Position.X);
                root.SetSingle("Y", transformComponent.Position.Y);
                root.SetSingle("Z", transformComponent.Position.Z);
                root.SetSingle("Facing", transformComponent.Facing);
                root.SetSingle("ScaleX", transformComponent.Scale.X);
                root.SetSingle("ScaleY", transformComponent.Scale.Y);
                root.SetSingle("ScaleZ", transformComponent.Scale.Z);

                // Serialize parent entity reference if present
                if (transformComponent.Parent != null)
                {
                    root.SetUInt32("ParentObjectId", transformComponent.Parent.ObjectId);
                }
            }

            // Serialize stats component (for creatures)
            var statsComponent = GetComponent<IStatsComponent>();
            if (statsComponent != null)
            {
                root.SetInt32("CurrentHP", statsComponent.CurrentHP);
                root.SetInt32("MaxHP", statsComponent.MaxHP);
                root.SetInt32("CurrentFP", statsComponent.CurrentFP);
                root.SetInt32("MaxFP", statsComponent.MaxFP);
                root.SetUInt8("IsDead", statsComponent.IsDead ? (byte)1 : (byte)0);
                root.SetInt32("BaseAttackBonus", statsComponent.BaseAttackBonus);
                root.SetInt32("ArmorClass", statsComponent.ArmorClass);
                root.SetInt32("FortitudeSave", statsComponent.FortitudeSave);
                root.SetInt32("ReflexSave", statsComponent.ReflexSave);
                root.SetInt32("WillSave", statsComponent.WillSave);
                root.SetSingle("WalkSpeed", statsComponent.WalkSpeed);
                root.SetSingle("RunSpeed", statsComponent.RunSpeed);
                root.SetInt32("Level", statsComponent.Level);

                // Serialize ability scores
                var abilityStruct = root.Acquire<GFFStruct>("Abilities", new GFFStruct());
                abilityStruct.SetInt32("STR", statsComponent.GetAbility(Ability.Strength));
                abilityStruct.SetInt32("DEX", statsComponent.GetAbility(Ability.Dexterity));
                abilityStruct.SetInt32("CON", statsComponent.GetAbility(Ability.Constitution));
                abilityStruct.SetInt32("INT", statsComponent.GetAbility(Ability.Intelligence));
                abilityStruct.SetInt32("WIS", statsComponent.GetAbility(Ability.Wisdom));
                abilityStruct.SetInt32("CHA", statsComponent.GetAbility(Ability.Charisma));

                // Serialize ability modifiers
                var abilityModStruct = root.Acquire<GFFStruct>("AbilityModifiers", new GFFStruct());
                abilityModStruct.SetInt32("STR", statsComponent.GetAbilityModifier(Ability.Strength));
                abilityModStruct.SetInt32("DEX", statsComponent.GetAbilityModifier(Ability.Dexterity));
                abilityModStruct.SetInt32("CON", statsComponent.GetAbilityModifier(Ability.Constitution));
                abilityModStruct.SetInt32("INT", statsComponent.GetAbilityModifier(Ability.Intelligence));
                abilityModStruct.SetInt32("WIS", statsComponent.GetAbilityModifier(Ability.Wisdom));
                abilityModStruct.SetInt32("CHA", statsComponent.GetAbilityModifier(Ability.Charisma));

                // Serialize known spells (if any)
                // Iterate through all known spells and serialize them as a list of structs
                // Each spell is stored as a GFFStruct with a "SpellId" field containing the spell ID (row index in spells.2da)
                var spellsList = root.Acquire<GFFList>("KnownSpells", new GFFList());
                foreach (int spellId in statsComponent.GetKnownSpells())
                {
                    var spellStruct = spellsList.Add();
                    spellStruct.SetInt32("SpellId", spellId);
                }
            }

            // Serialize door component
            var doorComponent = GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                root.SetUInt8("IsOpen", doorComponent.IsOpen ? (byte)1 : (byte)0);
                root.SetUInt8("IsLocked", doorComponent.IsLocked ? (byte)1 : (byte)0);
                root.SetUInt8("LockableByScript", doorComponent.LockableByScript ? (byte)1 : (byte)0);
                root.SetInt32("LockDC", doorComponent.LockDC);
                root.SetUInt8("IsBashed", doorComponent.IsBashed ? (byte)1 : (byte)0);
                root.SetInt32("HitPoints", doorComponent.HitPoints);
                root.SetInt32("MaxHitPoints", doorComponent.MaxHitPoints);
                root.SetInt32("Hardness", doorComponent.Hardness);
                root.SetString("KeyTag", doorComponent.KeyTag ?? "");
                root.SetUInt8("KeyRequired", doorComponent.KeyRequired ? (byte)1 : (byte)0);
                root.SetInt32("OpenState", doorComponent.OpenState);
                root.SetString("LinkedTo", doorComponent.LinkedTo ?? "");
                root.SetString("LinkedToModule", doorComponent.LinkedToModule ?? "");
            }

            // Serialize placeable component
            var placeableComponent = GetComponent<IPlaceableComponent>();
            if (placeableComponent != null)
            {
                root.SetUInt8("IsUseable", placeableComponent.IsUseable ? (byte)1 : (byte)0);
                root.SetUInt8("HasInventory", placeableComponent.HasInventory ? (byte)1 : (byte)0);
                root.SetUInt8("IsStatic", placeableComponent.IsStatic ? (byte)1 : (byte)0);
                root.SetUInt8("IsOpen", placeableComponent.IsOpen ? (byte)1 : (byte)0);
                root.SetUInt8("IsLocked", placeableComponent.IsLocked ? (byte)1 : (byte)0);
                root.SetInt32("LockDC", placeableComponent.LockDC);
                root.SetString("KeyTag", placeableComponent.KeyTag ?? "");
                root.SetInt32("HitPoints", placeableComponent.HitPoints);
                root.SetInt32("MaxHitPoints", placeableComponent.MaxHitPoints);
                root.SetInt32("Hardness", placeableComponent.Hardness);
                root.SetInt32("AnimationState", placeableComponent.AnimationState);
            }

            // Serialize sound component
            // Based on swkotor2.exe: Sound component data is serialized to GFF save data
            // Located via string references: "SoundList" @ 0x007bd080 (GIT sound list), "Sound" @ 0x007bc500 (sound entity type)
            // Sound properties are saved: Active, Continuous, Looping, Positional, Random, RandomPosition, Volume, VolumeVrtn, MaxDistance, MinDistance, Interval, IntervalVrtn, PitchVariation, SoundFiles, Hours
            var soundComponent = GetComponent<ISoundComponent>();
            if (soundComponent != null)
            {
                root.SetString("TemplateResRef", soundComponent.TemplateResRef ?? "");
                root.SetUInt8("Active", soundComponent.Active ? (byte)1 : (byte)0);
                root.SetUInt8("Continuous", soundComponent.Continuous ? (byte)1 : (byte)0);
                root.SetUInt8("Looping", soundComponent.Looping ? (byte)1 : (byte)0);
                root.SetUInt8("Positional", soundComponent.Positional ? (byte)1 : (byte)0);
                root.SetUInt8("RandomPosition", soundComponent.RandomPosition ? (byte)1 : (byte)0);
                root.SetUInt8("Random", soundComponent.Random ? (byte)1 : (byte)0);
                root.SetInt32("Volume", soundComponent.Volume);
                root.SetInt32("VolumeVrtn", soundComponent.VolumeVrtn);
                root.SetSingle("MaxDistance", soundComponent.MaxDistance);
                root.SetSingle("MinDistance", soundComponent.MinDistance);
                root.SetUInt32("Interval", soundComponent.Interval);
                root.SetUInt32("IntervalVrtn", soundComponent.IntervalVrtn);
                root.SetSingle("PitchVariation", soundComponent.PitchVariation);
                root.SetUInt32("Hours", soundComponent.Hours);
                root.SetSingle("TimeSinceLastPlay", soundComponent.TimeSinceLastPlay);
                root.SetUInt8("IsPlaying", soundComponent.IsPlaying ? (byte)1 : (byte)0);

                // Serialize sound files list
                if (soundComponent.SoundFiles != null && soundComponent.SoundFiles.Count > 0)
                {
                    var soundFilesList = root.Acquire<GFFList>("SoundFiles", new GFFList());
                    foreach (string soundFile in soundComponent.SoundFiles)
                    {
                        var soundFileStruct = soundFilesList.Add();
                        soundFileStruct.SetString("Sound", soundFile ?? "");
                    }
                }
            }

            // Serialize inventory component
            // Based on swkotor2.exe: Inventory items are serialized with their slot indices
            // Equipment slots: 0-19 (INVENTORY_SLOT_HEAD through INVENTORY_SLOT_LEFTWEAPON2)
            // Inventory bag: Typically starts at slot 20+ (varies by implementation)
            var inventoryComponent = GetComponent<IInventoryComponent>();
            if (inventoryComponent != null)
            {
                var inventoryList = root.Acquire<GFFList>("Inventory", new GFFList());

                // Search through all possible inventory slots (0-255 is a reasonable upper bound)
                // This ensures we capture all items including those in inventory bag slots
                for (int slot = 0; slot < 256; slot++)
                {
                    var item = inventoryComponent.GetItemInSlot(slot);
                    if (item != null)
                    {
                        var itemStruct = inventoryList.Add();
                        itemStruct.SetInt32("Slot", slot);
                        itemStruct.SetUInt32("ItemObjectId", item.ObjectId);
                        itemStruct.SetString("ItemTag", item.Tag ?? "");
                        itemStruct.SetInt32("ItemObjectType", (int)item.ObjectType);
                    }
                }
            }

            // Serialize script hooks component
            var scriptHooksComponent = GetComponent<IScriptHooksComponent>();
            if (scriptHooksComponent != null)
            {
                // Serialize script ResRefs for all event types
                var scriptsStruct = root.Acquire<GFFStruct>("Scripts", new GFFStruct());
                foreach (ScriptEvent eventType in System.Enum.GetValues(typeof(ScriptEvent)))
                {
                    string scriptResRef = scriptHooksComponent.GetScript(eventType);
                    if (!string.IsNullOrEmpty(scriptResRef))
                    {
                        scriptsStruct.SetString(eventType.ToString(), scriptResRef);
                    }
                }

                // Serialize local variables using reflection to access private dictionaries
                // Based on swkotor2.exe: Local variables are stored in ScriptHooksComponent
                // and serialized to GFF LocalVars structure
                var localVarsStruct = root.Acquire<GFFStruct>("LocalVariables", new GFFStruct());

                // Access private _localInts, _localFloats, _localStrings dictionaries via reflection
                Type componentType = scriptHooksComponent.GetType();
                FieldInfo localIntsField = componentType.GetField("_localInts", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo localFloatsField = componentType.GetField("_localFloats", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo localStringsField = componentType.GetField("_localStrings", BindingFlags.NonPublic | BindingFlags.Instance);

                if (localIntsField != null)
                {
                    var localInts = localIntsField.GetValue(scriptHooksComponent) as Dictionary<string, int>;
                    if (localInts != null && localInts.Count > 0)
                    {
                        var intList = localVarsStruct.Acquire<GFFList>("IntList", new GFFList());
                        foreach (var kvp in localInts)
                        {
                            var varStruct = intList.Add();
                            varStruct.SetString("Name", kvp.Key);
                            varStruct.SetInt32("Value", kvp.Value);
                        }
                    }
                }

                if (localFloatsField != null)
                {
                    var localFloats = localFloatsField.GetValue(scriptHooksComponent) as Dictionary<string, float>;
                    if (localFloats != null && localFloats.Count > 0)
                    {
                        var floatList = localVarsStruct.Acquire<GFFList>("FloatList", new GFFList());
                        foreach (var kvp in localFloats)
                        {
                            var varStruct = floatList.Add();
                            varStruct.SetString("Name", kvp.Key);
                            varStruct.SetSingle("Value", kvp.Value);
                        }
                    }
                }

                if (localStringsField != null)
                {
                    var localStrings = localStringsField.GetValue(scriptHooksComponent) as Dictionary<string, string>;
                    if (localStrings != null && localStrings.Count > 0)
                    {
                        var stringList = localVarsStruct.Acquire<GFFList>("StringList", new GFFList());
                        foreach (var kvp in localStrings)
                        {
                            var varStruct = stringList.Add();
                            varStruct.SetString("Name", kvp.Key);
                            varStruct.SetString("Value", kvp.Value ?? "");
                        }
                    }
                }
            }

            // Serialize custom data dictionary using reflection to access private _data field
            // BaseEntity stores custom data in _data dictionary for script variables and temporary state
            var customDataStruct = root.Acquire<GFFStruct>("CustomData", new GFFStruct());
            Type baseEntityType = typeof(BaseEntity);
            FieldInfo dataField = baseEntityType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);

            if (dataField != null)
            {
                var data = dataField.GetValue(this) as Dictionary<string, object>;
                if (data != null && data.Count > 0)
                {
                    var dataList = customDataStruct.Acquire<GFFList>("DataList", new GFFList());
                    foreach (var kvp in data)
                    {
                        var dataStruct = dataList.Add();
                        dataStruct.SetString("Key", kvp.Key);

                        // Serialize value based on type
                        if (kvp.Value == null)
                        {
                            dataStruct.SetString("Type", "null");
                            dataStruct.SetString("Value", "");
                        }
                        else
                        {
                            Type valueType = kvp.Value.GetType();
                            dataStruct.SetString("Type", valueType.Name);

                            if (valueType == typeof(int))
                            {
                                dataStruct.SetInt32("Value", (int)kvp.Value);
                            }
                            else if (valueType == typeof(float))
                            {
                                dataStruct.SetSingle("Value", (float)kvp.Value);
                            }
                            else if (valueType == typeof(string))
                            {
                                dataStruct.SetString("Value", (string)kvp.Value ?? "");
                            }
                            else if (valueType == typeof(bool))
                            {
                                dataStruct.SetUInt8("Value", (bool)kvp.Value ? (byte)1 : (byte)0);
                            }
                            else if (valueType == typeof(uint))
                            {
                                dataStruct.SetUInt32("Value", (uint)kvp.Value);
                            }
                            else
                            {
                                // For other types, serialize as string representation
                                dataStruct.SetString("Value", kvp.Value.ToString());
                            }
                        }
                    }
                }
            }

            // Convert GFF to byte array
            return gff.ToBytes();
        }

        /// <summary>
        /// Deserializes entity data from save games.
        /// </summary>
        /// <remarks>
        /// Based on FUN_005fb0f0 @ 0x005fb0f0 in swkotor2.exe.
        /// Restores ObjectId, Tag, components, and custom data.
        /// Recreates component attachments and state.
        /// </remarks>
        public override void Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Entity deserialization data cannot be null or empty", nameof(data));
            }

            // Parse GFF from byte array
            // Based on swkotor2.exe: FUN_005fb0f0 loads entity from GFF
            GFF gff = GFF.FromBytes(data);
            if (gff == null || gff.Root == null)
            {
                throw new InvalidDataException("Invalid GFF data for entity deserialization");
            }

            var root = gff.Root;

            // Deserialize basic entity properties
            if (root.Exists("ObjectId"))
            {
                _objectId = root.GetUInt32("ObjectId");
            }
            if (root.Exists("Tag"))
            {
                _tag = root.GetString("Tag") ?? "";
            }
            // ObjectType is read-only, so we verify it matches
            if (root.Exists("ObjectType"))
            {
                int objectTypeValue = root.GetInt32("ObjectType");
                if (objectTypeValue != (int)_objectType)
                {
                    throw new InvalidDataException($"Deserialized ObjectType {objectTypeValue} does not match entity ObjectType {(int)_objectType}");
                }
            }
            if (root.Exists("AreaId"))
            {
                _areaId = root.GetUInt32("AreaId");
            }
            if (root.Exists("IsValid"))
            {
                _isValid = root.GetUInt8("IsValid") != 0;
            }

            // Deserialize Transform component
            // Based on swkotor2.exe: Position stored as X, Y, Z in GFF
            // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 loads entity data from GFF
            // Transform component is always present on all entities (created in Initialize() or AttachCommonComponents())
            // If missing (edge case during deserialization), create it to match original engine behavior
            if (root.Exists("X") && root.Exists("Y") && root.Exists("Z"))
            {
                var transformComponent = GetComponent<ITransformComponent>();
                if (transformComponent == null)
                {
                    // Transform component should already exist from Initialize(), but create it if missing
                    // Based on swkotor2.exe: All entities have transform data, so component must exist
                    // Component creation pattern matches AttachCommonComponents() and ComponentInitializer.InitializeComponents()
                    // Located via string references: "XPosition" @ 0x007bd000, "YPosition" @ 0x007bcff4, "ZPosition" @ 0x007bcfe8 (swkotor2.exe)
                    transformComponent = new TransformComponent();
                    AddComponent<ITransformComponent>(transformComponent);
                }

                float x = root.GetSingle("X");
                float y = root.GetSingle("Y");
                float z = root.GetSingle("Z");
                transformComponent.Position = new System.Numerics.Vector3(x, y, z);

                if (root.Exists("Facing"))
                {
                    transformComponent.Facing = root.GetSingle("Facing");
                }

                if (root.Exists("ScaleX") && root.Exists("ScaleY") && root.Exists("ScaleZ"))
                {
                    float scaleX = root.GetSingle("ScaleX");
                    float scaleY = root.GetSingle("ScaleY");
                    float scaleZ = root.GetSingle("ScaleZ");
                    transformComponent.Scale = new System.Numerics.Vector3(scaleX, scaleY, scaleZ);
                }

                // Parent will be resolved later when all entities are loaded
                if (root.Exists("ParentObjectId"))
                {
                    uint parentObjectId = root.GetUInt32("ParentObjectId");
                    if (parentObjectId != 0)
                    {
                        Type baseEntityTypeForParent = typeof(BaseEntity);
                        FieldInfo dataFieldForParent = baseEntityTypeForParent.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (dataFieldForParent != null)
                        {
                            var parentData = dataFieldForParent.GetValue(this) as Dictionary<string, object>;
                            if (parentData == null)
                            {
                                parentData = new Dictionary<string, object>();
                                dataFieldForParent.SetValue(this, parentData);
                            }
                            parentData["_ParentObjectId"] = parentObjectId;
                        }
                    }
                }
            }

            // Deserialize Stats component
            if (root.Exists("CurrentHP"))
            {
                var statsComponent = GetComponent<IStatsComponent>();
                if (statsComponent != null)
                {
                    statsComponent.CurrentHP = root.GetInt32("CurrentHP");
                    if (root.Exists("MaxHP"))
                    {
                        statsComponent.MaxHP = root.GetInt32("MaxHP");
                    }
                    if (root.Exists("CurrentFP"))
                    {
                        statsComponent.CurrentFP = root.GetInt32("CurrentFP");
                    }
                    if (root.Exists("MaxFP"))
                    {
                        statsComponent.MaxFP = root.GetInt32("MaxFP");
                    }

                    // Deserialize ability scores
                    if (root.Exists("Abilities"))
                    {
                        var abilityStruct = root.GetStruct("Abilities");
                        if (abilityStruct != null && abilityStruct.Count > 0)
                        {
                            if (abilityStruct.Exists("STR"))
                            {
                                statsComponent.SetAbility(Ability.Strength, abilityStruct.GetInt32("STR"));
                            }
                            if (abilityStruct.Exists("DEX"))
                            {
                                statsComponent.SetAbility(Ability.Dexterity, abilityStruct.GetInt32("DEX"));
                            }
                            if (abilityStruct.Exists("CON"))
                            {
                                statsComponent.SetAbility(Ability.Constitution, abilityStruct.GetInt32("CON"));
                            }
                            if (abilityStruct.Exists("INT"))
                            {
                                statsComponent.SetAbility(Ability.Intelligence, abilityStruct.GetInt32("INT"));
                            }
                            if (abilityStruct.Exists("WIS"))
                            {
                                statsComponent.SetAbility(Ability.Wisdom, abilityStruct.GetInt32("WIS"));
                            }
                            if (abilityStruct.Exists("CHA"))
                            {
                                statsComponent.SetAbility(Ability.Charisma, abilityStruct.GetInt32("CHA"));
                            }
                        }
                    }
                }
            }

            // Deserialize Door component
            if (root.Exists("IsOpen") || root.Exists("IsLocked"))
            {
                var doorComponent = GetComponent<IDoorComponent>();
                if (doorComponent == null && _objectType == ObjectType.Door)
                {
                    doorComponent = new Andastra.Runtime.Games.Odyssey.Components.OdysseyDoorComponent();
                    doorComponent.Owner = this;
                    AddComponent<IDoorComponent>(doorComponent);
                }

                if (root.Exists("IsOpen"))
                {
                    doorComponent.IsOpen = root.GetUInt8("IsOpen") != 0;
                }
                if (root.Exists("IsLocked"))
                {
                    doorComponent.IsLocked = root.GetUInt8("IsLocked") != 0;
                }
                if (root.Exists("LockableByScript"))
                {
                    doorComponent.LockableByScript = root.GetUInt8("LockableByScript") != 0;
                }
                if (root.Exists("LockDC"))
                {
                    doorComponent.LockDC = root.GetInt32("LockDC");
                }
                if (root.Exists("IsBashed"))
                {
                    doorComponent.IsBashed = root.GetUInt8("IsBashed") != 0;
                }
                if (root.Exists("HitPoints"))
                {
                    doorComponent.HitPoints = root.GetInt32("HitPoints");
                }
                if (root.Exists("MaxHitPoints"))
                {
                    doorComponent.MaxHitPoints = root.GetInt32("MaxHitPoints");
                }
                if (root.Exists("Hardness"))
                {
                    doorComponent.Hardness = root.GetInt32("Hardness");
                }
                if (root.Exists("KeyTag"))
                {
                    doorComponent.KeyTag = root.GetString("KeyTag");
                }
                if (root.Exists("KeyRequired"))
                {
                    doorComponent.KeyRequired = root.GetUInt8("KeyRequired") != 0;
                }
                if (root.Exists("OpenState"))
                {
                    doorComponent.OpenState = root.GetInt32("OpenState");
                }
                if (root.Exists("LinkedTo"))
                {
                    doorComponent.LinkedTo = root.GetString("LinkedTo");
                }
                if (root.Exists("LinkedToModule"))
                {
                    doorComponent.LinkedToModule = root.GetString("LinkedToModule");
                }
            }

            // Deserialize Placeable component
            if (root.Exists("IsUseable") || root.Exists("HasInventory"))
            {
                var placeableComponent = GetComponent<IPlaceableComponent>();
                if (placeableComponent == null && _objectType == ObjectType.Placeable)
                {
                    placeableComponent = new Components.PlaceableComponent();
                    placeableComponent.Owner = this;
                    AddComponent<IPlaceableComponent>(placeableComponent);
                }

                if (root.Exists("IsUseable"))
                {
                    placeableComponent.IsUseable = root.GetUInt8("IsUseable") != 0;
                }
                if (root.Exists("HasInventory"))
                {
                    placeableComponent.HasInventory = root.GetUInt8("HasInventory") != 0;
                }
                if (root.Exists("IsStatic"))
                {
                    placeableComponent.IsStatic = root.GetUInt8("IsStatic") != 0;
                }
                if (root.Exists("IsOpen"))
                {
                    placeableComponent.IsOpen = root.GetUInt8("IsOpen") != 0;
                }
                if (root.Exists("IsLocked"))
                {
                    placeableComponent.IsLocked = root.GetUInt8("IsLocked") != 0;
                }
                if (root.Exists("LockDC"))
                {
                    placeableComponent.LockDC = root.GetInt32("LockDC");
                }
                if (root.Exists("KeyTag"))
                {
                    placeableComponent.KeyTag = root.GetString("KeyTag");
                }
                if (root.Exists("HitPoints"))
                {
                    placeableComponent.HitPoints = root.GetInt32("HitPoints");
                }
                if (root.Exists("MaxHitPoints"))
                {
                    placeableComponent.MaxHitPoints = root.GetInt32("MaxHitPoints");
                }
                if (root.Exists("Hardness"))
                {
                    placeableComponent.Hardness = root.GetInt32("Hardness");
                }
                if (root.Exists("AnimationState"))
                {
                    placeableComponent.AnimationState = root.GetInt32("AnimationState");
                }
            }

            // Deserialize sound component
            // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 loads sound component data from GFF save data
            // Located via string references: "SoundList" @ 0x007bd080 (GIT sound list), "Sound" @ 0x007bc500 (sound entity type)
            // Sound properties are loaded: Active, Continuous, Looping, Positional, Random, RandomPosition, Volume, VolumeVrtn, MaxDistance, MinDistance, Interval, IntervalVrtn, PitchVariation, SoundFiles, Hours
            if (root.Exists("Active") || root.Exists("TemplateResRef"))
            {
                var soundComponent = GetComponent<ISoundComponent>();
                if (soundComponent == null && _objectType == ObjectType.Sound)
                {
                    soundComponent = new Components.SoundComponent();
                    soundComponent.Owner = this;
                    AddComponent<ISoundComponent>(soundComponent);
                }

                if (soundComponent != null)
                {
                    if (root.Exists("TemplateResRef"))
                    {
                        soundComponent.TemplateResRef = root.GetString("TemplateResRef") ?? "";
                    }
                    if (root.Exists("Active"))
                    {
                        soundComponent.Active = root.GetUInt8("Active") != 0;
                    }
                    if (root.Exists("Continuous"))
                    {
                        soundComponent.Continuous = root.GetUInt8("Continuous") != 0;
                    }
                    if (root.Exists("Looping"))
                    {
                        soundComponent.Looping = root.GetUInt8("Looping") != 0;
                    }
                    if (root.Exists("Positional"))
                    {
                        soundComponent.Positional = root.GetUInt8("Positional") != 0;
                    }
                    if (root.Exists("RandomPosition"))
                    {
                        soundComponent.RandomPosition = root.GetUInt8("RandomPosition") != 0;
                    }
                    if (root.Exists("Random"))
                    {
                        soundComponent.Random = root.GetUInt8("Random") != 0;
                    }
                    if (root.Exists("Volume"))
                    {
                        soundComponent.Volume = root.GetInt32("Volume");
                    }
                    if (root.Exists("VolumeVrtn"))
                    {
                        soundComponent.VolumeVrtn = root.GetInt32("VolumeVrtn");
                    }
                    if (root.Exists("MaxDistance"))
                    {
                        soundComponent.MaxDistance = root.GetSingle("MaxDistance");
                    }
                    if (root.Exists("MinDistance"))
                    {
                        soundComponent.MinDistance = root.GetSingle("MinDistance");
                    }
                    if (root.Exists("Interval"))
                    {
                        soundComponent.Interval = root.GetUInt32("Interval");
                    }
                    if (root.Exists("IntervalVrtn"))
                    {
                        soundComponent.IntervalVrtn = root.GetUInt32("IntervalVrtn");
                    }
                    if (root.Exists("PitchVariation"))
                    {
                        soundComponent.PitchVariation = root.GetSingle("PitchVariation");
                    }
                    if (root.Exists("Hours"))
                    {
                        soundComponent.Hours = root.GetUInt32("Hours");
                    }
                    if (root.Exists("TimeSinceLastPlay"))
                    {
                        soundComponent.TimeSinceLastPlay = root.GetSingle("TimeSinceLastPlay");
                    }
                    if (root.Exists("IsPlaying"))
                    {
                        soundComponent.IsPlaying = root.GetUInt8("IsPlaying") != 0;
                    }

                    // Deserialize sound files list
                    if (root.Exists("SoundFiles"))
                    {
                        var soundFilesList = root.GetList("SoundFiles");
                        if (soundFilesList != null && soundFilesList.Count > 0)
                        {
                            soundComponent.SoundFiles = new List<string>();
                            for (int i = 0; i < soundFilesList.Count; i++)
                            {
                                var soundFileStruct = soundFilesList.At(i);
                                if (soundFileStruct != null && soundFileStruct.Exists("Sound"))
                                {
                                    string soundFile = soundFileStruct.GetString("Sound");
                                    if (!string.IsNullOrEmpty(soundFile))
                                    {
                                        soundComponent.SoundFiles.Add(soundFile);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Deserialize Inventory component
            if (root.Exists("Inventory"))
            {
                var inventoryComponent = GetComponent<IInventoryComponent>();
                if (inventoryComponent != null)
                {
                    var inventoryList = root.GetList("Inventory");
                    if (inventoryList != null)
                    {
                        // Store item references for later resolution when all entities are loaded
                        var itemReferences = new List<(int slot, uint objectId, string tag, int objectType)>();
                        for (int i = 0; i < inventoryList.Count; i++)
                        {
                            var itemStruct = inventoryList.At(i);
                            if (itemStruct != null)
                            {
                                int slot = itemStruct.GetInt32("Slot");
                                uint itemObjectId = itemStruct.GetUInt32("ItemObjectId");
                                string itemTag = itemStruct.GetString("ItemTag") ?? "";
                                int itemObjectType = itemStruct.GetInt32("ItemObjectType");
                                itemReferences.Add((slot, itemObjectId, itemTag, itemObjectType));
                            }
                        }
                        // Store item references in custom data for later resolution
                        Type baseEntityTypeForItems = typeof(BaseEntity);
                        FieldInfo dataFieldForItems = baseEntityTypeForItems.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (dataFieldForItems != null)
                        {
                            var itemsData = dataFieldForItems.GetValue(this) as Dictionary<string, object>;
                            if (itemsData == null)
                            {
                                itemsData = new Dictionary<string, object>();
                                dataFieldForItems.SetValue(this, itemsData);
                            }
                            itemsData["_ItemReferences"] = itemReferences;
                        }
                    }
                }
            }

            // Deserialize ScriptHooks component
            if (root.Exists("Scripts"))
            {
                var scriptHooksComponent = GetComponent<IScriptHooksComponent>();
                if (scriptHooksComponent == null)
                {
                    scriptHooksComponent = new Common.Components.BaseScriptHooksComponent();
                    AddComponent<IScriptHooksComponent>(scriptHooksComponent);
                }

                var scriptsStruct = root.GetStruct("Scripts");
                if (scriptsStruct != null && scriptsStruct.Count > 0)
                {
                    // Read all script fields from GFF
                    foreach (var (fieldName, fieldType, fieldValue) in scriptsStruct)
                    {
                        if (fieldType == GFFFieldType.String || fieldType == GFFFieldType.ResRef)
                        {
                            string resRef = fieldValue?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(resRef))
                            {
                                // Try to parse as enum name
                                if (Enum.TryParse<ScriptEvent>(fieldName, out ScriptEvent eventType))
                                {
                                    scriptHooksComponent.SetScript(eventType, resRef);
                                }
                            }
                        }
                    }
                }

                // Deserialize local variables using reflection
                if (root.Exists("LocalVariables"))
                {
                    var localVarsStruct = root.GetStruct("LocalVariables");
                    if (localVarsStruct != null && localVarsStruct.Count > 0)
                    {
                        Type componentType = scriptHooksComponent.GetType();
                        FieldInfo localIntsField = componentType.GetField("_localInts", BindingFlags.NonPublic | BindingFlags.Instance);
                        FieldInfo localFloatsField = componentType.GetField("_localFloats", BindingFlags.NonPublic | BindingFlags.Instance);
                        FieldInfo localStringsField = componentType.GetField("_localStrings", BindingFlags.NonPublic | BindingFlags.Instance);

                        // Deserialize local ints
                        if (localVarsStruct.Exists("IntList"))
                        {
                            var intList = localVarsStruct.GetList("IntList");
                            if (intList != null && intList.Count > 0 && localIntsField != null)
                            {
                                var localInts = localIntsField.GetValue(scriptHooksComponent) as Dictionary<string, int>;
                                if (localInts == null)
                                {
                                    localInts = new Dictionary<string, int>();
                                    localIntsField.SetValue(scriptHooksComponent, localInts);
                                }
                                localInts.Clear();
                                for (int i = 0; i < intList.Count; i++)
                                {
                                    var varStruct = intList.At(i);
                                    if (varStruct != null && varStruct.Exists("Name") && varStruct.Exists("Value"))
                                    {
                                        string name = varStruct.GetString("Name");
                                        int value = varStruct.GetInt32("Value");
                                        localInts[name] = value;
                                    }
                                }
                            }
                        }

                        // Deserialize local floats
                        if (localVarsStruct.Exists("FloatList"))
                        {
                            var floatList = localVarsStruct.GetList("FloatList");
                            if (floatList != null && floatList.Count > 0 && localFloatsField != null)
                            {
                                var localFloats = localFloatsField.GetValue(scriptHooksComponent) as Dictionary<string, float>;
                                if (localFloats == null)
                                {
                                    localFloats = new Dictionary<string, float>();
                                    localFloatsField.SetValue(scriptHooksComponent, localFloats);
                                }
                                localFloats.Clear();
                                for (int i = 0; i < floatList.Count; i++)
                                {
                                    var varStruct = floatList.At(i);
                                    if (varStruct != null && varStruct.Exists("Name") && varStruct.Exists("Value"))
                                    {
                                        string name = varStruct.GetString("Name");
                                        float value = varStruct.GetSingle("Value");
                                        localFloats[name] = value;
                                    }
                                }
                            }
                        }

                        // Deserialize local strings
                        if (localVarsStruct.Exists("StringList"))
                        {
                            var stringList = localVarsStruct.GetList("StringList");
                            if (stringList != null && stringList.Count > 0 && localStringsField != null)
                            {
                                var localStrings = localStringsField.GetValue(scriptHooksComponent) as Dictionary<string, string>;
                                if (localStrings == null)
                                {
                                    localStrings = new Dictionary<string, string>();
                                    localStringsField.SetValue(scriptHooksComponent, localStrings);
                                }
                                localStrings.Clear();
                                for (int i = 0; i < stringList.Count; i++)
                                {
                                    var varStruct = stringList.At(i);
                                    if (varStruct != null && varStruct.Exists("Name") && varStruct.Exists("Value"))
                                    {
                                        string name = varStruct.GetString("Name");
                                        string value = varStruct.GetString("Value") ?? "";
                                        localStrings[name] = value;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Deserialize custom data dictionary
            if (root.Exists("CustomData"))
            {
                var customDataStruct = root.GetStruct("CustomData");
                if (customDataStruct != null && customDataStruct.Exists("DataList"))
                {
                    var dataList = customDataStruct.GetList("DataList");
                    if (dataList != null && dataList.Count > 0)
                    {
                        Type baseEntityType = typeof(BaseEntity);
                        FieldInfo dataField = baseEntityType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (dataField != null)
                        {
                            var fieldData = dataField.GetValue(this) as Dictionary<string, object>;
                            if (fieldData == null)
                            {
                                fieldData = new Dictionary<string, object>();
                                dataField.SetValue(this, fieldData);
                            }
                            fieldData.Clear();

                            for (int i = 0; i < dataList.Count; i++)
                            {
                                var dataStruct = dataList.At(i);
                                if (dataStruct != null && dataStruct.Exists("Key") && dataStruct.Exists("Type") && dataStruct.Exists("Value"))
                                {
                                    string key = dataStruct.GetString("Key");
                                    string type = dataStruct.GetString("Type");
                                    object value = null;

                                    switch (type)
                                    {
                                        case "null":
                                            value = null;
                                            break;
                                        case "Int32":
                                        case "int":
                                            value = dataStruct.GetInt32("Value");
                                            break;
                                        case "Single":
                                        case "float":
                                            value = dataStruct.GetSingle("Value");
                                            break;
                                        case "String":
                                        case "string":
                                            value = dataStruct.GetString("Value");
                                            break;
                                        case "Boolean":
                                        case "bool":
                                            value = dataStruct.GetUInt8("Value") != 0;
                                            break;
                                        case "UInt32":
                                        case "uint":
                                            value = dataStruct.GetUInt32("Value");
                                            break;
                                        default:
                                            // For other types, try to deserialize as string
                                            value = dataStruct.GetString("Value");
                                            break;
                                    }

                                    fieldData[key] = value;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Interface for components that need per-frame updates.
    /// </summary>
    internal interface IUpdatableComponent
    {
        void Update(float deltaTime);
    }
}
