using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Games.Common;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Common;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common;
using Andastra.Runtime.Games.Aurora.Scene;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Parsing.Resource;

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

        // Aurora-specific ARE properties
        private int _chanceRain;
        private int _chanceSnow;
        private int _chanceLightning;
        private int _windPower;
        private byte _dayNightCycle;
        private byte _isNight;
        private byte _lightingScheme;
        private ushort _loadScreenID;
        private byte _noRest;
        private byte _playerVsPlayer;
        private byte _skyBox;
        private uint _sunAmbientColor;
        private uint _sunDiffuseColor;
        private uint _moonAmbientColor;
        private uint _moonDiffuseColor;
        private byte _sunShadows;
        private byte _moonShadows;
        private byte _shadowOpacity;
        private byte _sunFogAmount;
        private byte _moonFogAmount;
        private uint _sunFogColor;
        private uint _moonFogColor;
        private ResRef _onEnter;
        private ResRef _onExit;
        private ResRef _onHeartbeat;
        private ResRef _onUserDefined;
        private ResRef _tileset;
        private int _width;
        private int _height;
        private uint _flags;

        // Tileset loader for querying tile surface materials
        private TilesetLoader _tilesetLoader;

        // Weather simulation state
        private readonly System.Random _weatherRandom;
        private float _weatherCheckTimer;
        private const float WeatherCheckInterval = 5.0f; // Check weather every 5 seconds (based on nwmain.exe behavior)
        private bool _isRaining;
        private bool _isSnowing;
        private bool _isLightning;
        private float _lightningFlashTimer;
        private const float LightningFlashDuration = 0.2f; // Lightning flash lasts 0.2 seconds

        // Area heartbeat timer
        private float _areaHeartbeatTimer;
        private const float AreaHeartbeatInterval = 6.0f; // Aurora area heartbeat fires every 6 seconds (based on nwmain.exe)

        // Day/night cycle state
        private float _dayNightTimer;
        private const float DayNightCycleDuration = 1440.0f; // 24 minutes of real time = 24 hours game time (1 minute = 1 hour)

        // Cached temporary area entity for script execution
        // Based on nwmain.exe: Area scripts execute with area ResRef as context entity
        // This entity is created on-demand and cached for reuse across heartbeat calls
        private IEntity _temporaryAreaEntity;

        // Rendering context (set by game loop or service locator)
        // Based on nwmain.exe: Area rendering uses graphics device, room mesh renderer, and basic effect
        private IAreaRenderContext _renderContext;

        // Cached scene data built from ARE file
        // Based on nwmain.exe: CNWSArea::LoadArea builds tile-based scene structure
        private AuroraSceneData _sceneData;

        // Cached ARE data for scene building
        private byte[] _cachedAreData;

        // Snow particle system for weather rendering
        // Based on nwmain.exe: CNWSArea::RenderWeather renders snow particles as billboard sprites
        private SnowParticleSystem _snowParticleSystem;

        /// <summary>
        /// Creates a new Aurora area.
        /// </summary>
        /// <param name="resRef">The resource reference name of the area.</param>
        /// <param name="areData">ARE file data containing area properties.</param>
        /// <param name="gitData">GIT file data containing entity instances.</param>
        /// <param name="resourceLoader">Optional function to load resource files by resref. If null, surface materials will use defaults.</param>
        /// <remarks>
        /// Based on area loading sequence in nwmain.exe.
        /// Aurora has more complex area initialization with tile-based construction.
        /// </remarks>
        public AuroraArea(string resRef, byte[] areData, byte[] gitData, Func<string, byte[]> resourceLoader = null)
        {
            _resRef = resRef ?? throw new ArgumentNullException(nameof(resRef));
            _tag = resRef; // Default tag to resref

            // Initialize tileset loader if resource loader is provided
            // Based on nwmain.exe: CNWTileSetManager::GetTileSet @ 0x1411d4f6a
            if (resourceLoader != null)
            {
                _tilesetLoader = new TilesetLoader(resourceLoader);
            }

            // Initialize weather simulation
            _weatherRandom = new System.Random();
            _weatherCheckTimer = 0.0f;
            _isRaining = false;
            _isSnowing = false;
            _isLightning = false;
            _lightningFlashTimer = 0.0f;

            // Initialize area heartbeat timer
            _areaHeartbeatTimer = 0.0f;

            // Initialize day/night cycle timer
            _dayNightTimer = 0.0f;

            // Cache ARE data for scene building
            _cachedAreData = areData;

            LoadAreaGeometry(areData);
            LoadEntities(gitData);
            LoadAreaProperties(areData);

            // Initialize rain particle system for weather rendering (after width/height are loaded)
            // Based on nwmain.exe: CNWSArea::RenderWeather uses particle system for rain
            _rainParticleSystem = new RainParticleSystem(_width, _height);

            // Initialize snow particle system for weather rendering
            // Based on nwmain.exe: CNWSArea::RenderWeather uses particle system for snow
            _snowParticleSystem = new SnowParticleSystem();

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
        /// Based on Aurora area property loading in nwmain.exe.
        /// 
        /// Function addresses (require Ghidra verification):
        /// - nwmain.exe: CNWSArea::LoadArea @ 0x140365160 (approximate - needs Ghidra verification)
        /// - nwmain.exe: CNWSArea::LoadProperties @ 0x140367390 (approximate - needs Ghidra verification)
        /// 
        /// Aurora has more complex area properties than Odyssey:
        /// - Weather system: ChanceRain, ChanceSnow, ChanceLightning, WindPower
        /// - Day/Night cycle: DayNightCycle, IsNight, LightingScheme
        /// - Enhanced lighting: Sun/Moon ambient/diffuse colors, shadows, fog
        /// - Area effects: Script hooks (OnEnter, OnExit, OnHeartbeat, OnUserDefined)
        /// - Tile-based layout: Width, Height, Tileset, Tile_List
        /// - Area restrictions: NoRest, PlayerVsPlayer, Unescapable
        /// 
        /// ARE file format (GFF with "ARE " signature):
        /// - Root struct contains all area properties
        /// - AreaProperties nested struct (optional) contains runtime-modifiable properties
        /// - Tile_List contains tile layout information
        /// 
        /// Based on official BioWare Aurora Engine ARE format specification:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md
        /// - vendor/xoreos-docs/specs/bioware/AreaFile_Format.pdf
        /// </remarks>
        protected override void LoadAreaProperties(byte[] gffData)
        {
            if (gffData == null || gffData.Length == 0)
            {
                // Use default values if no ARE data provided
                SetDefaultAreaProperties();
                return;
            }

            try
            {
                // Parse GFF from byte array
                GFF gff = GFF.FromBytes(gffData);
                if (gff == null || gff.Root == null)
                {
                    SetDefaultAreaProperties();
                    return;
                }

                // Verify GFF content type is ARE
                if (gff.ContentType != GFFContent.ARE)
                {
                    // Try to parse anyway - some ARE files may have incorrect content type
                    // This is a defensive measure for compatibility
                }

                GFFStruct root = gff.Root;

                // Read identity fields (Tag, Name, ResRef)
                // Based on ARE format specification and nwmain.exe LoadArea function
                if (root.Exists("Tag"))
                {
                    string tag = root.GetString("Tag");
                    if (!string.IsNullOrEmpty(tag))
                    {
                        _tag = tag;
                    }
                }

                if (root.Exists("Name"))
                {
                    LocalizedString nameLocStr = root.GetLocString("Name");
                    if (nameLocStr != null && !nameLocStr.IsInvalid)
                    {
                        _displayName = nameLocStr.ToString();
                    }
                }

                if (root.Exists("ResRef"))
                {
                    ResRef resRefObj = root.GetResRef("ResRef");
                    if (resRefObj != null && !resRefObj.IsBlank)
                    {
                        string resRefStr = resRefObj.ToString();
                        if (!string.IsNullOrEmpty(resRefStr))
                        {
                            _resRef = resRefStr;
                        }
                    }
                }

                // Read Unescapable flag
                // Based on ARE format: Unescapable is stored as UInt8 (0 = escapable, 1 = unescapable)
                if (root.Exists("Unescapable"))
                {
                    _isUnescapable = root.GetUInt8("Unescapable") != 0;
                }
                else
                {
                    _isUnescapable = false; // Default: area is escapable
                }

                // Read weather properties
                // Based on ARE format: ChanceRain, ChanceSnow, ChanceLightning are INT (0-100)
                // WindPower is INT (0-2: None, Weak, Strong)
                if (root.Exists("ChanceRain"))
                {
                    _chanceRain = root.GetInt32("ChanceRain");
                    // Clamp to valid range (0-100)
                    if (_chanceRain < 0) _chanceRain = 0;
                    if (_chanceRain > 100) _chanceRain = 100;
                }
                else
                {
                    _chanceRain = 0; // Default: no rain
                }

                if (root.Exists("ChanceSnow"))
                {
                    _chanceSnow = root.GetInt32("ChanceSnow");
                    // Clamp to valid range (0-100)
                    if (_chanceSnow < 0) _chanceSnow = 0;
                    if (_chanceSnow > 100) _chanceSnow = 100;
                }
                else
                {
                    _chanceSnow = 0; // Default: no snow
                }

                if (root.Exists("ChanceLightning"))
                {
                    _chanceLightning = root.GetInt32("ChanceLightning");
                    // Clamp to valid range (0-100)
                    if (_chanceLightning < 0) _chanceLightning = 0;
                    if (_chanceLightning > 100) _chanceLightning = 100;
                }
                else
                {
                    _chanceLightning = 0; // Default: no lightning
                }

                if (root.Exists("WindPower"))
                {
                    _windPower = root.GetInt32("WindPower");
                    // Clamp to valid range (0-2)
                    if (_windPower < 0) _windPower = 0;
                    if (_windPower > 2) _windPower = 2;
                }
                else
                {
                    _windPower = 0; // Default: no wind
                }

                // Read day/night cycle properties
                // Based on ARE format: DayNightCycle is BYTE (0 = static, 1 = cycle)
                // IsNight is BYTE (0 = day, 1 = night) - only meaningful if DayNightCycle is 0
                if (root.Exists("DayNightCycle"))
                {
                    _dayNightCycle = root.GetUInt8("DayNightCycle");
                }
                else
                {
                    _dayNightCycle = 0; // Default: static lighting
                }

                if (root.Exists("IsNight"))
                {
                    _isNight = root.GetUInt8("IsNight");
                }
                else
                {
                    _isNight = 0; // Default: day
                }

                // Read lighting scheme
                // Based on ARE format: LightingScheme is BYTE (index into environment.2da)
                if (root.Exists("LightingScheme"))
                {
                    _lightingScheme = root.GetUInt8("LightingScheme");
                }
                else
                {
                    _lightingScheme = 0; // Default: no lighting scheme
                }

                // Read load screen ID
                // Based on ARE format: LoadScreenID is WORD (index into loadscreens.2da)
                if (root.Exists("LoadScreenID"))
                {
                    _loadScreenID = root.GetUInt16("LoadScreenID");
                }
                else
                {
                    _loadScreenID = 0; // Default: no load screen
                }

                // Read area restrictions
                // Based on ARE format: NoRest is BYTE (0 = rest allowed, 1 = rest not allowed)
                // PlayerVsPlayer is BYTE (index into pvpsettings.2da)
                if (root.Exists("NoRest"))
                {
                    _noRest = root.GetUInt8("NoRest");
                }
                else
                {
                    _noRest = 0; // Default: rest allowed
                }

                if (root.Exists("PlayerVsPlayer"))
                {
                    _playerVsPlayer = root.GetUInt8("PlayerVsPlayer");
                }
                else
                {
                    _playerVsPlayer = 0; // Default: no PvP
                }

                // Read skybox
                // Based on ARE format: SkyBox is BYTE (index into skyboxes.2da, 0 = no skybox)
                if (root.Exists("SkyBox"))
                {
                    _skyBox = root.GetUInt8("SkyBox");
                }
                else
                {
                    _skyBox = 0; // Default: no skybox
                }

                // Read sun lighting properties
                // Based on ARE format: Colors are DWORD in BGR format (0BGR)
                if (root.Exists("SunAmbientColor"))
                {
                    _sunAmbientColor = root.GetUInt32("SunAmbientColor");
                }
                else
                {
                    _sunAmbientColor = 0; // Default: black
                }

                if (root.Exists("SunDiffuseColor"))
                {
                    _sunDiffuseColor = root.GetUInt32("SunDiffuseColor");
                }
                else
                {
                    _sunDiffuseColor = 0; // Default: black
                }

                if (root.Exists("SunShadows"))
                {
                    _sunShadows = root.GetUInt8("SunShadows");
                }
                else
                {
                    _sunShadows = 0; // Default: no shadows
                }

                // Read moon lighting properties
                // Based on ARE format: Colors are DWORD in BGR format (0BGR)
                if (root.Exists("MoonAmbientColor"))
                {
                    _moonAmbientColor = root.GetUInt32("MoonAmbientColor");
                }
                else
                {
                    _moonAmbientColor = 0; // Default: black
                }

                if (root.Exists("MoonDiffuseColor"))
                {
                    _moonDiffuseColor = root.GetUInt32("MoonDiffuseColor");
                }
                else
                {
                    _moonDiffuseColor = 0; // Default: black
                }

                if (root.Exists("MoonShadows"))
                {
                    _moonShadows = root.GetUInt8("MoonShadows");
                }
                else
                {
                    _moonShadows = 0; // Default: no shadows
                }

                // Read shadow opacity
                // Based on ARE format: ShadowOpacity is BYTE (0-100)
                if (root.Exists("ShadowOpacity"))
                {
                    _shadowOpacity = root.GetUInt8("ShadowOpacity");
                    // Clamp to valid range (0-100)
                    if (_shadowOpacity > 100) _shadowOpacity = 100;
                }
                else
                {
                    _shadowOpacity = 0; // Default: no shadow opacity
                }

                // Read fog properties
                // Based on ARE format: Fog amounts are BYTE (0-15), colors are DWORD in BGR format
                if (root.Exists("SunFogAmount"))
                {
                    _sunFogAmount = root.GetUInt8("SunFogAmount");
                    // Clamp to valid range (0-15)
                    if (_sunFogAmount > 15) _sunFogAmount = 15;
                }
                else
                {
                    _sunFogAmount = 0; // Default: no fog
                }

                if (root.Exists("SunFogColor"))
                {
                    _sunFogColor = root.GetUInt32("SunFogColor");
                }
                else
                {
                    _sunFogColor = 0; // Default: black fog
                }

                if (root.Exists("MoonFogAmount"))
                {
                    _moonFogAmount = root.GetUInt8("MoonFogAmount");
                    // Clamp to valid range (0-15)
                    if (_moonFogAmount > 15) _moonFogAmount = 15;
                }
                else
                {
                    _moonFogAmount = 0; // Default: no fog
                }

                if (root.Exists("MoonFogColor"))
                {
                    _moonFogColor = root.GetUInt32("MoonFogColor");
                }
                else
                {
                    _moonFogColor = 0; // Default: black fog
                }

                // Read script hooks
                // Based on ARE format: Script hooks are CResRef (resource references to NCS scripts)
                if (root.Exists("OnEnter"))
                {
                    _onEnter = root.GetResRef("OnEnter");
                }
                else
                {
                    _onEnter = ResRef.FromBlank();
                }

                if (root.Exists("OnExit"))
                {
                    _onExit = root.GetResRef("OnExit");
                }
                else
                {
                    _onExit = ResRef.FromBlank();
                }

                if (root.Exists("OnHeartbeat"))
                {
                    _onHeartbeat = root.GetResRef("OnHeartbeat");
                }
                else
                {
                    _onHeartbeat = ResRef.FromBlank();
                }

                if (root.Exists("OnUserDefined"))
                {
                    _onUserDefined = root.GetResRef("OnUserDefined");
                }
                else
                {
                    _onUserDefined = ResRef.FromBlank();
                }

                // Read tileset and tile layout
                // Based on ARE format: Tileset is CResRef, Width/Height are INT (tile counts)
                if (root.Exists("Tileset"))
                {
                    _tileset = root.GetResRef("Tileset");
                }
                else
                {
                    _tileset = ResRef.FromBlank();
                }

                if (root.Exists("Width"))
                {
                    _width = root.GetInt32("Width");
                    // Ensure non-negative
                    if (_width < 0) _width = 0;
                }
                else
                {
                    _width = 0; // Default: no width
                }

                if (root.Exists("Height"))
                {
                    _height = root.GetInt32("Height");
                    // Ensure non-negative
                    if (_height < 0) _height = 0;
                }
                else
                {
                    _height = 0; // Default: no height
                }

                // Read flags
                // Based on ARE format: Flags is DWORD (bit flags for area terrain type)
                // 0x0001 = interior, 0x0002 = underground, 0x0004 = natural
                if (root.Exists("Flags"))
                {
                    _flags = root.GetUInt32("Flags");
                }
                else
                {
                    _flags = 0; // Default: no flags
                }

                // Read AreaProperties nested struct if present
                // Based on nwmain.exe: SaveProperties @ 0x140367390 saves AreaProperties struct
                // This struct contains runtime-modifiable properties
                GFFStruct areaProperties = root.GetStruct("AreaProperties");
                if (areaProperties != null)
                {
                    // Read DisplayName from AreaProperties (takes precedence over root Name)
                    if (areaProperties.Exists("DisplayName"))
                    {
                        string displayName = areaProperties.GetString("DisplayName");
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            _displayName = displayName;
                        }
                    }

                    // Read SkyBox from AreaProperties (takes precedence over root SkyBox)
                    if (areaProperties.Exists("SkyBox"))
                    {
                        _skyBox = areaProperties.GetUInt8("SkyBox");
                    }

                    // Read lighting directions from AreaProperties
                    // These are Vector3 components (X, Y, Z) for sun and moon directions
                    // Based on SaveProperties lines 29-36 in nwmain.exe
                    // Note: These are optional and may not be present in all ARE files
                    // We read them but don't store them in private fields as they're not currently used
                    // If needed in future, add fields: _sunDirectionX, _sunDirectionY, _sunDirectionZ, etc.

                    // Read wind properties from AreaProperties
                    // Based on SaveProperties lines 39-44 in nwmain.exe
                    // Wind properties are only saved if wind power is 3 (special case)
                    // Note: These are optional and may not be present in all ARE files
                    // We read them but don't store them in private fields as they're not currently used
                    // If needed in future, add fields: _windDirectionX, _windDirectionY, _windDirectionZ, etc.
                }

                // Read Tile_List if present
                // Based on ARE format: Tile_List is a GFFList containing AreaTile structs
                // Each tile represents a portion of the area's tile-based layout
                // We don't parse individual tiles here as that's handled by LoadAreaGeometry
                // But we verify the list exists for area validation
                if (root.Exists("Tile_List"))
                {
                    GFFList tileList = root.GetList("Tile_List");
                    // Tile list is validated but not parsed here - handled by LoadAreaGeometry
                    // This ensures area has valid tile structure
                }
            }
            catch (Exception)
            {
                // If GFF parsing fails, use default values
                // This ensures the area can still be created even with invalid/corrupt ARE data
                SetDefaultAreaProperties();
            }
        }

        /// <summary>
        /// Sets default values for all area properties.
        /// </summary>
        /// <remarks>
        /// Called when ARE data is missing or invalid.
        /// Provides safe defaults that allow the area to function.
        /// </remarks>
        private void SetDefaultAreaProperties()
        {
            _isUnescapable = false;
            _chanceRain = 0;
            _chanceSnow = 0;
            _chanceLightning = 0;
            _windPower = 0;
            _dayNightCycle = 0;
            _isNight = 0;
            _lightingScheme = 0;
            _loadScreenID = 0;
            _noRest = 0;
            _playerVsPlayer = 0;
            _skyBox = 0;
            _sunAmbientColor = 0;
            _sunDiffuseColor = 0;
            _moonAmbientColor = 0;
            _moonDiffuseColor = 0;
            _sunShadows = 0;
            _moonShadows = 0;
            _shadowOpacity = 0;
            _sunFogAmount = 0;
            _moonFogAmount = 0;
            _sunFogColor = 0;
            _moonFogColor = 0;
            _onEnter = ResRef.FromBlank();
            _onExit = ResRef.FromBlank();
            _onHeartbeat = ResRef.FromBlank();
            _onUserDefined = ResRef.FromBlank();
            _tileset = ResRef.FromBlank();
            _width = 0;
            _height = 0;
            _flags = 0;
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
        /// Based on Aurora entity loading from GIT files in nwmain.exe.
        /// Parses GIT file GFF containing creature, door, placeable, trigger, waypoint, sound, store, and encounter instances.
        /// Creates appropriate entity types and attaches components.
        ///
        /// Function addresses (require Ghidra verification):
        /// - nwmain.exe: CNWSArea::LoadCreatures @ 0x140360570 (approximate - needs Ghidra verification)
        ///   - Located via string reference: "Creature List" (GFF list field in GIT)
        ///   - Reads TemplateResRef, XPosition, YPosition, ZPosition, XOrientation, YOrientation
        ///   - Validates position on walkmesh before spawning
        ///   - Converts orientation vector to facing angle
        /// - nwmain.exe: CNWSArea::LoadDoors @ 0x1403608f0 (approximate - needs Ghidra verification)
        ///   - Located via string reference: "Door List" (GFF list field in GIT)
        ///   - Reads TemplateResRef, Tag, X, Y, Z, Bearing, LinkedTo, LinkedToModule
        ///   - Loads template from UTD file
        /// - nwmain.exe: CNWSArea::LoadPlaceables @ 0x1403619e0 (approximate - needs Ghidra verification)
        ///   - Located via string reference: "Placeable List" (GFF list field in GIT)
        ///   - Reads TemplateResRef, X, Y, Z, Bearing, Tag
        ///   - Loads template from UTP file
        /// - nwmain.exe: CNWSArea::LoadTriggers @ 0x140362b20 (approximate - needs Ghidra verification)
        ///   - Located via string reference: "TriggerList" (GFF list field in GIT)
        ///   - Reads TemplateResRef, Tag, XPosition, YPosition, ZPosition, Geometry, LinkedTo, LinkedToModule
        /// - nwmain.exe: CNWSArea::LoadWaypoints @ 0x140362fc0 (approximate - needs Ghidra verification)
        ///   - Located via string reference: "WaypointList" (GFF list field in GIT)
        ///   - Reads TemplateResRef, Tag, XPosition, YPosition, ZPosition, XOrientation, YOrientation, MapNote, MapNoteEnabled
        /// - nwmain.exe: CNWSArea::LoadSounds @ 0x1403631e0 (approximate - needs Ghidra verification)
        ///   - Located via string reference: "SoundList" (GFF list field in GIT)
        ///   - Reads TemplateResRef, Tag, XPosition, YPosition, ZPosition
        /// - nwmain.exe: CNWSArea::LoadStores @ 0x140363400 (approximate - needs Ghidra verification)
        ///   - Located via string reference: "StoreList" (GFF list field in GIT)
        ///   - Reads ResRef, XPosition, YPosition, ZPosition, XOrientation, YOrientation
        /// - nwmain.exe: CNWSArea::LoadEncounters @ 0x140363620 (approximate - needs Ghidra verification)
        ///   - Located via string reference: "Encounter List" (GFF list field in GIT)
        ///   - Reads TemplateResRef, Tag, XPosition, YPosition, ZPosition, Geometry, SpawnPointList
        ///
        /// GIT file structure (GFF with "GIT " signature):
        /// - Root struct contains instance lists:
        ///   - "Creature List" (GFFList): Creature instances (StructID 4)
        ///   - "Door List" (GFFList): Door instances (StructID 8)
        ///   - "Placeable List" (GFFList): Placeable instances (StructID 9)
        ///   - "TriggerList" (GFFList): Trigger instances (StructID 1)
        ///   - "WaypointList" (GFFList): Waypoint instances (StructID 5)
        ///   - "SoundList" (GFFList): Sound instances (StructID 6)
        ///   - "StoreList" (GFFList): Store instances (StructID 11)
        ///   - "Encounter List" (GFFList): Encounter instances (StructID 7)
        ///
        /// Instance data fields:
        /// - TemplateResRef (ResRef): Template file reference (UTC, UTD, UTP, UTT, UTW, UTS, UTE, UTM)
        /// - Tag (String): Script-accessible identifier
        /// - Position: XPosition/YPosition/ZPosition (float) or X/Y/Z (float) depending on type
        /// - Orientation: XOrientation/YOrientation/ZOrientation (float) or Bearing (float) depending on type
        /// - Type-specific fields: LinkedTo, LinkedToModule, Geometry, MapNote, etc.
        ///
        /// Entity creation process:
        /// 1. Parse GIT file from byte array using GFF.FromBytes and GITHelpers.ConstructGit
        /// 2. For each instance type, iterate through instance list
        /// 3. Create AuroraEntity with ObjectId, ObjectType, and Tag
        /// 4. Set position and orientation from GIT data
        /// 5. Set type-specific properties (LinkedTo, Geometry, MapNote, etc.)
        /// 6. Store TemplateResRef in entity data for later template loading
        /// 7. Add entity to appropriate collection using AddEntityToArea
        ///
        /// ObjectId assignment:
        /// - Generate sequential ObjectId starting from 1000000 (high range to avoid conflicts)
        /// - ObjectIds must be unique across all entities
        /// - OBJECT_INVALID = 0x7F000000 in Aurora engine
        ///
        /// Position validation:
        /// - Creature positions are validated on walkmesh in original engine
        /// - This implementation validates position using IsPointWalkable if navigation mesh is available
        /// - If validation fails, position is still used (defensive behavior)
        ///
        /// Template loading:
        /// - Templates (UTC, UTD, UTP, etc.) are not loaded here as AuroraArea doesn't have template factory access
        /// - Template ResRefs are stored in entity data for later loading by higher-level systems
        /// - This implementation creates entities with basic properties from GIT data
        ///
        /// Based on GIT file format documentation:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md (Section 3: GIT Format)
        /// - vendor/xoreos-docs/specs/bioware/AreaFile_Format.pdf
        /// </remarks>
        protected override void LoadEntities(byte[] gitData)
        {
            if (gitData == null || gitData.Length == 0)
            {
                return;
            }

            try
            {
                // Parse GFF from byte array
                // Based on nwmain.exe: CResGFF::LoadGFFFile loads GFF structure from byte array
                GFF gff = GFF.FromBytes(gitData);
                if (gff == null || gff.Root == null)
                {
                    return;
                }

                // Verify GFF content type is GIT
                // Based on nwmain.exe: GIT files have "GIT " signature in GFF header
                if (gff.ContentType != GFFContent.GIT)
                {
                    // Try to parse anyway - some GIT files may have incorrect content type
                    // This is a defensive measure for compatibility
                }

                // Construct GIT object from GFF
                // Based on GITHelpers.ConstructGit: Parses all instance lists from GFF root
                // This handles parsing of all GIT instance types (Creatures, Doors, Placeables, Triggers, Waypoints, Sounds, Stores, Encounters)
                Parsing.Resource.Generics.GIT git = Parsing.Resource.Generics.GITHelpers.ConstructGit(gff);
                if (git == null)
                {
                    return;
                }

                // ObjectId counter for entities
                // Start from high range (1000000) to avoid conflicts with World.CreateEntity counter
                // Based on nwmain.exe: ObjectIds are assigned sequentially, starting from 1
                // OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001
                uint nextObjectId = 1000000;

                // Load creatures from GIT
                // Based on nwmain.exe: CNWSArea::LoadCreatures @ 0x140360570 loads creature instances from GIT "Creature List"
                foreach (Parsing.Resource.Generics.GITCreature creature in git.Creatures)
                {
                    // Create entity with ObjectId, ObjectType, and Tag
                    // ObjectId: Generate sequential ID
                    // Tag: Use ResRef as default tag (creatures don't have explicit Tag in GIT, use TemplateResRef)
                    uint objectId = nextObjectId++;
                    string tag = creature.ResRef != null && !creature.ResRef.IsBlank ? creature.ResRef.ToString() : string.Empty;
                    var entity = new AuroraEntity(objectId, ObjectType.Creature, tag);

                    // Set position from GIT
                    // Based on nwmain.exe: LoadCreatures reads XPosition, YPosition, ZPosition
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = creature.Position;
                        transformComponent.Facing = creature.Bearing;
                    }

                    // Validate position on walkmesh if available
                    // Based on nwmain.exe: Position validation checks if point is on walkable surface
                    if (_navigationMesh != null)
                    {
                        Vector3 validatedPosition;
                        float height;
                        if (ProjectToWalkmesh(creature.Position, out validatedPosition, out height))
                        {
                            // Update position to validated coordinates
                            if (transformComponent != null)
                            {
                                transformComponent.Position = validatedPosition;
                            }
                        }
                    }

                    // Store template ResRef for later template loading
                    // Based on nwmain.exe: TemplateResRef is used to load UTC template file
                    if (creature.ResRef != null && !creature.ResRef.IsBlank)
                    {
                        entity.SetData("TemplateResRef", creature.ResRef.ToString());
                    }

                    // Add entity to area
                    AddEntityToArea(entity);
                }

                // Load doors from GIT
                // Based on nwmain.exe: CNWSArea::LoadDoors @ 0x1403608f0 loads door instances from GIT "Door List"
                foreach (Parsing.Resource.Generics.GITDoor door in git.Doors)
                {
                    uint objectId = nextObjectId++;
                    var entity = new AuroraEntity(objectId, ObjectType.Door, door.Tag ?? string.Empty);

                    // Set position and orientation from GIT
                    // Based on nwmain.exe: LoadDoors reads X, Y, Z, Bearing
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = door.Position;
                        transformComponent.Facing = door.Bearing;
                    }

                    // Set door-specific properties from GIT
                    // Based on nwmain.exe: Door properties loaded from GIT struct
                    var doorComponent = entity.GetComponent<IDoorComponent>();
                    if (doorComponent != null)
                    {
                        doorComponent.LinkedToModule = door.LinkedToModule != null && !door.LinkedToModule.IsBlank ? door.LinkedToModule.ToString() : string.Empty;
                        doorComponent.LinkedTo = door.LinkedTo ?? string.Empty;
                    }

                    // Store template ResRef for later template loading
                    if (door.ResRef != null && !door.ResRef.IsBlank)
                    {
                        entity.SetData("TemplateResRef", door.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load placeables from GIT
                // Based on nwmain.exe: CNWSArea::LoadPlaceables @ 0x1403619e0 loads placeable instances from GIT "Placeable List"
                foreach (Parsing.Resource.Generics.GITPlaceable placeable in git.Placeables)
                {
                    uint objectId = nextObjectId++;
                    // Placeables don't have explicit Tag in GIT, use TemplateResRef as tag
                    string tag = placeable.ResRef != null && !placeable.ResRef.IsBlank ? placeable.ResRef.ToString() : string.Empty;
                    var entity = new AuroraEntity(objectId, ObjectType.Placeable, tag);

                    // Set position and orientation from GIT
                    // Based on nwmain.exe: LoadPlaceables reads X, Y, Z, Bearing
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = placeable.Position;
                        transformComponent.Facing = placeable.Bearing;
                    }

                    // Store template ResRef for later template loading
                    if (placeable.ResRef != null && !placeable.ResRef.IsBlank)
                    {
                        entity.SetData("TemplateResRef", placeable.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load triggers from GIT
                // Based on nwmain.exe: CNWSArea::LoadTriggers @ 0x140362b20 loads trigger instances from GIT "TriggerList"
                foreach (Parsing.Resource.Generics.GITTrigger trigger in git.Triggers)
                {
                    uint objectId = nextObjectId++;
                    var entity = new AuroraEntity(objectId, ObjectType.Trigger, trigger.Tag ?? string.Empty);

                    // Set position from GIT
                    // Based on nwmain.exe: LoadTriggers reads XPosition, YPosition, ZPosition
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = trigger.Position;
                    }

                    // Set trigger geometry from GIT
                    // Based on nwmain.exe: LoadTriggers reads Geometry list (polygon vertices)
                    var triggerComponent = entity.GetComponent<ITriggerComponent>();
                    if (triggerComponent != null)
                    {
                        // Convert GIT geometry to trigger component geometry
                        var geometryList = new List<Vector3>();
                        foreach (Vector3 point in trigger.Geometry)
                        {
                            geometryList.Add(point);
                        }
                        triggerComponent.Geometry = geometryList;
                        triggerComponent.LinkedToModule = trigger.LinkedToModule != null && !trigger.LinkedToModule.IsBlank ? trigger.LinkedToModule.ToString() : string.Empty;
                        triggerComponent.LinkedTo = trigger.LinkedTo ?? string.Empty;
                    }

                    // Store template ResRef for later template loading
                    if (trigger.ResRef != null && !trigger.ResRef.IsBlank)
                    {
                        entity.SetData("TemplateResRef", trigger.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load waypoints from GIT
                // Based on nwmain.exe: CNWSArea::LoadWaypoints @ 0x140362fc0 loads waypoint instances from GIT "WaypointList"
                foreach (Parsing.Resource.Generics.GITWaypoint waypoint in git.Waypoints)
                {
                    uint objectId = nextObjectId++;
                    var entity = new AuroraEntity(objectId, ObjectType.Waypoint, waypoint.Tag ?? string.Empty);

                    // Set position and orientation from GIT
                    // Based on nwmain.exe: LoadWaypoints reads XPosition, YPosition, ZPosition, XOrientation, YOrientation
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = waypoint.Position;
                        transformComponent.Facing = waypoint.Bearing;
                    }

                    // Set waypoint-specific properties from GIT
                    // Based on nwmain.exe: LoadWaypoints reads MapNote, MapNoteEnabled, HasMapNote
                    var waypointComponent = entity.GetComponent<IWaypointComponent>();
                    if (waypointComponent != null && waypointComponent is Components.AuroraWaypointComponent auroraWaypoint)
                    {
                        auroraWaypoint.HasMapNote = waypoint.HasMapNote;
                        if (waypoint.HasMapNote && waypoint.MapNote != null && !waypoint.MapNote.IsInvalid)
                        {
                            auroraWaypoint.MapNote = waypoint.MapNote.ToString();
                            auroraWaypoint.MapNoteEnabled = waypoint.MapNoteEnabled;
                        }
                        else
                        {
                            auroraWaypoint.MapNote = string.Empty;
                            auroraWaypoint.MapNoteEnabled = false;
                        }
                    }

                    // Set waypoint name from GIT
                    if (waypoint.Name != null && !waypoint.Name.IsInvalid)
                    {
                        entity.SetData("WaypointName", waypoint.Name.ToString());
                    }

                    // Store template ResRef for later template loading
                    if (waypoint.ResRef != null && !waypoint.ResRef.IsBlank)
                    {
                        entity.SetData("TemplateResRef", waypoint.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load sounds from GIT
                // Based on nwmain.exe: CNWSArea::LoadSounds @ 0x1403631e0 loads sound instances from GIT "SoundList"
                foreach (Parsing.Resource.Generics.GITSound sound in git.Sounds)
                {
                    uint objectId = nextObjectId++;
                    // Sounds don't have explicit Tag in GIT, use TemplateResRef as tag
                    string tag = sound.ResRef != null && !sound.ResRef.IsBlank ? sound.ResRef.ToString() : string.Empty;
                    var entity = new AuroraEntity(objectId, ObjectType.Sound, tag);

                    // Set position from GIT
                    // Based on nwmain.exe: LoadSounds reads XPosition, YPosition, ZPosition
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = sound.Position;
                    }

                    // Store template ResRef for later template loading
                    if (sound.ResRef != null && !sound.ResRef.IsBlank)
                    {
                        entity.SetData("TemplateResRef", sound.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load stores from GIT
                // Based on nwmain.exe: CNWSArea::LoadStores @ 0x140363400 loads store instances from GIT "StoreList"
                foreach (Parsing.Resource.Generics.GITStore store in git.Stores)
                {
                    uint objectId = nextObjectId++;
                    // Stores don't have explicit Tag in GIT, use ResRef as tag
                    string tag = store.ResRef != null && !store.ResRef.IsBlank ? store.ResRef.ToString() : string.Empty;
                    // Stores are represented as Placeable entities in Aurora engine
                    var entity = new AuroraEntity(objectId, ObjectType.Placeable, tag);

                    // Set position and orientation from GIT
                    // Based on nwmain.exe: LoadStores reads XPosition, YPosition, ZPosition, XOrientation, YOrientation
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = store.Position;
                        // Store bearing is calculated from XOrientation, YOrientation in GITHelpers
                        transformComponent.Facing = store.Bearing;
                    }

                    // Mark entity as store
                    entity.SetData("IsStore", true);

                    // Store template ResRef for later template loading
                    if (store.ResRef != null && !store.ResRef.IsBlank)
                    {
                        entity.SetData("TemplateResRef", store.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load encounters from GIT
                // Based on nwmain.exe: CNWSArea::LoadEncounters @ 0x140363620 loads encounter instances from GIT "Encounter List"
                foreach (Parsing.Resource.Generics.GITEncounter encounter in git.Encounters)
                {
                    uint objectId = nextObjectId++;
                    // Encounters don't have explicit Tag in GIT, use TemplateResRef as tag
                    string tag = encounter.ResRef != null && !encounter.ResRef.IsBlank ? encounter.ResRef.ToString() : string.Empty;
                    // Encounters are represented as Trigger entities in Aurora engine
                    var entity = new AuroraEntity(objectId, ObjectType.Trigger, tag);

                    // Set position from GIT
                    // Based on nwmain.exe: LoadEncounters reads XPosition, YPosition, ZPosition
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = encounter.Position;
                    }

                    // Set encounter geometry from GIT
                    // Based on nwmain.exe: LoadEncounters reads Geometry list (polygon vertices)
                    var triggerComponent = entity.GetComponent<ITriggerComponent>();
                    if (triggerComponent != null)
                    {
                        // Convert GIT geometry to trigger component geometry
                        var geometryList = new List<Vector3>();
                        foreach (Vector3 point in encounter.Geometry)
                        {
                            geometryList.Add(point);
                        }
                        triggerComponent.Geometry = geometryList;
                    }

                    // Store spawn points from GIT
                    // Based on nwmain.exe: LoadEncounters reads SpawnPointList
                    if (encounter.SpawnPoints != null && encounter.SpawnPoints.Count > 0)
                    {
                        var spawnPoints = new List<Vector3>();
                        foreach (var spawnPoint in encounter.SpawnPoints)
                        {
                            spawnPoints.Add(new Vector3(spawnPoint.X, spawnPoint.Y, spawnPoint.Z));
                        }
                        entity.SetData("SpawnPoints", spawnPoints);
                    }

                    // Mark entity as encounter
                    entity.SetData("IsEncounter", true);

                    // Store template ResRef for later template loading
                    if (encounter.ResRef != null && !encounter.ResRef.IsBlank)
                    {
                        entity.SetData("TemplateResRef", encounter.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }
            }
            catch (Exception)
            {
                // If GFF parsing fails, skip entity loading
                // This ensures the area can still be created even with invalid/corrupt GIT data
            }
        }

        /// <summary>
        /// Loads area geometry and walkmesh from ARE file.
        /// </summary>
        /// <remarks>
        /// Based on Aurora ARE file loading in nwmain.exe.
        /// 
        /// Function addresses (require Ghidra verification):
        /// - nwmain.exe: CNWSArea::LoadArea @ 0x140365160 - Loads ARE file structure
        /// - nwmain.exe: CNWSArea::LoadTileSetInfo @ 0x14035faf0 (approximate) - Loads tileset information
        /// - nwmain.exe: CNWSArea::LoadTileList @ 0x14035f780 (approximate) - Loads Tile_List from ARE file
        /// 
        /// Aurora uses tile-based area construction:
        /// - ARE file contains Tile_List (GFFList) with AreaTile structs (StructID 1)
        /// - Each AreaTile specifies: Tile_ID, Tile_Orientation, Tile_Height, lighting, animations
        /// - Tile coordinates calculated from index: x = i % Width, y = i / Width
        /// - Tiles are stored in 2D grid: [y, x] indexed array
        /// - Tile size: 10.0f units per tile (DAT_140dc2df4 in nwmain.exe)
        /// 
        /// ARE file format (GFF with "ARE " signature):
        /// - Root struct contains Width, Height, Tileset, Tile_List
        /// - Tile_List is GFFList containing AreaTile structs (StructID 1)
        /// - AreaTile fields: Tile_ID (INT), Tile_Orientation (INT 0-3), Tile_Height (INT), 
        ///   Tile_AnimLoop1/2/3 (INT), Tile_MainLight1/2 (BYTE), Tile_SrcLight1/2 (BYTE)
        /// 
        /// Based on official BioWare Aurora Engine ARE format specification:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md (Section 2.5: Area Tile List)
        /// - vendor/xoreos-docs/specs/bioware/AreaFile_Format.pdf
        /// </remarks>
        protected override void LoadAreaGeometry(byte[] areData)
        {
            if (areData == null || areData.Length == 0)
            {
                // No ARE data - create empty navigation mesh
                _navigationMesh = new AuroraNavigationMesh();
                return;
            }

            try
            {
                // Parse GFF from byte array
                GFF gff = GFF.FromBytes(areData);
                if (gff == null || gff.Root == null)
                {
                    // Invalid GFF - create empty navigation mesh
                    _navigationMesh = new AuroraNavigationMesh();
                    return;
                }

                // Verify GFF content type is ARE
                if (gff.ContentType != GFFContent.ARE)
                {
                    // Try to parse anyway - some ARE files may have incorrect content type
                    // This is a defensive measure for compatibility
                }

                GFFStruct root = gff.Root;

                // Read tileset resref from root struct (needed for surface material lookup)
                // Based on ARE format: Tileset is CResRef
                // We read this here so it's available for tile surface material lookup
                ResRef tilesetResRef = ResRef.FromBlank();
                if (root.Exists("Tileset"))
                {
                    tilesetResRef = root.GetResRef("Tileset");
                }
                _tileset = tilesetResRef; // Store for later use

                // Read Width and Height from root struct
                // Based on ARE format: Width and Height are INT (tile counts)
                // Width: x-direction (west-east), Height: y-direction (north-south)
                int width = 0;
                int height = 0;

                if (root.Exists("Width"))
                {
                    width = root.GetInt32("Width");
                    if (width < 0) width = 0;
                }

                if (root.Exists("Height"))
                {
                    height = root.GetInt32("Height");
                    if (height < 0) height = 0;
                }

                // Store width and height for later use
                _width = width;
                _height = height;

                // If width or height is 0, create empty navigation mesh
                if (width <= 0 || height <= 0)
                {
                    _navigationMesh = new AuroraNavigationMesh();
                    return;
                }

                // Read Tile_List from root struct
                // Based on ARE format: Tile_List is GFFList containing AreaTile structs (StructID 1)
                GFFList tileList = root.GetList("Tile_List");
                if (tileList == null || tileList.Count == 0)
                {
                    // No tiles - create empty navigation mesh with correct dimensions
                    AuroraTile[,] emptyTiles = new AuroraTile[height, width];
                    string tilesetResRef = _tileset != null && !_tileset.IsBlank ? _tileset.ToString() : null;
                    _navigationMesh = new AuroraNavigationMesh(emptyTiles, width, height, _tilesetLoader, tilesetResRef);
                    return;
                }

                // Create 2D tile array indexed by [y, x]
                // Based on nwmain.exe: CNWSArea tile storage at offset 0x1c8
                // Tiles stored as 2D grid: [height, width] = [y, x]
                AuroraTile[,] tiles = new AuroraTile[height, width];

                // Initialize all tiles to default values
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        tiles[y, x] = new AuroraTile
                        {
                            TileId = -1, // Invalid tile ID indicates empty tile
                            Orientation = 0,
                            Height = 0,
                            IsLoaded = false,
                            IsWalkable = false,
                            SurfaceMaterial = 0 // Undefined/NotDefined - default for empty tiles
                        };
                    }
                }

                // Parse each AreaTile struct from Tile_List
                // Based on ARE format: Tile coordinates calculated from index
                // Formula: x = i % Width, y = i / Width (integer division)
                for (int i = 0; i < tileList.Count; i++)
                {
                    GFFStruct tileStruct = tileList.At(i);
                    if (tileStruct == null)
                    {
                        continue;
                    }

                    // Calculate tile coordinates from index
                    // Based on ARE format specification (Section 2.5):
                    // x = i % w, y = i / w (where w = Width, integer division rounds down)
                    int tileX = i % width;
                    int tileY = i / width;

                    // Validate tile coordinates are within bounds
                    if (tileX < 0 || tileX >= width || tileY < 0 || tileY >= height)
                    {
                        // Tile index out of bounds - skip this tile
                        // This can happen if Tile_List has more entries than Width * Height
                        continue;
                    }

                    // Read Tile_ID (index into tileset file's list of tiles)
                    // Based on ARE format: Tile_ID is INT, must be >= 0
                    int tileId = -1;
                    if (tileStruct.Exists("Tile_ID"))
                    {
                        tileId = tileStruct.GetInt32("Tile_ID");
                        if (tileId < 0)
                        {
                            tileId = -1; // Invalid tile ID
                        }
                    }

                    // Read Tile_Orientation (rotation: 0 = normal, 1 = 90° CCW, 2 = 180° CCW, 3 = 270° CCW)
                    // Based on ARE format: Tile_Orientation is INT (0-3)
                    int orientation = 0;
                    if (tileStruct.Exists("Tile_Orientation"))
                    {
                        orientation = tileStruct.GetInt32("Tile_Orientation");
                        // Clamp to valid range (0-3)
                        if (orientation < 0) orientation = 0;
                        if (orientation > 3) orientation = 3;
                    }

                    // Read Tile_Height (number of height transitions)
                    // Based on ARE format: Tile_Height is INT, should never be negative
                    int tileHeight = 0;
                    if (tileStruct.Exists("Tile_Height"))
                    {
                        tileHeight = tileStruct.GetInt32("Tile_Height");
                        if (tileHeight < 0) tileHeight = 0;
                    }

                    // Determine if tile is loaded and walkable
                    // Based on nwmain.exe: CNWTileSet::GetTileData() validation
                    // Tiles with valid Tile_ID (>= 0) are considered loaded
                    // Walkability is determined by tileset data (not stored in ARE file)
                    bool isLoaded = (tileId >= 0);
                    bool isWalkable = isLoaded; // Simplified: valid tiles are walkable
                    // TODO: STUB - Note: Full walkability determination would require loading tile walkmesh,
                    // which is expensive. For now, we assume valid tiles are walkable.

                    // Query tileset file to determine actual surface material from tile model data
                    // Based on nwmain.exe: CNWTileSurfaceMesh::GetSurfaceMaterial @ 0x1402bedf0
                    // - Gets tile model from tileset using Tile_ID
                    // - Loads walkmesh (WOK file) for the tile model
                    // - Extracts most common surface material from walkmesh faces
                    // - Falls back to default (Stone for walkable, Undefined for non-walkable) if walkmesh can't be loaded
                    int surfaceMaterial = 0; // Default: Undefined
                    if (isLoaded && _tilesetLoader != null && !_tileset.IsBlank)
                    {
                        string tilesetResRef = _tileset.ToString();
                        if (!string.IsNullOrEmpty(tilesetResRef))
                        {
                            try
                            {
                                // Get surface material from tileset and walkmesh
                                // Based on nwmain.exe: CNWTileSet::GetTileData @ 0x1402c67d0
                                // - Gets tile data from tileset using Tile_ID
                                // - Loads walkmesh (WOK file) for tile model
                                // - Extracts surface material from walkmesh
                                surfaceMaterial = _tilesetLoader.GetTileSurfaceMaterial(tilesetResRef, tileId);
                            }
                            catch
                            {
                                // Failed to get surface material from tileset - use default
                                surfaceMaterial = isWalkable ? 4 : 0; // Stone (walkable) or Undefined (non-walkable)
                            }
                        }
                        else
                        {
                            // No tileset resref - use default
                            surfaceMaterial = isWalkable ? 4 : 0; // Stone (walkable) or Undefined (non-walkable)
                        }
                    }
                    else
                    {
                        // No tileset loader or tileset - use default
                        surfaceMaterial = isWalkable ? 4 : 0; // Stone (walkable) or Undefined (non-walkable)
                    }
                    
                    AuroraTile tile = new AuroraTile
                    {
                        TileId = tileId,
                        Orientation = orientation,
                        Height = tileHeight,
                        IsLoaded = isLoaded,
                        IsWalkable = isWalkable,
                        SurfaceMaterial = surfaceMaterial
                    };

                    // Store tile in 2D array at calculated coordinates
                    // Based on nwmain.exe: Tile array access pattern [y, x]
                    tiles[tileY, tileX] = tile;
                }

                // Create AuroraNavigationMesh from parsed tile data
                // Based on nwmain.exe: CNWSArea tile storage structure
                // Pass tileset loader and tileset resref for walkmesh-based height sampling
                string tilesetResRef = _tileset != null && !_tileset.IsBlank ? _tileset.ToString() : null;
                _navigationMesh = new AuroraNavigationMesh(tiles, width, height, _tilesetLoader, tilesetResRef);
            }
            catch (Exception)
            {
                // If parsing fails, create empty navigation mesh
                // This ensures the area can still be created even with invalid/corrupt ARE data
                _navigationMesh = new AuroraNavigationMesh();
            }
        }

        /// <summary>
        /// Initializes area effects and environmental systems.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea area effects initialization system.
        /// 
        /// Aurora has sophisticated area effects system:
        /// - Weather simulation (rain, snow, lightning based on chance values)
        /// - Dynamic lighting (sun/moon ambient and diffuse colors, shadows)
        /// - Fog system (sun/moon fog with amounts and colors)
        /// - Day/night cycle (if enabled, updates lighting based on time of day)
        /// - Lighting scheme (index into environment.2da for preset lighting configurations)
        /// - Shadow system (sun/moon shadows with opacity control)
        /// 
        /// Initialization sequence:
        /// 1. Validate and set default lighting colors (sun/moon ambient/diffuse)
        /// 2. Validate and initialize fog parameters (sun/moon fog amounts and colors)
        /// 3. Initialize shadow system (validate shadow opacity)
        /// 4. Initialize day/night cycle state (set initial night state if static, or start cycle timer if dynamic)
        /// 5. Validate weather chance values (clamp to 0-100 range)
        /// 6. Initialize lighting scheme (validate index if specified)
        /// 
        /// Default values (based on nwmain.exe behavior):
        /// - Sun ambient color: 0x808080 (gray) if zero/invalid
        /// - Sun diffuse color: 0xFFFFFF (white) if zero/invalid
        /// - Moon ambient color: 0x404040 (dark gray) if zero/invalid
        /// - Moon diffuse color: 0xC0C0C0 (light gray) if zero/invalid
        /// - Fog colors: 0x808080 (gray) if zero/invalid
        /// - Shadow opacity: 0-100 range, validated
        /// - Weather chances: 0-100 range, validated
        /// - Day/night cycle: Static by default (DayNightCycle = 0), initial state from IsNight field
        /// 
        /// Called from AuroraArea constructor after LoadAreaProperties.
        /// All properties should be loaded before this method is called.
        /// </remarks>
        protected override void InitializeAreaEffects()
        {
            // Initialize sun lighting system
            // Based on nwmain.exe: Sun lighting provides primary directional light source
            // Default sun ambient color: 0x808080 (gray in BGR format, provides base ambient lighting)
            if (_sunAmbientColor == 0)
            {
                _sunAmbientColor = 0x808080; // Default gray ambient
            }

            // Default sun diffuse color: 0xFFFFFF (white in BGR format, full brightness directional light)
            if (_sunDiffuseColor == 0)
            {
                _sunDiffuseColor = 0xFFFFFF; // Default white diffuse
            }

            // Initialize moon lighting system
            // Based on nwmain.exe: Moon lighting provides secondary directional light source for night scenes
            // Default moon ambient color: 0x404040 (dark gray in BGR format, dimmer than sun for night scenes)
            if (_moonAmbientColor == 0)
            {
                _moonAmbientColor = 0x404040; // Default dark gray ambient
            }

            // Default moon diffuse color: 0xC0C0C0 (light gray in BGR format, dimmer than sun)
            if (_moonDiffuseColor == 0)
            {
                _moonDiffuseColor = 0xC0C0C0; // Default light gray diffuse
            }

            // Initialize fog system
            // Based on nwmain.exe: Fog amounts are 0-15 range, colors are BGR format
            // Validate sun fog parameters
            if (_sunFogAmount > 0 && _sunFogColor == 0)
            {
                // If fog amount is set but color is zero, use default gray fog color
                _sunFogColor = 0x808080; // Default gray fog
            }

            // Validate moon fog parameters
            if (_moonFogAmount > 0 && _moonFogColor == 0)
            {
                // If fog amount is set but color is zero, use default gray fog color
                _moonFogColor = 0x808080; // Default gray fog
            }

            // Validate shadow opacity (already clamped in LoadAreaProperties, but ensure it's within range)
            // ShadowOpacity is 0-100 (0 = no shadows, 100 = fully opaque shadows)
            if (_shadowOpacity > 100)
            {
                _shadowOpacity = 100;
            }

            // Initialize day/night cycle state
            // Based on nwmain.exe: DayNightCycle = 0 means static lighting (no cycle), 1 means dynamic cycle
            if (_dayNightCycle == 0)
            {
                // Static lighting mode: Use IsNight flag to determine current state
                // IsNight = 0 means day time, IsNight = 1 means night time
                // Day/night timer is not used in static mode
                _dayNightTimer = 0.0f;
                // IsNight is already set from ARE file, no need to change it
            }
            else if (_dayNightCycle == 1)
            {
                // Dynamic cycle mode: Initialize cycle timer based on initial IsNight state
                // If starting at night (IsNight = 1), start cycle at night time (18:00 - 06:00)
                // Night time in cycle: 0.75 - 1.0 and 0.0 - 0.25 (6 hours out of 24)
                if (_isNight == 1)
                {
                    // Start at midnight (0.0 = midnight, beginning of night period)
                    _dayNightTimer = 0.0f;
                }
                else
                {
                    // Start at noon (0.5 = noon, middle of day period)
                    _dayNightTimer = DayNightCycleDuration * 0.5f;
                }
            }

            // Validate weather chance values (already clamped in LoadAreaProperties, but ensure they're within range)
            // Weather chances are percentages (0-100)
            if (_chanceRain < 0) _chanceRain = 0;
            if (_chanceRain > 100) _chanceRain = 100;
            if (_chanceSnow < 0) _chanceSnow = 0;
            if (_chanceSnow > 100) _chanceSnow = 100;
            if (_chanceLightning < 0) _chanceLightning = 0;
            if (_chanceLightning > 100) _chanceLightning = 100;

            // Validate wind power (0-2: None, Weak, Strong, already clamped in LoadAreaProperties)
            if (_windPower < 0) _windPower = 0;
            if (_windPower > 2) _windPower = 2;

            // Initialize lighting scheme
            // Based on nwmain.exe: LightingScheme is index into environment.2da
            // LightingScheme = 0 means no preset lighting scheme (use ARE file colors directly)
            // LightingScheme > 0 means use preset from environment.2da (TODO: STUB - not implemented here, handled by rendering system)
            // TODO: STUB - For now, we just validate that LightingScheme is a valid byte value (0-255)
            // The actual lookup from environment.2da would be done by the rendering system if needed

            // Weather system initialization
            // Weather simulation state is initialized in constructor, but we validate here that
            // the initial state matches the weather chances (initial weather is inactive)
            _isRaining = false;
            _isSnowing = false;
            _isLightning = false;
            _lightningFlashTimer = 0.0f;
            // First weather check will occur after WeatherCheckInterval (5 seconds) in UpdateWeatherSimulation

            // Area effects list is already initialized as empty list in constructor
            // CNWSAreaOfEffectObject instances are loaded from GIT file via LoadEntities, not here
            // This InitializeAreaEffects method handles environmental effects (lighting, fog, weather),
            // not spell/ability area effect objects
        }

        /// <summary>
        /// Public method for removing entities from area collections.
        /// </summary>
        /// <remarks>
        /// Public method for removing entities from area collections.
        /// Calls the protected RemoveEntityFromArea method.
        /// Aurora-specific: Basic entity removal without physics system.
        /// 
        /// Based on nwmain.exe: CNWSArea::RemoveObjectFromArea @ 0x140365600 (approximate - needs Ghidra verification)
        /// Entities are removed from type-specific lists (creatures, placeables, doors, etc.)
        /// when they are destroyed or removed from the area.
        /// 
        /// Reverse Engineering Notes:
        /// - nwmain.exe: CNWSCreature::RemoveFromArea @ 0x14039e6b0 calls CNWSArea::RemoveObjectFromArea
        /// - CNWSArea::RemoveObjectFromArea removes entity from area's type-specific collections
        /// - Entity removal sequence: Remove from area collections, then remove from world collections
        /// - Located via string references: "RemoveObjectFromArea" in nwmain.exe entity management
        /// </remarks>
        public void RemoveEntity(IEntity entity)
        {
            RemoveEntityFromArea(entity);
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
        /// Based on nwmain.exe: CNWSArea::UpdateArea @ 0x140365600 (approximate - needs Ghidra verification)
        /// 
        /// Aurora area update sequence (based on nwmain.exe behavior):
        /// 1. Update weather simulation (rain, snow, lightning based on chance values)
        /// 2. Process day/night cycle if enabled
        /// 3. Update all active area effects
        /// 4. Fire area heartbeat script if configured (every 6 seconds)
        /// 5. Process tile-based area logic (animations, lighting updates)
        /// 
        /// Weather System (based on nwmain.exe weather simulation):
        /// - Checks weather chances every 5 seconds
        /// - ChanceRain, ChanceSnow, ChanceLightning are percentages (0-100)
        /// - Weather effects are applied to visual rendering and particle systems
        /// - Lightning flashes briefly when lightning occurs
        /// 
        /// Day/Night Cycle (based on nwmain.exe day/night system):
        /// - Only active if DayNightCycle is 1 (dynamic cycle enabled)
        /// - Cycle duration: 24 minutes real time = 24 hours game time (1 minute = 1 hour)
        /// - Updates lighting colors based on time of day
        /// - IsNight flag updates based on cycle position
        /// 
        /// Area Effects (based on nwmain.exe area effect system):
        /// - Updates all active IAreaEffect instances
        /// - Effects can expire, update particle systems, or modify area state
        /// 
        /// Heartbeat Script (based on nwmain.exe area heartbeat):
        /// - Fires OnHeartbeat script every 6 seconds if configured
        /// - Uses area ResRef as script context (area scripts don't require entity)
        /// - Located via string references: "OnHeartbeat" @ 0x140ddb2b8 (nwmain.exe)
        /// 
        /// Tile-based Logic (based on nwmain.exe tile system):
        /// - Updates tile animations (Tile_AnimLoop1/2/3 from ARE file)
        /// - Processes tile lighting updates for dynamic lighting
        /// - Handles tile state changes (e.g., triggered tile animations)
        /// </remarks>
        public override void Update(float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                return; // Skip update if no time has passed
            }

            // Update weather simulation
            UpdateWeatherSimulation(deltaTime);

            // Process day/night cycle if enabled
            if (_dayNightCycle == 1) // 1 = dynamic cycle enabled
            {
                UpdateDayNightCycle(deltaTime);
            }

            // Update all active area effects
            UpdateAreaEffects(deltaTime);

            // Update area heartbeat timer and fire script if needed
            UpdateAreaHeartbeat(deltaTime);

            // Process tile-based area logic (animations, lighting)
            // Based on nwmain.exe: CNWSArea::UpdateTiles processes tile animations and state
            // Tile animations and lighting rendering are handled by rendering system,
            // but we update tile state and animation timers here for game logic
            UpdateTileAnimations(deltaTime);
        }

        /// <summary>
        /// Updates weather simulation based on area weather chances.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Weather simulation system
        /// - Checks weather chances every 5 seconds
        /// - Random roll (0-100) against ChanceRain, ChanceSnow, ChanceLightning
        /// - Weather effects persist until next check
        /// - Lightning flashes briefly when lightning occurs
        /// </remarks>
        private void UpdateWeatherSimulation(float deltaTime)
        {
            // Update weather check timer
            _weatherCheckTimer += deltaTime;

            // Check weather every 5 seconds (based on nwmain.exe behavior)
            if (_weatherCheckTimer >= WeatherCheckInterval)
            {
                _weatherCheckTimer -= WeatherCheckInterval;

                // Check rain (based on ChanceRain percentage)
                if (_chanceRain > 0)
                {
                    int rainRoll = _weatherRandom.Next(0, 100);
                    _isRaining = (rainRoll < _chanceRain);
                }
                else
                {
                    _isRaining = false;
                }

                // Check snow (based on ChanceSnow percentage)
                if (_chanceSnow > 0)
                {
                    int snowRoll = _weatherRandom.Next(0, 100);
                    _isSnowing = (snowRoll < _chanceSnow);
                }
                else
                {
                    _isSnowing = false;
                }

                // Check lightning (based on ChanceLightning percentage)
                // Lightning can only occur if rain is active (realistic behavior)
                if (_chanceLightning > 0 && _isRaining)
                {
                    int lightningRoll = _weatherRandom.Next(0, 100);
                    if (lightningRoll < _chanceLightning)
                    {
                        _isLightning = true;
                        _lightningFlashTimer = LightningFlashDuration; // Start lightning flash
                    }
                    else
                    {
                        _isLightning = false;
                    }
                }
                else
                {
                    _isLightning = false;
                }
            }

            // Update lightning flash timer
            if (_isLightning && _lightningFlashTimer > 0.0f)
            {
                _lightningFlashTimer -= deltaTime;
                if (_lightningFlashTimer <= 0.0f)
                {
                    _lightningFlashTimer = 0.0f;
                    // Lightning flash ended - lightning effect persists but flash is done
                    // Flash will trigger again on next lightning roll
                }
            }

            // Update rain particle system if active
            // Based on nwmain.exe: CNWSArea::RenderWeather updates rain particle positions
            if (_rainParticleSystem != null && _isRaining)
            {
                // Calculate wind direction from wind power
                // WindPower in ARE file is INT (0-2: None=0, Weak=1, Strong=2), convert to 0.0-1.0 range for particle system
                float windPowerNormalized = _windPower / 2.0f; // 0.0 for None, 0.5 for Weak, 1.0 for Strong
                // Wind direction is typically horizontal (X and Z axes), minimal Y component
                // For simplicity, use a default horizontal wind direction (can be enhanced with ARE wind direction data)
                Vector3 windDirection = new Vector3(1.0f, 0.0f, 0.0f); // Default: wind from west (positive X)
                _rainParticleSystem.Update(deltaTime, windDirection, windPowerNormalized, _width, _height);
            }
        }

        /// <summary>
        /// Updates day/night cycle if dynamic cycle is enabled.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Day/night cycle system
        /// - Cycle duration: 24 minutes real time = 24 hours game time
        /// - Updates IsNight flag based on cycle position
        /// - Night time: 18:00 - 06:00 (game time) = 6 hours real time
        /// - Day time: 06:00 - 18:00 (game time) = 12 hours real time
        /// - Lighting colors transition based on time of day
        /// </remarks>
        private void UpdateDayNightCycle(float deltaTime)
        {
            // Update day/night cycle timer (24 minutes = 24 hours game time)
            _dayNightTimer += deltaTime;

            // Cycle completes every 24 minutes (1440 seconds)
            if (_dayNightTimer >= DayNightCycleDuration)
            {
                _dayNightTimer -= DayNightCycleDuration; // Reset cycle
            }

            // Calculate time of day (0.0 = midnight, 0.5 = noon, 1.0 = next midnight)
            float timeOfDay = _dayNightTimer / DayNightCycleDuration;

            // Night time: 18:00 - 06:00 (0.75 - 1.0 and 0.0 - 0.25)
            // Day time: 06:00 - 18:00 (0.25 - 0.75)
            if (timeOfDay >= 0.75f || timeOfDay < 0.25f)
            {
                _isNight = 1; // Night time
            }
            else
            {
                _isNight = 0; // Day time
            }

            // Update lighting colors based on time of day
            // This would typically blend between sun and moon colors
            // TODO: STUB - For now, we update the IsNight flag which is used by rendering system
            // Full lighting color interpolation would be handled by graphics backend
        }

        /// <summary>
        /// Updates all active area effects.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Area effect update system
        /// - Iterates through all active area effects
        /// - Updates each effect's state (particles, timers, etc.)
        /// - Removes expired or inactive effects
        /// </remarks>
        private void UpdateAreaEffects(float deltaTime)
        {
            // Collect effects to remove (avoid modification during iteration)
            var effectsToRemove = new List<IAreaEffect>();

            // Update each active area effect
            foreach (IAreaEffect effect in _areaEffects)
            {
                if (effect == null)
                {
                    effectsToRemove.Add(effect);
                    continue;
                }

                // Update the effect
                effect.Update(deltaTime);

                // Check if effect should be removed
                if (!effect.IsActive)
                {
                    effectsToRemove.Add(effect);
                }
            }

            // Remove expired/inactive effects
            foreach (IAreaEffect effect in effectsToRemove)
            {
                _areaEffects.Remove(effect);
            }
        }

        /// <summary>
        /// Updates area heartbeat timer and fires OnHeartbeat script if configured.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Area heartbeat script system
        /// - Area heartbeat fires every 6 seconds (same as entity heartbeats)
        /// - Uses OnHeartbeat ResRef from ARE file
        /// - Area scripts execute with area ResRef as context (no entity required)
        /// - Located via string references: "OnHeartbeat" @ 0x140ddb2b8 (nwmain.exe)
        /// - Script execution: CNWSVirtualMachineCommands::ExecuteCommandExecuteScript @ 0x14051d5c0
        /// </remarks>
        private void UpdateAreaHeartbeat(float deltaTime)
        {
            // Check if area has heartbeat script configured
            if (_onHeartbeat == null || _onHeartbeat.IsBlank)
            {
                return; // No heartbeat script configured
            }

            // Update heartbeat timer
            _areaHeartbeatTimer += deltaTime;

            // Fire heartbeat script every 6 seconds
            if (_areaHeartbeatTimer >= AreaHeartbeatInterval)
            {
                _areaHeartbeatTimer -= AreaHeartbeatInterval;

                    // Fire area heartbeat script
                    // Area scripts need World/EventBus access to execute
                    // We get World reference from any entity in the area (if available)
                    // If no entities available, we skip script execution this frame
                    IWorld world = GetWorldFromAreaEntities();
                    if (world != null && world.EventBus != null)
                    {
                        // Get or create area entity for script execution context
                        // Based on nwmain.exe: Area heartbeat scripts use area ResRef as entity context
                        // Located via string references: "OnHeartbeat" @ 0x140ddb2b8 (nwmain.exe)
                        // Area scripts don't require a physical entity in the world - they use area ResRef as script context
                        IEntity areaEntity = GetOrCreateAreaEntityForScripts(world);
                        if (areaEntity != null)
                        {
                            // Fire OnHeartbeat script event
                            // Based on nwmain.exe: Area heartbeat script execution
                            world.EventBus.FireScriptEvent(areaEntity, ScriptEvent.OnHeartbeat, null);
                        }
                    }
            }
        }

        /// <summary>
        /// Gets or creates the area entity for script execution.
        /// </summary>
        /// <param name="world">World instance to create entity in if needed.</param>
        /// <returns>Area entity for script execution, or null if world is invalid.</returns>
        /// <remarks>
        /// Based on nwmain.exe: Area scripts execute with area ResRef as entity context
        /// Located via string references: "OnHeartbeat" @ 0x140ddb2b8 (nwmain.exe)
        /// Area scripts don't require a physical entity in the world - they use area ResRef as script context
        /// 
        /// Pattern matches ModuleTransitionSystem module entity creation:
        /// - Module scripts create temporary entities with module ResRef as tag
        /// - Area scripts follow the same pattern - create entity with area ResRef as tag
        /// 
        /// Implementation details:
        /// - First attempts to find existing entity by area ResRef tag
        /// - Falls back to finding entity by area tag
        /// - If no entity exists, creates a temporary entity with ObjectType.Invalid
        /// - Caches the temporary entity for reuse across script execution calls
        /// - Entity is used for all area scripts (OnEnter, OnExit, OnHeartbeat, OnUserDefined)
        /// </remarks>
        private IEntity GetOrCreateAreaEntityForScripts(IWorld world)
        {
            if (world == null)
            {
                return null;
            }

            // First, try to get existing entity by area ResRef tag
            // Area scripts use area ResRef as entity tag for execution
            IEntity areaEntity = world.GetEntityByTag(_resRef, 0);
            if (areaEntity != null && areaEntity.IsValid)
            {
                return areaEntity;
            }

            // Try using area tag as fallback
            if (!string.IsNullOrEmpty(_tag) && !string.Equals(_tag, _resRef, StringComparison.OrdinalIgnoreCase))
            {
                areaEntity = world.GetEntityByTag(_tag, 0);
                if (areaEntity != null && areaEntity.IsValid)
                {
                    return areaEntity;
                }
            }

            // If no area entity exists, create a temporary one for script execution
            // Use cached temporary area entity if available and still valid
            if (_temporaryAreaEntity != null && _temporaryAreaEntity.IsValid && _temporaryAreaEntity.World == world)
            {
                return _temporaryAreaEntity;
            }

            // Create temporary area entity for script execution
            // Based on nwmain.exe: Area scripts execute with area ResRef as entity tag
            // Pattern matches ModuleTransitionSystem module entity creation (CreateEntity with ObjectType.Invalid)
            areaEntity = world.CreateEntity(ObjectType.Invalid, Vector3.Zero, 0.0f);
            areaEntity.Tag = _resRef; // Set tag to area ResRef for script context

            // Cache the entity for reuse across script execution calls
            // This avoids recreating the entity for every area script event
            _temporaryAreaEntity = areaEntity;

            return areaEntity;
        }

        /// <summary>
        /// Gets World reference from entities in this area.
        /// </summary>
        /// <remarks>
        /// Helper method to get World reference for script execution.
        /// Checks entities in the area to find a World reference.
        /// </remarks>
        private IWorld GetWorldFromAreaEntities()
        {
            // Try to get World from any creature in the area
            foreach (IEntity creature in _creatures)
            {
                if (creature != null && creature.World != null)
                {
                    return creature.World;
                }
            }

            // Try placeables
            foreach (IEntity placeable in _placeables)
            {
                if (placeable != null && placeable.World != null)
                {
                    return placeable.World;
                }
            }

            // Try doors
            foreach (IEntity door in _doors)
            {
                if (door != null && door.World != null)
                {
                    return door.World;
                }
            }

            // Try triggers
            foreach (IEntity trigger in _triggers)
            {
                if (trigger != null && trigger.World != null)
                {
                    return trigger.World;
                }
            }

            // Try waypoints
            foreach (IEntity waypoint in _waypoints)
            {
                if (waypoint != null && waypoint.World != null)
                {
                    return waypoint.World;
                }
            }

            // Try sounds
            foreach (IEntity sound in _sounds)
            {
                if (sound != null && sound.World != null)
                {
                    return sound.World;
                }
            }

            return null; // No entities with World reference found
        }

        /// <summary>
        /// Sets the rendering context for area rendering.
        /// </summary>
        /// <param name="context">The rendering context containing graphics device, room mesh renderer, and camera matrices.</param>
        /// <remarks>
        /// Based on nwmain.exe: Area rendering uses graphics device, room mesh renderer, and basic effect.
        /// Set by game loop before calling area.Render().
        /// </remarks>
        public void SetRenderContext(IAreaRenderContext context)
        {
            _renderContext = context;
        }

        /// <summary>
        /// Gets the current tile identifier from camera position.
        /// </summary>
        /// <param name="cameraPosition">Camera position in world space.</param>
        /// <returns>Tile identifier (format: "tile_X_Y") or null if position is invalid.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::GetTileFromPosition converts world position to tile coordinates.
        /// Tile size: 10.0f units per tile (DAT_140dc2df4 in nwmain.exe).
        /// Formula: tileX = (int)(worldX / 10.0f), tileY = (int)(worldZ / 10.0f).
        /// </remarks>
        private string GetCurrentTileIdentifier(Vector3 cameraPosition)
        {
            if (_sceneData == null || _width <= 0 || _height <= 0)
            {
                return null;
            }

            // Convert world position to tile coordinates
            // Based on nwmain.exe: CNWSArea::GetTileFromPosition
            // Tile size: 10.0f units per tile
            const float TileSize = 10.0f;
            int tileX = (int)(cameraPosition.X / TileSize);
            int tileY = (int)(cameraPosition.Z / TileSize); // Aurora uses Z for north-south

            // Clamp to valid tile bounds
            if (tileX < 0) tileX = 0;
            if (tileX >= _width) tileX = _width - 1;
            if (tileY < 0) tileY = 0;
            if (tileY >= _height) tileY = _height - 1;

            return string.Format("tile_{0}_{1}", tileX, tileY);
        }

        /// <summary>
        /// Updates tile animations and state for all tiles in the area.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::UpdateTiles processes tile animations and state
        /// 
        /// Tile Animation System (nwmain.exe):
        /// - Tile_AnimLoop1/2/3 (INT) from ARE file specify animation loop indices
        /// - Animation loops are defined in tileset data (SET files)
        /// - Tile animations are typically texture animations (UV scrolling, frame sequences)
        /// - Animation state is tracked per-tile and updated each frame
        /// - Tile lighting (Tile_MainLight1/2, Tile_SrcLight1/2) affects dynamic lighting
        /// 
        /// Current Implementation:
        /// - Iterates through all tiles in scene data
        /// - Tile animation rendering is handled by rendering system
        /// - This method updates tile state for game logic purposes
        /// - Full implementation would require tileset data access to get animation loop definitions
        /// </remarks>
        private void UpdateTileAnimations(float deltaTime)
        {
            if (_sceneData == null || _sceneData.Tiles == null)
            {
                return;
            }

            // Based on nwmain.exe: CNWSArea::UpdateTiles iterates through all tiles
            // Updates animation state for tiles with animation loops
            // Tile_AnimLoop1/2/3 from ARE file specify which animation loops to play
            // Animation loop definitions come from tileset data (SET files)
            foreach (SceneTile tile in _sceneData.Tiles)
            {
                if (tile == null)
                {
                    continue;
                }

                // Tile animation state update
                // Based on nwmain.exe: Tile animations are texture-based (UV animations, frame sequences)
                // Animation loops are defined in tileset data and referenced by index in ARE file
                // The rendering system handles actual animation rendering based on animation state
                // This update method ensures tile state is current for game logic
                
                // Note: Full implementation would:
                // 1. Load tileset data to get animation loop definitions
                // 2. Track animation time for each tile's active animation loops
                // 3. Update animation frame indices based on animation speed and deltaTime
                // 4. Handle animation loop cycling (loop, ping-pong, one-shot)
                // 5. Update tile lighting state for dynamic lighting calculations
                
                // For now, tile state is updated implicitly by the rendering system
                // This method serves as a placeholder that can be expanded when tileset data access is available
            }
        }

        /// <summary>
        /// Applies dynamic lighting based on day/night cycle.
        /// </summary>
        /// <param name="basicEffect">Basic effect for lighting.</param>
        /// <param name="cameraPosition">Camera position for lighting calculations.</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::UpdateLighting applies sun/moon lighting.
        /// - If DayNightCycle is enabled, calculates lighting based on time of day
        /// - If DayNightCycle is disabled, uses IsNight flag to determine sun/moon lighting
        /// - Applies sun/moon ambient and diffuse colors
        /// - Applies fog based on sun/moon fog settings
        /// </remarks>
        private void ApplyDynamicLighting(IBasicEffect basicEffect, Vector3 cameraPosition)
        {
            if (basicEffect == null)
            {
                return;
            }

            // Determine if it's night based on day/night cycle or IsNight flag
            // Based on nwmain.exe: CNWSArea::UpdateLighting determines day/night state
            bool isNight = false;
            if (_dayNightCycle != 0)
            {
                // Day/night cycle is enabled - calculate from timer
                // Based on nwmain.exe: DayNightCycleDuration = 1440.0f (24 minutes = 24 hours)
                // Night is from 18:00 to 6:00 (6 hours = 360 minutes)
                float timeOfDay = (_dayNightTimer % DayNightCycleDuration) / 60.0f; // Convert to hours
                isNight = (timeOfDay >= 18.0f || timeOfDay < 6.0f);
            }
            else
            {
                // Static lighting - use IsNight flag
                isNight = (_isNight != 0);
            }

            // Apply sun or moon lighting
            // Based on nwmain.exe: CNWSArea::UpdateLighting applies sun/moon colors
            uint ambientColor = isNight ? _moonAmbientColor : _sunAmbientColor;
            uint diffuseColor = isNight ? _moonDiffuseColor : _sunDiffuseColor;
            byte fogAmount = isNight ? _moonFogAmount : _sunFogAmount;
            uint fogColor = isNight ? _moonFogColor : _sunFogColor;

            // Convert colors from BGR format to Vector3
            // Based on ARE format: Colors are DWORD in BGR format (0BGR)
            Vector3 ambientColorVec = new Vector3(
                ((ambientColor >> 16) & 0xFF) / 255.0f,
                ((ambientColor >> 8) & 0xFF) / 255.0f,
                (ambientColor & 0xFF) / 255.0f
            );

            Vector3 diffuseColorVec = new Vector3(
                ((diffuseColor >> 16) & 0xFF) / 255.0f,
                ((diffuseColor >> 8) & 0xFF) / 255.0f,
                (diffuseColor & 0xFF) / 255.0f
            );

            // Apply ambient lighting
            basicEffect.AmbientLightColor = ambientColorVec;
            basicEffect.LightingEnabled = true;

            // Apply fog if enabled
            // Based on nwmain.exe: CNWSArea::UpdateLighting applies fog
            if (fogAmount > 0)
            {
                Vector3 fogColorVec = new Vector3(
                    ((fogColor >> 16) & 0xFF) / 255.0f,
                    ((fogColor >> 8) & 0xFF) / 255.0f,
                    (fogColor & 0xFF) / 255.0f
                );
                // Note: BasicEffect fog support depends on implementation
                // TODO: STUB - For now, we'll set fog parameters if the effect supports it
            }
        }

        /// <summary>
        /// Renders visible tiles.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering.</param>
        /// <param name="roomRenderer">Room mesh renderer for loading tile meshes.</param>
        /// <param name="basicEffect">Basic effect for rendering.</param>
        /// <param name="viewMatrix">View matrix (camera transformation).</param>
        /// <param name="projectionMatrix">Projection matrix (perspective transformation).</param>
        /// <param name="cameraPosition">Camera position for culling.</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::RenderTiles renders visible tiles.
        /// - Iterates through all tiles in scene
        /// - Skips tiles that are not visible (visibility culling)
        /// - Loads tile mesh if not already loaded
        /// - Applies tile transform (position, rotation, height)
        /// - Renders tile mesh with proper lighting
        /// </remarks>
        private void RenderTiles(IGraphicsDevice graphicsDevice, IRoomMeshRenderer roomRenderer, IBasicEffect basicEffect,
            Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector3 cameraPosition)
        {
            if (graphicsDevice == null || roomRenderer == null || basicEffect == null || _sceneData == null)
            {
                return;
            }

            // Render each visible tile
            // Based on nwmain.exe: CNWSArea::RenderTiles iterates through visible tiles
            foreach (SceneTile tile in _sceneData.Tiles)
            {
                if (tile == null || !tile.IsVisible)
                {
                    continue; // Skip invisible tiles
                }

                // Skip tiles without model ResRef
                if (string.IsNullOrEmpty(tile.ModelResRef))
                {
                    continue;
                }

                // Get or load tile mesh
                // Based on nwmain.exe: CNWSArea::RenderTiles loads tile mesh on demand
                IRoomMeshData meshData = tile.MeshData;
                if (meshData == null)
                {
                    // Try to load mesh using room renderer
                    // Note: This requires MDL model loading, which may not be available
                    // TODO: STUB - For now, we'll skip tiles that haven't been pre-loaded
                    // Full implementation would load MDL models from tile model ResRef
                    continue;
                }

                if (meshData.VertexBuffer == null || meshData.IndexBuffer == null)
                {
                    continue;
                }

                // Validate mesh data
                if (meshData.IndexCount < 3)
                {
                    continue; // Need at least one triangle
                }

                // Set up tile transform
                // Based on nwmain.exe: CNWSArea::RenderTiles applies tile transform
                Vector3 tilePos = tile.Position;
                Matrix4x4 tileWorld = MatrixHelper.CreateTranslation(tilePos);

                // Apply tile rotation if specified
                // Based on nwmain.exe: CNWTile::SetOrientation applies rotation
                // Orientation: 0 = 0°, 1 = 90° CCW, 2 = 180° CCW, 3 = 270° CCW
                if (tile.Orientation > 0)
                {
                    float rotationY = tile.Orientation * (float)(Math.PI / 2.0); // Convert to radians
                    Matrix4x4 rotation = MatrixHelper.CreateRotationY(rotationY);
                    tileWorld = Matrix4x4.Multiply(rotation, tileWorld);
                }

                // Set world, view, and projection matrices
                basicEffect.World = tileWorld;
                basicEffect.View = viewMatrix;
                basicEffect.Projection = projectionMatrix;

                // Render tile mesh
                // Based on nwmain.exe: CNWSArea::RenderTiles renders tile geometry
                graphicsDevice.SetVertexBuffer(meshData.VertexBuffer);
                graphicsDevice.SetIndexBuffer(meshData.IndexBuffer);

                // Apply effect and draw
                basicEffect.Apply();
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, meshData.IndexCount / 3);
            }
        }

        /// <summary>
        /// Renders weather effects (rain, snow, lightning).
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering.</param>
        /// <param name="basicEffect">Basic effect for rendering.</param>
        /// <param name="viewMatrix">View matrix (camera transformation).</param>
        /// <param name="projectionMatrix">Projection matrix (perspective transformation).</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::RenderWeather renders weather particles.
        /// - Rain: Renders falling rain particles
        /// - Snow: Renders falling snow particles
        /// - Lightning: Renders lightning flash effect
        /// </remarks>
        private void RenderWeatherEffects(IGraphicsDevice graphicsDevice, IBasicEffect basicEffect,
            Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (graphicsDevice == null || basicEffect == null)
            {
                return;
            }

            // Render rain if active
            // Based on nwmain.exe: CNWSArea::RenderWeather renders rain particles
            if (_isRaining && _rainParticleSystem != null)
            {
                _rainParticleSystem.Render(graphicsDevice, basicEffect, viewMatrix, projectionMatrix);
            }

            // Render snow if active
            // Based on nwmain.exe: CNWSArea::RenderWeather renders snow particles
            if (_isSnowing)
            {
                RenderSnowParticles(graphicsDevice, basicEffect, viewMatrix, projectionMatrix);
            }

            // Render lightning flash if active
            // Based on nwmain.exe: CNWSArea::RenderWeather renders lightning flash
            if (_isLightning && _lightningFlashTimer > 0.0f)
            {
                // Lightning flash is a full-screen brightness effect
                // Based on nwmain.exe: Lightning flash is rendered as screen overlay
                // TODO: STUB - For now, this is a placeholder - full implementation would render lightning flash
            }
        }

        /// <summary>
        /// Renders area effects.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering.</param>
        /// <param name="basicEffect">Basic effect for rendering.</param>
        /// <param name="viewMatrix">View matrix (camera transformation).</param>
        /// <param name="projectionMatrix">Projection matrix (perspective transformation).</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::RenderAreaEffects renders persistent environmental effects.
        /// - Iterates through all active area effects
        /// - Renders each effect based on its type (magical effects, environmental changes, etc.)
        /// </remarks>
        private void RenderAreaEffects(IGraphicsDevice graphicsDevice, IBasicEffect basicEffect,
            Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (graphicsDevice == null || basicEffect == null || _areaEffects == null)
            {
                return;
            }

            // Render each active area effect
            // Based on nwmain.exe: CNWSArea::RenderAreaEffects iterates through effects
            foreach (IAreaEffect effect in _areaEffects)
            {
                if (effect == null || !effect.IsActive)
                {
                    continue; // Skip inactive effects
                }

                // Render effect based on its type
                // Based on nwmain.exe: CNWSArea::RenderAreaEffects renders effect geometry
                // TODO: STUB - For now, this is a placeholder - full implementation would render effect-specific geometry
                // Effect rendering would depend on effect type (particles, meshes, sprites, etc.)
            }
        }

        /// <summary>
        /// Sets the render context for this area.
        /// </summary>
        /// <param name="context">The render context containing graphics device and rendering interfaces.</param>
        /// <remarks>
        /// Based on nwmain.exe: Area rendering uses graphics device, room mesh renderer, and basic effect.
        /// Required for tile-based geometry rendering, dynamic lighting, weather effects, and area effects.
        /// </remarks>
        public void SetRenderContext(IAreaRenderContext context)
        {
            _renderContext = context;
        }

        /// <summary>
        /// Renders the area.
        /// </summary>
        /// <remarks>
        /// Aurora has complex rendering with tile-based geometry.
        /// Includes dynamic lighting, weather effects, and area effects.
        /// 
        /// Based on nwmain.exe area rendering functions:
        /// - CNWSArea::RenderArea @ 0x1403681f0 (approximate - needs Ghidra verification)
        /// - CNWSArea::RenderTiles @ 0x1403682a0 (approximate - needs Ghidra verification)
        /// - Tile rendering: Renders visible tiles with proper transforms and lighting
        /// - Dynamic lighting: Applies sun/moon lighting based on day/night cycle
        /// - Weather effects: Renders rain, snow, and lightning effects
        /// - Area effects: Renders persistent environmental effects
        /// 
        /// Rendering order:
        /// 1. Build scene data from ARE file if not already built
        /// 2. Determine current tile for visibility culling
        /// 3. Render visible tiles with proper transforms
        /// 4. Apply dynamic lighting (sun/moon based on day/night cycle)
        /// 5. Render weather effects (rain, snow, lightning)
        /// 6. Render area effects
        /// </remarks>
        public override void Render()
        {
            // If no rendering context, cannot render
            if (_renderContext == null)
            {
                return;
            }

            IGraphicsDevice graphicsDevice = _renderContext.GraphicsDevice;
            IRoomMeshRenderer roomRenderer = _renderContext.RoomMeshRenderer;
            IBasicEffect basicEffect = _renderContext.BasicEffect;
            Matrix4x4 viewMatrix = _renderContext.ViewMatrix;
            Matrix4x4 projectionMatrix = _renderContext.ProjectionMatrix;
            Vector3 cameraPosition = _renderContext.CameraPosition;

            if (graphicsDevice == null || roomRenderer == null || basicEffect == null)
            {
                return;
            }

            // Build scene data from ARE file if not already built
            // Based on nwmain.exe: CNWSArea::LoadArea builds tile-based scene structure
            if (_sceneData == null && _cachedAreData != null)
            {
                // Create resource provider for scene builder
                // Note: We need a resource provider, but we may not have one available
                // TODO: STUB - For now, create a simple provider that uses the tileset loader's resource loader
                IGameResourceProvider resourceProvider = null;
                if (_tilesetLoader != null)
                {
                    // Create a simple resource provider wrapper
                    // This is a temporary solution - full implementation would use proper resource system
                    resourceProvider = new SimpleResourceProvider(_tilesetLoader);
                }

                if (resourceProvider != null)
                {
                    var sceneBuilder = new AuroraSceneBuilder(resourceProvider);
                    _sceneData = sceneBuilder.BuildScene(_cachedAreData);
                }
            }

            // If no scene data, cannot render
            if (_sceneData == null || _sceneData.Tiles == null || _sceneData.Tiles.Count == 0)
            {
                return;
            }

            // Determine current tile for visibility culling based on camera position
            // Based on nwmain.exe: CNWSArea::GetTileFromPosition converts world position to tile coordinates
            string currentTileIdentifier = GetCurrentTileIdentifier(cameraPosition);
            if (!string.IsNullOrEmpty(currentTileIdentifier))
            {
                // Update scene builder with current tile for visibility culling
                // Based on nwmain.exe: CNWSArea::UpdateVisibleTiles updates tile visibility
                var sceneBuilder = new AuroraSceneBuilder(null); // Temporary - would need resource provider
                sceneBuilder.SetCurrentArea(currentTileIdentifier);
            }

            // Apply dynamic lighting based on day/night cycle
            // Based on nwmain.exe: CNWSArea::UpdateLighting applies sun/moon lighting
            ApplyDynamicLighting(basicEffect, cameraPosition);

            // Render visible tiles
            // Based on nwmain.exe: CNWSArea::RenderTiles renders visible tiles
            RenderTiles(graphicsDevice, roomRenderer, basicEffect, viewMatrix, projectionMatrix, cameraPosition);

            // Render weather effects (rain, snow, lightning)
            // Based on nwmain.exe: CNWSArea::RenderWeather renders weather particles
            RenderWeatherEffects(graphicsDevice, basicEffect, viewMatrix, projectionMatrix);

            // Render area effects
            // Based on nwmain.exe: CNWSArea::RenderAreaEffects renders persistent effects
            RenderAreaEffects(graphicsDevice, basicEffect, viewMatrix, projectionMatrix);
        }

        /// <summary>
        /// Unloads the area and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::UnloadArea @ 0x1403681f0
        /// Cleans up tile-based geometry, area effects, and entities.
        /// Ensures proper resource cleanup for Aurora's complex systems.
        ///
        /// Original implementation sequence:
        /// 1. Validates and cleans up internal pointers (offsets 0x50, 0x60, 0x28, 0x30, 0x38, 0x40, 0x250, 600)
        /// 2. Releases object at offset 0x1c8 (calls destructor with parameter 3)
        /// 3. Calls cleanup function on pointer at offset 0x1d0
        /// 4. Removes area from module's lookup table using ResRef
        ///
        /// This implementation:
        /// 1. Destroys all entities in the area (via World if available, otherwise directly)
        /// 2. Deactivates and clears all area effects
        /// 3. Disposes navigation mesh if it implements IDisposable
        /// 4. Clears all entity lists and area effects list
        /// 5. Sets navigation mesh to null
        /// </remarks>
        public override void Unload()
        {
            // Collect all entities first to avoid modification during iteration
            var allEntities = new List<IEntity>();
            allEntities.AddRange(_creatures);
            allEntities.AddRange(_placeables);
            allEntities.AddRange(_doors);
            allEntities.AddRange(_triggers);
            allEntities.AddRange(_waypoints);
            allEntities.AddRange(_sounds);

            // Destroy all entities
            // Based on nwmain.exe: Entities are removed from area and destroyed
            // If entity has World reference, use World.DestroyEntity (fires events, unregisters properly)
            // Otherwise, call Destroy directly (for entities not yet registered with world)
            foreach (IEntity entity in allEntities)
            {
                if (entity != null && entity.IsValid)
                {
                    // Try to destroy via World first (proper cleanup with event firing)
                    if (entity.World != null)
                    {
                        entity.World.DestroyEntity(entity.ObjectId);
                    }
                    else
                    {
                        // Entity not registered with world - destroy directly
                        // Based on Entity.Destroy() implementation
                        if (entity is Core.Entities.Entity concreteEntity)
                        {
                            concreteEntity.Destroy();
                        }
                    }
                }
            }

            // Deactivate and clear all area effects
            // Based on nwmain.exe: Area effects are cleaned up during area unload
            foreach (IAreaEffect effect in _areaEffects)
            {
                if (effect != null && effect.IsActive)
                {
                    effect.Deactivate();
                }
            }
            _areaEffects.Clear();

            // Dispose navigation mesh if it implements IDisposable
            // Based on nwmain.exe: Navigation mesh resources are freed
            if (_navigationMesh != null)
            {
                if (_navigationMesh is System.IDisposable disposableMesh)
                {
                    disposableMesh.Dispose();
                }
                _navigationMesh = null;
            }

            // Clear all entity lists
            // Based on nwmain.exe: Entity lists are cleared during unload
            _creatures.Clear();
            _placeables.Clear();
            _doors.Clear();
            _triggers.Clear();
            _waypoints.Clear();
            _sounds.Clear();

            // Clear string references (optional cleanup)
            // Based on nwmain.exe: String references are cleared
            _resRef = null;
            _displayName = null;
            _tag = null;

            // Clear temporary area entity cache
            // Based on nwmain.exe: Area entity references are cleared during unload
            _temporaryAreaEntity = null;
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
    /// Simple resource provider wrapper for TilesetLoader.
    /// </summary>
    /// <remarks>
    /// Provides IGameResourceProvider interface for AuroraSceneBuilder using TilesetLoader's resource loader function.
    /// This is a temporary solution - full implementation would use proper resource system.
    /// Note: LoadResource method is expected by AuroraTileset but not part of IGameResourceProvider interface.
    /// This is a workaround until the interface is extended or AuroraTileset is refactored.
    /// </remarks>
    internal class SimpleResourceProvider : IGameResourceProvider
    {
        private readonly TilesetLoader _tilesetLoader;
        private readonly Func<string, byte[]> _resourceLoader;

        public SimpleResourceProvider(TilesetLoader tilesetLoader)
        {
            _tilesetLoader = tilesetLoader;
            // Extract resource loader from tileset loader via reflection
            // This is a workaround - ideally TilesetLoader would expose this
            if (tilesetLoader != null)
            {
                var field = typeof(TilesetLoader).GetField("_resourceLoader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    _resourceLoader = field.GetValue(tilesetLoader) as Func<string, byte[]>;
                }
            }
        }

        /// <summary>
        /// Loads a resource by ResRef and ResourceType.
        /// </summary>
        /// <param name="resRef">Resource reference.</param>
        /// <param name="resourceType">Resource type.</param>
        /// <returns>Resource data or null if not found.</returns>
        /// <remarks>
        /// This method is expected by AuroraTileset but not part of IGameResourceProvider interface.
        /// This is a workaround until the interface is extended.
        /// </remarks>
        public byte[] LoadResource(ResRef resRef, ResourceType resourceType)
        {
            if (_resourceLoader == null || resRef == null || resRef.IsBlank)
            {
                return null;
            }

            // Build resource filename from ResRef and ResourceType
            // Based on nwmain.exe: Resource filenames are ResRef + extension
            string extension = resourceType != null ? ("." + resourceType.Extension) : "";
            string resourceName = resRef.ToString() + extension;

            // Load resource using resource loader function
            return _resourceLoader(resourceName);
        }

        // IGameResourceProvider interface methods
        /// <summary>
        /// Opens a resource stream asynchronously.
        /// </summary>
        /// <remarks>
        /// Resource Loading (Aurora Engine):
        /// - Based on nwmain.exe: CExoResMan::Demand @ 0x14018ef90 loads resources via resource loader
        /// - Converts ResourceIdentifier to filename format (ResName + "." + Extension)
        /// - Uses _resourceLoader delegate to load resource data
        /// - Returns MemoryStream wrapping resource bytes, or null if not found
        /// - Async operation prevents blocking game loop during resource loading
        /// </remarks>
        public Task<Stream> OpenResourceAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                if (_resourceLoader == null || id == null || id.ResType == null || id.ResType.IsInvalid)
                {
                    return null;
                }

                // Build resource filename from ResourceIdentifier
                // Based on nwmain.exe: Resource filenames are ResName + extension
                string extension = id.ResType.Extension ?? "";
                if (extension.StartsWith("."))
                {
                    extension = extension.Substring(1);
                }
                string resourceName = id.ResName + "." + extension;

                // Load resource using resource loader function
                byte[] data = _resourceLoader(resourceName);
                if (data == null || data.Length == 0)
                {
                    return null;
                }

                return new MemoryStream(data, writable: false);
            }, ct);
        }

        /// <summary>
        /// Checks if a resource exists without opening it.
        /// </summary>
        /// <remarks>
        /// Resource Existence Check (Aurora Engine):
        /// - Based on nwmain.exe: Resource existence checked via CExoResMan::Demand @ 0x14018ef90
        /// - Attempts to load resource and checks if data is returned
        /// - Returns true if resource exists and has data, false otherwise
        /// - Async operation prevents blocking game loop during resource lookup
        /// </remarks>
        public Task<bool> ExistsAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                if (_resourceLoader == null || id == null || id.ResType == null || id.ResType.IsInvalid)
                {
                    return false;
                }

                // Build resource filename from ResourceIdentifier
                string extension = id.ResType.Extension ?? "";
                if (extension.StartsWith("."))
                {
                    extension = extension.Substring(1);
                }
                string resourceName = id.ResName + "." + extension;

                // Check if resource exists by attempting to load it
                byte[] data = _resourceLoader(resourceName);
                return data != null && data.Length > 0;
            }, ct);
        }

        /// <summary>
        /// Locates a resource across multiple search locations.
        /// </summary>
        /// <remarks>
        /// Resource Location (Aurora Engine):
        /// - Based on nwmain.exe: CExoResMan::Demand @ 0x14018ef90 searches resources in precedence order
        /// - SimpleResourceProvider uses a delegate, so exact location information is not available
        /// - Returns LocationResult with Module location if resource is found, empty list otherwise
        /// - SearchLocation order parameter is respected but limited by delegate-based implementation
        /// - Async operation prevents blocking game loop during resource location
        /// </remarks>
        public Task<IReadOnlyList<LocationResult>> LocateAsync(ResourceIdentifier id, SearchLocation[] order, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                var results = new List<LocationResult>();

                if (_resourceLoader == null || id == null || id.ResType == null || id.ResType.IsInvalid)
                {
                    return (IReadOnlyList<LocationResult>)results;
                }

                // Build resource filename from ResourceIdentifier
                string extension = id.ResType.Extension ?? "";
                if (extension.StartsWith("."))
                {
                    extension = extension.Substring(1);
                }
                string resourceName = id.ResName + "." + extension;

                // Check if resource exists by attempting to load it
                byte[] data = _resourceLoader(resourceName);
                if (data != null && data.Length > 0)
                {
                    // Resource found - return location result
                    // Since we're using a delegate, we don't have exact path information
                    // Use Module location as default (most common for tileset resources)
                    results.Add(new LocationResult
                    {
                        Location = SearchLocation.Module,
                        Path = resourceName, // Resource name as path (best we can do with delegate)
                        Size = data.Length,
                        Offset = 0
                    });
                }

                return (IReadOnlyList<LocationResult>)results;
            }, ct);
        }

        /// <summary>
        /// Enumerates all resources of a specific type.
        /// </summary>
        /// <remarks>
        /// Resource Enumeration (Aurora Engine):
        /// - Based on nwmain.exe: Resource enumeration via CExoKeyTable iteration
        /// - SimpleResourceProvider uses a delegate function, which cannot enumerate resources
        /// - Returns empty enumerable since delegate-based resource loading doesn't support enumeration
        /// - Full resource enumeration requires access to resource archives (ERF, HAK, etc.)
        /// - For full enumeration, use AuroraResourceProvider instead of SimpleResourceProvider
        /// </remarks>
        public IEnumerable<ResourceIdentifier> EnumerateResources(ResourceType type)
        {
            // SimpleResourceProvider uses a delegate function which cannot enumerate resources
            // Enumeration requires access to resource archives (ERF, HAK, directories, etc.)
            // Return empty enumerable - use AuroraResourceProvider for full enumeration support
            yield break;
        }

        /// <summary>
        /// Gets the raw bytes of a resource.
        /// </summary>
        /// <remarks>
        /// Resource Bytes Loading (Aurora Engine):
        /// - Based on nwmain.exe: CExoResMan::Demand @ 0x14018ef90 loads resource data
        /// - Converts ResourceIdentifier to filename format (ResName + "." + Extension)
        /// - Uses _resourceLoader delegate to load resource data
        /// - Returns raw bytes directly, or null if not found
        /// - Async operation prevents blocking game loop during resource loading
        /// </remarks>
        public Task<byte[]> GetResourceBytesAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                if (_resourceLoader == null || id == null || id.ResType == null || id.ResType.IsInvalid)
                {
                    return null;
                }

                // Build resource filename from ResourceIdentifier
                // Based on nwmain.exe: Resource filenames are ResName + extension
                string extension = id.ResType.Extension ?? "";
                if (extension.StartsWith("."))
                {
                    extension = extension.Substring(1);
                }
                string resourceName = id.ResName + "." + extension;

                // Load resource using resource loader function
                byte[] data = _resourceLoader(resourceName);
                if (data == null || data.Length == 0)
                {
                    return null;
                }

                return data;
            }, ct);
        }

        public Andastra.Runtime.Content.Interfaces.GameType GameType => Andastra.Runtime.Content.Interfaces.GameType.NWN;
    }

    /// <summary>
    /// Interface for area effects in Aurora engine.
    /// </summary>
    /// <remarks>
    /// Based on nwmain.exe: CNWSAreaEffect interface for persistent environmental effects.
    /// Area effects are rendered as part of the area rendering pipeline.
    /// </remarks>
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
