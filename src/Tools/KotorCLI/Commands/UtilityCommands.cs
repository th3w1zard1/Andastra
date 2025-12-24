using System;
using System.CommandLine;
using KotorCLI.Logging;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing.Resource.Formats.GFF;
using Andastra.Parsing.Resource.Formats.ERF;
using Andastra.Parsing.Resource.Formats.TLK;
using Andastra.Parsing.Resource.Formats.NCS;
using Andastra.Parsing.Resource.Formats.BIF;
using Andastra.Parsing.Resource.Formats.TwoDA;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Utility commands (diff, grep, stats, validate, merge, cat).
    /// </summary>
    public static class UtilityCommands
    {
        /// <summary>
        /// Interface for analyzing file statistics
        /// </summary>
        public interface IFileStatsAnalyzer
        {
            bool CanAnalyze(string filePath);
            FileStats Analyze(string filePath);
        }

        /// <summary>
        /// Base class for file statistics
        /// </summary>
        public class FileStats
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
            public long FileSize { get; set; }
            public string Format { get; set; }
            public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
            public List<string> Errors { get; set; } = new List<string>();
            public bool IsValid { get; set; } = true;

            public virtual void PrintToLogger(ILogger logger)
            {
                logger.Info($"File: {FileName}");
                logger.Info($"Path: {FilePath}");
                logger.Info($"Size: {FileSize:N0} bytes ({GetReadableFileSize(FileSize)})");
                logger.Info($"Format: {Format}");
                logger.Info($"Valid: {IsValid}");

                if (Errors.Any())
                {
                    logger.Info("Errors:");
                    foreach (var error in Errors)
                    {
                        logger.Info($"  - {error}");
                    }
                }

                if (Properties.Any())
                {
                    logger.Info("Properties:");
                    foreach (var kvp in Properties.OrderBy(x => x.Key))
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value}");
                    }
                }
            }

            protected static string GetReadableFileSize(long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// GFF file statistics analyzer
        /// </summary>
        public class GFFStatsAnalyzer : IFileStatsAnalyzer
        {
            public bool CanAnalyze(string filePath)
            {
                if (!File.Exists(filePath)) return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var supportedExtensions = new[] { ".utc", ".utd", ".ute", ".uti", ".utm", ".utp", ".uts", ".utt", ".utw", ".git", ".ifo", ".are", ".fac", ".dlg", ".jrl", ".sav", ".gff" };
                if (!supportedExtensions.Contains(extension)) return false;

                // Check magic bytes
                try
                {
                    using (var fs = File.OpenRead(filePath))
                    using (var reader = new BinaryReader(fs))
                    {
                        if (fs.Length < 8) return false;
                        var magic = reader.ReadBytes(4);
                        var version = reader.ReadBytes(4);

                        // GFF files start with file type (like "UTC ", "UTD ", etc.) followed by version
                        return version.SequenceEqual(new byte[] { (byte)'V', (byte)'3', (byte)'.', (byte)'2' }) ||
                               version.SequenceEqual(new byte[] { (byte)'V', (byte)'3', (byte)'.', (byte)'3' });
                    }
                }
                catch
                {
                    return false;
                }
            }

            public FileStats Analyze(string filePath)
            {
                var stats = new GFFFileStats
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Format = "GFF (Generic File Format)"
                };

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        var gff = GFFBinaryReader.Load(fs);

                        // Basic structure counts
                        stats.StructCount = gff.Structs.Count;
                        stats.FieldCount = gff.Fields.Count;
                        stats.LabelCount = gff.Labels.Count;
                        stats.FieldDataSize = gff.FieldData.Count;
                        stats.FieldIndicesSize = gff.FieldIndices.Count;
                        stats.ListIndicesSize = gff.ListIndices.Count;

                        // Analyze field types
                        var fieldTypeCounts = new Dictionary<GFFFieldType, int>();
                        foreach (var field in gff.Fields)
                        {
                            if (!fieldTypeCounts.ContainsKey(field.Type))
                                fieldTypeCounts[field.Type] = 0;
                            fieldTypeCounts[field.Type]++;
                        }
                        stats.FieldTypeCounts = fieldTypeCounts;

                        // Get root struct type if available
                        if (gff.Root.StructId >= 0)
                        {
                            stats.RootStructType = gff.Root.StructId;
                        }

                        // File type from header
                        stats.FileType = gff.Header.FileType;

                        // Calculate compression ratio (GFF is typically uncompressed)
                        stats.CompressionRatio = 1.0;

                        // Get structure depth
                        stats.MaxDepth = CalculateMaxDepth(gff.Root, gff);

                        // Count different data types
                        stats.StringFieldCount = fieldTypeCounts.GetValueOrDefault(GFFFieldType.CExoString, 0);
                        stats.IntegerFieldCount = fieldTypeCounts.GetValueOrDefault(GFFFieldType.DWORD, 0) +
                                                fieldTypeCounts.GetValueOrDefault(GFFFieldType.INT, 0) +
                                                fieldTypeCounts.GetValueOrDefault(GFFFieldType.SHORT, 0) +
                                                fieldTypeCounts.GetValueOrDefault(GFFFieldType.BYTE, 0);
                        stats.FloatFieldCount = fieldTypeCounts.GetValueOrDefault(GFFFieldType.FLOAT, 0);
                        stats.StructFieldCount = fieldTypeCounts.GetValueOrDefault(GFFFieldType.Struct, 0);
                        stats.ListFieldCount = fieldTypeCounts.GetValueOrDefault(GFFFieldType.List, 0);
                        stats.VectorFieldCount = fieldTypeCounts.GetValueOrDefault(GFFFieldType.Vector, 0);
                    }
                }
                catch (Exception ex)
                {
                    stats.IsValid = false;
                    stats.Errors.Add($"Failed to parse GFF file: {ex.Message}");
                }

                return stats;
            }

            private int CalculateMaxDepth(GFFStruct root, GFF gff, int currentDepth = 0)
            {
                int maxDepth = currentDepth;

                foreach (var field in root)
                {
                    if (field.FieldType == GFFFieldType.Struct && field.Value is GFFStruct childStruct)
                    {
                        int childDepth = CalculateMaxDepth(childStruct, gff, currentDepth + 1);
                        maxDepth = Math.Max(maxDepth, childDepth);
                    }
                    else if (field.FieldType == GFFFieldType.List && field.Value is GFFList list)
                    {
                        foreach (var listStruct in list)
                        {
                            int childDepth = CalculateMaxDepth(listStruct, gff, currentDepth + 1);
                            maxDepth = Math.Max(maxDepth, childDepth);
                        }
                    }
                }

                return maxDepth;
            }
        }

        /// <summary>
        /// GFF-specific file statistics
        /// </summary>
        public class GFFFileStats : FileStats
        {
            public int StructCount { get; set; }
            public int FieldCount { get; set; }
            public int LabelCount { get; set; }
            public int FieldDataSize { get; set; }
            public int FieldIndicesSize { get; set; }
            public int ListIndicesSize { get; set; }
            public string FileType { get; set; }
            public int RootStructType { get; set; }
            public double CompressionRatio { get; set; }
            public int MaxDepth { get; set; }
            public Dictionary<GFFFieldType, int> FieldTypeCounts { get; set; }
            public int StringFieldCount { get; set; }
            public int IntegerFieldCount { get; set; }
            public int FloatFieldCount { get; set; }
            public int StructFieldCount { get; set; }
            public int ListFieldCount { get; set; }
            public int VectorFieldCount { get; set; }

            public override void PrintToLogger(ILogger logger)
            {
                base.PrintToLogger(logger);

                if (!IsValid) return;

                logger.Info("");
                logger.Info("GFF Structure:");
                logger.Info($"  File Type: {FileType}");
                logger.Info($"  Root Struct Type: {RootStructType}");
                logger.Info($"  Total Structs: {StructCount:N0}");
                logger.Info($"  Total Fields: {FieldCount:N0}");
                logger.Info($"  Total Labels: {LabelCount:N0}");
                logger.Info($"  Max Structure Depth: {MaxDepth}");
                logger.Info("");
                logger.Info("Field Types:");
                if (FieldTypeCounts != null)
                {
                    foreach (var kvp in FieldTypeCounts.OrderByDescending(x => x.Value))
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value:N0}");
                    }
                }
                logger.Info("");
                logger.Info("Data Breakdown:");
                logger.Info($"  Strings: {StringFieldCount:N0}");
                logger.Info($"  Integers: {IntegerFieldCount:N0}");
                logger.Info($"  Floats: {FloatFieldCount:N0}");
                logger.Info($"  Structs: {StructFieldCount:N0}");
                logger.Info($"  Lists: {ListFieldCount:N0}");
                logger.Info($"  Vectors: {VectorFieldCount:N0}");
                logger.Info("");
                logger.Info("Memory Usage:");
                logger.Info($"  Field Data: {FieldDataSize:N0} bytes ({GetReadableFileSize(FieldDataSize)})");
                logger.Info($"  Field Indices: {FieldIndicesSize:N0} bytes ({GetReadableFileSize(FieldIndicesSize)})");
                logger.Info($"  List Indices: {ListIndicesSize:N0} bytes ({GetReadableFileSize(ListIndicesSize)})");
                logger.Info($"  Compression Ratio: {CompressionRatio:P2}");
            }
        }

        /// <summary>
        /// ERF archive statistics analyzer
        /// </summary>
        public class ERFStatsAnalyzer : IFileStatsAnalyzer
        {
            public bool CanAnalyze(string filePath)
            {
                if (!File.Exists(filePath)) return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var supportedExtensions = new[] { ".erf", ".mod", ".sav", ".hak" };
                if (!supportedExtensions.Contains(extension)) return false;

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    using (var reader = new BinaryReader(fs))
                    {
                        if (fs.Length < 8) return false;
                        var magic = reader.ReadBytes(8);
                        return magic.SequenceEqual("ERF V1.0"u8.ToArray()) ||
                               magic.SequenceEqual("MOD V1.0"u8.ToArray()) ||
                               magic.SequenceEqual("SAV V1.0"u8.ToArray()) ||
                               magic.SequenceEqual("HAK V1.0"u8.ToArray());
                    }
                }
                catch
                {
                    return false;
                }
            }

            public FileStats Analyze(string filePath)
            {
                var stats = new ERFFileStats
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Format = "ERF (Encapsulated Resource File)"
                };

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        var erf = ERFBinaryReader.Load(fs);

                        stats.ResourceCount = erf.Count;
                        stats.ErfType = erf.ErfType;
                        stats.IsSaveErf = erf.IsSaveErf;

                        // Analyze resource types
                        var resourceTypeCounts = new Dictionary<ResourceType, int>();
                        var resourceSizes = new List<long>();
                        long totalUncompressedSize = 0;

                        foreach (var resource in erf)
                        {
                            if (!resourceTypeCounts.ContainsKey(resource.ResType))
                                resourceTypeCounts[resource.ResType] = 0;
                            resourceTypeCounts[resource.ResType]++;

                            resourceSizes.Add(resource.Data.Length);
                            totalUncompressedSize += resource.Data.Length;
                        }

                        stats.ResourceTypeCounts = resourceTypeCounts;
                        stats.ResourceSizes = resourceSizes;
                        stats.TotalUncompressedSize = totalUncompressedSize;

                        if (resourceSizes.Any())
                        {
                            stats.MinResourceSize = resourceSizes.Min();
                            stats.MaxResourceSize = resourceSizes.Max();
                            stats.AverageResourceSize = (long)resourceSizes.Average();
                            stats.MedianResourceSize = GetMedian(resourceSizes);
                        }

                        // Calculate most common resource types
                        stats.MostCommonResourceTypes = resourceTypeCounts
                            .OrderByDescending(x => x.Value)
                            .Take(10)
                            .ToDictionary(x => x.Key, x => x.Value);
                    }
                }
                catch (Exception ex)
                {
                    stats.IsValid = false;
                    stats.Errors.Add($"Failed to parse ERF file: {ex.Message}");
                }

                return stats;
            }

            private long GetMedian(List<long> values)
            {
                var sorted = values.OrderBy(x => x).ToList();
                int count = sorted.Count;
                if (count % 2 == 0)
                    return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
                else
                    return sorted[count / 2];
            }
        }

        /// <summary>
        /// ERF-specific file statistics
        /// </summary>
        public class ERFFileStats : FileStats
        {
            public int ResourceCount { get; set; }
            public ERFType ErfType { get; set; }
            public bool IsSaveErf { get; set; }
            public Dictionary<ResourceType, int> ResourceTypeCounts { get; set; }
            public List<long> ResourceSizes { get; set; }
            public long TotalUncompressedSize { get; set; }
            public long MinResourceSize { get; set; }
            public long MaxResourceSize { get; set; }
            public long AverageResourceSize { get; set; }
            public long MedianResourceSize { get; set; }
            public Dictionary<ResourceType, int> MostCommonResourceTypes { get; set; }

            public override void PrintToLogger(ILogger logger)
            {
                base.PrintToLogger(logger);

                if (!IsValid) return;

                logger.Info("");
                logger.Info("ERF Archive:");
                logger.Info($"  ERF Type: {ErfType}");
                logger.Info($"  Is Save File: {IsSaveErf}");
                logger.Info($"  Total Resources: {ResourceCount:N0}");
                logger.Info($"  Total Uncompressed Size: {TotalUncompressedSize:N0} bytes ({GetReadableFileSize(TotalUncompressedSize)})");
                logger.Info("");
                logger.Info("Resource Statistics:");
                logger.Info($"  Min Resource Size: {MinResourceSize:N0} bytes ({GetReadableFileSize(MinResourceSize)})");
                logger.Info($"  Max Resource Size: {MaxResourceSize:N0} bytes ({GetReadableFileSize(MaxResourceSize)})");
                logger.Info($"  Average Resource Size: {AverageResourceSize:N0} bytes ({GetReadableFileSize(AverageResourceSize)})");
                logger.Info($"  Median Resource Size: {MedianResourceSize:N0} bytes ({GetReadableFileSize(MedianResourceSize)})");
                logger.Info("");
                logger.Info("Resource Types (Top 10):");
                if (MostCommonResourceTypes != null)
                {
                    foreach (var kvp in MostCommonResourceTypes)
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value:N0}");
                    }
                }
                logger.Info("");
                logger.Info("All Resource Types:");
                if (ResourceTypeCounts != null)
                {
                    foreach (var kvp in ResourceTypeCounts.OrderByDescending(x => x.Value))
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value:N0}");
                    }
                }
            }
        }

        /// <summary>
        /// TLK talk table statistics analyzer
        /// </summary>
        public class TLKStatsAnalyzer : IFileStatsAnalyzer
        {
            public bool CanAnalyze(string filePath)
            {
                if (!File.Exists(filePath)) return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension != ".tlk") return false;

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    using (var reader = new BinaryReader(fs))
                    {
                        if (fs.Length < 8) return false;
                        var magic = reader.ReadBytes(8);
                        return magic.SequenceEqual("TLK V3.0"u8.ToArray());
                    }
                }
                catch
                {
                    return false;
                }
            }

            public FileStats Analyze(string filePath)
            {
                var stats = new TLKFileStats
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Format = "TLK (Talk Table)"
                };

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        var tlk = TLKBinaryReader.Load(fs);

                        stats.Language = tlk.Language;
                        stats.EntryCount = tlk.Count;

                        // Analyze entries
                        int emptyEntries = 0;
                        int entriesWithSound = 0;
                        var textLengths = new List<int>();
                        var soundRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var entry in tlk.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Text))
                                emptyEntries++;
                            else
                                textLengths.Add(entry.Text.Length);

                            if (!string.IsNullOrEmpty(entry.Voiceover.ResRef))
                            {
                                entriesWithSound++;
                                soundRefs.Add(entry.Voiceover.ResRef);
                            }
                        }

                        stats.EmptyEntryCount = emptyEntries;
                        stats.EntriesWithSoundCount = entriesWithSound;
                        stats.UniqueSoundRefsCount = soundRefs.Count;

                        if (textLengths.Any())
                        {
                            stats.MinTextLength = textLengths.Min();
                            stats.MaxTextLength = textLengths.Max();
                            stats.AverageTextLength = (int)textLengths.Average();
                            stats.TotalTextLength = textLengths.Sum();
                        }

                        // Calculate used entries
                        stats.UsedEntryCount = stats.EntryCount - emptyEntries;
                        stats.UsagePercentage = stats.EntryCount > 0 ? (double)stats.UsedEntryCount / stats.EntryCount : 0;
                    }
                }
                catch (Exception ex)
                {
                    stats.IsValid = false;
                    stats.Errors.Add($"Failed to parse TLK file: {ex.Message}");
                }

                return stats;
            }
        }

        /// <summary>
        /// TLK-specific file statistics
        /// </summary>
        public class TLKFileStats : FileStats
        {
            public Language Language { get; set; }
            public int EntryCount { get; set; }
            public int UsedEntryCount { get; set; }
            public int EmptyEntryCount { get; set; }
            public int EntriesWithSoundCount { get; set; }
            public int UniqueSoundRefsCount { get; set; }
            public double UsagePercentage { get; set; }
            public int MinTextLength { get; set; }
            public int MaxTextLength { get; set; }
            public int AverageTextLength { get; set; }
            public int TotalTextLength { get; set; }

            public override void PrintToLogger(ILogger logger)
            {
                base.PrintToLogger(logger);

                if (!IsValid) return;

                logger.Info("");
                logger.Info("TLK Talk Table:");
                logger.Info($"  Language: {Language}");
                logger.Info($"  Total Entries: {EntryCount:N0}");
                logger.Info($"  Used Entries: {UsedEntryCount:N0} ({UsagePercentage:P1})");
                logger.Info($"  Empty Entries: {EmptyEntryCount:N0}");
                logger.Info($"  Entries with Sound: {EntriesWithSoundCount:N0}");
                logger.Info($"  Unique Sound References: {UniqueSoundRefsCount:N0}");
                logger.Info("");
                logger.Info("Text Statistics:");
                if (UsedEntryCount > 0)
                {
                    logger.Info($"  Min Text Length: {MinTextLength:N0} characters");
                    logger.Info($"  Max Text Length: {MaxTextLength:N0} characters");
                    logger.Info($"  Average Text Length: {AverageTextLength:N0} characters");
                    logger.Info($"  Total Text Length: {TotalTextLength:N0} characters");
                }
                else
                {
                    logger.Info("  No text entries found");
                }
            }
        }

        /// <summary>
        /// NCS compiled script statistics analyzer
        /// </summary>
        public class NCSStatsAnalyzer : IFileStatsAnalyzer
        {
            public bool CanAnalyze(string filePath)
            {
                if (!File.Exists(filePath)) return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension != ".ncs") return false;

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    using (var reader = new BinaryReader(fs))
                    {
                        if (fs.Length < 8) return false;
                        var magic = reader.ReadBytes(8);
                        return magic.SequenceEqual("NCS V1.0"u8.ToArray());
                    }
                }
                catch
                {
                    return false;
                }
            }

            public FileStats Analyze(string filePath)
            {
                var stats = new NCSFileStats
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Format = "NCS (Compiled NWScript)"
                };

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        var ncs = NCSBinaryReader.Load(fs);

                        stats.InstructionCount = ncs.Instructions.Count;

                        // Analyze instruction types
                        var instructionTypeCounts = new Dictionary<NCSInstructionType, int>();
                        int jumpInstructions = 0;
                        int arithmeticInstructions = 0;
                        int logicalInstructions = 0;
                        int stackInstructions = 0;
                        int variableInstructions = 0;
                        int functionInstructions = 0;

                        foreach (var instruction in ncs.Instructions)
                        {
                            if (!instructionTypeCounts.ContainsKey(instruction.InsType))
                                instructionTypeCounts[instruction.InsType] = 0;
                            instructionTypeCounts[instruction.InsType]++;

                            // Categorize instructions
                            switch (instruction.InsType)
                            {
                                case NCSInstructionType.JMP:
                                case NCSInstructionType.JSR:
                                case NCSInstructionType.JZ:
                                case NCSInstructionType.JNZ:
                                case NCSInstructionType.RETN:
                                    jumpInstructions++;
                                    break;
                                case NCSInstructionType.ADD:
                                case NCSInstructionType.SUB:
                                case NCSInstructionType.MUL:
                                case NCSInstructionType.DIV:
                                case NCSInstructionType.MOD:
                                case NCSInstructionType.NEG:
                                    arithmeticInstructions++;
                                    break;
                                case NCSInstructionType.EQ:
                                case NCSInstructionType.NEQ:
                                case NCSInstructionType.GT:
                                case NCSInstructionType.GE:
                                case NCSInstructionType.LT:
                                case NCSInstructionType.LE:
                                case NCSInstructionType.LOGAND:
                                case NCSInstructionType.LOGOR:
                                case NCSInstructionType.INCOR:
                                case NCSInstructionType.EXCOR:
                                case NCSInstructionType.BOOLAND:
                                case NCSInstructionType.BOOLOR:
                                case NCSInstructionType.SHLEFT:
                                case NCSInstructionType.SHRIGHT:
                                case NCSInstructionType.USHRIGHT:
                                    logicalInstructions++;
                                    break;
                                case NCSInstructionType.CPDOWNSP:
                                case NCSInstructionType.CPTOPSP:
                                case NCSInstructionType.CPDOWNBP:
                                case NCSInstructionType.CPTOPBP:
                                case NCSInstructionType.RSADDI:
                                case NCSInstructionType.RSADDF:
                                case NCSInstructionType.RSADDS:
                                case NCSInstructionType.RSADDO:
                                    stackInstructions++;
                                    break;
                                case NCSInstructionType.CONSTI:
                                case NCSInstructionType.CONSTF:
                                case NCSInstructionType.CONSTS:
                                case NCSInstructionType.CONSTO:
                                case NCSInstructionType.ACTION:
                                case NCSInstructionType.MOVSP:
                                    variableInstructions++;
                                    break;
                                case NCSInstructionType.SAVEPC:
                                case NCSInstructionType.RESTOREPC:
                                    functionInstructions++;
                                    break;
                            }
                        }

                        stats.InstructionTypeCounts = instructionTypeCounts;
                        stats.JumpInstructions = jumpInstructions;
                        stats.ArithmeticInstructions = arithmeticInstructions;
                        stats.LogicalInstructions = logicalInstructions;
                        stats.StackInstructions = stackInstructions;
                        stats.VariableInstructions = variableInstructions;
                        stats.FunctionInstructions = functionInstructions;

                        // Find most common instruction types
                        stats.MostCommonInstructions = instructionTypeCounts
                            .OrderByDescending(x => x.Value)
                            .Take(10)
                            .ToDictionary(x => x.Key, x => x.Value);

                        // Calculate script complexity metrics
                        stats.UniqueInstructionTypes = instructionTypeCounts.Count;
                        stats.InstructionDiversityRatio = stats.InstructionCount > 0 ?
                            (double)stats.UniqueInstructionTypes / stats.InstructionCount : 0;
                    }
                }
                catch (Exception ex)
                {
                    stats.IsValid = false;
                    stats.Errors.Add($"Failed to parse NCS file: {ex.Message}");
                }

                return stats;
            }
        }

        /// <summary>
        /// NCS-specific file statistics
        /// </summary>
        public class NCSFileStats : FileStats
        {
            public int InstructionCount { get; set; }
            public int UniqueInstructionTypes { get; set; }
            public double InstructionDiversityRatio { get; set; }
            public Dictionary<NCSInstructionType, int> InstructionTypeCounts { get; set; }
            public Dictionary<NCSInstructionType, int> MostCommonInstructions { get; set; }
            public int JumpInstructions { get; set; }
            public int ArithmeticInstructions { get; set; }
            public int LogicalInstructions { get; set; }
            public int StackInstructions { get; set; }
            public int VariableInstructions { get; set; }
            public int FunctionInstructions { get; set; }

            public override void PrintToLogger(ILogger logger)
            {
                base.PrintToLogger(logger);

                if (!IsValid) return;

                logger.Info("");
                logger.Info("NCS Compiled Script:");
                logger.Info($"  Total Instructions: {InstructionCount:N0}");
                logger.Info($"  Unique Instruction Types: {UniqueInstructionTypes:N0}");
                logger.Info($"  Instruction Diversity Ratio: {InstructionDiversityRatio:P3}");
                logger.Info("");
                logger.Info("Instruction Categories:");
                logger.Info($"  Jump/Control Flow: {JumpInstructions:N0}");
                logger.Info($"  Arithmetic: {ArithmeticInstructions:N0}");
                logger.Info($"  Logical/Bitwise: {LogicalInstructions:N0}");
                logger.Info($"  Stack Operations: {StackInstructions:N0}");
                logger.Info($"  Variables/Constants: {VariableInstructions:N0}");
                logger.Info($"  Functions: {FunctionInstructions:N0}");
                logger.Info("");
                logger.Info("Most Common Instructions (Top 10):");
                if (MostCommonInstructions != null)
                {
                    foreach (var kvp in MostCommonInstructions)
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value:N0}");
                    }
                }
                logger.Info("");
                logger.Info("All Instruction Types:");
                if (InstructionTypeCounts != null)
                {
                    foreach (var kvp in InstructionTypeCounts.OrderByDescending(x => x.Value))
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value:N0}");
                    }
                }
            }
        }

        /// <summary>
        /// TwoDA table statistics analyzer
        /// </summary>
        public class TwoDAStatsAnalyzer : IFileStatsAnalyzer
        {
            public bool CanAnalyze(string filePath)
            {
                if (!File.Exists(filePath)) return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension != ".2da") return false;

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    using (var reader = new BinaryReader(fs))
                    {
                        if (fs.Length < 12) return false;
                        var magic = reader.ReadBytes(8);
                        return magic.SequenceEqual("2DA V2.b"u8.ToArray());
                    }
                }
                catch
                {
                    return false;
                }
            }

            public FileStats Analyze(string filePath)
            {
                var stats = new TwoDAFileStats
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Format = "2DA (Two-Dimensional Array)"
                };

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        var twoda = TwoDABinaryReader.Load(fs);

                        stats.RowCount = twoda.Rows.Count;
                        stats.ColumnCount = twoda.Columns.Count;

                        // Analyze data types in cells
                        int totalCells = 0;
                        int emptyCells = 0;
                        int numericCells = 0;
                        int stringCells = 0;
                        var columnTypes = new Dictionary<string, HashSet<string>>();
                        var cellLengths = new List<int>();

                        foreach (var row in twoda.Rows)
                        {
                            foreach (var column in twoda.Columns)
                            {
                                totalCells++;
                                var cellValue = row.GetCell(column);

                                if (string.IsNullOrEmpty(cellValue))
                                {
                                    emptyCells++;
                                }
                                else
                                {
                                    cellLengths.Add(cellValue.Length);

                                    // Try to parse as number
                                    if (int.TryParse(cellValue, out _) || float.TryParse(cellValue, out _))
                                    {
                                        numericCells++;
                                    }
                                    else
                                    {
                                        stringCells++;
                                    }

                                    // Track column data types
                                    if (!columnTypes.ContainsKey(column))
                                        columnTypes[column] = new HashSet<string>();

                                    if (int.TryParse(cellValue, out _))
                                        columnTypes[column].Add("Integer");
                                    else if (float.TryParse(cellValue, out _))
                                        columnTypes[column].Add("Float");
                                    else
                                        columnTypes[column].Add("String");
                                }
                            }
                        }

                        stats.TotalCells = totalCells;
                        stats.EmptyCells = emptyCells;
                        stats.NumericCells = numericCells;
                        stats.StringCells = stringCells;
                        stats.FilledCells = totalCells - emptyCells;

                        if (cellLengths.Any())
                        {
                            stats.MinCellLength = cellLengths.Min();
                            stats.MaxCellLength = cellLengths.Max();
                            stats.AverageCellLength = (double)cellLengths.Sum() / cellLengths.Count;
                        }

                        // Calculate fill percentage
                        stats.FillPercentage = totalCells > 0 ? (double)stats.FilledCells / totalCells : 0;

                        // Column type analysis
                        stats.ColumnTypeAnalysis = columnTypes.ToDictionary(
                            x => x.Key,
                            x => string.Join("/", x.Value.OrderBy(t => t))
                        );
                    }
                }
                catch (Exception ex)
                {
                    stats.IsValid = false;
                    stats.Errors.Add($"Failed to parse 2DA file: {ex.Message}");
                }

                return stats;
            }
        }

        /// <summary>
        /// TwoDA-specific file statistics
        /// </summary>
        public class TwoDAFileStats : FileStats
        {
            public int RowCount { get; set; }
            public int ColumnCount { get; set; }
            public int TotalCells { get; set; }
            public int FilledCells { get; set; }
            public int EmptyCells { get; set; }
            public int NumericCells { get; set; }
            public int StringCells { get; set; }
            public double FillPercentage { get; set; }
            public int MinCellLength { get; set; }
            public int MaxCellLength { get; set; }
            public double AverageCellLength { get; set; }
            public Dictionary<string, string> ColumnTypeAnalysis { get; set; }

            public override void PrintToLogger(ILogger logger)
            {
                base.PrintToLogger(logger);

                if (!IsValid) return;

                logger.Info("");
                logger.Info("2DA Table:");
                logger.Info($"  Rows: {RowCount:N0}");
                logger.Info($"  Columns: {ColumnCount:N0}");
                logger.Info($"  Total Cells: {TotalCells:N0}");
                logger.Info($"  Filled Cells: {FilledCells:N0} ({FillPercentage:P1})");
                logger.Info($"  Empty Cells: {EmptyCells:N0}");
                logger.Info("");
                logger.Info("Cell Data Types:");
                logger.Info($"  Numeric: {NumericCells:N0}");
                logger.Info($"  String: {StringCells:N0}");
                logger.Info("");
                logger.Info("Cell Content Length:");
                if (FilledCells > 0)
                {
                    logger.Info($"  Min: {MinCellLength:N0} characters");
                    logger.Info($"  Max: {MaxCellLength:N0} characters");
                    logger.Info($"  Average: {AverageCellLength:F1} characters");
                }
                logger.Info("");
                logger.Info("Column Types:");
                if (ColumnTypeAnalysis != null)
                {
                    foreach (var kvp in ColumnTypeAnalysis.OrderBy(x => x.Key))
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value}");
                    }
                }
            }
        }

        /// <summary>
        /// BIF archive statistics analyzer
        /// </summary>
        public class BIFStatsAnalyzer : IFileStatsAnalyzer
        {
            public bool CanAnalyze(string filePath)
            {
                if (!File.Exists(filePath)) return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension != ".bif") return false;

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    using (var reader = new BinaryReader(fs))
                    {
                        if (fs.Length < 20) return false;
                        var magic = reader.ReadBytes(8);
                        return magic.SequenceEqual("BIFFV1.0"u8.ToArray());
                    }
                }
                catch
                {
                    return false;
                }
            }

            public FileStats Analyze(string filePath)
            {
                var stats = new BIFFileStats
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Format = "BIF (BioWare Resource Archive)"
                };

                try
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        var bif = BIFBinaryReader.Load(fs);

                        stats.VariableResourceCount = bif.VariableResources.Count;
                        stats.FixedResourceCount = bif.FixedResources.Count;
                        stats.TotalResourceCount = stats.VariableResourceCount + stats.FixedResourceCount;

                        // Analyze resource types
                        var resourceTypeCounts = new Dictionary<ResourceType, int>();
                        var variableSizes = new List<long>();
                        var fixedSizes = new List<long>();
                        long totalVariableSize = 0;
                        long totalFixedSize = 0;

                        foreach (var resource in bif.VariableResources)
                        {
                            if (!resourceTypeCounts.ContainsKey(resource.ResType))
                                resourceTypeCounts[resource.ResType] = 0;
                            resourceTypeCounts[resource.ResType]++;

                            variableSizes.Add(resource.UncompressedSize);
                            totalVariableSize += resource.UncompressedSize;
                        }

                        foreach (var resource in bif.FixedResources)
                        {
                            if (!resourceTypeCounts.ContainsKey(resource.ResType))
                                resourceTypeCounts[resource.ResType] = 0;
                            resourceTypeCounts[resource.ResType]++;

                            fixedSizes.Add(resource.UncompressedSize);
                            totalFixedSize += resource.UncompressedSize;
                        }

                        stats.ResourceTypeCounts = resourceTypeCounts;
                        stats.VariableResourceSizes = variableSizes;
                        stats.FixedResourceSizes = fixedSizes;
                        stats.TotalVariableSize = totalVariableSize;
                        stats.TotalFixedSize = totalFixedSize;
                        stats.TotalUncompressedSize = totalVariableSize + totalFixedSize;

                        // Calculate size statistics
                        if (variableSizes.Any())
                        {
                            stats.MinVariableResourceSize = variableSizes.Min();
                            stats.MaxVariableResourceSize = variableSizes.Max();
                            stats.AverageVariableResourceSize = (long)variableSizes.Average();
                        }

                        if (fixedSizes.Any())
                        {
                            stats.MinFixedResourceSize = fixedSizes.Min();
                            stats.MaxFixedResourceSize = fixedSizes.Max();
                            stats.AverageFixedResourceSize = (long)fixedSizes.Average();
                        }

                        // Calculate most common resource types
                        stats.MostCommonResourceTypes = resourceTypeCounts
                            .OrderByDescending(x => x.Value)
                            .Take(10)
                            .ToDictionary(x => x.Key, x => x.Value);
                    }
                }
                catch (Exception ex)
                {
                    stats.IsValid = false;
                    stats.Errors.Add($"Failed to parse BIF file: {ex.Message}");
                }

                return stats;
            }
        }

        /// <summary>
        /// BIF-specific file statistics
        /// </summary>
        public class BIFFileStats : FileStats
        {
            public int VariableResourceCount { get; set; }
            public int FixedResourceCount { get; set; }
            public int TotalResourceCount { get; set; }
            public Dictionary<ResourceType, int> ResourceTypeCounts { get; set; }
            public List<long> VariableResourceSizes { get; set; }
            public List<long> FixedResourceSizes { get; set; }
            public long TotalVariableSize { get; set; }
            public long TotalFixedSize { get; set; }
            public long TotalUncompressedSize { get; set; }
            public long MinVariableResourceSize { get; set; }
            public long MaxVariableResourceSize { get; set; }
            public long AverageVariableResourceSize { get; set; }
            public long MinFixedResourceSize { get; set; }
            public long MaxFixedResourceSize { get; set; }
            public long AverageFixedResourceSize { get; set; }
            public Dictionary<ResourceType, int> MostCommonResourceTypes { get; set; }

            public override void PrintToLogger(ILogger logger)
            {
                base.PrintToLogger(logger);

                if (!IsValid) return;

                logger.Info("");
                logger.Info("BIF Archive:");
                logger.Info($"  Variable Resources: {VariableResourceCount:N0}");
                logger.Info($"  Fixed Resources: {FixedResourceCount:N0}");
                logger.Info($"  Total Resources: {TotalResourceCount:N0}");
                logger.Info("");
                logger.Info("Size Information:");
                logger.Info($"  Variable Resources Size: {TotalVariableSize:N0} bytes ({GetReadableFileSize(TotalVariableSize)})");
                logger.Info($"  Fixed Resources Size: {TotalFixedSize:N0} bytes ({GetReadableFileSize(TotalFixedSize)})");
                logger.Info($"  Total Uncompressed Size: {TotalUncompressedSize:N0} bytes ({GetReadableFileSize(TotalUncompressedSize)})");
                logger.Info("");
                if (VariableResourceCount > 0)
                {
                    logger.Info("Variable Resource Sizes:");
                    logger.Info($"  Min: {MinVariableResourceSize:N0} bytes ({GetReadableFileSize(MinVariableResourceSize)})");
                    logger.Info($"  Max: {MaxVariableResourceSize:N0} bytes ({GetReadableFileSize(MaxVariableResourceSize)})");
                    logger.Info($"  Average: {AverageVariableResourceSize:N0} bytes ({GetReadableFileSize(AverageVariableResourceSize)})");
                }
                if (FixedResourceCount > 0)
                {
                    logger.Info("Fixed Resource Sizes:");
                    logger.Info($"  Min: {MinFixedResourceSize:N0} bytes ({GetReadableFileSize(MinFixedResourceSize)})");
                    logger.Info($"  Max: {MaxFixedResourceSize:N0} bytes ({GetReadableFileSize(MaxFixedResourceSize)})");
                    logger.Info($"  Average: {AverageFixedResourceSize:N0} bytes ({GetReadableFileSize(AverageFixedResourceSize)})");
                }
                logger.Info("");
                logger.Info("Resource Types (Top 10):");
                if (MostCommonResourceTypes != null)
                {
                    foreach (var kvp in MostCommonResourceTypes)
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value:N0}");
                    }
                }
                logger.Info("");
                logger.Info("All Resource Types:");
                if (ResourceTypeCounts != null)
                {
                    foreach (var kvp in ResourceTypeCounts.OrderByDescending(x => x.Value))
                    {
                        logger.Info($"  {kvp.Key}: {kvp.Value:N0}");
                    }
                }
            }
        }

        /// <summary>
        /// File format detector and stats analyzer
        /// </summary>
        public static class FileStatsAnalyzer
        {
            private static readonly List<IFileStatsAnalyzer> _analyzers = new List<IFileStatsAnalyzer>
            {
                new GFFStatsAnalyzer(),
                new ERFStatsAnalyzer(),
                new TLKStatsAnalyzer(),
                new NCSStatsAnalyzer(),
                new TwoDAStatsAnalyzer(),
                new BIFStatsAnalyzer()
            };

            public static FileStats AnalyzeFile(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    return new FileStats
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        IsValid = false,
                        Errors = { "File does not exist" }
                    };
                }

                foreach (var analyzer in _analyzers)
                {
                    if (analyzer.CanAnalyze(filePath))
                    {
                        return analyzer.Analyze(filePath);
                    }
                }

                // Generic file stats for unrecognized formats
                var fileInfo = new FileInfo(filePath);
                return new FileStats
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileSize = fileInfo.Length,
                    Format = "Unknown/Unsupported",
                    IsValid = false,
                    Errors = { "File format not recognized or not supported for detailed analysis" }
                };
            }
        }

        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var diffCmd = new Command("diff", "Compare two files and show differences");
            var file1Arg = new Argument<string>("file1", "First file");
            diffCmd.Add(file1Arg);
            var file2Arg = new Argument<string>("file2", "Second file");
            diffCmd.Add(file2Arg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output diff file");
            diffCmd.Options.Add(outputOpt);
            diffCmd.SetAction(parseResult =>
            {
                var file1 = parseResult.GetValue(file1Arg);
                var file2 = parseResult.GetValue(file2Arg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - diff not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(diffCmd);

            var grepCmd = new Command("grep", "Search for patterns in files");
            var grepFileArg = new Argument<string>("file", "File to search");
            grepCmd.Add(grepFileArg);
            var patternArg = new Argument<string>("pattern", "Search pattern");
            grepCmd.Add(patternArg);
            var caseSensitiveOption = new Option<bool>("--case-sensitive", "Case-sensitive search");
            grepCmd.Options.Add(caseSensitiveOption);
            var lineNumbersOption = new Option<bool>(new[] { "-n", "--line-numbers" }, "Show line numbers");
            grepCmd.Options.Add(lineNumbersOption);
            grepCmd.SetAction(parseResult =>
            {
                var file = parseResult.GetValue(grepFileArg);
                var pattern = parseResult.GetValue(patternArg);
                var caseSensitive = parseResult.GetValue(caseSensitiveOption);
                var lineNumbers = parseResult.GetValue(lineNumbersOption);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - grep not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(grepCmd);

            var statsCmd = new Command("stats", "Show statistics about a file");
            var statsFileArg = new Argument<string>("file", "File to analyze");
            statsCmd.Add(statsFileArg);
            statsCmd.SetAction(parseResult =>
            {
                var file = parseResult.GetValue(statsFileArg);
                var logger = new StandardLogger();

                if (string.IsNullOrEmpty(file))
                {
                    logger.Error("Error: No file specified");
                    Environment.Exit(1);
                }

                try
                {
                    var stats = FileStatsAnalyzer.AnalyzeFile(file);
                    stats.PrintToLogger(logger);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error analyzing file '{file}': {ex.Message}");
                    Environment.Exit(1);
                }
            });
            rootCommand.Add(statsCmd);

            var validateCmd = new Command("validate", "Validate file format and structure");
            var validateFileArg = new Argument<string>("file", "File to validate");
            validateCmd.Add(validateFileArg);
            validateCmd.SetAction(parseResult =>
            {
                var file = parseResult.GetValue(validateFileArg);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - validate not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(validateCmd);

            var mergeCmd = new Command("merge", "Merge two GFF files");
            var targetArg = new Argument<string>("target", "Target GFF file (will be modified)");
            mergeCmd.Add(targetArg);
            var sourceArg = new Argument<string>("source", "Source GFF file (fields to merge)");
            mergeCmd.Add(sourceArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output GFF file (default: overwrite target)");
            mergeCmd.Options.Add(outputOpt);
            mergeCmd.SetAction(parseResult =>
            {
                var target = parseResult.GetValue(targetArg);
                var source = parseResult.GetValue(sourceArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - merge not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(mergeCmd);
        }
    }
}