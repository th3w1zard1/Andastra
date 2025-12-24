using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common.Script;
using Andastra.Parsing.Formats.NCS.Compiler;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Tests for NCS interpreter/stack functionality.
    /// 1:1 port of test_ncs_interpreter.py from tests/resource/formats/test_ncs_interpreter.py
    /// </summary>
    public class NCSInterpreterTests
    {
        /// <summary>
        /// Python: test_peek_past_vector (from TestStack)
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPeekPastVector()
        {
            var stack = new Stack();
            stack.Add(DataType.Float, 1.0); // -20
            stack.Add(DataType.Vector, new Vector3(2.0f, 3.0f, 4.0f)); // -16
            stack.Add(DataType.Float, 5.0); // -4
            StackObject result = stack.Peek(-20);
            // Python: print(stack.peek(-20))
            // This test just verifies peek works, no assertion in Python
            result.Should().NotBeNull();
        }

        /// <summary>
        /// Python: test_move_negative (from TestStack)
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestMoveNegative()
        {
            var stack = new Stack();
            stack.Add(DataType.Float, 1); // -24
            stack.Add(DataType.Float, 2); // -20
            stack.Add(DataType.Float, 3); // -16
            stack.Add(DataType.Float, 4); // -12
            stack.Add(DataType.Float, 5); // -8
            stack.Add(DataType.Float, 6); // -4

            stack.Move(-12);
            List<StackObject> snapshot = stack.State();
            snapshot.Count.Should().Be(3);
            snapshot[2].Value.Should().Be(3.0);
            snapshot[1].Value.Should().Be(2.0);
            snapshot[0].Value.Should().Be(1.0);
        }

        /// <summary>
        /// Python: test_move_zero (from TestStack)
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestMoveZero()
        {
            var stack = new Stack();
            stack.Add(DataType.Float, 1); // -24
            stack.Add(DataType.Float, 2); // -20
            stack.Add(DataType.Float, 3); // -16
            stack.Add(DataType.Float, 4); // -12
            stack.Add(DataType.Float, 5); // -8
            stack.Add(DataType.Float, 6); // -4

            stack.Move(0);
            List<StackObject> snapshot = stack.State();
            snapshot.Count.Should().Be(6);
            snapshot[5].Value.Should().Be(6.0);
            snapshot[4].Value.Should().Be(5.0);
            snapshot[3].Value.Should().Be(4.0);
            snapshot[2].Value.Should().Be(3.0);
            snapshot[1].Value.Should().Be(2.0);
            snapshot[0].Value.Should().Be(1.0);
        }

        /// <summary>
        /// Python: test_copy_down_single (from TestStack)
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCopyDownSingle()
        {
            var stack = new Stack();
            stack.Add(DataType.Float, 1); // -24
            stack.Add(DataType.Float, 2); // -20
            stack.Add(DataType.Float, 3); // -16
            stack.Add(DataType.Float, 4); // -12
            stack.Add(DataType.Float, 5); // -8
            stack.Add(DataType.Float, 6); // -4

            stack.CopyDown(-12, 4);

            stack.Peek(-12).Value.Should().Be(6.0);
        }

        /// <summary>
        /// Python: test_copy_down_many (from TestStack)
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCopyDownMany()
        {
            var stack = new Stack();
            stack.Add(DataType.Float, 1); // -24
            stack.Add(DataType.Float, 2); // -20
            stack.Add(DataType.Float, 3); // -16
            stack.Add(DataType.Float, 4); // -12
            stack.Add(DataType.Float, 5); // -8
            stack.Add(DataType.Float, 6); // -4

            stack.CopyDown(-24, 12);
            // Python: print(stack.state())

            stack.Peek(-24).Value.Should().Be(4.0);
            stack.Peek(-20).Value.Should().Be(5.0);
            stack.Peek(-16).Value.Should().Be(6.0);
        }

    }
}



