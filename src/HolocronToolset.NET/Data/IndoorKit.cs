using System.Collections.Generic;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.BWM;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.MDL;
using CSharpKOTOR.Resource.Generics;

namespace HolocronToolset.NET.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:24
    // Original: class Kit:
    public class Kit
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:25-35
        // Original: def __init__(self, name: str):
        public Kit(string name)
        {
            Name = name;
            Components = new List<KitComponent>();
            Doors = new List<KitDoor>();
            Textures = new Dictionary<string, byte[]>();
            Lightmaps = new Dictionary<string, byte[]>();
            Txis = new Dictionary<string, byte[]>();
            Always = new Dictionary<string, byte[]>();
            SidePadding = new Dictionary<int, Dictionary<int, MDLMDXTuple>>();
            TopPadding = new Dictionary<int, Dictionary<int, MDLMDXTuple>>();
            Skyboxes = new Dictionary<string, MDLMDXTuple>();
        }

        public string Name { get; set; }
        public List<KitComponent> Components { get; set; }
        public List<KitDoor> Doors { get; set; }
        public Dictionary<string, byte[]> Textures { get; set; }
        public Dictionary<string, byte[]> Lightmaps { get; set; }
        public Dictionary<string, byte[]> Txis { get; set; }
        public Dictionary<string, byte[]> Always { get; set; }
        public Dictionary<int, Dictionary<int, MDLMDXTuple>> SidePadding { get; set; }
        public Dictionary<int, Dictionary<int, MDLMDXTuple>> TopPadding { get; set; }
        public Dictionary<string, MDLMDXTuple> Skyboxes { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:38
    // Original: class KitComponent:
    public class KitComponent
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:39-47
        // Original: def __init__(self, kit: Kit, name: str, image: QImage, bwm: BWM, mdl: bytes, mdx: bytes):
        public KitComponent(Kit kit, string name, object image, BWM bwm, byte[] mdl, byte[] mdx)
        {
            Kit = kit;
            Name = name;
            Image = image;
            Bwm = bwm;
            Mdl = mdl;
            Mdx = mdx;
            Hooks = new List<KitComponentHook>();
        }

        public Kit Kit { get; set; }
        public string Name { get; set; }
        public object Image { get; set; }
        public BWM Bwm { get; set; }
        public byte[] Mdl { get; set; }
        public byte[] Mdx { get; set; }
        public List<KitComponentHook> Hooks { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:50
    // Original: class KitComponentHook:
    public class KitComponentHook
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:51-55
        // Original: def __init__(self, position: Vector3, rotation: float, edge: int, door: KitDoor):
        public KitComponentHook(Vector3 position, float rotation, int edge, KitDoor door)
        {
            Position = position;
            Rotation = rotation;
            Edge = edge;
            Door = door;
        }

        public Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public int Edge { get; set; }
        public KitDoor Door { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:58
    // Original: class KitDoor:
    public class KitDoor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:59-63
        // Original: def __init__(self, utdK1: UTD, utdK2: UTD, width: float, height: float):
        public KitDoor(UTD utdK1, UTD utdK2, float width, float height)
        {
            UtdK1 = utdK1;
            UtdK2 = utdK2;
            Width = width;
            Height = height;
        }

        public UTD UtdK1 { get; set; }
        public UTD UtdK2 { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:66
    // Original: class MDLMDXTuple(NamedTuple):
    public class MDLMDXTuple
    {
        public MDLMDXTuple(byte[] mdl, byte[] mdx)
        {
            Mdl = mdl;
            Mdx = mdx;
        }

        public byte[] Mdl { get; set; }
        public byte[] Mdx { get; set; }
    }
}

