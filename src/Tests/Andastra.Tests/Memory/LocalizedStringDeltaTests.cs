using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.GFF;
using Xunit;

namespace Andastra.Parsing.Tests.Memory
{

    /// <summary>
    /// Tests for LocalizedStringDelta (ported from test_memory.py)
    /// </summary>
    public class LocalizedStringDeltaTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_StringRef_2DAMemory()
        {
            // Arrange
            var locstring = new LocalizedString(0);
            var delta = new LocalizedStringDelta(new FieldValue2DAMemory(5));
            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "123";

            // Act
            delta.Apply(locstring, memory);

            // Assert
            Assert.Equal(123, locstring.StringRef);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_StringRef_TLKMemory()
        {
            // Arrange
            var locstring = new LocalizedString(0);
            var delta = new LocalizedStringDelta(new FieldValueTLKMemory(5));
            var memory = new PatcherMemory();
            memory.MemoryStr[5] = 123;

            // Act
            delta.Apply(locstring, memory);

            // Assert
            Assert.Equal(123, locstring.StringRef);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_StringRef_Int()
        {
            // Arrange
            var locstring = new LocalizedString(0);
            var delta = new LocalizedStringDelta(new FieldValueConstant(123));
            var memory = new PatcherMemory();

            // Act
            delta.Apply(locstring, memory);

            // Assert
            Assert.Equal(123, locstring.StringRef);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_StringRef_None()
        {
            // Arrange
            var locstring = new LocalizedString(123);
            var delta = new LocalizedStringDelta(null);
            var memory = new PatcherMemory();

            // Act
            delta.Apply(locstring, memory);

            // Assert
            Assert.Equal(123, locstring.StringRef);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_Substring()
        {
            // Arrange
            var locstring = new LocalizedString(0);
            locstring.SetData(Language.English, Gender.Male, "a");
            locstring.SetData(Language.French, Gender.Male, "b");

            var delta = new LocalizedStringDelta();
            delta.SetData(Language.English, Gender.Male, "1");
            delta.SetData(Language.German, Gender.Male, "2");

            var memory = new PatcherMemory();

            // Act
            delta.Apply(locstring, memory);

            // Assert
            Assert.Equal(3, locstring.Count);
            Assert.Equal("1", locstring.Get(Language.English, Gender.Male));
            Assert.Equal("2", locstring.Get(Language.German, Gender.Male));
            Assert.Equal("b", locstring.Get(Language.French, Gender.Male));
        }
    }
}

