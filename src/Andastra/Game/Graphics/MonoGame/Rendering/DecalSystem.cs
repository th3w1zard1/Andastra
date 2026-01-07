using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Decal system for projecting textures onto surfaces.
    /// 
    /// Decals are used for bullet holes, blood splatters, damage marks,
    /// and other surface details that need to be projected onto geometry.
    /// 
    /// Features:
    /// - Projected decals
    /// - Depth testing
    /// - Fade-out over time
    /// - Automatic cleanup
    /// - Batching for efficiency
    /// </summary>
    public class DecalSystem
    {
        /// <summary>
        /// Decal instance.
        /// </summary>
        public struct Decal
        {
            /// <summary>
            /// Decal position.
            /// </summary>
            public Vector3 Position;

            /// <summary>
            /// Decal normal/direction.
            /// </summary>
            public Vector3 Normal;

            /// <summary>
            /// Decal size.
            /// </summary>
            public Vector2 Size;

            /// <summary>
            /// Decal texture.
            /// </summary>
            public Texture2D Texture;

            /// <summary>
            /// Decal color tint.
            /// </summary>
            public Vector4 Color;

            /// <summary>
            /// Lifetime in seconds.
            /// </summary>
            public float Lifetime;

            /// <summary>
            /// Age in seconds.
            /// </summary>
            public float Age;

            /// <summary>
            /// Decal ID.
            /// </summary>
            public uint DecalId;
        }

        private readonly List<Decal> _decals;
        private readonly Dictionary<uint, int> _decalIndices;
        private uint _nextDecalId;

        /// <summary>
        /// Gets the number of active decals.
        /// </summary>
        public int DecalCount
        {
            get { return _decals.Count; }
        }

        /// <summary>
        /// Initializes a new decal system.
        /// </summary>
        public DecalSystem()
        {
            _decals = new List<Decal>();
            _decalIndices = new Dictionary<uint, int>();
            _nextDecalId = 1;
        }

        /// <summary>
        /// Adds a decal to the system.
        /// </summary>
        public uint AddDecal(Vector3 position, Vector3 normal, Vector2 size, Texture2D texture, Vector4 color, float lifetime)
        {
            Decal decal = new Decal
            {
                Position = position,
                Normal = normal,
                Size = size,
                Texture = texture,
                Color = color,
                Lifetime = lifetime,
                Age = 0.0f,
                DecalId = _nextDecalId++
            };

            _decals.Add(decal);
            _decalIndices[decal.DecalId] = _decals.Count - 1;

            return decal.DecalId;
        }

        /// <summary>
        /// Removes a decal.
        /// </summary>
        public void RemoveDecal(uint decalId)
        {
            int index;
            if (_decalIndices.TryGetValue(decalId, out index))
            {
                _decals.RemoveAt(index);
                _decalIndices.Remove(decalId);

                // Update indices
                for (int i = index; i < _decals.Count; i++)
                {
                    _decalIndices[_decals[i].DecalId] = i;
                }
            }
        }

        /// <summary>
        /// Updates decals (ages them and removes expired ones).
        /// </summary>
        public void Update(float deltaTime)
        {
            for (int i = _decals.Count - 1; i >= 0; i--)
            {
                Decal decal = _decals[i];
                decal.Age += deltaTime;

                if (decal.Age >= decal.Lifetime)
                {
                    // Remove expired decal
                    _decalIndices.Remove(decal.DecalId);
                    _decals.RemoveAt(i);
                }
                else
                {
                    // Update fade based on age
                    float fade = 1.0f - (decal.Age / decal.Lifetime);
                    decal.Color.W = fade;
                    _decals[i] = decal;
                }
            }
        }

        /// <summary>
        /// Renders all decals.
        /// </summary>
        public void Render(GraphicsDevice device, Matrix viewMatrix, Matrix projectionMatrix)
        {
            // Render decals using projected quad geometry
            // Would use decal shader that projects onto depth buffer
            // Placeholder - requires actual rendering implementation
        }

        /// <summary>
        /// Clears all decals.
        /// </summary>
        public void Clear()
        {
            _decals.Clear();
            _decalIndices.Clear();
        }
    }
}

