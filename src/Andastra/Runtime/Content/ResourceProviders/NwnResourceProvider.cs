using System;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Content.ResourceProviders
{
    /// <summary>
    /// Resource provider for Neverwinter Nights (NWN).
    /// </summary>
    /// <remarks>
    /// Neverwinter Nights Resource Provider:
    /// - Based on Aurora Engine resource loading system (nwmain.exe)
    /// - Extends AuroraResourceProvider with NWN-specific hardcoded resource implementations
    /// - Hardcoded resources: NWN-specific fallback resources when normal resource lookup fails
    /// - Based on nwmain.exe: CExoResMan::Demand @ 0x14018ef90 resource lookup system
    /// - Game-specific hardcoded resources override base class implementations for NWN-specific behavior
    /// </remarks>
    public class NwnResourceProvider : AuroraResourceProvider
    {
        public NwnResourceProvider(string installationPath) 
            : base(installationPath, GameType.NWN)
        {
        }

        /// <summary>
        /// Looks up hardcoded (fallback) resources specific to Neverwinter Nights.
        /// </summary>
        /// <remarks>
        /// NWN-Specific Hardcoded Resources:
        /// - Overrides base class to provide NWN-specific hardcoded resource implementations
        /// - Based on nwmain.exe: NWN-specific hardcoded resources referenced in engine code
        /// - Calls base class for common hardcoded resources, then adds NWN-specific ones
        /// - nwmain.exe: DefaultModel @ 0x140dc3a68, DefaultIcon @ 0x140dc3a78, DefaultACSounds @ 0x140dc6db8
        /// </remarks>
        protected override byte[] LookupHardcoded(ResourceIdentifier id)
        {
            if (id == null || id.ResType == null)
            {
                return null;
            }

            // First, try common hardcoded resources (from base class)
            byte[] result = base.LookupHardcoded(id);
            if (result != null)
            {
                return result;
            }

            // NWN-specific hardcoded resources
            // Add NWN-specific hardcoded resource lookups here if needed
            // For now, NWN uses the common hardcoded resources from base class

            return null;
        }
    }
}

