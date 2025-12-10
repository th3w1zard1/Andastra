using CSharpKOTOR.Common;
using CSharpKOTOR.Resource.Formats.GFF;
using JetBrains.Annotations;

namespace CSharpKOTOR.Resource.Generics
{
    /// <summary>
    /// Stores placeable data.
    ///
    /// UTP files are GFF-based format files that store placeable object definitions including
    /// lock/unlock mechanics, HP, inventory, scripts, and appearance.
    /// </summary>
    [PublicAPI]
    public sealed class UTP
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:17
        // Original: BINARY_TYPE = ResourceType.UTP
        public static readonly ResourceType BinaryType = ResourceType.UTP;

        // Basic placeable properties
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:33-41
        // Original: resref: "TemplateResRef" field
        public ResRef ResRef { get; set; }

        // Original: tag: "Tag" field
        public string Tag { get; set; }

        // Original: name: "LocName" field
        public LocalizedString Name { get; set; }

        // Original: appearance_id: "Appearance" field
        public int AppearanceId { get; set; }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:17
        // Original: def __init__(self):
        public UTP()
        {
            ResRef = ResRef.FromInvalid();
            Tag = string.Empty;
            Name = LocalizedString.FromInvalid();
            AppearanceId = 0;
        }

        // Additional basic properties that would be implemented in full version
        public LocalizedString Description { get; set; } = LocalizedString.FromInvalid();
        public bool Static { get; set; }
        public bool Useable { get; set; }
        public bool Locked { get; set; }
        public bool Plot { get; set; }
        public bool HasInventory { get; set; }
        public bool PartyInteract { get; set; }
        public bool BodyBag { get; set; }
        public bool DeadSelectable { get; set; }
        public int HP { get; set; }
        public int CurrentHP { get; set; }
        public int Hardness { get; set; }
        public int Fortitude { get; set; }
        public int Reflex { get; set; }
        public int Will { get; set; }
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
