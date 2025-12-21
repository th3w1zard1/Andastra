using System.Numerics;
using Andastra.Runtime.MonoGame.Interfaces;

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

        /// <summary>
        /// Fog mode (exponential, linear, etc.).
        /// </summary>
        public FogMode Mode;

        /// <summary>
        /// Fog start distance (alias for StartDistance).
        /// </summary>
        public float Start
        {
            get { return StartDistance; }
            set { StartDistance = value; }
        }

        /// <summary>
        /// Fog end distance (alias for EndDistance).
        /// </summary>
        public float End
        {
            get { return EndDistance; }
            set { EndDistance = value; }
        }

        /// <summary>
        /// Whether height-based fog is enabled.
        /// </summary>
        public bool HeightFog;

        /// <summary>
        /// Whether volumetric fog is enabled.
        /// </summary>
        public bool Volumetric;
    }
}



