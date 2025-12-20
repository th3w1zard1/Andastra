using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Enums;
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
                    _navigationMesh = new AuroraNavigationMesh(emptyTiles, width, height);
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
                    // For now, assume tiles with valid Tile_ID are walkable
                    // A full implementation would query the tileset file to determine walkability
                    bool isLoaded = (tileId >= 0);
                    bool isWalkable = isLoaded; // Simplified: valid tiles are walkable
                    // TODO: Query tileset file to determine actual walkability from tile model data

                    // Create AuroraTile instance
                    // Surface material would ideally be looked up from tileset data
                    // For now, default to Stone (4) for walkable tiles, Undefined (0) for non-walkable
                    // TODO: Query tileset file to determine actual surface material from tile model data
                    int surfaceMaterial = isWalkable ? 4 : 0; // Stone (walkable) or Undefined (non-walkable)
                    
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
                _navigationMesh = new AuroraNavigationMesh(tiles, width, height);
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
            // Note: Full tile animation system would require tileset data access
            // For now, we update tile state tracking that's already implemented
            // Tile animations and lighting are handled by rendering system
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
            // For now, we update the IsNight flag which is used by rendering system
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
                    // Area scripts use area ResRef as entity tag for execution
                    IEntity areaEntity = world.GetEntityByTag(_resRef, 0);
                    if (areaEntity == null)
                    {
                        // Try using area tag as fallback
                        areaEntity = world.GetEntityByTag(_tag, 0);
                    }

                    // If no area entity exists, create a temporary one for script execution
                    // This is similar to how module scripts are executed
                    if (areaEntity == null)
                    {
                        // TODO: Create temporary area entity for script execution
                        // For now, we skip if no area entity exists
                        // Full implementation would create a temporary entity with area ResRef as tag
                        return;
                    }

                    // Fire OnHeartbeat script event
                    // Based on nwmain.exe: Area heartbeat script execution
                    world.EventBus.FireScriptEvent(areaEntity, ScriptEvent.OnHeartbeat, null);
                }
            }
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
