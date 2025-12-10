using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.LYT;

namespace CSharpKOTOR.Formats.LYT
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:17-113
    // Original: class LYTAsciiReader(ResourceReader)
    public class LYTAsciiReader : IDisposable
    {
        private const string RoomCountKey = "roomcount";
        private const string TrackCountKey = "trackcount";
        private const string ObstacleCountKey = "obstaclecount";
        private const string DoorhookCountKey = "doorhookcount";

        private readonly RawBinaryReader _reader;
        private LYT _lyt;
        private List<string> _lines;

        public LYTAsciiReader(byte[] data, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromBytes(data, offset, size > 0 ? size : null);
        }

        public LYTAsciiReader(string filepath, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromFile(filepath, offset, size > 0 ? size : null);
        }

        public LYTAsciiReader(Stream source, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromStream(source, offset, size > 0 ? size : null);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:42-61
        // Original: @autoclose def load(self, *, auto_close: bool = True) -> LYT
        public LYT Load(bool autoClose = true)
        {
            try
            {
                _lyt = new LYT();
                byte[] allBytes = _reader.ReadAll();
                string text = Encoding.ASCII.GetString(allBytes);
                _lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();

                using (var enumerator = _lines.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        string line = enumerator.Current;
                        string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (tokens.Length == 0)
                        {
                            continue;
                        }

                        if (tokens[0] == RoomCountKey && tokens.Length >= 2)
                        {
                            LoadRooms(enumerator, int.Parse(tokens[1]));
                        }
                        if (tokens[0] == TrackCountKey && tokens.Length >= 2)
                        {
                            LoadTracks(enumerator, int.Parse(tokens[1]));
                        }
                        if (tokens[0] == ObstacleCountKey && tokens.Length >= 2)
                        {
                            LoadObstacles(enumerator, int.Parse(tokens[1]));
                        }
                        if (tokens[0] == DoorhookCountKey && tokens.Length >= 2)
                        {
                            LoadDoorhooks(enumerator, int.Parse(tokens[1]));
                        }
                    }
                }

                return _lyt;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:63-73
        // Original: def _load_rooms(self, iterator: Iterator[str], count: int)
        private void LoadRooms(IEnumerator<string> enumerator, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!enumerator.MoveNext())
                {
                    break;
                }
                string[] tokens = enumerator.Current.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 4)
                {
                    string model = tokens[0];
                    Vector3 position = new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]));
                    _lyt.Rooms.Add(new LYTRoom(model, position));
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:75-84
        // Original: def _load_tracks(self, iterator: Iterator[str], count: int)
        private void LoadTracks(IEnumerator<string> enumerator, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!enumerator.MoveNext())
                {
                    break;
                }
                string[] tokens = enumerator.Current.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 4)
                {
                    string model = tokens[0];
                    Vector3 position = new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]));
                    _lyt.Tracks.Add(new LYTTrack(model, position));
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:86-95
        // Original: def _load_obstacles(self, iterator: Iterator[str], count: int)
        private void LoadObstacles(IEnumerator<string> enumerator, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!enumerator.MoveNext())
                {
                    break;
                }
                string[] tokens = enumerator.Current.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 4)
                {
                    string model = tokens[0];
                    Vector3 position = new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]));
                    _lyt.Obstacles.Add(new LYTObstacle(model, position));
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/io_lyt.py:97-113
        // Original: def _load_doorhooks(self, iterator: Iterator[str], count: int)
        private void LoadDoorhooks(IEnumerator<string> enumerator, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!enumerator.MoveNext())
                {
                    break;
                }
                string[] tokens = enumerator.Current.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 10)
                {
                    string room = tokens[0];
                    string door = tokens[1];
                    Vector3 position = new Vector3(float.Parse(tokens[3]), float.Parse(tokens[4]), float.Parse(tokens[5]));
                    Vector4 orientation = new Vector4(
                        float.Parse(tokens[6]),
                        float.Parse(tokens[7]),
                        float.Parse(tokens[8]),
                        float.Parse(tokens[9])
                    );
                    _lyt.Doorhooks.Add(new LYTDoorHook(room, door, position, orientation));
                }
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}

