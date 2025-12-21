using System;
using System.Collections.Generic;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Engines.Eclipse.EngineApi
{
    /// <summary>
    /// Dragon Age 2 specific engine API implementation (DragonAge2.exe).
    /// </summary>
    /// <remarks>
    /// Dragon Age 2 Engine API:
    /// - Based on DragonAge2.exe: Eclipse/Unreal Engine script API
    /// - Inherits common Eclipse functions from EclipseEngineApi
    /// - Contains only DA2-specific function implementations that differ from common Eclipse or DAO
    /// - Game-specific details: DA2-specific function IDs, parameter differences, or behavior variations
    /// - Cross-engine: Most functions are common with DragonAgeOriginsEngineApi, only differences are in this class
    /// </remarks>
    public class DragonAge2EngineApi : EclipseEngineApi
    {
        public DragonAge2EngineApi()
            : base()
        {
        }

        protected override void RegisterFunctions()
        {
            // Register all common Eclipse functions from base class
            base.RegisterFunctions();
            
            // Register DA2-specific function names if any differ from common Eclipse
            // Most functions are common, so this is typically empty unless DA2 has unique functions
        }

        public override Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Try DA2-specific implementations first
            // If no DA2-specific implementation, delegate to base class (common Eclipse functions)
            
            switch (routineId)
            {
                // DA2-specific function overrides would go here
                // TODO: STUB - For now, all functions are common between DAO and DA2, so delegate to base
                
                default:
                    // Delegate to base class for common Eclipse functions
                    return base.CallEngineFunction(routineId, args, ctx);
            }
        }
    }
}

