using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Formats.NCS.Compiler;
using Andastra.Parsing.Formats.NCS.Optimizers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Tests for NCS optimizers.
    /// 1:1 port of test_ncs_optimizer.py from tests/resource/formats/test_ncs_optimizer.py
    ///
    /// NOTE: These tests require NSS compilation and NCS optimizer functionality.
    /// </summary>
    public class NCSOptimizerTests
    {
        private NCS Compile(
            string script,
            [CanBeNull] Dictionary<string, byte[]> library = null,
            [CanBeNull] string libraryLookup = null)
        {
            if (library is null)
            {
                library = new Dictionary<string, byte[]>();
            }
            List<string> lookup = !(libraryLookup is null) ? new List<string> { libraryLookup } : null;
            return NCSAuto.CompileNss(script, BioWareGame.K1, null, null, lookup);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestNoOpOptimizer()
        {
            NCS ncs = Compile(@"
            void main()
            {
                int value = 3;
                while (value > 0)
                {
                    if (value > 0)
                    {
                        PrintInteger(value);
                        value -= 1;
                    }
                }
            }
        ");

            ncs.Optimize(new List<NCSOptimizer> { new RemoveNopOptimizer() });
            ncs.Print();

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(1);
        }
    }
}


