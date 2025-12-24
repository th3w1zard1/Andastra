using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Shader cache system for efficient shader compilation and reuse.
    /// 
    /// Shader caching stores compiled shaders to avoid recompilation overhead,
    /// significantly improving load times and reducing CPU usage.
    /// 
    /// Features:
    /// - Shader compilation caching
    /// - Persistent shader storage
    /// - Shader variant management
    /// - Hot reload support
    /// </summary>
    /// <remarks>
    /// Shader Cache System (Modern Enhancement):
    /// - Based on swkotor2.exe rendering system architecture
    /// - Located via string references: OpenGL shader extension functions
    /// - Vertex shader extensions: glGenVertexShadersEXT, glBindVertexShaderEXT, glBeginVertexShaderEXT, glEndVertexShaderEXT
    /// - glDeleteVertexShadersEXT, glShaderOp1EXT, glShaderOp2EXT, glShaderOp3EXT
    /// - Fragment shader extensions: "GL_ATI_fragment_shader" @ 0x007b7454
    /// - glGenFragmentShadersATI, glBindFragmentShaderATI, glBeginFragmentShaderATI, glEndFragmentShaderATI
    /// - glDeleteFragmentShaderATI, glSetFragmentShaderConstantATI
    /// - Texture shader: "GL_NV_texture_shader" @ 0x007b895c
    /// - Original implementation: KOTOR used fixed-function DirectX 8/9 pipeline with minimal programmable shaders
    /// - Original engine: Most rendering used fixed-function pipeline, shaders compiled at engine initialization
    /// - This is a modernization feature: Original engine did not have runtime shader compilation/caching
    /// - Original shaders: Pre-compiled HLSL/FX shaders embedded in engine, loaded from .fx files
    /// - Modern enhancement: Runtime shader compilation with caching improves flexibility and development workflow
    /// </remarks>
    public class ShaderCache
    {
        /// <summary>
        /// Cached shader entry.
        /// </summary>
        private class ShaderEntry
        {
            public string ShaderName;
            public string ShaderSource;
            public byte[] CompiledBytecode;
            public Effect CompiledEffect;
            public DateTime LastModified;
            public int UseCount;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, ShaderEntry> _cache;
        private readonly object _lock;
        private readonly string _cacheDirectory;

        /// <summary>
        /// Gets the number of cached shaders.
        /// </summary>
        public int CacheSize
        {
            get { return _cache.Count; }
        }

        /// <summary>
        /// Initializes a new shader cache.
        /// </summary>
        public ShaderCache(GraphicsDevice graphicsDevice, string cacheDirectory = null)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _cache = new Dictionary<string, ShaderEntry>();
            _lock = new object();
            _cacheDirectory = cacheDirectory ?? "ShaderCache";

            // Load existing cache from disk if available
            LoadCache();
        }

        /// <summary>
        /// Gets a shader from cache or compiles it.
        /// </summary>
        /// <param name="shaderName">Shader name/identifier. Must not be null or empty.</param>
        /// <param name="shaderSource">Shader source code. Must not be null or empty.</param>
        /// <returns>Compiled shader effect, or null if compilation failed or parameters are invalid.</returns>
        public Effect GetShader(string shaderName, string shaderSource)
        {
            if (string.IsNullOrEmpty(shaderName) || string.IsNullOrEmpty(shaderSource))
            {
                return null;
            }

            lock (_lock)
            {
                if (_cache.TryGetValue(shaderName, out ShaderEntry entry))
                {
                    // Check if shader source changed
                    if (entry.ShaderSource == shaderSource)
                    {
                        entry.UseCount++;
                        return entry.CompiledEffect;
                    }
                    else
                    {
                        // Shader source changed, recompile
                        entry.ShaderSource = shaderSource;
                        entry.CompiledEffect?.Dispose();
                    }
                }
                else
                {
                    entry = new ShaderEntry
                    {
                        ShaderName = shaderName,
                        ShaderSource = shaderSource,
                        UseCount = 0
                    };
                    _cache[shaderName] = entry;
                }

                // Compile shader
                try
                {
                    // Compile shader from source to bytecode
                    byte[] bytecode = CompileShaderToBytecode(shaderSource);
                    if (bytecode == null || bytecode.Length == 0)
                    {
                        // Compilation failed
                        _cache.Remove(shaderName);
                        return null;
                    }

                    // Create Effect from compiled bytecode
                    Effect effect = new Effect(_graphicsDevice, bytecode);

                    entry.CompiledEffect = effect;
                    entry.CompiledBytecode = bytecode;
                    entry.LastModified = DateTime.UtcNow;
                    entry.UseCount++;

                    // Save to disk cache
                    SaveShaderToDisk(entry);

                    return entry.CompiledEffect;
                }
                catch (Exception ex)
                {
                    // Compilation failed - log error for debugging
                    System.Diagnostics.Debug.WriteLine($"[ShaderCache] Failed to compile shader '{shaderName}': {ex.Message}");
                    _cache.Remove(shaderName);
                    return null;
                }
            }
        }

        /// <summary>
        /// Precompiles and caches a shader.
        /// </summary>
        /// <param name="shaderName">Shader name/identifier. Must not be null or empty.</param>
        /// <param name="shaderSource">Shader source code. Must not be null or empty.</param>
        /// <returns>True if shader was compiled and cached successfully, false otherwise.</returns>
        public bool PrecompileShader(string shaderName, string shaderSource)
        {
            return GetShader(shaderName, shaderSource) != null;
        }

        /// <summary>
        /// Clears the shader cache.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (ShaderEntry entry in _cache.Values)
                {
                    entry.CompiledEffect?.Dispose();
                }
                _cache.Clear();
            }
        }

        /// <summary>
        /// Loads shader cache from disk.
        /// </summary>
        /// <remarks>
        /// Loads pre-compiled shader bytecode from disk cache to avoid recompilation.
        /// This significantly speeds up subsequent runs by skipping shader compilation.
        /// </remarks>
        private void LoadCache()
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                    return;
                }

                string[] cacheFiles = Directory.GetFiles(_cacheDirectory, "*.shader");
                foreach (string cacheFile in cacheFiles)
                {
                    try
                    {
                        string shaderName = Path.GetFileNameWithoutExtension(cacheFile);
                        byte[] bytecode = File.ReadAllBytes(cacheFile);

                        if (bytecode != null && bytecode.Length > 0)
                        {
                            // Try to create Effect from cached bytecode
                            Effect effect = new Effect(_graphicsDevice, bytecode);

                            // Create cache entry
                            ShaderEntry entry = new ShaderEntry
                            {
                                ShaderName = shaderName,
                                ShaderSource = null, // Source not stored in cache, will need recompilation if changed
                                CompiledBytecode = bytecode,
                                CompiledEffect = effect,
                                LastModified = File.GetLastWriteTimeUtc(cacheFile),
                                UseCount = 0
                            };

                            _cache[shaderName] = entry;
                        }
                    }
                    catch
                    {
                        // Skip invalid cache files
                        continue;
                    }
                }
            }
            catch
            {
                // Cache loading failed, continue without cache
            }
        }

        /// <summary>
        /// Saves shader to disk cache.
        /// </summary>
        /// <param name="entry">Shader cache entry to save. Must not be null.</param>
        /// <remarks>
        /// Saves compiled shader bytecode to disk for fast loading on subsequent runs.
        /// Allows fast loading on subsequent runs without recompilation.
        /// </remarks>
        private void SaveShaderToDisk(ShaderEntry entry)
        {
            if (entry == null || entry.CompiledBytecode == null || entry.CompiledBytecode.Length == 0)
            {
                return;
            }

            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                }

                string cacheFile = Path.Combine(_cacheDirectory, $"{entry.ShaderName}.shader");
                File.WriteAllBytes(cacheFile, entry.CompiledBytecode);
            }
            catch
            {
                // Cache save failed, continue without caching
            }
        }

        /// <summary>
        /// Compiles shader source code to bytecode.
        /// </summary>
        /// <param name="shaderSource">HLSL/FX shader source code. Must not be null or empty.</param>
        /// <returns>Compiled shader bytecode, or null if compilation failed.</returns>
        /// <remarks>
        /// Shader Compilation (swkotor2.exe: 0x0081c228, 0x0081fe20):
        /// - Original engine: Pre-compiled HLSL/FX shaders embedded in engine, loaded from .fx files
        /// - Original implementation: DirectX 8/9 fixed-function pipeline, minimal programmable shaders
        /// - Vertex program for skinned animations: GPU skinning shader compiled at engine initialization
        /// - This implementation: Runtime shader compilation with platform-specific APIs
        /// - Supports DirectX (D3DCompile) and OpenGL (via MGFXC or platform APIs)
        /// - Modern enhancement: Runtime compilation improves flexibility and development workflow
        /// </remarks>
        private byte[] CompileShaderToBytecode(string shaderSource)
        {
            if (string.IsNullOrEmpty(shaderSource))
            {
                return null;
            }

            // Try DirectX compilation first (Windows)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                byte[] dxBytecode = CompileShaderDirectX(shaderSource);
                if (dxBytecode != null && dxBytecode.Length > 0)
                {
                    return dxBytecode;
                }
            }

            // Fallback: Try MGFXC compilation (cross-platform)
            byte[] mgfxcBytecode = CompileShaderMGFXC(shaderSource);
            if (mgfxcBytecode != null && mgfxcBytecode.Length > 0)
            {
                return mgfxcBytecode;
            }

            // Last resort: Try OpenGL compilation
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                byte[] glBytecode = CompileShaderOpenGL(shaderSource);
                if (glBytecode != null && glBytecode.Length > 0)
                {
                    return glBytecode;
                }
            }

            return null;
        }

        /// <summary>
        /// Compiles shader using DirectX D3DCompile API.
        /// </summary>
        /// <param name="shaderSource">HLSL shader source code.</param>
        /// <returns>Compiled bytecode, or null if compilation failed.</returns>
        /// <remarks>
        /// DirectX Shader Compilation:
        /// - Uses D3DCompile from d3dcompiler_47.dll (Windows)
        /// - Compiles HLSL source to DirectX bytecode
        /// - Supports shader model 4.0+ (vs_4_0, ps_4_0, etc.)
        /// - Original game: DirectX 8/9 fixed-function pipeline, minimal shader usage
        /// - Modern enhancement: Full programmable shader support
        /// </remarks>
        private byte[] CompileShaderDirectX(string shaderSource)
        {
            try
            {
                // Determine shader type from source (look for technique/pass keywords)
                bool isEffectFile = shaderSource.Contains("technique") || shaderSource.Contains("pass");

                // For effect files, compile as effect
                // For individual shaders, determine type from entry point
                string entryPoint = "Main";
                string target = "fx_5_0"; // Effect file target

                if (!isEffectFile)
                {
                    // Try to detect shader type from source
                    if (shaderSource.Contains("VS_") || shaderSource.Contains("VertexShader"))
                    {
                        target = "vs_5_0";
                    }
                    else if (shaderSource.Contains("PS_") || shaderSource.Contains("PixelShader"))
                    {
                        target = "ps_5_0";
                    }
                    else
                    {
                        // Default to effect file
                        target = "fx_5_0";
                    }
                }

                IntPtr compiledCode = IntPtr.Zero;
                IntPtr errorMessages = IntPtr.Zero;

                try
                {
                    // Compile shader using D3DCompile
                    uint flags = 0; // D3DCOMPILE_ENABLE_STRICTNESS | D3DCOMPILE_OPTIMIZATION_LEVEL3
                    uint flags2 = 0;

                    int result = D3DCompile(
                        shaderSource,
                        (uint)shaderSource.Length,
                        null, // Source name
                        IntPtr.Zero, // Defines
                        IntPtr.Zero, // Include handler
                        entryPoint,
                        target,
                        flags,
                        flags2,
                        out compiledCode,
                        out errorMessages
                    );

                    if (result != 0 || compiledCode == IntPtr.Zero)
                    {
                        // Compilation failed - get error messages
                        if (errorMessages != IntPtr.Zero)
                        {
                            string errorText = Marshal.PtrToStringAnsi(errorMessages);
                            System.Diagnostics.Debug.WriteLine($"[ShaderCache] D3DCompile error: {errorText}");
                            D3DFree(errorMessages);
                        }
                        return null;
                    }

                    // Extract bytecode from compiled shader (ID3DBlob interface)
                    IntPtr bufferPtr = D3DGetBufferPointer(compiledCode);
                    uint bufferSize = D3DGetBufferSize(compiledCode);

                    if (bufferPtr == IntPtr.Zero || bufferSize == 0)
                    {
                        D3DFree(compiledCode);
                        return null;
                    }

                    // Copy bytecode to managed array
                    byte[] bytecode = new byte[bufferSize];
                    Marshal.Copy(bufferPtr, bytecode, 0, (int)bufferSize);

                    // Cleanup
                    D3DFree(compiledCode);
                    if (errorMessages != IntPtr.Zero)
                    {
                        D3DFree(errorMessages);
                    }

                    return bytecode;
                }
                finally
                {
                    if (compiledCode != IntPtr.Zero)
                    {
                        D3DFree(compiledCode);
                    }
                    if (errorMessages != IntPtr.Zero)
                    {
                        D3DFree(errorMessages);
                    }
                }
            }
            catch (DllNotFoundException)
            {
                // D3DCompiler not available
                return null;
            }
            catch
            {
                // Compilation failed
                return null;
            }
        }

        /// <summary>
        /// Compiles shader using MGFXC tool (MonoGame Effects Compiler).
        /// </summary>
        /// <param name="shaderSource">HLSL/FX shader source code.</param>
        /// <returns>Compiled bytecode, or null if compilation failed.</returns>
        /// <remarks>
        /// MGFXC Compilation (Cross-Platform):
        /// - Uses MonoGame's MGFXC tool for cross-platform shader compilation
        /// - Supports DirectX, OpenGL, and other platforms
        /// - Requires MGFXC to be available in PATH or as .NET tool
        /// - Fallback method when platform-specific compilation fails
        /// </remarks>
        private byte[] CompileShaderMGFXC(string shaderSource)
        {
            try
            {
                // Create temporary source file
                string tempSourceFile = Path.Combine(Path.GetTempPath(), $"shader_{Guid.NewGuid()}.fx");
                string tempOutputFile = Path.Combine(Path.GetTempPath(), $"shader_{Guid.NewGuid()}.mgfxo");

                try
                {
                    // Write source to temporary file
                    File.WriteAllText(tempSourceFile, shaderSource, Encoding.UTF8);

                    // Try to find MGFXC
                    string mgfxcPath = FindMGFXCPath();
                    if (string.IsNullOrEmpty(mgfxcPath))
                    {
                        return null;
                    }

                    // Run MGFXC to compile shader
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mgfxcPath,
                        Arguments = $"\"{tempSourceFile}\" \"{tempOutputFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
                    {
                        if (process == null)
                        {
                            return null;
                        }

                        process.WaitForExit(5000); // 5 second timeout

                        if (process.ExitCode != 0)
                        {
                            string error = process.StandardError.ReadToEnd();
                            System.Diagnostics.Debug.WriteLine($"[ShaderCache] MGFXC compilation error: {error}");
                            return null;
                        }

                        // Read compiled bytecode
                        if (File.Exists(tempOutputFile))
                        {
                            byte[] bytecode = File.ReadAllBytes(tempOutputFile);
                            return bytecode;
                        }
                    }
                }
                finally
                {
                    // Cleanup temporary files
                    try
                    {
                        if (File.Exists(tempSourceFile))
                        {
                            File.Delete(tempSourceFile);
                        }
                        if (File.Exists(tempOutputFile))
                        {
                            File.Delete(tempOutputFile);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch
            {
                // MGFXC compilation failed
            }

            return null;
        }

        /// <summary>
        /// Compiles shader for OpenGL (fallback for non-Windows platforms).
        /// </summary>
        /// <param name="shaderSource">Shader source code (HLSL/FX, will be converted to GLSL by MGFXC).</param>
        /// <returns>Compiled bytecode, or null if compilation failed.</returns>
        /// <remarks>
        /// OpenGL Shader Compilation:
        /// - Uses MonoGame's MGFXC tool to compile HLSL/FX shaders for OpenGL
        /// - MGFXC automatically handles HLSL to GLSL conversion
        /// - This is a fallback for non-Windows platforms when DirectX compilation isn't available
        /// - MGFXC compiles shaders to platform-specific bytecode (.mgfxo format)
        /// - Original game: OpenGL backend used fixed-function pipeline
        /// - Modern enhancement: Runtime shader compilation with cross-platform support
        /// </remarks>
        private byte[] CompileShaderOpenGL(string shaderSource)
        {
            if (string.IsNullOrEmpty(shaderSource))
            {
                return null;
            }

            try
            {
                // Use MGFXC to compile shader for OpenGL platform
                // MGFXC handles HLSL to GLSL conversion automatically
                // Create temporary source file
                string tempSourceFile = Path.Combine(Path.GetTempPath(), $"shader_gl_{Guid.NewGuid()}.fx");
                string tempOutputFile = Path.Combine(Path.GetTempPath(), $"shader_gl_{Guid.NewGuid()}.mgfxo");

                try
                {
                    // Write source to temporary file
                    File.WriteAllText(tempSourceFile, shaderSource, Encoding.UTF8);

                    // Find MGFXC tool
                    string mgfxcPath = FindMGFXCPath();
                    if (string.IsNullOrEmpty(mgfxcPath))
                    {
                        System.Diagnostics.Debug.WriteLine("[ShaderCache] CompileShaderOpenGL: MGFXC not found, cannot compile OpenGL shader");
                        return null;
                    }

                    // Run MGFXC to compile shader for OpenGL
                    // MGFXC automatically detects the target platform and converts HLSL to GLSL as needed
                    // For OpenGL, MGFXC compiles to .mgfxo format (MonoGame Effect format)
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mgfxcPath,
                        Arguments = $"\"{tempSourceFile}\" \"{tempOutputFile}\" /Profile:OpenGL",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
                    {
                        if (process == null)
                        {
                            System.Diagnostics.Debug.WriteLine("[ShaderCache] CompileShaderOpenGL: Failed to start MGFXC process");
                            return null;
                        }

                        process.WaitForExit(5000); // 5 second timeout

                        if (process.ExitCode != 0)
                        {
                            string error = process.StandardError.ReadToEnd();
                            string output = process.StandardOutput.ReadToEnd();
                            System.Diagnostics.Debug.WriteLine($"[ShaderCache] CompileShaderOpenGL: MGFXC compilation error (exit code {process.ExitCode}): {error}");
                            if (!string.IsNullOrEmpty(output))
                            {
                                System.Diagnostics.Debug.WriteLine($"[ShaderCache] CompileShaderOpenGL: MGFXC output: {output}");
                            }
                            return null;
                        }

                        // Read compiled bytecode
                        if (File.Exists(tempOutputFile))
                        {
                            byte[] bytecode = File.ReadAllBytes(tempOutputFile);
                            System.Diagnostics.Debug.WriteLine($"[ShaderCache] CompileShaderOpenGL: Successfully compiled shader ({bytecode.Length} bytes)");
                            return bytecode;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[ShaderCache] CompileShaderOpenGL: MGFXC succeeded but output file not found");
                            return null;
                        }
                    }
                }
                finally
                {
                    // Cleanup temporary files
                    try
                    {
                        if (File.Exists(tempSourceFile))
                        {
                            File.Delete(tempSourceFile);
                        }
                        if (File.Exists(tempOutputFile))
                        {
                            File.Delete(tempOutputFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log cleanup errors but don't fail compilation
                        System.Diagnostics.Debug.WriteLine($"[ShaderCache] CompileShaderOpenGL: Cleanup error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // MGFXC compilation failed
                System.Diagnostics.Debug.WriteLine($"[ShaderCache] CompileShaderOpenGL: Exception during compilation: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds the path to MGFXC tool.
        /// </summary>
        /// <returns>Path to MGFXC executable, or null if not found.</returns>
        private string FindMGFXCPath()
        {
            // Try .NET tool first
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dotnetToolsPath = Path.Combine(userProfile, ".dotnet", "tools", "mgfxc");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dotnetToolsPath += ".exe";
            }
            if (File.Exists(dotnetToolsPath))
            {
                return dotnetToolsPath;
            }

            // Try PATH
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] paths = pathEnv.Split(Path.PathSeparator);
                foreach (string path in paths)
                {
                    string mgfxcPath = Path.Combine(path, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mgfxc.exe" : "mgfxc");
                    if (File.Exists(mgfxcPath))
                    {
                        return mgfxcPath;
                    }
                }
            }

            return null;
        }

        #region DirectX Compilation P/Invoke

        // ID3DBlob interface methods (vtable offsets)
        // ID3DBlob is a COM interface with IUnknown methods + GetBufferPointer/GetBufferSize
        // VTable layout: QueryInterface(0), AddRef(1), Release(2), GetBufferPointer(3), GetBufferSize(4)

        [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "D3DCompile")]
        private static extern int D3DCompile(
            [MarshalAs(UnmanagedType.LPStr)] string pSrcData,
            uint srcDataSize,
            [MarshalAs(UnmanagedType.LPStr)] string pSourceName,
            IntPtr pDefines,
            IntPtr pInclude,
            [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
            [MarshalAs(UnmanagedType.LPStr)] string pTarget,
            uint flags1,
            uint flags2,
            out IntPtr ppCode,
            out IntPtr ppErrorMsgs
        );

        /// <summary>
        /// Gets the buffer pointer from an ID3DBlob interface.
        /// </summary>
        /// <param name="blob">ID3DBlob interface pointer.</param>
        /// <returns>Pointer to the compiled bytecode buffer.</returns>
        private static IntPtr D3DGetBufferPointer(IntPtr blob)
        {
            if (blob == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // ID3DBlob vtable: GetBufferPointer is at offset 3 (after IUnknown methods)
            IntPtr vtable = Marshal.ReadIntPtr(blob);
            IntPtr getBufferPointerFunc = Marshal.ReadIntPtr(vtable, IntPtr.Size * 3);

            // Call GetBufferPointer method
            GetBufferPointerDelegate del = Marshal.GetDelegateForFunctionPointer<GetBufferPointerDelegate>(getBufferPointerFunc);
            return del(blob);
        }

        /// <summary>
        /// Gets the buffer size from an ID3DBlob interface.
        /// </summary>
        /// <param name="blob">ID3DBlob interface pointer.</param>
        /// <returns>Size of the compiled bytecode buffer in bytes.</returns>
        private static uint D3DGetBufferSize(IntPtr blob)
        {
            if (blob == IntPtr.Zero)
            {
                return 0;
            }

            // ID3DBlob vtable: GetBufferSize is at offset 4 (after IUnknown methods and GetBufferPointer)
            IntPtr vtable = Marshal.ReadIntPtr(blob);
            IntPtr getBufferSizeFunc = Marshal.ReadIntPtr(vtable, IntPtr.Size * 4);

            // Call GetBufferSize method
            GetBufferSizeDelegate del = Marshal.GetDelegateForFunctionPointer<GetBufferSizeDelegate>(getBufferSizeFunc);
            return del(blob);
        }

        /// <summary>
        /// Releases an ID3DBlob interface (calls Release method).
        /// </summary>
        /// <param name="blob">ID3DBlob interface pointer to release.</param>
        private static void D3DFree(IntPtr blob)
        {
            if (blob == IntPtr.Zero)
            {
                return;
            }

            // ID3DBlob vtable: Release is at offset 2 (IUnknown::Release)
            IntPtr vtable = Marshal.ReadIntPtr(blob);
            IntPtr releaseFunc = Marshal.ReadIntPtr(vtable, IntPtr.Size * 2);

            // Call Release method
            ReleaseDelegate del = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(releaseFunc);
            del(blob);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferPointerDelegate(IntPtr blob);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetBufferSizeDelegate(IntPtr blob);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr blob);

        #endregion
    }
}

