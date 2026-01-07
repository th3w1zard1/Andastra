using System;
using System.Numerics;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Effects;
using Microsoft.Xna.Framework.Graphics;
using MonoGameTexture2D = Andastra.Runtime.Graphics.MonoGame.Graphics.MonoGameTexture2D;

namespace Andastra.Runtime.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IBasicEffect.
    /// Inherits from BaseBasicEffect to share common implementation logic.
    /// </summary>
    public class MonoGameBasicEffect : BaseBasicEffect
    {
        private readonly BasicEffect _effect;

        public MonoGameBasicEffect(GraphicsDevice device) : base()
        {
            _effect = new BasicEffect(device);
        }

        public override Matrix4x4 World
        {
            get { return base.World; }
            set
            {
                base.World = value;
                _effect.World = ConvertMatrix(value);
            }
        }

        public override Matrix4x4 View
        {
            get { return base.View; }
            set
            {
                base.View = value;
                _effect.View = ConvertMatrix(value);
            }
        }

        public override Matrix4x4 Projection
        {
            get { return base.Projection; }
            set
            {
                base.Projection = value;
                _effect.Projection = ConvertMatrix(value);
            }
        }

        public override bool VertexColorEnabled
        {
            get { return base.VertexColorEnabled; }
            set
            {
                base.VertexColorEnabled = value;
                _effect.VertexColorEnabled = value;
            }
        }

        public override bool LightingEnabled
        {
            get { return base.LightingEnabled; }
            set
            {
                base.LightingEnabled = value;
                _effect.LightingEnabled = value;
            }
        }

        public override bool TextureEnabled
        {
            get { return base.TextureEnabled; }
            set
            {
                base.TextureEnabled = value;
                _effect.TextureEnabled = value;
            }
        }

        public override Vector3 AmbientLightColor
        {
            get { return base.AmbientLightColor; }
            set
            {
                base.AmbientLightColor = value;
                _effect.AmbientLightColor = ConvertVector3(value);
            }
        }

        public override Vector3 DiffuseColor
        {
            get { return base.DiffuseColor; }
            set
            {
                base.DiffuseColor = value;
                _effect.DiffuseColor = ConvertVector3(value);
            }
        }

        public override Vector3 EmissiveColor
        {
            get { return base.EmissiveColor; }
            set
            {
                base.EmissiveColor = value;
                _effect.EmissiveColor = ConvertVector3(value);
            }
        }

        public override Vector3 SpecularColor
        {
            get { return base.SpecularColor; }
            set
            {
                base.SpecularColor = value;
                _effect.SpecularColor = ConvertVector3(value);
            }
        }

        public override float SpecularPower
        {
            get { return base.SpecularPower; }
            set
            {
                base.SpecularPower = value;
                _effect.SpecularPower = value;
            }
        }

        public override float Alpha
        {
            get { return base.Alpha; }
            set
            {
                base.Alpha = value;
                _effect.Alpha = value;
            }
        }

        public override ITexture2D Texture
        {
            get { return base.Texture; }
            set
            {
                base.Texture = value;
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

        public override bool FogEnabled
        {
            get { return base.FogEnabled; }
            set
            {
                base.FogEnabled = value;
                _effect.FogEnabled = value;
            }
        }

        public override Vector3 FogColor
        {
            get { return base.FogColor; }
            set
            {
                base.FogColor = value;
                _effect.FogColor = ConvertVector3(value);
            }
        }

        public override float FogStart
        {
            get { return base.FogStart; }
            set
            {
                base.FogStart = value;
                _effect.FogStart = value;
            }
        }

        public override float FogEnd
        {
            get { return base.FogEnd; }
            set
            {
                base.FogEnd = value;
                _effect.FogEnd = value;
            }
        }

        protected override IEffectTechnique GetCurrentTechniqueInternal()
        {
            return new MonoGameEffectTechnique(_effect.CurrentTechnique);
        }

        protected override IEffectTechnique[] GetTechniquesInternal()
        {
            var techniques = new IEffectTechnique[_effect.Techniques.Count];
            for (int i = 0; i < _effect.Techniques.Count; i++)
            {
                techniques[i] = new MonoGameEffectTechnique(_effect.Techniques[i]);
            }
            return techniques;
        }

        protected override void OnDispose()
        {
            _effect?.Dispose();
            base.OnDispose();
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

