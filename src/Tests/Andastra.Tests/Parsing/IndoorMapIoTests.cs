using System;
using System.IO;

using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tools;

using Xunit;

namespace Andastra.Tests.Parsing
{
    public class IndoorMapIoTests
    {
        [Fact]
        public void ExtractEmbeddedIndoorJsonFromMod_ReturnsBytes()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), "and_indoor_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            try
            {
                string modPath = Path.Combine(tmpDir, "test01.mod");

                var erf = new ERF(ERFType.MOD);
                byte[] payload = System.Text.Encoding.UTF8.GetBytes("{\"module_id\":\"test01\",\"rooms\":[]}");
                erf.SetData(IndoorMapIo.EmbeddedResRef, ResourceType.TXT, payload);
                ERFAuto.WriteErf(erf, modPath, ResourceType.MOD);

                byte[] extracted = IndoorMapIo.TryExtractEmbeddedIndoorJsonFromModuleFiles(new[] { modPath });
                Assert.NotNull(extracted);
                Assert.Equal(payload, extracted);
            }
            finally
            {
                try { Directory.Delete(tmpDir, true); }
                catch { }
            }
        }
    }
}


