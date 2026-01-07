using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Aurora.Components
{
    /// <summary>
    /// Aurora engine-specific implementation of item component.
    /// </summary>
    /// <remarks>
    /// Aurora Item Component:
    /// - Inherits common functionality from BaseItemComponent
    /// - Implements Aurora-specific item system features
    /// - Based on nwmain.exe and nwn2main.exe item systems
    ///
    /// Aurora-specific details:
    /// - nwmain.exe: Aurora item component system using UTI format (identical to Odyssey)
    /// - nwn2main.exe: Enhanced Aurora item system with additional features
    /// - Uses same UTI file format as Odyssey engines (GFF with "UTI " signature)
    /// - Item component structure identical to Odyssey: BaseItem, Properties, Charges, Cost, StackSize, Identified
    /// - Property system and cost calculation identical to Odyssey
    /// - Item events similar to Odyssey but with Aurora-specific event names
    ///
    /// Aurora-specific features:
    /// - UTI file format: GFF with "UTI " signature containing item data (BaseItem, Properties, Charges, Cost, StackSize, Identified)
    /// - Item properties modify item behavior (damage bonuses, AC bonuses, effects, etc.) - stored as PropertyList array in UTI
    /// - Charges: -1 = unlimited charges, 0+ = limited charges
    /// - Stack size: 1 = not stackable, 2+ = stackable (maximum stack size from baseitems.2da MaxStackSize column)
    /// - Identified: false = unidentified item (shows generic name, can be identified via IdentifyItem NWScript function)
    /// - Item value: Cost field stores item base value (for selling/trading, modified by merchant markups)
    /// - Item properties: Properties array contains ItemProperty entries (PropertyName, Subtype, CostValue, ParamTable, etc.)
    /// - Based on UTI file format documentation in vendor/PyKotor/wiki/ and Bioware Aurora Item Format specification
    /// </remarks>
    public class AuroraItemComponent : BaseItemComponent
    {
        // Aurora-specific implementation can override base methods or add new functionality as needed
        // All common functionality is inherited from BaseItemComponent
    }
}

