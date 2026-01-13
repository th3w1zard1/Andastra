using System;
using System.Collections.Generic;

namespace BioWare.NET.Resource.Formats.TXI
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:361-745
    // Original: class TXIFeatures
    public class TXIFeatures
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:565-734
        // Original: def __init__(self)
        public TXIFeatures()
        {
            // All fields initialized to null (None in Python)
        }

        // Main texture properties
        public bool? Blending { get; set; }
        public bool? Mipmap { get; set; }
        public bool? Filter { get; set; }
        public bool? Decal { get; set; }
        public bool? Cube { get; set; }

        // Companion textures
        public string Bumpmaptexture { get; set; }
        public string Bumpyshinytexture { get; set; }
        public string Envmaptexture { get; set; }
        public float? Bumpmapscaling { get; set; }

        // Water properties
        public float? Wateralpha { get; set; }
        public float? Waterheight { get; set; }
        public float? Waterwidth { get; set; }

        // Animation properties
        public string Proceduretype { get; set; }
        public int? Numx { get; set; }
        public int? Numy { get; set; }
        public float? Fps { get; set; }
        public float? Speed { get; set; }

        // Font properties
        public int? Numchars { get; set; }
        public float? Fontheight { get; set; }
        public float? Fontwidth { get; set; }
        public float? Baselineheight { get; set; }
        public float? Texturewidth { get; set; }
        public float? SpacingR { get; set; }
        public float? SpacingB { get; set; }
        public float? Caretindent { get; set; }
        public List<Tuple<float, float, int>> Upperleftcoords { get; set; }
        public List<Tuple<float, float, int>> Lowerrightcoords { get; set; }

        // Additional properties
        public float? Alphamean { get; set; }
        public int? Arturoheight { get; set; }
        public int? Arturowidth { get; set; }
        public bool? Candownsample { get; set; }
        public List<float> Channelscale { get; set; }
        public List<float> Channeltranslate { get; set; }
        public bool? Clamp { get; set; }
        public int? Codepage { get; set; }
        public int? Cols { get; set; }
        public bool? Compresstexture { get; set; }
        public string Controllerscript { get; set; }
        public int? Defaultbpp { get; set; }
        public int? Defaultheight { get; set; }
        public int? Defaultwidth { get; set; }
        public bool? Distort { get; set; }
        public float? Distortangle { get; set; }
        public float? Distortionamplitude { get; set; }
        public float? Downsamplefactor { get; set; }
        public int? Downsamplemax { get; set; }
        public int? Downsamplemin { get; set; }
        public List<int> Filerange { get; set; }
        public bool? Isbumpmap { get; set; }
        public bool? Isdiffusebumpmap { get; set; }
        public bool? Islightmap { get; set; }
        public bool? Isspecularbumpmap { get; set; }
        public int? MaxSizeHQ { get; set; }
        public int? MaxSizeLQ { get; set; }
        public int? MinSizeHQ { get; set; }
        public int? MinSizeLQ { get; set; }
        public int? Numcharspersheet { get; set; }
        public bool? Ondemand { get; set; }
        public int? Priority { get; set; }
        public int? Rows { get; set; }
        public bool? Temporary { get; set; }
        public bool? Unique { get; set; }
        public bool? Xbox_downsample { get; set; }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:736-745
        // Original: @property def is_flipbook(self) -> bool
        public bool IsFlipbook
        {
            get
            {
                return !string.IsNullOrEmpty(Proceduretype) &&
                       Proceduretype.ToLowerInvariant() == "cycle" &&
                       Numx.HasValue && Numx.Value != 0 &&
                       Numy.HasValue && Numy.Value != 0 &&
                       Fps.HasValue && Fps.Value != 0;
            }
        }
    }
}

