using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Common;

namespace Andastra.Runtime.Games.Aurora
{
    /// <summary>
    /// Aurora Engine (NWN) specific area implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Area Implementation:
    /// - Based on nwmain.exe area systems
    /// - Uses ARE (area properties) and GIT (instances) file formats
    /// - Implements walkmesh navigation and area transitions
    /// - Supports area effects and environmental systems
    ///
    /// Based on reverse engineering of:
    /// - nwmain.exe: Area loading and management functions
    /// - Aurora engine area properties and entity management
    /// - ARE/GIT format documentation in vendor/PyKotor/wiki/
    ///
    /// Aurora-specific features:
    /// - Enhanced area effects system
    /// - Weather and environmental simulation
    /// - Dynamic lighting and shadows
    /// - Area-based scripting triggers
    /// - Tile-based area construction
    /// </remarks>
    [PublicAPI]
    public class AuroraArea : BaseArea
    {
        private readonly List<IEntity> _creatures = new List<IEntity>();
        private readonly List<IEntity> _placeables = new List<IEntity>();
        private readonly List<IEntity> _doors = new List<IEntity>();
        private readonly List<IEntity> _triggers = new List<IEntity>();
        private readonly List<IEntity> _waypoints = new List<IEntity>();
        private readonly List<IEntity> _sounds = new List<IEntity>();
        private readonly List<IAreaEffect> _areaEffects = new List<IAreaEffect>();

        private string _resRef;
        private string _displayName;
        private string _tag;
        private bool _isUnescapable;
        private INavigationMesh _navigationMesh;

        /// <summary>
        /// Creates a new Aurora area.
        /// </summary>
        /// <param name="resRef">The resource reference name of the area.</param>
        /// <param name="areData">ARE file data containing area properties.</param>
        /// <param name="gitData">GIT file data containing entity instances.</param>
        /// <remarks>
        /// Based on area loading sequence in nwmain.exe.
        /// Aurora has more complex area initialization with tile-based construction.
        /// </remarks>
        public AuroraArea(string resRef, byte[] areData, byte[] gitData)
        {
            _resRef = resRef ?? throw new ArgumentNullException(nameof(resRef));
            _tag = resRef; // Default tag to resref

            LoadAreaGeometry(areData);
            LoadEntities(gitData);
            LoadAreaProperties(areData);
            InitializeAreaEffects();
        }

        /// <summary>
        /// The resource reference name of this area.
        /// </summary>
        public override string ResRef => _resRef;

        /// <summary>
        /// The display name of the area.
        /// </summary>
        public override string DisplayName => _displayName ?? _resRef;

        /// <summary>
        /// The tag of the area.
        /// </summary>
        public override string Tag => _tag;

        /// <summary>
        /// All creatures in this area.
        /// </summary>
        public override IEnumerable<IEntity> Creatures => _creatures;

        /// <summary>
        /// All placeables in this area.
        /// </summary>
        public override IEnumerable<IEntity> Placeables => _placeables;

        /// <summary>
        /// All doors in this area.
        /// </summary>
        public override IEnumerable<IEntity> Doors => _doors;

        /// <summary>
        /// All triggers in this area.
        /// </summary>
        public override IEnumerable<IEntity> Triggers => _triggers;

        /// <summary>
        /// All waypoints in this area.
        /// </summary>
        public override IEnumerable<IEntity> Waypoints => _waypoints;

        /// <summary>
        /// All sounds in this area.
        /// </summary>
        public override IEnumerable<IEntity> Sounds => _sounds;

        /// <summary>
        /// Gets the walkmesh navigation system for this area.
        /// </summary>
        public override INavigationMesh NavigationMesh => _navigationMesh;

        /// <summary>
        /// Gets or sets whether the area is unescapable.
        /// </summary>
        /// <remarks>
        /// Based on Aurora area properties system.
        /// Similar to Odyssey but with additional restrictions.
        /// </remarks>
        public override bool IsUnescapable
        {
            get => _isUnescapable;
            set => _isUnescapable = value;
        }

        /// <summary>
        /// Aurora engine doesn't use stealth XP - always returns false.
        /// </summary>
        /// <remarks>
        /// Aurora engine doesn't have the stealth XP system found in Odyssey.
        /// This property is not applicable to Aurora areas.
        /// </remarks>
        public override bool StealthXPEnabled
        {
            get => false;
            set { /* No-op for Aurora */ }
        }

        /// <summary>
        /// Gets an object by tag within this area.
        /// </summary>
        public override IEntity GetObjectByTag(string tag, int nth = 0)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            var allEntities = _creatures.Concat(_placeables).Concat(_doors)
                                       .Concat(_triggers).Concat(_waypoints).Concat(_sounds);

            return allEntities.Where(e => string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase))
                             .Skip(nth).FirstOrDefault();
        }

        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        /// <remarks>
        /// Based on Aurora walkmesh system.
        /// More complex than Odyssey due to tile-based areas.
        /// </remarks>
        public override bool IsPointWalkable(Vector3 point)
        {
            return _navigationMesh?.IsPointWalkable(point) ?? false;
        }

        /// <summary>
        /// Projects a point onto the walkmesh.
        /// </summary>
        /// <remarks>
        /// Based on Aurora walkmesh projection functions.
        /// Handles tile-based area geometry.
        /// </remarks>
        public override bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            if (_navigationMesh == null)
            {
                result = point;
                height = point.Y;
                return false;
            }

            return _navigationMesh.ProjectToWalkmesh(point, out result, out height);
        }

        /// <summary>
        /// Loads area properties from GFF data.
        /// </summary>
        /// <remarks>
        /// Based on Aurora area property loading.
        /// Aurora has more complex area properties than Odyssey.
        /// Includes weather, lighting, and area effect settings.
        /// </remarks>
        protected override void LoadAreaProperties(byte[] gffData)
        {
            // TODO: Implement Aurora ARE file parsing
            // Read area properties including:
            // - Unescapable flag
            // - Weather settings
            // - Lighting configuration
            // - Area effect references
            // - Tile layout information

            _isUnescapable = false; // Default value
        }

        /// <summary>
        /// Saves area properties to GFF data.
        /// </summary>
        /// <remarks>
        /// Based on Aurora area property saving in nwmain.exe.
        /// - SaveArea @ 0x140365160: Saves main ARE file structure with Tag, Name, Unescapable, lighting, fog, weather, script hooks, tiles
        /// - SaveProperties @ 0x140367390: Saves AreaProperties nested struct with SkyBox, lighting directions, wind, grass overrides, ambient sounds
        ///
        /// This implementation saves runtime-modifiable area properties to a valid ARE GFF format.
        /// Saves properties that can change during gameplay: Unescapable, Tag, DisplayName, ResRef.
        /// Creates minimal but valid ARE GFF structure following nwmain.exe SaveArea pattern.
        /// </remarks>
        protected override byte[] SaveAreaProperties()
        {
            // Based on nwmain.exe: CNWSArea::SaveArea @ 0x140365160
            // Creates ARE GFF structure and saves runtime-modifiable area properties
            var gff = new GFF(GFFContent.ARE);
            var root = gff.Root;

            // Save basic identity fields (based on SaveArea lines 27-66 in nwmain.exe)
            // Tag field - based on line 66: WriteFieldCExoString("Tag")
            root.SetString("Tag", _tag ?? _resRef ?? "");

            // Name field - based on line 48: WriteFieldCExoLocString("Name")
            // Convert display name to LocalizedString format
            LocalizedString name = LocalizedString.FromInvalid();
            if (!string.IsNullOrEmpty(_displayName))
            {
                // Create a simple localized string with the display name
                name = LocalizedString.FromEnglish(_displayName);
            }
            root.SetLocString("Name", name);

            // Unescapable field - based on ARE format specification
            // Aurora uses Unescapable flag like Odyssey, stored as UInt8
            root.SetUInt8("Unescapable", _isUnescapable ? (byte)1 : (byte)0);

            // ResRef field - based on line 59: WriteFieldCResRef("ResRef")
            if (!string.IsNullOrEmpty(_resRef))
            {
                ResRef resRefObj = ResRef.FromString(_resRef);
                root.SetResRef("ResRef", resRefObj);
            }

            // Set default values for required ARE fields to ensure valid GFF structure
            // These match the minimal structure expected by the ARE format
            // Based on SaveArea function structure in nwmain.exe

            // Creator_ID - based on line 31: WriteFieldDWORD("Creator_ID", -1)
            root.SetUInt32("Creator_ID", 0xFFFFFFFF); // -1 as DWORD

            // ID - based on line 35: WriteFieldINT("ID", -1)
            root.SetInt32("ID", -1);

            // Version - based on line 98: WriteFieldINT("Version", version)
            root.SetInt32("Version", 0);

            // Flags - based on line 33: WriteFieldDWORD("Flags", flags)
            root.SetUInt32("Flags", 0);

            // Width and Height - based on lines 34, 99: WriteFieldINT("Height"/"Width")
            root.SetInt32("Width", 0);
            root.SetInt32("Height", 0);

            // Script hooks - set to empty ResRefs if not specified
            // Based on lines 51-57: WriteFieldCResRef("OnEnter", "OnExit", "OnHeartbeat", "OnUserDefined")
            root.SetResRef("OnEnter", ResRef.FromBlank());
            root.SetResRef("OnExit", ResRef.FromBlank());
            root.SetResRef("OnHeartbeat", ResRef.FromBlank());
            root.SetResRef("OnUserDefined", ResRef.FromBlank());

            // Lighting defaults - based on lines 61-65: WriteFieldDWORD/BYTE for lighting
            root.SetUInt32("SunAmbientColor", 0);
            root.SetUInt32("SunDiffuseColor", 0);
            root.SetUInt8("SunShadows", 0);
            root.SetUInt8("ShadowOpacity", 0);

            // Fog defaults - based on lines 63-64: WriteFieldBYTE/DWORD for fog
            root.SetUInt8("SunFogAmount", 0);
            root.SetUInt32("SunFogColor", 0);

            // Moon lighting defaults - based on lines 41-46: WriteFieldDWORD/BYTE for moon
            root.SetUInt32("MoonAmbientColor", 0);
            root.SetUInt32("MoonDiffuseColor", 0);
            root.SetUInt8("MoonFogAmount", 0);
            root.SetUInt32("MoonFogColor", 0);
            root.SetUInt8("MoonShadows", 0);

            // Weather defaults - based on lines 27-29: WriteFieldINT for weather chances
            root.SetInt32("ChanceRain", 0);
            root.SetInt32("ChanceSnow", 0);
            root.SetInt32("ChanceLightning", 0);
            root.SetInt32("WindPower", 0);

            // Day/Night cycle defaults - based on line 32: WriteFieldBYTE("DayNightCycle")
            root.SetUInt8("DayNightCycle", 0);
            root.SetUInt8("IsNight", 0);

            // Lighting scheme - based on line 37: WriteFieldBYTE("LightingScheme")
            root.SetUInt8("LightingScheme", 0);

            // Load screen - based on line 38: WriteFieldWORD("LoadScreenID")
            root.SetUInt16("LoadScreenID", 0);

            // Modifier checks - based on lines 39-40: WriteFieldINT("ModListenCheck", "ModSpotCheck")
            root.SetInt32("ModListenCheck", 0);
            root.SetInt32("ModSpotCheck", 0);

            // Fog clip distance - based on line 45: WriteFieldFLOAT("FogClipDist")
            root.SetSingle("FogClipDist", 0.0f);

            // No rest flag - based on line 49: WriteFieldBYTE("NoRest")
            root.SetUInt8("NoRest", 0);

            // Player vs Player - based on line 58: WriteFieldBYTE("PlayerVsPlayer")
            root.SetUInt8("PlayerVsPlayer", 0);

            // Tileset - based on line 97: WriteFieldCResRef("Tileset")
            root.SetResRef("Tileset", ResRef.FromBlank());

            // Comments - based on line 30: WriteFieldCExoString("Comments")
            root.SetString("Comments", "");

            // Create AreaProperties nested struct (based on SaveProperties @ 0x140367390)
            // This struct contains additional runtime properties
            var areaPropertiesStruct = new GFFStruct(100); // Struct ID 100 as seen in SaveProperties line 21
            root.SetStruct("AreaProperties", areaPropertiesStruct);

            // SkyBox - based on SaveProperties line 22: WriteFieldBYTE("SkyBox")
            areaPropertiesStruct.SetUInt8("SkyBox", 0);

            // DisplayName - based on SaveProperties line 37: WriteFieldCExoString("DisplayName")
            if (!string.IsNullOrEmpty(_displayName))
            {
                areaPropertiesStruct.SetString("DisplayName", _displayName);
            }
            else
            {
                areaPropertiesStruct.SetString("DisplayName", _resRef ?? "");
            }

            // Lighting directions and colors (defaults) - based on SaveProperties lines 29-36
            areaPropertiesStruct.SetUInt32("MoonFogColor", 0);
            areaPropertiesStruct.SetUInt32("SunFogColor", 0);
            areaPropertiesStruct.SetUInt8("MoonFogAmount", 0);
            areaPropertiesStruct.SetUInt8("SunFogAmount", 0);
            areaPropertiesStruct.SetUInt32("MoonAmbientColor", 0);
            areaPropertiesStruct.SetUInt32("MoonDiffuseColor", 0);
            areaPropertiesStruct.SetSingle("MoonDirectionX", 0.0f);
            areaPropertiesStruct.SetSingle("MoonDirectionY", 0.0f);
            areaPropertiesStruct.SetSingle("MoonDirectionZ", 0.0f);
            areaPropertiesStruct.SetUInt32("SunAmbientColor", 0);
            areaPropertiesStruct.SetUInt32("SunDiffuseColor", 0);
            areaPropertiesStruct.SetSingle("SunDirectionX", 0.0f);
            areaPropertiesStruct.SetSingle("SunDirectionY", 0.0f);
            areaPropertiesStruct.SetSingle("SunDirectionZ", 0.0f);

            // Wind properties (defaults) - based on SaveProperties lines 39-44
            // Wind is only saved if wind power is 3 (line 38 check)
            areaPropertiesStruct.SetSingle("WindDirectionX", 0.0f);
            areaPropertiesStruct.SetSingle("WindDirectionY", 0.0f);
            areaPropertiesStruct.SetSingle("WindDirectionZ", 0.0f);
            areaPropertiesStruct.SetSingle("WindMagnitude", 0.0f);
            areaPropertiesStruct.SetSingle("WindYaw", 0.0f);
            areaPropertiesStruct.SetSingle("WindPitch", 0.0f);

            // Grass properties (defaults) - based on SaveProperties lines 46-70
            areaPropertiesStruct.SetUInt8("GrassDefDisabled", 0);

            // Create empty Tile_List - based on SaveArea lines 67-96
            // Tile list is required for valid ARE structure
            var tileList = new GFFList();
            root.SetList("Tile_List", tileList);

            // Serialize GFF to byte array
            // Based on pattern used in OdysseyEntity.Serialize() and other serialization methods
            return gff.ToBytes();
        }

        /// <summary>
        /// Loads entities from GIT file.
        /// </summary>
        /// <remarks>
        /// Based on Aurora entity loading from GIT files.
        /// Aurora has more complex entity templates than Odyssey.
        /// Includes faction, reputation, and AI settings.
        /// </remarks>
        protected override void LoadEntities(byte[] gitData)
        {
            // TODO: Implement Aurora GIT file parsing
            // Parse GFF with "GIT " signature
            // Load Creature List, Door List, Placeable List, etc.
            // Create AuroraEntity instances with enhanced components
        }

        /// <summary>
        /// Loads area geometry and walkmesh from ARE file.
        /// </summary>
        /// <remarks>
        /// Based on Aurora ARE file loading.
        /// Aurora uses tile-based area construction.
        /// Loads tile layout, terrain, and navigation data.
        /// </remarks>
        protected override void LoadAreaGeometry(byte[] areData)
        {
            // TODO: Implement Aurora ARE file parsing
            // Parse GFF with "ARE " signature
            // Load tile-based area layout
            // Construct walkmesh from tile data
            // Create AuroraNavigationMesh instance
            _navigationMesh = new AuroraNavigationMesh(); // Placeholder
        }

        /// <summary>
        /// Initializes area effects and environmental systems.
        /// </summary>
        /// <remarks>
        /// Aurora has sophisticated area effects system.
        /// Includes weather simulation, dynamic lighting, particle effects.
        /// Area effects are more complex than Odyssey's basic system.
        /// </remarks>
        protected override void InitializeAreaEffects()
        {
            // TODO: Initialize Aurora area effects
            // Load weather systems
            // Set up dynamic lighting
            // Initialize particle effects
            // Configure area audio
        }

        /// <summary>
        /// Removes an entity from this area's collections.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Basic entity removal without physics system.
        /// Based on nwmain.exe entity management.
        /// </remarks>
        protected override void RemoveEntityFromArea(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Remove from type-specific lists
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    _creatures.Remove(entity);
                    break;
                case ObjectType.Placeable:
                    _placeables.Remove(entity);
                    break;
                case ObjectType.Door:
                    _doors.Remove(entity);
                    break;
                case ObjectType.Trigger:
                    _triggers.Remove(entity);
                    break;
                case ObjectType.Waypoint:
                    _waypoints.Remove(entity);
                    break;
                case ObjectType.Sound:
                    _sounds.Remove(entity);
                    break;
            }
        }

        /// <summary>
        /// Adds an entity to this area's collections.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Basic entity addition without physics system.
        /// Based on nwmain.exe entity management.
        /// </remarks>
        protected override void AddEntityToArea(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Add to type-specific lists
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    if (!_creatures.Contains(entity))
                    {
                        _creatures.Add(entity);
                    }
                    break;
                case ObjectType.Placeable:
                    if (!_placeables.Contains(entity))
                    {
                        _placeables.Add(entity);
                    }
                    break;
                case ObjectType.Door:
                    if (!_doors.Contains(entity))
                    {
                        _doors.Add(entity);
                    }
                    break;
                case ObjectType.Trigger:
                    if (!_triggers.Contains(entity))
                    {
                        _triggers.Add(entity);
                    }
                    break;
                case ObjectType.Waypoint:
                    if (!_waypoints.Contains(entity))
                    {
                        _waypoints.Add(entity);
                    }
                    break;
                case ObjectType.Sound:
                    if (!_sounds.Contains(entity))
                    {
                        _sounds.Add(entity);
                    }
                    break;
            }
        }

        /// <summary>
        /// Updates area state each frame.
        /// </summary>
        /// <remarks>
        /// Updates area effects, weather simulation, dynamic lighting.
        /// Processes tile-based area updates.
        /// </remarks>
        public override void Update(float deltaTime)
        {
            // TODO: Update Aurora area systems
            // Update weather simulation
            // Process dynamic lighting
            // Update area effects
            // Handle tile-based area logic
        }

        /// <summary>
        /// Renders the area.
        /// </summary>
        /// <remarks>
        /// Aurora has complex rendering with tile-based geometry.
        /// Includes dynamic lighting, weather effects, and area effects.
        /// </remarks>
        public override void Render()
        {
            // TODO: Implement Aurora area rendering
            // Render tile-based geometry
            // Apply dynamic lighting
            // Render weather effects
            // Draw area effects
        }

        /// <summary>
        /// Unloads the area and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Cleans up tile-based geometry, area effects, and entities.
        /// Ensures proper resource cleanup for Aurora's complex systems.
        /// </remarks>
        public override void Unload()
        {
            // TODO: Implement Aurora area unloading
            // Clean up tile-based geometry
            // Destroy area effects
            // Free navigation mesh resources
            // Clean up weather and lighting systems
        }

        /// <summary>
        /// Gets all active area effects in this area.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Area effects are persistent environmental effects.
        /// Includes magical effects, weather, lighting changes.
        /// </remarks>
        public IEnumerable<IAreaEffect> GetAreaEffects()
        {
            return _areaEffects;
        }

        /// <summary>
        /// Adds an area effect to this area.
        /// </summary>
        /// <remarks>
        /// Aurora-specific area effect management.
        /// Effects persist until explicitly removed or expired.
        /// </remarks>
        public void AddAreaEffect(IAreaEffect effect)
        {
            _areaEffects.Add(effect);
        }

        /// <summary>
        /// Removes an area effect from this area.
        /// </summary>
        public bool RemoveAreaEffect(IAreaEffect effect)
        {
            return _areaEffects.Remove(effect);
        }
    }

    /// <summary>
    /// Interface for area effects in Aurora engine.
    /// </summary>
    public interface IAreaEffect
    {
        /// <summary>
        /// Updates the area effect.
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// Renders the area effect.
        /// </summary>
        void Render();

        /// <summary>
        /// Gets whether the effect is still active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Deactivates the effect.
        /// </summary>
        void Deactivate();
    }
}
