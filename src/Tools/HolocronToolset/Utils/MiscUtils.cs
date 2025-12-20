using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Andastra.Parsing.Formats.RIM;

namespace HolocronToolset.Utils
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:117
    // Original: def get_nums(string_input: str) -> list[int]:
    public static class MiscUtils
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:117-140
        // Original: def get_nums(string_input: str) -> list[int]:
        public static List<int> GetNums(string stringInput)
        {
            var nums = new List<int>();
            var currentNum = new StringBuilder();
            foreach (char c in stringInput + " ")
            {
                if (char.IsDigit(c))
                {
                    currentNum.Append(c);
                }
                else if (currentNum.Length > 0)
                {
                    if (int.TryParse(currentNum.ToString(), out int num))
                    {
                        nums.Add(num);
                    }
                    currentNum.Clear();
                }
            }
            return nums;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:143-145
        // Original: def open_link(link: str):
        public static void OpenLink(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return;
            }

            try
            {
                var uri = new Uri(link);
                // TODO: SIMPLIFIED - Using Process.Start as workaround, should use Avalonia's Launcher when TopLevel is available
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = link,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore errors
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:148-153
        // Original: def clamp(value: float, min_value: float, max_value: float) -> float:
        public static float Clamp(float value, float minValue, float maxValue)
        {
            return Math.Max(minValue, Math.Min(value, maxValue));
        }

        public static double Clamp(double value, double minValue, double maxValue)
        {
            return Math.Max(minValue, Math.Min(value, maxValue));
        }

        public static int Clamp(int value, int minValue, int maxValue)
        {
            return Math.Max(minValue, Math.Min(value, maxValue));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:221-262
        // Original: def get_resource_from_file(filepath, resname, restype) -> bytes:
        public static byte[] GetResourceFromFile(string filepath, string resname, Andastra.Parsing.Resource.ResourceType restype)
        {
            if (string.IsNullOrEmpty(filepath) || !System.IO.File.Exists(filepath))
            {
                throw new FileNotFoundException($"File not found: {filepath}");
            }

            string ext = System.IO.Path.GetExtension(filepath).ToLowerInvariant();

            // Check if it's an ERF type file
            if (ext == ".erf" || ext == ".mod" || ext == ".sav" || ext == ".hak")
            {
                // TODO: STUB - Implement ERF file reading using Andastra.Parsing
                throw new NotImplementedException("ERF file reading not yet implemented");
            }
            else if (ext == ".rim")
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:252-254
                // Original: rim: RIM = read_rim(filepath); data = rim.get(resname, restype)
                RIM rim = RIMAuto.ReadRim(filepath);
                byte[] data = rim.Get(resname, restype);
                
                if (data == null)
                {
                    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:258-260
                    // Original: if data is None: raise ValueError("Could not find resource in RIM/ERF")
                    throw new ArgumentException($"Could not find resource '{resname}.{restype.Extension}' in RIM file '{filepath}'");
                }
                
                return data;
            }
            else
            {
                // Regular file
                return System.IO.File.ReadAllBytes(filepath);
            }
        }
    }
}
