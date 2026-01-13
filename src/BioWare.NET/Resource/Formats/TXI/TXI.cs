using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BioWare.NET.Resource.Formats.TXI
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:56-359
    // Original: class TXI
    public class TXI
    {
        public TXIFeatures Features { get; set; }
        private bool _empty;

        public TXI(string txi = null)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:57-61
            // Original: def __init__(self, txi: str | None = None)
            Features = new TXIFeatures();
            _empty = true;
            if (!string.IsNullOrWhiteSpace(txi))
            {
                Load(txi);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:63-324
        // Original: def load(self, txi: str)
        public void Load(string txi)
        {
            _empty = true;
            TXIReaderMode mode = TXIReaderMode.Normal;
            int curCoords = 0;
            int maxCoords = 0;

            string[] lines = txi.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                try
                {
                    string parsedLine = line.Trim();
                    if (string.IsNullOrEmpty(parsedLine))
                    {
                        continue;
                    }

                    if (mode == TXIReaderMode.UpperLeftCoords)
                    {
                        string[] parts = parsedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            Tuple<float, float, int> coords = Tuple.Create(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                int.Parse(parts[2].Trim())
                            );
                            if (Features.Upperleftcoords == null)
                            {
                                Features.Upperleftcoords = new List<Tuple<float, float, int>>();
                            }
                            Features.Upperleftcoords.Add(coords);
                            curCoords++;
                            if (curCoords >= maxCoords)
                            {
                                mode = TXIReaderMode.Normal;
                                curCoords = 0;
                            }
                        }
                        continue;
                    }

                    if (mode == TXIReaderMode.LowerRightCoords)
                    {
                        string[] parts = parsedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            Tuple<float, float, int> coords = Tuple.Create(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                int.Parse(parts[2].Trim())
                            );
                            if (Features.Lowerrightcoords == null)
                            {
                                Features.Lowerrightcoords = new List<Tuple<float, float, int>>();
                            }
                            Features.Lowerrightcoords.Add(coords);
                            curCoords++;
                            if (curCoords >= maxCoords)
                            {
                                mode = TXIReaderMode.Normal;
                            }
                        }
                        continue;
                    }

                    string rawCmd;
                    string args;
                    int spaceIndex = parsedLine.IndexOf(' ');
                    if (spaceIndex >= 0)
                    {
                        rawCmd = parsedLine.Substring(0, spaceIndex);
                        args = parsedLine.Substring(spaceIndex + 1);
                    }
                    else
                    {
                        rawCmd = parsedLine;
                        args = "";
                    }

                    string parsedCmdStr = rawCmd.Trim().ToUpperInvariant();
                    if (parsedCmdStr == "DECAL1")
                    {
                        parsedCmdStr = "DECAL";
                        args = "1";
                    }

                    TXICommand? command = TXICommandExtensions.FromString(parsedCmdStr);
                    if (!command.HasValue)
                    {
                        // Invalid command, skip
                        continue;
                    }

                    args = args.Trim();

                    // Handle all commands
                    switch (command.Value)
                    {
                        case TXICommand.Alphamean:
                            Features.Alphamean = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Arturoheight:
                            Features.Arturoheight = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Arturowidth:
                            Features.Arturowidth = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Baselineheight:
                            Features.Baselineheight = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Blending:
                            Features.Blending = ParseBlending(args);
                            _empty = false;
                            break;
                        case TXICommand.Bumpmapscaling:
                            Features.Bumpmapscaling = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Bumpmaptexture:
                            Features.Bumpmaptexture = args;
                            _empty = false;
                            break;
                        case TXICommand.Bumpyshinytexture:
                            Features.Bumpyshinytexture = args;
                            _empty = false;
                            break;
                        case TXICommand.Candownsample:
                            Features.Candownsample = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Caretindent:
                            Features.Caretindent = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Channelscale:
                            Features.Channelscale = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToList();
                            _empty = false;
                            break;
                        case TXICommand.Channeltranslate:
                            Features.Channeltranslate = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToList();
                            _empty = false;
                            break;
                        case TXICommand.Clamp:
                            Features.Clamp = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Codepage:
                            Features.Codepage = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Cols:
                            Features.Cols = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Compresstexture:
                            Features.Compresstexture = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Controllerscript:
                            Features.Controllerscript = args;
                            _empty = false;
                            break;
                        case TXICommand.Cube:
                            Features.Cube = string.IsNullOrEmpty(args) ? true : (int.Parse(args, CultureInfo.InvariantCulture) != 0);
                            _empty = false;
                            break;
                        case TXICommand.Decal:
                            Features.Decal = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Defaultbpp:
                            Features.Defaultbpp = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Defaultheight:
                            Features.Defaultheight = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Defaultwidth:
                            Features.Defaultwidth = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Distort:
                            Features.Distort = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Distortangle:
                            Features.Distortangle = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Distortionamplitude:
                            Features.Distortionamplitude = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Downsamplefactor:
                            Features.Downsamplefactor = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Downsamplemax:
                            Features.Downsamplemax = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Downsamplemin:
                            Features.Downsamplemin = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Envmaptexture:
                            Features.Envmaptexture = args;
                            _empty = false;
                            break;
                        case TXICommand.Filerange:
                            Features.Filerange = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToList();
                            _empty = false;
                            break;
                        case TXICommand.Filter:
                            Features.Filter = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Fontheight:
                            Features.Fontheight = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Fontwidth:
                            Features.Fontwidth = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Fps:
                            Features.Fps = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Isbumpmap:
                            Features.Isbumpmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Isdiffusebumpmap:
                            Features.Isdiffusebumpmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Islightmap:
                            Features.Islightmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Isspecularbumpmap:
                            Features.Isspecularbumpmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Lowerrightcoords:
                            if (string.IsNullOrEmpty(args))
                            {
                                continue;
                            }
                            curCoords = 0;
                            maxCoords = int.Parse(args, CultureInfo.InvariantCulture);
                            mode = TXIReaderMode.LowerRightCoords;
                            Features.Lowerrightcoords = new List<Tuple<float, float, int>>();
                            _empty = false;
                            break;
                        case TXICommand.MaxSizeHQ:
                            Features.MaxSizeHQ = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.MaxSizeLQ:
                            Features.MaxSizeLQ = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.MinSizeHQ:
                            Features.MinSizeHQ = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.MinSizeLQ:
                            Features.MinSizeLQ = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Mipmap:
                            Features.Mipmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Numchars:
                            Features.Numchars = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Numcharspersheet:
                            Features.Numcharspersheet = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Numx:
                            Features.Numx = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Numy:
                            Features.Numy = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Ondemand:
                            Features.Ondemand = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Priority:
                            Features.Priority = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Proceduretype:
                            Features.Proceduretype = args;
                            _empty = false;
                            break;
                        case TXICommand.Rows:
                            Features.Rows = int.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.SpacingB:
                            Features.SpacingB = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.SpacingR:
                            Features.SpacingR = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Speed:
                            Features.Speed = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Temporary:
                            Features.Temporary = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Texturewidth:
                            Features.Texturewidth = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Unique:
                            Features.Unique = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                        case TXICommand.Upperleftcoords:
                            if (string.IsNullOrEmpty(args))
                            {
                                continue;
                            }
                            curCoords = 0;
                            maxCoords = int.Parse(args, CultureInfo.InvariantCulture);
                            mode = TXIReaderMode.UpperLeftCoords;
                            Features.Upperleftcoords = new List<Tuple<float, float, int>>();
                            _empty = false;
                            break;
                        case TXICommand.Wateralpha:
                            Features.Wateralpha = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Waterheight:
                            Features.Waterheight = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Waterwidth:
                            Features.Waterwidth = float.Parse(args, CultureInfo.InvariantCulture);
                            _empty = false;
                            break;
                        case TXICommand.Xbox_downsample:
                            Features.Xbox_downsample = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                            _empty = false;
                            break;
                    }
                }
                catch (Exception)
                {
                    // Invalid TXI line, skip
                    continue;
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:326-330
        // Original: def empty(self) -> bool, def get_features(self) -> TXIFeatures
        public bool Empty()
        {
            return _empty;
        }

        public TXIFeatures GetFeatures()
        {
            return Features;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:332-334
        // Original: @staticmethod def parse_blending(s: str) -> int
        public static bool ParseBlending(string s)
        {
            string lower = s.ToLowerInvariant();
            return (lower == "default" || lower == "additive" || lower == "punchthrough");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_data.py:336-358
        // Original: def __str__(self) -> str
        public override string ToString()
        {
            List<string> lines = new List<string>();
            var properties = typeof(TXIFeatures).GetProperties();
            foreach (var prop in properties)
            {
                object value = prop.GetValue(Features);
                if (value == null)
                {
                    continue;
                }

                string attr = prop.Name;
                string upperAttr = attr.ToUpperInvariant();
                TXICommand? command = TXICommandExtensions.FromString(upperAttr);
                if (!command.HasValue)
                {
                    continue;
                }

                if (value is bool boolValue)
                {
                    lines.Add($"{command.Value.GetValue()} {(boolValue ? 1 : 0)}");
                }
                else if (value is int || value is float || value is double)
                {
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}", command.Value.GetValue(), value));
                }
                else if (value is List<Tuple<float, float, int>> coordList)
                {
                    if (attr.ToLowerInvariant() == "upperleftcoords" || attr.ToLowerInvariant() == "lowerrightcoords")
                    {
                        lines.Add($"{command.Value.GetValue()} {coordList.Count}");
                        foreach (var coord in coordList)
                        {
                            lines.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", coord.Item1, coord.Item2, coord.Item3));
                        }
                    }
                }
                else if (value is List<float> floatList)
                {
                    lines.Add($"{command.Value.GetValue()} {string.Join(" ", floatList.Select(v => v.ToString(CultureInfo.InvariantCulture)))}");
                }
                else if (value is List<int> intList)
                {
                    lines.Add($"{command.Value.GetValue()} {string.Join(" ", intList.Select(v => v.ToString(CultureInfo.InvariantCulture)))}");
                }
                else if (value is string stringValue)
                {
                    lines.Add($"{command.Value.GetValue()} {stringValue}");
                }
            }
            return string.Join("\n", lines);
        }
    }
}

