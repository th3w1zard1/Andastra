//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public class ActionsData
    {
        private List<object> actions;
        private StreamReader actionsreader;
        public ActionsData(StreamReader actionsreader)
        {
            this.actionsreader = actionsreader;
            this.ReadActions();
        }

        public virtual string GetAction(int index)
        {
            try
            {
                Action action = (Action)this.actions[index];
                return action.ToString();
            }
            catch (IndexOutOfRangeException)
            {
                throw new Exception("Invalid action call: action " + index.ToString());
            }
        }

        private void ReadActions()
        {
            Pattern p = Pattern.Compile("^\\s*(\\w+)\\s+(\\w+)\\s*\\((.*)\\).*");
            this.actions = new List<object>(877);
            while (true)
            {
                string str;
                while ((str = this.actionsreader.ReadLine()) != null)
                {
                    if (str.StartsWith("// 0"))
                    {
                        while ((str = this.actionsreader.ReadLine()) != null)
                        {
                            if (str.StartsWith("//"))
                            {
                                continue;
                            }

                            if (str.Length == 0)
                            {
                                continue;
                            }

                            Matcher m = p.Matcher(str);
                            if (!m.Matches())
                            {
                                continue;
                            }

                            this.actions.Add(new Action(m.Group(1), m.Group(2), m.Group(3)));
                        }

                        ((JavaPrintStream)JavaSystem.@out).Println("read actions.  There were " + this.actions.Count.ToString());
                        return;
                    }
                }

                continue;
            }
        }

        public virtual UtilsType GetReturnType(int index)
        {
            return ((Action)this.actions[index]).ReturnType();
        }

        public virtual string GetName(int index)
        {
            return ((Action)this.actions[index]).Name();
        }

        public virtual List<object> GetParamTypes(int index)
        {
            return ((Action)this.actions[index]).Params();
        }

        public class Action
        {
            private string name;
            private UtilsType returntype;
            private int paramsize;
            private List<object> paramList;
            public Action(string type, string name, string @params)
            {
                this.name = name;
                this.returntype = UtilsType.ParseType(type);
                this.paramList = new List<object>();
                this.paramsize = 0;
                Pattern p = Pattern.Compile("\\s*(\\w+)\\s+\\w+(\\s*=\\s*\\S+)?\\s*");
                String[] tokens = @params.Split(",");
                for (int i = 0; i < tokens.Length; ++i)
                {
                    Matcher m = p.Matcher(tokens[i]);
                    if (m.Matches())
                    {
                        this.paramList.Add(new UtilsType(m.Group(1)));
                        this.paramsize += UtilsType.TypeSize(m.Group(1));
                    }
                }
            }

            public override string ToString()
            {
                return "\"" + this.name + "\" " + this.returntype.ToValueString() + " " + this.paramsize.ToString();
            }

            public virtual List<object> Params()
            {
                return this.paramList;
            }

            public virtual UtilsType ReturnType()
            {
                return this.returntype;
            }

            public virtual int Paramsize()
            {
                return this.paramsize;
            }

            public virtual string Name()
            {
                return this.name;
            }
        }
    }
}




