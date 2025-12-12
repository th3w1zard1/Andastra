// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:29-363
    // Original: public class CompilerUtil
    /// <summary>
    /// Utility class for compiler path resolution.
    /// </summary>
    public static class CompilerUtil
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:32-38
        // Original: private static final String[] COMPILER_NAMES = { ... }
        private static readonly string[] COMPILER_NAMES = {
            "nwnnsscomp.exe",              // Primary - generic name (highest priority)
            "nwnnsscomp_kscript.exe",      // Secondary - KOTOR Scripting Tool
            "nwnnsscomp_ktool.exe"         // KOTOR Tool variant
        };

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:51-75
        // Original: public static File resolveCompilerPath(String folderPath, String filename)
        public static File ResolveCompilerPath(string folderPath, string filename)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrWhiteSpace(folderPath))
            {
                return null;
            }
            if (string.IsNullOrEmpty(filename) || string.IsNullOrWhiteSpace(filename))
            {
                return null;
            }

            folderPath = folderPath.Trim();
            filename = filename.Trim();

            // Normalize folder path (ensure it's a directory path, not a file path)
            File folder = new File(folderPath);
            if (folder.IsFile())
            {
                // If it's a file, use its parent directory
                File parent = folder.GetParentFile();
                if (parent != null)
                {
                    folder = parent;
                }
                else
                {
                    return null;
                }
            }

            return new File(Path.Combine(folder.FullName, filename));
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:89-108
        // Original: public static File getCompilerFromSettings()
        public static File GetCompilerFromSettings()
        {
            // Get folder path and filename from Settings
            string folderPath = Decompiler.settings.GetProperty("nwnnsscomp Folder Path", "");
            string filename = Decompiler.settings.GetProperty("nwnnsscomp Filename", "");

            JavaSystem.@err.Println("DEBUG CompilerUtil.getCompilerFromSettings: folderPath='" + folderPath + "', filename='" + filename + "'");

            // Use shared resolution function
            File compilerFile = ResolveCompilerPath(folderPath, filename);

            if (compilerFile == null)
            {
                JavaSystem.@err.Println("DEBUG CompilerUtil.getCompilerFromSettings: folderPath or filename is empty/invalid");
                return null;
            }

            JavaSystem.@err.Println("DEBUG CompilerUtil.getCompilerFromSettings: compilerFile='" + compilerFile.GetAbsolutePath() + "', exists=" + compilerFile.Exists());

            // Return the file (even if it doesn't exist - caller can check)
            return compilerFile;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:325-352
        // Original: public static File getNCSDecompDirectory()
        public static File GetNCSDecompDirectory()
        {
            try
            {
                // Try to get the location of the assembly
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                if (assembly != null && !string.IsNullOrEmpty(assembly.Location))
                {
                    File assemblyFile = new File(assembly.Location);
                    if (assemblyFile.Exists() && assemblyFile.Directory != null)
                    {
                        return new File(assemblyFile.Directory);
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to user.dir
            }
            // Fallback to user.dir if we can't determine assembly location
            return new File(JavaSystem.GetProperty("user.dir"));
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:360-362
        // Original: public static String[] getCompilerNames()
        public static string[] GetCompilerNames()
        {
            return (string[])COMPILER_NAMES.Clone();
        }
    }
}

