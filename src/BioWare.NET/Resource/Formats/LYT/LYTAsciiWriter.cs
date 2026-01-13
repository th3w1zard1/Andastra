using System;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.LYT;

namespace BioWare.NET.Resource.Formats.LYT
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:116-165
    // Original: class LYTAsciiWriter(ResourceWriter)
    public class LYTAsciiWriter : IDisposable
    {
        private const string LytLineSep = "\r\n";
        private const string LytIndent = "   ";
        private const string RoomCountKey = "roomcount";
        private const string TrackCountKey = "trackcount";
        private const string ObstacleCountKey = "obstaclecount";
        private const string DoorhookCountKey = "doorhookcount";

        private readonly BioWare.NET.Resource.Formats.LYT.LYT _lyt;
        private readonly RawBinaryWriter _writer;

        public LYTAsciiWriter(BioWare.NET.Resource.Formats.LYT.LYT lyt, string filepath)
        {
            _lyt = lyt ?? throw new ArgumentNullException(nameof(lyt));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public LYTAsciiWriter(BioWare.NET.Resource.Formats.LYT.LYT lyt, Stream target)
        {
            _lyt = lyt ?? throw new ArgumentNullException(nameof(lyt));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public LYTAsciiWriter(BioWare.NET.Resource.Formats.LYT.LYT lyt)
        {
            _lyt = lyt ?? throw new ArgumentNullException(nameof(lyt));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:133-165
        // Original: @autoclose def write(self, *, auto_close: bool = True)
        public void Write(bool autoClose = true)
        {
            try
            {
                int roomcount = _lyt.Rooms.Count;
                int trackcount = _lyt.Tracks.Count;
                int obstaclecount = _lyt.Obstacles.Count;
                int doorhookcount = _lyt.DoorHooks.Count;

                _writer.WriteString($"beginlayout{LytLineSep}", Encoding.ASCII.WebName);

                _writer.WriteString($"{LytIndent}{RoomCountKey} {roomcount}{LytLineSep}", Encoding.ASCII.WebName);
                foreach (var room in _lyt.Rooms)
                {
                    _writer.WriteString($"{LytIndent}{LytIndent}{room.Model} {room.Position.X} {room.Position.Y} {room.Position.Z}{LytLineSep}", Encoding.ASCII.WebName);
                }

                _writer.WriteString($"{LytIndent}{TrackCountKey} {trackcount}{LytLineSep}", Encoding.ASCII.WebName);
                foreach (var track in _lyt.Tracks)
                {
                    _writer.WriteString($"{LytIndent}{LytIndent}{track.Model} {track.Position.X} {track.Position.Y} {track.Position.Z}{LytLineSep}", Encoding.ASCII.WebName);
                }

                _writer.WriteString($"{LytIndent}{ObstacleCountKey} {obstaclecount}{LytLineSep}", Encoding.ASCII.WebName);
                foreach (var obstacle in _lyt.Obstacles)
                {
                    _writer.WriteString($"{LytIndent}{LytIndent}{obstacle.Model} {obstacle.Position.X} {obstacle.Position.Y} {obstacle.Position.Z}{LytLineSep}", Encoding.ASCII.WebName);
                }

                _writer.WriteString($"{LytIndent}{DoorhookCountKey} {doorhookcount}{LytLineSep}", Encoding.ASCII.WebName);
                foreach (var doorhook in _lyt.DoorHooks)
                {
                    _writer.WriteString($"{LytIndent}{LytIndent}{doorhook.Room} {doorhook.Door} 0 {doorhook.Position.X} {doorhook.Position.Y} {doorhook.Position.Z} {doorhook.Orientation.X} {doorhook.Orientation.Y} {doorhook.Orientation.Z} {doorhook.Orientation.W}{LytLineSep}", Encoding.ASCII.WebName);
                }

                _writer.WriteString("donelayout", Encoding.ASCII.WebName);
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
    }
}
