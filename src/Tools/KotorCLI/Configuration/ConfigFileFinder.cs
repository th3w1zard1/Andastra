using System.IO;

namespace KotorCLI.Configuration
{
    /// <summary>
    /// Utility to find kotorcli.cfg configuration file by walking up the directory tree.
    /// </summary>
    public static class ConfigFileFinder
    {
        /// <summary>
        /// Find kotorcli.cfg by walking up the directory tree from the current directory.
        /// </summary>
        public static string FindConfigFile(string startDir = null)
        {
            if (startDir == null)
            {
                startDir = Directory.GetCurrentDirectory();
            }

            DirectoryInfo current = new DirectoryInfo(startDir);
            
            while (true)
            {
                string configPath = Path.Combine(current.FullName, "kotorcli.cfg");
                if (File.Exists(configPath))
                {
                    return configPath;
                }

                DirectoryInfo parent = current.Parent;
                if (parent == null || parent.FullName == current.FullName)
                {
                    // Reached root
                    return null;
                }
                current = parent;
            }
        }
    }
}

