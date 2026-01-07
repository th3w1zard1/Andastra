using Andastra.Core.Common;

// Forwarding types to maintain backward compatibility
// These delegate to the new Andastra.Core.Common implementations

namespace Andastra.Parsing.Common
{
    public enum BioWareGame
    {
        K1 = Andastra.Core.Common.BioWareGame.K1,
        K2 = Andastra.Core.Common.BioWareGame.K2,
        K3 = Andastra.Core.Common.BioWareGame.K3
    }

    public static class GameExtensions
    {
        public static bool IsK1(this BioWareGame game) => ((Andastra.Core.Common.BioWareGame)game).IsK1();
        public static bool IsK2(this BioWareGame game) => ((Andastra.Core.Common.BioWareGame)game).IsK2();
        public static bool IsK3(this BioWareGame game) => ((Andastra.Core.Common.BioWareGame)game).IsK3();
        public static bool IsKotOR(this BioWareGame game) => ((Andastra.Core.Common.BioWareGame)game).IsKotOR();
        public static bool IsTSL(this BioWareGame game) => ((Andastra.Core.Common.BioWareGame)game).IsTSL();
        public static bool IsInfinity(this BioWareGame game) => ((Andastra.Core.Common.BioWareGame)game).IsInfinity();
        public static string GetDisplayName(this BioWareGame game) => ((Andastra.Core.Common.BioWareGame)game).GetDisplayName();
    }

    public enum Language
    {
        English = Andastra.Core.Common.Language.English,
        French = Andastra.Core.Common.Language.French,
        German = Andastra.Core.Common.Language.German,
        Italian = Andastra.Core.Common.Language.Italian,
        Spanish = Andastra.Core.Common.Language.Spanish,
        Polish = Andastra.Core.Common.Language.Polish,
        Korean = Andastra.Core.Common.Language.Korean,
        ChineseTraditional = Andastra.Core.Common.Language.ChineseTraditional,
        ChineseSimplified = Andastra.Core.Common.Language.ChineseSimplified,
        Japanese = Andastra.Core.Common.Language.Japanese
    }

    public enum Gender
    {
        Male = Andastra.Core.Common.Gender.Male,
        Female = Andastra.Core.Common.Gender.Female
    }

    public static class LanguageExtensions
    {
        public static string GetTwoLetterISOLanguageName(this Language language) => ((Andastra.Core.Common.Language)language).GetTwoLetterISOLanguageName();
        public static string GetDisplayName(this Language language) => ((Andastra.Core.Common.Language)language).GetDisplayName();
        public static Language FromTwoLetterISOLanguageName(string isoCode) => (Language)Andastra.Core.Common.LanguageExtensions.FromTwoLetterISOLanguageName(isoCode);
    }

    public class ResRef : IEquatable<ResRef>
    {
        private Andastra.Core.Common.ResRef _inner;

        public ResRef(string text) => _inner = new Andastra.Core.Common.ResRef(text);
        public static ResRef FromBlank() => new ResRef(string.Empty);

        public string Value => _inner.Value;
        public static int MaxLength => Andastra.Core.Common.ResRef.MaxLength;

        public bool Equals(ResRef other) => _inner.Equals(other?._inner);
        public override bool Equals(object obj) => obj is ResRef other && Equals(other);
        public override int GetHashCode() => _inner.GetHashCode();
        public override string ToString() => _inner.ToString();

        public static implicit operator string(ResRef resRef) => resRef.Value;
        public static implicit operator ResRef(string text) => new ResRef(text);
    }

    public class LocalizedString : IEquatable<LocalizedString>, IEnumerable<(Language, Gender, string)>
    {
        private Andastra.Core.Common.LocalizedString _inner;

        public LocalizedString() => _inner = new Andastra.Core.Common.LocalizedString();
        public LocalizedString(int strref) => _inner = new Andastra.Core.Common.LocalizedString(strref);
        public LocalizedString(int strref, string substring) => _inner = new Andastra.Core.Common.LocalizedString(strref, substring);

        public int StringRef => _inner.StringRef;
        public Dictionary<(Language, Gender), string> Substrings => _inner.Substrings;

        public string this[Language language, Gender gender] => _inner[(Andastra.Core.Common.Language)language, (Andastra.Core.Common.Gender)gender];

        public void AddSubstring(Language language, Gender gender, string substring) => 
            _inner.AddSubstring((Andastra.Core.Common.Language)language, (Andastra.Core.Common.Gender)gender, substring);

        public bool Equals(LocalizedString other) => _inner.Equals(other?._inner);
        public override bool Equals(object obj) => obj is LocalizedString other && Equals(other);
        public override int GetHashCode() => _inner.GetHashCode();
        public override string ToString() => _inner.ToString();

        public IEnumerator<(Language, Gender, string)> GetEnumerator() => _inner.Select(x => ((Language)x.Item1, (Gender)x.Item2, x.Item3)).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
