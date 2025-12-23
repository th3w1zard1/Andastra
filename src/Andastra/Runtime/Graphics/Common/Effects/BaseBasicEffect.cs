using System;
using System.Numerics;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Graphics.Common.Effects
{
    /// <summary>
    /// Abstract base class for BasicEffect implementations.
    ///
    /// Provides shared implementation logic for basic 3D rendering effects
    /// that can be inherited by both MonoGame and Stride implementations.
    ///
    /// Based on MonoGame BasicEffect API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.BasicEffect.html
    /// </summary>
    /// <remarks>
    /// Basic Effect:
    /// - This is an abstraction layer for modern graphics effect systems
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game effects: VisualEffect_01 @ 0x007c0210, VisualEffect_02 @ 0x007c01f0, VisualEffect_03 @ 0x007c01d0, VisualEffect_04 @ 0x007c01b0
    /// - Effect types: DEffectType @ 0x007c016b
    /// - Original game used fixed-function pipeline and programmable shaders (vertex/pixel shaders)
    /// - This abstraction: Provides modern effect system for different graphics backends, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public abstract class BaseBasicEffect : IBasicEffect
    {
        protected Matrix4x4 _world;
        protected Matrix4x4 _view;
        protected Matrix4x4 _projection;
        protected bool _vertexColorEnabled;
        protected bool _lightingEnabled;
        protected bool _textureEnabled;
        protected Vector3 _ambientLightColor;
        protected Vector3 _diffuseColor;
        protected Vector3 _emissiveColor;
        protected Vector3 _specularColor;
        protected float _specularPower;
        protected float _alpha;
        protected ITexture2D _texture;
        protected bool _fogEnabled;
        protected Vector3 _fogColor;
        protected float _fogStart;
        protected float _fogEnd;
        protected IEffectTechnique _currentTechnique;
        protected IEffectTechnique[] _techniques;

        public virtual bool FogEnabled
        {
            get { return _fogEnabled; }
            set { _fogEnabled = value; }
        }

        public virtual Vector3 FogColor
        {
            get { return _fogColor; }
            set { _fogColor = value; }
        }

        public virtual float FogStart
        {
            get { return _fogStart; }
            set { _fogStart = value; }
        }

        public virtual float FogEnd
        {
            get { return _fogEnd; }
            set { _fogEnd = value; }
        }

        protected BaseBasicEffect()
        {
            _world = Matrix4x4.Identity;
            _view = Matrix4x4.Identity;
            _projection = Matrix4x4.Identity;
            _vertexColorEnabled = false;
            _lightingEnabled = false;
            _textureEnabled = false;
            _ambientLightColor = new Vector3(0.2f, 0.2f, 0.2f);
            _diffuseColor = new Vector3(1.0f, 1.0f, 1.0f);
            _emissiveColor = Vector3.Zero;
            _specularColor = Vector3.Zero;
            _specularPower = 0.0f;
            _alpha = 1.0f;
            _texture = null;
            _fogEnabled = false;
            _fogColor = Vector3.Zero;
            _fogStart = 0.0f;
            _fogEnd = 1000.0f;
        }

        #region IBasicEffect Implementation

        public virtual Matrix4x4 World
        {
            get { return _world; }
            set
            {
                if (_world != value)
                {
                    _world = value;
                    OnWorldChanged(value);
                }
            }
        }

        public virtual Matrix4x4 View
        {
            get { return _view; }
            set
            {
                if (_view != value)
                {
                    _view = value;
                    OnViewChanged(value);
                }
            }
        }

        public virtual Matrix4x4 Projection
        {
            get { return _projection; }
            set
            {
                if (_projection != value)
                {
                    _projection = value;
                    OnProjectionChanged(value);
                }
            }
        }

        public virtual bool VertexColorEnabled
        {
            get { return _vertexColorEnabled; }
            set
            {
                if (_vertexColorEnabled != value)
                {
                    _vertexColorEnabled = value;
                    OnVertexColorEnabledChanged(value);
                }
            }
        }

        public virtual bool LightingEnabled
        {
            get { return _lightingEnabled; }
            set
            {
                if (_lightingEnabled != value)
                {
                    _lightingEnabled = value;
                    OnLightingEnabledChanged(value);
                }
            }
        }

        public virtual bool TextureEnabled
        {
            get { return _textureEnabled; }
            set
            {
                if (_textureEnabled != value)
                {
                    _textureEnabled = value;
                    OnTextureEnabledChanged(value);
                }
            }
        }

        public virtual Vector3 AmbientLightColor
        {
            get { return _ambientLightColor; }
            set
            {
                if (_ambientLightColor != value)
                {
                    _ambientLightColor = value;
                    OnAmbientLightColorChanged(value);
                }
            }
        }

        public virtual Vector3 DiffuseColor
        {
            get { return _diffuseColor; }
            set
            {
                if (_diffuseColor != value)
                {
                    _diffuseColor = value;
                    OnDiffuseColorChanged(value);
                }
            }
        }

        public virtual Vector3 EmissiveColor
        {
            get { return _emissiveColor; }
            set
            {
                if (_emissiveColor != value)
                {
                    _emissiveColor = value;
                    OnEmissiveColorChanged(value);
                }
            }
        }

        public virtual Vector3 SpecularColor
        {
            get { return _specularColor; }
            set
            {
                if (_specularColor != value)
                {
                    _specularColor = value;
                    OnSpecularColorChanged(value);
                }
            }
        }

        public virtual float SpecularPower
        {
            get { return _specularPower; }
            set
            {
                if (Math.Abs(_specularPower - value) > float.Epsilon)
                {
                    _specularPower = value;
                    OnSpecularPowerChanged(value);
                }
            }
        }

        public virtual float Alpha
        {
            get { return _alpha; }
            set
            {
                if (Math.Abs(_alpha - value) > float.Epsilon)
                {
                    _alpha = Math.Max(0.0f, Math.Min(1.0f, value));
                    OnAlphaChanged(_alpha);
                }
            }
        }

        public virtual ITexture2D Texture
        {
            get { return _texture; }
            set
            {
                if (_texture != value)
                {
                    _texture = value;
                    OnTextureChanged(value);
                }
            }
        }

        public virtual IEffectTechnique CurrentTechnique => _currentTechnique ?? (_currentTechnique = GetCurrentTechniqueInternal());

        public virtual IEffectTechnique[] Techniques => _techniques ?? (_techniques = GetTechniquesInternal());

        /// <summary>
        /// Applies the effect to the graphics device.
        /// This applies all passes of the current technique.
        /// </summary>
        public virtual void Apply()
        {
            IEffectTechnique technique = CurrentTechnique;
            if (technique != null && technique.Passes != null)
            {
                foreach (IEffectPass pass in technique.Passes)
                {
                    pass.Apply();
                }
            }
        }

        public virtual void Dispose()
        {
            _texture = null;
            _currentTechnique = null;
            _techniques = null;
            OnDispose();
        }

        #endregion

        #region Abstract Methods (Override in Derived Classes)

        /// <summary>
        /// Gets the current technique. Called when CurrentTechnique is first accessed.
        /// </summary>
        protected abstract IEffectTechnique GetCurrentTechniqueInternal();

        /// <summary>
        /// Gets all techniques. Called when Techniques is first accessed.
        /// </summary>
        protected abstract IEffectTechnique[] GetTechniquesInternal();

        #endregion

        #region Virtual Hooks (Optional Overrides)

        protected virtual void OnWorldChanged(Matrix4x4 world) { }
        protected virtual void OnViewChanged(Matrix4x4 view) { }
        protected virtual void OnProjectionChanged(Matrix4x4 projection) { }
        protected virtual void OnVertexColorEnabledChanged(bool enabled) { }
        protected virtual void OnLightingEnabledChanged(bool enabled) { }
        protected virtual void OnTextureEnabledChanged(bool enabled) { }
        protected virtual void OnAmbientLightColorChanged(Vector3 color) { }
        protected virtual void OnDiffuseColorChanged(Vector3 color) { }
        protected virtual void OnEmissiveColorChanged(Vector3 color) { }
        protected virtual void OnSpecularColorChanged(Vector3 color) { }
        protected virtual void OnSpecularPowerChanged(float power) { }
        protected virtual void OnAlphaChanged(float alpha) { }
        protected virtual void OnTextureChanged(ITexture2D texture) { }
        protected virtual void OnDispose() { }

        #endregion
    }
}

