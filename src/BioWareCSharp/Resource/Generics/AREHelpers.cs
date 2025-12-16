using BioWareCSharp.Common;
using BioWareCSharp.Common.Formats.GFF;
using BioWareCSharp.Common.Resources;
using GFFAuto = AuroraEngine.Common.Formats.GFF.GFFAuto;

namespace BioWareCSharp.Common.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py
    // Original: construct_are and dismantle_are functions
    public static class AREHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:394-535
        // Original: def construct_are(gff: GFF) -> ARE:
        public static ARE ConstructAre(GFF gff)
        {
            var are = new ARE();

            var root = gff.Root;
            var mapStruct = root.Acquire<GFFStruct>("Map", new GFFStruct());
            // map_original_struct_id would need to be stored in ARE class
            // are.map_original_struct_id = mapStruct.StructId;

            // are.north_axis = (ARENorthAxis)mapStruct.Acquire<int>("NorthAxis", 0);
            are.MapList = new System.Collections.Generic.List<ResRef>(); // Placeholder

            // Extract basic fields
            are.Tag = root.Acquire<string>("Tag", "");
            are.Name = root.Acquire<LocalizedString>("Name", LocalizedString.FromInvalid());
            are.AlphaTest = root.Acquire<int>("AlphaTest", 0);
            are.CameraStyle = root.Acquire<int>("CameraStyle", 0);
            are.DefaultEnvMap = root.Acquire<ResRef>("DefaultEnvMap", ResRef.FromBlank());
            are.GrassTexture = root.Acquire<ResRef>("Grass_TexName", ResRef.FromBlank());
            are.GrassDensity = root.Acquire<float>("Grass_Density", 0.0f);
            are.GrassSize = root.Acquire<float>("Grass_QuadSize", 0.0f);
            are.GrassProbLL = root.Acquire<float>("Grass_Prob_LL", 0.0f);
            are.GrassProbLR = root.Acquire<float>("Grass_Prob_LR", 0.0f);
            are.GrassProbUL = root.Acquire<float>("Grass_Prob_UL", 0.0f);
            are.GrassProbUR = root.Acquire<float>("Grass_Prob_UR", 0.0f);
            are.FogEnabled = root.Acquire<int>("SunFogOn", 0) != 0;
            are.FogNear = root.Acquire<float>("SunFogNear", 0.0f);
            are.FogFar = root.Acquire<float>("SunFogFar", 0.0f);
            are.WindPower = root.Acquire<int>("WindPower", 0);
            are.ShadowOpacity = root.Acquire<ResRef>("ShadowOpacity", ResRef.FromBlank());
            are.OnEnter = root.Acquire<ResRef>("OnEnter", ResRef.FromBlank());
            are.OnExit = root.Acquire<ResRef>("OnExit", ResRef.FromBlank());
            are.OnHeartbeat = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            are.OnUserDefined = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:496
            // Original: are.loadscreen_id = root.acquire("LoadScreenID", 0)
            are.LoadScreenID = root.Acquire<int>("LoadScreenID", 0);

            // Extract color fields (as RGB integers)
            int sunAmbientInt = root.Acquire<int>("SunAmbientColor", 0);
            int sunDiffuseInt = root.Acquire<int>("SunDiffuseColor", 0);
            int fogColorInt = root.Acquire<int>("SunFogColor", 0);
            // Convert RGB integers to Color objects (Color class would need FromRgbInteger method)
            // are.SunAmbient = Color.FromRgbInteger(sunAmbientInt);
            // are.SunDiffuse = Color.FromRgbInteger(sunDiffuseInt);
            // are.FogColor = Color.FromRgbInteger(fogColorInt);

            // Extract rooms list
            var roomsList = root.Acquire<GFFList>("Rooms", new GFFList());
            // are.Rooms would need to be a List<ARERoom> in ARE class
            // foreach (GFFStruct roomStruct in roomsList) { ... }

            return are;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:538-682
        // Original: def dismantle_are(are: ARE, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleAre(ARE are, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.ARE);
            var root = gff.Root;

            // Create Map struct
            var mapStruct = new GFFStruct();
            root.SetStruct("Map", mapStruct);
            // mapStruct.SetInt32("MapZoom", are.MapZoom);
            // mapStruct.SetInt32("MapResX", are.MapResX);
            // mapStruct.SetInt32("NorthAxis", (int)are.NorthAxis);

            // Set basic fields
            root.SetString("Tag", are.Tag);
            root.SetLocString("Name", are.Name);
            root.SetSingle("AlphaTest", are.AlphaTest);
            root.SetInt32("CameraStyle", are.CameraStyle);
            root.SetResRef("DefaultEnvMap", are.DefaultEnvMap);
            root.SetResRef("Grass_TexName", are.GrassTexture);
            root.SetSingle("Grass_Density", are.GrassDensity);
            root.SetSingle("Grass_QuadSize", are.GrassSize);
            root.SetSingle("Grass_Prob_LL", are.GrassProbLL);
            root.SetSingle("Grass_Prob_LR", are.GrassProbLR);
            root.SetSingle("Grass_Prob_UL", are.GrassProbUL);
            root.SetSingle("Grass_Prob_UR", are.GrassProbUR);
            root.SetUInt8("SunFogOn", are.FogEnabled ? (byte)1 : (byte)0);
            root.SetSingle("SunFogNear", are.FogNear);
            root.SetSingle("SunFogFar", are.FogFar);
            root.SetInt32("WindPower", are.WindPower);
            root.SetResRef("ShadowOpacity", are.ShadowOpacity);
            root.SetResRef("OnEnter", are.OnEnter);
            root.SetResRef("OnExit", are.OnExit);
            root.SetResRef("OnHeartbeat", are.OnHeartbeat);
            root.SetResRef("OnUserDefined", are.OnUserDefined);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:673
            // Original: root.set_uint16("LoadScreenID", are.loadscreen_id)
            root.SetUInt16("LoadScreenID", (ushort)are.LoadScreenID);

            // Set color fields (as RGB integers)
            // root.SetUInt32("SunAmbientColor", are.SunAmbient.RgbInteger());
            // root.SetUInt32("SunDiffuseColor", are.SunDiffuse.RgbInteger());
            // root.SetUInt32("SunFogColor", are.FogColor.RgbInteger());

            // Set rooms list
            var roomsList = new GFFList();
            root.SetList("Rooms", roomsList);
            // foreach (var room in are.Rooms) { ... }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:685-700
        // Original: def read_are(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> ARE:
        public static ARE ReadAre(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructAre(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:742-757
        // Original: def bytes_are(are: ARE, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesAre(ARE are, Game game = Game.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.ARE;
            }
            GFF gff = DismantleAre(are, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}
