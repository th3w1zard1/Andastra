// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:86-1759
// Original: public class SubScriptState
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using CSharpKOTOR.Formats.NCS.NCSDecomp.AST;
using AVarRef = CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode.AVarRef;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptutils
{
    public class SubScriptState
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:87-105
        // Original: private static final byte STATE_DONE = -1; private static final byte STATE_NORMAL = 0; private static final byte STATE_INMOD = 1; private static final byte STATE_INACTIONARG = 2; private static final byte STATE_WHILECOND = 3; private static final byte STATE_SWITCHCASES = 4; private static final byte STATE_INPREFIXSTACK = 5; private ASub root; private ScriptRootNode current; private byte state; private NodeAnalysisData nodedata; private SubroutineAnalysisData subdata; private ActionsData actions; private LocalVarStack stack; private String varprefix; private Hashtable<Variable, AVarDecl> vardecs; private Hashtable<Type, Integer> varcounts; private Hashtable<String, Integer> varnames; private boolean preferSwitches;
        private const sbyte STATE_DONE = -1;
        private const sbyte STATE_NORMAL = 0;
        private const sbyte STATE_INMOD = 1;
        private const sbyte STATE_INACTIONARG = 2;
        private const sbyte STATE_WHILECOND = 3;
        private const sbyte STATE_SWITCHCASES = 4;
        private const sbyte STATE_INPREFIXSTACK = 5;
        private ScriptNode.ASub root;
        private ScriptRootNode current;
        private sbyte state;
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private ActionsData actions;
        private LocalVarStack stack;
        private string varprefix;
        private HashMap vardecs;
        private HashMap varcounts;
        private HashMap varnames;
        private bool preferSwitches;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:107-122
        // Original: public SubScriptState(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, LocalVarStack stack, SubroutineState protostate, ActionsData actions, boolean preferSwitches)
        public SubScriptState(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, LocalVarStack stack, SubroutineState protostate, ActionsData actions, bool preferSwitches)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.state = 0;
            this.vardecs = new HashMap();
            this.stack = stack;
            this.varcounts = new HashMap();
            this.varprefix = "";
            UtilsType type = protostate.Type();
            byte id = protostate.GetId();
            this.root = new ScriptNode.ASub(type, id, this.GetParams(protostate.GetParamCount()), protostate.GetStart(), protostate.GetEnd());
            this.current = this.root;
            this.varnames = new HashMap();
            this.actions = actions;
            this.preferSwitches = preferSwitches;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:124-136
        // Original: public SubScriptState(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, LocalVarStack stack, boolean preferSwitches)
        public SubScriptState(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, LocalVarStack stack, bool preferSwitches)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.state = 0;
            this.vardecs = new HashMap();
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:129
            // Original: this.root = new ASub(0, 0);
            // For globals, use a large end value so CheckEnd never matches and moves away from root
            // This ensures that CheckEnd won't think we're at the end of the root and try to move up
            this.root = new ScriptNode.ASub(0, null, null, 0, int.MaxValue);
            this.current = this.root;
            this.stack = stack;
            this.varcounts = new HashMap();
            this.varprefix = "";
            this.varnames = new HashMap();
            this.preferSwitches = preferSwitches;
        }

        public virtual void SetVarPrefix(string prefix)
        {
            this.varprefix = prefix;
        }

        public virtual void SetStack(LocalVarStack stack)
        {
            this.stack = stack;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:146-164
        // Original: public void parseDone()
        public virtual void ParseDone()
        {
            this.nodedata = null;
            this.subdata = null;
            if (this.stack != null)
            {
                this.stack.DoneParse();
            }

            this.stack = null;
            if (this.vardecs != null)
            {
                foreach (object key in this.vardecs.Keys)
                {
                    Variable var = (Variable)key;
                    var.DoneParse();
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:166-195
        // Original: public void close()
        public virtual void Close()
        {
            if (this.vardecs != null)
            {
                foreach (object key in this.vardecs.Keys)
                {
                    Variable var = (Variable)key;
                    var.Close();
                }

                this.vardecs = null;
            }

            this.varcounts = null;
            this.varnames = null;
            if (this.root != null)
            {
                this.root.Close();
            }

            this.current = null;
            this.root = null;
            this.nodedata = null;
            this.subdata = null;
            this.actions = null;
            if (this.stack != null)
            {
                this.stack.Close();
                this.stack = null;
            }
        }

        public override string ToString()
        {
            return this.root.ToString();
        }

        public virtual string ToStringGlobals()
        {
            return this.root.GetBody();
        }

        public virtual string GetProto()
        {
            return this.root.GetHeader();
        }

        public virtual ScriptNode.ASub GetRoot()
        {
            return this.root;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:214-216
        // Original: public String getName() { return this.root.name(); }
        public virtual string GetName()
        {
            return this.root.GetName();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:218-220
        // Original: public void setName(String name)
        public virtual void SetName(string name)
        {
            this.root.SetName(name);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:222-270
        // Original: public Vector<Variable> getVariables()
        public virtual Vector GetVariables()
        {
            Vector vars = new Vector(this.vardecs.Keys);
            SortedSet<object> varstructs = new SortedSet<object>();
            List<object> toRemove = new List<object>();
            IEnumerator<object> it = vars.Iterator();
            while (it.HasNext())
            {
                Variable var = (Variable)it.Next();
                if (var.IsStruct())
                {
                    varstructs.Add(var.Varstruct());
                    toRemove.Add(var);
                }
            }
            foreach (var var in toRemove)
            {
                vars.Remove(var);
            }

            vars.AddAll(varstructs);
            vars.AddAll(this.root.GetParamVars());
            return vars;
        }

        public virtual void IsMain(bool ismain)
        {
            this.root.SetIsMain(ismain);
        }

        public virtual bool IsMain()
        {
            return this.root.IsMain();
        }

        private void AssertState(Node node)
        {
            if (this.state == 0)
            {
                return;
            }

            if (this.state == 2 && !typeof(AJumpCommand).IsInstanceOfType(node))
            {
                throw new Exception("In action arg, expected JUMP at node " + node);
            }

            if (this.state == -1)
            {
                throw new Exception("In DONE state, no more nodes expected at node " + node);
            }

            if (this.state == 5 && !typeof(ACopyTopSpCommand).IsInstanceOfType(node))
            {
                throw new Exception("In prefix stack op state, expected CPTOPSP at node " + node);
            }
        }

        private void CheckStart(Node node)
        {
            this.AssertState(node);
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:292-314
            // Original: private void checkStart(Node node) { this.assertState(node); ... if (this.current.hasChildren()) { ... } }
            // Note: The vendor code doesn't check for null current - it assumes current is never null
            // If current is null, we need to reset it to root (this can happen for globals)
            if (this.current == null)
            {
                this.current = this.root;
            }
            // For globals, prevent CheckStart from moving away from root
            // The root has end = int.MaxValue, so CheckEnd won't match, but CheckStart might move to a switch case
            // For globals, we want to keep current at root to add variable declarations
            if (this.current == this.root && this.root.GetEnd() == int.MaxValue)
            {
                // This is the globals root - don't move away from it
                return;
            }
            if (this.current.HasChildren())
            {
                ScriptNode.ScriptNode lastNode = this.current.GetLastChild();
                if (typeof(ScriptNode.ASwitch).IsInstanceOfType(lastNode) && this.nodedata.GetPos(node) == ((ScriptNode.ASwitch)lastNode).GetFirstCaseStart())
                {
                    this.current = ((ScriptNode.ASwitch)lastNode).GetFirstCase();
                }
            }
        }

        private void CheckEnd(Node node)
        {
            while (this.current != null)
            {
                if (this.nodedata.GetPos(node) != this.current.GetEnd())
                {
                    return;
                }

                if (typeof(ASwitchCase).IsInstanceOfType(this.current))
                {
                    ASwitchCase nextCase = ((ScriptNode.ASwitch)this.current.Parent()).GetNextCase((ASwitchCase)this.current);
                    if (nextCase != null)
                    {
                        this.current = nextCase;
                    }
                    else
                    {
                        this.current = (ScriptRootNode)this.current.Parent().Parent();
                    }

                    nextCase = null;
                    return;
                }

                if (typeof(AIf).IsInstanceOfType(this.current))
                {
                    Node dest = this.nodedata.GetDestination(node);
                    if (dest == null)
                    {
                        return;
                    }

                    if (this.nodedata.GetPos(dest) != this.current.GetEnd() + 6)
                    {
                        AElse aelse = new AElse(this.current.GetEnd() + 6, this.nodedata.GetPos(NodeUtils.GetPreviousCommand(dest, this.nodedata)));
                        (this.current = (ScriptRootNode)this.current.Parent()).AddChild(aelse);
                        this.current = aelse;
                        aelse = null;
                        dest = null;
                        return;
                    }
                }

                if (typeof(ADoLoop).IsInstanceOfType(this.current))
                {
                    this.TransformEndDoLoop();
                }

                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:443-445
                // Original: ScriptRootNode newCurrent = (ScriptRootNode) this.current.parent(); this.current = newCurrent;
                // For globals, if current is the root (ASub), parent() returns null, so current becomes null and loop exits
                // This is expected behavior - the root has no parent, so we can't move up
                // CheckStart will reset current to root if it becomes null
                ScriptRootNode newCurrent = (ScriptRootNode)this.current.Parent();
                this.current = newCurrent;
            }

            this.state = STATE_DONE;
        }

        public virtual bool InActionArg()
        {
            return this.state == 2;
        }

        public virtual void TransformPlaceholderVariableRemoved(Variable var)
        {
            ScriptNode.AVarDecl vardec = (ScriptNode.AVarDecl)this.vardecs[var];
            if (vardec != null && vardec.IsFcnReturn())
            {
                object exp = vardec.GetExp();
                ScriptRootNode parent = (ScriptRootNode)vardec.Parent();
                if (exp != null)
                {
                    parent.ReplaceChild(vardec, (ScriptNode.ScriptNode)exp);
                }
                else
                {
                    parent.RemoveChild(vardec);
                }

                parent = null;
                this.vardecs.Remove(var);
            }

            vardec = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:476-483
        // Original: public void emitError(Node node, int pos) { String message = "ERROR: failed to decompile statement"; if (pos >= 0) { message = message + " at " + pos; } this.current.addChild(new AErrorComment(message)); }
        public virtual void EmitError(Node node, int pos)
        {
            string message = "ERROR: failed to decompile statement";
            if (pos >= 0)
            {
                message = message + " at " + pos;
            }

            this.current.AddChild(new AErrorComment(message));
        }

        private bool RemovingSwitchVar(List<object> vars, Node node)
        {
            if (vars.Count == 1 && this.current.HasChildren() && typeof(ScriptNode.ASwitch).IsInstanceOfType(this.current.GetLastChild()))
            {
                AExpression exp = ((ScriptNode.ASwitch)this.current.GetLastChild()).GetSwitchExp();
                return typeof(ScriptNode.AVarRef).IsInstanceOfType(exp) && ((ScriptNode.AVarRef)exp).Var().Equals(vars[0]);
            }

            return false;
        }

        public virtual void TransformMoveSPVariablesRemoved(List<object> vars, Node node)
        {
            if (this.AtLastCommand(node) && this.CurrentContainsVars(vars))
            {
                return;
            }

            if (vars.Count == 0)
            {
                return;
            }

            if (this.IsMiddleOfReturn(node))
            {
                return;
            }

            if (this.RemovingSwitchVar(vars, node))
            {
                return;
            }

            if (!this.CurrentContainsVars(vars))
            {
                return;
            }

            int earliestdec = -1;
            for (int i = 0; i < vars.Count; ++i)
            {
                Variable var = (Variable)vars[i];
                ScriptNode.AVarDecl vardec = (ScriptNode.AVarDecl)this.vardecs[var];
                earliestdec = this.GetEarlierDec(vardec, earliestdec);
            }

            if (earliestdec != -1)
            {
                Node prev = NodeUtils.GetPreviousCommand(node, this.nodedata);
                ACodeBlock block = new ACodeBlock(-1, this.nodedata.GetPos(prev));
                List<ScriptNode.ScriptNode> children = this.current.RemoveChildren(earliestdec);
                this.current.AddChild(block);
                block.AddChildren(children);
                children = null;
                block = null;
                prev = null;
            }
        }

        public virtual void TransformEndDoLoop()
        {
            AExpression cond = null;
            try
            {
                if (this.current.HasChildren())
                {
                    cond = this.RemoveLastExp(false);
                }
            }
            catch (Exception)
            {
                cond = null;
            }

            if (cond != null)
            {
                ((ADoLoop)this.current).Condition(cond);
            }
            else
            {
                AConst constTrue = new AConst(Const.NewConst(new UtilsType((byte)3), Long.ParseLong("1")));
                ((ADoLoop)this.current).Condition(constTrue);
            }
        }

        public virtual void TransformOriginFound(Node destination, Node origin)
        {
            ScriptNode.AControlLoop loop = this.GetLoop(destination, origin);
            this.current.AddChild(loop);
            this.current = loop;
            if (typeof(AWhileLoop).IsInstanceOfType(loop))
            {
                this.state = 3;
            }

            loop = null;
        }

        public virtual void TransformLogOrExtraJump(AConditionalJumpCommand node)
        {
            this.RemoveLastExp(true);
        }

        public virtual void TransformConditionalJump(AConditionalJumpCommand node)
        {
            this.CheckStart(node);
            if (this.state == 3)
            {
                ((AWhileLoop)this.current).Condition(this.RemoveLastExp(false));
                this.state = 0;
            }
            else if (!NodeUtils.IsJz(node))
            {
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:551-620
                // Original: Equality comparison - prefer switch when preferSwitches is enabled
                if (this.state != 4)
                {
                    AConditionalExp cond = (AConditionalExp)this.RemoveLastExp(true);
                    // When preferSwitches is enabled, be more aggressive about creating switches
                    // Check if we can add to an existing switch or create a new one
                    bool canCreateSwitch = typeof(AConst).IsInstanceOfType(cond.GetRight());
                    ScriptNode.ASwitch existingSwitch = null;

                    // Check if we can continue an existing switch when preferSwitches is enabled
                    if (this.preferSwitches && this.current.HasChildren())
                    {
                        ScriptNode.ScriptNode last = this.current.GetLastChild();
                        if (typeof(ScriptNode.ASwitch).IsInstanceOfType(last))
                        {
                            existingSwitch = (ScriptNode.ASwitch)last;
                            // Verify the switch expression matches
                            AExpression switchExp = existingSwitch.GetSwitchExp();
                            if (typeof(AVarRef).IsInstanceOfType(cond.GetLeft()) && typeof(AVarRef).IsInstanceOfType(switchExp)
                                && ((AVarRef)cond.GetLeft()).Var().Equals(((AVarRef)switchExp).Var()))
                            {
                                // Can continue existing switch
                                ScriptNode.ASwitchCase aprevcase = existingSwitch.GetLastCase();
                                if (aprevcase != null)
                                {
                                    aprevcase.End(this.nodedata.GetPos(NodeUtils.GetPreviousCommand(this.nodedata.GetDestination(node), this.nodedata)));
                                }
                                ScriptNode.ASwitchCase acasex = new ScriptNode.ASwitchCase(this.nodedata.GetPos(this.nodedata.GetDestination(node)), (ScriptNode.AConst)(object)(ScriptNode.AConst)cond.GetRight());
                                existingSwitch.AddCase(acasex);
                                this.state = 4;
                                this.CheckEnd(node);
                                return;
                            }
                        }
                    }

                    if (canCreateSwitch)
                    {
                                ScriptNode.ASwitch aswitch = null;
                        ScriptNode.ASwitchCase acase = new ScriptNode.ASwitchCase(this.nodedata.GetPos(this.nodedata.GetDestination(node)), (ScriptNode.AConst)(object)(ScriptNode.AConst)cond.GetRight());
                        if (this.current.HasChildren())
                        {
                            ScriptNode.ScriptNode last = this.current.GetLastChild();
                            if (typeof(ScriptNode.AVarRef).IsInstanceOfType(last) && typeof(ScriptNode.AVarRef).IsInstanceOfType(cond.GetLeft())
                                && ((ScriptNode.AVarRef)(object)last).Var().Equals(((ScriptNode.AVarRef)cond.GetLeft()).Var()))
                            {
                                ScriptNode.AExpression exp = this.RemoveLastExp(false);
                                if (exp is AVarRef varref)
                                {
                                    aswitch = new ScriptNode.ASwitch(this.nodedata.GetPos(node), varref);
                                }
                                else
                                {
                                    aswitch = new ScriptNode.ASwitch(this.nodedata.GetPos(node), cond.GetLeft());
                                }
                            }
                        }

                        if (aswitch == null)
                        {
                            aswitch = new ScriptNode.ASwitch(this.nodedata.GetPos(node), cond.GetLeft());
                        }

                        this.current.AddChild(aswitch);
                        aswitch.AddCase(acase);
                        this.state = 4;
                    }
                    else
                    {
                        // Fall back to if statement if we can't create a switch
                        AIf aif = new AIf(this.nodedata.GetPos(node), this.nodedata.GetPos(this.nodedata.GetDestination(node)) - 6, cond);
                        this.current.AddChild(aif);
                        this.current = aif;
                    }
                }
                else
                {
                    AConditionalExp condx = (AConditionalExp)this.RemoveLastExp(true);
                    ScriptNode.ASwitch aswitchx = (ScriptNode.ASwitch)this.current.GetLastChild();
                    ScriptNode.ASwitchCase aprevcase = aswitchx.GetLastCase();
                    aprevcase.End(this.nodedata.GetPos(NodeUtils.GetPreviousCommand(this.nodedata.GetDestination(node), this.nodedata)));
                    ScriptNode.ASwitchCase acasex = new ScriptNode.ASwitchCase(this.nodedata.GetPos(this.nodedata.GetDestination(node)), (ScriptNode.AConst)(object)(ScriptNode.AConst)condx.GetRight());
                    aswitchx.AddCase(acasex);
                }
            }
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:621-635
            // Original: else if (AIf.class.isInstance(this.current) && this.isModifyConditional() && this.state != 4)
            else if (typeof(AIf).IsInstanceOfType(this.current) && this.IsModifyConditional() && this.state != 4)
            {
                // Don't modify AIf's end when processing switch cases (state == 4)
                int newEnd = this.nodedata.GetPos(this.nodedata.GetDestination(node)) - 6;
                ((AIf)this.current).End(newEnd);
                if (this.current.HasChildren())
                {
                    this.current.RemoveLastChild();
                }
            }
            else if (typeof(AIf).IsInstanceOfType(this.current) && this.IsModifyConditional() && this.state == 4)
            {
                // Don't modify AIf end when state==4, processing switch case
            }
            else if (typeof(AWhileLoop).IsInstanceOfType(this.current) && this.IsModifyConditional())
            {
                ((AWhileLoop)this.current).End(this.nodedata.GetPos(this.nodedata.GetDestination(node)) - 6);
                if (this.current.HasChildren())
                {
                    this.current.RemoveLastChild();
                }
            }
            else
            {
                AIf aif = new AIf(this.nodedata.GetPos(node), this.nodedata.GetPos(this.nodedata.GetDestination(node)) - 6, this.RemoveLastExp(false));
                this.current.AddChild(aif);
                this.current = aif;
            }

            this.CheckEnd(node);
        }

        private bool IsModifyConditional()
        {
            if (!this.current.HasChildren())
            {
                return true;
            }

            if (this.current.Size() == 1)
            {
                ScriptNode.ScriptNode last = this.current.GetLastChild();
                if (last is AExpression lastExp && lastExp is ScriptNode.AVarRef lastVarRef)
                {
                    return !lastVarRef.Var().IsAssigned() && !lastVarRef.Var().IsParam();
                }
                return false;
            }

            return false;
        }

        public virtual void TransformJump(AJumpCommand node)
        {
            this.CheckStart(node);
            Node dest = this.nodedata.GetDestination(node);
            if (this.state == 2)
            {
                this.state = 0;
                AActionArgExp aarg = new AActionArgExp(this.GetNextCommand(node), this.GetPriorToDestCommand(node));
                this.current.AddChild(aarg);
                this.current = aarg;
            }
            else if (!typeof(AIf).IsInstanceOfType(this.current) || this.nodedata.GetPos(node) != this.current.GetEnd())
            {
                if (this.state == 4)
                {
                    ScriptNode.ASwitch aswitch = (ScriptNode.ASwitch)this.current.GetLastChild();
                    ScriptNode.ASwitchCase aprevcase = aswitch.GetLastCase();
                    if (aprevcase != null)
                    {
                        aprevcase.End(this.nodedata.GetPos(NodeUtils.GetPreviousCommand(dest, this.nodedata)));
                    }

                    if (typeof(AMoveSpCommand).IsInstanceOfType(dest))
                    {
                        aswitch.SetEnd(this.nodedata.GetPos(this.nodedata.GetDestination(node)));
                    }
                    else
                    {
                        ScriptNode.ASwitchCase adefault = new ScriptNode.ASwitchCase(this.nodedata.GetPos(dest));
                        aswitch.AddDefaultCase(adefault);
                    }

                    this.state = 0;
                }
                else if (this.IsReturn(node))
                {
                    AReturnStatement areturn;
                    if (!this.root.GetType().Equals((byte)0))
                    {
                        areturn = new AReturnStatement(this.GetReturnExp());
                    }
                    else
                    {
                        areturn = new AReturnStatement();
                    }

                    this.current.AddChild(areturn);
                }
                else if (this.nodedata.GetPos(dest) >= this.nodedata.GetPos(node))
                {
                    ScriptRootNode loop = this.GetBreakable();
                    if (typeof(ASwitchCase).IsInstanceOfType(loop))
                    {
                        loop = this.GetEnclosingLoop(loop);
                        if (loop == null)
                        {
                            ABreakStatement abreak = new ABreakStatement();
                            this.current.AddChild(abreak);
                        }
                        else
                        {
                            AUnkLoopControl aunk = new AUnkLoopControl(this.nodedata.GetPos(dest));
                            this.current.AddChild(aunk);
                        }
                    }
                    else if (loop != null && this.nodedata.GetPos(dest) > loop.GetEnd())
                    {
                        ABreakStatement abreak = new ABreakStatement();
                        this.current.AddChild(abreak);
                    }
                    else
                    {
                        loop = this.GetLoop();
                        if (loop != null && this.nodedata.GetPos(dest) <= loop.GetEnd())
                        {
                            AContinueStatement acont = new AContinueStatement();
                            this.current.AddChild(acont);
                        }
                    }
                }
            }

            this.CheckEnd(node);
        }

        public virtual void TransformJSR(AJumpToSubroutine node)
        {
            this.CheckStart(node);
            var paramObjects = this.RemoveFcnParams(node);
            List<AExpression> @params = new List<AExpression>();
            foreach (var paramObj in paramObjects)
            {
                if (paramObj is AExpression param)
                {
                    @params.Add(param);
                }
            }
            AFcnCallExp jsr = new AFcnCallExp(this.GetFcnId(node), @params);
            if (!this.GetFcnType(node).Equals((byte)0))
            {
                ScriptNode.ScriptNode lastChild = this.current.GetLastChild();
                if (typeof(AVarDecl).IsInstanceOfType(lastChild))
                {
                    ((AVarDecl)lastChild).SetIsFcnReturn(true);
                    ((AVarDecl)lastChild).InitializeExp(jsr);
                    jsr.Stackentry(this.stack.Get(1));
                }
                else if (typeof(ScriptNode.AVarRef).IsInstanceOfType(lastChild))
                {

                    // Handle case where last child is a variable reference instead of declaration
                    // Create a new variable declaration to hold the function return value
                    Variable var = (Variable)this.stack.Get(1);
                    AVarDecl vardec = new AVarDecl(var);
                    vardec.SetIsFcnReturn(true);
                    vardec.InitializeExp(jsr);
                    jsr.Stackentry(var);
                    this.UpdateVarCount(var);

                    // Replace the var ref with the var decl
                    this.current.RemoveLastChild();
                    this.current.AddChild(vardec);
                    this.vardecs.Put(var, vardec);
                }
                else
                {

                    // Fallback: add the function call as a new declaration
                    Variable var = (Variable)this.stack.Get(1);
                    AVarDecl vardec = new AVarDecl(var);
                    vardec.SetIsFcnReturn(true);
                    vardec.InitializeExp(jsr);
                    jsr.Stackentry(var);
                    this.UpdateVarCount(var);
                    this.current.AddChild(vardec);
                    this.vardecs.Put(var, vardec);
                }
            }
            else
            {
                this.current.AddChild(jsr);
            }

            jsr = null;
            this.CheckEnd(node);
        }

        public virtual void TransformAction(AActionCommand node)
        {
            this.CheckStart(node);
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:881
            // Original: List<AExpression> params = this.removeActionParams(node);
            List<AExpression> @params = this.RemoveActionParams(node);
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:889
            // Original: AActionExp act = new AActionExp(actionName, NodeUtils.getActionId(node), params, this.actions);
            AActionExp act = new AActionExp(NodeUtils.GetActionName(node, this.actions), NodeUtils.GetActionId(node), @params);
            UtilsType type = NodeUtils.GetReturnType(node, this.actions);
            if (!type.Equals((byte)0))
            {
                Variable var = (Variable)this.stack.Get(1);
                if (type.Equals(unchecked((byte)(-16))))
                {
                    var = var.Varstruct();
                }

                act.Stackentry(var);
                AVarDecl vardec = new AVarDecl(var);
                vardec.SetIsFcnReturn(true);
                vardec.InitializeExp(act);
                this.UpdateVarCount(var);
                this.current.AddChild(vardec);
                this.vardecs.Put(var, vardec);
            }
            else
            {
                this.current.AddChild(act);
            }

            this.CheckEnd(node);
        }

        public virtual void TransformReturn(AReturn node)
        {
            this.CheckStart(node);
            this.CheckEnd(node);
        }

        public virtual void TransformCopyDownSp(ACopyDownSpCommand node)
        {
            this.CheckStart(node);
            AExpression exp = this.RemoveLastExp(false);
            if (this.IsReturn(node))
            {
                AReturnStatement ret = new AReturnStatement(exp);
                this.current.AddChild(ret);
            }
            else
            {
                AExpression target = this.GetVarToAssignTo(node);
                if (typeof(ScriptNode.AVarRef).IsInstanceOfType(target))
                {
                    ScriptNode.AVarRef varref = (ScriptNode.AVarRef)target;
                    AModifyExp modexp = new AModifyExp(varref, exp);
                    this.UpdateName(varref, exp);
                    this.current.AddChild(modexp);
                }
                else
                {
                    // Edge case: target is a constant, create a pseudo-assignment expression
                    // Note: AModifyExp requires AVarRef, so we cast target
                    if (target is ScriptNode.AVarRef targetVarRef)
                    {
                        AModifyExp modexp = new AModifyExp(targetVarRef, exp);
                        this.current.AddChild(modexp);
                    }
                }

                this.state = 1;
            }

            this.CheckEnd(node);
        }

        private void UpdateName(ScriptNode.AVarRef varref, AExpression exp)
        {
            if (typeof(AActionExp).IsInstanceOfType(exp))
            {
                string name = NameGenerator.GetNameFromAction((AActionExp)exp);
                if (name != null && !this.varnames.ContainsKey(name))
                {
                    varref.Var().Name(name);
                    this.varnames.Put(name, 1);
                }
            }
        }

        public virtual void TransformCopyTopSp(ACopyTopSpCommand node)
        {
            this.CheckStart(node);
            if (this.state == 5)
            {
                this.state = 0;
            }
            else
            {
                AExpression varref = this.GetVarToCopy(node);
                this.current.AddChild((ScriptNode.ScriptNode)varref);
            }

            this.CheckEnd(node);
        }

        public virtual void TransformCopyDownBp(ACopyDownBpCommand node)
        {
            this.CheckStart(node);
            AExpression target = this.GetVarToAssignTo(node);
            AExpression exp = this.RemoveLastExp(false);
            if (target is ScriptNode.AVarRef targetVarRef)
            {
                AModifyExp modexp = new AModifyExp(targetVarRef, exp);
                this.current.AddChild(modexp);
            }
            this.state = 1;
            this.CheckEnd(node);
        }

        public virtual void TransformCopyTopBp(ACopyTopBpCommand node)
        {
            this.CheckStart(node);
            AExpression varref = this.GetVarToCopy(node);
            this.current.AddChild((ScriptNode.ScriptNode)varref);
            this.CheckEnd(node);
        }

        public virtual void TransformMoveSp(AMoveSpCommand node)
        {
            this.CheckStart(node);
            if (this.state == 1)
            {
                ScriptNode.ScriptNode last = this.current.GetLastChild();
                if (!typeof(AReturnStatement).IsInstanceOfType(last))
                {

                    // Handle both AModifyExp (x = y) and AUnaryModExp (x++, ++x)
                    if (typeof(AModifyExp).IsInstanceOfType(last))
                    {
                        AModifyExp modexp = (AModifyExp)this.RemoveLastExp(true);
                        AExpressionStatement stmt = new AExpressionStatement(modexp);
                        this.current.AddChild(stmt);
                        stmt.Parent(this.current);
                    }
                    else if (typeof(AUnaryModExp).IsInstanceOfType(last))
                    {
                        AUnaryModExp unaryModExp = (AUnaryModExp)this.RemoveLastExp(true);
                        AExpressionStatement stmt = new AExpressionStatement(unaryModExp);
                        this.current.AddChild(stmt);
                        stmt.Parent(this.current);
                    }
                    else
                    {

                        // Fallback: treat any expression as a statement
                        AExpression exp = this.RemoveLastExp(true);
                        AExpressionStatement stmt = new AExpressionStatement(exp);
                        this.current.AddChild(stmt);
                        stmt.Parent(this.current);
                    }
                }

                this.state = 0;
            }
            else
            {
                this.CheckSwitchEnd(node);
            }

            this.CheckEnd(node);
        }

        public virtual void TransformRSAdd(ARsaddCommand node)
        {
            this.CheckStart(node);
            Variable var = (Variable)this.stack.Get(1);
            // Matching NCSDecomp implementation: check if variable is already declared to prevent duplicates
            AVarDecl existingVardec = (AVarDecl)this.vardecs[var];
            if (existingVardec == null)
            {
                AVarDecl vardec = new AVarDecl(var);
                this.UpdateVarCount(var);
                this.current.AddChild(vardec);
                this.vardecs.Put(var, vardec);
            }
            this.CheckEnd(node);
        }


        public virtual void TransformConst(AConstCommand node)
        {
            this.CheckStart(node);
            Const theconst = (Const)this.stack.Get(1);
            AConst constdec = new AConst(theconst);
            this.current.AddChild(constdec);
            this.CheckEnd(node);
        }

        public virtual void TransformLogii(ALogiiCommand node)
        {
            this.CheckStart(node);
            if (!this.current.HasChildren() && typeof(AIf).IsInstanceOfType(this.current) && typeof(AIf).IsInstanceOfType(this.current.Parent()))
            {
                AIf right = (AIf)this.current;
                AIf left = (AIf)this.current.Parent();
                AConditionalExp conexp = new AConditionalExp(left.Condition(), right.Condition(), NodeUtils.GetOp(node));
                conexp.Stackentry(this.stack.Get(1));
                this.current = (ScriptRootNode)this.current.Parent();
                ((AIf)this.current).Condition(conexp);
                this.current.RemoveLastChild();
            }
            else
            {
                AExpression right2 = this.RemoveLastExp(false);
                if (!this.current.HasChildren() && typeof(AIf).IsInstanceOfType(this.current))
                {
                    AExpression left2 = ((AIf)this.current).Condition();
                    AConditionalExp conexp = new AConditionalExp(left2, right2, NodeUtils.GetOp(node));
                    conexp.Stackentry(this.stack.Get(1));
                    ((AIf)this.current).Condition(conexp);
                }
                else if (!this.current.HasChildren() && typeof(AWhileLoop).IsInstanceOfType(this.current))
                {
                    AExpression left2 = ((AWhileLoop)this.current).Condition();
                    AConditionalExp conexp = new AConditionalExp(left2, right2, NodeUtils.GetOp(node));
                    conexp.Stackentry(this.stack.Get(1));
                    ((AWhileLoop)this.current).Condition(conexp);
                }
                else
                {
                    AExpression left2 = this.RemoveLastExp(false);
                    AConditionalExp conexp = new AConditionalExp(left2, right2, NodeUtils.GetOp(node));
                    conexp.Stackentry(this.stack.Get(1));
                    this.current.AddChild(conexp);
                }
            }

            this.CheckEnd(node);
        }

        public virtual void TransformBinary(ABinaryCommand node)
        {
            this.CheckStart(node);
            AExpression right = this.RemoveLastExp(false);
            AExpression left = this.RemoveLastExp(this.state == 4);
            AExpression exp;
            if (NodeUtils.IsArithmeticOp(node))
            {
                exp = new ABinaryExp(left, right, NodeUtils.GetOp(node));
            }
            else
            {
                if (!NodeUtils.IsConditionalOp(node))
                {
                    throw new Exception("Unknown binary op at " + this.nodedata.GetPos(node));
                }

                exp = new AConditionalExp(left, right, NodeUtils.GetOp(node));
            }

            exp.Stackentry(this.stack.Get(1));
            this.current.AddChild((ScriptNode.ScriptNode)exp);
            this.CheckEnd(node);
        }

        public virtual void TransformUnary(AUnaryCommand node)
        {
            this.CheckStart(node);
            AExpression exp = this.RemoveLastExp(false);
            AUnaryExp unexp = new AUnaryExp(exp, NodeUtils.GetOp(node));
            unexp.Stackentry(this.stack.Get(1));
            this.current.AddChild(unexp);
            this.CheckEnd(node);
        }

        public virtual void TransformStack(AStackCommand node)
        {
            this.CheckStart(node);
            ScriptNode.ScriptNode last = this.current.GetLastChild();
            AExpression target = this.GetVarToAssignTo(node);
            bool prefix;
            if (typeof(AVarRef).IsInstanceOfType(target) && typeof(AVarRef).IsInstanceOfType(last) && ((AVarRef)(object)last).Var() == ((AVarRef)target).Var())
            {
                this.RemoveLastExp(true);
                prefix = false;
            }
            else
            {
                this.state = 5;
                prefix = true;
            }

            if (target is ScriptNode.AVarRef targetVarRef)
            {
                AUnaryModExp unexp = new AUnaryModExp(targetVarRef, NodeUtils.GetOp(node), prefix);
                unexp.Stackentry(this.stack.Get(1));
                this.current.AddChild(unexp);
            }
            this.CheckEnd(node);
        }

        public virtual void TransformDestruct(ADestructCommand node)
        {
            this.CheckStart(node);
            this.UpdateStructVar(node);
            this.CheckEnd(node);
        }

        public virtual void TransformBp(ABpCommand node)
        {
            this.CheckStart(node);
            this.CheckEnd(node);
        }

        public virtual void TransformStoreState(AStoreStateCommand node)
        {
            this.CheckStart(node);
            this.state = 2;
            this.CheckEnd(node);
        }

        public virtual void TransformDeadCode(Node node)
        {
            this.CheckEnd(node);
        }

        public virtual bool AtLastCommand(Node node)
        {
            if (node == null)
            {
                return false;
            }

            int nodePos = this.nodedata.GetPos(node);
            if (nodePos == -1)
            {
                return false;
            }

            if (nodePos == this.current.GetEnd())
            {
                return true;
            }

            if (typeof(ASwitchCase).IsInstanceOfType(this.current) && ((ASwitch)this.current.Parent()).GetEnd() == nodePos)
            {
                return true;
            }

            if (typeof(ASub).IsInstanceOfType(this.current))
            {
                Node next = NodeUtils.GetNextCommand(node, this.nodedata);
                if (next == null)
                {
                    return true;
                }
            }

            if (typeof(AIf).IsInstanceOfType(this.current) || typeof(AElse).IsInstanceOfType(this.current))
            {
                Node next = NodeUtils.GetNextCommand(node, this.nodedata);
                if (next != null && this.nodedata.GetPos(next) == this.current.GetEnd())
                {
                    return true;
                }
            }

            return false;
        }

        public virtual bool IsMiddleOfReturn(Node node)
        {
            if (!this.root.GetType().Equals((byte)0) && this.current.HasChildren() && typeof(AReturnStatement).IsInstanceOfType(this.current.GetLastChild()))
            {
                return true;
            }

            if (this.root.GetType().Equals((byte)0))
            {
                Node next = NodeUtils.GetNextCommand(node, this.nodedata);
                if (next != null && typeof(AJumpCommand).IsInstanceOfType(next) && typeof(AReturn).IsInstanceOfType(this.nodedata.GetDestination(next)))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual bool CurrentContainsVars(List<object> vars)
        {
            for (int i = 0; i < vars.Count; ++i)
            {
                Variable var = (Variable)vars[i];
                if (var.IsParam())
                {
                    continue;
                }

                ScriptNode.AVarDecl vardec = (ScriptNode.AVarDecl)this.vardecs[var];
                if (vardec == null)
                {
                    continue;
                }

                ScriptNode.ScriptNode parent = vardec.Parent();
                bool found = false;
                while (parent != null && !found)
                {
                    if (parent == this.current)
                    {
                        found = true;
                    }
                    else
                    {
                        parent = parent.Parent();
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private int GetEarlierDec(ScriptNode.AVarDecl vardec, int earliestdec)
        {
            if (this.current.GetChildLocation(vardec) == -1)
            {
                return -1;
            }

            if (earliestdec == -1)
            {
                return this.current.GetChildLocation(vardec);
            }

            if (this.current.GetChildLocation(vardec) < earliestdec)
            {
                return this.current.GetChildLocation(vardec);
            }

            return earliestdec;
        }

        public virtual AExpression GetReturnExp()
        {
            ScriptNode.ScriptNode last = this.current.RemoveLastChild();
            if (typeof(AModifyExp).IsInstanceOfType(last))
            {
                return ((AModifyExp)last).GetExpression();
            }

            if (typeof(AExpressionStatement).IsInstanceOfType(last) && typeof(AModifyExp).IsInstanceOfType(((AExpressionStatement)last).GetExp()))
            {
                return ((AModifyExp)((AExpressionStatement)last).GetExp()).GetExpression();
            }

            if (typeof(AReturnStatement).IsInstanceOfType(last))
            {
                return ((AReturnStatement)last).GetExp();
            }

            JavaSystem.@out.Println(last);
            throw new Exception("Trying to get return expression, unexpected scriptnode.scriptnode class " + last.GetType());
        }

        private void CheckSwitchEnd(AMoveSpCommand node)
        {
            if (typeof(ASwitchCase).IsInstanceOfType(this.current))
            {
                StackEntry entry = this.stack.Get(1);
                if (typeof(Variable).IsInstanceOfType(entry) && ((ScriptNode.ASwitch)this.current.Parent()).GetSwitchExp().Stackentry().Equals(entry))
                {
                    ((ScriptNode.ASwitch)this.current.Parent()).SetEnd(this.nodedata.GetPos(node));
                    this.UpdateSwitchUnknowns((ScriptNode.ASwitch)this.current.Parent());
                }
            }
        }

        private void UpdateSwitchUnknowns(ScriptNode.ASwitch aswitch)
        {
            ScriptNode.ASwitchCase acase = null;
            while ((acase = aswitch.GetNextCase(acase)) != null)
            {
                List<ScriptNode.AUnkLoopControl> unknowns = acase.GetUnknowns();
                for (int i = 0; i < unknowns.Count; ++i)
                {
                    ScriptNode.AUnkLoopControl unk = unknowns[i];
                    if (unk.GetDestination() > aswitch.GetEnd())
                    {
                        acase.ReplaceUnknown(unk, new ScriptNode.AContinueStatement());
                    }
                    else
                    {
                        acase.ReplaceUnknown(unk, new ScriptNode.ABreakStatement());
                    }
                }
            }
        }

        private ScriptRootNode GetLoop()
        {
            return this.GetEnclosingLoop(this.current);
        }

        private ScriptRootNode GetEnclosingLoop(ScriptNode.ScriptNode start)
        {
            for (ScriptNode.ScriptNode node = start; node != null; node = node.Parent())
            {
                if (typeof(ADoLoop).IsInstanceOfType(node) || typeof(AWhileLoop).IsInstanceOfType(node))
                {
                    return (ScriptRootNode)node;
                }
            }

            return null;
        }

        private ScriptRootNode GetBreakable()
        {
            for (ScriptNode.ScriptNode node = this.current; node != null; node = node.Parent())
            {
                if (typeof(ScriptNode.ADoLoop).IsInstanceOfType(node) || typeof(ScriptNode.AWhileLoop).IsInstanceOfType(node) || typeof(ScriptNode.ASwitchCase).IsInstanceOfType(node))
                {
                    return (ScriptRootNode)node;
                }
            }

            return null;
        }

        private ScriptNode.AControlLoop GetLoop(Node destination, Node origin)
        {
            Node beforeJump = NodeUtils.GetPreviousCommand(origin, this.nodedata);
            if (NodeUtils.IsJzPastOne(beforeJump))
            {
                ScriptNode.ADoLoop doloop = new ScriptNode.ADoLoop(this.nodedata.GetPos(destination), this.nodedata.GetPos(origin));
                return doloop;
            }

            ScriptNode.AWhileLoop whileloop = new ScriptNode.AWhileLoop(this.nodedata.GetPos(destination), this.nodedata.GetPos(origin));
            return whileloop;
        }

        private ScriptNode.AExpression RemoveIfAsExp()
        {
            ScriptNode.AIf aif = (ScriptNode.AIf)this.current;
            ScriptNode.AExpression exp = aif.Condition();
            (this.current = (ScriptRootNode)this.current.Parent()).RemoveChild(aif);
            aif.Parent(null);
            exp.Parent(null);
            return exp;
        }

        private ScriptNode.AExpression RemoveLastExp(bool forceOneOnly)
        {
            if (!this.current.HasChildren() && typeof(AIf).IsInstanceOfType(this.current))
            {
                return this.RemoveIfAsExp();
            }

            ScriptNode.ScriptNode anode = this.current.RemoveLastChild();
            if (typeof(ScriptNode.AExpression).IsInstanceOfType(anode))
            {
                if (!forceOneOnly && typeof(AVarRef).IsInstanceOfType(anode) && !((AVarRef)(object)anode).Var().IsAssigned() && !((AVarRef)(object)anode).Var().IsParam() && this.current.HasChildren())
                {
                    ScriptNode.ScriptNode last = this.current.GetLastChild();
                    if (typeof(ScriptNode.AExpression).IsInstanceOfType(last) && ((AVarRef)(object)anode).Var().Equals(((ScriptNode.AExpression)last).Stackentry()))
                    {
                        return this.RemoveLastExp(false);
                    }

                    if (typeof(ScriptNode.AVarDecl).IsInstanceOfType(last) && ((AVarRef)(object)anode).Var().Equals(((ScriptNode.AVarDecl)last).GetVarVar()) && ((ScriptNode.AVarDecl)last).GetExp() != null)
                    {
                        return this.RemoveLastExp(false);
                    }
                }

                return (ScriptNode.AExpression)anode;
            }

            if (typeof(ScriptNode.AVarDecl).IsInstanceOfType(anode))
            {
                ScriptNode.AVarDecl vardec = (ScriptNode.AVarDecl)anode;
                if (vardec.IsFcnReturn() && vardec.GetExp() != null)
                {
                    ScriptNode.AExpression exp = vardec.GetExp();
                    vardec.RemoveExp();
                    return exp;
                }

                if (vardec.GetExp() != null)
                {
                    return vardec.RemoveExp();
                }

                if (!forceOneOnly)
                {
                    AVarRef varref = new AVarRef(vardec.GetVarVar());
                    return varref;
                }

                throw new Exception("Last child is AVarDecl without expression when forceOneOnly=true: " + anode);
            }


            // Handle AExpressionStatement - extract the contained expression
            if (typeof(ScriptNode.AExpressionStatement).IsInstanceOfType(anode))
            {
                ScriptNode.AExpressionStatement exprStmt = (ScriptNode.AExpressionStatement)anode;
                return exprStmt.GetExp();
            }

            JavaSystem.@out.Println(anode.ToString());
            throw new Exception("Last child not an expression: " + anode.GetType());
        }

        private ScriptNode.AExpression GetLastExp()
        {
            ScriptNode.ScriptNode anode = this.current.GetLastChild();
            if (typeof(ScriptNode.AExpression).IsInstanceOfType(anode))
            {
                return (ScriptNode.AExpression)anode;
            }

            if (typeof(ScriptNode.AVarDecl).IsInstanceOfType(anode) && ((ScriptNode.AVarDecl)anode).IsFcnReturn())
            {
                return ((ScriptNode.AVarDecl)anode).GetExp();
            }

            JavaSystem.@out.Println(anode.ToString());
            throw new Exception("Last child not an expression " + anode);
        }

        private ScriptNode.AExpression GetPreviousExp(int pos)
        {
            ScriptNode.ScriptNode node = this.current.GetPreviousChild(pos);
            if (node == null)
            {
                return null;
            }

            if (typeof(ScriptNode.AVarDecl).IsInstanceOfType(node) && ((ScriptNode.AVarDecl)node).IsFcnReturn())
            {
                return ((ScriptNode.AVarDecl)node).GetExp();
            }

            if (!typeof(ScriptNode.AExpression).IsInstanceOfType(node))
            {
                return null;
            }

            return (ScriptNode.AExpression)node;
        }

        public virtual void SetVarStructName(VarStruct varstruct)
        {
            if (varstruct.Name() == null)
            {
                int count = 1;
                UtilsType key = new UtilsType(unchecked((byte)(-15)));
                object curcountObj = this.varcounts[key];
                if (curcountObj != null)
                {
                    int curcount = (int)curcountObj;
                    count += curcount;
                }

                varstruct.Name(this.varprefix, count);
                this.varcounts.Put(key, count);
            }
        }

        private void UpdateVarCount(Variable var)
        {
            int count = 1;
            UtilsType key = var.Type();
            object curcountObj = this.varcounts[key];
            if (curcountObj != null)
            {
                int curcount = (int)curcountObj;
                count += curcount;
            }

            var.Name(this.varprefix, count);
            this.varcounts.Put(key, count);
        }

        private void UpdateStructVar(ADestructCommand node)
        {
            ScriptNode.AExpression lastExp = this.GetLastExp();
            int removesize = NodeUtils.StackSizeToPos(node.GetSizeRem());
            int savestart = NodeUtils.StackSizeToPos(node.GetOffset());
            int savesize = NodeUtils.StackSizeToPos(node.GetSizeSave());
            if (savesize > 1)
            {
                throw new Exception("Ah-ha!  A nested struct!  Now I have to code for that.  *sob*");
            }

            Variable elementVar = (Variable)this.stack.Get(removesize - savestart);
            if (typeof(AVarRef).IsInstanceOfType(lastExp))
            {
                AVarRef varref = (AVarRef)lastExp;
                this.SetVarStructName((VarStruct)varref.Var());
                varref.ChooseStructElement(elementVar);
            }
            else if (typeof(ScriptNode.AActionExp).IsInstanceOfType(lastExp))
            {
                ScriptNode.AActionExp actionExp = (ScriptNode.AActionExp)lastExp;
                StackEntry stackEntry = actionExp.Stackentry();
                if (stackEntry != null && typeof(VarStruct).IsInstanceOfType(stackEntry))
                {
                    VarStruct varStruct = (VarStruct)stackEntry;
                    this.SetVarStructName(varStruct);
                    actionExp.Stackentry(elementVar);
                }
                else if (stackEntry != null && typeof(Variable).IsInstanceOfType(stackEntry))
                {
                    Variable stackVar = (Variable)stackEntry;
                    if (stackVar.IsStruct())
                    {
                        this.SetVarStructName(stackVar.Varstruct());
                        actionExp.Stackentry(elementVar);
                    }
                }
            }
            else if (typeof(ScriptNode.AFcnCallExp).IsInstanceOfType(lastExp))
            {
                ScriptNode.AFcnCallExp fcnExp = (ScriptNode.AFcnCallExp)lastExp;
                StackEntry stackEntry = fcnExp.Stackentry();
                if (stackEntry != null && typeof(VarStruct).IsInstanceOfType(stackEntry))
                {
                    VarStruct varStruct = (VarStruct)stackEntry;
                    this.SetVarStructName(varStruct);
                    fcnExp.Stackentry(elementVar);
                }
                else if (stackEntry != null && typeof(Variable).IsInstanceOfType(stackEntry))
                {
                    Variable stackVar = (Variable)stackEntry;
                    if (stackVar.IsStruct())
                    {
                        this.SetVarStructName(stackVar.Varstruct());
                        fcnExp.Stackentry(elementVar);
                    }
                }
            }
            else if (typeof(ScriptNode.ABinaryExp).IsInstanceOfType(lastExp) || typeof(ScriptNode.AUnaryExp).IsInstanceOfType(lastExp) || typeof(ScriptNode.AConditionalExp).IsInstanceOfType(lastExp))
            {
                StackEntry stackEntry = lastExp.Stackentry();
                if (stackEntry != null && typeof(VarStruct).IsInstanceOfType(stackEntry))
                {
                    VarStruct varStruct = (VarStruct)stackEntry;
                    this.SetVarStructName(varStruct);
                    lastExp.Stackentry(elementVar);
                }
                else if (stackEntry != null && typeof(Variable).IsInstanceOfType(stackEntry))
                {
                    Variable stackVar = (Variable)stackEntry;
                    if (stackVar.IsStruct())
                    {
                        this.SetVarStructName(stackVar.Varstruct());
                        lastExp.Stackentry(elementVar);
                    }
                }
            }
        }

        private ScriptNode.AExpression GetVarToAssignTo(AStackCommand node)
        {
            int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
            if (NodeUtils.IsGlobalStackOp(node))
            {
                --loc;
            }

            StackEntry entry;
            if (NodeUtils.IsGlobalStackOp(node))
            {
                entry = this.subdata.GetGlobalStack().Get(loc);
            }
            else
            {
                entry = this.stack.Get(loc);
            }


            // Handle case where entry is not a Variable
            if (!typeof(Variable).IsInstanceOfType(entry))
            {
                if (typeof(Const).IsInstanceOfType(entry))
                {
                    return new ScriptNode.AConst((Const)entry);
                }

                throw new Exception("getVarToAssignTo: unexpected type at loc " + loc + ": " + entry.GetType().Name);
            }

            Variable var = (Variable)entry;
            var.Assigned();
            return new AVarRef(var);
        }

        private bool IsReturn(ACopyDownSpCommand node)
        {
            return !this.root.GetType().Equals((byte)0) && this.stack.Size() == NodeUtils.StackOffsetToPos(node.GetOffset());
        }

        private bool IsReturn(AJumpCommand node)
        {
            Node dest = NodeUtils.GetCommandChild(this.nodedata.GetDestination(node));
            if (NodeUtils.IsReturn(dest))
            {
                return true;
            }

            if (typeof(AMoveSpCommand).IsInstanceOfType(dest))
            {
                Node afterdest = NodeUtils.GetNextCommand(dest, this.nodedata);
                return afterdest == null;
            }

            return false;
        }

        private ScriptNode.AExpression GetVarToAssignTo(ACopyDownSpCommand node)
        {
            return this.GetVar(NodeUtils.StackSizeToPos(node.GetSize()), NodeUtils.StackOffsetToPos(node.GetOffset()), this.stack, true, this);
        }

        private ScriptNode.AExpression GetVarToAssignTo(ACopyDownBpCommand node)
        {
            return this.GetVar(NodeUtils.StackSizeToPos(node.GetSize()), NodeUtils.StackOffsetToPos(node.GetOffset()), this.subdata.GetGlobalStack(), true, this.subdata.GlobalState());
        }

        private ScriptNode.AExpression GetVarToCopy(ACopyTopSpCommand node)
        {
            return this.GetVar(NodeUtils.StackSizeToPos(node.GetSize()), NodeUtils.StackOffsetToPos(node.GetOffset()), this.stack, false, this);
        }

        private ScriptNode.AExpression GetVarToCopy(ACopyTopBpCommand node)
        {
            return this.GetVar(NodeUtils.StackSizeToPos(node.GetSize()), NodeUtils.StackOffsetToPos(node.GetOffset()), this.subdata.GetGlobalStack(), false, this.subdata.GlobalState());
        }

        private ScriptNode.AExpression GetVar(int copy, int loc, LocalVarStack stack, bool assign, SubScriptState state)
        {
            bool isstruct = copy > 1;
            StackEntry entry = stack.Get(loc);
            if (!typeof(Variable).IsInstanceOfType(entry))
            {
                if (assign)
                {

                    // In some edge cases, the stack might contain a constant where an assignment is expected
                    // This can happen with certain bytecode patterns. Return a constant reference instead of throwing.
                    if (typeof(Const).IsInstanceOfType(entry))
                    {

                        // Return the constant - the decompiler will handle it as an expression
                        return new ScriptNode.AConst((Const)entry);
                    }

                    throw new Exception("Attempting to assign to a non-variable of type: " + entry.GetType().Name);
                }
            }

            if (typeof(Const).IsInstanceOfType(entry))
            {
                return new ScriptNode.AConst((Const)entry);
            }

            Variable var = (Variable)entry;
            if (!isstruct)
            {
                if (assign)
                {
                    var.Assigned();
                }

                return new AVarRef(var);
            }

            if (var.IsStruct())
            {
                if (assign)
                {
                    var.Varstruct().Assigned();
                }

                state.SetVarStructName(var.Varstruct());
                return new AVarRef(var.Varstruct());
            }

            VarStruct newstruct = new VarStruct();
            newstruct.AddVar(var);
            for (int i = loc - 1; i > loc - copy; --i)
            {
                var = (Variable)stack.Get(i);
                newstruct.AddVar(var);
            }

            if (assign)
            {
                newstruct.Assigned();
            }

            this.subdata.AddStruct(newstruct);
            state.SetVarStructName(newstruct);
            return new AVarRef(newstruct);
        }

        private List<AVarRef> GetParams(int paramcount)
        {
            List<AVarRef> @params = new List<AVarRef>();
            for (int i = 1; i <= paramcount; ++i)
            {
                Variable var = (Variable)this.stack.Get(i);
                var.Name("Param", i);
                AVarRef varref = new AVarRef(var);
                @params.Add(varref);
            }

            return @params;
        }

        private List<object> RemoveFcnParams(AJumpToSubroutine node)
        {
            List<object> @params = new List<object>();
            int paramcount = this.subdata.GetState(this.nodedata.GetDestination(node)).GetParamCount();
            int i = 0;
            while (i < paramcount)
            {
                ScriptNode.AExpression exp = this.RemoveLastExp(false);
                i += this.GetExpSize(exp);
                @params.Add(exp);
            }

            return @params;
        }

        private int GetExpSize(AExpression exp)
        {
            if (typeof(AVarRef).IsInstanceOfType(exp))
            {
                return ((AVarRef)exp).Var().Size();
            }

            if (typeof(ScriptNode.AConst).IsInstanceOfType(exp))
            {
                return 1;
            }

            return 1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/SubScriptState.java:1921
        // Original: private List<AExpression> removeActionParams(AActionCommand node) { ArrayList<AExpression> params = new ArrayList<>(); ... }
        private List<AExpression> RemoveActionParams(AActionCommand node)
        {
            List<AExpression> @params = new List<AExpression>();
            List<object> paramtypes = NodeUtils.GetActionParamTypes(node, this.actions);
            for (int paramcount = NodeUtils.GetActionParamCount(node), i = 0; i < paramcount; ++i)
            {
                UtilsType paramtype = (UtilsType)paramtypes[i];
                ScriptNode.AExpression exp;
                if (paramtype.Equals(unchecked((byte)(-16))))
                {
                    exp = this.GetLastExp();
                    if (exp.Stackentry().GetType().Equals(unchecked((byte)(-16))) || exp.Stackentry().GetType().Equals(unchecked((byte)(-15))))
                    {
                        exp = this.RemoveLastExp(false);
                    }
                    else
                    {
                        exp = new ScriptNode.AVectorConstExp(this.RemoveLastExp(false), this.RemoveLastExp(false), this.RemoveLastExp(false));
                    }
                }
                else
                {
                    exp = this.RemoveLastExp(false);
                }

                @params.Add(exp);
            }

            return @params;
        }

        private byte GetFcnId(AJumpToSubroutine node)
        {
            return this.subdata.GetState(this.nodedata.GetDestination(node)).GetId();
        }

        private UtilsType GetFcnType(AJumpToSubroutine node)
        {
            return this.subdata.GetState(this.nodedata.GetDestination(node)).Type();
        }

        private int GetNextCommand(AJumpCommand node)
        {
            return this.nodedata.GetPos(node) + 6;
        }

        private int GetPriorToDestCommand(AJumpCommand node)
        {
            return this.nodedata.GetPos(this.nodedata.GetDestination(node)) - 2;
        }
    }
}




