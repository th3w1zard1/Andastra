namespace Andastra.Runtime.Graphics.Common.Enums
{
    /// <summary>
    /// Types of particle emitters for graphics rendering.
    /// Based on particle system in daorigins.exe, DragonAge2.exe.
    /// Different emitter types produce different particle effects.
    /// </summary>
    public enum ParticleEmitterType
    {
        /// <summary>
        /// Fire particle emitter.
        /// </summary>
        Fire = 0,

        /// <summary>
        /// Smoke particle emitter.
        /// </summary>
        Smoke = 1,

        /// <summary>
        /// Magic particle emitter.
        /// </summary>
        Magic = 2,

        /// <summary>
        /// Environmental particle emitter.
        /// </summary>
        Environmental = 3,

        /// <summary>
        /// Explosion particle emitter.
        /// </summary>
        Explosion = 4,

        /// <summary>
        /// Custom particle emitter.
        /// </summary>
        Custom = 5
    }
}

