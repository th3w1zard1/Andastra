using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Extract.Capsule;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tools;
using JetBrains.Annotations;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Resource
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/salvage.py
    // Original: Handles resource data validation/salvage strategies
    [PublicAPI]
    public static class Salvage
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/salvage.py:87-143
        // Original: def validate_capsule(...)
        [CanBeNull]
        public static object ValidateCapsule(
            object capsuleObj,
            bool strict = false,
            Game? game = null)
        {
            object container = LoadAsErfRim(capsuleObj);
            if (container == null)
            {
                return null;
            }

            ERF newErf = null;
            RIM newRim = null;
            if (container is ERF erf)
            {
                newErf = new ERF(erf.ErfType);
            }
            else if (container is RIM rim)
            {
                newRim = new RIM();
            }
            else
            {
                return null;
            }

            try
            {
                if (container is ERF erfContainer)
                {
                    foreach (var resource in erfContainer)
                    {
                        new RobustLogger().Info($"Validating '{resource.ResRef}.{resource.ResType.Extension}'");
                        if (resource.ResType == ResourceType.NCS)
                        {
                            newErf.SetData(resource.ResRef.ToString(), resource.ResType, resource.Data);
                            continue;
                        }
                        try
                        {
                            byte[] newData = ValidateResource(resource, strict, game, shouldRaise: true);
                            newData = strict ? newData : resource.Data;
                            if (newData == null)
                            {
                                new RobustLogger().Info($"Not packaging unknown resource '{resource.ResRef}.{resource.ResType.Extension}'");
                                continue;
                            }
                            newErf.SetData(resource.ResRef.ToString(), resource.ResType, newData);
                        }
                        catch (Exception ex) when (ex is IOException || ex is ArgumentException)
                        {
                            new RobustLogger().Error($" - Corrupted resource: '{resource.ResRef}.{resource.ResType.Extension}'");
                        }
                    }
                }
                else if (container is RIM rimContainer)
                {
                    foreach (var resource in rimContainer)
                    {
                        new RobustLogger().Info($"Validating '{resource.ResRef}.{resource.ResType.Extension}'");
                        if (resource.ResType == ResourceType.NCS)
                        {
                            newRim.SetData(resource.ResRef.ToString(), resource.ResType, resource.Data);
                            continue;
                        }
                        try
                        {
                            byte[] newData = ValidateResource(resource, strict, game, shouldRaise: true);
                            newData = strict ? newData : resource.Data;
                            if (newData == null)
                            {
                                new RobustLogger().Info($"Not packaging unknown resource '{resource.ResRef}.{resource.ResType.Extension}'");
                                continue;
                            }
                            newRim.SetData(resource.ResRef.ToString(), resource.ResType, newData);
                        }
                        catch (Exception ex) when (ex is IOException || ex is ArgumentException)
                        {
                            new RobustLogger().Error($" - Corrupted resource: '{resource.ResRef}.{resource.ResType.Extension}'");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException)
            {
                new RobustLogger().Error($"Corrupted ERF/RIM, could not salvage: '{capsuleObj}'");
            }

            int resourceCount = newErf != null ? newErf.Count : (newRim != null ? newRim.Count : 0);
            new RobustLogger().Info($"Returning salvaged ERF/RIM container with {resourceCount} total resources in it.");
            return newErf ?? (object)newRim;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/salvage.py:146-208
        // Original: def validate_resource(...)
        [CanBeNull]
        public static byte[] ValidateResource(
            object resource,
            bool strict = false,
            Game? game = null,
            bool shouldRaise = false)
        {
            try
            {
                byte[] data = null;
                ResourceType restype = ResourceType.INVALID;

                if (resource is FileResource fileRes)
                {
                    data = fileRes.GetData();
                    restype = fileRes.ResType;
                }
                else if (resource is ERFResource erfRes)
                {
                    data = erfRes.Data;
                    restype = erfRes.ResType;
                }
                else if (resource is RIMResource rimRes)
                {
                    data = rimRes.Data;
                    restype = rimRes.ResType;
                }

                if (data == null)
                {
                    return null;
                }

                if (restype.IsGff())
                {
                    var reader = new GFFBinaryReader(data);
                    GFF loadedGff = reader.Load();
                    if (strict && game.HasValue)
                    {
                        return ValidateGff(loadedGff, restype);
                    }
                    return GFFAuto.BytesGff(loadedGff, ResourceType.GFF);
                }

                // Other resource types would need to be validated here
                // For now, return the data as-is
                return data;
            }
            catch (Exception e)
            {
                if (shouldRaise)
                {
                    throw;
                }
                new RobustLogger().Error($"Corrupted resource: {resource}", !(e is IOException || e is ArgumentException));
            }
            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/salvage.py:211-254
        // Original: def validate_gff(...)
        private static byte[] ValidateGff(GFF gff, ResourceType restype)
        {
            // Use construct/dismantle functions to validate GFF
            if (restype == ResourceType.ARE)
            {
                var are = AREHelpers.ConstructAre(gff);
                return GFFAuto.BytesGff(AREHelpers.DismantleAre(are), ResourceType.GFF);
            }
            if (restype == ResourceType.GIT)
            {
                var git = GITHelpers.ConstructGit(gff);
                return GFFAuto.BytesGff(GITHelpers.DismantleGit(git), ResourceType.GFF);
            }
            if (restype == ResourceType.IFO)
            {
                var ifo = IFOHelpers.ConstructIfo(gff);
                return GFFAuto.BytesGff(IFOHelpers.DismantleIfo(ifo), ResourceType.GFF);
            }
            if (restype == ResourceType.UTC)
            {
                var utc = UTCHelpers.ConstructUtc(gff);
                return GFFAuto.BytesGff(UTCHelpers.DismantleUtc(utc), ResourceType.GFF);
            }
            // Other resource types would need their construct/dismantle functions ported
            return GFFAuto.BytesGff(gff, ResourceType.GFF);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/salvage.py:257-302
        // Original: def _load_as_erf_rim(...)
        [CanBeNull]
        private static object LoadAsErfRim(object capsuleObj)
        {
            if (capsuleObj is LazyCapsule lazyCapsule)
            {
                try
                {
                    // LazyCapsule.as_cached() needs to be implemented
                    // For now, try to load as ERF or RIM
                    return ERFAuto.ReadErf(lazyCapsule.FilePath);
                }
                catch
                {
                    try
                    {
                        return RIMAuto.ReadRim(lazyCapsule.FilePath);
                    }
                    catch
                    {
                        new RobustLogger().Warning($"Corrupted LazyCapsule object passed to `validate_capsule` could not be loaded into memory");
                        return null;
                    }
                }
            }

            if (capsuleObj is ERF || capsuleObj is RIM)
            {
                return capsuleObj;
            }

            if (capsuleObj is string path)
            {
                try
                {
                    var lazy = new LazyCapsule(path, createIfNotExist: true);
                    return LoadAsErfRim(lazy);
                }
                catch
                {
                    new RobustLogger().Warning($"Invalid path passed to `validate_capsule`: '{path}'");
                    return null;
                }
            }

            if (capsuleObj is byte[] bytes)
            {
                try
                {
                    return ERFAuto.ReadErf(bytes);
                }
                catch
                {
                    try
                    {
                        return RIMAuto.ReadRim(bytes);
                    }
                    catch
                    {
                        new RobustLogger().Error("the binary data passed to `validate_capsule` could not be loaded as an ERF/RIM.");
                        return null;
                    }
                }
            }

            throw new ArgumentException($"Invalid capsule argument: '{capsuleObj}' type '{capsuleObj?.GetType().Name ?? "null"}', expected one of ERF | RIM | LazyCapsule | string | byte[]");
        }

        /// <summary>
        /// Attempts to salvage data from a corrupted resource file.
        /// </summary>
        [CanBeNull]
        public static object TrySalvage(FileResource fileResource)
        {
            if (fileResource == null)
            {
                return null;
            }

            if (FileHelpers.IsAnyErfTypeFile(fileResource.FilePath) || FileHelpers.IsRimFile(fileResource.FilePath))
            {
                return ValidateCapsule(fileResource.FilePath);
            }

            return null;
        }

        /// <summary>
        /// Validates that a resource file is intact and readable.
        /// </summary>
        public static bool ValidateResourceFile(FileResource fileResource)
        {
            if (fileResource == null)
            {
                return false;
            }

            try
            {
                byte[] data = fileResource.GetData();
                return data != null && data.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets salvage strategies for different resource types.
        /// </summary>
        public static Dictionary<ResourceType, Func<FileResource, object>> GetSalvageStrategies()
        {
            return new Dictionary<ResourceType, Func<FileResource, object>>
            {
                // Placeholder - full implementation would have salvage strategies
            };
        }
    }
}
