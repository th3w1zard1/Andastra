namespace TSLPatcher.Core.Formats.NCS.DeNCS.Node
{
    public sealed class EOF : Token
    {
        public EOF() : this(0, 0)
        {
        }

        public EOF(int line, int pos) : base("")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new EOF(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            if (sw is Analysis.PrunedDepthFirstAdapter adapter)
            {
                adapter.CaseEOF(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }
    }
}

