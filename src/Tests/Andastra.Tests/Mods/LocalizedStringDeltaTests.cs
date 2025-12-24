using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.GFF;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Mods
{

    /// <summary>
    /// Tests for LocalizedStringDelta apply operations.
    /// </summary>
    public class LocalizedStringDeltaTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ApplyStringRef2DAMemory_ShouldSetCorrectValue()
        {
            var locstring = new LocalizedString(0);
            var delta = new LocalizedStringDelta(new FieldValue2DAMemory(5));

            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "123";

            delta.Apply(locstring, memory);

            locstring.StringRef.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ApplyStringRefTLKMemory_ShouldSetCorrectValue()
        {
            var locstring = new LocalizedString(0);
            var delta = new LocalizedStringDelta(new FieldValueTLKMemory(5));

            var memory = new PatcherMemory();
            memory.MemoryStr[5] = 123;

            delta.Apply(locstring, memory);

            locstring.StringRef.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ApplyStringRefInt_ShouldSetCorrectValue()
        {
            var locstring = new LocalizedString(0);
            var delta = new LocalizedStringDelta(new FieldValueConstant(123));

            var memory = new PatcherMemory();

            delta.Apply(locstring, memory);

            locstring.StringRef.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ApplyStringRefNone_ShouldLeaveValueUnchanged()
        {
            var locstring = new LocalizedString(123);
            var delta = new LocalizedStringDelta(null);

            var memory = new PatcherMemory();

            delta.Apply(locstring, memory);

            locstring.StringRef.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ApplySubstring_ShouldMergeCorrectly()
        {
            var locstring = new LocalizedString(0);
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

    }
}

