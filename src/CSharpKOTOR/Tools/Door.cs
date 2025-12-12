using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.TwoDA;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Logger;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;

namespace CSharpKOTOR.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py
    public static class Door
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:25-64
        // Original: def get_model(utd: UTD, installation: Installation, *, genericdoors: TwoDA | SOURCE_TYPES | None = None) -> str:
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
                // TODO: Implement installation.locations() equivalent
                // var locationResults = installation.Locations(
                //     new[] { new ResourceIdentifier { ResName = "genericdoors", ResType = ResourceType.TwoDA } },
                //     new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN });
                // foreach (var kvp in locationResults)
                // {
                //     if (kvp.Value != null && kvp.Value.Count > 0)
                //     {
                //         var loc = kvp.Value[0];
                //         if (loc.FilePath != null && File.Exists(loc.FilePath))
                //         {
                //             using (var f = File.OpenRead(loc.FilePath))
                //             {
                //                 f.Seek(loc.Offset, SeekOrigin.Begin);
                //                 var data = new byte[loc.Size];
                //                 f.Read(data, 0, loc.Size);
                //                 var reader = new TwoDABinaryReader(data);
                //                 genericdoors2DA = reader.Load();
                //                 break;
                //             }
                //         }
                //     }
                // }
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

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/door.py:483-587
        // Original: def get_door_dimensions(utd_data: bytes, installation: Installation, *, door_name: str | None = None, default_width: float = 2.0, default_height: float = 3.0, genericdoors: TwoDA | None = None, logger: RobustLogger | None = None) -> tuple[float, float]:
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
                // TODO: Implement read_utd equivalent
                // var utd = ReadUTD(utdData);
                // logger.Debug($"[DOOR DEBUG] Processing door {doorNameStr} (appearance_id={utd.AppearanceId})");
                //
                // // Get door model name from UTD using genericdoors.2da
                // var genericdoors2DA = genericdoors ?? LoadGenericDoors2DA(installation, logger);
                // if (genericdoors2DA == null)
                // {
                //     logger.Warning($"Could not load genericdoors.2da for door {doorNameStr}, using defaults");
                //     return (doorWidth, doorHeight);
                // }
                //
                // string modelName = GetModel(utd, installation, genericdoors: genericdoors2DA);
                // if (string.IsNullOrEmpty(modelName))
                // {
                //     logger.Warning($"Could not get model name for door {doorNameStr} (appearance_id={utd.AppearanceId}), using defaults");
                //     return (doorWidth, doorHeight);
                // }
                //
                // // Try method 1: Get dimensions from model bounding box
                // // TODO: Implement _load_mdl_with_variations and _get_door_dimensions_from_model
                //
                // // Fallback: Get dimensions from door texture if model-based extraction failed
                // // TODO: Implement _get_door_dimensions_from_texture
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

