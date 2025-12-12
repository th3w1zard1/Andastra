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
    public class FileDecompiler
    {
        public static readonly int FAILURE = 0;
        public static readonly int SUCCESS = 1;
        public static readonly int PARTIAL_COMPILE = 2;
        public static readonly int PARTIAL_COMPARE = 3;
        public static readonly string GLOBAL_SUB_NAME = "GLOBALS";
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

        public virtual int Decompile(File file)
        {
            Utils.FileScriptData data = null;
            if (this.filedata.ContainsKey(file))
            {
                data = (Utils.FileScriptData)this.filedata[file];
            }

            if (data == null)
            {
                JavaSystem.@out.Println("\n---> starting decompilation: " + file.Name + " <---");
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
                    JavaSystem.@out.Println("Failed to read NCS file: " + ex.Message);
                    return FAILURE;
                }

                if (ncs == null)
                {
                    return FAILURE;
                }

                data = this.DecompileNcsObject(ncs);
                if (data == null)
                {
                    return FAILURE;
                }

                this.filedata[file] = data;
            }

            data.GenerateCode();
            return this.CompileAndCompare(file, data.GetCode(), data);
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
                if (kvp.Value is FileScriptData fileData)
                {
                    fileData.Dispose();
                }
            }

            this.filedata.Clear();
            GC.Collect();
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

        private string ComparePcodeFiles(File file1, File file2)
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
                sub = subdata.GetGlobalsSub();
                if (sub != null)
                {
                    doglobs = new DoGlobalVars(nodedata, subdata);
                    sub.Apply(doglobs);
                    cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                    cleanpass.Apply();
                    subdata.SetGlobalStack(doglobs.GetStack());
                    subdata.GlobalState(doglobs.GetState());
                    cleanpass.Done();
                }

                bool alldone = false;
                bool onedone = true;
                for (int pass = 1; !alldone && (onedone || pass < 5); ++pass)
                {
                    subs = subdata.GetSubroutines();
                    alldone = true;
                    onedone = false;
                    while (subs.HasNext())
                    {
                        sub = (ASubroutine)subs.Next();
                        if (!subdata.IsPrototyped(nodedata.GetPos(sub), true))
                        {
                            sub.Apply(new SubroutinePathFinder(subdata.GetState(sub), nodedata, subdata, pass));
                            if (subdata.IsBeingPrototyped(nodedata.GetPos(sub)))
                            {
                                dotypes = new DoTypes(subdata.GetState(sub), nodedata, subdata, this.actions, true);
                                sub.Apply(dotypes);
                                dotypes.Done();
                                onedone = true;
                            }
                            else
                            {
                                alldone = false;
                            }
                        }
                    }
                }

                if (!alldone)
                {
                    JavaSystem.@out.Println("Unable to do initial prototype of all subroutines after 5 passes.");
                    // Don't call PrintStates() here as it can cause infinite output spam in loops
                    return null;
                }

                dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                mainsub.Apply(dotypes);
                dotypes.AssertStack();
                dotypes.Done();
                alldone = (subdata.CountSubsDone() == subdata.NumSubs());
                onedone = true;
                for (int donecount = subdata.CountSubsDone(), loopcount = 0; !alldone && onedone && loopcount < 1000; alldone = (subdata.CountSubsDone() == subdata.NumSubs()), onedone = (onedone || subdata.CountSubsDone() > donecount), donecount = subdata.CountSubsDone(), ++loopcount)
                {
                    onedone = false;
                    subs = subdata.GetSubroutines();
                    while (subs.HasNext())
                    {
                        sub = (ASubroutine)subs.Next();
                        dotypes = new DoTypes(subdata.GetState(sub), nodedata, subdata, this.actions, false);
                        sub.Apply(dotypes);
                        dotypes.Done();
                    }

                    dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                    mainsub.Apply(dotypes);
                    dotypes.Done();
                }

                if (!alldone)
                {
                    JavaSystem.@out.Println("Unable to do final prototype of all subroutines after 1000 iterations.");
                    // Don't call PrintStates() here as it can cause infinite output spam
                    return null;
                }

                dotypes = null;
                nodedata.ClearProtoData();
                subs = subdata.GetSubroutines();
                while (subs.HasNext())
                {
                    sub = (ASubroutine)subs.Next();
                    mainpass = new MainPass(subdata.GetState(sub), nodedata, subdata, this.actions);
                    sub.Apply(mainpass);
                    cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                    cleanpass.Apply();
                    data.AddSub(mainpass.GetState());
                    mainpass.Done();
                    cleanpass.Done();
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
                if (doglobs != null)
                {
                    cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                    cleanpass.Apply();
                    data.SetGlobals(doglobs.GetState());
                    doglobs.Done();
                    cleanpass.Done();
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

            public virtual void Dispose()
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

            public virtual void GenerateCode()
            {
                if (this.subs.Count == 0)
                {
                    return;
                }

                string newline = Environment.NewLine;
                StringBuilder protobuff = new StringBuilder();
                StringBuilder fcnbuff = new StringBuilder();
                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState subState = (SubScriptState)this.subs[i];
                    if (!subState.IsMain())
                    {
                        protobuff.Append(subState.GetProto() + ";" + newline);
                    }

                    fcnbuff.Append(subState.ToString() + newline);
                }

                string globs = "";
                if (this.globals != null)
                {
                    globs = "// Globals" + newline + this.globals.ToStringGlobals() + newline;
                }

                string protohdr = "";
                if (protobuff.Length > 0)
                {
                    protohdr = "// Prototypes" + newline;
                    protobuff.Append(newline);
                }

                this.code = this.subdata.GetStructDeclarations() + globs + protohdr + protobuff.ToString() + fcnbuff.ToString();
            }
        }

        private class WindowsExec
        {
            public WindowsExec()
            {
            }

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




