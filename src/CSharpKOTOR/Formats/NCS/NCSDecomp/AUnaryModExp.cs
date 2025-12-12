// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using ScriptNodeNS = CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AUnaryModExp : ScriptNode, AExpression
    {
        private ScriptNodeNS.AVarRef varref;
        private AExpression target; // Generic target for edge cases
        // Generic target for edge cases
        private string op;
        // Generic target for edge cases
        private bool prefix;
        // Generic target for edge cases
        private StackEntry stackentry;
        // Generic target for edge cases
        public AUnaryModExp(ScriptNodeNS.AVarRef varref, string op, bool prefix)
        {
            this.VarRef(varref);
            this.op = op;
            this.prefix = prefix;
        }

        // Generic target for edge cases
        // Constructor for edge cases where target is not a variable reference
        public AUnaryModExp(AExpression target, string op, bool prefix)
        {
            if (target is ScriptNodeNS.AVarRef)
            {
                this.VarRef((ScriptNodeNS.AVarRef)target);
            }
            else
            {
                this.target = target;
                if (target != null)
                {
                    ((ScriptNode)target).Parent(this);
                }
            }

            this.op = op;
            this.prefix = prefix;
        }

        // Generic target for edge cases
        // Constructor for edge cases where target is not a variable reference
        protected virtual void VarRef(ScriptNodeNS.AVarRef varref)
        {
            this.varref = varref;
            if (varref != null)
            {
                // Use AExpression.Parent() which accepts Scriptnode.ScriptNode
                ((AExpression)varref).Parent(this);
            }
        }

        // Generic target for edge cases
        // Constructor for edge cases where target is not a variable reference
        public override string ToString()
        {
            string targetStr = this.varref != null ? this.varref.ToString() : (this.target != null ? this.target.ToString() : "/* unknown */");
            if (this.prefix)
            {
                return "(" + this.op + targetStr + ")";
            }

            return "(" + targetStr + this.op + ")";
        }

        // Generic target for edge cases
        // Constructor for edge cases where target is not a variable reference
        public virtual StackEntry Stackentry()
        {
            return this.stackentry;
        }

        // Generic target for edge cases
        // Constructor for edge cases where target is not a variable reference
        public virtual void Stackentry(StackEntry stackentry)
        {
            this.stackentry = stackentry;
        }

        // Generic target for edge cases
        // Constructor for edge cases where target is not a variable reference
        public override void Dispose()
        {
            base.Dispose();
            if (this.varref != null && this.varref is IDisposable disposableVarRef)
            {
                disposableVarRef.Dispose();
            }

            this.varref = null;
            if (this.stackentry != null && this.stackentry is IDisposable disposableStackEntry)
            {
                disposableStackEntry.Dispose();
            }

            this.stackentry = null;
        }
    }
}




