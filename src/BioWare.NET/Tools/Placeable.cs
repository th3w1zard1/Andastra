using System;
using System.Collections.Generic;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Common.Logger;
using JetBrains.Annotations;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/placeable.py
    public static class Placeable
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/placeable.py:20-50
        // Original: def get_model(utp: UTP, installation: Installation, *, placeables: 2DA | SOURCE_TYPES | None = None) -> str:
        public static string GetModel(
            UTP utp,
            BioWare.NET.Extract.Installation installation,
            TwoDA placeables = null)
        {
            TwoDA placeables2DA;
            if (placeables == null)
            {
                var result = installation.Resources.LookupResource("placeables", ResourceType.TwoDA);
                if (result == null)
                {
                    throw new ArgumentException("Resource 'placeables.2da' not found in the installation, cannot get UTP model.");
                }
                var reader = new TwoDABinaryReader(result.Data);
                placeables2DA = reader.Load();
            }
            else
            {
                placeables2DA = placeables;
            }

            return placeables2DA.GetRow(utp.AppearanceId).GetString("modelname");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/placeable.py:53-104
        // Original: def load_placeables_2da(installation: Installation, logger: RobustLogger | None = None) -> 2DA | None:
        public static TwoDA LoadPlaceables2DA(
            BioWare.NET.Extract.Installation installation,
            RobustLogger logger = null)
        {
            if (logger == null)
            {
                logger = new RobustLogger();
            }

            TwoDA placeables2DA = null;

            // Try locations() first (more reliable, handles BIF files)
            try
            {
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/placeable.py:53-104
                // Original: locations_result = installation.locations([ResourceIdentifier("placeables", ResourceType.TwoDA)], [SearchLocation.OVERRIDE, SearchLocation.CHITIN])
                var locationResults = installation.Locations(
                    new List<ResourceIdentifier> { new ResourceIdentifier("placeables", ResourceType.TwoDA) },
                    new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN });
                foreach (var kvp in locationResults)
                {
                    if (kvp.Value != null && kvp.Value.Count > 0)
                    {
                        var loc = kvp.Value[0];
                        if (loc.FilePath != null && System.IO.File.Exists(loc.FilePath))
                        {
                            using (var f = System.IO.File.OpenRead(loc.FilePath))
                            {
                                f.Seek(loc.Offset, System.IO.SeekOrigin.Begin);
                                var data = new byte[loc.Size];
                                f.Read(data, 0, loc.Size);
                                var reader = new TwoDABinaryReader(data);
                                placeables2DA = reader.Load();
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"locations() failed for placeables.2da: {ex}");
            }

            // Fallback: try resource() if locations() didn't work
            if (placeables2DA == null)
            {
                try
                {
                    var placeablesResult = installation.Resources.LookupResource("placeables", ResourceType.TwoDA);
                    if (placeablesResult != null && placeablesResult.Data != null)
                    {
                        var reader = new TwoDABinaryReader(placeablesResult.Data);
                        placeables2DA = reader.Load();
                    }
                }
                catch (Exception ex)
                {
                    logger.Debug($"resource() also failed for placeables.2da: {ex}");
                }
            }

            return placeables2DA;
        }
    }
}
