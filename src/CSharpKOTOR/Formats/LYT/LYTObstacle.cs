using System;
using CSharpKOTOR.Common;

namespace CSharpKOTOR.Formats.LYT
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:360-410
    // Original: class LYTObstacle(ComparableMixin)
    public class LYTObstacle : IEquatable<LYTObstacle>
    {
        public string Model { get; set; }
        public Vector3 Position { get; set; }

        public LYTObstacle(string model, Vector3 position)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:386-393
            // Original: def __init__(self, model: str, position: Vector3)
            Model = model;
            Position = position;
        }

        public override bool Equals(object obj)
        {
            return obj is LYTObstacle other && Equals(other);
        }

        public bool Equals(LYTObstacle other)
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

