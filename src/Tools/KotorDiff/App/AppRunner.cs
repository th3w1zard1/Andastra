// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:582-631
// Original: def run_application(config: KotorDiffConfig) -> int: ...
using System;

namespace KotorDiff.AppCore
{
    /// <summary>
    /// Main application runner for KotorDiff.
    /// 1:1 port of run_application from vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:582-631
    /// </summary>
    public static class AppRunner
    {
        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:582-631
        // Original: def run_application(config: KotorDiffConfig) -> int: ...
        /// <summary>
        /// Run the main KotorDiff application with parsed configuration.
        /// </summary>
        public static int RunApplication(KotorDiffConfig config)
        {
            // Store config in global config
            GlobalConfig.Instance.Config = config;
            GlobalConfig.Instance.LoggingEnabled = config.LoggingEnabled;

            // Set up output log path
            if (config.OutputLogPath != null)
            {
                GlobalConfig.Instance.OutputLog = config.OutputLogPath;
            }

            // Set up the logging system
            DiffApplicationHelpers.SetupLogging(config);

            // Log configuration
            DiffApplicationHelpers.LogConfiguration(config);

            // Run with optional profiler
            // Python: if config.use_profiler: profiler = cProfile.Profile(); profiler.enable()
            Profiler profiler = null;
            try
            {
                if (config.UseProfiler)
                {
                    profiler = new Profiler();
                    profiler.Enable();
                }

                var comparison = DiffApplicationHelpers.ExecuteDiff(config);

                if (profiler != null)
                {
                    DiffApplicationHelpers.StopProfiler(profiler);
                }

                // Format and return final output
                if (comparison.HasValue)
                {
                    return DiffApplicationHelpers.FormatComparisonOutput(comparison.Value, config);
                }

                // If comparison is null, check if we have an exit code from HandleDiff
                var handleDiffResult = DiffApplicationHelpers.HandleDiff(config);
                return handleDiffResult.exitCode ?? 0;
            }
            catch (Exception ex)
            {
                if (profiler != null)
                {
                    DiffApplicationHelpers.StopProfiler(profiler);
                }
                DiffApplicationHelpers.LogOutput($"KeyboardInterrupt - KotorDiff was cancelled by user: {ex.Message}");
                throw;
            }
        }
    }
}
