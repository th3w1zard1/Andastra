using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.NCS
{
    /// <summary>
    /// Stub logger implementation for NCS compiler to avoid circular dependencies
    /// </summary>
    public class NcsPatchLogger
    {
        public void AddError(string message)
        {
            Console.Error.WriteLine($"ERROR: {message}");
        }

        public void AddWarning(string message)
        {
            Console.WriteLine($"WARNING: {message}");
        }

        public void AddNote(string message)
        {
            Console.WriteLine($"NOTE: {message}");
        }

        public void AddVerbose(string message)
        {
            Console.WriteLine($"VERBOSE: {message}");
        }
    }
}