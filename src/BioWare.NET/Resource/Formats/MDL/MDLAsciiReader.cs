using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.MDL;
using BioWare.NET.Resource.Formats.MDLData;

namespace BioWare.NET.Resource.Formats.MDL
{
    // 1:1 port of PyKotor io_mdl_ascii.MDLAsciiReader
    // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl_ascii.py
    public class MDLAsciiReader : IDisposable
    {
        private readonly StreamReader _reader;
        private MDLData.MDL _mdl;
        private Dictionary<string, int> _nodeIndex;
        private List<MDLNode> _nodes;
        private MDLNode _currentNode;
        private bool _isGeometry;
        private bool _isAnimation;
        private bool _inNode;
        private int _currentAnimNum;
        private string _task;
        private int _taskCount;
        private int _taskTotal;
        // Track animation nodes separately from geometry nodes
        private List<MDLNode> _animNodes;
        private Dictionary<string, int> _animNodeIndex;
        // Track animation nodes that have model node parents (animation node index -> model node parent name)
        private Dictionary<int, string> _animNodeModelParents;

        // Controller name mappings (matching Python _CONTROLLER_NAMES)
        private static readonly Dictionary<int, Dictionary<int, string>> ControllerNames = new Dictionary<int, Dictionary<int, string>>
        {
            [MDLAsciiHelpers.NODE_HAS_HEADER] = new Dictionary<int, string>
            {
                [8] = "position",
                [20] = "orientation",
                [36] = "scale",
                [132] = "alpha",
            },
            [MDLAsciiHelpers.NODE_HAS_LIGHT] = new Dictionary<int, string>
            {
                [76] = "color",
                [88] = "radius",
                [96] = "shadowradius",
                [100] = "verticaldisplacement",
                [140] = "multiplier",
            },
            [MDLAsciiHelpers.NODE_HAS_EMITTER] = new Dictionary<int, string>
            {
                [80] = "alphaEnd",
                [84] = "alphaStart",
                [88] = "birthrate",
                [92] = "bounce_co",
                [96] = "combinetime",
                [100] = "drag",
                [104] = "fps",
                [108] = "frameEnd",
                [112] = "frameStart",
                [116] = "grav",
                [120] = "lifeExp",
                [124] = "mass",
                [128] = "p2p_bezier2",
                [132] = "p2p_bezier3",
                [136] = "particleRot",
                [140] = "randvel",
                [144] = "sizeStart",
                [148] = "sizeEnd",
                [152] = "sizeStart_y",
                [156] = "sizeEnd_y",
                [160] = "spread",
                [164] = "threshold",
                [168] = "velocity",
                [172] = "xsize",
                [176] = "ysize",
                [180] = "blurlength",
                [184] = "lightningDelay",
                [188] = "lightningRadius",
                [192] = "lightningScale",
                [196] = "lightningSubDiv",
                [200] = "lightningzigzag",
                [216] = "alphaMid",
                [220] = "percentStart",
                [224] = "percentMid",
                [228] = "percentEnd",
                [232] = "sizeMid",
                [236] = "sizeMid_y",
                [240] = "m_fRandomBirthRate",
                [252] = "targetsize",
                [256] = "numcontrolpts",
                [260] = "controlptradius",
                [264] = "controlptdelay",
                [268] = "tangentspread",
                [272] = "tangentlength",
                [284] = "colorMid",
                [380] = "colorEnd",
                [392] = "colorStart",
                [502] = "detonate",
            },
            [MDLAsciiHelpers.NODE_HAS_MESH] = new Dictionary<int, string>
            {
                [100] = "selfillumcolor",
            },
        };

        // Controller type mappings (matching Python _CONTROLLER_NAME_TO_TYPE)
        private static readonly Dictionary<string, MDLControllerType> ControllerNameToType = new Dictionary<string, MDLControllerType>(StringComparer.OrdinalIgnoreCase)
        {
            ["position"] = MDLControllerType.POSITION,
            ["orientation"] = MDLControllerType.ORIENTATION,
            ["scale"] = MDLControllerType.SCALE,
            ["alpha"] = MDLControllerType.ALPHA,
            ["color"] = MDLControllerType.COLOR,
            ["radius"] = MDLControllerType.RADIUS,
            ["shadowradius"] = MDLControllerType.SHADOWRADIUS,
            ["verticaldisplacement"] = MDLControllerType.VERTICALDISPLACEMENT,
            ["multiplier"] = MDLControllerType.MULTIPLIER,
            ["alphaEnd"] = MDLControllerType.ALPHAEND,
            ["alphaStart"] = MDLControllerType.ALPHASTART,
            ["birthrate"] = MDLControllerType.BIRTHRATE,
            ["bounce_co"] = MDLControllerType.BOUNCE_CO,
            ["combinetime"] = MDLControllerType.COMBINETIME,
            ["drag"] = MDLControllerType.DRAG,
            ["fps"] = MDLControllerType.FPS,
            ["frameEnd"] = MDLControllerType.FRAMEEND,
            ["frameStart"] = MDLControllerType.FRAMESTART,
            ["grav"] = MDLControllerType.GRAV,
            ["lifeExp"] = MDLControllerType.LIFEEXP,
            ["mass"] = MDLControllerType.MASS,
            ["p2p_bezier2"] = MDLControllerType.P2P_BEZIER2,
            ["p2p_bezier3"] = MDLControllerType.P2P_BEZIER3,
            ["particleRot"] = MDLControllerType.PARTICLEROT,
            ["randvel"] = MDLControllerType.RANDVEL,
            ["sizeStart"] = MDLControllerType.SIZESTART,
            ["sizeEnd"] = MDLControllerType.SIZEEND,
            ["sizeStart_y"] = MDLControllerType.SIZESTART_Y,
            ["sizeEnd_y"] = MDLControllerType.SIZEEND_Y,
            ["spread"] = MDLControllerType.SPREAD,
            ["threshold"] = MDLControllerType.THRESHOLD,
            ["velocity"] = MDLControllerType.VELOCITY,
            ["xsize"] = MDLControllerType.XSIZE,
            ["ysize"] = MDLControllerType.YSIZE,
            ["blurlength"] = MDLControllerType.BLURLENGTH,
            ["lightningDelay"] = MDLControllerType.LIGHTNINGDELAY,
            ["lightningRadius"] = MDLControllerType.LIGHTNINGRADIUS,
            ["lightningScale"] = MDLControllerType.LIGHTNINGSCALE,
            ["lightningSubDiv"] = MDLControllerType.LIGHTNINGSUBDIV,
            ["lightningzigzag"] = MDLControllerType.LIGHTNINGZIGZAG,
            ["alphaMid"] = MDLControllerType.ALPHAMID,
            ["percentStart"] = MDLControllerType.PERCENTSTART,
            ["percentMid"] = MDLControllerType.PERCENTMID,
            ["percentEnd"] = MDLControllerType.PERCENTEND,
            ["sizeMid"] = MDLControllerType.SIZEMID,
            ["sizeMid_y"] = MDLControllerType.SIZEMID_Y,
            ["m_fRandomBirthRate"] = MDLControllerType.RANDOMBIRTHRATE,
            ["targetsize"] = MDLControllerType.TARGETSIZE,
            ["numcontrolpts"] = MDLControllerType.NUMCONTROLPTS,
            ["controlptradius"] = MDLControllerType.CONTROLPTRADIUS,
            ["controlptdelay"] = MDLControllerType.CONTROLPTDELAY,
            ["tangentspread"] = MDLControllerType.TANGENTSPREAD,
            ["tangentlength"] = MDLControllerType.TANGENTLENGTH,
            ["colorMid"] = MDLControllerType.COLORMID,
            ["colorEnd"] = MDLControllerType.COLOREND,
            ["colorStart"] = MDLControllerType.COLORSTART,
            ["detonate"] = MDLControllerType.DETONATE,
            ["selfillumcolor"] = MDLControllerType.SELFILLUMCOLOR,
        };

        public MDLAsciiReader(byte[] data, int offset = 0, int size = 0)
        {
            var stream = new MemoryStream(data, offset, size > 0 ? size : data.Length - offset);
            _reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
        }

        public MDLAsciiReader(string filepath, int offset = 0, int size = 0)
        {
            var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (offset > 0)
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }
            _reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
            if (size > 0)
            {
                // Note: StreamReader doesn't support size limit directly, but we'll handle it in reading
            }
        }

        public MDLAsciiReader(Stream source, int offset = 0, int size = 0)
        {
            if (offset > 0)
            {
                source.Seek(offset, SeekOrigin.Begin);
            }
            _reader = new StreamReader(source, Encoding.UTF8, true, 1024, true);
        }

        public MDLData.MDL Load(bool autoClose = true)
        {
            try
            {
                _mdl = new MDLData.MDL();
                _nodeIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["null"] = -1 };
                _nodes = new List<MDLNode>();
                _animNodes = new List<MDLNode>();
                _animNodeIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["null"] = -1 };
                _animNodeModelParents = new Dictionary<int, string>();
                _currentNode = null;
                _isGeometry = false;
                _isAnimation = false;
                _inNode = false;
                _currentAnimNum = 0;
                _task = "";
                _taskCount = 0;
                _taskTotal = 0;

                // Set defaults matching mdlops
                _mdl.Name = "";
                _mdl.Supermodel = "null";
                _mdl.Fog = false;
                _mdl.Classification = MDLClassification.OTHER;
                _mdl.ClassificationUnk1 = 0;
                _mdl.AnimationScale = 0.971f;
                _mdl.BMin = new Vector3(-5, -5, -1);
                _mdl.BMax = new Vector3(5, 5, 10);
                _mdl.Radius = 7.0f;

                // Parse the file line by line
                string line;
                while ((line = _reader.ReadLine()) != null)
                {
                    line = line.TrimEnd();
                    if (string.IsNullOrEmpty(line) || line.TrimStart().StartsWith("#"))
                    {
                        continue;
                    }

                    // Model header parsing
                    var match = Regex.Match(line, @"^\s*newmodel\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _mdl.Name = match.Groups[1].Value;
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*setsupermodel\s+\S+\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _mdl.Supermodel = match.Groups[1].Value;
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*classification\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var className = match.Groups[1].Value.ToLowerInvariant();
                        if (Enum.TryParse<MDLClassification>(className, true, out var classification))
                        {
                            _mdl.Classification = classification;
                        }
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*classification_unk1\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _mdl.ClassificationUnk1 = int.Parse(match.Groups[1].Value);
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*ignorefog\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _mdl.Fog = int.Parse(match.Groups[1].Value) == 0;
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*setanimationscale\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _mdl.AnimationScale = float.Parse(match.Groups[1].Value);
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*headlink\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _mdl.Headlink = match.Groups[1].Value;
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*compress_quaternions\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _mdl.CompressQuaternions = int.Parse(match.Groups[1].Value);
                        continue;
                    }

                    if (Regex.IsMatch(line, @"^\s*beginmodelgeom", RegexOptions.IgnoreCase))
                    {
                        _isGeometry = true;
                        continue;
                    }

                    if (Regex.IsMatch(line, @"^\s*endmodelgeom", RegexOptions.IgnoreCase))
                    {
                        _isGeometry = false;
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*newanim\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var anim = new MDLAnimation();
                        anim.Name = match.Groups[1].Value;
                        anim.RootModel = match.Groups[2].Value;
                        _mdl.Anims.Add(anim);
                        _isAnimation = true;
                        _currentAnimNum = _mdl.Anims.Count - 1;
                        continue;
                    }

                    if (Regex.IsMatch(line, @"^\s*doneanim", RegexOptions.IgnoreCase))
                    {
                        // Build animation node hierarchy before ending animation
                        if (_isAnimation && _mdl.Anims.Count > _currentAnimNum && _animNodes.Count > 0)
                        {
                            BuildAnimationNodeHierarchy(_mdl.Anims[_currentAnimNum]);
                        }
                        _animNodes.Clear();
                        _animNodeIndex.Clear();
                        _animNodeIndex["null"] = -1;
                        _animNodeModelParents.Clear();
                        _isAnimation = false;
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*length\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success && _isAnimation && _mdl.Anims.Count > _currentAnimNum)
                    {
                        _mdl.Anims[_currentAnimNum].AnimLength = float.Parse(match.Groups[1].Value);
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*animroot\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success && _isAnimation && _mdl.Anims.Count > _currentAnimNum)
                    {
                        _mdl.Anims[_currentAnimNum].RootModel = match.Groups[1].Value;
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*transtime\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success && _isAnimation && _mdl.Anims.Count > _currentAnimNum)
                    {
                        _mdl.Anims[_currentAnimNum].TransitionLength = float.Parse(match.Groups[1].Value);
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*event\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success && _isAnimation && !_inNode && _mdl.Anims.Count > _currentAnimNum)
                    {
                        var evt = new MDLEvent();
                        evt.ActivationTime = float.Parse(match.Groups[1].Value);
                        evt.Name = match.Groups[2].Value;
                        _mdl.Anims[_currentAnimNum].Events.Add(evt);
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*node\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        ParseNode(line);
                        continue;
                    }

                    if (_inNode)
                    {
                        ParseNodeData(line);
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*bmin\s+(\S+)\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success && !_inNode)
                    {
                        _mdl.BMin = new Vector3(
                            float.Parse(match.Groups[1].Value),
                            float.Parse(match.Groups[2].Value),
                            float.Parse(match.Groups[3].Value));
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*bmax\s+(\S+)\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success && !_inNode)
                    {
                        _mdl.BMax = new Vector3(
                            float.Parse(match.Groups[1].Value),
                            float.Parse(match.Groups[2].Value),
                            float.Parse(match.Groups[3].Value));
                        continue;
                    }

                    match = Regex.Match(line, @"^\s*radius\s+(\S+)", RegexOptions.IgnoreCase);
                    if (match.Success && !_inNode)
                    {
                        _mdl.Radius = float.Parse(match.Groups[1].Value);
                        continue;
                    }
                }

                // Build node hierarchy for geometry nodes
                BuildNodeHierarchy();

                // Animation node hierarchies are built immediately after each "doneanim" directive
                // so they're already built at this point

                return _mdl;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        private void ParseNode(string line)
        {
            var match = Regex.Match(line, @"^\s*node\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return;
            }

            var nodeTypeStr = match.Groups[1].Value.ToLowerInvariant();
            var nodeName = match.Groups[2].Value;

            // Handle saber prefix
            if (nodeName.StartsWith("2081__"))
            {
                nodeTypeStr = "lightsaber";
                nodeName = nodeName.Substring(6);
            }

            // Map node type string to MDLNodeType
            MDLNodeType nodeType;
            switch (nodeTypeStr)
            {
                case "dummy":
                    nodeType = MDLNodeType.DUMMY;
                    break;
                case "trimesh":
                    nodeType = MDLNodeType.TRIMESH;
                    break;
                case "danglymesh":
                    nodeType = MDLNodeType.DANGLYMESH;
                    break;
                case "light":
                    nodeType = MDLNodeType.LIGHT;
                    break;
                case "emitter":
                    nodeType = MDLNodeType.EMITTER;
                    break;
                case "reference":
                    nodeType = MDLNodeType.REFERENCE;
                    break;
                case "aabb":
                    nodeType = MDLNodeType.AABB;
                    break;
                case "lightsaber":
                    nodeType = MDLNodeType.SABER;
                    break;
                default:
                    nodeType = MDLNodeType.DUMMY;
                    break;
            }

            // Create node
            var node = new MDLNode();
            node.Name = nodeName;
            node.NodeType = nodeType;
            node.NodeId = _nodes.Count;
            node.Position = Vector3.Zero;
            node.Orientation = new Vector4(0, 0, 0, 1);

            // Initialize based on node type
            switch (nodeType)
            {
                case MDLNodeType.LIGHT:
                    node.Light = new MDLLight();
                    break;
                case MDLNodeType.EMITTER:
                    node.Emitter = new MDLEmitter();
                    break;
                case MDLNodeType.REFERENCE:
                    node.Reference = new MDLReference();
                    break;
                case MDLNodeType.AABB:
                    node.Aabb = new MDLWalkmesh();
                    break;
                case MDLNodeType.SABER:
                    node.Saber = new MDLSaber();
                    break;
                case MDLNodeType.TRIMESH:
                    node.Mesh = new MDLMesh();
                    break;
                case MDLNodeType.DANGLYMESH:
                    node.Mesh = new MDLDangly();
                    break;
            }

            // Add to appropriate list based on whether we're in an animation section
            if (_isAnimation)
            {
                _animNodes.Add(node);
                _animNodeIndex[nodeName.ToLowerInvariant()] = _animNodes.Count - 1;
            }
            else
            {
                _nodes.Add(node);
                _nodeIndex[nodeName.ToLowerInvariant()] = _nodes.Count - 1;
            }
            _currentNode = node;
            _inNode = true;
            _task = "";
        }

        private void ParseNodeData(string line)
        {
            if (_currentNode == null)
            {
                return;
            }

            // Check for endnode
            if (Regex.IsMatch(line, @"^\s*endnode", RegexOptions.IgnoreCase))
            {
                _inNode = false;
                _currentNode = null;
                _task = "";
                _taskCount = 0;
                return;
            }

            // Parse parent
            var match = Regex.Match(line, @"^\s*parent\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var parentNameOriginal = match.Groups[1].Value;
                var parentName = parentNameOriginal.ToLowerInvariant();
                // For animation nodes, parent can be either an animation node or a model node
                // Check animation node index first, then model node index
                if (_isAnimation)
                {
                    if (_animNodeIndex.ContainsKey(parentName))
                    {
                        // Parent is an animation node - use animation node index
                        _currentNode.ParentId = _animNodeIndex[parentName];
                    }
                    else if (_nodeIndex.ContainsKey(parentName))
                    {
                        // Parent is a model node - store the parent name (lowercase) for later resolution during hierarchy building
                        // Animation nodes can reference model nodes by name, but we need to resolve this
                        // when building the hierarchy since model nodes are in a separate list
                        // The model node parent reference is preserved in _animNodeModelParents for animation application
                        int animNodeIndex = _animNodes.Count - 1; // Current node was just added to _animNodes
                        _animNodeModelParents[animNodeIndex] = parentName; // Store lowercase to match _nodeIndex keys (case-insensitive lookup)
                        _currentNode.ParentId = -1; // Mark as no animation node parent (model node parent handled separately)
                    }
                    else
                    {
                        _currentNode.ParentId = -1;
                    }
                }
                else
                {
                    _currentNode.ParentId = _nodeIndex.ContainsKey(parentName) ? _nodeIndex[parentName] : -1;
                }
                return;
            }

            // Parse position
            match = Regex.Match(line, @"^\s*position\s+(\S+)\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _currentNode.Position = new Vector3(
                    float.Parse(match.Groups[1].Value),
                    float.Parse(match.Groups[2].Value),
                    float.Parse(match.Groups[3].Value));
                return;
            }

            // Parse orientation
            match = Regex.Match(line, @"^\s*orientation\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _currentNode.Orientation = new Vector4(
                    float.Parse(match.Groups[1].Value),
                    float.Parse(match.Groups[2].Value),
                    float.Parse(match.Groups[3].Value),
                    float.Parse(match.Groups[4].Value));
                return;
            }

            // Parse controllers
            if (ParseController(line))
            {
                return;
            }

            // Parse mesh data
            if (_currentNode.Mesh != null)
            {
                if (ParseMeshData(line))
                {
                    return;
                }
            }

            // Parse light data
            if (_currentNode.Light != null)
            {
                if (ParseLightData(line))
                {
                    return;
                }
            }

            // Parse emitter data
            if (_currentNode.Emitter != null)
            {
                if (ParseEmitterData(line))
                {
                    return;
                }
            }

            // Parse reference data
            if (_currentNode.Reference != null)
            {
                if (ParseReferenceData(line))
                {
                    return;
                }
            }

            // Parse saber data
            if (_currentNode.Saber != null)
            {
                if (ParseSaberData(line))
                {
                    return;
                }
            }

            // Parse walkmesh data
            if (_currentNode.Aabb != null)
            {
                if (ParseWalkmeshData(line))
                {
                    return;
                }
            }
        }

        private bool ParseController(string line)
        {
            if (_currentNode == null)
            {
                return false;
            }

            var nodeFlags = MDLAsciiHelpers.GetNodeFlags(_currentNode);
            var nodeTypeValue = (int)nodeFlags;

            // Check for keyed controllers
            var flagTypes = new[] { MDLAsciiHelpers.NODE_HAS_LIGHT, MDLAsciiHelpers.NODE_HAS_EMITTER, MDLAsciiHelpers.NODE_HAS_MESH, MDLAsciiHelpers.NODE_HAS_HEADER };
            foreach (var flagType in flagTypes)
            {
                if ((nodeTypeValue & flagType) == 0)
                {
                    continue;
                }

                if (!ControllerNames.ContainsKey(flagType))
                {
                    continue;
                }

                var controllers = ControllerNames[flagType];
                foreach (var kvp in controllers)
                {
                    var controllerName = kvp.Value;
                    var keyedPattern = $@"^\s*{Regex.Escape(controllerName)}(bezier)?key";
                    var keyedMatch = Regex.Match(line, keyedPattern, RegexOptions.IgnoreCase);
                    if (keyedMatch.Success)
                    {
                        var isBezier = keyedMatch.Groups[1].Success && keyedMatch.Groups[1].Value.ToLowerInvariant() == "bezier";
                        var countMatch = Regex.Match(line, @"key\s+(\d+)$", RegexOptions.IgnoreCase);
                        var total = countMatch.Success ? int.Parse(countMatch.Groups[1].Value) : 0;

                        var rows = new List<MDLControllerRow>();
                        var controllerType = ControllerNameToType.ContainsKey(controllerName) ? ControllerNameToType[controllerName] : MDLControllerType.INVALID;

                        // Read keyframe data
                        for (int i = 0; i < (total > 0 ? total : 10000); i++)
                        {
                            var rowLine = _reader.ReadLine();
                            if (string.IsNullOrEmpty(rowLine) || Regex.IsMatch(rowLine, @"^\s*endlist", RegexOptions.IgnoreCase))
                            {
                                break;
                            }

                            var parts = rowLine.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 0)
                            {
                                break;
                            }

                            var time = float.Parse(parts[0]);
                            var data = new List<float>();
                            for (int j = 1; j < parts.Length; j++)
                            {
                                data.Add(float.Parse(parts[j]));
                            }

                            // Special handling for orientation (convert angle-axis to quaternion)
                            if (controllerType == MDLControllerType.ORIENTATION && data.Count == 4)
                            {
                                var q = MDLAsciiHelpers.AngleAxisToQuaternion(data[0], data[1], data[2], data[3]);
                                data = new List<float> { q.X, q.Y, q.Z, q.W };
                            }

                            rows.Add(new MDLControllerRow { Time = time, Data = data });
                        }

                        if (rows.Count > 0)
                        {
                            var controller = new MDLController
                            {
                                ControllerType = controllerType,
                                IsBezier = isBezier,
                                Rows = rows
                            };
                            _currentNode.Controllers.Add(controller);
                        }
                        return true;
                    }

                    // Check for single controller
                    var singlePattern = $@"^\s*{Regex.Escape(controllerName)}(\s+(\S+))+";
                    var singleMatch = Regex.Match(line, singlePattern, RegexOptions.IgnoreCase);
                    if (singleMatch.Success)
                    {
                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        var data = new List<float>();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            data.Add(float.Parse(parts[i]));
                        }

                        var controllerType = ControllerNameToType.ContainsKey(controllerName) ? ControllerNameToType[controllerName] : MDLControllerType.INVALID;
                        if (controllerType == MDLControllerType.ORIENTATION && data.Count == 4)
                        {
                            var q = MDLAsciiHelpers.AngleAxisToQuaternion(data[0], data[1], data[2], data[3]);
                            data = new List<float> { q.X, q.Y, q.Z, q.W };
                        }

                        var singleController = new MDLController
                        {
                            ControllerType = controllerType,
                            Rows = new List<MDLControllerRow> { new MDLControllerRow { Time = 0.0f, Data = data } },
                            IsBezier = false
                        };
                        _currentNode.Controllers.Add(singleController);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ParseMeshData(string line)
        {
            if (_currentNode == null || _currentNode.Mesh == null)
            {
                return false;
            }

            var mesh = _currentNode.Mesh;

            // Parse ambient color
            var match = Regex.Match(line, @"^\s*ambient\s+(\S+)\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var color = new Color(
                    float.Parse(match.Groups[1].Value),
                    float.Parse(match.Groups[2].Value),
                    float.Parse(match.Groups[3].Value));
                mesh.Ambient = color.ToRgbVector3();
                return true;
            }

            // Parse diffuse Color match = Regex.Match(line, @"^\s*diffuse\s+(\S+)\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var color = new Color(
                    float.Parse(match.Groups[1].Value),
                    float.Parse(match.Groups[2].Value),
                    float.Parse(match.Groups[3].Value));
                mesh.Diffuse = color.ToRgbVector3();
                return true;
            }

            // Parse transparency hint
            match = Regex.Match(line, @"^\s*transparencyhint\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                mesh.TransparencyHint = int.Parse(match.Groups[1].Value);
                return true;
            }

            // Parse bitmap/texture
            match = Regex.Match(line, @"^\s*bitmap\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                mesh.Texture1 = match.Groups[1].Value;
                return true;
            }

            // Parse lightmap
            match = Regex.Match(line, @"^\s*lightmap\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                mesh.Texture2 = match.Groups[1].Value;
                mesh.HasLightmap = true;
                return true;
            }

            // Parse verts declaration
            match = Regex.Match(line, @"^\s*verts\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _task = "verts";
                _taskTotal = int.Parse(match.Groups[1].Value);
                _taskCount = 0;
                mesh.VertexPositions = new List<Vector3>();
                mesh.VertexNormals = new List<Vector3>();
                mesh.VertexUv1 = new List<Vector2>();
                mesh.VertexUv2 = new List<Vector2>();
                return true;
            }

            // Parse faces declaration
            match = Regex.Match(line, @"^\s*faces\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _task = "faces";
                _taskTotal = int.Parse(match.Groups[1].Value);
                _taskCount = 0;
                mesh.Faces = new List<MDLFace>();
                return true;
            }

            // Parse tverts declaration
            match = Regex.Match(line, @"^\s*tverts\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _task = "tverts";
                _taskTotal = int.Parse(match.Groups[1].Value);
                _taskCount = 0;
                if (mesh.VertexUv1 == null)
                {
                    mesh.VertexUv1 = new List<Vector2>();
                }
                return true;
            }

            // Parse tverts1/lightmaptverts declaration
            match = Regex.Match(line, @"^\s*(tverts1|lightmaptverts)\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _task = "tverts1";
                _taskTotal = int.Parse(match.Groups[2].Value);
                _taskCount = 0;
                if (mesh.VertexUv2 == null)
                {
                    mesh.VertexUv2 = new List<Vector2>();
                }
                return true;
            }

            // Parse task data (verts, faces, tverts)
            if (!string.IsNullOrEmpty(_task))
            {
                return ParseTaskData(line);
            }

            // Parse skin data (check for bones/weights declarations to detect skin meshes)
            if (ParseSkinData(line))
            {
                return true;
            }

            // Parse dangly data
            if (mesh is MDLDangly dangly)
            {
                if (ParseDanglyData(line))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ParseTaskData(string line)
        {
            if (_currentNode == null || _currentNode.Mesh == null)
            {
                return false;
            }

            var mesh = _currentNode.Mesh;

            if (_task == "verts")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    var idx = int.Parse(parts[0]);
                    var pos = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                    mesh.VertexPositions.Add(pos);

                    // Optional normal
                    if (parts.Length >= 7)
                    {
                        var normal = new Vector3(float.Parse(parts[4]), float.Parse(parts[5]), float.Parse(parts[6]));
                        if (mesh.VertexNormals == null)
                        {
                            mesh.VertexNormals = new List<Vector3>();
                        }
                        while (mesh.VertexNormals.Count <= idx)
                        {
                            mesh.VertexNormals.Add(Vector3.Zero);
                        }
                        mesh.VertexNormals[idx] = normal;
                    }

                    // Optional UV1
                    if (parts.Length >= 9)
                    {
                        var uv = new Vector2(float.Parse(parts[7]), float.Parse(parts[8]));
                        if (mesh.VertexUv1 == null)
                        {
                            mesh.VertexUv1 = new List<Vector2>();
                        }
                        while (mesh.VertexUv1.Count <= idx)
                        {
                            mesh.VertexUv1.Add(Vector2.Zero);
                        }
                        mesh.VertexUv1[idx] = uv;
                    }

                    // Optional UV2
                    if (parts.Length >= 11)
                    {
                        var uv = new Vector2(float.Parse(parts[9]), float.Parse(parts[10]));
                        if (mesh.VertexUv2 == null)
                        {
                            mesh.VertexUv2 = new List<Vector2>();
                        }
                        while (mesh.VertexUv2.Count <= idx)
                        {
                            mesh.VertexUv2.Add(Vector2.Zero);
                        }
                        mesh.VertexUv2[idx] = uv;
                    }

                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }
            else if (_task == "faces")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 6)
                {
                    var v1 = int.Parse(parts[1]);
                    var v2 = int.Parse(parts[2]);
                    var v3 = int.Parse(parts[3]);
                    var surfaceMaterial = int.Parse(parts[4]);
                    var smoothingGroup = int.Parse(parts[5]);

                    var face = new MDLFace();
                    face.V1 = v1;
                    face.V2 = v2;
                    face.V3 = v3;
                    // Store surface material (0-31) and smoothing group separately
                    face.Material = (SurfaceMaterial)(surfaceMaterial & 0x1F);
                    face.SmoothingGroup = smoothingGroup;

                    mesh.Faces.Add(face);
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }
            else if (_task == "tverts")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var idx = int.Parse(parts[0]);
                    var uv = new Vector2(float.Parse(parts[1]), float.Parse(parts[2]));
                    if (mesh.VertexUv1 == null)
                    {
                        mesh.VertexUv1 = new List<Vector2>();
                    }
                    while (mesh.VertexUv1.Count <= idx)
                    {
                        mesh.VertexUv1.Add(Vector2.Zero);
                    }
                    mesh.VertexUv1[idx] = uv;
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }
            else if (_task == "tverts1")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var idx = int.Parse(parts[0]);
                    var uv = new Vector2(float.Parse(parts[1]), float.Parse(parts[2]));
                    if (mesh.VertexUv2 == null)
                    {
                        mesh.VertexUv2 = new List<Vector2>();
                    }
                    while (mesh.VertexUv2.Count <= idx)
                    {
                        mesh.VertexUv2.Add(Vector2.Zero);
                    }
                    mesh.VertexUv2[idx] = uv;
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }

            return false;
        }

        private bool ParseSkinData(string line)
        {
            if (_currentNode == null || _currentNode.Mesh == null)
            {
                return false;
            }

            // Create Skin object if it doesn't exist (when we encounter bones/weights)
            if (_currentNode.Mesh.Skin == null)
            {
                _currentNode.Mesh.Skin = new MDLSkin();
            }
            var skin = _currentNode.Mesh.Skin;

            // Parse bones declaration
            var match = Regex.Match(line, @"^\s*bones\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _task = "bones";
                _taskTotal = int.Parse(match.Groups[1].Value);
                _taskCount = 0;
                skin.Qbones = new List<Vector4>();
                skin.Tbones = new List<Vector3>();
                skin.BoneIndices = new List<int>();
                return true;
            }

            // Parse weights declaration
            match = Regex.Match(line, @"^\s*weights\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _task = "weights";
                _taskTotal = int.Parse(match.Groups[1].Value);
                _taskCount = 0;
                skin.VertexBones = new List<MDLBoneVertex>();
                return true;
            }

            // Parse bones data
            if (_task == "bones")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 9)
                {
                    var idx = int.Parse(parts[0]);
                    var boneIdx = int.Parse(parts[1]);
                    var qbone = new Vector4(float.Parse(parts[2]), float.Parse(parts[3]), float.Parse(parts[4]), float.Parse(parts[5]));
                    var tbone = new Vector3(float.Parse(parts[6]), float.Parse(parts[7]), float.Parse(parts[8]));

                    while (skin.BoneIndices.Count <= idx)
                    {
                        skin.BoneIndices.Add(0);
                    }
                    skin.BoneIndices[idx] = boneIdx;
                    skin.Qbones.Add(qbone);
                    skin.Tbones.Add(tbone);
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }

            // Parse weights data
            if (_task == "weights")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var boneHash = new Dictionary<int, float>();
                    int i = 0;
                    while (i < parts.Length - 1)
                    {
                        var boneIdx = int.Parse(parts[i]);
                        var weight = float.Parse(parts[i + 1]);
                        boneHash[boneIdx] = weight;
                        i += 2;
                        if (i >= parts.Length - 1)
                        {
                            break;
                        }
                    }

                    var sortedBones = boneHash.Keys.OrderBy(k => k).ToList();
                    var boneVertex = new MDLBoneVertex();
                    var indices = new List<float>();
                    var weights = new List<float>();
                    for (int j = 0; j < 4; j++)
                    {
                        if (j < sortedBones.Count)
                        {
                            indices.Add(sortedBones[j]);
                            weights.Add(boneHash[sortedBones[j]]);
                        }
                        else
                        {
                            indices.Add(-1.0f);
                            weights.Add(0.0f);
                        }
                    }
                    boneVertex.VertexIndices = Tuple.Create(indices[0], indices[1], indices[2], indices[3]);
                    boneVertex.VertexWeights = Tuple.Create(weights[0], weights[1], weights[2], weights[3]);

                    skin.VertexBones.Add(boneVertex);
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }

            return false;
        }

        private bool ParseDanglyData(string line)
        {
            if (_currentNode == null || !(_currentNode.Mesh is MDLDangly dangly))
            {
                return false;
            }

            // Parse constraints declaration
            var match = Regex.Match(line, @"^\s*constraints\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _task = "constraints";
                _taskTotal = int.Parse(match.Groups[1].Value);
                _taskCount = 0;
                var mesh = (MDLMesh)dangly;
                mesh.Constraints = new List<MDLConstraint>();
                return true;
            }

            // Parse constraints data
            if (_task == "constraints")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    var constraint = new MDLConstraint();
                    constraint.Type = int.Parse(parts[1]);
                    constraint.Target = int.Parse(parts[2]);
                    constraint.TargetNode = int.Parse(parts[3]);
                    var mesh = (MDLMesh)dangly;
                    mesh.Constraints.Add(constraint);
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }

            return false;
        }

        private bool ParseLightData(string line)
        {
            if (_currentNode == null || _currentNode.Light == null)
            {
                return false;
            }

            var light = _currentNode.Light;

            var match = Regex.Match(line, @"^\s*flareradius\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                light.FlareRadius = float.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*lightpriority\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                light.LightPriority = int.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*ambientonly\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                light.AmbientOnly = int.Parse(match.Groups[1].Value) != 0;
                return true;
            }

            match = Regex.Match(line, @"^\s*shadow\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                light.Shadow = int.Parse(match.Groups[1].Value) != 0;
                return true;
            }

            match = Regex.Match(line, @"^\s*flare\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                light.Flare = int.Parse(match.Groups[1].Value) != 0;
                return true;
            }

            match = Regex.Match(line, @"^\s*fadinglight\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                light.FadingLight = int.Parse(match.Groups[1].Value) != 0;
                return true;
            }

            match = Regex.Match(line, @"^\s*(flarepositions|flaresizes|texturenames)\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var taskName = match.Groups[1].Value.ToLowerInvariant();
                var count = int.Parse(match.Groups[2].Value);
                if (count > 0)
                {
                    _task = taskName;
                    _taskTotal = count;
                    _taskCount = 0;
                    if (taskName == "flarepositions")
                    {
                        light.FlarePositions = new List<float>();
                    }
                    else if (taskName == "flaresizes")
                    {
                        light.FlareSizes = new List<float>();
                    }
                    else if (taskName == "texturenames")
                    {
                        light.FlareTextures = new List<string>();
                    }
                }
                return true;
            }

            match = Regex.Match(line, @"^\s*flarecolorshifts\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var count = int.Parse(match.Groups[1].Value);
                if (count > 0)
                {
                    _task = "flarecolorshifts";
                    _taskTotal = count;
                    _taskCount = 0;
                    light.FlareColorShifts = new List<float>();
                }
                return true;
            }

            // Parse flare array data
            if (_task == "flarepositions")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    light.FlarePositions.Add(float.Parse(parts[0]));
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }

            if (_task == "flaresizes")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    light.FlareSizes.Add(float.Parse(parts[0]));
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }

            if (_task == "texturenames")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    light.FlareTextures.Add(parts[0]);
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }

            if (_task == "flarecolorshifts")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    // Python stores as list of [r, g, b] lists; C# stores as flat list [r, g, b, r, g, b, ...]
                    light.FlareColorShifts.Add(float.Parse(parts[0]));
                    light.FlareColorShifts.Add(float.Parse(parts[1]));
                    light.FlareColorShifts.Add(float.Parse(parts[2]));
                    _taskCount++;
                    if (_taskCount >= _taskTotal)
                    {
                        _task = "";
                    }
                    return true;
                }
            }

            return false;
        }

        private bool ParseEmitterData(string line)
        {
            if (_currentNode == null || _currentNode.Emitter == null)
            {
                return false;
            }

            var emitter = _currentNode.Emitter;

            // Parse specific emitter properties
            var match = Regex.Match(line, @"^\s*deadspace\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.DeadSpace = float.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*blastradius\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.BlastRadius = float.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*blastlength\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.BlastLength = float.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*numbranches\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.BranchCount = int.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*controlptsmoothing\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.ControlPointSmoothing = float.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*xgrid\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.XGrid = int.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*ygrid\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.YGrid = int.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*spawntype\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // SpawnType is stored as int in MDLEmitter
                // Note: Python uses spawn_type as int, C# might need to map this differently
                return true; // Skip for now if not in MDLEmitter
            }

            match = Regex.Match(line, @"^\s*update\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                if (Enum.TryParse<MDLUpdateType>(value, true, out var updateType))
                {
                    emitter.UpdateType = updateType;
                }
                return true;
            }

            match = Regex.Match(line, @"^\s*render\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                if (Enum.TryParse<MDLRenderType>(value, true, out var renderType))
                {
                    emitter.RenderType = renderType;
                }
                return true;
            }

            match = Regex.Match(line, @"^\s*blend\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                if (Enum.TryParse<MDLBlendType>(value, true, out var blendType))
                {
                    emitter.BlendType = blendType;
                }
                return true;
            }

            match = Regex.Match(line, @"^\s*texture\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.Texture = match.Groups[1].Value;
                return true;
            }

            match = Regex.Match(line, @"^\s*chunkname\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.ChunkName = match.Groups[1].Value;
                return true;
            }

            match = Regex.Match(line, @"^\s*twosidedtex\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.Twosided = int.Parse(match.Groups[1].Value) != 0;
                return true;
            }

            match = Regex.Match(line, @"^\s*loop\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.Loop = int.Parse(match.Groups[1].Value) != 0;
                return true;
            }

            match = Regex.Match(line, @"^\s*renderorder\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.RenderOrder = int.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*m_bframeblending\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.FrameBlend = int.Parse(match.Groups[1].Value) != 0;
                return true;
            }

            match = Regex.Match(line, @"^\s*m_sdepthtexturename\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                emitter.DepthTexture = match.Groups[1].Value;
                return true;
            }

            // Emitter flags
            var emitterFlags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["p2p"] = 0x0001,
                ["p2p_sel"] = 0x0002,
                ["affectedbywind"] = 0x0004,
                ["m_istinted"] = 0x0008,
                ["bounce"] = 0x0010,
                ["random"] = 0x0020,
                ["inherit"] = 0x0040,
                ["inheritvel"] = 0x0080,
                ["inherit_local"] = 0x0100,
                ["splat"] = 0x0200,
                ["inherit_part"] = 0x0400,
                ["depth_texture"] = 0x0800,
                ["emitterflag13"] = 0x1000,
            };

            foreach (var flag in emitterFlags)
            {
                var pattern = $@"^\s*{Regex.Escape(flag.Key)}\s+(\S+)";
                var flagMatch = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                if (flagMatch.Success)
                {
                    if (int.Parse(flagMatch.Groups[1].Value) == 1)
                    {
                        emitter.Flags |= flag.Value;
                    }
                    return true;
                }
            }

            return false;
        }

        private bool ParseReferenceData(string line)
        {
            if (_currentNode == null || _currentNode.Reference == null)
            {
                return false;
            }

            var reference = _currentNode.Reference;

            var match = Regex.Match(line, @"^\s*refmodel\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                reference.Model = match.Groups[1].Value;
                return true;
            }

            match = Regex.Match(line, @"^\s*reattachable\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                reference.Reattachable = int.Parse(match.Groups[1].Value) != 0;
                return true;
            }

            return false;
        }

        private bool ParseSaberData(string line)
        {
            if (_currentNode == null || _currentNode.Saber == null)
            {
                return false;
            }

            var saber = _currentNode.Saber;

            var match = Regex.Match(line, @"^\s*sabertype\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                saber.SaberType = int.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*sabercolor\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                saber.SaberColor = int.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*saberlength\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                saber.SaberLength = float.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*saberwidth\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                saber.SaberWidth = float.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*saberflarecolor\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                saber.SaberFlareColor = int.Parse(match.Groups[1].Value);
                return true;
            }

            match = Regex.Match(line, @"^\s*saberflareradius\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                saber.SaberFlareRadius = float.Parse(match.Groups[1].Value);
                return true;
            }

            return false;
        }

        private bool ParseWalkmeshData(string line)
        {
            if (_currentNode == null || _currentNode.Aabb == null)
            {
                return false;
            }

            var walkmesh = _currentNode.Aabb;

            // Parse aabb declaration
            if (Regex.IsMatch(line, @"^\s*aabb", RegexOptions.IgnoreCase))
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 8)
                {
                    var aabbNode = new MDLNode();
                    aabbNode.Position = new Vector3(float.Parse(parts[2]), float.Parse(parts[3]), float.Parse(parts[4]));
                    walkmesh.Aabbs.Add(aabbNode);
                    _task = "aabb";
                    _taskCount = 1;
                }
                else
                {
                    _task = "aabb";
                    _taskCount = 0;
                }
                return true;
            }

            // Parse aabb data
            if (_task == "aabb")
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 8)
                {
                    var aabbNode = new MDLNode();
                    aabbNode.Position = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                    walkmesh.Aabbs.Add(aabbNode);
                    _taskCount++;
                    return true;
                }
            }

            return false;
        }

        private void BuildAnimationNodeHierarchy(MDLAnimation anim)
        {
            // Animation nodes are stored in _animNodes during parsing
            // Build the hierarchy similar to the main model hierarchy
            if (_animNodes.Count == 0)
            {
                anim.Root = new MDLNode();
                return;
            }

            // Find root animation node (parent_id == -1 and not in _animNodeModelParents)
            // Animation nodes with model node parents are not considered root nodes
            MDLNode animRootNode = null;
            for (int i = 0; i < _animNodes.Count; i++)
            {
                var node = _animNodes[i];
                if (node.ParentId == -1 && !_animNodeModelParents.ContainsKey(i))
                {
                    animRootNode = node;
                    break;
                }
            }

            // If no root found, use first node that doesn't have a model node parent
            if (animRootNode == null && _animNodes.Count > 0)
            {
                for (int i = 0; i < _animNodes.Count; i++)
                {
                    if (!_animNodeModelParents.ContainsKey(i))
                    {
                        animRootNode = _animNodes[i];
                        animRootNode.ParentId = -1;
                        break;
                    }
                }
                // If all nodes have model node parents, use first node as root
                if (animRootNode == null)
                {
                    animRootNode = _animNodes[0];
                    animRootNode.ParentId = -1;
                }
            }

            if (animRootNode != null)
            {
                anim.Root = animRootNode;
            }
            else
            {
                anim.Root = new MDLNode();
            }

            // Build parent-child relationships
            // Animation nodes with model node parents are treated as root-level nodes in the animation hierarchy
            // The model node parent reference is preserved in _animNodeModelParents for animation application
            for (int i = 0; i < _animNodes.Count; i++)
            {
                var node = _animNodes[i];

                // Skip if this is the root node itself
                if (node == anim.Root)
                {
                    continue;
                }

                if (node.ParentId == -1)
                {
                    // Check if this node has a model node parent
                    if (_animNodeModelParents.ContainsKey(i))
                    {
                        // Node has a model node parent - validate that the model node exists
                        string modelParentName = _animNodeModelParents[i];
                        if (_nodeIndex.ContainsKey(modelParentName))
                        {
                            int modelParentIndex = _nodeIndex[modelParentName];
                            if (modelParentIndex >= 0 && modelParentIndex < _nodes.Count)
                            {
                                // Model node parent exists and is valid
                                // The reference is stored in _animNodeModelParents for animation application
                                // Animation nodes remain in the animation tree but reference their model node parents
                            }
                            else
                            {
                                // Invalid model node index - treat as root-level animation node
                                // This should not happen in valid MDL files, but handle gracefully
                            }
                        }
                        // If model node not found, treat as root-level (should not happen in valid MDL files)
                    }
                    // Node has no parent in the animation tree (either truly root-level or has model node parent)
                    // Both cases: attach to animation root
                    // The model node parent reference (if any) is stored in _animNodeModelParents for later use
                    anim.Root.Children.Add(node);
                }
                else if (node.ParentId >= 0 && node.ParentId < _animNodes.Count)
                {
                    // Parent is an animation node (by index in _animNodes)
                    var parent = _animNodes[node.ParentId];
                    parent.Children.Add(node);
                }
            }
        }

        private void BuildNodeHierarchy()
        {
            if (_nodes.Count == 0)
            {
                return;
            }

            // Set root node (first node or node with parent_id == -1)
            MDLNode rootNode = null;
            foreach (var node in _nodes)
            {
                if (node.ParentId == -1)
                {
                    rootNode = node;
                    break;
                }
            }

            if (rootNode == null && _nodes.Count > 0)
            {
                rootNode = _nodes[0];
                rootNode.ParentId = -1;
            }

            if (rootNode != null)
            {
                _mdl.Root = rootNode;
            }

            // Build parent-child relationships
            foreach (var node in _nodes)
            {
                if (node.ParentId == -1)
                {
                    // Attach to root
                    if (_mdl.Root != null && node != _mdl.Root)
                    {
                        _mdl.Root.Children.Add(node);
                    }
                }
                else if (node.ParentId >= 0 && node.ParentId < _nodes.Count)
                {
                    var parent = _nodes[node.ParentId];
                    parent.Children.Add(node);
                }
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
