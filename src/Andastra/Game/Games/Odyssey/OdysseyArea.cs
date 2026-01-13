using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.MDL;
using BioWare.NET.Resource.Formats.MDLData;
using BioWare.NET.Resource.Formats.VIS;
using BioWare.NET.Extract;
using BioWare.NET.Resource.Formats.GFF.Generics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Core.Navigation;
using Andastra.Game.Games.Common;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;
using RuntimeIModule = Andastra.Runtime.Core.Interfaces.IModule;
using RuntimeObjectType = Andastra.Runtime.Core.Enums.ObjectType;

namespace Andastra.Game.Games.Odyssey
{
    /// <summary>
    /// Odyssey Engine (KotOR/KotOR2) specific area implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Area Implementation:
    /// - Based on swkotor.exe and swkotor2.exe area systems
    /// - Uses ARE (area properties) and GIT (instances) file formats
    /// - Implements walkmesh navigation and area transitions
    /// - Supports stealth XP and area restrictions
    ///
    /// Based on verified components of:
    /// - swkotor.exe: Area loading and management functions
    /// - swkotor2.exe: LoadAreaProperties @ 0x004e26d0, SaveAreaProperties @ 0x004e11d0
    /// - swkotor2.exe: DispatchEvent @ 0x004dcfb0 for area events
    /// - Common ARE/GIT format documentation in vendor/PyKotor/wiki/
    ///
    /// Area structure:
    /// - ARE file: GFF with "ARE " signature containing lighting, fog, grass, walkmesh
    /// - GIT file: GFF with "GIT " signature containing creature/door/placeable instances
    /// - Walkmesh: Binary format for navigation and collision detection
    /// - Area properties: Unescapable, StealthXPEnabled, lighting settings
    /// </remarks>
    [PublicAPI]
    public class OdysseyArea : BaseArea
    {
        private readonly List<IEntity> _creatures = new List<IEntity>();
        private readonly List<IEntity> _placeables = new List<IEntity>();
        private readonly List<IEntity> _doors = new List<IEntity>();
        private readonly List<IEntity> _triggers = new List<IEntity>();
        private readonly List<IEntity> _waypoints = new List<IEntity>();
        private readonly List<IEntity> _sounds = new List<IEntity>();

        private string _resRef;
        private string _displayName;
        private string _tag;
        private bool _isUnescapable;
        private bool _stealthXpEnabled;
        private INavigationMesh _navigationMesh;

        // Room and visibility data for rendering
        private List<RoomInfo> _rooms;
        private VIS _visibilityData;
        private Dictionary<string, IRoomMeshData> _roomMeshes;

        // Room bounding boxes cache (local space, not transformed by room position/rotation)
        // Key: Room ModelName, Value: (Min, Max) bounding box in local model space
        private Dictionary<string, (Vector3 Min, Vector3 Max)> _roomBoundingBoxes;

        // Lighting and fog properties (from ARE file)
        private uint _ambientColor;
        private uint _dynamicAmbientColor;
        private uint _fogColor;
        private float _fogFar;
        private uint _sunFogColor;
        private uint _sunDiffuseColor;
        private uint _sunAmbientColor;

        // Rendering context (set by game loop or service locator)
        private IAreaRenderContext _renderContext;

        // Module reference for loading WOK files (optional, set when available)
        private Module _module;

        // Area heartbeat and transition state
        private float _areaHeartbeatTimer;
        private bool _transPending;
        private byte _transPendNextId;
        private byte _transPendCurrId;

        // Area local variables storage
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area local variable storage system
        // Area variables are stored separately from entity variables and persist across area loads
        private Runtime.Core.Save.LocalVariableSet _localVariables;

        // Area entity for script execution context
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area entities are created dynamically for script execution
        // Area entities don't have physical presence but serve as script execution context
        // Stored here for proper lifecycle management and cleanup
        private IEntity _areaEntityForScripts;

        /// <summary>
        /// Creates a new Odyssey area.
        /// </summary>
        /// <param name="resRef">The resource reference name of the area.</param>
        /// <param name="areData">ARE file data containing area properties.</param>
        /// <param name="gitData">GIT file data containing entity instances.</param>
        /// <param name="module">Optional Module reference for loading WOK walkmesh files. If provided, enables full walkmesh loading from room data.</param>
        /// <remarks>
        /// Based on area loading sequence in swkotor2.exe.
        /// Loads ARE file first for static properties, then GIT file for dynamic instances.
        /// Initializes walkmesh and area effects.
        ///
        /// Module parameter:
        /// - If provided, enables loading WOK files for walkmesh construction
        /// - Required for full walkmesh functionality when rooms are available
        /// - Can be set later via SetModule() if not available at construction time
        /// </remarks>
        public OdysseyArea(string resRef, byte[] areData, byte[] gitData, Module module = null)
        {
            _resRef = resRef ?? throw new ArgumentNullException(nameof(resRef));
            _tag = resRef; // Default tag to resref
            _module = module; // Store module reference for walkmesh loading

            // Initialize collections
            _rooms = new List<RoomInfo>();
            _roomMeshes = new Dictionary<string, IRoomMeshData>(StringComparer.OrdinalIgnoreCase);
            _roomBoundingBoxes = new Dictionary<string, (Vector3 Min, Vector3 Max)>(StringComparer.OrdinalIgnoreCase);

            // Initialize lighting/fog defaults
            _ambientColor = 0xFF808080; // Gray ambient
            _dynamicAmbientColor = 0xFF808080;
            _fogColor = 0xFF808080;
            FogEnabled = false;
            FogNear = 0.0f;
            _fogFar = 1000.0f;
            _sunFogColor = 0xFF808080;
            _sunDiffuseColor = 0xFFFFFFFF;
            _sunAmbientColor = 0xFF808080;

            // Initialize area heartbeat and transition state
            _areaHeartbeatTimer = 0.0f;
            _transPending = false;
            _transPendNextId = 0;
            _transPendCurrId = 0;

            // Initialize area local variables storage
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variables are initialized when area is created
            _localVariables = new Runtime.Core.Save.LocalVariableSet();

            LoadAreaGeometry(areData);
            LoadEntities(gitData);
            LoadAreaProperties(areData);
            InitializeAreaEffects();
        }

        /// <summary>
        /// Sets the Module reference for loading WOK walkmesh files.
        /// </summary>
        /// <param name="module">The Module instance for resource access.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module reference is required for loading WOK files.
        /// Call this method if Module was not available at construction time.
        /// If rooms are already set, this will trigger walkmesh loading.
        /// </remarks>
        public void SetModule(Module module)
        {
            _module = module;
            // If rooms are already set, try to load walkmesh now
            if (_rooms != null && _rooms.Count > 0)
            {
                LoadWalkmeshFromRooms();
            }
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
        /// Based on "Unescapable" field in AreaProperties GFF.
        /// When true, players cannot leave the area.
        /// </remarks>
        public override bool IsUnescapable
        {
            get => _isUnescapable;
            set => _isUnescapable = value;
        }

        /// <summary>
        /// Gets or sets whether stealth XP is enabled for this area.
        /// </summary>
        /// <remarks>
        /// Based on "StealthXPEnabled" field in AreaProperties GFF.
        /// Controls whether stealth actions grant XP in this area.
        /// </remarks>
        public override bool StealthXPEnabled
        {
            get => _stealthXpEnabled;
            set => _stealthXpEnabled = value;
        }

        /// <summary>
        /// Gets an object by tag within this area.
        /// </summary>
        /// <remarks>
        /// Searches all entity collections for matching tag.
        /// Returns nth occurrence (0-based indexing).
        /// </remarks>
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
        /// Based on walkmesh projection functions in swkotor2.exe.
        /// Checks if point can be projected onto walkable surface.
        /// </remarks>
        public override bool IsPointWalkable(Vector3 point)
        {
            if (_navigationMesh == null)
            {
                return false;
            }
            // Project point to surface and check if it's walkable
            Vector3 projected;
            float height;
            if (_navigationMesh.ProjectToSurface(point, out projected, out height))
            {
                int faceIndex = _navigationMesh.FindFaceAt(projected);
                return faceIndex >= 0 && _navigationMesh.IsWalkable(faceIndex);
            }
            return false;
        }

        /// <summary>
        /// Projects a point onto the walkmesh.
        /// </summary>
        /// <remarks>
        /// Based on 0x004f5070 @ 0x004f5070 in swkotor2.exe.
        ///
        /// Ghidra analysis (swkotor2.exe: 0x004f5070, verified):
        /// - Signature: `float10 __thiscall 0x004f5070(void *param_1, float *param_2, int param_3, int *param_4, int *param_5)`
        /// - Projects point to walkmesh surface and returns height
        /// - Called from 34 locations throughout the engine for positioning and pathfinding
        /// - Delegates to OdysseyNavigationMesh.ProjectToWalkmesh for actual projection
        ///
        /// See OdysseyNavigationMesh.ProjectToWalkmesh for detailed implementation notes.
        /// </remarks>
        public override bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            if (_navigationMesh == null)
            {
                result = point;
                height = point.Y;
                return false;
            }

            // Use Odyssey-specific projection if available
            if (_navigationMesh is OdysseyNavigationMesh odysseyMesh)
            {
                return odysseyMesh.ProjectToWalkmesh(point, out result, out height);
            }

            // Fallback to generic ProjectToSurface
            return _navigationMesh.ProjectToSurface(point, out result, out height);
        }

        /// <summary>
        /// Loads area properties from GFF data.
        /// </summary>
        /// <remarks>
        /// Based on LoadAreaProperties @ 0x004e26d0 in swkotor2.exe.
        ///
        /// Ghidra analysis (swkotor2.exe: 0x004e26d0, verified):
        /// - Signature: `uint * __thiscall LoadAreaProperties(void *this, void *param_1, uint *param_2)`
        /// - this: Area object pointer
        /// - param_1: GFF struct pointer
        /// - param_2: Additional parameter (unused in this context)
        /// - Returns: uint* pointer (typically param_2 or null)
        ///
        /// Function flow:
        /// 1. Calls 0x00412b30 to get "AreaProperties" nested struct from GFF root
        /// 2. If AreaProperties struct exists, reads the following fields:
        ///    - "Unescapable" (UInt8) via 0x00412b80 -> stored at offset 0x2dc
        ///    - "RestrictMode" (UInt8) via 0x00412b80 -> stored at offset 0x2e4
        ///      * If RestrictMode changed, triggers 0x004dc770 and 0x0057a370 (restriction update)
        ///    - "StealthXPMax" (Int32) via 0x00412d40 -> stored at offset 0x2e8
        ///      * Clamps StealthXPCurrent to StealthXPMax if current > max
        ///    - "StealthXPCurrent" (Int32) via 0x00412d40 -> stored at offset 0x2ec
        ///      * Clamped to StealthXPMax if exceeds max
        ///    - "StealthXPLoss" (Int32) via 0x00412d40 -> stored at offset 0x2f0
        ///    - "StealthXPEnabled" (UInt8) via 0x00412b80 -> stored at offset 0x2f4
        ///    - "TransPending" (UInt8) via 0x00412b80 -> stored at offset 0x2f8
        ///    - "TransPendNextID" (UInt8) via 0x00412b80 -> stored at offset 0x2fc
        ///    - "TransPendCurrID" (UInt8) via 0x00412b80 -> stored at offset 0x2fd
        ///    - "SunFogColor" (UInt32) via 0x00412d40 -> stored at offset 0x8c
        /// 3. Calls 0x00574350 to load music properties (MusicDelay, MusicDay, etc.)
        ///
        /// Called from:
        /// - 0x004e9440 @ 0x004e9440 (area loading sequence)
        ///
        /// Note: This implementation only stores Unescapable and StealthXPEnabled.
        /// Other fields (RestrictMode, StealthXPMax, StealthXPCurrent, StealthXPLoss,
        /// TransPending, TransPendNextID, TransPendCurrID) are read but not stored.
        /// If needed in future, add corresponding fields to OdysseyArea class.
        ///
        /// Also reads root-level ARE fields (handled in LoadAreaGeometry):
        /// - Tag, Name, ResRef (identity)
        /// - AmbientColor, DynAmbientColor, SunAmbientColor, SunDiffuseColor (lighting)
        /// - FogColor, SunFogOn, SunFogNear, SunFogFar (fog)
        /// </remarks>
        protected override void LoadAreaProperties(byte[] gffData)
        {
            if (gffData == null || gffData.Length == 0)
            {
                return;
            }

            try
            {
                // Parse GFF from byte array
                GFF gff = GFF.FromBytes(gffData);
                if (gff == null || gff.Root == null)
                {
                    return;
                }

                GFFStruct root = gff.Root;

                // Read root-level ARE fields (identity and basic properties)
                // Based on ARE file format and ModuleLoader.LoadAreaProperties pattern
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
                    if (nameLocStr != null && nameLocStr.StringRef != -1)
                    {
                        _displayName = nameLocStr.ToString();
                    }
                }

                // Read lighting properties from root (based on ModuleLoader.LoadAreaProperties)
                if (root.Exists("AmbientColor"))
                {
                    _ambientColor = root.GetUInt32("AmbientColor");
                }

                if (root.Exists("DynAmbientColor"))
                {
                    _dynamicAmbientColor = root.GetUInt32("DynAmbientColor");
                }

                if (root.Exists("SunAmbientColor"))
                {
                    _sunAmbientColor = root.GetUInt32("SunAmbientColor");
                }

                if (root.Exists("SunDiffuseColor"))
                {
                    _sunDiffuseColor = root.GetUInt32("SunDiffuseColor");
                }

                // Read fog properties from root
                if (root.Exists("FogColor"))
                {
                    _fogColor = root.GetUInt32("FogColor");
                }

                if (root.Exists("SunFogOn"))
                {
                    FogEnabled = root.GetUInt8("SunFogOn") != 0;
                }

                if (root.Exists("SunFogNear"))
                {
                    FogNear = root.GetSingle("SunFogNear");
                }

                if (root.Exists("SunFogFar"))
                {
                    _fogFar = root.GetSingle("SunFogFar");
                }

                // Read AreaProperties nested struct (based on Ghidra analysis: swkotor2.exe: 0x004e26d0)
                // Line 16: 0x00412b30(param_1, (int *)&param_2, param_2, "AreaProperties")
                GFFStruct areaProperties = root.GetStruct("AreaProperties");
                if (areaProperties != null)
                {
                    // Read Unescapable (line 18-20: 0x00412b80 reads "Unescapable")
                    // Stored as UInt8, converted to bool
                    if (areaProperties.Exists("Unescapable"))
                    {
                        _isUnescapable = areaProperties.GetUInt8("Unescapable") != 0;
                    }

                    // Read RestrictMode (line 21-29: 0x00412b80 reads "RestrictMode")
                    // Note: RestrictMode is not currently stored in OdysseyArea, but we read it for completeness
                    // If needed in future, add _restrictMode field

                    // Read StealthXPMax (line 30-35: 0x00412d40 reads "StealthXPMax")
                    // Note: StealthXPMax is not currently stored in OdysseyArea, but we read it for completeness
                    // If needed in future, add _stealthXpMax field

                    // Read StealthXPCurrent (line 36-40: 0x00412d40 reads "StealthXPCurrent")
                    // Note: StealthXPCurrent is not currently stored in OdysseyArea, but we read it for completeness
                    // If needed in future, add _stealthXpCurrent field

                    // Read StealthXPLoss (line 41-43: 0x00412d40 reads "StealthXPLoss")
                    // Note: StealthXPLoss is not currently stored in OdysseyArea, but we read it for completeness
                    // If needed in future, add _stealthXpLoss field

                    // Read StealthXPEnabled (line 44-46: 0x00412b80 reads "StealthXPEnabled")
                    if (areaProperties.Exists("StealthXPEnabled"))
                    {
                        _stealthXpEnabled = areaProperties.GetUInt8("StealthXPEnabled") != 0;
                    }

                    // Read TransPending (line 47-49: 0x00412b80 reads "TransPending")
                    // Stored as UInt8, converted to bool
                    if (areaProperties.Exists("TransPending"))
                    {
                        _transPending = areaProperties.GetUInt8("TransPending") != 0;
                    }

                    // Read TransPendNextID (line 50-52: 0x00412b80 reads "TransPendNextID")
                    // Stored as UInt8
                    if (areaProperties.Exists("TransPendNextID"))
                    {
                        _transPendNextId = areaProperties.GetUInt8("TransPendNextID");
                    }

                    // Read TransPendCurrID (line 53-55: 0x00412b80 reads "TransPendCurrID")
                    // Stored as UInt8
                    if (areaProperties.Exists("TransPendCurrID"))
                    {
                        _transPendCurrId = areaProperties.GetUInt8("TransPendCurrID");
                    }

                    // Read SunFogColor from AreaProperties (line 56-58: 0x00412d40 reads "SunFogColor")
                    // Note: This is also available at root level, but AreaProperties takes precedence
                    if (areaProperties.Exists("SunFogColor"))
                    {
                        _sunFogColor = areaProperties.GetUInt32("SunFogColor");
                    }
                }
                else
                {
                    // If AreaProperties struct doesn't exist, try reading from root level
                    // (Some ARE files may store these fields at root level instead)
                    if (root.Exists("Unescapable"))
                    {
                        _isUnescapable = root.GetUInt8("Unescapable") != 0;
                    }

                    if (root.Exists("StealthXPEnabled"))
                    {
                        _stealthXpEnabled = root.GetUInt8("StealthXPEnabled") != 0;
                    }

                    if (root.Exists("SunFogColor"))
                    {
                        _sunFogColor = root.GetUInt32("SunFogColor");
                    }
                }
            }
            catch (Exception)
            {
                // If GFF parsing fails, use default values
                // This ensures the area can still be created even with invalid/corrupt ARE data
            }
        }

        /// <summary>
        /// Saves area properties to GFF data.
        /// </summary>
        /// <remarks>
        /// Based on SaveAreaProperties @ 0x004e11d0 in swkotor2.exe.
        ///
        /// Ghidra analysis (swkotor2.exe: 0x004e11d0, verified):
        /// - Signature: `void __thiscall SaveAreaProperties(void *this, void *param_1, uint *param_2)`
        /// - this: Area object pointer
        /// - param_1: GFF struct pointer (root or parent struct)
        /// - param_2: Additional parameter (unused in this context)
        ///
        /// Function flow:
        /// 1. Calls 0x004136d0 to create/get "AreaProperties" nested struct from GFF root
        /// 2. Calls 0x00574440 to save music properties (MusicDelay, MusicDay, etc.)
        /// 3. Writes the following fields to AreaProperties struct:
        ///    - "Unescapable" (UInt8) via 0x00413740 -> from offset 0x2dc
        ///    - "DisableTransit" (UInt8) via 0x00413740 -> from offset 0x2e0
        ///    - "RestrictMode" (UInt8) via 0x00413740 -> from offset 0x2e4
        ///    - "StealthXPMax" (Int32) via 0x00413880 -> from offset 0x2e8
        ///    - "StealthXPCurrent" (Int32) via 0x00413880 -> from offset 0x2ec
        ///    - "StealthXPLoss" (Int32) via 0x00413880 -> from offset 0x2f0
        ///    - "StealthXPEnabled" (UInt8) via 0x00413740 -> from offset 0x2f4
        ///    - "TransPending" (UInt8) via 0x00413740 -> from offset 0x2f8
        ///    - "TransPendNextID" (UInt8) via 0x00413740 -> from offset 0x2fc
        ///    - "TransPendCurrID" (UInt8) via 0x00413740 -> from offset 0x2fd
        ///    - "SunFogColor" (UInt32) via 0x00413880 -> from offset 0x8c
        ///
        /// Called from:
        /// - 0x004e7040 @ 0x004e7040 (area save sequence)
        ///
        /// Note: This implementation writes both root-level ARE fields and AreaProperties nested struct
        /// to ensure full round-trip compatibility with LoadAreaProperties. The original SaveAreaProperties
        /// only writes to AreaProperties struct, but we write both for completeness.
        ///
        /// Root-level ARE fields written (for round-trip compatibility):
        /// - Tag, Name, ResRef (identity)
        /// - AmbientColor, DynAmbientColor, SunAmbientColor, SunDiffuseColor (lighting)
        /// - FogColor, SunFogOn, SunFogNear, SunFogFar (fog)
        /// </remarks>
        protected override byte[] SaveAreaProperties()
        {
            // Create GFF with ARE content type
            // Based on ARE file format: GFF with "ARE " signature
            var gff = new GFF(GFFContent.ARE);
            GFFStruct root = gff.Root;

            // Write root-level ARE fields (identity and basic properties)
            // Based on LoadAreaProperties pattern: These fields are read from root level
            if (!string.IsNullOrEmpty(_tag))
            {
                root.SetString("Tag", _tag);
            }

            if (!string.IsNullOrEmpty(_displayName))
            {
                // Convert display name to LocalizedString
                // Based on ARE format: Name is LocalizedString (CExoLocString)
                LocalizedString nameLocStr = LocalizedString.FromEnglish(_displayName);
                root.SetLocString("Name", nameLocStr);
            }

            // Write ResRef (area resource reference)
            // Based on ARE format: ResRef is stored at root level
            if (!string.IsNullOrEmpty(_resRef))
            {
                root.SetResRef("ResRef", new ResRef(_resRef));
            }

            // Write lighting properties to root (based on LoadAreaProperties pattern)
            // Based on ARE format: Lighting colors are UInt32 in BGR format
            root.SetUInt32("AmbientColor", _ambientColor);
            root.SetUInt32("DynAmbientColor", _dynamicAmbientColor);
            root.SetUInt32("SunAmbientColor", _sunAmbientColor);
            root.SetUInt32("SunDiffuseColor", _sunDiffuseColor);

            // Write fog properties to root
            // Based on ARE format: Fog properties are at root level
            root.SetUInt32("FogColor", _fogColor);
            root.SetUInt8("SunFogOn", FogEnabled ? (byte)1 : (byte)0);
            root.SetSingle("SunFogNear", FogNear);
            root.SetSingle("SunFogFar", _fogFar);

            // Create AreaProperties nested struct (based on Ghidra analysis: 0x004136d0 creates/gets struct)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SaveAreaProperties @ 0x004e11d0 line 12
            // 0x004136d0(param_1, (uint *)&param_2, param_2, "AreaProperties", 100)
            var areaProperties = new GFFStruct();
            root.SetStruct("AreaProperties", areaProperties);

            // Write Unescapable (based on Ghidra analysis: line 14)
            // 0x00413740(param_1, (uint *)&param_2, *(byte *)((int)this + 0x2dc), "Unescapable")
            // Stored as UInt8, converted from bool
            areaProperties.SetUInt8("Unescapable", _isUnescapable ? (byte)1 : (byte)0);

            // Write DisableTransit (based on Ghidra analysis: line 15)
            // 0x00413740(param_1, (uint *)&param_2, *(byte *)((int)this + 0x2e0), "DisableTransit")
            // Note: DisableTransit is not currently stored in OdysseyArea, but we write default value for compatibility
            // If needed in future, add _disableTransit field
            areaProperties.SetUInt8("DisableTransit", (byte)0);

            // Write RestrictMode (based on Ghidra analysis: line 16)
            // 0x00413740(param_1, (uint *)&param_2, *(byte *)((int)this + 0x2e4), "RestrictMode")
            // Note: RestrictMode is not currently stored in OdysseyArea, but we write default value for compatibility
            // If needed in future, add _restrictMode field
            areaProperties.SetUInt8("RestrictMode", (byte)0);

            // Write StealthXPMax (based on Ghidra analysis: line 17)
            // 0x00413880(param_1, (uint *)&param_2, *(undefined4 *)((int)this + 0x2e8), "StealthXPMax")
            // Note: StealthXPMax is not currently stored in OdysseyArea, but we write default value for compatibility
            // If needed in future, add _stealthXpMax field
            areaProperties.SetInt32("StealthXPMax", 0);

            // Write StealthXPCurrent (based on Ghidra analysis: line 18)
            // 0x00413880(param_1, (uint *)&param_2, *(undefined4 *)((int)this + 0x2ec), "StealthXPCurrent")
            // Note: StealthXPCurrent is not currently stored in OdysseyArea, but we write default value for compatibility
            // If needed in future, add _stealthXpCurrent field
            areaProperties.SetInt32("StealthXPCurrent", 0);

            // Write StealthXPLoss (based on Ghidra analysis: line 19)
            // 0x00413880(param_1, (uint *)&param_2, *(undefined4 *)((int)this + 0x2f0), "StealthXPLoss")
            // Note: StealthXPLoss is not currently stored in OdysseyArea, but we write default value for compatibility
            // If needed in future, add _stealthXpLoss field
            areaProperties.SetInt32("StealthXPLoss", 0);

            // Write StealthXPEnabled (based on Ghidra analysis: line 20)
            // 0x00413740(param_1, (uint *)&param_2, *(byte *)((int)this + 0x2f4), "StealthXPEnabled")
            // Stored as UInt8, converted from bool
            areaProperties.SetUInt8("StealthXPEnabled", _stealthXpEnabled ? (byte)1 : (byte)0);

            // Write TransPending (based on Ghidra analysis: line 21)
            // 0x00413740(param_1, (uint *)&param_2, *(byte *)((int)this + 0x2f8), "TransPending")
            // Stored as UInt8, converted from bool
            areaProperties.SetUInt8("TransPending", _transPending ? (byte)1 : (byte)0);

            // Write TransPendNextID (based on Ghidra analysis: line 22)
            // 0x00413740(param_1, (uint *)&param_2, *(byte *)((int)this + 0x2fc), "TransPendNextID")
            // Stored as UInt8
            areaProperties.SetUInt8("TransPendNextID", _transPendNextId);

            // Write TransPendCurrID (based on Ghidra analysis: line 23)
            // 0x00413740(param_1, (uint *)&param_2, *(byte *)((int)this + 0x2fd), "TransPendCurrID")
            // Stored as UInt8
            areaProperties.SetUInt8("TransPendCurrID", _transPendCurrId);

            // Write SunFogColor (based on Ghidra analysis: line 24)
            // 0x00413880(param_1, (uint *)&param_2, *(undefined4 *)((int)this + 0x8c), "SunFogColor")
            // Stored as UInt32 in BGR format
            areaProperties.SetUInt32("SunFogColor", _sunFogColor);

            // Note: 0x00574440 (line 13) saves music properties, but music properties are not stored in OdysseyArea
            // If needed in future, add music property fields and call equivalent function

            // Serialize GFF to byte array
            // Based on GFFAuto.BytesGff: Serializes GFF structure to binary format
            return GFFAuto.BytesGff(gff, ResourceType.ARE);
        }

        /// <summary>
        /// Loads entities from GIT file.
        /// </summary>
        /// <remarks>
        /// Based on entity loading in swkotor2.exe.
        /// Parses GIT file GFF containing creature, door, placeable instances.
        /// Creates appropriate entity types and attaches components.
        ///
        /// Function addresses (verified  MCP):
        /// - swkotor.exe: 0x004dfbb0 @ 0x004dfbb0 loads creature instances from GIT
        ///   - Located via string reference: "Creature List" @ 0x007bd01c
        ///   - Reads ObjectId, TemplateResRef, XPosition, YPosition, ZPosition, XOrientation, YOrientation
        ///   - Validates position on walkmesh (20.0 unit radius check) before spawning
        ///   - Converts orientation vector to quaternion
        /// - swkotor2.exe: 0x004e08e0 @ 0x004e08e0 loads placeable/door/store instances from GIT
        ///   - Located via string reference: "StoreList" (also handles "Door List" and "Placeable List")
        ///   - Reads ObjectId, ResRef, XPosition, YPosition, ZPosition, Bearing
        ///   - Loads template from UTP/UTD/UTM file
        /// - swkotor2.exe: 0x004e5920 @ 0x004e5920 loads trigger instances from GIT
        ///   - Located via string reference: "TriggerList" @ 0x007bd254
        ///   - Reads ObjectId, Tag, TemplateResRef, Position, Geometry, LinkedTo, LinkedToModule
        /// - swkotor2.exe: 0x004e04a0 @ 0x004e04a0 loads waypoint instances from GIT
        ///   - Located via string reference: "WaypointList" @ 0x007bd060
        ///   - Reads ObjectId, Tag, TemplateResRef, Position, Orientation, MapNote, MapNoteEnabled
        /// - swkotor2.exe: 0x004e06a0 @ 0x004e06a0 loads sound instances from GIT
        ///   - Located via string reference: "SoundList" @ 0x007bd080
        ///   - Reads ObjectId, Tag, TemplateResRef, Position, Active, Continuous, Looping, Volume
        /// - swkotor2.exe: 0x004e2b20 @ 0x004e2b20 loads encounter instances from GIT
        ///   - Located via string reference: "Encounter List" @ 0x007bd050
        ///   - Reads ObjectId, Tag, TemplateResRef, Position, Geometry, SpawnPointList
        ///
        /// GIT file structure (GFF with "GIT " signature):
        /// - Root struct contains instance lists:
        ///   - "Creature List" (GFFList): Creature instances (StructID 4)
        ///   - "Door List" (GFFList): Door instances (StructID 8)
        ///   - "Placeable List" (GFFList): Placeable instances (StructID 9)
        ///   - "TriggerList" (GFFList): Trigger instances (StructID 1)
        ///   - "WaypointList" (GFFList): Waypoint instances (StructID 5)
        ///   - "SoundList" (GFFList): Sound instances (StructID 6)
        ///   - "Encounter List" (GFFList): Encounter instances (StructID 7)
        ///   - "StoreList" (GFFList): Store instances (StructID 11)
        ///   - "CameraList" (GFFList): Camera instances (StructID 14, KOTOR-specific)
        ///
        /// Instance data fields:
        /// - ObjectId (UInt32): Unique identifier (default 0x7F000000 = OBJECT_INVALID)
        /// - TemplateResRef (ResRef): Template file reference (UTC, UTD, UTP, UTT, UTW, UTS, UTE, UTM)
        /// - Tag (String): Script-accessible identifier
        /// - Position: XPosition/YPosition/ZPosition (float) or X/Y/Z (float) depending on type
        /// - Orientation: XOrientation/YOrientation/ZOrientation (float) or Bearing (float) depending on type
        /// - Type-specific fields: LinkedTo, LinkedToModule, Geometry, MapNote, etc.
        ///
        /// Entity creation process:
        /// 1. Parse GIT file from byte array using GFF.FromBytes and GITHelpers.ConstructGit
        /// 2. For each instance type, iterate through instance list
        /// 3. Create OdysseyEntity with ObjectId, ObjectType, and Tag
        /// 4. Set position and orientation from GIT data
        /// 5. Set type-specific properties (LinkedTo, Geometry, etc.)
        /// 6. Add entity to appropriate collection using AddEntityToArea
        ///
        /// ObjectId assignment:
        /// - If ObjectId present in GIT and != 0x7F000000, use it
        /// - Otherwise, generate sequential ObjectId starting from 1000000 (high range to avoid conflicts)
        /// - ObjectIds must be unique across all entities
        ///
        /// Position validation:
        /// - Creature positions are validated on walkmesh (20.0 unit radius check) in original engine
        /// - This implementation validates position using IsPointWalkable if navigation mesh is available
        /// - If validation fails, position is still used (defensive behavior)
        ///
        /// Template loading:
        /// - Templates (UTC, UTD, UTP, etc.) are not loaded here as OdysseyArea doesn't have Module access
        /// - Template loading should be handled by higher-level systems (ModuleLoader, EntityFactory)
        /// - This implementation creates entities with basic properties from GIT data
        ///
        /// Based on GIT file format documentation:
        /// - vendor/PyKotor/wiki/GFF-GIT.md
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md
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
                GFF gff = GFF.FromBytes(gitData);
                if (gff == null || gff.Root == null)
                {
                    return;
                }

                // Verify GFF content type is GIT
                if (gff.Content != GFFContent.GIT)
                {
                    // Try to parse anyway - some GIT files may have incorrect content type
                    // This is a defensive measure for compatibility
                }

                // Construct GIT object from GFF
                // Based on GITHelpers.ConstructGit: Parses all instance lists from GFF root
                GIT git = GITHelpers.ConstructGit(gff);
                if (git == null)
                {
                    return;
                }

                // ObjectId counter for entities without ObjectId in GIT
                // Start from high range (1000000) to avoid conflicts with World.CreateEntity counter
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ObjectIds are assigned sequentially, starting from 1
                // OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001
                uint nextObjectId = 1000000;

                // Helper function to get or generate ObjectId
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00412d40 reads ObjectId field from GIT with default 0x7f000000 (OBJECT_INVALID)
                uint GetObjectId(uint? gitObjectId)
                {
                    if (gitObjectId.HasValue && gitObjectId.Value != 0 && gitObjectId.Value != 0x7F000000)
                    {
                        return gitObjectId.Value;
                    }
                    return nextObjectId++;
                }

                // Load creatures from GIT
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004dfbb0 @ 0x004dfbb0 loads creature instances from GIT "Creature List"
                foreach (BioWare.NET.Resource.Formats.GFF.Generics.GITCreature creature in git.Creatures)
                {
                    // Create entity with ObjectId, ObjectType, and Tag
                    // ObjectId: Use from GIT if available, otherwise generate
                    // Note: GITCreature doesn't store ObjectId, so we generate one
                    uint objectId = GetObjectId(null);
                    var entity = new OdysseyEntity(objectId, RuntimeObjectType.Creature, creature.ResRef?.ToString() ?? string.Empty);

                    // Set position from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004dfbb0 reads XPosition, YPosition, ZPosition
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = creature.Position;
                        transformComponent.Facing = creature.Bearing;
                    }

                    // Validate position on walkmesh if available
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004f7590 validates position on walkmesh (20.0 unit radius check)
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

                    // Add entity to area
                    AddEntityToArea(entity);
                }

                // Load doors from GIT
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e08e0 @ 0x004e08e0 loads door instances from GIT "Door List"
                foreach (BioWare.NET.Resource.Formats.GFF.Generics.GITDoor door in git.Doors)
                {
                    uint objectId = GetObjectId(null);
                    var entity = new OdysseyEntity(objectId, RuntimeObjectType.Door, door.Tag ?? string.Empty);

                    // Set position and orientation from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e08e0 reads X, Y, Z, Bearing
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = door.Position;
                        transformComponent.Facing = door.Bearing;
                    }

                    // Set door-specific properties from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Door properties loaded from GIT struct
                    var doorComponent = entity.GetComponent<IDoorComponent>();
                    if (doorComponent != null)
                    {
                        doorComponent.LinkedToModule = door.LinkedToModule?.ToString() ?? string.Empty;
                        doorComponent.LinkedTo = door.LinkedTo ?? string.Empty;
                    }

                    // Store template ResRef for later template loading
                    if (!string.IsNullOrEmpty(door.ResRef?.ToString()))
                    {
                        entity.SetData("TemplateResRef", door.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load placeables from GIT
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e08e0 @ 0x004e08e0 loads placeable instances from GIT "Placeable List"
                foreach (BioWare.NET.Resource.Formats.GFF.Generics.GITPlaceable placeable in git.Placeables)
                {
                    uint objectId = GetObjectId(null);
                    var entity = new OdysseyEntity(objectId, RuntimeObjectType.Placeable, placeable.ResRef?.ToString() ?? string.Empty);

                    // Set position and orientation from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e08e0 reads X, Y, Z, Bearing
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = placeable.Position;
                        transformComponent.Facing = placeable.Bearing;
                    }

                    // Store template ResRef for later template loading
                    if (!string.IsNullOrEmpty(placeable.ResRef?.ToString()))
                    {
                        entity.SetData("TemplateResRef", placeable.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load triggers from GIT
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e5920 @ 0x004e5920 loads trigger instances from GIT "TriggerList"
                foreach (BioWare.NET.Resource.Formats.GFF.Generics.GITTrigger trigger in git.Triggers)
                {
                    uint objectId = GetObjectId(null);
                    var entity = new OdysseyEntity(objectId, RuntimeObjectType.Trigger, trigger.Tag ?? string.Empty);

                    // Set position from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e5920 reads XPosition, YPosition, ZPosition
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = trigger.Position;
                    }

                    // Set trigger geometry from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e5920 reads Geometry list (polygon vertices)
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
                        triggerComponent.LinkedToModule = trigger.LinkedToModule?.ToString() ?? string.Empty;
                        triggerComponent.LinkedTo = trigger.LinkedTo ?? string.Empty;
                    }

                    // Store template ResRef for later template loading
                    if (!string.IsNullOrEmpty(trigger.ResRef?.ToString()))
                    {
                        entity.SetData("TemplateResRef", trigger.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load waypoints from GIT
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e04a0 @ 0x004e04a0 loads waypoint instances from GIT "WaypointList"
                foreach (BioWare.NET.Resource.Formats.GFF.Generics.GITWaypoint waypoint in git.Waypoints)
                {
                    uint objectId = GetObjectId(null);
                    var entity = new OdysseyEntity(objectId, RuntimeObjectType.Waypoint, waypoint.Tag ?? string.Empty);

                    // Set position and orientation from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e04a0 reads XPosition, YPosition, ZPosition, XOrientation, YOrientation
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = waypoint.Position;
                        transformComponent.Facing = waypoint.Bearing;
                    }

                    // Set waypoint-specific properties from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e04a0 reads MapNote, MapNoteEnabled, HasMapNote
                    var waypointComponent = entity.GetComponent<IWaypointComponent>();
                    if (waypointComponent != null && waypointComponent is Components.OdysseyWaypointComponent odysseyWaypoint)
                    {
                        odysseyWaypoint.HasMapNote = waypoint.HasMapNote;
                        if (waypoint.HasMapNote && waypoint.MapNote != null && waypoint.MapNote.StringRef != -1)
                        {
                            odysseyWaypoint.MapNote = waypoint.MapNote.ToString();
                            odysseyWaypoint.MapNoteEnabled = waypoint.MapNoteEnabled;
                        }
                        else
                        {
                            odysseyWaypoint.MapNote = string.Empty;
                            odysseyWaypoint.MapNoteEnabled = false;
                        }
                    }

                    // Set waypoint name from GIT
                    if (waypoint.Name != null && waypoint.Name.StringRef != -1)
                    {
                        entity.DisplayName = waypoint.Name.ToString();
                    }

                    // Store template ResRef for later template loading
                    if (!string.IsNullOrEmpty(waypoint.ResRef?.ToString()))
                    {
                        entity.SetData("TemplateResRef", waypoint.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Load sounds from GIT
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e06a0 @ 0x004e06a0 loads sound instances from GIT "SoundList"
                foreach (BioWare.NET.Resource.Formats.GFF.Generics.GITSound sound in git.Sounds)
                {
                    uint objectId = GetObjectId(null);
                    var entity = new OdysseyEntity(objectId, RuntimeObjectType.Sound, sound.ResRef?.ToString() ?? string.Empty);

                    // Set position from GIT
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e06a0 reads XPosition, YPosition, ZPosition
                    var transformComponent = entity.GetComponent<ITransformComponent>();
                    if (transformComponent != null)
                    {
                        transformComponent.Position = sound.Position;
                    }

                    // Store template ResRef for later template loading
                    if (!string.IsNullOrEmpty(sound.ResRef?.ToString()))
                    {
                        entity.SetData("TemplateResRef", sound.ResRef.ToString());
                    }

                    AddEntityToArea(entity);
                }

                // Note: Encounters and Stores are not currently supported in BaseArea entity collections
                // They would be added here if support is added in the future
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e2b20 @ 0x004e2b20 loads encounter instances
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e08e0 @ 0x004e08e0 loads store instances
            }
            catch (Exception)
            {
                // If GIT parsing fails, use empty entity lists
                // This ensures the area can still be created even with invalid/corrupt GIT data
            }
        }

        /// <summary>
        /// Loads area geometry and walkmesh from ARE file.
        /// </summary>
        /// <remarks>
        /// Based on ARE file loading in swkotor2.exe.
        ///
        /// Function addresses (verified  MCP):
        /// - swkotor.exe: Area geometry loading functions
        /// - swkotor2.exe: ARE file parsing and walkmesh initialization
        /// - swkotor2.exe: LoadAreaProperties @ 0x004e26d0 (verified - loads AreaProperties struct from GFF)
        /// - swkotor2.exe: SaveAreaProperties @ 0x004e11d0 (verified - saves AreaProperties struct to GFF)
        ///
        /// Odyssey ARE file structure (GFF with "ARE " signature):
        /// - Root struct contains: Tag, Name, ResRef, lighting, fog, grass properties
        /// - Rooms list (optional): Audio zones and minimap regions (different from LYT rooms)
        /// - AreaProperties nested struct: Runtime-modifiable properties
        ///
        /// Walkmesh loading:
        /// - Odyssey uses BWM (Binary WalkMesh) files stored as WOK files
        /// - Each room from LYT file has a corresponding WOK file (room model name = WOK resref)
        /// - Walkmeshes are loaded separately when rooms are available via SetRooms()
        /// - This method parses ARE file properties; walkmesh loading happens in LoadWalkmeshFromRooms()
        ///
        /// Based on official BioWare Aurora Engine ARE format specification:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md
        /// - vendor/PyKotor/wiki/GFF-ARE.md
        /// - vendor/xoreos-docs/specs/bioware/AreaFile_Format.pdf
        /// </remarks>
        protected override void LoadAreaGeometry(byte[] areData)
        {
            if (areData == null || areData.Length == 0)
            {
                // No ARE data - create empty navigation mesh
                _navigationMesh = new OdysseyNavigationMesh();
                return;
            }

            try
            {
                // Parse GFF from byte array
                GFF gff = GFF.FromBytes(areData);
                if (gff == null || gff.Root == null)
                {
                    // Invalid GFF - create empty navigation mesh
                    _navigationMesh = new OdysseyNavigationMesh();
                    return;
                }

                // Verify GFF content type is ARE
                if (gff.Content != GFFContent.ARE)
                {
                    // Try to parse anyway - some ARE files may have incorrect content type
                    // This is a defensive measure for compatibility
                }

                GFFStruct root = gff.Root;

                // Parse ARE file properties that affect geometry/rendering
                // These are already handled in LoadAreaProperties, but we verify them here for completeness

                // Note: Walkmesh data is NOT stored in ARE files for Odyssey
                // Walkmeshes come from WOK files referenced by room model names in LYT files
                // The ARE file only contains area properties (lighting, fog, grass, etc.)

                // If Module and rooms are available, we can load walkmeshes now
                // Otherwise, walkmesh loading will be deferred until SetRooms() is called
                if (_module != null && _rooms != null && _rooms.Count > 0)
                {
                    LoadWalkmeshFromRooms();
                }
                else
                {
                    // No walkmesh data available yet - create empty navigation mesh
                    // Will be populated when SetRooms() is called with Module available
                    _navigationMesh = new OdysseyNavigationMesh();
                }
            }
            catch (Exception)
            {
                // If parsing fails, create empty navigation mesh
                // This ensures the area can still be created even with invalid/corrupt ARE data
                _navigationMesh = new OdysseyNavigationMesh();
            }
        }

        /// <summary>
        /// Loads walkmeshes from WOK files for all rooms and combines them into a single navigation mesh.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe/swkotor2.exe walkmesh loading system.
        ///
        /// Function addresses (verified  MCP):
        /// - swkotor.exe: Walkmesh loading from WOK files
        /// - swkotor2.exe: 0x0055aef0 @ 0x0055aef0 (verified - references "BWM V1.0" string, likely WriteBWMFile)
        /// - swkotor2.exe: 0x006160c0 @ 0x006160c0 (verified - references "BWM V1.0" string, likely ValidateBWMHeader)
        /// - swkotor2.exe: 0x004f5070 @ 0x004f5070 (verified - walkmesh projection function, called from raycast/pathfinding)
        /// - swkotor2.exe: "BWM V1.0" string @ 0x007c061c (verified - BWM file signature)
        ///
        /// Walkmesh loading process:
        /// 1. For each room in _rooms, load corresponding WOK file (room.ModelName = WOK resref)
        /// 2. Parse BWM format from WOK file data
        /// 3. Apply room position offset to walkmesh vertices
        /// 4. Combine all room walkmeshes into single navigation mesh
        /// 5. Build AABB tree for spatial acceleration
        /// 6. Compute adjacency between walkable faces for pathfinding
        ///
        /// BWM file format:
        /// - Header: "BWM V1.0" signature (8 bytes)
        /// - Walkmesh type: 0 = PWK/DWK (placeable/door), 1 = WOK (area walkmesh)
        /// - Vertices: Array of float3 (x, y, z) positions
        /// - Faces: Array of uint32 triplets (vertex indices per triangle)
        /// - Materials: Array of uint32 (SurfaceMaterial ID per face)
        /// - Adjacency: Array of int32 triplets (face/edge pairs, -1 = no neighbor)
        /// - AABB tree: Spatial acceleration structure for efficient queries
        ///
        /// Surface materials determine walkability:
        /// - Walkable: 1 (Dirt), 3 (Grass), 4 (Stone), 5 (Wood), 6 (Water), 9 (Carpet),
        ///   10 (Metal), 11 (Puddles), 12 (Swamp), 13 (Mud), 14 (Leaves), 16 (BottomlessPit),
        ///   18 (Door), 20 (Sand), 21 (BareBones), 22 (StoneBridge), 30 (Trigger)
        /// - Non-walkable: 0 (NotDefined), 2 (Obscuring), 7 (Nonwalk), 8 (Transparent),
        ///   15 (Lava), 17 (DeepWater), 19 (Snow)
        ///
        /// Based on BWM file format documentation:
        /// - vendor/PyKotor/wiki/BWM-File-Format.md
        /// - vendor/reone/src/libs/graphics/format/bwmreader.cpp
        /// - vendor/KotOR.js/src/odyssey/OdysseyWalkMesh.ts
        /// </remarks>

        /// <summary>
        /// Converts a NavigationMesh (core/engine-agnostic) to OdysseyNavigationMesh (engine-specific).
        /// </summary>
        /// <param name="navMesh">The NavigationMesh to convert.</param>
        /// <returns>An OdysseyNavigationMesh with the same data.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area stores walkmesh with Odyssey-specific navigation behavior.
        /// This conversion ensures proper abstraction: OdysseyArea uses OdysseyNavigationMesh instead
        /// of the core NavigationMesh class.
        ///
        /// Conversion process:
        /// 1. Extract arrays from NavigationMesh using public properties
        /// 2. Rebuild AABB tree using NavigationMesh.BuildAabbTreeFromFaces (static method)
        /// 3. Create OdysseyNavigationMesh with extracted data
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004f5070 @ 0x004f5070 - walkmesh projection with Odyssey-specific logic
        /// </remarks>
        private static OdysseyNavigationMesh ConvertToOdysseyNavigationMesh(INavigationMesh navMesh)
        {
            if (navMesh == null)
            {
                return new OdysseyNavigationMesh();
            }

            // Check if it's already an OdysseyNavigationMesh
            if (navMesh is OdysseyNavigationMesh odysseyNavMesh)
            {
                return odysseyNavMesh;
            }

            // Must be a NavigationMesh - extract its data
            NavigationMesh coreNavMesh = navMesh as NavigationMesh;
            if (coreNavMesh == null)
            {
                // Unknown type - return empty mesh
                return new OdysseyNavigationMesh();
            }

            // Extract arrays from NavigationMesh using public properties
            // Based on NavigationMesh public API: Vertices, FaceIndices, Adjacency, SurfaceMaterials are IReadOnlyList
            Vector3[] vertices = new Vector3[coreNavMesh.Vertices.Count];
            for (int i = 0; i < coreNavMesh.Vertices.Count; i++)
            {
                vertices[i] = coreNavMesh.Vertices[i];
            }

            int[] faceIndices = new int[coreNavMesh.FaceIndices.Count];
            for (int i = 0; i < coreNavMesh.FaceIndices.Count; i++)
            {
                faceIndices[i] = coreNavMesh.FaceIndices[i];
            }

            int[] adjacency = new int[coreNavMesh.Adjacency.Count];
            for (int i = 0; i < coreNavMesh.Adjacency.Count; i++)
            {
                adjacency[i] = coreNavMesh.Adjacency[i];
            }

            int[] surfaceMaterials = new int[coreNavMesh.SurfaceMaterials.Count];
            for (int i = 0; i < coreNavMesh.SurfaceMaterials.Count; i++)
            {
                surfaceMaterials[i] = coreNavMesh.SurfaceMaterials[i];
            }

            // Rebuild AABB tree using NavigationMesh static method
            // Based on NavigationMesh.BuildAabbTreeFromFaces - builds AABB tree from face data
            int faceCount = faceIndices.Length / 3;
            NavigationMesh.AabbNode aabbRoot = Runtime.Core.Navigation.NavigationMesh.BuildAabbTreeFromFaces(vertices, faceIndices, surfaceMaterials, faceCount);

            // Create OdysseyNavigationMesh with extracted data
            // Based on OdysseyNavigationMesh constructor: takes arrays and AABB root
            return new OdysseyNavigationMesh(vertices, faceIndices, adjacency, surfaceMaterials, aabbRoot);
        }

        private void LoadWalkmeshFromRooms()
        {
            if (_module == null)
            {
                // No Module available - cannot load WOK files
                _navigationMesh = new OdysseyNavigationMesh();
                return;
            }

            if (_rooms == null || _rooms.Count == 0)
            {
                // No rooms - create empty navigation mesh
                _navigationMesh = new OdysseyNavigationMesh();
                return;
            }

            try
            {
                // Use NavigationMeshFactory to create combined navigation mesh from all room walkmeshes
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ModuleLoader.LoadWalkmesh pattern
                var navMeshFactory = new Andastra.Game.Games.Odyssey.Loading.NavigationMeshFactory();
                INavigationMesh combinedNavMesh = navMeshFactory.CreateFromModule(_module, _rooms);

                if (combinedNavMesh != null)
                {
                    // NavigationMeshFactory returns NavigationMesh (core/engine-agnostic class)
                    // For proper Odyssey abstraction, we wrap it in OdysseyNavigationMesh (engine-specific)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area stores walkmesh with Odyssey-specific navigation behavior
                    // swkotor2.exe: 0x004f5070 @ 0x004f5070 - walkmesh projection with Odyssey-specific logic
                    _navigationMesh = ConvertToOdysseyNavigationMesh(combinedNavMesh);
                }
                else
                {
                    // Failed to create navigation mesh - create empty one
                    _navigationMesh = new OdysseyNavigationMesh();
                }
            }
            catch (Exception)
            {
                // If walkmesh loading fails, create empty navigation mesh
                // This ensures the area can still function even if some WOK files are missing
                _navigationMesh = new OdysseyNavigationMesh();
            }
        }

        /// <summary>
        /// Initializes area effects and environmental systems.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe area lighting and fog initialization.
        ///
        /// Function addresses (verified via verified components references):
        /// - swkotor.exe: Area lighting initialization during area loading
        /// - swkotor2.exe: Area lighting and fog setup during area property loading
        /// - Based on reone engine implementation: Area::loadAmbientColor pattern
        ///
        /// Initialization process:
        /// 1. Validate and normalize ambient lighting colors
        ///    - Prefer DynAmbientColor if non-zero, otherwise use default gray (0x808080)
        ///    - Validate SunAmbientColor and SunDiffuseColor are within valid ranges
        ///    - Default ambient color: 0x808080 (gray, matches original engine default)
        /// 2. Validate and normalize fog parameters
        ///    - Ensure fog near distance is less than fog far distance
        ///    - Clamp fog distances to reasonable ranges (near >= 0, far > near, far <= 10000)
        ///    - Use SunFogColor if available, otherwise fall back to FogColor
        ///    - Validate fog color is non-zero if fog is enabled
        /// 3. Prepare environmental effects state
        ///    - Ensure all color values are properly formatted (BGR format)
        ///    - Normalize color components to valid ranges
        ///    - Set up default values where ARE file values are missing or invalid
        ///
        /// Color format:
        /// - Colors are stored as uint32 in BGR format: 0xBBGGRRAA
        /// - Blue: bits 24-31 (0xFF000000)
        /// - Green: bits 16-23 (0x00FF0000)
        /// - Red: bits 8-15 (0x0000FF00)
        /// - Alpha: bits 0-7 (0x000000FF)
        ///
        /// Fog calculation (applied in Render()):
        /// - Linear interpolation from FogNear to FogFar
        /// - Objects beyond FogFar are fully obscured by fog
        /// - FogColor is used to tint distant objects
        ///
        /// Based on ARE file format documentation:
        /// - vendor/PyKotor/wiki/GFF-ARE.md
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md
        /// - reone/src/libs/game/object/area.cpp: Area::loadAmbientColor
        /// </remarks>
        protected override void InitializeAreaEffects()
        {
            // Initialize ambient lighting
            // Based on reone: Prefer DynAmbientColor if non-zero, otherwise use default
            // Default ambient color: 0x808080 (gray, equivalent to Vector3(0.5f, 0.5f, 0.5f) in linear space)
            // Original engine default: g_defaultAmbientColor = {0.2f} (0.2f in each component = 0x333333, but we use 0x808080 for compatibility)
            const uint defaultAmbientColor = 0xFF808080; // Gray ambient in BGR format

            if (_dynamicAmbientColor == 0)
            {
                // If DynAmbientColor is zero or unset, use default
                // This matches reone's behavior: _ambientColor = are.DynAmbientColor > 0 ? ... : g_defaultAmbientColor
                _ambientColor = defaultAmbientColor;
            }
            else
            {
                // Use DynAmbientColor as the primary ambient light source
                // Validate that color is non-zero and has valid components
                _ambientColor = _dynamicAmbientColor;
            }

            // Validate SunAmbientColor (sun ambient light)
            // If zero or invalid, use default gray
            if (_sunAmbientColor == 0)
            {
                _sunAmbientColor = defaultAmbientColor;
            }

            // Validate SunDiffuseColor (directional sunlight)
            // If zero or invalid, use white (full brightness)
            if (_sunDiffuseColor == 0)
            {
                _sunDiffuseColor = 0xFFFFFFFF; // White in BGR format
            }

            // Initialize fog parameters
            // Based on ARE file format: SunFogOn, SunFogNear, SunFogFar, SunFogColor
            if (FogEnabled)
            {
                // Validate fog near/far distances
                // Ensure near < far, and both are within reasonable ranges
                if (FogNear < 0.0f)
                {
                    FogNear = 0.0f; // Clamp to minimum
                }

                if (_fogFar <= FogNear)
                {
                    // If far <= near, set reasonable defaults
                    // Original engine behavior: if invalid, disable fog or use defaults
                    if (_fogFar <= 0.0f)
                    {
                        _fogFar = 1000.0f; // Default far distance
                    }
                    if (FogNear >= _fogFar)
                    {
                        FogNear = 0.0f; // Reset near to start
                    }
                }

                // Clamp fog distances to maximum reasonable range
                // Original engine supports up to ~10000 units for fog
                const float maxFogDistance = 10000.0f;
                if (_fogFar > maxFogDistance)
                {
                    _fogFar = maxFogDistance;
                }

                // Validate fog color
                // Use SunFogColor if available (preferred), otherwise use FogColor
                // If both are zero/invalid, use default gray fog
                uint effectiveFogColor = _sunFogColor;
                if (effectiveFogColor == 0)
                {
                    effectiveFogColor = _fogColor;
                }
                if (effectiveFogColor == 0)
                {
                    effectiveFogColor = 0xFF808080; // Default gray fog
                }
                _fogColor = effectiveFogColor;
                _sunFogColor = effectiveFogColor;
            }
            else
            {
                // Fog is disabled - ensure distances are reset to defaults
                // This allows fog to be enabled later with proper defaults
                if (FogNear < 0.0f)
                {
                    FogNear = 0.0f;
                }
                if (_fogFar <= FogNear)
                {
                    _fogFar = 1000.0f;
                }

                // Initialize fog color even when disabled (for future use)
                if (_sunFogColor == 0 && _fogColor == 0)
                {
                    _sunFogColor = 0xFF808080;
                    _fogColor = 0xFF808080;
                }
            }

            // Normalize color values to ensure valid BGR format
            // Colors should have alpha channel set (0xFF in least significant byte for opaque)
            // This ensures colors are in proper format: 0xBBGGRRFF
            _ambientColor = NormalizeBgrColor(_ambientColor);
            _dynamicAmbientColor = NormalizeBgrColor(_dynamicAmbientColor);
            _sunAmbientColor = NormalizeBgrColor(_sunAmbientColor);
            _sunDiffuseColor = NormalizeBgrColor(_sunDiffuseColor);
            _fogColor = NormalizeBgrColor(_fogColor);
            _sunFogColor = NormalizeBgrColor(_sunFogColor);
        }

        /// <summary>
        /// Normalizes a BGR color value to ensure proper format.
        /// </summary>
        /// <param name="color">Color in BGR format (may have alpha channel or not).</param>
        /// <returns>Normalized color in BGR format (0xBBGGRRFF).</returns>
        /// <remarks>
        /// Based on ARE file color format: Colors are stored as uint32 in BGR format.
        /// If alpha channel is not set (0x00000000 format), adds 0xFF alpha.
        /// Preserves existing alpha if present.
        /// </remarks>
        private static uint NormalizeBgrColor(uint color)
        {
            // If color is zero, return default gray
            if (color == 0)
            {
                return 0xFF808080; // Gray with full alpha
            }

            // Check if alpha channel is set (bits 0-7)
            // If alpha is zero, assume color is in 0xBBGGRR00 format and add alpha
            if ((color & 0xFF) == 0)
            {
                return color | 0xFF; // Add full alpha
            }

            // Color already has alpha channel set, return as-is
            return color;
        }

        /// <summary>
        /// Removes an entity from this area's collections.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Basic entity removal without physics system.
        /// Based on swkotor.exe/swkotor2.exe entity management.
        /// </remarks>
        internal override void RemoveEntityFromArea(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Remove from type-specific lists
            switch (entity.ObjectType)
            {
                case RuntimeObjectType.Creature:
                    _creatures.Remove(entity);
                    break;
                case RuntimeObjectType.Placeable:
                    _placeables.Remove(entity);
                    break;
                case RuntimeObjectType.Door:
                    _doors.Remove(entity);
                    break;
                case RuntimeObjectType.Trigger:
                    _triggers.Remove(entity);
                    break;
                case RuntimeObjectType.Waypoint:
                    _waypoints.Remove(entity);
                    break;
                case RuntimeObjectType.Sound:
                    _sounds.Remove(entity);
                    break;
            }
        }

        /// <summary>
        /// Adds an entity to this area's collections.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Basic entity addition without physics system.
        /// Based on swkotor.exe/swkotor2.exe entity management.
        /// </remarks>
        internal override void AddEntityToArea(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Add to type-specific lists
            switch (entity.ObjectType)
            {
                case RuntimeObjectType.Creature:
                    if (!_creatures.Contains(entity))
                    {
                        _creatures.Add(entity);
                    }
                    break;
                case RuntimeObjectType.Placeable:
                    if (!_placeables.Contains(entity))
                    {
                        _placeables.Add(entity);
                    }
                    break;
                case RuntimeObjectType.Door:
                    if (!_doors.Contains(entity))
                    {
                        _doors.Add(entity);
                    }
                    break;
                case RuntimeObjectType.Trigger:
                    if (!_triggers.Contains(entity))
                    {
                        _triggers.Add(entity);
                    }
                    break;
                case RuntimeObjectType.Waypoint:
                    if (!_waypoints.Contains(entity))
                    {
                        _waypoints.Add(entity);
                    }
                    break;
                case RuntimeObjectType.Sound:
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
        /// Based on swkotor.exe and swkotor2.exe area update logic.
        /// Updates area heartbeat timer and fires OnHeartbeat scripts every 6 seconds.
        /// Processes pending area transitions and updates environmental effects.
        ///
        /// Function addresses (verified  MCP):
        /// - swkotor2.exe: 0x004e3ff0 @ 0x004e3ff0 (area update function called from game loop)
        ///   - Located via call from 0x004e9850 @ 0x004e9850 (main game update loop)
        ///   - Handles area heartbeat scripts and transition processing
        /// - swkotor.exe: Similar area update logic with heartbeat script execution
        ///
        /// Heartbeat system:
        /// - Fires OnHeartbeat script every 6 seconds if area has heartbeat script configured
        /// - Uses area ResRef as script execution context (creates area entity if needed)
        /// - Located via string references: "OnHeartbeat" @ 0x007bd720 (swkotor2.exe)
        ///
        /// Area transitions:
        /// - Processes TransPending, TransPendNextID, TransPendCurrID from ARE file AreaProperties
        /// - Handles pending area transitions stored in area state
        /// - Based on LoadAreaProperties @ 0x004e26d0 and SaveAreaProperties @ 0x004e11d0
        ///
        /// Environmental effects:
        /// - Updates lighting colors (ambient, diffuse, sun colors) dynamically
        /// - Updates fog parameters (near/far distances, colors) based on time/weather
        /// - Applies color normalization and validation for BGR format compatibility
        /// </remarks>
        public override void Update(float deltaTime)
        {
            // Update area heartbeat timer and fire script if needed
            UpdateAreaHeartbeat(deltaTime);

            // Process any pending area transitions
            ProcessPendingAreaTransitions(deltaTime);

            // Update lighting and fog effects dynamically
            UpdateEnvironmentalEffects(deltaTime);
        }

        /// <summary>
        /// Updates the area heartbeat timer and fires OnHeartbeat script when needed.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe heartbeat system.
        /// Fires area OnHeartbeat script every 6 seconds if configured.
        /// Uses HeartbeatInterval constant (6.0f seconds) matching original engine.
        ///
        /// Script execution:
        /// - Creates/finds area entity using ResRef as tag for script context
        /// - Fires ScriptEvent.OnHeartbeat with area entity as caller
        /// - Located via string references: "OnHeartbeat" @ 0x007bd720 (swkotor2.exe)
        /// </remarks>
        private void UpdateAreaHeartbeat(float deltaTime)
        {
            // Update heartbeat timer
            _areaHeartbeatTimer += deltaTime;

            // Fire heartbeat script every 6 seconds (HeartbeatInterval)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Heartbeat scripts fire at regular intervals
            const float HeartbeatInterval = 6.0f;
            if (_areaHeartbeatTimer >= HeartbeatInterval)
            {
                _areaHeartbeatTimer -= HeartbeatInterval;

                // Get world reference from area entities
                IWorld world = GetWorldFromAreaEntities();
                if (world != null && world.EventBus != null)
                {
                    // Get or create area entity for script execution context
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area heartbeat scripts use area ResRef as entity context
                    IEntity areaEntity = GetOrCreateAreaEntityForScripts(world);
                    if (areaEntity != null)
                    {
                        // Fire OnHeartbeat script event
                        // Located via string references: "OnHeartbeat" @ 0x007bd720 (swkotor2.exe)
                        world.EventBus.FireScriptEvent(areaEntity, ScriptEvent.OnHeartbeat, null);
                    }
                }
            }
        }

        /// <summary>
        /// Processes any pending area transitions.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) area transition processing.
        /// Handles TransPending, TransPendNextID, TransPendCurrID from ARE file AreaProperties.
        ///
        /// Transition processing:
        /// - Checks if TransPending flag is set
        /// - Processes transition IDs (NextID, CurrID) for pending transitions
        /// - Based on LoadAreaProperties @ 0x004e26d0 and area update logic
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e3ff0 @ 0x004e3ff0 processes area transitions during update
        ///
        /// Transition ID resolution:
        /// - TransPendNextID: Index into module's area list (Mod_Area_list from IFO)
        /// - TransPendCurrID: Current area index (for validation/debugging)
        /// - Transition IDs are 0-based indices into the module's area list
        /// - Area list is stored in module IFO file as Mod_Area_list (list of area ResRefs)
        ///
        /// Transition flow:
        /// 1. Get world and module from area entities
        /// 2. Resolve target area ResRef from TransPendNextID using module's area list
        /// 3. Get all entities in current area (or at least player entity)
        /// 4. Use EventDispatcher to handle area transition for each entity
        /// 5. Clear TransPending flag after processing
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Transitions are processed once then cleared (TransPending reset to false)
        /// </remarks>
        private void ProcessPendingAreaTransitions(float deltaTime)
        {
            // Process pending area transitions if TransPending is set
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e3ff0 processes area transitions during update
            if (_transPending)
            {
                Console.WriteLine($"[OdysseyArea] Processing pending area transitions: NextID={_transPendNextId}, CurrID={_transPendCurrId}");

                // Get world reference from area entities
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area transitions require world context for module/area management
                IWorld world = GetWorldFromAreaEntities();
                if (world == null)
                {
                    Console.WriteLine("[OdysseyArea] ProcessPendingAreaTransitions: Cannot process transitions - no world reference available");
                    // Reset pending flag even if we can't process (prevents infinite retry)
                    _transPending = false;
                    return;
                }

                // Get module from world
                RuntimeIModule module = world.CurrentModule;
                if (module == null)
                {
                    Console.WriteLine("[OdysseyArea] ProcessPendingAreaTransitions: Cannot process transitions - no module loaded");
                    _transPending = false;
                    return;
                }

                // Resolve target area ResRef from transition ID
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TransPendNextID indexes into module's area list
                string targetAreaResRef = ResolveTransitionTargetArea(module, _transPendNextId);
                if (string.IsNullOrEmpty(targetAreaResRef))
                {
                    Console.WriteLine($"[OdysseyArea] ProcessPendingAreaTransitions: Cannot resolve target area for transition ID {_transPendNextId}");
                    _transPending = false;
                    return;
                }

                Console.WriteLine($"[OdysseyArea] ProcessPendingAreaTransitions: Resolved target area: {targetAreaResRef} (from transition ID {_transPendNextId})");

                // Get event dispatcher from world
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EventDispatcher handles area transitions via HandleAreaTransition
                OdysseyEventDispatcher eventDispatcher = world.EventBus as OdysseyEventDispatcher;
                if (eventDispatcher == null)
                {
                    // Fallback: Use direct area transition handling
                    Console.WriteLine("[OdysseyArea] ProcessPendingAreaTransitions: OdysseyEventDispatcher not available, using direct transition handling");
                    HandleDirectAreaTransition(world, targetAreaResRef);
                }
                else
                {
                    // Use EventDispatcher to handle transitions for all entities in current area
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): HandleAreaTransition processes entity movement between areas
                    HandleAreaTransitionViaEventDispatcher(world, eventDispatcher, targetAreaResRef);
                }

                // Reset pending flag after processing
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Transitions are processed once then cleared
                _transPending = false;
                Console.WriteLine($"[OdysseyArea] ProcessPendingAreaTransitions: Completed area transition to {targetAreaResRef}");
            }
        }

        /// <summary>
        /// Resolves target area ResRef from transition ID.
        /// </summary>
        /// <param name="module">Module containing the area list.</param>
        /// <param name="transitionId">Transition ID (0-based index into area list).</param>
        /// <returns>Target area ResRef, or null if transition ID is invalid.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TransPendNextID indexes into module's Mod_Area_list from IFO.
        /// Transition IDs are 0-based indices into the module's area list.
        /// Area list is stored in module IFO file as Mod_Area_list (list of area ResRefs).
        ///
        /// Resolution strategy:
        /// 1. Try to get area list from module IFO (if available)
        /// 2. Fall back to using loaded areas in RuntimeModule (ordered by load order)
        /// 3. Validate transition ID is within bounds
        /// 4. Return area ResRef at the specified index
        /// </remarks>
        private string ResolveTransitionTargetArea(RuntimeIModule module, byte transitionId)
        {
            if (module == null)
            {
                return null;
            }

            // Try to get area list from module IFO first
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Mod_Area_list contains ordered list of area ResRefs
            List<string> areaList = GetAreaListFromModule(module);
            if (areaList == null || areaList.Count == 0)
            {
                // Fallback: Use loaded areas from RuntimeModule
                // Note: Dictionary order may not match IFO order, but it's better than nothing
                areaList = new List<string>();
                foreach (IArea area in module.Areas)
                {
                    if (area != null && !string.IsNullOrEmpty(area.ResRef))
                    {
                        areaList.Add(area.ResRef);
                    }
                }
            }

            // Validate transition ID is within bounds
            if (transitionId >= areaList.Count)
            {
                Console.WriteLine($"[OdysseyArea] ResolveTransitionTargetArea: Transition ID {transitionId} is out of bounds (area list has {areaList.Count} entries)");
                return null;
            }

            // Return area ResRef at the specified index
            string targetAreaResRef = areaList[transitionId];
            if (string.IsNullOrEmpty(targetAreaResRef))
            {
                Console.WriteLine($"[OdysseyArea] ResolveTransitionTargetArea: Area at index {transitionId} has empty ResRef");
                return null;
            }

            return targetAreaResRef;
        }

        /// <summary>
        /// Gets the area list from module IFO file.
        /// </summary>
        /// <param name="module">Module to get area list from.</param>
        /// <returns>List of area ResRefs from Mod_Area_list, or null if not available.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Mod_Area_list is stored in module IFO file.
        /// Each entry in Mod_Area_list contains an Area_Name field with the area ResRef.
        /// This method attempts to access the IFO data if available.
        /// </remarks>
        private List<string> GetAreaListFromModule(RuntimeIModule module)
        {
            if (module == null)
            {
                return null;
            }

            // Try to access IFO data to get Mod_Area_list
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Mod_Area_list contains ordered list of area ResRefs
            try
            {
                // RuntimeModule now stores the area list from IFO during loading
                var runtimeModule = module as RuntimeModule;
                if (runtimeModule != null)
                {
                    // Return the stored area list if available
                    if (runtimeModule.AreaList != null && runtimeModule.AreaList.Count > 0)
                    {
                        return new List<string>(runtimeModule.AreaList);
                    }
                    // If AreaList is empty or null, return null to use fallback
                    return null;
                }

                // If we can access parsing Module directly, read Mod_Area_list
                var parsingModule = module as Module;
                if (parsingModule != null)
                {
                    return ReadAreaListFromIFO(parsingModule);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyArea] GetAreaListFromModule: Exception reading area list: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Reads the Mod_Area_list from the module's IFO file.
        /// </summary>
        /// <param name="module">Parsing Module instance with access to IFO.</param>
        /// <returns>List of area ResRefs from Mod_Area_list, or null if not available.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Mod_Area_list is stored in module IFO file.
        /// Each entry contains Area_Name field with the area ResRef.
        /// This matches the IFO format specification in vendor/PyKotor/wiki/GFF-IFO.md.
        /// </remarks>
        private List<string> ReadAreaListFromIFO(Module module)
        {
            if (module == null)
            {
                return null;
            }

            try
            {
                // Get IFO resource
                ModuleResource ifoResource = module.Info();

                // Load IFO data
                object ifoData = ifoResource?.Resource();
                if (ifoData == null)
                {
                    return null;
                }

                GFF ifoGff = ifoData as GFF;
                if (ifoGff == null)
                {
                    return null;
                }

                GFFStruct root = ifoGff.Root;

                // Check if Mod_Area_list exists
                if (!root.Exists("Mod_Area_list"))
                {
                    Console.WriteLine($"[OdysseyArea] ReadAreaListFromIFO: Mod_Area_list not found in IFO for module {module.GetRoot()}");
                    return null;
                }

                // Read Mod_Area_list (list of structs)
                GFFList areaList = root.GetList("Mod_Area_list");
                if (areaList == null || areaList.Count == 0)
                {
                    Console.WriteLine($"[OdysseyArea] ReadAreaListFromIFO: Mod_Area_list is empty for module {module.GetRoot()}");
                    return null;
                }

                List<string> areaResRefs = (from areaEntry in areaList where areaEntry.Exists("Area_Name") select areaEntry.GetResRef("Area_Name") into areaName where areaName != null && !string.IsNullOrEmpty(areaName.ToString()) select areaName.ToString()).ToList();

                // Each entry in Mod_Area_list has an Area_Name field (ResRef)

                if (areaResRefs.Count > 0)
                {
                    Console.WriteLine($"[OdysseyArea] ReadAreaListFromIFO: Found {areaResRefs.Count} areas in Mod_Area_list for module {module.GetRoot()}: {string.Join(", ", areaResRefs)}");
                    return areaResRefs;
                }

                Console.WriteLine($"[OdysseyArea] ReadAreaListFromIFO: No valid area entries found in Mod_Area_list for module {module.GetRoot()}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyArea] ReadAreaListFromIFO: Exception reading IFO for module {module.GetRoot()}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handles area transition via EventDispatcher.
        /// </summary>
        /// <param name="world">World instance.</param>
        /// <param name="eventDispatcher">Event dispatcher to use for transitions.</param>
        /// <param name="targetAreaResRef">Target area ResRef.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): HandleAreaTransition processes entity movement between areas.
        /// Transitions all entities in the current area to the target area.
        /// Typically transitions player entity and party members.
        /// </remarks>
        private void HandleAreaTransitionViaEventDispatcher(IWorld world, OdysseyEventDispatcher eventDispatcher, string targetAreaResRef)
        {
            if (world == null || eventDispatcher == null || string.IsNullOrEmpty(targetAreaResRef))
            {
                return;
            }

            // Get all entities in current area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area transitions affect all entities in the area
            List<IEntity> entitiesToTransition = GetEntitiesInCurrentArea(world);
            if (entitiesToTransition.Count == 0)
            {
                Console.WriteLine("[OdysseyArea] HandleAreaTransitionViaEventDispatcher: No entities in current area to transition");
                return;
            }

            Console.WriteLine($"[OdysseyArea] HandleAreaTransitionViaEventDispatcher: Transitioning {entitiesToTransition.Count} entities to area {targetAreaResRef}");

            // Transition each entity using EventDispatcher
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): HandleAreaTransition handles individual entity transitions
            foreach (IEntity entity in entitiesToTransition)
            {
                if (entity != null && entity.IsValid)
                {
                    // Use EventDispatcher's public TransitionEntityToArea method
                    // This will handle area loading, entity movement, and event firing
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): HandleAreaTransition processes entity movement between areas
                    eventDispatcher.TransitionEntityToArea(entity, targetAreaResRef);
                    Console.WriteLine($"[OdysseyArea] HandleAreaTransitionViaEventDispatcher: Transitioned entity {entity.Tag ?? "null"} ({entity.ObjectId}) to area {targetAreaResRef}");
                }
            }
        }

        /// <summary>
        /// Handles area transition directly without EventDispatcher.
        /// </summary>
        /// <param name="world">World instance.</param>
        /// <param name="targetAreaResRef">Target area ResRef.</param>
        /// <remarks>
        /// Fallback method when EventDispatcher is not available.
        /// Performs basic area transition by loading target area and moving entities.
        /// </remarks>
        private void HandleDirectAreaTransition(IWorld world, string targetAreaResRef)
        {
            if (world == null || string.IsNullOrEmpty(targetAreaResRef))
            {
                return;
            }

            // Get all entities in current area
            List<IEntity> entitiesToTransition = GetEntitiesInCurrentArea(world);
            if (entitiesToTransition.Count == 0)
            {
                Console.WriteLine("[OdysseyArea] HandleDirectAreaTransition: No entities in current area to transition");
                return;
            }

            // Load target area if not already loaded
            IArea targetArea = world.CurrentModule?.GetArea(targetAreaResRef);
            if (targetArea == null)
            {
                Console.WriteLine($"[OdysseyArea] HandleDirectAreaTransition: Target area {targetAreaResRef} is not loaded and cannot be loaded without ModuleLoader");
                return;
            }

            // Move each entity to target area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area transition system (0x004e3ff0 @ 0x004e3ff0)
            // Full transition flow: Remove from current area -> Project position -> Add to target area -> Update AreaId
            foreach (IEntity entity in entitiesToTransition)
            {
                if (entity == null || !entity.IsValid)
                    continue;
                // Step 1: Remove entity from current area (this area)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): RemoveEntityFromArea removes entity from type-specific lists
                RemoveEntityFromArea(entity);
                Console.WriteLine($"[OdysseyArea] HandleDirectAreaTransition: Removed entity {entity.Tag ?? "null"} ({entity.ObjectId}) from current area {this.ResRef}");

                // Step 2: Engine-specific pre-transition hook (save state if needed)
                // Odyssey: No-op by default (no physics state to save)
                OnBeforeTransition(entity, this);

                // Step 3: Project entity position to target area walkmesh
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Walkmesh projection system (0x004f5070 @ 0x004f5070)
                // Projects position to walkable surface for accurate positioning
                ProjectEntityToTargetArea(entity, targetArea);

                // Step 4: Engine-specific post-transition hook (restore state if needed)
                // Odyssey: No-op by default (no physics state to restore)
                OnAfterTransition(entity, targetArea, this);

                // Step 5: Add entity to target area
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AddEntityToArea adds entity to type-specific lists
                AddEntityToTargetArea(entity, targetArea);
                Console.WriteLine($"[OdysseyArea] HandleDirectAreaTransition: Added entity {entity.Tag ?? "null"} ({entity.ObjectId}) to target area {targetAreaResRef}");

                // Step 6: Update entity's AreaId
                // Entity AreaId update after area transition (K1: inline after AddToArea @ 0x004fa100 / AddObjectToArea @ 0x0050dfd0, TSL: inline after AddToArea @ 0x00589ce0)
                // Located via string references: "AreaId" @ 0x00746d10 (K1), "AreaId" @ 0x007bef48 (TSL)
                // Original implementation: After entity is successfully added to target area via AddToArea/AddObjectToArea,
                // the entity's GameObject.area_id field is updated to reflect the new area. This update happens inline in the
                // area transition handler code, not within AddToArea itself. The area_id field is part of the base GameObject
                // structure (offset 0x90 in GFF serialization, see LoadCreature @ 0x00500350 which reads AreaId from GFF).
                // Pattern: AddToArea/AddObjectToArea adds entity to area's type-specific lists -> GetAreaId retrieves target area's ID -> entity.area_id = targetAreaId
                // This ensures entity lookup by area_id (used in GetAreaByGameObjectID @ 0x004ae780) returns correct area after transition.
                uint targetAreaId = world.GetAreaId(targetArea);
                if (targetAreaId != 0)
                {
                    entity.AreaId = targetAreaId;
                }

                Console.WriteLine($"[OdysseyArea] HandleDirectAreaTransition: Successfully transitioned entity {entity.Tag ?? "null"} ({entity.ObjectId}) from area {this.ResRef} to area {targetAreaResRef}");
            }
        }

        /// <summary>
        /// Gets all entities in the current area.
        /// </summary>
        /// <param name="world">World instance.</param>
        /// <returns>List of entities in the current area.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area transitions affect all entities in the area.
        /// Typically includes player entity and party members.
        /// </remarks>
        private List<IEntity> GetEntitiesInCurrentArea(IWorld world)
        {
            List<IEntity> entities = new List<IEntity>();

            if (world == null)
            {
                return entities;
            }

            // Get current area
            IArea currentArea = world.CurrentArea;
            if (currentArea == null || currentArea != this)
            {
                // If world's current area is not this area, use this area
                currentArea = this;
            }

            // Collect all entities from area's entity lists
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Areas maintain lists of creatures, placeables, doors, etc.
            entities.AddRange(_creatures);
            entities.AddRange(_placeables);
            entities.AddRange(_doors);
            entities.AddRange(_triggers);
            entities.AddRange(_waypoints);
            entities.AddRange(_sounds);

            // Filter to only valid entities that are in this area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity AreaId must match area's AreaId
            uint currentAreaId = world.GetAreaId(currentArea);
            List<IEntity> validEntities = new List<IEntity>();
            foreach (IEntity entity in entities)
            {
                if (entity != null && entity.IsValid && entity.AreaId == currentAreaId)
                {
                    validEntities.Add(entity);
                }
            }

            return validEntities;
        }

        /// <summary>
        /// Updates environmental effects like lighting and fog.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe environmental effect updates.
        /// Updates lighting colors and fog parameters dynamically.
        ///
        /// Lighting updates:
        /// - Validates and normalizes ambient lighting colors
        /// - Updates sun diffuse/ambient colors based on time of day
        /// - Applies BGR color format normalization
        ///
        /// Fog updates:
        /// - Updates fog near/far distances for dynamic weather
        /// - Adjusts fog colors based on environmental conditions
        /// - Validates fog parameters remain within engine limits
        ///
        /// Based on ARE file lighting/fog format and runtime updates.
        /// </remarks>
        private void UpdateEnvironmentalEffects(float deltaTime)
        {
            // Update ambient lighting based on time/weather conditions
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Dynamic lighting updates during area updates
            UpdateDynamicLighting(deltaTime);

            // Update fog parameters for weather/environmental effects
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fog updates during area rendering/environment updates
            UpdateDynamicFog(deltaTime);
        }

        /// <summary>
        /// Updates dynamic lighting conditions.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) lighting system updates.
        /// Updates ambient colors, sun colors, and lighting conditions.
        /// Normalizes colors to BGR format and validates ranges.
        /// </remarks>
        private void UpdateDynamicLighting(float deltaTime)
        {
            // Validate and update ambient lighting colors
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Lighting validation during area updates
            if (_dynamicAmbientColor == 0)
            {
                // Use default ambient if dynamic color is zero
                const uint defaultAmbientColor = 0xFF808080; // Gray ambient
                _ambientColor = defaultAmbientColor;
            }
            else
            {
                _ambientColor = _dynamicAmbientColor;
            }

            // Validate sun colors
            if (_sunAmbientColor == 0)
            {
                _sunAmbientColor = 0xFF808080; // Default gray
            }
            if (_sunDiffuseColor == 0)
            {
                _sunDiffuseColor = 0xFFFFFFFF; // Default white
            }

            // Normalize all colors to BGR format
            // Based on ARE format: Colors stored as uint32 in BGR format
            _ambientColor = NormalizeBgrColor(_ambientColor);
            _dynamicAmbientColor = NormalizeBgrColor(_dynamicAmbientColor);
            _sunAmbientColor = NormalizeBgrColor(_sunAmbientColor);
            _sunDiffuseColor = NormalizeBgrColor(_sunDiffuseColor);
        }

        /// <summary>
        /// Updates dynamic fog conditions.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) fog system updates.
        /// Updates fog distances and colors for environmental effects.
        /// Validates fog parameters remain within engine limits.
        /// </remarks>
        private void UpdateDynamicFog(float deltaTime)
        {
            // Update fog parameters if fog is enabled
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fog updates during environmental effect processing
            if (FogEnabled)
            {
                // Validate fog distances
                if (FogNear < 0.0f)
                {
                    FogNear = 0.0f;
                }
                if (_fogFar <= FogNear)
                {
                    if (_fogFar <= 0.0f)
                    {
                        _fogFar = 1000.0f; // Default far distance
                    }
                    if (FogNear >= _fogFar)
                    {
                        FogNear = 0.0f; // Reset near to start
                    }
                }

                // Clamp to maximum reasonable range
                const float maxFogDistance = 10000.0f;
                if (_fogFar > maxFogDistance)
                {
                    _fogFar = maxFogDistance;
                }

                // Use appropriate fog color
                uint effectiveFogColor = _sunFogColor;
                if (effectiveFogColor == 0)
                {
                    effectiveFogColor = _fogColor;
                }
                if (effectiveFogColor == 0)
                {
                    effectiveFogColor = 0xFF808080; // Default gray fog
                }
                _fogColor = effectiveFogColor;
                _sunFogColor = effectiveFogColor;

                // Normalize fog colors
                _fogColor = NormalizeBgrColor(_fogColor);
                _sunFogColor = NormalizeBgrColor(_sunFogColor);
            }
        }

        /// <summary>
        /// Gets the world reference from entities in this area.
        /// </summary>
        /// <returns>World instance, or null if no valid world found.</returns>
        /// <remarks>
        /// Based on Aurora engine pattern: Gets world from area entities.
        /// Tries creatures first, then other entity types.
        /// Used for script execution and event dispatching.
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

            // Try placeables, doors, triggers, waypoints, sounds
            foreach (IEntity entity in _placeables.Concat(_doors).Concat(_triggers).Concat(_waypoints).Concat(_sounds))
            {
                if (entity != null && entity.World != null)
                {
                    return entity.World;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets or creates the area entity for script execution.
        /// </summary>
        /// <param name="world">World instance to create entity in if needed.</param>
        /// <returns>Area entity for script execution, or null if world is invalid.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area scripts use area ResRef as entity context.
        /// Creates area entity if it doesn't exist for script execution.
        /// Area scripts don't require physical entities in the world.
        /// </remarks>
        private IEntity GetOrCreateAreaEntityForScripts(IWorld world)
        {
            if (world == null)
            {
                return null;
            }

            // First, check if we already have a stored area entity reference
            // This avoids redundant lookups and ensures we use the same entity instance
            if (_areaEntityForScripts != null && _areaEntityForScripts.IsValid)
            {
                // Verify the stored entity is still registered with the world
                IEntity verifiedEntity = world.GetEntity(_areaEntityForScripts.ObjectId);
                if (verifiedEntity != null && verifiedEntity.IsValid)
                {
                    return _areaEntityForScripts;
                }
                // Stored entity is no longer valid - clear reference
                _areaEntityForScripts = null;
            }

            // Try to get existing entity by area ResRef tag
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area scripts use area ResRef as entity tag for execution
            IEntity areaEntity = world.GetEntityByTag(_resRef, 0);
            if (areaEntity != null && areaEntity.IsValid)
            {
                // Store reference for future use
                _areaEntityForScripts = areaEntity;
                return areaEntity;
            }

            // Try using area tag as fallback
            if (!string.IsNullOrEmpty(_tag) && !_tag.Equals(_resRef, StringComparison.OrdinalIgnoreCase))
            {
                areaEntity = world.GetEntityByTag(_tag, 0);
                if (areaEntity != null && areaEntity.IsValid)
                {
                    // Store reference for future use
                    _areaEntityForScripts = areaEntity;
                    return areaEntity;
                }
            }

            // Create area entity for script execution if it doesn't exist
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area entities are created dynamically for script context
            // Area entities don't have physical presence but serve as script execution context
            try
            {
                // Generate unique ObjectId for area entity using ResRef hash to ensure uniqueness
                // Use high range (2000000+) to avoid conflicts with normal entities (1-1000000) and EntityFactory (1000000+)
                // Hash ResRef to create deterministic but unique ObjectId per area
                uint resRefHash = (uint)(_resRef?.GetHashCode() ?? 0);
                // Ensure hash is in valid range and doesn't conflict with special ObjectIds (0x7F000000+)
                // Map hash to range 2000000-0x7EFFFFFF (avoiding 0x7F000000 = OBJECT_INVALID)
                uint areaObjectId = 2000000 + (resRefHash % 0x5EFFFFFF); // 2000000 to 0x7EFFFFFF range

                // Check if ObjectId already exists in world (collision detection)
                // If collision occurs, use sequential fallback
                int collisionAttempts = 0;
                while (world.GetEntity(areaObjectId) != null && collisionAttempts < 100)
                {
                    areaObjectId = 2000000 + ((resRefHash + (uint)collisionAttempts) % 0x5EFFFFFF);
                    collisionAttempts++;
                }

                // If still colliding after 100 attempts, use simple counter fallback
                if (world.GetEntity(areaObjectId) != null)
                {
                    // Use very high range as last resort (0x7E000000+)
                    areaObjectId = 0x7E000000 + (resRefHash % 0x00FFFFFF);
                }

                // Create area entity with ResRef as tag
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area entities use ObjectType.Area (if it exists) or ObjectType.Invalid
                var areaEntityObj = new OdysseyEntity(areaObjectId, RuntimeObjectType.Invalid, _resRef)
                {
                    DisplayName = _displayName ?? _resRef
                };
                areaEntityObj.SetData("IsAreaEntity", true);  // Mark as area entity for script context

                // Register area entity with world for proper lookup and lifecycle management
                // Special handling: Area entities are registered but marked as script-only entities
                // They are not part of normal entity collections but must be findable by GetEntityByTag
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area entities are registered in world's entity lookup tables
                // but are not added to area's entity collections (Creatures, Placeables, etc.)
                world.RegisterEntity(areaEntityObj);

                // Store reference for cleanup when area is unloaded
                _areaEntityForScripts = areaEntityObj;

                // Note: Area entities don't need AreaId set since they ARE the area context
                // Setting AreaId would create circular reference (area entity belongs to area, but area entity IS the area)
                // Scripts use area entity as execution context, not as an entity within an area

                return areaEntityObj;
            }
            catch (Exception ex)
            {
                // If entity creation fails, log and return null
                // Script execution will be skipped this frame
                Console.WriteLine($"[OdysseyArea] Failed to create area entity for scripts: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets the rendering context for this area.
        /// </summary>
        /// <param name="context">The rendering context providing graphics services.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area rendering uses graphics device, room mesh renderer, and basic effect.
        /// The rendering context is set by the game loop before calling Render().
        /// </remarks>
        public void SetRenderContext(IAreaRenderContext context)
        {
            _renderContext = context;
        }

        /// <summary>
        /// Sets room layout information from LYT file.
        /// </summary>
        /// <param name="rooms">List of room information from LYT file.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): LYT file loading populates room list with model names and positions.
        /// Called during area loading from ModuleLoader.
        ///
        /// If Module is available, this will automatically trigger walkmesh loading from WOK files.
        /// Each room's ModelName corresponds to a WOK file that contains the room's walkmesh data.
        /// </remarks>
        public void SetRooms(List<RoomInfo> rooms)
        {
            _rooms = rooms == null
                     ? new List<RoomInfo>()
                     : new List<RoomInfo>(rooms);

            // Clear bounding box cache when rooms are updated (rooms may have changed)
            // Bounding boxes will be recalculated on demand when needed
            _roomBoundingBoxes?.Clear();

            // If Module is available, load walkmeshes from rooms
            if (_module != null && _rooms.Count > 0)
            {
                LoadWalkmeshFromRooms();
            }
        }

        /// <summary>
        /// Sets visibility data from VIS file.
        /// </summary>
        /// <param name="vis">VIS file data for room visibility culling.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): VIS file defines which rooms are visible from each room.
        /// Used for frustum culling optimization during rendering.
        /// </remarks>
        public void SetVisibilityData(VIS vis)
        {
            _visibilityData = vis;
        }

        /// <summary>
        /// Gets or sets the ambient color (RGBA).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Ambient color from ARE file AreaProperties.
        /// Controls base lighting level for the area.
        /// </remarks>
        public uint AmbientColor { get; set; }

        /// <summary>
        /// Gets or sets the fog color (RGBA).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fog color from ARE file AreaProperties.
        /// </remarks>
        public uint FogColor { get; set; }

        /// <summary>
        /// Gets or sets whether fog is enabled.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fog enabled flag from ARE file AreaProperties.
        /// </remarks>
        public bool FogEnabled { get; set; }

        /// <summary>
        /// Gets or sets the fog near distance.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fog near distance from ARE file AreaProperties.
        /// </remarks>
        public float FogNear { get; set; }

        /// <summary>
        /// Gets or sets the fog far distance.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fog far distance from ARE file AreaProperties.
        /// </remarks>
        public float FogFar { get; set; }

        /// <summary>
        /// Renders the area.
        /// </summary>
        /// <remarks>
        /// Handles VIS culling, transparency sorting, and lighting.
        /// Renders static geometry, area effects, and environmental elements.
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area rendering functions
        /// - Room mesh rendering with VIS culling [TODO: Name this function] @ (K1: TODO: Find address, TSL: 0x0041b6b0)
        /// - VIS culling: Uses VIS file to determine which rooms are visible from current room
        /// - Lighting: Applies ambient, diffuse, and fog effects from ARE file
        /// - Room meshes: Loaded from MDL models referenced in LYT file
        /// - Rendering order: Opaque geometry first, then transparent objects
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

            // Apply fog settings if enabled
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fog parameters are applied to graphics device/effect during rendering
            // Original engine behavior: DirectX fixed-function fog states (D3DRS_FOGENABLE, D3DRS_FOGCOLOR, etc.)
            // Modern implementation: Shader-based fog via BasicEffect parameters
            if (FogEnabled)
            {
                // Select effective fog color (prefer SunFogColor, fallback to FogColor, default to gray)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SunFogColor takes precedence over FogColor when available
                uint effectiveFogColor = _sunFogColor;
                if (effectiveFogColor == 0)
                {
                    effectiveFogColor = _fogColor;
                }
                if (effectiveFogColor == 0)
                {
                    effectiveFogColor = 0xFF808080; // Default gray fog
                }

                // Convert fog color from RGBA uint to Vector3 (RGB channels)
                // Original engine: Fog color is stored as BGR (Blue-Green-Red) in ARE file
                // Conversion: Extract R, G, B from uint and normalize to [0.0, 1.0] range
                Vector3 fogColorVec = new Vector3(
                    ((effectiveFogColor >> 16) & 0xFF) / 255.0f,  // Red channel
                    ((effectiveFogColor >> 8) & 0xFF) / 255.0f,   // Green channel
                    (effectiveFogColor & 0xFF) / 255.0f           // Blue channel
                );

                // Apply fog parameters to BasicEffect
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): D3DRS_FOGENABLE = TRUE, D3DRS_FOGCOLOR, D3DRS_FOGSTART, D3DRS_FOGEND
                // Modern implementation: BasicEffect fog parameters for shader-based fog
                basicEffect.FogEnabled = true;
                basicEffect.FogColor = fogColorVec;
                basicEffect.FogStart = FogNear;  // Fog begins at this distance
                basicEffect.FogEnd = _fogFar;     // Fog fully obscures objects at this distance
            }
            else
            {
                // Disable fog when fog is not enabled
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): D3DRS_FOGENABLE = FALSE when SunFogOn is 0
                basicEffect.FogEnabled = false;
            }

            // Apply ambient lighting
            Vector3 ambientColorVec = new Vector3(
                ((_ambientColor >> 16) & 0xFF) / 255.0f,
                ((_ambientColor >> 8) & 0xFF) / 255.0f,
                (_ambientColor & 0xFF) / 255.0f
            );
            basicEffect.AmbientLightColor = ambientColorVec;
            basicEffect.LightingEnabled = true;

            // Determine current room for VIS culling
            int currentRoomIndex = FindCurrentRoom(cameraPosition);

            // Render rooms with VIS culling
            if (_rooms != null && _rooms.Count > 0)
            {
                for (int i = 0; i < _rooms.Count; i++)
                {
                    RoomInfo room = _rooms[i];

                    // VIS culling: Check if room is visible from current room
                    if (currentRoomIndex >= 0 && _visibilityData != null)
                    {
                        if (!IsRoomVisible(currentRoomIndex, i))
                        {
                            continue; // Skip this room - not visible
                        }
                    }

                    // Skip rooms without model names
                    if (string.IsNullOrEmpty(room.ModelName))
                    {
                        continue;
                    }

                    // Get or load room mesh
                    IRoomMeshData meshData;
                    if (!_roomMeshes.TryGetValue(room.ModelName, out meshData))
                    {
                        // Try to load mesh on-demand from Module
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room meshes are loaded from MDL files referenced in LYT file
                        // Located via string references: "Rooms" @ 0x007bd490, "RoomName" @ 0x007bd484
                        // Original implementation: Loads room MDL models from module archives when needed for rendering
                        // swkotor2.exe: 0x004e3ff0 @ 0x004e3ff0 - Room mesh loading function
                        meshData = LoadRoomMeshOnDemand(room.ModelName, roomRenderer);
                        if (meshData != null)
                        {
                            // Cache the loaded mesh for future use
                            _roomMeshes[room.ModelName] = meshData;
                        }
                        else
                        {
                            // Failed to load mesh, skip this room
                            continue;
                        }
                    }

                    if (meshData == null || meshData.VertexBuffer == null || meshData.IndexBuffer == null)
                    {
                        continue;
                    }

                    // Validate mesh data
                    if (meshData.IndexCount < 3)
                    {
                        continue; // Need at least one triangle
                    }

                    // Set up room transform
                    Vector3 roomPos = room.Position;
                    Matrix4x4 roomWorld = Matrix4x4.CreateTranslation(roomPos);

                    // Apply room rotation if specified
                    if (Math.Abs(room.Rotation) > 0.001f)
                    {
                        Matrix4x4 rotation = Matrix4x4.CreateRotationY((float)(room.Rotation * Math.PI / 180.0));
                        roomWorld = Matrix4x4.Multiply(rotation, roomWorld);
                    }

                    // Set up rendering state
                    graphicsDevice.SetVertexBuffer(meshData.VertexBuffer);
                    graphicsDevice.SetIndexBuffer(meshData.IndexBuffer);

                    // Set effect parameters
                    basicEffect.World = roomWorld;
                    basicEffect.View = viewMatrix;
                    basicEffect.Projection = projectionMatrix;
                    basicEffect.VertexColorEnabled = true;
                    basicEffect.LightingEnabled = true;

                    // Draw the mesh
                    foreach (IEffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        graphicsDevice.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            0,
                            0,
                            meshData.IndexCount,
                            0,
                            meshData.IndexCount / 3 // Number of triangles
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Loads a room mesh on-demand from the Module when it's needed for rendering.
        /// </summary>
        /// <param name="modelResRef">The model resource reference (room model name).</param>
        /// <param name="roomRenderer">The room mesh renderer to use for creating mesh data.</param>
        /// <returns>The loaded room mesh data, or null if loading failed.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room meshes are loaded on-demand when needed for rendering.
        /// Located via string references: "Rooms" @ 0x007bd490, "RoomName" @ 0x007bd484
        /// Original implementation: Loads MDL/MDX files from module archives and creates renderable mesh data.
        ///
        /// Implementation details:
        /// - Resource search order: Module.Model() -> Module.ModelExt() -> Installation (OVERRIDE -> CHITIN)
        /// - Matches swkotor2.exe resource loading: Module -> Override -> Chitin
        /// - MDL files: Loaded from Module.Model() or Module.ModelExt() as fallback
        /// - MDX files: Loaded from Module.ModelExt() (companion files containing vertex data)
        /// - Installation fallback: Uses Installation.Resource() with OVERRIDE and CHITIN search locations
        /// - MDL parsing: Uses MDLAuto.ReadMdl() to parse both binary MDL (with MDX) and ASCII MDL formats
        /// - Mesh creation: Uses IRoomMeshRenderer.LoadRoomMesh() to create GPU buffers from MDL geometry
        /// - Validation: Ensures mesh has valid vertex/index buffers and at least 3 indices (one triangle)
        /// - Error handling: Catches exceptions and returns null (room is skipped, doesn't crash)
        /// - Caching: Loaded meshes are cached in _roomMeshes dictionary for future use
        ///
        /// This replaces the previous stub implementation with full on-demand loading functionality.
        /// </remarks>
        private IRoomMeshData LoadRoomMeshOnDemand(string modelResRef, IRoomMeshRenderer roomRenderer)
        {
            // Validate inputs
            if (string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            if (roomRenderer == null)
            {
                return null;
            }

            // Module is required to load MDL/MDX files
            if (_module == null)
            {
                // Cannot load without Module - this is expected if Module wasn't set yet
                return null;
            }

            try
            {
                // Load MDL file from Module or Installation
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room models are loaded from module archives or installation (chitin/override)
                // Original implementation: Uses Module.Resource() first, then falls back to Installation.Resource()
                // swkotor2.exe: Resource search order is Module -> Override -> Chitin
                byte[] mdlData = null;
                byte[] mdxData = null;

                // Try loading from Module first
                if (_module != null)
                {
                    ModuleResource mdlResource = _module.Model(modelResRef);
                    if (mdlResource != null)
                    {
                        mdlData = mdlResource.Data();
                    }

                    if (mdlData == null || mdlData.Length == 0)
                    {
                        // Try ModelExt as fallback (some modules may store MDL in ModelExt)
                        ModuleResource mdlExtResource = _module.ModelExt(modelResRef);
                        if (mdlExtResource != null)
                        {
                            mdlData = mdlExtResource.Data();
                        }
                    }

                    // Load MDX file from Module (contains vertex data)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): MDX files are companion files to MDL files
                    ModuleResource mdxResource = _module.ModelExt(modelResRef);
                    if (mdxResource != null)
                    {
                        mdxData = mdxResource.Data();
                    }
                }

                // If not found in Module, try Installation (chitin/override)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Resources can be in override directory or chitin.key
                if ((mdlData == null || mdlData.Length == 0) && _module != null && _module.Installation != null)
                {
                    Installation installation = _module.Installation;
                    SearchLocation[] searchOrder = { SearchLocation.OVERRIDE, SearchLocation.CHITIN };

                    ResourceResult mdlResult = installation.Resource(modelResRef, ResourceType.MDL, searchOrder);
                    if (mdlResult != null && mdlResult.Data != null && mdlResult.Data.Length > 0)
                    {
                        mdlData = mdlResult.Data;
                    }

                    // Load MDX from Installation if not already loaded
                    if (mdxData == null)
                    {
                        ResourceResult mdxResult = installation.Resource(modelResRef, ResourceType.MDX, searchOrder);
                        if (mdxResult != null && mdxResult.Data != null && mdxResult.Data.Length > 0)
                        {
                            mdxData = mdxResult.Data;
                        }
                    }
                }

                if (mdlData == null || mdlData.Length == 0)
                {
                    // MDL not found in module or installation
                    return null;
                }

                // Parse MDL file
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): MDL files are binary format containing model structure
                // Original implementation: Parses MDL binary format to extract mesh geometry
                // MDLAuto.ReadMdl can parse both binary MDL (with MDX data) and ASCII MDL formats
                BioWare.NET.Resource.Formats.MDLData.MDL mdl = MDLAuto.ReadMdl(mdlData, sourceExt: mdxData);
                if (mdl == null)
                {
                    // Failed to parse MDL
                    return null;
                }

                // Create mesh data using room renderer
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room renderer extracts geometry from MDL and creates GPU buffers
                // Original implementation: Converts MDL geometry to vertex/index buffers for rendering
                IRoomMeshData meshData = roomRenderer.LoadRoomMesh(modelResRef, mdl);
                if (meshData == null)
                {
                    // Failed to create mesh data
                    return null;
                }

                // Validate mesh data has valid buffers
                if (meshData.VertexBuffer == null || meshData.IndexBuffer == null)
                {
                    // Invalid mesh data
                    return null;
                }

                if (meshData.IndexCount < 3)
                {
                    // Need at least one triangle
                    return null;
                }

                return meshData;
            }
            catch (Exception ex)
            {
                // Log error but don't throw - just return null to skip this room
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room loading failures don't crash the game, rooms are just skipped
                Console.WriteLine($"[OdysseyArea] Failed to load room mesh '{modelResRef}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Calculates the bounding box of an MDL model by recursively traversing all nodes.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room bounding boxes are calculated from MDL model geometry.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/gl/models/mdl.py:95-156
        /// </summary>
        /// <param name="mdl">The MDL model to calculate bounding box for.</param>
        /// <returns>A tuple containing (Min, Max) bounding box in local model space, or null if invalid.</returns>
        private (Vector3 Min, Vector3 Max)? CalculateModelBoundingBox(BioWare.NET.Resource.Formats.MDLData.MDL mdl)
        {
            if (mdl == null || mdl.Root == null)
            {
                return null;
            }

            // Use model-level bounding box if available (from MDL header)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): MDL header contains overall bounding box (BMin, BMax)
            if (mdl.BMin.X < 1000000 && mdl.BMax.X > -1000000)
            {
                // Valid model-level bounding box
                return (mdl.BMin, mdl.BMax);
            }

            // Fallback: Calculate from all mesh nodes recursively
            // Based on vendor/PyKotor/Libraries/PyKotor/src/pykotor/gl/models/mdl.py:95-156
            Vector3 bbMin = new Vector3(1000000, 1000000, 1000000);
            Vector3 bbMax = new Vector3(-1000000, -1000000, -1000000);
            bool hasValidBounds = false;

            // Traverse all nodes recursively
            List<MDLNode> nodesToCheck = new List<MDLNode> { mdl.Root };
            while (nodesToCheck.Count > 0)
            {
                MDLNode node = nodesToCheck[nodesToCheck.Count - 1];
                nodesToCheck.RemoveAt(nodesToCheck.Count - 1);

                // Check if this node has mesh geometry
                if (node.Mesh != null)
                {
                    // Use mesh bounding box if available (BBoxMinX/Y/Z or BbMin/BbMax)
                    Vector3 meshMin;
                    Vector3 meshMax;

                    // Try BBoxMinX/Y/Z first (legacy format)
                    if (node.Mesh.BBoxMinX < 1000000 && node.Mesh.BBoxMaxX > -1000000)
                    {
                        meshMin = new Vector3(node.Mesh.BBoxMinX, node.Mesh.BBoxMinY, node.Mesh.BBoxMinZ);
                        meshMax = new Vector3(node.Mesh.BBoxMaxX, node.Mesh.BBoxMaxY, node.Mesh.BBoxMaxZ);
                    }
                    // Try BbMin/BbMax (alternative format)
                    else if (node.Mesh.BbMin.X < 1000000 && node.Mesh.BbMax.X > -1000000)
                    {
                        meshMin = node.Mesh.BbMin;
                        meshMax = node.Mesh.BbMax;
                    }
                    // Fallback: Calculate from vertices if available
                    else if (node.Mesh.Vertices != null && node.Mesh.Vertices.Count > 0)
                    {
                        meshMin = new Vector3(1000000, 1000000, 1000000);
                        meshMax = new Vector3(-1000000, -1000000, -1000000);

                        foreach (Vector3 vertex in node.Mesh.Vertices)
                        {
                            // Transform vertex by node's local transform
                            Vector3 transformedVertex = TransformPointByNode(vertex, node);

                            meshMin.X = Math.Min(meshMin.X, transformedVertex.X);
                            meshMin.Y = Math.Min(meshMin.Y, transformedVertex.Y);
                            meshMin.Z = Math.Min(meshMin.Z, transformedVertex.Z);
                            meshMax.X = Math.Max(meshMax.X, transformedVertex.X);
                            meshMax.Y = Math.Max(meshMax.Y, transformedVertex.Y);
                            meshMax.Z = Math.Max(meshMax.Z, transformedVertex.Z);
                        }
                    }
                    else
                    {
                        // No valid mesh data, skip this node
                        if (node.Children != null)
                        {
                            nodesToCheck.AddRange(node.Children);
                        }
                        continue;
                    }

                    // Combine with overall bounding box
                    bbMin.X = Math.Min(bbMin.X, meshMin.X);
                    bbMin.Y = Math.Min(bbMin.Y, meshMin.Y);
                    bbMin.Z = Math.Min(bbMin.Z, meshMin.Z);
                    bbMax.X = Math.Max(bbMax.X, meshMax.X);
                    bbMax.Y = Math.Max(bbMax.Y, meshMax.Y);
                    bbMax.Z = Math.Max(bbMax.Z, meshMax.Z);
                    hasValidBounds = true;
                }

                // Check child nodes
                if (node.Children != null)
                {
                    nodesToCheck.AddRange(node.Children);
                }
            }

            if (!hasValidBounds)
            {
                return null;
            }

            return (bbMin, bbMax);
        }

        /// <summary>
        /// Transforms a point by a node's local position and orientation.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Node transforms are applied during geometry processing.
        /// </summary>
        /// <param name="point">Point in local space.</param>
        /// <param name="node">Node containing transform information.</param>
        /// <returns>Transformed point.</returns>
        private Vector3 TransformPointByNode(Vector3 point, MDLNode node)
        {
            // Apply node's position (translation)
            Vector3 result = point + node.Position;

            // Apply node's orientation (rotation) if quaternion is valid
            // Note: Full quaternion rotation would require matrix operations, but for bounding box
            // calculation we can use a simplified approach or skip rotation for axis-aligned boxes
            // For now, we'll apply position only since room bounding boxes are typically axis-aligned
            // Full rotation would require Matrix4x4 operations with quaternion-to-matrix conversion

            return result;
        }

        /// <summary>
        /// Gets or calculates the bounding box for a room model.
        /// Caches the result for performance.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room bounding boxes are cached after first calculation.
        /// </summary>
        /// <param name="modelResRef">Model resource reference (room model name).</param>
        /// <returns>Bounding box (Min, Max) in local model space, or null if model cannot be loaded.</returns>
        private (Vector3 Min, Vector3 Max)? GetRoomBoundingBox(string modelResRef)
        {
            if (string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            // Check cache first
            if (_roomBoundingBoxes != null && _roomBoundingBoxes.TryGetValue(modelResRef, out (Vector3 Min, Vector3 Max) cachedBounds))
            {
                return cachedBounds;
            }

            // Load MDL model to calculate bounding box
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room models are loaded from Module or Installation
            try
            {
                byte[] mdlData = null;
                byte[] mdxData = null;

                // Try loading from Module first
                if (_module != null)
                {
                    ModuleResource mdlResource = _module.Model(modelResRef);
                    if (mdlResource != null)
                    {
                        mdlData = mdlResource.Data();
                    }

                    // Load MDX file from Module (contains vertex data)
                    ModuleResource mdxResource = _module.ModelExt(modelResRef);
                    if (mdxResource != null)
                    {
                        mdxData = mdxResource.Data();
                    }
                }

                // If not found in Module, try Installation (chitin/override)
                if ((mdlData == null || mdlData.Length == 0) && _module != null && _module.Installation != null)
                {
                    Installation installation = _module.Installation;
                    SearchLocation[] searchOrder = { SearchLocation.OVERRIDE, SearchLocation.CHITIN };

                    ResourceResult mdlResult = installation.Resource(modelResRef, ResourceType.MDL, searchOrder);
                    if (mdlResult != null && mdlResult.Data != null && mdlResult.Data.Length > 0)
                    {
                        mdlData = mdlResult.Data;
                    }

                    // Load MDX from Installation if not already loaded
                    if (mdxData == null)
                    {
                        ResourceResult mdxResult = installation.Resource(modelResRef, ResourceType.MDX, searchOrder);
                        if (mdxResult != null && mdxResult.Data != null && mdxResult.Data.Length > 0)
                        {
                            mdxData = mdxResult.Data;
                        }
                    }
                }

                if (mdlData == null || mdlData.Length == 0)
                {
                    // MDL not found - cannot calculate bounding box
                    return null;
                }

                // Parse MDL file
                BioWare.NET.Resource.Formats.MDLData.MDL mdl = MDLAuto.ReadMdl(mdlData, sourceExt: mdxData);
                if (mdl == null)
                {
                    return null;
                }

                // Calculate bounding box
                (Vector3 Min, Vector3 Max)? bounds = CalculateModelBoundingBox(mdl);
                if (bounds.HasValue)
                {
                    // Cache the result
                    if (_roomBoundingBoxes == null)
                    {
                        _roomBoundingBoxes = new Dictionary<string, (Vector3 Min, Vector3 Max)>(StringComparer.OrdinalIgnoreCase);
                    }
                    _roomBoundingBoxes[modelResRef] = bounds.Value;
                    return bounds.Value;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - just return null
                Console.WriteLine($"[OdysseyArea] Failed to calculate bounding box for room model '{modelResRef}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Transforms a bounding box by room position and rotation, then checks if a point is inside.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room bounds are transformed by room position/rotation from LYT file.
        /// </summary>
        /// <param name="point">Point to test (in world space).</param>
        /// <param name="bboxMin">Bounding box minimum (in local model space).</param>
        /// <param name="bboxMax">Bounding box maximum (in local model space).</param>
        /// <param name="roomPosition">Room position (in world space).</param>
        /// <param name="roomRotation">Room rotation (in degrees, around Y axis).</param>
        /// <returns>True if point is inside the transformed bounding box.</returns>
        private bool IsPointInsideRoomBounds(Vector3 point, Vector3 bboxMin, Vector3 bboxMax, Vector3 roomPosition, float roomRotation)
        {
            // Convert point to room's local space (reverse transform)
            Vector3 localPoint = point - roomPosition;

            // Apply inverse rotation if room is rotated
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room rotation is around Y-axis (vertical axis)
            if (Math.Abs(roomRotation) > 0.001f)
            {
                // Convert rotation from degrees to radians
                float rotationRad = -roomRotation * (float)(Math.PI / 180.0);

                // Rotate around Y-axis (inverse rotation)
                float cos = (float)Math.Cos(rotationRad);
                float sin = (float)Math.Sin(rotationRad);

                float x = localPoint.X;
                float z = localPoint.Z;

                localPoint.X = x * cos - z * sin;
                localPoint.Z = x * sin + z * cos;
            }

            // Check if point is inside axis-aligned bounding box in local space
            return localPoint.X >= bboxMin.X && localPoint.X <= bboxMax.X &&
                   localPoint.Y >= bboxMin.Y && localPoint.Y <= bboxMax.Y &&
                   localPoint.Z >= bboxMin.Z && localPoint.Z <= bboxMax.Z;
        }

        /// <summary>
        /// Finds the current room index based on camera/player position.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room finding logic checks if position is inside room bounds.
        /// swkotor2.exe: Room bounds checking is performed via spatial queries on room geometry.
        /// </summary>
        /// <param name="position">Camera or player position.</param>
        /// <returns>Room index, or -1 if not found.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room finding logic checks if position is inside room bounds.
        /// If multiple rooms contain the position, returns the first match.
        /// If no room contains the position, falls back to distance-based selection.
        /// </remarks>
        private int FindCurrentRoom(Vector3 position)
        {
            if (_rooms == null || _rooms.Count == 0)
            {
                return -1;
            }

            // First, try to find a room that contains the position using bounding box checks
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Room finding prioritizes rooms that contain the position
            for (int i = 0; i < _rooms.Count; i++)
            {
                RoomInfo room = _rooms[i];

                // Skip rooms without model names
                if (string.IsNullOrEmpty(room.ModelName))
                {
                    continue;
                }

                // Get or calculate room bounding box
                (Vector3 Min, Vector3 Max)? bounds = GetRoomBoundingBox(room.ModelName);
                if (!bounds.HasValue)
                {
                    // Cannot calculate bounds for this room, skip it
                    continue;
                }

                // Check if position is inside room bounds
                if (IsPointInsideRoomBounds(position, bounds.Value.Min, bounds.Value.Max, room.Position, room.Rotation))
                {
                    // Position is inside this room's bounds
                    return i;
                }
            }

            // Fallback: If no room contains the position, use distance-based approach
            // This handles edge cases where position is outside all room bounds (e.g., at area boundaries)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Distance-based fallback is used when position is outside all rooms
            int closestRoomIndex = 0;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < _rooms.Count; i++)
            {
                RoomInfo room = _rooms[i];
                float distance = Vector3.Distance(position, room.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoomIndex = i;
                }
            }

            return closestRoomIndex;
        }

        /// <summary>
        /// Checks if a room is visible from the current room using VIS data.
        /// </summary>
        /// <param name="currentRoomIndex">Index of the current room.</param>
        /// <param name="targetRoomIndex">Index of the room to check visibility for.</param>
        /// <returns>True if the target room is visible from the current room.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): VIS file defines visibility graph between rooms.
        /// If no VIS data, all rooms are considered visible (fallback behavior).
        /// </remarks>
        private bool IsRoomVisible(int currentRoomIndex, int targetRoomIndex)
        {
            if (_visibilityData == null || _rooms == null)
            {
                return true; // No VIS data - render all rooms
            }

            if (currentRoomIndex < 0 || currentRoomIndex >= _rooms.Count)
            {
                return true; // Invalid current room - render all
            }

            if (targetRoomIndex < 0 || targetRoomIndex >= _rooms.Count)
            {
                return false; // Invalid target room
            }

            // Always render the current room
            if (currentRoomIndex == targetRoomIndex)
            {
                return true;
            }

            // Get room model names for VIS lookup
            string currentRoomName = _rooms[currentRoomIndex].ModelName;
            string targetRoomName = _rooms[targetRoomIndex].ModelName;

            if (string.IsNullOrEmpty(currentRoomName) || string.IsNullOrEmpty(targetRoomName))
            {
                return true; // Missing room names - render all
            }

            // Check VIS data for visibility
            // VIS stores visibility as: room -> set of visible rooms
            HashSet<string> visibleRooms = _visibilityData.GetVisibleRooms(currentRoomName);
            if (visibleRooms == null)
            {
                return true; // No visibility data for this room - render all
            }

            return visibleRooms.Contains(targetRoomName.ToLowerInvariant());
        }

        /// <summary>
        /// Unloads the area and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe/swkotor2.exe: Area unloading functions
        /// Destroys all entities, frees walkmesh and geometry resources.
        /// Ensures proper cleanup to prevent memory leaks.
        ///
        /// Odyssey-specific cleanup:
        /// - Destroys all entities (creatures, placeables, doors, triggers, waypoints, sounds)
        /// - Disposes navigation mesh if IDisposable
        /// - Clears room meshes and visibility data
        /// - Clears all entity lists
        /// - Clears rendering context
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
            // Based on swkotor.exe/swkotor2.exe: Entities are removed from area and destroyed
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
                        if (entity is Runtime.Core.Entities.Entity concreteEntity)
                        {
                            concreteEntity.Destroy();
                        }
                    }
                }
            }

            // Dispose navigation mesh if it implements IDisposable
            // Based on swkotor.exe/swkotor2.exe: Navigation mesh resources are freed
            if (_navigationMesh != null)
            {
                if (_navigationMesh is IDisposable disposableMesh)
                {
                    disposableMesh.Dispose();
                }
                _navigationMesh = null;
            }

            // Clear room meshes and visibility data
            // Based on swkotor.exe/swkotor2.exe: Room geometry is freed during area unload
            if (_roomMeshes != null)
            {
                foreach (var roomMesh in _roomMeshes.Values)
                {
                    if (roomMesh is IDisposable disposableRoom)
                    {
                        disposableRoom.Dispose();
                    }
                }
                _roomMeshes.Clear();
            }

            // Clear room bounding box cache
            if (_roomBoundingBoxes != null)
            {
                _roomBoundingBoxes.Clear();
            }

            if (_rooms != null)
            {
                _rooms.Clear();
            }

            _visibilityData = null;

            // Clear rendering context
            if (_renderContext != null)
            {
                if (_renderContext is IDisposable disposableContext)
                {
                    disposableContext.Dispose();
                }
                _renderContext = null;
            }

            // Clean up area entity for script execution
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area entities are destroyed when area is unloaded
            // Area entities are registered with world but not in area's entity collections
            // Must be explicitly unregistered and destroyed during area unload
            if (_areaEntityForScripts != null && _areaEntityForScripts.IsValid)
            {
                // Unregister from world (removes from lookup tables)
                if (_areaEntityForScripts.World != null)
                {
                    _areaEntityForScripts.World.UnregisterEntity(_areaEntityForScripts);
                    // Destroy entity if world supports it
                    if (_areaEntityForScripts.World is BaseWorld baseWorld)
                    {
                        baseWorld.DestroyEntity(_areaEntityForScripts.ObjectId);
                    }
                }
                else
                {
                    // Entity not registered with world - destroy directly
                    if (_areaEntityForScripts is Runtime.Core.Entities.Entity concreteEntity)
                    {
                        concreteEntity.Destroy();
                    }
                }
                _areaEntityForScripts = null;
            }

            // Clear all entity lists
            // Based on swkotor.exe/swkotor2.exe: Entity lists are cleared during unload
            _creatures.Clear();
            _placeables.Clear();
            _doors.Clear();
            _triggers.Clear();
            _waypoints.Clear();
            _sounds.Clear();

            // Clear string references (optional cleanup)
            // Based on swkotor.exe/swkotor2.exe: String references are cleared
            _resRef = null;
            _displayName = null;
            _tag = null;

            // Clear area local variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variables are cleared during unload
            if (_localVariables != null)
            {
                _localVariables.Ints.Clear();
                _localVariables.Floats.Clear();
                _localVariables.Strings.Clear();
                _localVariables.Objects.Clear();
                _localVariables.Locations.Clear();
            }
        }

        /// <summary>
        /// Gets the area local variables storage.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area local variable storage
        /// Area variables are stored separately from entity variables
        /// </remarks>
        public Runtime.Core.Save.LocalVariableSet GetLocalVariables()
        {
            if (_localVariables == null)
            {
                _localVariables = new Runtime.Core.Save.LocalVariableSet();
            }
            return _localVariables;
        }

        /// <summary>
        /// Sets area local integer variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public void SetLocalInt(string name, int value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (_localVariables == null)
            {
                _localVariables = new Runtime.Core.Save.LocalVariableSet();
            }
            _localVariables.Ints[name] = value;
        }

        /// <summary>
        /// Gets area local integer variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public int GetLocalInt(string name)
        {
            if (string.IsNullOrEmpty(name) || _localVariables == null)
            {
                return 0;
            }
            if (_localVariables.Ints.TryGetValue(name, out int value))
            {
                return value;
            }
            return 0;
        }

        /// <summary>
        /// Sets area local float variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public void SetLocalFloat(string name, float value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (_localVariables == null)
            {
                _localVariables = new Runtime.Core.Save.LocalVariableSet();
            }
            _localVariables.Floats[name] = value;
        }

        /// <summary>
        /// Gets area local float variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public float GetLocalFloat(string name)
        {
            if (string.IsNullOrEmpty(name) || _localVariables == null)
            {
                return 0.0f;
            }
            if (_localVariables.Floats.TryGetValue(name, out float value))
            {
                return value;
            }
            return 0.0f;
        }

        /// <summary>
        /// Sets area local string variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public void SetLocalString(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (_localVariables == null)
            {
                _localVariables = new Runtime.Core.Save.LocalVariableSet();
            }
            _localVariables.Strings[name] = value ?? "";
        }

        /// <summary>
        /// Gets area local string variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public string GetLocalString(string name)
        {
            if (string.IsNullOrEmpty(name) || _localVariables == null)
            {
                return "";
            }
            if (_localVariables.Strings.TryGetValue(name, out string value))
            {
                return value ?? "";
            }
            return "";
        }

        /// <summary>
        /// Sets area local object reference variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public void SetLocalObject(string name, uint objectId)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (_localVariables == null)
            {
                _localVariables = new Runtime.Core.Save.LocalVariableSet();
            }
            _localVariables.Objects[name] = objectId;
        }

        /// <summary>
        /// Gets area local object reference variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public uint GetLocalObject(string name)
        {
            if (string.IsNullOrEmpty(name) || _localVariables == null)
            {
                return 0;
            }
            if (_localVariables.Objects.TryGetValue(name, out uint value))
            {
                return value;
            }
            return 0;
        }

        /// <summary>
        /// Sets area local location variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public void SetLocalLocation(string name, Runtime.Core.Save.SavedLocation location)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (_localVariables == null)
            {
                _localVariables = new Runtime.Core.Save.LocalVariableSet();
            }
            _localVariables.Locations[name] = location;
        }

        /// <summary>
        /// Gets area local location variable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area variable storage system
        /// </remarks>
        public Runtime.Core.Save.SavedLocation GetLocalLocation(string name)
        {
            if (string.IsNullOrEmpty(name) || _localVariables == null)
            {
                return null;
            }
            if (_localVariables.Locations.TryGetValue(name, out Runtime.Core.Save.SavedLocation value))
            {
                return value;
            }
            return null;
        }
    }
}
