using System;
using CSharpKOTOR.Common;

namespace CSharpKOTOR.Formats.LYT
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:307-357
    // Original: class LYTTrack(ComparableMixin)
    public class LYTTrack : IEquatable<LYTTrack>
    {
        public string Model { get; set; }
        public Vector3 Position { get; set; }

        public LYTTrack(string model, Vector3 position)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:333-340
            // Original: def __init__(self, model: str, position: Vector3)
            Model = model;
            Position = position;
        }

        public override bool Equals(object obj)
        {
            return obj is LYTTrack other && Equals(other);
        }

        public bool Equals(LYTTrack other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other == null)
            {
                return false;
            }
            return Model.ToLowerInvariant() == other.Model.ToLowerInvariant() && Position.Equals(other.Position);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Model.ToLowerInvariant(), Position);
        }
    }
}

