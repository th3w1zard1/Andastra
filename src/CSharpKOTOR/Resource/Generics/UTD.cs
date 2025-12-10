using CSharpKOTOR.Common;
using CSharpKOTOR.Resource.Formats.GFF;
using JetBrains.Annotations;

namespace CSharpKOTOR.Resource.Generics
{
    /// <summary>
    /// Stores door data.
    ///
    /// UTD files are GFF-based format files that store door definitions including
    /// lock/unlock mechanics, HP, scripts, and appearance.
    /// </summary>
    [PublicAPI]
    public sealed class UTD
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:16
        // Original: BINARY_TYPE = ResourceType.UTD
        public static readonly ResourceType BinaryType = ResourceType.UTD;

        // Basic door properties
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:35-46
        // Original: resref: "TemplateResRef" field
        public ResRef ResRef { get; set; }

        // Original: tag: "Tag" field
        public string Tag { get; set; }

        // Original: name: "LocName" field
        public LocalizedString Name { get; set; }

        // Original: description: "Description" field
        public LocalizedString Description { get; set; }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:16
        // Original: def __init__(self):
        public UTD()
        {
            ResRef = ResRef.FromInvalid();
            Tag = string.Empty;
            Name = LocalizedString.FromInvalid();
            Description = LocalizedString.FromInvalid();
        }

        // Additional basic properties that would be implemented in full version
        public int AppearanceId { get; set; }
        public bool GenericType { get; set; }
        public bool Static { get; set; }
        public bool Useable { get; set; }
        public bool Locked { get; set; }
        public bool OpenState { get; set; }
        public bool Plot { get; set; }
        public bool NotBlastable { get; set; }
        public bool AnimLoop { get; set; }
        public bool Hardness { get; set; }
        public bool Fort { get; set; }
        public bool Ref { get; set; }
        public bool Will { get; set; }
        public int HP { get; set; }
        public int CurrentHP { get; set; }
        public int Hardness { get; set; }
        public int Fortitude { get; set; }
        public int Reflex { get; set; }
        public int Will { get; set; }
        public int DC { get; set; }
        public ResRef KeyRequired { get; set; } = ResRef.FromInvalid();
        public ResRef KeyName { get; set; } = ResRef.FromInvalid();

        // Script hooks (simplified)
        public ResRef OnOpen { get; set; } = ResRef.FromInvalid();
        public ResRef OnClosed { get; set; } = ResRef.FromInvalid();
        public ResRef OnDamaged { get; set; } = ResRef.FromInvalid();
        public ResRef OnDeath { get; set; } = ResRef.FromInvalid();
        public ResRef OnHeartbeat { get; set; } = ResRef.FromInvalid();
        public ResRef OnMeleeAttacked { get; set; } = ResRef.FromInvalid();
        public ResRef OnSpellCastAt { get; set; } = ResRef.FromInvalid();
        public ResRef OnUserDefined { get; set; } = ResRef.FromInvalid();
        public ResRef OnLock { get; set; } = ResRef.FromInvalid();
        public ResRef OnUnlock { get; set; } = ResRef.FromInvalid();
        public ResRef OnFailToOpen { get; set; } = ResRef.FromInvalid();
    }
}
