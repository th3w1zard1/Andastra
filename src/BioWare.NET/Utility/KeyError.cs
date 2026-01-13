using System;
using System.Collections.Generic;

namespace BioWare.NET.Utility
{

    /// <summary>
    /// Exception thrown when a required key is not found in memory or a dictionary.
    /// Equivalent to Python's KeyError.
    /// </summary>
    public class KeyError : KeyNotFoundException
    {
        public KeyError(string message) : base(message)
        {
        }

        public KeyError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

