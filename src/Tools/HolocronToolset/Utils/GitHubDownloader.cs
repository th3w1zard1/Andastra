using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HolocronToolset.Utils
{
    /// <summary>
    /// Utility class for downloading files from GitHub repositories.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/github.py:609-643
    /// </summary>
    public static class GitHubDownloader
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        static GitHubDownloader()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HolocronToolset.NET");
            HttpClient.Timeout = TimeSpan.FromSeconds(180);
        }

        /// <summary>
        /// Downloads a file from a GitHub repository.
        /// Matching PyKotor implementation: download_github_file(url_or_repo, local_path, repo_path, timeout)
        /// </summary>
        /// <param name="owner">GitHub repository owner</param>
        /// <param name="repo">GitHub repository name</param>
        /// <param name="repoPath">Path to the file within the repository</param>
        /// <param name="localPath">Local path where the file should be saved</param>
        /// <param name="timeout">Request timeout in seconds (default: 180)</param>
        public static async Task DownloadGitHubFileAsync(string owner, string repo, string repoPath, string localPath, int timeout = 180)
        {
            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
            {
                throw new ArgumentException("Owner and repo must be specified");
            }

            if (string.IsNullOrEmpty(repoPath))
            {
                throw new ArgumentException("Repository path must be specified");
            }

            if (string.IsNullOrEmpty(localPath))
            {
                throw new ArgumentException("Local path must be specified");
            }

            // Ensure parent directory exists
            string directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Get file info from GitHub API
            string baseUrl = $"https://api.github.com/repos/{owner}/{repo}";
            string apiUrl = $"{baseUrl}/contents/{repoPath.Replace('\\', '/')}";

            try
            {
                // Request file info from GitHub API
                string responseJson = await HttpClient.GetStringAsync(apiUrl);
                JObject fileInfo = JObject.Parse(responseJson);

                string fileType = fileInfo["type"]?.Value<string>();
                if (fileType != "file")
                {
                    throw new InvalidOperationException($"The provided repo_path does not point to a file. Type: {fileType}");
                }

                string downloadUrl = fileInfo["download_url"]?.Value<string>();
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    throw new InvalidOperationException("Failed to get download URL from GitHub API");
                }

                // Download the file
                using (HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl))
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream httpStream = await response.Content.ReadAsStreamAsync())
                    using (FileStream fileStream = File.Create(localPath))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"Failed to download file from GitHub: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error downloading GitHub file: {ex.Message}", ex);
            }
        }
    }
}

