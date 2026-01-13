using System;
using System.IO;
using System.Text;
using System.Text.Json;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip_json.py:64-89
    // Original: class LIPJSONWriter(ResourceWriter)
    /// <summary>
    /// Writes LIP files to JSON format.
    /// </summary>
    /// <remarks>
    /// JSON is a PyKotor-specific convenience format for easier editing of lip-sync data.
    /// Format: {"duration": "1.5", "keyframes": [{"time": "0.0", "shape": "0"}, ...]}
    /// </remarks>
    public class LIPJSONWriter : IDisposable
    {
        private readonly LIP _lip;
        private readonly RawBinaryWriter _writer;

        public LIPJSONWriter(LIP lip, string filepath)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public LIPJSONWriter(LIP lip, Stream target)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public LIPJSONWriter(LIP lip)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip_json.py:77-89
        // Original: @autoclose def write(self, *, auto_close: bool = True)
        public void Write(bool autoClose = true)
        {
            try
            {
                // Build JSON structure matching PyKotor format using JsonDocument/JsonObject
                // Format: {"duration": "1.5", "keyframes": [{"time": "0.0", "shape": "0"}, ...]}
                using (var stream = new MemoryStream())
                {
                    using (var jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                    {
                        jsonWriter.WriteStartObject();

                        // Write duration as string (matching PyKotor format)
                        jsonWriter.WriteString("duration", _lip.Length.ToString("F6"));

                        // Write keyframes array
                        jsonWriter.WriteStartArray("keyframes");
                        foreach (var frame in _lip.Frames)
                        {
                            jsonWriter.WriteStartObject();
                            jsonWriter.WriteString("time", frame.Time.ToString("F6"));
                            jsonWriter.WriteString("shape", ((int)frame.Shape).ToString());
                            jsonWriter.WriteEndObject();
                        }
                        jsonWriter.WriteEndArray();

                        jsonWriter.WriteEndObject();
                    }

                    // Get the JSON bytes from the stream
                    byte[] jsonBytes = stream.ToArray();
                    _writer.WriteBytes(jsonBytes);
                }
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }

        // Matching LIPBinaryWriter pattern for BytesLip
        // Get the data from the underlying RawBinaryWriter
        public byte[] Data()
        {
            return _writer?.Data() ?? new byte[0];
        }
    }
}

