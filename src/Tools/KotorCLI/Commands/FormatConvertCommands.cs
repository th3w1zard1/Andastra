using System;
using System.CommandLine;
using System.IO;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using BioWare.NET.Tools;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Format conversion commands (gff2json, json2gff, gff2xml, xml2gff, tlk2xml, xml2tlk, ssf2xml, xml2ssf, 2da2csv, csv22da).
    /// </summary>
    public static class FormatConvertCommands
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            // GFF conversions
            AddGff2Json(rootCommand);
            AddJson2Gff(rootCommand);
            AddGff2Xml(rootCommand);
            AddXml2Gff(rootCommand);

            // TLK conversions
            AddTlk2Xml(rootCommand);
            AddXml2Tlk(rootCommand);

            // SSF conversions
            AddSsf2Xml(rootCommand);
            AddXml2Ssf(rootCommand);

            // 2DA conversions
            Add2Da2Csv(rootCommand);
            AddCsv22Da(rootCommand);
        }

        private static void AddGff2Json(RootCommand rootCommand)
        {
            var cmd = new Command("gff2json", "Convert GFF to JSON");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input GFF file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output JSON file");
            cmd.Options.Add(outputOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                var exitCode = ExecuteGff2Json(input, output, logger);
                Environment.Exit(exitCode);
            });
            rootCommand.Add(cmd);
        }

        private static int ExecuteGff2Json(string input, string output, ILogger logger)
        {
            try
            {
                if (string.IsNullOrEmpty(output))
                {
                    output = Path.ChangeExtension(input, ".json");
                }
                Conversions.ConvertGffToJson(input, output);
                logger.Info($"Converted {input} to {output}");
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to convert GFF to JSON: {ex.Message}");
                return 1;
            }
        }

        private static void AddJson2Gff(RootCommand rootCommand)
        {
            var cmd = new Command("json2gff", "Convert JSON to GFF");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input JSON file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output GFF file");
            cmd.Options.Add(outputOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                logger.Warning("TODO: STUB - JSON to GFF conversion not yet implemented in BioWare.NET");
                Environment.Exit(1);
            });
            rootCommand.Add(cmd);
        }

        private static void AddGff2Xml(RootCommand rootCommand)
        {
            var cmd = new Command("gff2xml", "Convert GFF to XML");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input GFF file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output XML file");
            cmd.Options.Add(outputOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                try
                {
                    if (string.IsNullOrEmpty(output))
                    {
                        output = Path.ChangeExtension(input, ".xml");
                    }
                    Conversions.ConvertGffToXml(input, output);
                    logger.Info($"Converted {input} to {output}");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to convert GFF to XML: {ex.Message}");
                    Environment.Exit(1);
                }
            });
            rootCommand.Add(cmd);
        }

        private static void AddXml2Gff(RootCommand rootCommand)
        {
            var cmd = new Command("xml2gff", "Convert XML to GFF");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input XML file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output GFF file");
            cmd.Options.Add(outputOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                try
                {
                    if (string.IsNullOrEmpty(output))
                    {
                        output = Path.ChangeExtension(input, ".gff");
                    }
                    Conversions.ConvertXmlToGff(input, output);
                    logger.Info($"Converted {input} to {output}");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to convert XML to GFF: {ex.Message}");
                    Environment.Exit(1);
                }
            });
            rootCommand.Add(cmd);
        }

        private static void AddTlk2Xml(RootCommand rootCommand)
        {
            var cmd = new Command("tlk2xml", "Convert TLK to XML");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input TLK file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output XML file");
            cmd.Options.Add(outputOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - tlk2xml not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(cmd);
        }

        private static void AddXml2Tlk(RootCommand rootCommand)
        {
            var cmd = new Command("xml2tlk", "Convert XML to TLK");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input XML file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output TLK file");
            cmd.Options.Add(outputOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - xml2tlk not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(cmd);
        }

        private static void AddSsf2Xml(RootCommand rootCommand)
        {
            var cmd = new Command("ssf2xml", "Convert SSF to XML");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input SSF file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output XML file");
            cmd.Options.Add(outputOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - ssf2xml not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(cmd);
        }

        private static void AddXml2Ssf(RootCommand rootCommand)
        {
            var cmd = new Command("xml2ssf", "Convert XML to SSF");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input XML file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output SSF file");
            cmd.Options.Add(outputOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var logger = new StandardLogger();
                var exitCode = ExecuteXml2Ssf(input, output, logger);
                Environment.Exit(exitCode);
            });
            rootCommand.Add(cmd);
        }

        private static int ExecuteXml2Ssf(string input, string output, ILogger logger)
        {
            try
            {
                if (string.IsNullOrEmpty(output))
                {
                    output = Path.ChangeExtension(input, ".ssf");
                }
                Conversions.ConvertXmlToSsf(input, output);
                logger.Info($"Converted {input} to {output}");
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to convert XML to SSF: {ex.Message}");
                return 1;
            }
        }

        private static void Add2Da2Csv(RootCommand rootCommand)
        {
            var cmd = new Command("2da2csv", "Convert 2DA to CSV");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input 2DA file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output CSV file");
            cmd.Options.Add(outputOpt);
            var delimiterOpt = new Option<string>("--delimiter", "CSV delimiter");
            cmd.Options.Add(delimiterOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var delimiter = parseResult.GetValue(delimiterOpt) ?? ",";
                var logger = new StandardLogger();
                try
                {
                    if (string.IsNullOrEmpty(output))
                    {
                        output = Path.ChangeExtension(input, ".csv");
                    }
                    Conversions.Convert2daToCsv(input, output, delimiter);
                    logger.Info($"Converted {input} to {output}");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to convert 2DA to CSV: {ex.Message}");
                    Environment.Exit(1);
                }
            });
            rootCommand.Add(cmd);
        }

        private static void AddCsv22Da(RootCommand rootCommand)
        {
            var cmd = new Command("csv22da", "Convert CSV to 2DA");
            var inputArg = new Argument<string>("input");
            inputArg.Description = "Input CSV file";
            cmd.Add(inputArg);
            var outputOpt = new Option<string>("--output", "Output 2DA file");
            cmd.Options.Add(outputOpt);
            var delimiterOpt = new Option<string>("--delimiter", "CSV delimiter");
            cmd.Options.Add(delimiterOpt);
            cmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(inputArg);
                var output = parseResult.GetValue(outputOpt);
                var delimiter = parseResult.GetValue(delimiterOpt) ?? ",";
                var logger = new StandardLogger();
                try
                {
                    if (string.IsNullOrEmpty(output))
                    {
                        output = Path.ChangeExtension(input, ".2da");
                    }
                    Conversions.ConvertCsvTo2da(input, output, delimiter);
                    logger.Info($"Converted {input} to {output}");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to convert CSV to 2DA: {ex.Message}");
                    Environment.Exit(1);
                }
            });
            rootCommand.Add(cmd);
        }
    }
}
