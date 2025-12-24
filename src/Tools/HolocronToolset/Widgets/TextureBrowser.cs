using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace HolocronToolset.Widgets
{
    /// <summary>
    /// Texture browser widget for displaying and selecting imported textures.
    /// Similar to ModelBrowser but for 2D textures.
    /// </summary>
    /// <remarks>
    /// Matching PyKotor implementation concept at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/texture_browser.py
    /// This widget provides a visual browser for imported textures in the LYT editor.
    /// </remarks>
    public class TextureBrowser : UserControl
    {
        private ListBox _textureList;
        private Dictionary<string, string> _textures; // Maps texture name to file path
        private string _selectedTexture;

        /// <summary>
        /// Event fired when a texture is selected.
        /// </summary>
        public event EventHandler<string> TextureSelected;

        /// <summary>
        /// Event fired when the selected texture changes.
        /// </summary>
        public event EventHandler<string> TextureChanged;

        /// <summary>
        /// Gets the currently selected texture name, or null if none selected.
        /// </summary>
        public string SelectedTexture
        {
            get { return _selectedTexture; }
            private set
            {
                if (_selectedTexture != value)
                {
                    _selectedTexture = value;
                    TextureChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Gets all available texture names.
        /// </summary>
        public List<string> GetTextures()
        {
            return new List<string>(_textures.Keys);
        }

        /// <summary>
        /// Gets the file path for a texture by name.
        /// </summary>
        public string GetTexturePath(string textureName)
        {
            return _textures.TryGetValue(textureName, out string path) ? path : null;
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TextureBrowser()
        {
            _textures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Create main container
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 4,
                Margin = new Thickness(8)
            };

            // Create header label
            var headerLabel = new TextBlock
            {
                Text = "Imported Textures",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(headerLabel);

            // Create texture list
            _textureList = new ListBox
            {
                SelectionMode = SelectionMode.Single,
                MinHeight = 200,
                MaxHeight = 400
            };
            _textureList.SelectionChanged += OnTextureSelectionChanged;
            _textureList.DoubleTapped += OnTextureDoubleTapped;

            // Set item template for texture list items
            _textureList.ItemTemplate = new FuncDataTemplate<object>((item, scope) =>
            {
                if (item is string textureName)
                {
                    var panel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Margin = new Thickness(4)
                    };

                    // Texture icon (placeholder - could be enhanced with actual texture preview)
                    var iconBorder = new Border
                    {
                        Width = 48,
                        Height = 48,
                        Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 0, 8, 0)
                    };

                    var iconText = new TextBlock
                    {
                        Text = "TPC",
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                    };
                    iconBorder.Child = iconText;

                    // Texture name
                    var nameText = new TextBlock
                    {
                        Text = textureName,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 12
                    };

                    // File path (if available)
                    string texturePath = GetTexturePath(textureName);
                    if (!string.IsNullOrEmpty(texturePath))
                    {
                        var pathText = new TextBlock
                        {
                            Text = Path.GetFileName(texturePath),
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        panel.Children.Add(iconBorder);
                        panel.Children.Add(nameText);
                        panel.Children.Add(pathText);
                    }
                    else
                    {
                        panel.Children.Add(iconBorder);
                        panel.Children.Add(nameText);
                    }

                    return panel;
                }
                return new TextBlock { Text = item?.ToString() ?? "" };
            });

            mainPanel.Children.Add(_textureList);

            // Create status label
            var statusLabel = new TextBlock
            {
                Name = "statusLabel",
                Text = "No textures imported",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            mainPanel.Children.Add(statusLabel);

            Content = mainPanel;
        }

        /// <summary>
        /// Updates the texture browser with the provided textures.
        /// </summary>
        /// <param name="textures">Dictionary mapping texture names to file paths.</param>
        public void UpdateTextures(Dictionary<string, string> textures)
        {
            if (textures == null)
            {
                _textures.Clear();
            }
            else
            {
                _textures = new Dictionary<string, string>(textures, StringComparer.OrdinalIgnoreCase);
            }

            RefreshTextureList();
        }

        /// <summary>
        /// Adds or updates a texture in the browser.
        /// </summary>
        /// <param name="textureName">The texture name (ResRef).</param>
        /// <param name="filePath">The file path to the texture.</param>
        public void AddTexture(string textureName, string filePath)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return;
            }

            _textures[textureName] = filePath ?? "";
            RefreshTextureList();
        }

        /// <summary>
        /// Removes a texture from the browser.
        /// </summary>
        /// <param name="textureName">The texture name to remove.</param>
        public void RemoveTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return;
            }

            if (_textures.Remove(textureName))
            {
                RefreshTextureList();
                if (SelectedTexture == textureName)
                {
                    SelectedTexture = null;
                }
            }
        }

        /// <summary>
        /// Clears all textures from the browser.
        /// </summary>
        public void ClearTextures()
        {
            _textures.Clear();
            SelectedTexture = null;
            RefreshTextureList();
        }

        /// <summary>
        /// Highlights/selects a specific texture in the browser.
        /// </summary>
        /// <param name="textureName">The texture name to highlight.</param>
        public void HighlightTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return;
            }

            if (_textureList != null && _textures.ContainsKey(textureName))
            {
                var items = _textureList.Items?.Cast<string>().ToList();
                if (items != null)
                {
                    int index = items.IndexOf(textureName);
                    if (index >= 0)
                    {
                        _textureList.SelectedIndex = index;
                        _textureList.ScrollIntoView(index);
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes the texture list display.
        /// </summary>
        private void RefreshTextureList()
        {
            if (_textureList == null)
            {
                return;
            }

            var textureNames = new List<string>(_textures.Keys);
            textureNames.Sort(StringComparer.OrdinalIgnoreCase);

            _textureList.ItemsSource = textureNames;

            // Update status label
            var statusLabel = this.FindControl<TextBlock>("statusLabel");
            if (statusLabel != null)
            {
                if (textureNames.Count == 0)
                {
                    statusLabel.Text = "No textures imported";
                }
                else if (textureNames.Count == 1)
                {
                    statusLabel.Text = "1 texture available";
                }
                else
                {
                    statusLabel.Text = $"{textureNames.Count} textures available";
                }
            }
        }

        /// <summary>
        /// Handles texture selection changes.
        /// </summary>
        private void OnTextureSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_textureList?.SelectedItem is string textureName)
            {
                SelectedTexture = textureName;
                TextureSelected?.Invoke(this, textureName);
            }
            else
            {
                SelectedTexture = null;
            }
        }

        /// <summary>
        /// Handles texture double-tap (for potential actions like preview or use).
        /// </summary>
        private void OnTextureDoubleTapped(object sender, TappedEventArgs e)
        {
            if (_textureList?.SelectedItem is string textureName)
            {
                // Double-tap could trigger texture preview or usage
                // TODO: STUB - For now, just ensure it's selected
                SelectedTexture = textureName;
                TextureSelected?.Invoke(this, textureName);
            }
        }
    }
}
