using System;

namespace Andastra.Runtime.Content.MDL
{
    /// <summary>
    /// Constants for MDL/MDX file format parsing.
    /// Based on MDLOps reference and vendor/PyKotor/wiki/MDL-MDX-File-Format.md specifications.
    /// </summary>
    /// <remarks>
    /// MDL Constants:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) MDL/MDX file format
    /// - Located via string references: "DoubleMdlVar" @ 0x007d05d8, "ShortMdlVar" @ 0x007d05e8, "LongMdlVar" @ 0x007d05f4
    /// - Model loading: 0x005261b0 @ 0x005261b0 loads creature models, uses MDL file format structures
    /// - Original implementation: MDL file format uses specific header sizes, offsets, and structures
    /// - File format constants: Header sizes, geometry function pointers, node type bitmasks match original engine
    /// - Reference: MDLOps reference and vendor/PyKotor/wiki/MDL-MDX-File-Format.md specifications
    /// </remarks>
    public static class MDLConstants
    {
        #region Header Sizes
        // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - File Headers section
        public const int FILE_HEADER_SIZE = 12;
        public const int GEOMETRY_HEADER_SIZE = 80;
        public const int MODEL_HEADER_SIZE = 92;
        public const int NAMES_HEADER_SIZE = 28;
        public const int ANIMATION_HEADER_SIZE = 136; // Geometry header + 56 bytes
        public const int EVENT_SIZE = 36;
        public const int NODE_HEADER_SIZE = 80;
        public const int CONTROLLER_SIZE = 16;

        // Trimesh header sizes vary by game version
        public const int TRIMESH_HEADER_SIZE_K1 = 332;
        public const int TRIMESH_HEADER_SIZE_K2 = 340;
        public const int DANGLYMESH_EXTENSION_SIZE = 28;
        public const int SKINMESH_EXTENSION_SIZE = 100;
        public const int LIGHTSABER_EXTENSION_SIZE = 20;
        public const int LIGHT_HEADER_SIZE = 92;
        public const int EMITTER_HEADER_SIZE = 224;
        public const int REFERENCE_HEADER_SIZE = 36;
        public const int FACE_SIZE = 32;
        #endregion

        #region Geometry Function Pointers (for version detection)
        // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - KotOR 1 vs KotOR 2 Models
        // KotOR 1 PC
        public const uint K1_PC_GEOMETRY_FP = 4273776;  // 0x413750
        public const uint K1_PC_ANIMATION_FP = 4273392; // 0x4135D0
        // KotOR 2 PC
        public const uint K2_PC_GEOMETRY_FP = 4285200;  // 0x416610
        public const uint K2_PC_ANIMATION_FP = 4284816; // 0x416490
        // KotOR 1 Xbox
        public const uint K1_XBOX_GEOMETRY_FP = 4254992;  // 0x40EE90
        public const uint K1_XBOX_ANIMATION_FP = 4254608; // 0x40ED10
        // KotOR 2 Xbox
        public const uint K2_XBOX_GEOMETRY_FP = 4285872;  // 0x416950
        public const uint K2_XBOX_ANIMATION_FP = 4285488; // 0x4167D0
        #endregion

        #region Node Type Bitmasks
        // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Node Type Bitmasks
        public const ushort NODE_HAS_HEADER = 0x0001;
        public const ushort NODE_HAS_LIGHT = 0x0002;
        public const ushort NODE_HAS_EMITTER = 0x0004;
        public const ushort NODE_HAS_CAMERA = 0x0008;
        public const ushort NODE_HAS_REFERENCE = 0x0010;
        public const ushort NODE_HAS_MESH = 0x0020;
        public const ushort NODE_HAS_SKIN = 0x0040;
        public const ushort NODE_HAS_ANIM = 0x0080;
        public const ushort NODE_HAS_DANGLY = 0x0100;
        public const ushort NODE_HAS_AABB = 0x0200;
        public const ushort NODE_HAS_SABER = 0x0800;
        #endregion

        #region Common Node Type Combinations
        public const ushort NODE_TYPE_DUMMY = NODE_HAS_HEADER;                                    // 0x001
        public const ushort NODE_TYPE_LIGHT = NODE_HAS_HEADER | NODE_HAS_LIGHT;                   // 0x003
        public const ushort NODE_TYPE_EMITTER = NODE_HAS_HEADER | NODE_HAS_EMITTER;               // 0x005
        public const ushort NODE_TYPE_REFERENCE = NODE_HAS_HEADER | NODE_HAS_REFERENCE;           // 0x011
        public const ushort NODE_TYPE_MESH = NODE_HAS_HEADER | NODE_HAS_MESH;                     // 0x021
        public const ushort NODE_TYPE_SKIN = NODE_HAS_HEADER | NODE_HAS_MESH | NODE_HAS_SKIN;     // 0x061
        public const ushort NODE_TYPE_ANIMMESH = NODE_HAS_HEADER | NODE_HAS_MESH | NODE_HAS_ANIM; // 0x0A1
        public const ushort NODE_TYPE_DANGLY = NODE_HAS_HEADER | NODE_HAS_MESH | NODE_HAS_DANGLY; // 0x121
        public const ushort NODE_TYPE_AABB = NODE_HAS_HEADER | NODE_HAS_MESH | NODE_HAS_AABB;     // 0x221
        public const ushort NODE_TYPE_SABER = NODE_HAS_HEADER | NODE_HAS_MESH | NODE_HAS_SABER;   // 0x821
        #endregion

        #region MDX Data Bitmap Masks
        // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - MDX Data Bitmap Masks
        public const uint MDX_VERTICES = 0x00000001;
        public const uint MDX_TEX0_VERTICES = 0x00000002;
        public const uint MDX_TEX1_VERTICES = 0x00000004;
        public const uint MDX_TEX2_VERTICES = 0x00000008;
        public const uint MDX_TEX3_VERTICES = 0x00000010;
        public const uint MDX_VERTEX_NORMALS = 0x00000020;
        public const uint MDX_VERTEX_COLORS = 0x00000040;
        public const uint MDX_TANGENT_SPACE = 0x00000080;
        // Skin mesh specific (not stored in MDX flags, used internally)
        public const uint MDX_BONE_WEIGHTS = 0x00000800;
        public const uint MDX_BONE_INDICES = 0x00001000;
        #endregion

        #region Controller Types
        // Common controller types
        public const int CONTROLLER_POSITION = 8;
        public const int CONTROLLER_ORIENTATION = 20;
        public const int CONTROLLER_SCALE = 36;
        // Mesh controllers
        public const int CONTROLLER_SELFILLUMCOLOR = 100;
        public const int CONTROLLER_ALPHA = 128;
        // Light controllers
        public const int CONTROLLER_LIGHT_COLOR = 76;
        public const int CONTROLLER_LIGHT_RADIUS = 88;
        public const int CONTROLLER_LIGHT_SHADOWRADIUS = 96;
        public const int CONTROLLER_LIGHT_VERTICALDISP = 100;
        public const int CONTROLLER_LIGHT_MULTIPLIER = 140;
        #endregion

        #region Model Classification
        public const byte CLASSIFICATION_OTHER = 0x00;
        public const byte CLASSIFICATION_EFFECT = 0x01;
        public const byte CLASSIFICATION_TILE = 0x02;
        public const byte CLASSIFICATION_CHARACTER = 0x04;
        public const byte CLASSIFICATION_DOOR = 0x08;
        public const byte CLASSIFICATION_LIGHTSABER = 0x10;
        public const byte CLASSIFICATION_PLACEABLE = 0x20;
        public const byte CLASSIFICATION_FLYER = 0x40;
        #endregion

        #region Bezier Flag
        // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Controller Structure
        // If bit 4 (0x10) is set in column count, controller uses Bezier interpolation
        public const byte CONTROLLER_BEZIER_FLAG = 0x10;
        #endregion
    }
}

