using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace Andastra.Parsing.Resource.Generics.ARE
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py
    // Original: construct_are and dismantle_are functions
    public static class AREHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:394-535
        // Original: def construct_are(gff: GFF) -> ARE:
        // Note: This function loads ALL fields from GFF regardless of game type.
        // Game-specific field handling is done in DismantleAre when writing.
        // All engines (Odyssey/KOTOR, Aurora/NWN, Eclipse/DA/ME) use the same ARE file format structure.
        // K2-specific fields (Grass_Emissive, DirtyARGB, ChanceRain/Snow/Lightning, etc.) are optional
        // and will be 0/default if not present (which is correct for K1, Aurora, and Eclipse).
        //
        // Default values verified against engine behavior:
        // - swkotor.exe: FUN_00508c50 (LoadAreaProperties) @ 0x00508c50
        // - swkotor2.exe: FUN_004e3ff0 (LoadAreaProperties) @ 0x004e3ff0
        public static ARE ConstructAre(GFF gff)
        {
            var are = new ARE();

            var root = gff.Root;
            var mapStruct = root.Acquire<GFFStruct>("Map", new GFFStruct());
            // map_original_struct_id would need to be stored in ARE class
            // are.map_original_struct_id = mapStruct.StructId;

            // Map fields - all optional, defaults verified from engine
            // swkotor.exe: 0x00508c50, swkotor2.exe: 0x004e3ff0
            // Matching Python: are.north_axis = ARENorthAxis(map_struct.acquire("NorthAxis", 0))
            // Engine default: 0 (swkotor.exe: 0x00509c4f line 447, swkotor2.exe: 0x004e507f line 454)
            are.NorthAxis = (ARENorthAxis)mapStruct.Acquire<int>("NorthAxis", 0);
            // Matching Python: are.map_zoom = map_struct.acquire("MapZoom", 0)
            // Engine default: 1 (swkotor.exe: 0x00509c4f line 448, swkotor2.exe: 0x004e507f line 455)
            // NOTE: Engine uses 1 as default, not 0. This is important for map display.
            are.MapZoom = mapStruct.Acquire<int>("MapZoom", 1);
            // Matching Python: are.map_res_x = map_struct.acquire("MapResX", 0)
            // Engine default: 0 (swkotor.exe: 0x00509c4f line 445, swkotor2.exe: 0x004e507f line 452)
            are.MapResX = mapStruct.Acquire<int>("MapResX", 0);
            // Matching Python: are.map_point_1 = Vector2(map_struct.acquire("MapPt1X", 0.0), map_struct.acquire("MapPt1Y", 0.0))
            are.MapPoint1 = new System.Numerics.Vector2(
                mapStruct.Acquire<float>("MapPt1X", 0.0f),
                mapStruct.Acquire<float>("MapPt1Y", 0.0f));
            // Matching Python: are.map_point_2 = Vector2(map_struct.acquire("MapPt2X", 0.0), map_struct.acquire("MapPt2Y", 0.0))
            are.MapPoint2 = new System.Numerics.Vector2(
                mapStruct.Acquire<float>("MapPt2X", 0.0f),
                mapStruct.Acquire<float>("MapPt2Y", 0.0f));
            // Matching Python: are.world_point_1 = Vector2(map_struct.acquire("WorldPt1X", 0.0), map_struct.acquire("WorldPt1Y", 0.0))
            are.WorldPoint1 = new System.Numerics.Vector2(
                mapStruct.Acquire<float>("WorldPt1X", 0.0f),
                mapStruct.Acquire<float>("WorldPt1Y", 0.0f));
            // Matching Python: are.world_point_2 = Vector2(map_struct.acquire("WorldPt2X", 0.0), map_struct.acquire("WorldPt2Y", 0.0))
            are.WorldPoint2 = new System.Numerics.Vector2(
                mapStruct.Acquire<float>("WorldPt2X", 0.0f),
                mapStruct.Acquire<float>("WorldPt2Y", 0.0f));
            are.MapList = new System.Collections.Generic.List<ResRef>(); // Placeholder

            // Extract basic fields - all optional unless otherwise noted
            // swkotor.exe: 0x00508c50, swkotor2.exe: 0x004e3ff0
            // Engine default: "" (swkotor.exe: 0x00508c50 line 161, swkotor2.exe: 0x004e3ff0 line 161)
            are.Tag = root.Acquire<string>("Tag", "");
            // Engine default: Invalid LocalizedString (swkotor.exe: 0x00508c50 line 152, swkotor2.exe: 0x004e3ff0 line 152)
            are.Name = root.Acquire<LocalizedString>("Name", LocalizedString.FromInvalid());
            // Engine default: 0.2 (swkotor.exe: 0x00508c50 line 303, swkotor2.exe: 0x004e3ff0 line 307)
            // Engine reads and stores AlphaTest as float (verified from Ghidra decompilation)
            // Line 303-304 in swkotor.exe: fVar14 = FUN_00411d00(..., "AlphaTest", ..., 0.2); *(float *)((int)this + 0xfc) = (float)fVar14;
            // Line 307-308 in swkotor2.exe: fVar14 = FUN_00412e20(..., "AlphaTest", ..., 0.2); *(float *)((int)this + 0x100) = (float)fVar14;
            are.AlphaTest = root.Acquire<float>("AlphaTest", 0.2f);
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 174, swkotor2.exe: 0x004e3ff0 line 174)
            are.CameraStyle = root.Acquire<int>("CameraStyle", 0);
            // Engine default: "" (swkotor.exe: 0x00508c50 line 177-179, swkotor2.exe: 0x004e3ff0 line 177-179)
            are.DefaultEnvMap = root.Acquire<ResRef>("DefaultEnvMap", ResRef.FromBlank());
            // Matching Python: are.unescapable = bool(root.acquire("Unescapable", 0))
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x00508c50 line 186-188, swkotor2.exe: 0x004e3ff0 line 186-188)
            // For new ARE objects, default is false (0)
            are.Unescapable = root.GetUInt8("Unescapable") == 1;
            // Matching Python: are.disable_transit = bool(root.acquire("DisableTransit", 0))
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 189, swkotor2.exe: 0x004e3ff0 line 189)
            are.DisableTransit = root.GetUInt8("DisableTransit") == 1;
            // Grass fields - all optional
            // Engine default: "" but if empty, engine sets to "grass" (swkotor.exe: 0x00508c50 line 286-294, swkotor2.exe: 0x004e3ff0 line 290-298)
            // NOTE: Engine has special handling - if Grass_TexName is empty, it defaults to "grass"
            // We preserve the empty string here; engine-specific code should handle the "grass" fallback
            are.GrassTexture = root.Acquire<ResRef>("Grass_TexName", ResRef.FromBlank());
            // Engine default: 0.0 (swkotor.exe: 0x00508c50 line 282, swkotor2.exe: 0x004e3ff0 line 286)
            are.GrassDensity = root.Acquire<float>("Grass_Density", 0.0f);
            // Engine default: 0.0 (swkotor.exe: 0x00508c50 line 284, swkotor2.exe: 0x004e3ff0 line 288)
            are.GrassSize = root.Acquire<float>("Grass_QuadSize", 0.0f);
            // Engine default: 0.0 (swkotor.exe: 0x00508c50 line 295, swkotor2.exe: 0x004e3ff0 line 299)
            are.GrassProbLL = root.Acquire<float>("Grass_Prob_LL", 0.0f);
            // Engine default: 0.0 (swkotor.exe: 0x00508c50 line 297, swkotor2.exe: 0x004e3ff0 line 301)
            are.GrassProbLR = root.Acquire<float>("Grass_Prob_LR", 0.0f);
            // Engine default: 0.0 (swkotor.exe: 0x00508c50 line 299, swkotor2.exe: 0x004e3ff0 line 303)
            are.GrassProbUL = root.Acquire<float>("Grass_Prob_UL", 0.0f);
            // Engine default: 0.0 (swkotor.exe: 0x00508c50 line 301, swkotor2.exe: 0x004e3ff0 line 305)
            are.GrassProbUR = root.Acquire<float>("Grass_Prob_UR", 0.0f);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:506-509
            // Original: are.grass_ambient = Color.from_rgb_integer(root.acquire("Grass_Ambient", 0))
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 280, swkotor2.exe: 0x004e3ff0 line 282)
            are.GrassAmbient = Color.FromRgbInteger(root.Acquire<int>("Grass_Ambient", 0));
            // Original: are.grass_diffuse = Color.from_rgb_integer(root.acquire("Grass_Diffuse", 0))
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 278, swkotor2.exe: 0x004e3ff0 line 280)
            are.GrassDiffuse = Color.FromRgbInteger(root.Acquire<int>("Grass_Diffuse", 0));
            // Original: are.grass_emissive = Color.from_rgb_integer(root.acquire("Grass_Emissive", 0))
            // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 284) - K2 only, not in K1
            are.GrassEmissive = Color.FromRgbInteger(root.Acquire<int>("Grass_Emissive", 0));
            // Fog and lighting fields - all optional
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 251, swkotor2.exe: 0x004e3ff0 line 253)
            are.FogEnabled = root.Acquire<int>("SunFogOn", 0) != 0;
            // Engine default: 10000.0, but if < 0, engine sets to 0 (swkotor.exe: 0x00508c50 line 241-245, swkotor2.exe: 0x004e3ff0 line 243-247)
            // NOTE: Engine uses 10000.0 as default, but we use 0.0 to match PyKotor and avoid confusion
            // The engine's 10000.0 default is likely a "no fog" sentinel value
            are.FogNear = root.Acquire<float>("SunFogNear", 0.0f);
            // Engine default: 10000.0, but if < 0, engine sets to 0 (swkotor.exe: 0x00508c50 line 246-250, swkotor2.exe: 0x004e3ff0 line 248-252)
            // NOTE: Engine uses 10000.0 as default, but we use 0.0 to match PyKotor and avoid confusion
            are.FogFar = root.Acquire<float>("SunFogFar", 0.0f);
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 206, swkotor2.exe: 0x004e3ff0 line 208)
            are.WindPower = root.Acquire<int>("WindPower", 0);
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x00508c50 line 265-267, swkotor2.exe: 0x004e3ff0 line 267-269)
            // NOTE: Engine reads ShadowOpacity as UInt8 (0-255), default is 0 for new ARE objects
            // Aurora uses 0-100 range, Eclipse uses 0-255 range
            are.ShadowOpacity = root.Acquire<byte>("ShadowOpacity", 0);
            // Weather fields (K2-specific) - all optional
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:131-134
            // Original: are.chance_lightning = root.acquire("ChanceLightning", 0)
            // Original: are.chance_snow = root.acquire("ChanceSnow", 0)
            // Original: are.chance_rain = root.acquire("ChanceRain", 0)
            // Note: These are K2-specific fields (KotOR 2 Only), will be 0 for K1, Aurora, and Eclipse
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 198, swkotor2.exe: 0x004e3ff0 line 200)
            // NOTE: If Flags & 1 is set, engine forces all weather chances to 0 (swkotor.exe: 0x00508c50 line 208-213, swkotor2.exe: 0x004e3ff0 line 210-216)
            are.ChanceRain = root.Acquire<int>("ChanceRain", 0);
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 200, swkotor2.exe: 0x004e3ff0 line 202)
            are.ChanceSnow = root.Acquire<int>("ChanceSnow", 0);
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 204, swkotor2.exe: 0x004e3ff0 line 206)
            are.ChanceLightning = root.Acquire<int>("ChanceLightning", 0);
            // Script hooks - all optional
            // Engine default: "" (swkotor.exe: 0x00508c50 line 140-142, swkotor2.exe: 0x004e3ff0 line 140-142)
            are.OnEnter = root.Acquire<ResRef>("OnEnter", ResRef.FromBlank());
            // Engine default: "" (swkotor.exe: 0x00508c50 line 146-148, swkotor2.exe: 0x004e3ff0 line 146-148)
            are.OnExit = root.Acquire<ResRef>("OnExit", ResRef.FromBlank());
            // Engine default: "" (swkotor.exe: 0x00508c50 line 128-130, swkotor2.exe: 0x004e3ff0 line 128-130)
            are.OnHeartbeat = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            // Engine default: "" (swkotor.exe: 0x00508c50 line 134-136, swkotor2.exe: 0x004e3ff0 line 134-136)
            are.OnUserDefined = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            // Stealth XP fields - all optional
            // Matching Python: are.stealth_xp = bool(root.acquire("StealthXPEnabled", 0))
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x00508c50 line 530-532, swkotor2.exe: 0x004e3ff0 line 537-539)
            // For new ARE objects, default is false (0)
            are.StealthXp = root.GetUInt8("StealthXPEnabled") == 1;
            // Matching Python: are.stealth_xp_loss = root.acquire("StealthXPLoss", 0)
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x00508c50 line 527-529, swkotor2.exe: 0x004e3ff0 line 534-536)
            // For new ARE objects, default is 0
            are.StealthXpLoss = root.Acquire<int>("StealthXPLoss", 0);
            // Matching Python: are.stealth_xp_max = root.acquire("StealthXPMax", 0)
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x00508c50 line 516-521, swkotor2.exe: 0x004e3ff0 line 523-528)
            // For new ARE objects, default is 0
            are.StealthXpMax = root.Acquire<int>("StealthXPMax", 0);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:496
            // Original: are.loadscreen_id = root.acquire("LoadScreenID", 0)
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 276, swkotor2.exe: 0x004e3ff0 line 278)
            are.LoadScreenID = root.Acquire<int>("LoadScreenID", 0);

            // Extract color fields (as RGB integers) - all optional
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:502-505
            // Original: are.sun_ambient = Color.from_rgb_integer(root.acquire("SunAmbientColor", 0))
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 235, swkotor2.exe: 0x004e3ff0 line 237)
            are.SunAmbient = Color.FromRgbInteger(root.Acquire<int>("SunAmbientColor", 0));
            // Original: are.sun_diffuse = Color.from_rgb_integer(root.acquire("SunDiffuseColor", 0))
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 237, swkotor2.exe: 0x004e3ff0 line 239)
            are.SunDiffuse = Color.FromRgbInteger(root.Acquire<int>("SunDiffuseColor", 0));
            // Original: are.dynamic_light = Color.from_rgb_integer(root.acquire("DynAmbientColor", 0))
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 259, swkotor2.exe: 0x004e3ff0 line 261)
            are.DynamicLight = Color.FromRgbInteger(root.Acquire<int>("DynAmbientColor", 0));
            // Original: are.fog_color = Color.from_rgb_integer(root.acquire("SunFogColor", 0))
            // Engine default: 0 (swkotor.exe: 0x00508c50 line 239, swkotor2.exe: 0x004e3ff0 line 241)
            are.FogColor = Color.FromRgbInteger(root.Acquire<int>("SunFogColor", 0));

            // Extract K2-specific dirty formula fields (KotOR 2 Only) - all optional
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:183,191,199
            // Original: are.dirty_formula_1 = root.acquire("DirtyFormulaOne", 0)
            // Engine default: 1, but engine inverts value (1->0, 0->1) (swkotor2.exe: 0x004e3ff0 line 544-551)
            // NOTE: Engine reads 1 as default, then inverts it. We store the raw value from file.
            are.DirtyFormula1 = root.Acquire<int>("DirtyFormulaOne", 0);
            // Original: are.dirty_formula_2 = root.acquire("DirtyFormulaTwo", 0)
            // Engine default: 1, but engine inverts value (1->0, 0->1) (swkotor2.exe: 0x004e3ff0 line 558-565)
            are.DirtyFormula2 = root.Acquire<int>("DirtyFormulaTwo", 0);
            // Original: are.dirty_formula_3 = root.acquire("DirtyFormulaThre", 0)
            // Engine default: 1, but engine inverts value (1->0, 0->1) (swkotor2.exe: 0x004e3ff0 line 572-579)
            are.DirtyFormula3 = root.Acquire<int>("DirtyFormulaThre", 0);
            // Extract K2-specific dirty ARGB, Size, and Func fields (KotOR 2 Only) - all optional
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:472-480,510-512
            // Original: are.dirty_argb_1 = Color.from_rgb_integer(root.acquire("DirtyARGBOne", 0))
            // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 540)
            are.DirtyArgb1 = Color.FromRgbInteger(root.Acquire<int>("DirtyARGBOne", 0));
            // Original: are.dirty_argb_2 = Color.from_rgb_integer(root.acquire("DirtyARGBTwo", 0))
            // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 554)
            are.DirtyArgb2 = Color.FromRgbInteger(root.Acquire<int>("DirtyARGBTwo", 0));
            // Original: are.dirty_argb_3 = Color.from_rgb_integer(root.acquire("DirtyARGBThree", 0))
            // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 568)
            are.DirtyArgb3 = Color.FromRgbInteger(root.Acquire<int>("DirtyARGBThree", 0));
            // Original: are.dirty_size_1 = root.acquire("DirtySizeOne", 0)
            // Engine default: 0x10 (16) (swkotor2.exe: 0x004e3ff0 line 542)
            are.DirtySize1 = root.Acquire<int>("DirtySizeOne", 16);
            // Original: are.dirty_size_2 = root.acquire("DirtySizeTwo", 0)
            // Engine default: 0x10 (16) (swkotor2.exe: 0x004e3ff0 line 556)
            are.DirtySize2 = root.Acquire<int>("DirtySizeTwo", 16);
            // Original: are.dirty_size_3 = root.acquire("DirtySizeThree", 0)
            // Engine default: 0x10 (16) (swkotor2.exe: 0x004e3ff0 line 570)
            are.DirtySize3 = root.Acquire<int>("DirtySizeThree", 16);
            // Original: are.dirty_func_1 = root.acquire("DirtyFuncOne", 0)
            // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 552)
            are.DirtyFunc1 = root.Acquire<int>("DirtyFuncOne", 0);
            // Original: are.dirty_func_2 = root.acquire("DirtyFuncTwo", 0)
            // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 566)
            are.DirtyFunc2 = root.Acquire<int>("DirtyFuncTwo", 0);
            // Original: are.dirty_func_3 = root.acquire("DirtyFuncThree", 0)
            // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 580)
            are.DirtyFunc3 = root.Acquire<int>("DirtyFuncThree", 0);

            // Extract Comments field (toolset-only, not used by game engine)
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:356
            // Original: are.comment = root.acquire("Comments", "")
            are.Comment = root.Acquire<string>("Comments", "");

            // Extract deprecated fields (toolset-only, not used by game engines)
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:481-500
            // Original: These fields are from NWN and are preserved for compatibility with existing ARE files
            // Reference: vendor/reone/src/libs/resource/parser/gff/are.cpp:308,325,336,338-339,348-365
            // Matching Python: are.unused_id = root.acquire("ID", 0)
            are.UnusedId = root.Acquire<int>("ID", 0);
            // Matching Python: are.creator_id = root.acquire("Creator_ID", 0)
            are.CreatorId = root.Acquire<int>("Creator_ID", 0);
            // Matching Python: are.flags = root.acquire("Flags", 0)
            are.Flags = root.Acquire<uint>("Flags", 0);
            // Matching Python: are.mod_spot_check = root.acquire("ModSpotCheck", 0)
            are.ModSpotCheck = root.Acquire<int>("ModSpotCheck", 0);
            // Matching Python: are.mod_listen_check = root.acquire("ModListenCheck", 0)
            are.ModListenCheck = root.Acquire<int>("ModListenCheck", 0);
            // Matching Python: are.moon_ambient = root.acquire("MoonAmbientColor", 0)
            are.MoonAmbient = Color.FromRgbInteger(root.Acquire<int>("MoonAmbientColor", 0));
            // Matching Python: are.moon_diffuse = root.acquire("MoonDiffuseColor", 0)
            are.MoonDiffuse = Color.FromRgbInteger(root.Acquire<int>("MoonDiffuseColor", 0));
            // Matching Python: are.moon_fog = root.acquire("MoonFogOn", 0)
            are.MoonFog = root.Acquire<int>("MoonFogOn", 0) != 0;
            // Matching Python: are.moon_fog_near = root.acquire("MoonFogNear", 0.0)
            // Engine default: 10000.0, but if < 0, engine sets to 0 (swkotor.exe: 0x00508c50 line 221-225, swkotor2.exe: 0x004e3ff0 line 223-227)
            // NOTE: Engine uses 10000.0 as default, but we use 0.0 to match PyKotor and avoid confusion
            are.MoonFogNear = root.Acquire<float>("MoonFogNear", 0.0f);
            // Matching Python: are.moon_fog_far = root.acquire("MoonFogFar", 0.0)
            // Engine default: 10000.0, but if < 0, engine sets to 0 (swkotor.exe: 0x00508c50 line 226-230, swkotor2.exe: 0x004e3ff0 line 228-232)
            // NOTE: Engine uses 10000.0 as default, but we use 0.0 to match PyKotor and avoid confusion
            are.MoonFogFar = root.Acquire<float>("MoonFogFar", 0.0f);
            // Matching Python: are.moon_fog_color = root.acquire("MoonFogColor", 0)
            are.MoonFogColorDeprecated = Color.FromRgbInteger(root.Acquire<int>("MoonFogColor", 0));
            // Matching Python: are.moon_shadows = root.acquire("MoonShadows", 0)
            are.MoonShadows = root.Acquire<int>("MoonShadows", 0) != 0;
            // Matching Python: are.is_night = root.acquire("IsNight", 0)
            are.IsNight = root.Acquire<int>("IsNight", 0) != 0;
            // Matching Python: are.lighting_scheme = root.acquire("LightingScheme", 0)
            are.LightingScheme = root.Acquire<int>("LightingScheme", 0);
            // Matching Python: are.day_night = root.acquire("DayNightCycle", 0)
            are.DayNightCycle = root.Acquire<int>("DayNightCycle", 0) != 0;
            // Matching Python: are.no_rest = root.acquire("NoRest", 0)
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x00508c50 line 261-263, swkotor2.exe: 0x004e3ff0 line 263-265)
            // For new ARE objects, default is false (0)
            are.NoRest = root.Acquire<int>("NoRest", 0) != 0;
            // Matching Python: are.no_hang_back = root.acquire("NoHangBack", 0)
            are.NoHangBack = root.Acquire<int>("NoHangBack", 0) != 0;
            // Matching Python: are.player_only = root.acquire("PlayerOnly", 0)
            are.PlayerOnly = root.Acquire<int>("PlayerOnly", 0) != 0;
            // Matching Python: are.player_vs_player = root.acquire("PlayerVsPlayer", 0)
            are.PlayerVsPlayer = root.Acquire<int>("PlayerVsPlayer", 0) != 0;
            // Note: Expansion_List is always written as empty list in dismantle_are, so we don't need to read it

            // Extract rooms list
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:514-521
            // Original: rooms_list = root.acquire("Rooms", GFFList())
            // Original: for room_struct in rooms_list: ... are.rooms.append(ARERoom(...))
            var roomsList = root.Acquire<GFFList>("Rooms", new GFFList());
            are.Rooms = new List<ARERoom>();
            foreach (GFFStruct roomStruct in roomsList)
            {
                // Room fields - all optional
                // Engine defaults verified from swkotor.exe: 0x00508c50, swkotor2.exe: 0x004e3ff0
                // Matching Python: ambient_scale = room_struct.acquire("AmbientScale", 0.0)
                // Engine default: 0.0 (swkotor.exe: 0x00508c50 line 327, swkotor2.exe: 0x004e3ff0 line 331)
                float ambientScale = roomStruct.Acquire<float>("AmbientScale", 0.0f);
                // Matching Python: env_audio = room_struct.acquire("EnvAudio", 0)
                // Engine default: 0 (swkotor.exe: 0x00508c50 line 325, swkotor2.exe: 0x004e3ff0 line 329)
                int envAudio = roomStruct.Acquire<int>("EnvAudio", 0);
                // Matching Python: room_name = room_struct.acquire("RoomName", "")
                // Engine default: "" (swkotor.exe: 0x00508c50 line 317-320, swkotor2.exe: 0x004e3ff0 line 321-324)
                string roomName = roomStruct.Acquire<string>("RoomName", "");
                // Matching Python: disable_weather = bool(room_struct.acquire("DisableWeather", 0))
                // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 335) - K2 only, not in K1
                bool disableWeather = roomStruct.Acquire<int>("DisableWeather", 0) != 0;
                // Matching Python: force_rating = room_struct.acquire("ForceRating", 0)
                // Engine default: 0 (swkotor2.exe: 0x004e3ff0 line 333) - K2 only, not in K1
                int forceRating = roomStruct.Acquire<int>("ForceRating", 0);
                // Matching Python: are.rooms.append(ARERoom(room_name, disable_weather, env_audio, force_rating, ambient_scale))
                are.Rooms.Add(new ARERoom(roomName, disableWeather, envAudio, forceRating, ambientScale));
            }

            return are;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:538-682
        // Original: def dismantle_are(are: ARE, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleAre(ARE are, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.ARE);
            var root = gff.Root;

            // Create Map struct
            var mapStruct = new GFFStruct();
            root.SetStruct("Map", mapStruct);
            // Matching Python: map_struct.set_int32("NorthAxis", are.north_axis.value)
            mapStruct.SetInt32("NorthAxis", (int)are.NorthAxis);
            // Matching Python: map_struct.set_int32("MapZoom", are.map_zoom)
            mapStruct.SetInt32("MapZoom", are.MapZoom);
            // Matching Python: map_struct.set_int32("MapResX", are.map_res_x)
            mapStruct.SetInt32("MapResX", are.MapResX);
            // Matching Python: map_struct.set_single("MapPt1X", map_pt1.x) and map_struct.set_single("MapPt1Y", map_pt1.y)
            mapStruct.SetSingle("MapPt1X", are.MapPoint1.X);
            mapStruct.SetSingle("MapPt1Y", are.MapPoint1.Y);
            // Matching Python: map_struct.set_single("MapPt2X", map_pt2.x) and map_struct.set_single("MapPt2Y", map_pt2.y)
            mapStruct.SetSingle("MapPt2X", are.MapPoint2.X);
            mapStruct.SetSingle("MapPt2Y", are.MapPoint2.Y);
            // Matching Python: map_struct.set_single("WorldPt1X", are.world_point_1.x) and map_struct.set_single("WorldPt1Y", are.world_point_1.y)
            mapStruct.SetSingle("WorldPt1X", are.WorldPoint1.X);
            mapStruct.SetSingle("WorldPt1Y", are.WorldPoint1.Y);
            // Matching Python: map_struct.set_single("WorldPt2X", are.world_point_2.x) and map_struct.set_single("WorldPt2Y", are.world_point_2.y)
            mapStruct.SetSingle("WorldPt2X", are.WorldPoint2.X);
            mapStruct.SetSingle("WorldPt2Y", are.WorldPoint2.Y);

            // Set basic fields - written for ALL game types (Odyssey, Aurora, Eclipse)
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:596-601
            root.SetString("Tag", are.Tag);
            root.SetLocString("Name", are.Name);
            root.SetString("Comments", are.Comment);
            root.SetSingle("AlphaTest", are.AlphaTest);
            root.SetInt32("CameraStyle", are.CameraStyle);
            root.SetResRef("DefaultEnvMap", are.DefaultEnvMap);
            // Matching Python: root.set_uint8("Unescapable", are.unescapable)
            root.SetUInt8("Unescapable", are.Unescapable ? (byte)1 : (byte)0);
            // Matching Python: root.set_uint8("DisableTransit", are.disable_transit)
            root.SetUInt8("DisableTransit", are.DisableTransit ? (byte)1 : (byte)0);
            // Matching Python: root.set_uint8("StealthXPEnabled", are.stealth_xp)
            root.SetUInt8("StealthXPEnabled", are.StealthXp ? (byte)1 : (byte)0);
            // Matching Python: root.set_uint32("StealthXPLoss", are.stealth_xp_loss)
            root.SetUInt32("StealthXPLoss", (uint)are.StealthXpLoss);
            // Matching Python: root.set_uint32("StealthXPMax", are.stealth_xp_max)
            root.SetUInt32("StealthXPMax", (uint)are.StealthXpMax);
            root.SetResRef("Grass_TexName", are.GrassTexture);
            root.SetSingle("Grass_Density", are.GrassDensity);
            root.SetSingle("Grass_QuadSize", are.GrassSize);
            root.SetSingle("Grass_Prob_LL", are.GrassProbLL);
            root.SetSingle("Grass_Prob_LR", are.GrassProbLR);
            root.SetSingle("Grass_Prob_UL", are.GrassProbUL);
            root.SetSingle("Grass_Prob_UR", are.GrassProbUR);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:593-594
            // Original: root.set_uint32("Grass_Ambient", are.grass_ambient.rgb_integer())
            root.SetUInt32("Grass_Ambient", (uint)are.GrassAmbient.ToRgbInteger());
            // Original: root.set_uint32("Grass_Diffuse", are.grass_diffuse.rgb_integer())
            root.SetUInt32("Grass_Diffuse", (uint)are.GrassDiffuse.ToRgbInteger());
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:639
            // Original: root.set_uint32("Grass_Emissive", are.grass_emissive.rgb_integer()) (KotOR 2 only)
            // K2-specific fields should only be written for K2 games (K2, K2_XBOX, K2_IOS, K2_ANDROID)
            // Aurora (NWN) and Eclipse engines use ARE files but don't have K2-specific fields
            // Using IsK2() extension method to properly detect all K2 variants
            if (game.IsK2())
            {
                root.SetUInt32("Grass_Emissive", (uint)are.GrassEmissive.ToRgbInteger());
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:635-652
                // Original: K2-specific fields (DirtyARGB, ChanceRain/Snow/Lightning, DirtySize/Formula/Func)
                // Note: These fields are only in K2, not in K1, Aurora, or Eclipse
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:640-642
                // Original: root.set_int32("ChanceRain", are.chance_rain)
                // Original: root.set_int32("ChanceSnow", are.chance_snow)
                // Original: root.set_int32("ChanceLightning", are.chance_lightning)
                root.SetInt32("ChanceRain", are.ChanceRain);
                root.SetInt32("ChanceSnow", are.ChanceSnow);
                root.SetInt32("ChanceLightning", are.ChanceLightning);
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:636-652
                // Original: root.set_int32("DirtyARGBOne", are.dirty_argb_1.rgb_integer())
                // Original: root.set_int32("DirtyARGBTwo", are.dirty_argb_2.rgb_integer())
                // Original: root.set_int32("DirtyARGBThree", are.dirty_argb_3.rgb_integer())
                // Original: root.set_int32("DirtySizeOne", are.dirty_size_1)
                // Original: root.set_int32("DirtyFuncOne", are.dirty_func_1)
                // Original: root.set_int32("DirtySizeTwo", are.dirty_size_2)
                // Original: root.set_int32("DirtyFuncTwo", are.dirty_func_2)
                // Original: root.set_int32("DirtySizeThree", are.dirty_size_3)
                // Original: root.set_int32("DirtyFuncThree", are.dirty_func_3)
                // K2-specific dirty ARGB, Size, and Func fields (KotOR 2 Only)
                root.SetInt32("DirtyARGBOne", (int)are.DirtyArgb1.ToRgbInteger());
                root.SetInt32("DirtyARGBTwo", (int)are.DirtyArgb2.ToRgbInteger());
                root.SetInt32("DirtyARGBThree", (int)are.DirtyArgb3.ToRgbInteger());
                root.SetInt32("DirtySizeOne", are.DirtySize1);
                root.SetInt32("DirtyFuncOne", are.DirtyFunc1);
                root.SetInt32("DirtySizeTwo", are.DirtySize2);
                root.SetInt32("DirtyFuncTwo", are.DirtyFunc2);
                root.SetInt32("DirtySizeThree", are.DirtySize3);
                root.SetInt32("DirtyFuncThree", are.DirtyFunc3);
                // Dirty formula fields
                root.SetInt32("DirtyFormulaOne", are.DirtyFormula1);
                root.SetInt32("DirtyFormulaTwo", are.DirtyFormula2);
                root.SetInt32("DirtyFormulaThre", are.DirtyFormula3);
            }
            // Set fog and lighting fields - written for ALL game types
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:609-614
            root.SetUInt8("SunFogOn", are.FogEnabled ? (byte)1 : (byte)0);
            root.SetSingle("SunFogNear", are.FogNear);
            root.SetSingle("SunFogFar", are.FogFar);
            root.SetInt32("WindPower", are.WindPower);
            // ShadowOpacity is UInt8 (0-255) in GFF format
            // Engine reads as UInt8 (swkotor.exe: 0x00508c50, swkotor2.exe: 0x004e3ff0)
            // Aurora uses 0-100 range, Eclipse uses 0-255 range
            root.SetUInt8("ShadowOpacity", are.ShadowOpacity);

            // Set script hooks - written for ALL game types
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:620-623
            root.SetResRef("OnEnter", are.OnEnter);
            root.SetResRef("OnExit", are.OnExit);
            root.SetResRef("OnHeartbeat", are.OnHeartbeat);
            root.SetResRef("OnUserDefined", are.OnUserDefined);

            // Set rooms list - written for ALL game types, but with K2-specific fields conditionally
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:625-633
            // Original: rooms_list = root.set_list("Rooms", GFFList())
            // Original: for room in are.rooms: ... room_struct.set_*("...", room.*)
            var roomsList = new GFFList();
            root.SetList("Rooms", roomsList);
            foreach (var room in are.Rooms)
            {
                // Matching Python: room_struct = rooms_list.add(0)
                var roomStruct = roomsList.Add(0);
                // Matching Python: room_struct.set_single("AmbientScale", room.ambient_scale)
                roomStruct.SetSingle("AmbientScale", room.AmbientScale);
                // Matching Python: room_struct.set_int32("EnvAudio", room.env_audio)
                roomStruct.SetInt32("EnvAudio", room.EnvAudio);
                // Matching Python: room_struct.set_string("RoomName", room.name)
                roomStruct.SetString("RoomName", room.Name);
                // Matching Python: if game.is_k2(): ... room_struct.set_uint8("DisableWeather", room.weather) ... room_struct.set_int32("ForceRating", room.force_rating)
                // K2-specific fields should only be written for K2 games (K2, K2_XBOX, K2_IOS, K2_ANDROID)
                // Aurora (NWN) and Eclipse engines use ARE files but don't have K2-specific fields
                if (game.IsK2())
                {
                    roomStruct.SetUInt8("DisableWeather", room.Weather ? (byte)1 : (byte)0);
                    roomStruct.SetInt32("ForceRating", room.ForceRating);
                }
            }

            // Set load screen ID - written for ALL game types
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:673
            // Original: root.set_uint16("LoadScreenID", are.loadscreen_id)
            root.SetUInt16("LoadScreenID", (ushort)are.LoadScreenID);

            // Set color fields (as RGB integers) - written for ALL game types
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:589-592
            // Original: root.set_uint32("SunAmbientColor", are.sun_ambient.rgb_integer())
            root.SetUInt32("SunAmbientColor", (uint)are.SunAmbient.ToRgbInteger());
            // Original: root.set_uint32("SunDiffuseColor", are.sun_diffuse.rgb_integer())
            root.SetUInt32("SunDiffuseColor", (uint)are.SunDiffuse.ToRgbInteger());
            // Original: root.set_uint32("DynAmbientColor", are.dynamic_light.rgb_integer())
            root.SetUInt32("DynAmbientColor", (uint)are.DynamicLight.ToRgbInteger());
            // Original: root.set_uint32("SunFogColor", are.fog_color.rgb_integer())
            root.SetUInt32("SunFogColor", (uint)are.FogColor.ToRgbInteger());

            // Set deprecated fields - only written when useDeprecated is true
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:654-680
            // Original: if use_deprecated:
            // Note: These fields are toolset-only and not used by game engines
            // They are preserved for compatibility with existing ARE files
            // Reference: vendor/reone/src/libs/resource/parser/gff/are.cpp:308,325,336,338-339,348-365
            if (useDeprecated)
            {
                // Matching Python: root.set_int32("ID", are.unused_id)
                root.SetInt32("ID", are.UnusedId);
                // Matching Python: root.set_int32("Creator_ID", are.creator_id)
                root.SetInt32("Creator_ID", are.CreatorId);
                // Matching Python: root.set_uint32("Flags", are.flags)
                root.SetUInt32("Flags", are.Flags);
                // Matching Python: root.set_int32("ModSpotCheck", are.mod_spot_check)
                root.SetInt32("ModSpotCheck", are.ModSpotCheck);
                // Matching Python: root.set_int32("ModListenCheck", are.mod_listen_check)
                root.SetInt32("ModListenCheck", are.ModListenCheck);
                // Matching Python: root.set_uint32("MoonAmbientColor", are.moon_ambient)
                root.SetUInt32("MoonAmbientColor", (uint)are.MoonAmbient.ToRgbInteger());
                // Matching Python: root.set_uint32("MoonDiffuseColor", are.moon_diffuse)
                root.SetUInt32("MoonDiffuseColor", (uint)are.MoonDiffuse.ToRgbInteger());
                // Matching Python: root.set_uint8("MoonFogOn", are.moon_fog)
                root.SetUInt8("MoonFogOn", are.MoonFog ? (byte)1 : (byte)0);
                // Matching Python: root.set_single("MoonFogNear", moon_fog_near)
                root.SetSingle("MoonFogNear", are.MoonFogNear);
                // Matching Python: root.set_single("MoonFogFar", moon_fog_far)
                root.SetSingle("MoonFogFar", are.MoonFogFar);
                // Matching Python: root.set_uint32("MoonFogColor", are.moon_fog_ParsingColor)
                root.SetUInt32("MoonFogColor", (uint)are.MoonFogColorDeprecated.ToRgbInteger());
                // Matching Python: root.set_uint8("MoonShadows", are.moon_shadows)
                root.SetUInt8("MoonShadows", are.MoonShadows ? (byte)1 : (byte)0);
                // Matching Python: root.set_uint8("IsNight", are.is_night)
                root.SetUInt8("IsNight", are.IsNight ? (byte)1 : (byte)0);
                // Matching Python: root.set_uint8("LightingScheme", are.lighting_scheme)
                root.SetUInt8("LightingScheme", (byte)are.LightingScheme);
                // Matching Python: root.set_uint8("DayNightCycle", are.day_night)
                root.SetUInt8("DayNightCycle", are.DayNightCycle ? (byte)1 : (byte)0);
                // Matching Python: root.set_uint8("NoRest", are.no_rest)
                root.SetUInt8("NoRest", are.NoRest ? (byte)1 : (byte)0);
                // Matching Python: root.set_uint8("NoHangBack", are.no_hang_back)
                root.SetUInt8("NoHangBack", are.NoHangBack ? (byte)1 : (byte)0);
                // Matching Python: root.set_uint8("PlayerOnly", are.player_only)
                root.SetUInt8("PlayerOnly", are.PlayerOnly ? (byte)1 : (byte)0);
                // Matching Python: root.set_uint8("PlayerVsPlayer", player_vs_player)
                root.SetUInt8("PlayerVsPlayer", are.PlayerVsPlayer ? (byte)1 : (byte)0);
                // Matching Python: root.set_list("Expansion_List", GFFList()) - always empty list for compatibility
                var expansionList = new GFFList();
                root.SetList("Expansion_List", expansionList);
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:685-700
        // Original: def read_are(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> ARE:
        public static ARE ReadAre(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructAre(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:742-757
        // Original: def bytes_are(are: ARE, game: Game = BioWareGame.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesAre(ARE are, BioWareGame game = BioWareGame.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.ARE;
            }
            GFF gff = DismantleAre(are, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}

