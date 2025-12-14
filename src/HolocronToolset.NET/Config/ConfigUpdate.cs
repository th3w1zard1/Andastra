using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HolocronToolset.NET.Config;
using Newtonsoft.Json;

namespace HolocronToolset.NET.Config
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_update.py:45
    // Original: def fetch_update_info(update_link: str, timeout: int = 15) -> dict[str, Any]:
    public static class ConfigUpdate
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_update.py:45-61
        // Original: def fetch_update_info(update_link: str, timeout: int = 15) -> dict[str, Any]:
        public static async Task<Dictionary<string, object>> FetchUpdateInfoAsync(string updateLink, int timeoutSeconds = 15)
        {
            try
            {
                HttpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                string response = await HttpClient.GetStringAsync(updateLink);
                var fileData = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                return fileData ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_update.py:64-123
        // Original: def get_remote_toolset_update_info(*, use_beta_channel: bool = False, silent: bool = False):
        public static async Task<Dictionary<string, object>> GetRemoteToolsetUpdateInfoAsync(bool useBetaChannel = false, bool silent = false)
        {
            string updateInfoLink = useBetaChannel
                ? ConfigInfo.LocalProgramInfo["updateBetaInfoLink"].ToString()
                : ConfigInfo.LocalProgramInfo["updateInfoLink"].ToString();

            try
            {
                int timeout = silent ? 2 : 10;
                var fileData = await FetchUpdateInfoAsync(updateInfoLink, timeout);

                if (fileData.TryGetValue("content", out object contentObj) && contentObj is string base64Content)
                {
                    byte[] decodedBytes = Convert.FromBase64String(base64Content);
                    string decodedContent = System.Text.Encoding.UTF8.GetString(decodedBytes);

                    // Extract JSON from markers
                    var match = Regex.Match(decodedContent, @"<---JSON_START--->\s*#\s*(.*?)\s*#\s*<---JSON_END--->", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        string jsonStr = match.Groups[1].Value;
                        // Clean trailing commas
                        jsonStr = Regex.Replace(jsonStr, @",\s*([}\]])", "$1");
                        var remoteInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonStr);
                        if (remoteInfo != null)
                        {
                            return remoteInfo;
                        }
                    }
                }
            }
            catch
            {
                // Fall through to return local info
            }

            return ConfigInfo.LocalProgramInfo;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_update.py:126-141
        // Original: def is_remote_version_newer(local_version: str, remote_version: str) -> bool | None:
        public static bool? IsRemoteVersionNewer(string localVersion, string remoteVersion)
        {
            try
            {
                var local = ParseVersion(localVersion);
                var remote = ParseVersion(remoteVersion);

                if (local == null || remote == null)
                {
                    return null;
                }

                return CompareVersions(remote, local) > 0;
            }
            catch
            {
                return null;
            }
        }

        private static int[] ParseVersion(string version)
        {
            var parts = version.Split('.');
            var result = new List<int>();
            foreach (string part in parts)
            {
                if (int.TryParse(part, out int num))
                {
                    result.Add(num);
                }
                else
                {
                    return null;
                }
            }
            return result.ToArray();
        }

        private static int CompareVersions(int[] v1, int[] v2)
        {
            int maxLength = Math.Max(v1.Length, v2.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int num1 = i < v1.Length ? v1[i] : 0;
                int num2 = i < v2.Length ? v2[i] : 0;
                if (num1 != num2)
                {
                    return num1.CompareTo(num2);
                }
            }
            return 0;
        }
    }
}
