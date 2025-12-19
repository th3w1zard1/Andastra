using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// FIFO action queue for entity actions.
    /// </summary>
    /// <remarks>
    /// Action Queue System:
    /// Legacy action queue class. New code should use engine-specific action queue classes:
    /// - Odyssey: Runtime.Games.Odyssey.Actions.OdysseyActionQueue
    /// - Aurora: Runtime.Games.Aurora.Actions.AuroraActionQueue
    /// - Common: Runtime.Games.Common.Actions.BaseActionQueue
    /// 
    /// This class is kept for backward compatibility and delegates to BaseActionQueue.
    /// </remarks>
    public class ActionQueue : Games.Common.Actions.BaseActionQueue
    {
        public ActionQueue() : base()
        {
        }

        public ActionQueue(IEntity owner) : base(owner)
        {
        }
    }
}

