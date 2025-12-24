using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.GFF;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Memory
{

    /// <summary>
    /// Tests for PatcherMemory and LocalizedStringDelta
    /// 1:1 port from tests/tslpatcher/test_memory.py
    /// </summary>
    public class PatcherMemoryTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Memory2DA_StoresAndRetrievesValues()
        {
            var memory = new PatcherMemory();

            memory.Memory2DA[5] = "123";
            memory.Memory2DA[10] = "test";

            memory.Memory2DA[5].Should().Be("123");
            memory.Memory2DA[10].Should().Be("test");
            memory.Memory2DA.Should().HaveCount(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void MemoryStr_StoresAndRetrievesValues()
        {
            var memory = new PatcherMemory();

            memory.MemoryStr[5] = 123;
            memory.MemoryStr[10] = 456;

            memory.MemoryStr[5].Should().Be(123);
            memory.MemoryStr[10].Should().Be(456);
            memory.MemoryStr.Should().HaveCount(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ToString_ReturnsFormattedString()
        {
            var memory = new PatcherMemory();
            memory.Memory2DA[1] = "test";
            memory.MemoryStr[2] = 42;

            string result = memory.ToString();

            result.Should().Contain("memory_2da=1 items");
            result.Should().Contain("memory_str=1 items");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Memory_SupportsMultipleUpdates()
        {
            var memory = new PatcherMemory();

            memory.Memory2DA[0] = "first";
            memory.Memory2DA[0] = "updated";
            memory.MemoryStr[0] = 100;
            memory.MemoryStr[0] = 200;

            memory.Memory2DA[0].Should().Be("updated");
            memory.MemoryStr[0].Should().Be(200);
        }

        #region LocalizedStringDelta Tests - from test_memory.py

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LocalizedStringDelta_ApplyStringref2DAMemory()
        {
            // Python test: test_apply_stringref_2damemory
            var locstring = LocalizedString.FromInvalid();

            var delta = new LocalizedStringDelta(new FieldValue2DAMemory(5));

            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "123";

            delta.Apply(locstring, memory);

            locstring.StringRef.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LocalizedStringDelta_ApplyStringrefTLKMemory()
        {
            // Python test: test_apply_stringref_tlkmemory
            var locstring = LocalizedString.FromInvalid();

            var delta = new LocalizedStringDelta(new FieldValueTLKMemory(5));

            var memory = new PatcherMemory();
            memory.MemoryStr[5] = 123;

            delta.Apply(locstring, memory);

            locstring.StringRef.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LocalizedStringDelta_ApplyStringrefInt()
        {
            // Python test: test_apply_stringref_int
            var locstring = LocalizedString.FromInvalid();

            var delta = new LocalizedStringDelta(new FieldValueConstant(123));

            var memory = new PatcherMemory();

            delta.Apply(locstring, memory);

            locstring.StringRef.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LocalizedStringDelta_ApplyStringrefNone()
        {
            // Python test: test_apply_stringref_none
            var locstring = new LocalizedString(123);

            var delta = new LocalizedStringDelta(null);

            var memory = new PatcherMemory();

            delta.Apply(locstring, memory);

            locstring.StringRef.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LocalizedStringDelta_ApplySubstring()
        {
            // Python test: test_apply_substring
            var locstring = LocalizedString.FromInvalid();
            locstring.SetData(Language.English, Gender.Male, "a");
            locstring.SetData(Language.French, Gender.Male, "b");

            var delta = new LocalizedStringDelta();
            delta.SetData(Language.English, Gender.Male, "1");
            delta.SetData(Language.German, Gender.Male, "2");

            var memory = new PatcherMemory();

            delta.Apply(locstring, memory);

            locstring.Count.Should().Be(3);
            locstring.Get(Language.English, Gender.Male).Should().Be("1");
            locstring.Get(Language.German, Gender.Male).Should().Be("2");
            locstring.Get(Language.French, Gender.Male).Should().Be("b");
        }

        #endregion
    }
}
