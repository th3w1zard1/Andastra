using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;

namespace Andastra.Parsing.Resource.Generics
{
    /// <summary>
    /// Stores static area data.
    /// </summary>
    /// <remarks>
    /// WHAT IS AN ARE FILE?
    /// 
    /// An ARE file is an Area file that stores all the static (unchanging) information about a
    /// game area. An area is a location within a module that the player can explore, like a room,
    /// hallway, or outdoor space. The ARE file tells the game engine how to render the area,
    /// what the lighting looks like, what the weather is, and what scripts to run.
    /// 
    /// WHAT DATA DOES IT STORE?
    /// 
    /// An ARE file contains:
    /// 
    /// 1. BASIC AREA PROPERTIES:
    ///    - Tag: A tag identifier used by scripts to reference this area
    ///    - Name: The display name of the area (shown in-game, can be in different languages)
    ///    - AlphaTest: Alpha testing value for transparency rendering
    ///    - CameraStyle: How the camera behaves in this area
    ///    - DefaultEnvMap: The environment map texture used for reflections
    ///    - DisableTransit: Whether area transitions are disabled
    ///    - Unescapable: Whether the player can leave this area
    /// 
    /// 2. LIGHTING:
    ///    - SunAmbient: The ambient (indirect) light color from the sun
    ///    - SunDiffuse: The diffuse (direct) light color from the sun
    ///    - DynamicLight: The color of dynamic lights in the area
    ///    - DawnAmbient, DayAmbient, DuskAmbient, NightAmbient: Ambient light colors for different times of day
    ///    - DawnColor1/2/3, DayColor1/2/3, DuskColor1/2/3, NightColor1/2/3: Light colors for different times of day
    ///    - DawnDir1/2/3, DayDir1/2/3, DuskDir1/2/3, NightDir1/2/3: Light directions for different times of day
    /// 
    /// 3. FOG:
    ///    - FogEnabled: Whether fog is enabled in this area
    ///    - FogNear: The distance where fog starts (near clipping plane)
    ///    - FogFar: The distance where fog is fully opaque (far clipping plane)
    ///    - FogColor: The color of the fog
    ///    - SunFogEnabled: Whether sun fog is enabled
    ///    - SunFogNear, SunFogFar: Sun fog distance settings
    ///    - SunFogColor: The color of the sun fog
    /// 
    /// 4. GRASS:
    ///    - GrassTexture: The texture file used for grass
    ///    - GrassDensity: How many grass objects per unit area
    ///    - GrassSize: The size of grass objects
    ///    - GrassProbLL/LR/UL/UR: Probability of grass in different quadrants (lower-left, lower-right, upper-left, upper-right)
    ///    - GrassDiffuse: The diffuse color of grass
    ///    - GrassAmbient: The ambient color of grass
    ///    - GrassEmissive: The emissive (glowing) color of grass (KotOR 2 only)
    /// 
    /// 5. WEATHER:
    ///    - Weather: The weather type (clear, rain, snow, etc.)
    ///    - ChanceRain: Percent chance of rain (0-100, KotOR 2 only)
    ///    - ChanceSnow: Percent chance of snow (0-100, KotOR 2 only)
    ///    - ChanceLightning: Percent chance of lightning (0-100, KotOR 2 only)
    ///    - WindPower: The strength of wind in the area
    ///    - SkyBox: The skybox model to use
    /// 
    /// 6. STEALTH:
    ///    - StealthXp: Whether stealth experience is enabled
    ///    - StealthXpMax: Maximum stealth experience that can be gained
    ///    - StealthXpLoss: Stealth experience lost when detected
    /// 
    /// 7. SCRIPT HOOKS:
    ///    - OnEnter: Script that runs when a creature enters the area
    ///    - OnExit: Script that runs when a creature exits the area
    ///    - OnHeartbeat: Script that runs periodically while the area is loaded
    ///    - OnUserDefined: Script that runs when triggered by other scripts
    ///    - OnEnter2, OnExit2, OnHeartbeat2, OnUserDefined2: Additional script hooks (KotOR 2 only)
    /// 
    /// 8. MAP DATA:
    ///    - MapList: List of map image files (TPC files) for the minimap
    ///    - MapResX: Map resolution in X direction
    ///    - MapZoom: Map zoom level
    ///    - MapPoint1, MapPoint2: Image coordinates (normalized 0.0-1.0) for map calibration
    ///    - WorldPoint1, WorldPoint2: World coordinates for map calibration
    ///    - NorthAxis: Which axis points north on the map
    /// 
    /// 9. ROOMS:
    ///    - Rooms: List of room definitions for audio zones and minimap regions
    ///    - Each room defines audio properties, weather behavior, and force rating for a specific region
    /// 
    /// 10. DIRTY FORMULAS (KotOR 2 only):
    ///     - DirtyFormula1, DirtyFormula2, DirtyFormula3: Formulas for calculating "dirtiness" of surfaces
    /// 
    /// HOW DOES THE GAME ENGINE USE ARE FILES?
    /// 
    /// STEP 1: Loading the Area
    /// - When the player enters an area, the engine loads the ARE file
    /// - It reads the basic properties to set up the area
    /// - It reads the lighting settings to set up the sun and ambient light
    /// 
    /// STEP 2: Setting Up Rendering
    /// - The engine uses the fog settings to render fog in the distance
    /// - It uses the grass settings to render grass objects on the ground
    /// - It uses the lighting settings to light the area correctly
    /// 
    /// STEP 3: Setting Up Weather
    /// - The engine uses the weather settings to determine if it's raining, snowing, etc.
    /// - It uses the chance values to randomly trigger weather effects
    /// 
    /// STEP 4: Setting Up Scripts
    /// - The engine registers the script hooks so they run when events occur
    /// - When a creature enters, the OnEnter script runs
    /// - Periodically, the OnHeartbeat script runs
    /// 
    /// STEP 5: Setting Up the Map
    /// - The engine loads the map images from MapList
    /// - It uses the map calibration points to align the map with the world
    /// - It displays the map in the minimap UI
    /// 
    /// WHY ARE ARE FILES NEEDED?
    /// 
    /// Without ARE files, the game engine wouldn't know:
    /// - How to light the area
    /// - What the weather should be
    /// - What the fog should look like
    /// - What scripts to run when events happen
    /// - How to display the minimap
    /// 
    /// The ARE file acts as a configuration file that tells the engine everything about how to render and manage the area.
    /// 
    /// RELATIONSHIP TO OTHER FILES:
    /// 
    /// - LYT files: The layout files that define room positions
    /// - GIT files: The game instance template files that contain creatures, placeables, etc.
    /// - WOK files: The walkmesh files that define where characters can walk
    /// - VIS files: The visibility files that define which rooms can see each other
    /// - TPC files: The texture files used for grass, maps, etc.
    /// - MDL files: The 3D model files for rooms
    /// 
    /// Together, these files define a complete game area that the player can explore.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// Based on swkotor2.exe: ARE files are loaded when an area is initialized. The engine reads
    /// the lighting, fog, grass, and weather settings to set up the rendering environment. Script
    /// hooks are registered for event handling, and map data is loaded for the minimap display.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:19-541
    /// </remarks>
    [PublicAPI]
    public sealed class ARE
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:19
        // Original: BINARY_TYPE = ResourceType.ARE
        public static readonly ResourceType BinaryType = ResourceType.ARE;

        // Basic ARE properties
        public string Tag { get; set; } = string.Empty;
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public int AlphaTest { get; set; }
        public int CameraStyle { get; set; }
        public ResRef DefaultEnvMap { get; set; } = ResRef.FromBlank();
        public bool DisableTransit { get; set; }
        public bool Unescapable { get; set; }
        public bool StealthXp { get; set; }
        public int StealthXpMax { get; set; }
        public int StealthXpLoss { get; set; }
        public ResRef GrassTexture { get; set; } = ResRef.FromBlank();
        public float GrassDensity { get; set; }
        public float GrassSize { get; set; }
        public float GrassProbLL { get; set; }
        public float GrassProbLR { get; set; }
        public float GrassProbUL { get; set; }
        public float GrassProbUR { get; set; }
        /// <summary>
        /// Grass diffuse color (RGB).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:68
        /// Original: grass_diffuse: "Grass_Diffuse" field.
        /// </remarks>
        public Color GrassDiffuse { get; set; } = new Color(0, 0, 0);
        /// <summary>
        /// Grass ambient color (RGB).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:67
        /// Original: grass_ambient: "Grass_Ambient" field.
        /// </remarks>
        public Color GrassAmbient { get; set; } = new Color(0, 0, 0);
        /// <summary>
        /// Grass emissive color (RGB). KotOR 2 Only.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:73
        /// Original: grass_emissive: "Grass_Emissive" field. KotOR 2 Only.
        /// </remarks>
        public Color GrassEmissive { get; set; } = new Color(0, 0, 0);
        public bool FogEnabled { get; set; }
        public float FogNear { get; set; }
        public float FogFar { get; set; }
        public Color FogColor { get; set; } = new Color(0, 0, 0);
        public bool SunFogEnabled { get; set; }
        public float SunFogNear { get; set; }
        public float SunFogFar { get; set; }
        public Color SunFogColor { get; set; } = new Color(0, 0, 0);
        public Color SunAmbient { get; set; } = new Color(0, 0, 0);
        public Color SunDiffuse { get; set; } = new Color(0, 0, 0);
        public Color DynamicLight { get; set; } = new Color(0, 0, 0);
        public int WindPower { get; set; }
        public ResRef ShadowOpacity { get; set; } = ResRef.FromBlank();
        /// <summary>
        /// Percent chance of rain (0-100). KotOR 2 Only.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:74
        /// Original: chance_rain: "ChanceRain" field. KotOR 2 Only.
        /// </remarks>
        public int ChanceRain { get; set; }
        /// <summary>
        /// Percent chance of snow (0-100). KotOR 2 Only.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:75
        /// Original: chance_snow: "ChanceSnow" field. KotOR 2 Only.
        /// </remarks>
        public int ChanceSnow { get; set; }
        /// <summary>
        /// Percent chance of lightning (0-100). KotOR 2 Only.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:76
        /// Original: chance_lightning: "ChanceLightning" field. KotOR 2 Only.
        /// </remarks>
        public int ChanceLightning { get; set; }
        public ResRef ChancesOfFog { get; set; } = ResRef.FromBlank();
        public int Weather { get; set; }
        public int SkyBox { get; set; }
        public int DawnAmbient { get; set; }
        public int DayAmbient { get; set; }
        public int DuskAmbient { get; set; }
        public int NightAmbient { get; set; }
        
        // Aurora (NWN) specific fields
        // Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        // Reads from AreaProperties struct: EnvAudio, SkyBox, MoonFogColor, SunFogColor, MoonFogAmount, SunFogAmount, MoonAmbientColor, MoonDiffuseColor, SunAmbientColor, SunDiffuseColor, DisplayName
        /// <summary>
        /// Environment audio ID (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldINT(param_1, local_res20, "EnvAudio", ...)
        /// </remarks>
        public int EnvAudio { get; set; }
        
        /// <summary>
        /// Display name (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldCExoString(param_1, local_60, local_res20, "DisplayName", ...)
        /// </remarks>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Moon fog color (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldDWORD(param_1, local_res20, "MoonFogColor", ...)
        /// </remarks>
        public Color MoonFogColor { get; set; } = new Color(0, 0, 0);
        
        /// <summary>
        /// Moon fog amount (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldBYTE(param_1, local_res20, "MoonFogAmount", ...)
        /// </remarks>
        public byte MoonFogAmount { get; set; }
        
        /// <summary>
        /// Moon ambient color (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldDWORD(param_1, local_res20, "MoonAmbientColor", ...)
        /// </remarks>
        public Color MoonAmbientColor { get; set; } = new Color(0, 0, 0);
        
        /// <summary>
        /// Moon diffuse color (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldDWORD(param_1, local_res20, "MoonDiffuseColor", ...)
        /// </remarks>
        public Color MoonDiffuseColor { get; set; } = new Color(0, 0, 0);
        public int DawnDir1 { get; set; }
        public int DawnDir2 { get; set; }
        public int DawnDir3 { get; set; }
        public int DayDir1 { get; set; }
        public int DayDir2 { get; set; }
        public int DayDir3 { get; set; }
        public int DuskDir1 { get; set; }
        public int DuskDir2 { get; set; }
        public int DuskDir3 { get; set; }
        public int NightDir1 { get; set; }
        public int NightDir2 { get; set; }
        public int NightDir3 { get; set; }
        public Color DawnColor1 { get; set; } = new Color(0, 0, 0);
        public Color DawnColor2 { get; set; } = new Color(0, 0, 0);
        public Color DawnColor3 { get; set; } = new Color(0, 0, 0);
        public Color DayColor1 { get; set; } = new Color(0, 0, 0);
        public Color DayColor2 { get; set; } = new Color(0, 0, 0);
        public Color DayColor3 { get; set; } = new Color(0, 0, 0);
        public Color DuskColor1 { get; set; } = new Color(0, 0, 0);
        public Color DuskColor2 { get; set; } = new Color(0, 0, 0);
        public Color DuskColor3 { get; set; } = new Color(0, 0, 0);
        public Color NightColor1 { get; set; } = new Color(0, 0, 0);
        public Color NightColor2 { get; set; } = new Color(0, 0, 0);
        public Color NightColor3 { get; set; } = new Color(0, 0, 0);
        public ResRef OnEnter { get; set; } = ResRef.FromBlank();
        public ResRef OnExit { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined { get; set; } = ResRef.FromBlank();
        public ResRef OnEnter2 { get; set; } = ResRef.FromBlank();
        public ResRef OnExit2 { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat2 { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined2 { get; set; } = ResRef.FromBlank();
        public List<string> AreaList { get; set; } = new List<string>();
        public List<ResRef> MapList { get; set; } = new List<ResRef>();

        /// <summary>
        /// List of room definitions for audio zones and minimap regions.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:267
        /// Original: self.rooms: list[ARERoom] = []
        /// 
        /// Reference: vendor/reone/src/libs/resource/parser/gff/are.cpp:244-251 - parseARE_Rooms function
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:185-191 - ARE_Rooms struct
        /// Reference: vendor/Kotor.NET/Kotor.NET/Resources/KotorARE/ARE.cs:96 - Rooms property
        /// Reference: vendor/KotOR.js/src/module/ModuleArea.ts:120 - rooms array
        /// 
        /// Rooms define audio properties, weather behavior, and force rating for specific
        /// regions within an area. Rooms are referenced by VIS (visibility) files and
        /// used for audio occlusion and weather control.
        /// </remarks>
        public List<ARERoom> Rooms { get; set; } = new List<ARERoom>();

        /// <summary>
        /// Dirty formula 1 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:78
        /// Original: dirty_formula_1: "DirtyFormulaOne" field. KotOR 2 Only.
        /// </remarks>
        public int DirtyFormula1 { get; set; }

        /// <summary>
        /// Dirty formula 2 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:81
        /// Original: dirty_formula_2: "DirtyFormulaTwo" field. KotOR 2 Only.
        /// </remarks>
        public int DirtyFormula2 { get; set; }

        /// <summary>
        /// Dirty formula 3 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:84
        /// Original: dirty_formula_3: "DirtyFormulaThre" field. KotOR 2 Only.
        /// </remarks>
        public int DirtyFormula3 { get; set; }

        /// <summary>
        /// Dirty size 1 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:77
        /// Original: dirty_size_1: "DirtySizeOne" field. KotOR 2 Only.
        /// Note: Stored as Float in GFF but treated as integer value.
        /// </remarks>
        public int DirtySize1 { get; set; }

        /// <summary>
        /// Dirty size 2 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:80
        /// Original: dirty_size_2: "DirtySizeTwo" field. KotOR 2 Only.
        /// Note: Stored as Float in GFF but treated as integer value.
        /// </remarks>
        public int DirtySize2 { get; set; }

        /// <summary>
        /// Dirty size 3 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:83
        /// Original: dirty_size_3: "DirtySizeThree" field. KotOR 2 Only.
        /// Note: Stored as Float in GFF but treated as integer value.
        /// </remarks>
        public int DirtySize3 { get; set; }

        /// <summary>
        /// Developer comments/notes (toolset only, not used by game engine).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:140
        /// Original: self.comment: str = ""
        /// </remarks>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// Map north axis orientation.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:260
        /// Original: self.north_axis: ARENorthAxis = ARENorthAxis.PositiveX
        /// </remarks>
        public ARENorthAxis NorthAxis { get; set; } = ARENorthAxis.PositiveX;

        /// <summary>
        /// Map zoom level.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:259
        /// Original: self.map_zoom: int = 0
        /// </remarks>
        public int MapZoom { get; set; }

        /// <summary>
        /// Map resolution X.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:258
        /// Original: self.map_res_x: int = 0
        /// </remarks>
        public int MapResX { get; set; }

        /// <summary>
        /// Map point 1 (image coordinates, normalized 0.0-1.0).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:254
        /// Original: self.map_point_1: Vector2 = Vector2.from_null()
        /// </remarks>
        public Vector2 MapPoint1 { get; set; }

        /// <summary>
        /// Map point 2 (image coordinates, normalized 0.0-1.0).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:255
        /// Original: self.map_point_2: Vector2 = Vector2.from_null()
        /// </remarks>
        public Vector2 MapPoint2 { get; set; }

        /// <summary>
        /// World point 1 (world coordinates).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:256
        /// Original: self.world_point_1: Vector2 = Vector2.from_null()
        /// </remarks>
        public Vector2 WorldPoint1 { get; set; }

        /// <summary>
        /// World point 2 (world coordinates).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:257
        /// Original: self.world_point_2: Vector2 = Vector2.from_null()
        /// </remarks>
        public Vector2 WorldPoint2 { get; set; }

        /// <summary>
        /// Load screen ID (index into loadscreens.2da).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:104,293
        /// Original: loadscreen_id: "LoadScreenID" field. Not used by the game engine.
        /// </remarks>
        public int LoadScreenID { get; set; }

        // Deprecated fields (toolset-only, not used by game engines)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:89-108
        // Original: These fields are from NWN and are preserved for compatibility with existing ARE files
        // Reference: vendor/reone/include/reone/resource/parser/gff/are.h:232,250,267-268,269-274,262-263,278,283-284
        // Reference: vendor/reone/src/libs/resource/parser/gff/are.cpp:308,325,336,338-339,348-365

        /// <summary>
        /// Unused ID field (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:89
        /// Original: unused_id: "ID" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:261
        /// </remarks>
        public int UnusedId { get; set; }

        /// <summary>
        /// Creator ID field (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:90
        /// Original: creator_id: "Creator_ID" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:232
        /// </remarks>
        public int CreatorId { get; set; }

        /// <summary>
        /// Flags field (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:91
        /// Original: flags: "Flags" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:250
        /// </remarks>
        public uint Flags { get; set; }

        /// <summary>
        /// Modifier for spot check (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:92
        /// Original: mod_spot_check: "ModSpotCheck" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:268
        /// </remarks>
        public int ModSpotCheck { get; set; }

        /// <summary>
        /// Modifier for listen check (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:93
        /// Original: mod_listen_check: "ModListenCheck" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:267
        /// </remarks>
        public int ModListenCheck { get; set; }

        /// <summary>
        /// Moon ambient color (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:94
        /// Original: moon_ambient: "MoonAmbientColor" field. Not used by the game engine.
        /// Note: This is separate from the Aurora-specific MoonAmbientColor property.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:269
        /// </remarks>
        public Color MoonAmbient { get; set; } = new Color(0, 0, 0);

        /// <summary>
        /// Moon diffuse color (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:95
        /// Original: moon_diffuse: "MoonDiffuseColor" field. Not used by the game engine.
        /// Note: This is separate from the Aurora-specific MoonDiffuseColor property.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:270
        /// </remarks>
        public Color MoonDiffuse { get; set; } = new Color(0, 0, 0);

        /// <summary>
        /// Moon fog enabled flag (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:96
        /// Original: moon_fog: "MoonFogOn" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:274
        /// </remarks>
        public bool MoonFog { get; set; }

        /// <summary>
        /// Moon fog near distance (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:97
        /// Original: moon_fog_near: "MoonFogNear" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:273
        /// </remarks>
        public float MoonFogNear { get; set; }

        /// <summary>
        /// Moon fog far distance (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:98
        /// Original: moon_fog_far: "MoonFogFar" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:272
        /// </remarks>
        public float MoonFogFar { get; set; }

        /// <summary>
        /// Moon fog color (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:99
        /// Original: moon_fog_color: "MoonFogColor" field. Not used by the game engine.
        /// Note: This is separate from the Aurora-specific MoonFogColor property.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:271
        /// </remarks>
        public Color MoonFogColorDeprecated { get; set; } = new Color(0, 0, 0);

        /// <summary>
        /// Moon shadows enabled flag (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:100
        /// Original: moon_shadows: "MoonShadows" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:275
        /// </remarks>
        public bool MoonShadows { get; set; }

        /// <summary>
        /// Is night flag (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:101
        /// Original: is_night: "IsNight" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:262
        /// </remarks>
        public bool IsNight { get; set; }

        /// <summary>
        /// Lighting scheme (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:102
        /// Original: lighting_scheme: "LightingScheme" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:263
        /// </remarks>
        public int LightingScheme { get; set; }

        /// <summary>
        /// Day/night cycle enabled flag (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:103
        /// Original: day_night: "DayNightCycle" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:233
        /// </remarks>
        public bool DayNightCycle { get; set; }

        /// <summary>
        /// No rest flag (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:105
        /// Original: no_rest: "NoRest" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:278
        /// </remarks>
        public bool NoRest { get; set; }

        /// <summary>
        /// No hang back flag (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:106
        /// Original: no_hang_back: "NoHangBack" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:277
        /// </remarks>
        public bool NoHangBack { get; set; }

        /// <summary>
        /// Player only flag (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:107
        /// Original: player_only: "PlayerOnly" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:283
        /// </remarks>
        public bool PlayerOnly { get; set; }

        /// <summary>
        /// Player vs player flag (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:108
        /// Original: player_vs_player: "PlayerVsPlayer" field. Not used by the game engine.
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:284
        /// </remarks>
        public bool PlayerVsPlayer { get; set; }

        /// <summary>
        /// Expansion list (deprecated, toolset-only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py (dismantle_are line 680)
        /// Original: root.set_list("Expansion_List", GFFList()) - empty list preserved for compatibility
        /// Reference: vendor/reone/include/reone/resource/parser/gff/are.h:249
        /// Note: This is always written as an empty list for compatibility.
        /// </remarks>
        public GFFList ExpansionList { get; set; } = new GFFList();

        public ARE()
        {
        }
    }
}
