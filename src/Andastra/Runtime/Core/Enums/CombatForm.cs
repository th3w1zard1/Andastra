namespace Andastra.Runtime.Core.Enums
{
    /// <summary>
    /// Combat forms available in KOTOR 2: The Sith Lords.
    /// </summary>
    /// <remarks>
    /// Combat Form Enum:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) combat form system
    /// - Located via string references: GetIsFormActive function in NWScript
    /// - Combat form constants from k2_nwscript.nss:
    ///   - FORM_SABER_I_SHII_CHO = 258
    ///   - FORM_SABER_II_MAKASHI = 259
    ///   - FORM_SABER_III_SORESU = 260
    ///   - FORM_SABER_IV_ATARU = 261
    ///   - FORM_SABER_V_SHIEN = 262
    ///   - FORM_SABER_VI_NIMAN = 263
    ///   - FORM_SABER_VII_JUYO = 264
    ///   - FORM_FORCE_I_FOCUS = 265
    ///   - FORM_FORCE_II_POTENCY = 266
    ///   - FORM_FORCE_III_AFFINITY = 267
    ///   - FORM_FORCE_IV_MASTERY = 268
    /// - Original implementation: Combat forms are stored as "ActiveCombatForm" in entity data
    /// - Forms affect combat mechanics, attack bonuses, defense bonuses, and cursor display
    /// - Lightsaber forms (258-264): Affect melee combat with lightsabers
    /// - Force forms (265-268): Affect Force power usage and effectiveness
    /// - Form activation: Set via SetIsFormActive NWScript function or combat form selection UI
    /// - Form checking: GetIsFormActive NWScript function checks if specific form is active
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Combat forms are K2-specific feature (not in K1)
    /// </remarks>
    public enum CombatForm
    {
        /// <summary>
        /// No combat form active.
        /// </summary>
        None = 0,

        /// <summary>
        /// Shii-Cho (Form I) - Basic lightsaber form, balanced offense and defense.
        /// </summary>
        ShiiCho = 258,

        /// <summary>
        /// Makashi (Form II) - Dueling form, high attack bonus, low defense.
        /// </summary>
        Makashi = 259,

        /// <summary>
        /// Soresu (Form III) - Defensive form, high defense bonus, low attack.
        /// </summary>
        Soresu = 260,

        /// <summary>
        /// Ataru (Form IV) - Aggressive form, high attack bonus, low defense.
        /// </summary>
        Ataru = 261,

        /// <summary>
        /// Shien (Form V) - Balanced form, moderate attack and defense.
        /// </summary>
        Shien = 262,

        /// <summary>
        /// Niman (Form VI) - Balanced form, moderate attack and defense, good for Force powers.
        /// </summary>
        Niman = 263,

        /// <summary>
        /// Juyo (Form VII) - Aggressive form, very high attack bonus, very low defense.
        /// </summary>
        Juyo = 264,

        /// <summary>
        /// Force Focus (Form I) - Enhances Force power effectiveness.
        /// </summary>
        ForceFocus = 265,

        /// <summary>
        /// Force Potency (Form II) - Further enhances Force power effectiveness.
        /// </summary>
        ForcePotency = 266,

        /// <summary>
        /// Force Affinity (Form III) - Strong Force power enhancement.
        /// </summary>
        ForceAffinity = 267,

        /// <summary>
        /// Force Mastery (Form IV) - Maximum Force power enhancement.
        /// </summary>
        ForceMastery = 268
    }
}

