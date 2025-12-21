namespace KotorCLI.Logging
{
    /// <summary>
    /// Logger interface for KotorCLI commands.
    /// </summary>
    public interface ILogger
    {
        bool IsDebug { get; }
        void Debug(string message);
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Critical(string message);
    }
}

