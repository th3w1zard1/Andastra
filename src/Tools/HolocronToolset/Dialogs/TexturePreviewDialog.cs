using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using BioWare.NET.Resource.Formats.TPC;
using JetBrains.Annotations;
using Avalonia;

namespace HolocronToolset.Dialogs
{
    /// <summary>
    /// Dialog for previewing texture images.
    /// Displays texture preview with image and metadata.
    /// Matching PyKotor concept: texture preview functionality in texture browser.
    /// </summary>
    public class TexturePreviewDialog : Window
    {
        private Image _textureImage;
        private TextBlock _textureNameLabel;
        private TextBlock _textureInfoLabel;

        /// <summary>
        /// Creates a new texture preview dialog.
        /// </summary>
        /// <param name="parent">Parent window.</param>
        /// <param name="textureName">Name of the texture (ResRef).</param>
        /// <param name="texturePath">File path to the texture.</param>
        public TexturePreviewDialog([CanBeNull] Window parent, string textureName, [CanBeNull] string texturePath)
        {
            Title = $"Texture Preview - {textureName ?? "Unknown"}";
            Width = 600;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (parent != null)
            {
                Owner = parent;
            }

            InitializeComponent(textureName, texturePath);
        }

        private void InitializeComponent(string textureName, [CanBeNull] string texturePath)
        {
            // Create main container
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 10,
                Margin = new Thickness(16)
            };

            // Texture name label
            _textureNameLabel = new TextBlock
            {
                Text = textureName ?? "Unknown Texture",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(_textureNameLabel);

            // Texture image display
            _textureImage = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 512,
                MaxHeight = 512,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(_textureImage);

            // Texture info label
            _textureInfoLabel = new TextBlock
            {
                Text = "Loading texture...",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(_textureInfoLabel);

            // Close button
            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            closeButton.Click += (s, e) => Close();
            mainPanel.Children.Add(closeButton);

            Content = mainPanel;

            // Load texture asynchronously
            LoadTexture(textureName, texturePath);
        }

        /// <summary>
        /// Loads and displays the texture from the file path.
        /// </summary>
        private void LoadTexture([CanBeNull] string textureName, [CanBeNull] string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath) || !File.Exists(texturePath))
            {
                _textureInfoLabel.Text = "Texture file not found or path not available.";
                _textureInfoLabel.Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 100));
                return;
            }

            try
            {
                string extension = Path.GetExtension(texturePath).ToLowerInvariant();
                Bitmap bitmap = null;

                // Load TPC files
                if (extension == ".tpc")
                {
                    byte[] tpcData = File.ReadAllBytes(texturePath);
                    TPC tpc = TPCAuto.ReadTpc(tpcData);

                    if (tpc != null && tpc.Layers != null && tpc.Layers.Count > 0 &&
                        tpc.Layers[0].Mipmaps != null && tpc.Layers[0].Mipmaps.Count > 0)
                    {
                        // Convert TPC mipmap to Avalonia bitmap
                        var mipmap = tpc.Layers[0].Mipmaps[0];
                        bitmap = HolocronToolset.Data.HTInstallation.ConvertTpcMipmapToAvaloniaBitmap(mipmap);

                        if (bitmap != null)
                        {
                            _textureInfoLabel.Text = $"Format: TPC | Size: {mipmap.Width}x{mipmap.Height} | " +
                                                     $"Layers: {tpc.Layers.Count} | Mipmaps: {tpc.Layers[0].Mipmaps.Count}";
                        }
                    }
                }
                // Load standard image formats (PNG, JPG, BMP, TGA)
                else if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" ||
                         extension == ".bmp" || extension == ".tga")
                {
                    using (var fileStream = File.OpenRead(texturePath))
                    {
                        bitmap = new Bitmap(fileStream);
                    }

                    if (bitmap != null)
                    {
                        _textureInfoLabel.Text = $"Format: {extension.ToUpperInvariant().Substring(1)} | " +
                                                 $"Size: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}";
                    }
                }
                else
                {
                    _textureInfoLabel.Text = $"Unsupported texture format: {extension}";
                    _textureInfoLabel.Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 100));
                    return;
                }

                if (bitmap != null)
                {
                    _textureImage.Source = bitmap;
                    _textureInfoLabel.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
                }
                else
                {
                    _textureInfoLabel.Text = "Failed to load texture image.";
                    _textureInfoLabel.Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 100));
                }
            }
            catch (Exception ex)
            {
                _textureInfoLabel.Text = $"Error loading texture: {ex.Message}";
                _textureInfoLabel.Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 100));
            }
        }
    }
}

