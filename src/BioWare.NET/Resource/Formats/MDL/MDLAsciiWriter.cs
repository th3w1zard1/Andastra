using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.MDL;
using BioWare.NET.Resource.Formats.MDLData;

namespace BioWare.NET.Resource.Formats.MDL
{
    // 1:1 port of PyKotor io_mdl_ascii.MDLAsciiWriter
    // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl_ascii.py
    public class MDLAsciiWriter : IDisposable
    {
        private readonly MDLData.MDL _mdl;
        private readonly StringBuilder _sb;
        private readonly RawBinaryWriter _writer;
        private readonly bool _isByteArrayTarget;
        private readonly MemoryStream _targetBytes;

        public MDLAsciiWriter(MDLData.MDL mdl, string filepath)
        {
            _mdl = mdl ?? throw new ArgumentNullException(nameof(mdl));
            _sb = new StringBuilder();
            _writer = RawBinaryWriter.ToFile(filepath);
            _isByteArrayTarget = false;
            _targetBytes = null;
        }

        public MDLAsciiWriter(MDLData.MDL mdl, Stream target)
        {
            _mdl = mdl ?? throw new ArgumentNullException(nameof(mdl));
            _sb = new StringBuilder();
            _writer = RawBinaryWriter.ToStream(target);
            _isByteArrayTarget = false;
            _targetBytes = null;
        }

        public MDLAsciiWriter(MDLData.MDL mdl)
        {
            _mdl = mdl ?? throw new ArgumentNullException(nameof(mdl));
            _sb = new StringBuilder();
            _writer = RawBinaryWriter.ToByteArray();
            _isByteArrayTarget = true;
            _targetBytes = new MemoryStream();
        }

        private void WriteLine(int indent, string line)
        {
            for (int i = 0; i < indent; i++)
            {
                _sb.Append("  ");
            }
            _sb.Append(line);
            _sb.Append("\n");
        }

        public void Write(bool autoClose = true)
        {
            try
            {
                var mdl = _mdl;
                WriteLine(0, "# ASCII MDL");
                WriteLine(0, "filedependancy unknown.tga");
                WriteLine(0, $"newmodel {mdl.Name}");
                WriteLine(0, "");
                WriteLine(0, "setsupermodel " + mdl.Name + " " + mdl.Supermodel);
                WriteLine(0, $"classification {mdl.Classification.ToString().ToLowerInvariant()}");
                WriteLine(0, $"classification_unk1 {mdl.ClassificationUnk1}");
                // mdlops uses ignorefog 0/1; fog=True means ignorefog=0 (affected by fog)
                WriteLine(0, $"ignorefog {(mdl.Fog ? 0 : 1)}");
                WriteLine(0, $"compress_quaternions {mdl.CompressQuaternions}");
                if (!string.IsNullOrEmpty(mdl.Headlink))
                {
                    WriteLine(0, $"headlink {mdl.Headlink}");
                }
                WriteLine(0, "");
                WriteLine(0, $"setanimationscale {mdl.AnimationScale.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
                WriteLine(0, "");
                WriteLine(0, "beginmodelgeom " + mdl.Name);
                WriteLine(1, $"bmin {mdl.BMin.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {mdl.BMin.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {mdl.BMin.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
                WriteLine(1, $"bmax {mdl.BMax.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {mdl.BMax.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {mdl.BMax.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
                WriteLine(1, $"radius {mdl.Radius.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
                WriteLine(0, "");
                WriteNode(1, mdl.Root);
                WriteLine(0, "");
                WriteLine(0, "endmodelgeom " + mdl.Name);
                WriteLine(0, "");

                // Write animations if any
                if (mdl.Anims != null && mdl.Anims.Count > 0)
                {
                    foreach (var anim in mdl.Anims)
                    {
                        WriteAnimation(anim, mdl.Name);
                    }
                }

                WriteLine(0, "");
                WriteLine(0, "donemodel " + mdl.Name);

                // Write the StringBuilder content to the target
                var content = _sb.ToString();
                var bytes = Encoding.UTF8.GetBytes(content);
                _writer.WriteBytes(bytes);
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        private void WriteNode(int indent, MDLNode node)
        {
            var nodeTypeName = node.NodeType.ToString().ToLowerInvariant();
            if (nodeTypeName == "saber")
            {
                nodeTypeName = "lightsaber";
            }
            WriteLine(indent, $"node {nodeTypeName} {node.Name}");
            WriteLine(indent, "{");
            WriteNodeData(indent + 1, node);
            WriteLine(indent, "}");

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    WriteNode(indent, child);
                }
            }
        }

        private void WriteNodeData(int indent, MDLNode node)
        {
            WriteLine(indent, $"parent {node.ParentId}");
            WriteLine(indent, $"position {node.Position.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {node.Position.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {node.Position.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"orientation {node.Orientation.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {node.Orientation.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {node.Orientation.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {node.Orientation.W.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");

            if (node.Mesh != null)
            {
                WriteMesh(indent, node.Mesh);
            }
            if (node.Light != null)
            {
                WriteLight(indent, node.Light);
            }
            if (node.Emitter != null)
            {
                WriteEmitter(indent, node.Emitter);
            }
            if (node.Reference != null)
            {
                WriteReference(indent, node.Reference);
            }
            if (node.Saber != null)
            {
                WriteSaber(indent, node.Saber);
            }
            if (node.Aabb != null)
            {
                WriteWalkmesh(indent, node.Aabb);
            }

            if (node.Controllers != null)
            {
                foreach (var controller in node.Controllers)
                {
                    WriteController(indent, controller);
                }
            }
        }

        private void WriteMesh(int indent, MDLMesh mesh)
        {
            var ambient = mesh.Ambient;
            WriteLine(indent, $"ambient {ambient.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {ambient.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {ambient.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            var diffuse = mesh.Diffuse;
            WriteLine(indent, $"diffuse {diffuse.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {diffuse.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {diffuse.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"transparencyhint {mesh.TransparencyHint}");
            WriteLine(indent, $"bitmap {mesh.Texture1}");
            if (!string.IsNullOrEmpty(mesh.Texture2))
            {
                WriteLine(indent, $"lightmap {mesh.Texture2}");
            }

            if (mesh.Skin != null)
            {
                WriteSkin(indent, mesh.Skin);
            }
            else if (mesh is MDLDangly dangly)
            {
                WriteDangly(indent, dangly);
            }

            if (mesh.VertexPositions != null)
            {
                WriteLine(indent, "verts " + mesh.VertexPositions.Count);
                for (int i = 0; i < mesh.VertexPositions.Count; i++)
                {
                    var pos = mesh.VertexPositions[i];
                    var line = $"{i} {pos.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {pos.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {pos.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}";
                    if (mesh.VertexNormals != null && i < mesh.VertexNormals.Count)
                    {
                        var normal = mesh.VertexNormals[i];
                        line += $" {normal.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {normal.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {normal.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}";
                    }
                    if (mesh.VertexUv1 != null && i < mesh.VertexUv1.Count)
                    {
                        var uv = mesh.VertexUv1[i];
                        line += $" {uv.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {uv.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}";
                    }
                    if (mesh.VertexUv2 != null && i < mesh.VertexUv2.Count)
                    {
                        var uv = mesh.VertexUv2[i];
                        line += $" {uv.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {uv.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}";
                    }
                    WriteLine(indent + 1, line);
                }
            }

            if (mesh.Faces != null)
            {
                WriteLine(indent, "faces " + mesh.Faces.Count);
                for (int i = 0; i < mesh.Faces.Count; i++)
                {
                    var face = mesh.Faces[i];
                    var (surfaceMaterial, smoothingGroup) = MDLAsciiHelpers.UnpackFaceMaterial(face);
                    WriteLine(indent + 1, $"{i} {face.V1} {face.V2} {face.V3} {surfaceMaterial} {smoothingGroup}");
                }
            }
        }

        private void WriteSkin(int indent, MDLSkin skin)
        {
            if (skin.BoneIndices != null && skin.Qbones != null && skin.Tbones != null)
            {
                WriteLine(indent, "bones " + skin.BoneIndices.Count);
                for (int i = 0; i < skin.BoneIndices.Count && i < skin.Qbones.Count && i < skin.Tbones.Count; i++)
                {
                    var boneIdx = skin.BoneIndices[i];
                    var qbone = skin.Qbones[i];
                    var tbone = skin.Tbones[i];
                    WriteLine(indent + 1, $"{i} {boneIdx} {qbone.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {qbone.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {qbone.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {qbone.W.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {tbone.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {tbone.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {tbone.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
                }
            }
        }

        private void WriteDangly(int indent, MDLDangly dangly)
        {
            // Access base class Constraints (List<MDLConstraint>), not MDLDangly.Constraints (Vector3)
            MDLMesh meshBase = dangly;
            if (meshBase.Constraints != null && meshBase.Constraints.Count > 0)
            {
                WriteLine(indent, "constraints " + meshBase.Constraints.Count);
                for (int i = 0; i < meshBase.Constraints.Count; i++)
                {
                    var constraint = meshBase.Constraints[i];
                    WriteLine(indent + 1, $"{i} {constraint.Type} {constraint.Target} {constraint.TargetNode}");
                }
            }
        }

        private void WriteLight(int indent, MDLLight light)
        {
            // Write flare data arrays if present (vendor/mdlops/MDLOpsM.pm:3235-3256)
            bool hasFlares = light.Flare && (
                (light.FlareTextures != null && light.FlareTextures.Count > 0) ||
                (light.FlarePositions != null && light.FlarePositions.Count > 0) ||
                (light.FlareSizes != null && light.FlareSizes.Count > 0) ||
                (light.FlareColorShifts != null && light.FlareColorShifts.Count > 0)
            );

            if (hasFlares)
            {
                // Write lensflares count (vendor/mdlops/MDLOpsM.pm:3233)
                if (light.FlarePositions != null && light.FlarePositions.Count > 0)
                {
                    WriteLine(indent, $"lensflares {light.FlarePositions.Count}");
                }

                // Write texturenames (vendor/mdlops/MDLOpsM.pm:3235-3239)
                if (light.FlareTextures != null && light.FlareTextures.Count > 0)
                {
                    WriteLine(indent, $"texturenames {light.FlareTextures.Count}");
                    foreach (var texture in light.FlareTextures)
                    {
                        WriteLine(indent + 1, texture);
                    }
                }

                // Write flarepositions (vendor/mdlops/MDLOpsM.pm:3240-3244)
                if (light.FlarePositions != null && light.FlarePositions.Count > 0)
                {
                    WriteLine(indent, $"flarepositions {light.FlarePositions.Count}");
                    foreach (var pos in light.FlarePositions)
                    {
                        WriteLine(indent + 1, pos.ToString("G7", System.Globalization.CultureInfo.InvariantCulture));
                    }
                }

                // Write flaresizes (vendor/mdlops/MDLOpsM.pm:3245-3249)
                if (light.FlareSizes != null && light.FlareSizes.Count > 0)
                {
                    WriteLine(indent, $"flaresizes {light.FlareSizes.Count}");
                    foreach (var size in light.FlareSizes)
                    {
                        WriteLine(indent + 1, size.ToString("G7", System.Globalization.CultureInfo.InvariantCulture));
                    }
                }

                // Write flarecolorshifts (vendor/mdlops/MDLOpsM.pm:3250-3256)
                // FlareColorShifts is stored as a flat list of floats (3 floats per color: R, G, B)
                if (light.FlareColorShifts != null && light.FlareColorShifts.Count >= 3)
                {
                    int colorCount = light.FlareColorShifts.Count / 3;
                    WriteLine(indent, $"flarecolorshifts {colorCount}");
                    for (int i = 0; i < colorCount; i++)
                    {
                        int idx = i * 3;
                        WriteLine(indent + 1, $"{light.FlareColorShifts[idx].ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {light.FlareColorShifts[idx + 1].ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {light.FlareColorShifts[idx + 2].ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
                    }
                }
            }

            WriteLine(indent, $"flareradius {light.FlareRadius.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"priority {light.LightPriority}");
            if (light.AmbientOnly)
            {
                WriteLine(indent, "ambientonly");
            }
            if (light.Shadow)
            {
                WriteLine(indent, "shadow");
            }
            if (light.Flare)
            {
                WriteLine(indent, "flare");
            }
            if (light.FadingLight)
            {
                WriteLine(indent, "fadinglight");
            }
        }

        private void WriteEmitter(int indent, MDLEmitter emitter)
        {
            // Reference: vendor/mdlops/MDLOpsM.pm:3268-3307
            WriteLine(indent, $"deadspace {emitter.DeadSpace.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"blastRadius {emitter.BlastRadius.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"blastLength {emitter.BlastLength.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"numBranches {emitter.BranchCount}");
            WriteLine(indent, $"controlptsmoothing {emitter.ControlPointSmoothing.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"xgrid {emitter.XGrid}");
            WriteLine(indent, $"ygrid {emitter.YGrid}");
            // mdlops writes spawntype (vendor/mdlops/MDLOpsM.pm:3278)
            WriteLine(indent, $"spawntype {emitter.SpawnType}");
            // mdlops writes render/update/blend as strings (vendor/mdlops/MDLOpsM.pm:3279-3281)
            WriteLine(indent, $"update {emitter.UpdateType.ToString().ToLowerInvariant()}");
            WriteLine(indent, $"render {emitter.RenderType.ToString().ToLowerInvariant()}");
            WriteLine(indent, $"blend {emitter.BlendType.ToString().ToLowerInvariant()}");
            WriteLine(indent, $"texture {emitter.Texture}");
            if (!string.IsNullOrEmpty(emitter.ChunkName))
            {
                WriteLine(indent, $"chunkname {emitter.ChunkName}");
            }
            // mdlops writes twosidedtex as integer (vendor/mdlops/MDLOpsM.pm:3286)
            WriteLine(indent, $"twosidedtex {(emitter.Twosided ? 1 : 0)}");
            // mdlops writes loop as integer (vendor/mdlops/MDLOpsM.pm:3287)
            WriteLine(indent, $"loop {(emitter.Loop ? 1 : 0)}");
            WriteLine(indent, $"renderorder {emitter.RenderOrder}");
            // mdlops writes m_bFrameBlending as integer (vendor/mdlops/MDLOpsM.pm:3289)
            WriteLine(indent, $"m_bFrameBlending {(emitter.FrameBlend ? 1 : 0)}");
            // mdlops writes m_sDepthTextureName as string (vendor/mdlops/MDLOpsM.pm:3290)
            WriteLine(indent, $"m_sDepthTextureName {emitter.DepthTexture ?? ""}");

            // Write emitter flags (vendor/mdlops/MDLOpsM.pm:3295-3307)
            var flags = (MDLEmitterFlags)emitter.Flags;
            WriteLine(indent, $"p2p {((flags & MDLEmitterFlags.P2P) != 0 ? 1 : 0)}");
            WriteLine(indent, $"p2p_sel {((flags & MDLEmitterFlags.P2P_SEL) != 0 ? 1 : 0)}");
            WriteLine(indent, $"affectedByWind {((flags & MDLEmitterFlags.AFFECTED_WIND) != 0 ? 1 : 0)}");
            WriteLine(indent, $"m_isTinted {((flags & MDLEmitterFlags.TINTED) != 0 ? 1 : 0)}");
            WriteLine(indent, $"bounce {((flags & MDLEmitterFlags.BOUNCE) != 0 ? 1 : 0)}");
            WriteLine(indent, $"random {((flags & MDLEmitterFlags.RANDOM) != 0 ? 1 : 0)}");
            WriteLine(indent, $"inherit {((flags & MDLEmitterFlags.INHERIT) != 0 ? 1 : 0)}");
            WriteLine(indent, $"inheritvel {((flags & MDLEmitterFlags.INHERIT_VEL) != 0 ? 1 : 0)}");
            WriteLine(indent, $"inherit_local {((flags & MDLEmitterFlags.INHERIT_LOCAL) != 0 ? 1 : 0)}");
            WriteLine(indent, $"splat {((flags & MDLEmitterFlags.SPLAT) != 0 ? 1 : 0)}");
            WriteLine(indent, $"inherit_part {((flags & MDLEmitterFlags.INHERIT_PART) != 0 ? 1 : 0)}");
            WriteLine(indent, $"depth_texture {((flags & MDLEmitterFlags.DEPTH_TEXTURE) != 0 ? 1 : 0)}");
            WriteLine(indent, $"emitterflag13 {((flags & MDLEmitterFlags.FLAG_13) != 0 ? 1 : 0)}");
        }

        private void WriteReference(int indent, MDLReference reference)
        {
            WriteLine(indent, $"refmodel {reference.Model}");
            if (reference.Reattachable)
            {
                WriteLine(indent, "reattachable");
            }
        }

        private void WriteSaber(int indent, MDLSaber saber)
        {
            WriteLine(indent, $"sabertype {saber.SaberType}");
            WriteLine(indent, $"sabercolor {saber.SaberColor}");
            WriteLine(indent, $"saberlength {saber.SaberLength.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"saberwidth {saber.SaberWidth.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(indent, $"saberflarecolor {saber.SaberFlareColor}");
            WriteLine(indent, $"saberflareradius {saber.SaberFlareRadius.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        private void WriteWalkmesh(int indent, MDLWalkmesh walkmesh)
        {
            if (walkmesh.Aabbs != null)
            {
                WriteLine(indent, "aabb " + walkmesh.Aabbs.Count);
                for (int i = 0; i < walkmesh.Aabbs.Count; i++)
                {
                    var aabb = walkmesh.Aabbs[i];
                    WriteLine(indent + 1, $"{i} {aabb.Position.X.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {aabb.Position.Y.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {aabb.Position.Z.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
                }
            }
        }

        private void WriteController(int indent, MDLController controller)
        {
            if (controller.Rows == null || controller.Rows.Count == 0)
            {
                return;
            }

            // Map controller type to name
            var controllerNameMap = new Dictionary<MDLControllerType, string>
            {
                [MDLControllerType.POSITION] = "positionkey",
                [MDLControllerType.ORIENTATION] = "orientationkey",
                [MDLControllerType.SCALE] = "scalekey",
                [MDLControllerType.COLOR] = "colorkey",
                [MDLControllerType.RADIUS] = "radiuskey",
                [MDLControllerType.SHADOWRADIUS] = "shadowradiuskey",
                [MDLControllerType.VERTICALDISPLACEMENT] = "verticaldisplacementkey",
                [MDLControllerType.MULTIPLIER] = "multiplierkey",
                [MDLControllerType.ALPHAEND] = "alphaendkey",
                [MDLControllerType.ALPHASTART] = "alphastartkey",
                [MDLControllerType.BIRTHRATE] = "birthratekey",
                [MDLControllerType.BOUNCE_CO] = "bouncecokey",
                [MDLControllerType.COMBINETIME] = "combineetimekey",
                [MDLControllerType.DRAG] = "dragkey",
                [MDLControllerType.GRAV] = "gravkey",
                [MDLControllerType.LIFEEXP] = "lifeexpkey",
                [MDLControllerType.MASS] = "masskey",
                [MDLControllerType.P2P_BEZIER2] = "p2p_bezier2key",
                [MDLControllerType.P2P_BEZIER3] = "p2p_bezier3key",
                [MDLControllerType.PARTICLEROT] = "particlerotkey",
                [MDLControllerType.SIZESTART] = "sizestartkey",
                [MDLControllerType.SIZEEND] = "sizeendkey",
                [MDLControllerType.SIZESTART_Y] = "sizestart_ykey",
                [MDLControllerType.SIZEEND_Y] = "sizeend_ykey",
                [MDLControllerType.SPREAD] = "spreadkey",
                [MDLControllerType.THRESHOLD] = "thresholdkey",
                [MDLControllerType.VELOCITY] = "velocitykey",
                [MDLControllerType.XSIZE] = "xsizekey",
                [MDLControllerType.YSIZE] = "ysizekey",
                [MDLControllerType.BLURLENGTH] = "blurkey",
                [MDLControllerType.LIGHTNINGDELAY] = "lightningdelaykey",
                [MDLControllerType.LIGHTNINGRADIUS] = "lightningradiuskey",
                [MDLControllerType.LIGHTNINGSCALE] = "lightningscalekey",
                [MDLControllerType.DETONATE] = "detonatekey",
                [MDLControllerType.ALPHAMID] = "alphamidkey",
                [MDLControllerType.SIZEMID] = "sizemidkey",
                [MDLControllerType.SIZEMID_Y] = "sizemid_ykey",
                [MDLControllerType.RANDVEL] = "randvelkey",
                [MDLControllerType.SELFILLUMCOLOR] = "selfillumcolorkey",
                [MDLControllerType.ALPHA] = "alphakey",
            };

            var controllerName = controllerNameMap.ContainsKey(controller.ControllerType)
                ? controllerNameMap[controller.ControllerType]
                : "unknownkey";
            if (controller.IsBezier)
            {
                controllerName = controllerName.Replace("key", "bezierkey");
            }

            WriteLine(indent, controllerName);
            foreach (var row in controller.Rows)
            {
                var dataStr = string.Join(" ", row.Data.Select(d => d.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)));
                WriteLine(indent + 1, $"{row.Time.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {dataStr}");
            }
            WriteLine(indent, "endlist");
        }

        private void WriteAnimation(MDLAnimation anim, string modelName)
        {
            WriteLine(0, "");
            WriteLine(0, $"newanim {anim.Name} {modelName}");
            WriteLine(1, $"length {anim.AnimLength.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            WriteLine(1, $"transtime {anim.TransitionLength.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)}");
            if (!string.IsNullOrEmpty(anim.RootModel))
            {
                WriteLine(1, $"animroot {anim.RootModel}");
            }

            // Write events
            if (anim.Events != null && anim.Events.Count > 0)
            {
                foreach (var evt in anim.Events)
                {
                    WriteLine(1, $"event {evt.ActivationTime.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)} {evt.Name}");
                }
            }

            // Write animation nodes
            // Animation nodes should match the type of the corresponding model node
            // Build a mapping from animation nodes to their parents for parent writing
            var parentMap = new Dictionary<string, MDLNode>();
            BuildAnimationParentMap(anim.Root, null, parentMap);

            // Build a mapping from node names to model nodes for type lookup
            var modelNodeMap = new Dictionary<string, MDLNode>();
            if (_mdl.Root != null)
            {
                BuildModelNodeMap(_mdl.Root, modelNodeMap);
            }

            // Write all animation nodes (sorted by name to match MDLOps behavior)
            var allAnimNodes = anim.AllNodes();
            allAnimNodes = allAnimNodes.OrderBy(n => n.Name).ToList();

            foreach (var node in allAnimNodes)
            {
                if (!string.IsNullOrEmpty(node.Name))  // Skip root if it has no name
                {
                    parentMap.TryGetValue(node.Name, out var parent);
                    modelNodeMap.TryGetValue(node.Name, out var modelNode);
                    WriteAnimationNode(1, node, parent, modelNode);
                }
            }

            WriteLine(0, "");
            WriteLine(0, $"doneanim {anim.Name} {modelName}");
        }

        private void BuildAnimationParentMap(MDLNode node, MDLNode parent, Dictionary<string, MDLNode> parentMap)
        {
            parentMap[node.Name] = parent;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    BuildAnimationParentMap(child, node, parentMap);
                }
            }
        }

        private void BuildModelNodeMap(MDLNode node, Dictionary<string, MDLNode> nodeMap)
        {
            if (!string.IsNullOrEmpty(node.Name))
            {
                nodeMap[node.Name] = node;
            }
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    BuildModelNodeMap(child, nodeMap);
                }
            }
        }

        private void WriteAnimationNode(int indent, MDLNode node, MDLNode parent, MDLNode modelNode)
        {
            // Animation nodes should match the type of the corresponding model node
            // Look up the model node by name and use its type, defaulting to dummy if not found
            MDLNodeType nodeType = MDLNodeType.DUMMY;
            if (modelNode != null)
            {
                nodeType = modelNode.NodeType;
            }

            // Convert node type to string (matching WriteNode behavior)
            var nodeTypeName = nodeType.ToString().ToLowerInvariant();
            if (nodeTypeName == "saber")
            {
                nodeTypeName = "lightsaber";
            }

            WriteLine(indent, $"node {nodeTypeName} {node.Name}");

            // Write parent if this node has one
            // In MDLOps, parent is a model node index, but we use the parent node's name
            if (parent != null && !string.IsNullOrEmpty(parent.Name))
            {
                WriteLine(indent + 1, $"parent {parent.Name}");
            }

            // Write controllers
            if (node.Controllers != null)
            {
                foreach (var controller in node.Controllers)
                {
                    WriteController(indent + 1, controller);
                }
            }

            WriteLine(indent, "endnode");
        }

        public byte[] GetBytes()
        {
            if (_isByteArrayTarget && _writer is RawBinaryWriterMemory memoryWriter)
            {
                return memoryWriter.Data();
            }
            return Encoding.UTF8.GetBytes(_sb.ToString());
        }

        public void Dispose()
        {
            _targetBytes?.Dispose();
            _writer?.Dispose();
        }
    }
}
