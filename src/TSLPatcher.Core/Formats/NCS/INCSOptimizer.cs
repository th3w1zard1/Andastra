using System.Collections.Generic;
using JetBrains.Annotations;
using TSLPatcher.Core.Common;

namespace TSLPatcher.Core.Formats.NCS
{

    /// <summary>
    /// Base interface for NCS bytecode optimizers.
    /// </summary>
    public interface INCSOptimizer
    {
        int InstructionsCleared { get; }
        void Optimize(NCS ncs);
        void Reset();
    }

    /// <summary>
    /// Base class for NCS optimizers with common functionality.
    /// </summary>
    public abstract class NCSOptimizerBase : INCSOptimizer
    {
        public int InstructionsCleared { get; protected set; }

        public abstract void Optimize(NCS ncs);

        public virtual void Reset()
        {
            InstructionsCleared = 0;
        }
    }

    /// <summary>
    /// Abstract base class for NCS compilers.
    /// </summary>
    public abstract class NCSCompiler
    {
        /// <summary>
        /// Compiles an NSS script file to an NCS bytecode file.
        /// </summary>
        /// <param name="sourcePath">Path to the source NSS file</param>
        /// <param name="outputPath">Path to output the compiled NCS file</param>
        /// <param name="game">Target game (K1 or TSL)</param>
        /// <param name="optimizers">Optional list of optimizers to apply</param>
        /// <param name="debug">Enable debug output</param>
        public abstract void CompileScript(
            string sourcePath,
            string outputPath,
            Game game,
            [CanBeNull] List<INCSOptimizer> optimizers = null,
            bool debug = false);
    }
}

