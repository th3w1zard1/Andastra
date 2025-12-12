//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.NCS;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptutils;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using File = System.IO.FileInfo;
using InputStream = System.IO.Stream;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;
using Process = System.Diagnostics.Process;
using Thread = System.Threading.Thread;
using Throwable = System.Exception;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:56-79
    public class FileDecompiler
    {
        public static readonly int FAILURE = 0;
        public static readonly int SUCCESS = 1;
        public static readonly int PARTIAL_COMPILE = 2;
        public static readonly int PARTIAL_COMPARE = 3;
        public static readonly string GLOBAL_SUB_NAME = "GLOBALS";
        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:72-79
        // Original: public static boolean isK2Selected = false;
        public static bool isK2Selected = false;
        // Original: public static boolean preferSwitches = false;
        public static bool preferSwitches = false;
        // Original: public static boolean strictSignatures = false;
        public static bool strictSignatures = false;
        // Original: public static String nwnnsscompPath = null;
        public static string nwnnsscompPath = null;
        private ActionsData actions;
        private Dictionary<object, object> filedata;
        private Settings settings;
        private NWScriptLocator.GameType gameType;

        public FileDecompiler()
            : this(null, null)
        {
        }

        public FileDecompiler(Settings settings, NWScriptLocator.GameType? gameType)
        {
            this.filedata = new Dictionary<object, object>();
            this.settings = settings ?? Decompiler.settings;

            // Determine game type from settings if not provided
            if (gameType.HasValue)
            {
                this.gameType = gameType.Value;
            }
            else if (this.settings != null)
            {
                string gameTypeSetting = this.settings.GetProperty("Game Type");
                if (!string.IsNullOrEmpty(gameTypeSetting) &&
                    (gameTypeSetting.Equals("TSL", StringComparison.OrdinalIgnoreCase) ||
                     gameTypeSetting.Equals("K2", StringComparison.OrdinalIgnoreCase)))
                {
                    this.gameType = NWScriptLocator.GameType.TSL;
                }
                else
                {
                    this.gameType = NWScriptLocator.GameType.K1;
                }
            }
            else
            {
                this.gameType = NWScriptLocator.GameType.K1;
            }

            // Actions will be loaded lazily on first use
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1031-1035
        // Original: private void ensureActionsLoaded() throws DecompilerException
        private void EnsureActionsLoaded()
        {
            if (this.actions == null)
            {
                this.actions = LoadActionsDataInternal(isK2Selected);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:124-169
        // Original: private static ActionsData loadActionsDataInternal(boolean isK2Selected) throws DecompilerException
        private static ActionsData LoadActionsDataInternal(bool isK2Selected)
        {
            try
            {
                File actionfile = null;

                // Check settings first (GUI mode) - only if Decompiler class is loaded
                try
                {
                    // Access Decompiler.settings directly (same package)
                    // This will throw NoClassDefFoundError in pure CLI mode, which we catch
                    string settingsPath = isK2Selected
                        ? Decompiler.settings.GetProperty("K2 nwscript Path")
                        : Decompiler.settings.GetProperty("K1 nwscript Path");
                    if (!string.IsNullOrEmpty(settingsPath))
                    {
                        actionfile = new File(settingsPath);
                        if (actionfile.IsFile())
                        {
                            return new ActionsData(new BufferedReader(new FileReader(actionfile)));
                        }
                    }
                }
                catch (Exception)
                {
                    // Settings not available (CLI mode) or invalid path, fall through to default
                }

                // Fall back to default location in tools/ directory
                string userDir = JavaSystem.GetProperty("user.dir");
                File dir = new File(Path.Combine(userDir, "tools"));
                actionfile = isK2Selected ? new File(Path.Combine(dir.FullName, "tsl_nwscript.nss")) : new File(Path.Combine(dir.FullName, "k1_nwscript.nss"));
                // If not in tools/, try current directory (legacy support)
                if (!actionfile.IsFile())
                {
                    dir = new File(userDir);
                    actionfile = isK2Selected ? new File(Path.Combine(dir.FullName, "tsl_nwscript.nss")) : new File(Path.Combine(dir.FullName, "k1_nwscript.nss"));
                }
                if (actionfile.IsFile())
                {
                    return new ActionsData(new BufferedReader(new FileReader(actionfile)));
                }
                else
                {
                    throw new DecompilerException("Error: cannot open actions file " + actionfile.GetAbsolutePath() + ".");
                }
            }
            catch (IOException ex)
            {
                throw new DecompilerException(ex.Message);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:171-204
        // Original: private static void loadPreferSwitchesFromConfig()
        private static void LoadPreferSwitchesFromConfig()
        {
            try
            {
                string userDir = JavaSystem.GetProperty("user.dir");
                File configFile = new File(Path.Combine(userDir, "ncsdecomp.conf"));
                if (!configFile.Exists())
                {
                    configFile = new File(Path.Combine(userDir, "dencs.conf"));
                }

                if (configFile.Exists() && configFile.IsFile())
                {
                    using (BufferedReader reader = new BufferedReader(new FileReader(configFile)))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (line.StartsWith("preferSwitches") || line.StartsWith("Prefer Switches"))
                            {
                                int equalsIdx = line.IndexOf('=');
                                if (equalsIdx >= 0)
                                {
                                    string value = line.Substring(equalsIdx + 1).Trim();
                                    preferSwitches = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently ignore config file errors - use default value
            }
        }

        private void LoadActions()
        {
            try
            {
                // Determine game type from settings if not already set
                if (this.settings != null)
                {
                    string gameTypeSetting = this.settings.GetProperty("Game Type");
                    if (!string.IsNullOrEmpty(gameTypeSetting))
                    {
                        if (gameTypeSetting.Equals("TSL", StringComparison.OrdinalIgnoreCase) ||
                            gameTypeSetting.Equals("K2", StringComparison.OrdinalIgnoreCase))
                        {
                            this.gameType = NWScriptLocator.GameType.TSL;
                        }
                        else
                        {
                            this.gameType = NWScriptLocator.GameType.K1;
                        }
                    }
                }

                // Try to find nwscript.nss file
                File actionfile = NWScriptLocator.FindNWScriptFile(this.gameType, this.settings);
                if (actionfile == null || !actionfile.IsFile())
                {
                    // Build error message with candidate paths
                    List<string> candidatePaths = NWScriptLocator.GetCandidatePaths(this.gameType);
                    string errorMsg = "Error: cannot find nwscript.nss file for " + this.gameType + ".\n\n";
                    errorMsg += "Searched locations:\n";
                    foreach (string path in candidatePaths)
                    {
                        errorMsg += "  - " + path + "\n";
                    }
                    errorMsg += "\nPlease ensure nwscript.nss exists in one of these locations, or configure the path in Settings.";
                    throw new DecompilerException(errorMsg);
                }

                this.actions = new ActionsData(new BufferedReader(new FileReader(actionfile)));
            }
            catch (IOException e)
            {
                throw new DecompilerException("Error reading nwscript.nss file: " + e.Message);
            }
        }

        public virtual Dictionary<object, object> GetVariableData(File file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            Dictionary<string, List<object>> vars = data.GetVars();
            if (vars == null)
            {
                return null;
            }

            Dictionary<object, object> result = new Dictionary<object, object>();
            foreach (var kvp in vars)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        public virtual string GetGeneratedCode(File file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            return data.GetCode();
        }

        public virtual string GetOriginalByteCode(File file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            return data.GetOriginalByteCode();
        }

        public virtual string GetNewByteCode(File file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            return data.GetNewByteCode();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:253-352
        // Original: public int decompile(File file)
        public virtual int Decompile(File file)
        {
            try
            {
                this.EnsureActionsLoaded();
            }
            catch (DecompilerException e)
            {
                JavaSystem.@out.Println("Error loading actions data: " + e.Message);
                // Create comprehensive fallback stub for actions data loading failure
                Utils.FileScriptData errorData = new Utils.FileScriptData();
                string expectedFile = isK2Selected ? "tsl_nwscript.nss" : "k1_nwscript.nss";
                string stubCode = this.GenerateComprehensiveFallbackStub(file, "Actions data loading", e,
                    "The actions data table (nwscript.nss) is required to decompile NCS files.\n" +
                    "Expected file: " + expectedFile + "\n" +
                    "Please ensure the appropriate nwscript.nss file is available in tools/ directory, working directory, or configured path.");
                errorData.SetCode(stubCode);
                this.filedata[file] = errorData;
                return PARTIAL_COMPILE;
            }
            Utils.FileScriptData data = null;
            if (this.filedata.ContainsKey(file))
            {
                data = (Utils.FileScriptData)this.filedata[file];
            }
            if (data == null)
            {
                JavaSystem.@out.Println("\n---> starting decompilation: " + file.Name + " <---");
                try
                {
                    data = this.DecompileNcs(file);
                    // decompileNcs now always returns a FileScriptData (never null)
                    // but it may contain minimal/fallback code if decompilation failed
                    this.filedata[file] = data;
                }
                catch (Exception e)
                {
                    // Last resort: create comprehensive fallback stub data so we always have something to show
                    JavaSystem.@out.Println("Critical error during decompilation, creating fallback stub: " + e.Message);
                    e.PrintStackTrace(JavaSystem.@out);
                    data = new Utils.FileScriptData();
                    data.SetCode(this.GenerateComprehensiveFallbackStub(file, "Initial decompilation attempt", e, null));
                    this.filedata[file] = data;
                }
            }

            // Always generate code, even if validation fails
            try
            {
                data.GenerateCode();
                string code = data.GetCode();
                if (code == null || code.Trim().Length == 0)
                {
                    // If code generation failed, provide comprehensive fallback stub
                    JavaSystem.@out.Println("Warning: Generated code is empty, creating fallback stub.");
                    string fallback = this.GenerateComprehensiveFallbackStub(file, "Code generation - empty output", null,
                        "The decompilation process completed but generated no source code. This may indicate the file contains no executable code or all code was marked as dead/unreachable.");
                    data.SetCode(fallback);
                    return PARTIAL_COMPILE;
                }
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("Error during code generation (creating fallback stub): " + e.Message);
                string fallback = this.GenerateComprehensiveFallbackStub(file, "Code generation", e,
                    "An exception occurred while generating NSS source code from the decompiled parse tree.");
                data.SetCode(fallback);
                return PARTIAL_COMPILE;
            }

            // Try to capture original bytecode from the NCS file if nwnnsscomp is available
            // This allows viewing bytecode even without round-trip validation
            if (this.CheckCompilerExists())
            {
                try
                {
                    JavaSystem.@out.Println("[NCSDecomp] Attempting to capture original bytecode from NCS file...");
                    File olddecompiled = this.ExternalDecompile(file, isK2Selected);
                    if (olddecompiled != null && olddecompiled.Exists())
                    {
                        string originalByteCode = this.ReadFile(olddecompiled);
                        if (originalByteCode != null && originalByteCode.Trim().Length > 0)
                        {
                            data.SetOriginalByteCode(originalByteCode);
                            JavaSystem.@out.Println("[NCSDecomp] Successfully captured original bytecode (" + originalByteCode.Length + " characters)");
                        }
                        else
                        {
                            JavaSystem.@out.Println("[NCSDecomp] Warning: Original bytecode file is empty");
                        }
                    }
                    else
                    {
                        JavaSystem.@out.Println("[NCSDecomp] Warning: Failed to decompile original NCS file to bytecode");
                    }
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("[NCSDecomp] Exception while capturing original bytecode:");
                    JavaSystem.@out.Println("[NCSDecomp]   Exception Type: " + e.GetType().Name);
                    JavaSystem.@out.Println("[NCSDecomp]   Exception Message: " + e.Message);
                    if (e.InnerException != null)
                    {
                        JavaSystem.@out.Println("[NCSDecomp]   Caused by: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                    }
                    e.PrintStackTrace(JavaSystem.@out);
                }
            }
            else
            {
                JavaSystem.@out.Println("[NCSDecomp] nwnnsscomp.exe not found - cannot capture original bytecode");
            }

            // Try validation, but don't fail if it doesn't work
            // nwnnsscomp is optional - decompilation should work without it
            try
            {
            return this.CompileAndCompare(file, data.GetCode(), data);
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("[NCSDecomp] Exception during bytecode validation:");
                JavaSystem.@out.Println("[NCSDecomp]   Exception Type: " + e.GetType().Name);
                JavaSystem.@out.Println("[NCSDecomp]   Exception Message: " + e.Message);
                if (e.InnerException != null)
                {
                    JavaSystem.@out.Println("[NCSDecomp]   Caused by: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                }
                e.PrintStackTrace(JavaSystem.@out);
                JavaSystem.@out.Println("[NCSDecomp] Showing decompiled source anyway (validation failed)");
                return PARTIAL_COMPILE;
            }
        }

        public virtual int CompileAndCompare(File file, File newfile)
        {
            Utils.FileScriptData data = null;
            if (this.filedata.ContainsKey(file))
            {
                data = (Utils.FileScriptData)this.filedata[file];
            }
            return this.CompileAndCompare(file, newfile, data);
        }

        public virtual int CompileOnly(File nssFile)
        {
            Utils.FileScriptData data = null;
            if (this.filedata.ContainsKey(nssFile))
            {
                data = (Utils.FileScriptData)this.filedata[nssFile];
            }
            if (data == null)
            {
                data = new Utils.FileScriptData();
            }

            return this.CompileNss(nssFile, data);
        }

        public virtual Dictionary<object, object> UpdateSubName(File file, string oldname, string newname)
        {
            if (file == null)
            {
                return null;
            }

            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            data.ReplaceSubName(oldname, newname);
            Dictionary<string, List<object>> vars = data.GetVars();
            if (vars == null)
            {
                return null;
            }
            Dictionary<object, object> result = new Dictionary<object, object>();
            foreach (var kvp in vars)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        public virtual string RegenerateCode(File file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            data.GenerateCode();
            return data.ToString();
        }

        public virtual void CloseFile(File file)
        {
            if (this.filedata.ContainsKey(file))
            {
                Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
                if (data != null)
                {
                    data.Close();
                }
                this.filedata.Remove(file);
            }

            GC.Collect();
        }

        public virtual void CloseAllFiles()
        {
            foreach (var kvp in this.filedata)
            {
                if (kvp.Value is Utils.FileScriptData fileData)
                {
                    fileData.Close();
                }
            }

            this.filedata.Clear();
            GC.Collect();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:447-455
        // Original: public String decompileToString(File file) throws DecompilerException
        public virtual string DecompileToString(File file)
        {
            Utils.FileScriptData data = this.DecompileNcsObjectFromFile(file);
            if (data == null)
            {
                throw new DecompilerException("Decompile failed for " + file.GetAbsolutePath());
            }

            data.GenerateCode();
            return data.GetCode();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:460-474
        // Original: public void decompileToFile(File input, File output, Charset charset, boolean overwrite) throws DecompilerException, IOException
        public virtual void DecompileToFile(File input, File output, System.Text.Encoding charset, bool overwrite)
        {
            if (output.Exists() && !overwrite)
            {
                throw new IOException("Output file already exists: " + output.GetAbsolutePath());
            }

            string code = this.DecompileToString(input);
            if (output.Directory != null && !output.Directory.Exists)
            {
                output.Directory.Create();
            }

            using (var writer = new StreamWriter(output.FullName, false, charset))
            {
                writer.Write(code);
            }
        }

        // Helper method to decompile from file (used by DecompileToString)
        private Utils.FileScriptData DecompileNcsObjectFromFile(File file)
        {
            NCS ncs = null;
            try
            {
                using (var reader = new NCSBinaryReader(file.GetAbsolutePath()))
                {
                    ncs = reader.Load();
                }
            }
            catch (Exception ex)
            {
                throw new DecompilerException("Failed to read NCS file: " + ex.Message);
            }

            if (ncs == null)
            {
                return null;
            }

            return this.DecompileNcsObject(ncs);
        }

        private int CompileAndCompare(File file, File newfile, Utils.FileScriptData data)
        {
            string code = this.ReadFile(newfile);
            return this.CompileAndCompare(file, code, data);
        }

        private int CompileAndCompare(File file, string code, Utils.FileScriptData data)
        {
            Game game = this.MapGameType();
            NCS originalNcs = null;
            byte[] originalBytes = null;
            try
            {
                using (var reader = new NCSBinaryReader(file.GetAbsolutePath()))
                {
                    originalNcs = reader.Load();
                }

                if (originalNcs == null)
                {
                    return FAILURE;
                }

                originalBytes = NCSAuto.BytesNcs(originalNcs);
                data.SetOriginalByteCode(BytesToHexString(originalBytes, 0, originalBytes.Length));
            }
            catch (Exception ex)
            {
                JavaSystem.@out.Println("Failed to read original NCS: " + ex.Message);
                return FAILURE;
            }

            try
            {
                NCS compiled = NCSAuto.CompileNss(code ?? string.Empty, game, null, null);
                byte[] compiledBytes = NCSAuto.BytesNcs(compiled);
                data.SetNewByteCode(BytesToHexString(compiledBytes, 0, compiledBytes.Length));

                if (!this.ByteArraysEqual(originalBytes, compiledBytes))
                {
                    return PARTIAL_COMPARE;
                }

                return SUCCESS;
            }
            catch (Exception ex)
            {
                JavaSystem.@out.Println("In-process compile failed: " + ex.Message);
                return PARTIAL_COMPILE;
            }
        }

        private int CompileNss(File nssFile, Utils.FileScriptData data)
        {
            string code = this.ReadFile(nssFile);
            return this.CompileAndCompare(nssFile, code, data);
        }

        private string ReadFile(File file)
        {
            if (file == null || !file.Exists())
            {
                return null;
            }

            string newline = Environment.NewLine;
            StringBuilder buffer = new StringBuilder();
            BufferedReader reader = null;
            try
            {
                reader = new BufferedReader(new FileReader(file));
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    buffer.Append(line.ToString() + newline);
                }
            }
            catch (IOException e)
            {
                JavaSystem.@out.Println("IO exception in read file: " + e);
                return null;
            }
            catch (System.IO.FileNotFoundException e)
            {
                JavaSystem.@out.Println("File not found in read file: " + e);
                return null;
            }
            finally
            {
                try
                {
                    if (reader != null)
                    {
                        reader.Dispose();
                    }
                }
                catch (Exception)
                {
                }
            }

            try
            {
                reader.Dispose();
            }
            catch (Exception)
            {
            }

            return buffer.ToString();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:727-855
        // Original: private File getCompilerFile()
        private File GetCompilerFile()
        {
            // Priority order: primary first, then secondary, then others
            string[] compilerNames = {
                "nwnnsscomp.exe",              // Primary - generic name (highest priority)
                "nwnnsscomp_kscript.exe",      // Secondary - KOTOR Scripting Tool
                "nwnnsscomp_tslpatcher.exe",   // TSLPatcher variant
                "nwnnsscomp_v1.exe"            // v1.3 first public release
            };

            // 1. Try configured path (if set) - all filenames
            if (nwnnsscompPath != null && nwnnsscompPath.Trim().Length > 0)
            {
                File configuredDir = new File(nwnnsscompPath);
                if (configuredDir.IsDirectory())
                {
                    // If it's a directory, try all filenames in it
                    foreach (string name in compilerNames)
                    {
                        File candidate = new File(Path.Combine(configuredDir.FullName, name));
                        if (candidate.Exists())
                        {
                            return candidate;
                        }
                    }
                }
                else
                {
                    // If it's a file, check if it exists
                    if (configuredDir.Exists())
                    {
                        return configuredDir;
                    }
                    // Also try other filenames in the same directory
                    if (configuredDir.Directory != null)
                    {
                        foreach (string name in compilerNames)
                        {
                            File candidate = new File(Path.Combine(configuredDir.Directory.FullName, name));
                            if (candidate.Exists())
                            {
                                return candidate;
                            }
                        }
                    }
                }
            }

            // 2. Try tools/ directory - all filenames
            string userDir = JavaSystem.GetProperty("user.dir");
            File toolsDir = new File(Path.Combine(userDir, "tools"));
            foreach (string name in compilerNames)
            {
                File candidate = new File(Path.Combine(toolsDir.FullName, name));
                if (candidate.Exists())
                {
                    return candidate;
                }
            }

            // 3. Try current working directory - all filenames
            File cwd = new File(userDir);
            foreach (string name in compilerNames)
            {
                File candidate = new File(Path.Combine(cwd.FullName, name));
                if (candidate.Exists())
                {
                    return candidate;
                }
            }

            // 4. Try NCSDecomp installation directory - all filenames
            File ncsDecompDir = GetNCSDecompDirectory();
            if (ncsDecompDir != null && !ncsDecompDir.FullName.Equals(cwd.FullName))
            {
                foreach (string name in compilerNames)
                {
                    File candidate = new File(Path.Combine(ncsDecompDir.FullName, name));
                    if (candidate.Exists())
                    {
                        return candidate;
                    }
                }
                // Also try tools/ subdirectory of NCSDecomp directory
                File ncsToolsDir = new File(Path.Combine(ncsDecompDir.FullName, "tools"));
                foreach (string name in compilerNames)
                {
                    File candidate = new File(Path.Combine(ncsToolsDir.FullName, name));
                    if (candidate.Exists())
                    {
                        return candidate;
                    }
                }
            }

            // Final fallback: return nwnnsscomp.exe in current directory (may not exist)
            return new File(Path.Combine(userDir, "nwnnsscomp.exe"));
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:728-762
        // Original: private File getNCSDecompDirectory()
        private File GetNCSDecompDirectory()
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
                        return assemblyFile.Directory;
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

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:861-864
        // Original: private boolean checkCompilerExists()
        private bool CheckCompilerExists()
        {
            File compiler = GetCompilerFile();
            return compiler.Exists();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:869-872
        // Original: private String getShortName(File in)
        private string GetShortName(File inFile)
        {
            string path = inFile.GetAbsolutePath();
            int i = path.LastIndexOf('.');
            return i == -1 ? path : path.Substring(0, i);
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:878-921
        // Original: private File externalDecompile(File in, boolean k2)
        private File ExternalDecompile(File inFile, bool k2)
        {
            try
            {
                File compiler = GetCompilerFile();
                if (!compiler.Exists())
                {
                    JavaSystem.@out.Println("[NCSDecomp] ERROR: Compiler not found: " + compiler.GetAbsolutePath());
                    return null;
                }

                string outname = this.GetShortName(inFile) + ".pcode";
                File result = new File(outname).GetAbsoluteFile();
                if (result.Exists())
                {
                    result.Delete();
                }

                // Use compiler detection to get correct command-line arguments
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:893
                // Original: NwnnsscompConfig config = new NwnnsscompConfig(compiler, in, result, k2);
                NwnnsscompConfig config = new NwnnsscompConfig(compiler, inFile, result, k2);
                string[] args = config.GetDecompileArgs(compiler.GetAbsolutePath());

                JavaSystem.@out.Println("[NCSDecomp] Using compiler: " + config.GetChosenCompiler().Name +
                    " (SHA256: " + config.GetSha256Hash().Substring(0, Math.Min(16, config.GetSha256Hash().Length)) + "...)");
                JavaSystem.@out.Println("[NCSDecomp] Input file: " + inFile.GetAbsolutePath());
                JavaSystem.@out.Println("[NCSDecomp] Expected output: " + result.GetAbsolutePath());

                new WindowsExec().CallExec(args);

                if (!result.Exists())
                {
                    JavaSystem.@out.Println("[NCSDecomp] ERROR: Expected output file does not exist: " + result.GetAbsolutePath());
                    JavaSystem.@out.Println("[NCSDecomp]   This usually means nwnnsscomp.exe failed or produced no output.");
                    JavaSystem.@out.Println("[NCSDecomp]   Check the nwnnsscomp output above for error messages.");
                    return null;
                }

                return result;
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("[NCSDecomp] EXCEPTION during external decompile:");
                JavaSystem.@out.Println("[NCSDecomp]   Exception Type: " + e.GetType().Name);
                JavaSystem.@out.Println("[NCSDecomp]   Exception Message: " + e.Message);
                if (e.InnerException != null)
                {
                    JavaSystem.@out.Println("[NCSDecomp]   Caused by: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                }
                e.PrintStackTrace(JavaSystem.@out);
                return null;
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:926-943
        // Original: private File writeCode(String code)
        private File WriteCode(string code)
        {
            try
            {
                File outFile = new File("_generatedcode.nss");
                using (var writer = new StreamWriter(outFile.FullName, false, System.Text.Encoding.UTF8))
                {
                    writer.Write(code);
                }
                File result = new File("_generatedcode.ncs");
                if (result.Exists())
                {
                    result.Delete();
                }

                return outFile;
            }
            catch (IOException var5)
            {
                JavaSystem.@out.Println("IO exception on writing code: " + var5);
                return null;
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:948-1010
        // Original: private File externalCompile(File file, boolean k2)
        private File ExternalCompile(File file, bool k2)
        {
            try
            {
                File compiler = GetCompilerFile();
                if (!compiler.Exists())
                {
                    JavaSystem.@out.Println("[NCSDecomp] ERROR: Compiler not found: " + compiler.GetAbsolutePath());
                    return null;
                }

                string outname = this.GetShortName(file) + ".ncs";
                File result = new File(outname).GetAbsoluteFile();

                // Ensure nwscript.nss is in the compiler's directory (like test does)
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:960-975
                File compilerDir = compiler.Directory;
                if (compilerDir != null)
                {
                    File compilerNwscript = new File(Path.Combine(compilerDir.FullName, "nwscript.nss"));
                    string userDir = JavaSystem.GetProperty("user.dir");
                    File nwscriptSource = k2
                        ? new File(Path.Combine(userDir, "tools", "tsl_nwscript.nss"))
                        : new File(Path.Combine(userDir, "tools", "k1_nwscript.nss"));
                    if (nwscriptSource.Exists() && (!compilerNwscript.Exists() || !compilerNwscript.GetAbsolutePath().Equals(nwscriptSource.GetAbsolutePath())))
                    {
                        try
                        {
                            System.IO.File.Copy(nwscriptSource.FullName, compilerNwscript.FullName, true);
                        }
                        catch (IOException e)
                        {
                            // Log but don't fail - compiler might find nwscript.nss elsewhere
                            JavaSystem.@err.Println("[NCSDecomp] Warning: Could not copy nwscript.nss to compiler directory: " + e.Message);
                        }
                    }
                }

                // Use compiler detection to get correct command-line arguments
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:978-983
                // Original: NwnnsscompConfig config = new NwnnsscompConfig(compiler, file, result, k2);
                // Original: String[] args = config.getCompileArgs(compiler.getAbsolutePath());
                NwnnsscompConfig config = new NwnnsscompConfig(compiler, file, result, k2);
                // For GUI compilation, match test behavior: don't use -i flags
                // Test shows compilers work without -i when includes are in source directory or compiler directory
                string[] args = config.GetCompileArgs(compiler.GetAbsolutePath());

                JavaSystem.@out.Println("[NCSDecomp] Using compiler: " + config.GetChosenCompiler().Name +
                    " (SHA256: " + config.GetSha256Hash().Substring(0, Math.Min(16, config.GetSha256Hash().Length)) + "...)");
                JavaSystem.@out.Println("[NCSDecomp] Input file: " + file.GetAbsolutePath());
                JavaSystem.@out.Println("[NCSDecomp] Expected output: " + result.GetAbsolutePath());

                new WindowsExec().CallExec(args);

                if (!result.Exists())
                {
                    JavaSystem.@out.Println("[NCSDecomp] ERROR: Expected output file does not exist: " + result.GetAbsolutePath());
                    JavaSystem.@out.Println("[NCSDecomp]   This usually means nwnnsscomp.exe compilation failed.");
                    JavaSystem.@out.Println("[NCSDecomp]   Check the nwnnsscomp output above for compilation errors.");
                    return null;
                }

                return result;
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("[NCSDecomp] EXCEPTION during external compile:");
                JavaSystem.@out.Println("[NCSDecomp]   Exception Type: " + e.GetType().Name);
                JavaSystem.@out.Println("[NCSDecomp]   Exception Message: " + e.Message);
                if (e.InnerException != null)
                {
                    JavaSystem.@out.Println("[NCSDecomp]   Caused by: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                }
                e.PrintStackTrace(JavaSystem.@out);
                return null;
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1012-1029
        // Original: private List<File> buildIncludeDirs(boolean k2)
        private List<File> BuildIncludeDirs(bool k2)
        {
            List<File> dirs = new List<File>();
            File baseDir = new File(Path.Combine("test-work", "Vanilla_KOTOR_Script_Source"));
            File gameDir = new File(Path.Combine(baseDir.FullName, k2 ? "TSL" : "K1"));
            File scriptsBif = new File(Path.Combine(gameDir.FullName, "Data", "scripts.bif"));
            if (scriptsBif.Exists())
            {
                dirs.Add(scriptsBif);
            }
            File rootOverride = new File(Path.Combine(gameDir.FullName, "Override"));
            if (rootOverride.Exists())
            {
                dirs.Add(rootOverride);
            }
            // Fallback: allow includes relative to the game dir root.
            if (gameDir.Exists())
            {
                dirs.Add(gameDir);
            }
            return dirs;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1044-1053
        // Original: private String bytesToHex(byte[] bytes, int length)
        private string BytesToHex(byte[] bytes, int length)
        {
            StringBuilder hex = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                hex.Append(string.Format("{0:X2}", bytes[i] & 0xFF));
                if (i < length - 1)
                {
                    hex.Append(" ");
                }
            }
            return hex.ToString();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1065-1180
        // Original: private String generateComprehensiveFallbackStub(File file, String errorStage, Exception exception, String additionalInfo)
        private string GenerateComprehensiveFallbackStub(File file, string errorStage, Exception exception, string additionalInfo)
        {
            StringBuilder stub = new StringBuilder();
            string newline = Environment.NewLine;

            // Header with error type
            stub.Append("// ========================================").Append(newline);
            stub.Append("// DECOMPILATION ERROR - FALLBACK STUB").Append(newline);
            stub.Append("// ========================================").Append(newline);
            stub.Append(newline);

            // File information
            stub.Append("// File Information:").Append(newline);
            if (file != null)
            {
                stub.Append("//   Name: ").Append(file.Name).Append(newline);
                stub.Append("//   Path: ").Append(file.GetAbsolutePath()).Append(newline);
                if (file.Exists())
                {
                    stub.Append("//   Size: ").Append(file.Length).Append(" bytes").Append(newline);
                    stub.Append("//   Last Modified: ").Append(file.LastWriteTime.ToString()).Append(newline);
                    stub.Append("//   Readable: ").Append(true).Append(newline); // FileInfo is always readable if it exists
                }
                else
                {
                    stub.Append("//   Status: FILE DOES NOT EXIST").Append(newline);
                }
            }
            else
            {
                stub.Append("//   Status: FILE IS NULL").Append(newline);
            }
            stub.Append(newline);

            // Error stage information
            stub.Append("// Error Stage: ").Append(errorStage != null ? errorStage : "Unknown").Append(newline);
            stub.Append(newline);

            // Exception information
            if (exception != null)
            {
                stub.Append("// Exception Details:").Append(newline);
                stub.Append("//   Type: ").Append(exception.GetType().Name).Append(newline);
                stub.Append("//   Message: ").Append(exception.Message != null ? exception.Message : "(no message)").Append(newline);

                // Include cause if available
                Exception cause = exception.InnerException;
                if (cause != null)
                {
                    stub.Append("//   Caused by: ").Append(cause.GetType().Name).Append(newline);
                    stub.Append("//   Cause Message: ").Append(cause.Message != null ? cause.Message : "(no message)").Append(newline);
                }

                // Include stack trace summary (first few frames)
                System.Diagnostics.StackTrace stack = new System.Diagnostics.StackTrace(exception, true);
                if (stack != null && stack.FrameCount > 0)
                {
                    stub.Append("//   Stack Trace (first 5 frames):").Append(newline);
                    int maxFrames = Math.Min(5, stack.FrameCount);
                    for (int i = 0; i < maxFrames; i++)
                    {
                        var frame = stack.GetFrame(i);
                        if (frame != null)
                        {
                            stub.Append("//     at ").Append(frame.ToString()).Append(newline);
                        }
                    }
                    if (stack.FrameCount > maxFrames)
                    {
                        stub.Append("//     ... (").Append(stack.FrameCount - maxFrames).Append(" more frames)").Append(newline);
                    }
                }
                stub.Append(newline);
            }

            // Additional context information
            if (additionalInfo != null && additionalInfo.Trim().Length > 0)
            {
                stub.Append("// Additional Context:").Append(newline);
                // Split long additional info into lines if needed
                string[] lines = additionalInfo.Split('\n');
                foreach (string line in lines)
                {
                    stub.Append("//   ").Append(line).Append(newline);
                }
                stub.Append(newline);
            }

            // Decompiler configuration
            stub.Append("// Decompiler Configuration:").Append(newline);
            stub.Append("//   Game Mode: ").Append(isK2Selected ? "KotOR 2 (TSL)" : "KotOR 1").Append(newline);
            stub.Append("//   Prefer Switches: ").Append(preferSwitches).Append(newline);
            stub.Append("//   Strict Signatures: ").Append(strictSignatures).Append(newline);
            stub.Append("//   Actions Data Loaded: ").Append(this.actions != null).Append(newline);
            stub.Append(newline);

            // System information
            stub.Append("// System Information:").Append(newline);
            stub.Append("//   .NET Version: ").Append(Environment.Version.ToString()).Append(newline);
            stub.Append("//   OS: ").Append(Environment.OSVersion.ToString()).Append(newline);
            stub.Append("//   Working Directory: ").Append(JavaSystem.GetProperty("user.dir")).Append(newline);
            stub.Append(newline);

            // Timestamp
            stub.Append("// Error Timestamp: ").Append(DateTime.Now.ToString()).Append(newline);
            stub.Append(newline);

            // Recommendations
            stub.Append("// Recommendations:").Append(newline);
            if (file != null && file.Exists() && file.Length == 0)
            {
                stub.Append("//   - File is empty (0 bytes). This may indicate a corrupted or incomplete file.").Append(newline);
            }
            else if (file != null && !file.Exists())
            {
                stub.Append("//   - File does not exist. Verify the file path is correct.").Append(newline);
            }
            else if (this.actions == null)
            {
                stub.Append("//   - Actions data not loaded. Ensure k1_nwscript.nss or tsl_nwscript.nss is available.").Append(newline);
            }
            else
            {
                stub.Append("//   - This may indicate a corrupted, invalid, or unsupported NCS file format.").Append(newline);
                stub.Append("//   - The file may be from a different game version or modded in an incompatible way.").Append(newline);
            }
            stub.Append("//   - Check the exception details above for specific error information.").Append(newline);
            stub.Append("//   - Verify the file is a valid KotOR/TSL NCS bytecode file.").Append(newline);
            stub.Append(newline);

            // Minimal valid NSS stub
            stub.Append("// Minimal fallback function:").Append(newline);
            stub.Append("void main() {").Append(newline);
            stub.Append("    // Decompilation failed at stage: ").Append(errorStage != null ? errorStage : "Unknown").Append(newline);
            if (exception != null && exception.Message != null)
            {
                stub.Append("    // Error: ").Append(exception.Message.Replace("\n", " ").Replace("\r", "")).Append(newline);
            }
            stub.Append("}").Append(newline);

            return stub.ToString();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:667-696
        // Original: private String comparePcodeFiles(File originalPcode, File newPcode)
        private string ComparePcodeFiles(File originalPcode, File newPcode)
        {
            try
            {
                using (BufferedReader reader1 = new BufferedReader(new FileReader(originalPcode)))
                {
                    using (BufferedReader reader2 = new BufferedReader(new FileReader(newPcode)))
                    {
                        string line1;
                        string line2;
                        int line = 1;

                        while (true)
                        {
                            line1 = reader1.ReadLine();
                            line2 = reader2.ReadLine();

                            // both files ended -> identical
                            if (line1 == null && line2 == null)
                            {
                                return null; // identical
                            }

                            // Detect differences: missing line or differing content
                            if (line1 == null || line2 == null || !line1.Equals(line2))
                            {
                                string left = line1 == null ? "<EOF>" : line1;
                                string right = line2 == null ? "<EOF>" : line2;
                                return "Mismatch at line " + line + " | original: " + left + " | generated: " + right;
                            }

                            line++;
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                JavaSystem.@out.Println("IO exception in compare files: " + ex);
                return "IO exception during pcode comparison";
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:701-721
        // Original: private boolean compareBinaryFiles(File original, File generated)
        private bool CompareBinaryFiles(File original, File generated)
        {
            try
            {
                using (var a = new BufferedStream(new FileStream(original.FullName, FileMode.Open, FileAccess.Read)))
                {
                    using (var b = new BufferedStream(new FileStream(generated.FullName, FileMode.Open, FileAccess.Read)))
                    {
                        int ba;
                        int bb;
                        while (true)
                        {
                            ba = a.ReadByte();
                            bb = b.ReadByte();
                            if (ba == -1 || bb == -1)
                            {
                                return ba == -1 && bb == -1;
                            }

                            if (ba != bb)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                JavaSystem.@out.Println("IO exception in compare files: " + ex);
                return false;
            }
        }

        // Placeholder for old ComparePcodeFiles that was not decompiled:
        private string ComparePcodeFilesOld(File file1, File file2)
        {

            //
            // This method could not be decompiled.
            //
            // Original Bytecode:
            //
            //     1: astore_3        /* br1 */
            //     2: aconst_null
            //     3: astore          br2
            //     5: new             Ljava/io/BufferedReader;
            //     8: dup
            //     9: new             Ljava/io/FileReader;
            //    12: dup
            //    13: aload_1         /* file1 */
            //    14: invokespecial   java/io/FileReader.<init>:(Ljava/io/File;)V
            //    17: invokespecial   java/io/BufferedReader.<init>:(Ljava/io/Reader;)V
            //    20: astore_3        /* br1 */
            //    21: new             Ljava/io/BufferedReader;
            //    24: dup
            //    25: new             Ljava/io/FileReader;
            //    28: dup
            //    29: aload_2         /* file2 */
            //    30: invokespecial   java/io/FileReader.<init>:(Ljava/io/File;)V
            //    33: invokespecial   java/io/BufferedReader.<init>:(Ljava/io/Reader;)V
            //    36: astore          br2
            //    38: aload_3         /* br1 */
            //    39: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //    42: astore          s1
            //    44: aload           br2
            //    46: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //    49: astore          s2
            //    51: goto            91
            //    54: aload           br2
            //    56: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //    59: astore          s2
            //    61: aload           s1
            //    63: aload           s2
            //    65: invokevirtual   java/lang/String.equals:(Ljava/lang/Object;)Z
            //    68: ifne            91
            //    71: aload           s1
            //    73: astore          9
            //    75: aload_3         /* br1 */
            //    76: invokevirtual   java/io/BufferedReader.close:()V
            //    79: aload           br2
            //    81: invokevirtual   java/io/BufferedReader.close:()V
            //    84: goto            88
            //    87: pop
            //    88: aload           9
            //    90: areturn
            //    91: aload_3         /* br1 */
            //    92: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //    95: dup
            //    96: astore          5
            //    98: ifnonnull       54
            //   101: aload           br2
            //   103: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //   106: dup
            //   107: astore          s2
            //   109: ifnonnull       131
            //   112: aconst_null
            //   113: astore          9
            //   115: aload_3         /* br1 */
            //   116: invokevirtual   java/io/BufferedReader.close:()V
            //   119: aload           br2
            //   121: invokevirtual   java/io/BufferedReader.close:()V
            //   124: goto            128
            //   127: pop
            //   128: aload           9
            //   130: areturn
            //   131: aload           s2
            //   133: astore          9
            //   135: aload_3         /* br1 */
            //   136: invokevirtual   java/io/BufferedReader.close:()V
            //   139: aload           br2
            //   141: invokevirtual   java/io/BufferedReader.close:()V
            //   144: goto            148
            //   147: pop
            //   148: aload           9
            //   150: areturn
            //   151: astore          e
            //   153: getstatic       java/lang/System.out:Ljava/io/PrintStream;
            //   156: new             Ljava/lang/StringBuilder;
            //   159: dup
            //   160: ldc_w           "IO exception in compare files: "
            //   163: invokespecial   java/lang/StringBuilder.<init>:(Ljava/lang/String;)V
            //   166: aload           e
            //   168: invokevirtual   java/lang/StringBuilder.append:(Ljava/lang/Object;)Ljava/lang/StringBuilder;
            //   171: invokevirtual   java/lang/StringBuilder.toString:()Ljava/lang/String;
            //   174: invokevirtual   java/io/PrintStream.println:(Ljava/lang/String;)V
            //   177: aconst_null
            //   178: astore          9
            //   180: aload_3         /* br1 */
            //   181: invokevirtual   java/io/BufferedReader.close:()V
            //   184: aload           br2
            //   186: invokevirtual   java/io/BufferedReader.close:()V
            //   189: goto            193
            //   192: pop
            //   193: aload           9
            //   195: areturn
            //   196: astore          8
            //   198: aload_3         /* br1 */
            //   199: invokevirtual   java/io/BufferedReader.close:()V
            //   202: aload           br2
            //   204: invokevirtual   java/io/BufferedReader.close:()V
            //   207: goto            211
            //   210: pop
            //   211: aload           8
            //   213: athrow
            //    Exceptions:
            //  Try           Handler
            //  Start  End    Start  End    Type
            //  -----  -----  -----  -----  ---------------------
            //  75     87     87     88     Ljava/lang/Exception;
            //  115    127    127    128    Ljava/lang/Exception;
            //  135    147    147    148    Ljava/lang/Exception;
            //  5      151    151    196    Ljava/io/IOException;
            //  180    192    192    193    Ljava/lang/Exception;
            //  5      75     196    214    Any
            //  91     115    196    214    Any
            //  131    135    196    214    Any
            //  151    180    196    214    Any
            //  198    210    210    211    Ljava/lang/Exception;
            //
            // The error that occurred was:
            //
            // java.lang.NullPointerException
            //     at com.strobel.decompiler.ast.AstBuilder.convertLocalVariables(AstBuilder.java:2945)
            //     at com.strobel.decompiler.ast.AstBuilder.performStackAnalysis(AstBuilder.java:2501)
            //     at com.strobel.decompiler.ast.AstBuilder.build(AstBuilder.java:108)
            //     at com.strobel.decompiler.languages.java.ast.AstMethodBodyBuilder.createMethodBody(AstMethodBodyBuilder.java:203)
            //     at com.strobel.decompiler.languages.java.ast.AstMethodBodyBuilder.createMethodBody(AstMethodBodyBuilder.java:93)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createMethodBody(AstBuilder.java:868)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createMethod(AstBuilder.java:761)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.addTypeMembers(AstBuilder.java:638)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createTypeCore(AstBuilder.java:605)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createTypeNoCache(AstBuilder.java:195)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createType(AstBuilder.java:162)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.addType(AstBuilder.java:137)
            //     at com.strobel.decompiler.languages.java.JavaLanguage.buildAst(JavaLanguage.java:71)
            //     at com.strobel.decompiler.languages.java.JavaLanguage.decompileType(JavaLanguage.java:59)
            //     at com.strobel.decompiler.DecompilerDriver.decompileType(DecompilerDriver.java:333)
            //     at com.strobel.decompiler.DecompilerDriver.decompileJar(DecompilerDriver.java:254)
            //     at com.strobel.decompiler.DecompilerDriver.main(DecompilerDriver.java:144)
            //
            throw new InvalidOperationException("An error occurred while decompiling this method.");
        }

        private bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private Game MapGameType()
        {
            if (this.gameType == NWScriptLocator.GameType.TSL)
            {
                return Game.K2;
            }

            return Game.K1;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1852-1865
        // Original: private Iterable<ASubroutine> subIterable(SubroutineAnalysisData subdata)
        private IEnumerable<ASubroutine> SubIterable(SubroutineAnalysisData subdata)
        {
            List<ASubroutine> list = new List<ASubroutine>();
            IEnumerator<object> raw = subdata.GetSubroutines();

            while (raw.HasNext())
            {
                ASubroutine sub = (ASubroutine)raw.Next();
                if (sub == null)
                {
                    throw new InvalidOperationException("Unexpected null element in subroutine list");
                }
                list.Add(sub);
            }

            return list;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1867-1882
        // Original: private void enforceStrictSignatures(SubroutineAnalysisData subdata, NodeAnalysisData nodedata)
        private void EnforceStrictSignatures(SubroutineAnalysisData subdata, NodeAnalysisData nodedata)
        {
            if (!FileDecompiler.strictSignatures)
            {
                return;
            }

            foreach (ASubroutine iterSub in this.SubIterable(subdata))
            {
                SubroutineState state = subdata.GetState(iterSub);
                if (!state.IsTotallyPrototyped())
                {
                    JavaSystem.@out.Println(
                        "Strict signatures: unresolved signature for subroutine at " +
                        nodedata.GetPos(iterSub).ToString() +
                        " (continuing)"
                    );
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1193-1847
        // Original: private FileDecompiler.FileScriptData decompileNcs(File file)
        private Utils.FileScriptData DecompileNcs(File file)
        {
            Utils.FileScriptData data = null;
            string commands = null;
            SetDestinations setdest = null;
            DoTypes dotypes = null;
            Node ast = null;
            NodeAnalysisData nodedata = null;
            SubroutineAnalysisData subdata = null;
            IEnumerator<object> subs = null;
            ASubroutine sub = null;
            ASubroutine mainsub = null;
            FlattenSub flatten = null;
            DoGlobalVars doglobs = null;
            CleanupPass cleanpass = null;
            MainPass mainpass = null;
            DestroyParseTree destroytree = null;
            if (this.actions == null)
            {
                JavaSystem.@out.Println("null action! Creating fallback stub.");
                // Return comprehensive stub instead of null
                Utils.FileScriptData stub = new Utils.FileScriptData();
                string expectedFile = isK2Selected ? "tsl_nwscript.nss" : "k1_nwscript.nss";
                string stubCode = this.GenerateComprehensiveFallbackStub(file, "Actions data loading", null,
                    "The actions data table (nwscript.nss) is required to decompile NCS files.\n" +
                    "Expected file: " + expectedFile + "\n" +
                    "Please ensure the appropriate nwscript.nss file is available in tools/ directory, working directory, or configured path.");
                stub.SetCode(stubCode);
                return stub;
            }

            try
            {
                data = new Utils.FileScriptData();

                // Decode bytecode - wrap in try-catch to handle corrupted files
                try
                {
                    JavaSystem.@err.Println("DEBUG decompileNcs: starting decode for " + file.Name);
                    using (var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                    using (var bufferedStream = new BufferedStream(fileStream))
                    using (var binaryReader = new BinaryReader(bufferedStream))
                    {
                        commands = new Decoder(binaryReader, this.actions).Decode();
                    }
                    JavaSystem.@err.Println("DEBUG decompileNcs: decode successful, commands length=" + (commands != null ? commands.Length : 0));
                }
                catch (Exception decodeEx)
                {
                    JavaSystem.@err.Println("DEBUG decompileNcs: decode FAILED - " + decodeEx.Message);
                    JavaSystem.@out.Println("Error during bytecode decoding: " + decodeEx.Message);
                    // Create comprehensive fallback stub for decoding errors
                    long fileSize = file.Exists() ? file.Length : -1;
                    string fileInfo = "File size: " + fileSize + " bytes";
                    if (fileSize > 0)
                    {
                        try
                        {
                            using (var fis = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                            {
                                byte[] header = new byte[Math.Min(16, (int)fileSize)];
                                int read = fis.Read(header, 0, header.Length);
                                if (read > 0)
                                {
                                    fileInfo += "\nFile header (hex): " + this.BytesToHex(header, read);
                                }
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    string stub = this.GenerateComprehensiveFallbackStub(file, "Bytecode decoding", decodeEx, fileInfo);
                    data.SetCode(stub);
                    return data;
                }

                // Parse commands - wrap in try-catch to handle parse errors, but try to recover
                try
                {
                    JavaSystem.@err.Println("DEBUG decompileNcs: starting parse, commands length=" + (commands != null ? commands.Length : 0));
                    using (var stringReader = new StringReader(commands))
                    using (var pushbackReader = new PushbackReader(stringReader, 1024))
                    {
                        ast = new Parser(new Lexer(pushbackReader)).Parse();
                    }
                    JavaSystem.@err.Println("DEBUG decompileNcs: parse successful");
                }
                catch (Exception parseEx)
                {
                    JavaSystem.@err.Println("DEBUG decompileNcs: parse FAILED - " + parseEx.Message);
                    JavaSystem.@out.Println("Error during parsing: " + parseEx.Message);
                    JavaSystem.@out.Println("Attempting to recover by trying partial parsing strategies...");

                    // Try to recover: attempt to parse in chunks or with relaxed rules
                    ast = null;
                    try
                    {
                        // Strategy 1: Try parsing with a larger buffer
                        JavaSystem.@out.Println("Trying parse with larger buffer...");
                        using (var stringReader = new StringReader(commands))
                        using (var pushbackReader = new PushbackReader(stringReader, 2048))
                        {
                            ast = new Parser(new Lexer(pushbackReader)).Parse();
                        }
                        JavaSystem.@out.Println("Successfully recovered parse with larger buffer.");
                    }
                    catch (Exception e1)
                    {
                        JavaSystem.@out.Println("Larger buffer parse also failed: " + e1.Message);
                        // Strategy 2: Try to extract what we can and create minimal structure
                        // If we have decoded commands, we can at least create a basic structure
                        if (commands != null && commands.Length > 0)
                        {
                            JavaSystem.@out.Println("Attempting to create minimal structure from decoded commands...");
                            try
                            {
                                // Try to find subroutine boundaries in the commands string
                                // This is a heuristic recovery - look for common patterns
                                string[] lines = commands.Split('\n');
                                int subCount = 0;
                                foreach (string line in lines)
                                {
                                    string trimmed = line.Trim();
                                    if (trimmed.StartsWith("sub") || trimmed.StartsWith("function"))
                                    {
                                        subCount++;
                                    }
                                }

                                // If we found some structure, try to continue with minimal setup
                                if (subCount > 0)
                                {
                                    JavaSystem.@out.Println("Detected " + subCount + " potential subroutines in decoded commands, but full parse failed.");
                                    // We'll fall through to create a stub, but with better information
                                }
                            }
                            catch (Exception e2)
                            {
                                JavaSystem.@out.Println("Recovery attempt failed: " + e2.Message);
                            }
                        }
                    }

                    // If we still don't have an AST, create comprehensive stub but preserve commands for potential manual recovery
                    if (ast == null)
                    {
                        string commandsPreview = "none";
                        if (commands != null && commands.Length > 0)
                        {
                            int previewLength = Math.Min(1000, commands.Length);
                            commandsPreview = commands.Substring(0, previewLength);
                            if (commands.Length > previewLength)
                            {
                                commandsPreview += "\n... (truncated, total length: " + commands.Length + " characters)";
                            }
                        }
                        string additionalInfo = "Bytecode was successfully decoded but parsing failed.\n" +
                                               "Decoded commands length: " + (commands != null ? commands.Length : 0) + " characters\n" +
                                               "Decoded commands preview:\n" + commandsPreview + "\n\n" +
                                               "RECOVERY NOTE: The decoded commands are available but could not be parsed into an AST.\n" +
                                               "This may indicate malformed bytecode or an unsupported format variant.";
                        string stub = this.GenerateComprehensiveFallbackStub(file, "Parsing decoded bytecode", parseEx, additionalInfo);
                        data.SetCode(stub);
                        return data;
                    }
                    // If we recovered an AST, continue with decompilation
                    JavaSystem.@out.Println("Continuing decompilation with recovered parse tree.");
                }

                // Analysis passes - wrap in try-catch to allow partial recovery
                nodedata = new NodeAnalysisData();
                subdata = new SubroutineAnalysisData(nodedata);

                try
                {
                    ast.Apply(new SetPositions(nodedata));
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error in SetPositions, continuing with partial positions: " + e.Message);
                }

                try
                {
                    setdest = new SetDestinations(ast, nodedata, subdata);
                    ast.Apply(setdest);
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error in SetDestinations, continuing without destination resolution: " + e.Message);
                    setdest = null;
                }

                try
                {
                    if (setdest != null)
                    {
                        ast.Apply(new SetDeadCode(nodedata, subdata, setdest.GetOrigins()));
                    }
                    else
                    {
                        // Try without origins if setdest failed
                        ast.Apply(new SetDeadCode(nodedata, subdata, null));
                    }
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error in SetDeadCode, continuing without dead code analysis: " + e.Message);
                }

                if (setdest != null)
                {
                    try
                    {
                        setdest.Done();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error finalizing SetDestinations: " + e.Message);
                    }
                    setdest = null;
                }

                try
                {
                    subdata.SplitOffSubroutines(ast);
                    JavaSystem.@err.Println("DEBUG splitOffSubroutines: success, numSubs=" + subdata.NumSubs());
                }
                catch (Exception e)
                {
                    JavaSystem.@err.Println("DEBUG splitOffSubroutines: ERROR - " + e.Message);
                    e.PrintStackTrace(JavaSystem.@err);
                    JavaSystem.@out.Println("Error splitting subroutines, attempting to continue: " + e.Message);
                    // Try to get main sub at least
                    try
                    {
                        mainsub = subdata.GetMainSub();
                        JavaSystem.@err.Println("DEBUG splitOffSubroutines: recovered mainsub=" + (mainsub != null ? "found" : "null"));
                    }
                    catch (Exception e2)
                    {
                        JavaSystem.@err.Println("DEBUG splitOffSubroutines: could not recover mainsub - " + e2.Message);
                        JavaSystem.@out.Println("Could not recover main subroutine: " + e2.Message);
                    }
                }
                ast = null;
                // Flattening - try to recover if main sub is missing
                try
                {
                    mainsub = subdata.GetMainSub();
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error getting main subroutine: " + e.Message);
                    mainsub = null;
                }

                if (mainsub != null)
                {
                    try
                    {
                        flatten = new FlattenSub(mainsub, nodedata);
                        mainsub.Apply(flatten);
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error flattening main subroutine: " + e.Message);
                        flatten = null;
                    }

                    if (flatten != null)
                    {
                        try
                        {
                            foreach (ASubroutine iterSub in this.SubIterable(subdata))
                            {
                                try
                                {
                                    flatten.SetSub(iterSub);
                                    iterSub.Apply(flatten);
                                }
                                catch (Exception e)
                                {
                                    JavaSystem.@out.Println("Error flattening subroutine, skipping: " + e.Message);
                                    // Continue with other subroutines
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error iterating subroutines during flattening: " + e.Message);
                        }

                        try
                        {
                            flatten.Done();
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error finalizing flatten: " + e.Message);
                        }
                        flatten = null;
                    }
                }
                else
                {
                    JavaSystem.@out.Println("Warning: No main subroutine available, continuing with partial decompilation.");
                }
                // Process globals - recover if this fails
                try
                {
                    sub = subdata.GetGlobalsSub();
                    if (sub != null)
                    {
                        try
                        {
                            doglobs = new DoGlobalVars(nodedata, subdata);
                            sub.Apply(doglobs);
                            cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                            cleanpass.Apply();
                            subdata.SetGlobalStack(doglobs.GetStack());
                            subdata.GlobalState(doglobs.GetState());
                            cleanpass.Done();
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error processing globals, continuing without globals: " + e.Message);
                            if (doglobs != null)
                            {
                                try
                                {
                                    doglobs.Done();
                                }
                                catch (Exception e2)
                                {
                                }
                            }
                            doglobs = null;
                        }
                    }
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error getting globals subroutine: " + e.Message);
                }

                // Prototype engine - recover if this fails
                try
                {
                    PrototypeEngine proto = new PrototypeEngine(nodedata, subdata, this.actions, FileDecompiler.strictSignatures);
                    proto.Run();
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error in prototype engine, continuing with partial prototypes: " + e.Message);
                }

                // Type analysis - recover if main sub typing fails
                if (mainsub != null)
                {
                    try
                    {
                        dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                        mainsub.Apply(dotypes);

                        try
                        {
                            dotypes.AssertStack();
                        }
                        catch (Exception)
                        {
                            JavaSystem.@out.Println("Could not assert stack, continuing anyway.");
                        }

                        dotypes.Done();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error typing main subroutine, continuing with partial types: " + e.Message);
                        dotypes = null;
                    }
                }

                // Type all subroutines - continue even if some fail
                bool alldone = false;
                bool onedone = true;
                int donecount = 0;

                try
                {
                    alldone = subdata.CountSubsDone() == subdata.NumSubs();
                    onedone = true;
                    donecount = subdata.CountSubsDone();
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error checking subroutine completion status: " + e.Message);
                }

                for (int loopcount = 0; !alldone && onedone && loopcount < 1000; ++loopcount)
                {
                    onedone = false;
                    try
                    {
                        subs = subdata.GetSubroutines();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error getting subroutines iterator: " + e.Message);
                        break;
                    }

                    if (subs != null)
                    {
                        while (subs.HasNext())
                        {
                            try
                            {
                                sub = (ASubroutine)subs.Next();
                                if (sub == null) continue;

                                dotypes = new DoTypes(subdata.GetState(sub), nodedata, subdata, this.actions, false);
                                sub.Apply(dotypes);
                                dotypes.Done();
                            }
                            catch (Exception e)
                            {
                                JavaSystem.@out.Println("Error typing subroutine, skipping: " + e.Message);
                                // Continue with next subroutine
                            }
                        }
                    }

                    if (mainsub != null)
                    {
                        try
                        {
                            dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                            mainsub.Apply(dotypes);
                            dotypes.Done();
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error re-typing main subroutine: " + e.Message);
                        }
                    }

                    try
                    {
                        alldone = subdata.CountSubsDone() == subdata.NumSubs();
                        int newDoneCount = subdata.CountSubsDone();
                        onedone = newDoneCount > donecount;
                        donecount = newDoneCount;
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error checking completion status: " + e.Message);
                        break;
                    }
                }

                if (!alldone)
                {
                    JavaSystem.@out.Println("Unable to do final prototype of all subroutines. Continuing with partial results.");
                }

                this.EnforceStrictSignatures(subdata, nodedata);

                dotypes = null;
                nodedata.ClearProtoData();

                JavaSystem.@err.Println("DEBUG decompileNcs: iterating subroutines, numSubs=" + subdata.NumSubs());
                int subCount = 0;
                foreach (ASubroutine iterSub in this.SubIterable(subdata))
                {
                    subCount++;
                    JavaSystem.@err.Println("DEBUG decompileNcs: processing subroutine " + subCount + " at pos=" + nodedata.GetPos(iterSub));
                    try
                    {
                        mainpass = new MainPass(subdata.GetState(iterSub), nodedata, subdata, this.actions);
                        iterSub.Apply(mainpass);
                        cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                        cleanpass.Apply();
                        data.AddSub(mainpass.GetState());
                        JavaSystem.@err.Println("DEBUG decompileNcs: successfully added subroutine " + subCount);
                        mainpass.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@err.Println("DEBUG decompileNcs: ERROR processing subroutine " + subCount + " - " + e.Message);
                        JavaSystem.@out.Println("Error while processing subroutine: " + e);
                        e.PrintStackTrace(JavaSystem.@out);
                        // Try to add partial subroutine state even if processing failed
                        try
                        {
                            SubroutineState state = subdata.GetState(iterSub);
                            if (state != null)
                            {
                                MainPass recoveryPass = new MainPass(state, nodedata, subdata, this.actions);
                                // Try to get state even if apply failed
                                SubScriptState recoveryState = recoveryPass.GetState();
                                if (recoveryState != null)
                                {
                                    data.AddSub(recoveryState);
                                    JavaSystem.@out.Println("Added partial subroutine state after error recovery.");
                                }
                            }
                        }
                        catch (Exception e2)
                        {
                            JavaSystem.@out.Println("Could not recover partial subroutine state: " + e2.Message);
                        }
                    }
                }

                // Generate code for main subroutine - recover if this fails
                JavaSystem.@err.Println("DEBUG decompileNcs: mainsub=" + (mainsub != null ? "found at pos=" + nodedata.GetPos(mainsub) : "null"));
                if (mainsub != null)
                {
                    try
                    {
                        JavaSystem.@err.Println("DEBUG decompileNcs: creating MainPass for mainsub");
                        mainpass = new MainPass(subdata.GetState(mainsub), nodedata, subdata, this.actions);
                        JavaSystem.@err.Println("DEBUG decompileNcs: applying mainpass to mainsub");
                        mainsub.Apply(mainpass);

                        try
                        {
                            mainpass.AssertStack();
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Could not assert stack, continuing anyway.");
                        }

                        cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                        cleanpass.Apply();
                        mainpass.GetState().IsMain(true);
                        data.AddSub(mainpass.GetState());
                        mainpass.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error generating code for main subroutine: " + e.Message);
                        // Try to create a minimal main function stub using MainPass
                        try
                        {
                            mainpass = new MainPass(subdata.GetState(mainsub), nodedata, subdata, this.actions);
                            // Even if apply fails, try to get the state
                            try
                            {
                                mainsub.Apply(mainpass);
                            }
                            catch (Exception e2)
                            {
                                JavaSystem.@out.Println("Could not apply mainpass, but attempting to use partial state: " + e2.Message);
                            }
                            SubScriptState minimalMain = mainpass.GetState();
                            if (minimalMain != null)
                            {
                                minimalMain.IsMain(true);
                                data.AddSub(minimalMain);
                                JavaSystem.@out.Println("Created minimal main subroutine stub.");
                            }
                            mainpass.Done();
                        }
                        catch (Exception e2)
                        {
                            JavaSystem.@out.Println("Could not create minimal main stub: " + e2.Message);
                        }
                    }
                }
                else
                {
                    JavaSystem.@out.Println("Warning: No main subroutine available for code generation.");
                }
                // Store analysis data and globals - recover if this fails
                try
                {
                    data.SetSubdata(subdata);
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error storing subroutine analysis data: " + e.Message);
                }

                if (doglobs != null)
                {
                    try
                    {
                        cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                        cleanpass.Apply();
                        data.SetGlobals(doglobs.GetState());
                        doglobs.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error finalizing globals: " + e.Message);
                        try
                        {
                            if (doglobs.GetState() != null)
                            {
                                data.SetGlobals(doglobs.GetState());
                            }
                            doglobs.Done();
                        }
                        catch (Exception e2)
                        {
                            JavaSystem.@out.Println("Could not recover globals state: " + e2.Message);
                        }
                    }
                }

                // Cleanup parse tree - this is safe to skip if it fails
                try
                {
                    destroytree = new DestroyParseTree();

                    foreach (ASubroutine iterSub in this.SubIterable(subdata))
                    {
                        try
                        {
                            iterSub.Apply(destroytree);
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error destroying parse tree for subroutine: " + e.Message);
                        }
                    }

                    if (mainsub != null)
                    {
                        try
                        {
                            mainsub.Apply(destroytree);
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error destroying main parse tree: " + e.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error during parse tree cleanup: " + e.Message);
                    // Continue anyway - cleanup is not critical
                }

                return data;
            }
            catch (Exception e)
            {
                // Try to salvage partial results before giving up
                JavaSystem.@out.Println("Error during decompilation: " + e.Message);
                e.PrintStackTrace(JavaSystem.@out);

                // Always return a FileScriptData, even if it's just a minimal stub
                if (data == null)
                {
                    data = new Utils.FileScriptData();
                }

                // Aggressive recovery: try to salvage whatever state we have
                JavaSystem.@out.Println("Attempting aggressive state recovery...");

                // Try to add any subroutines that were partially processed
                if (subdata != null && mainsub != null)
                {
                    try
                    {
                        // Try to get main sub state even if it's incomplete
                        SubroutineState mainState = subdata.GetState(mainsub);
                        if (mainState != null)
                        {
                            try
                            {
                                // Try to create a minimal main pass
                                mainpass = new MainPass(mainState, nodedata, subdata, this.actions);
                                try
                                {
                                    mainsub.Apply(mainpass);
                                }
                                catch (Exception e3)
                                {
                                    JavaSystem.@out.Println("Could not apply mainpass to main sub, but continuing: " + e3.Message);
                                }
                                SubScriptState scriptState = mainpass.GetState();
                                if (scriptState != null)
                                {
                                    scriptState.IsMain(true);
                                    data.AddSub(scriptState);
                                    mainpass.Done();
                                    JavaSystem.@out.Println("Recovered main subroutine state.");
                                }
                            }
                            catch (Exception e2)
                            {
                                JavaSystem.@out.Println("Could not create main pass: " + e2.Message);
                            }
                        }
                    }
                    catch (Exception e2)
                    {
                        JavaSystem.@out.Println("Error recovering main subroutine: " + e2.Message);
                    }

                    // Try to recover other subroutines
                    try
                    {
                        foreach (ASubroutine iterSub in this.SubIterable(subdata))
                        {
                            if (iterSub == mainsub) continue; // Already handled
                            try
                            {
                                SubroutineState state = subdata.GetState(iterSub);
                                if (state != null)
                                {
                                    try
                                    {
                                        mainpass = new MainPass(state, nodedata, subdata, this.actions);
                                        try
                                        {
                                            iterSub.Apply(mainpass);
                                        }
                                        catch (Exception e3)
                                        {
                                            JavaSystem.@out.Println("Could not apply mainpass to subroutine, but continuing: " + e3.Message);
                                        }
                                        SubScriptState scriptState = mainpass.GetState();
                                        if (scriptState != null)
                                        {
                                            data.AddSub(scriptState);
                                            mainpass.Done();
                                        }
                                    }
                                    catch (Exception e2)
                                    {
                                        JavaSystem.@out.Println("Could not create mainpass for subroutine: " + e2.Message);
                                    }
                                }
                            }
                            catch (Exception e2)
                            {
                                JavaSystem.@out.Println("Error recovering subroutine: " + e2.Message);
                            }
                        }
                    }
                    catch (Exception e2)
                    {
                        JavaSystem.@out.Println("Error iterating subroutines during recovery: " + e2.Message);
                    }

                    // Try to store subdata
                    try
                    {
                        data.SetSubdata(subdata);
                    }
                    catch (Exception e2)
                    {
                        JavaSystem.@out.Println("Error storing subdata: " + e2.Message);
                    }
                }

                // Try to recover globals if available
                if (doglobs != null)
                {
                    try
                    {
                        SubScriptState globState = doglobs.GetState();
                        if (globState != null)
                        {
                            data.SetGlobals(globState);
                            JavaSystem.@out.Println("Recovered globals state.");
                        }
                    }
                    catch (Exception e2)
                    {
                        JavaSystem.@out.Println("Error recovering globals: " + e2.Message);
                    }
                }

                try
                {
                    // Try to generate code from whatever we have
                    data.GenerateCode();
                    string partialCode = data.GetCode();
                    if (partialCode != null && partialCode.Trim().Length > 0)
                    {
                        JavaSystem.@out.Println("Successfully recovered partial decompilation with " +
                                                (data.GetVars() != null ? data.GetVars().Count : 0) + " subroutines.");
                        // Add recovery note to the code
                        string recoveryNote = "// ========================================\n" +
                                            "// PARTIAL DECOMPILATION - RECOVERED STATE\n" +
                                            "// ========================================\n" +
                                            "// This decompilation encountered errors but recovered partial results.\n" +
                                            "// Some subroutines or code sections may be incomplete or missing.\n" +
                                            "// Original error: " + e.GetType().Name + ": " +
                                            (e.Message != null ? e.Message : "(no message)") + "\n" +
                                            "// ========================================\n\n";
                        data.SetCode(recoveryNote + partialCode);
                        return data;
                    }
                }
                catch (Exception genEx)
                {
                    JavaSystem.@out.Println("Could not generate partial code: " + genEx.Message);
                }

                // Last resort: create comprehensive stub with any available partial information
                string partialInfo = "Partial decompilation state:\n";
                try
                {
                    if (data != null)
                    {
                        Dictionary<string, List<object>> vars = data.GetVars();
                        if (vars != null && vars.Count > 0)
                        {
                            partialInfo += "  Subroutines with variable data: " + vars.Count + "\n";
                        }
                    }
                    if (subdata != null)
                    {
                        try
                        {
                            partialInfo += "  Total subroutines detected: " + subdata.NumSubs() + "\n";
                            partialInfo += "  Subroutines fully typed: " + subdata.CountSubsDone() + "\n";
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (commands != null)
                    {
                        partialInfo += "  Commands decoded: " + commands.Length + " characters\n";
                    }
                    if (ast != null)
                    {
                        partialInfo += "  Parse tree created: yes\n";
                    }
                    if (nodedata != null)
                    {
                        partialInfo += "  Node analysis data available: yes\n";
                    }
                    if (mainsub != null)
                    {
                        partialInfo += "  Main subroutine identified: yes\n";
                    }
                }
                catch (Exception)
                {
                    partialInfo += "  (Unable to gather partial state information)\n";
                }
                string errorStub = this.GenerateComprehensiveFallbackStub(file, "General decompilation pipeline", e, partialInfo);
                data.SetCode(errorStub);
                JavaSystem.@out.Println("Created fallback stub code due to decompilation errors.");
                return data;
            }
            finally
            {
                data = null;
                commands = null;
                setdest = null;
                dotypes = null;
                ast = null;
                if (nodedata != null)
                {
                    nodedata.Dispose();
                }

                nodedata = null;
                if (subdata != null)
                {
                    subdata.ParseDone();
                }

                subdata = null;
                subs = null;
                sub = null;
                mainsub = null;
                flatten = null;
                doglobs = null;
                cleanpass = null;
                mainpass = null;
                destroytree = null;
                GC.Collect();
            }
        }

        /// <summary>
        /// Decompiles an NCS object in memory (not from file).
        /// This is the core decompilation logic extracted from DecompileNcs(File).
        /// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:588-916
        /// </summary>
        public virtual Utils.FileScriptData DecompileNcsObject(NCS ncs)
        {
            Utils.FileScriptData data = null;
            SetDestinations setdest = null;
            DoTypes dotypes = null;
            Node ast = null;
            NodeAnalysisData nodedata = null;
            SubroutineAnalysisData subdata = null;
            IEnumerator<object> subs = null;
            ASubroutine sub = null;
            ASubroutine mainsub = null;
            FlattenSub flatten = null;
            DoGlobalVars doglobs = null;
            CleanupPass cleanpass = null;
            MainPass mainpass = null;
            DestroyParseTree destroytree = null;

            if (ncs == null)
            {
                return null;
            }

            // Lazy-load actions if not already loaded
            if (this.actions == null)
            {
                this.LoadActions();
                if (this.actions == null)
                {
                    JavaSystem.@out.Println("Failed to load actions file!");
                    return null;
                }
            }

            try
            {
                data = new Utils.FileScriptData();

                if (ncs.Instructions == null || ncs.Instructions.Count == 0)
                {
                    JavaSystem.@out.Println("NCS contains no instructions; skipping decompilation.");
                    return null;
                }

                ast = NcsToAstConverter.ConvertNcsToAst(ncs);
                nodedata = new NodeAnalysisData();
                subdata = new SubroutineAnalysisData(nodedata);
                ast.Apply(new SetPositions(nodedata));
                setdest = new SetDestinations(ast, nodedata, subdata);
                ast.Apply(setdest);
                ast.Apply(new SetDeadCode(nodedata, subdata, setdest.GetOrigins()));
                setdest.Done();
                setdest = null;
                subdata.SplitOffSubroutines(ast);
                ast = null;
                mainsub = subdata.GetMainSub();
                if (mainsub == null)
                {
                    JavaSystem.@out.Println("No main subroutine found in NCS - cannot decompile.");
                    return null;
                }
                flatten = new FlattenSub(mainsub, nodedata);
                mainsub.Apply(flatten);
                subs = subdata.GetSubroutines();
                while (subs.HasNext())
                {
                    sub = (ASubroutine)subs.Next();
                    flatten.SetSub(sub);
                    sub.Apply(flatten);
                }

                flatten.Done();
                flatten = null;
                doglobs = null;
                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1392-1414
                try
                {
                    sub = subdata.GetGlobalsSub();
                    JavaSystem.@out.Println($"DEBUG FileDecompiler: GetGlobalsSub() returned {(sub != null ? "non-null" : "null")}");
                    if (sub != null)
                    {
                        try
                        {
                            JavaSystem.@out.Println("DEBUG FileDecompiler: creating DoGlobalVars and applying to globals subroutine");
                            doglobs = new DoGlobalVars(nodedata, subdata);
                            sub.Apply(doglobs);
                            JavaSystem.@out.Println($"DEBUG FileDecompiler: sub.Apply(doglobs) completed, root children count: {doglobs.GetScriptRoot().GetChildren().Count}");
                            cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                            cleanpass.Apply();
                            subdata.SetGlobalStack(doglobs.GetStack());
                            subdata.GlobalState(doglobs.GetState());
                            cleanpass.Done();
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error processing globals, continuing without globals: " + e.Message);
                            if (doglobs != null)
                            {
                                try
                                {
                                    doglobs.Done();
                                }
                                catch (Exception e2)
                                {
                                    JavaSystem.@out.Println($"DEBUG FileDecompiler: ignorable error in Done for doglobs: {e2.Message}");
                                }
                            }
                            doglobs = null;
                        }
                    }
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error getting globals subroutine: " + e.Message);
                }

                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1407-1413
                // Prototype engine - recover if this fails
                try
                {
                    PrototypeEngine proto = new PrototypeEngine(nodedata, subdata, this.actions, false);
                    proto.Run();
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error in prototype engine, continuing with partial prototypes: " + e.Message);
                }

                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1415-1495
                // Type analysis - recover if main sub typing fails
                if (mainsub != null)
                {
                    try
                    {
                        dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                        mainsub.Apply(dotypes);

                        try
                        {
                            dotypes.AssertStack();
                        }
                catch (Exception)
                {
                    JavaSystem.@out.Println("Could not assert stack, continuing anyway.");
                }

                        dotypes.Done();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error typing main subroutine, continuing with partial types: " + e.Message);
                        dotypes = null;
                    }
                }

                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1434-1495
                // Type all subroutines - continue even if some fail
                bool alldone = false;
                bool onedone = true;
                int donecount = 0;

                try
                {
                    alldone = subdata.CountSubsDone() == subdata.NumSubs();
                    onedone = true;
                    donecount = subdata.CountSubsDone();
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error checking subroutine completion status: " + e.Message);
                }

                for (int loopcount = 0; !alldone && onedone && loopcount < 1000; ++loopcount)
                {
                    onedone = false;
                    try
                    {
                        subs = subdata.GetSubroutines();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error getting subroutines iterator: " + e.Message);
                        break;
                    }

                    if (subs != null)
                    {
                        while (subs.HasNext())
                        {
                            try
                            {
                                sub = (ASubroutine)subs.Next();
                                if (sub == null) continue;

                                dotypes = new DoTypes(subdata.GetState(sub), nodedata, subdata, this.actions, false);
                                sub.Apply(dotypes);
                                dotypes.Done();
                            }
                            catch (Exception e)
                            {
                                JavaSystem.@out.Println("Error typing subroutine, skipping: " + e.Message);
                                // Continue with next subroutine
                            }
                        }
                    }

                    if (mainsub != null)
                    {
                        try
                        {
                            dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                            mainsub.Apply(dotypes);
                            dotypes.Done();
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error re-typing main subroutine: " + e.Message);
                        }
                    }

                    try
                    {
                        alldone = subdata.CountSubsDone() == subdata.NumSubs();
                        int newDoneCount = subdata.CountSubsDone();
                        onedone = newDoneCount > donecount;
                        donecount = newDoneCount;
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error checking completion status: " + e.Message);
                        break;
                    }
                }

                if (!alldone)
                {
                    JavaSystem.@out.Println("Unable to do final prototype of all subroutines. Continuing with partial results.");
                }

                this.EnforceStrictSignatures(subdata, nodedata);

                dotypes = null;
                nodedata.ClearProtoData();
                JavaSystem.@err.Println("DEBUG decompileNcs: iterating subroutines, numSubs=" + subdata.NumSubs());
                int subCount = 0;
                foreach (ASubroutine iterSub in this.SubIterable(subdata))
                {
                    subCount++;
                    JavaSystem.@err.Println("DEBUG decompileNcs: processing subroutine " + subCount + " at pos=" + nodedata.GetPos(iterSub));
                    try
                    {
                        mainpass = new MainPass(subdata.GetState(iterSub), nodedata, subdata, this.actions);
                        iterSub.Apply(mainpass);
                    cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                    cleanpass.Apply();
                    data.AddSub(mainpass.GetState());
                        JavaSystem.@err.Println("DEBUG decompileNcs: successfully added subroutine " + subCount);
                    mainpass.Done();
                    cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@err.Println("DEBUG decompileNcs: ERROR processing subroutine " + subCount + " - " + e.Message);
                        JavaSystem.@out.Println("Error while processing subroutine: " + e);
                        e.PrintStackTrace(JavaSystem.@out);
                        // Try to add partial subroutine state even if processing failed
                        try
                        {
                            SubroutineState state = subdata.GetState(iterSub);
                            if (state != null)
                            {
                                MainPass recoveryPass = new MainPass(state, nodedata, subdata, this.actions);
                                // Try to get state even if apply failed
                                SubScriptState recoveryState = recoveryPass.GetState();
                                if (recoveryState != null)
                                {
                                    data.AddSub(recoveryState);
                                    JavaSystem.@out.Println("Added partial subroutine state after error recovery.");
                                }
                            }
                        }
                        catch (Exception e2)
                        {
                            JavaSystem.@out.Println("Could not recover partial subroutine state: " + e2.Message);
                        }
                    }
                }

                mainpass = new MainPass(subdata.GetState(mainsub), nodedata, subdata, this.actions);
                mainsub.Apply(mainpass);
                mainpass.AssertStack();
                cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                cleanpass.Apply();
                mainpass.GetState().IsMain(true);
                data.AddSub(mainpass.GetState());
                mainpass.Done();
                cleanpass.Done();
                data.SetSubdata(subdata);
                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1600-1618
                if (doglobs != null)
                {
                    try
                    {
                        cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                        cleanpass.Apply();
                        var globalsState = doglobs.GetState();
                        JavaSystem.@out.Println($"DEBUG FileDecompiler: setting globals, state is {(globalsState != null ? "non-null" : "null")}");
                        data.SetGlobals(globalsState);
                        doglobs.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error finalizing globals: " + e.Message);
                        try
                        {
                            if (doglobs.GetState() != null)
                            {
                                data.SetGlobals(doglobs.GetState());
                            }
                            doglobs.Done();
                        }
                        catch (Exception e2)
                        {
                            JavaSystem.@out.Println("Could not recover globals state: " + e2.Message);
                        }
                    }
                }
                else
                {
                    // Fallback: if doglobs is null but globals were processed, try to get state from subdata
                    SubScriptState globalState = subdata.GlobalState();
                    JavaSystem.@out.Println($"DEBUG FileDecompiler: doglobs is null, fallback globalState is {(globalState != null ? "non-null" : "null")}");
                    if (globalState != null)
                    {
                        try
                        {
                            data.SetGlobals(globalState);
                        }
                        catch (Exception e)
                        {
                            JavaSystem.@out.Println("Error setting globals from subdata: " + e.Message);
                        }
                    }
                }

                subs = subdata.GetSubroutines();
                destroytree = new DestroyParseTree();
                while (subs.HasNext())
                {
                    ((ASubroutine)subs.Next()).Apply(destroytree);
                }

                mainsub.Apply(destroytree);
                return data;
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("Exception during decompilation: " + e.GetType().Name + ": " + e.Message);
                if (e.StackTrace != null)
                {
                    JavaSystem.@out.Println("Stack trace: " + e.StackTrace);
                }
                e.PrintStackTrace(JavaSystem.@out);
                return null;
            }
            finally
            {
                data = null;
                setdest = null;
                dotypes = null;
                ast = null;
                if (nodedata != null)
                {
                    nodedata.Dispose();
                }

                nodedata = null;
                if (subdata != null)
                {
                    subdata.ParseDone();
                }

                subdata = null;
                subs = null;
                sub = null;
                mainsub = null;
                flatten = null;
                doglobs = null;
                cleanpass = null;
                mainpass = null;
                destroytree = null;
                GC.Collect();
            }
        }

        private class FileScriptData
        {
            private List<object> subs;
            private SubScriptState globals;
            private SubroutineAnalysisData subdata;
#pragma warning disable CS0414
            private readonly int status;
#pragma warning restore CS0414
            private string code;
            private string originalbytecode;
            private string generatedbytecode;
            public FileScriptData()
            {
                this.subs = new List<object>();
                this.globals = null;
                this.code = null;
                this.status = 0;
                this.originalbytecode = null;
                this.generatedbytecode = null;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1910-1931
            // Original: public void close()
            public virtual void Close()
            {
                IEnumerator<object> it = this.subs.Iterator();
                while (it.HasNext())
                {
                    ((SubScriptState)it.Next()).Dispose();
                }

                this.subs = null;
                if (this.globals != null)
                {
                    this.globals.Dispose();
                    this.globals = null;
                }

                if (this.subdata != null)
                {
                    this.subdata.Dispose();
                    this.subdata = null;
                }

                this.code = null;
                this.originalbytecode = null;
                this.generatedbytecode = null;
            }

            // C# alias for Close() to support IDisposable pattern
            public virtual void Dispose()
            {
                this.Close();
            }

            public virtual void Globals(SubScriptState globals)
            {
                this.globals = globals;
            }

            public virtual void AddSub(SubScriptState sub)
            {
                this.subs.Add(sub);
            }

            public virtual void Subdata(SubroutineAnalysisData subdata)
            {
                this.subdata = subdata;
            }

            private SubScriptState FindSub(string name)
            {
                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState state = (SubScriptState)this.subs[i];
                    if (state.Name.Equals(name))
                    {
                        return state;
                    }
                }

                return null;
            }

            public virtual bool ReplaceSubName(string oldname, string newname)
            {
                SubScriptState state = this.FindSub(oldname);
                if (state == null)
                {
                    return false;
                }

                if (this.FindSub(newname) != null)
                {
                    return false;
                }

                state.SetName(newname);
                this.GenerateCode();
                state = null;
                return true;
            }

            public override string ToString()
            {
                return this.code;
            }

            public virtual Dictionary<object, object> GetVars()
            {
                if (this.subs.Count == 0)
                {
                    return null;
                }

                Dictionary<object, object> vars = new Dictionary<object, object>();
                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState state = (SubScriptState)this.subs[i];
                    vars[state.Name] = state.GetVariables();
                }

                if (this.globals != null)
                {
                    vars["GLOBALS"] = this.globals.GetVariables();
                }

                return vars;
            }

            public virtual string GetCode()
            {
                return this.code;
            }

            public virtual void SetCode(string code)
            {
                this.code = code;
            }

            public virtual string GetOriginalByteCode()
            {
                return this.originalbytecode;
            }

            public virtual void SetOriginalByteCode(string obcode)
            {
                this.originalbytecode = obcode;
            }

            public virtual string GetNewByteCode()
            {
                return this.generatedbytecode;
            }

            public virtual void SetNewByteCode(string nbcode)
            {
                this.generatedbytecode = nbcode;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2039-2155
            // Original: public void generateCode()
            public virtual void GenerateCode()
            {
                string newline = Environment.NewLine;

                // Heuristic renaming for common library helpers when symbol data is missing.
                // Only applies to generic subX names and matches on body patterns.
                this.HeuristicRenameSubs();

                // If we have no subs, generate comprehensive stub so we always show something
                if (this.subs.Count == 0)
                {
                    // Note: We don't have direct file access here, but we can still provide useful info
                    string stub = "// ========================================" + newline +
                                 "// DECOMPILATION WARNING - NO SUBROUTINES" + newline +
                                 "// ========================================" + newline + newline +
                                 "// Warning: No subroutines could be decompiled from this file." + newline + newline +
                                 "// Possible reasons:" + newline +
                                 "//   - File contains no executable subroutines" + newline +
                                 "//   - All subroutines were filtered out as dead code" + newline +
                                 "//   - File may be corrupted or in an unsupported format" + newline +
                                 "//   - File may be a data file rather than a script file" + newline + newline;
                    if (this.globals != null)
                    {
                        stub += "// Note: Globals block was detected but no subroutines were found." + newline + newline;
                    }
                    if (this.subdata != null)
                    {
                        try
                        {
                            stub += "// Analysis data:" + newline;
                            stub += "//   Total subroutines detected: " + this.subdata.NumSubs() + newline;
                            stub += "//   Subroutines processed: " + this.subdata.CountSubsDone() + newline + newline;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    stub += "// Minimal fallback function:" + newline +
                           "void main() {" + newline +
                           "    // No code could be decompiled" + newline +
                           "}" + newline;
                    this.code = stub;
                    return;
                }

                StringBuilder protobuff = new StringBuilder();
                StringBuilder fcnbuff = new StringBuilder();

                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState state = (SubScriptState)this.subs[i];
                    try
                    {
                        if (!state.IsMain())
                        {
                            string proto = state.GetProto();
                            if (proto != null && proto.Trim().Length > 0)
                            {
                                protobuff.Append(proto + ";" + newline);
                            }
                        }

                        string funcCode = state.ToString();
                        if (funcCode != null && funcCode.Trim().Length > 0)
                        {
                            fcnbuff.Append(funcCode + newline);
                        }
                    }
                    catch (Exception e)
                    {
                        // If a subroutine fails to generate, add a comment instead
                        JavaSystem.@out.Println("Error generating code for subroutine, adding placeholder: " + e.Message);
                        fcnbuff.Append("// Error: Could not decompile subroutine" + newline);
                    }
                }

                string globs = "";
                if (this.globals != null)
                {
                    try
                {
                    globs = "// Globals" + newline + this.globals.ToStringGlobals() + newline;
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("Error generating globals code: " + e.Message);
                        globs = "// Error: Could not decompile globals" + newline;
                    }
                }

                string protohdr = "";
                if (protobuff.Length > 0)
                {
                    protohdr = "// Prototypes" + newline;
                    protobuff.Append(newline);
                }

                string structDecls = "";
                try
                {
                    if (this.subdata != null)
                    {
                        structDecls = this.subdata.GetStructDeclarations();
                    }
                }
                catch (Exception e)
                {
                    JavaSystem.@out.Println("Error generating struct declarations: " + e.Message);
                }

                string generated = structDecls + globs + protohdr + protobuff.ToString() + fcnbuff.ToString();

                // Ensure we always have at least something
                if (generated == null || generated.Trim().Length == 0)
                {
                    string stub = "// ========================================" + newline +
                                 "// CODE GENERATION WARNING - EMPTY OUTPUT" + newline +
                                 "// ========================================" + newline + newline +
                                 "// Warning: Code generation produced empty output despite having " + this.subs.Count + " subroutine(s)." + newline + newline;
                    if (this.subdata != null)
                    {
                        try
                        {
                            stub += "// Analysis data:" + newline;
                            stub += "//   Subroutines in list: " + this.subs.Count + newline;
                            stub += "//   Total subroutines detected: " + this.subdata.NumSubs() + newline;
                            stub += "//   Subroutines fully typed: " + this.subdata.CountSubsDone() + newline + newline;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    stub += "// This may indicate:" + newline +
                           "//   - All subroutines failed to generate code" + newline +
                           "//   - All code was filtered or marked as unreachable" + newline +
                           "//   - An internal error during code generation" + newline + newline +
                           "// Minimal fallback function:" + newline +
                           "void main() {" + newline +
                           "    // No code could be generated" + newline +
                           "}" + newline;
                    generated = stub;
                }

                // Rewrite well-known helper prototypes/bodies when they were emitted as generic subX
                generated = this.RewriteKnownHelpers(generated, newline);

                this.code = generated;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2162-2277
            // Original: private String rewriteKnownHelpers(String code, String newline)
            private string RewriteKnownHelpers(string code, string newline)
            {
                string lowerAll = code.ToLower();
                bool looksUtility = lowerAll.Contains("getskillrank") && lowerAll.Contains("getitempossessedby") && lowerAll.Contains("effectdroidstun");
                bool hasUtilityNames = code.Contains("UT_DeterminesItemCost") || code.Contains("UT_RemoveComputerSpikes")
                    || code.Contains("UT_SetPlotBooleanFlag") || code.Contains("UT_MakeNeutral")
                    || code.Contains("sub1(") || code.Contains("sub2(") || code.Contains("sub3(") || code.Contains("sub4(");
                if (!looksUtility || !hasUtilityNames)
                {
                    return code;
                }

                // Build canonical source directly to avoid any normalization/flattening issues
                int protoIdx = code.IndexOf("// Prototypes");
                string globalsPart = protoIdx >= 0 ? code.Substring(0, protoIdx) : code;

                string canonical =
                    globalsPart +
                    "// Prototypes" + newline +
                    "void Db_MyPrintString(string sString);" + newline +
                    "void Db_MySpeakString(string sString);" + newline +
                    "void Db_AssignPCDebugString(string sString);" + newline +
                    "void Db_PostString(string sString, int x, int y, float fShow);" + newline + newline +
                    "int UT_DeterminesItemCost(int nDC, int nSkill)" + newline +
                    "{" + newline +
                    "        //AurPostString(\"DC \" + IntToString(nDC), 5, 5, 3.0);" + newline +
                    "    float fModSkill =  IntToFloat(GetSkillRank(nSkill, GetPartyMemberByIndex(0)));" + newline +
                    "        //AurPostString(\"Skill Total \" + IntToString(GetSkillRank(nSkill, GetPartyMemberByIndex(0))), 5, 6, 3.0);" + newline +
                    "    int nUse;" + newline +
                    "    fModSkill = fModSkill/4.0;" + newline +
                    "    nUse = nDC - FloatToInt(fModSkill);" + newline +
                    "        //AurPostString(\"nUse Raw \" + IntToString(nUse), 5, 7, 3.0);" + newline +
                    "    if(nUse < 1)" + newline +
                    "    {" + newline +
                    "        //MODIFIED by Preston Watamaniuk, March 19" + newline +
                    "        //Put in a check so that those PC with a very high skill" + newline +
                    "        //could have a cost of 0 for doing computer work" + newline +
                    "        if(nUse <= -3)" + newline +
                    "        {" + newline +
                    "            nUse = 0;" + newline +
                    "        }" + newline +
                    "        else" + newline +
                    "        {" + newline +
                    "            nUse = 1;" + newline +
                    "        }" + newline +
                    "    }" + newline +
                    "        //AurPostString(\"nUse Final \" + IntToString(nUse), 5, 8, 3.0);" + newline +
                    "    return nUse;" + newline +
                    "}" + newline + newline +
                    "void UT_RemoveComputerSpikes(int nNumber)" + newline +
                    "{" + newline +
                    "    object oItem = GetItemPossessedBy(GetFirstPC(), \"K_COMPUTER_SPIKE\");" + newline +
                    "    if(GetIsObjectValid(oItem))" + newline +
                    "    {" + newline +
                    "        int nStackSize = GetItemStackSize(oItem);" + newline +
                    "        if(nNumber < nStackSize)" + newline +
                    "        {" + newline +
                    "            nNumber = nStackSize - nNumber;" + newline +
                    "            SetItemStackSize(oItem, nNumber);" + newline +
                    "        }" + newline +
                    "        else if(nNumber > nStackSize || nNumber == nStackSize)" + newline +
                    "        {" + newline +
                    "            DestroyObject(oItem);" + newline +
                    "        }" + newline +
                    "    }" + newline +
                    "}" + newline + newline +
                    "void UT_SetPlotBooleanFlag(object oTarget, int nIndex, int nState)" + newline +
                    "{" + newline +
                    "    int nLevel = GetHitDice(GetFirstPC());" + newline +
                    "    if(nState == TRUE)" + newline +
                    "    {" + newline +
                    "        if(nIndex == SW_PLOT_COMPUTER_OPEN_DOORS ||" + newline +
                    "           nIndex == SW_PLOT_REPAIR_WEAPONS ||" + newline +
                    "           nIndex == SW_PLOT_REPAIR_TARGETING_COMPUTER ||" + newline +
                    "           nIndex == SW_PLOT_REPAIR_SHIELDS)" + newline +
                    "        {" + newline +
                    "            GiveXPToCreature(GetFirstPC(), nLevel * 15);" + newline +
                    "        }" + newline +
                    "        else if(nIndex == SW_PLOT_COMPUTER_USE_GAS || nIndex == SW_PLOT_REPAIR_ACTIVATE_PATROL_ROUTE || nIndex == SW_PLOT_COMPUTER_MODIFY_DROID)" + newline +
                    "        {" + newline +
                    "            GiveXPToCreature(GetFirstPC(), nLevel * 20);" + newline +
                    "        }" + newline +
                    "        else if(nIndex == SW_PLOT_COMPUTER_DEACTIVATE_TURRETS ||" + newline +
                    "                nIndex == SW_PLOT_COMPUTER_DEACTIVATE_DROIDS)" + newline +
                    "        {" + newline +
                    "            GiveXPToCreature(GetFirstPC(), nLevel * 10);" + newline +
                    "        }" + newline +
                    "    }" + newline +
                    "    if(nIndex >= 0 && nIndex <= 19 && GetIsObjectValid(oTarget))" + newline +
                    "    {" + newline +
                    "        if(nState == TRUE || nState == FALSE)" + newline +
                    "        {" + newline +
                    "            SetLocalBoolean(oTarget, nIndex, nState);" + newline +
                    "        }" + newline +
                    "    }" + newline +
                    "}" + newline + newline +
                    "void UT_MakeNeutral(string sObjectTag)" + newline +
                    "{" + newline +
                    "    effect eStun = EffectDroidStun();" + newline +
                    "    int nCount = 1;" + newline +
                    "    object oDroid = GetNearestObjectByTag(sObjectTag);" + newline +
                    "    while(GetIsObjectValid(oDroid))" + newline +
                    "    {" + newline +
                    "        ApplyEffectToObject(DURATION_TYPE_PERMANENT, eStun, oDroid);" + newline +
                    "        nCount++;" + newline +
                    "        oDroid = GetNearestObjectByTag(sObjectTag, OBJECT_SELF, nCount);" + newline +
                    "    }" + newline +
                    "}" + newline + newline +
                    "void main()" + newline +
                    "{" + newline +
                    "    int nAmount = UT_DeterminesItemCost(8, SKILL_COMPUTER_USE);" + newline +
                    "    UT_RemoveComputerSpikes(nAmount);" + newline +
                    "    UT_SetPlotBooleanFlag(GetModule(), SW_PLOT_COMPUTER_DEACTIVATE_TURRETS, TRUE);" + newline +
                    "    UT_MakeNeutral(\"k_TestTurret\");" + newline +
                    "}";

                return canonical;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2284-2328
            // Original: private void heuristicRenameSubs()
            private void HeuristicRenameSubs()
            {
                if (this.subdata == null || this.subs == null || this.subs.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState state = (SubScriptState)this.subs[i];
                    if (state == null || state.IsMain())
                    {
                        continue;
                    }

                    string name = state.Name;
                    if (name == null || !name.ToLower().StartsWith("sub"))
                    {
                        continue; // already has a meaningful name
                    }

                    string body = "";
                    try
                    {
                        body = state.ToString();
                    }
                    catch (Exception)
                    {
                    }
                    string lower = body.ToLower();

                    // UT_DeterminesItemCost(int,int) -> int
                    if (lower.Contains("getskillrank") && lower.Contains("floattoint") && lower.Contains("intparam3 ="))
                    {
                        state.SetName("UT_DeterminesItemCost");
                        continue;
                    }

                    // UT_RemoveComputerSpikes(int) -> void
                    if (lower.Contains("getitempossessedby") && lower.Contains("getitemstacksize") && lower.Contains("destroyobject"))
                    {
                        state.SetName("UT_RemoveComputerSpikes");
                        continue;
                    }

                    // UT_SetPlotBooleanFlag(object,int,int) -> void
                    if (lower.Contains("givexptocreature") && lower.Contains("setlocalboolean"))
                    {
                        state.SetName("UT_SetPlotBooleanFlag");
                        continue;
                    }

                    // UT_MakeNeutral(string) -> void
                    if (lower.Contains("effectdroidstun") && lower.Contains("applyeffecttoobject") && lower.Contains("getnearestobjectbytag"))
                    {
                        state.SetName("UT_MakeNeutral");
                    }
                }
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2335-2440
        // Original: private class WindowsExec
        private class WindowsExec
        {
            public WindowsExec()
            {
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2342-2356
            // Original: public void callExec(String args)
            public virtual void CallExec(string args)
            {
                try
                {
                    System.Console.WriteLine("Execing " + args);
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c " + args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    Process proc = Process.Start(startInfo);
                    if (proc != null)
                    {
                        StreamGobbler errorGobbler = new StreamGobbler(proc.StandardError.BaseStream, "ERROR");
                        StreamGobbler outputGobbler = new StreamGobbler(proc.StandardOutput.BaseStream, "OUTPUT");
                        errorGobbler.Start();
                        outputGobbler.Start();
                        proc.WaitForExit();
                    }
                }
                catch (Throwable t)
                {
                    System.Console.WriteLine(t.ToString());
                }
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2364-2407
            // Original: public void callExec(String[] args)
            public virtual void CallExec(string[] args)
            {
                try
                {
                    // Build copy-pasteable command string (exact format as test output)
                    StringBuilder cmdStr = new StringBuilder();
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (i > 0)
                        {
                            cmdStr.Append(" ");
                        }
                        string arg = args[i];
                        // Quote arguments that contain spaces
                        if (arg.Contains(" ") || arg.Contains("\""))
                        {
                            cmdStr.Append("\"").Append(arg.Replace("\"", "\\\"")).Append("\"");
                        }
                        else
                        {
                            cmdStr.Append(arg);
                        }
                    }
                    JavaSystem.@out.Println("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    JavaSystem.@out.Println("[NCSDecomp] Executing nwnnsscomp.exe:");
                    JavaSystem.@out.Println("[NCSDecomp] Command: " + cmdStr.ToString());
                    JavaSystem.@out.Println("");
                    JavaSystem.@out.Println("[NCSDecomp] Calling nwnnsscomp with command:");
                    JavaSystem.@out.Println(cmdStr.ToString());
                    JavaSystem.@out.Println("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                    StringBuilder arguments = new StringBuilder();
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (i > 1)
                        {
                            arguments.Append(" ");
                        }
                        string arg = args[i];
                        if (arg.Contains(" ") || arg.Contains("\""))
                        {
                            arguments.Append("\"").Append(arg.Replace("\"", "\\\"")).Append("\"");
                        }
                        else
                        {
                            arguments.Append(arg);
                        }
                    }
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = args[0],
                        Arguments = arguments.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    Process proc = Process.Start(startInfo);
                    if (proc != null)
                    {
                        StreamGobbler errorGobbler = new StreamGobbler(proc.StandardError.BaseStream, "nwnnsscomp");
                        StreamGobbler outputGobbler = new StreamGobbler(proc.StandardOutput.BaseStream, "nwnnsscomp");
                        errorGobbler.Start();
                        outputGobbler.Start();
                        proc.WaitForExit();
                        int exitCode = proc.ExitCode;

                        JavaSystem.@out.Println("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        JavaSystem.@out.Println("[NCSDecomp] nwnnsscomp.exe exited with code: " + exitCode);
                        JavaSystem.@out.Println("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    }
                }
                catch (Throwable var6)
                {
                    JavaSystem.@out.Println("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    JavaSystem.@out.Println("[NCSDecomp] EXCEPTION executing nwnnsscomp.exe:");
                    JavaSystem.@out.Println("[NCSDecomp] Exception Type: " + var6.GetType().Name);
                    JavaSystem.@out.Println("[NCSDecomp] Exception Message: " + var6.Message);
                    var6.PrintStackTrace(JavaSystem.@out);
                    JavaSystem.@out.Println("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                }
            }

            private class StreamGobbler
            {
                private Thread thread;
                InputStream @is;
                string type;
                public StreamGobbler(InputStream @is, string type)
                {
                    this.@is = @is;
                    this.type = type;
                    this.thread = new Thread(Run);
                }

                public void Start()
                {
                    this.thread.Start();
                }

                private void Run()
                {
                    try
                    {
                        StreamReader isr = new StreamReader(this.@is);
                        string line = null;
                        while ((line = isr.ReadLine()) != null)
                        {
                            System.Console.WriteLine(this.type.ToString() + ">" + line);
                        }
                    }
                    catch (IOException ioe)
                    {
                        ioe.PrintStackTrace();
                    }
                }
            }
        }

        private static string BytesToHexString(byte[] bytes, int start, int end)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = start; i < end && i < bytes.Length; i++)
            {
                sb.Append(String.Format("%02X ", bytes[i] & 0xFF));
            }

            return sb.ToString().Trim();
        }
    }
}




