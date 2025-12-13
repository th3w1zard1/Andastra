// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:845-869
// Original: class DiffAnalyzerFactory: ...
using System;
using System.Collections.Generic;
using System.IO;
using CSharpKOTOR.Diff;
using CSharpKOTOR.Mods;

namespace KotorDiff.NET.Diff
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:89-104
    // Original: class DiffAnalyzer(ABC): ...
    public abstract class DiffAnalyzer
    {
        public abstract object Analyze(byte[] leftData, byte[] rightData, string identifier);
    }

    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:845-869
    // Original: class DiffAnalyzerFactory: ...
    public static class DiffAnalyzerFactory
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:848-869
        // Original: def get_analyzer(resource_type: str) -> DiffAnalyzer | None: ...
        public static DiffAnalyzer GetAnalyzer(string resourceType)
        {
            if (string.IsNullOrEmpty(resourceType))
            {
                return null;
            }

            string resourceTypeLower = resourceType.ToLowerInvariant();

            // 2DA analyzer
            if (resourceTypeLower == "2da" || resourceTypeLower == "twoda")
            {
                return new TwoDADiffAnalyzerWrapper();
            }

            // GFF analyzer
            if (resourceTypeLower == "gff" || 
                resourceTypeLower == "utc" || resourceTypeLower == "uti" || resourceTypeLower == "utp" ||
                resourceTypeLower == "ute" || resourceTypeLower == "utm" || resourceTypeLower == "utd" ||
                resourceTypeLower == "utw" || resourceTypeLower == "dlg" || resourceTypeLower == "are" ||
                resourceTypeLower == "git" || resourceTypeLower == "ifo" || resourceTypeLower == "gui" ||
                resourceTypeLower == "jrl" || resourceTypeLower == "fac")
            {
                return new GFFDiffAnalyzerWrapper();
            }

            // TLK analyzer
            if (resourceTypeLower == "tlk")
            {
                return new TLKDiffAnalyzerWrapper();
            }

            // SSF analyzer
            if (resourceTypeLower == "ssf")
            {
                return new SSFDiffAnalyzerWrapper();
            }

            return null;
        }
    }

    // Wrapper for TwoDADiffAnalyzer from CSharpKOTOR
    internal class TwoDADiffAnalyzerWrapper : DiffAnalyzer
    {
        public override object Analyze(byte[] leftData, byte[] rightData, string identifier)
        {
            var analyzer = new TwoDaDiffAnalyzer();
            return analyzer.Analyze(leftData, rightData, identifier);
        }
    }

    // Wrapper for GFF analyzer - uses CSharpKOTOR's GffDiff
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:296-688
    // Original: class GFFDiffAnalyzer(DiffAnalyzer): ...
    internal class GFFDiffAnalyzerWrapper : DiffAnalyzer
    {
        public override object Analyze(byte[] leftData, byte[] rightData, string identifier)
        {
            try
            {
                // Read GFF files
                var leftReader = new CSharpKOTOR.Formats.GFF.GFFBinaryReader(leftData);
                var rightReader = new CSharpKOTOR.Formats.GFF.GFFBinaryReader(rightData);
                var leftGff = leftReader.Load();
                var rightGff = rightReader.Load();

                // Extract filename from identifier
                string filename = System.IO.Path.GetFileName(identifier);
                var modifications = new CSharpKOTOR.Mods.GFF.ModificationsGFF(filename, replace: false);

                // Use GffDiff to find differences
                var compareResult = CSharpKOTOR.Diff.GffDiff.Compare(leftGff.Root, rightGff.Root);

                // Convert differences to ModificationsGFF
                foreach (var (path, oldValue, newValue) in compareResult.Differences)
                {
                    if (newValue != null)
                    {
                        // Create ModifyFieldGFF for changed/added fields
                        var modifyField = new CSharpKOTOR.Mods.GFF.ModifyFieldGFF(
                            path: path.Replace("/", "\\"), // TSLPatcher uses backslashes
                            value: new CSharpKOTOR.Mods.GFF.FieldValueConstant(newValue)
                        );
                        modifications.Modifiers.Add(modifyField);
                    }
                }

                return modifications.Modifiers.Count > 0 ? modifications : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    // Wrapper for TLK analyzer - uses CSharpKOTOR's TlkDiff
    internal class TLKDiffAnalyzerWrapper : DiffAnalyzer
    {
        public override object Analyze(byte[] leftData, byte[] rightData, string identifier)
        {
            try
            {
                // Read TLK files
                var leftReader = new CSharpKOTOR.Formats.TLK.TLKBinaryReader(leftData);
                var rightReader = new CSharpKOTOR.Formats.TLK.TLKBinaryReader(rightData);
                var leftTlk = leftReader.Load();
                var rightTlk = rightReader.Load();

                // Use existing TlkDiff comparison
                var compareResult = CSharpKOTOR.Diff.TlkDiff.Compare(leftTlk, rightTlk);

                // Generate ModificationsTLK from comparison result
                string filename = System.IO.Path.GetFileName(identifier);
                var modifications = new CSharpKOTOR.Mods.TLK.ModificationsTLK("append.tlk", false);
                modifications.SaveAs = filename;

                int tokenId = 0;
                var strrefMappings = new Dictionary<int, int>();

                // Process changed entries
                foreach (var kvp in compareResult.ChangedEntries)
                {
                    int idx = kvp.Key;
                    var entry = kvp.Value;
                    var modify = new CSharpKOTOR.Mods.TLK.ModifyTLK(tokenId, false);
                    modify.ModIndex = idx;
                    modify.Text = entry.Text ?? "";
                    modify.Sound = entry.Sound ?? "";
                    modifications.Modifiers.Add(modify);
                    strrefMappings[idx] = tokenId;
                    tokenId++;
                }

                // Process added entries
                foreach (var kvp in compareResult.AddedEntries)
                {
                    int idx = kvp.Key;
                    var (text, sound) = kvp.Value;
                    var modify = new CSharpKOTOR.Mods.TLK.ModifyTLK(tokenId, false);
                    modify.ModIndex = idx;
                    modify.Text = text ?? "";
                    modify.Sound = sound ?? "";
                    modifications.Modifiers.Add(modify);
                    strrefMappings[idx] = tokenId;
                    tokenId++;
                }

                if (modifications.Modifiers.Count > 0)
                {
                    // Return tuple: (modifications, strref_mappings) per Python implementation
                    return (modifications, strrefMappings);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    // Wrapper for SSF analyzer - uses CSharpKOTOR's SsfDiff
    internal class SSFDiffAnalyzerWrapper : DiffAnalyzer
    {
        public override object Analyze(byte[] leftData, byte[] rightData, string identifier)
        {
            try
            {
                // Read SSF files
                var leftReader = new CSharpKOTOR.Formats.SSF.SSFBinaryReader(leftData);
                var rightReader = new CSharpKOTOR.Formats.SSF.SSFBinaryReader(rightData);
                var leftSsf = leftReader.Load();
                var rightSsf = rightReader.Load();

                // Use existing SsfDiff comparison
                var compareResult = CSharpKOTOR.Diff.SsfDiff.Compare(leftSsf, rightSsf);

                // Generate ModificationsSSF from comparison result
                string filename = System.IO.Path.GetFileName(identifier);
                var modifications = new CSharpKOTOR.Mods.SSF.ModificationsSSF(filename, false);

                // Process changed sounds
                foreach (var kvp in compareResult.ChangedSounds)
                {
                    var sound = kvp.Key;
                    int stringref = kvp.Value;
                    var modify = new CSharpKOTOR.Mods.SSF.ModifySSF(sound, new CSharpKOTOR.Memory.NoTokenUsage(stringref.ToString()));
                    modifications.Modifiers.Add(modify);
                }

                return modifications.Modifiers.Count > 0 ? modifications : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

