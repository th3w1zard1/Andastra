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
    public class AModifyExp : ScriptNode, AExpression
    {
        private ScriptNodeNS.AVarRef varref;
        private AExpression target; // Generic target expression for edge cases
        // Generic target expression for edge cases
        private AExpression exp;
        // Generic target expression for edge cases
        public AModifyExp(ScriptNodeNS.AVarRef varref, AExpression exp)
        {
            this.VarRef(varref);
            this.Expression(exp);
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        public AModifyExp(AExpression target, AExpression exp)
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

            this.Expression(exp);
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        protected virtual void VarRef(ScriptNodeNS.AVarRef varref)
        {
            this.varref = varref;
            if (varref != null)
            {
                ((AExpression)varref).Parent(this);
            }
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        protected virtual void Expression(AExpression exp)
        {
            (this.exp = exp).Parent(this);
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        public virtual AExpression Expression()
        {
            return this.exp;
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        public virtual ScriptNodeNS.AVarRef VarRef()
        {
            return this.varref;
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        public override string ToString()
        {
            // Check for compound assignments: x = x + 5 -> x += 5
            // Use reflection to access private fields since ABinaryExp doesn't expose Left/Right/GetOp
            if (this.varref != null && this.exp is ABinaryExp binaryExp)
            {
                System.Reflection.FieldInfo leftField = typeof(ABinaryExp).GetField("left", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Reflection.FieldInfo rightField = typeof(ABinaryExp).GetField("right", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Reflection.FieldInfo opField = typeof(ABinaryExp).GetField("op", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (leftField != null && rightField != null && opField != null)
                {
                    AExpression left = (AExpression)leftField.GetValue(binaryExp);
                    AExpression right = (AExpression)rightField.GetValue(binaryExp);
                    string op = (string)opField.GetValue(binaryExp);

                    if (left is ScriptNodeNS.AVarRef leftVarRef &&
                        leftVarRef.Var() == this.varref.Var())
                    {
                        string compoundOp = GetCompoundOperator(op);
                        if (compoundOp != null)
                        {
                            return this.varref.ToString() + " " + compoundOp + " " + right.ToString();
                        }
                    }
                }
            }

            if (this.varref != null)
            {
                return this.varref.ToString() + " = " + this.exp.ToString();
            }
            else if (this.target != null)
            {
                return this.target.ToString() + " = " + this.exp.ToString();
            }

            return "/* unknown target */ = " + this.exp.ToString();
        }

        private string GetCompoundOperator(string op)
        {
            switch (op)
            {
                case "+": return "+=";
                case "-": return "-=";
                case "*": return "*=";
                case "/": return "/=";
                case "%": return "%=";
                case "<<": return "<<=";
                case ">>": return ">>=";
                case ">>>": return ">>>=";
                case "&": return "&=";
                case "|": return "|=";
                case "^": return "^=";
                default: return null;
            }
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        public virtual StackEntry Stackentry()
        {
            if (this.varref != null)
            {
                return this.varref.Var();
            }

            return null;
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        public virtual void Stackentry(StackEntry stackentry)
        {
        }

        // Generic target expression for edge cases
        // Constructor for edge cases where target is not a variable reference
        public override void Dispose()
        {
            base.Dispose();
            if (this.exp != null)
            {
                ((ScriptNode)this.exp).Dispose();
            }

            this.exp = null;
            if (this.varref != null && this.varref is IDisposable disposableVarRef)
            {
                disposableVarRef.Dispose();
            }

            this.varref = null;
        }
    }
}




