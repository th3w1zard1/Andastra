using System;
using System.Collections.Generic;
using System.ComponentModel;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Game.GUI
{
    /// <summary>
    /// ViewModel for GraphicsSettingsDialog that provides data binding support.
    /// </summary>
    public class GraphicsSettingsViewModel : INotifyPropertyChanged
    {
        private GraphicsBackendType _selectedBackend;
        private GraphicsSettingsData _settings;
        private GraphicsPreset _currentPreset;
        private string _searchText;

        /// <summary>
        /// Gets the graphics settings.
        /// </summary>
        public GraphicsSettingsData Settings
        {
            get => _settings;
            set
            {
                if (_settings != value)
                {
                    _settings = value;
                    OnPropertyChanged("Settings");
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected backend.
        /// </summary>
        public GraphicsBackendType SelectedBackend
        {
            get => _selectedBackend;
            set
            {
                if (_selectedBackend != value)
                {
                    _selectedBackend = value;
                    OnPropertyChanged("SelectedBackend");
                    OnPropertyChanged("BackendName");
                }
            }
        }

        /// <summary>
        /// Gets the backend name for display.
        /// </summary>
        public string BackendName => GetBackendName(SelectedBackend);

        /// <summary>
        /// Gets or sets the current preset.
        /// </summary>
        public GraphicsPreset CurrentPreset
        {
            get => _currentPreset;
            set
            {
                if (_currentPreset != value)
                {
                    _currentPreset = value;
                    OnPropertyChanged("CurrentPreset");
                }
            }
        }

        /// <summary>
        /// Gets or sets the search text.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged("SearchText");
                }
            }
        }

        /// <summary>
        /// Gets the list of available presets.
        /// </summary>
        public List<string> Presets { get; } = new List<string> { "Low", "Medium", "High", "Ultra", "Custom" };

        /// <summary>
        /// Gets or sets the selected preset.
        /// </summary>
        public string SelectedPreset
        {
            get => CurrentPreset.ToString();
            set
            {
                if (Enum.TryParse<GraphicsPreset>(value, out var preset))
                {
                    CurrentPreset = preset;
                    OnPropertyChanged("SelectedPreset");
                }
            }
        }

        /// <summary>
        /// Creates a new graphics settings view model.
        /// </summary>
        /// <param name="backendType">The selected graphics backend.</param>
        /// <param name="initialSettings">Initial settings to load (can be null).</param>
        public GraphicsSettingsViewModel(GraphicsBackendType backendType, GraphicsSettingsData initialSettings = null)
        {
            SelectedBackend = backendType;
            Settings = initialSettings ?? new GraphicsSettingsData();
            CurrentPreset = GraphicsPreset.Custom;
        }

        /// <summary>
        /// Gets the backend name for display.
        /// </summary>
        private string GetBackendName(GraphicsBackendType backendType)
        {
            if (backendType == GraphicsBackendType.MonoGame)
            {
                return "MonoGame";
            }
            else if (backendType == GraphicsBackendType.Stride)
            {
                return "Stride";
            }
            return backendType.ToString();
        }

        /// <summary>
        /// Property changed event for data binding.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Loads settings into the view model.
        /// </summary>
        public void LoadSettings()
        {
            // Settings are loaded through data binding
        }

        /// <summary>
        /// Saves settings from the view model.
        /// </summary>
        public void SaveSettings()
        {
            // Settings are saved through data binding
        }

        /// <summary>
        /// Detects the current preset based on settings.
        /// </summary>
        public void DetectCurrentPreset()
        {
            var presets = new[] { GraphicsPreset.Low, GraphicsPreset.Medium, GraphicsPreset.High, GraphicsPreset.Ultra };
            foreach (var preset in presets)
            {
                var presetSettings = GraphicsSettingsPresetFactory.CreatePreset(preset);
                if (SettingsMatch(Settings, presetSettings))
                {
                    CurrentPreset = preset;
                    return;
                }
            }
            CurrentPreset = GraphicsPreset.Custom;
        }

        /// <summary>
        /// Checks if two settings configurations match.
        /// </summary>
        private bool SettingsMatch(GraphicsSettingsData a, GraphicsSettingsData b)
        {
            return a.WindowWidth == b.WindowWidth &&
                   a.WindowHeight == b.WindowHeight &&
                   a.WindowFullscreen == b.WindowFullscreen &&
                   a.WindowVSync == b.WindowVSync &&
                   a.MonoGameSynchronizeWithVerticalRetrace == b.MonoGameSynchronizeWithVerticalRetrace &&
                   a.MonoGamePreferMultiSampling == b.MonoGamePreferMultiSampling &&
                   a.RasterizerMultiSampleAntiAlias == b.RasterizerMultiSampleAntiAlias &&
                   a.SamplerMaxAnisotropy == b.SamplerMaxAnisotropy &&
                   a.SamplerFilter == b.SamplerFilter;
        }
    }
}
