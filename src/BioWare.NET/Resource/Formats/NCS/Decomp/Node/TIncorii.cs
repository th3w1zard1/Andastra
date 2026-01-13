namespace BioWare.NET.Resource.Formats.NCS.Decomp.Node
{
    public sealed class TIncorii : Token
    {
        public TIncorii() : this(0, 0)
        {
        }

        public TIncorii(int line, int pos) : base("INCORII")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TIncorii(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TIncorii text.");
        }
    }
}





