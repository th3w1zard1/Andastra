using System.Collections.Generic;
using System.Numerics;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics.ARE
{
    /// <summary>
    /// Area Resource (ARE) file handler.
    /// 
    /// ARE files are GFF-based format files that store static area information including
    /// lighting, fog, weather, grass properties, map configuration, and script hooks.
    /// Used by all engines (Odyssey/KOTOR, Aurora/NWN, Eclipse/DA/ME).
    /// </summary>
    /// <remarks>
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py
    /// Original: class ARE:
    /// </remarks>
    [PublicAPI]
    public sealed class ARE
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:41
        // Original: BINARY_TYPE = ResourceType.ARE
        public static readonly ResourceType BinaryType = ResourceType.ARE;

        // Map configuration
        public ARENorthAxis NorthAxis { get; set; }
        public int MapZoom { get; set; }
        public int MapResX { get; set; }
        public Vector2 MapPoint1 { get; set; }
        public Vector2 MapPoint2 { get; set; }
        public Vector2 WorldPoint1 { get; set; }
        public Vector2 WorldPoint2 { get; set; }
        public List<ResRef> MapList { get; set; } = new List<ResRef>();

        // Basic fields
        public string Tag { get; set; } = string.Empty;
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        // Engine reads and stores AlphaTest as float (swkotor.exe: 0x00508c50 line 303-304, swkotor2.exe: 0x004e3ff0 line 307-308)
        // Default value: 0.2 (verified from engine behavior)
        // Line 303-304 in swkotor.exe: fVar14 = FUN_00411d00(..., "AlphaTest", ..., 0.2); *(float *)((int)this + 0xfc) = (float)fVar14;
        // Line 307-308 in swkotor2.exe: fVar14 = FUN_00412e20(..., "AlphaTest", ..., 0.2); *(float *)((int)this + 0x100) = (float)fVar14;
        public float AlphaTest { get; set; } = 0.2f;
        public int CameraStyle { get; set; }
        public ResRef DefaultEnvMap { get; set; } = ResRef.FromBlank();
        public bool Unescapable { get; set; }
        public bool DisableTransit { get; set; }

        // Grass properties
        public ResRef GrassTexture { get; set; } = ResRef.FromBlank();
        public float GrassDensity { get; set; }
        public float GrassSize { get; set; }
        public float GrassProbLL { get; set; }
        public float GrassProbLR { get; set; }
        public float GrassProbUL { get; set; }
        public float GrassProbUR { get; set; }
        public Color GrassAmbient { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color GrassDiffuse { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color GrassEmissive { get; set; } = new Color(0.0f, 0.0f, 0.0f);

        // Fog and lighting
        public bool FogEnabled { get; set; }
        public float FogNear { get; set; }
        public float FogFar { get; set; }
        public int WindPower { get; set; }
        // Shadow opacity: 0-255 (0 = no shadows, 255 = fully opaque shadows)
        // Engine reads as UInt8 from GFF (swkotor.exe: 0x00508c50, swkotor2.exe: 0x004e3ff0)
        // Aurora uses 0-100 range, Eclipse uses 0-255 range
        public byte ShadowOpacity { get; set; } = 0;

        // Weather (K2-specific)
        public int ChanceRain { get; set; }
        public int ChanceSnow { get; set; }
        public int ChanceLightning { get; set; }

        // Script hooks
        public ResRef OnEnter { get; set; } = ResRef.FromBlank();
        public ResRef OnExit { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined { get; set; } = ResRef.FromBlank();

        // Stealth XP
        public bool StealthXp { get; set; }
        public int StealthXpLoss { get; set; }
        public int StealthXpMax { get; set; }

        // Load screen
        public int LoadScreenID { get; set; }

        // Color fields
        public Color SunAmbient { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color SunDiffuse { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DynamicLight { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color FogColor { get; set; } = new Color(0.0f, 0.0f, 0.0f);

        // K2-specific dirty formula fields
        public int DirtyFormula1 { get; set; }
        public int DirtyFormula2 { get; set; }
        public int DirtyFormula3 { get; set; }
        public int DirtySize1 { get; set; }
        public int DirtySize2 { get; set; }
        public int DirtySize3 { get; set; }
        public int DirtyFunc1 { get; set; }
        public int DirtyFunc2 { get; set; }
        public int DirtyFunc3 { get; set; }
        public Color DirtyArgb1 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DirtyArgb2 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DirtyArgb3 { get; set; } = new Color(0.0f, 0.0f, 0.0f);

        // Sun fog properties (aliases for Fog properties for editor compatibility)
        public bool SunFogEnabled
        {
            get => FogEnabled;
            set => FogEnabled = value;
        }
        public float SunFogNear
        {
            get => FogNear;
            set => FogNear = value;
        }
        public float SunFogFar
        {
            get => FogFar;
            set => FogFar = value;
        }
        public Color SunFogColor
        {
            get => FogColor;
            set => FogColor = value;
        }

        // Toolset-only/deprecated properties (for editor compatibility)
        public int ChancesOfFog { get; set; }
        public int Weather { get; set; }
        public string SkyBox { get; set; } = string.Empty;
        public Color DawnAmbient { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DayAmbient { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DuskAmbient { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color NightAmbient { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public float DawnDir1 { get; set; }
        public float DawnDir2 { get; set; }
        public float DawnDir3 { get; set; }
        public float DayDir1 { get; set; }
        public float DayDir2 { get; set; }
        public float DayDir3 { get; set; }
        public float DuskDir1 { get; set; }
        public float DuskDir2 { get; set; }
        public float DuskDir3 { get; set; }
        public float NightDir1 { get; set; }
        public float NightDir2 { get; set; }
        public float NightDir3 { get; set; }
        public Color DawnColor1 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DawnColor2 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DawnColor3 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DayColor1 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DayColor2 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DayColor3 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DuskColor1 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DuskColor2 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color DuskColor3 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color NightColor1 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color NightColor2 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color NightColor3 { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public ResRef OnEnter2 { get; set; } = ResRef.FromBlank();
        public ResRef OnExit2 { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat2 { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined2 { get; set; } = ResRef.FromBlank();
        public List<string> AreaList { get; set; } = new List<string>();

        // Comments
        public string Comment { get; set; } = string.Empty;

        // Deprecated fields (toolset-only, not used by game engines)
        public int UnusedId { get; set; }
        public int CreatorId { get; set; }
        public uint Flags { get; set; }
        public int ModSpotCheck { get; set; }
        public int ModListenCheck { get; set; }
        public Color MoonAmbient { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public Color MoonDiffuse { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public bool MoonFog { get; set; }
        public float MoonFogNear { get; set; }
        public float MoonFogFar { get; set; }
        public Color MoonFogColorDeprecated { get; set; } = new Color(0.0f, 0.0f, 0.0f);
        public bool MoonShadows { get; set; }
        public bool IsNight { get; set; }
        public int LightingScheme { get; set; }
        public bool DayNightCycle { get; set; }
        public bool NoRest { get; set; }
        public bool NoHangBack { get; set; }
        public bool PlayerOnly { get; set; }
        public bool PlayerVsPlayer { get; set; }

        // Rooms list
        public List<ARERoom> Rooms { get; set; } = new List<ARERoom>();

        public ARE()
        {
        }
    }
}

