using System;

namespace HoloPatcher.UI.Update
{
    /// <summary>
    /// Interface for reporting update progress without creating a circular dependency.
    /// </summary>
    public interface IUpdateProgress
    {
        void ReportStatus(string status);
        void ReportDownload(long downloadedBytes, long? totalBytes, TimeSpan? eta = null);
    }
}

