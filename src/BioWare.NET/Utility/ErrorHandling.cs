using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace BioWare.NET.Utility
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/error_handling.py:21-87
    // Original: def universal_simplify_exception(e: BaseException) -> tuple[str, str]:
    /// <summary>
    /// Simplify exceptions into a standardized format.
    /// </summary>
    public static class ErrorHandling
    {
        /// <summary>
        /// Simplify exceptions into a standardized format.
        ///
        /// Args:
        ///     e: Exception - The exception to simplify
        ///
        /// Returns:
        ///     error_name: string - The name of the exception
        ///     error_message: string - A human-readable message for the exception
        /// </summary>
        public static (string errorName, string errorMessage) UniversalSimplifyException(Exception e)
        {
            if (e == null)
            {
                return ("Exception", "Unknown exception");
            }

            string errorName = e.GetType().Name;

            // Handle FileNotFoundError, which has 'filename' attribute
            if (e is FileNotFoundException fnf)
            {
                string filename = fnf.FileName;
                if (!string.IsNullOrEmpty(filename))
                {
                    return (errorName, $"Could not find the file: '{filename}'");
                }
                if (e.Data.Count > 0)
                {
                    return (errorName, $"Could not find the file: '{e.Data.Values.Cast<object>().FirstOrDefault()}'");
                }
                return (errorName, $"Could not find the file: '{e.Message}'");
            }

            // Handle DirectoryNotFoundException
            if (e is DirectoryNotFoundException)
            {
                return (errorName, $"Could not find the directory: '{e.Message}'");
            }

            // Handle PermissionError/UnauthorizedAccessException
            if (e is UnauthorizedAccessException uae)
            {
                return (errorName, $"Permission Denied: {uae.Message}");
            }

            // Handle TimeoutError/TimeoutException
            if (e is TimeoutException)
            {
                return (errorName, $"Operation timed out: {e.Message}");
            }

            // Handle OperationCanceledException
            if (e is OperationCanceledException)
            {
                return (errorName, $"Operation was cancelled: {e.Message}");
            }

            // Handle IOException
            if (e is IOException io)
            {
                return (errorName, $"I/O Error: {io.Message}");
            }

            // Try to extract error details from common exception attributes
            var errorMessages = new global::System.Collections.Generic.List<string>();

            // Check for common exception properties
            var props = e.GetType().GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name == "Message" || prop.Name == "StackTrace")
                {
                    continue; // Skip these as we'll handle them separately
                }

                try
                {
                    var value = prop.GetValue(e);
                    if (value != null)
                    {
                        errorMessages.Add($"- {prop.Name}: {value}");
                    }
                }
                catch
                {
                    // Ignore properties that can't be read
                }
            }

            if (errorMessages.Count > 0)
            {
                string errorDetails = string.Join("\n", errorMessages);
                return (errorName, $"{e}:\n\nDetails:\n{errorDetails}");
            }

            // Fallback to exception message
            string errStr = e.Message;
            if (e.Data.Count > 0)
            {
                errStr += "\n  Information:";
                foreach (var key in e.Data.Keys)
                {
                    errStr += $"\n    {key}: {e.Data[key]}";
                }
            }

            return (errorName, errStr);
        }
    }
}
