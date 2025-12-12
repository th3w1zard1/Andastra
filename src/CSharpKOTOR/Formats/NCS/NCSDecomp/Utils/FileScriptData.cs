using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Scriptutils = CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptutils;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
{
    public class FileScriptData
    {
        private List<Scriptutils.SubScriptState> subs;
        private Scriptutils.SubScriptState globals;
        private SubroutineAnalysisData subdata;
        //private int status;
        private string code;
        private string originalbytecode;
        private string generatedbytecode;

        public FileScriptData()
        {
            subs = new List<Scriptutils.SubScriptState>();
        }

        public void Close()
        {
            if (subs != null)
            {
                foreach (var sub in subs)
                {
                    sub.Dispose();
                }
                subs = null;
            }
            if (globals != null)
            {
                globals.Dispose();
                globals = null;
            }
            if (subdata != null && subdata is IDisposable disposable)
            {
                disposable.Dispose();
            }
            subdata = null;
            code = null;
            originalbytecode = null;
            generatedbytecode = null;
        }

        public void SetGlobals(Scriptutils.SubScriptState globals)
        {
            this.globals = globals;
        }

        public void AddSub(Scriptutils.SubScriptState sub)
        {
            subs.Add(sub);
        }

        public void SetSubdata(SubroutineAnalysisData subdata)
        {
            this.subdata = subdata;
        }

        [CanBeNull]
        public Scriptutils.SubScriptState FindSub(string name)
        {
            return subs.FirstOrDefault(state => state.Name == name);
        }

        public bool ReplaceSubName(string oldname, string newname)
        {
            var state = FindSub(oldname);
            if (state == null)
            {
                return false;
            }
            if (FindSub(newname) != null)
            {
                return false;
            }
            state.SetName(newname);
            GenerateCode();
            return true;
        }

        public override string ToString()
        {
            return code ?? "";
        }

        [CanBeNull]
        public Dictionary<string, List<object>> GetVars()
        {
            if (subs.Count == 0)
            {
                return null;
            }
            var vars = new Dictionary<string, List<object>>();
            foreach (var state in subs)
            {
                vars[state.Name] = state.GetVariables();
            }
            if (globals != null)
            {
                vars["GLOBALS"] = globals.GetVariables();
            }
            return vars;
        }

        [CanBeNull]
        public string GetCode()
        {
            return code;
        }

        public void SetCode(string code)
        {
            this.code = code;
        }

        [CanBeNull]
        public string GetOriginalByteCode()
        {
            return originalbytecode;
        }

        public void SetOriginalByteCode(string obcode)
        {
            originalbytecode = obcode;
        }

        [CanBeNull]
        public string GetNewByteCode()
        {
            return generatedbytecode;
        }

        public void SetNewByteCode(string nbcode)
        {
            generatedbytecode = nbcode;
        }

        public void GenerateCode()
        {
            try
            {
                if (subs.Count == 0)
                {
                    return;
                }
                string newline = Environment.NewLine;
                var protobuff = new List<string>();
                var fcnbuff = new List<string>();
                foreach (var state in subs)
                {
                    if (!state.IsMain())
                    {
                        protobuff.Add(state.GetProto().ToString() + ";" + newline);
                    }
                    fcnbuff.Add(state.ToString() + newline);
                }
                string globs = "";
                if (globals != null)
                {
                    try
                    {
                        string globalsStr = globals.ToStringGlobals();
                        JavaSystem.@out.Println($"DEBUG GenerateCode: globals is non-null, ToStringGlobals() returned length {globalsStr?.Length ?? 0}");
                        globs = "// Globals" + newline + globalsStr + newline;
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println($"DEBUG GenerateCode: error calling ToStringGlobals(): {e.Message}");
                        globs = "// Error: Could not decompile globals" + newline;
                    }
                }
                else
                {
                    JavaSystem.@out.Println("DEBUG GenerateCode: globals is null");
                }
                string protohdr = "";
                if (protobuff.Count > 0)
                {
                    protohdr = "// Prototypes" + newline;
                    protobuff.Add(newline);
                }
                string struct_decls = "";
                if (subdata != null)
                {
                    struct_decls = subdata.GetStructDeclarations().ToString();
                }
                code = struct_decls + globs + protohdr + string.Join("", protobuff) + string.Join("", fcnbuff);
            }
            finally
            {
            }
        }
    }
}





