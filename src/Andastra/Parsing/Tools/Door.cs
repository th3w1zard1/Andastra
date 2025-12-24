using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using JetBrains.Annotations;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py
    public static class Door
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:25-64
        // Original: def get_model(utd: UTD, installation: Installation, *, genericdoors: 2DA | SOURCE_TYPES | None = None) -> str:
        public static string GetModel(
            UTD utd,
            Installation.Installation installation,
            TwoDA genericdoors = null)
        {
            if (genericdoors == null)
            {
                var result = installation.Resources.LookupResource("genericdoors", ResourceType.TwoDA);
                if (result == null)
                {
                    throw new ArgumentException("Resource 'genericdoors.2da' not found in the installation, cannot get UTD model.");
                }
                var reader = new TwoDABinaryReader(result.Data);
                genericdoors = reader.Load();
            }

            return genericdoors.GetRow(utd.AppearanceId).GetString("modelname");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:67-118
        // Original: def load_genericdoors_2da(installation: Installation, logger: RobustLogger | None = None) -> TwoDA | None:
        public static TwoDA LoadGenericDoors2DA(
            Installation.Installation installation,
            RobustLogger logger = null)
        {
            if (logger == null)
            {
                logger = new RobustLogger();
            }

            TwoDA genericdoors2DA = null;

            // Try locations() first (more reliable, handles BIF files)
            try
            {
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:67-118
                // Original: locations_result = installation.locations([ResourceIdentifier("genericdoors", ResourceType.TwoDA)], [SearchLocation.OVERRIDE, SearchLocation.CHITIN])
                var locationResults = installation.Locations(
                    new List<ResourceIdentifier> { new ResourceIdentifier("genericdoors", ResourceType.TwoDA) },
                    new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN });
                foreach (var kvp in locationResults)
                {
                    if (kvp.Value != null && kvp.Value.Count > 0)
                    {
                        var loc = kvp.Value[0];
                        if (loc.FilePath != null && File.Exists(loc.FilePath))
                        {
                            using (var f = File.OpenRead(loc.FilePath))
                            {
                                f.Seek(loc.Offset, SeekOrigin.Begin);
                                var data = new byte[loc.Size];
                                f.Read(data, 0, loc.Size);
                                var reader = new TwoDABinaryReader(data);
                                genericdoors2DA = reader.Load();
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"locations() failed for genericdoors.2da: {ex}");
            }

            // Fallback: try resource() if locations() didn't work
            if (genericdoors2DA == null)
            {
                try
                {
                    var genericdoorsResult = installation.Resources.LookupResource("genericdoors", ResourceType.TwoDA);
                    if (genericdoorsResult != null && genericdoorsResult.Data != null)
                    {
                        var reader = new TwoDABinaryReader(genericdoorsResult.Data);
                        genericdoors2DA = reader.Load();
                    }
                }
                catch (Exception ex)
                {
                    logger.Debug($"resource() also failed for genericdoors.2da: {ex}");
                }
            }

            return genericdoors2DA;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:215-237
        // Original: def _get_model_variations(model_name: str) -> list[str]:
        private static List<string> GetModelVariations(string modelName)
        {
            var variations = new List<string>
            {
                modelName,  // Original case
                modelName.ToLowerInvariant(),  // Lowercase
                modelName.ToUpperInvariant(),  // Uppercase
                modelName.ToLowerInvariant().Replace(".mdl", "").Replace(".mdx", "")  // Normalized lowercase
            };

            // Remove duplicates while preserving order
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var v in variations)
            {
                if (!seen.Contains(v))
                {
                    seen.Add(v);
                    result.Add(v);
                }
            }
            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:240-297
        // Original: def _load_mdl_with_variations(model_name: str, installation: Installation, logger: RobustLogger | None = None) -> tuple[MDL | None, bytes | None]:
        private static (Formats.MDLData.MDL mdl, byte[] mdlData) LoadMdlWithVariations(
            string modelName,
            Installation.Installation installation,
            RobustLogger logger = null)
        {
            if (logger == null)
            {
                logger = new RobustLogger();
            }

            var modelVariations = GetModelVariations(modelName);

            // Try locations() first (more reliable, searches multiple locations)
            foreach (string modelVar in modelVariations)
            {
                try
                {
                    var locationResults = installation.Locations(
                        new List<ResourceIdentifier> { new ResourceIdentifier(modelVar, ResourceType.MDL) },
                        new[] { SearchLocation.OVERRIDE, SearchLocation.MODULES, SearchLocation.CHITIN });
                    foreach (var kvp in locationResults)
                    {
                        if (kvp.Value != null && kvp.Value.Count > 0)
                        {
                            var loc = kvp.Value[0];
                            try
                            {
                                using (var f = File.OpenRead(loc.FilePath))
                                {
                                    f.Seek(loc.Offset, SeekOrigin.Begin);
                                    var mdlData = new byte[loc.Size];
                                    f.Read(mdlData, 0, loc.Size);
                                    var reader = new Formats.MDL.MDLBinaryReader(mdlData);
                                    var mdl = reader.Load();
                                    return (mdl, mdlData);
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Fallback to resource() if locations() didn't work
            foreach (string modelVar in modelVariations)
            {
                try
                {
                    var mdlResult = installation.Resources.LookupResource(modelVar, ResourceType.MDL);
                    if (mdlResult != null && mdlResult.Data != null)
                    {
                        var reader = new Formats.MDL.MDLBinaryReader(mdlResult.Data);
                        var mdl = reader.Load();
                        return (mdl, mdlResult.Data);
                    }
                }
                catch
                {
                    continue;
                }
            }

            return (null, null);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:300-387
        // Original: def _get_door_dimensions_from_model(mdl: MDL, model_name: str, door_name: str | None = None, logger: RobustLogger | None = None) -> tuple[float, float] | None:
        private static (float width, float height)? GetDoorDimensionsFromModel(
            Formats.MDLData.MDL mdl,
            string modelName,
            string doorName = null,
            RobustLogger logger = null)
        {
            if (logger == null)
            {
                logger = new RobustLogger();
            }

            if (mdl == null || mdl.Root == null)
            {
                return null;
            }

            var bbMin = new System.Numerics.Vector3(1000000, 1000000, 1000000);
            var bbMax = new System.Numerics.Vector3(-1000000, -1000000, -1000000);

            // Iterate through all nodes and their meshes
            var nodesToCheck = new List<Formats.MDLData.MDLNode> { mdl.Root };
            int meshCount = 0;
            while (nodesToCheck.Count > 0)
            {
                var node = nodesToCheck[nodesToCheck.Count - 1];
                nodesToCheck.RemoveAt(nodesToCheck.Count - 1);
                if (node.Mesh != null)
                {
                    meshCount++;
                    // Use mesh bounding box if available (BBoxMinX/Y/Z and BBoxMaxX/Y/Z properties)
                    if (node.Mesh.BBoxMinX < 1000000 && node.Mesh.BBoxMaxX > -1000000)
                    {
                        bbMin.X = Math.Min(bbMin.X, node.Mesh.BBoxMinX);
                        bbMin.Y = Math.Min(bbMin.Y, node.Mesh.BBoxMinY);
                        bbMin.Z = Math.Min(bbMin.Z, node.Mesh.BBoxMinZ);
                        bbMax.X = Math.Max(bbMax.X, node.Mesh.BBoxMaxX);
                        bbMax.Y = Math.Max(bbMax.Y, node.Mesh.BBoxMaxY);
                        bbMax.Z = Math.Max(bbMax.Z, node.Mesh.BBoxMaxZ);
                    }
                    // Fallback: calculate from vertex positions if bounding box not set
                    else if (node.Mesh.Vertices != null && node.Mesh.Vertices.Count > 0)
                    {
                        foreach (var vertex in node.Mesh.Vertices)
                        {
                            bbMin.X = Math.Min(bbMin.X, vertex.X);
                            bbMin.Y = Math.Min(bbMin.Y, vertex.Y);
                            bbMin.Z = Math.Min(bbMin.Z, vertex.Z);
                            bbMax.X = Math.Max(bbMax.X, vertex.X);
                            bbMax.Y = Math.Max(bbMax.Y, vertex.Y);
                            bbMax.Z = Math.Max(bbMax.Z, vertex.Z);
                        }
                    }
                }

                // Check child nodes
                if (node.Children != null)
                {
                    nodesToCheck.AddRange(node.Children);
                }
            }

            // Calculate dimensions from bounding box
            // Width is typically the Y dimension (horizontal when door is closed)
            // Height is typically the Z dimension (vertical)
            if (bbMin.X < 1000000)  // Valid bounding box calculated
            {
                float width = Math.Abs(bbMax.Y - bbMin.Y);
                float height = Math.Abs(bbMax.Z - bbMin.Z);

                // Only use calculated values if they're reasonable (not zero or extremely large)
                if (0.1f < width && width < 50.0f && 0.1f < height && height < 50.0f)
                {
                    string doorNameStr = !string.IsNullOrEmpty(doorName) ? $"'{doorName}'" : "";
                    logger.Debug(
                        $"[DOOR DEBUG] Extracted dimensions for door {doorNameStr}: " +
                        $"{width:F2} x {height:F2} (from {meshCount} meshes, model='{modelName}')");
                    return (width, height);
                }
                else
                {
                    string doorNameStr = !string.IsNullOrEmpty(doorName) ? $"'{doorName}'" : "";
                    logger.Warning(
                        $"Calculated dimensions for door {doorNameStr} out of range: " +
                        $"{width:F2} x {height:F2}, using defaults");
                }
            }
            else
            {
                string doorNameStr = !string.IsNullOrEmpty(doorName) ? $"'{doorName}'" : "";
                logger.Warning(
                    $"Could not calculate bounding box for door {doorNameStr} " +
                    $"(processed {meshCount} meshes), using defaults");
            }

            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:390-480
        // Original: def _get_door_dimensions_from_texture(model_name: str, installation: Installation, door_name: str | None = None, logger: RobustLogger | None = None) -> tuple[float, float] | None:
        private static (float width, float height)? GetDoorDimensionsFromTexture(
            string modelName,
            Installation.Installation installation,
            string doorName = null,
            RobustLogger logger = null)
        {
            if (logger == null)
            {
                logger = new RobustLogger();
            }

            // Get textures from the model
            var textureNames = new List<string>();
            var modelVariations = GetModelVariations(modelName);

            foreach (string modelVar in modelVariations)
            {
                try
                {
                    var mdlResult = installation.Resources.LookupResource(modelVar, ResourceType.MDL);
                    if (mdlResult != null && mdlResult.Data != null)
                    {
                        textureNames = Andastra.Parsing.Tools.ModelTools.IterateTextures(mdlResult.Data).ToList();
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (textureNames.Count == 0)
            {
                return null;
            }

            // Try to load the first texture
            string textureName = textureNames[0];
            var textureResult = installation.Resources.LookupResource(textureName, ResourceType.TPC);
            if (textureResult == null)
            {
                // Try TGA as fallback
                textureResult = installation.Resources.LookupResource(textureName, ResourceType.TGA);
            }

            if (textureResult == null || textureResult.Data == null)
            {
                return null;
            }

            // Read texture to get dimensions
            int texWidth = 0;
            int texHeight = 0;

            if (textureResult.ResType == ResourceType.TPC)
            {
                var reader = new Formats.TPC.TPCBinaryReader(textureResult.Data);
                var tpc = reader.Load();
                var dims = tpc.Dimensions();
                texWidth = dims.width;
                texHeight = dims.height;
            }
            else if (textureResult.ResType == ResourceType.TGA)
            {
                // TGA header: width at offset 12, height at offset 14 (little-endian)
                if (textureResult.Data.Length >= 18)
                {
                    texWidth = BitConverter.ToInt16(textureResult.Data, 12);
                    texHeight = BitConverter.ToInt16(textureResult.Data, 14);
                }
            }

            if (texWidth <= 0 || texHeight <= 0)
            {
                return null;
            }

            // Convert texture pixels to world units
            // Use aspect ratio to determine which dimension is width vs height
            // Doors are typically taller than wide, so height > width
            float doorWidth;
            float doorHeight;
            if (texHeight > texWidth)
            {
                // Portrait orientation - height is vertical, width is horizontal
                // Typical: 256x512 = 2.0x4.0, 512x1024 = 4.0x8.0
                // Scale factor: ~0.008-0.01 units per pixel
                float scaleFactor = 0.008f;  // Conservative estimate
                doorWidth = texWidth * scaleFactor;
                doorHeight = texHeight * scaleFactor;
            }
            else
            {
                // Landscape or square - assume standard door proportions
                // Use height as the primary dimension
                float scaleFactor = 0.008f;
                doorHeight = texHeight * scaleFactor;
                // Width is typically 0.6-0.8x height for doors
                doorWidth = doorHeight * 0.7f;
            }

            // Clamp to reasonable values
            doorWidth = Math.Max(1.0f, Math.Min(doorWidth, 10.0f));
            doorHeight = Math.Max(1.5f, Math.Min(doorHeight, 10.0f));

            return (doorWidth, doorHeight);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:483-587
        // Original: def get_door_dimensions(utd_data: bytes, installation: Installation, *, door_name: str | None = None, default_width: float = 2.0, default_height: float = 3.0, genericdoors: 2DA | None = None, logger: RobustLogger | None = None) -> tuple[float, float]:
        public static (float width, float height) GetDoorDimensions(
            byte[] utdData,
            Installation.Installation installation,
            string doorName = null,
            float defaultWidth = 2.0f,
            float defaultHeight = 3.0f,
            TwoDA genericdoors = null,
            RobustLogger logger = null)
        {
            if (logger == null)
            {
                logger = new RobustLogger();
            }

            float doorWidth = defaultWidth;
            float doorHeight = defaultHeight;
            string doorNameStr = !string.IsNullOrEmpty(doorName) ? $"'{doorName}'" : "";

            try
            {
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:483-587
                // Original: utd_data = read_utd(utd_data)
                var utd = ResourceAutoHelpers.ReadUtd(utdData);
                logger.Debug($"[DOOR DEBUG] Processing door {doorNameStr} (appearance_id={utd.AppearanceId})");

                // Get door model name from UTD using genericdoors.2da
                var genericdoors2DA = genericdoors ?? LoadGenericDoors2DA(installation, logger);
                if (genericdoors2DA == null)
                {
                    logger.Warning($"Could not load genericdoors.2da for door {doorNameStr}, using defaults");
                    return (doorWidth, doorHeight);
                }

                string modelName = GetModel(utd, installation, genericdoors: genericdoors2DA);
                if (string.IsNullOrEmpty(modelName))
                {
                    logger.Warning($"Could not get model name for door {doorNameStr} (appearance_id={utd.AppearanceId}), using defaults");
                    return (doorWidth, doorHeight);
                }

                // Try method 1: Get dimensions from model bounding box
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:547-565
                // Original: mdl, mdl_data = _load_mdl_with_variations(model_name, installation, logger)
                var (mdl, mdlData) = LoadMdlWithVariations(modelName, installation, logger);
                if (mdl != null)
                {
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:550-553
                    // Original: dimensions = _get_door_dimensions_from_model(mdl, model_name, door_name, logger)
                    var dimensions = GetDoorDimensionsFromModel(mdl, modelName, doorName, logger);
                    if (dimensions.HasValue)
                    {
                        doorWidth = dimensions.Value.width;
                        doorHeight = dimensions.Value.height;
                        return (doorWidth, doorHeight);
                    }
                    else
                    {
                        logger.Warning(
                            $"Could not extract dimensions from model '{modelName}' " +
                            $"for door {doorNameStr}, trying texture fallback");
                    }
                }
                else
                {
                    var modelVariations = GetModelVariations(modelName);
                    logger.Warning(
                        $"Could not load MDL '{modelName}' (tried variations: {string.Join(", ", modelVariations)}) " +
                        $"for door {doorNameStr} " +
                        $"(appearance_id={utd.AppearanceId}), trying texture fallback");
                }

                // Fallback: Get dimensions from door texture if model-based extraction failed
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:567-576
                // Original: dimensions = _get_door_dimensions_from_texture(model_name, installation, door_name, logger)
                var textureDimensions = GetDoorDimensionsFromTexture(modelName, installation, doorName, logger);
                if (textureDimensions.HasValue)
                {
                    doorWidth = textureDimensions.Value.width;
                    doorHeight = textureDimensions.Value.height;
                }
                else
                {
                    logger.Debug(
                        $"[DOOR DEBUG] Door {doorNameStr}: " +
                        $"Using default dimensions ({defaultWidth} x {defaultHeight}) - " +
                        $"model and texture extraction failed");
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to get dimensions for door {doorNameStr}: {ex}");
            }

            logger.Debug($"[DOOR DEBUG] Final dimensions for door {doorNameStr}: width={doorWidth:F2}, height={doorHeight:F2}");
            return (doorWidth, doorHeight);
        }
    }
}
