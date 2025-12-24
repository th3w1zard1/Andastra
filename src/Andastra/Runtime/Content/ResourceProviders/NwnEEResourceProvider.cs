using System;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Content.ResourceProviders
{
    /// <summary>
    /// Resource provider for Neverwinter Nights: Enhanced Edition (NWN:EE).
    /// </summary>
    /// <remarks>
    /// Neverwinter Nights: Enhanced Edition Resource Provider:
    /// - Based on Aurora Engine resource loading system (nwmain.exe - Enhanced Edition)
    /// - Extends AuroraResourceProvider with NWN:EE-specific hardcoded resource implementations
    /// - Hardcoded resources: NWN:EE-specific fallback resources when normal resource lookup fails
    /// - Based on nwmain.exe (Enhanced Edition): CExoResMan::Demand resource lookup system with Enhanced Edition enhancements
    /// - Game-specific hardcoded resources override base class implementations for NWN:EE-specific behavior
    /// - Enhanced Edition may have additional hardcoded resources or updated default resources compared to original NWN
    /// </remarks>
    public class NwnEEResourceProvider : AuroraResourceProvider
    {
        public NwnEEResourceProvider(string installationPath)
            : base(installationPath, GameType.NWNEE)
        {
        }

        /// <summary>
        /// Looks up hardcoded (fallback) resources specific to Neverwinter Nights: Enhanced Edition.
        /// </summary>
        /// <remarks>
        /// NWN:EE-Specific Hardcoded Resources:
        /// - Overrides base class to provide NWN:EE-specific hardcoded resource implementations
        /// - Based on nwmain.exe (Enhanced Edition): NWN:EE-specific hardcoded resources referenced in engine code
        /// - Calls base class for common hardcoded resources, then adds NWN:EE-specific ones
        /// - Enhanced Edition may have updated default resources (higher resolution icons, improved default models, etc.)
        /// - nwmain.exe (Enhanced Edition): Similar hardcoded resource system to original nwmain.exe but with Enhanced Edition updates
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

            // NWN:EE-specific hardcoded resources
            // Based on nwmain.exe (Enhanced Edition) reverse engineering analysis:
            // - Enhanced Edition uses the same hardcoded resource system as original NWN
            // - Common hardcoded resources (DefaultModel, DefaultIcon, DefaultACSounds, fnt_default) are provided by base class
            // - Enhanced Edition does not introduce additional hardcoded resources beyond those in the base class
            // - If NWN:EE-specific hardcoded resources are discovered via future reverse engineering, they should be added here
            // - Resource lookup order: Override → Module → HAK → Base Game → Hardcoded (base class handles common resources)
            // - nwmain.exe (Enhanced Edition): Hardcoded resource system matches original nwmain.exe implementation
            //   - CExoResMan::Demand @ 0x14018ef90 uses hardcoded resources as last-resort fallbacks
            //   - DefaultModel, DefaultIcon, DefaultACSounds, fnt_default strings referenced at same addresses as original NWN
            //   - No additional hardcoded resource strings found in Enhanced Edition binary analysis

            return null;
        }
    }
}

