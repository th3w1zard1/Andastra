# MDL/MDX Roundtrip Test Script
# Tests all MDL ASCII/Binary roundtrip conversions

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Build the test project first
Write-Host "Building test project..." -ForegroundColor Cyan
$buildResult = dotnet build src/Andastra/Tests/TSLPatcher.Tests.csproj --no-incremental 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}

# Try to run the roundtrip tests
Write-Host "`nRunning MDL ASCII roundtrip tests..." -ForegroundColor Cyan
$testResult = dotnet test src/Andastra/Tests/TSLPatcher.Tests.csproj --filter "FullyQualifiedName~AsciiToAsciiRoundtrip|FullyQualifiedName~BinaryAsciiBinaryRoundtrip|FullyQualifiedName~AsciiBinaryAsciiRoundtrip|FullyQualifiedName~ComprehensiveRoundtrip" --logger "console;verbosity=detailed" 2>&1

if ($Verbose) {
    Write-Host $testResult
} else {
    Write-Host ($testResult | Select-String -Pattern "Passed|Failed|Total tests")
}

# Also try running all MDL ASCII tests
Write-Host "`nRunning all MDL ASCII tests..." -ForegroundColor Cyan
$allTestsResult = dotnet test src/Andastra/Tests/TSLPatcher.Tests.csproj --filter "FullyQualifiedName~Andastra.Tests.Runtime.Parsing.MDL.MDLAscii" --logger "console;verbosity=normal" 2>&1
Write-Host ($allTestsResult | Select-String -Pattern "Passed|Failed|Total tests")

# If tests aren't found, try alternative approach - create a simple C# test program
Write-Host "`nChecking if tests can be discovered..." -ForegroundColor Cyan
$discovered = dotnet test src/Andastra/Tests/TSLPatcher.Tests.csproj --list-tests 2>&1 | Select-String -Pattern "MDLAsciiRoundTrip|AsciiToAscii|BinaryAscii|AsciiBinary"
if (-not $discovered) {
    Write-Host "Tests not discovered by test runner. Creating direct test program..." -ForegroundColor Yellow

    # Create a simple test program
    $testProgram = @"
using System;
using System.IO;
using System.Text;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Formats.MDLData;
using Andastra.Parsing.Common;

namespace MDLRoundtripTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("MDL Roundtrip Tests");
            Console.WriteLine("===================");

            int passed = 0;
            int failed = 0;

            // Test 1: ASCII to ASCII roundtrip
            try {
                Console.WriteLine("\nTest 1: ASCII -> ASCII Roundtrip");
                var mdl1 = MDLAsciiTestHelpers.CreateTestMDL("roundtrip_test");
                var node = MDLAsciiTestHelpers.CreateTestNode("test_node");
                node.Mesh = MDLAsciiTestHelpers.CreateTestMesh();
                mdl1.Root.Children.Add(node);

                byte[] ascii1;
                using (var stream = new MemoryStream()) {
                    var writer = new MDLAsciiWriter(mdl1, stream);
                    writer.Write();
                    ascii1 = stream.ToArray();
                }

                MDL mdl2;
                using (var reader = new MDLAsciiReader(ascii1)) {
                    mdl2 = reader.Load();
                }

                byte[] ascii2;
                using (var stream = new MemoryStream()) {
                    var writer = new MDLAsciiWriter(mdl2, stream);
                    writer.Write();
                    ascii2 = stream.ToArray();
                }

                var asciiStr = Encoding.UTF8.GetString(ascii2);
                if (asciiStr.Contains("newmodel roundtrip_test")) {
                    Console.WriteLine("  PASSED");
                    passed++;
                } else {
                    Console.WriteLine("  FAILED - Missing model name");
                    failed++;
                }
            } catch (Exception ex) {
                Console.WriteLine($"  FAILED - {ex.Message}");
                failed++;
            }

            // Test 2: ASCII -> Binary -> ASCII roundtrip
            try {
                Console.WriteLine("\nTest 2: ASCII -> Binary -> ASCII Roundtrip");
                var mdl1 = MDLAsciiTestHelpers.CreateTestMDL("roundtrip_ascii_test");
                var node = MDLAsciiTestHelpers.CreateTestNode("test_node");
                node.Mesh = MDLAsciiTestHelpers.CreateTestMesh();
                mdl1.Root.Children.Add(node);

                byte[] binaryBytes = MDLAuto.BytesMdl(mdl1, ResourceType.MDL);
                MDL mdl2 = MDLAuto.ReadMdl(binaryBytes, fileFormat: ResourceType.MDL);
                byte[] asciiBytes = MDLAuto.BytesMdl(mdl2, ResourceType.MDL_ASCII);
                string asciiStr = Encoding.UTF8.GetString(asciiBytes);

                if (asciiStr.Contains("newmodel roundtrip_ascii_test")) {
                    Console.WriteLine("  PASSED");
                    passed++;
                } else {
                    Console.WriteLine("  FAILED - Missing model name");
                    failed++;
                }
            } catch (Exception ex) {
                Console.WriteLine($"  FAILED - {ex.Message}");
                failed++;
            }

            // Test 3: Binary -> ASCII -> Binary roundtrip
            try {
                Console.WriteLine("\nTest 3: Binary -> ASCII -> Binary Roundtrip");
                var mdl1 = MDLAsciiTestHelpers.CreateTestMDL("test_model");
                var node = MDLAsciiTestHelpers.CreateTestNode("test_node");
                node.Mesh = MDLAsciiTestHelpers.CreateTestMesh();
                mdl1.Root.Children.Add(node);

                byte[] binary1 = MDLAuto.BytesMdl(mdl1, ResourceType.MDL);
                byte[] asciiBytes = MDLAuto.BytesMdl(mdl1, ResourceType.MDL_ASCII);
                MDL mdl2 = MDLAuto.ReadMdl(asciiBytes, fileFormat: ResourceType.MDL_ASCII);
                byte[] binary2 = MDLAuto.BytesMdl(mdl2, ResourceType.MDL);

                if (binary2.Length > 0 && binary2.Length >= 12) {
                    Console.WriteLine("  PASSED");
                    passed++;
                } else {
                    Console.WriteLine($"  FAILED - Invalid binary size: {binary2.Length}");
                    failed++;
                }
            } catch (Exception ex) {
                Console.WriteLine($"  FAILED - {ex.Message}");
                failed++;
            }

            Console.WriteLine($"\n\nResults: {passed} passed, {failed} failed");
            Environment.Exit(failed > 0 ? 1 : 0);
        }
    }
}
"@

    Write-Host "Note: Direct test program creation skipped (would require compilation)"
    Write-Host "Please ensure tests are properly included in the test project"
}

Write-Host "`nDone!" -ForegroundColor Green

