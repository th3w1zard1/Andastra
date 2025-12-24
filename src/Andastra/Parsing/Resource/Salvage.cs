using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Extract.Capsule;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.LIP;
using Andastra.Parsing.Formats.LTR;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Formats.MDLData;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Formats.VIS;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.ARE;
using Andastra.Parsing.Resource.Generics.DLG;
using Andastra.Parsing.Resource.Generics.UTC;
using Andastra.Parsing.Resource.Generics.UTI;
using Andastra.Parsing.Resource.Generics.UTM;
using Andastra.Parsing.Tools;
using JetBrains.Annotations;

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
            BioWareGame? game = null)
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
            BioWareGame? game = null,
            bool shouldRaise = false)
        {
            try
            {
                byte[] data = null;
                ResourceType restype = ResourceType.INVALID;

                if (resource is FileResource fileResource)
                {
                    data = fileResource.GetData();
                    restype = fileResource.ResType;
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

                // Validate non-GFF resource types using salvage strategies
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/salvage.py:146-208
                // Original: validate_resource function provides salvage strategies for each resource type
                Dictionary<ResourceType, Func<FileResource, object>> strategies = GetSalvageStrategies();
                if (strategies.ContainsKey(restype))
                {
                    string tempFile = null;
                    try
                    {
                        // Create a temporary FileResource wrapper for ERFResource/RIMResource
                        FileResource fileResWrapper = null;
                        if (resource is FileResource existingFileRes)
                        {
                            fileResWrapper = existingFileRes;
                        }
                        else if (resource is ERFResource erfRes)
                        {
                            // Create temporary FileResource from ERFResource data
                            // FileResource requires a file path, so we create a temporary file
                            string tempResName = erfRes.ResRef.ToString();
                            tempFile = Path.GetTempFileName();
                            File.WriteAllBytes(tempFile, erfRes.Data);
                            fileResWrapper = new FileResource(
                                tempResName,
                                erfRes.ResType,
                                erfRes.Data.Length,
                                0,
                                tempFile
                            );
                        }
                        else if (resource is RIMResource rimRes)
                        {
                            // Create temporary FileResource from RIMResource data
                            string tempResName = rimRes.ResRef.ToString();
                            tempFile = Path.GetTempFileName();
                            File.WriteAllBytes(tempFile, rimRes.Data);
                            fileResWrapper = new FileResource(
                                tempResName,
                                rimRes.ResType,
                                rimRes.Data.Length,
                                0,
                                tempFile
                            );
                        }

                        if (fileResWrapper != null)
                        {
                            Func<FileResource, object> strategy = strategies[restype];
                            object validatedResult = strategy(fileResWrapper);

                            if (validatedResult is byte[] validatedBytes)
                            {
                                return validatedBytes;
                            }
                            // If strategy returns something else, try to convert or return original
                            return data;
                        }
                    }
                    catch (Exception strategyEx)
                    {
                        // If strategy fails, log and return original data (non-strict mode) or null (strict mode)
                        new RobustLogger().Warning($"Salvage strategy failed for {restype.Extension}: {strategyEx.Message}");
                        if (strict)
                        {
                            if (shouldRaise)
                            {
                                throw;
                            }
                            return null;
                        }
                        return data;
                    }
                    finally
                    {
                        // Clean up temporary file if created
                        if (tempFile != null && File.Exists(tempFile))
                        {
                            try
                            {
                                File.Delete(tempFile);
                            }
                            catch
                            {
                                // Ignore cleanup errors - temp file will be cleaned up by OS eventually
                            }
                        }
                    }
                }

                // No strategy available for this resource type
                // In strict mode, return null for unknown types; otherwise return data as-is
                if (strict)
                {
                    new RobustLogger().Info($"No validation strategy available for resource type '{restype.Extension}', returning null (strict mode)");
                    return null;
                }
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
                var utc = Andastra.Parsing.Resource.Generics.UTC.UTCHelpers.ConstructUtc(gff);
                return GFFAuto.BytesGff(Andastra.Parsing.Resource.Generics.UTC.UTCHelpers.DismantleUtc(utc), ResourceType.GFF);
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
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/salvage.py:262
                    // Original: return capsule_obj.as_cached()
                    return lazyCapsule.AsCached();
                }
                catch (Exception ex)
                {
                    new RobustLogger().Warning($"Corrupted LazyCapsule object passed to `validate_capsule` could not be loaded into memory: {ex.Message}");
                    return null;
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
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/salvage.py:146-208
        /// Original: validate_resource function provides salvage strategies for each resource type
        /// Each strategy attempts to read and re-serialize the resource to validate and repair corrupted data
        /// </remarks>
        public static Dictionary<ResourceType, Func<FileResource, object>> GetSalvageStrategies()
        {
            return new Dictionary<ResourceType, Func<FileResource, object>>
            {
                // GFF-based resources - validate by reading and re-serializing
                { ResourceType.ARE, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        ARE are = AREHelpers.ConstructAre(gff);
                        return AREHelpers.BytesAre(are, BioWareGame.K2);
                    } catch { return null; }
                }},
                { ResourceType.DLG, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        DLG dlg = DLGHelper.ConstructDlg(gff);
                        return DLGHelper.BytesDlg(dlg, BioWareGame.K2);
                    } catch { return null; }
                }},
                { ResourceType.GIT, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        GIT git = GITHelpers.ConstructGit(gff);
                        return GITHelpers.BytesGit(git, BioWareGame.K2);
                    } catch { return null; }
                }},
                { ResourceType.IFO, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        IFO ifo = IFOHelpers.ConstructIfo(gff);
                        GFF ifoGff = IFOHelpers.DismantleIfo(ifo, BioWareGame.K2);
                        return GFFAuto.BytesGff(ifoGff, IFO.BinaryType);
                    } catch { return null; }
                }},
                { ResourceType.JRL, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        JRL jrl = JRLHelpers.ConstructJrl(gff);
                        return JRLHelpers.BytesJrl(jrl);
                    } catch { return null; }
                }},
                { ResourceType.PTH, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        PTH pth = PTHHelpers.ConstructPth(gff);
                        return PTHAuto.BytesPth(pth, BioWareGame.K2);
                    } catch { return null; }
                }},
                { ResourceType.UTC, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTC utc = UTCHelpers.ConstructUtc(gff);
                        return UTCHelpers.BytesUtc(utc, BioWareGame.K2);
                    } catch { return null; }
                }},
                { ResourceType.UTD, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTD utd = UTDHelpers.ConstructUtd(gff);
                        GFF utdGff = UTDHelpers.DismantleUtd(utd, BioWareGame.K2);
                        return GFFAuto.BytesGff(utdGff, UTD.BinaryType);
                    } catch { return null; }
                }},
                { ResourceType.UTE, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTE ute = UTEHelpers.ConstructUte(gff);
                        GFF uteGff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
                        return GFFAuto.BytesGff(uteGff, UTE.BinaryType);
                    } catch { return null; }
                }},
                { ResourceType.UTI, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTI uti = UTIHelpers.ConstructUti(gff);
                        return UTIHelpers.BytesUti(uti, BioWareGame.K2);
                    } catch { return null; }
                }},
                { ResourceType.UTM, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTM utm = UTMHelpers.ConstructUtm(gff);
                        return UTMHelpers.BytesUtm(utm, BioWareGame.K2);
                    } catch { return null; }
                }},
                { ResourceType.UTP, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTP utp = UTPHelpers.ConstructUtp(gff);
                        GFF utpGff = UTPHelpers.DismantleUtp(utp, BioWareGame.K2);
                        return GFFAuto.BytesGff(utpGff, UTP.BinaryType);
                    } catch { return null; }
                }},
                { ResourceType.UTS, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTS uts = UTSHelpers.ConstructUts(gff);
                        GFF utsGff = UTSHelpers.DismantleUts(uts, BioWareGame.K2);
                        return GFFAuto.BytesGff(utsGff, UTS.BinaryType);
                    } catch { return null; }
                }},
                { ResourceType.UTT, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTT utt = UTTHelpers.ConstructUtt(gff);
                        return UTTAuto.BytesUtt(utt, BioWareGame.K2);
                    } catch { return null; }
                }},
                { ResourceType.UTW, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new GFFBinaryReader(data);
                        GFF gff = reader.Load();
                        UTW utw = UTWHelpers.ConstructUtw(gff);
                        return UTWAuto.BytesUtw(utw, BioWareGame.K2);
                    } catch { return null; }
                }},
                // Walkmesh resources - validate by reading and re-serializing
                { ResourceType.WOK, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        BWM bwm = BWMAuto.ReadBwm(data);
                        return BWMAuto.BytesBwm(bwm);
                    } catch { return null; }
                }},
                { ResourceType.PWK, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        BWM bwm = BWMAuto.ReadBwm(data);
                        return BWMAuto.BytesBwm(bwm);
                    } catch { return null; }
                }},
                { ResourceType.DWK, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        BWM bwm = BWMAuto.ReadBwm(data);
                        return BWMAuto.BytesBwm(bwm);
                    } catch { return null; }
                }},
                // Capsule resources - validate by reading and re-serializing
                { ResourceType.ERF, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        ERF erf = ERFAuto.ReadErf(data);
                        return ERFAuto.BytesErf(erf);
                    } catch { return null; }
                }},
                { ResourceType.RIM, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        RIM rim = RIMAuto.ReadRim(data);
                        return RIMAuto.BytesRim(rim);
                    } catch { return null; }
                }},
                // Other resource types - validate by reading and re-serializing
                { ResourceType.LIP, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        LIP lip = LIPAuto.ReadLip(data);
                        return LIPAuto.BytesLip(lip);
                    } catch { return null; }
                }},
                { ResourceType.LTR, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        LTR ltr = LTRAuto.ReadLtr(data);
                        return LTRAuto.BytesLtr(ltr);
                    } catch { return null; }
                }},
                { ResourceType.LYT, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        Andastra.Parsing.Resource.Formats.LYT.LYT lyt = LYTAuto.ReadLyt(data);
                        return LYTAuto.BytesLyt(lyt);
                    } catch { return null; }
                }},
                { ResourceType.MDL, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var mdl = MDLAuto.ReadMdl(data);
                        using (var ms = new System.IO.MemoryStream())
                        {
                            MDLAuto.WriteMdl(mdl, ms);
                            return ms.ToArray();
                        }
                    } catch { return null; }
                }},
                { ResourceType.NCS, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        NCS ncs = NCSAuto.ReadNcs(data);
                        return NCSAuto.BytesNcs(ncs);
                    } catch { return null; }
                }},
                { ResourceType.SSF, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        SSF ssf = SSFAuto.ReadSsf(data);
                        return SSFAuto.BytesSsf(ssf);
                    } catch { return null; }
                }},
                { ResourceType.TLK, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        TLK tlk = TLKAuto.ReadTlk(data);
                        return TLKAuto.BytesTlk(tlk);
                    } catch { return null; }
                }},
                { ResourceType.TPC, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        TPC tpc = TPCAuto.ReadTpc(data);
                        return TPCAuto.BytesTpc(tpc);
                    } catch { return null; }
                }},
                { ResourceType.TGA, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        TPC tpc = TPCAuto.ReadTpc(data);
                        return TPCAuto.BytesTpc(tpc);
                    } catch { return null; }
                }},
                { ResourceType.TwoDA, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        var reader = new TwoDABinaryReader(data);
                        TwoDA twoda = reader.Load();
                        return TwoDAAuto.Bytes2DA(twoda);
                    } catch { return null; }
                }},
                { ResourceType.TXI, (fileRes) => {
                    try {
                        // TXI is ASCII text - validate by re-encoding
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        string text = System.Text.Encoding.ASCII.GetString(data);
                        return System.Text.Encoding.ASCII.GetBytes(text);
                    } catch { return null; }
                }},
                { ResourceType.VIS, (fileRes) => {
                    try {
                        byte[] data = fileRes.GetData();
                        if (data == null) return null;
                        VIS vis = VISAuto.ReadVis(data);
                        return VISAuto.BytesVis(vis);
                    } catch { return null; }
                }},
            };
        }
    }
}
