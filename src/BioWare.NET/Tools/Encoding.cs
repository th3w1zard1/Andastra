using System;
using System.Linq;
using System.Text;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/encoding.py
    public static class Encoding
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/encoding.py:22-144
        // Original: def decode_bytes_with_fallbacks(byte_content: bytes | bytearray, errors: str = "strict", encoding: str | None = None, lang: Language | None = None, only_8bit_encodings: bool | None = False) -> str:
        public static string DecodeBytesWithFallbacks(
            byte[] byteContent,
            string errors = "strict",
            string encoding = null,
            Language? lang = null,
            bool only8BitEncodings = false)
        {
            // Try provided encoding first
            string providedEncoding = encoding ?? (lang.HasValue ? lang.Value.GetEncoding() : null);
            if (providedEncoding != null)
            {
                try
                {
                    return System.Text.Encoding.GetEncoding(providedEncoding).GetString(byteContent);
                }
                catch (Exception)
                {
                    // Fall through to auto-detection
                    providedEncoding = null;
                }
            }

            // Fallback to windows-1252 or utf-8 if charset_normalizer not available
            if (providedEncoding == null)
            {
                providedEncoding = only8BitEncodings ? "windows-1252" : "utf-8";
            }

            try
            {
                return System.Text.Encoding.GetEncoding(providedEncoding).GetString(byteContent);
            }
            catch (Exception)
            {
                // Try with error handling
                if (errors == "replace")
                {
                    try
                    {
                        return System.Text.Encoding.GetEncoding(providedEncoding).GetString(byteContent);
                    }
                    catch (Exception)
                    {
                        // Final fallback to latin1
                        return System.Text.Encoding.GetEncoding("latin1").GetString(byteContent);
                    }
                }
                else
                {
                    // Try utf-8 first
                    try
                    {
                        return System.Text.Encoding.UTF8.GetString(byteContent);
                    }
                    catch (Exception)
                    {
                        // Final fallback to latin1
                        return System.Text.Encoding.GetEncoding("latin1").GetString(byteContent);
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/encoding.py:147-159
        // Original: def get_charset_from_singlebyte_encoding(encoding: str, *, indexing: bool = True) -> list[str]:
        public static string[] GetCharsetFromSinglebyteEncoding(string encoding, bool indexing = true)
        {
            var charset = new System.Collections.Generic.List<string>();
            var enc = System.Text.Encoding.GetEncoding(encoding);
            for (int i = 0; i < 256; i++)
            {
                try
                {
                    charset.Add(enc.GetString(new byte[] { (byte)i }));
                }
                catch (Exception)
                {
                    if (indexing)
                    {
                        charset.Add("");
                    }
                }
            }
            return charset.ToArray();
        }
    }
}
