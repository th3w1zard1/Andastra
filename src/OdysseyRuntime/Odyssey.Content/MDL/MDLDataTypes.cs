using System;
using System.Collections.Generic;

namespace Odyssey.Content.MDL
{
    /// <summary>
    /// Lightweight vector types for MDL data without external dependencies.
    /// These are used for intermediate parsing before conversion to rendering-specific types.
    /// </summary>
    public struct Vector2Data
    {
        public float X;
        public float Y;

        public Vector2Data(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public struct Vector3Data
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct Vector4Data
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

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
        public int MDXUnknown1Offset;
        public int MDXUnknown2Offset;
        public int MDXUnknown3Offset;

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
        public ushort[] Indices;
        public MDLFaceData[] Faces;

        // Skinning data (if applicable)
        public MDLSkinData Skin;
    }

    /// <summary>
    /// Face data structure.
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
}

