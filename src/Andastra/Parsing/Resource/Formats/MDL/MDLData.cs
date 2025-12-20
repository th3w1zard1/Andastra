using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Formats.MDLData
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py
    // Simplified data containers mirroring fields for MDL/MDX structures.

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
        /// Get a node by name.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:115-134
        /// </summary>
        public MDLNode Get(string nodeName)
        {
            MDLNode pick = null;
            var nodes = new List<MDLNode> { Root };
            while (nodes.Count > 0)
            {
                var node = nodes[nodes.Count - 1];
                nodes.RemoveAt(nodes.Count - 1);
                if (node.Name == nodeName)
                {
                    pick = node;
                }
                else
                {
                    if (node.Children != null)
                    {
                        nodes.AddRange(node.Children);
                    }
                }
            }
            return pick;
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
            Color = Color.WHITE;
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

    public class MDLNode : IEquatable<MDLNode>
    {
        public string Name { get; set; }
        public string ModelName { get; set; }
        public int NodeId { get; set; }
        public Vector3 Position { get; set; }
        public Vector4 Orientation { get; set; }
        /// <summary>
        /// Unknown field 0 in MDL node structure.
        /// Based on MDLOps analysis, this may correspond to m_bUnknown1 field at index 18 in trimesh subheader.
        /// Possibly a boolean flag related to rendering or processing state.
        /// Currently unimplemented - defaults to 0.
        /// </summary>
        public int Unknown0 { get; set; }

        /// <summary>
        /// Unknown field 1 in MDL node structure.
        /// Purpose unknown. Currently unimplemented - defaults to 0.
        /// </summary>
        public int Unknown1 { get; set; }

        /// <summary>
        /// Unknown field 2 in MDL node structure.
        /// Based on MDLOps analysis, this may correspond to 'unknown' field at index 32 in trimesh subheader.
        /// Purpose unknown. Currently unimplemented - defaults to 0.
        /// </summary>
        public int Unknown2 { get; set; }
        public bool IgnoreFog { get; set; }
        public bool Shadow { get; set; }
        public int Animation { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float OffsetZ { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float ScaleZ { get; set; }
        public MDLNodeFlags NodeFlags { get; set; }
        public List<MDLNode> Children { get; set; }
        public List<MDLController> Controllers { get; set; }
        public MDLMesh Mesh { get; set; }
        public MDLReference Reference { get; set; }
        public MDLWalkmesh Walkmesh { get; set; }

        // ASCII MDL format compatibility fields (matching PyKotor mdl_data.py)
        public MDLNodeType NodeType { get; set; }
        public int ParentId { get; set; }
        public MDLLight Light { get; set; }
        public MDLEmitter Emitter { get; set; }
        public MDLSaber Saber { get; set; }
        public MDLWalkmesh Aabb { get; set; }
        // Direct node properties matching PyKotor mdl_data.py:MDLNode structure
        // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:627,631
        public MDLSkin Skin { get; set; }
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
                   Children.SequenceEqual(other.Children) &&
                   Controllers.SequenceEqual(other.Controllers) &&
                   Equals(Mesh, other.Mesh) &&
                   Equals(Reference, other.Reference) &&
                   Equals(Walkmesh, other.Walkmesh);
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
            foreach (var c in Children) hash.Add(c);
            foreach (var c in Controllers) hash.Add(c);
            hash.Add(Mesh);
            hash.Add(Reference);
            hash.Add(Walkmesh);
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
