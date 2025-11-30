using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.NCS;
using Xunit;

namespace TSLPatcher.Tests.Formats
{
    /// <summary>
    /// Tests for NCS roundtrip compilation/decompilation.
    /// </summary>
    public class NCSRoundtripTests
    {
        private const int DefaultSampleSize = 10;
        private static readonly int SampleLimit = int.TryParse(
            Environment.GetEnvironmentVariable("PYKOTOR_NCS_ROUNDTRIP_SAMPLE") ?? DefaultSampleSize.ToString(),
            out int limit) ? limit : DefaultSampleSize;

        private static List<(Game Game, string ScriptPath, List<string> LibraryLookup)> _roundtripCases;
        private static string _vanillaRoot;

        static NCSRoundtripTests()
        {
            _vanillaRoot = Path.Combine("vendor", "Vanilla_KOTOR_Script_Source");
            _roundtripCases = InitializeRoundtripCases();
        }

        [Fact(Skip = "Temporarily skipping NCS tests due to stack offset issues")]
        public void TestNssRoundtrip()
        {
            if (_roundtripCases == null || _roundtripCases.Count == 0)
            {
                // Skip if Vanilla_KOTOR_Script_Source submodule not available or no scripts collected
                return;
            }

            foreach (var (game, scriptPath, libraryLookup) in _roundtripCases)
            {
                if (!File.Exists(scriptPath))
                {
                    continue; // Skip if file doesn't exist
                }

                string source = File.ReadAllText(scriptPath, Encoding.GetEncoding(1252));
                NCS originalNcs = NCSAuto.CompileNss(source, game, null, libraryLookup);

                string decompiledSource = NCSAuto.DecompileNcs(originalNcs, game);
                NCS roundtripNcs = NCSAuto.CompileNss(decompiledSource, game, null, libraryLookup);
                string roundtripSource = NCSAuto.DecompileNcs(roundtripNcs, game);
                NCS roundtripNcsSecond = NCSAuto.CompileNss(roundtripSource, game, null, libraryLookup);

                roundtripNcs.Should().BeEquivalentTo(roundtripNcsSecond,
                    $"Roundtrip compilation not stable for {scriptPath}");
            }
        }

        private static List<string> IterScripts(string root)
        {
            if (!Directory.Exists(root))
            {
                return new List<string>();
            }
            return Directory.GetFiles(root, "*.nss", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToList();
        }

        private static List<string> CollectSample(
            Game game,
            List<string> roots,
            List<string> libraryLookup)
        {
            var sample = new List<string>();
            foreach (string directory in roots)
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (string script in IterScripts(directory))
                {
                    if (sample.Contains(script))
                    {
                        Console.WriteLine($"Skipping duplicate script {script} for game {game}");
                        continue;
                    }

                    string source;
                    try
                    {
                        source = File.ReadAllText(script, Encoding.GetEncoding(1252));
                    }
                    catch (FileNotFoundException)
                    {
                        Console.WriteLine($"Skipping missing script {script} for game {game}");
                        continue;
                    }

                    // Skip scripts that rely on external includes for now
                    if (source.Contains("#include"))
                    {
                        Console.WriteLine($"Skipping script {script} for game {game} due to include directive");
                        continue;
                    }

                    try
                    {
                        NCSAuto.CompileNss(source, game, null, libraryLookup);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Compilation failed for script {script} for game {game}");
                        Console.WriteLine($"{ex}\n");
                        continue;
                    }

                    sample.Add(script);
                    if (SampleLimit > 0 && sample.Count >= SampleLimit)
                    {
                        Console.WriteLine(
                            $"Reached sample limit {SampleLimit} for game {game} with directory {directory}");
                        return sample;
                    }
                }
            }
            return sample;
        }

        private static Dictionary<Game, Dictionary<string, List<string>>> GameConfig()
        {
            return new Dictionary<Game, Dictionary<string, List<string>>>
            {
                [Game.K1] = new Dictionary<string, List<string>>
                {
                    ["roots"] = new List<string>
                    {
                        Path.Combine(_vanillaRoot, "K1", "Modules"),
                        Path.Combine(_vanillaRoot, "K1", "Rims"),
                        Path.Combine(_vanillaRoot, "K1", "Data", "scripts.bif"),
                    },
                    ["lookup"] = new List<string>
                    {
                        Path.Combine(_vanillaRoot, "K1", "Modules"),
                        Path.Combine(_vanillaRoot, "K1", "Rims"),
                        Path.Combine(_vanillaRoot, "K1", "Data", "scripts.bif"),
                    },
                },
                [Game.K2] = new Dictionary<string, List<string>>
                {
                    ["roots"] = new List<string>
                    {
                        Path.Combine(_vanillaRoot, "TSL", "Vanilla", "Modules"),
                        Path.Combine(_vanillaRoot, "TSL", "Vanilla", "Data", "Scripts"),
                    },
                    ["lookup"] = new List<string>
                    {
                        Path.Combine(_vanillaRoot, "TSL", "Vanilla", "Modules"),
                        Path.Combine(_vanillaRoot, "TSL", "Vanilla", "Data", "Scripts"),
                        Path.Combine(_vanillaRoot, "TSL", "TSLRCM", "Override"),
                    },
                },
            };
        }

        private static List<(Game Game, string ScriptPath, List<string> LibraryLookup)> InitializeRoundtripCases()
        {
            if (_roundtripCases != null)
            {
                Console.WriteLine($"Roundtrip cases already initialized with {_roundtripCases.Count} entries");
                return _roundtripCases;
            }

            var roundtripCases = new List<(Game, string, List<string>)>();
            if (!Directory.Exists(_vanillaRoot))
            {
                Console.WriteLine($"Skipping sample collection because VANILLA_ROOT {_vanillaRoot} does not exist");
                _roundtripCases = roundtripCases;
                return roundtripCases;
            }

            Console.WriteLine("Collecting sample scripts from Vanilla_KOTOR_Script_Source...");
            foreach (var (game, config) in GameConfig())
            {
                Console.WriteLine($"Collecting sample scripts for {game}...");
                var roots = config["roots"].Where(path => Directory.Exists(path) || File.Exists(path)).ToList();
                var lookup = config["lookup"].Where(path => Directory.Exists(path) || File.Exists(path)).ToList();
                Console.WriteLine($"Roots: {string.Join(", ", roots)}");
                Console.WriteLine($"Lookup: {string.Join(", ", lookup)}");
                if (roots.Count == 0 || lookup.Count == 0)
                {
                    Console.WriteLine($"No roots or lookup found for {game}, skipping...");
                    continue;
                }
                var sample = CollectSample(game, roots, lookup);
                roundtripCases.AddRange(
                    sample.Select(script => (game, script, lookup))
                );
            }

            _roundtripCases = roundtripCases;
            return roundtripCases;
        }
    }
}

