//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/ASubroutine.java:9-26
    // Original: public final class ASubroutine extends PSubroutine { ... }
    public sealed class ASubroutine : PSubroutine
    {
        private PCommandBlock _commandBlock_;
        private PReturn _return_;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/ASubroutine.java:13-14
        // Original: public ASubroutine() { }
        public ASubroutine()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/ASubroutine.java:16-19
        // Original: public ASubroutine(PCommandBlock _commandBlock_, PReturn _return_) { ... }
        public ASubroutine(PCommandBlock _commandBlock_, PReturn _return_)
        {
            this.SetCommandBlock(_commandBlock_);
            this.SetReturn(_return_);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/ASubroutine.java:21-26
        // Original: @Override public ASubroutine clone() { ... }
        public override object Clone()
        {
            PCommandBlock clonedCommandBlock = this._commandBlock_ != null ? (PCommandBlock)this._commandBlock_.Clone() : null;
            PReturn clonedReturn = this._return_ != null ? (PReturn)this._return_.Clone() : null;
            return new ASubroutine(clonedCommandBlock, clonedReturn);
        }
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/ASubroutine.java:28-31
        // Original: @Override public void apply(Switch sw) { ((Analysis)sw).caseASubroutine(this); }
        public override void Apply(Switch sw)
        {
            ((Analysis)sw).CaseASubroutine(this);
        }

        public PCommandBlock GetCommandBlock()
        {
            return this._commandBlock_;
        }

        public void SetCommandBlock(PCommandBlock node)
        {
            if (this._commandBlock_ != null)
            {
                this._commandBlock_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._commandBlock_ = node;
        }

        public PReturn GetReturn()
        {
            return this._return_;
        }

        public void SetReturn(PReturn node)
        {
            if (this._return_ != null)
            {
                this._return_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._return_ = node;
        }

        public override string ToString()
        {
            return this.ToString(this._commandBlock_) + this.ToString(this._return_);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/ASubroutine.java:78-85
        // Original: @Override void removeChild(Node child) { ... }
        public override void RemoveChild(Node child)
        {
            if (this._commandBlock_ == child)
            {
                this._commandBlock_ = null;
            }
            else if (this._return_ == child)
            {
                this._return_ = null;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/ASubroutine.java:87-94
        // Original: @Override void replaceChild(Node oldChild, Node newChild) { ... }
        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._commandBlock_ == oldChild)
            {
                this.SetCommandBlock((PCommandBlock)newChild);
            }
            else if (this._return_ == oldChild)
            {
                this.SetReturn((PReturn)newChild);
            }
        }
    }
}




