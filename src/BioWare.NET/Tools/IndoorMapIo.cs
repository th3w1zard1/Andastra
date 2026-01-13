using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.RIM;
using BioWare.NET.Resource;
using ResourceType = BioWare.NET.Common.ResourceType;

using JetBrains.Annotations;

namespace BioWare.NET.Tools
{
    /// <summary>
    /// Minimal headless helpers for embedding and extracting <c>.indoor</c> JSON from module containers.
    ///
    /// This mirrors PyKotor's KotorCLI behavior: modules built from <c>.indoor</c> embed the original JSON as
    /// <c>indoormap.txt</c> so extraction is deterministic (no reverse-engineering back to kits required).
    /// </summary>
    public static class IndoorMapIo
    {
        public const string EmbeddedResRef = "indoormap";

        /// <summary>
        /// Embed indoor JSON bytes into a module ERF/MOD as <c>indoormap.txt</c>.
        /// </summary>
        public static void EmbedIndoorJson(ERF mod, byte[] indoorJson)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            if (indoorJson == null) throw new ArgumentNullException(nameof(indoorJson));
            mod.SetData(EmbeddedResRef, ResourceType.TXT, indoorJson);
        }

        /// <summary>
        /// Attempt to read <c>indoormap.txt</c> from an ERF/MOD.
        /// </summary>
        [CanBeNull]
        public static byte[] TryExtractFromErf(ERF erf)
        {
            if (erf == null) return null;
            return erf.Get(EmbeddedResRef, ResourceType.TXT);
        }

        /// <summary>
        /// Attempt to read <c>indoormap.txt</c> from a RIM.
        /// </summary>
        [CanBeNull]
        public static byte[] TryExtractFromRim(RIM rim)
        {
            if (rim == null) return null;
            return rim.Get(EmbeddedResRef, ResourceType.TXT);
        }

        /// <summary>
        /// Try extract embedded indoor JSON from module containers on disk.
        ///
        /// Containers supported:
        /// - <c>.mod</c> / <c>.erf</c> (ERF)
        /// - <c>.rim</c> / <c>_s.rim</c> (RIM)
        /// - <c>_dlg.erf</c> (ERF)
        /// </summary>
        [CanBeNull]
        public static byte[] TryExtractEmbeddedIndoorJsonFromModuleFiles(IEnumerable<string> moduleFiles)
        {
            if (moduleFiles == null) return null;

            var files = moduleFiles
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0) return null;

            // Prefer .mod first if present.
            files = files
                .OrderBy(f => f.EndsWith(".mod", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();

            foreach (var path in files)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    if (path.EndsWith(".rim", StringComparison.OrdinalIgnoreCase))
                    {
                        var rim = RIMAuto.ReadRim(path);
                        var data = TryExtractFromRim(rim);
                        if (data != null && data.Length > 0)
                        {
                            return data;
                        }
                        continue;
                    }

                    // .mod, .erf, _dlg.erf
                    var erf = ERFAuto.ReadErf(path);
                    var erfData = TryExtractFromErf(erf);
                    if (erfData != null && erfData.Length > 0)
                    {
                        return erfData;
                    }
                }
                catch
                {
                    // Ignore container read errors; try next file.
                }
            }

            return null;
        }
    }
}


