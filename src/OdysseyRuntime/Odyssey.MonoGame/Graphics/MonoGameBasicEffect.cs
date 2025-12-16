using System;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using Odyssey.Graphics;

namespace Odyssey.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IBasicEffect.
    /// </summary>
    public class MonoGameBasicEffect : IBasicEffect
    {
        private readonly BasicEffect _effect;

        public MonoGameBasicEffect(GraphicsDevice device)
        {
            _effect = new BasicEffect(device);
        }

        public Matrix4x4 World
        {
            get { return ConvertMatrix(_effect.World); }
            set { _effect.World = ConvertMatrix(value); }
        }

        public Matrix4x4 View
        {
            get { return ConvertMatrix(_effect.View); }
            set { _effect.View = ConvertMatrix(value); }
        }

        public Matrix4x4 Projection
        {
            get { return ConvertMatrix(_effect.Projection); }
            set { _effect.Projection = ConvertMatrix(value); }
        }

        public bool VertexColorEnabled
        {
            get { return _effect.VertexColorEnabled; }
            set { _effect.VertexColorEnabled = value; }
        }

        public bool LightingEnabled
        {
            get { return _effect.LightingEnabled; }
            set { _effect.LightingEnabled = value; }
        }

        public bool TextureEnabled
        {
            get { return _effect.TextureEnabled; }
            set { _effect.TextureEnabled = value; }
        }

        public Vector3 AmbientLightColor
        {
            get { return ConvertVector3(_effect.AmbientLightColor); }
            set { _effect.AmbientLightColor = ConvertVector3(value); }
        }

        public Vector3 DiffuseColor
        {
            get { return ConvertVector3(_effect.DiffuseColor); }
            set { _effect.DiffuseColor = ConvertVector3(value); }
        }

        public Vector3 EmissiveColor
        {
            get { return ConvertVector3(_effect.EmissiveColor); }
            set { _effect.EmissiveColor = ConvertVector3(value); }
        }

        public Vector3 SpecularColor
        {
            get { return ConvertVector3(_effect.SpecularColor); }
            set { _effect.SpecularColor = ConvertVector3(value); }
        }

        public float SpecularPower
        {
            get { return _effect.SpecularPower; }
            set { _effect.SpecularPower = value; }
        }

        public float Alpha
        {
            get { return _effect.Alpha; }
            set { _effect.Alpha = value; }
        }

        public ITexture2D Texture
        {
            get
            {
                if (_effect.Texture != null)
                {
                    return new MonoGameTexture2D(_effect.Texture);
                }
                return null;
            }
            set
            {
                if (value == null)
                {
                    _effect.Texture = null;
                }
                else if (value is MonoGameTexture2D mgTex)
                {
                    _effect.Texture = mgTex.Texture;
                }
                else
                {
                    throw new ArgumentException("Texture must be a MonoGameTexture2D", nameof(value));
                }
            }
        }

        public IEffectTechnique CurrentTechnique
        {
            get { return new MonoGameEffectTechnique(_effect.CurrentTechnique); }
        }

        public IEffectTechnique[] Techniques
        {
            get
            {
                var techniques = new IEffectTechnique[_effect.Techniques.Count];
                for (int i = 0; i < _effect.Techniques.Count; i++)
                {
                    techniques[i] = new MonoGameEffectTechnique(_effect.Techniques[i]);
                }
                return techniques;
            }
        }

        public void Dispose()
        {
            _effect?.Dispose();
        }

        private static Microsoft.Xna.Framework.Matrix ConvertMatrix(Matrix4x4 matrix)
        {
            return new Microsoft.Xna.Framework.Matrix(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        }

        private static Matrix4x4 ConvertMatrix(Microsoft.Xna.Framework.Matrix matrix)
        {
            return new Matrix4x4(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        }

        private static Microsoft.Xna.Framework.Vector3 ConvertVector3(Vector3 vector)
        {
            return new Microsoft.Xna.Framework.Vector3(vector.X, vector.Y, vector.Z);
        }

        private static Vector3 ConvertVector3(Microsoft.Xna.Framework.Vector3 vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }
    }

    /// <summary>
    /// MonoGame implementation of IEffectTechnique.
    /// </summary>
    public class MonoGameEffectTechnique : IEffectTechnique
    {
        private readonly EffectTechnique _technique;

        public MonoGameEffectTechnique(EffectTechnique technique)
        {
            _technique = technique ?? throw new ArgumentNullException(nameof(technique));
        }

        public string Name => _technique.Name;

        public IEffectPass[] Passes
        {
            get
            {
                var passes = new IEffectPass[_technique.Passes.Count];
                for (int i = 0; i < _technique.Passes.Count; i++)
                {
                    passes[i] = new MonoGameEffectPass(_technique.Passes[i]);
                }
                return passes;
            }
        }
    }

    /// <summary>
    /// MonoGame implementation of IEffectPass.
    /// </summary>
    public class MonoGameEffectPass : IEffectPass
    {
        private readonly EffectPass _pass;

        public MonoGameEffectPass(EffectPass pass)
        {
            _pass = pass ?? throw new ArgumentNullException(nameof(pass));
        }

        public string Name => _pass.Name;

        public void Apply()
        {
            _pass.Apply();
        }
    }
}

