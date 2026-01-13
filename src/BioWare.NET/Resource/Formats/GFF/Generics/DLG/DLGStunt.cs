using System;
using System.Collections.Generic;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics.DLG
{
    /// <summary>
    /// Represents a stunt model in a dialog.
    /// </summary>
    [PublicAPI]
    public sealed class DLGStunt : IEquatable<DLGStunt>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/stunts.py:12
        // Original: class DLGStunt:
        private readonly int _hashCache;

        public string Participant { get; set; } = string.Empty;
        public ResRef StuntModel { get; set; } = ResRef.FromBlank();

        public DLGStunt()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DLGStunt other && Equals(other);
        }

        public bool Equals(DLGStunt other)
        {
            if (other == null) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/stunts.py:36
        // Original: def to_dict(self) -> dict[str, Any]:
        /// <summary>
        /// Serializes this stunt to a dictionary representation.
        /// </summary>
        /// <returns>A dictionary representation of this stunt</returns>
        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                { "participant", Participant },
                { "stunt_model", StuntModel?.ToString() ?? "" },
                { "_hash_cache", _hashCache }
            };
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/stunts.py:39
        // Original: @classmethod def from_dict(cls, data: dict[str, Any], node_map: dict[str | int, Any] | None = None) -> DLGStunt:
        /// <summary>
        /// Deserializes a stunt from a dictionary representation.
        /// </summary>
        /// <param name="data">The dictionary data</param>
        /// <param name="nodeMap">Optional map (unused for stunts, but kept for API compatibility)</param>
        /// <returns>A DLGStunt instance</returns>
        public static DLGStunt FromDict(Dictionary<string, object> data, Dictionary<string, object> nodeMap = null)
        {
            DLGStunt stunt = new DLGStunt();
            stunt.Participant = data.ContainsKey("participant") ? data["participant"].ToString() : "";
            stunt.StuntModel = data.ContainsKey("stunt_model") ? new ResRef(data["stunt_model"].ToString()) : ResRef.FromBlank();

            // Extract hash cache if present
            if (data.ContainsKey("_hash_cache"))
            {
                int hashValue = Convert.ToInt32(data["_hash_cache"]);
                // Use reflection to set private _hashCache field
                var field = typeof(DLGStunt).GetField("_hashCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(stunt, hashValue);
                }
            }

            return stunt;
        }
    }
}
