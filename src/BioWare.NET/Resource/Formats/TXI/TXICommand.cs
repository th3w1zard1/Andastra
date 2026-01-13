namespace BioWare.NET.Resource.Formats.TXI
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:748-813
    // Original: class TXICommand(Enum)
    public enum TXICommand
    {
        Alphamean,
        Arturoheight,
        Arturowidth,
        Baselineheight,
        Blending,
        Bumpmapscaling,
        Bumpmaptexture,
        Bumpyshinytexture,
        Candownsample,
        Caretindent,
        Channelscale,
        Channeltranslate,
        Clamp,
        Codepage,
        Cols,
        Compresstexture,
        Controllerscript,
        Cube,
        Decal,
        Defaultbpp,
        Defaultheight,
        Defaultwidth,
        Distort,
        Distortangle,
        Distortionamplitude,
        Downsamplefactor,
        Downsamplemax,
        Downsamplemin,
        Envmaptexture,
        Filerange,
        Filter,
        Fontheight,
        Fontwidth,
        Fps,
        Isbumpmap,
        Isdiffusebumpmap,
        Islightmap,
        Isspecularbumpmap,
        Lowerrightcoords,
        MaxSizeHQ,
        MaxSizeLQ,
        MinSizeHQ,
        MinSizeLQ,
        Mipmap,
        Numchars,
        Numcharspersheet,
        Numx,
        Numy,
        Ondemand,
        Priority,
        Proceduretype,
        Rows,
        SpacingB,
        SpacingR,
        Speed,
        Temporary,
        Texturewidth,
        Unique,
        Upperleftcoords,
        Wateralpha,
        Waterheight,
        Waterwidth,
        Xbox_downsample
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:748-813
    // Original: TXICommand enum values
    public static class TXICommandExtensions
    {
        public static string GetValue(this TXICommand command)
        {
            return command.ToString().ToLowerInvariant();
        }

        public static TXICommand? FromString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            string upper = value.Trim().ToUpperInvariant();
            if (upper == "DECAL1")
            {
                return TXICommand.Decal;
            }
            if (System.Enum.TryParse<TXICommand>(upper, true, out TXICommand result))
            {
                return result;
            }
            return null;
        }
    }
}

