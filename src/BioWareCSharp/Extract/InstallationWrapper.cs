using System.Collections.Generic;
using BioWareCSharp.Common.Installation;
using ResourceType = AuroraEngine.Common.Resources.ResourceType;
using LocationResult = AuroraEngine.Common.Resources.LocationResult;

namespace BioWareCSharp.Common.Extract
{
    // Thin wrapper to mirror PyKotor extract.installation.Installation semantics.
    public class InstallationWrapper
    {
        private readonly Installation.Installation _installation;

        public InstallationWrapper(string installPath)
        {
            _installation = new Installation.Installation(installPath);
        }

        public AuroraEngine.Common.Installation.ResourceResult Resource(string resref, ResourceType restype)
        {
            return _installation.Resources.LookupResource(resref, restype);
        }

        public List<LocationResult> Locate(string resref, ResourceType restype)
        {
            return _installation.Resources.LocateResource(resref, restype);
        }

        public Installation.Installation Inner => _installation;
    }
}

