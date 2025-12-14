using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HolocronToolset.NET.Config
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_info.py:37
    // Original: LOCAL_PROGRAM_INFO: dict[str, Any] = {
    public static class ConfigInfo
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_info.py:110-111
        // Original: CURRENT_VERSION = LOCAL_PROGRAM_INFO["currentVersion"]
        public const string CurrentVersion = "4.0.0";

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_info.py:37-108
        // Original: LOCAL_PROGRAM_INFO: dict[str, Any] = {
        public static Dictionary<string, object> LocalProgramInfo => new Dictionary<string, object>
        {
            ["currentVersion"] = CurrentVersion,
            ["toolsetLatestVersion"] = "3.1.1",
            ["toolsetLatestBetaVersion"] = "4.0.0",
            ["updateInfoLink"] = "https://api.github.com/repos/th3w1zard1/PyKotor/contents/Tools/HolocronToolset/src/toolset/config/config_info.py",
            ["updateBetaInfoLink"] = "https://api.github.com/repos/th3w1zard1/PyKotor/contents/Tools/HolocronToolset/src/toolset/config/config_info.py?ref=bleeding-edge",
            ["toolsetDownloadLink"] = "https://deadlystream.com/files/file/1982-holocron-toolset",
            ["toolsetBetaDownloadLink"] = "https://github.com/th3w1zard1/PyKotor/releases/tag/v{tag}-toolset",
            ["toolsetDirectLinks"] = new Dictionary<string, Dictionary<string, List<string>>>
            {
                ["Darwin"] = new Dictionary<string, List<string>>
                {
                    ["32bit"] = new List<string>(),
                    ["64bit"] = new List<string>
                    {
                        "https://github.com/th3w1zard1/PyKotor/releases/download/v{tag}-toolset/HolocronToolset_Mac_PyQt5_x64.zip"
                    }
                },
                ["Linux"] = new Dictionary<string, List<string>>
                {
                    ["32bit"] = new List<string>(),
                    ["64bit"] = new List<string>
                    {
                        "https://github.com/th3w1zard1/PyKotor/releases/download/v{tag}-toolset/HolocronToolset_Linux_PyQt5_x64.zip"
                    }
                },
                ["Windows"] = new Dictionary<string, List<string>>
                {
                    ["32bit"] = new List<string>
                    {
                        "https://github.com/th3w1zard1/PyKotor/releases/download/v{tag}-toolset/HolocronToolset_Windows_PyQt5_x86.zip"
                    },
                    ["64bit"] = new List<string>
                    {
                        "https://github.com/th3w1zard1/PyKotor/releases/download/v{tag}-toolset/HolocronToolset_Windows_PyQt5_x64.zip"
                    }
                }
            },
            ["toolsetBetaDirectLinks"] = new Dictionary<string, Dictionary<string, List<string>>>
            {
                ["Darwin"] = new Dictionary<string, List<string>>
                {
                    ["32bit"] = new List<string>(),
                    ["64bit"] = new List<string>
                    {
                        "https://github.com/th3w1zard1/PyKotor/releases/download/v{tag}-toolset/HolocronToolset_Mac_PyQt5_x64.zip"
                    }
                },
                ["Linux"] = new Dictionary<string, List<string>>
                {
                    ["32bit"] = new List<string>(),
                    ["64bit"] = new List<string>
                    {
                        "https://github.com/th3w1zard1/PyKotor/releases/download/v{tag}-toolset/HolocronToolset_Linux_PyQt5_x64.zip"
                    }
                },
                ["Windows"] = new Dictionary<string, List<string>>
                {
                    ["32bit"] = new List<string>
                    {
                        "https://github.com/th3w1zard1/PyKotor/releases/download/v{tag}-toolset/HolocronToolset_Windows_PyQt5_x86.zip"
                    },
                    ["64bit"] = new List<string>
                    {
                        "https://github.com/th3w1zard1/PyKotor/releases/download/v{tag}-toolset/HolocronToolset_Windows_PyQt5_x64.zip"
                    }
                }
            },
            ["toolsetLatestNotes"] = "Path editor now creates bidirectional links automatically, eliminating manual reciprocal edges and preventing zero-connection points.",
            ["toolsetLatestBetaNotes"] = "Path editor now creates bidirectional links automatically, eliminating manual reciprocal edges and preventing zero-connection points.",
            ["kits"] = new Dictionary<string, object>
            {
                ["Black Vulkar Base"] = new Dictionary<string, object> { ["version"] = 1, ["id"] = "blackvulkar" },
                ["Endar Spire"] = new Dictionary<string, object> { ["version"] = 1, ["id"] = "endarspire" },
                ["Hidden Bek Base"] = new Dictionary<string, object> { ["version"] = 1, ["id"] = "hiddenbek" },
                ["repository"] = "th3w1zard1/ToolsetData",
                ["release_tag"] = "latest"
            },
            ["help"] = new Dictionary<string, object> { ["version"] = 3 }
        };
    }
}
