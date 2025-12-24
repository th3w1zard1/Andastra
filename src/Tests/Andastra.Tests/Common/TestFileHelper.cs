using System;
using System.IO;

namespace Andastra.Parsing.Tests.Common
{

    internal static class TestFileHelper
    {
        public static string GetPath(string relativePath) =>
            Path.Combine(AppContext.BaseDirectory, "test_files", relativePath);
    }
}

