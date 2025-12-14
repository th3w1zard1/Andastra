using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HolocronToolset.NET.Utils
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_github.py:12
    // Original: def fetch_fork_releases(...):
    public static class UpdateGitHub
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_github.py:12-32
        // Original: def fetch_fork_releases(fork_full_name: str, *, include_all: bool = False, include_prerelease: bool = False) -> list[GithubRelease]:
        public static async Task<List<object>> FetchForkReleasesAsync(
            string forkFullName,
            bool includeAll = false,
            bool includePrerelease = false)
        {
            string url = $"https://api.github.com/repos/{forkFullName}/releases";
            try
            {
                var response = await HttpClient.GetStringAsync(url);
                var releasesJson = JArray.Parse(response);
                var releases = new List<object>();

                foreach (var release in releasesJson)
                {
                    bool isDraft = release["draft"]?.Value<bool>() ?? false;
                    bool isPrerelease = release["prerelease"]?.Value<bool>() ?? false;

                    if (includeAll || (!isDraft && (includePrerelease || !isPrerelease)))
                    {
                        releases.Add(release);
                    }
                }

                return releases;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to fetch releases for {forkFullName}: {ex}");
                return new List<object>();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_github.py:34-48
        // Original: def fetch_and_cache_forks() -> dict[str, list[GithubRelease]]:
        public static async Task<Dictionary<string, List<object>>> FetchAndCacheForksAsync()
        {
            var forksCache = new Dictionary<string, List<object>>();
            string forksUrl = "https://api.github.com/repos/th3w1zard1/PyKotor/forks";
            try
            {
                var response = await HttpClient.GetStringAsync(forksUrl);
                var forksJson = JArray.Parse(response);

                foreach (var fork in forksJson)
                {
                    string forkOwnerLogin = fork["owner"]?["login"]?.Value<string>() ?? "";
                    string forkName = fork["name"]?.Value<string>() ?? "";
                    string forkFullName = $"{forkOwnerLogin}/{forkName}";
                    var releases = await FetchForkReleasesAsync(forkFullName, includeAll: true);
                    forksCache[forkFullName] = releases;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to fetch forks: {ex}");
            }

            return forksCache;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_github.py:50-62
        // Original: def filter_releases(releases: list[GithubRelease], *, include_prerelease: bool = False) -> list[GithubRelease]:
        public static List<object> FilterReleases(
            List<object> releases,
            bool includePrerelease = false)
        {
            var filtered = new List<object>();
            foreach (var releaseObj in releases)
            {
                if (releaseObj is JObject release)
                {
                    bool isDraft = release["draft"]?.Value<bool>() ?? false;
                    bool isPrerelease = release["prerelease"]?.Value<bool>() ?? false;
                    string tagName = release["tag_name"]?.Value<string>()?.ToLower() ?? "";

                    if (!isDraft && tagName.Contains("toolset") && (includePrerelease || !isPrerelease))
                    {
                        filtered.Add(release);
                    }
                }
            }

            return filtered;
        }
    }
}
