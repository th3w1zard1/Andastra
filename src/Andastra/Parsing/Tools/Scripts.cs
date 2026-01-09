using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Common.Script;
using Andastra.Parsing.Formats.NCS;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/scripts.py
    // Original: Script utility functions for NCS bytecode manipulation
    public static class Scripts
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/scripts.py:26-58
        // Original: def decompile_ncs_to_nss(ncs_path: Path, output_path: Path | None = None, *, game: Game, functions: list[ScriptFunction] | None = None, constants: list[ScriptConstant] | None = None) -> str:
        public static string DecompileNcsToNss(string ncsPath, string outputPath = null, BioWareGame game = BioWareGame.K1, List<ScriptFunction> functions = null, List<ScriptConstant> constants = null)
        {
            Andastra.Parsing.Formats.NCS.NCS ncs = Andastra.Parsing.Formats.NCS.NCSAuto.ReadNcs(ncsPath);
            string source = Andastra.Parsing.Formats.NCS.NCSAuto.DecompileNcs(ncs, game, functions, constants);

            if (!string.IsNullOrEmpty(outputPath))
            {
                File.WriteAllText(outputPath, source, System.Text.Encoding.UTF8);
            }

            return source;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/scripts.py:61-111
        // Original: def disassemble_ncs(ncs_path: Path, output_path: Path | None = None, *, game: Game | None = None, pretty: bool = True) -> str:
        public static string DisassembleNcs(string ncsPath, string outputPath = null, BioWareGame? game = null, bool pretty = true)
        {
            var ncs = Andastra.Parsing.Formats.NCS.NCSAuto.ReadNcs(ncsPath);

            var lines = new System.Collections.Generic.List<string>();
            lines.Add("; NCS Disassembly");
            lines.Add($"; Instructions: {ncs.Instructions.Count}");
            lines.Add("");

            for (int i = 0; i < ncs.Instructions.Count; i++)
            {
                var instruction = ncs.Instructions[i];
                string instructionStr = instruction.ToString();

                if (pretty)
                {
                    // Use instruction offset if available, otherwise use index
                    int byteOffset;
                    if (instruction.Offset >= 0)
                    {
                        byteOffset = instruction.Offset;
                    }
                    else
                    {
                        // Estimate offset (rough approximation)
                        byteOffset = i * 4; // Average ~4 bytes per instruction
                    }
                    lines.Add($"{byteOffset:X8}: {instructionStr}");
                }
                else
                {
                    lines.Add(instructionStr);
                }
            }

            string result = string.Join("\n", lines);

            if (!string.IsNullOrEmpty(outputPath))
            {
                File.WriteAllText(outputPath, result, System.Text.Encoding.UTF8);
            }

            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/scripts.py:114-147
        // Original: def ncs_to_text(ncs_path: Path, output_path: Path | None = None, *, mode: str = "decompile", game: Game | None = None) -> str:
        public static string NcsToText(string ncsPath, string outputPath = null, string mode = "decompile", BioWareGame? game = null)
        {
            if (mode == "decompile")
            {
                if (!game.HasValue)
                {
                    throw new ArgumentException("Game version is required for decompilation mode");
                }
                return DecompileNcsToNss(ncsPath, outputPath, game.Value);
            }
            if (mode == "disassemble")
            {
                return DisassembleNcs(ncsPath, outputPath, game, pretty: true);
            }

            throw new ArgumentException($"Invalid mode: '{mode}'. Must be 'decompile' or 'disassemble'");
        }
    }
}
