using System;
using System.Diagnostics;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.NCS.Decomp;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.NCS.Compiler
{
    /// <summary>
    /// External NSS compiler wrapper for nwnnsscomp.exe.
    /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compilers.py:219-440
    /// </summary>
    public class ExternalNCSCompiler
    {
        private string _nwnnsscompPath;
        private string _fileHash;

        public ExternalNCSCompiler(string nwnnsscompPath)
        {
            ChangeNwnnsscompPath(nwnnsscompPath);
        }

        /// <summary>
        /// Gets information about the compiler based on its SHA256 hash.
        /// </summary>
        public KnownExternalCompilers.CompilerInfo GetInfo()
        {
            if (string.IsNullOrEmpty(_fileHash))
            {
                return null;
            }
            return KnownExternalCompilers.FromSha256(_fileHash);
        }

        /// <summary>
        /// Changes the path to nwnnsscomp.exe and updates the file hash.
        /// </summary>
        public void ChangeNwnnsscompPath(string nwnnsscompPath)
        {
            _nwnnsscompPath = nwnnsscompPath;
            if (string.IsNullOrEmpty(nwnnsscompPath) || !File.Exists(nwnnsscompPath))
            {
                _fileHash = "";
                return;
            }

            try
            {
                NcsFile compilerFile = new NcsFile(nwnnsscompPath);
                _fileHash = HashUtil.CalculateSHA256(compilerFile);
            }
            catch (Exception)
            {
                _fileHash = "";
            }
        }

        /// <summary>
        /// Compiles a NSS script to NCS using the external compiler and returns stdout/stderr.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compilers.py:262-338
        /// </summary>
        /// <param name="sourceFile">Path to the NSS source file</param>
        /// <param name="outputFile">Path to the output NCS file</param>
        /// <param name="game">The game being compiled for (K1 or K2)</param>
        /// <param name="timeout">Timeout in seconds (default: 5)</param>
        /// <returns>Tuple of (stdout, stderr) strings from the compilation process</returns>
        /// <exception cref="FileNotFoundException">If source file doesn't exist</exception>
        /// <exception cref="Exception">If compiler executable doesn't exist or compilation fails</exception>
        /// <exception cref="EntryPointException">If file has no entry point and is an include file</exception>
        public (string stdout, string stderr) CompileScriptWithOutput(string sourceFile, string outputFile, BioWareGame game, int timeout = 5)
        {
            if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile))
            {
                throw new System.IO.FileNotFoundException("Source file not found: " + sourceFile);
            }

            if (string.IsNullOrEmpty(_nwnnsscompPath) || !File.Exists(_nwnnsscompPath))
            {
                throw new Exception("Compiler executable not found: " + _nwnnsscompPath);
            }

            try
            {
                NcsFile compilerFile = new NcsFile(_nwnnsscompPath);
                NcsFile sourceNcsFile = new NcsFile(sourceFile);
                NcsFile outputNcsFile = new NcsFile(outputFile);
                bool isK2 = game == BioWareGame.TSL;

                NwnnsscompConfig config = new NwnnsscompConfig(compilerFile, sourceNcsFile, outputNcsFile, isK2);
                string[] args = config.GetCompileArgs(_nwnnsscompPath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = args[0],
                    Arguments = string.Join(" ", args, 1, args.Length - 1),
                    WorkingDirectory = Path.GetDirectoryName(sourceFile) ?? Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    var output = new System.Text.StringBuilder();
                    var error = new System.Text.StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            output.AppendLine(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            error.AppendLine(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool finished = process.WaitForExit(timeout * 1000);

                    if (!finished)
                    {
                        process.Kill();
                        throw new Exception($"Compilation timed out after {timeout} seconds");
                    }

                    process.WaitForExit(); // Ensure all async output is captured

                    string stdout = GetOutput(output);
                    string stderr = GetOutput(error);

                    // Check for known error conditions
                    if (stdout.Contains("File is an include file, ignored"))
                    {
                        throw new EntryPointException("This file has no entry point and cannot be compiled (Most likely an include file).");
                    }

                    // Move "Error:" lines from stdout to stderr (matching PyKotor behavior)
                    if (stdout.Contains("Error:"))
                    {
                        string[] stdoutLines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        string errorLines = "";
                        System.Text.StringBuilder filteredStdout = new System.Text.StringBuilder();

                        foreach (string line in stdoutLines)
                        {
                            if (line.Contains("Error:"))
                            {
                                if (errorLines.Length > 0)
                                {
                                    errorLines += "\n";
                                }
                                errorLines += line;
                            }
                            else
                            {
                                if (filteredStdout.Length > 0)
                                {
                                    filteredStdout.Append("\n");
                                }
                                filteredStdout.Append(line);
                            }
                        }

                        stdout = filteredStdout.ToString();
                        if (errorLines.Length > 0)
                        {
                            if (stderr.Length > 0)
                            {
                                stderr += "\n" + errorLines;
                            }
                            else
                            {
                                stderr = errorLines;
                            }
                        }
                    }

                    // Handle error output if return code is non-zero
                    if (process.ExitCode != 0)
                    {
                        if (string.IsNullOrEmpty(stderr) || stderr.Trim().Length == 0)
                        {
                            stderr = $"No error provided, but return code is nonzero: ({process.ExitCode})";
                        }
                        throw new Exception($"Compilation failed with return code {process.ExitCode}: {stderr}");
                    }

                    return (stdout, stderr);
                }
            }
            catch (EntryPointException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (e is System.IO.FileNotFoundException || e is EntryPointException)
                {
                    throw;
                }
                throw new Exception("Failed to run compiler: " + e.Message, e);
            }
        }

        /// <summary>
        /// Helper method to get output from StringBuilder.
        /// </summary>
        private string GetOutput(System.Text.StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                return "";
            }
            string result = builder.ToString();
            // Remove trailing newline if present
            if (result.EndsWith("\r\n"))
            {
                return result.Substring(0, result.Length - 2);
            }
            if (result.EndsWith("\n") || result.EndsWith("\r"))
            {
                return result.Substring(0, result.Length - 1);
            }
            return result;
        }

        /// <summary>
        /// Exception thrown when a file has no entry point (e.g., include files).
        /// Matching PyKotor EntryPointError at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compilers.py:300-305
        /// </summary>
        public class EntryPointException : Exception
        {
            public EntryPointException(string message) : base(message)
            {
            }
        }
    }
}
