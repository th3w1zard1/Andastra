using System;
using JetBrains.Annotations;

namespace HolocronToolset.Tests.TestHelpers
{
    /// <summary>
    /// Mock module entry for testing purposes.
    /// Represents a module entry similar to what HTInstallation.ModuleNames() would return.
    /// </summary>
    /// <remarks>
    /// Matching PyKotor test implementation at Tools/HolocronToolset/tests/test_ui_main.py:116-119
    /// Original: QStandardItem objects used to mock modules
    /// This class provides a C# equivalent for mocking module entries in tests.
    /// </remarks>
    public class MockModuleEntry
    {
        /// <summary>
        /// The module filename (e.g., "end_m01aa.rim", "module.mod")
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// The area name extracted from the module (e.g., "Endar Spire", "Unknown Area")
        /// This corresponds to the value in the Dictionary returned by ModuleNames()
        /// </summary>
        [CanBeNull]
        public string AreaName { get; set; }

        /// <summary>
        /// Creates a new mock module entry.
        /// </summary>
        /// <param name="filename">The module filename</param>
        /// <param name="areaName">Optional area name (defaults to null)</param>
        public MockModuleEntry(string filename, [CanBeNull] string areaName = null)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException("Filename cannot be null or empty", nameof(filename));
            }

            Filename = filename;
            AreaName = areaName;
        }

        /// <summary>
        /// Returns the module filename as the string representation.
        /// This matches how RefreshModuleList processes module items (calls ToString()).
        /// </summary>
        /// <returns>The module filename</returns>
        public override string ToString()
        {
            return Filename;
        }

        /// <summary>
        /// Creates a mock module entry with a standard .rim filename.
        /// </summary>
        /// <param name="moduleName">The base module name (without extension)</param>
        /// <param name="areaName">Optional area name</param>
        /// <returns>A MockModuleEntry with .rim extension</returns>
        public static MockModuleEntry CreateRimModule(string moduleName, [CanBeNull] string areaName = null)
        {
            return new MockModuleEntry($"{moduleName}.rim", areaName);
        }

        /// <summary>
        /// Creates a mock module entry with a .mod filename (override module).
        /// </summary>
        /// <param name="moduleName">The base module name (without extension)</param>
        /// <param name="areaName">Optional area name</param>
        /// <returns>A MockModuleEntry with .mod extension</returns>
        public static MockModuleEntry CreateModModule(string moduleName, [CanBeNull] string areaName = null)
        {
            return new MockModuleEntry($"{moduleName}.mod", areaName);
        }

        /// <summary>
        /// Creates a mock module entry with a .erf filename (KotOR 2 dialog module).
        /// </summary>
        /// <param name="moduleName">The base module name (without extension)</param>
        /// <param name="areaName">Optional area name</param>
        /// <returns>A MockModuleEntry with .erf extension</returns>
        public static MockModuleEntry CreateErfModule(string moduleName, [CanBeNull] string areaName = null)
        {
            return new MockModuleEntry($"{moduleName}_dlg.erf", areaName);
        }
    }
}

