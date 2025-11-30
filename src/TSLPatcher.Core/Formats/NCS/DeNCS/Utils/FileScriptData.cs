using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Utils
{
    public class FileScriptData
    {
        private List<ScriptUtils.SubScriptState> subs;
        private ScriptUtils.SubScriptState globals;
        private SubroutineAnalysisData subdata;
        private int status;
        private string code;
        private string originalbytecode;
        private string generatedbytecode;

        public FileScriptData()
        {
            subs = new List<ScriptUtils.SubScriptState>();
        }

        public void Close()
        {
            if (subs != null)
            {
                foreach (var sub in subs)
                {
                    sub.Close();
                }
                subs = null;
            }
            if (globals != null)
            {
                globals.Close();
                globals = null;
            }
            if (subdata != null)
            {
                subdata.Close();
                subdata = null;
            }
            code = null;
            originalbytecode = null;
            generatedbytecode = null;
        }

        public void SetGlobals(ScriptUtils.SubScriptState globals)
        {
            this.globals = globals;
        }

        public void AddSub(ScriptUtils.SubScriptState sub)
        {
            subs.Add(sub);
        }

        public void SetSubdata(SubroutineAnalysisData subdata)
        {
            this.subdata = subdata;
        }

        [CanBeNull]
        public ScriptUtils.SubScriptState FindSub(string name)
        {
            return subs.FirstOrDefault(state => state.GetName() == name);
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
                vars[state.GetName()] = state.GetVariables();
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
                    globs = "// Globals" + newline + globals.ToStringGlobals() + newline;
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

