using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;

namespace Andastra.Game.Graphics.MonoGame.Materials
{
    /// <summary>
    /// Physically-based rendering material implementation.
    /// Converts KOTOR's legacy materials to modern PBR workflow.
    /// </summary>
    public class PbrMaterial : IPbrMaterial
    {
        private bool _disposed;
        private TextureChannels _activeChannels;

        public string Name { get; private set; }
        public MaterialType Type { get; set; }

        public TextureChannels ActiveChannels
        {
            get { return _activeChannels; }
        }

        #region Base Color

        public IntPtr AlbedoTexture { get; set; }
        public Vector4 AlbedoColor { get; set; } = Vector4.One;
        public Vector2 AlbedoUvScale { get; set; } = Vector2.One;
        public Vector2 AlbedoUvOffset { get; set; } = Vector2.Zero;

        #endregion

        #region Normal Mapping

        public IntPtr NormalTexture { get; set; }
        public float NormalIntensity { get; set; } = 1.0f;
        public bool NormalFlipY { get; set; } = false;

        #endregion

        #region PBR Properties

        public IntPtr MetallicTexture { get; set; }
        public float Metallic { get; set; } = 0.0f;

        public IntPtr RoughnessTexture { get; set; }
        public float Roughness { get; set; } = 0.5f;

        public IntPtr OrmTexture { get; set; }

        public IntPtr AoTexture { get; set; }
        public float AoIntensity { get; set; } = 1.0f;

        #endregion

        #region Emission

        public IntPtr EmissiveTexture { get; set; }
        public Vector3 EmissiveColor { get; set; } = Vector3.Zero;
        public float EmissiveIntensity { get; set; } = 1.0f;

        #endregion

        #region Displacement

        public IntPtr HeightTexture { get; set; }
        public float HeightIntensity { get; set; } = 0.05f;
        public int HeightLayers { get; set; } = 8;

        #endregion

        #region Lightmap

        public IntPtr LightmapTexture { get; set; }
        public int LightmapUvChannel { get; set; } = 1;
        public float LightmapIntensity { get; set; } = 1.0f;

        #endregion

        #region Environment

        public IntPtr EnvironmentTexture { get; set; }
        public float EnvironmentIntensity { get; set; } = 1.0f;

        #endregion

        #region Transparency

        public float AlphaCutoff { get; set; } = 0.5f;
        public float Opacity { get; set; } = 1.0f;
        public float IndexOfRefraction { get; set; } = 1.5f;
        public bool RefractionEnabled { get; set; } = false;

        #endregion

        #region Subsurface Scattering

        public Vector3 SubsurfaceColor { get; set; } = new Vector3(1.0f, 0.2f, 0.1f);
        public float SubsurfaceRadius { get; set; } = 1.0f;
        public IntPtr SubsurfaceTexture { get; set; }

        #endregion

        #region Clear Coat

        public float ClearCoat { get; set; } = 0.0f;
        public float ClearCoatRoughness { get; set; } = 0.1f;
        public IntPtr ClearCoatNormalTexture { get; set; }

        #endregion

        #region Anisotropy

        public float Anisotropy { get; set; } = 0.0f;
        public float AnisotropyRotation { get; set; } = 0.0f;
        public IntPtr AnisotropyTexture { get; set; }

        #endregion

        #region Detail Textures

        public IntPtr DetailAlbedoTexture { get; set; }
        public IntPtr DetailNormalTexture { get; set; }
        public Vector2 DetailUvScale { get; set; } = new Vector2(10, 10);
        public IntPtr DetailMaskTexture { get; set; }

        #endregion

        #region Rendering State

        public bool DoubleSided { get; set; } = false;
        public int RenderQueue { get; set; } = 2000;
        public bool CastShadows { get; set; } = true;
        public bool ReceiveShadows { get; set; } = true;

        #endregion

        public PbrMaterial(string name, MaterialType type = MaterialType.Opaque)
        {
            Name = name;
            Type = type;
        }

        public void Bind()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PbrMaterial));
            }

            // Bind textures to shader slots
            // Set material uniforms
        }

        public void UpdateParameters()
        {
            if (_disposed)
            {
                return;
            }

            // Update GPU constant buffer with current parameters
            UpdateActiveChannels();
        }

        public IPbrMaterial Clone()
        {
            var clone = new PbrMaterial(Name + "_clone", Type)
            {
                AlbedoTexture = AlbedoTexture,
                AlbedoColor = AlbedoColor,
                AlbedoUvScale = AlbedoUvScale,
                AlbedoUvOffset = AlbedoUvOffset,
                NormalTexture = NormalTexture,
                NormalIntensity = NormalIntensity,
                NormalFlipY = NormalFlipY,
                MetallicTexture = MetallicTexture,
                Metallic = Metallic,
                RoughnessTexture = RoughnessTexture,
                Roughness = Roughness,
                OrmTexture = OrmTexture,
                AoTexture = AoTexture,
                AoIntensity = AoIntensity,
                EmissiveTexture = EmissiveTexture,
                EmissiveColor = EmissiveColor,
                EmissiveIntensity = EmissiveIntensity,
                HeightTexture = HeightTexture,
                HeightIntensity = HeightIntensity,
                HeightLayers = HeightLayers,
                LightmapTexture = LightmapTexture,
                LightmapUvChannel = LightmapUvChannel,
                LightmapIntensity = LightmapIntensity,
                EnvironmentTexture = EnvironmentTexture,
                EnvironmentIntensity = EnvironmentIntensity,
                AlphaCutoff = AlphaCutoff,
                Opacity = Opacity,
                IndexOfRefraction = IndexOfRefraction,
                RefractionEnabled = RefractionEnabled,
                SubsurfaceColor = SubsurfaceColor,
                SubsurfaceRadius = SubsurfaceRadius,
                SubsurfaceTexture = SubsurfaceTexture,
                ClearCoat = ClearCoat,
                ClearCoatRoughness = ClearCoatRoughness,
                ClearCoatNormalTexture = ClearCoatNormalTexture,
                Anisotropy = Anisotropy,
                AnisotropyRotation = AnisotropyRotation,
                AnisotropyTexture = AnisotropyTexture,
                DetailAlbedoTexture = DetailAlbedoTexture,
                DetailNormalTexture = DetailNormalTexture,
                DetailUvScale = DetailUvScale,
                DetailMaskTexture = DetailMaskTexture,
                DoubleSided = DoubleSided,
                RenderQueue = RenderQueue,
                CastShadows = CastShadows,
                ReceiveShadows = ReceiveShadows
            };
            clone.UpdateActiveChannels();
            return clone;
        }

        private void UpdateActiveChannels()
        {
            _activeChannels = TextureChannels.None;

            if (AlbedoTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Albedo;
            if (NormalTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Normal;
            if (MetallicTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Metallic;
            if (RoughnessTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Roughness;
            if (AoTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.AmbientOcclusion;
            if (EmissiveTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Emissive;
            if (HeightTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Height;
            if (LightmapTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Lightmap;
            if (EnvironmentTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Environment;
            if (OrmTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.ORM;
            if (SubsurfaceTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.Subsurface;
            if (DetailAlbedoTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.DetailAlbedo;
            if (DetailNormalTexture != IntPtr.Zero)
                _activeChannels |= TextureChannels.DetailNormal;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Release GPU resources
            // Textures are managed by the texture cache, not destroyed here

            _disposed = true;
        }
    }
}

