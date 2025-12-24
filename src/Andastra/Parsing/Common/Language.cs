using System;
using System.Collections.Generic;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:12-145
    // Original: class Language(IntEnum):
    /// <summary>
    /// Language IDs recognized by both KOTOR games.
    /// Found in the TalkTable header, and CExoLocStrings (LocalizedStrings) within GFFs.
    /// </summary>
    public enum Language
    {
        Unknown = 0x7FFFFFFE,
        English = 0,
        French = 1,
        German = 2,
        Italian = 3,
        Spanish = 4,
        Polish = 5,
        Afrikaans = 6,
        Basque = 7,
        Breton = 9,
        Catalan = 10,
        Chamorro = 11,
        Chichewa = 12,
        Corsican = 13,
        Danish = 14,
        Dutch = 15,
        Faroese = 16,
        Filipino = 18,
        Finnish = 19,
        Flemish = 20,
        Frisian = 21,
        Galician = 22,
        Ganda = 23,
        HaitianCreole = 24,
        HausaLatin = 25,
        Hawaiian = 26,
        Icelandic = 27,
        Ido = 28,
        Indonesian = 29,
        Igbo = 30,
        Irish = 31,
        Interlingua = 32,
        JavaneseLatin = 33,
        Latin = 34,
        Luxembourgish = 35,
        Maltese = 36,
        Norwegian = 37,
        Occitan = 38,
        Portuguese = 39,
        Scots = 40,
        ScottishGaelic = 41,
        Shona = 42,
        Soto = 43,
        SundaneseLatin = 44,
        Swahili = 45,
        Swedish = 46,
        Tagalog = 47,
        Tahitian = 48,
        Tongan = 49,
        UzbekLatin = 50,
        Walloon = 51,
        Xhosa = 52,
        Yoruba = 53,
        Welsh = 54,
        Zulu = 55,
        Bulgarian = 58,
        Belarisian = 59,
        Macedonian = 60,
        Russian = 61,
        SerbianCyrillic = 62,
        Tajik = 63,
        TatarCyrillic = 64,
        Ukrainian = 66,
        Uzbek = 67,
        Albanian = 68,
        BosnianLatin = 69,
        Czech = 70,
        Slovak = 71,
        Slovene = 72,
        Croatian = 73,
        Hungarian = 75,
        Romanian = 76,
        Greek = 77,
        Esperanto = 78,
        AzerbaijaniLatin = 79,
        Turkish = 81,
        TurkmenLatin = 82,
        Hebrew = 83,
        Arabic = 84,
        Estonian = 85,
        Latvian = 86,
        Lithuanian = 87,
        Vietnamese = 88,
        Thai = 89,
        Aymara = 90,
        Kinyarwanda = 91,
        KurdishLatin = 92,
        Malagasy = 93,
        MalayLatin = 94,
        Maori = 95,
        MoldovanLatin = 96,
        Samoan = 97,
        Somali = 98,
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:140-145
        // Original: KOREAN = 128 / CHINESE_TRADITIONAL = 129 / CHINESE_SIMPLIFIED = 130 / JAPANESE = 131
        Korean = 128,
        ChineseTraditional = 129,
        ChineseSimplified = 130,
        Japanese = 131
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:422-426
    // Original: class Gender(IntEnum):
    /// <summary>
    /// Gender for localized strings.
    /// </summary>
    public enum Gender
    {
        Male = 0,
        Female = 1
    }

    public static class LanguageExtensions
    {
        private static readonly HashSet<Language> Cp1250Languages = new HashSet<Language>
        {
            Language.Albanian,
            Language.BosnianLatin,
            Language.Croatian,
            Language.Czech,
            Language.Hungarian,
            Language.MoldovanLatin,
            Language.Polish,
            Language.Romanian,
            Language.Slovak,
            Language.Slovene
        };

        private static readonly HashSet<Language> Cp1251Languages = new HashSet<Language>
        {
            Language.Belarisian,
            Language.Bulgarian,
            Language.Macedonian,
            Language.Russian,
            Language.SerbianCyrillic,
            Language.Tajik,
            Language.TatarCyrillic,
            Language.Ukrainian,
            Language.Uzbek
        };

        private static readonly HashSet<Language> Cp1252Languages = new HashSet<Language>
        {
            Language.English,
            Language.French,
            Language.German,
            Language.Italian,
            Language.Spanish,
            Language.Afrikaans,
            Language.Basque,
            Language.Breton,
            Language.Catalan,
            Language.Chamorro,
            Language.Chichewa,
            Language.Corsican,
            Language.Danish,
            Language.Dutch,
            Language.Faroese,
            Language.Filipino,
            Language.Finnish,
            Language.Flemish,
            Language.Frisian,
            Language.Galician,
            Language.Ganda,
            Language.HaitianCreole,
            Language.HausaLatin,
            Language.Hawaiian,
            Language.Icelandic,
            Language.Ido,
            Language.Indonesian,
            Language.Igbo,
            Language.Irish,
            Language.Interlingua,
            Language.JavaneseLatin,
            Language.Latin,
            Language.Luxembourgish,
            Language.Maltese,
            Language.Maori,
            Language.Norwegian,
            Language.Occitan,
            Language.Portuguese,
            Language.Scots,
            Language.ScottishGaelic,
            Language.Shona,
            Language.Soto,
            Language.SundaneseLatin,
            Language.Swahili,
            Language.Swedish,
            Language.Tagalog,
            Language.Tahitian,
            Language.Tongan,
            Language.UzbekLatin,
            Language.Walloon,
            Language.Xhosa,
            Language.Yoruba,
            Language.Welsh,
            Language.Zulu
        };

        private static readonly HashSet<Language> Cp1254Languages = new HashSet<Language>
        {
            Language.AzerbaijaniLatin,
            Language.Turkish,
            Language.TurkmenLatin
        };

        private static readonly HashSet<Language> Cp1257Languages = new HashSet<Language>
        {
            Language.Estonian,
            Language.Latvian,
            Language.Lithuanian
        };

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:147-155
        // Original: def _missing_(cls, value: Any) -> Language:
        public static Language FromValue(int value)
        {
            if (Enum.IsDefined(typeof(Language), value))
            {
                return (Language)value;
            }

            if (value != 0x7FFFFFFF)
            {
                Console.WriteLine("Language integer not known: " + value);
            }

            return Language.English;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:156-165
        // Original: def is_8bit_encoding(self) -> bool:
        public static bool Is8BitEncoding(this Language language)
        {
            return language != Language.Unknown
                   && language != Language.Korean
                   && language != Language.Japanese
                   && language != Language.ChineseSimplified
                   && language != Language.ChineseTraditional
                   && language != Language.Thai;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:166-318
        // Original: def get_encoding(self) -> str | None:
        public static string GetEncoding(this Language language)
        {
            if (Cp1250Languages.Contains(language))
            {
                return "cp1250";
            }

            if (Cp1251Languages.Contains(language))
            {
                return "cp1251";
            }

            if (Cp1252Languages.Contains(language))
            {
                return "cp1252";
            }

            if (language == Language.Greek)
            {
                return "cp1253";
            }

            if (Cp1254Languages.Contains(language))
            {
                return "cp1254";
            }

            if (language == Language.Hebrew)
            {
                return "cp1255";
            }

            if (language == Language.Arabic)
            {
                return "cp1256";
            }

            if (Cp1257Languages.Contains(language))
            {
                return "cp1257";
            }

            if (language == Language.Vietnamese)
            {
                return "cp1258";
            }

            if (language == Language.Thai)
            {
                return "cp874";
            }

            if (language == Language.MalayLatin
                || language == Language.Samoan
                || language == Language.Somali)
            {
                return "ISO-8859-1";
            }

            if (language == Language.Aymara
                || language == Language.Esperanto
                || language == Language.Malagasy)
            {
                return "ISO-8859-3";
            }

            if (language == Language.KurdishLatin)
            {
                return "ISO-8859-9";
            }

            if (language == Language.Kinyarwanda)
            {
                return "ISO-8859-10";
            }

            if (language == Language.Korean)
            {
                return "cp949";
            }

            if (language == Language.ChineseTraditional)
            {
                return "cp950";
            }

            if (language == Language.ChineseSimplified)
            {
                return "cp936";
            }

            if (language == Language.Japanese)
            {
                return "cp932";
            }

            if (language == Language.Unknown)
            {
                return null;
            }

            throw new ArgumentException("No encoding defined for language: " + language);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:319-419
        // Original: def get_bcp47_code(self):
        public static string GetBcp47Code(this Language language)
        {
            Dictionary<Language, string> langMap = new Dictionary<Language, string>
            {
                { Language.English, "en" },
                { Language.French, "fr" },
                { Language.German, "de" },
                { Language.Italian, "it" },
                { Language.Spanish, "es" },
                { Language.Polish, "pl" },
                { Language.Afrikaans, "af" },
                { Language.Basque, "eu" },
                { Language.Breton, "br" },
                { Language.Catalan, "ca" },
                { Language.Chamorro, "ch" },
                { Language.Chichewa, "ny" },
                { Language.Corsican, "co" },
                { Language.Danish, "da" },
                { Language.Dutch, "nl" },
                { Language.Faroese, "fo" },
                { Language.Filipino, "filipino" },
                { Language.Finnish, "fi" },
                { Language.Flemish, "nl-BE" },
                { Language.Frisian, "fy" },
                { Language.Galician, "gl" },
                { Language.Ganda, "lg" },
                { Language.HaitianCreole, "ht" },
                { Language.HausaLatin, "ha" },
                { Language.Hawaiian, "haw" },
                { Language.Icelandic, "is" },
                { Language.Ido, "io" },
                { Language.Indonesian, "id" },
                { Language.Igbo, "ig" },
                { Language.Irish, "ga" },
                { Language.Interlingua, "ia" },
                { Language.JavaneseLatin, "jv" },
                { Language.Latin, "la" },
                { Language.Luxembourgish, "lb" },
                { Language.Maltese, "mt" },
                { Language.Norwegian, "no" },
                { Language.Occitan, "oc" },
                { Language.Portuguese, "pt" },
                { Language.Scots, "sco" },
                { Language.ScottishGaelic, "gd" },
                { Language.Shona, "sn" },
                { Language.Soto, "st" },
                { Language.SundaneseLatin, "su" },
                { Language.Swahili, "sw" },
                { Language.Swedish, "sv" },
                { Language.Tagalog, "tl" },
                { Language.Tahitian, "ty" },
                { Language.Tongan, "to" },
                { Language.UzbekLatin, "uz" },
                { Language.Walloon, "wa" },
                { Language.Xhosa, "xh" },
                { Language.Yoruba, "yo" },
                { Language.Welsh, "cy" },
                { Language.Zulu, "zu" },
                { Language.Bulgarian, "bg" },
                { Language.Belarisian, "be" },
                { Language.Macedonian, "mk" },
                { Language.Russian, "ru" },
                { Language.SerbianCyrillic, "sr" },
                { Language.Tajik, "tg" },
                { Language.TatarCyrillic, "tt" },
                { Language.Ukrainian, "uk" },
                { Language.Uzbek, "uz" },
                { Language.Albanian, "sq" },
                { Language.BosnianLatin, "bs" },
                { Language.Czech, "cs" },
                { Language.Slovak, "sk" },
                { Language.Slovene, "sl" },
                { Language.Croatian, "hr" },
                { Language.Hungarian, "hu" },
                { Language.Romanian, "ro" },
                { Language.Greek, "el" },
                { Language.Esperanto, "eo" },
                { Language.AzerbaijaniLatin, "az" },
                { Language.Turkish, "tr" },
                { Language.TurkmenLatin, "tk" },
                { Language.Hebrew, "he" },
                { Language.Arabic, "ar" },
                { Language.Estonian, "et" },
                { Language.Latvian, "lv" },
                { Language.Lithuanian, "lt" },
                { Language.Vietnamese, "vi" },
                { Language.Thai, "th" },
                { Language.Aymara, "ay" },
                { Language.Kinyarwanda, "rw" },
                { Language.KurdishLatin, "ku" },
                { Language.Malagasy, "mg" },
                { Language.MalayLatin, "ms" },
                { Language.Maori, "mi" },
                { Language.MoldovanLatin, "mo" },
                { Language.Samoan, "sm" },
                { Language.Somali, "so" },
                { Language.Korean, "ko" },
                { Language.ChineseTraditional, "zh-TW" },
                { Language.ChineseSimplified, "zh-CN" },
                { Language.Japanese, "ja" }
            };

            string code;
            if (langMap.TryGetValue(language, out code))
            {
                return code;
            }

            return null;
        }
    }
}
