namespace Andastra.Runtime.Graphics.MonoGame.Enums
{
    /// <summary>
    /// Light type enumeration for dynamic lighting system.
    /// </summary>
    public enum LightType
    {
        /// <summary>
        /// Point light (omnidirectional).
        /// </summary>
        Point,

        /// <summary>
        /// Spot light (directional with cone).
        /// </summary>
        Spot,

        /// <summary>
        /// Directional light (parallel rays, like sun).
        /// </summary>
        Directional,

        /// <summary>
        /// Area light (rectangular area).
        /// </summary>
        Area
    }
}



