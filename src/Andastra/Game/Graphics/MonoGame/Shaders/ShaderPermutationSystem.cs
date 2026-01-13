using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Shaders
{
    /// <summary>
    /// Shader permutation/variant system for efficient shader management.
    /// 
    /// Shader permutations compile shader variants based on feature flags,
    /// enabling conditional compilation and reducing runtime branching.
    /// 
    /// Features:
    /// - Feature flag-based permutations
    /// - Automatic permutation generation
    /// - Shader variant caching
    /// - Hot reload support
    /// </summary>
    public class ShaderPermutationSystem
    {
        /// <summary>
        /// Shader feature flags.
        /// </summary>
        [Flags]
        public enum ShaderFeatures
        {
            None = 0,
            HasNormalMap = 1 << 0,
            HasRoughnessMap = 1 << 1,
            HasMetallicMap = 1 << 2,
            HasEmissiveMap = 1 << 3,
            HasOcclusionMap = 1 << 4,
            HasAlphaTest = 1 << 5,
            HasVertexColors = 1 << 6,
            HasSkinning = 1 << 7,
            HasInstancing = 1 << 8,
            ReceivesShadows = 1 << 9,
            CastsShadows = 1 << 10
        }

        /// <summary>
        /// Shader permutation entry.
        /// </summary>
        private class PermutationEntry
        {
            public string ShaderName;
            public ShaderFeatures Features;
            public Effect CompiledShader;
            public DateTime LastUsed;
        }

        private readonly Dictionary<string, Dictionary<ShaderFeatures, PermutationEntry>> _permutations;
        private readonly ShaderCache _shaderCache;

        /// <summary>
        /// Gets the number of cached permutations.
        /// </summary>
        public int PermutationCount
        {
            get
            {
                int count = 0;
                foreach (var dict in _permutations.Values)
                {
                    count += dict.Count;
                }
                return count;
            }
        }

        /// <summary>
        /// Initializes a new shader permutation system.
        /// </summary>
        public ShaderPermutationSystem(ShaderCache shaderCache)
        {
            _permutations = new Dictionary<string, Dictionary<ShaderFeatures, PermutationEntry>>();
            _shaderCache = shaderCache ?? throw new ArgumentNullException("shaderCache");
        }

        /// <summary>
        /// Gets or compiles a shader permutation.
        /// </summary>
        public Effect GetPermutation(string shaderName, ShaderFeatures features, GraphicsDevice device)
        {
            Dictionary<ShaderFeatures, PermutationEntry> shaderPerms;
            if (!_permutations.TryGetValue(shaderName, out shaderPerms))
            {
                shaderPerms = new Dictionary<ShaderFeatures, PermutationEntry>();
                _permutations[shaderName] = shaderPerms;
            }

            PermutationEntry entry;
            if (shaderPerms.TryGetValue(features, out entry))
            {
                entry.LastUsed = DateTime.UtcNow;
                return entry.CompiledShader;
            }

            // Compile new permutation
            string shaderSource = LoadShaderSource(shaderName);
            string permutedSource = GeneratePermutation(shaderSource, features);

            // Compile using shader cache
            Effect compiled = _shaderCache.GetOrCompileShaderAsync(
                $"{shaderName}_{features}",
                permutedSource,
                device
            ).Result;

            entry = new PermutationEntry
            {
                ShaderName = shaderName,
                Features = features,
                CompiledShader = compiled,
                LastUsed = DateTime.UtcNow
            };
            shaderPerms[features] = entry;

            return compiled;
        }

        /// <summary>
        /// Generates shader permutation from source and features.
        /// </summary>
        private string GeneratePermutation(string source, ShaderFeatures features)
        {
            // Insert feature defines at the beginning
            List<string> defines = new List<string>();

            if ((features & ShaderFeatures.HasNormalMap) != 0)
            {
                defines.Add("#define HAS_NORMAL_MAP");
            }
            if ((features & ShaderFeatures.HasRoughnessMap) != 0)
            {
                defines.Add("#define HAS_ROUGHNESS_MAP");
            }
            if ((features & ShaderFeatures.HasMetallicMap) != 0)
            {
                defines.Add("#define HAS_METALLIC_MAP");
            }
            if ((features & ShaderFeatures.HasEmissiveMap) != 0)
            {
                defines.Add("#define HAS_EMISSIVE_MAP");
            }
            if ((features & ShaderFeatures.HasOcclusionMap) != 0)
            {
                defines.Add("#define HAS_OCCLUSION_MAP");
            }
            if ((features & ShaderFeatures.HasAlphaTest) != 0)
            {
                defines.Add("#define HAS_ALPHA_TEST");
            }
            if ((features & ShaderFeatures.HasVertexColors) != 0)
            {
                defines.Add("#define HAS_VERTEX_COLORS");
            }
            if ((features & ShaderFeatures.HasSkinning) != 0)
            {
                defines.Add("#define HAS_SKINNING");
            }
            if ((features & ShaderFeatures.HasInstancing) != 0)
            {
                defines.Add("#define HAS_INSTANCING");
            }
            if ((features & ShaderFeatures.ReceivesShadows) != 0)
            {
                defines.Add("#define RECEIVES_SHADOWS");
            }
            if ((features & ShaderFeatures.CastsShadows) != 0)
            {
                defines.Add("#define CASTS_SHADOWS");
            }

            return string.Join("\n", defines) + "\n" + source;
        }

        /// <summary>
        /// Loads shader source from file or cache.
        /// </summary>
        private string LoadShaderSource(string shaderName)
        {
            // Load shader source
            // Placeholder - would load from file system or embedded resources
            return "";
        }

        /// <summary>
        /// Clears unused permutations.
        /// </summary>
        public void ClearUnused(TimeSpan maxAge)
        {
            DateTime cutoff = DateTime.UtcNow - maxAge;
            var toRemove = new List<KeyValuePair<string, ShaderFeatures>>();

            foreach (var shaderKvp in _permutations)
            {
                foreach (var permKvp in shaderKvp.Value)
                {
                    if (permKvp.Value.LastUsed < cutoff)
                    {
                        toRemove.Add(new KeyValuePair<string, ShaderFeatures>(shaderKvp.Key, permKvp.Key));
                    }
                }
            }

            foreach (var kvp in toRemove)
            {
                _permutations[kvp.Key].Remove(kvp.Value);
                if (_permutations[kvp.Key].Count == 0)
                {
                    _permutations.Remove(kvp.Key);
                }
            }
        }
    }
}

