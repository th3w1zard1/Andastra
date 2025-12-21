using System.CommandLine;
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
            var inputArg = new Argument<string>("input", "Input GFF file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output JSON file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("gff2json not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void AddJson2Gff(RootCommand rootCommand)
        {
            var cmd = new Command("json2gff", "Convert JSON to GFF");
            var inputArg = new Argument<string>("input", "Input JSON file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output GFF file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("json2gff not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void AddGff2Xml(RootCommand rootCommand)
        {
            var cmd = new Command("gff2xml", "Convert GFF to XML");
            var inputArg = new Argument<string>("input", "Input GFF file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output XML file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("gff2xml not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void AddXml2Gff(RootCommand rootCommand)
        {
            var cmd = new Command("xml2gff", "Convert XML to GFF");
            var inputArg = new Argument<string>("input", "Input XML file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output GFF file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("xml2gff not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void AddTlk2Xml(RootCommand rootCommand)
        {
            var cmd = new Command("tlk2xml", "Convert TLK to XML");
            var inputArg = new Argument<string>("input", "Input TLK file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output XML file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("tlk2xml not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void AddXml2Tlk(RootCommand rootCommand)
        {
            var cmd = new Command("xml2tlk", "Convert XML to TLK");
            var inputArg = new Argument<string>("input", "Input XML file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output TLK file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("xml2tlk not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void AddSsf2Xml(RootCommand rootCommand)
        {
            var cmd = new Command("ssf2xml", "Convert SSF to XML");
            var inputArg = new Argument<string>("input", "Input SSF file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output XML file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("ssf2xml not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void AddXml2Ssf(RootCommand rootCommand)
        {
            var cmd = new Command("xml2ssf", "Convert XML to SSF");
            var inputArg = new Argument<string>("input", "Input XML file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output SSF file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("xml2ssf not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void Add2Da2Csv(RootCommand rootCommand)
        {
            var cmd = new Command("2da2csv", "Convert 2DA to CSV");
            var inputArg = new Argument<string>("input", "Input 2DA file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output CSV file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("2da2csv not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }

        private static void AddCsv22Da(RootCommand rootCommand)
        {
            var cmd = new Command("csv22da", "Convert CSV to 2DA");
            var inputArg = new Argument<string>("input", "Input CSV file");
            cmd.AddArgument(inputArg);
            var outputOpt = new Option<string>(new[] { "-o", "--output" }, "Output 2DA file");
            cmd.AddOption(outputOpt);
            cmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("csv22da not yet implemented"); }, inputArg, outputOpt);
            rootCommand.AddCommand(cmd);
        }
    }
}

