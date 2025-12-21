using System;

namespace KotorCLI.Logging
{
    /// <summary>
    /// Verbose logger that shows all info and debug messages.
    /// </summary>
    public class VerboseLogger : StandardLogger
    {
        public VerboseLogger(bool noColor = false) : base(noColor)
        {
        }

        public new void Debug(string message)
        {
            Info($"[DEBUG] {message}");
        }
    }
}

