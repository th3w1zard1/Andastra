using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.MDL;

namespace Andastra.Parsing.Formats.MDLData
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py
    // Comprehensive data containers mirroring all fields from MDL/MDX binary structures.
    // Binary format references:
    // - vendor/mdlops/MDLOpsM.pm:172 - Node header structure (80 bytes: "SSSSllffffffflllllllll")
    // - vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Complete format specification
    // - vendor/reone/src/libs/graphics/format/mdlmdxreader.cpp - Binary parsing implementation
    // - vendor/kotorblender/io_scene_kotor/format/mdl/reader.py - Python parsing reference
    //
    // These classes represent high-level data structures for MDL/MDX model files used in BioWare games
    // (KotOR, KotOR 2, Dragon Age, etc.). The structures are designed to match the binary format
    // specification while providing a clean, object-oriented interface for model manipulation.
    //
    // References:
    // - vendor/mdlops/MDLOpsM.pm - Authoritative MDL/MDX binary format specification
    // - vendor/reone/src/libs/graphics/format/mdlmdxreader.cpp - Binary format reader implementation
    // - vendor/kotorblender/io_scene_kotor/format/mdl/ - Blender MDL loader/exporter
    // - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py - Python reference implementation
    // - vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Complete format documentation

    public class MDL : IEquatable<MDL>
    {
        public MDLNode Root { get; set; }
        public List<MDLAnimation> Anims { get; set; }
        public string Name { get; set; }
        public bool Fog { get; set; }
        public string Supermodel { get; set; }
        public MDLClassification Classification { get; set; }
        /// <summary>
        /// Unknown subclassification value from model header.
        /// Corresponds to the "Subclassification" byte field in the binary model header.
        /// Purpose unknown - may be reserved for future use or engine-specific flags.
        /// </summary>
        public int ClassificationUnk1 { get; set; }
        public float AnimationScale { get; set; }
        public Vector3 BMin { get; set; }
        public Vector3 BMax { get; set; }
        public float Radius { get; set; }
        public string Headlink { get; set; }
        /// <summary>
        /// Flag indicating whether orientation quaternions should be compressed.
        /// When true, uses compressed quaternion format (2 floats + reconstructed W).
        /// When false, uses full quaternion format (4 floats).
        /// Corresponds to controller data format detection in binary parsing.
        /// </summary>
        public int CompressQuaternions { get; set; }

        public MDL()
        {
            Root = new MDLNode();
            Anims = new List<MDLAnimation>();
            Name = string.Empty;
            Fog = false;
            Supermodel = string.Empty;
            Classification = MDLClassification.OTHER;
            ClassificationUnk1 = 0;
            AnimationScale = 0.971f;
            BMin = new Vector3(-5, -5, -1);
            BMax = new Vector3(5, 5, 10);
            Radius = 7.0f;
            Headlink = string.Empty;
            CompressQuaternions = 0;
        }

        public override bool Equals(object obj) => obj is MDL other && Equals(other);

        public bool Equals(MDL other)
        {
            if (other == null) return false;
            return Root.Equals(other.Root) &&
                   Anims.SequenceEqual(other.Anims) &&
                   Name == other.Name &&
                   Fog == other.Fog &&
                   Supermodel == other.Supermodel &&
                   Classification == other.Classification &&
                   ClassificationUnk1 == other.ClassificationUnk1 &&
                   AnimationScale.Equals(other.AnimationScale) &&
                   BMin.Equals(other.BMin) &&
                   BMax.Equals(other.BMax) &&
                   Radius.Equals(other.Radius) &&
                   Headlink == other.Headlink &&
                   CompressQuaternions == other.CompressQuaternions;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Root);
            foreach (var a in Anims) hash.Add(a);
            hash.Add(Name);
            hash.Add(Fog);
            hash.Add(Supermodel);
            hash.Add(Classification);
            hash.Add(ClassificationUnk1);
            hash.Add(AnimationScale);
            hash.Add(BMin);
            hash.Add(BMax);
            hash.Add(Radius);
            hash.Add(Headlink);
            hash.Add(CompressQuaternions);
            return hash.ToHashCode();
        }

        /// <summary>
        /// Gets a node by name from the tree.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:110-134
        /// </summary>
        public MDLNode Get(string nodeName)
        {
            var nodes = new List<MDLNode> { Root };
            while (nodes.Count > 0)
            {
                var node = nodes[nodes.Count - 1];
                nodes.RemoveAt(nodes.Count - 1);

                if (node.Name == nodeName)
                {
                    return node;
                }

                if (node.Children != null)
                {
                    nodes.AddRange(node.Children);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a list of all nodes in the tree including the root node and children recursively.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:136-155
        /// </summary>
        public List<MDLNode> AllNodes()
        {
            var nodes = new List<MDLNode>();
            var scan = new List<MDLNode> { Root };
            while (scan.Count > 0)
            {
                var node = scan[scan.Count - 1];
                scan.RemoveAt(scan.Count - 1);
                nodes.Add(node);
                if (node.Children != null)
                {
                    scan.AddRange(node.Children);
                }
            }
            return nodes;
        }

        /// <summary>
        /// Find the parent node of the given child node.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:157-176
        /// </summary>
        public MDLNode FindParent(MDLNode child)
        {
            var allNodes = AllNodes();
            foreach (var node in allNodes)
            {
                if (node.Children != null && node.Children.Contains(child))
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the global position of a node by traversing up the parent chain.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:178-197
        /// </summary>
        public Vector3 GlobalPosition(MDLNode node)
        {
            var position = node.Position;
            var parent = FindParent(node);
            while (parent != null)
            {
                position += parent.Position;
                parent = FindParent(parent);
            }
            return position;
        }

        /// <summary>
        /// Get node by node id.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:199-216
        /// </summary>
        public MDLNode GetByNodeId(int nodeId)
        {
            foreach (var node in AllNodes())
            {
                if (node.NodeId == nodeId)
                {
                    return node;
                }
            }
            throw new ArgumentException($"No node with id {nodeId}");
        }

        /// <summary>
        /// Returns all unique texture names used in the scene.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:218-235
        /// </summary>
        public HashSet<string> AllTextures()
        {
            var textures = new HashSet<string>();
            foreach (var node in AllNodes())
            {
                if (node.Mesh != null && !string.IsNullOrEmpty(node.Mesh.Texture1) && node.Mesh.Texture1 != "NULL")
                {
                    textures.Add(node.Mesh.Texture1);
                }
            }
            return textures;
        }

        /// <summary>
        /// Returns a set of all lightmap textures used in the scene.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:237-254
        /// </summary>
        public HashSet<string> AllLightmaps()
        {
            var lightmaps = new HashSet<string>();
            foreach (var node in AllNodes())
            {
                if (node.Mesh != null && !string.IsNullOrEmpty(node.Mesh.Texture2) && node.Mesh.Texture2 != "NULL")
                {
                    lightmaps.Add(node.Mesh.Texture2);
                }
            }
            return lightmaps;
        }

        /// <summary>
        /// Alias for AllNodes() for test compatibility.
        /// </summary>
        public List<MDLNode> GetAllNodes()
        {
            return AllNodes();
        }
    }

    public class MDLAnimation : IEquatable<MDLAnimation>
    {
        public string Name { get; set; }
        public string RootModel { get; set; }
        public float AnimLength { get; set; }
        public float TransitionLength { get; set; }
        public List<MDLEvent> Events { get; set; }
        public MDLNode Root { get; set; }

        public MDLAnimation()
        {
            Name = string.Empty;
            RootModel = string.Empty;
            AnimLength = 0.0f;
            TransitionLength = 0.0f;
            Events = new List<MDLEvent>();
            Root = new MDLNode();
        }

        public override bool Equals(object obj) => obj is MDLAnimation other && Equals(other);

        public bool Equals(MDLAnimation other)
        {
            if (other == null) return false;
            return Name == other.Name &&
                   RootModel == other.RootModel &&
                   AnimLength.Equals(other.AnimLength) &&
                   TransitionLength.Equals(other.TransitionLength) &&
                   Events.SequenceEqual(other.Events) &&
                   Root.Equals(other.Root);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            hash.Add(RootModel);
            hash.Add(AnimLength);
            hash.Add(TransitionLength);
            foreach (var e in Events) hash.Add(e);
            hash.Add(Root);
            return hash.ToHashCode();
        }

        /// <summary>
        /// Returns all nodes in the animation tree including children recursively.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:370-389
        /// </summary>
        public List<MDLNode> AllNodes()
        {
            var nodes = new List<MDLNode>();
            var scan = new List<MDLNode> { Root };
            while (scan.Count > 0)
            {
                var node = scan[scan.Count - 1];
                scan.RemoveAt(scan.Count - 1);
                nodes.Add(node);
                if (node.Children != null)
                {
                    scan.AddRange(node.Children);
                }
            }
            return nodes;
        }
    }

    public class MDLControllerRow : IEquatable<MDLControllerRow>
    {
        public float Time { get; set; }
        public List<float> Data { get; set; }

        public MDLControllerRow()
        {
            Time = 0.0f;
            Data = new List<float>();
        }

        public override bool Equals(object obj) => obj is MDLControllerRow other && Equals(other);

        public bool Equals(MDLControllerRow other)
        {
            if (other == null) return false;
            if (!Time.Equals(other.Time)) return false;
            if (Data.Count != other.Data.Count) return false;
            for (int i = 0; i < Data.Count; i++)
            {
                if (!Data[i].Equals(other.Data[i])) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Time);
            foreach (var d in Data) hash.Add(d);
            return hash.ToHashCode();
        }
    }

    public class MDLController : IEquatable<MDLController>
    {
        public MDLControllerType ControllerType { get; set; }
        public MDLControllerType Type
        {
            get { return ControllerType; }
            set { ControllerType = value; }
        }
        public bool IsBezier { get; set; }
        public List<MDLControllerRow> Rows { get; set; }
        public int DataOffset { get; set; }
        public int ColumnCount { get; set; }
        public int RowCount { get; set; }
        public int TimekeysOffset { get; set; }
        public int Columns { get; set; }

        public MDLController()
        {
            ControllerType = MDLControllerType.INVALID;
            IsBezier = false;
            Rows = new List<MDLControllerRow>();
        }

        public override bool Equals(object obj) => obj is MDLController other && Equals(other);

        public bool Equals(MDLController other)
        {
            if (other == null) return false;
            return ControllerType == other.ControllerType &&
                   DataOffset == other.DataOffset &&
                   ColumnCount == other.ColumnCount &&
                   RowCount == other.RowCount &&
                   TimekeysOffset == other.TimekeysOffset &&
                   Columns == other.Columns &&
                   Rows.SequenceEqual(other.Rows);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(ControllerType);
            hash.Add(DataOffset);
            hash.Add(ColumnCount);
            hash.Add(RowCount);
            hash.Add(TimekeysOffset);
            hash.Add(Columns);
            foreach (var r in Rows) hash.Add(r);
            return hash.ToHashCode();
        }
    }

    public class MDLEvent : IEquatable<MDLEvent>
    {
        public float ActivationTime { get; set; }
        public string Name { get; set; }

        public MDLEvent()
        {
            ActivationTime = 0.0f;
            Name = string.Empty;
        }

        public override bool Equals(object obj) => obj is MDLEvent other && Equals(other);

        public bool Equals(MDLEvent other)
        {
            if (other == null) return false;
            return ActivationTime.Equals(other.ActivationTime) && Name == other.Name;
        }

        public override int GetHashCode() => HashCode.Combine(ActivationTime, Name);
    }

    public class MDLBoneVertex : IEquatable<MDLBoneVertex>
    {
        public Tuple<float, float, float, float> VertexWeights { get; set; }
        public Tuple<float, float, float, float> VertexIndices { get; set; }

        public MDLBoneVertex()
        {
            VertexWeights = Tuple.Create(0.0f, 0.0f, 0.0f, 0.0f);
            VertexIndices = Tuple.Create(-1.0f, -1.0f, -1.0f, -1.0f);
        }

        public override bool Equals(object obj) => obj is MDLBoneVertex other && Equals(other);

        public bool Equals(MDLBoneVertex other)
        {
            if (other == null) return false;
            return VertexWeights.Equals(other.VertexWeights) && VertexIndices.Equals(other.VertexIndices);
        }

        public override int GetHashCode() => HashCode.Combine(VertexWeights, VertexIndices);
    }

    public class MDLFace : IEquatable<MDLFace>
    {
        public int V1 { get; set; }
        public int V2 { get; set; }
        public int V3 { get; set; }
        public SurfaceMaterial Material { get; set; }
        public int SmoothingGroup { get; set; }
        public int SurfaceLight { get; set; }
        public float PlaneDistance { get; set; }
        public Vector3 Normal { get; set; }
        // Binary MDL format properties (matching PyKotor mdl_data.py:MDLFace)
        public int A1 { get; set; }  // Adjacent face 1 index
        public int A2 { get; set; }  // Adjacent face 2 index
        public int A3 { get; set; }  // Adjacent face 3 index
        public int Coefficient { get; set; }  // Plane coefficient (stored as int in binary, but can be float)

        public MDLFace()
        {
            Material = SurfaceMaterial.Undefined;
            Normal = Vector3.Zero;
        }

        public override bool Equals(object obj) => obj is MDLFace other && Equals(other);

        public bool Equals(MDLFace other)
        {
            if (other == null) return false;
            return V1 == other.V1 && V2 == other.V2 && V3 == other.V3 &&
                   Material == other.Material && SmoothingGroup == other.SmoothingGroup &&
                   SurfaceLight == other.SurfaceLight && PlaneDistance.Equals(other.PlaneDistance) &&
                   Normal.Equals(other.Normal) && A1 == other.A1 && A2 == other.A2 && A3 == other.A3 &&
                   Coefficient == other.Coefficient;
        }

        public override int GetHashCode()
        {
            int hash = HashCode.Combine(V1, V2, V3, Material, SmoothingGroup, SurfaceLight, PlaneDistance, Normal);
            hash = HashCode.Combine(hash, A1, A2, A3, Coefficient);
            return hash;
        }
    }

    public class MDLConstraint : IEquatable<MDLConstraint>
    {
        public string Name { get; set; }
        public int Type { get; set; }
        public int Target { get; set; }
        public int TargetNode { get; set; }

        public MDLConstraint()
        {
            Name = string.Empty;
        }

        public override bool Equals(object obj) => obj is MDLConstraint other && Equals(other);

        public bool Equals(MDLConstraint other)
        {
            if (other == null) return false;
            return Name == other.Name && Type == other.Type && Target == other.Target && TargetNode == other.TargetNode;
        }

        public override int GetHashCode() => HashCode.Combine(Name, Type, Target, TargetNode);
    }

    public class MDLLight : IEquatable<MDLLight>
    {
        public float FlareRadius { get; set; }
        public int LightPriority { get; set; }
        public bool AmbientOnly { get; set; }
        public int DynamicType { get; set; }
        public bool Shadow { get; set; }
        public bool Flare { get; set; }
        public bool FadingLight { get; set; }
        public List<float> FlareSizes { get; set; }
        public List<float> FlarePositions { get; set; }
        public List<float> FlareColorShifts { get; set; }
        public List<string> FlareTextures { get; set; }
        public int FlareCount { get; set; }
        public MDLLightFlags LightFlags { get; set; }
        public Color Color { get; set; }
        public float Multiplier { get; set; }
        public float Cutoff { get; set; }
        public bool Corona { get; set; }
        public float CoronaStrength { get; set; }
        public float CoronaSize { get; set; }
        public string ShadowTexture { get; set; }
        public float FlareSizeFactor { get; set; }
        public float FlareInnerStrength { get; set; }
        public float FlareOuterStrength { get; set; }

        public MDLLight()
        {
            FlareSizes = new List<float>();
            FlarePositions = new List<float>();
            FlareColorShifts = new List<float>();
            FlareTextures = new List<string>();
            LightFlags = 0;
            Color = new Color(ParsingColor.WHITE.R, ParsingColor.WHITE.G, ParsingColor.WHITE.B, ParsingColor.WHITE.A);
            ShadowTexture = string.Empty;
        }

        public override bool Equals(object obj) => obj is MDLLight other && Equals(other);

        public bool Equals(MDLLight other)
        {
            if (other == null) return false;
            return FlareRadius.Equals(other.FlareRadius) &&
                   LightPriority == other.LightPriority &&
                   AmbientOnly == other.AmbientOnly &&
                   DynamicType == other.DynamicType &&
                   Shadow == other.Shadow &&
                   Flare == other.Flare &&
                   FadingLight == other.FadingLight &&
                   FlareSizes.SequenceEqual(other.FlareSizes) &&
                   FlarePositions.SequenceEqual(other.FlarePositions) &&
                   FlareColorShifts.SequenceEqual(other.FlareColorShifts) &&
                   FlareTextures.SequenceEqual(other.FlareTextures) &&
                   FlareCount == other.FlareCount &&
                   LightFlags == other.LightFlags &&
                   Color.Equals(other.Color) &&
                   Multiplier.Equals(other.Multiplier) &&
                   Cutoff.Equals(other.Cutoff) &&
                   Corona == other.Corona &&
                   CoronaStrength.Equals(other.CoronaStrength) &&
                   CoronaSize.Equals(other.CoronaSize) &&
                   ShadowTexture == other.ShadowTexture &&
                   FlareSizeFactor.Equals(other.FlareSizeFactor) &&
                   FlareInnerStrength.Equals(other.FlareInnerStrength) &&
                   FlareOuterStrength.Equals(other.FlareOuterStrength);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(FlareRadius);
            hash.Add(LightPriority);
            hash.Add(AmbientOnly);
            hash.Add(DynamicType);
            hash.Add(Shadow);
            hash.Add(Flare);
            hash.Add(FadingLight);
            foreach (var v in FlareSizes) hash.Add(v);
            foreach (var v in FlarePositions) hash.Add(v);
            foreach (var v in FlareColorShifts) hash.Add(v);
            foreach (var v in FlareTextures) hash.Add(v);
            hash.Add(FlareCount);
            hash.Add(LightFlags);
            hash.Add(Color);
            hash.Add(Multiplier);
            hash.Add(Cutoff);
            hash.Add(Corona);
            hash.Add(CoronaStrength);
            hash.Add(CoronaSize);
            hash.Add(ShadowTexture);
            hash.Add(FlareSizeFactor);
            hash.Add(FlareInnerStrength);
            hash.Add(FlareOuterStrength);
            return hash.ToHashCode();
        }
    }

    public class MDLEmitter : IEquatable<MDLEmitter>
    {
        public float DeadSpace { get; set; }
        public float BlastRadius { get; set; }
        public float BlastLength { get; set; }
        public int BranchCount { get; set; }
        public float ControlPointSmoothing { get; set; }
        public int XGrid { get; set; }
        public int YGrid { get; set; }
        public int SpawnType { get; set; }  // Spawn location mode (0=Normal, 1=Trail) - vendor/mdlops/MDLOpsM.pm:3278
        public MDLRenderType RenderType { get; set; }
        public MDLUpdateType UpdateType { get; set; }
        public MDLBlendType BlendType { get; set; }
        public string Texture { get; set; }
        public string ChunkName { get; set; }
        public bool Twosided { get; set; }
        public bool Loop { get; set; }
        public int RenderOrder { get; set; }
        public bool FrameBlend { get; set; }
        public string DepthTexture { get; set; }
        public int UpdateFlags { get; set; }
        public int RenderFlags { get; set; }

        // ASCII MDL format compatibility property
        public int Flags { get; set; }

        public MDLEmitter()
        {
            Texture = string.Empty;
            ChunkName = string.Empty;
            DepthTexture = string.Empty;
            Flags = 0;
        }

        public override bool Equals(object obj) => obj is MDLEmitter other && Equals(other);

        public bool Equals(MDLEmitter other)
        {
            if (other == null) return false;
            return DeadSpace.Equals(other.DeadSpace) &&
                   BlastRadius.Equals(other.BlastRadius) &&
                   BlastLength.Equals(other.BlastLength) &&
                   BranchCount == other.BranchCount &&
                   ControlPointSmoothing.Equals(other.ControlPointSmoothing) &&
                   XGrid == other.XGrid &&
                   YGrid == other.YGrid &&
                   SpawnType == other.SpawnType &&
                   RenderType == other.RenderType &&
                   UpdateType == other.UpdateType &&
                   BlendType == other.BlendType &&
                   Texture == other.Texture &&
                   ChunkName == other.ChunkName &&
                   Twosided == other.Twosided &&
                   Loop == other.Loop &&
                   RenderOrder == other.RenderOrder &&
                   FrameBlend == other.FrameBlend &&
                   DepthTexture == other.DepthTexture &&
                   UpdateFlags == other.UpdateFlags &&
                   RenderFlags == other.RenderFlags;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(DeadSpace);
            hash.Add(BlastRadius);
            hash.Add(BlastLength);
            hash.Add(BranchCount);
            hash.Add(ControlPointSmoothing);
            hash.Add(XGrid);
            hash.Add(YGrid);
            hash.Add(SpawnType);
            hash.Add(RenderType);
            hash.Add(UpdateType);
            hash.Add(BlendType);
            hash.Add(Texture);
            hash.Add(ChunkName);
            hash.Add(Twosided);
            hash.Add(Loop);
            hash.Add(RenderOrder);
            hash.Add(FrameBlend);
            hash.Add(DepthTexture);
            hash.Add(UpdateFlags);
            hash.Add(RenderFlags);
            return hash.ToHashCode();
        }
    }

    public class MDLDangly : MDLMesh, IEquatable<MDLDangly>
    {
        public float Displacement { get; set; }
        public float Tightness { get; set; }
        public float Period { get; set; }
        public Vector3 Constraints { get; set; }

        public MDLDangly()
            : base()
        {
            Constraints = Vector3.Zero;
        }

        public override bool Equals(object obj) => obj is MDLDangly other && Equals(other);

        public bool Equals(MDLDangly other)
        {
            if (other == null) return false;
            return base.Equals(other) &&
                   Displacement.Equals(other.Displacement) &&
                   Tightness.Equals(other.Tightness) &&
                   Period.Equals(other.Period) &&
                   Constraints.Equals(other.Constraints);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(base.GetHashCode());
            hash.Add(Displacement);
            hash.Add(Tightness);
            hash.Add(Period);
            hash.Add(Constraints);
            return hash.ToHashCode();
        }
    }

    public class MDLSaber : IEquatable<MDLSaber>
    {
        public MDLSaberFlags Flags { get; set; }
        public float BladeLength { get; set; }
        public float BladeWidth { get; set; }
        public float BladeScale { get; set; }
        public float Hardness { get; set; }
        public string Texture { get; set; }
        public string EnvTexture { get; set; }

        // ASCII MDL format compatibility properties
        public int SaberType { get; set; }
        public int SaberColor { get; set; }
        public float SaberLength { get; set; }
        public float SaberWidth { get; set; }
        public int SaberFlareColor { get; set; }
        public float SaberFlareRadius { get; set; }

        public MDLSaber()
        {
            Texture = string.Empty;
            EnvTexture = string.Empty;
            SaberType = 0;
            SaberColor = 0;
            SaberLength = 0.0f;
            SaberWidth = 0.0f;
            SaberFlareColor = 0;
            SaberFlareRadius = 0.0f;
        }

        public override bool Equals(object obj) => obj is MDLSaber other && Equals(other);

        public bool Equals(MDLSaber other)
        {
            if (other == null) return false;
            return Flags == other.Flags &&
                   BladeLength.Equals(other.BladeLength) &&
                   BladeWidth.Equals(other.BladeWidth) &&
                   BladeScale.Equals(other.BladeScale) &&
                   Hardness.Equals(other.Hardness) &&
                   Texture == other.Texture &&
                   EnvTexture == other.EnvTexture;
        }

        public override int GetHashCode() => HashCode.Combine(Flags, BladeLength, BladeWidth, BladeScale, Hardness, Texture, EnvTexture);
    }

    public class MDLReference : IEquatable<MDLReference>
    {
        public string ModelName { get; set; }
        public string SupermodelName { get; set; }
        public int DummyRot { get; set; }

        // ASCII MDL format compatibility properties
        public string Model { get; set; }
        public bool Reattachable { get; set; }

        public MDLReference()
        {
            ModelName = string.Empty;
            SupermodelName = string.Empty;
            Model = string.Empty;
            Reattachable = false;
        }

        public override bool Equals(object obj) => obj is MDLReference other && Equals(other);

        public bool Equals(MDLReference other)
        {
            if (other == null) return false;
            return ModelName == other.ModelName &&
                   SupermodelName == other.SupermodelName &&
                   DummyRot == other.DummyRot;
        }

        public override int GetHashCode() => HashCode.Combine(ModelName, SupermodelName, DummyRot);
    }

    public class MDLSkin : IEquatable<MDLSkin>
    {
        public List<int> BoneSerials { get; set; }
        public List<int> BoneNumbers { get; set; }
        public List<MDLBoneVertex> BoneWeights { get; set; }
        public List<int> BoneWeightIndices { get; set; }
        public int NodeCount { get; set; }

        // ASCII MDL format compatibility properties
        public List<Vector4> Qbones { get; set; }
        public List<Vector3> Tbones { get; set; }
        public List<int> BoneIndices { get; set; }
        public List<MDLBoneVertex> VertexBones { get; set; }

        public MDLSkin()
        {
            BoneSerials = new List<int>();
            BoneNumbers = new List<int>();
            BoneWeights = new List<MDLBoneVertex>();
            BoneWeightIndices = new List<int>();
            Qbones = new List<Vector4>();
            Tbones = new List<Vector3>();
            BoneIndices = new List<int>();
            VertexBones = new List<MDLBoneVertex>();
        }

        public override bool Equals(object obj) => obj is MDLSkin other && Equals(other);

        public bool Equals(MDLSkin other)
        {
            if (other == null) return false;
            return BoneSerials.SequenceEqual(other.BoneSerials) &&
                   BoneNumbers.SequenceEqual(other.BoneNumbers) &&
                   BoneWeights.SequenceEqual(other.BoneWeights) &&
                   BoneWeightIndices.SequenceEqual(other.BoneWeightIndices) &&
                   NodeCount == other.NodeCount;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var v in BoneSerials) hash.Add(v);
            foreach (var v in BoneNumbers) hash.Add(v);
            foreach (var v in BoneWeights) hash.Add(v);
            foreach (var v in BoneWeightIndices) hash.Add(v);
            hash.Add(NodeCount);
            return hash.ToHashCode();
        }
    }

    public class MDLMesh : IEquatable<MDLMesh>
    {
        public int NodeNumber { get; set; }
        public int ParentNode { get; set; }
        public int Model { get; set; }
        public List<Vector3> Vertices { get; set; }
        public List<Vector3> Normals { get; set; }
        public List<Vector2> UV1 { get; set; }
        public List<Vector2> UV2 { get; set; }
        public List<MDLFace> Faces { get; set; }
        public List<int> Tverts1 { get; set; }
        public List<int> Tverts2 { get; set; }
        public List<int> Edges { get; set; }
        public List<int> SmoothGroups { get; set; }
        public List<MDLConstraint> Constraints { get; set; }
        public MDLLight Light { get; set; }
        public MDLEmitter Emitter { get; set; }
        public MDLDangly Dangly { get; set; }
        public MDLSaber Saber { get; set; }
        public MDLSkin Skin { get; set; }
        public string Texture1 { get; set; }
        public string Texture2 { get; set; }
        public Vector3 Diffuse { get; set; }
        public Vector3 Ambient { get; set; }
        public MDLTrimeshProps TrimeshProps { get; set; }
        public MDLTrimeshFlags TrimeshFlags { get; set; }
        public float Lightmapped { get; set; }
        public float Tilefade { get; set; }
        public Vector2 TexOffset { get; set; }
        public Vector2 TexScale { get; set; }
        public float Tint { get; set; }
        public int Beaming { get; set; }
        public int Render { get; set; }
        public float Alpha { get; set; }
        public float Aabb { get; set; }
        public float SelfIllumColor { get; set; }
        public float Shadow { get; set; }
        public float BBoxMinX { get; set; }
        public float BBoxMinY { get; set; }
        public float BBoxMinZ { get; set; }
        public float BBoxMaxX { get; set; }
        public float BBoxMaxY { get; set; }
        public float BBoxMaxZ { get; set; }
        public int HasWeight { get; set; }
        public int Transpar { get; set; }
        public int Rotational { get; set; }
        /// <summary>
        /// Unknown field 12 in MDL mesh structure.
        /// Based on MDLOps trimesh header template analysis, this may correspond to
        /// one of the unknown uint32 fields in the binary trimesh header structure.
        /// Purpose unknown. Currently unimplemented - defaults to 0.
        /// </summary>
        public int Unknown12 { get; set; }

        // ASCII MDL format compatibility properties
        public int TransparencyHint { get; set; }
        public bool HasLightmap { get; set; }
        // Binary MDL format properties (matching PyKotor mdl_data.py:MDLMesh)
        public Vector3 AveragePoint { get; set; }  // Average point of mesh
        public float Area { get; set; }  // Total surface area
        public float Radius { get; set; }  // Bounding sphere radius
        public Vector3 BbMin { get; set; }  // Bounding box minimum (alternative to BBoxMinX/Y/Z)
        public Vector3 BbMax { get; set; }  // Bounding box maximum (alternative to BBoxMaxX/Y/Z)
        // UV animation properties (matching PyKotor mdl_data.py:MDLMesh)
        // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:1243-1249
        public float UvDirectionX { get; set; }  // UV scroll direction X component
        public float UvDirectionY { get; set; }  // UV scroll direction Y component
        public float UvJitter { get; set; }  // UV jitter amount
        public float UvJitterSpeed { get; set; }  // UV jitter animation speed
        // Texture and rendering properties (matching PyKotor mdl_data.py:MDLMesh)
        // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:1255-1256,1284-1286
        public bool RotateTexture { get; set; }  // Rotate texture 90 degrees
        public bool BackgroundGeometry { get; set; }  // Render in background pass
        // Dirt/weathering properties (matching PyKotor mdl_data.py:MDLMesh)
        // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:1284-1286
        public bool DirtEnabled { get; set; }  // Dirt/weathering overlay texture enabled
        public string DirtTexture { get; set; }  // Dirt texture name
        // Saber-specific properties (matching PyKotor mdl_data.py:MDLMesh)
        // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:1223
        /// <summary>
        /// Saber-specific unknown data (8 bytes).
        /// Used only for lightsaber mesh nodes (NODE_HAS_SABER flag set).
        /// Based on MDLOps analysis, this corresponds to saber mesh header data after the standard trimesh header.
        /// Default values: { 3, 0, 0, 0, 0, 0, 0, 0 }
        /// The first byte (value 3) may indicate saber piece type or rendering mode.
        /// Purpose of remaining 7 bytes unknown.
        /// </summary>
        public byte[] SaberUnknowns { get; set; }
        public List<Vector3> VertexPositions
        {
            get { return Vertices; }
            set { Vertices = value; }
        }
        public List<Vector3> VertexNormals
        {
            get { return Normals; }
            set { Normals = value; }
        }
        public List<Vector2> VertexUv1
        {
            get { return UV1; }
            set { UV1 = value; }
        }
        public List<Vector2> VertexUv2
        {
            get { return UV2; }
            set { UV2 = value; }
        }

        public MDLMesh()
        {
            Vertices = new List<Vector3>();
            Normals = new List<Vector3>();
            UV1 = new List<Vector2>();
            UV2 = new List<Vector2>();
            Faces = new List<MDLFace>();
            Tverts1 = new List<int>();
            Tverts2 = new List<int>();
            Edges = new List<int>();
            SmoothGroups = new List<int>();
            Constraints = new List<MDLConstraint>();
            Texture1 = "NULL";
            Texture2 = "NULL";
            Diffuse = Vector3.Zero;
            Ambient = Vector3.Zero;
            TexOffset = new Vector2(0, 0);
            TexScale = new Vector2(1, 1);
            // Initialize UV animation properties (matching PyKotor defaults)
            UvDirectionX = 0.0f;
            UvDirectionY = 0.0f;
            UvJitter = 0.0f;
            UvJitterSpeed = 0.0f;
            // Initialize texture and rendering properties (matching PyKotor defaults)
            RotateTexture = false;
            BackgroundGeometry = false;
            // Initialize dirt properties (matching PyKotor defaults)
            DirtEnabled = false;
            DirtTexture = string.Empty;
            // Initialize saber unknowns (matching PyKotor default: 3,0,0,0,0,0,0,0)
            SaberUnknowns = new byte[] { 3, 0, 0, 0, 0, 0, 0, 0 };
        }

        public override bool Equals(object obj) => obj is MDLMesh other && Equals(other);

        public bool Equals(MDLMesh other)
        {
            if (other == null) return false;
            return NodeNumber == other.NodeNumber &&
                   ParentNode == other.ParentNode &&
                   Model == other.Model &&
                   Vertices.SequenceEqual(other.Vertices) &&
                   Normals.SequenceEqual(other.Normals) &&
                   UV1.SequenceEqual(other.UV1) &&
                   UV2.SequenceEqual(other.UV2) &&
                   Faces.SequenceEqual(other.Faces) &&
                   Tverts1.SequenceEqual(other.Tverts1) &&
                   Tverts2.SequenceEqual(other.Tverts2) &&
                   Edges.SequenceEqual(other.Edges) &&
                   SmoothGroups.SequenceEqual(other.SmoothGroups) &&
                   Constraints.SequenceEqual(other.Constraints) &&
                   Equals(Light, other.Light) &&
                   Equals(Emitter, other.Emitter) &&
                   Equals(Dangly, other.Dangly) &&
                   Equals(Saber, other.Saber) &&
                   Equals(Skin, other.Skin) &&
                   Texture1 == other.Texture1 &&
                   Texture2 == other.Texture2 &&
                   Diffuse.Equals(other.Diffuse) &&
                   Ambient.Equals(other.Ambient) &&
                   TrimeshProps == other.TrimeshProps &&
                   TrimeshFlags == other.TrimeshFlags &&
                   Lightmapped.Equals(other.Lightmapped) &&
                   Tilefade.Equals(other.Tilefade) &&
                   TexOffset.Equals(other.TexOffset) &&
                   TexScale.Equals(other.TexScale) &&
                   Tint.Equals(other.Tint) &&
                   Beaming == other.Beaming &&
                   Render == other.Render &&
                   Alpha.Equals(other.Alpha) &&
                   Aabb.Equals(other.Aabb) &&
                   SelfIllumColor.Equals(other.SelfIllumColor) &&
                   Shadow.Equals(other.Shadow) &&
                   BBoxMinX.Equals(other.BBoxMinX) &&
                   BBoxMinY.Equals(other.BBoxMinY) &&
                   BBoxMinZ.Equals(other.BBoxMinZ) &&
                   BBoxMaxX.Equals(other.BBoxMaxX) &&
                   BBoxMaxY.Equals(other.BBoxMaxY) &&
                   BBoxMaxZ.Equals(other.BBoxMaxZ) &&
                   HasWeight == other.HasWeight &&
                   Transpar == other.Transpar &&
                   Rotational == other.Rotational &&
                   Unknown12 == other.Unknown12;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(NodeNumber);
            hash.Add(ParentNode);
            hash.Add(Model);
            foreach (var v in Vertices) hash.Add(v);
            foreach (var v in Normals) hash.Add(v);
            foreach (var v in UV1) hash.Add(v);
            foreach (var v in UV2) hash.Add(v);
            foreach (var f in Faces) hash.Add(f);
            foreach (var v in Tverts1) hash.Add(v);
            foreach (var v in Tverts2) hash.Add(v);
            foreach (var v in Edges) hash.Add(v);
            foreach (var v in SmoothGroups) hash.Add(v);
            foreach (var v in Constraints) hash.Add(v);
            hash.Add(Light);
            hash.Add(Emitter);
            hash.Add(Dangly);
            hash.Add(Saber);
            hash.Add(Skin);
            hash.Add(Texture1);
            hash.Add(Texture2);
            hash.Add(Diffuse);
            hash.Add(Ambient);
            hash.Add(TrimeshProps);
            hash.Add(TrimeshFlags);
            hash.Add(Lightmapped);
            hash.Add(Tilefade);
            hash.Add(TexOffset);
            hash.Add(TexScale);
            hash.Add(Tint);
            hash.Add(Beaming);
            hash.Add(Render);
            hash.Add(Alpha);
            hash.Add(Aabb);
            hash.Add(SelfIllumColor);
            hash.Add(Shadow);
            hash.Add(BBoxMinX);
            hash.Add(BBoxMinY);
            hash.Add(BBoxMinZ);
            hash.Add(BBoxMaxX);
            hash.Add(BBoxMaxY);
            hash.Add(BBoxMaxZ);
            hash.Add(HasWeight);
            hash.Add(Transpar);
            hash.Add(Rotational);
            hash.Add(Unknown12);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Represents a node in the MDL tree structure. Nodes form a hierarchical tree where each node
    /// can contain geometric data (mesh, skin, dangly, saber, walkmesh), light sources, particle emitters,
    /// or serve as positioning dummies. Controller keyframes can animate node properties over time.
    ///
    /// Binary Format (80-byte node header):
    /// - Offset 0x00: Node type flags (uint16) - bitmask indicating node features
    /// - Offset 0x02: Node index (uint16) - sequential index of this node in the model
    /// - Offset 0x04: Node name index (uint16) - index into the names array for this node's name
    /// - Offset 0x06: Padding (uint16) - alignment padding
    /// - Offset 0x08: Root node offset (uint32) - offset to the model's root node
    /// - Offset 0x0C: Parent node offset (uint32) - offset to this node's parent node (0 if root)
    /// - Offset 0x10: Position X (float) - node position in local space
    /// - Offset 0x14: Position Y (float)
    /// - Offset 0x18: Position Z (float)
    /// - Offset 0x1C: Orientation W (float) - quaternion rotation (W, X, Y, Z order)
    /// - Offset 0x20: Orientation X (float)
    /// - Offset 0x24: Orientation Y (float)
    /// - Offset 0x28: Orientation Z (float)
    /// - Offset 0x2C: Child array offset (uint32) - offset to array of child node offsets
    /// - Offset 0x30: Child count (uint32) - number of child nodes
    /// - Offset 0x34: Child count duplicate (uint32) - duplicate value of child count
    /// - Offset 0x38: Controller array offset (uint32) - offset to array of controller structures
    /// - Offset 0x3C: Controller count (uint32) - number of controllers attached to this node
    /// - Offset 0x40: Controller count duplicate (uint32) - duplicate value of controller count
    /// - Offset 0x44: Controller data offset (uint32) - offset to controller keyframe/data array
    /// - Offset 0x48: Controller data count (uint32) - number of floats in controller data array
    /// - Offset 0x4C: Controller data count duplicate (uint32) - duplicate value of controller data count
    ///
    /// References:
    /// - vendor/mdlops/MDLOpsM.pm:172 - Node header structure definition
    /// - vendor/mdlops/MDLOpsM.pm:1590-1622 - Node header reading implementation
    /// - vendor/reone/include/reone/graphics/modelnode.h:31-287 - ModelNode class definition
    /// - vendor/reone/src/libs/graphics/format/mdlmdxreader.cpp:182-290 - Node loading from MDL
    /// - vendor/kotorblender/io_scene_kotor/format/mdl/reader.py:406-582 - Node reading
    /// - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:466-640 - PyKotor MDLNode
    /// </summary>
    public class MDLNode : IEquatable<MDLNode>
    {
        /// <summary>
        /// Node name (ASCII string, max 32 chars in binary format).
        /// Binary: Offset 0x04 - Node name index (uint16) into names array.
        /// Reference: vendor/reone/src/libs/graphics/format/mdlmdxreader.cpp:212
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Model name for reference nodes (ASCII MDL format compatibility).
        /// Used when node type includes REFERENCE flag (0x10).
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:155-158
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Unique node number within model for quick lookups.
        /// Binary: Offset 0x02 - Node index (uint16).
        /// Reference: vendor/reone/src/libs/graphics/format/mdlmdxreader.cpp:202-203
        /// </summary>
        public int NodeId { get; set; }

        /// <summary>
        /// Local position relative to parent (x, y, z).
        /// Binary: Offset 0x10-0x18 - Position X/Y/Z (float[3]).
        /// Can be animated via position controller (type 8).
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:243
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Local rotation as quaternion (x, y, z, w).
        /// Binary: Offset 0x1C-0x28 - Orientation W/X/Y/Z (float[4], stored as W,X,Y,Z).
        /// Quaternion format ensures smooth interpolation for animation.
        /// Can be animated via orientation controller (type 20).
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:244
        /// </summary>
        public Vector4 Orientation { get; set; }
        /// <summary>
        /// Unknown field 0 in MDL node structure.
        /// Binary: Not directly in node header - may be from trimesh subheader or computed during parsing.
        /// Based on MDLOps analysis, this may correspond to m_bUnknown1 field at index 18 in trimesh subheader.
        /// Possibly a boolean flag related to rendering or processing state.
        /// Reference: vendor/mdlops/MDLOpsM.pm - trimesh header analysis
        /// </summary>
        public int Unknown0 { get; set; }

        /// <summary>
        /// Unknown field 1 in MDL node structure.
        /// Binary: Not directly in node header - may be from trimesh subheader or computed during parsing.
        /// Purpose unknown. Preserved for binary format compatibility.
        /// </summary>
        public int Unknown1 { get; set; }

        /// <summary>
        /// Unknown field 2 in MDL node structure.
        /// Binary: Not directly in node header - may be from trimesh subheader or computed during parsing.
        /// Based on MDLOps analysis, this may correspond to 'unknown' field at index 32 in trimesh subheader.
        /// Purpose unknown. Preserved for binary format compatibility.
        /// Reference: vendor/mdlops/MDLOpsM.pm - trimesh header analysis
        /// </summary>
        public int Unknown2 { get; set; }
        /// <summary>
        /// Whether this node ignores fog effects.
        /// Binary: Derived from model header flags, not directly in node header.
        /// Reference: vendor/mdlops/MDLOpsM.pm:797 - model header ignorefog flag
        /// </summary>
        public bool IgnoreFog { get; set; }

        /// <summary>
        /// Whether this node casts shadows.
        /// Binary: Derived from mesh flags in trimesh subheader (offset 0x137).
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - trimesh header Shadow field
        /// </summary>
        public bool Shadow { get; set; }

        /// <summary>
        /// Animation reference index (ASCII MDL format compatibility).
        /// Binary: Not directly in node header - used for animation node references.
        /// </summary>
        public int Animation { get; set; }

        /// <summary>
        /// Position offset X component (ASCII MDL format compatibility).
        /// Binary: Not directly in node header - typically derived from position controller or computed.
        /// </summary>
        public float OffsetX { get; set; }

        /// <summary>
        /// Position offset Y component (ASCII MDL format compatibility).
        /// Binary: Not directly in node header - typically derived from position controller or computed.
        /// </summary>
        public float OffsetY { get; set; }

        /// <summary>
        /// Position offset Z component (ASCII MDL format compatibility).
        /// Binary: Not directly in node header - typically derived from position controller or computed.
        /// </summary>
        public float OffsetZ { get; set; }

        /// <summary>
        /// Scale X component (ASCII MDL format compatibility).
        /// Binary: Not directly in node header - can be animated via scale controller (type 9).
        /// </summary>
        public float ScaleX { get; set; }

        /// <summary>
        /// Scale Y component (ASCII MDL format compatibility).
        /// Binary: Not directly in node header - can be animated via scale controller (type 9).
        /// </summary>
        public float ScaleY { get; set; }

        /// <summary>
        /// Scale Z component (ASCII MDL format compatibility).
        /// Binary: Not directly in node header - can be animated via scale controller (type 9).
        /// </summary>
        public float ScaleZ { get; set; }

        /// <summary>
        /// Node type flags bitmask indicating node features.
        /// Binary: Offset 0x00 - Node type flags (uint16).
        /// Flags: HEADER (0x01), LIGHT (0x02), EMITTER (0x04), REFERENCE (0x10), MESH (0x20),
        /// SKIN (0x40), DANGLY (0x100), AABB (0x200), SABER (0x800).
        /// Reference: vendor/reone/src/libs/graphics/format/mdlmdxreader.cpp:135-150
        /// </summary>
        public MDLNodeFlags NodeFlags { get; set; }

        /// <summary>
        /// List of child nodes in hierarchy.
        /// Binary: Offset 0x2C - Child array offset (uint32), Offset 0x30 - Child count (uint32).
        /// Child nodes inherit parent transforms and can be enumerated for rendering.
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:219
        /// </summary>
        public List<MDLNode> Children { get; set; }

        /// <summary>
        /// Animation controller keyframe data.
        /// Binary: Offset 0x38 - Controller array offset (uint32), Offset 0x3C - Controller count (uint32),
        /// Offset 0x44 - Controller data offset (uint32), Offset 0x48 - Controller data count (uint32).
        /// Controllers animate position, orientation, scale, color, alpha, and other properties.
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:34
        /// </summary>
        public List<MDLController> Controllers { get; set; }
        /// <summary>
        /// Triangle mesh geometry data (vertices, faces, materials).
        /// Binary: Present when node type includes MESH flag (0x20).
        /// Trimesh header immediately follows 80-byte node header (332 bytes K1, 340 bytes K2).
        /// Contains vertex data in companion MDX file.
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:70-91
        /// </summary>
        public MDLMesh Mesh { get; set; }

        /// <summary>
        /// Reference node (links to external model).
        /// Binary: Present when node type includes REFERENCE flag (0x10).
        /// Reference header: 36 bytes (Z[32] model name + uint32 dummy rotation).
        /// Used for equippable items, attached weapons, etc.
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:155-158
        /// </summary>
        public MDLReference Reference { get; set; }

        /// <summary>
        /// Walkmesh AABB tree for collision/pathfinding.
        /// Binary: Present when node type includes AABB flag (0x200).
        /// Walkmesh header extends trimesh header with additional collision data.
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:55-68
        /// </summary>
        public MDLWalkmesh Walkmesh { get; set; }

        /// <summary>
        /// Node type for ASCII MDL format compatibility.
        /// Binary: Derived from node type flags (offset 0x00).
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:642
        /// </summary>
        public MDLNodeType NodeType { get; set; }

        /// <summary>
        /// Parent node ID for ASCII MDL format compatibility.
        /// Binary: Offset 0x0C - Parent node offset (uint32), converted to ID during parsing.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:645
        /// </summary>
        public int ParentId { get; set; }

        /// <summary>
        /// Light source data (color, radius, flare properties).
        /// Binary: Present when node type includes LIGHT flag (0x02).
        /// Light header: 92 bytes (float[4] color + uint32[12] flares + int32 unknown).
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:116-127
        /// </summary>
        public MDLLight Light { get; set; }

        /// <summary>
        /// Particle emitter data (spawn rate, velocity, textures).
        /// Binary: Present when node type includes EMITTER flag (0x04).
        /// Emitter header: 224 bytes (float[3] position + uint32[5] + 5x Z[32] strings + flags).
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:129-153
        /// </summary>
        public MDLEmitter Emitter { get; set; }

        /// <summary>
        /// Lightsaber blade mesh with special rendering.
        /// Binary: Present when node type includes SABER flag (0x800).
        /// Saber header extends trimesh header with additional blade properties.
        /// Single plane geometry rendered with additive blending.
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:99
        /// </summary>
        public MDLSaber Saber { get; set; }

        /// <summary>
        /// Axis-aligned bounding box tree for walkmesh collision (alias for Walkmesh).
        /// Binary: Present when node type includes AABB flag (0x200).
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:55-68
        /// </summary>
        public MDLWalkmesh Aabb { get; set; }

        /// <summary>
        /// Skinned mesh with bone weighting for character animation.
        /// Binary: Present when node type includes SKIN flag (0x40).
        /// Skin header extends trimesh header with bone binding data (432 bytes K1, 440 bytes K2).
        /// Vertices deform based on bone transforms using weight maps.
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:36-41
        /// </summary>
        public MDLSkin Skin { get; set; }

        /// <summary>
        /// Cloth/hair physics mesh with constraint simulation.
        /// Binary: Present when node type includes DANGLY flag (0x100).
        /// Dangly header extends trimesh header with physics parameters (360 bytes K1, 368 bytes K2).
        /// Vertices constrained by displacement, tightness, period values.
        /// Reference: vendor/reone/include/reone/graphics/modelnode.h:47-53
        /// </summary>
        public MDLDangly Dangly { get; set; }

        public MDLNode()
        {
            Name = string.Empty;
            ModelName = string.Empty;
            Position = Vector3.Zero;
            Orientation = new Vector4(0, 0, 0, 1);
            Children = new List<MDLNode>();
            Controllers = new List<MDLController>();
            NodeType = MDLNodeType.DUMMY;
            ParentId = -1;
        }

        public override bool Equals(object obj) => obj is MDLNode other && Equals(other);

        public bool Equals(MDLNode other)
        {
            if (other == null) return false;
            return Name == other.Name &&
                   ModelName == other.ModelName &&
                   NodeId == other.NodeId &&
                   Position.Equals(other.Position) &&
                   Orientation.Equals(other.Orientation) &&
                   Unknown0 == other.Unknown0 &&
                   Unknown1 == other.Unknown1 &&
                   Unknown2 == other.Unknown2 &&
                   IgnoreFog == other.IgnoreFog &&
                   Shadow == other.Shadow &&
                   Animation == other.Animation &&
                   OffsetX.Equals(other.OffsetX) &&
                   OffsetY.Equals(other.OffsetY) &&
                   OffsetZ.Equals(other.OffsetZ) &&
                   ScaleX.Equals(other.ScaleX) &&
                   ScaleY.Equals(other.ScaleY) &&
                   ScaleZ.Equals(other.ScaleZ) &&
                   NodeFlags == other.NodeFlags &&
                   NodeType == other.NodeType &&
                   ParentId == other.ParentId &&
                   Children.SequenceEqual(other.Children) &&
                   Controllers.SequenceEqual(other.Controllers) &&
                   Equals(Mesh, other.Mesh) &&
                   Equals(Reference, other.Reference) &&
                   Equals(Walkmesh, other.Walkmesh) &&
                   Equals(Light, other.Light) &&
                   Equals(Emitter, other.Emitter) &&
                   Equals(Saber, other.Saber) &&
                   Equals(Aabb, other.Aabb) &&
                   Equals(Skin, other.Skin) &&
                   Equals(Dangly, other.Dangly);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            hash.Add(ModelName);
            hash.Add(NodeId);
            hash.Add(Position);
            hash.Add(Orientation);
            hash.Add(Unknown0);
            hash.Add(Unknown1);
            hash.Add(Unknown2);
            hash.Add(IgnoreFog);
            hash.Add(Shadow);
            hash.Add(Animation);
            hash.Add(OffsetX);
            hash.Add(OffsetY);
            hash.Add(OffsetZ);
            hash.Add(ScaleX);
            hash.Add(ScaleY);
            hash.Add(ScaleZ);
            hash.Add(NodeFlags);
            hash.Add(NodeType);
            hash.Add(ParentId);
            foreach (var c in Children) hash.Add(c);
            foreach (var c in Controllers) hash.Add(c);
            hash.Add(Mesh);
            hash.Add(Reference);
            hash.Add(Walkmesh);
            hash.Add(Light);
            hash.Add(Emitter);
            hash.Add(Saber);
            hash.Add(Aabb);
            hash.Add(Skin);
            hash.Add(Dangly);
            return hash.ToHashCode();
        }
    }

    public class MDLWalkmesh : IEquatable<MDLWalkmesh>
    {
        public string ModelName { get; set; }
        public List<Vector3> Vertices { get; set; }
        public List<MDLFace> Faces { get; set; }
        public List<Vector3> Normals { get; set; }
        public List<int> Adjacency { get; set; }
        public List<int> Adjacency2 { get; set; }

        // ASCII MDL format compatibility property
        public List<MDLNode> Aabbs { get; set; }

        public MDLWalkmesh()
        {
            ModelName = string.Empty;
            Vertices = new List<Vector3>();
            Faces = new List<MDLFace>();
            Normals = new List<Vector3>();
            Adjacency = new List<int>();
            Adjacency2 = new List<int>();
            Aabbs = new List<MDLNode>();
        }

        public override bool Equals(object obj) => obj is MDLWalkmesh other && Equals(other);

        public bool Equals(MDLWalkmesh other)
        {
            if (other == null) return false;
            return ModelName == other.ModelName &&
                   Vertices.SequenceEqual(other.Vertices) &&
                   Faces.SequenceEqual(other.Faces) &&
                   Normals.SequenceEqual(other.Normals) &&
                   Adjacency.SequenceEqual(other.Adjacency) &&
                   Adjacency2.SequenceEqual(other.Adjacency2);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(ModelName);
            foreach (var v in Vertices) hash.Add(v);
            foreach (var f in Faces) hash.Add(f);
            foreach (var n in Normals) hash.Add(n);
            foreach (var a in Adjacency) hash.Add(a);
            foreach (var a in Adjacency2) hash.Add(a);
            return hash.ToHashCode();
        }
    }
}
