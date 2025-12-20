using System.Numerics;

namespace Andastra.Runtime.Games.Eclipse.Lighting
{
    /// <summary>
    /// Fog settings for Eclipse engine lighting system.
    /// </summary>
    public struct FogSettings
    {
        /// <summary>
        /// Fog color.
        /// </summary>
        public Vector3 Color;

        /// <summary>
        /// Fog density.
        /// </summary>
        public float Density;

        /// <summary>
        /// Fog start distance.
        /// </summary>
        public float StartDistance;

        /// <summary>
        /// Fog end distance.
        /// </summary>
        public float EndDistance;

        /// <summary>
        /// Whether fog is enabled.
        /// </summary>
        public bool Enabled;
    }
}

