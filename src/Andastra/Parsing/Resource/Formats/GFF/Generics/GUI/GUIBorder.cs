using Andastra.Parsing;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Generics.GUI
{
    /// <summary>
    /// Represents border properties for GUI controls.
    /// </summary>
    [PublicAPI]
    public class GUIBorder
    {
        public Color Color { get; set; }
        public ResRef Corner { get; set; } = ResRef.FromBlank();
        public int Dimension { get; set; }
        public ResRef Edge { get; set; } = ResRef.FromBlank();
        public ResRef Fill { get; set; } = ResRef.FromBlank();
        public int FillStyle { get; set; } = 2;
        public int? InnerOffset { get; set; }
        public int? InnerOffsetY { get; set; }
        public int? Pulsing { get; set; }

        public GUIBorder()
        {
        }
    }
}
