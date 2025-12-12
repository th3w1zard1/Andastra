// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptutils
{
    public class CleanupPass
    {
        private ASub root;
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private SubScriptState state;
        public CleanupPass(ASub root, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, SubScriptState state)
        {
            this.root = root;
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.state = state;
        }

        public virtual void Apply()
        {
            this.CheckSubCodeBlock();
            this.Apply(this.root);
        }

        public virtual void Done()
        {
            this.root = null;
            this.nodedata = null;
            this.subdata = null;
            this.state = null;
        }

        private void CheckSubCodeBlock()
        {
            try
            {
                if (this.root.GetChildren().Count == 1 && typeof(ACodeBlock).IsInstanceOfType(this.root.GetLastChild()))
                {
                    ACodeBlock block = (ACodeBlock)this.root.RemoveLastChild();
                    List<object> children = block.RemoveChildren();
                    this.root.AddChildren(children);
                }
            }
            finally
            {
                // Variables are disposed in finally block
            }
        }

        private void Apply(ScriptRootNode rootnode)
        {
            try
            {
                LinkedList childrenListLocal = rootnode.GetChildren();
                ListIterator itLocal = childrenListLocal.ListIterator();
                while (itLocal.HasNext())
                {
                    Scriptnode.ScriptNode node1Local = (Scriptnode.ScriptNode)itLocal.Next();
                    if (typeof(AVarDecl).IsInstanceOfType(node1Local))
                    {
                        Variable var = ((AVarDecl)node1Local).Var();
                        if (var != null && var.IsStruct())
                        {
                            VarStruct @struct = ((AVarDecl)node1Local).Var().Varstruct();
                            AVarDecl structdec = new AVarDecl(@struct);
                            if (itLocal.HasNext())
                            {
                                node1Local = (Scriptnode.ScriptNode)itLocal.Next();
                            }
                            else
                            {
                                node1Local = null;
                            }

                            while (typeof(AVarDecl).IsInstanceOfType(node1Local) && @struct.Equals(((AVarDecl)node1Local).Var().Varstruct()))
                            {
                                itLocal.Remove();
                                node1Local.Parent(null);
                                if (itLocal.HasNext())
                                {
                                    node1Local = (Scriptnode.ScriptNode)itLocal.Next();
                                }
                                else
                                {
                                    node1Local = null;
                                }
                            }

                            itLocal.Previous();
                            if (node1Local != null)
                            {
                                itLocal.Previous();
                            }

                            node1Local = (Scriptnode.ScriptNode)itLocal.Next();
                            structdec.Parent(node1Local.Parent());
                            itLocal.Set(structdec);
                            node1Local = structdec;
                        }
                    }

                    if (typeof(AVarDecl).IsInstanceOfType(node1Local) && itLocal.HasNext())
                    {
                        Scriptnode.ScriptNode node2Local = (Scriptnode.ScriptNode)itLocal.Next();
                        itLocal.Previous();
                        if (typeof(AExpressionStatement).IsInstanceOfType(node2Local) && typeof(AModifyExp).IsInstanceOfType(((AExpressionStatement)node2Local).Exp()))
                        {
                            AModifyExp modexp = (AModifyExp)((AExpressionStatement)node2Local).Exp();

                            // Check if varRef is not null before accessing it
                            if (modexp.VarRef() != null && ((AVarDecl)node1Local).Var() == modexp.VarRef().Var())
                            {
                                itLocal.Remove();
                                node2Local.Parent(null);
                                ((AVarDecl)node1Local).InitializeExp(modexp.Expression());
                            }
                        }

                        itLocal.Previous();
                        itLocal.Next();
                    }

                    if (this.IsDanglingExpression(node1Local))
                    {
                        AExpressionStatement expstm = new AExpressionStatement((AExpression)node1Local);
                        expstm.Parent(rootnode);
                        itLocal.Set(expstm);
                    }

                    itLocal.Previous();
                    itLocal.Next();
                    if (typeof(ScriptRootNode).IsInstanceOfType(node1Local))
                    {
                        this.Apply((ScriptRootNode)node1Local);
                    }

                    ASwitchCase acase = null;
                    if (typeof(ASwitch).IsInstanceOfType(node1Local))
                    {
                        while ((acase = ((ASwitch)node1Local).GetNextCase(acase)) != null)
                        {
                            this.Apply(acase);
                        }
                    }
                }
            }
            finally
            {
                // Variables are disposed in finally block
            }
        }

        private bool IsDanglingExpression(Scriptnode.ScriptNode node)
        {
            return node is AExpression;
        }
    }
}




