// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AElse : ScriptRootNode
    {
        public AElse(int start, int end) : base(start, end)
        {
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            // Check if first child is an AIf - if so, output "else if" instead of "else { if ... }"
            if (this.children.Count > 0 && this.children[0] is AIf aif)
            {
                // Output "else if" by modifying the AIf's output
                // Remove the "if" prefix and tabs from AIf, add "else if" prefix
                string ifOutput = aif.ToString();
                // Find the "if (" part and replace with "else if ("
                int ifPos = ifOutput.IndexOf("if (");
                if (ifPos >= 0)
                {
                    // Remove leading tabs and "if", add "else if"
                    string afterIf = ifOutput.Substring(ifPos + 4); // Skip "if ("
                    buff.Append(this.tabs.ToString() + "else if (" + afterIf);
                }
                else
                {
                    // Fallback: just prepend "else "
                    buff.Append(this.tabs.ToString() + "else " + ifOutput);
                }

                // Output remaining children
                for (int i = 1; i < this.children.Count; ++i)
                {
                    buff.Append(this.children[i].ToString());
                }
            }
            else
            {
                buff.Append(this.tabs.ToString() + "else {" + this.newline);
                for (int i = 0; i < this.children.Count; ++i)
                {
                    buff.Append(this.children[i].ToString());
                }

                buff.Append(this.tabs.ToString() + "}" + this.newline);
            }
            return buff.ToString();
        }
    }
}




