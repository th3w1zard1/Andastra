using System;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Content.ResourceProviders
{
    /// <summary>
    /// Resource provider for Neverwinter Nights 2 (NWN2).
    /// </summary>
    /// <remarks>
    /// Neverwinter Nights 2 Resource Provider:
    /// - Based on Aurora Engine resource loading system (nwn2main.exe)
    /// - Extends AuroraResourceProvider with NWN2-specific hardcoded resource implementations
    /// - Hardcoded resources: NWN2-specific fallback resources when normal resource lookup fails
    /// - Based on nwn2main.exe: CExoResMan::Demand resource lookup system (similar to nwmain.exe but with NWN2-specific addresses)
    /// - Game-specific hardcoded resources override base class implementations for NWN2-specific behavior
    /// </remarks>
    public class Nwn2ResourceProvider : AuroraResourceProvider
    {
        public Nwn2ResourceProvider(string installationPath)
            : base(installationPath, GameType.NWN2)
        {
        }

        /// <summary>
        /// Looks up hardcoded (fallback) resources specific to Neverwinter Nights 2.
        /// </summary>
        /// <remarks>
        /// NWN2-Specific Hardcoded Resources:
        /// - Overrides base class to provide NWN2-specific hardcoded resource implementations
        /// - Based on nwn2main.exe: NWN2-specific hardcoded resources referenced in engine code
        /// - Calls base class for common hardcoded resources, then adds NWN2-specific ones
        /// - nwn2main.exe: Similar hardcoded resource system to nwmain.exe but with NWN2-specific addresses
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

            // NWN2-specific hardcoded resources
            // Based on analysis of nwn2main.exe: NWN2 uses the same hardcoded resource system as NWN
            // No NWN2-specific hardcoded resources have been identified that differ from the common ones
            // All hardcoded resources (DefaultModel, DefaultIcon, DefaultACSounds, fnt_default) are shared
            // across all Aurora Engine games and are provided by the base class implementation
            // If NWN2-specific hardcoded resources are discovered in the future, they should be added here

            return null;
        }
    }
}

