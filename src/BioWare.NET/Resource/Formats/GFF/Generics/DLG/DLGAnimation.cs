using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics.DLG
{
    /// <summary>
    /// Represents a unit of animation executed during a node.
    /// </summary>
    [PublicAPI]
    public sealed class DLGAnimation : IEquatable<DLGAnimation>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/anims.py:10
        // Original: class DLGAnimation:
        private readonly int _hashCache;

        public int AnimationId { get; set; } = 6;
        public string Participant { get; set; } = string.Empty;

        public DLGAnimation()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DLGAnimation other && Equals(other);
        }

        public bool Equals(DLGAnimation other)
        {
            if (other == null) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        public override string ToString()
        {
            return $"{GetType().Name}(animation_id={AnimationId}, participant={Participant})";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/anims.py:31
        // Original: def to_dict(self) -> dict[str, Any]:
        /// <summary>
        /// Serializes this animation to a dictionary representation.
        /// </summary>
        /// <returns>A dictionary representation of this animation</returns>
        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                { "animation_id", AnimationId },
                { "participant", Participant },
                { "_hash_cache", _hashCache }
            };
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/anims.py:34
        // Original: @classmethod def from_dict(cls, data: dict) -> DLGAnimation:
        /// <summary>
        /// Deserializes an animation from a dictionary representation.
        /// </summary>
        /// <param name="data">The dictionary data</param>
        /// <returns>A DLGAnimation instance</returns>
        public static DLGAnimation FromDict(Dictionary<string, object> data)
        {
            DLGAnimation animation = new DLGAnimation();
            animation.AnimationId = data.ContainsKey("animation_id") ? Convert.ToInt32(data["animation_id"]) : 6;
            animation.Participant = data.ContainsKey("participant") ? data["participant"].ToString() : "";

            // Extract hash cache if present
            if (data.ContainsKey("_hash_cache"))
            {
                int hashValue = Convert.ToInt32(data["_hash_cache"]);
                // Use reflection to set private _hashCache field
                var field = typeof(DLGAnimation).GetField("_hashCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(animation, hashValue);
                }
            }

            return animation;
        }
    }
}

