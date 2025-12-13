// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:21-40
// Original: class DiffFormatter(ABC): ...
using System;
using KotorDiff.NET.Diff.Objects;

namespace KotorDiff.NET.Formatters
{
    /// <summary>
    /// Abstract base class for diff formatters.
    /// 1:1 port of DiffFormatter from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:21-40
    /// </summary>
    public abstract class DiffFormatter
    {
        protected Action<string> OutputFunc { get; set; }

        protected DiffFormatter(Action<string> outputFunc = null)
        {
            OutputFunc = outputFunc ?? Console.WriteLine;
        }

        /// <summary>
        /// Format a diff result into a string.
        /// </summary>
        public abstract string FormatDiff<T>(DiffResult<T> diffResult);

        /// <summary>
        /// Output a formatted diff result.
        /// </summary>
        public void OutputDiff<T>(DiffResult<T> diffResult)
        {
            string formatted = FormatDiff(diffResult);
            if (!string.IsNullOrEmpty(formatted))
            {
                OutputFunc(formatted);
            }
        }
    }
}

