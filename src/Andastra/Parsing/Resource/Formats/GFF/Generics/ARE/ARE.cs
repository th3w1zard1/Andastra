using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Generics.ARE
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
        public int AlphaTest { get; set; }
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
        public ResRef ShadowOpacity { get; set; } = ResRef.FromBlank();

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

