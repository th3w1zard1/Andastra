using Andastra.Parsing;
using Andastra.Runtime.Engines.Common;

namespace Andastra.Runtime.Engines.Aurora.Profiles
{
    /// <summary>
    /// Game profile for Neverwinter Nights 2 (Aurora Engine).
    /// </summary>
    /// <remarks>
    /// Neverwinter Nights 2 Game Profile:
    /// - Based on nwn2main.exe: Neverwinter Nights 2 executable
    /// - Aurora Engine game profile for NWN 2
    /// - Resource system: HAK files, module files, override directory (enhanced from NWN 1)
    /// - Original implementation: nwn2main.exe initializes Aurora Engine resource system
    /// - Enhanced features over NWN 1: Improved graphics, expanded NWScript functions, enhanced toolset
    /// </remarks>
    public class NeverwinterNights2GameProfile : AuroraEngineProfile
    {
        public override string GameType
        {
            get { return "NWN2"; }
        }

        public override string Name
        {
            get { return "Neverwinter Nights 2"; }
        }

        protected override void InitializeSupportedFeatures()
        {
            // Neverwinter Nights 2 features (includes all NWN 1 features plus enhancements)
            _supportedFeatures.Add("NWScript");
            _supportedFeatures.Add("ModuleSystem");
            _supportedFeatures.Add("HAKFiles");
            _supportedFeatures.Add("OverrideDirectory");
            _supportedFeatures.Add("DialogueSystem");
            _supportedFeatures.Add("CombatSystem");
            _supportedFeatures.Add("SpellSystem");
            _supportedFeatures.Add("FeatSystem");
            _supportedFeatures.Add("SkillSystem");
            _supportedFeatures.Add("ItemSystem");
            _supportedFeatures.Add("CreatureSystem");
            _supportedFeatures.Add("PlaceableSystem");
            _supportedFeatures.Add("DoorSystem");
            _supportedFeatures.Add("AreaSystem");
            _supportedFeatures.Add("TriggerSystem");
            _supportedFeatures.Add("WaypointSystem");
            _supportedFeatures.Add("EncounterSystem");
            _supportedFeatures.Add("StoreSystem");
            _supportedFeatures.Add("ConversationSystem");
            _supportedFeatures.Add("QuestSystem");
            _supportedFeatures.Add("EnhancedGraphics");
            _supportedFeatures.Add("ExpandedNWScript");
            _supportedFeatures.Add("EnhancedToolset");
        }
    }
}

