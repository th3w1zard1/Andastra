using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Reader;
using IniParser.Model;
using IniParser.Parser;

namespace Andastra.Parsing.Tests.Integration
{

    /// <summary>
    /// Base class for integration tests providing common setup/teardown and helper methods.
    /// Ported from test_tslpatcher.py base infrastructure.
    /// </summary>
    public abstract class IntegrationTestBase : IDisposable
    {
        protected string TempDir { get; private set; }
        protected string TslPatchDataPath { get; private set; }
        protected IniDataParser Parser { get; private set; }
        protected PatchLogger Logger { get; private set; }
        protected PatcherMemory Memory { get; private set; }

        protected IntegrationTestBase()
        {
            TempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            TslPatchDataPath = Path.Combine(TempDir, "tslpatchdata");
            Directory.CreateDirectory(TslPatchDataPath);

            Parser = new IniDataParser();
            Parser.Configuration.AllowDuplicateKeys = true;
            Parser.Configuration.AllowDuplicateSections = true;
            Parser.Configuration.CaseInsensitive = false;

            Logger = new PatchLogger();
            Memory = new PatcherMemory();
        }

        public virtual void Dispose()
        {
            if (Directory.Exists(TempDir))
            {
                try
                {
                    Directory.Delete(TempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Sets up an INI configuration and parses it into a PatcherConfig.
        /// </summary>
        protected PatcherConfig SetupIniAndConfig(string iniText, string modPath = null)
        {
            // Use unified INI parser (case-sensitive for changes.ini files)
            IniData ini = Andastra.Parsing.Reader.ConfigReader.ParseIniText(iniText, caseInsensitive: false);
            var config = new PatcherConfig();
            string actualModPath = modPath ?? TempDir;

            var reader = new ConfigReader(ini, actualModPath, Logger, TslPatchDataPath);
            reader.Load(config);

            return config;
        }

        /// <summary>
        /// Creates a simple test 2DA file with specified columns and rows.
        /// </summary>
        protected static TwoDA CreateTest2DA(string[] columns, (string label, string[] values)[] rows)
        {
            var twoda = new TwoDA(columns.ToList());

            foreach ((string label, string[] values) in rows)
            {
                var cells = new Dictionary<string, object>();
                for (int i = 0; i < values.Length && i < columns.Length; i++)
                {
                    cells[columns[i]] = values[i];
                }
                twoda.AddRow(label, cells);
            }

            return twoda;
        }

        /// <summary>
        /// Creates a test TLK file with specified entries.
        /// </summary>
        protected static TLK CreateTestTLK((string text, string sound)[] entries)
        {
            var tlk = new TLK(Language.English);
            foreach ((string text, string sound) in entries)
            {
                tlk.Add(text, sound);
            }
            return tlk;
        }

        /// <summary>
        /// Saves a TLK file to the tslpatchdata directory.
        /// </summary>
        protected void SaveTestTLK(string filename, TLK tlk)
        {
            string path = Path.Combine(TslPatchDataPath, filename);
            tlk.Save(path);
        }

        /// <summary>
        /// Saves a 2DA file to the tslpatchdata directory.
        /// </summary>
        protected void SaveTest2DA(string filename, TwoDA twoda)
        {
            string path = Path.Combine(TslPatchDataPath, filename);
            twoda.Save(path);
        }

        /// <summary>
        /// Asserts that a 2DA cell contains the expected value.
        /// </summary>
        protected static void AssertCellValue(TwoDA twoda, string rowLabel, string columnLabel, string expectedValue)
        {
            int rowIndex = twoda.GetRowIndex(rowLabel);
            string actualValue = twoda.GetCellString(rowIndex, columnLabel) ?? string.Empty;
            if (!actualValue.Equals(expectedValue, StringComparison.Ordinal))
            {
                throw new Exception($"Expected cell [{rowLabel},{columnLabel}] to be '{expectedValue}' but was '{actualValue}'");
            }
        }

        /// <summary>
        /// Asserts that a 2DA cell contains the expected value (by row index).
        /// </summary>
        protected static void AssertCellValue(TwoDA twoda, int rowIndex, string columnLabel, string expectedValue)
        {
            string actualValue = twoda.GetCellString(rowIndex, columnLabel) ?? string.Empty;
            if (!actualValue.Equals(expectedValue, StringComparison.Ordinal))
            {
                throw new Exception($"Expected cell [{rowIndex},{columnLabel}] to be '{expectedValue}' but was '{actualValue}'");
            }
        }
    }
}
