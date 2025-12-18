using System.Numerics;
using HolocronToolset.Data;

namespace HolocronToolset.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:451-461
    // Original: @dataclass class RoomClipboardData:
    public class RoomClipboardData
    {
        // Matching Python dataclass fields
        public string ComponentKitName { get; set; }
        public string ComponentName { get; set; }
        public Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
        public byte[] WalkmeshOverride { get; set; }

        public RoomClipboardData(
            string componentKitName,
            string componentName,
            Vector3 position,
            float rotation,
            bool flipX,
            bool flipY,
            byte[] walkmeshOverride = null)
        {
            ComponentKitName = componentKitName;
            ComponentName = componentName;
            Position = position;
            Rotation = rotation;
            FlipX = flipX;
            FlipY = flipY;
            WalkmeshOverride = walkmeshOverride;
        }
    }
}

