using System;
using System.Collections.Generic;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Engines.Eclipse.EngineApi
{
    /// <summary>
    /// Dragon Age: Origins specific engine API implementation (daorigins.exe).
    /// </summary>
    /// <remarks>
    /// Dragon Age: Origins Engine API:
    /// - Based on daorigins.exe: Eclipse/Unreal Engine script API
    /// - Inherits common Eclipse functions from EclipseEngineApi
    /// - Contains only DAO-specific function implementations that differ from common Eclipse or DA2
    /// - Game-specific details: DAO-specific function IDs, parameter differences, or behavior variations
    /// - Cross-engine: Most functions are common with DragonAge2EngineApi, only differences are in this class
    /// </remarks>
    public class DragonAgeOriginsEngineApi : EclipseEngineApi
    {
        public DragonAgeOriginsEngineApi()
            : base()
        {
        }

        protected override void RegisterFunctions()
        {
            // Register all common Eclipse functions from base class
            base.RegisterFunctions();
            
            // Register DAO-specific function names if any differ from common Eclipse
            // Most functions are common, so this is typically empty unless DAO has unique functions
        }

        public override Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Try DAO-specific implementations first
            // If no DAO-specific implementation, delegate to base class (common Eclipse functions)
            
            switch (routineId)
            {
                // DAO-specific function overrides would go here
                // TODO: STUB - For now, all functions are common between DAO and DA2, so delegate to base
                
                default:
                    // Delegate to base class for common Eclipse functions
                    return base.CallEngineFunction(routineId, args, ctx);
            }
        }
    }
}

