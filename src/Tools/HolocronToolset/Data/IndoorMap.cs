using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:66
    // Original: class IndoorMap:
    public class IndoorMap
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:67-81
        // Original: def __init__(self, rooms: list[IndoorMapRoom] | None = None, module_id: str | None = None, name: LocalizedString | None = None, lighting: Color | None = None, skybox: str | None = None, warp_point: Vector3 | None = None):
        public IndoorMap(
            List<IndoorMapRoom> rooms = null,
            string moduleId = null,
            LocalizedString name = null,
            Color lighting = null,
            string skybox = null,
            System.Numerics.Vector3? warpPoint = null)
        {
            Rooms = rooms ?? new List<IndoorMapRoom>();
            ModuleId = moduleId ?? "test01";
            Name = name ?? LocalizedString.FromEnglish("New Module");
            Lighting = lighting ?? new Color(0.5f, 0.5f, 0.5f);
            Skybox = skybox ?? "";
            WarpPoint = warpPoint ?? System.Numerics.Vector3.Zero;
        }

        public List<IndoorMapRoom> Rooms { get; set; }
        public string ModuleId { get; set; }
        public LocalizedString Name { get; set; }
        public Color Lighting { get; set; }
        public string Skybox { get; set; }
        public System.Numerics.Vector3 WarpPoint { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1058
    // Original: class IndoorMapRoom:
    public class IndoorMapRoom
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1059-1074
        // Original: def __init__(self, component: KitComponent, position: Vector3, rotation: float, *, flip_x: bool, flip_y: bool):
        public IndoorMapRoom(
            KitComponent component,
            System.Numerics.Vector3 position,
            float rotation,
            bool flipX = false,
            bool flipY = false)
        {
            Component = component;
            Position = position;
            Rotation = rotation;
            FlipX = flipX;
            FlipY = flipY;
            Hooks = new List<IndoorMapRoom>();
            if (component != null && component.Hooks != null)
            {
                for (int i = 0; i < component.Hooks.Count; i++)
                {
                    Hooks.Add(null);
                }
            }
        }

        public KitComponent Component { get; set; }
        public System.Numerics.Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public List<IndoorMapRoom> Hooks { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
    }
}

