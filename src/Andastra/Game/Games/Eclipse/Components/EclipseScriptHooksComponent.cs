using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse Engine (Dragon Age Origins, Dragon Age 2) specific script hooks component.
    /// </summary>
    /// <remarks>
    /// Eclipse Script Hooks Component:
    /// - Inherits from BaseScriptHooksComponent (common functionality)
    /// - Eclipse-specific: No engine-specific differences identified - uses base implementation
    /// - Based on daorigins.exe and DragonAge2.exe script event system
    /// - Eclipse engine uses UnrealScript-based event dispatching (different from Odyssey's NWScript)
    /// - Script hooks stored in entity templates and can be set/modified at runtime
    /// - Event system architecture:
    ///   - Event data structures: "EventListeners" @ 0x00ae8194 (daorigins.exe), 0x00bf543c (DragonAge2.exe)
    ///   - Event identifiers: "EventId" @ 0x00ae81a4 (daorigins.exe), 0x00bf544c (DragonAge2.exe)
    ///   - Event scripts: "EventScripts" @ 0x00ae81bc (daorigins.exe), 0x00bf5464 (DragonAge2.exe)
    ///   - Enabled events: "EnabledEvents" @ 0x00ae81ac (daorigins.exe), 0x00bf5454 (DragonAge2.exe)
    ///   - Event list: "EventList" @ 0x00aedb74 (daorigins.exe), 0x00c01250 (DragonAge2.exe)
    /// - Command-based event system: "COMMAND_SIGNALEVENT" @ 0x00af4180 (daorigins.exe) processes event commands
    ///   - Command processing: Events are dispatched through UnrealScript command system
    ///   - Event commands: COMMAND_HANDLEEVENT, COMMAND_SETEVENTSCRIPT, COMMAND_ENABLEEVENT, etc.
    /// - UnrealScript event dispatcher: Uses BioEventDispatcher interface
    ///   - Function names (UnrealScript): "intUBioEventDispatcherexecDispatch" (: 0x117e7b90)
    ///   - Function names (UnrealScript): "intUBioEventDispatcherexecSubscribe" (: 0x117e7c28)
    ///   - Function names (UnrealScript): "intUBioEventDispatcherexecUnsubscribe" (: 0x117e7bd8)
    ///   - Note: These are UnrealScript interface functions, not direct C++ addresses
    /// - Maps script events to script resource references (ResRef strings)
    /// - Scripts are executed by UnrealScript VM when events fire (OnHeartbeat, OnPerception, OnAttacked, etc.)
    /// - Script ResRefs stored in entity templates and save game files
    /// - Local variables (int, float, string) stored per-entity for script execution context
    /// - Local variables accessed via GetLocalInt/GetLocalFloat/GetLocalString script functions
    /// - Script execution context: Entity is caller (OBJECT_SELF), event triggerer is parameter
    /// - Eclipse-specific: Uses UnrealScript instead of NWScript, but script hooks interface is compatible
    /// </remarks>
    public class EclipseScriptHooksComponent : BaseScriptHooksComponent
    {
        // Eclipse-specific implementation: Currently uses base class implementation
        // No engine-specific differences identified - all script hooks functionality is common
        // Eclipse uses UnrealScript for execution, but the script hooks storage and interface are identical
    }
}

