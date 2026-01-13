using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BioWare.NET.Resource.Formats.TXI
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/io_txi.py:23-313
    // Original: class TXIBinaryReader(ResourceReader)
    public class TXIBinaryReader : IDisposable
    {
        private readonly BioWare.NET.Common.RawBinaryReader _reader;
        private TXI _txi;

        public TXIBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromBytes(data, offset, sizeNullable);
            _txi = new TXI();
        }

        public TXIBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromFile(filepath, offset, sizeNullable);
            _txi = new TXI();
        }

        public TXIBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromStream(source, offset, sizeNullable);
            _txi = new TXI();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/io_txi.py:40-313
        // Original: def load(self, *func_args, auto_close: bool = True, **func_kwargs) -> TXI
        public TXI Load(bool autoClose = true)
        {
            try
            {
                _txi.Features = new TXIFeatures();
                bool empty = true;
                TXIReaderMode mode = TXIReaderMode.Normal;
                int curCoords = 0;

                byte[] txiBytes = _reader.ReadAll();
                string text = Encoding.ASCII.GetString(txiBytes);
                string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                foreach (string line in lines)
                {
                    try
                    {
                        string parsedLine = line.Trim();
                        if (string.IsNullOrEmpty(parsedLine))
                        {
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
                        bool isCommand = TXICommandExtensions.FromString(parsedCmdStr).HasValue;

                        if (mode == TXIReaderMode.UpperLeftCoords)
                        {
                            if (isCommand)
                            {
                                mode = TXIReaderMode.Normal;
                            }
                            else
                            {
                                try
                                {
                                    string[] parts = parsedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length >= 3)
                                    {
                                        Tuple<float, float, int> coords = Tuple.Create(
                                            float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                                            float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                                            int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture)
                                        );
                                        if (_txi.Features.Upperleftcoords != null && curCoords < _txi.Features.Upperleftcoords.Count)
                                        {
                                            _txi.Features.Upperleftcoords[curCoords] = coords;
                                        }
                                        curCoords++;
                                        if (_txi.Features.Upperleftcoords != null && curCoords >= _txi.Features.Upperleftcoords.Count)
                                        {
                                            mode = TXIReaderMode.Normal;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    mode = TXIReaderMode.Normal;
                                }
                                continue;
                            }
                        }

                        if (mode == TXIReaderMode.LowerRightCoords)
                        {
                            if (isCommand)
                            {
                                mode = TXIReaderMode.Normal;
                            }
                            else
                            {
                                try
                                {
                                    string[] parts = parsedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length >= 3)
                                    {
                                        Tuple<float, float, int> coords = Tuple.Create(
                                            float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                                            float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                                            int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture)
                                        );
                                        if (_txi.Features.Lowerrightcoords != null && curCoords < _txi.Features.Lowerrightcoords.Count)
                                        {
                                            _txi.Features.Lowerrightcoords[curCoords] = coords;
                                        }
                                        curCoords++;
                                        if (_txi.Features.Lowerrightcoords != null && curCoords >= _txi.Features.Lowerrightcoords.Count)
                                        {
                                            mode = TXIReaderMode.Normal;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    mode = TXIReaderMode.Normal;
                                }
                                continue;
                            }
                        }

                        if (!isCommand)
                        {
                            continue;
                        }

                        TXICommand? command = TXICommandExtensions.FromString(parsedCmdStr);
                        if (!command.HasValue)
                        {
                            continue;
                        }

                        args = args.Trim();

                        // Handle all commands (same as TXI.Load method)
                        ProcessCommand(_txi.Features, command.Value, args, ref mode, ref curCoords, ref empty);
                    }
                    catch (Exception)
                    {
                        // Invalid TXI line, skip
                        continue;
                    }
                }

                return _txi;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        private static void ProcessCommand(TXIFeatures features, TXICommand command, string args, ref TXIReaderMode mode, ref int curCoords, ref bool empty)
        {
            switch (command)
            {
                case TXICommand.Alphamean:
                    features.Alphamean = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Arturoheight:
                    features.Arturoheight = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Arturowidth:
                    features.Arturowidth = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Baselineheight:
                    features.Baselineheight = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Blending:
                    features.Blending = TXI.ParseBlending(args);
                    empty = false;
                    break;
                case TXICommand.Bumpmapscaling:
                    features.Bumpmapscaling = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Bumpmaptexture:
                    features.Bumpmaptexture = args;
                    empty = false;
                    break;
                case TXICommand.Bumpyshinytexture:
                    features.Bumpyshinytexture = args;
                    empty = false;
                    break;
                case TXICommand.Candownsample:
                    features.Candownsample = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Caretindent:
                    features.Caretindent = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Channelscale:
                    features.Channelscale = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToList();
                    empty = false;
                    break;
                case TXICommand.Channeltranslate:
                    features.Channeltranslate = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToList();
                    empty = false;
                    break;
                case TXICommand.Clamp:
                    features.Clamp = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Codepage:
                    features.Codepage = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Cols:
                    features.Cols = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Compresstexture:
                    features.Compresstexture = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Controllerscript:
                    features.Controllerscript = args;
                    empty = false;
                    break;
                case TXICommand.Cube:
                    features.Cube = string.IsNullOrEmpty(args) ? true : (int.Parse(args, CultureInfo.InvariantCulture) != 0);
                    empty = false;
                    break;
                case TXICommand.Decal:
                    features.Decal = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Defaultbpp:
                    features.Defaultbpp = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Defaultheight:
                    features.Defaultheight = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Defaultwidth:
                    features.Defaultwidth = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Distort:
                    features.Distort = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Distortangle:
                    features.Distortangle = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Distortionamplitude:
                    features.Distortionamplitude = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Downsamplefactor:
                    features.Downsamplefactor = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Downsamplemax:
                    features.Downsamplemax = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Downsamplemin:
                    features.Downsamplemin = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Envmaptexture:
                    features.Envmaptexture = args;
                    empty = false;
                    break;
                case TXICommand.Filerange:
                    features.Filerange = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToList();
                    empty = false;
                    break;
                case TXICommand.Filter:
                    features.Filter = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Fontheight:
                    features.Fontheight = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Fontwidth:
                    features.Fontwidth = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Fps:
                    features.Fps = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Isbumpmap:
                    features.Isbumpmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Isdiffusebumpmap:
                    features.Isdiffusebumpmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Islightmap:
                    features.Islightmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Isspecularbumpmap:
                    features.Isspecularbumpmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Lowerrightcoords:
                    int lrCount = int.Parse(args, CultureInfo.InvariantCulture);
                    features.Lowerrightcoords = new List<Tuple<float, float, int>>(lrCount);
                    for (int i = 0; i < lrCount; i++)
                    {
                        features.Lowerrightcoords.Add(Tuple.Create(0f, 0f, 0));
                    }
                    mode = TXIReaderMode.LowerRightCoords;
                    curCoords = 0;
                    empty = false;
                    break;
                case TXICommand.MaxSizeHQ:
                    features.MaxSizeHQ = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.MaxSizeLQ:
                    features.MaxSizeLQ = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.MinSizeHQ:
                    features.MinSizeHQ = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.MinSizeLQ:
                    features.MinSizeLQ = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Mipmap:
                    features.Mipmap = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Numchars:
                    features.Numchars = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Numcharspersheet:
                    features.Numcharspersheet = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Numx:
                    features.Numx = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Numy:
                    features.Numy = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Ondemand:
                    features.Ondemand = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Priority:
                    features.Priority = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Proceduretype:
                    features.Proceduretype = args;
                    empty = false;
                    break;
                case TXICommand.Rows:
                    features.Rows = int.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.SpacingB:
                    features.SpacingB = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.SpacingR:
                    features.SpacingR = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Speed:
                    features.Speed = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Temporary:
                    features.Temporary = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Texturewidth:
                    features.Texturewidth = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Unique:
                    features.Unique = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
                case TXICommand.Upperleftcoords:
                    int ulCount = int.Parse(args, CultureInfo.InvariantCulture);
                    features.Upperleftcoords = new List<Tuple<float, float, int>>(ulCount);
                    for (int i = 0; i < ulCount; i++)
                    {
                        features.Upperleftcoords.Add(Tuple.Create(0f, 0f, 0));
                    }
                    mode = TXIReaderMode.UpperLeftCoords;
                    curCoords = 0;
                    empty = false;
                    break;
                case TXICommand.Wateralpha:
                    features.Wateralpha = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Waterheight:
                    features.Waterheight = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Waterwidth:
                    features.Waterwidth = float.Parse(args, CultureInfo.InvariantCulture);
                    empty = false;
                    break;
                case TXICommand.Xbox_downsample:
                    features.Xbox_downsample = int.Parse(args, CultureInfo.InvariantCulture) != 0;
                    empty = false;
                    break;
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}

