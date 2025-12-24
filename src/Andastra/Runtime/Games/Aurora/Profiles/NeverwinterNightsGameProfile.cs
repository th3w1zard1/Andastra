using Andastra.Parsing;
using Andastra.Runtime.Engines.Common;

namespace Andastra.Runtime.Engines.Aurora.Profiles
{
    /// <summary>
    /// Game profile for Neverwinter Nights (Aurora Engine).
    /// </summary>
    /// <remarks>
    /// Neverwinter Nights Game Profile:
    /// - Based on nwmain.exe: Neverwinter Nights executable
    /// - Aurora Engine game profile for NWN 1
    /// - Resource system: HAK files, module files, override directory
    /// - Original implementation: nwmain.exe initializes Aurora Engine resource system
    /// </remarks>
    public class NeverwinterNightsGameProfile : AuroraEngineProfile
    {
        public override string GameType
        {
            get { return "NWN"; }
        }

        public override string Name
        {
            get { return "Neverwinter Nights"; }
        }

        protected override void InitializeSupportedFeatures()
        {
            // Neverwinter Nights 1 features
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
        }
    }
}

