namespace Andastra.Runtime.Core.Game
{
    /// <summary>
    /// Character creation data for new game initialization.
    /// </summary>
    /// <remarks>
    /// Character Creation Data:
    /// - Based on swkotor.exe and swkotor2.exe character creation system
    /// - Used during new game initialization to create player entity
    /// - Contains: Class, Gender, Appearance, Portrait, Name, Attributes, Skills
    /// </remarks>
    public class CharacterCreationData
    {
        /// <summary>
        /// Character class (Soldier, Scout, Scoundrel, Jedi Guardian, Jedi Consular, Jedi Sentinel).
        /// </summary>
        public CharacterClass Class { get; set; }

        /// <summary>
        /// Character gender (Male or Female).
        /// </summary>
        public Gender Gender { get; set; }

        /// <summary>
        /// Appearance type ID (from appearance.2da).
        /// </summary>
        public int Appearance { get; set; }

        /// <summary>
        /// Portrait ID (from portraits.2da).
        /// </summary>
        public int Portrait { get; set; }

        /// <summary>
        /// Character name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Strength attribute.
        /// </summary>
        public int Strength { get; set; }

        /// <summary>
        /// Dexterity attribute.
        /// </summary>
        public int Dexterity { get; set; }

        /// <summary>
        /// Constitution attribute.
        /// </summary>
        public int Constitution { get; set; }

        /// <summary>
        /// Intelligence attribute.
        /// </summary>
        public int Intelligence { get; set; }

        /// <summary>
        /// Wisdom attribute.
        /// </summary>
        public int Wisdom { get; set; }

        /// <summary>
        /// Charisma attribute.
        /// </summary>
        public int Charisma { get; set; }
    }

    /// <summary>
    /// Character class enumeration for KOTOR/TSL.
    /// </summary>
    /// <remarks>
    /// Character Class Enum:
    /// - Based on swkotor.exe and swkotor2.exe classes.2da
    /// - Class IDs: Soldier=0, Scout=1, Scoundrel=2, JediGuardian=3, JediConsular=4, JediSentinel=5
    /// </remarks>
    public enum CharacterClass
    {
        Soldier = 0,
        Scout = 1,
        Scoundrel = 2,
        JediGuardian = 3,
        JediConsular = 4,
        JediSentinel = 5
    }

    /// <summary>
    /// Gender enumeration.
    /// </summary>
    /// <remarks>
    /// Gender Enum:
    /// - Based on swkotor.exe and swkotor2.exe gender system
    /// - Gender values: Male=0, Female=1
    /// </remarks>
    public enum Gender
    {
        Male = 0,
        Female = 1
    }
}

