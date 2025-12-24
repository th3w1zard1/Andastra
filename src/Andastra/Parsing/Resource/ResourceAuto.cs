using System;
using System.IO;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.LIP;
using Andastra.Parsing.Formats.LTR;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Formats.MDLData;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Formats.PCC;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Formats.VIS;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.ARE;
using Andastra.Parsing.Resource.Generics.CNV;
using Andastra.Parsing.Resource.Generics.DLG;
using Andastra.Parsing.Resource.Generics.UTC;
using Andastra.Parsing.Resource.Generics.UTI;
using Andastra.Parsing.Resource.Generics.UTM;
using JetBrains.Annotations;
using AREHelpers = Andastra.Parsing.Resource.Generics.ARE.AREHelpers;

namespace Andastra.Parsing.Resource
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/resource_auto.py
    // Original: Automatic resource loading and saving utilities
    [PublicAPI]
    public static class ResourceAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/resource_auto.py:58-123
        // Original: def read_resource(source: SOURCE_TYPES, resource_type: ResourceType | None = None) -> bytes:
        public static byte[] ReadResource(object source, ResourceType resourceType = null)
        {
            string sourcePath = null;
            try
            {
                if (source is string path)
                {
                    sourcePath = path;
                    if (resourceType == null)
                    {
                        var resId = ResourceIdentifier.FromPath(path);
                        resourceType = resId.ResType;
                    }
                }
            }
            catch
            {
                // Ignore errors in path detection
            }

            if (resourceType == null)
            {
                return ReadUnknownResource(source);
            }

            try
            {
                string ext = resourceType.Extension?.ToLowerInvariant() ?? "";
                string resourceExt = ext.StartsWith(".") ? ext.Substring(1) : ext;

                if (resourceType.Category == "Talk Tables")
                {
                    TLK tlk = source is string s ? TLKAuto.ReadTlk(s) : TLKAuto.ReadTlk(source as byte[]);
                    return TLKAuto.BytesTlk(tlk);
                }
                if (resourceType == ResourceType.TGA || resourceType == ResourceType.TPC)
                {
                    TPC tpc = TPCAuto.ReadTpc(source);
                    return TPCAuto.BytesTpc(tpc);
                }
                if (resourceExt == "ssf")
                {
                    SSF ssf = source is string s2 ? SSFAuto.ReadSsf(s2) : SSFAuto.ReadSsf(source as byte[]);
                    return SSFAuto.BytesSsf(ssf);
                }
                if (resourceExt == "2da")
                {
                    byte[] data2da = source is string s3 ? File.ReadAllBytes(s3) : source as byte[];
                    var reader2da = new TwoDABinaryReader(data2da);
                    TwoDA twoda = reader2da.Load();
                    return TwoDAAuto.Bytes2DA(twoda);
                }
                if (resourceExt == "lip")
                {
                    LIP lip = LIPAuto.ReadLip(source);
                    return LIPAuto.BytesLip(lip);
                }
                if (ResourceType.FromExtension(resourceExt) == ResourceType.ERF ||
                    ResourceType.FromExtension(resourceExt) == ResourceType.MOD ||
                    ResourceType.FromExtension(resourceExt) == ResourceType.SAV ||
                    ResourceType.FromExtension(resourceExt) == ResourceType.HAK)
                {
                    ERF erf = source is string s4 ? ERFAuto.ReadErf(s4) : ERFAuto.ReadErf(source as byte[]);
                    return ERFAuto.BytesErf(erf);
                }
                if (resourceExt == "rim")
                {
                    RIM rim = source is string s5 ? RIMAuto.ReadRim(s5) : RIMAuto.ReadRim(source as byte[]);
                    return RIMAuto.BytesRim(rim);
                }
                if (resourceExt == "pcc" || resourceExt == "upk")
                {
                    PCC pcc = source is string s6 ? PCCAuto.ReadPcc(s6) : PCCAuto.ReadPcc(source as byte[]);
                    return PCCAuto.BytesPcc(pcc);
                }
                if (resourceType.Extension?.ToUpperInvariant() == "GFF" || GFFContentExtensions.Contains(resourceType.Extension?.ToUpperInvariant() ?? ""))
                {
                    byte[] dataGff = source is string s6 ? File.ReadAllBytes(s6) : source as byte[];
                    var readerGff = new GFFBinaryReader(dataGff);
                    GFF gff = readerGff.Load();
                    return GFFAuto.BytesGff(gff, ResourceType.GFF);
                }
                if (resourceExt == "ncs")
                {
                    NCS ncs = source is string s8 ? NCSAuto.ReadNcs(s8) : NCSAuto.ReadNcs(source as byte[]);
                    return NCSAuto.BytesNcs(ncs);
                }
                if (resourceExt == "mdl")
                {
                    MDL mdl = MDLAuto.ReadMdl(source);
                    using (var ms = new MemoryStream())
                    {
                        MDLAuto.WriteMdl(mdl, ms);
                        return ms.ToArray();
                    }
                }
                if (resourceExt == "vis")
                {
                    VIS vis = VISAuto.ReadVis(source);
                    return VISAuto.BytesVis(vis);
                }
                if (resourceExt == "lyt")
                {
                    Andastra.Parsing.Resource.Formats.LYT.LYT lyt = LYTAuto.ReadLyt(source);
                    return LYTAuto.BytesLyt(lyt);
                }
                if (resourceExt == "ltr")
                {
                    LTR ltr = LTRAuto.ReadLtr(source);
                    return LTRAuto.BytesLtr(ltr);
                }
                if (resourceType.Category == "Walkmeshes")
                {
                    BWM bwm = source is string s9 ? BWMAuto.ReadBwm(s9) : BWMAuto.ReadBwm(source as byte[]);
                    return BWMAuto.BytesBwm(bwm);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Could not load resource '{sourcePath}' as resource type '{resourceType}': {e.Message}", e);
            }

            throw new ArgumentException($"Resource type {resourceType} is not supported by this library.");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/resource_auto.py:126-158
        // Original: def read_unknown_resource(source: SOURCE_TYPES) -> bytes:
        public static byte[] ReadUnknownResource(object source)
        {
            try
            {
                TLK tlk = source is string s ? TLKAuto.ReadTlk(s) : TLKAuto.ReadTlk(source as byte[]);
                return TLKAuto.BytesTlk(tlk);
            }
            catch { }

            try
            {
                SSF ssf = source is string s2 ? SSFAuto.ReadSsf(s2) : SSFAuto.ReadSsf(source as byte[]);
                return SSFAuto.BytesSsf(ssf);
            }
            catch { }

            try
            {
                byte[] data2da = source is string s3 ? File.ReadAllBytes(s3) : source as byte[];
                var reader2da = new TwoDABinaryReader(data2da);
                TwoDA twoda = reader2da.Load();
                return TwoDAAuto.Bytes2DA(twoda);
            }
            catch { }

            try
            {
                LIP lip = LIPAuto.ReadLip(source);
                return LIPAuto.BytesLip(lip);
            }
            catch { }

            try
            {
                TPC tpc = TPCAuto.ReadTpc(source);
                return TPCAuto.BytesTpc(tpc);
            }
            catch { }

            try
            {
                ERF erf = source is string s4 ? ERFAuto.ReadErf(s4) : ERFAuto.ReadErf(source as byte[]);
                return ERFAuto.BytesErf(erf);
            }
            catch { }

            try
            {
                RIM rim = source is string s5 ? RIMAuto.ReadRim(s5) : RIMAuto.ReadRim(source as byte[]);
                return RIMAuto.BytesRim(rim);
            }
            catch { }

            try
            {
                NCS ncs = source is string s6 ? NCSAuto.ReadNcs(s6) : NCSAuto.ReadNcs(source as byte[]);
                return NCSAuto.BytesNcs(ncs);
            }
            catch { }

            try
            {
                byte[] dataGff = source is string s7 ? File.ReadAllBytes(s7) : source as byte[];
                var readerGff = new GFFBinaryReader(dataGff);
                GFF gff = readerGff.Load();
                return GFFAuto.BytesGff(gff, ResourceType.GFF);
            }
            catch { }
            try
            {
                MDL mdl = MDLAuto.ReadMdl(source);
                using (var ms = new MemoryStream())
                {
                    MDLAuto.WriteMdl(mdl, ms);
                    return ms.ToArray();
                }
            }
            catch { }

            try
            {
                VIS vis = VISAuto.ReadVis(source);
                return VISAuto.BytesVis(vis);
            }
            catch { }

            try
            {
                Andastra.Parsing.Resource.Formats.LYT.LYT lyt = LYTAuto.ReadLyt(source);
                return LYTAuto.BytesLyt(lyt);
            }
            catch { }

            try
            {
                LTR ltr = LTRAuto.ReadLtr(source);
                return LTRAuto.BytesLtr(ltr);
            }
            catch { }

            try
            {
                BWM bwm = source is string s8 ? BWMAuto.ReadBwm(s8) : BWMAuto.ReadBwm(source as byte[]);
                return BWMAuto.BytesBwm(bwm);
            }
            catch { }

            throw new ArgumentException("Source resource data not recognized as any kotor file formats.");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/resource_auto.py:210-243
        // Original: def resource_to_bytes(resource: ...) -> bytes:
        public static byte[] ResourceToBytes(object resource)
        {
            // Handle GFF generic types - convert to GFF and then to bytes
            // Matching PyKotor: uses dismantle methods to convert generics back to GFF, then bytes_gff
            if (resource is ARE are)
            {
                return AREHelpers.BytesAre(are, BioWareGame.K2);
            }
            if (resource is DLG dlg)
            {
                return DLGHelper.BytesDlg(dlg, BioWareGame.K2);
            }
            if (resource is CNV cnv)
            {
                // CNV format is only used by Eclipse Engine games
                // Default to Dragon Age Origins if game type is not specified
                return Andastra.Parsing.Resource.Generics.CNV.CNVHelper.BytesCnv(cnv, BioWareGame.DA);
            }
            if (resource is GIT git)
            {
                return GITHelpers.BytesGit(git, BioWareGame.K2);
            }
            if (resource is IFO ifo)
            {
                // IFO doesn't have a BytesIfo method, so use DismantleIfo + BytesGff
                GFF ifoGff = IFOHelpers.DismantleIfo(ifo, BioWareGame.K2);
                return GFFAuto.BytesGff(ifoGff, IFO.BinaryType);
            }
            if (resource is JRL jrl)
            {
                return JRLHelpers.BytesJrl(jrl);
            }
            if (resource is PTH pth)
            {
                return PTHAuto.BytesPth(pth, BioWareGame.K2);
            }
            if (resource is UTC utc)
            {
                return UTCHelpers.BytesUtc(utc, BioWareGame.K2);
            }
            if (resource is UTD utd)
            {
                // UTD doesn't have a BytesUtd method, so use DismantleUtd + BytesGff
                GFF utdGff = UTDHelpers.DismantleUtd(utd, BioWareGame.K2);
                return GFFAuto.BytesGff(utdGff, UTD.BinaryType);
            }
            if (resource is UTE ute)
            {
                // UTE doesn't have a BytesUte method, so use DismantleUte + BytesGff
                GFF uteGff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
                return GFFAuto.BytesGff(uteGff, UTE.BinaryType);
            }
            if (resource is UTM utm)
            {
                // UTM doesn't have a BytesUtm method, so use DismantleUtm + BytesGff
                GFF utmGff = UTMHelpers.DismantleUtm(utm, BioWareGame.K2);
                return GFFAuto.BytesGff(utmGff, UTM.BinaryType);
            }
            if (resource is UTP utp)
            {
                // UTP doesn't have a BytesUtp method, so use DismantleUtp + BytesGff
                GFF utpGff = UTPHelpers.DismantleUtp(utp, BioWareGame.K2);
                return GFFAuto.BytesGff(utpGff, UTP.BinaryType);
            }
            if (resource is UTS uts)
            {
                // UTS doesn't have a BytesUts method, so use DismantleUts + BytesGff
                GFF utsGff = UTSHelpers.DismantleUts(uts, BioWareGame.K2);
                return GFFAuto.BytesGff(utsGff, UTS.BinaryType);
            }
            if (resource is UTW utw)
            {
                return UTWAuto.BytesUtw(utw, BioWareGame.K2);
            }
            if (resource is UTT utt)
            {
                return UTTAuto.BytesUtt(utt, BioWareGame.K2);
            }
            if (resource is UTI uti)
            {
                return UTIHelpers.BytesUti(uti, BioWareGame.K2);
            }
            if (resource is BWM bwm)
            {
                return BWMAuto.BytesBwm(bwm);
            }
            if (resource is GFF gff)
            {
                return GFFAuto.BytesGff(gff, ResourceType.GFF);
            }
            if (resource is ERF erf)
            {
                return ERFAuto.BytesErf(erf);
            }
            if (resource is LIP lip)
            {
                return LIPAuto.BytesLip(lip);
            }
            if (resource is LTR ltr)
            {
                return LTRAuto.BytesLtr(ltr);
            }
            if (resource is Andastra.Parsing.Resource.Formats.LYT.LYT lyt)
            {
                return LYTAuto.BytesLyt(lyt);
            }
            if (resource is MDL mdl)
            {
                using (var ms = new MemoryStream())
                {
                    MDLAuto.WriteMdl(mdl, ms);
                    return ms.ToArray();
                }
            }
            if (resource is NCS ncs)
            {
                return NCSAuto.BytesNcs(ncs);
            }
            if (resource is RIM rim)
            {
                return RIMAuto.BytesRim(rim);
            }
            if (resource is SSF ssf)
            {
                return SSFAuto.BytesSsf(ssf);
            }
            if (resource is TLK tlk)
            {
                return TLKAuto.BytesTlk(tlk);
            }
            if (resource is TPC tpc)
            {
                return TPCAuto.BytesTpc(tpc);
            }
            if (resource is TwoDA twoda)
            {
                return TwoDAAuto.Bytes2DA(twoda);
            }
            if (resource is VIS vis)
            {
                return VISAuto.BytesVis(vis);
            }

            throw new ArgumentException($"Invalid resource {resource} of type '{resource.GetType().Name}' passed to ResourceToBytes.");
        }

        private static readonly System.Collections.Generic.HashSet<string> GFFContentExtensions = new System.Collections.Generic.HashSet<string>
        {
            "ARE", "IFO", "GIT", "UTC", "UTI", "UTD", "UTE", "UTP", "UTS", "UTT", "UTW", "DLG", "CNV", "JRL", "PTH"
        };
        /// <summary>
        /// Automatically loads a resource from file data based on its type.
        /// </summary>
        /// <param name="data">The resource data.</param>
        /// <param name="resourceType">The resource type.</param>
        /// <returns>The loaded resource object, or null if loading failed.</returns>
        [CanBeNull]
        public static object LoadResource(byte[] data, ResourceType resourceType)
        {
            if (data == null || resourceType == null)
            {
                return null;
            }

            try
            {
                switch (resourceType.TypeId)
                {
                    case 2002: // ERF
                        var erfReader = new ERFBinaryReader(data);
                        return erfReader.Load();

                    case 2005: // GFF
                        var gffReader = new GFFBinaryReader(data);
                        return gffReader.Load();

                    case 2008: // RIM
                        var rimReader = new RIMBinaryReader(data);
                        return rimReader.Load();

                    case 10000: // PCC
                    case 10001: // UPK
                        var pccReader = new PCCBinaryReader(data);
                        return pccReader.Load();

                    case 2017: // SSF
                        var ssfReader = new SSFBinaryReader(data);
                        return ssfReader.Load();

                    case 2018: // TLK
                        var tlkReader = new TLKBinaryReader(data);
                        return tlkReader.Load();

                    case 2019: // 2DA
                        var twodaReader = new TwoDABinaryReader(data);
                        return twodaReader.Load();
                    case 2034: // CNV
                        return Andastra.Parsing.Resource.Generics.CNV.CNVHelper.ReadCnv(data);

                    // Add more resource types as they are implemented
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Automatically loads a resource from a file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The loaded resource object, or null if loading failed.</returns>
        [CanBeNull]
        public static object LoadResourceFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                ResourceType resourceType = ResourceType.FromExtension(extension);
                if (resourceType == null)
                {
                    return null;
                }

                return LoadResource(data, resourceType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Automatically saves a resource object to bytes based on its type.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/resource_auto.py:resource_to_bytes
        /// Original: def resource_to_bytes(resource: ...) -> bytes:
        /// </summary>
        /// <param name="resource">The resource object.</param>
        /// <param name="resourceType">The resource type (used for validation and format-specific handling).</param>
        /// <returns>The resource data as bytes, or null if saving failed.</returns>
        [CanBeNull]
        public static byte[] SaveResource(object resource, ResourceType resourceType)
        {
            if (resource == null || resourceType == null)
            {
                return null;
            }

            try
            {
                // Delegate to ResourceToBytes which handles all resource types by checking the object type
                // This matches the PyKotor implementation where resource_to_bytes handles all formats
                return ResourceToBytes(resource);
            }
            catch (ArgumentException)
            {
                // ResourceToBytes throws ArgumentException for unsupported types
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Automatically saves a resource object to a file.
        /// </summary>
        /// <param name="resource">The resource object.</param>
        /// <param name="resourceType">The resource type.</param>
        /// <param name="filePath">The file path to save to.</param>
        /// <returns>True if saving succeeded, false otherwise.</returns>
        public static bool SaveResourceToFile(object resource, ResourceType resourceType, string filePath)
        {
            if (resource == null || resourceType == null || string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                byte[] data = SaveResource(resource, resourceType);
                if (data == null)
                {
                    return false;
                }

                File.WriteAllBytes(filePath, data);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
