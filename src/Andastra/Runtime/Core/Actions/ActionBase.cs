using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Base class for all actions.
    /// </summary>
    /// <remarks>
    /// Action Base:
    /// Legacy base class for actions. New code should use engine-specific base classes:
    /// - Odyssey: Runtime.Games.Odyssey.Actions.OdysseyAction
    /// - Aurora: Runtime.Games.Aurora.Actions.AuroraAction
    /// - Common: Runtime.Games.Common.Actions.BaseAction
    /// 
    /// This class is kept for backward compatibility and delegates to BaseAction.
    /// </remarks>
    public abstract class ActionBase : Games.Common.Actions.BaseAction
    {
        protected ActionBase(ActionType type) : base(type)
        {
        }
    }
}

