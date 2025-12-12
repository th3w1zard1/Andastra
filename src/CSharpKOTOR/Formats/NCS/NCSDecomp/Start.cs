// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class Start : Node
    {
        private PProgram _pProgram_;
        private EOF _eof_;
        public Start()
        {
        }

        public Start(PProgram _pProgram_, EOF _eof_)
        {
            this.SetPProgram(_pProgram_);
            this.SetEOF(_eof_);
        }

        public override object Clone()
        {
            return new Start((PProgram)this.CloneNode(this._pProgram_), (EOF)this.CloneNode(this._eof_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseStart(this);
        }

        public PProgram GetPProgram()
        {
            return this._pProgram_;
        }

        public void SetPProgram(PProgram node)
        {
            if (this._pProgram_ != null)
            {
                this._pProgram_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._pProgram_ = node;
        }

        public EOF GetEOF()
        {
            return this._eof_;
        }

        public void SetEOF(EOF node)
        {
            if (this._eof_ != null)
            {
                this._eof_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._eof_ = node;
        }

        public override void RemoveChild(Node child)
        {
            if (this._pProgram_ == child)
            {
                this._pProgram_ = null;
                return;
            }

            if (this._eof_ == child)
            {
                this._eof_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._pProgram_ == oldChild)
            {
                this.SetPProgram((PProgram)newChild);
                return;
            }

            if (this._eof_ == oldChild)
            {
                this.SetEOF((EOF)newChild);
            }
        }

        public override string ToString()
        {
            return this.ToString(this._pProgram_) + this.ToString(this._eof_);
        }
    }
}




