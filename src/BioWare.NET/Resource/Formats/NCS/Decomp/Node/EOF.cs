namespace BioWare.NET.Resource.Formats.NCS.Decomp.Node
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
            // AST.EOF is different from Decomp.EOF, so use DefaultIn
            sw.DefaultIn(this);
        }
    }
}





