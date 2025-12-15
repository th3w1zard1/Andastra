namespace Odyssey.Core.Enums
{
    /// <summary>
    /// Character ability scores (D20 system).
    /// </summary>
    /// <remarks>
    /// Ability Enum (D20 System):
    /// - Based on swkotor2.exe D20 ability system
    /// - Located via string references: Ability scores stored in creature templates (UTC GFF)
    /// - D20 standard abilities: STR (Strength), DEX (Dexterity), CON (Constitution),
    ///   INT (Intelligence), WIS (Wisdom), CHA (Charisma)
    /// - Ability scores range: 1-30 (typical), modifiers = (score - 10) / 2 (rounded down)
    /// - Ability modifiers used for: Attack rolls, skill checks, saving throws, damage, etc.
    /// - Classes.2da defines ability requirements and modifiers for classes
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

