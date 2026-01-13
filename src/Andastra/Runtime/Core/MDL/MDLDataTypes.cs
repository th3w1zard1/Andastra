using System;

namespace Andastra.Runtime.Core.MDL
{
    /// <summary>
    /// Lightweight vector types for MDL data without external dependencies.
    /// These are used for intermediate parsing before conversion to rendering-specific types.
    /// </summary>
    /// <remarks>
    /// MDL Data Types:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) MDL/MDX file format
    /// - Located via string references: "ModelName" @ 0x007c1c8c, "Model" @ 0x007c1ca8, model loading functions
    /// - Model loading: FUN_005261b0 @ 0x005261b0 loads creature models, parses MDL vertex/face data
    /// - Original implementation: MDL file format uses Vector3 (position/normal), Vector2 (UV), Color (vertex colors)
    /// - Data structures: Match original engine's internal MDL data structures for vertex positions, normals, UVs, colors
    /// - Intermediate types: Used for parsing before conversion to rendering-specific types (MonoGame, Stride, etc.)
    /// </remarks>
    /// <summary>
    /// Represents a 2D vector with X and Y components.
    /// Used for texture coordinates in MDL mesh data.
    /// </summary>
    public struct Vector2Data
    {
        /// <summary>
        /// X component of the vector.
        /// </summary>
        public float X;

        /// <summary>
        /// Y component of the vector.
        /// </summary>
        public float Y;

        /// <summary>
        /// Initializes a new instance of Vector2Data.
        /// </summary>
        /// <param name="x">X component</param>
        /// <param name="y">Y component</param>
        public Vector2Data(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Represents a 3D vector with X, Y, and Z components.
    /// Used for positions, normals, colors, and other 3D data in MDL mesh data.
    /// </summary>
    public struct Vector3Data
    {
        /// <summary>
        /// X component of the vector.
        /// </summary>
        public float X;

        /// <summary>
        /// Y component of the vector.
        /// </summary>
        public float Y;

        /// <summary>
        /// Z component of the vector.
        /// </summary>
        public float Z;

        /// <summary>
        /// Initializes a new instance of Vector3Data.
        /// </summary>
        /// <param name="x">X component</param>
        /// <param name="y">Y component</param>
        /// <param name="z">Z component</param>
        public Vector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Represents a 4D vector with X, Y, Z, and W components.
    /// Used for quaternions (rotations) in MDL animation and node data.
    /// </summary>
    public struct Vector4Data
    {
        /// <summary>
        /// X component of the vector.
        /// </summary>
        public float X;

        /// <summary>
        /// Y component of the vector.
        /// </summary>
        public float Y;

        /// <summary>
        /// Z component of the vector.
        /// </summary>
        public float Z;

        /// <summary>
        /// W component of the vector.
        /// </summary>
        public float W;

        /// <summary>
        /// Initializes a new instance of Vector4Data.
        /// </summary>
        /// <param name="x">X component</param>
        /// <param name="y">Y component</param>
        /// <param name="z">Z component</param>
        /// <param name="w">W component</param>
        public Vector4Data(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    /// <summary>
    /// Complete MDL model representation optimized for runtime loading.
    /// </summary>
    public sealed class MDLModel
    {
        public string Name;
        public string Supermodel;
        public byte Classification;
        public byte SubClassification;
        public bool AffectedByFog;
        public Vector3Data BoundingBoxMin;
        public Vector3Data BoundingBoxMax;
        public float Radius;
        public float AnimationScale;
        public int NodeCount;
        public int AnimationArrayOffset;
        public int AnimationCount;
        public MDLNodeData RootNode;
        public MDLAnimationData[] Animations;
    }

    /// <summary>
    /// Animation data structure.
    /// </summary>
    public sealed class MDLAnimationData
    {
        public string Name;
        public string AnimRoot;
        public float Length;
        public float TransitionTime;
        public MDLEventData[] Events;
        public MDLNodeData RootNode;
    }

    /// <summary>
    /// Animation event data.
    /// Events are triggered at specific times during animation playback.
    /// </summary>
    public struct MDLEventData
    {
        public float ActivationTime;
        public string Name;
    }

    /// <summary>
    /// Node data structure representing a single node in the model hierarchy.
    /// </summary>
    public sealed class MDLNodeData
    {
        public string Name;
        public ushort NodeType;
        public ushort NodeIndex;
        public ushort NameIndex;
        public Vector3Data Position;
        public Vector4Data Orientation;
        public MDLNodeData[] Children;
        public MDLControllerData[] Controllers;
        public MDLMeshData Mesh;
        public MDLLightData Light;
        public MDLEmitterData Emitter;
        public MDLReferenceData Reference;
    }

    /// <summary>
    /// Controller data for animation.
    /// </summary>
    public sealed class MDLControllerData
    {
        public int Type;
        public int RowCount;
        public int TimeIndex;
        public int DataIndex;
        public int ColumnCount;
        public bool IsBezier;
        public float[] TimeKeys;
        public float[] Values;
    }

    /// <summary>
    /// Mesh data structure containing geometry and rendering information.
    /// Optimized for direct conversion to GPU vertex/index buffers.
    /// </summary>
    public sealed class MDLMeshData
    {
        // Bounding volume
        public Vector3Data BoundingBoxMin;
        public Vector3Data BoundingBoxMax;
        public float Radius;
        public Vector3Data AveragePoint;

        // Material properties
        public Vector3Data DiffuseColor;
        public Vector3Data AmbientColor;
        public uint TransparencyHint;

        // Textures
        public string Texture0;
        public string Texture1;
        public string Texture2;
        public string Texture3;

        // UV animation
        public float UVDirectionX;
        public float UVDirectionY;
        public float UVJitter;
        public float UVJitterSpeed;

        // MDX data layout
        public int MDXVertexSize;
        public uint MDXDataFlags;
        public int MDXDataOffset;
        public int MDXPositionOffset;
        public int MDXNormalOffset;
        public int MDXColorOffset;
        public int MDXTex0Offset;
        public int MDXTex1Offset;
        public int MDXTex2Offset;
        public int MDXTex3Offset;
        public int MDXTangentOffset;
        public int MDXUnknown1Offset;  // Offset to unknown MDX data block 1 - purpose unknown
        public int MDXUnknown2Offset;  // Offset to unknown MDX data block 2 - purpose unknown
        public int MDXUnknown3Offset;  // Offset to unknown MDX data block 3 - purpose unknown

        // Vertex/face counts
        public int VertexCount;
        public int FaceCount;
        public int TextureCount;

        // Flags
        public bool HasLightmap;
        public bool RotateTexture;
        public bool BackgroundGeometry;
        public bool Shadow;
        public bool Beaming;
        public bool Render;
        public float TotalArea;

        // Geometry data (loaded from MDX)
        public Vector3Data[] Positions;
        public Vector3Data[] Normals;
        public Vector2Data[] TexCoords0;
        public Vector2Data[] TexCoords1;
        public Vector2Data[] TexCoords2;
        public Vector2Data[] TexCoords3;
        public Vector3Data[] Colors;
        public Vector3Data[] Tangents;
        public Vector3Data[] Bitangents;
        public ushort[] Indices;
        public MDLFaceData[] Faces;

        // Skinning data (if applicable)
        public MDLSkinData Skin;

        // Danglymesh data (if applicable)
        public MDLDanglymeshData Danglymesh;
    }

    /// <summary>
    /// Face data structure representing a single triangle face in the mesh.
    /// Contains face normal, plane distance, material index, adjacency information, and vertex indices.
    /// </summary>
    public struct MDLFaceData
    {
        public Vector3Data Normal;
        public float PlaneDistance;
        public int Material;
        public short Adjacent0;
        public short Adjacent1;
        public short Adjacent2;
        public short Vertex0;
        public short Vertex1;
        public short Vertex2;
    }

    /// <summary>
    /// Skinning data for skeletal animation.
    /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Skinmesh Header
    /// </summary>
    public sealed class MDLSkinData
    {
        // MDX offsets for bone data
        public int MDXBoneWeightsOffset;
        public int MDXBoneIndicesOffset;

        // Per-vertex skinning data (4 bones per vertex)
        public float[] BoneWeights;
        public int[] BoneIndices;

        // Bone mapping (local bone index to skeleton bone number)
        public int[] BoneMap;

        // Bind pose data
        public Vector4Data[] QBones;  // Quaternion rotations (W, X, Y, Z)
        public Vector3Data[] TBones;  // Translation vectors
    }

    /// <summary>
    /// Light node data.
    /// </summary>
    public sealed class MDLLightData
    {
        public float FlareRadius;
        public int LightPriority;
        public bool AmbientOnly;
        public int DynamicType;
        public bool AffectDynamic;
        public bool Shadow;
        public bool Flare;
        public bool FadingLight;
        public float[] FlareSizes;
        public float[] FlarePositions;
        public Vector3Data[] FlareColorShifts;
        public string[] FlareTextures;
    }

    /// <summary>
    /// Emitter node data for particle effects.
    /// </summary>
    public sealed class MDLEmitterData
    {
        public float DeadSpace;
        public float BlastRadius;
        public float BlastLength;
        public int BranchCount;
        public float ControlPtSmoothing;
        public int XGrid;
        public int YGrid;
        public string UpdateScript;
        public string RenderScript;
        public string BlendScript;
        public string Texture;
        public string ChunkName;
        public bool TwoSidedTex;
        public bool Loop;
        public int RenderOrder;
        public bool FrameBlending;
        public string DepthTexture;
        public uint Flags;
    }

    /// <summary>
    /// Reference node data for external model attachments.
    /// </summary>
    public sealed class MDLReferenceData
    {
        public string ModelResRef;
        public bool Reattachable;
    }

    /// <summary>
    /// Danglymesh data for physics simulation (cloth/hair).
    /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Danglymesh Header
    /// </summary>
    public sealed class MDLDanglymeshData
    {
        public float[] Constraints;      // Per-vertex constraint values
        public float Displacement;       // Maximum displacement distance
        public float Tightness;          // Spring stiffness (0.0-1.0)
        public float Period;             // Oscillation period in seconds
    }
}

