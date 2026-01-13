using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BioWare.NET.Utility.MiscString
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/util.py:7-23
    // Original: def insert_newlines(text: str, length: int = 100) -> str:
    public static class StringUtilFunctions
    {
        public static string InsertNewlines(string text, int length = 100)
        {
            string[] words = text.Split(' ');
            string newString = "";
            string currentLine = "";

            foreach (string word in words)
            {
                if (currentLine.Length + word.Length + 1 <= length)
                {
                    currentLine += word + " ";
                }
                else
                {
                    newString += currentLine.TrimEnd() + "\n";
                    currentLine = word + " ";
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                newString += currentLine.TrimEnd();
            }

            return newString;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/util.py:26-51
        // Original: def ireplace(original: str, target: str, replacement: str) -> str:
        public static string IReplace(string original, string target, string replacement)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(target))
            {
                return original;
            }

            string result = "";
            int i = 0;
            int targetLength = target.Length;
            string targetLower = target.ToLower();
            string originalLower = original.ToLower();

            while (i < original.Length)
            {
                if (i + targetLength <= originalLower.Length && originalLower.Substring(i, targetLength) == targetLower)
                {
                    result += replacement;
                    i += targetLength;
                }
                else
                {
                    result += original[i];
                    i += 1;
                }
            }
            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/util.py:54-58
        // Original: def format_text(text: object, max_chars_before_newline: int = 20) -> str:
        public static string FormatText(object text, int maxCharsBeforeNewline = 20)
        {
            string textStr = text?.ToString() ?? "";
            if (textStr.Contains("\n") || textStr.Length > maxCharsBeforeNewline)
            {
                return $"\"\"\"{Environment.NewLine}{textStr}{Environment.NewLine}\"\"\"";
            }
            return $"'{textStr}'";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/util.py:61-67
        // Original: def first_char_diff_index(str1: str, str2: str) -> int:
        public static int FirstCharDiffIndex(string str1, string str2)
        {
            int minLength = Math.Min(str1.Length, str2.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (str1[i] != str2[i])
                {
                    return i;
                }
            }
            return minLength != str1.Length || minLength != str2.Length ? minLength : -1;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/util.py:70-72
        // Original: def generate_diff_marker_line(index: int, length: int) -> str:
        public static string GenerateDiffMarkerLine(int index, int length)
        {
            if (index == -1)
            {
                return "";
            }
            return new string(' ', index) + "^" + new string(' ', length - index - 1);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/util.py:75-113
        // Original: def compare_and_format(old_value: object, new_value: object) -> tuple[str, str]:
        public static Tuple<string, string> CompareAndFormat(object oldValue, object newValue)
        {
            string oldText = oldValue?.ToString() ?? "";
            string newText = newValue?.ToString() ?? "";
            string[] oldLines = oldText.Split('\n');
            string[] newLines = newText.Split('\n');
            List<string> formattedOld = new List<string>();
            List<string> formattedNew = new List<string>();

            int maxLines = Math.Max(oldLines.Length, newLines.Length);
            for (int i = 0; i < maxLines; i++)
            {
                string oldLine = i < oldLines.Length ? oldLines[i] : "";
                string newLine = i < newLines.Length ? newLines[i] : "";
                int diffIndex = FirstCharDiffIndex(oldLine, newLine);
                string markerLine = GenerateDiffMarkerLine(diffIndex, Math.Max(oldLine.Length, newLine.Length));

                formattedOld.Add(oldLine);
                formattedNew.Add(newLine);
                if (!string.IsNullOrEmpty(markerLine))
                {
                    formattedOld.Add(markerLine);
                    formattedNew.Add(markerLine);
                }
            }

            return Tuple.Create(string.Join(Environment.NewLine, formattedOld), string.Join(Environment.NewLine, formattedNew));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/util.py:116-233
        // Original: def striprtf(text: str) -> str:
        public static string StripRtf(string text)
        {
            Regex pattern = new Regex(@"\\([a-z]{1,32})(-?\d{1,10})?[ ]?|\\'([0-9a-f]{2})|\\([^a-z])|([{}])|[\r\n]+|(.)", RegexOptions.IgnoreCase);
            HashSet<string> destinations = new HashSet<string>
            {
                "aftncn", "aftnsep", "aftnsepc", "annotation", "atnauthor", "atndate", "atnicn", "atnid", "atnparent", "atnref", "atntime", "atrfend", "atrfstart",
                "author", "background", "bkmkend", "bkmkstart", "blipuid", "buptim", "category", "colorschememapping", "colortbl", "comment", "company", "creatim",
                "datafield", "datastore", "defchp", "defpap", "do", "doccomm", "docvar", "dptxbxtext", "ebcend", "ebcstart", "factoidname", "falt", "fchars", "ffdeftext",
                "ffentrymcr", "ffexitmcr", "ffformat", "ffhelptext", "ffl", "ffname", "ffstattext", "field", "file", "filetbl", "fldinst", "fldrslt", "fldtype", "fname",
                "fontemb", "fontfile", "fonttbl", "footer", "footerf", "footerl", "footerr", "footnote", "formfield", "ftncn", "ftnsep", "ftnsepc", "g", "generator", "gridtbl",
                "header", "headerf", "headerl", "headerr", "hl", "hlfr", "hlinkbase", "hlloc", "hlsrc", "hsv", "htmltag", "info", "keycode", "keywords", "latentstyles",
                "lchars", "levelnumbers", "leveltext", "lfolevel", "linkval", "list", "listlevel", "listname", "listoverride", "listoverridetable", "listpicture", "liststylename",
                "listtable", "listtext", "lsdlockedexcept", "macc", "maccPr", "mailmerge", "maln", "malnScr", "manager", "margPr", "mbar", "mbarPr", "mbaseJc", "mbegChr",
                "mborderBox", "mborderBoxPr", "mbox", "mboxPr", "mchr", "mcount", "mctrlPr", "md", "mdeg", "mdegHide", "mden", "mdiff", "mdPr", "me", "mendChr", "meqArr",
                "meqArrPr", "mf", "mfName", "mfPr", "mfunc", "mfuncPr", "mgroupChr", "mgroupChrPr", "mgrow", "mhideBot", "mhideLeft", "mhideRight", "mhideTop", "mhtmltag",
                "mlim", "mlimloc", "mlimlow", "mlimlowPr", "mlimupp", "mlimuppPr", "mm", "mmaddfieldname", "mmath", "mmathPict", "mmathPr", "mmaxdist", "mmc", "mmcJc",
                "mmconnectstr", "mmconnectstrdata", "mmcPr", "mmcs", "mmdatasource", "mmheadersource", "mmmailsubject", "mmodso", "mmodsofilter", "mmodsofldmpdata",
                "mmodsomappedname", "mmodsoname", "mmodsorecipdata", "mmodsosort", "mmodsosrc", "mmodsotable", "mmodsoudl", "mmodsoudldata", "mmodsouniquetag", "mmPr",
                "mmquery", "mmr", "mnary", "mnaryPr", "mnoBreak", "mnum", "mobjDist", "moMath", "moMathPara", "moMathParaPr", "mopEmu", "mphant", "mphantPr", "mplcHide",
                "mpos", "mr", "mrad", "mradPr", "mrPr", "msepChr", "mshow", "mshp", "msPre", "msPrePr", "msSub", "msSubPr", "msSubSup", "msSubSupPr", "msSup", "msSupPr",
                "mstrikeBLTR", "mstrikeH", "mstrikeTLBR", "mstrikeV", "msub", "msubHide", "msup", "msupHide", "mtransp", "mtype", "mvertJc", "mvfmf", "mvfml", "mvtof",
                "mvtol", "mzeroAsc", "mzeroDesc", "mzeroWid", "nesttableprops", "nextfile", "nonesttables", "objalias", "objclass", "objdata", "object", "objname", "objsect",
                "objtime", "oldcprops", "oldpprops", "oldsprops", "oldtprops", "oleclsid", "operator", "panose", "password", "passwordhash", "pgp", "pgptbl", "picprop",
                "pict", "pn", "pnseclvl", "pntext", "pntxta", "pntxtb", "printim", "private", "propname", "protend", "protstart", "protusertbl", "pxe", "result", "revtbl",
                "revtim", "rsidtbl", "rxe", "shp", "shpgrp", "shpinst", "shppict", "shprslt", "shptxt", "sn", "sp", "staticval", "stylesheet", "subject", "sv", "svb", "tc",
                "template", "themedata", "title", "txe", "ud", "upr", "userprops", "wgrffmtfilter", "windowcaption", "writereservation", "writereservhash", "xe", "xform",
                "xmlattrname", "xmlattrvalue", "xmlclose", "xmlname", "xmlnstbl", "xmlopen"
            };

            Dictionary<string, string> specialchars = new Dictionary<string, string>
            {
                { "par", "\n" },
                { "sect", "\n\n" },
                { "page", "\n\n" },
                { "line", "\n" },
                { "tab", "\t" },
                { "emdash", "\u2014" },
                { "endash", "\u2013" },
                { "emspace", "\u2003" },
                { "enspace", "\u2002" },
                { "qmspace", "\u2005" },
                { "bullet", "\u2022" },
                { "lquote", "\u2018" },
                { "rquote", "\u2019" },
                { "ldblquote", "\u201C" },
                { "rdblquote", "\u201D" }
            };

            List<Tuple<int, bool>> stack = new List<Tuple<int, bool>>();
            bool ignorable = false;
            int ucskip = 1;
            int curskip = 0;
            List<string> output = new List<string>();

            MatchCollection matches = pattern.Matches(text);
            foreach (Match match in matches)
            {
                string word = match.Groups[1].Success ? match.Groups[1].Value : null;
                string arg = match.Groups[2].Success ? match.Groups[2].Value : null;
                string hexcode = match.Groups[3].Success ? match.Groups[3].Value : null;
                string char_ = match.Groups[4].Success ? match.Groups[4].Value : null;
                string brace = match.Groups[5].Success ? match.Groups[5].Value : null;
                string tchar = match.Groups[6].Success ? match.Groups[6].Value : null;

                if (!string.IsNullOrEmpty(brace))
                {
                    curskip = 0;
                    if (brace == "{")
                    {
                        stack.Add(Tuple.Create(ucskip, ignorable));
                    }
                    else if (brace == "}")
                    {
                        var popped = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        ucskip = popped.Item1;
                        ignorable = popped.Item2;
                    }
                }
                else if (!string.IsNullOrEmpty(char_))
                {
                    curskip = 0;
                    if (char_ == "~")
                    {
                        if (!ignorable)
                        {
                            output.Add("\u00A0");
                        }
                    }
                    else if (char_ == "{" || char_ == "}" || char_ == "\\")
                    {
                        if (!ignorable)
                        {
                            output.Add(char_);
                        }
                    }
                    else if (char_ == "*")
                    {
                        ignorable = true;
                    }
                }
                else if (!string.IsNullOrEmpty(word))
                {
                    curskip = 0;
                    if (destinations.Contains(word))
                    {
                        ignorable = true;
                    }
                    else if (ignorable)
                    {
                        // Skip
                    }
                    else if (specialchars.ContainsKey(word))
                    {
                        output.Add(specialchars[word]);
                    }
                    else if (word == "uc")
                    {
                        ucskip = int.Parse(arg ?? "1");
                    }
                    else if (word == "u")
                    {
                        int c = int.Parse(arg ?? "0");
                        if (c < 0)
                        {
                            c += 0x10000;
                        }
                        output.Add(((char)c).ToString());
                        curskip = ucskip;
                    }
                }
                else if (!string.IsNullOrEmpty(hexcode))
                {
                    if (curskip > 0)
                    {
                        curskip -= 1;
                    }
                    else if (!ignorable)
                    {
                        int c = Convert.ToInt32(hexcode, 16);
                        output.Add(((char)c).ToString());
                    }
                }
                else if (!string.IsNullOrEmpty(tchar))
                {
                    if (curskip > 0)
                    {
                        curskip -= 1;
                    }
                    else if (!ignorable)
                    {
                        output.Add(tchar);
                    }
                }
            }

            return string.Join("", output);
        }
    }
}
