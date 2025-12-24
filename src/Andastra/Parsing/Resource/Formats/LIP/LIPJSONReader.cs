using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Andastra.Parsing;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip_json.py:25-60
    // Original: class LIPJSONReader(ResourceReader)
    /// <summary>
    /// Reads LIP files from JSON format.
    /// </summary>
    /// <remarks>
    /// JSON is a PyKotor-specific convenience format for easier editing of lip-sync data.
    /// Format: {"duration": "1.5", "keyframes": [{"time": "0.0", "shape": "0"}, ...]}
    /// Note: This implementation fixes a bug in PyKotor where the reader expected a different format than the writer produces.
    /// </remarks>
    public class LIPJSONReader : IDisposable
    {
        private readonly RawBinaryReader _reader;
        private LIP _lip;

        public LIPJSONReader(byte[] data, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = RawBinaryReader.FromBytes(data, offset, sizeNullable);
        }

        public LIPJSONReader(string filepath, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = RawBinaryReader.FromFile(filepath, offset, sizeNullable);
        }

        public LIPJSONReader(Stream source, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = RawBinaryReader.FromStream(source, offset, sizeNullable);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip_json.py:44-60
        // Original: @autoclose def load(self, *, auto_close: bool = True) -> LIP
        // Note: Fixed to match writer format - writer produces {"duration": "...", "keyframes": [...]} not {"lip": {...}}
        public LIP Load(bool autoClose = true)
        {
            try
            {
                _lip = new LIP();

                // Read all bytes from the reader
                byte[] jsonBytes = _reader.ReadBytes(_reader.Size);

                // Decode bytes to string with fallback encodings (matching PyKotor's decode_bytes_with_fallbacks)
                string jsonString = DecodeBytesWithFallbacks(jsonBytes);

                // Parse JSON document
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(jsonString);
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException("The JSON file that was loaded was not a valid LIP.", ex);
                }

                var root = doc.RootElement;

                // Check if "duration" key exists (matching writer format)
                // Note: PyKotor reader checks for "lip" key, but writer produces top-level "duration"/"keyframes"
                // This implementation matches the writer format (canonical)
                if (!root.TryGetProperty("duration", out var durationProp))
                {
                    throw new ArgumentException("The JSON file that was loaded was not a valid LIP - missing 'duration' field.");
                }

                // Parse duration
                if (durationProp.ValueKind == JsonValueKind.String)
                {
                    string durationStr = durationProp.GetString();
                    if (!float.TryParse(durationStr, out float duration))
                    {
                        throw new ArgumentException($"The JSON file that was loaded was not a valid LIP - invalid duration value: '{durationStr}'.");
                    }
                    _lip.Length = duration;
                }
                else if (durationProp.ValueKind == JsonValueKind.Number)
                {
                    _lip.Length = (float)durationProp.GetDouble();
                }
                else
                {
                    throw new ArgumentException("The JSON file that was loaded was not a valid LIP - 'duration' must be a number or string.");
                }

                // Parse keyframes (matching writer format - uses "keyframes" not "elements")
                if (!root.TryGetProperty("keyframes", out var keyframesProp))
                {
                    // Try "elements" for backward compatibility with old format
                    if (root.TryGetProperty("elements", out var elementsProp))
                    {
                        keyframesProp = elementsProp;
                    }
                    else
                    {
                        throw new ArgumentException("The JSON file that was loaded was not a valid LIP - missing 'keyframes' field.");
                    }
                }

                if (keyframesProp.ValueKind != JsonValueKind.Array)
                {
                    throw new ArgumentException("The JSON file that was loaded was not a valid LIP - 'keyframes' must be an array.");
                }

                foreach (var keyframeElement in keyframesProp.EnumerateArray())
                {
                    if (keyframeElement.ValueKind != JsonValueKind.Object)
                    {
                        continue; // Skip invalid keyframe entries
                    }

                    // Parse time
                    if (!keyframeElement.TryGetProperty("time", out var timeProp))
                    {
                        continue; // Skip keyframes without time
                    }

                    float time;
                    if (timeProp.ValueKind == JsonValueKind.String)
                    {
                        string timeStr = timeProp.GetString();
                        if (!float.TryParse(timeStr, out time))
                        {
                            continue; // Skip invalid time values
                        }
                    }
                    else if (timeProp.ValueKind == JsonValueKind.Number)
                    {
                        time = (float)timeProp.GetDouble();
                    }
                    else
                    {
                        continue; // Skip invalid time values
                    }

                    // Parse shape
                    if (!keyframeElement.TryGetProperty("shape", out var shapeProp))
                    {
                        continue; // Skip keyframes without shape
                    }

                    LIPShape shape;
                    if (shapeProp.ValueKind == JsonValueKind.String)
                    {
                        string shapeStr = shapeProp.GetString();
                        if (!int.TryParse(shapeStr, out int shapeValue))
                        {
                            continue; // Skip invalid shape values
                        }
                        shape = (LIPShape)shapeValue;
                    }
                    else if (shapeProp.ValueKind == JsonValueKind.Number)
                    {
                        shape = (LIPShape)shapeProp.GetInt32();
                    }
                    else
                    {
                        continue; // Skip invalid shape values
                    }

                    // Add keyframe to LIP
                    _lip.Add(time, shape);
                }

                doc.Dispose();
                return _lip;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        // Matching PyKotor's decode_bytes_with_fallbacks behavior
        // Try UTF-8 first, then fall back to other encodings
        private string DecodeBytesWithFallbacks(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            // Try UTF-8 first (most common)
            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // Fall through to next encoding
            }

            // Try ASCII
            try
            {
                return Encoding.ASCII.GetString(bytes);
            }
            catch
            {
                // Fall through to next encoding
            }

            // Try Windows-1252 (common for legacy files)
            try
            {
                Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(1252).GetString(bytes);
            }
            catch
            {
                // Fall back to UTF-8 with error handling
                return Encoding.UTF8.GetString(bytes);
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}

