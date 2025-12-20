using Andastra.Runtime.Core.MDL;

namespace Andastra.Runtime.Core.MDL
{
    /// <summary>
    /// Interface for loading MDL models.
    /// </summary>
    /// <remarks>
    /// This interface exists to break the circular dependency between Core and Content.
    /// Content.MDLLoader implements this interface.
    /// </remarks>
    public interface IMDLLoader
    {
        /// <summary>
        /// Loads an MDL model by ResRef.
        /// </summary>
        /// <param name="resRef">Resource reference (model name without extension)</param>
        /// <returns>Loaded MDL model, or null if not found</returns>
        MDLModel Load(string resRef);
    }
}

