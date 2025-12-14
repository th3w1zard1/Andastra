// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
using CSharpKOTOR.Formats.NCS.NCSDecomp.AST;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AActionCmd : PCmd
    {
        private PActionCommand _actionCommand_;
        public AActionCmd()
        {
        }

        public AActionCmd(PActionCommand _actionCommand_)
        {
            this.SetActionCommand(_actionCommand_);
        }

        public override object Clone()
        {
            return new AActionCmd((PActionCommand)this.CloneNode(this._actionCommand_));
        }
        public override void Apply(Switch sw)
        {
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/AActionCmd.java:24-27
            // Original: @Override public void apply(Switch sw) { ((Analysis)sw).caseAActionCmd(this); }
            string debugMsg = "DEBUG AActionCmd.Apply: ENTERED, sw type=" + (sw != null ? sw.GetType().Name : "null");
            Console.Error.WriteLine(debugMsg);
            JavaSystem.@out.Println(debugMsg);
            try { System.IO.File.AppendAllText("debug_ast_traversal.txt", debugMsg + "\n"); } catch { }
            if (sw is Analysis.IAnalysis analysis)
            {
                debugMsg = "DEBUG AActionCmd.Apply: sw is IAnalysis, calling CaseAActionCmd";
                Console.Error.WriteLine(debugMsg);
                JavaSystem.@out.Println(debugMsg);
                try { System.IO.File.AppendAllText("debug_ast_traversal.txt", debugMsg + "\n"); } catch { }
                analysis.CaseAActionCmd(this);
                debugMsg = "DEBUG AActionCmd.Apply: CaseAActionCmd returned";
                Console.Error.WriteLine(debugMsg);
                JavaSystem.@out.Println(debugMsg);
                try { System.IO.File.AppendAllText("debug_ast_traversal.txt", debugMsg + "\n"); } catch { }
            }
            else
            {
                debugMsg = "DEBUG AActionCmd.Apply: sw is NOT IAnalysis, type=" + (sw != null ? sw.GetType().Name : "null");
                Console.Error.WriteLine(debugMsg);
                JavaSystem.@out.Println(debugMsg);
                try { System.IO.File.AppendAllText("debug_ast_traversal.txt", debugMsg + "\n"); } catch { }
            }
        }

        public PActionCommand GetActionCommand()
        {
            return this._actionCommand_;
        }

        public void SetActionCommand(PActionCommand node)
        {
            if (this._actionCommand_ != null)
            {
                this._actionCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._actionCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._actionCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._actionCommand_ == child)
            {
                this._actionCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._actionCommand_ == oldChild)
            {
                this.SetActionCommand((PActionCommand)newChild);
            }
        }
    }
}




