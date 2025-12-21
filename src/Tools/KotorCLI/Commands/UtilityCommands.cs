using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Utility commands (diff, grep, stats, validate, merge, cat).
    /// </summary>
    public static class UtilityCommands
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var diffCmd = new Command("diff", "Compare two files and show differences");
            var file1Arg = new Argument<string>("file1", "First file");
            diffCmd.AddArgument(file1Arg);
            var file2Arg = new Argument<string>("file2", "Second file");
            diffCmd.AddArgument(file2Arg);
            diffCmd.SetHandler((string file1, string file2) => { var logger = new StandardLogger(); logger.Info("diff not yet implemented"); }, file1Arg, file2Arg);
            rootCommand.AddCommand(diffCmd);

            var grepCmd = new Command("grep", "Search for patterns in files");
            var grepFileArg = new Argument<string>("file", "File to search");
            grepCmd.AddArgument(grepFileArg);
            var patternArg = new Argument<string>("pattern", "Search pattern");
            grepCmd.AddArgument(patternArg);
            grepCmd.SetHandler((string file, string pattern) => { var logger = new StandardLogger(); logger.Info("grep not yet implemented"); }, grepFileArg, patternArg);
            rootCommand.AddCommand(grepCmd);

            var statsCmd = new Command("stats", "Show statistics about a file");
            var statsFileArg = new Argument<string>("file", "File to analyze");
            statsCmd.AddArgument(statsFileArg);
            statsCmd.SetHandler((string file) => { var logger = new StandardLogger(); logger.Info("stats not yet implemented"); }, statsFileArg);
            rootCommand.AddCommand(statsCmd);

            var validateCmd = new Command("validate", "Validate file format and structure");
            var validateFileArg = new Argument<string>("file", "File to validate");
            validateCmd.AddArgument(validateFileArg);
            validateCmd.SetHandler((string file) => { var logger = new StandardLogger(); logger.Info("validate not yet implemented"); }, validateFileArg);
            rootCommand.AddCommand(validateCmd);

            var mergeCmd = new Command("merge", "Merge two GFF files");
            var targetArg = new Argument<string>("target", "Target GFF file (will be modified)");
            mergeCmd.AddArgument(targetArg);
            var sourceArg = new Argument<string>("source", "Source GFF file (fields to merge)");
            mergeCmd.AddArgument(sourceArg);
            mergeCmd.SetHandler((string target, string source) => { var logger = new StandardLogger(); logger.Info("merge not yet implemented"); }, targetArg, sourceArg);
            rootCommand.AddCommand(mergeCmd);

            var catCmd = new Command("cat", "Display resource contents to stdout");
            var archiveArg = new Argument<string>("archive", "Archive file (ERF, RIM)");
            catCmd.AddArgument(archiveArg);
            var resourceArg = new Argument<string>("resource", "Resource reference name");
            catCmd.AddArgument(resourceArg);
            catCmd.SetHandler((string archive, string resource) => { var logger = new StandardLogger(); logger.Info("cat not yet implemented"); }, archiveArg, resourceArg);
            rootCommand.AddCommand(catCmd);
        }
    }
}

