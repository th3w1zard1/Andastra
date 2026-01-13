namespace Andastra.Runtime.Core.Enums
{
    /// <summary>
    /// Character ability scores (D20 system).
    /// </summary>
    /// <remarks>
    /// Ability Enum (D20 System):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) D20 ability system
    /// - Located via string references: Ability scores stored in creature templates (UTC GFF)
    /// - Ability field names: "STR" @ 0x007c3a44, "DEX" @ 0x007c3a54, "CON" @ 0x007c3a64
    /// - "INT" @ 0x007c3a74, "WIS" @ 0x007c3a84, "CHA" @ 0x007c3a94
    /// - Ability modifier strings: " + %d (Str Modifier)" @ 0x007c3a44, " + %d (Dex Modifier)" @ 0x007c3a84
    /// - " + %d (Dex Mod)" @ 0x007c3e54, "DEXBONUS" @ 0x007c4320, "DEXAdjust" @ 0x007c2bec
    /// - "MinDEX" @ 0x007c2f50 (minimum DEX for armor), "LBL_DEX" @ 0x007cfb60, "LBL_DEXTERITY" @ 0x007cfb50
    /// - Ability damage: "DAM_STR" @ 0x007bf120, "DAM_DEX" @ 0x007bf118 (ability damage types)
    /// - "POISONTRACE: Applying STR damage: %d\n" @ 0x007bf038, "POISONTRACE: Applying DEX damage: %d\n" @ 0x007bf010
    /// - D20 standard abilities: STR (Strength), DEX (Dexterity), CON (Constitution),
    ///   INT (Intelligence), WIS (Wisdom), CHA (Charisma)
    /// - Ability scores range: 1-30 (typical), modifiers = (score - 10) / 2 (rounded down)
    /// - Ability modifiers used for: Attack rolls, skill checks, saving throws, damage, etc.
    /// - Classes.2da defines ability requirements and modifiers for classes
    /// - Original implementation: 0x005226d0 @ 0x005226d0 saves ability scores to creature GFF
    /// - 0x0050c510 @ 0x0050c510 loads ability scores from UTC template
    /// </remarks>
    public enum Ability
    {
        Strength = 0,
        Dexterity = 1,
        Constitution = 2,
        Intelligence = 3,
        Wisdom = 4,
        Charisma = 5
    }
}

