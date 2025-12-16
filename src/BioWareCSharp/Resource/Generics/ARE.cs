using System.Collections.Generic;
using BioWareCSharp.Common;
using BioWareCSharp.Common.Resources;
using JetBrains.Annotations;

namespace BioWareCSharp.Common.Resource.Generics
{
    /// <summary>
    /// Stores static area data.
    ///
    /// ARE files are GFF-based format files that store static area information including
    /// lighting, fog, grass, weather, script hooks, and map data.
    /// </summary>
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
        public ResRef GrassTexture { get; set; } = ResRef.FromBlank();
        public float GrassDensity { get; set; }
        public float GrassSize { get; set; }
        public float GrassProbLL { get; set; }
        public float GrassProbLR { get; set; }
        public float GrassProbUL { get; set; }
        public float GrassProbUR { get; set; }
        public bool FogEnabled { get; set; }
        public float FogNear { get; set; }
        public float FogFar { get; set; }
        public Color FogColor { get; set; } = new Color(0, 0, 0);
        public bool SunFogEnabled { get; set; }
        public float SunFogNear { get; set; }
        public float SunFogFar { get; set; }
        public Color SunFogColor { get; set; } = new Color(0, 0, 0);
        public int WindPower { get; set; }
        public ResRef ShadowOpacity { get; set; } = ResRef.FromBlank();
        public ResRef ChancesOfRain { get; set; } = ResRef.FromBlank();
        public ResRef ChancesOfSnow { get; set; } = ResRef.FromBlank();
        public ResRef ChancesOfLightning { get; set; } = ResRef.FromBlank();
        public ResRef ChancesOfFog { get; set; } = ResRef.FromBlank();
        public int Weather { get; set; }
        public int SkyBox { get; set; }
        public int MoonAmbient { get; set; }
        public int DawnAmbient { get; set; }
        public int DayAmbient { get; set; }
        public int DuskAmbient { get; set; }
        public int NightAmbient { get; set; }
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
        /// Load screen ID (index into loadscreens.2da).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:104,293
        /// Original: loadscreen_id: "LoadScreenID" field. Not used by the game engine.
        /// </remarks>
        public int LoadScreenID { get; set; }

        public ARE()
        {
        }
    }
}
